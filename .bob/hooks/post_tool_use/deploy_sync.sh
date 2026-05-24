#!/bin/bash
# Auto-run deploy-sync after src/ edits

set -e

TOOL_INPUT=$(cat)
TOOL_NAME=$(echo "$TOOL_INPUT" | jq -r '.tool_name')
FILE_PATH=$(echo "$TOOL_INPUT" | jq -r '.tool_input.file_path // empty')

# Only run deploy-sync after src/ edits
if [[ "$TOOL_NAME" =~ ^(Edit|Create|ApplyPatch)$ ]] && [[ "$FILE_PATH" =~ ^src/ ]]; then
    echo "🔄 Running deploy-sync for $FILE_PATH..."
    powershell -File ./deploy-sync.ps1
    
    if [ $? -eq 0 ]; then
        echo "✓ deploy-sync passed"
    else
        echo "❌ deploy-sync failed"
        exit 2
    fi
fi

exit 0

# Made with Bob
