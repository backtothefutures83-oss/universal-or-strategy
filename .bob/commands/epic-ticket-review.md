---
description: Review a completed ticket against its specification for drift detection.
argument-hint: <epic-slug> <ticket-number>
---
# EPIC TICKET REVIEW
**Epic:** $1
**Ticket:** $2
**Mode:** v12-validator (Read-Only Verification)
**Protocol:** Droid Missions Validation Pattern

> This command verifies that a completed ticket matches its specification.
> Detects logic drift, scope creep, and DNA violations.
> Read-only mode: no edits, only verification.

---

## STEP 1 -- LOAD TICKET SPECIFICATION

Read the ticket file at `docs/brain/$1/ticket-$2-*.md`. Extract:
- Objective and scope boundaries
- Sub-methods to extract (names, responsibilities)
- Acceptance criteria
- V12 DNA guardrails

---

## STEP 2 -- VERIFY IMPLEMENTATION

### 2a. File Outline Comparison
Use `get_file_outline` on each modified file.
Compare actual symbol names against ticket specification.

**Check:**
- [ ] All specified sub-methods created?
- [ ] Method names match specification?
- [ ] No extra methods added (scope creep)?

### 2b. Complexity Verification
```powershell
python scripts/complexity_audit.py
```

**Check:**
- [ ] Target method CYC < 20?
- [ ] CYC reduction matches estimate?
- [ ] No new high-complexity methods?

### 2c. Blast Radius Verification
Use `get_blast_radius` on modified methods.

**Check:**
- [ ] Caller count matches analysis?
- [ ] No unexpected callers added?
- [ ] All call sites updated correctly?

---

## STEP 3 -- DNA COMPLIANCE AUDIT

### 3a. Lock-Free Compliance
```powershell
grep -r "lock(" src/
```
**Expected:** 0 matches

### 3b. ASCII Compliance
```powershell
grep -Prn "[^\x00-\x7F]" src/
```
**Expected:** 0 matches

### 3c. Deploy-Sync Verification
```powershell
powershell -File .\deploy-sync.ps1
```
**Expected:** ASCII gate PASS

### 3d. Semgrep (V12 DNA Patterns)
```powershell
powershell -File .\scripts\run_semgrep.ps1
```
**Expected:** 0 V12 DNA violations

---

## STEP 4 -- LOGIC DRIFT DETECTION

### 4a. Compare Implementation to Specification
For each sub-method created:
- Read actual implementation using `get_symbol_source`
- Compare against ticket's responsibility description
- Flag any logic changes beyond pure structural movement

**Red Flags:**
- New conditional logic not in original method
- Changed algorithm or calculation
- Modified state mutation patterns
- Added external dependencies

### 4b. Scope Creep Detection
**Check:**
- [ ] Only specified methods extracted?
- [ ] No "improvements" or "optimizations"?
- [ ] No refactoring of adjacent code?
- [ ] No formatting changes to untouched lines?

---

## STEP 5 -- TEST COVERAGE VERIFICATION

### 5a. Test Existence
```powershell
# Check for test file
ls tests/V12_Performance.Tests/*Tests.cs | grep -i <feature>
```

**Check:**
- [ ] Test file exists?
- [ ] Test covers new sub-methods?
- [ ] Test follows TDD RED-GREEN-REFACTOR pattern?

### 5b. Test Execution
```powershell
dotnet test --filter <TestName>
```

**Check:**
- [ ] All tests pass?
- [ ] No skipped tests?
- [ ] No flaky tests?

---

## STEP 6 -- GENERATE REVIEW REPORT

Output format:
```
[TICKET-REVIEW] $1 / ticket-$2
========================================
SPECIFICATION COMPLIANCE
  Sub-methods created : [N of N expected]
  Method names match  : YES / NO [details]
  Scope adherence     : CLEAN / DRIFT [details]

DNA COMPLIANCE
  Lock-free           : PASS / FAIL
  ASCII-only          : PASS / FAIL
  Deploy-sync         : PASS / FAIL
  Semgrep             : PASS / FAIL

COMPLEXITY
  Target method CYC   : [before] -> [after] (target: < 20)
  CYC reduction       : [actual] vs [estimated]
  New methods CYC     : [list with scores]

LOGIC DRIFT
  Pure structural     : YES / NO [details]
  Scope creep         : NONE / DETECTED [details]
  Unexpected changes  : NONE / DETECTED [details]

TEST COVERAGE
  Test file exists    : YES / NO
  Tests pass          : YES / NO
  TDD compliance      : YES / NO

========================================
Verdict: PASS / FAIL
Recommendation: [APPROVE / REVISE / REJECT]
========================================
```

---

## STEP 7 -- HANDOFF TO DIRECTOR

If Verdict: PASS:
> "[TICKET-REVIEW-PASS] Ticket $2 matches specification. No drift detected. Ready for next ticket."

If Verdict: FAIL:
> "[TICKET-REVIEW-FAIL] Ticket $2 has issues. See report above. Recommend: [REVISE / REJECT]"

**CRITICAL:** If FAIL, DO NOT advance to next ticket. Fix issues first.