#!/bin/bash
set -e

# Build script for ONNX Runtime static library extraction
# This script builds the ONNX Runtime Docker image and extracts static libraries

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build/onnx"

echo "Building ONNX Runtime Docker image..."
docker build -t speedreader-onnx-builder "$SCRIPT_DIR"

echo "Extracting static libraries..."
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Create temporary container
CONTAINER_ID=$(docker create speedreader-onnx-builder)

# Copy build artifacts
docker cp "$CONTAINER_ID:/build/onnxruntime/build/Linux/Release/." "$BUILD_DIR/"

# Remove temporary container
docker rm "$CONTAINER_ID" > /dev/null

# Keep only .a files, remove everything else
cd "$BUILD_DIR"
find . -type f ! -name "*.a" -delete
find . -type d -empty -delete

# Report results
NUM_LIBS=$(find . -name "*.a" | wc -l)
TOTAL_SIZE=$(du -sh . | cut -f1)

echo "Extraction complete:"
echo "  - $NUM_LIBS static libraries"
echo "  - $TOTAL_SIZE total size"
echo "  - Location: $BUILD_DIR"
