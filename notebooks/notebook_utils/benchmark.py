import os
import select
import subprocess
from datetime import datetime


def run_benchmark(
    cmd: list[str],
    duration: float,
    warmup: float,
):
    """
    Run a benchmark command and yield results as they stream in.

    The command must output lines in format: start_timestamp,end_timestamp
    where timestamps are ISO format (e.g., 2024-01-01T12:00:00.000000Z).

    Args:
        cmd: Command to run (e.g., ["dotnet", "run", "script.cs", "--", "-m", "dbnet"])
        duration: Benchmark duration in seconds (passed as -d)
        warmup: Warmup duration in seconds (passed as -w)

    Yields:
        (start_time, end_time) datetime pairs as they arrive.
    """
    full_cmd = [*cmd, "-d", str(duration), "-w", str(warmup)]

    proc = subprocess.Popen(
        full_cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )

    # Make stdout non-blocking
    fd = proc.stdout.fileno()
    os.set_blocking(fd, False)

    buffer = ""

    while proc.poll() is None:
        ready, _, _ = select.select([proc.stdout], [], [], 0.1)

        if ready:
            chunk = proc.stdout.read()
            if chunk:
                buffer += chunk
                while "\n" in buffer:
                    line, buffer = buffer.split("\n", 1)
                    line = line.strip()
                    if line:
                        start_ts, end_ts = line.split(",")
                        yield (
                            datetime.fromisoformat(start_ts.replace("Z", "+00:00")),
                            datetime.fromisoformat(end_ts.replace("Z", "+00:00")),
                        )

    # Read any remaining output
    leftover = proc.stdout.read()
    if leftover:
        buffer += leftover
    for line in buffer.strip().split("\n"):
        line = line.strip()
        if line:
            start_ts, end_ts = line.split(",")
            yield (
                datetime.fromisoformat(start_ts.replace("Z", "+00:00")),
                datetime.fromisoformat(end_ts.replace("Z", "+00:00")),
            )

    stderr_output = proc.stderr.read()

    if proc.returncode != 0:
        raise RuntimeError(f"Benchmark failed:\n{stderr_output}")
