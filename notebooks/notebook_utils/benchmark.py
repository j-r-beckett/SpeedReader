import json
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

    The command must output NDJSON lines with format:
        {"start": "ISO timestamp", "end": "ISO timestamp", "tags": {...}}

    Args:
        cmd: Command to run (e.g., ["dotnet", "run", "script.cs", "--", "-m", "dbnet"])
        duration: Benchmark duration in seconds (passed as -d)
        warmup: Warmup duration in seconds (passed as -w)

    Yields:
        (start_time, end_time, tags) tuples as they arrive.
    """
    full_cmd = [*cmd, "--duration", str(duration), "--warmup", str(warmup)]

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
    non_json_output = []

    def parse_line(line: str):
        if not line.startswith("{"):
            non_json_output.append(line)
            return None
        obj = json.loads(line)
        return (
            datetime.fromisoformat(obj["start"].replace("Z", "+00:00")),
            datetime.fromisoformat(obj["end"].replace("Z", "+00:00")),
            obj.get("tags", {}),
        )

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
                        result = parse_line(line)
                        if result:
                            yield result

    # Read any remaining output
    leftover = proc.stdout.read()
    if leftover:
        buffer += leftover
    for line in buffer.strip().split("\n"):
        line = line.strip()
        if line:
            result = parse_line(line)
            if result:
                yield result

    stderr_output = proc.stderr.read()

    if proc.returncode != 0:
        stdout_output = "\n".join(non_json_output)
        raise RuntimeError(
            f"Benchmark failed (exit code {proc.returncode}):\n"
            f"stdout:\n{stdout_output}\n"
            f"stderr:\n{stderr_output}"
        )
