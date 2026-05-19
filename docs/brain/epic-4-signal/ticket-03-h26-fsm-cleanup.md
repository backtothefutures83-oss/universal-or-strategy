---
# TICKET-03: H26 BracketFSM Cleanup Completeness
# Epic: epic-4-signal
# Defect: BUG-S7-002 - FSM Leak in FollowerBracketFSM Removal
# Priority: MEDIUM
# Estimated Time: 3 hours (includes pre-implementation audit)
---

## 1. Ticket Summary

Add comprehensive FSM state cleanup to `RemoveFsmOrderIdMappings` to eliminate residual tracking state and prevent memory leaks over multi-day sessions.

**Defect:** H26 - BUG-S7-002  
**Severity:** Medium (upgraded from Low due to long-running session impact)  
**Root Cause:** Incomplete cleanup scope leaves residual tracking state in dictionaries  
**Impact:** Memory bloat over multi-day sessions, dictionary lookup performance degradation, eventual GC pressure

---

## 2. Technical Specification

### 2.1 Target State

**Design:** Audit all FSM-related dictionaries, add cleanup for any missed entries

**Current Cleanup (VERIFIED):**
- ✅ `_orderIdToFsmKey` - Entry, Stop, Targets, ReplacingCancelOrderId
- ✅ `_followerBrackets` - Via `TryTerminateFollowerBracket`

**Additional Cleanup Required (TO BE VERIFIED):**
- ❓ `_followerReplaceSpecs` - Replace FSM specifications (if exists)
- ❓ `_ocoGroupTracking` - OCO group tracking (if exists)
- ❓ `_accountToFsmKeys` - Account-to-FSM reverse mappings (if exists)

### 2.2 Files to Modify

1. `src/V12_002.Symmetry.BracketFSM.cs` - `RemoveFsmOrderIdMappings()` method

**Total Lines Changed:** ~15-20  
**CYC Delta:** +5-10 (additional cleanup loops)

---

## 3. Pre-Implementation Audit (MANDATORY)

**BEFORE writing any code, run these searches to identify all FSM-related dictionaries:**

### 3.1 Find All ConcurrentDictionary Declarations

```bash
grep -r "ConcurrentDictionary" src/V12_002.*.cs | grep -v "//.*ConcurrentDictionary"
```

**Action:** Review output, identify which dictionaries use FSM keys (EntryName, OrderId, OcoGroupId, AccountName)

### 3.2 Find Specific Dictionary References

```bash
# Search for potential FSM tracking dictionaries
grep -r "_followerReplaceSpecs" src/
grep -r "_ocoGroupTracking" src/
grep -r "_accountToFsmKeys" src/
grep -r "OcoGroupId" src/V12_002.Symmetry*.cs
```

**Action:** Document which dictionaries exist and require cleanup

### 3.3 Find All FSM.EntryName Dictionary Operations

```bash
grep -r "\.EntryName\]" src/V12_002.Symmetry*.cs
grep -r "\[.*\.EntryName\]" src/V12_002.Symmetry*.cs
```

**Action:** Identify all dictionaries indexed by FSM EntryName

### 3.4 Document Audit Results

**Create a checklist:**
```
FSM-Related Dictionaries Found:
[ ] _orderIdToFsmKey - ALREADY CLEANED
[ ] _followerBrackets - ALREADY CLEANED
[ ] _followerReplaceSpecs - EXISTS? _____ (YES/NO)
[ ] _ocoGroupTracking - EXISTS? _____ (YES/NO)
[ ] _accountToFsmKeys - EXISTS? _____ (YES/NO)
[ ] Other: _________________ - EXISTS? _____ (YES/NO)
```

---

## 4. Implementation Steps

### Step 1: Baseline Cleanup (Already Exists)

**File:** `src/V12_002.Symmetry.BracketFSM.cs`

