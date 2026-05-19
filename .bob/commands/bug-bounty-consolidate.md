---
description: Consolidate 7 cluster bug reports into a validated, hallucination-filtered, ranked repair plan. Run after all 7 cluster-bug-hunt reports are complete.
argument-hint: runner (optional -- runner suffix, e.g. bob, qwen, jules, gemini. Defaults to bob)
---
# MISSION: Bug Bounty Consolidation -- V12 Photon Kernel
**Spec Ref**: docs/brain/bug_bounty_workflow.md
**Input**: docs/brain/bug_report_s1_[runner].md through bug_report_s7_[runner].md (all 7 must exist)
**Output**: docs/brain/cluster_bug_bounty_report_[runner].md
**Mode**: Plan mode -- READ ONLY. No src/ edits.

---

## STEP 1 -- INGEST ALL 7 REPORTS

Read the following files in order:
- docs/brain/bug_report_s1_[runner].md (S1: SIMA Core)
- docs/brain/bug_report_s2_[runner].md (S2: Execution Engine)
- docs/brain/bug_report_s3_[runner].md (S3: UI & Photon IO)
- docs/brain/bug_report_s4_[runner].md (S4: REAPER Defense)
- docs/brain/bug_report_s5_[runner].md (S5: Kernel State)
- docs/brain/bug_report_s6_[runner].md (S6: Signals & Entries)
- docs/brain/bug_report_s7_[runner].md (S7: Kernel Infrastructure)

If any file is missing: HALT and report to Director which clusters are incomplete.

---

## STEP 2 -- HALLUCINATION FILTER

For EVERY reported bug across all 7 reports:
1. Use jCodemunch `search_symbols` to verify the cited method exists
2. Use `get_file_content` to verify the cited code pattern matches actual src/
3. Use `find_references` to confirm the cited shared state is actually accessed

Disposition for each bug:
- VALIDATED: evidence confirmed in src/
- FILTERED: cited method/pattern does not match src/ reality
- UNCERTAIN: partially verifiable -- flag for Director review

Track and report the filter rate per cluster.

---

## STEP 3 -- CROSS-CLUSTER DEDUPLICATION

Identify bugs reported by multiple agents for the same root cause:
- Match on: same file + same method + same root cause mechanism
- Merge into single canonical entry
- List all clusters that reported it
- Elevate severity if blast radius spans 2+ clusters

---

## STEP 4 -- SEVERITY RANKING

Final ranking of all validated bugs:
- Critical: Data corruption, race conditions, use-after-free
- High: FSM state leaks, ghost order windows, O(N^2) hot paths, semaphore leaks
- Med: Missing null guards, incomplete resets, inefficient lookups
- Low: Style violations, minor inefficiencies

---

## STEP 5 -- OUTPUT

Write docs/brain/cluster_bug_bounty_report_[runner].md containing:

```
# V12 Cluster Bug Bounty Report
Generated: [date]

## Summary
Total bugs found (raw): [N]
Validated: [N] | Filtered (hallucinations): [N] | Uncertain (Director review): [N]
Critical: [N] | High: [N] | Med: [N] | Low: [N]

## Filter Rate by Cluster
| Cluster | Found | Validated | Filtered |
|---------|-------|-----------|----------|
| S1 SIMA | N | N | N |
...

## Validated Bug List (ranked by severity)
[full list in BUG-[S#]-[NNN] format]

## Recommended Repair Sequence
[cluster order based on Critical count and dependency graph]

## /epic-tdd Ticket Blocks
[copy-paste ready ticket for each validated bug]
```

---

## STEP 6 -- HANDOFF

Output:
```
[BUG-BOUNTY-CONSOLIDATION-COMPLETE]
Total validated: [N]
Filtered: [N]
Uncertain (needs Director review): [N]
Report: docs/brain/cluster_bug_bounty_report_[runner].md
Next: Director reviews report -> selects cluster -> /epic-tdd for repairs
```

---

## BANNED
- Any src/ edit -- BANNED
- Fixing bugs inline -- BANNED
- Marking a bug VALIDATED without jCodemunch verification -- BANNED
