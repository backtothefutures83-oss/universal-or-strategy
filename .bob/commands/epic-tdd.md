---
description: Execute single or multi-cluster tickets using the P2-P6 TDD Red-Green-Refactor pipeline. Supports single-ticket mode (P3 gate active) and multi-cluster YOLO mode (P3 waived, auto-continue on P6 PASS).
argument-hint: <traycer-ticket | DIRECTOR PRE-APPROVAL + cluster list>
---
# MISSION: Epic TDD -- V12 Photon Kernel

**Spec Ref**: docs/brain/epic_tdd_workflow.md
**Protocol**: V12 Photon Kernel DNA (Lock-Free, ASCII-Only, TDD Red-Green-Refactor)

---

## MODE DETECTION (auto)

Read the prompt header to determine mode:

| Header contains | Mode | P3 stop | Auto-continue |
|:----------------|:-----|:--------|:--------------|
| `DIRECTOR PRE-APPROVAL` | **Multi-Cluster YOLO** | WAIVED | YES -- on P6 PASS |
| No pre-approval | **Single-Ticket** | ACTIVE -- wait for Director | NO |

### Session Sizing (YOLO mode only)

Stop and report `[BATCH-COMPLETE]` when EITHER is true:

- 3 clusters completed in this session, OR
- 25+ source files processed in this session

Do NOT start a new cluster beyond these limits -- context degradation risk.

### YOLO Gates (non-negotiable even with pre-approval)

- P4.5 re-audit: MANDATORY if P4 returns CONDITIONAL PASS
- P6 PASS required before advancing -- cannot skip
- HALT on P6 FAIL -- do NOT self-repair and continue
- Loop limit of 2 at P4.5 -- HALT if exceeded

---

## STEP 1 -- P2 FORENSICS (Plan Mode)

> **CREATES** `docs/brain/forensics_report_t[ID].md` -- this file does NOT pre-exist.
> P2 scans the source files and generates this report. Never wait for it to appear.

Before writing any code or plan, run forensic analysis:

### 1a. jCodemunch Structural Scan

- `get_file_outline` on the target file -- map every symbol, signature, complexity score
- `get_blast_radius` on the target method -- identify all downstream callers
- `find_references` on any shared state accessed in the method

### 1b. Complexity Baseline

Run: `python scripts/complexity_audit.py`
Record the BEFORE CYC score for the target method.

### 1c. Graphify Caller Map

Run: `graphify update .`
Read `graphify-out/GRAPH_REPORT.md` -- confirm caller impact scope.

**Output**: Write `docs/brain/forensics_report_t[ID].md` with:

- Target method name + current CYC score
- Blast radius (callers list)
- Proposed extraction boundary (helper name + signature draft)
- DNA risks identified

---

## STEP 2 -- P3 ARCHITECT PLAN (Plan Mode)

> **READS** `docs/brain/forensics_report_t[ID].md` (created by P2)
> **CREATES** `docs/brain/implementation_plan_t[ID].md`

Produce a written implementation plan:

```
## Epic TDD Plan: [ticket ID] -- [method name]
### Extraction Design
| Helper Name | Signature | Lines Extracted | CYC Impact |
|-------------|-----------|-----------------|------------|
| ...         | ...       | ...             | ...        |

### Caller Impact
| Caller File | Caller Method | Change Required |
|-------------|---------------|-----------------|
| ...         | ...           | ...             |

### TDD Contract Tests Required
| Test # | Scenario        | Expected Result |
|--------|-----------------|-----------------|
| 1      | Happy path      | ...             |
| 2      | Null/guard edge | ...             |
| 3      | Caller invariant| ...             |
```

**Output**: Write `docs/brain/implementation_plan_t[ID].md`

### !!! DIRECTOR APPROVAL GATE

**STOP HERE. Do NOT proceed to Step 3 until the Director explicitly confirms.**

Output: "[EPIC-TDD-GATE] Plan written to docs/brain/implementation_plan_t[ID].md. Awaiting Director approval."

---

## STEP 3 -- P4 ADJUDICATOR AUDIT (Internal)

> **READS** `docs/brain/implementation_plan_t[ID].md` (created by P3)
> **CREATES** `docs/brain/adjudicator_audit_t[ID].md`

Perform adversarial self-audit of the plan against V12 DNA:

Checklist:

- [ ] Zero lock() usage in proposed code
- [ ] No Thread.Sleep in proposed tests
- [ ] Extraction is >= 15 LOC (extraction floor)
- [ ] No logic drift -- pure structural extraction
- [ ] ASCII-only in all string literals
- [ ] deploy-sync.ps1 is included in post-edit sequence

**Output**: Write `docs/brain/adjudicator_audit_t[ID].md`

If any checklist item FAILS: return `CONDITIONAL PASS` with specific clarification.
If all pass: return `PASS -- CLEARED FOR P5 EXECUTION`.

---

## STEP 3.5 -- P4.5 TARGETED RE-AUDIT (Only fires after CONDITIONAL PASS revision)

This step is SKIPPED if P4 returned a clean PASS.
This step is MANDATORY if P4 returned CONDITIONAL PASS and the Architect revised the plan.

### Re-Audit Scope (targeted -- not a full P4 repeat)

- Read the list of N gaps documented in the original P4 CONDITIONAL PASS
- Check ONLY those specific gaps against the revised plan
- Do NOT re-audit the entire plan -- only the flagged items

### Re-Audit Outcomes

