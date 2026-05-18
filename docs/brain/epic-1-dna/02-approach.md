# Epic: epic-1-dna -- Refactoring Approach & Implementation Strategy

## Executive Summary

This document defines the surgical refactoring strategy for resolving 4 Build 981 protocol violations and TOCTOU race conditions in the V12 execution core. The approach implements synchronous atomic writes for stop order registration (H05, H08) and synchronous add + synchronous rollback for Retest entry logic (H21, H22).

**Director-Approved Decisions:**
1. Synchronous atomic writes for H05 and H08 (stop order registration)
2. Option A (Synchronous Add + Synchronous Rollback) for H21 and H22 (Retest fixes)
3. Mandatory inline comments documenting Build 981 exemption at all sync write sites
4. Non-blocking runtime diagnostic assertion in REAPER to detect orphaned FSM positions

**Constraints:**
- ZERO new `lock()` statements (DNA mandate)
- PR diff size strictly under 150,000 characters
- ASCII-only compliance in string literals
- F5 compile gate + NinjaTrader hard-link sync via `deploy-sync.ps1`

---

## 1. Refactoring Strategy Overview

### Pattern A: Synchronous Atomic Write (H05, H08)

**Problem:**
- `Enqueue(ctx => { ctx.stopOrders[key] = order; });` creates async write window
- If flatten occurs before actor mailbox drains, stop orders are unmapped at broker but still tracked
- Result: Ghost orders (orphaned protection orders)

**Solution:**
- Replace `Enqueue` with direct synchronous write: `stopOrders[key] = order;`
- ConcurrentDictionary single-writes are thread-safe (no lock required)
- Add inline comment documenting Build 981 exemption

**Code Pattern:**
```csharp
// OLD (Build 981 violation):
Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; });

// NEW (Build 981 compliant):
// [BUILD 981 EXEMPTION]: Synchronous write to stopOrders during bracket submission.
// Prevents ghost-order tracking window if flatten occurs before actor drain.
// ConcurrentDictionary single-write is thread-safe (no lock required).
stopOrders[_en966] = _ns966;
```

---

### Pattern B: Synchronous Add + Synchronous Rollback (H21, H22)

**Problem:**
- `Enqueue(ctx => { ctx.activePositions[key] = pos; });` (async add)
- `activePositions.TryRemove(key, out _);` (sync remove)
- **TOCTOU Race:** If broker submission fails, sync remove executes before queued addition
- Result: Permanent FSM state leak (ghost position in `activePositions`)

**Solution:**
- Replace async `Enqueue` add with synchronous write: `activePositions[key] = pos;`
- Keep synchronous `TryRemove` in catch block (rollback on failure)
- Add inline comment documenting synchronous mutation

**Code Pattern:**
```csharp
// OLD (TOCTOU race):
Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });
try {
    SubmitOrder(...);
} catch {
    activePositions.TryRemove(entryName, out _); // Sync remove before async add → RACE
}

// NEW (Race-free):
// [BUILD 981 EXEMPTION]: Synchronous write to activePositions before broker submission.
// Prevents TOCTOU race where rollback executes before queued addition.
// ConcurrentDictionary single-write is thread-safe (no lock required).
activePositions[_en966] = _p966;
try {
    SubmitOrder(...);
} catch {
    activePositions.TryRemove(entryName, out _); // Rollback on failure
}
```

---

## 2. Implementation Plan by Target

### H05: CreateNewStopOrder() (src/V12_002.Orders.Management.StopSync.cs)

**File:** `src/V12_002.Orders.Management.StopSync.cs`
**Method:** `CreateNewStopOrder(string entryName, int quantity, double stopPrice, MarketPosition direction, bool isRecovery = false)`
**Line:** 320 (violation site)

**Note:** Method signature updated to reflect actual source code. The core refactoring pattern (synchronous write to `stopOrders`) remains unchanged.

