#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../../build_utils", editable = true }
# ///

"""
Deep clean native build artifacts.

Deletes:
- src/Native/onnx/out/
- src/Native/speedreader_ort/out/
- .external/onnxruntime/build/
- .external/onnxruntime/.venv/

Resets:
- .external/onnxruntime/ to its checked-out tag (discards local changes like musl patches)
"""

import shutil
import subprocess
from pathlib import Path
from build_utils import info, error, ScriptError

SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent.parent
ONNXRUNTIME_DIR = REPO_ROOT / ".external" / "onnxruntime"

DIRS_TO_DELETE = [
    SCRIPT_DIR / "onnx" / "out",
    SCRIPT_DIR / "speedreader_ort" / "out",
    ONNXRUNTIME_DIR / "build",
    ONNXRUNTIME_DIR / ".venv",
]


def reset_onnxruntime_repo():
    """Reset onnxruntime repo to its current tag, discarding local changes."""
    if not ONNXRUNTIME_DIR.exists():
        return

    # Get current tag
    result = subprocess.run(
        ["git", "describe", "--tags", "--exact-match"],
        cwd=ONNXRUNTIME_DIR,
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        info("onnxruntime not on a tag, skipping reset")
        return

    tag = result.stdout.strip()
    info(f"Resetting onnxruntime to {tag}")

    # Reset to tag and clean untracked files
    subprocess.run(["git", "reset", "--hard", tag], cwd=ONNXRUNTIME_DIR, check=True)
    subprocess.run(["git", "clean", "-fdx"], cwd=ONNXRUNTIME_DIR, check=True)


def main():
    info("Deep cleaning native build artifacts")

    deleted_any = False
    for dir_path in DIRS_TO_DELETE:
        if dir_path.exists():
            shutil.rmtree(dir_path)
            info(f"Deleted {dir_path.relative_to(REPO_ROOT)}")
            deleted_any = True

    reset_onnxruntime_repo()

    if not deleted_any:
        info("Nothing to clean")


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
