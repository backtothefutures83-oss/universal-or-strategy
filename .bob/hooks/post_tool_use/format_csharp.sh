#!/bin/bash
# Auto-format C# files after Edit/Create

set -e

TOOL_INPUT=$(cat)
TOOL_NAME=$(echo "$TOOL_INPUT" | jq -r '.tool_name')
FILE_PATH=$(echo "$TOOL_INPUT" | jq -r '.tool_input.file_path // empty')

# Only format C# files after Edit/Create
if [[ "$TOOL_NAME" =~ ^(Edit|Create)$ ]] && [[ "$FILE_PATH" =~ \.cs$ ]]; then
    echo "🔧 Auto-formatting $FILE_PATH..."
    dotnet format "$FILE_PATH" --no-restore 2>/dev/null || true
    echo "✓ Format complete"
fi

exit 0

# Made with Bob
