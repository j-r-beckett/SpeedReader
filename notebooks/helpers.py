# /// script
# requires-python = ">=3.11"
# dependencies = ["marimo", "pandas"]
# ///

import marimo as mo
import os
import select
import signal
import subprocess
import tempfile
import time
from datetime import datetime, timedelta, timezone
from itertools import product
from pathlib import Path

import pandas as pd


def format_duration(seconds: float) -> str:
    """Format duration in human-readable form"""
    if seconds < 60:
        return f"{seconds:.0f}s"
    elif seconds < 3600:
        mins = int(seconds // 60)
        secs = int(seconds % 60)
        return f"{mins}m {secs}s"
    else:
        hours = int(seconds // 3600)
        mins = int((seconds % 3600) // 60)
        return f"{hours}h {mins}m"


def get_physical_p_cores() -> int | None:
    """
    Get the number of physical performance cores.

    Returns None if detection fails or platform doesn't have hybrid architecture.
    """
    # Intel hybrid CPUs expose P-cores via sysfs
    try:
        cpus = Path("/sys/devices/cpu_core/cpus").read_text().strip()
        # Parse range like "0-15" to count
        if "-" in cpus:
            start, end = cpus.split("-")
            # These are logical CPUs, divide by 2 for physical cores (hyperthreading)
            return (int(end) - int(start) + 1) // 2
        return len(cpus.split(",")) // 2
    except FileNotFoundError:
        return None


def get_hardware_summary() -> str:
    """Get hardware summary using inxi."""
    import shutil

    if shutil.which("inxi"):
        result = subprocess.run(
            ["inxi", "-C", "-M", "-c0"],  # -c0 disables color codes
            capture_output=True, text=True
        )
        if result.returncode == 0:
            return result.stdout.strip()

    return "inxi not available"


class PerfBandwidthMeasurement:
    """
    Measures memory bandwidth using perf stat.

    Usage:
        perf = start_perf_bandwidth()
        # ... run workload ...
        df = perf.stop()  # Returns DataFrame with timestamp, bandwidth_pct
    """

    def __init__(self):
        self._output_file = tempfile.mktemp(suffix=".csv")
        self._start_time = datetime.now(timezone.utc)
        self._proc = subprocess.Popen(
            [
                "perf", "stat", "-a",
                "-M", "tma_info_system_dram_bw_use",
                "-I", "250",
                "-x", ",",
                "-o", self._output_file,
            ],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        time.sleep(0.1)  # Let perf initialize

    def stop(self) -> pd.DataFrame:
        """Stop measurement and return DataFrame with timestamp + bandwidth_pct."""
        self._proc.send_signal(signal.SIGINT)
        self._proc.wait(timeout=5)
        time.sleep(0.2)  # Let perf flush output

        df = self._parse_output()
        os.unlink(self._output_file)
        return df

    def _parse_output(self) -> pd.DataFrame:
        rows = []
        with open(self._output_file) as f:
            for line in f:
                if "tma_info_system_dram_bw_use" in line:
                    parts = line.strip().split(",")
                    relative_secs = float(parts[0])
                    bandwidth_gbps = float(parts[6])
                    absolute_time = self._start_time + timedelta(seconds=relative_secs)
                    rows.append({
                        "timestamp": absolute_time,
                        "bandwidth_gbps": bandwidth_gbps,
                    })
        return pd.DataFrame(rows)


def start_perf_bandwidth() -> PerfBandwidthMeasurement:
    """Start measuring memory bandwidth. Call .stop() on the returned object to get results."""
    return PerfBandwidthMeasurement()


def build_inference_benchmark() -> Path:
    """Build the MicroBenchmarks project and return the project path."""
    project_path = Path(__file__).parent.parent / "src" / "MicroBenchmarks"

    with mo.status.spinner(title="Building benchmark..."):
        build_result = subprocess.run(
            ["dotnet", "build", str(project_path), "-c", "Release", "--verbosity", "quiet"],
            capture_output=True,
            text=True,
        )
    if build_result.returncode != 0:
        raise RuntimeError(f"Build failed:\n{build_result.stdout}\n{build_result.stderr}")

    return project_path


def _run_inference_benchmark(
    project_path: Path,
    model: str,
    duration_seconds: float,
    batch_size: int,
    intra_threads: int,
    inter_threads: int,
    cores: list[int],
    warmup_seconds: float,
    on_tick,
) -> tuple[list[tuple[str, str]], str]:
    """
    Internal function to run inference benchmark.

    Returns (results, stderr) where results is list of (start_time, end_time) ISO timestamp pairs.
    on_tick() is called periodically (~10Hz) during execution for progress updates.
    """
    cmd = [
        "dotnet", "run",
        "--project", str(project_path),
        "--no-build", "-c", "Release",
        "--",
        "inference",
        "-m", model,
        "-d", str(duration_seconds),
        "-b", str(batch_size),
        "--intra-threads", str(intra_threads),
        "--inter-threads", str(inter_threads),
        "-c", *[str(c) for c in cores],
        "-w", str(warmup_seconds),
    ]

    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )

    # Make stdout non-blocking
    fd = proc.stdout.fileno()
    os.set_blocking(fd, False)

    results = []
    buffer = ""

    while proc.poll() is None:
        # Wait for data with timeout for periodic progress updates
        ready, _, _ = select.select([proc.stdout], [], [], 0.1)
        on_tick()

        if ready:
            chunk = proc.stdout.read()
            if chunk:
                buffer += chunk
                while "\n" in buffer:
                    line, buffer = buffer.split("\n", 1)
                    line = line.strip()
                    if line:
                        start_ts, end_ts = line.split(",")
                        results.append((start_ts, end_ts))

    # Read any remaining output
    remaining = proc.stdout.read()
    if remaining:
        buffer += remaining
    for line in buffer.strip().split("\n"):
        line = line.strip()
        if line:
            start_ts, end_ts = line.split(",")
            results.append((start_ts, end_ts))

    stderr_output = proc.stderr.read()

    if proc.returncode != 0:
        raise RuntimeError(f"Benchmark failed:\n{stderr_output}")

    return results, stderr_output


def run_benchmark_sweep(
    project_path: Path,
    model: str | list[str],
    duration_seconds: float = 10.0,
    batch_size: int | list[int] = 1,
    intra_threads: int | list[int] = 1,
    inter_threads: int | list[int] = 1,
    cores: list[list[int]] = [[0]],
    warmup_seconds: float = 2.0,
) -> pd.DataFrame:
    """
    Run inference benchmark over all combinations of parameters.

    Parameters that accept lists will be swept over (cartesian product).
    cores is a list of core configurations to sweep over (e.g., [[0], [0,1], [0,1,2]]).
    Returns a DataFrame with columns for each config parameter plus start_time and end_time.
    Writes diagnostic logs to a timestamped file in /tmp.
    """
    def to_list(x):
        return x if isinstance(x, list) else [x]

    configs = [
        {"model": m, "batch_size": b, "intra_threads": intra, "inter_threads": inter, "cores": c}
        for m, b, intra, inter, c in product(
            to_list(model), to_list(batch_size), to_list(intra_threads),
            to_list(inter_threads), cores
        )
    ]

    rows = []
    all_stderr = []
    estimated_total = len(configs) * (warmup_seconds + duration_seconds + 1)
    start_time = time.time()

    with mo.status.spinner(title="Running benchmark...", remove_on_exit=True) as spinner:
        for cfg in configs:
            def on_tick():
                elapsed = time.time() - start_time
                remaining = max(0, estimated_total - elapsed)
                pct = min(99, int((elapsed / estimated_total) * 100))
                spinner.update(
                    title=f"Running benchmark... {pct}%",
                    subtitle=f"{format_duration(remaining)} remaining",
                )

            results, stderr = _run_inference_benchmark(
                project_path=project_path,
                model=cfg["model"],
                duration_seconds=duration_seconds,
                batch_size=cfg["batch_size"],
                intra_threads=cfg["intra_threads"],
                inter_threads=cfg["inter_threads"],
                cores=cfg["cores"],
                warmup_seconds=warmup_seconds,
                on_tick=on_tick,
            )

            all_stderr.append(f"=== Config: cores={cfg['cores']} ===\n{stderr}\n")

            for start_str, end_str in results:
                rows.append({
                    **cfg,
                    "start_time": datetime.fromisoformat(start_str.replace("Z", "+00:00")),
                    "end_time": datetime.fromisoformat(end_str.replace("Z", "+00:00")),
                })

    # Write stderr to log file
    log_path = Path(f"/tmp/benchmark_log_{int(time.time())}.txt")
    log_path.write_text("".join(all_stderr))
    print(f"Diagnostic log: {log_path}", file=__import__('sys').stderr)

    return pd.DataFrame(rows)
