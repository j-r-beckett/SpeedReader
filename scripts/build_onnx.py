#!/usr/bin/env -S uv run

import time
import click
import psutil
import shutil
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration


def get_parallel_jobs() -> int:
    """Calculate safe number of parallel jobs based on available memory (1 job per 2GB)"""
    mem_gb = psutil.virtual_memory().total / (1024**3)
    return max(1, int(mem_gb / 1.5))


def create_onnx_venv(src_dir: Path) -> Path:
    venv_dir = src_dir / ".venv"
    requirements_file = src_dir / "tools/ci_build/requirements.txt"

    if not requirements_file.exists():
        raise ScriptError(f"Requirements file not found: {requirements_file}")

    if not venv_dir.exists():
        info("Creating onnx python environment")
        # onnx uses python 3.11 for building, relies on pre-build 3.11 numpy wheels
        bash(f"uv venv {venv_dir} --python 3.11", directory=src_dir)

    # uv automatically caches dependencies
    info("Installing onnx python environment")
    bash(
        f"uv pip sync --python {venv_dir}/bin/python {requirements_file}",
        directory=src_dir,
    )

    return venv_dir


def checkout_onnx_sources(onnx_src_dir: Path, version: str):
    """Clone or checkout the correct version of onnxruntime sources"""
    if not (onnx_src_dir / "README.md").exists():
        onnx_src_dir.mkdir(parents=True, exist_ok=True)
        info(f"Cloning onnxruntime v{version} to {onnx_src_dir}")
        bash(
            f"git clone --recursive --depth 1 --branch v{version} "
            f"https://github.com/microsoft/onnxruntime.git {onnx_src_dir}"
        )
        if not (onnx_src_dir / "README.md").exists():
            raise ScriptError("Failed to clone Onnx runtime")
        info("Successfully cloned Onnx runtime")
    else:
        # Check if we're already on the right version
        current_tag = bash(
            "git describe --tags --exact-match 2>/dev/null || echo ''",
            directory=onnx_src_dir,
        ).strip()
        expected_tag = f"v{version}"

        if current_tag == expected_tag:
            info(f"Onnx sources already at {expected_tag}")
        else:
            info(
                f"Checking out onnxruntime {expected_tag} (currently at {current_tag or 'unknown'})"
            )
            # Fetch the tag and checkout
            bash(f"git fetch --depth 1 origin tag v{version}", directory=onnx_src_dir)
            bash(f"git checkout v{version}", directory=onnx_src_dir)
            bash(
                "git submodule update --init --recursive --depth 1",
                directory=onnx_src_dir,
            )

    return onnx_src_dir


@click.command()
@click.option("--onnx-version", help="Onnx version to build", required=True)
@click.option(
    "--platform-dir", help="Platform directory in build output", required=True
)
def build_onnx(onnx_version, platform_dir):
    gcc_version = 11
    platform_dir = Path(platform_dir).resolve()
    build_dir = platform_dir / "build" / "onnxruntime"
    lib_dir = platform_dir / "lib" / "onnxruntime"
    version_file = lib_dir / "version.txt"

    # Check if we already have this version built
    if version_file.exists():
        built_version = version_file.read_text().strip()
        if built_version == onnx_version:
            info(f"Onnx {onnx_version} already built, skipping")
            return
        else:
            info(f"Onnx version mismatch: have {built_version}, need {onnx_version}")

    info(f"Building Onnx {onnx_version}")
    checkout_onnx_sources(build_dir, onnx_version)
    venv_dir = create_onnx_venv(build_dir)

    start_time = time.time()

    parallelism = get_parallel_jobs()
    info(f"Compiling Onnx with {parallelism} threads")
    bash(
        (
            f"source {venv_dir}/bin/activate && "
            f"CC=gcc-{gcc_version} CXX=g++-{gcc_version} "
            f"{build_dir / 'build.sh'} "
            "--config Release "
            f"--parallel {parallelism} "
            "--skip_tests "
            "--allow_running_as_root "  # needed for building in docker
            "--cmake_extra_defines CMAKE_POSITION_INDEPENDENT_CODE=ON"
        )
    )
    elapsed_time = time.time() - start_time
    info(f"Onnx build completed in {format_duration(elapsed_time)}")

    # Copy static libraries to lib directory
    static_libs = list(build_dir.rglob("Release/**/*.a"))
    static_lib_dir = lib_dir / "static"
    static_lib_dir.mkdir(parents=True, exist_ok=True)

    for lib in static_libs:
        shutil.copy2(lib, static_lib_dir / lib.name)

    info(f"Copied {len(static_libs)} static libs to {static_lib_dir}")

    # Write version file after successful build
    lib_dir.mkdir(parents=True, exist_ok=True)
    version_file.write_text(onnx_version)
    info(f"Wrote version {onnx_version} to {version_file}")


# Typically run with `./build_onnx.py --onnx-version 1.15.0 --platform-dir ../target/platforms/linux-x64`
if __name__ == "__main__":
    try:
        build_onnx()
    except ScriptError as e:
        error(f"Fatal: {e}")
