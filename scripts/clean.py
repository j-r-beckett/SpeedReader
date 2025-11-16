#!/usr/bin/env -S uv run

import click
import shutil
from pathlib import Path
from utils import ScriptError, info, error


def clean_dir(path):
    for file in path.rglob("*"):
        if file.is_file():
            file.unlink()


def empty_dir(path):
    # delete all files and directories in path/
    for item in path.iterdir():
        if item.is_dir():
            shutil.rmtree(item)
        else:
            item.unlink()


@click.command()
@click.option("--deps", help="Clean dependencies (Onnx)", is_flag=True)
@click.option("--nuke", help="Delete the entire build directory", is_flag=True)
def clean(deps, nuke):
    platform_dir = Path("../target/platforms/linux-x64")
    if nuke:
        info(f"Cleaning {platform_dir}")
        empty_dir(platform_dir)
        return  # nothing more to do

    if deps:
        onnx_path = platform_dir / "build" / "onnxruntime"
        info(f"Cleaning {onnx_path}")
        empty_dir(onnx_path)

    bin_path = platform_dir / "bin"
    info(f"Cleaning {bin_path}")
    clean_dir(bin_path)

    lib_path = platform_dir / "lib"
    info(f"Cleaning {lib_path}")
    clean_dir(lib_path)


if __name__ == "__main__":
    try:
        clean()
    except ScriptError as e:
        error(f"Fatal: {e}")