**Change:**
```csharp
// BEFORE (line 320):
Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; });

// AFTER:
// [BUILD 981 EXEMPTION]: Synchronous write to stopOrders during bracket submission.
// Prevents ghost-order tracking window if flatten occurs before actor drain.
// ConcurrentDictionary single-write is thread-safe (no lock required).
stopOrders[_en966] = _ns966;
```

**Verification:**
- Grep confirm: `grep -n "stopOrders\[_en966\]" src/V12_002.Orders.Management.StopSync.cs`
- Expected: Line 320 shows synchronous write (no `Enqueue`)
- F5 compile gate: Must compile without errors
- Manual test: Submit bracket order, verify stop registration before flatten

---

### H08: CreateDirectStopOrder() (src/V12_002.Trailing.StopUpdate.cs)

**File:** `src/V12_002.Trailing.StopUpdate.cs`
**Method:** `CreateDirectStopOrder(string entryName, PositionInfo pos, double validatedStopPrice, int newTrailLevel)`
**Line Range:** 254-290 (method definition)
**Lines:** 264, 276 (violation sites)

**Director Verification:** Method exists at line 254. Parser cache hallucination resolved.

**Change 1 (line 264):**
```csharp
// BEFORE:
Enqueue(ctx => { ctx._followerTargetReplaceSpecs[...] = ...; });

// AFTER:
// [BUILD 981 EXEMPTION]: Synchronous write to _followerTargetReplaceSpecs during stop replacement.
// Prevents ghost-order tracking window if flatten occurs before actor drain.
// ConcurrentDictionary single-write is thread-safe (no lock required).
_followerTargetReplaceSpecs[...] = ...;
```

**Change 2 (line 276):**
```csharp
// BEFORE:
Enqueue(ctx => { ctx.stopOrders[...] = ...; });

// AFTER:
// [BUILD 981 EXEMPTION]: Synchronous write to stopOrders during stop replacement.
// Prevents ghost-order tracking window if flatten occurs before actor drain.
// ConcurrentDictionary single-write is thread-safe (no lock required).
stopOrders[...] = ...;
```

**Verification:**
- Grep confirm: `grep -n "Enqueue.*stopOrders" src/V12_002.Trailing.StopUpdate.cs`
- Expected: Zero matches (all `Enqueue` calls removed)
- F5 compile gate: Must compile without errors
- Manual test: Trigger trailing stop update, verify stop replacement before flatten

---

### H21: ExecuteRetestEntry() (src/V12_002.Entries.Retest.cs)

**File:** `src/V12_002.Entries.Retest.cs`  
**Method:** `ExecuteRetestEntry(string entryName, MarketPosition direction, int qty, double entryPrice, string reason)`  
**Lines:** 173 (async add), 187 (sync remove)

**Change:**
```csharp
// BEFORE (line 173):
Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });

// AFTER:
// [BUILD 981 EXEMPTION]: Synchronous write to activePositions before broker submission.
// Prevents TOCTOU race where rollback (line 187) executes before queued addition.
// ConcurrentDictionary single-write is thread-safe (no lock required).
activePositions[_en966] = _p966;

// Line 187 (sync remove in catch block) remains unchanged:
activePositions.TryRemove(entryName, out _);
```

**Verification:**
- Grep confirm: `grep -n "activePositions\[_en966\]" src/V12_002.Entries.Retest.cs`
- Expected: Line 173 shows synchronous write (no `Enqueue`)
- F5 compile gate: Must compile without errors
- Manual test: Trigger auto retest entry, force broker rejection, verify no ghost position

---

### H22: ExecuteRetestManualEntry() (src/V12_002.Entries.Retest.cs)

**File:** `src/V12_002.Entries.Retest.cs`  
**Method:** `ExecuteRetestManualEntry(string entryName, MarketPosition direction, int qty, double entryPrice)`  
**Lines:** 310 (async add), 324 (sync remove)

