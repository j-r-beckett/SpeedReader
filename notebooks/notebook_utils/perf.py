import os
import signal
import subprocess
import tempfile
import time
from collections import defaultdict
from datetime import datetime, timedelta, timezone

import pandas as pd


class PerfMeasurement:
    """
    Measures system-wide performance metrics using perf stat with TMA metrics.

    Captures:
    - DRAM bandwidth (GB/s)
    - Memory bound percentage (P+E cores)
    - L1/L2/L3/DRAM bound percentages (P-core only)
    - IPC (instructions per cycle, P+E cores)

    Usage:
        perf = start_perf()
        # ... run workload ...
        df = perf.stop()  # Returns DataFrame with metrics
    """

    # Raw events for IPC calculation (works on both P and E cores)
    EVENTS = [
        "instructions",
        "cycles",
    ]

    # TMA metrics for memory analysis
    METRICS = [
        "tma_info_system_dram_bw_use",
        "tma_memory_bound",
        "tma_l1_bound",
        "tma_l2_bound",
        "tma_l3_bound",
        "tma_dram_bound",
    ]

    def __init__(self, interval_ms: int = 250):
        self._output_file = tempfile.mktemp(suffix=".csv")
        self._start_time = datetime.now(timezone.utc)
        self._interval_ms = interval_ms

        events = ",".join(self.EVENTS)
        metrics = ",".join(self.METRICS)

        self._proc = subprocess.Popen(
            [
                "perf", "stat", "-a",
                "-e", events,
                "-M", metrics,
                "-I", str(interval_ms),
                "-x", ",",
                "-o", self._output_file,
            ],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        time.sleep(0.1)  # Let perf initialize

    def stop(self) -> pd.DataFrame:
        """Stop measurement and return DataFrame with derived metrics."""
        self._proc.send_signal(signal.SIGINT)
        self._proc.wait(timeout=5)
        time.sleep(0.2)  # Let perf flush output

        df = self._parse_output()
        os.unlink(self._output_file)
        return df

    def _parse_output(self) -> pd.DataFrame:
        # Collect raw values and derived metrics grouped by timestamp
        samples = defaultdict(lambda: {
            "instructions": 0.0,
            "cycles": 0.0,
            "bandwidth_gbps": 0.0,
            "memory_bound_pct": 0.0,
            "l1_bound_pct": 0.0,
            "l2_bound_pct": 0.0,
            "l3_bound_pct": 0.0,
            "dram_bound_pct": 0.0,
        })

        with open(self._output_file) as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue

                parts = line.split(",")
                if len(parts) < 4:
                    continue

                try:
                    relative_secs = float(parts[0])
                except ValueError:
                    continue

                s = samples[relative_secs]

                # Parse raw event value
                value_str = parts[1].strip()
                if value_str and not value_str.startswith("<"):
                    try:
                        value = float(value_str)
                    except ValueError:
                        value = 0.0
                else:
                    value = 0.0

                # Get event name from parts[3] (e.g., "cpu_core/instructions/")
                event_name = parts[3].strip() if len(parts) > 3 else ""

                # Sum instructions and cycles from both core types
                if "instructions" in event_name.lower():
                    s["instructions"] += value
                elif "cycles" in event_name.lower() and "unhalted" not in event_name.lower():
                    s["cycles"] += value

                # Check for TMA derived metrics (appear at end of line)
                # Format: ...,derived_value,%  tma_metric_name  (for percentages)
                # Format: ...,derived_value,tma_metric_name     (for bandwidth)
                for i in range(len(parts) - 1, 3, -1):
                    part = parts[i].strip()
                    # Strip leading "%" and whitespace from metric name
                    if part.startswith("%"):
                        part = part[1:].strip()

                    if part.startswith("tma_"):
                        try:
                            derived_value = float(parts[i - 1].strip())
                        except (ValueError, IndexError):
                            break

                        if part == "tma_info_system_dram_bw_use":
                            s["bandwidth_gbps"] = derived_value
                        elif part == "tma_memory_bound":
                            # Sum memory_bound from P and E cores
                            s["memory_bound_pct"] += derived_value
                        elif part == "tma_l1_bound":
                            s["l1_bound_pct"] += derived_value
                        elif part == "tma_l2_bound":
                            s["l2_bound_pct"] += derived_value
                        elif part == "tma_l3_bound":
                            s["l3_bound_pct"] += derived_value
                        elif part == "tma_dram_bound":
                            s["dram_bound_pct"] += derived_value
                        break

        # Build output DataFrame
        rows = []
        for relative_secs in sorted(samples.keys()):
            s = samples[relative_secs]
            absolute_time = self._start_time + timedelta(seconds=relative_secs)

            ipc = s["instructions"] / s["cycles"] if s["cycles"] > 0 else 0.0

            rows.append({
                "timestamp": absolute_time,
                "bandwidth_gbps": s["bandwidth_gbps"],
                "memory_bound_pct": s["memory_bound_pct"],
                "l1_bound_pct": s["l1_bound_pct"],
                "l2_bound_pct": s["l2_bound_pct"],
                "l3_bound_pct": s["l3_bound_pct"],
                "dram_bound_pct": s["dram_bound_pct"],
                "ipc": ipc,
            })

        return pd.DataFrame(rows)


def start_perf(interval_ms: int = 250) -> PerfMeasurement:
    """Start measuring performance metrics. Call .stop() on the returned object to get results."""
    return PerfMeasurement(interval_ms)


# Keep old name for backwards compatibility
PerfBandwidthMeasurement = PerfMeasurement
start_perf_bandwidth = start_perf
