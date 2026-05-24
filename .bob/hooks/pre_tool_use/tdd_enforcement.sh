#!/bin/bash
# TDD Enforcement Hook
# Blocks src/ edits without corresponding tests

set -e

# Read tool input from stdin
TOOL_INPUT=$(cat)

# Extract tool name and file path
TOOL_NAME=$(echo "$TOOL_INPUT" | jq -r '.tool_name')
FILE_PATH=$(echo "$TOOL_INPUT" | jq -r '.tool_input.file_path // empty')

# Only check Edit/Create/ApplyPatch tools on src/ files
if [[ "$TOOL_NAME" =~ ^(Edit|Create|ApplyPatch)$ ]] && [[ "$FILE_PATH" =~ ^src/ ]]; then
    # Extract feature name from file path
    FEATURE=$(basename "$FILE_PATH" .cs)
    
    # Check if test exists
    TEST_FILE="tests/V12_Performance.Tests/${FEATURE}Tests.cs"
    
    if [ ! -f "$TEST_FILE" ]; then
        echo "❌ TDD VIOLATION: No test file found for $FILE_PATH"
        echo "Expected: $TEST_FILE"
        echo ""
        echo "TDD RED phase required:"
        echo "1. Create test file: $TEST_FILE"
        echo "2. Write failing test"
        echo "3. Verify test FAILS"
        echo "4. Then implement feature"
        exit 2
    fi
    
    echo "✓ TDD check passed: Test file exists at $TEST_FILE"
fi

exit 0

# Made with Bob
