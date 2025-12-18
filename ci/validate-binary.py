#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../build_utils", editable = true }
# ///

"""
Validate a speedreader binary.

Usage:
    ./validate-binary.py static
    ./validate-binary.py dynamic
"""

from pathlib import Path

import click

from build_utils import ScriptError, bash, error, info

SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent

BINARY_PATHS = {
    "static": REPO_ROOT / "src/Frontend/bin/Release/net10.0/linux-musl-x64/publish/speedreader",
    "dynamic": REPO_ROOT / "src/Frontend/bin/Release/net10.0/linux-x64/publish/speedreader",
}

TEST_IMAGE = REPO_ROOT / "hello.png"


def check_static_linking(binary: Path):
    """Verify binary is statically linked."""
    file_output = bash(f"file {binary}", directory=REPO_ROOT)
    if "static-pie linked" in file_output or "statically linked" in file_output:
        info("Binary is statically linked")
    else:
        raise ScriptError(f"Binary is not statically linked: {file_output}")


def check_dynamic_linking(binary: Path):
    """Verify binary is dynamically linked and depends on libonnxruntime.so."""
    file_output = bash(f"file {binary}", directory=REPO_ROOT)
    if "dynamically linked" not in file_output:
        raise ScriptError(f"Binary is not dynamically linked: {file_output}")
    info("Binary is dynamically linked")

    ldd_output = bash(f"ldd {binary}", directory=REPO_ROOT)
    if "libonnxruntime.so" not in ldd_output:
        raise ScriptError(f"Binary does not link against libonnxruntime.so: {ldd_output}")
    info("Binary links against libonnxruntime.so")


def smoke_test(binary: Path):
    """Run binary on test image and verify output."""
    output = bash(f"{binary} {TEST_IMAGE}", directory=REPO_ROOT)
    if '"text":"Hello"' not in output:
        raise ScriptError(f"Smoke test failed, expected '\"text\":\"Hello\"' in output: {output}")
    info("Smoke test passed")


@click.command()
@click.argument("build_type", type=click.Choice(["static", "dynamic"]))
def main(build_type: str):
    """Validate a speedreader binary."""
    binary = BINARY_PATHS[build_type]

    if not binary.exists():
        raise ScriptError(f"Binary not found: {binary}")

    info(f"Validating {build_type} binary: {binary}")

    if build_type == "static":
        check_static_linking(binary)
    else:
        check_dynamic_linking(binary)

    smoke_test(binary)


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