**Change:**
```csharp
// BEFORE (line 310):
Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });

// AFTER:
// [BUILD 981 EXEMPTION]: Synchronous write to activePositions before broker submission.
// Prevents TOCTOU race where rollback (line 324) executes before queued addition.
// ConcurrentDictionary single-write is thread-safe (no lock required).
activePositions[_en966] = _p966;

// Line 324 (sync remove in catch block) remains unchanged:
activePositions.TryRemove(entryName, out _);
```

**Verification:**
- Grep confirm: `grep -n "activePositions\[_en966\]" src/V12_002.Entries.Retest.cs`
- Expected: Line 310 shows synchronous write (no `Enqueue`)
- F5 compile gate: Must compile without errors
- Manual test: Trigger manual retest entry, force broker rejection, verify no ghost position

---

## 3. REAPER Diagnostic Assertion Design

### Objective

Add a non-blocking, time-gated runtime diagnostic assertion in REAPER to detect orphaned FSM positions (positions in `activePositions` with no corresponding broker position after grace period expires).

### Design Constraints

1. **Non-blocking:** Must not interfere with normal REAPER audit cycle
2. **Time-gated:** Only fire after grace period (10 seconds) to avoid false positives during broker-confirm lag
3. **Diagnostic-only:** Log warning, do NOT trigger Emergency Flatten
4. **ASCII-only:** No Unicode in string literals

### Implementation Location

**File:** `src/V12_002.REAPER.Audit.cs`  
**Method:** `AuditSingleFleetAccount(Account acct, bool shouldLog)`  
**Insertion Point:** After line 105 (existing desync detection logic)

### Pseudocode

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

### Required State Tracking

**File:** `src/V12_002.REAPER.cs`  
**New Field:**
```csharp
// [BUILD 981 DIAGNOSTIC]: Tracks when an orphaned FSM position was first detected.
// Key = entry name; Value = UTC time of first detection.
private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen 
    = new ConcurrentDictionary<string, DateTime>();
```

### Verification

- Grep confirm: `grep -n "_orphanedPositionFirstSeen" src/V12_002.REAPER.cs`
- Expected: Field declaration + usage in AuditSingleFleetAccount
- Manual test: Force TOCTOU race (async-add/sync-remove), verify diagnostic log after 10s

---

## 4. Inline Comment Strategy

### Comment Template

All synchronous writes must include this 3-line comment block:

```csharp
// [BUILD 981 EXEMPTION]: Synchronous write to <dictionary> during <operation>.
// Prevents <specific race condition or ghost-order window>.
// ConcurrentDictionary single-write is thread-safe (no lock required).
<synchronous write statement>
```

### Comment Placement

- **H05:** Line 320 in `src/V12_002.Orders.Management.StopSync.cs`
- **H08:** Lines 264, 276 in `src/V12_002.Trailing.StopUpdate.cs`
- **H21:** Line 173 in `src/V12_002.Entries.Retest.cs`
- **H22:** Line 310 in `src/V12_002.Entries.Retest.cs`

### Rationale

- **Searchability:** `grep -r "BUILD 981 EXEMPTION" src/` finds all exemption sites
- **Auditability:** Future reviewers understand why synchronous write is used
- **DNA Compliance:** Documents intentional deviation from actor pattern

---

## 5. Risk Mitigation Strategy

### Risk 1: REAPER False Flatten (CRITICAL)

**Mitigation:**
- Synchronous add + synchronous rollback eliminates TOCTOU window
- REAPER diagnostic assertion detects orphaned positions after 10s grace
- Manual testing with forced broker rejections to verify no ghost positions

**Verification:**
- Unit test: Force broker rejection in H21/H22, verify `activePositions` is clean
- Integration test: Run REAPER audit cycle, verify no false flattens
- Stress test: 100 concurrent entry submissions, verify no ghost positions

---

### Risk 2: Ghost Order Tracking (HIGH)

