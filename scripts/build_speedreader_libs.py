#!/usr/bin/env -S uv run

import time
import click
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration


@click.command()
@click.option("--platform-dir", help="Platform directory in build output", required=True)
def build_speedreader_libs(platform_dir):
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

    # 2. Compile each .c file to .o
    onnx_include_dir = platform_dir / "lib" / "onnxruntime" / "include"
    if not onnx_include_dir.exists():
        raise ScriptError(f"ONNX headers not found at {onnx_include_dir}. Build ONNX first.")

    object_files = []
    for c_file in c_files:
        o_file = static_dir / f"{c_file.stem}.o"
        bash(
            f"gcc -c -O3 -fPIC "
            f"-I{native_dir} "
            f"-I{onnx_include_dir} "
            f"-o {o_file} "
            f"{c_file}"
        )
        object_files.append(o_file)

    # 3. Create static library from all object files
    info("Creating speedreader_ort.a")
    bash(f"ar rcs {static_lib} {' '.join(str(o) for o in object_files)}")

    # 4. Create shared library that links with libonnxruntime.so
    info("Creating speedreader_ort.so")
    onnx_shared_dir = platform_dir / "lib" / "onnxruntime" / "shared"
    onnx_shared_lib = onnx_shared_dir / "libonnxruntime.so"

    if not onnx_shared_lib.exists():
        raise ScriptError(f"ONNX shared library not found at {onnx_shared_lib}. Build ONNX first.")

    bash(
        f"g++ -shared -fPIC "
        f"-Wl,--whole-archive {static_lib} -Wl,--no-whole-archive "
        f"-L{onnx_shared_dir} -lonnxruntime "
        f"-Wl,-rpath,'$ORIGIN:$ORIGIN/../onnxruntime/shared' "
        f"-o {shared_lib} "
        f"-lstdc++ -lpthread -lm -ldl"
    )

    # Cleanup object files
    for o_file in object_files:
        o_file.unlink()

    elapsed_time = time.time() - start_time
    info(f"Built {static_lib} and {shared_lib} in {format_duration(elapsed_time)}")


if __name__ == "__main__":
    try:
        build_speedreader_libs()
    except ScriptError as e:
        error(f"Fatal: {e}")
