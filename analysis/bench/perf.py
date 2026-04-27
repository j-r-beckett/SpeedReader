import json
import os
import signal
import subprocess
import time

import pandas as pd

# TMA level-2 metrics that require GP counters.
# 3 is the sweet spot before multiplexing on both P-cores (8 GP) and the
# counter-group constraints perf imposes.  l1_bound and l2_bound can be
# approximated as memory_bound - l3_bound - dram_bound when needed.
_CORE_EVENTS = ["instructions", "cycles"]
_CORE_METRICS = [
    "tma_memory_bound",
    "tma_dram_bound",
    "tma_l3_bound",
]
_SYSTEM_METRICS = [
    "tma_info_system_dram_bw_use",
]

# Metric-unit strings perf emits → column names
_TMA_METRIC_MAP = {
    "tma_memory_bound": "memory_bound_pct",
    "tma_dram_bound": "dram_bound_pct",
    "tma_l3_bound": "l3_bound_pct",
    "tma_info_system_dram_bw_use": "bandwidth_gbps",
}


class PerfMeasurement:
    """
    Per-CPU and system-level perf metrics via ``perf stat --json``.

    Captures per-CPU: instructions, cycles, IPC, memory_bound, l3_bound,
    dram_bound (TMA), pcnt_running (multiplexing indicator).
    Captures system: DRAM bandwidth (GB/s), pcnt_running.

    Usage::

        perf = start_perf()
        # ... workload ...
        cpu_df, sys_df = perf.stop()
    """

    def __init__(self, interval_ms: int = 250):
        self._interval_ms = interval_ms

        events = ",".join(_CORE_EVENTS)
        metrics = ",".join(_CORE_METRICS + _SYSTEM_METRICS)

        self._proc = subprocess.Popen(
            [
                "perf", "stat", "--json",
                "-a", "-A",
                "-e", events,
                "-M", metrics,
                "-I", str(interval_ms),
            ],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
        )
        time.sleep(0.1)

    def stop(self) -> tuple[pd.DataFrame, pd.DataFrame]:
        """Stop measurement and return (cpu_df, system_df)."""
        self._proc.send_signal(signal.SIGINT)
        _, output = self._proc.communicate(timeout=10)
        return _parse(output)


def start_perf(interval_ms: int = 250) -> PerfMeasurement:
    """Start measuring perf metrics. Call .stop() to get results."""
    return PerfMeasurement(interval_ms)


def _parse(output: bytes) -> tuple[pd.DataFrame, pd.DataFrame]:
    # -- first pass: bucket every JSON line ---------------------------------
    # Per-(interval, cpu): raw counters + derived metrics + pcnt tracking
    cpu_samples: dict[tuple[float, int], dict] = {}
    # Per-interval: system-level metrics
    sys_samples: dict[float, dict] = {}
    # Track which CPUs have counted cpu_core vs cpu_atom events to detect
    # hybrid topology.  On non-hybrid Xeons event names lack these prefixes.
    core_cpus: set[int] = set()
    atom_cpus: set[int] = set()

    for line in output.decode(errors="replace").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            obj = json.loads(line)
        except json.JSONDecodeError:
            continue

        interval = obj.get("interval")
        if interval is None:
            continue

        cpu_str = obj.get("cpu")
        event = obj.get("event", "")
        counter_value = obj.get("counter-value", "")
        metric_unit = obj.get("metric-unit", "").strip()
        pcnt = obj.get("pcnt-running")

        # Strip leading "%" and whitespace from metric-unit
        # perf emits e.g. "%  tma_memory_bound"
        mu_clean = metric_unit.lstrip("% ").strip()

        is_counted = counter_value not in ("", "<not counted>")

        # ---- system-level metric (uncore, reported on CPU 0) ----
        if mu_clean == "tma_info_system_dram_bw_use":
            try:
                val = float(obj["metric-value"])
            except (KeyError, ValueError, TypeError):
                continue
            s = sys_samples.setdefault(interval, {
                "bandwidth_gbps": 0.0,
                "min_pcnt_running": 100.0,
            })
            s["bandwidth_gbps"] = val
            if pcnt is not None:
                s["min_pcnt_running"] = min(s["min_pcnt_running"], pcnt)
            continue

        # Everything below needs a CPU id
        if cpu_str is None:
            continue
        cpu = int(cpu_str)

        # ---- hybrid topology detection ----
        if is_counted:
            if "cpu_core/" in event:
                core_cpus.add(cpu)
            elif "cpu_atom/" in event:
                atom_cpus.add(cpu)

        # ---- per-CPU derived TMA metric ----
        if mu_clean in _TMA_METRIC_MAP:
            col = _TMA_METRIC_MAP[mu_clean]
            try:
                val = float(obj["metric-value"])
            except (KeyError, ValueError, TypeError):
                continue
            key = (interval, cpu)
            sample = cpu_samples.setdefault(key, _empty_cpu_sample())
            sample[col] = val
            if pcnt is not None:
                sample["min_pcnt_running"] = min(sample["min_pcnt_running"], pcnt)
            continue

        # ---- per-CPU raw event (instructions, cycles) ----
        if not is_counted:
            continue

        try:
            value = float(counter_value)
        except ValueError:
            continue

        event_lower = event.lower()
        key = (interval, cpu)
        sample = cpu_samples.setdefault(key, _empty_cpu_sample())

        if "instructions" in event_lower:
            sample["instructions"] += value
        elif "cycles" in event_lower and "unhalted" not in event_lower:
            sample["cycles"] += value

        if pcnt is not None:
            sample["min_pcnt_running"] = min(sample["min_pcnt_running"], pcnt)

    # -- second pass: build DataFrames --------------------------------------
    is_hybrid = bool(core_cpus) and bool(atom_cpus)

    cpu_rows = []
    for (interval, cpu), s in sorted(cpu_samples.items()):
        ipc = s["instructions"] / s["cycles"] if s["cycles"] > 0 else 0.0

        if is_hybrid:
            if cpu in core_cpus:
                core_type = "P"
            elif cpu in atom_cpus:
                core_type = "E"
            else:
                core_type = "?"
        else:
            core_type = None

        row: dict = {
            "interval": interval,
            "cpu": cpu,
            "instructions": s["instructions"],
            "cycles": s["cycles"],
            "ipc": ipc,
            "memory_bound_pct": s["memory_bound_pct"],
        }

        # Level-2 TMA: available on P-cores (and E-cores with multiplexing).
        # NaN when no data was reported for this (interval, cpu).
        row["l3_bound_pct"] = s["l3_bound_pct"]
        row["dram_bound_pct"] = s["dram_bound_pct"]

        if is_hybrid:
            row["core_type"] = core_type

        row["pcnt_running"] = s["min_pcnt_running"]
        cpu_rows.append(row)

    sys_rows = []
    for interval, s in sorted(sys_samples.items()):
        sys_rows.append({
            "interval": interval,
            "bandwidth_gbps": s["bandwidth_gbps"],
            "pcnt_running": s["min_pcnt_running"],
        })

    cpu_df = pd.DataFrame(cpu_rows)
    sys_df = pd.DataFrame(sys_rows)

    return cpu_df, sys_df


def _empty_cpu_sample() -> dict:
    return {
        "instructions": 0.0,
        "cycles": 0.0,
        "memory_bound_pct": float("nan"),
        "l3_bound_pct": float("nan"),
        "dram_bound_pct": float("nan"),
        "min_pcnt_running": 100.0,
    }
