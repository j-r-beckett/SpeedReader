#!/bin/bash

# Usage: ./git_tag_manager.sh <repo_url> <target_dir> <tag>
# Example: ./git_tag_manager.sh https://github.com/user/repo.git ./my-repo v1.0.0

set -e

if [ "$#" -ne 3 ]; then
    echo "Usage: $0 <repo_url> <target_dir> <tag>"
    exit 1
fi

REPO_URL="$1"
TARGET_DIR="$2"
TAG="$3"

# Check if .git directory exists
if [ ! -d "$TARGET_DIR/.git" ]; then
    echo "Repository not found. Cloning with tag $TAG..."
    git clone --branch "$TAG" --depth 1 --recursive "$REPO_URL" "$TARGET_DIR"
    echo "✓ Cloned repository and checked out tag $TAG"
else
    echo "Repository exists. Checking current tag..."
    cd "$TARGET_DIR"

    # Get the current tag (if we're on one)
    CURRENT_TAG=$(git describe --tags --exact-match 2>/dev/null || echo "")

    if [ "$CURRENT_TAG" = "$TAG" ]; then
        echo "✓ Already on tag $TAG. Nothing to do."
    else
        echo "Current state: ${CURRENT_TAG:-'not on a tag'}"
        echo "Fetching tags and checking out $TAG..."
        git fetch --tags
        git checkout "tags/$TAG"
        git submodule update --init --recursive
        echo "✓ Checked out tag $TAG"
    fi
fi
