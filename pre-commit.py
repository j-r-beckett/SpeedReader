#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "utils"]
#
# [tool.uv.sources]
# utils = { path = "tools/utils", editable = true }
# ///

"""
Pre-commit hook for formatting, building, and testing.

Usage:
    ./pre-commit.py
"""

import hashlib
import os
from pathlib import Path

import click

from utils import ScriptError, bash, error, info

SCRIPT_DIR = Path(__file__).parent.resolve()


def get_modification_hash() -> str:
    """Get a hash of modification times for all tracked and untracked non-ignored files."""
    files_output = bash(
        "git ls-files --cached --others --exclude-standard",
        directory=SCRIPT_DIR,
    )

    entries = []
    for filename in files_output.strip().split("\n"):
        if not filename:
            continue
        filepath = SCRIPT_DIR / filename
        try:
            mtime = os.stat(filepath).st_mtime_ns
            entries.append(f"{mtime} {filename}")
        except OSError:
            pass

    entries.sort()
    content = "\n".join(entries)
    return hashlib.sha256(content.encode()).hexdigest()


@click.command()
def main():
    info("Running formatter")
    bash("dotnet format src/SpeedReader.slnx --no-restore", directory=SCRIPT_DIR)

    before_tests_hash = get_modification_hash()

    info("Building")
    try:
        bash("dotnet build src/SpeedReader.slnx /warnaserror -v q", directory=SCRIPT_DIR)
    except ScriptError:
        raise ScriptError("Build failed. Commit aborted.")

    info("Running tests")
    try:
        bash("dotnet test src/SpeedReader.slnx -v q --no-build", directory=SCRIPT_DIR)
    except ScriptError:
        raise ScriptError("Tests failed. Commit aborted.")

    after_tests_hash = get_modification_hash()

    if before_tests_hash != after_tests_hash:
        raise ScriptError(
            "Detected file modification while hook was running. Commit aborted."
        )

    info("Staging formatted changes")
    bash("git add -u", directory=SCRIPT_DIR)

    info("Pre-commit checks passed")


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
