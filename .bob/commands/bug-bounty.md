---
description: Dispatch 7 parallel cluster agents for a focused bug hunt across all V12 src files. Consolidate, validate, and filter findings into a repair-ready report.
argument-hint: runner (optional -- runner suffix, e.g. bob, qwen, jules, gemini. Defaults to bob)
---
# MISSION: Bug Bounty -- V12 Photon Kernel 7-Cluster Parallel Hunt
**Spec Ref**: docs/brain/bug_bounty_workflow.md
**Protocol**: Read-only forensic hunt. NO src/ edits. Output feeds /epic-tdd for repairs.
**Prerequisite**: All 7 cluster test suites must be complete before running this command.

---

## STEP 1 -- ORCHESTRATOR: PARALLEL DISPATCH

Spawn 7 sub-agents simultaneously, one per cluster. Each agent MUST operate in Plan mode
(read-only for src/, write-access for docs/). Each receives ONLY its cluster's files as context.

### Agent Context Assignments

| Agent | Cluster | Files |
|:------|:--------|:------|
| Agent-S1 | SIMA Core | V12_002.SIMA.*.cs (7 files) |
| Agent-S2 | Execution Engine | V12_002.Orders.*.cs + Symmetry.*.cs + Trailing.*.cs (16 files) |
| Agent-S3 | UI & Photon IO | V12_002.UI.*.cs (16 files) |
| Agent-S4 | REAPER Defense | V12_002.REAPER.*.cs + Safety.*.cs (5 files) |
| Agent-S5 | Kernel State | V12_002.Lifecycle.cs + StickyState + Telemetry + StructuredLog + Properties (5 files) |
| Agent-S6 | Signals & Entries | V12_002.Entries.*.cs (7 files) |
| Agent-S7 | Kernel Infrastructure | V12_002.cs + Constants + LogicAudit + DrawingHelpers + AccountUpdate + BarUpdate + Atm + PureLogic + Data + PositionInfo + Entries.cs + SignalBroadcaster (11 files) |

### Per-Agent Hunt Instructions

Each agent MUST scan for:
1. Race conditions -- shared state without atomic guards
2. Use-after-free windows -- resource released before all references cleared
3. Re-entrancy floods -- callbacks triggered inside critical sections
4. Ghost order windows -- async ID registered before submission completes
5. FSM state leaks -- incomplete reset during cancel/error
6. Null ref hot paths -- property access before null check
7. O(N^2) nested loops -- fleet/account list iterations
8. Semaphore leaks -- missing finally blocks
9. lock() remnants -- any remaining banned patterns
10. Non-ASCII string literals -- compiler safety violations
11. Wildcard Logic & Architectural Anomalies -- Leverage your full, unconstrained reasoning capacity to identify any deep structural flaws, data corruption windows, or subtle logical bugs violating the V12 Platinum Standard (even if they fall completely outside this checklist).

Bug report format per finding:
```
BUG-[S#]-[NNN]
Title: [short description]
Severity: Critical / High / Med / Low
Location: [file].[method] (line range if known)
Root Cause: [exact mechanism]
Evidence: [pattern or code reference]
Test Impact: [which test type would catch this]
```

Output per agent: docs/brain/bug_report_s[N]_[runner].md (e.g. docs/brain/bug_report_s1_bob.md, docs/brain/bug_report_s1_qwen.md, docs/brain/bug_report_s1_jules.md, docs/brain/bug_report_s1_gemini.md. Defaults to _bob if runner is not provided).

---

## STEP 2 -- ORCHESTRATOR: CONSOLIDATION

After all 7 agents report, run the consolidation phase:

### 2a. Hallucination Filter
- Verify each cited file/method exists via jCodemunch `search_symbols`
- Verify cited evidence matches actual src/ content
- Discard unverifiable findings -- mark as [FILTERED: hallucination]
- Report filter rate to Director

### 2b. Cross-Cluster Deduplication
- Merge bugs with same root cause across clusters
- Elevate severity for cross-cluster blast radius findings

### 2c. Severity Ranking
Final ranked list: Critical -> High -> Med -> Low

### 2d. Output
Write docs/brain/cluster_bug_bounty_report.md with:
- Total validated bugs by severity
- Per-cluster breakdown table
- Hallucination filter count (transparency)
- Recommended repair sequence
- /epic-tdd ticket block for each validated bug (copy-paste ready)

---

## STEP 3 -- HANDOFF TO DIRECTOR

Output:
```
[BUG-BOUNTY-COMPLETE]
Total bugs found: [N]
Validated: [N] | Filtered (hallucinations): [N]
Critical: [N] | High: [N] | Med: [N] | Low: [N]
Report: docs/brain/cluster_bug_bounty_report.md
Next step: Director selects cluster -> /epic-tdd for repairs
```

---

## BANNED DURING THIS COMMAND
- Any src/ edit -- BANNED (this is forensic-only)
- Fixing bugs inline -- BANNED (all fixes go through /epic-tdd)
- Reporting a bug without verifiable evidence -- BANNED
