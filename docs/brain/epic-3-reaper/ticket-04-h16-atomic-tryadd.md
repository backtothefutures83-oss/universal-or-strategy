---
# TICKET-04: H16 - REAPER In-Flight Guards Atomic TryAdd
# Epic: epic-3-reaper
# Defect: BUG-S4-005
# Priority: HIGH
# Estimated CYC Delta: 0 (no new complexity)
---

## 1. Ticket Summary

**Defect:** H16 - TOCTOU race in REAPER in-flight guards (3 sites)  
**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)  
**Locations:** Lines 337, 420, 622  
**Methods:** `EnqueueReaperRepairCandidate()`, `EnqueueReaperNakedStopCandidate()`, `EnqueueReaperMasterNakedStop()`

**Root Cause:** Check-then-act pattern (`ContainsKey` → `TryAdd`) is not atomic. Two concurrent audit cycles can both pass the check before either calls `TryAdd`, causing duplicate enqueues.

**Fix:** Replace `ContainsKey` + `TryAdd` with atomic `if (!TryAdd(...))` pattern (mirrors line 367).

---

## 2. Current Code - Site 1 (Line 337 - Repair Candidate)

```csharp
bool alreadyInFlight;
alreadyInFlight = _repairInFlight.ContainsKey(repairKey);

if (!alreadyInFlight)
{
    // Phase 4: Use FSM to identify working entry
    bool hasWorkingEntry = accountFsms.Any(f => f.State == FollowerBracketState.Submitted || f.State == FollowerBracketState.Accepted);

    if (!hasWorkingEntry)
    {
        if (shouldLog)
        {
            Print($"[REAPER] * REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
        }
        _repairInFlight.TryAdd(repairKey, 0);
        _reaperRepairQueue.Enqueue(acct.Name);
        return true;
    }
}
```

---

## 3. Surgical Repair - Site 1 (Line 337)

```csharp
// H16-FIX: Atomic TryAdd check prevents TOCTOU race where two audit cycles both pass
// ContainsKey check before either calls TryAdd, causing duplicate repair submissions.
if (!_repairInFlight.TryAdd(repairKey, 0))
{
    // Already in flight - skip
    if (shouldLog)
    {
        Print($"[REAPER] {acct.Name} repair already in-flight -- skipping.");
    }
    return false;
}

// Phase 4: Use FSM to identify working entry (EXISTING LOGIC - not new)
bool hasWorkingEntry = accountFsms.Any(f => f.State == FollowerBracketState.Submitted || f.State == FollowerBracketState.Accepted);

if (!hasWorkingEntry)
{
    if (shouldLog)
    {
        Print($"[REAPER] * REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
    }
    _reaperRepairQueue.Enqueue(acct.Name);
    return true;
}
else
{
    // Has working entry - clear in-flight flag since we're not enqueuing.
    // CRITICAL: Without this TryRemove, the account would be permanently blocked.
    _repairInFlight.TryRemove(repairKey, out _);
    return false;
}
```

---

## 4. Current Code - Site 2 (Line 420 - Naked Stop Candidate)

```csharp
bool alreadyNakedInFlight;
alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(expectedKey);
if (!alreadyNakedInFlight)
{
    _reaperNakedStopInFlight.TryAdd(expectedKey, 0);
    Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
        acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
    _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
    return true;
}
```

---

## 5. Surgical Repair - Site 2 (Line 420)

```csharp
// H16-FIX: Atomic TryAdd check prevents duplicate naked stop submissions.
if (!_reaperNakedStopInFlight.TryAdd(expectedKey, 0))
{
    // Already in flight - skip
    return false;
}
Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
    acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
_reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
return true;
```

---

## 6. Current Code - Site 3 (Line 622 - Master Naked Stop)

```csharp
bool alreadyNakedInFlight;
alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(masterExpectedKey);
if (!alreadyNakedInFlight)
{
    _reaperNakedStopInFlight.TryAdd(masterExpectedKey, 0);
    Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
        Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
    _reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
    return true;
}
```

---

## 7. Surgical Repair - Site 3 (Line 622)

```csharp
// H16-FIX: Atomic TryAdd check prevents duplicate master naked stop submissions.
if (!_reaperNakedStopInFlight.TryAdd(masterExpectedKey, 0))
{
    // Already in flight - skip
    return false;
}
Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
    Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
_reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
return true;
```

---

## 8. Implementation Steps

### Step 1: Locate All 3 Sites
```bash
grep -n "ContainsKey(repairKey)" src/V12_002.REAPER.Audit.cs
grep -n "ContainsKey(expectedKey)" src/V12_002.REAPER.Audit.cs
grep -n "ContainsKey(masterExpectedKey)" src/V12_002.REAPER.Audit.cs
```
Expected: Lines 337, 420, 622

### Step 2: Apply Surgical Edits (3 sites)
Use `apply_diff` tool for each site. Order: Site 1 → Site 2 → Site 3.

### Step 3: Verify Syntax
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS

---

## 9. Self-Audit Checklist

### DNA Compliance
```bash
grep -n "lock(" src/V12_002.REAPER.Audit.cs
```
Expected: 0 matches

```bash
grep -Prn "[^\x00-\x7F]" src/V12_002.REAPER.Audit.cs
```
Expected: 0 matches

### Hard-Link Sync
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS + ASCII gate PASS

### Verification
```bash
# Confirm all 3 sites use atomic pattern
grep -n "if (!_repairInFlight.TryAdd" src/V12_002.REAPER.Audit.cs
grep -n "if (!_reaperNakedStopInFlight.TryAdd" src/V12_002.REAPER.Audit.cs
```
Expected: 3 total matches (1 repair, 2 naked stop)

---

## 10. Testing

### Unit Test
```csharp
[Test]
public void Test_H16_RepairEnqueue_ConcurrentAuditCycles_EnqueuesExactlyOnce()
{
    // Arrange: Two concurrent audit cycles
    // Act: Both call EnqueueReaperRepairCandidate() simultaneously
    // Assert: Repair queue contains exactly 1 entry (not 2)
}
```

### Manual Verification
1. Run strategy under high-volume conditions (1000+ orders/sec)
2. Monitor repair queue logs for duplicate entries
3. Expected: Zero duplicate repairs over 1-hour test

---

## 11. Success Criteria

- ✅ All 3 sites converted to atomic `TryAdd` pattern
- ✅ Site 1 includes `TryRemove` for `hasWorkingEntry` case
- ✅ No compile errors
- ✅ Zero new `lock()` statements
- ✅ ASCII gate passes
- ✅ Hard-link sync succeeds
- ✅ Unit test passes
- ✅ Zero duplicate enqueues in production logs

---

## 12. Rollback Plan

```bash
git checkout HEAD~1 -- src/V12_002.REAPER.Audit.cs
powershell -File .\deploy-sync.ps1
```

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** None (independent of other tickets)  
**Estimated Time:** 15 minutes (3 sites)  
**Risk Level:** LOW (proven pattern from line 367)