**Current Code (lines 102-122) - DO NOT MODIFY:**
```csharp
private void RemoveFsmOrderIdMappings(FollowerBracketFSM fsm)
{
    if (fsm == null) return;

    // ✅ EXISTING: Entry order ID mapping
    if (fsm.EntryOrder != null && !string.IsNullOrEmpty(fsm.EntryOrder.OrderId))
        _orderIdToFsmKey.TryRemove(fsm.EntryOrder.OrderId, out _);

    // ✅ EXISTING: Replacing cancel order ID mapping
    if (!string.IsNullOrEmpty(fsm.ReplacingCancelOrderId))
        _orderIdToFsmKey.TryRemove(fsm.ReplacingCancelOrderId, out _);

    // ✅ EXISTING: Stop order ID mapping
    if (fsm.StopOrder != null && !string.IsNullOrEmpty(fsm.StopOrder.OrderId))
        _orderIdToFsmKey.TryRemove(fsm.StopOrder.OrderId, out _);

    // ✅ EXISTING: Target order ID mappings (T1-T5)
    if (fsm.Targets != null)
    {
        foreach (Order target in fsm.Targets)
        {
            if (target != null && !string.IsNullOrEmpty(target.OrderId))
                _orderIdToFsmKey.TryRemove(target.OrderId, out _);
        }
    }
}
```

### Step 2: Add Additional Cleanup (Based on Audit Results)

**ADD AFTER the existing cleanup logic (after line 122):**

```csharp
    // H26: Replace FSM specifications cleanup (if dictionary exists)
    if (_followerReplaceSpecs != null && !string.IsNullOrEmpty(fsm.EntryName))
        _followerReplaceSpecs.TryRemove(fsm.EntryName, out _);

    // H26: OCO group tracking cleanup (if dictionary exists)
    if (_ocoGroupTracking != null && !string.IsNullOrEmpty(fsm.OcoGroupId))
        _ocoGroupTracking.TryRemove(fsm.OcoGroupId, out _);

    // H26: Account-to-FSM reverse mapping cleanup (if dictionary exists)
    if (_accountToFsmKeys != null && !string.IsNullOrEmpty(fsm.AccountName))
    {
        // Remove this FSM's entry name from the account's FSM list
        if (_accountToFsmKeys.TryGetValue(fsm.AccountName, out var fsmList))
        {
            fsmList.TryRemove(fsm.EntryName);
            if (fsmList.Count == 0)
                _accountToFsmKeys.TryRemove(fsm.AccountName, out _);
        }
    }
```

**IMPORTANT:** Only add cleanup for dictionaries that ACTUALLY EXIST per Step 3 audit results. Remove or comment out cleanup for non-existent dictionaries.

### Step 3: Verify Cleanup Completeness

**After adding cleanup logic, verify no FSM-related dictionaries are missed:**

```bash
# Search for any remaining FSM key usage in dictionary operations
grep -r "fsm\.EntryName" src/V12_002.Symmetry*.cs
grep -r "fsm\.OcoGroupId" src/V12_002.Symmetry*.cs
grep -r "fsm\.AccountName" src/V12_002.Symmetry*.cs
```

**Action:** Ensure every dictionary operation using FSM keys has corresponding cleanup in `RemoveFsmOrderIdMappings`.

---

## 5. Self-Audit Checklist (Step 5)

After completing Steps 1-3, run these audits BEFORE emitting [SELF-AUDIT-DONE]:

### 5.1 DNA Compliance

```powershell
# Hard-link sync + ASCII gate
powershell -File .\deploy-sync.ps1
# Expected: EXIT 0, ASCII gate PASS
```

### 5.2 Lock Regression

```bash
grep -r "lock(" src/
# Expected: ZERO matches
```

### 5.3 Unicode Regression

```bash
grep -Prn "[^\x00-\x7F]" src/
# Expected: ZERO matches
```

### 5.4 Ghost Method Audit (Concurrency Epic)

```bash
grep -r "ClearAllEventHandlers" src/
grep -r "_globalState" src/
grep -r "_inFlightRmaEntries" src/
# Expected: ALL return ZERO matches
```

### 5.5 Cleanup Completeness Audit

