#!/bin/bash
#
# Get Docker Container ID from inside a container
#
# Works by parsing /proc/self/mountinfo which contains mount paths like:
#   /var/lib/docker/containers/<CONTAINER_ID>/resolv.conf
#
# This works in act containers where traditional methods fail:
#   - hostname returns the host machine name (e.g., "desktop")
#   - /proc/self/cgroup shows "/" or "0::/" (cgroups v2)
#   - /proc/1/cpuset shows "/"
#
# Usage:
#   ./get-container-id-final.sh           # Full 64-char container ID
#   ./get-container-id-final.sh --short   # Short 12-char container ID
#

set -euo pipefail

# Extract container ID from mountinfo
# Looks for: /var/lib/docker/containers/<64-hex-chars>/
CONTAINER_ID=$(cat /proc/self/mountinfo | grep -oP '/var/lib/docker/containers/\K[a-f0-9]{64}(?=/)' | head -n1)

if [ -z "$CONTAINER_ID" ]; then
    echo "ERROR: Could not extract container ID from /proc/self/mountinfo" >&2
    echo "This script must be run from inside a Docker container" >&2
    exit 1
fi

# Output short or full ID
if [ "${1:-}" = "--short" ] || [ "${1:-}" = "-s" ]; then
    echo "${CONTAINER_ID:0:12}"
else
    echo "$CONTAINER_ID"
fi
