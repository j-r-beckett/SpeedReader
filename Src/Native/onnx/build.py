#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "rich", "psutil"]
# ///

"""
Build ONNX Runtime from the submodule.

Usage:
    ./build.py --version 1.15.0
    ./build.py --version 1.15.0 --musl
"""

import subprocess
import shutil
import time
import psutil
import click
from pathlib import Path
from rich.console import Console

console = Console()

# Directories relative to this script
SCRIPT_DIR = Path(__file__).parent.resolve()
SUBMODULE_DIR = SCRIPT_DIR / "external" / "onnxruntime"
PATCHES_DIR = SCRIPT_DIR / "patches"
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


def get_parallel_jobs() -> int:
    """Calculate safe number of parallel jobs based on available memory (1 job per 1.5GB)."""
    mem_gb = psutil.virtual_memory().total / (1024**3)
    return max(1, int(mem_gb / 1.5))


def checkout_version(version: str):
    """Checkout the requested version tag from the submodule."""
    if not SUBMODULE_DIR.exists():
        raise BuildError(f"Submodule not found at {SUBMODULE_DIR}. Run: git submodule update --init")

    expected_tag = f"v{version}"

    # Check current tag
    current_tag = bash(
        "git describe --tags --exact-match 2>/dev/null || echo ''",
        directory=SUBMODULE_DIR,
    ).strip()

    if current_tag == expected_tag:
        info(f"ONNX Runtime already at {expected_tag}")
        return

    info(f"Checking out ONNX Runtime {expected_tag} (currently at {current_tag or 'unknown'})")

    # Fetch the tag and checkout
    bash(f"git fetch --depth 1 origin tag {expected_tag}", directory=SUBMODULE_DIR)
    bash(f"git checkout {expected_tag}", directory=SUBMODULE_DIR)
    bash("git submodule update --init --recursive --depth 1", directory=SUBMODULE_DIR)


def create_venv() -> Path:
    """Create Python virtual environment for ONNX build."""
    venv_dir = SUBMODULE_DIR / ".venv"
    requirements_file = SUBMODULE_DIR / "tools/ci_build/requirements.txt"

    if not requirements_file.exists():
        raise BuildError(f"Requirements file not found: {requirements_file}")

    if not venv_dir.exists():
        info("Creating ONNX Python environment")
        bash(f"uv venv {venv_dir} --python 3.11", directory=SUBMODULE_DIR)

    info("Installing ONNX Python dependencies")
    bash(
        f"uv pip sync --python {venv_dir}/bin/python {requirements_file}",
        directory=SUBMODULE_DIR,
    )

    return venv_dir


def apply_musl_patches():
    """Apply patches needed for musl/Alpine build."""
    no_execinfo_patch = PATCHES_DIR / "no-execinfo.patch"
    if no_execinfo_patch.exists():
        info("Applying no-execinfo.patch for musl compatibility")
        bash(f"patch -p1 -N < {no_execinfo_patch} || true", directory=SUBMODULE_DIR)


def combine_static_libs(libs: list[Path], output_path: Path):
    """Combine multiple .a files into a single archive using AR MRI script."""
    mri_script = output_path.parent / "combine.mri"
    mri_commands = [f"CREATE {output_path}"]
    for lib in libs:
        mri_commands.append(f"ADDLIB {lib}")
    mri_commands.extend(["SAVE", "END"])

    mri_script.write_text("\n".join(mri_commands))
    bash(f"zig ar -M < {mri_script}")
    mri_script.unlink()


@click.command()
@click.option("--version", "onnx_version", required=True, help="ONNX Runtime version (e.g. 1.15.0)")
@click.option("--musl", is_flag=True, help="Build for Alpine Linux (musl libc)")
def build(onnx_version: str, musl: bool):
    """Build ONNX Runtime from submodule."""
    version_file = OUT_DIR / "version.txt"

    # Check if already built
    if version_file.exists():
        built_version = version_file.read_text().strip()
        if built_version == onnx_version:
            info(f"ONNX Runtime {onnx_version} already built, skipping")
            return
        else:
            info(f"Version mismatch: have {built_version}, need {onnx_version}")

    info(f"Building ONNX Runtime {onnx_version}")

    # Checkout requested version
    checkout_version(onnx_version)

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
            f"{SUBMODULE_DIR / 'build.sh'} "
            "--config Release "
            f"--parallel {parallelism} "
            "--skip_tests "
            "--allow_running_as_root "
            "--build_shared_lib "
            f"{extra_flags}"
            f"--cmake_extra_defines {cmake_extra}"
        )
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
    static_libs = list(SUBMODULE_DIR.rglob("Release/**/*.a"))
    info(f"Found {len(static_libs)} static libraries")

    combined_archive = static_dir / "onnxruntime.a"
    info("Combining static libraries into onnxruntime.a")
    combine_static_libs(static_libs, combined_archive)
    info(f"Created {combined_archive}")

    # Copy and patch shared library
    shared_libs = list(SUBMODULE_DIR.rglob("Release/**/libonnxruntime.so"))
    if shared_libs:
        for so in shared_libs:
            dest = shared_dir / "libonnxruntime.so"
            shutil.copy2(so, dest)
            info("Patching SONAME to libonnxruntime.so")
            bash(f"patchelf --set-soname libonnxruntime.so {dest}")
        info(f"Copied and patched shared library to {shared_dir}")
    else:
        info("Warning: No shared libraries found")

    # Copy headers
    header_src = SUBMODULE_DIR / "include" / "onnxruntime" / "core" / "session"
    if header_src.exists():
        headers = list(header_src.glob("*.h"))
        for header in headers:
            shutil.copy2(header, include_dir / header.name)
        info(f"Copied {len(headers)} headers to {include_dir}")

    # Write version file
    version_file.write_text(onnx_version)
    info(f"Wrote version {onnx_version} to {version_file}")


if __name__ == "__main__":
    try:
        build()
    except BuildError as e:
        error(f"Fatal: {e}")
        exit(1)
