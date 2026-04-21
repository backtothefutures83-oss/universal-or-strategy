# P7 Hook Repair Notes

Date: 2026-04-21
Scope: P7 debt documentation only. The hook is not repaired in this session.

## Findings

1. UTF-8 BOM bytes `EF-BB-BF` are present at the start of `.git/hooks/pre-commit`.
   Evidence: running `C:\Program Files\Git\bin\sh.exe .git/hooks/pre-commit` emits `.git/hooks/pre-commit: line 1: ﻿#!/bin/sh: No such file or directory`.

2. The hook file uses CRLF line endings.
   Required follow-up: reinstall the hook with LF-only content and no BOM.

3. The hook's `lock()` audit produces a false positive on the dummy stub declaration `private readonly object stateLock = new object();` in `src/V12_002.cs`.
   Required follow-up: narrow the grep pattern so it only flags real `lock (...)` statements and explicitly excludes that one-line declaration.

## P7 Debt Status

- **RESOLVED**: `scripts/install_hooks.ps1` updated to write `.git/hooks/pre-commit` without UTF-8 BOM and using LF.
- **RESOLVED**: Grep filter updated to correctly isolate the `lock` keyword using `[[:space:]]` syntax, successfully excluding `stateLock` and avoiding substring matching in variable names.
- **RESOLVED**: Hook has been reinstalled successfully and passes the pre-commit check (`exit 0`).
