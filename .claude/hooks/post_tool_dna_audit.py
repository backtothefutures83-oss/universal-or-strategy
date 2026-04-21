#!/usr/bin/env python3
"""
V12 Post-Tool DNA Audit Hook
PostToolUse: Fires after any Write/Edit to src/ files.
Runs two mandatory scans per CLAUDE.md Section: ASCII gate + lock(stateLock) gate.
Outputs warnings to Claude's context if violations found.
Exit code 2 = show blocking warning (soft -- does not abort since edit already happened).
Exit code 0 = clean.
"""
import sys
import json
import os
import re
import subprocess

def main():
    try:
        hook_input = json.loads(sys.stdin.read())
    except Exception:
        sys.exit(0)

    tool_name = hook_input.get("tool_name", "")
    tool_input = hook_input.get("tool_input", {})

    if tool_name not in ("Write", "Edit", "MultiEdit", "str_replace_editor"):
        sys.exit(0)

    path = (tool_input.get("path")
            or tool_input.get("file_path")
            or tool_input.get("target_file")
            or "")
    normalized = path.replace("\\", "/")
    if "/src/" not in normalized and not normalized.startswith("src/"):
        sys.exit(0)

    violations = []

    # --- ASCII Gate ---
    try:
        with open(path, "rb") as f:
            content = f.read()
        non_ascii_lines = []
        for i, line in enumerate(content.split(b"\n"), 1):
            # Skip comment lines
            stripped = line.strip()
            if stripped.startswith(b"//") or stripped.startswith(b"*"):
                continue
            if any(b >= 0x80 for b in line):
                non_ascii_lines.append(i)
        if non_ascii_lines:
            violations.append(
                "[ASCII GATE FAIL] Non-ASCII bytes found in C# string context at lines: "
                + ", ".join(str(l) for l in non_ascii_lines[:10])
                + (" (+ more)" if len(non_ascii_lines) > 10 else "")
                + "\n  -> Replace with ASCII substitutes per CLAUDE.md: (!) not emoji, -- not em-dash, -> not arrow"
            )
    except Exception as e:
        violations.append("[ASCII GATE] Could not scan file: " + str(e))

    # --- Lock Gate ---
    try:
        lock_matches = []
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for i, line in enumerate(f, 1):
                stripped = line.strip()
                if stripped.startswith("//") or stripped.startswith("*"):
                    continue
                if "lock(stateLock)" in line or "lock (stateLock)" in line:
                    lock_matches.append(i)
        if lock_matches:
            violations.append(
                "[LOCK GATE FAIL] Banned lock(stateLock) found at lines: "
                + ", ".join(str(l) for l in lock_matches)
                + "\n  -> Use Enqueue() actor model or ConcurrentDictionary direct writes per CLAUDE.md"
            )
    except Exception as e:
        violations.append("[LOCK GATE] Could not scan file: " + str(e))

    if violations:
        print("\n[V12 DNA AUDIT] WARNING -- Post-edit violations in: " + path)
        for v in violations:
            print("  " + v)
        print("\nDo NOT hand off to the Director until these are resolved.")
        sys.exit(2)

    print("[V12 DNA AUDIT] PASS -- " + os.path.basename(path) + " is ASCII-clean and lock-free.")
    sys.exit(0)

if __name__ == "__main__":
    main()
