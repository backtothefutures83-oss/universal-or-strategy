# Epic TDD Workflow

## V12 Photon Kernel -- Complexity Extraction (TDD-Enhanced, Permanent Standard)

> **Version**: 1.0
> **Status**: Active | **Proven on**: T-Q1 (BUILD_TAG 1111.007-phase7-tQ1), Symmetry FSM Epic (20/20 tests)
> **Command**: `/epic-tdd` (Bob CLI) | `SPEC REF: docs/brain/epic_tdd_workflow.md` (all CLIs)
> **Last Updated**: 2026-05-16

---

## Purpose

This is the **permanent, agent-agnostic repeatable workflow** for all V12 complexity-reduction epics.
It integrates the Phase 7 per-ticket execution pipeline with the TDD contract protocol
proven in the Symmetry FSM Epic (100% lock-free, 20/20 pass rate).

**All future complexity extraction tickets -- regardless of which CLI executes them -- MUST use this workflow.**

---

## Stage 1: Epic Creation (Traycer)

**Trigger**: Director decides to open a new complexity-reduction Epic.

**Tool**: Traycer -> Epic View

**Prompts needed** (user has these):

- `/plan-refactor` -- submit alignment answers to lock Analysis + Approach specs
- `/architecture-validation` -- stress-test the Approach for invariant carry-over
- `/ticket-breakdown` -- generate sequenced ticket set under 150KB diff cap

**Alignment Q&A loop** (handled by Antigravity before user pastes into Traycer):

1. Traycer surfaces alignment questions (Q-V*, Q-A*, V-A* series)
2. Antigravity analyzes against V12 DNA and responds with lettered answers
3. User pastes Antigravity's formatted answer block into Traycer with the appropriate slash command
4. Repeat until Traycer confirms specs are locked

**Gate**: Both Analysis + Approach specs in Artifacts panel (Traycer) -> proceed to Stage 2.

---

## Stage 2: Per-Ticket Execution

> **Single-ticket mode**: Director pastes one ticket at a time. P3 + P4 gates active.
> **Multi-cluster YOLO mode**: Director pre-approves the pattern. P3 stop waived. See Stage 2-Multi below.

### Header Prompt Template (all CLIs)

Paste this before EVERY ticket. Update `BUILD_TAG_BASELINE` to the previous ticket's output tag.

> **PHASE OUTPUT CLARIFICATION**: Every phase CREATES its output file from scratch.
> No output file needs to pre-exist before its phase runs. P2 creates the forensics report.
> P3 reads P2's output and creates the plan. P4 reads the plan. P5 reads P4 output.

```
MISSION: [Epic Name] -- V12 Photon Kernel (Epic TDD)
BUILD_TAG_BASELINE: [PREVIOUS_TAG]
REPO: c:\WSGTA\universal-or-strategy
BRANCH: [active branch]
SPEC REF: docs/brain/epic_tdd_workflow.md
TDD PROTOCOL: Red-Green-Refactor (3-attempt auto-retry). Worker-Validator loop active.
IDENTITY MANDATE: YOU are the active agent executing this phase. Do NOT assume you are an orchestrator waiting for a subordinate. If the prompt tells you to generate code, YOU must generate it physically in this terminal immediately. Do not simulate a handoff.

Execute PLAN-THEN-EXECUTE PROTOCOL with TDD Contract Gate:
  P2 Forensics   CREATES docs/brain/forensics_report_t[ID].md
                 (scan source files -- this file does not pre-exist, you generate it)
  P3 Architect   READS forensics_report_t[ID].md
                 CREATES docs/brain/implementation_plan_t[ID].md
                 (STOP and confirm with Director before P4)
  P4 Adjudicator READS implementation_plan_t[ID].md
                 CREATES docs/brain/adjudicator_audit_t[ID].md
  P5 Engineer    READS adjudicator_audit_t[ID].md
                 EDITS src/ files, CREATES test file
                 Post-edit: deploy-sync.ps1 + bump BUILD_TAG
  P6 Verifier    RUNS dotnet test + complexity_audit.py
                 CREATES docs/brain/verification_report_t[ID].md

TDD Constraints:
  - Zero lock() statements -- pure atomic primitives only.
  - MockTime pattern for any timer/async assertions -- zero Thread.Sleep.
  - Contract test MUST cover: happy path, null-guard edge case, caller invariant.
  - Shared Mocks (MANDATORY): Extract mock infrastructure to `tests/Mocks/SharedMocks.cs`. Test files contain tests ONLY.
  - Incremental Build (MANDATORY): For files >1,000 lines, P3 Plan MUST divide P5 generation into discrete stopped phases.
  - Self-healing: if GREEN fails, auto-retry extraction up to 3 times before halting.

--- TICKET BELOW ---
[paste full Traycer ticket content here]
```

