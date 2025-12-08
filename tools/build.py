#!/usr/bin/env -S uv run

import click
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration
from build_onnx import build_onnx
from build_speedreader_libs import build_speedreader_libs


@click.command()
@click.option("--onnx-version", help="Onnx runtime version", required=True)
@click.option("--musl", is_flag=True, help="Build for Alpine Linux (musl libc)")
def build(onnx_version, musl):
    if musl:
        platform_dir = "../target/platforms/linux-musl-x64"
    else:
        platform_dir = "../target/platforms/linux-x64"
    build_onnx(onnx_version, platform_dir, musl=musl)
    build_speedreader_libs(platform_dir, musl=musl)


if __name__ == "__main__":
    try:
        build()
    except ScriptError as e:
        error(f"Fatal: {e}")
