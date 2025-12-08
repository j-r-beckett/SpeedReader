#!/bin/bash
#
# Get Docker Container ID from inside a container
#
# Usage:
#   ./get-container-id.sh           # Full 64-char container ID
#   ./get-container-id.sh --short   # Short 12-char container ID
#

set -euo pipefail

# Extract container ID from mountinfo
# Looks for: /var/lib/docker/containers/<64-hex-chars>/
# Uses sed instead of grep -P for POSIX/BusyBox compatibility
# The `|| true` handles SIGPIPE when head closes the pipe early
CONTAINER_ID=$(sed -n 's|.*/var/lib/docker/containers/\([a-f0-9]\{64\}\)/.*|\1|p' /proc/self/mountinfo | head -n1 || true)

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
