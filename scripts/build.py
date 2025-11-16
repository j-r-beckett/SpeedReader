#!/usr/bin/env -S uv run

import click
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration
from build_onnx import build_onnx
from build_speedreader_libs import build_speedreader_libs


@click.command()
@click.option("--onnx-version", help="Onnx runtime version", required=True)
def build(onnx_version):
    platform_dir = "../target/platforms/linux-x64"
    build_onnx(onnx_version, platform_dir)
    build_speedreader_libs(platform_dir)


if __name__ == "__main__":
    try:
        build()
    except ScriptError as e:
        error(f"Fatal: {e}")
