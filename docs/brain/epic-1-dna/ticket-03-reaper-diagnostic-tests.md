---
# TICKET epic-1-dna-03: REAPER Diagnostic & Unit Tests
# Epic: epic-1-dna
# Sequence: 3 of 3
# Depends on: ticket-01, ticket-02 (must complete code fixes before adding tests)
---

## Objective
Add non-blocking REAPER diagnostic assertion to detect orphaned FSM positions after 10-second grace period, and create comprehensive unit test suite to validate all 4 bug fixes (H05, H08, H21, H22).

## Scope
IN scope:
- `src/V12_002.REAPER.cs` - Add `_orphanedPositionFirstSeen` field
- `src/V12_002.REAPER.Audit.cs` - Add diagnostic assertion in `AuditSingleFleetAccount()`
- `tests/Build981ComplianceTests.cs` - Create new test file with 5 unit tests

OUT of scope:
- REAPER Emergency Flatten logic modifications
- Existing integration test modifications
- Manual test scenario documentation (covered in approach doc)

## Context References
- Analysis: docs/brain/epic-1-dna/01-analysis.md -- Section 3 (REAPER dependencies)
- Approach: docs/brain/epic-1-dna/02-approach.md -- Section 3 (REAPER diagnostic design), Section 6 (testing strategy)

## Implementation Instructions

### Part 1: REAPER Diagnostic Field

**File:** `src/V12_002.REAPER.cs`

**Add new field (after existing ConcurrentDictionary declarations):**
```csharp
// [BUILD 981 DIAGNOSTIC]: Tracks when an orphaned FSM position was first detected.
// Key = entry name; Value = UTC time of first detection.
private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen 
    = new ConcurrentDictionary<string, DateTime>();
```

### Part 2: REAPER Diagnostic Assertion

**File:** `src/V12_002.REAPER.Audit.cs`

**Method:** `AuditSingleFleetAccount(Account acct, bool shouldLog)`

**Insertion Point:** After line 105 (existing desync detection logic)

**Add diagnostic assertion block:**
```csharp
// [BUILD 981 DIAGNOSTIC]: Detect orphaned FSM positions after grace period.
// Orphaned position = activePositions entry exists but broker position is flat.
// This is a diagnostic assertion -- logs warning but does NOT trigger flatten.
// KEY MAPPING (Director-verified):
//   - activePositions uses FSM entryName as key (e.g., "RetestLong_12345678")
//   - expectedPositions uses ExpKey(accountName) as key (composite key)
if (actualQty == 0 && activePositions.ContainsKey(entryName))
{
    // Check if grace period has expired (10 seconds)
    DateTime? firstSeen = _orphanedPositionFirstSeen.GetOrAdd(entryName, DateTime.UtcNow);
    double graceElapsed = (DateTime.UtcNow - firstSeen.Value).TotalSeconds;
    
    if (graceElapsed > 10.0)
    {
        // Grace expired -- log diagnostic warning
        Print($"[REAPER][DIAGNOSTIC] Orphaned FSM position detected: {acct.Name} entry={entryName}. " +
              $"Broker flat but activePositions entry exists after {graceElapsed:F1}s grace. " +
              $"This may indicate a TOCTOU race in entry rollback logic.");
        
        // Clear first-seen timestamp to avoid log spam
        _orphanedPositionFirstSeen.TryRemove(entryName, out _);
    }
}
else
{
    // Position is live or activePositions is clean -- clear first-seen timestamp
    _orphanedPositionFirstSeen.TryRemove(entryName, out _);
}
```

### Part 3: Unit Test Suite

**File:** `tests/Build981ComplianceTests.cs` (NEW FILE)

**Create test file with 5 test cases:**

1. `Test_H05_CreateNewStopOrder_SynchronousWrite()`
   - Submit bracket order
   - Verify `stopOrders` contains entry before actor drain
   - Flatten immediately
   - Verify no ghost orders

2. `Test_H08_CreateDirectStopOrder_SynchronousWrite()`
   - Trigger trailing stop update
   - Verify `stopOrders` contains replacement before actor drain
   - Flatten immediately
   - Verify no ghost orders

3. `Test_H21_ExecuteRetestEntry_SynchronousAdd()`
   - Trigger auto retest entry
   - Force broker rejection
   - Verify `activePositions` is clean (no ghost position)

4. `Test_H22_ExecuteRetestManualEntry_SynchronousAdd()`
   - Trigger manual retest entry
   - Force broker rejection
   - Verify `activePositions` is clean (no ghost position)

5. `Test_REAPER_DiagnosticAssertion_OrphanedPosition()`
   - Manually inject orphaned position into `activePositions`
   - Wait 10 seconds
   - Verify REAPER diagnostic log appears
   - Verify no Emergency Flatten triggered

**Note:** Reference existing test patterns from `tests/MetricsIntegrationTests.cs` and `tests/OrchestrationIntegrationTests.cs` for test infrastructure setup.

## V12 DNA Guardrails
- [x] Zero new lock() statements
- [x] Zero non-ASCII characters in string literals (diagnostic log message)
- [x] REAPER diagnostic is non-blocking (logs only, no Emergency Flatten)
- [x] 10-second grace period prevents false positives during broker-confirm lag
- [x] All test methods follow existing test naming conventions

## Post-Edit Verification (Mandatory)
```powershell
# 1. Re-establish hard links (MANDATORY after every src/ edit)
powershell -File .\deploy-sync.ps1

# 2. Lock regression (must return ZERO)
grep -r "lock(" src/V12_002.REAPER.cs
grep -r "lock(" src/V12_002.REAPER.Audit.cs

# 3. ASCII gate (must return ZERO)
grep -Prn "[^\x00-\x7F]" src/V12_002.REAPER.cs
grep -Prn "[^\x00-\x7F]" src/V12_002.REAPER.Audit.cs

# 4. Verify field declaration
grep -n "_orphanedPositionFirstSeen" src/V12_002.REAPER.cs
# Expected: 2 matches (field declaration + usage in Audit.cs)

# 5. Verify diagnostic assertion
grep -n "BUILD 981 DIAGNOSTIC" src/V12_002.REAPER.Audit.cs
# Expected: 1 match

# 6. Run unit tests
dotnet test tests/Build981ComplianceTests.cs
# Expected: 5 tests pass
```

## Acceptance Criteria
- [x] `_orphanedPositionFirstSeen` field added to `src/V12_002.REAPER.cs`
- [x] Diagnostic assertion added to `AuditSingleFleetAccount()` in `src/V12_002.REAPER.Audit.cs`
- [x] Diagnostic uses correct key mapping (`entryName` for `activePositions`)
- [x] Diagnostic logs warning after 10-second grace period
- [x] Diagnostic does NOT trigger Emergency Flatten
- [x] `tests/Build981ComplianceTests.cs` created with 5 unit tests
- [x] All 5 unit tests pass
- [x] deploy-sync.ps1 ASCII gate: PASS
- [x] lock() audit: ZERO matches in modified files
- [x] Director presses F5 in NinjaTrader -- BUILD_TAG banner visible