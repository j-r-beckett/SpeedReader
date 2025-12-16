# /// script
# requires-python = ">=3.11"
# dependencies = ["psutil"]
# ///
"""
Watchdog for auto-shutdown of SpeedReader server.
"""

import sys
import time
from pathlib import Path

import psutil

SCRIPT_DIR = Path(__file__).parent.resolve()
PIDFILE_DIR = SCRIPT_DIR.parent / "output"


def read_pidfile(name: str) -> int | None:
    pidfile = PIDFILE_DIR / f"{name}.pid"
    if pidfile.exists():
        try:
            return int(pidfile.read_text().strip())
        except (ValueError, OSError):
            pass
    return None


def cleanup_pidfile(name: str):
    pidfile = PIDFILE_DIR / f"{name}.pid"
    pidfile.unlink(missing_ok=True)


def kill_if_matches(name: str, expected_pid: int):
    """Kill process only if current pidfile matches expected PID."""
    current_pid = read_pidfile(name)
    if current_pid == expected_pid:
        try:
            proc = psutil.Process(expected_pid)
            proc.terminate()
            proc.wait(timeout=5)
        except (psutil.NoSuchProcess, psutil.TimeoutExpired):
            pass
        cleanup_pidfile(name)


def main():
    if len(sys.argv) < 3:
        print("Usage: watchdog.py <timeout_seconds> <speedreader_pid>", file=sys.stderr)
        sys.exit(1)

    timeout = int(sys.argv[1])
    speedreader_pid = int(sys.argv[2])

    time.sleep(timeout)

    kill_if_matches("speedreader", speedreader_pid)
    cleanup_pidfile("watchdog")


if __name__ == "__main__":
    main()