**Mitigation:**
- Synchronous atomic writes for `stopOrders` eliminate async window
- Inline comments document Build 981 exemption
- Manual testing with flatten during bracket submission to verify no ghost orders

**Verification:**
- Unit test: Submit bracket, flatten immediately, verify `stopOrders` is clean
- Integration test: Run REAPER naked-position audit, verify no false emergency stops
- Stress test: 100 concurrent bracket submissions + flattens, verify no ghost orders

---

### Risk 3: FSM State Leak (HIGH)

**Mitigation:**
- Synchronous add pattern eliminates async-add/sync-remove race
- REAPER diagnostic assertion detects orphaned positions after 10s grace
- Manual testing with forced broker rejections to verify no state leaks

**Verification:**
- Unit test: Force broker rejection in H21/H22, verify `activePositions` is clean
- Integration test: Run REAPER audit cycle, verify no orphaned positions detected
- Stress test: 100 concurrent entry submissions with 50% rejection rate, verify no leaks

---

### Risk 4: Diff Size Overflow (MEDIUM)

**Mitigation:**
- Surgical changes only (4 methods, ~10 lines total)
- No whitespace mutations (STRICT DIFF LIMIT enforcement)
- Pre-check via `deploy-sync.ps1` DIFF GUARD before push

**Verification:**
- Run `deploy-sync.ps1` after all changes
- Verify DIFF GUARD passes (< 150,000 characters)
- If DIFF GUARD fails, isolate logic changes and revert whitespace bloat

---

## 6. Testing Strategy

### Unit Tests (NEW)

**Test File:** `tests/Build981ComplianceTests.cs` (to be created)

**Test Cases:**
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

---

### Integration Tests (EXISTING)

**Test File:** `tests/OrchestrationIntegrationTests.cs`

**Test Cases:**
- Existing orchestration tests cover end-to-end flows
- No new integration tests required (unit tests cover edge cases)

---

### Manual Testing (F5 Compile Gate)

**Test Scenarios:**
1. **H05/H08 Verification:**
   - Submit bracket order in NinjaTrader
   - Flatten immediately (before actor drain)
   - Verify no ghost orders in `stopOrders` dictionary
   - Verify REAPER does not fire false naked-position alert

2. **H21/H22 Verification:**
   - Trigger auto retest entry (H21) or manual retest entry (H22)
   - Force broker rejection (disconnect broker, invalid price, etc.)
   - Verify no ghost position in `activePositions` dictionary
   - Verify REAPER does not fire false desync flatten

3. **REAPER Diagnostic Verification:**
   - Manually inject orphaned position into `activePositions`
   - Wait 10 seconds
   - Verify REAPER diagnostic log appears
   - Verify no Emergency Flatten triggered

---

## 7. Rollback Plan

### Rollback Trigger Conditions

1. F5 compile gate fails (syntax errors, missing references)
2. DIFF GUARD fails (PR diff > 150,000 characters)
3. REAPER false flatten detected in manual testing
4. Ghost orders detected in manual testing
5. FSM state leak detected in stress testing

### Rollback Procedure

1. **Revert all changes:**
   ```powershell
   git checkout HEAD -- src/V12_002.Orders.Management.StopSync.cs
   git checkout HEAD -- src/V12_002.Trailing.StopUpdate.cs
   git checkout HEAD -- src/V12_002.Entries.Retest.cs
   git checkout HEAD -- src/V12_002.REAPER.cs
   git checkout HEAD -- src/V12_002.REAPER.Audit.cs
   ```

2. **Re-sync NinjaTrader hard links:**
   ```powershell
   powershell -File .\deploy-sync.ps1
   ```

3. **Verify rollback:**
   - F5 in NinjaTrader
   - Verify BUILD_TAG matches pre-refactor state
   - Run existing integration tests

