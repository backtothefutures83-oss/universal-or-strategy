# Implementation Plan: Phase 8 Architectural Repair

## Phase 1: Architectural Repair (Claude P3)
- [x] Task: Provide Claude (P3 Architect) with the `docs/brain/claude_repair_prompt.md` failure report. [104eead]
- [x] Task: Review the newly generated `implementation_plan.md` from Claude to ensure it contains no `lock(stateLock)` usage and uses the correct Replace FSM. [c7040b9]
- [x] Task: Approve the repaired implementation plan. [e252651]

## Phase 2: Engineering Execution (Codex P4) [checkpoint: 914010e]
- [x] Task: Hand off the approved `implementation_plan.md` to Codex (P4 Engineer). [3764ed3]
- [x] Task: Codex implements the changes according to the repaired plan. [0abb48b]
- [x] Task: Codex performs P4 Self-Audit (Grep for locks, ASCII check, Dry Run). [f3d4b5e]
- [x] Task: Conductor - User Manual Verification 'Phase 2: Engineering Execution' (Protocol in workflow.md) [914010e]
