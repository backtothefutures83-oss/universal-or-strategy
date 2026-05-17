---
description: Execute a single complexity extraction ticket using the full P2-P6 TDD Red-Green-Refactor pipeline.
argument-hint: <traycer-ticket-content>
---
# MISSION: Bob TDD -- V12 Photon Kernel Complexity Extraction
**Spec Ref**: docs/brain/bob_tdd_workflow.md
**Protocol**: V12 Photon Kernel DNA (Lock-Free, ASCII-Only, TDD Red-Green-Refactor)

---

## STEP 1 -- P2 FORENSICS (Plan Mode)

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

Produce a written implementation plan:

```
## Bob TDD Plan: [ticket ID] -- [method name]
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

### !!! DIRECTOR APPROVAL GATE !!!
**STOP HERE. Do NOT proceed to Step 3 until the Director explicitly confirms.**

Output: "[BOB-TDD-GATE] Plan written to docs/brain/implementation_plan_t[ID].md. Awaiting Director approval."

---

## STEP 3 -- P4 ADJUDICATOR AUDIT (Internal)

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

## STEP 4 -- P5 ENGINEER (Advanced/Code Mode) -- RED-GREEN

### RED Phase: Write Failing Contract Tests FIRST

Before touching src/, write the contract tests to `tests/[SubgraphName]IntegrationTests.cs`:

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

Only after ALL Step 5 audits PASS, output:

```
[BOB-TDD-COMPLETE]
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

---

## BANNED PATTERNS (immediate halt)

- `lock(anything)` -- BANNED
- `Monitor.Enter` / `Monitor.Exit` -- BANNED
- `Thread.Sleep()` anywhere -- BANNED
- Unicode / emoji / curly quotes in any string literal -- BANNED
- Manual copy-paste for extractions > 50 lines -- BANNED (use v12_split.py)
- Skipping RED phase (writing GREEN without a failing test first) -- BANNED
- Proceeding past any GATE without explicit Director confirmation -- BANNED
