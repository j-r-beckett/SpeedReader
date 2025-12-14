#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../build_utils", editable = true }
# ///

import tarfile
import urllib.request
from pathlib import Path
from build_utils import ScriptError, info, error

BASE_URL = "https://jimmybeckett.com/speedreader/datasets"


def download_dataset(dataset_name: str, datasets_dir: Path | None = None) -> Path:
    """
    Download and extract a dataset from VPS file server.

    Args:
        dataset_name: Name of the dataset (e.g., 'icdar2015')
        datasets_dir: Directory to extract dataset to (defaults to repo_root/datasets)

    Returns:
        Path to the extracted dataset directory
    """
    if datasets_dir is None:
        repo_root = Path(__file__).parent.parent.resolve()
        datasets_dir = repo_root / "datasets"

    dataset_dir = datasets_dir / dataset_name

    # Check if dataset already exists
    if dataset_dir.exists() and any(dataset_dir.iterdir()):
        info(f"Dataset '{dataset_name}' already exists at {dataset_dir}")
        return dataset_dir

    # Download archive
    archive_name = f"{dataset_name}.tar.gz"
    archive_url = f"{BASE_URL}/{archive_name}"
    archive_path = datasets_dir / archive_name

    datasets_dir.mkdir(parents=True, exist_ok=True)

    info(f"Downloading {dataset_name} from {archive_url}")
    try:
        urllib.request.urlretrieve(archive_url, archive_path)
    except Exception as e:
        raise ScriptError(f"Failed to download dataset: {e}")

    if not archive_path.exists():
        raise ScriptError(f"Downloaded archive not found at {archive_path}")

    # Extract archive
    info(f"Extracting {archive_name} to {datasets_dir}")
    try:
        with tarfile.open(archive_path, "r:gz") as tar:
            tar.extractall(datasets_dir)
    except Exception as e:
        raise ScriptError(f"Failed to extract dataset: {e}")

    # Clean up archive
    archive_path.unlink()
    info(f"Removed archive {archive_name}")

    if not dataset_dir.exists():
        raise ScriptError(f"Dataset directory not found at {dataset_dir} after extraction")

    info(f"Successfully downloaded dataset to {dataset_dir}")
    return dataset_dir


if __name__ == "__main__":
    import click

    @click.command()
    @click.argument("dataset_name")
    def main(dataset_name: str):
        """Download a dataset from VPS file server to datasets/ directory."""
        try:
            download_dataset(dataset_name)
        except ScriptError as e:
            error(f"Fatal: {e}")

    main()
