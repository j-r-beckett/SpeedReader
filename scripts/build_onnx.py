#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.13"
# dependencies = [
#   "click",
#   "rich",
#   "psutil"
# ]
# ///

import subprocess
import time
import click
import psutil
from pathlib import Path
from rich.console import Console

console = Console()


class ScriptError(Exception):
    pass


def bash(command: str, directory: str | Path = None) -> str:
    """
    Execute a bash command in a specified directory, streaming output in real-time.

    Args:
        command: The bash command to execute
        directory: The directory in which to execute the command (defaults to script's directory)

    Returns:
        The combined stdout/stderr output as a string

    Raises:
        subprocess.CalledProcessError: If the command exits with a non-zero status code
    """
    # If directory is empty, use the script's directory
    if directory is None:
        directory = Path(__file__).parent

    # Convert directory to Path object if it's a string
    dir_path = Path(directory)

    # Print the command being executed in yellow
    console.print(command, style="yellow", highlight=False)

    # Execute the command using bash with streaming output
    process = subprocess.Popen(
        ["bash", "-c", command],
        cwd=dir_path,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,  # Merge stderr into stdout
        text=True,
        bufsize=1,  # Line buffered
    )

    # Capture output while streaming it in real-time
    output_lines = []
    for line in process.stdout:
        output_lines.append(line)
        console.print(line, style="bright_black", end="", highlight=False)

    # Wait for process to complete and get return code
    returnCode = process.wait()

    # Raise exception if command failed
    if returnCode == 127:
        raise ScriptError(
            f"Command {command.split()[0]} not found. Are you missing a dependency?"
        )

    if returnCode != 0:
        raise ScriptError(f"Command {command} returned {returnCode}")

    # Return the captured output
    return "".join(output_lines)


def info(msg):
    console.print(msg, style="green", highlight=False)


def error(msg):
    console.print(msg, style="red", highlight=False)


def get_parallel_jobs() -> int:
    """Calculate safe number of parallel jobs based on available memory (1 job per 2GB)"""
    mem_gb = psutil.virtual_memory().total / (1024**3)
    return max(1, int(mem_gb / 2))


def verify_gcc_version(major_version: int):
    """Verify that a specific gcc version is installed"""
    result = bash("ls /usr/bin/gcc-* | grep -E 'gcc-[0-9]+'")
    if f"gcc-{major_version}" not in result:
        raise ScriptError(f"gcc-{major_version} not found")


def create_onnx_venv(src_dir: Path) -> Path:
    venv_dir = src_dir / ".venv"
    requirements_file = src_dir / "tools/ci_build/requirements.txt"

    if not requirements_file.exists():
        raise ScriptError(f"Requirements file not found: {requirements_file}")

    if not venv_dir.exists():
        info("Creating venv for onnx build dependencies")
        # onnx uses python 3.11 for building, relies on pre-build 3.11 numpy wheels
        bash(f"uv venv {venv_dir} --python 3.11", directory=src_dir)

    # uv automatically caches dependencies
    info("Installing onnx build dependencies")
    bash(
        f"uv pip sync --python {venv_dir}/bin/python {requirements_file}",
        directory=src_dir,
    )

    return venv_dir


def clone_onnx(platform_dir: Path, version: str):
    dest_dir = platform_dir / "onnxruntime" / version
    if (dest_dir / "README.md").exists():
        info(f"Onnx {version} sources exist, skipping clone")
    else:
        if not dest_dir.exists():
            dest_dir.mkdir(parents=True, exist_ok=False)
        info(f"Cloning to {dest_dir}")
        bash(
            f"git clone --recursive --depth 1 --branch v{version} "
            f"https://github.com/microsoft/onnxruntime.git {dest_dir}"
        )
        if not (dest_dir / "README.md").exists():
            raise ScriptError("Failed to clone Onnx runtime")
        info("Successfully cloned Onnx runtime")
    return dest_dir


@click.command()
@click.option("--onnx-version", help="Onnx version to build", required=True)
@click.option(
    "--platform-dir", help="Platform directory in build output", required=True
)
def build_onnx(onnx_version, platform_dir):
    gcc_version = 11
    platform_dir = Path(platform_dir).resolve()
    info(f"Building Onnx {onnx_version}")
    verify_gcc_version(gcc_version)
    src_dir = clone_onnx(platform_dir, onnx_version)
    venv_dir = create_onnx_venv(src_dir)

    start_time = time.time()
    bash(
        (
            f"source {venv_dir}/bin/activate && "
            f"CC=gcc-{gcc_version} CXX=g++-{gcc_version} "
            f"{src_dir / 'build.sh'} "
            "--config Release "
            f"--parallel {get_parallel_jobs()} "
            "--skip_tests "
            "--allow_running_as_root "  # needed for building in docker
            "--cmake_extra_defines CMAKE_POSITION_INDEPENDENT_CODE=ON"
        )
    )
    elapsed_time = time.time() - start_time
    info(f"Onnx build completed in {elapsed_time:.1f}s")

    static_libs = [str(p) for p in src_dir.rglob("Release/**/*.a")]
    info(f"Found {len(static_libs)} static libs")
    return static_libs


# Typically run with `./build_onnx.py --onnx-version 1.15.0 --platform-dir ../target/platforms/linux-x64`
if __name__ == "__main__":
    try:
        build_onnx()
    except ScriptError as e:
        error(f"Fatal: {e}")
