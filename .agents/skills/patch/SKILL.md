---
name: patch
description: Execute a surgical code patch based on a pre-defined plan.
---

# Surgical Patch Workflow

Use this skill when implementing a numbered build or a specific mission brief.

## 1. Analysis
- Read the patch plan/mission brief thoroughly.
- Locate target code using `grep` (NEVER read the entire file if >500 lines).
- Confirm files and line ranges with the Director before editing.

## 2. Execution
- Apply edits one file at a time.
- Follow the backup convention: `cp file.cs file.cs.yyyyMMdd_HHmm.bak`.
- If a bulk operation is needed, write a temporary Python script to handle it safely.

## 3. Verification
- **Mandatory**: Perform a secondary audit immediately after applying the fix.
- Check callers of modified functions for regressions.
- Look for state sync issues introduced by the change.

## 4. Documentation
- Update the `task.md` or mission brief status.
- Provide a concise summary of applied changes in trading terms.

---

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform a forensic audit to identify and eliminate gaps:

1. **Clarity Audit**: Did any instruction produce an unexpected result, ambiguity, or require a judgment call?
2. **Completeness Audit**: Was a step missing that caused backtracking, or was a reference file out of date?
3. **Traceability Audit**: Did the skill's output perfectly align with the V12 Protocol (P1-P7) and ASCII Gate requirements?

**Corrective Action**:
If gaps are found: **Update this SKILL.md immediately** (no Director approval required for skill-only edits) and commit with:
`skill(patch): [specific fix]`

If no gaps found: State `skill(patch): no gaps identified.` in your turn response.