### Orchestrator Pipeline (auto-executed per ticket)

> Each phase CREATES its output. No output file pre-exists before its phase runs.

| Phase | Creates | Reads | Gate |
|:------|:--------|:------|:-----|
| P2 Forensics | `forensics_report_t[ID].md` | source files in `src/` | Auto -- no pre-existing file needed |
| P3 Architect | `implementation_plan_t[ID].md` | forensics report | STOP -- Director confirms |
| P4 Adjudicator | `adjudicator_audit_t[ID].md` | implementation plan | CONDITIONAL PASS -> Antigravity resolves |
| P4.5 Re-Audit | appends to `adjudicator_audit_t[ID].md` | gap list from P4 | Only fires if P4 was CONDITIONAL PASS |
| P5 Engineer | `src/` edits + test file | adjudicator audit | RED test written first, GREEN on pass |
| P6 Verifier | `verification_report_t[ID].md` | test results + audit | Full test suite + CYC audit |

### TDD Contract Protocol (Permanent -- Integrated from Symmetry FSM Epic)

**P5 Engineer -- RED Phase:**

- Write a failing NUnit contract test for the extracted helper BEFORE any `src/` edit.
- Test file: `tests/[SubgraphName]IntegrationTests.cs`
- **Output Size Mitigation (MANDATORY)**: If generating >1,000 lines or >15 tests, the P3 Architect MUST mandate incremental file construction in the plan. The Engineer MUST build the file in discrete phases (e.g., Step 1: Namespace/Usings, Step 2: Helpers, Step 3: Phase 1+2 tests, etc.) waiting for confirmation between each.
- **Shared Mock Infrastructure (MANDATORY)**: All mock components MUST be extracted to a shared namespace (e.g., `tests/Mocks/`). Cluster test files MUST ONLY contain the test methods.
- Required test scenarios per extraction:
  1. Happy path -- normal input, expected output
  2. Null/guard edge case -- boundary condition that must not throw
  3. Caller invariant -- verify call site behavior is unchanged after extraction

**P5 Engineer -- GREEN Phase:**

- Extract the helper method until all three contract tests pass.
- Self-healing retry: if GREEN fails, re-examine extraction boundary and retry up to 3 times.
- If 3 attempts fail: HALT and report to Director with exact failure trace.

**P6 Verifier -- REFACTOR Phase:**

- Run full test suite: `dotnet test tests/`
- Run `python scripts/complexity_audit.py` -- confirm CYC delta meets ticket target.
- Run `deploy-sync.ps1` -- ASCII gate must PASS.
- Populate `docs/brain/verification_report_t[ID].md` with:
  - Test pass rate (e.g., `20/20 PASS`)
  - CYC before/after delta
  - Lock audit: CLEAN
  - BUILD_TAG (bumped)

**TDD DNA Constraints (non-negotiable):**

- `lock()` in any form -- BANNED
- `Thread.Sleep()` in tests -- BANNED (use MockTime pattern)
- Unicode/emoji in any string literal -- BANNED
- Manual copy-paste for extractions > 50 lines -- BANNED (use `v12_split.py`)

### Adjudicator Clarification Gate

If the Adjudicator returns `CONDITIONAL PASS` with clarifications:

- Paste clarifications to Antigravity
- Antigravity resolves against agreed V12 DNA decisions
- Architect revises the plan to address the specific flagged gaps
- **P4.5 Targeted Re-Audit fires automatically after any revision:**
  - Re-audit checks ONLY the N previously flagged gaps -- not the full plan
  - If all gaps resolved: PASS -> proceed to P5
  - If new gaps introduced: full loop back to P3 (max 2 loops)
  - If loop limit (2) hit: HALT and report to Director -- do NOT proceed to P5
- Do NOT skip P4.5 even if the revision looks correct -- the Orchestrator cannot self-certify

### Post-Ticket Checklist (Director)

- [ ] NinjaTrader F5 -> verify BUILD_TAG banner matches
- [ ] `complexity_audit.py` pass confirmed in verification report
- [ ] Test pass rate confirmed in verification report (e.g., 20/20)
- [ ] `docs/brain/Living_Document_Registry.md` updated
- [ ] `docs/brain/forensics_report_t[ID].md` committed
- [ ] `docs/brain/implementation_plan_t[ID].md` committed
- [ ] `docs/brain/verification_report_t[ID].md` committed
- [ ] Update `BUILD_TAG_BASELINE` in header for next ticket

---

## Stage 2-Multi: Multi-Cluster YOLO Mode

Use when the Director has pre-approved the cluster testing pattern (reference implementation exists)
and wants Bob to execute multiple clusters autonomously within a session.

### Session Sizing Heuristics

Bob's context fills as test files, forensic reports, and source are loaded. Use this sizing table
to determine how many clusters fit in one session:

| Session Load | Rule | Example |
|:-------------|:-----|:--------|
| **Small clusters** (1-7 files each) | Max 3 clusters per session | S4(5) + S5(5) + S6(7) = 17 files |
| **Medium clusters** (8-14 files each) | Max 2 clusters per session | S2(12) + S3(16) = 28 files |
| **Large clusters** (15+ files each) | Max 1 cluster per session | S3(16) alone |
| **Hard cap** | Never exceed 25 src files per session | Regardless of cluster count |

**Standard V12 cluster batch assignments (based on 25-file cap):**

| Batch | Clusters | Files | Notes |
|:------|:---------|:-----:|:------|
| Batch A | S2 + S3 | 28 files | Allowed: S2 reuses Symmetry harness, reducing actual new context |
| Batch B | S4 + S5 + S6 | 17 files | Three small clusters fit cleanly |
| Batch C | S7 | 11 files | Final cluster, runs standalone |

### Auto-Continue Protocol

When `DIRECTOR PRE-APPROVAL` is declared in the prompt header:

1. **P3 Director stop is WAIVED** -- Bob mirrors the reference implementation (S1) without waiting
2. **P4 self-resolution is AUTHORIZED** -- Bob resolves CONDITIONAL PASS gaps using S1 precedent
3. **P4.5 re-audit is MANDATORY** -- fires automatically, Bob does not skip it
4. **P6 PASS = auto-advance** -- immediately begin next cluster in the batch, no stop
5. **P6 FAIL = HALT** -- output failure report, do NOT self-repair, do NOT advance
6. **Batch complete = report to Director** -- output batch summary, await Batch N+1 prompt
7. **Usage Limit / Mid-Task Halt Protocol**: If the active agent runs out of usage, crashes, or becomes unreachable mid-batch (e.g., at S4):
   - All work completed up to that point is already physically saved to disk (source files, test suites, and P6 `verification_report_cluster_s*.md`).
   - The user or backup agent can instantly resume by reading the project directory's brain files to identify the last successful cluster, then executing the batch starting from the first unfinished cluster (e.g., resuming from S4).
   - In Qwen Code CLI sessions, use `/restore` to recover conversation history and re-propose the pending tool call.


### Multi-Cluster Header Prompt Template

