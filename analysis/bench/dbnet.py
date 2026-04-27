import ctypes
import subprocess
import threading
import time
from pathlib import Path

import marimo as mo
import pandas as pd

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

    try:
        all_rows = _run_with_spinner(lib, configs, duration)
    finally:
        lib.benchlib_destroy()

    df = pd.DataFrame(all_rows)
    return _trim(df, trim)


def _run_with_spinner(
    lib: ctypes.CDLL,
    configs: list[list[int]],
    duration: float,
) -> list[dict]:
    total_time = len(configs) * duration * 1.05
    start_wall = time.monotonic()
    all_rows: list[dict] = []

    with mo.status.spinner(title="Running benchmark...", remove_on_exit=True) as spinner:
        spinner.update(subtitle=f"0s / {_format_duration(total_time)}")

        for cores in configs:
            parallelism = len(cores)
            thread_results: list[list[tuple[int, float, float]]] = [[] for _ in cores]

            def worker(results: list, core_id: int):
                deadline = time.monotonic() + duration
                while time.monotonic() < deadline:
                    start = time.monotonic()
                    lib.benchlib_rundbnet(core_id)
                    end = time.monotonic()
                    results.append((core_id, start, end))

            threads = []
            for i, core_id in enumerate(cores):
                t = threading.Thread(target=worker, args=(thread_results[i], core_id))
                threads.append(t)

            for t in threads:
                t.start()

            # Poll with short joins so main thread can update spinner
            while any(t.is_alive() for t in threads):
                time.sleep(0.5)
                elapsed = time.monotonic() - start_wall
                spinner.update(
                    subtitle=f"{_format_duration(elapsed)} / {_format_duration(total_time)}"
                )

            for t in threads:
                t.join()

            for thread_res in thread_results:
                for core_id, start, end in thread_res:
                    all_rows.append({
                        "core_id": core_id,
                        "parallelism": parallelism,
                        "start_mono": start,
                        "end_mono": end,
                        "duration_ms": (end - start) * 1000,
                    })

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
