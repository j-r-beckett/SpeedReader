#!/bin/bash

echo "Downloading FFmpeg binaries..."

# Require outDir parameter
if [ -z "$1" ]; then
  echo "Error: outDir parameter is required"
  exit 1
fi

OUT_DIR="$1"
FFMPEG_URL="https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-lgpl.tar.xz"
TEMP_DIR=$(mktemp -d)

# Download FFmpeg archive
echo "Downloading from $FFMPEG_URL..."
if ! curl -L -o "$TEMP_DIR/ffmpeg.tar.xz" "$FFMPEG_URL"; then
  echo "Error: Failed to download FFmpeg archive"
  rm -rf "$TEMP_DIR"
  exit 1
fi

# Extract archive
echo "Extracting archive..."
if ! tar -xf "$TEMP_DIR/ffmpeg.tar.xz" -C "$TEMP_DIR"; then
  echo "Error: Failed to extract FFmpeg archive"
  rm -rf "$TEMP_DIR"
  exit 1
fi

# Find the extracted directory (it has a version-specific name)
EXTRACTED_DIR=$(find "$TEMP_DIR" -maxdepth 1 -type d -name "ffmpeg-*" | head -1)
if [ -z "$EXTRACTED_DIR" ]; then
  echo "Error: Could not find extracted FFmpeg directory"
  rm -rf "$TEMP_DIR"
  exit 1
fi

# Create output directory
mkdir -p "$OUT_DIR"

# Copy binaries
echo "Copying binaries to $OUT_DIR..."
if [ -f "$EXTRACTED_DIR/bin/ffmpeg" ]; then
  cp "$EXTRACTED_DIR/bin/ffmpeg" "$OUT_DIR/ffmpeg"
  chmod +x "$OUT_DIR/ffmpeg"
  echo "Copied ffmpeg"
else
  echo "Error: ffmpeg binary not found in archive"
  rm -rf "$TEMP_DIR"
  exit 1
fi

if [ -f "$EXTRACTED_DIR/bin/ffprobe" ]; then
  cp "$EXTRACTED_DIR/bin/ffprobe" "$OUT_DIR/ffprobe"
  chmod +x "$OUT_DIR/ffprobe"
  echo "Copied ffprobe"
else
  echo "Error: ffprobe binary not found in archive"
  rm -rf "$TEMP_DIR"
  exit 1
fi

# Clean up
rm -rf "$TEMP_DIR"

echo "FFmpeg binaries downloaded successfully to $OUT_DIR"