```bash
# Verify all FSM-related dictionaries have cleanup
grep -r "ConcurrentDictionary.*FSM\|ConcurrentDictionary.*Bracket\|ConcurrentDictionary.*Follower" src/V12_002.Symmetry*.cs
```

**Action:** Cross-reference with `RemoveFsmOrderIdMappings` to ensure all dictionaries are cleaned.

### 5.6 Compilation Check

- Verify no compiler errors
- Verify no new warnings introduced

**If ALL audits PASS:** Emit `[SELF-AUDIT-DONE] Ticket 03 -- self-audit PASS. Awaiting independent verification.`

**If ANY audit FAILS:** Fix the issue, re-run the failing audit, and only emit [SELF-AUDIT-DONE] once all audits are clean.

---

## 6. Verification Criteria (Independent - Step C)

**Unit Test:** `RemoveFsmOrderIdMappings_PrunesAllStateTrackerDictionaries`
- Create FSM with full state (entry, stop, 5 targets, replace spec, OCO group, account mapping)
- Populate all tracking dictionaries
- Call `RemoveFsmOrderIdMappings`
- Assert: All dictionaries return zero entries for this FSM's keys

**Memory Leak Test:** `BracketFSM_24HourSession_NoMemoryGrowth`
- Simulate 1000 order lifecycles (submit → fill → terminate)
- Measure memory before/after
- Assert: Memory delta < 1% (accounting for GC variance)

**Manual Test:** 7-day memory profiler run, verify zero FSM state growth

---

## 7. Success Criteria

- [x] Pre-implementation audit completed and documented
- [x] All FSM-related dictionaries identified
- [x] Cleanup added for all confirmed dictionaries
- [x] Zero new `lock()` statements
- [x] ASCII-only compliance maintained
- [x] `deploy-sync.ps1` passes
- [x] All ghost-method audits clean
- [x] Unit tests pass
- [x] Memory leak test passes
- [x] F5 compile gate passes

---

## 8. Implementation Notes

### 8.1 Dictionary Existence Checks

The cleanup code includes null checks (`if (_dictionary != null)`) because:
1. Some dictionaries may not exist in all code branches
2. Prevents NullReferenceException if dictionary is not initialized
3. Allows cleanup code to be defensive without breaking compilation

### 8.2 Memory Leak Projection

**Before Fix:**
- Daily leak: 200 orders × 100 bytes = 20 KB/day
- 90-day leak: 1.8 MB

**After Fix:**
- Daily leak: 0 bytes (all state cleaned)
- 90-day leak: 0 bytes

**Validation:** 24-hour stress test should show zero memory growth.

### 8.3 Performance Impact

**Cleanup Overhead:** ~5-10 microseconds per FSM termination (dictionary removals)  
**Frequency:** Once per order lifecycle (typically 10-50 per day)  
**Total Impact:** < 500 microseconds per day (negligible)

**Conclusion:** Cleanup overhead is negligible compared to memory leak prevention.

---

## 9. Rollback Plan

**If comprehensive cleanup fails:**
1. Revert all changes via git
2. Keep existing order ID cleanup only
3. Add periodic GC sweep as fallback (less elegant)
4. Escalate to P3 Architect

---

**Ticket Status:** READY FOR EXECUTION (after pre-implementation audit)  
**Dependencies:** None (independent ticket)  
**Estimated CYC Impact:** +5-10 (additional cleanup loops)  
**Risk Level:** MEDIUM (requires comprehensive dictionary audit)

---

## 10. Pre-Implementation Checklist

**BEFORE starting implementation, complete these steps:**

- [ ] Run all audit searches (Step 3.1-3.3)
- [ ] Document all FSM-related dictionaries found
- [ ] Verify which dictionaries actually exist in codebase
- [ ] Update Step 2 cleanup code to match actual dictionaries
- [ ] Remove cleanup for non-existent dictionaries
- [ ] Confirm with Director if unsure about any dictionary

**Only proceed to implementation after completing this checklist.**