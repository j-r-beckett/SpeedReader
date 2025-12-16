# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "psutil"]
# ///
"""
Reload SpeedReader server for web development/testing.
Handles build, process management, and auto-shutdown.
"""

import os
import subprocess
import sys
import time
from pathlib import Path

import click
import psutil

SCRIPT_DIR = Path(__file__).parent.resolve()
PROJECT_ROOT = SCRIPT_DIR.parent.parent.parent.parent
SRC_DIR = PROJECT_ROOT / "src"
FRONTEND_DIR = SRC_DIR / "Frontend"
OUTPUT_DIR = SCRIPT_DIR.parent / "output"
PIDFILE_DIR = OUTPUT_DIR

PORT = 5050  # Fixed port to avoid conflicts with regular development on 5000
HEALTH_POLL_INTERVAL_MS = 100
HEALTH_TIMEOUT_S = 5
SHUTDOWN_S = 900  # 15 minutes


def info(msg: str):
    print(f"[info] {msg}", file=sys.stderr)


def error(msg: str):
    print(f"[error] {msg}", file=sys.stderr)


class ScriptError(Exception):
    pass


def kill_process_on_port(port: int) -> bool:
    """Kill any process listening on the specified port."""
    killed = False
    for conn in psutil.net_connections(kind="inet"):
        if conn.laddr.port == port and conn.status == "LISTEN":
            try:
                proc = psutil.Process(conn.pid)
                info(f"Killing process {proc.name()} (PID {conn.pid}) on port {port}")
                proc.terminate()
                proc.wait(timeout=5)
                killed = True
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.TimeoutExpired):
                try:
                    proc.kill()
                    killed = True
                except Exception:
                    pass
    return killed


def wait_for_health(port: int, timeout_s: float, process: subprocess.Popen = None) -> bool:
    """Poll health endpoint until healthy, process dies, or timeout."""
    import urllib.request
    import urllib.error

    url = f"http://localhost:{port}/api/health"
    start = time.time()
    poll_interval = HEALTH_POLL_INTERVAL_MS / 1000.0

    while time.time() - start < timeout_s:
        if process and process.poll() is not None:
            return False
        try:
            with urllib.request.urlopen(url, timeout=1) as resp:
                if resp.status == 200:
                    return True
        except (urllib.error.URLError, urllib.error.HTTPError, TimeoutError, ConnectionRefusedError):
            pass
        time.sleep(poll_interval)

    return False


def write_pidfile(name: str, pid: int):
    """Write a PID file for later cleanup."""
    pidfile = PIDFILE_DIR / f"{name}.pid"
    pidfile.write_text(str(pid))


def read_pidfile(name: str) -> int | None:
    """Read a PID from pidfile."""
    pidfile = PIDFILE_DIR / f"{name}.pid"
    if pidfile.exists():
        try:
            return int(pidfile.read_text().strip())
        except (ValueError, OSError):
            pass
    return None


def cleanup_pidfile(name: str):
    """Remove a pidfile."""
    pidfile = PIDFILE_DIR / f"{name}.pid"
    pidfile.unlink(missing_ok=True)


def kill_from_pidfile(name: str) -> bool:
    """Kill process from pidfile if running."""
    pid = read_pidfile(name)
    if pid:
        try:
            proc = psutil.Process(pid)
            proc.terminate()
            proc.wait(timeout=5)
            cleanup_pidfile(name)
            return True
        except (psutil.NoSuchProcess, psutil.TimeoutExpired):
            cleanup_pidfile(name)
    return False


@click.command()
def main():
    """
    Build and reload SpeedReader server.

    Handles the full lifecycle: build, kill previous instances, start server,
    set up auto-shutdown watchdog. Use Playwright MCP for browser interaction.
    """
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # Kill any previous processes
    kill_from_pidfile("watchdog")
    kill_from_pidfile("speedreader")
    kill_process_on_port(PORT)

    # Build
    info("Building SpeedReader...")
    result = subprocess.run(
        ["dotnet", "build", str(FRONTEND_DIR)],
        cwd=str(PROJECT_ROOT),
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        raise ScriptError(f"Build failed:\n{result.stdout}\n{result.stderr}")

    # Start server
    info("Starting SpeedReader server...")
    env = os.environ.copy()
    env["ASPNETCORE_URLS"] = f"http://localhost:{PORT}"

    server_proc = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--project", str(FRONTEND_DIR), "--", "--serve"],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        env=env,
        cwd=str(PROJECT_ROOT),
        start_new_session=True,
    )

    try:
        # Wait for health
        info("Waiting for server health...")
        if not wait_for_health(PORT, HEALTH_TIMEOUT_S, server_proc):
            if server_proc.poll() is not None:
                output = server_proc.stdout.read().decode() if server_proc.stdout else ""
                raise ScriptError(f"Server process died. Output:\n{output}")
            raise ScriptError(f"Server did not become healthy within {HEALTH_TIMEOUT_S}s")

        info("Server is healthy")

        # Write pidfile
        write_pidfile("speedreader", server_proc.pid)

        # Spawn watchdog for auto-shutdown
        watchdog_script = SCRIPT_DIR / "watchdog.py"
        watchdog_proc = subprocess.Popen(
            ["uv", "run", str(watchdog_script), str(SHUTDOWN_S), str(server_proc.pid)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            start_new_session=True,
        )
        write_pidfile("watchdog", watchdog_proc.pid)

        # Output
        print(f"\nSpeedReader running at http://localhost:{PORT}")
        print(f"Auto-shutdown in {SHUTDOWN_S // 60} minutes")

        # Detach
        server_proc = None

    finally:
        if server_proc:
            server_proc.terminate()
            try:
                server_proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                server_proc.kill()


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        sys.exit(1)
