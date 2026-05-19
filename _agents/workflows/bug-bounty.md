# Bug Bounty Workflow
## V12 Photon Kernel -- 7-Cluster Parallel Bug Hunt

> **Agent-agnostic** -- Bob native: `/bug-bounty` | All CLIs: `SPEC REF: docs/brain/bug_bounty_workflow.md`
> **Prerequisite**: All 7 cluster test suites complete (Testing Setup Epic done first)
> **Full spec**: `docs/brain/bug_bounty_workflow.md`

---

## Trigger

Use this workflow after the Testing Setup Epic is complete (all 7 clusters have 100% test coverage).
Runs a parallel 7-agent bug hunt across all 71 src files, consolidates findings with hallucination
filtering, and produces a repair-ready report for `/epic-tdd`.

---

## How to Invoke

### In Bob CLI (native -- recommended, supports parallel dispatch)
```
/bug-bounty
```

### In any other CLI (sequential fallback)
```
SPEC REF: docs/brain/bug_bounty_workflow.md

Run a focused bug hunt on cluster [S#: cluster name].
Scope: [list cluster files only]
Output: docs/brain/bug_report_s[N].md

Bug report format per finding:
BUG-[S#]-[NNN] | Title | Severity | Location | Root Cause | Evidence | Test Impact
```

---

## Workflow Sequence

```
Bob Orchestrator (/bug-bounty)
    |
    +-- Agent-S1 (SIMA Core, 7 files)      -> bug_report_s1.md
    +-- Agent-S2 (Execution, 16 files)     -> bug_report_s2.md
    +-- Agent-S3 (UI & IO, 16 files)       -> bug_report_s3.md
    +-- Agent-S4 (REAPER, 5 files)         -> bug_report_s4.md
    +-- Agent-S5 (Kernel State, 5 files)   -> bug_report_s5.md
    +-- Agent-S6 (Signals, 7 files)        -> bug_report_s6.md
    +-- Agent-S7 (Infra, 11 files)         -> bug_report_s7.md
         |
    [Orchestrator Consolidation]
    Hallucination filter + deduplication + severity ranking
         |
    cluster_bug_bounty_report.md
         |
    /epic-tdd (repairs, one cluster at a time)
```

---

## Full Spec Reference

See `docs/brain/bug_bounty_workflow.md` for:
- Per-agent hunt protocol (10 bug pattern categories)
- Hallucination filter process
- Cross-cluster deduplication rules
- Consolidated report format
- Handoff to /epic-tdd repair pipeline
