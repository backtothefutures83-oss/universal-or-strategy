# Zero-Waste Discovery Protocol (V12.932)

## 🛡️ MISSION
To prevent AI agents from wasting tokens, time, and "Context Space" on irrelevant legacy code, backups, and non-active logic nodes.

## 1. Automated Exclusion Rules
AI agents MUST automatically exclude the following patterns from all global scans, refactoring tasks, and audits (unless explicitly instructed by the Director):

*   **Backups**: `*.bak`, `*.bak_*`, `*.backup`, `*.old`
*   **Datestamps**: Files containing `YYYYMMDD` patterns (e.g. `*.20260224.cs`)
*   **Archives**: Files in `ARCHIVE_*`, `LEGACY_*`, or `DEVELOPMENT/TRASH` folders.
*   **Binaries**: `*.exe`, `*.dll`, `*.pdb`, `*.user`, `*.suo`
*   **Meta**: `.git`, `.qodo`, `.testsprite`, `.claude/cache`

## 2. Active Fleet Focus
When a "Global Scan" or "Project Simplification" is requested, the agent MUST restrict its search space to the **Active Logic Fleet**:
*   Directory: `src/`
*   Inclusion: `*.cs`
*   Exclusion: (All patterns in Section 1)

## 3. Context Conservation
*   **Single-File Priority**: If a bug is in `SIMA.cs`, do NOT read `Properties.cs` or `Entries.cs`.
*   **Grep-First**: Always use `grep` to find specific line numbers before reading a full file.
*   **No Redundant Scanning**: If a file was audited in the last 24 hours and the hash hasn't changed, do not re-scan it.

## 4. Director Escalation
If an agent believes a legacy or backup file contains critical "Logic DNA" needed for a repair, it MUST ask the Director: *"I have found a potential fix in a legacy backup [filename]. Permission to ingest?"*

---
> [!IMPORTANT]
> Failure to follow this protocol results in "Context Debt" and degrades the quality of AI-driven repairs. Protect the Token Budget.
