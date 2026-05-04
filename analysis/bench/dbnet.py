import ctypes
import subprocess
import threading
import time
from pathlib import Path

import marimo as mo
import pandas as pd

from bench.perf import PerfCounters

_REPO_ROOT = Path(__file__).parent.parent.parent.resolve()
_BENCHLIB_DIR = _REPO_ROOT / "src" / "BenchLib"
_PUBLISH_DIR = _BENCHLIB_DIR / "bin" / "Release" / "net10.0" / "linux-x64" / "publish"
_LIB_PATH = _PUBLISH_DIR / "BenchLib.so"


def _format_duration(seconds: float) -> str:
    if seconds < 60:
        return f"{seconds:.0f}s"
    mins = int(seconds // 60)
    secs = int(seconds % 60)
    return f"{mins}m {secs}s"


def build() -> None:
    with mo.status.spinner(title="Building BenchLib...", remove_on_exit=True):
        result = subprocess.run(
            [
                "dotnet", "publish", str(_BENCHLIB_DIR),
                "-p:NativeLib=Shared",
                "-p:OnnxLinkMode=Dynamic",
                "--use-current-runtime",
            ],
            cwd=_REPO_ROOT,
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            raise RuntimeError(f"Build failed:\n{result.stderr}")


def run_dbnet(
    configs: list[list[int]],
    duration: float,
    trim: float,
) -> pd.DataFrame:
    lib = ctypes.CDLL(str(_LIB_PATH))
    lib.benchlib_init.argtypes = []
    lib.benchlib_init.restype = None
    lib.benchlib_rundbnet.argtypes = [ctypes.c_int]
    lib.benchlib_rundbnet.restype = None
    lib.benchlib_destroy.argtypes = []
    lib.benchlib_destroy.restype = None

    lib.benchlib_init()
    perf = PerfCounters()

    try:
        all_rows = _run_with_spinner(lib, configs, duration, perf)
    finally:
        perf.close()
        lib.benchlib_destroy()

    df = pd.DataFrame(all_rows)
    return _trim(df, trim)


def _run_with_spinner(
    lib: ctypes.CDLL,
    configs: list[list[int]],
    duration: float,
    perf: PerfCounters,
) -> list[dict]:
    total_time = len(configs) * duration * 1.05
    start_wall = time.monotonic()
    all_rows: list[dict] = []

    with mo.status.spinner(title="Running benchmark...", remove_on_exit=True) as spinner:
        spinner.update(subtitle=f"0s / {_format_duration(total_time)}")

        for cores in configs:
            parallelism = len(cores)
            thread_results: list[list] = [[] for _ in cores]

            def worker(results: list, core_id: int):
                deadline = time.monotonic() + duration
                snap = perf.read_cpu(core_id)
                while time.monotonic() < deadline:
                    start = time.monotonic()
                    lib.benchlib_rundbnet(core_id)
                    end = time.monotonic()
                    new_snap = perf.read_cpu(core_id)
                    results.append((core_id, start, end, snap, new_snap))
                    snap = new_snap

            threads = []
            for i, core_id in enumerate(cores):
                t = threading.Thread(target=worker, args=(thread_results[i], core_id))
                threads.append(t)

            bw_before = perf.read_uncore()
            bw_start = time.monotonic()

            for t in threads:
                t.start()

            while any(t.is_alive() for t in threads):
                time.sleep(0.5)
                elapsed = time.monotonic() - start_wall
                spinner.update(
                    subtitle=f"{_format_duration(elapsed)} / {_format_duration(total_time)}"
                )

            for t in threads:
                t.join()

            bw_end = time.monotonic()
            bw_after = perf.read_uncore()
            bandwidth = (
                perf.compute_bandwidth(bw_before, bw_after, bw_end - bw_start)
                if bw_before and bw_after
                else None
            )

            for thread_res in thread_results:
                for core_id, start, end, snap_before, snap_after in thread_res:
                    row: dict = {
                        "core_id": core_id,
                        "parallelism": parallelism,
                        "start_mono": start,
                        "end_mono": end,
                        "duration_ms": (end - start) * 1000,
                        **perf.compute_cpu_metrics(core_id, snap_before, snap_after),
                    }
                    if perf.is_hybrid:
                        row["core_type"] = perf.core_type(core_id)
                    if bandwidth is not None:
                        row["bandwidth_gbps"] = bandwidth
                    all_rows.append(row)

    return all_rows


def _trim(df: pd.DataFrame, trim: float) -> pd.DataFrame:
    trimmed = []
    for _, g in df.groupby("parallelism"):
        run_start = g["start_mono"].min()
        run_end = g["end_mono"].max()
        midpoints = g["start_mono"] + (g["end_mono"] - g["start_mono"]) / 2
        mask = (midpoints >= run_start + trim) & (midpoints <= run_end - trim)
        trimmed.append(g[mask])
    return pd.concat(trimmed, ignore_index=True)
