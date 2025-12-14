#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "psutil", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../../../build_utils", editable = true }
# ///

"""
Build ONNX Runtime from source.

Usage:
    ./build.py --version 1.15.0
    ./build.py --version 1.15.0 --musl
"""

import shutil
import time
import psutil
import click
from pathlib import Path
from build_utils import ScriptError, bash, info, error, format_duration, ensure_repo

# Directories
SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent.parent.parent
ONNXRUNTIME_DIR = REPO_ROOT / ".external" / "onnxruntime"
ONNXRUNTIME_URL = "https://github.com/microsoft/onnxruntime.git"
PATCHES_DIR = SCRIPT_DIR / "patches"
OUT_DIR = SCRIPT_DIR / "out"


def get_parallel_jobs() -> int:
    """Calculate safe number of parallel jobs based on available memory (1 job per 1.5GB)."""
    mem_gb = psutil.virtual_memory().total / (1024**3)
    return max(1, int(mem_gb / 1.5))


def create_venv() -> Path:
    """Create Python virtual environment for ONNX build."""
    venv_dir = ONNXRUNTIME_DIR / ".venv"
    requirements_file = ONNXRUNTIME_DIR / "tools/ci_build/requirements.txt"

    if not requirements_file.exists():
        raise ScriptError(f"Requirements file not found: {requirements_file}")

    if not venv_dir.exists():
        info("Creating ONNX Python environment")
        bash(f"uv venv {venv_dir} --python 3.11", directory=ONNXRUNTIME_DIR)

    info("Installing ONNX Python dependencies")
    bash(
        f"uv pip sync --python {venv_dir}/bin/python {requirements_file}",
        directory=ONNXRUNTIME_DIR,
    )

    return venv_dir


def apply_musl_patches():
    """Apply patches needed for musl/Alpine build."""
    no_execinfo_patch = PATCHES_DIR / "no-execinfo.patch"
    if no_execinfo_patch.exists():
        info("Applying no-execinfo.patch for musl compatibility")
        bash(f"patch -p1 -N < {no_execinfo_patch} || true", directory=ONNXRUNTIME_DIR)


def combine_static_libs(libs: list[Path], output_path: Path):
    """Combine multiple .a files into a single archive using AR MRI script."""
    mri_script = output_path.parent / "combine.mri"
    mri_commands = [f"CREATE {output_path}"]
    for lib in libs:
        mri_commands.append(f"ADDLIB {lib}")
    mri_commands.extend(["SAVE", "END"])

    mri_script.write_text("\n".join(mri_commands))
    bash(f"zig ar -M < {mri_script}", directory=SCRIPT_DIR)
    mri_script.unlink()


@click.command()
@click.option("--version", "onnx_version", required=True, help="ONNX Runtime version (e.g. 1.15.0)")
@click.option("--musl", is_flag=True, help="Build for Alpine Linux (musl libc)")
def build(onnx_version: str, musl: bool):
    """Build ONNX Runtime from source."""
    info(f"Building ONNX Runtime {onnx_version}")

    # Ensure onnxruntime is cloned at the requested version
    ensure_repo(ONNXRUNTIME_DIR, ONNXRUNTIME_URL, f"v{onnx_version}", "ONNX Runtime")

    # Apply musl patches if needed
    if musl:
        apply_musl_patches()

    # Create venv for build
    venv_dir = create_venv()

    start_time = time.time()
    parallelism = get_parallel_jobs()
    info(f"Compiling ONNX Runtime with {parallelism} threads")

    if musl:
        compiler_env = "CC=gcc CXX=g++"
        cmake_extra = (
            "CMAKE_POSITION_INDEPENDENT_CODE=ON "
            "CMAKE_POLICY_VERSION_MINIMUM=3.5 "
            "CMAKE_C_FLAGS=-D_LARGEFILE64_SOURCE "
            'CMAKE_CXX_FLAGS="-D_LARGEFILE64_SOURCE -include cstdint" '
            "onnxruntime_BUILD_UNIT_TESTS=OFF"
        )
        extra_flags = "--compile_no_warning_as_error "
    else:
        compiler_env = "CC=gcc-11 CXX=g++-11"
        cmake_extra = "CMAKE_POSITION_INDEPENDENT_CODE=ON"
        extra_flags = ""

    bash(
        (
            f"source {venv_dir}/bin/activate && "
            f"{compiler_env} "
            f"{ONNXRUNTIME_DIR / 'build.sh'} "
            "--config Release "
            f"--parallel {parallelism} "
            "--skip_tests "
            "--allow_running_as_root "
            "--build_shared_lib "
            f"{extra_flags}"
            f"--cmake_extra_defines {cmake_extra}"
        ),
        directory=SCRIPT_DIR,
    )

    elapsed_time = time.time() - start_time
    info(f"ONNX Runtime build completed in {format_duration(elapsed_time)}")

    # Create output directories
    static_dir = OUT_DIR / "static"
    shared_dir = OUT_DIR / "shared"
    include_dir = OUT_DIR / "include"
    static_dir.mkdir(parents=True, exist_ok=True)
    shared_dir.mkdir(parents=True, exist_ok=True)
    include_dir.mkdir(parents=True, exist_ok=True)

    # Combine static libraries
    static_libs = list(ONNXRUNTIME_DIR.rglob("Release/**/*.a"))
    info(f"Found {len(static_libs)} static libraries")

    combined_archive = static_dir / "onnxruntime.a"
    info("Combining static libraries into onnxruntime.a")
    combine_static_libs(static_libs, combined_archive)
    info(f"Created {combined_archive}")

    # Copy and patch shared library
    shared_libs = list(ONNXRUNTIME_DIR.rglob("Release/**/libonnxruntime.so"))
    if shared_libs:
        for so in shared_libs:
            dest = shared_dir / "libonnxruntime.so"
            shutil.copy2(so, dest)
            info("Patching SONAME to libonnxruntime.so")
            bash(f"patchelf --set-soname libonnxruntime.so {dest}", directory=SCRIPT_DIR)
        info(f"Copied and patched shared library to {shared_dir}")
    else:
        info("Warning: No shared libraries found")

    # Copy headers
    header_src = ONNXRUNTIME_DIR / "include" / "onnxruntime" / "core" / "session"
    if header_src.exists():
        headers = list(header_src.glob("*.h"))
        for header in headers:
            shutil.copy2(header, include_dir / header.name)
        info(f"Copied {len(headers)} headers to {include_dir}")


if __name__ == "__main__":
    try:
        build()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
