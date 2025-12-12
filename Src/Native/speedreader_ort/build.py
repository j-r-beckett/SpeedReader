#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "utils"]
#
# [tool.uv.sources]
# utils = { path = "../../../tools/utils", editable = true }
# ///

"""
Build speedreader_ort native library.

Usage:
    ./build.py
"""

import time
import click
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration

# Directories relative to this script
SCRIPT_DIR = Path(__file__).parent.resolve()
ONNX_OUT_DIR = SCRIPT_DIR.parent / "onnx" / "out"
OUT_DIR = SCRIPT_DIR / "out"


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
        raise ScriptError(
            f"ONNX shared library not found at {onnx_shared_dir}. "
            "Build ONNX first: ../onnx/build.py --version <version>"
        )

    start_time = time.time()

    # Find all .c files
    c_files = sorted(SCRIPT_DIR.glob("*.c"))
    if not c_files:
        raise ScriptError(f"No .c files found in {SCRIPT_DIR}")

    info(f"Compiling {len(c_files)} C source files")

    c_files_str = " ".join(str(f) for f in c_files)
    include_flags = f"-I{SCRIPT_DIR} -I{onnx_include_dir}"

    # Create static library
    info("Creating speedreader_ort.a")
    bash(
        f"zig build-lib -lc -static -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-femit-bin={static_lib}",
        directory=SCRIPT_DIR,
    )

    # Create shared library
    info("Creating speedreader_ort.so")
    bash(
        f"zig build-lib -lc -dynamic -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-L{onnx_shared_dir} -lonnxruntime "
        f"-rpath '$ORIGIN:$ORIGIN/../onnx/out/shared' "
        f"-femit-bin={shared_lib}",
        directory=SCRIPT_DIR,
    )

    elapsed_time = time.time() - start_time
    info(f"Built {static_lib.name} and {shared_lib.name} in {format_duration(elapsed_time)}")


if __name__ == "__main__":
    try:
        build()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
