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

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform a forensic audit to identify and eliminate gaps:

1. **Clarity Audit**: Did any instruction produce an unexpected result, ambiguity, or require a judgment call?
2. **Completeness Audit**: Was a step missing that caused backtracking, or was a reference file out of date?
3. **Traceability Audit**: Did the skill's output perfectly align with the V12 Protocol (P1-P7) and ASCII Gate requirements?

**Corrective Action**:
If gaps are found: **Update this SKILL.md immediately** (no Director approval required for skill-only edits) and commit with:
`skill(audit): [specific fix]`

If no gaps found: State `skill(audit): no gaps identified.` in your turn response.
