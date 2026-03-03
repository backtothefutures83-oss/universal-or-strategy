---
name: audit
description: Perform a forensic logic audit of NinjaScript strategy files.
---

# Forensic Audit Workflow

Trigger this skill when validating a new patch or scanning for fleet stability.

## 1. Discovery
- Grep for core state mutations: `activePositions`, `expectedPositions`, `_simaToggleSem`.
- Identify entry/exit hooks in `OnOrderUpdate` and `OnAccountOrderUpdate`.

## 2. Checklist (Mandatory)
- **Race conditions**: Are state lookups/mods inside `stateLock`?
- **Semaphore Leaks**: Does every `WaitAsync()` have a matched `Release()` in a `finally` block?
- **Ghost Matching**: Does the substring order-name matching have enough anchors (exact match preferred)?
- **Delta-After-Submit**: Are `expectedPositions` updated *before* or *immediately after* the `Submit()` call?
- **Naked Position Risk**: Does the `REAPER` have a clear path to detect and flatten if metadata is purged?
- **IPC Flood**: Are UI button clicks debounced or limited to one-at-a-time?

## 3. Reporting Format
Classify findings in a markdown table:

| Severity | File | Line Range | Issue | Risk | Recommended Fix |
| :--- | :--- | :--- | :--- | :--- | :--- |
| P0 (Critical) | ... | ... | ... | ... | ... |
| P1 (High) | ... | ... | ... | ... | ... |

## 4. Stability Score
Provide an overall system stability score from 0 to 100 based on the findings.

---
*Zero-Trust Forensic Protocol Enabled.*
