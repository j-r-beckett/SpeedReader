#!/usr/bin/env -S uv run

import time
from pathlib import Path
from utils import ScriptError, bash, info, format_duration


def build_speedreader_libs(platform_dir, musl=False):
    platform_dir = Path(platform_dir).resolve()
    native_dir = Path(__file__).parent.parent / "native"
    lib_dir = platform_dir / "lib" / "speedreader_ort"
    static_dir = lib_dir / "static"
    shared_dir = lib_dir / "shared"
    static_dir.mkdir(parents=True, exist_ok=True)
    shared_dir.mkdir(parents=True, exist_ok=True)

    static_lib = static_dir / "speedreader_ort.a"
    shared_lib = shared_dir / "speedreader_ort.so"

    start_time = time.time()

    # 1. Find all .c files in native directory
    c_files = sorted(native_dir.glob("*.c"))
    if not c_files:
        raise ScriptError(f"No .c files found in {native_dir}")

    info(f"Compiling {len(c_files)} C source files")

    onnx_include_dir = platform_dir / "lib" / "onnxruntime" / "include"
    onnx_shared_dir = platform_dir / "lib" / "onnxruntime" / "shared"
    if not (onnx_shared_dir / "libonnxruntime.so").exists():
        raise ScriptError(
            f"ONNX shared library not found at {onnx_shared_dir}. Build ONNX first."
        )

    c_files_str = " ".join(str(f) for f in c_files)
    include_flags = f"-I{native_dir} -I{onnx_include_dir}"

    # 2. Create static library
    info("Creating speedreader_ort.a")
    bash(
        f"zig build-lib -lc -static -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-femit-bin={static_lib}"
    )

    # 3. Create shared library that links with libonnxruntime.so
    info("Creating speedreader_ort.so")
    bash(
        f"zig build-lib -lc -dynamic -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-L{onnx_shared_dir} -lonnxruntime "
        f"-rpath '$ORIGIN:$ORIGIN/../onnxruntime/shared' "
        f"-femit-bin={shared_lib}"
    )

    elapsed_time = time.time() - start_time
    info(f"Built {static_lib} and {shared_lib} in {format_duration(elapsed_time)}")
