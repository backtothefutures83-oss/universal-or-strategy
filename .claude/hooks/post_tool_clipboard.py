#!/usr/bin/env python3
"""
V12 Clipboard Mandate Hook
PostToolUse: Fires after any Write tool call targeting implementation_plan.md.
Per CLAUDE.md line 55: All handoff prompts and implementation plans MUST be 
automatically copied to the Director's clipboard.
"""
import sys
import json
import subprocess
import os

def main():
    try:
        hook_input = json.loads(sys.stdin.read())
    except Exception:
        sys.exit(0)

    tool_name = hook_input.get("tool_name", "")
    tool_input = hook_input.get("tool_input", {})

    if tool_name not in ("Write", "Edit", "MultiEdit"):
        sys.exit(0)

    path = (tool_input.get("path")
            or tool_input.get("file_path")
            or tool_input.get("target_file")
            or "")
    normalized = path.replace("\\", "/").lower()

    # Trigger on implementation_plan.md or any handoff artifact
    if "implementation_plan" not in normalized and "handoff" not in normalized:
        sys.exit(0)

    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            content = f.read()
        # Copy to Windows clipboard via PowerShell
        result = subprocess.run(
            ["powershell", "-Command",
             "$input | Set-Clipboard"],
            input=content,
            text=True,
            capture_output=True,
            timeout=5
        )
        if result.returncode == 0:
            print("[V12 CLIPBOARD] implementation_plan.md copied to Director clipboard. (CLAUDE.md mandate)")
        else:
            print("[V12 CLIPBOARD] WARNING: Clipboard copy failed -- " + result.stderr.strip())
    except Exception as e:
        print("[V12 CLIPBOARD] WARNING: Could not copy to clipboard: " + str(e))

    sys.exit(0)

if __name__ == "__main__":
    main()
