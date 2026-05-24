#!/bin/bash
# ASCII Compliance Hook
# Blocks Unicode/emoji in src/ files

set -e

TOOL_INPUT=$(cat)
TOOL_NAME=$(echo "$TOOL_INPUT" | jq -r '.tool_name')
FILE_PATH=$(echo "$TOOL_INPUT" | jq -r '.tool_input.file_path // empty')
CONTENT=$(echo "$TOOL_INPUT" | jq -r '.tool_input.content // empty')

# Only check Edit/Create tools on src/ files
if [[ "$TOOL_NAME" =~ ^(Edit|Create)$ ]] && [[ "$FILE_PATH" =~ ^src/ ]]; then
    # Check for non-ASCII characters
    if echo "$CONTENT" | grep -P '[^\x00-\x7F]' > /dev/null; then
        echo "❌ ASCII VIOLATION: Non-ASCII characters detected in $FILE_PATH"
        echo ""
        echo "V12 DNA mandate: ASCII-only in src/ files"
        echo "Remove Unicode/emoji before proceeding"
        exit 2
    fi
    
    echo "✓ ASCII check passed: $FILE_PATH"
fi

exit 0

# Made with Bob
