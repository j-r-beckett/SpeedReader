#!/usr/bin/env -S uv run

import time
import hashlib
import shutil
import click
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration


def get_directory_hash(directory: Path) -> str:
    """Calculate SHA256 hash of all source files in a directory"""
    sha256 = hashlib.sha256()

    # Hash all source files in sorted order for determinism
    source_files = sorted(directory.glob("*.c")) + sorted(directory.glob("*.h"))

    if not source_files:
        raise ScriptError(f"No source files found in {directory}")

    for file_path in source_files:
        # Hash the file path (relative to directory) and contents
        sha256.update(file_path.name.encode())
        with open(file_path, 'rb') as f:
            sha256.update(f.read())

    return sha256.hexdigest()


@click.command()
@click.option("--platform-dir", help="Platform directory in build output", required=True)
def build_speedreader_libs(platform_dir):
    platform_dir = Path(platform_dir).resolve()
    native_dir = Path(__file__).parent.parent / "native"
    lib_dir = platform_dir / "lib"
    output_lib = lib_dir / "speedreader_ort.a"
    version_file = lib_dir / "speedreader_ort_version.txt"

    # Version tracking using hash of all native source files
    current_hash = get_directory_hash(native_dir)

    if version_file.exists():
        built_hash = version_file.read_text().strip()
        if built_hash == current_hash:
            info(f"speedreader_ort.a already built, skipping")
            return

    start_time = time.time()

    # 1. Compile wrapper to object file
    wrapper_src = native_dir / "speedreader_ort.c"
    info("Compiling speedreader_ort.c")
    wrapper_o = lib_dir / "speedreader_ort.o"
    lib_dir.mkdir(parents=True, exist_ok=True)

    bash(
        f"gcc -c -O3 -fPIC "
        f"-I{native_dir} "
        f"-o {wrapper_o} "
        f"{wrapper_src}"
    )

    # 2. Collect ONNX static libraries
    onnx_lib_dir = lib_dir / "onnxruntime" / "static"
    onnx_libs = sorted(onnx_lib_dir.glob("*.a"))

    if not onnx_libs:
        raise ScriptError(f"No ONNX libraries found in {onnx_lib_dir}")

    info(f"Found {len(onnx_libs)} ONNX libraries")

    # 3. Combine all archives into a single archive
    info("Creating combined static library speedreader_ort.a")

    # Use MRI script to combine archives (avoids extraction)
    # This is cleaner than extracting/re-archiving and preserves structure
    mri_script = lib_dir / "combine.mri"
    mri_commands = [f"CREATE {output_lib}"]
    mri_commands.append(f"ADDMOD {wrapper_o}")
    for lib in onnx_libs:
        mri_commands.append(f"ADDLIB {lib}")
    mri_commands.append("SAVE")
    mri_commands.append("END")

    mri_script.write_text("\n".join(mri_commands))
    bash(f"ar -M < {mri_script}")

    # Cleanup
    mri_script.unlink()
    wrapper_o.unlink()

    elapsed_time = time.time() - start_time
    info(f"Built speedreader_ort.a in {format_duration(elapsed_time)}")

    # Write version file
    version_file.write_text(current_hash)
    info(f"Wrote version hash to {version_file}")


if __name__ == "__main__":
    try:
        build_speedreader_libs()
    except ScriptError as e:
        error(f"Fatal: {e}")
