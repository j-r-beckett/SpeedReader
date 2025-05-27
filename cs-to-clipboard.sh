#!/bin/bash
# Check if at least one directory was provided as an argument
if [ $# -eq 0 ]; then
  echo "Usage: $0 <directory1> [directory2] [directory3] ..."
  exit 1
fi
# Create a temporary file to store the concatenated contents
TEMP_FILE=$(mktemp)
# Clear the temporary file
> "$TEMP_FILE"
# Process each directory provided as an argument
for DIR in "$@"; do
  if [ ! -d "$DIR" ]; then
    echo "Warning: '$DIR' is not a directory. Skipping."
    continue
  fi

  echo "Processing directory: $DIR"

  # Find all C# files recursively and process them, ignoring .claude directory
  find "$DIR" -type f -name "*.cs" -not -path "*/.claude/*" | while read -r FILE; do
  # Add a header with the file path
  echo "=== FILE: $FILE ===" >> "$TEMP_FILE"
  echo "" >> "$TEMP_FILE"

  # Add the file contents
  cat "$FILE" >> "$TEMP_FILE"

  # Add a separator between files
  echo "" >> "$TEMP_FILE"
  echo "=== END OF FILE ===" >> "$TEMP_FILE"
  echo "" >> "$TEMP_FILE"
  echo "" >> "$TEMP_FILE"
  done
done
# Check if any files were found
if [ ! -s "$TEMP_FILE" ]; then
  echo "No C# files found in the specified directories"
  rm "$TEMP_FILE"
  exit 0
fi
# Copy the contents to clipboard
# This supports multiple platforms
if command -v pbcopy &> /dev/null; then
  # macOS
  cat "$TEMP_FILE" | pbcopy
  echo "C# files copied to clipboard using pbcopy"
elif command -v xclip &> /dev/null; then
  # Linux with xclip
  cat "$TEMP_FILE" | xclip -selection clipboard
  echo "C# files copied to clipboard using xclip"
elif command -v xsel &> /dev/null; then
  # Linux with xsel
  cat "$TEMP_FILE" | xsel --clipboard
  echo "C# files copied to clipboard using xsel"
elif command -v clip.exe &> /dev/null; then
  # Windows with WSL
  cat "$TEMP_FILE" | clip.exe
  echo "C# files copied to clipboard using clip.exe"
else
  echo "No clipboard command found. Install one of: pbcopy (macOS), xclip/xsel (Linux), or use WSL with clip.exe (Windows)"
  echo "Content saved to: $TEMP_FILE"
  exit 1
fi
# Clean up
rm "$TEMP_FILE"
echo "All C# files have been copied to clipboard with headers."
