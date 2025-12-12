#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "utils"]
#
# [tool.uv.sources]
# utils = { path = "./utils", editable = true }
# ///

"""
Run GitHub Actions workflows locally using act.

Usage:
    ./act.py static              # Run static.yml workflow
    ./act.py static --clean      # Clean containers/volumes first, then run
    ./act.py static -j build-onnx-musl  # Run specific job
    ./act.py dynamic --clean -j build-onnx  # Clean and run specific job
"""

import json
import re
import shutil
from pathlib import Path

import click

from utils import ScriptError, bash, error, info

SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
DEFAULT_ARTIFACTS_PATH = REPO_ROOT / ".act-artifacts"


def get_workflow_file(workflow: str) -> Path:
    """Resolve workflow name to file path."""
    # Try exact match first
    workflow_file = WORKFLOWS_DIR / workflow
    if workflow_file.exists():
        return workflow_file

    # Try with .yml extension
    workflow_file = WORKFLOWS_DIR / f"{workflow}.yml"
    if workflow_file.exists():
        return workflow_file

    # Try with .yaml extension
    workflow_file = WORKFLOWS_DIR / f"{workflow}.yaml"
    if workflow_file.exists():
        return workflow_file

    raise ScriptError(
        f"Workflow '{workflow}' not found. "
        f"Available workflows: {', '.join(f.stem for f in WORKFLOWS_DIR.glob('*.yml'))}"
    )


def get_workflow_name(workflow_file: Path) -> str:
    """Extract workflow name from workflow file."""
    content = workflow_file.read_text()
    for line in content.splitlines():
        if line.startswith("name:"):
            # Handle both "name: Foo" and "name: 'Foo'" formats
            name = line.split(":", 1)[1].strip().strip("'\"")
            return name
    # Fall back to filename without extension
    return workflow_file.stem


def sanitize_for_container_name(name: str) -> str:
    """Sanitize a string for use in container names (mimics act's logic)."""
    # Replace non-alphanumeric with dash
    sanitized = re.sub(r"[^a-zA-Z0-9]", "-", name)
    # Collapse multiple dashes
    sanitized = re.sub(r"--+", "-", sanitized)
    return sanitized


def find_act_containers(workflow_name: str) -> list[str]:
    """Find Docker containers created by act for a given workflow."""
    sanitized = sanitize_for_container_name(f"act-{workflow_name}")
    result = bash(
        f"docker ps -a --format '{{{{.Names}}}}' | grep -E '^{sanitized}' || true",
        directory=REPO_ROOT,
    )
    containers = [c.strip() for c in result.strip().splitlines() if c.strip()]
    return containers


def find_act_volumes(workflow_name: str) -> list[str]:
    """Find Docker volumes created by act for a given workflow."""
    sanitized = sanitize_for_container_name(f"act-{workflow_name}")
    result = bash(
        f"docker volume ls --format '{{{{.Name}}}}' | grep -E '^{sanitized}' || true",
        directory=REPO_ROOT,
    )
    volumes = [v.strip() for v in result.strip().splitlines() if v.strip()]
    return volumes


def clean_workflow(workflow_name: str):
    """Remove all containers and volumes for a workflow."""
    info(f"Cleaning containers and volumes for workflow: {workflow_name}")

    containers = find_act_containers(workflow_name)
    if containers:
        info(f"Removing {len(containers)} container(s)")
        for container in containers:
            bash(f"docker rm -f {container}", directory=REPO_ROOT)
    else:
        info("No containers to remove")

    volumes = find_act_volumes(workflow_name)
    if volumes:
        info(f"Removing {len(volumes)} volume(s)")
        for volume in volumes:
            bash(f"docker volume rm -f {volume}", directory=REPO_ROOT)
    else:
        info("No volumes to remove")


def clear_artifacts(artifacts_path: Path):
    """Clear the artifacts directory."""
    if artifacts_path.exists():
        info(f"Clearing artifacts directory: {artifacts_path}")
        shutil.rmtree(artifacts_path)
    artifacts_path.mkdir(parents=True, exist_ok=True)


@click.command()
@click.argument("workflow")
@click.option("--clean", is_flag=True, help="Clean containers/volumes before running")
@click.option("-j", "--job", "job_id", help="Run a specific job ID")
@click.option(
    "--artifacts-path",
    type=click.Path(path_type=Path),
    default=DEFAULT_ARTIFACTS_PATH,
    help="Path for artifact storage",
)
def main(workflow: str, clean: bool, job_id: str | None, artifacts_path: Path):
    """Run a GitHub Actions workflow locally using act.

    WORKFLOW is the name of the workflow file (e.g., 'static' or 'static.yml').
    """
    workflow_file = get_workflow_file(workflow)
    workflow_name = get_workflow_name(workflow_file)

    info(f"Workflow: {workflow_name} ({workflow_file.name})")

    if clean:
        clean_workflow(workflow_name)

    # Always clear and recreate artifacts directory
    clear_artifacts(artifacts_path)

    # Build act command
    cmd_parts = [
        "act",
        "-r",  # Always reuse containers
        f"-W {workflow_file}",
        f"--artifact-server-path {artifacts_path}",
    ]

    if job_id:
        cmd_parts.append(f"-j {job_id}")

    cmd = " ".join(cmd_parts)

    info(f"Running: {cmd}")
    bash(cmd, directory=REPO_ROOT)


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
