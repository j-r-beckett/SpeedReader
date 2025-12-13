#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click"]
# ///

"""
Get Docker Container ID from inside a container and print debug command.

Usage:
    ./get-container-id.py   # Print debug command for attaching to container
"""

import re
import sys
from pathlib import Path

import click

MOUNTINFO_PATH = Path("/proc/self/mountinfo")
CONTAINER_ID_PATTERN = re.compile(r"/var/lib/docker/containers/([a-f0-9]{64})/")


def get_container_id() -> str | None:
    """Extract container ID from /proc/self/mountinfo."""
    if not MOUNTINFO_PATH.exists():
        return None

    content = MOUNTINFO_PATH.read_text()
    match = CONTAINER_ID_PATTERN.search(content)
    return match.group(1) if match else None


@click.command()
def main():
    """Print debug command for attaching to the current Docker container."""
    container_id = get_container_id()

    if not container_id:
        click.echo(
            "ERROR: Could not extract container ID from /proc/self/mountinfo",
            err=True,
        )
        click.echo("This script must be run from inside a Docker container", err=True)
        sys.exit(1)

    short_id = container_id[:12]
    click.echo(f"Debug: docker start {short_id} && docker exec -it {short_id} /bin/bash")


if __name__ == "__main__":
    main()
