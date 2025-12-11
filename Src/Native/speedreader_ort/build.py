#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "rich"]
# ///

"""
Build speedreader_ort native library.

Usage:
    ./build.py
"""

import subprocess
import time
import click
from pathlib import Path
from rich.console import Console

console = Console()

# Directories relative to this script
SCRIPT_DIR = Path(__file__).parent.resolve()
ONNX_OUT_DIR = SCRIPT_DIR.parent / "onnx" / "out"
OUT_DIR = SCRIPT_DIR / "out"


class BuildError(Exception):
    pass


def bash(command: str, directory: Path = None) -> str:
    """Execute a bash command, streaming output in real-time."""
    if directory is None:
        directory = SCRIPT_DIR

    console.print(f"$ {command}", style="yellow", highlight=False)

    process = subprocess.Popen(
        ["bash", "-c", command],
        cwd=directory,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
    )

    output_lines = []
    for line in process.stdout:
        output_lines.append(line)
        console.print(line, style="bright_black", end="", highlight=False, markup=False)

    return_code = process.wait()

    if return_code == 127:
        raise BuildError(f"Command {command.split()[0]} not found. Are you missing a dependency?")
    if return_code != 0:
        raise BuildError(f"Command failed with exit code {return_code}")

    return "".join(output_lines)


def info(msg: str):
    console.print(msg, style="green", highlight=False, markup=False)


def error(msg: str):
    console.print(msg, style="red", highlight=False, markup=False)


def format_duration(seconds: float) -> str:
    if seconds < 60:
        return f"{seconds:.1f}s"
    elif seconds < 3600:
        mins = int(seconds // 60)
        secs = int(seconds % 60)
        return f"{mins}m {secs}s"
    else:
        hours = int(seconds // 3600)
        mins = int((seconds % 3600) // 60)
        return f"{hours}h {mins}m"


@click.command()
def build():
    """Build speedreader_ort native library."""
    static_dir = OUT_DIR / "static"
    shared_dir = OUT_DIR / "shared"
    static_dir.mkdir(parents=True, exist_ok=True)
    shared_dir.mkdir(parents=True, exist_ok=True)

    static_lib = static_dir / "speedreader_ort.a"
    shared_lib = shared_dir / "speedreader_ort.so"

    # Check ONNX dependencies
    onnx_include_dir = ONNX_OUT_DIR / "include"
    onnx_shared_dir = ONNX_OUT_DIR / "shared"

    if not (onnx_shared_dir / "libonnxruntime.so").exists():
        raise BuildError(
            f"ONNX shared library not found at {onnx_shared_dir}. "
            "Build ONNX first: ../onnx/build.py --version <version>"
        )

    start_time = time.time()

    # Find all .c files
    c_files = sorted(SCRIPT_DIR.glob("*.c"))
    if not c_files:
        raise BuildError(f"No .c files found in {SCRIPT_DIR}")

    info(f"Compiling {len(c_files)} C source files")

    c_files_str = " ".join(str(f) for f in c_files)
    include_flags = f"-I{SCRIPT_DIR} -I{onnx_include_dir}"

    # Create static library
    info("Creating speedreader_ort.a")
    bash(
        f"zig build-lib -lc -static -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-femit-bin={static_lib}"
    )

    # Create shared library
    info("Creating speedreader_ort.so")
    bash(
        f"zig build-lib -lc -dynamic -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-L{onnx_shared_dir} -lonnxruntime "
        f"-rpath '$ORIGIN:$ORIGIN/../onnx/out/shared' "
        f"-femit-bin={shared_lib}"
    )

    elapsed_time = time.time() - start_time
    info(f"Built {static_lib.name} and {shared_lib.name} in {format_duration(elapsed_time)}")


if __name__ == "__main__":
    try:
        build()
    except BuildError as e:
        error(f"Fatal: {e}")
        exit(1)
