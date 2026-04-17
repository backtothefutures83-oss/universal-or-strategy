#!/usr/bin/env python3
"""
V12 Protocol Hook: PLAN-ONLY src/ Guard
Fires on PreToolUse for Write/Edit/MultiEdit tools.
Blocks any write to src/ and injects the protocol reminder.
Hook output on stdout = message shown to Claude before execution.
Exit code 2 = blocking error (aborts the tool call).
Exit code 0 = allow.
"""
import sys
import json
import os

def main():
    try:
        hook_input = json.loads(sys.stdin.read())
    except Exception:
        sys.exit(0)  # Can't parse -- allow

    tool_name = hook_input.get("tool_name", "")
    tool_input = hook_input.get("tool_input", {})

    # Only intercept file-write tools
    if tool_name not in ("Write", "Edit", "MultiEdit", "str_replace_editor"):
        sys.exit(0)

    # Get the file path being written
    path = tool_input.get("path", tool_input.get("file_path", ""))
    if not path:
        # MultiEdit uses a different key
        path = tool_input.get("target_file", "")

    if not path:
        sys.exit(0)

    # Check for Director's Override
    if os.environ.get("V12_OVERRIDE_HOOK") == "true":
        sys.exit(0)

    # Normalize to forward slashes and check for src/ directory
    normalized = path.replace("\\", "/")
    if "/src/" in normalized or normalized.startswith("src/"):
        msg = (
            "\n[V12 PROTOCOL BLOCK] You attempted to write to src/ directly.\n"
            "Per V12 Director's Gate hierarchy, Claude (ARCHITECT/P3) is BANNED from writing to src/.\n"
            "ALL code must be embedded inside docs/brain/implementation_plan.md.\n"
            "The ENGINEER (Codex/P4) applies the plan after Director approval.\n\n"
            "DIRECTOR OVERRIDE: If you have been explicitly granted execution permission\n"
            "for surgical patches, set env var: V12_OVERRIDE_HOOK=true\n\n"
            "ACTION REQUIRED:\n"
            "  1. Save your plan to docs/brain/implementation_plan.md instead.\n"
            "  2. Include all code blocks fully embedded (no pseudocode).\n"
            "  3. End with the Director's Handoff Block.\n"
            "  4. Type: 'Plan saved. Awaiting Director approval.'\n"
        )
        print(msg)
        sys.exit(2)  # Blocking error

    sys.exit(0)

if __name__ == "__main__":
    main()
