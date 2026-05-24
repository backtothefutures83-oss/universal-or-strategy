---
name: milestone-validation
description: Droid-style milestone validation after each ticket. Prevents drift and catches regressions early. Use after every ticket execution.
user-invocable: false
disable-model-invocation: false
---

# Milestone Validation (Droid Missions Pattern)

## Purpose
Run comprehensive validation after EVERY ticket to prevent drift and catch regressions early. Inspired by Droid Factory Missions pattern.

## When to Use
- After EVERY ticket execution (not just at end of epic)
- Before advancing to next ticket
- After any src/ file modification

## Protocol

### Step 1: Full Test Suite
```powershell
dotnet test
# Expected: All tests PASS
```

### Step 2: Benchmarks
```powershell
dotnet run --project benchmarks --configuration Release
# Expected: No performance regressions
```

### Step 3: DNA Audits
```powershell
# Deploy-sync (hard links + ASCII)
powershell -File .\deploy-sync.ps1
# Expected: ASCII gate PASS

# Lock-free compliance
grep -r "lock(" src/
# Expected: 0 matches

# Unicode compliance
grep -Prn "[^\x00-\x7F]" src/
# Expected: 0 matches

# Complexity compliance
python scripts/complexity_audit.py
# Expected: All methods CYC < 20
```

### Step 4: Semgrep (V12 DNA Patterns)
```powershell
powershell -File .\scripts\run_semgrep.ps1
# Expected: 0 V12 DNA violations
```

### Step 5: Dead Code Scan
```powershell
python scripts/dead_code_scan.py
# Expected: No new dead code
```

### Step 6: Graphify Update
```powershell
graphify update . --silent
# Expected: Knowledge graph updated
```

## Validation Report Format

```
[MILESTONE-VALIDATION] Ticket XX
========================================
Test Suite      : PASS (X tests)
Benchmarks      : PASS (no regressions)
Deploy-Sync     : PASS
Lock() Audit    : CLEAN (0 matches)
Unicode Audit   : CLEAN (0 matches)
Complexity      : PASS (all < 20)
Semgrep         : PASS (0 violations)
Dead Code       : PASS (no new dead code)
Graphify        : UPDATED
========================================
Verdict: PASS / FAIL
```

## Failure Handling

**If ANY validation fails:**
1. HALT immediately
2. Report failure to Director
3. DO NOT advance to next ticket
4. Fix issue before continuing

**Common Failures:**
- Test failures → Fix broken tests or implementation
- Lock() violations → Refactor to FSM/Enqueue pattern
- Unicode violations → Replace with ASCII
- CYC > 20 → Extract methods
- Semgrep violations → Fix V12 DNA issues

## Droid Missions Alignment

**From Droid Factory:**
- Validation workers run at end of each milestone
- Prevents drift and reduces rework
- More frequent validation for complex projects

**V12 Adoption:**
- Run after EVERY ticket (not just milestones)
- Block next ticket if validation fails
- Comprehensive DNA audit included

## Success Criteria
- [ ] All tests pass
- [ ] No performance regressions
- [ ] DNA audits clean
- [ ] Semgrep clean
- [ ] No new dead code
- [ ] Graphify updated

## Cost Estimation (from Droid)
```
total runs ≈ #tickets + 2 * #validation_runs
```

V12: Validation runs after EVERY ticket, so:
```
total validation runs = #tickets
```

## Next Step
If validation PASSES: Advance to next ticket
If validation FAILS: Fix issues before continuing