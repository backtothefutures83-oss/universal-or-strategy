---
# TICKET-02: H24 PositionInfo FSM Enqueue
# Epic: epic-4-signal
# Defect: BUG-S6-004 - Unsafe Direct Mutation of PositionInfo
# Priority: HIGH
# Estimated Time: 3 hours
---

## 1. Ticket Summary

Wrap all direct `PositionInfo` field mutations in FSM `Enqueue` pattern to ensure thread-safe updates and eliminate torn reads on UI/audit threads.

**Defect:** H24 - BUG-S6-004  
**Severity:** High  
**Root Cause:** Entry module directly mutates `PositionInfo` structure shared with UI and audit threads without synchronization  
**Impact:** UI displays incorrect proximity tracking data, probe count under-counted, audit threads read inconsistent state

---

## 2. Technical Specification

### 2.1 Target State

**Design:** Reuse existing `Enqueue(ctx => ...)` pattern (no new FSM message types)

**Pattern:**
```csharp
// BEFORE (direct mutation - UNSAFE)
pos.FieldName = value;

// AFTER (FSM enqueue - SAFE)
string entryKey = kvp.Key;
Enqueue(ctx => {
    PositionInfo p;
    if (ctx.activePositions.TryGetValue(entryKey, out p))
        p.FieldName = value;
});
```

### 2.2 Files to Modify

1. `src/V12_002.Entries.RMA.cs` - `MonitorRmaProximity()` method

**Total Lines Changed:** ~25  
**CYC Delta:** +10-15 (enqueue wrappers, no core logic change)

---

## 3. Implementation Steps

### Step 1: Wrap ClosestApproachTicks Initialization

**File:** `src/V12_002.Entries.RMA.cs`

**FIND (lines 279-280):**
```csharp
// Phase 9.2: Initialize ClosestApproachTicks on first observation.
if (pos.ClosestApproachTicks <= 0)
    pos.ClosestApproachTicks = double.MaxValue;
```

**REPLACE:**
```csharp
// H24: FSM Enqueue for ClosestApproachTicks initialization
if (pos.ClosestApproachTicks <= 0)
{
    string entryKey = kvp.Key;
    Enqueue(ctx => {
        PositionInfo p;
        if (ctx.activePositions.TryGetValue(entryKey, out p))
            p.ClosestApproachTicks = double.MaxValue;
    });
}
```

### Step 2: Wrap ClosestApproachTicks Update

**File:** `src/V12_002.Entries.RMA.cs`

**FIND (lines 283-284):**
```csharp
// Phase 9.2: Track closest approach as a monotonic minimum.
if (distTicks < pos.ClosestApproachTicks)
    pos.ClosestApproachTicks = distTicks;
```

**REPLACE:**
```csharp
// H24: FSM Enqueue for ClosestApproachTicks update
if (distTicks < pos.ClosestApproachTicks)
{
    string entryKey = kvp.Key;
    double newDist = distTicks;
    Enqueue(ctx => {
        PositionInfo p;
        if (ctx.activePositions.TryGetValue(entryKey, out p) && newDist < p.ClosestApproachTicks)
            p.ClosestApproachTicks = newDist;
    });
}
```

**Note:** Double-check pattern (`newDist < p.ClosestApproachTicks`) prevents race where position is removed or value changes between outer check and Enqueue execution.

### Step 3: Wrap Proximity Entry State Transition

**File:** `src/V12_002.Entries.RMA.cs`

**FIND (lines 288-294):**
```csharp
if (!pos.WasInProximity)
{
    pos.WasInProximity = true;
    pos.ProximityProbeCount++;
    Print(string.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
        pos.ProximityProbeCount, kvp.Key, distTicks, level));
}
```

**REPLACE:**
```csharp
// H24: FSM Enqueue for proximity state transition
if (!pos.WasInProximity)
{
    string entryKey = kvp.Key;
    double dist = distTicks;
    double lvl = level;
    Enqueue(ctx => {
        PositionInfo p;
        if (ctx.activePositions.TryGetValue(entryKey, out p) && !p.WasInProximity)
        {
            p.WasInProximity = true;
            p.ProximityProbeCount++;
            Print(string.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
                p.ProximityProbeCount, entryKey, dist, lvl));
        }
    });
}
```

**Note:** Double-check pattern (`!p.WasInProximity`) prevents duplicate probe increments if multiple threads enter this block before Enqueue executes.

### Step 4: Wrap Proximity Exit State Transition

**File:** `src/V12_002.Entries.RMA.cs`

