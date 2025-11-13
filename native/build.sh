#!/bin/bash
# Copyright (c) 2025 j-r-beckett
# Licensed under the Apache License, Version 2.0

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"

echo "=== Building ONNX Runtime + SpeedReader Wrapper ==="
echo ""

# Build Docker image (targets wrapper-builder stage)
echo "Building Docker image (this will take ~45-60 minutes on first run)..."
docker build --target wrapper-builder -t speedreader-onnx-builder "$SCRIPT_DIR"
echo ""

# Extract artifacts from Docker
echo "Extracting build artifacts..."
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Create temporary container
CONTAINER_ID=$(docker create speedreader-onnx-builder)

# Copy wrapper object file
docker cp "$CONTAINER_ID:/build/wrapper/speedreader_ort.o" "$BUILD_DIR/speedreader_ort.o"

# Copy ONNX libraries
docker cp "$CONTAINER_ID:/build/onnxruntime/build/Linux/Release/." "$BUILD_DIR/onnx/"

# Remove temporary container
docker rm "$CONTAINER_ID" > /dev/null

# Keep only .a files in onnx directory
cd "$BUILD_DIR/onnx"
find . -type f ! -name "*.a" -delete
find . -type d -empty -delete
cd "$SCRIPT_DIR"

NUM_LIBS=$(find "$BUILD_DIR/onnx" -name "*.a" | wc -l)

echo ""
echo "=== Build Complete ==="
echo "Wrapper object: $BUILD_DIR/speedreader_ort.o"
echo "ONNX libraries: $NUM_LIBS static libraries in $BUILD_DIR/onnx/"
ls -lh "$BUILD_DIR/speedreader_ort.o"
