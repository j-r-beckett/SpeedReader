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
        # onnx uses python 3.11 for building, relies on pre-built 3.11 numpy wheels
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


def combine_static_libs(libs_path: Path, output_path: Path):
    """Combine multiple .a files into a single archive using AR MRI script"""
    mri_script = output_path.parent / "combine.mri"
    mri_commands = [f"CREATE {output_path}"]
    for lib in list(libs_path.rglob("Release/**/*.a")):
        mri_commands.append(f"ADDLIB {lib}")
    mri_commands.extend(["SAVE", "END"])

    mri_script.write_text("\n".join(mri_commands))
    bash(f"ar -M < {mri_script}")
    mri_script.unlink()


def combine_static_libs(lib_paths: list[Path], output_path: Path):
    """Combine multiple .a files into a single archive using AR MRI script"""
    mri_script = output_path.parent / "combine.mri"
    mri_commands = [f"CREATE {output_path}"]
    for lib in lib_paths:
        mri_commands.append(f"ADDLIB {lib}")
    mri_commands.extend(["SAVE", "END"])

    mri_script.write_text("\n".join(mri_commands))
    bash(f"ar -M < {mri_script}")
    mri_script.unlink()


# @click.command()
# @click.option("--onnx-version", help="Onnx version to build", required=True)
# @click.option(
#     "--platform-dir", help="Platform directory in build output", required=True
# )
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
            "--build_shared_lib "  # always build shared library
            "--cmake_extra_defines CMAKE_POSITION_INDEPENDENT_CODE=ON"
        )
    )
    elapsed_time = time.time() - start_time
    info(f"Onnx build completed in {format_duration(elapsed_time)}")

    # 1. Get .a file paths
    static_libs = list(build_dir.rglob("Release/**/*.a"))
    info(f"Found {len(static_libs)} static libraries")

    # 2. Combine into single onnxruntime.a
    static_dir = lib_dir / "static"
    static_dir.mkdir(parents=True, exist_ok=True)
    combined_archive = static_dir / "onnxruntime.a"

    info("Combining static libraries into onnxruntime.a")
    combine_static_libs(static_libs, combined_archive)
    info(f"Created {combined_archive}")

    # 3. Copy shared library and patch SONAME for version compatibility
    shared_libs = list(build_dir.rglob("Release/**/libonnxruntime.so"))
    if shared_libs:
        shared_dir = lib_dir / "shared"
        shared_dir.mkdir(parents=True, exist_ok=True)
        for so in shared_libs:
            # Copy the library (following symlink to get actual file content)
            temp_lib = shared_dir / "libonnxruntime.so"
            shutil.copy2(so, temp_lib)

            # Patch SONAME from libonnxruntime.so.1.15.0 to libonnxruntime.so.1
            # This allows users to swap any 1.x version of ONNX Runtime
            info("Patching SONAME to libonnxruntime.so.1 for version compatibility")
            bash(f"patchelf --set-soname libonnxruntime.so.1 {temp_lib}")

            # Rename to match the new SONAME
            final_lib = shared_dir / "libonnxruntime.so.1"
            temp_lib.rename(final_lib)

            # Create linker symlink for build time
            linker_symlink = shared_dir / "libonnxruntime.so"
            if linker_symlink.exists():
                linker_symlink.unlink()
            linker_symlink.symlink_to("libonnxruntime.so.1")

        info(f"Copied and patched shared library to {shared_dir}")
    else:
        info("Warning: No shared libraries found")

    # 4. Copy headers
    include_dir = lib_dir / "include"
    include_dir.mkdir(parents=True, exist_ok=True)
    header_src = build_dir / "include" / "onnxruntime" / "core" / "session"
    if header_src.exists():
        headers = list(header_src.glob("*.h"))
        for header in headers:
            shutil.copy2(header, include_dir / header.name)
        info(f"Copied {len(headers)} headers to {include_dir}")

    # Write version file after successful build
    lib_dir.mkdir(parents=True, exist_ok=True)
    version_file.write_text(onnx_version)
    info(f"Wrote version {onnx_version} to {version_file}")


# Typically run with `./build_onnx.py --onnx-version 1.15.0 --platform-dir ../target/platforms/linux-x64`
# if __name__ == "__main__":
# try:
#     build_onnx()
# except ScriptError as e:
#     error(f"Fatal: {e}")
