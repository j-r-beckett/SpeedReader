#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../../../build_utils", editable = true }
# ///

"""
Build speedreader_cpuinfo native library.

This builds:
1. cpuinfo library (from pytorch/cpuinfo)
2. speedreader_cpuinfo wrapper (our C code that uses cpuinfo)

Usage:
    ./build.py
"""

import time
import click
from pathlib import Path
from build_utils import ScriptError, bash, info, error, format_duration, ensure_repo

# Directories
SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent.parent.parent
CPUINFO_DIR = REPO_ROOT / ".external" / "cpuinfo"
CPUINFO_URL = "https://github.com/pytorch/cpuinfo.git"
CPUINFO_COMMIT = "b3b25967b5b80406304d575321e572c5f9e5e3c4"  # main as of 2025-01
OUT_DIR = SCRIPT_DIR / "out"


def build_cpuinfo(build_dir: Path, install_dir: Path):
    """Build cpuinfo library using cmake."""
    build_dir.mkdir(parents=True, exist_ok=True)
    install_dir.mkdir(parents=True, exist_ok=True)

    info("Configuring cpuinfo with cmake")
    bash(
        f"cmake {CPUINFO_DIR} "
        f"-DCMAKE_BUILD_TYPE=Release "
        f"-DCMAKE_POSITION_INDEPENDENT_CODE=ON "
        f"-DCPUINFO_BUILD_TOOLS=OFF "
        f"-DCPUINFO_BUILD_UNIT_TESTS=OFF "
        f"-DCPUINFO_BUILD_MOCK_TESTS=OFF "
        f"-DCPUINFO_BUILD_BENCHMARKS=OFF "
        f"-DCMAKE_INSTALL_PREFIX={install_dir}",
        directory=build_dir,
    )

    info("Building cpuinfo")
    bash("cmake --build . --config Release", directory=build_dir)

    info("Installing cpuinfo")
    bash("cmake --install . --config Release", directory=build_dir)


@click.command()
def build():
    """Build speedreader_cpuinfo native library."""
    # Ensure cpuinfo is cloned
    ensure_repo(CPUINFO_DIR, CPUINFO_URL, CPUINFO_COMMIT, "cpuinfo")

    # Build cpuinfo
    cpuinfo_build_dir = CPUINFO_DIR / "build"
    cpuinfo_install_dir = OUT_DIR / "cpuinfo"

    # Check if cpuinfo is already built
    cpuinfo_lib = cpuinfo_install_dir / "lib" / "libcpuinfo.a"
    if not cpuinfo_lib.exists():
        build_cpuinfo(cpuinfo_build_dir, cpuinfo_install_dir)
    else:
        info("cpuinfo already built, skipping")

    # Build speedreader_cpuinfo
    static_dir = OUT_DIR / "static"
    shared_dir = OUT_DIR / "shared"
    static_dir.mkdir(parents=True, exist_ok=True)
    shared_dir.mkdir(parents=True, exist_ok=True)

    static_lib = static_dir / "speedreader_cpuinfo.a"
    shared_lib = shared_dir / "speedreader_cpuinfo.so"

    start_time = time.time()

    # Find all .c files
    c_files = sorted(SCRIPT_DIR.glob("*.c"))
    if not c_files:
        raise ScriptError(f"No .c files found in {SCRIPT_DIR}")

    info(f"Compiling {len(c_files)} C source files")

    c_files_str = " ".join(str(f) for f in c_files)
    cpuinfo_include = cpuinfo_install_dir / "include"
    cpuinfo_libdir = cpuinfo_install_dir / "lib"
    include_flags = f"-I{SCRIPT_DIR} -I{cpuinfo_include}"

    # Create static library (combines our code with cpuinfo)
    info("Creating speedreader_cpuinfo.a")

    # First compile our code to object file
    obj_file = static_dir / "speedreader_cpuinfo.o"
    bash(
        f"zig build-obj -lc -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-femit-bin={obj_file}",
        directory=SCRIPT_DIR,
    )

    # Combine with cpuinfo into a single static library using AR MRI script
    mri_script = static_dir / "combine.mri"
    mri_commands = [
        f"CREATE {static_lib}",
        f"ADDLIB {cpuinfo_lib}",
        f"ADDMOD {obj_file}",
        "SAVE",
        "END"
    ]
    mri_script.write_text("\n".join(mri_commands))
    bash(f"zig ar -M < {mri_script}", directory=SCRIPT_DIR)
    mri_script.unlink()
    obj_file.unlink()

    # Create shared library
    info("Creating speedreader_cpuinfo.so")
    bash(
        f"zig build-lib -lc -dynamic -O ReleaseFast "
        f"{include_flags} {c_files_str} "
        f"-L{cpuinfo_libdir} {cpuinfo_lib} "
        f"-rpath '$ORIGIN' "
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
