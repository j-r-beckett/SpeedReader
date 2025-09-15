#!/bin/bash
# Copyright (c) 2025 j-r-beckett
# Licensed under the Apache License, Version 2.0

set -e

# Function to get modification time of all non-gitignored files in root dir
get_modification_hash() {
    git ls-files --cached --others --exclude-standard | xargs -I {} stat -c "%Y %n" {} 2>/dev/null | sort | sha256sum | cut -d' ' -f1
}

# Copyright header for C# files
CS_HEADER="// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0"

# Function to add header to C# files
add_cs_header() {
    local file="$1"
    if ! grep -q "Copyright" "$file"; then
        echo "$CS_HEADER" > "$file.tmp"
        echo "" >> "$file.tmp"
        cat "$file" >> "$file.tmp"
        mv "$file.tmp" "$file"
        git add "$file"
    fi
}

# Run formatter
dotnet format --no-restore

# Add copyright to new files
git diff --cached --name-only --diff-filter=ACM | while read file; do
    if [[ "$file" == *.cs && "$file" != */obj/* ]]; then
        add_cs_header "$file"
    fi
done

BEFORE_TESTS_HASH=$(get_modification_hash)

# Build
if ! dotnet build /warnaserror -v q; then
    echo "Build failed. Commit aborted."
    exit 1
fi

# Test
if ! dotnet test -v q --no-build; then
    echo "Tests failed. Commit aborted."
    exit 1
fi

# Ensure code didn't change while we were building and testing
AFTER_TESTS_HASH=$(get_modification_hash)

if [ "$BEFORE_TESTS_HASH" != "$AFTER_TESTS_HASH" ]; then
    echo "Detected file modification while hook was running. Commit aborted."
    exit 1
fi

git add -u

exit 0
