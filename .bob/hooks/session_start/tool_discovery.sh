#!/bin/bash
# Auto-discover tools at session start

set -e

echo "🔍 Discovering tools..."
powershell -File ./scripts/discover_tools.ps1

if [ $? -eq 0 ]; then
    echo "✓ Tool discovery complete"
else
    echo "⚠️ Tool discovery failed (non-blocking)"
fi

exit 0

# Made with Bob
