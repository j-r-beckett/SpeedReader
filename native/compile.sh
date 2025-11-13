#!/bin/bash
# Copyright (c) 2025 j-r-beckett
# Licensed under the Apache License, Version 2.0

# Creates libspeedreader_ort.so for use with dotnet test
# This is separate from the main build because tests use dynamic linking
# while the final binary uses static linking

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"
ONNX_LIB_DIR="$BUILD_DIR/onnx"

echo "=== Building shared library for testing ==="
echo ""

# Check if wrapper object exists
if [ ! -f "$BUILD_DIR/speedreader_ort.o" ]; then
    echo "ERROR: Wrapper object not found at $BUILD_DIR/speedreader_ort.o"
    echo "Please run ./build.sh first"
    exit 1
fi

# Check if ONNX libraries exist
if [ ! -d "$ONNX_LIB_DIR" ] || [ -z "$(find "$ONNX_LIB_DIR" -name "*.a" 2>/dev/null)" ]; then
    echo "ERROR: ONNX libraries not found in $ONNX_LIB_DIR"
    echo "Please run ./build.sh first"
    exit 1
fi

echo "Building shared library (.so)..."

# Collect all .a files
ONNX_LIBS=$(find "$ONNX_LIB_DIR" -name "*.a")

# Build shared library from wrapper + all ONNX libraries
# Use --start-group/--end-group to handle circular dependencies
g++ -shared -fPIC \
    -o "$BUILD_DIR/libspeedreader_ort.so" \
    "$BUILD_DIR/speedreader_ort.o" \
    -Wl,--start-group \
    $ONNX_LIBS \
    -Wl,--end-group \
    -lstdc++ \
    -lpthread \
    -lm \
    -ldl

SO_SIZE=$(du -h "$BUILD_DIR/libspeedreader_ort.so" | cut -f1)
echo "✓ Created libspeedreader_ort.so ($SO_SIZE)"
echo ""
echo "=== Build Complete ==="
