#!/bin/bash
# Analyze mistakes at session end

set -e

echo "📊 Analyzing mistakes..."
powershell -File ./scripts/analyze_mistakes.ps1

if [ $? -eq 0 ]; then
    echo "✓ Mistake analysis complete"
else
    echo "⚠️ Mistake analysis failed (non-blocking)"
fi

exit 0

# Made with Bob
