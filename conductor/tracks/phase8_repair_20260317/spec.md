# Specification: Phase 8 Architectural Repair

## Objective
The goal of this track is to repair the architectural design for "Phase 8: REAPER FSM Authority Expansion". The initial design by Claude (P3 Architect) failed the Codex P2 Forensic Audit due to Concurrency violations (lock usage) and FSM Compliance violations (raw Cancel/Submit instead of Replace FSM).

## Scope
- Send the failure report (`docs/brain/claude_repair_prompt.md`) to Claude (P3).
- Claude will analyze the failures and generate a repaired `implementation_plan.md` that strictly adheres to the "Director's Gate Hierarchy", explicitly avoiding `lock(stateLock)` and utilizing the proper Replace FSM.
- Hand off the corrected, approved implementation plan to Codex (P4 Engineer) for execution.

## Requirements
- The repaired plan must not contain any `lock(stateLock)` usage.
- The repaired plan must use the MOVE-SYNC FSM for follower order replace patterns (no raw Cancel/Submit).
- The execution must comply with all ASCII string requirements.