- **All gaps resolved, no new gaps**: PASS -- append result to `docs/brain/adjudicator_audit_t[ID].md` and proceed to STEP 4
- **New gaps introduced by revision**: Full loop back to STEP 2 (P3 Architect). Loop counter +1.
- **Loop counter reaches 2**: HALT. Output:

  ```
  [EPIC-TDD-LOOP-LIMIT]
  Ticket: [ID]
  Status: HALTED -- 2 revision loops exhausted without clean P4.5 pass.
  Action: Director review required. Do NOT proceed to P5.
  ```

### Non-negotiable

- The Orchestrator CANNOT self-certify that gaps are resolved.
- P4.5 must run even if the revision looks obviously correct.
- Skipping P4.5 after a CONDITIONAL PASS is a protocol violation.

---

## STEP 4 -- P5 ENGINEER (Advanced/Code Mode) -- RED-GREEN

> **READS** `docs/brain/adjudicator_audit_t[ID].md` (created by P4)
> **CREATES** test file in `tests/` and edits `src/` files

### RED Phase: Write Failing Contract Tests FIRST

Before touching src/, write the contract tests to `tests/[SubgraphName]IntegrationTests.cs`:

**Output Size Mitigation**: If generating >15 tests, DO NOT write them all in one go. Break the writing into multiple passes (e.g., append Phase 1+2 tests first, confirm compile, then append Phase 3+4 tests).

Required scenarios:

1. **Happy path**: normal input -> expected extracted-helper output
2. **Null/guard edge**: boundary condition -> must not throw or corrupt state
3. **Caller invariant**: call site behavior is identical before and after extraction

The tests MUST fail at this point (RED). Do NOT proceed if they pass -- that means
the test is not actually targeting the new helper.

### GREEN Phase: Extract the Method

Apply surgical extraction:

- Use `v12_split.py` for any extraction exceeding 50 lines (manual copy-paste BANNED)
- Touch ONLY the target method and its new helper
- NEVER mutate whitespace, indentation, or adjacent unrelated code
- After extraction, run the contract tests -- they must now PASS (GREEN)

**Self-healing retry**: If GREEN fails, re-examine extraction boundary and retry up to 3 times.
If 3 attempts fail: HALT. Report exact failure trace. Do NOT proceed.

### Post-Edit Deployment (MANDATORY)

```powershell
# Re-establish hard links + ASCII gate
powershell -File .\deploy-sync.ps1

# Lock regression audit (must return ZERO matches)
grep -r "lock(" src/

# Unicode regression audit (must return ZERO matches)
grep -Prn "[^\x00-\x7F]" src/
```

All three must PASS before proceeding to P6.

---

## STEP 5 -- P6 VERIFIER (Plan/Code Mode) -- REFACTOR

> **RUNS** full test suite + complexity audit
> **CREATES** `docs/brain/verification_report_t[ID].md`

Run full verification suite:

```powershell
# Full test suite
dotnet test tests/

# Complexity audit -- confirm CYC delta meets ticket target
python scripts/complexity_audit.py

# Final hard-link sync
powershell -File .\deploy-sync.ps1
```

**Output**: Write `docs/brain/verification_report_t[ID].md` containing:

- Test pass rate (e.g., `20/20 PASS`)
- CYC before/after delta
- Lock audit: CLEAN
- Unicode audit: CLEAN
- deploy-sync.ps1: PASS
- BUILD_TAG (bump now)

---

## STEP 6 -- HANDOFF TO DIRECTOR

### Single-Ticket Mode Output

Only after ALL Step 5 audits PASS:

```
[EPIC-TDD-COMPLETE]
Ticket: [ID]
Method: [target method]
CYC: [before] -> [after]
Tests: [N]/[N] PASS
BUILD_TAG: [new tag]
Status: READY FOR F5 COMPILE

Director Post-Ticket Checklist:
[ ] Press F5 in NinjaTrader -- verify BUILD_TAG banner
[ ] Confirm complexity_audit.py pass in verification report
[ ] Confirm test pass rate in verification report
[ ] Commit forensics + plan + verification reports
[ ] Update BUILD_TAG_BASELINE in next ticket's header prompt
```

### Multi-Cluster YOLO Mode -- Per-Cluster Output

After each cluster P6 PASS (auto-advance, no Director stop):

```
[CLUSTER-[S#]-COMPLETE]
Cluster: [name] ([N] files)
Tests added: [N] | Total passing: [N]
BUILD_TAG: [tag]
Advancing to: [next cluster name]
```

### Multi-Cluster YOLO Mode -- Batch Complete Output

After all clusters in the batch complete OR session size limit reached:

```
[BATCH-[LABEL]-COMPLETE]
Clusters completed: [list]
Tests added this batch: [N]
Total tests passing: [N] (all suites)
BUILD_TAG: [final tag]
HALTs this batch: [none / list with reason]
Session limit hit: [yes/no]
Next action: Paste Batch [N+1] prompt
```

---

## BANNED PATTERNS (immediate halt)

- `lock(anything)` -- BANNED
- `Monitor.Enter` / `Monitor.Exit` -- BANNED
- `Thread.Sleep()` anywhere -- BANNED
- Unicode / emoji / curly quotes in any string literal -- BANNED
- Manual copy-paste for extractions > 50 lines -- BANNED (use v12_split.py)
- Skipping RED phase (writing GREEN without a failing test first) -- BANNED
- Self-certifying P4.5 pass (Orchestrator declaring gaps resolved without re-audit) -- BANNED
- Advancing past P6 FAIL in YOLO mode -- BANNED
- Exceeding 3 clusters or 25 src files per session in YOLO mode -- BANNED
