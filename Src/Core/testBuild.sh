#!/bin/bash

echo "Building and testing Core project..."

# Check if Docker daemon is running with timeout
if ! timeout 5s docker info >/dev/null 2>&1; then
  echo "Error: Docker daemon is not running or not responding"
  exit 1
fi

# Publish the Core project first
echo "Publishing Wheft Core project..."
cd /home/jimmy/Wheft
dotnet publish Src/Core/Core.csproj -c Release
if [ $? -ne 0 ]; then
  echo "Error: Failed to publish Core project"
  exit 1
fi

# Build Docker image
echo "Building Docker image..."
cd Src/Core
docker build -t wheft-test:latest --progress plain .
if [ $? -ne 0 ]; then
  echo "Error: Failed to build Docker image"
  exit 1
fi

# Create container
echo "Creating container..."
CONTAINER_ID=$(docker create wheft-test:latest)
if [ -z "$CONTAINER_ID" ]; then
  echo "Error: Failed to create container"
  exit 1
fi

# Start container in detached mode
echo "Starting container..."
docker start "$CONTAINER_ID" > /dev/null
if [ $? -ne 0 ]; then
  echo "Error: Failed to start container"
  docker rm "$CONTAINER_ID" > /dev/null
  exit 1
fi

# Copy wheft executable into container
echo "Copying wheft executable into container..."
docker cp bin/Release/net10.0/linux-x64/publish/wheft "$CONTAINER_ID":/app/wheft
#docker cp bin/Release/net10.0/linux-x64/native/wheft "$CONTAINER_ID":/app/wheft
if [ $? -ne 0 ]; then
  echo "Error: Failed to copy wheft executable into container"
  docker rm "$CONTAINER_ID" > /dev/null
  exit 1
fi

# Make wheft executable
docker exec "$CONTAINER_ID" chmod +x /app/wheft

# Copy test image into container
echo "Copying hello.jpg into container..."
docker cp hello.jpg "$CONTAINER_ID":/app/hello.jpg
if [ $? -ne 0 ]; then
  echo "Error: Failed to copy hello.jpg into container"
  docker rm "$CONTAINER_ID" > /dev/null
  exit 1
fi

# Run wheft binary and capture output
echo "Running wheft binary..."
OUTPUT=$(docker exec "$CONTAINER_ID" /app/wheft hello.jpg output.png 2>&1)
EXIT_CODE=$?

echo "=== Wheft Output ==="
echo "$OUTPUT"
echo "===================="

# Clean up container
docker stop "$CONTAINER_ID" > /dev/null
docker rm "$CONTAINER_ID" > /dev/null

# Verify output
if [ $EXIT_CODE -ne 0 ]; then
  echo "Error: wheft binary failed with exit code $EXIT_CODE"
  exit 1
fi

# Check for expected output patterns (case insensitive)
echo "Verifying output..."

if echo "$OUTPUT" | grep -qi "detected 1 text regions"; then
  echo "✓ Found 'Detected 1 text regions'"
else
  echo "✗ Missing 'Detected 1 text regions'"
  exit 1
fi

if echo "$OUTPUT" | grep -qi "recognized 1 text segments"; then
  echo "✓ Found 'Recognized 1 text segments'"
else
  echo "✗ Missing 'Recognized 1 text segments'"
  exit 1
fi

if echo "$OUTPUT" | grep -qi "hello"; then
  echo "✓ Found 'HELLO'"
else
  echo "✗ Missing 'HELLO'"
  exit 1
fi

echo "All tests passed!"
