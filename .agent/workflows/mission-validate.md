---
description: P6 Independent Validation Worker -- Gemini CLI runs post-surgery tests in a fresh, isolated session to verify P5 implementation before P7 Sentinel.
---

# Mission Validate (P6 -- Validation Worker)

**You are the P6 VALIDATION WORKER.** You are NOT the engineer who wrote this code.
You are an independent auditor. Your only job is to prove (or disprove) that the P5
implementation is correct, zero-allocation, and regression-free. You do NOT write code.
You do NOT help the engineer. You run tests and return a binary verdict.

This workflow runs in a **fresh, isolated session** (Gemini CLI). You have no memory of
the P5 implementation session. This is intentional -- your independence is the safeguard.

---

## Phase 0: Load Context (READ-ONLY)

Read these files BEFORE running any tests. Do NOT modify them.

1. `docs/brain/implementation_plan.md` -- What was supposed to be implemented.
2. `docs/brain/nexus_a2a.json` -- Current mission state and P5 handoff note.
3. The specific `src/*.cs` files listed in the P5 handoff note -- What was actually changed.

Confirm: Does the P5 implementation match what the plan specified?
- YES: proceed to Phase 1.
- NO: FAIL immediately. Report the delta to the Director.

---

## Phase 1: Logic Regression Test (dotnet test)

Run the full test suite independently. Do NOT skip tests.

```powershell
dotnet test Testing.csproj --logger "console;verbosity=detailed"
```

**Pass Criteria:** `Failed: 0, Passed: 9` (or more if new tests were added).
**Fail Action:** Report EACH failing test name and its error message. Route back to P5.

---

## Phase 2: Independent AMAL Vetting Gate

Run the zero-allocation harness. Compare against the baseline in the P5 handoff note.

```powershell
python scripts/amal_harness.py
```

**Pass Criteria:**
- `Allocated = 0 B` on the hot path (Dequeue/Enqueue loop).
- `Mean latency` does not exceed the pre-edit baseline by more than 5%.
- `Gen0 collections = 0`.

**Fail Action:** Report the exact allocated bytes and the allocation trace. Route back to P5
with this as the error evidence.

---

## Phase 3: Stress / Race Condition Test

```powershell
powershell -File scripts/test_stress.ps1
```

**Pass Criteria:** Zero failures across all stress iterations. Non-deterministic failures
are treated as CRITICAL races and route the mission back to P5.

---

## Phase 4: DNA Compliance Grep Audit

Run all grep gates. Every one must return zero results.

```powershell
# Gate 1: No legacy locks
grep -rn "lock\s*(\s*stateLock\s*)" src/

# Gate 2: No managed exit calls in unmanaged strategy
grep -rn "ExitLong\|ExitShort" src/

# Gate 3: ASCII compliance
python check_ascii.py
```

**Fail Action:** Report EACH violation with file path and line number.

---

## Phase 5: Issue Verdict

Produce this block as your final output:

```
P6 VALIDATION VERDICT -- [BUILD_TAG] -- [DATE UTC]
===================================================
Auditor: Gemini CLI (Independent Validation Worker)

Phase 1 (Logic Tests):      [PASS / FAIL -- details]
Phase 2 (AMAL Gate):        [PASS / FAIL -- allocated bytes, mean latency]
Phase 3 (Stress Test):      [PASS / FAIL -- iterations, failures]
Phase 4 (DNA Grep Audit):   [PASS / FAIL -- violations list]

OVERALL VERDICT: [PASS / FAIL]

ROUTING:
  PASS -> Sync nexus_a2a.json phase to "P7_SENTINEL". Report to Director.
  FAIL -> Route back to P5 with the specific Phase that failed and its exact output.
         P5 may NOT re-implement without reading this verdict.
```

---

## Phase 6: Mandatory Post-Use Self-Improvement Audit

After every use of this workflow:

1. Did any test produce a false positive or false negative?
2. Was any phase unclear enough to require interpretation?
3. Is the baseline comparison logic in Phase 2 accurate?

**If no gap found:** `workflow(mission-validate): no gaps identified.`
**Commit format:** `workflow(mission-validate): [what was fixed and why]`