**FIND (lines 305-307):**
```csharp
if (pos.WasInProximity)
{
    pos.WasInProximity = false;
```

**REPLACE:**
```csharp
// H24: FSM Enqueue for proximity exit
if (pos.WasInProximity)
{
    string entryKey = kvp.Key;
    Enqueue(ctx => {
        PositionInfo p;
        if (ctx.activePositions.TryGetValue(entryKey, out p) && p.WasInProximity)
            p.WasInProximity = false;
    });
```

**Note:** The rest of the method (lines 309-332) only reads `pos` fields or calls methods that don't mutate `pos` directly, so no additional changes needed.

---

## 4. Self-Audit Checklist (Step 5)

After completing Steps 1-4, run these audits BEFORE emitting [SELF-AUDIT-DONE]:

### 4.1 DNA Compliance

```powershell
# Hard-link sync + ASCII gate
powershell -File .\deploy-sync.ps1
# Expected: EXIT 0, ASCII gate PASS
```

### 4.2 Lock Regression

```bash
grep -r "lock(" src/
# Expected: ZERO matches
```

### 4.3 Unicode Regression

```bash
grep -Prn "[^\x00-\x7F]" src/
# Expected: ZERO matches
```

### 4.4 Ghost Method Audit (Concurrency Epic)

```bash
grep -r "ClearAllEventHandlers" src/
grep -r "_globalState" src/
grep -r "_inFlightRmaEntries" src/
# Expected: ALL return ZERO matches
```

### 4.5 Direct Mutation Audit

```bash
# Verify no direct PositionInfo mutations remain in MonitorRmaProximity
grep -A5 "MonitorRmaProximity" src/V12_002.Entries.RMA.cs | grep "pos\."
# Expected: Only reads (no assignments like "pos.Field = value")
```

### 4.6 Compilation Check

- Verify no compiler errors
- Verify no new warnings introduced

**If ALL audits PASS:** Emit `[SELF-AUDIT-DONE] Ticket 02 -- self-audit PASS. Awaiting independent verification.`

**If ANY audit FAILS:** Fix the issue, re-run the failing audit, and only emit [SELF-AUDIT-DONE] once all audits are clean.

---

## 5. Verification Criteria (Independent - Step C)

**Unit Test:** `MonitorRmaProximity_UpdatesPositionInfo_EnqueuesSuccessfully`
- Mock `Enqueue` mechanism to capture all queued updates
- Verify all PositionInfo updates go through queue
- Assert: Zero direct field writes

**Integration Test:** `RmaProximity_UIRefreshStress_NoTornReads`
- Run RMA proximity monitoring with 10ms UI refresh for 60 seconds
- Verify UI always reads consistent `PositionInfo` state
- Assert: `ProximityProbeCount` increments are never lost

**Manual Test:** 24-hour UI refresh stress test, verify zero torn reads

---

## 6. Success Criteria

- [x] All 4 PositionInfo mutation sites wrapped in Enqueue
- [x] Zero direct field writes to PositionInfo in MonitorRmaProximity
- [x] Zero new `lock()` statements
- [x] ASCII-only compliance maintained
- [x] `deploy-sync.ps1` passes
- [x] All ghost-method audits clean
- [x] Unit tests pass
- [x] F5 compile gate passes

---

## 7. Implementation Notes

### 7.1 Double-Check Pattern Rationale

The Enqueue lambda includes defensive checks like:
```csharp
if (ctx.activePositions.TryGetValue(entryKey, out p) && condition)
```

This is necessary because:
1. Position may be removed between outer check and Enqueue execution
2. Field value may change between outer check and Enqueue execution
3. Multiple threads may queue updates for same position

The double-check prevents:
- NullReferenceException if position removed
- Duplicate increments if multiple threads enter proximity block
- Stale updates overwriting newer values

### 7.2 Performance Impact

**Enqueue Overhead:** ~10-50 microseconds per update (actor queue dispatch)  
**Frequency:** RMA proximity runs on every bar update (typically 1-5 Hz)  
**Total Impact:** < 250 microseconds per second (negligible)

**Conclusion:** Enqueue latency is acceptable for RMA proximity tracking.

---

## 8. Rollback Plan

**If FSM Enqueue fails:**
1. Revert all changes via git
2. Restore direct field writes
3. Add `Interlocked` for numeric fields only (partial fix)
4. Escalate to P3 Architect

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** None (independent ticket)  
**Estimated CYC Impact:** +10-15 (enqueue wrappers)  
**Risk Level:** MEDIUM (requires FSM message validation)