4. **Document rollback reason:**
   - Create `docs/brain/epic-1-dna/rollback-report.md`
   - Document trigger condition, symptoms, and root cause
   - Propose alternative approach for next iteration

---

## 8. Success Criteria

### Functional Requirements

1. **H05/H08 Fixed:** Zero ghost orders after flatten during bracket submission
2. **H21/H22 Fixed:** Zero ghost positions after broker rejection in Retest entries
3. **REAPER Diagnostic:** Orphaned positions detected and logged after 10s grace
4. **DNA Compliance:** Zero new `lock()` statements in execution paths

### Non-Functional Requirements

1. **Diff Size:** PR diff < 150,000 characters (DIFF GUARD passes)
2. **ASCII Compliance:** No Unicode in string literals
3. **F5 Compile Gate:** All src/ files compile without errors
4. **Hard-Link Sync:** `deploy-sync.ps1` succeeds without errors

### Test Coverage

1. **Unit Tests:** 5 new tests in `tests/Build981ComplianceTests.cs`
2. **Integration Tests:** Existing tests pass without modification
3. **Manual Tests:** All 3 manual test scenarios pass

---

## 9. Implementation Checklist

### Phase 1: Code Changes

- [ ] H05: Replace `Enqueue` with synchronous write in `CreateNewStopOrder()` (line 320)
- [ ] H05: Add inline comment documenting Build 981 exemption
- [ ] H08: Replace `Enqueue` with synchronous write in `CreateDirectStopOrder()` (lines 264, 276)
- [ ] H08: Add inline comments documenting Build 981 exemption
- [ ] H21: Replace `Enqueue` with synchronous write in `ExecuteRetestEntry()` (line 173)
- [ ] H21: Add inline comment documenting Build 981 exemption
- [ ] H22: Replace `Enqueue` with synchronous write in `ExecuteRetestManualEntry()` (line 310)
- [ ] H22: Add inline comment documenting Build 981 exemption

### Phase 2: REAPER Diagnostic

- [ ] Add `_orphanedPositionFirstSeen` field to `src/V12_002.REAPER.cs`
- [ ] Add diagnostic assertion logic to `AuditSingleFleetAccount()` in `src/V12_002.REAPER.Audit.cs`
- [ ] Verify ASCII-only compliance in diagnostic log messages

### Phase 3: Testing

- [ ] Create `tests/Build981ComplianceTests.cs`
- [ ] Implement 5 unit tests (H05, H08, H21, H22, REAPER diagnostic)
- [ ] Run existing integration tests (verify no regressions)
- [ ] Execute manual test scenarios (H05/H08, H21/H22, REAPER diagnostic)

### Phase 4: Verification

- [ ] Run `deploy-sync.ps1` (verify DIFF GUARD passes)
- [ ] F5 in NinjaTrader (verify compile gate passes)
- [ ] Verify BUILD_TAG matches expected value
- [ ] Run stress tests (100 concurrent submissions, 50% rejection rate)

### Phase 5: Documentation

- [ ] Update `docs/architecture.md` with Build 981 exemption notes
- [ ] Update `AGENTS.md` with new REAPER diagnostic assertion
- [ ] Create `docs/brain/epic-1-dna/verification-report.md`

---

## 10. Conclusion

This approach implements the Director's approved decisions with surgical precision:
- **H05/H08:** Synchronous atomic writes eliminate ghost-order windows
- **H21/H22:** Synchronous add + synchronous rollback eliminate TOCTOU races
- **REAPER Diagnostic:** Non-blocking assertion detects orphaned positions after 10s grace
- **DNA Compliance:** Zero new `lock()` statements, ASCII-only, diff size < 150K

The refactoring is scoped to 4 methods (~10 lines total) with comprehensive test coverage and rollback plan. All changes are reversible via `git checkout` + `deploy-sync.ps1`.

**Next Steps:**
- Present this approach document to Director for approval
- Await explicit "APPROVED" confirmation before proceeding to `/epic-validate`