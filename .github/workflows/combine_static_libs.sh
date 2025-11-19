#!/bin/bash

# Usage: ./combine_static_libs.sh <libs_path> <output_path>
# Example: ./combine_static_libs.sh ./build/libs ./build/combined.a

set -e

if [ "$#" -ne 2 ]; then
    echo "Usage: $0 <libs_path> <output_path>"
    exit 1
fi

LIBS_PATH="$1"
OUTPUT_PATH="$2"
MRI_SCRIPT="$(dirname "$OUTPUT_PATH")/combine.mri"

# Check if libs_path exists
if [ ! -d "$LIBS_PATH" ]; then
    echo "Error: Libraries path '$LIBS_PATH' does not exist"
    exit 1
fi

echo "Searching for static libraries in $LIBS_PATH..."

# Count libraries to be combined
LIB_COUNT=$(find "$LIBS_PATH" -path "*/Release/*" -name "*.a" -type f | wc -l)

if [ "$LIB_COUNT" -eq 0 ]; then
    echo "Error: No .a files found in Release subdirectories"
    exit 1
fi

echo "Found $LIB_COUNT static libraries to combine"

# Create MRI script
echo "Creating AR MRI script..."
echo "CREATE $OUTPUT_PATH" > "$MRI_SCRIPT"

find "$LIBS_PATH" -path "*/Release/*" -name "*.a" -type f | while read -r lib; do
    echo "ADDLIB $lib" >> "$MRI_SCRIPT"
done

echo "SAVE" >> "$MRI_SCRIPT"
echo "END" >> "$MRI_SCRIPT"

# Run ar with the script
echo "Combining libraries into $OUTPUT_PATH..."
ar -M < "$MRI_SCRIPT"

# Clean up
rm "$MRI_SCRIPT"

echo "✓ Successfully combined $LIB_COUNT libraries into $OUTPUT_PATH"
