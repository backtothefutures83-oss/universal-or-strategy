#!/bin/bash
# load_memory.sh - Auto-load persistent memory at session start
#
# Purpose: Load MEMORY.md and USER.md into system prompt at session start
# Inspired by Hermes Agent's memory system
#
# This hook runs automatically when a Bob CLI session starts.
# It injects memory content into the system prompt so the agent
# remembers V12 DNA, project structure, user preferences, and past mistakes.

set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
MEMORY_FILE="$REPO_ROOT/docs/brain/MEMORY.md"
USER_FILE="$REPO_ROOT/docs/brain/USER.md"

echo "[load_memory] Loading persistent memory..."

# Check if memory files exist
if [ ! -f "$MEMORY_FILE" ]; then
    echo "[load_memory] WARNING: MEMORY.md not found at $MEMORY_FILE"
    echo "[load_memory] Skipping memory load"
    exit 0
fi

if [ ! -f "$USER_FILE" ]; then
    echo "[load_memory] WARNING: USER.md not found at $USER_FILE"
    echo "[load_memory] Skipping user profile load"
    exit 0
fi

# Memory files exist - they will be auto-loaded by Bob CLI
# The actual injection happens in the Bob CLI harness, not here
# This hook just verifies the files exist and logs the load

echo "[load_memory] ✓ MEMORY.md found ($(wc -l < "$MEMORY_FILE") lines)"
echo "[load_memory] ✓ USER.md found ($(wc -l < "$USER_FILE") lines)"
echo "[load_memory] Memory will be injected into system prompt"

# Optional: Show memory summary
if command -v head &> /dev/null; then
    echo ""
    echo "[load_memory] Memory Summary:"
    echo "  V12 DNA: Lock-free, ASCII-only, CYC ≤15, 0B allocation, <300μs"
    echo "  Project: universal-or-strategy (C# .NET 8, NinjaTrader 8)"
    echo "  User: Mohammed Khalid (Director, PowerShell, Bob CLI primary)"
    echo ""
fi

exit 0

# Made with Bob
