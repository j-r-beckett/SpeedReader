#!/bin/bash
# Copy all tracked files in a git repository to clipboard, respecting .gitignore
# Usage: copy-repo.sh [directory] [--include-claude]

# Parse arguments
INCLUDE_CLAUDE=false
DIR="."

while [[ $# -gt 0 ]]; do
  case $1 in
    --include-claude)
      INCLUDE_CLAUDE=true
      shift
      ;;
    -*)
      echo "Unknown option $1"
      exit 1
      ;;
    *)
      DIR="$1"
      shift
      ;;
  esac
done

# Check if the directory exists
if [ ! -d "$DIR" ]; then
  echo "Error: '$DIR' is not a directory."
  exit 1
fi

# Check if we're in a git repository
if ! git -C "$DIR" rev-parse --git-dir > /dev/null 2>&1; then
  echo "Error: '$DIR' is not a git repository."
  exit 1
fi

echo "Processing git repository: $DIR"

# Create a temporary file to store the concatenated contents
TEMP_FILE=$(mktemp)
# Clear the temporary file
> "$TEMP_FILE"

# Get all files tracked by git (respects .gitignore automatically)
git -C "$DIR" ls-files | while read -r FILE; do
  # Skip if file doesn't exist (shouldn't happen with ls-files, but safety check)
  if [ ! -f "$DIR/$FILE" ]; then
    continue
  fi
  
  # Skip excluded files
  if [[ "$FILE" == "copy-repo.sh" || "$FILE" == ".editorconfig" || "$FILE" == "CharacterDictionary.Data.txt" || "$FILE" == ".gitignore" ]]; then
    echo "Skipping excluded file: $FILE"
    continue
  fi
  
  # Skip entire .claude directory
  if [[ "$FILE" == .claude/* ]]; then
    echo "Skipping .claude directory file: $FILE"
    continue
  fi
  
  # Skip CLAUDE.md unless --include-claude is specified
  if [[ "$FILE" == "CLAUDE.md" && "$INCLUDE_CLAUDE" == false ]]; then
    echo "Skipping CLAUDE.md (use --include-claude to include)"
    continue
  fi
  
  # Skip binary files (check if file is text)
  if file "$DIR/$FILE" | grep -q "text\|script\|source"; then
    # File is text, include it
    :
  else
    echo "Skipping binary file: $FILE"
    continue
  fi
  
  # Add a header with the file path
  echo "=== FILE: $FILE ===" >> "$TEMP_FILE"
  echo "" >> "$TEMP_FILE"

  # Add the file contents
  cat "$DIR/$FILE" >> "$TEMP_FILE"

  # Add a separator between files
  echo "" >> "$TEMP_FILE"
  echo "=== END OF FILE ===" >> "$TEMP_FILE"
  echo "" >> "$TEMP_FILE"
  echo "" >> "$TEMP_FILE"
done

# Check if any files were found
if [ ! -s "$TEMP_FILE" ]; then
  echo "No files found in the git repository"
  rm "$TEMP_FILE"
  exit 0
fi

# Copy the contents to clipboard
# This supports multiple platforms
if command -v pbcopy &> /dev/null; then
  # macOS
  cat "$TEMP_FILE" | pbcopy
  echo "Repository files copied to clipboard using pbcopy"
elif command -v xclip &> /dev/null; then
  # Linux with xclip
  cat "$TEMP_FILE" | xclip -selection clipboard
  echo "Repository files copied to clipboard using xclip"
elif command -v xsel &> /dev/null; then
  # Linux with xsel
  cat "$TEMP_FILE" | xsel --clipboard
  echo "Repository files copied to clipboard using xsel"
elif command -v clip.exe &> /dev/null; then
  # Windows with WSL
  cat "$TEMP_FILE" | clip.exe
  echo "Repository files copied to clipboard using clip.exe"
else
  echo "No clipboard command found. Install one of: pbcopy (macOS), xclip/xsel (Linux), or use WSL with clip.exe (Windows)"
  echo "Content saved to: $TEMP_FILE"
  exit 1
fi

# Clean up
rm "$TEMP_FILE"
echo "All repository files have been copied to clipboard with headers."