```
MISSION: V12 Cluster Testing Epic -- Multi-Cluster YOLO -- [Batch Label]
BUILD_TAG_BASELINE: [last tag from previous batch or S1]
REPO: c:\WSGTA\universal-or-strategy
SPEC REF: docs/brain/epic_tdd_workflow.md
REFERENCE IMPLEMENTATIONS: [list completed test files]

PHASE OUTPUT CLARIFICATION: Every phase CREATES its output file from scratch.
No output file needs to pre-exist. P2 scans source and creates the forensics report.
P3 reads that report and creates the plan. Never wait for a file that a phase creates.

DIRECTOR PRE-APPROVAL: Pattern approved. Auto-continue active.
  - P3 Director stop: WAIVED (mirror S1 implementation_plan_cluster_s1.md)
  - P4 self-resolution: AUTHORIZED (apply S1 gap precedents)
  - P4.5 re-audit: MANDATORY if CONDITIONAL PASS
  - P6 PASS: auto-advance to next cluster
  - P6 FAIL: HALT and report -- do NOT self-repair
  - Batch complete: output [BATCH-COMPLETE] summary and await next prompt

GLOBAL TDD CONSTRAINTS:
  - Zero lock() / Monitor.Enter -- atomic primitives only
  - MockTime -- zero Thread.Sleep
  - NinjaTrader harness mocked (no live broker)
  - Diff cap: under 150KB per cluster
  - SETUP ONLY -- assert current behavior, no bug fixes

--- CLUSTER LIST BELOW ---
[paste cluster definitions]
```

### Cluster Definition Block Format

Each cluster in the prompt must include:

```
## CLUSTER [S#]: [Name] ([N] files)
Files to cover:
  src/[file1.cs]
  src/[file2.cs]
Output test file: tests/[ClusterName]IntegrationTests.cs
P2: docs/brain/forensics_report_cluster_s[N].md
P3: docs/brain/implementation_plan_cluster_s[N].md
P4: docs/brain/adjudicator_audit_cluster_s[N].md
P6: docs/brain/verification_report_cluster_s[N].md
P6 gate: dotnet test tests/ -- ALL suites must pass
On PASS: advance to next cluster.
On FAIL: HALT.
```

### Batch Completion Output Format

```
[BATCH-[LABEL]-COMPLETE]
Clusters: [list]
Tests added: [N]
Total tests passing: [N]
BUILD_TAG: [final tag]
HALTs: [none / list]
Next: Paste Batch [N+1] prompt
```

---

## Stage 3: Epic Close (Acceptance Ticket)

The final ticket bundles:

- Final CYC verification across all extracted methods
- Verbatim Print/wrapped-statement diff confirmation
- Full test suite run (all contract tests)
- `docs/` updates (`architecture.md`, `Living_Document_Registry.md`)
- Any deferred perf follow-up tickets documented
- BUILD_TAG final increment

---

## Handoff Pattern (Traycer -> Engineer CLI)

```
Traycer Epic (aligned specs + tickets)
         |
  [Copy ticket content]
         |
Engineer CLI (header prompt + ticket)
  Bob CLI:  /epic-tdd + ticket
  Codex:    SPEC REF header + ticket
  Gemini:   SPEC REF header + ticket
         |
  Autonomous P2->P3->P4->P5(TDD RED->GREEN)->P6(REFACTOR+audit)
         |
  [Director verifies F5 + BUILD_TAG + test pass rate]
         |
  [Update baseline tag in header]
         |
  Next ticket
```

---

## Upstream Feed: Bug Bounty Workflow

> **Status**: DESIGNED -- runs after all 7 cluster test suites are complete.

The Bug Bounty Workflow runs BEFORE repairs to identify real bugs cluster by cluster.

```
[Bug Bounty Workflow]  (upstream)
  Trigger: Testing Setup Epic complete (all 7 clusters covered)
  Tools: /cluster-bug-hunt (any CLI) -> /bug-bounty-consolidate (Bob)
  Output: docs/brain/cluster_bug_bounty_report.md
         |
         v
[Epic TDD Workflow]  (this document -- downstream)
  Input: bug_bounty_report -> one cluster at a time for repairs
  Execution: Stage 2 per bug ticket -> Stage 3
```

See `docs/brain/bug_bounty_workflow.md` + `.agent/workflows/cluster-bug-hunt.md`.

---

**Document Owner**: Antigravity Orchestrator
**Bob Command**: `.bob/commands/epic-tdd.md`
**Universal Workflow**: `.agent/workflows/epic-tdd.md`
**Linked Manifesto Entry**: `docs/brain/V12_Workflow_Manifesto.md` Section 5
