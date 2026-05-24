# Pre-PR Quality Gate - 13 Exhaustive Tests

## Overview
The Pre-PR Quality Gate is a comprehensive validation suite that runs 13 exhaustive tests before PR submission. This ensures all V12 DNA mandates are met and prevents regressions.

## Purpose
- **Catch issues locally** before CI/CD
- **Reduce PR loop iterations** by fixing issues upfront
- **Enforce V12 DNA** compliance at every PR
- **Prevent regressions** in performance, quality, and architecture

## Usage

```powershell
# Run full quality gate
powershell -File .\scripts\pre_pr_quality_gate.ps1

# Run with verbose output
powershell -File .\scripts\pre_pr_quality_gate.ps1 -Verbose
```

## 13 Tests

### Test 1: Full Test Suite
**Purpose:** Verify all unit and integration tests pass
**Command:** `dotnet test --no-build --verbosity quiet`
**Pass Criteria:** EXIT 0, all tests pass
**Failure Impact:** HARD BLOCK - Cannot submit PR with failing tests

### Test 2: Benchmarks (No Regressions)
**Purpose:** Verify Epic 5 performance targets maintained
**Command:** `dotnet run --project benchmarks --configuration Release --no-build`
**Pass Criteria:** 0 B allocation, < 300μs latency
**Failure Impact:** HARD BLOCK - Performance regression detected

### Test 3: Deploy-Sync (Hard Links + ASCII)
**Purpose:** Verify NinjaTrader hard links synced and ASCII compliance
**Command:** `powershell -File .\deploy-sync.ps1`
**Pass Criteria:** ASCII gate PASS, hard links synced
**Failure Impact:** HARD BLOCK - NinjaTrader will not compile

### Test 4: Lock-Free Compliance
**Purpose:** Verify zero `lock()` statements in src/
**Command:** `Select-String -Path "src\*.cs" -Pattern "lock\(" -SimpleMatch`
**Pass Criteria:** 0 matches
**Failure Impact:** HARD BLOCK - V12 DNA violation

### Test 5: Unicode Compliance (ASCII-Only)
**Purpose:** Verify zero non-ASCII characters in src/
**Command:** `Select-String -Path "src\*.cs" -Pattern "[^\x00-\x7F]"`
**Pass Criteria:** 0 matches
**Failure Impact:** HARD BLOCK - V12 DNA violation

### Test 6: Complexity Compliance (CYC < 20)
**Purpose:** Verify all methods below cyclomatic complexity threshold
**Command:** `python scripts/complexity_audit.py`
**Pass Criteria:** All methods CYC < 20
**Failure Impact:** SOFT WARN - Should refactor but not blocking

### Test 7: Semgrep (V12 DNA Patterns)
**Purpose:** Verify V12 DNA patterns enforced
**Command:** `powershell -File .\scripts\run_semgrep.ps1`
**Pass Criteria:** 0 V12 DNA violations
**Failure Impact:** HARD BLOCK - Architecture violation

### Test 8: Dead Code Scan
**Purpose:** Verify no unreachable code
**Command:** `python scripts/dead_code_scan.py`
**Pass Criteria:** No new dead code
**Failure Impact:** SOFT WARN - Should clean up but not blocking

### Test 9: Build Readiness
**Purpose:** Verify project builds successfully
**Command:** `powershell -File .\scripts\build_readiness.ps1`
**Pass Criteria:** Build succeeds
**Failure Impact:** HARD BLOCK - Cannot merge broken build

### Test 10: Format Check (CSharpier)
**Purpose:** Verify code formatting consistent
**Command:** `dotnet csharpier --check .`
**Pass Criteria:** No format violations
**Failure Impact:** SOFT WARN - Auto-fix available

### Test 11: PR Hygiene (Rebase + Clean)
**Purpose:** Verify branch rebased on main and clean
**Command:** `powershell -File .\scripts\verify_pr_hygiene.ps1`
**Pass Criteria:** Branch up-to-date, no conflicts
**Failure Impact:** HARD BLOCK - Must rebase before PR

