#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../build_utils", editable = true }
# ///

"""
Run GitHub Actions workflows locally using act.

Usage:
    ./act.py static              # Run static job
    ./act.py dynamic             # Run dynamic job
    ./act.py static --clean      # Clean containers/volumes first, then run
"""

import re
import shutil
from pathlib import Path

import click

from build_utils import ScriptError, bash, error, info

SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
DEFAULT_ARTIFACTS_PATH = REPO_ROOT / ".act-artifacts"


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


JOB_NAMES = {
    "static": "Build SpeedReader (Static/musl)",
    "dynamic": "Build SpeedReader (Dynamic)",
}


def find_act_containers(workflow_name: str, job_name: str) -> list[str]:
    """Find Docker containers created by act for a given job."""
    sanitized = sanitize_for_container_name(f"act-{workflow_name}-{job_name}")
    result = bash(
        f"docker ps -a --format '{{{{.Names}}}}' | grep -E '^{sanitized}' || true",
        directory=REPO_ROOT,
    )
    containers = [c.strip() for c in result.strip().splitlines() if c.strip()]
    return containers


def find_act_volumes(workflow_name: str, job_name: str) -> list[str]:
    """Find Docker volumes created by act for a given job."""
    sanitized = sanitize_for_container_name(f"act-{workflow_name}-{job_name}")
    result = bash(
        f"docker volume ls --format '{{{{.Name}}}}' | grep -E '^{sanitized}' || true",
        directory=REPO_ROOT,
    )
    volumes = [v.strip() for v in result.strip().splitlines() if v.strip()]
    return volumes


def clean_job(workflow_name: str, job_name: str):
    """Remove all containers and volumes for a job."""
    info(f"Cleaning containers and volumes for job: {job_name}")

    containers = find_act_containers(workflow_name, job_name)
    if containers:
        info(f"Removing {len(containers)} container(s)")
        for container in containers:
            bash(f"docker rm -f {container}", directory=REPO_ROOT)
    else:
        info("No containers to remove")

    volumes = find_act_volumes(workflow_name, job_name)
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


WORKFLOW_FILE = WORKFLOWS_DIR / "build.yml"


@click.command()
@click.argument("job", type=click.Choice(["static", "dynamic"]))
@click.option("--clean", is_flag=True, help="Clean containers/volumes before running")
def main(job: str, clean: bool):
    """Run a GitHub Actions job locally using act.

    JOB is the job to run (static or dynamic).
    """
    workflow_name = get_workflow_name(WORKFLOW_FILE)
    artifacts_path = DEFAULT_ARTIFACTS_PATH / job

    job_name = JOB_NAMES[job]
    info(f"Workflow: {workflow_name}, Job: {job_name}")

    if clean:
        clean_job(workflow_name, job_name)

    # Always clear and recreate artifacts directory
    clear_artifacts(artifacts_path)

    # Build act command
    cmd_parts = [
        "act",
        "-r",  # Always reuse containers
        f"-W {WORKFLOW_FILE}",
        f"--artifact-server-path {artifacts_path}",
        f"-j {job}",
    ]

    cmd = " ".join(cmd_parts)

    info(f"Running: {cmd}")
    bash(cmd, directory=REPO_ROOT)


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
