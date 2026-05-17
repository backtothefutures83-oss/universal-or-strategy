# Phase 7 Repeatable Execution Workflow
## V12 Photon Kernel -- Complexity Extraction Epic (TDD-Enhanced)

> **Version**: 2.0 (TDD Contract Protocol integrated)
> **Status**: Active | **Proven on**: T-Q1 (BUILD_TAG 1111.007-phase7-tQ1)
> **Last Updated**: 2026-05-16

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

## Stage 2: Per-Ticket Execution (Bob CLI -- Orchestrator Mode)

### Header Prompt Template

Paste this before EVERY ticket. Update `BUILD_TAG_BASELINE` to the previous ticket's output tag.

```
MISSION: Phase 7 Complexity Extraction Epic -- V12 Photon Kernel (TDD-Enhanced)
BUILD_TAG_BASELINE: [PREVIOUS_TAG]
REPO: c:\WSGTA\universal-or-strategy
BRANCH: feature/phase7-sprint5-extraction
SPEC REF: docs/brain/phase7_complexity_epic_brief.md
TDD PROTOCOL: Red-Green-Refactor (3-attempt auto-retry). Worker-Validator loop active.

Execute PLAN-THEN-EXECUTE PROTOCOL with TDD Contract Gate:
  P2 Forensics   -> docs/brain/forensics_report_t[ID].md
  P3 Architect   -> docs/brain/implementation_plan_t[ID].md
                    (helper names, signatures, caller impact -- STOP and confirm)
  P4 Adjudicator -> docs/brain/adjudicator_audit_t[ID].md -- STOP for Director confirm
  P5 Engineer    -> RED: write failing contract test for extracted helper signature first.
                    GREEN: extract method until test passes.
                    Post-edit: deploy-sync.ps1 + bump BUILD_TAG.
  P6 Verifier    -> REFACTOR: run full test suite + complexity_audit.py.
                    Report: docs/brain/verification_report_t[ID].md
                    (include test pass rate + CYC delta)

TDD Constraints:
  - Zero lock() statements -- pure atomic primitives only.
  - MockTime pattern for any timer/async assertions -- zero Thread.Sleep.
  - Contract test MUST cover: happy path, null-guard edge case, caller invariant.
  - Self-healing: if GREEN fails, auto-retry extraction up to 3 times before halting.

--- TICKET BELOW ---
[paste full Traycer ticket content here]
```

### Bob Orchestrator Pipeline (auto-executed per ticket)

| Phase | Agent | Gate | Output Artifact |
|:------|:------|:-----|:----------------|
| P2 Forensics | Plan mode | -- | `docs/brain/forensics_report_t[ID].md` |
| P3 Architect | Plan mode | STOP -- Director confirms plan | `docs/brain/implementation_plan_t[ID].md` |
| P4 Adjudicator | Internal audit | CONDITIONAL PASS -> Antigravity resolves | `docs/brain/adjudicator_audit_t[ID].md` |
| P5 Engineer | Advanced/Code mode | RED test written first, GREEN on pass | `src/` edits + `deploy-sync.ps1` |
| P6 Verifier | Plan/Code mode | Full test suite + CYC audit | `docs/brain/verification_report_t[ID].md` |

### TDD Contract Protocol (Permanent -- Integrated from Symmetry FSM Epic)

**P5 Engineer -- RED Phase:**
- Write a failing NUnit contract test targeting the extracted helper's signature BEFORE making any `src/` edit.
- Test file: `tests/[SubgraphName]IntegrationTests.cs`
- Required test scenarios per extraction:
  1. Happy path -- normal input, expected output
  2. Null/guard edge case -- boundary condition that must not throw
  3. Caller invariant -- verify that the caller's behavior is unchanged after extraction

**P5 Engineer -- GREEN Phase:**
- Extract the helper method until all three contract tests pass.
- Self-healing retry: if GREEN fails, re-examine extraction boundary and retry up to 3 times.
- If 3 attempts fail: HALT and report to Director with exact failure trace.

**P6 Verifier -- REFACTOR Phase:**
- Run full test suite: `dotnet test tests/`
- Run `python scripts/complexity_audit.py` -- confirm CYC delta matches ticket target.
- Run `deploy-sync.ps1` -- ASCII gate must PASS.
- Populate `docs/brain/verification_report_t[ID].md` with:
  - Test pass rate (e.g., `20/20 PASS`)
  - CYC before/after delta
  - Lock audit result (`CLEAN`)
  - BUILD_TAG

**TDD DNA Constraints (non-negotiable):**
- `lock()` in any form -- BANNED
- `Thread.Sleep()` in tests -- BANNED (use MockTime pattern)
- Unicode/emoji in any string literal -- BANNED
- Manual copy-paste for extractions > 50 lines -- BANNED (use `v12_split.py`)

### Adjudicator Clarification Gate

If Bob's Adjudicator returns `CONDITIONAL PASS` with clarifications:
- Paste clarifications to Antigravity
- Antigravity resolves against agreed V12 DNA decisions
- Select the matching pre-built Bob response option (usually option 1)
- Do NOT send back to Architect -- clarifications are policy confirmations, not design changes

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

## Stage 3: Epic Close (T4 ticket)

T4 bundles:
- Final CYC verification across all extracted methods
- Verbatim Print/wrapped-statement diff confirmation
- Full test suite run (all contract tests)
- `docs/` updates (`architecture.md`, `Living_Document_Registry.md`)
- `T-W1-Perf` follow-up ticket documented (LINQ allocation deferral)
- BUILD_TAG final increment

---

## Traycer <-> Bob Handoff Pattern

```
Traycer Epic (aligned specs + tickets)
         |
  [Copy ticket content]
         |
Bob Orchestrator (header prompt + ticket)
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

## Upstream Feed: Cluster Audit Workflow (Future)

> **Status**: Planned -- to be designed after current epic completes.

The Cluster Audit Workflow will run BEFORE Stage 1 to produce the raw signal
that populates the Epic ticket backlog.

```
[Cluster Audit Workflow]
  Trigger: Director decides a subgraph needs health check
  Tools: jCodemunch + graphify + arenaclusterreview
  Output: docs/brain/cluster_audit_report_[tag].md
          (CYC targets, blast radius, DNA violations, recommended Epic shape)
         |
         v
[Phase 7 Repeatable Execution Workflow] (this document)
  Input: cluster_audit_report -> informs Traycer ticket backlog
  Execution: Stage 1 -> Stage 2 (P2-P6 per ticket) -> Stage 3
```

See `.agent/workflows/arenaclusterreview.md` for the upstream workflow definition.

---

**Document Owner**: Antigravity Orchestrator
**Source of Truth**: This file supersedes the Phase 7 header prompt pattern held
                    in chat history only.
**Linked Manifesto Entry**: `docs/brain/V12_Workflow_Manifesto.md` Section 5