### Test 12: Graphify Update
**Purpose:** Verify knowledge graph updated
**Command:** `graphify update . --silent`
**Pass Criteria:** Graph updated successfully
**Failure Impact:** SOFT WARN - Graph may be stale

### Test 13: TDD Compliance (Tests Exist)
**Purpose:** Verify test coverage >= 50%
**Command:** Count src/ files vs test files
**Pass Criteria:** Test coverage >= 50%
**Failure Impact:** HARD BLOCK - Insufficient test coverage

## Output Format

```
========================================
Pre-PR Quality Gate
========================================

[TEST] Full Test Suite...
[PASS] Full Test Suite

[TEST] Benchmarks (No Regressions)...
[PASS] Benchmarks (No Regressions)

... (11 more tests)

========================================
Quality Gate Summary
========================================
Full Test Suite: PASS
Benchmarks (No Regressions): PASS
Deploy-Sync (Hard Links + ASCII): PASS
Lock-Free Compliance: PASS
Unicode Compliance (ASCII-Only): PASS
Complexity Compliance (CYC < 20): PASS
Semgrep (V12 DNA Patterns): PASS
Dead Code Scan: PASS
Build Readiness: PASS
Format Check (CSharpier): PASS
PR Hygiene (Rebase + Clean): PASS
Graphify Update: PASS
TDD Compliance (Tests Exist): PASS

Total Tests: 13
Passed: 13
Failed: 0

[APPROVED] Quality gate PASSED. Ready for PR submission.
```

## Integration with Workflows

### Manual Pre-PR Check
```powershell
# Before creating PR
powershell -File .\scripts\pre_pr_quality_gate.ps1

# If PASS: Create PR
gh pr create --title "..." --body "..."

# If FAIL: Fix issues and re-run
```

### Automated Pre-Push Hook
```bash
#!/bin/bash
# .git/hooks/pre-push

echo "Running Pre-PR Quality Gate..."
powershell -File ./scripts/pre_pr_quality_gate.ps1

if [ $? -ne 0 ]; then
    echo "Quality gate FAILED. Push blocked."
    exit 1
fi

echo "Quality gate PASSED. Proceeding with push."
exit 0
```

### CI/CD Integration
```yaml
# .github/workflows/quality-gate.yml
name: Quality Gate

on: [push, pull_request]

jobs:
  quality-gate:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Quality Gate
        run: powershell -File .\scripts\pre_pr_quality_gate.ps1
```

## Troubleshooting

### Test 1 Fails (Tests)
**Fix:** Run `dotnet test` locally, fix failing tests

### Test 2 Fails (Benchmarks)
**Fix:** Investigate performance regression, optimize hot path

### Test 3 Fails (Deploy-Sync)
**Fix:** Run `powershell -File .\deploy-sync.ps1` manually, fix ASCII violations

### Test 4 Fails (Lock-Free)
**Fix:** Refactor `lock()` statements to FSM/Enqueue pattern

### Test 5 Fails (Unicode)
**Fix:** Replace non-ASCII characters with ASCII equivalents

### Test 6 Fails (Complexity)
**Fix:** Extract methods to reduce CYC below 20

### Test 7 Fails (Semgrep)
**Fix:** Address V12 DNA violations flagged by Semgrep

### Test 11 Fails (PR Hygiene)
**Fix:** Rebase branch on main: `git fetch origin main && git rebase origin/main`

### Test 13 Fails (TDD Compliance)
**Fix:** Add missing tests to reach 50% coverage

## References
- [TDD Hardening Protocol](TDD_HARDENING_PROTOCOL.md)
- [TDD Integration Matrix](TDD_INTEGRATION_MATRIX.md)
- [Pre-PR Quality Gate Script](../../scripts/pre_pr_quality_gate.ps1)