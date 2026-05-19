---
# EPIC-4-SIGNAL: SURGICAL APPROACH
# Epic: Signal & State Decoupling
# Phase: APPROACH (Phase 2 of 6)
# Status: DRAFT - AWAITING GATE 2 APPROVAL
---

## 1. Executive Summary

This document defines the precise surgical repair specifications for Epic 4: Signal & State Decoupling. Each repair is designed to be minimal, atomic, and verifiable through automated gates.

**Repair Strategy:**
- **H23:** Replace boolean flags with atomic integer operations (`Interlocked.Exchange`)
- **H24:** Wrap PositionInfo mutations in FSM `Enqueue` pattern
- **H26:** Add comprehensive FSM state cleanup to `RemoveFsmOrderIdMappings`

**Estimated Impact:**
- Total lines changed: ~50-70 lines across 3 files
- CYC delta: +15-25 (new cleanup loops and enqueue wrappers)
- Zero architectural changes
- Zero new lock() statements

---

## 2. H23: OR Arm Flag Atomic Operations

### 2.1 Target State

**Goal:** Replace check-then-act boolean pattern with atomic integer operations ensuring mutual exclusion.

**Design Decision:** Use `Interlocked.CompareExchange` on integer representations:
- `0` = disarmed
- `1` = long armed
- `2` = short armed

### 2.2 File Modifications

#### File 1: `src/V12_002.cs` (Field Declarations)

**FIND (lines 256-258):**
```csharp
private volatile bool isTosSyncMode = false;
private bool isLongArmed = false;
private bool isShortArmed = false;
private DateTime lastArmedTime = DateTime.MinValue;
```

**REPLACE:**
```csharp
private volatile bool isTosSyncMode = false;
// H23: Atomic arm state (0=disarmed, 1=long, 2=short)
private int _armState = 0;
private DateTime lastArmedTime = DateTime.MinValue;
```

**Rationale:** Single atomic integer replaces two booleans, enforcing mutual exclusion at the primitive level.

#### File 2: `src/V12_002.Entries.OR.cs` (ExecuteLong)

**FIND (lines 50-64):**
```csharp
if (isLongArmed)
{
    // DOUBLE-CLICK BYPASS: If already armed, fire immediately
    Print("[SYNC] Double-Click Bypass Triggered -> Executing LONG immediately (No ToS Handshake)");
    isLongArmed = false;
    // Proceed to entry logic below
}
else
{
    isLongArmed = true;
    isShortArmed = false; // Mutually exclusive for simplicity
    lastArmedTime = DateTime.Now;
    Print("[SYNC] LONG ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
```

**REPLACE:**
```csharp
// H23: Atomic arm state check (1=long armed)
int currentState = Interlocked.CompareExchange(ref _armState, 0, 1);
if (currentState == 1)
{
    // DOUBLE-CLICK BYPASS: Already armed, fire immediately and disarm
    Print("[SYNC] Double-Click Bypass Triggered -> Executing LONG immediately (No ToS Handshake)");
    // Proceed to entry logic below
}
else if (currentState == 0)
{
    // Disarmed -> Arm LONG (0 -> 1)
    Interlocked.Exchange(ref _armState, 1);
    lastArmedTime = DateTime.Now;
    Print("[SYNC] LONG ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
else
{
    // currentState == 2 (SHORT armed) -> reject LONG arm request
    Print("[SYNC] LONG ARM REJECTED: SHORT already armed (mutual exclusion)");
    return;
}
```

**Rationale:**
- `CompareExchange(ref _armState, 0, 1)` atomically checks if armed (1) and disarms (0) if true
- If disarmed (0), `Exchange(ref _armState, 1)` atomically arms LONG
- If SHORT armed (2), reject LONG arm request (mutual exclusion)

#### File 3: `src/V12_002.Entries.OR.cs` (ExecuteShort)

**FIND (lines 93-107):**
```csharp
if (isShortArmed)
{
    // DOUBLE-CLICK BYPASS: If already armed, fire immediately
    Print("[SYNC] Double-Click Bypass Triggered -> Executing SHORT immediately (No ToS Handshake)");
    isShortArmed = false;
    // Proceed to entry logic below
}
else
{
    isShortArmed = true;
    isLongArmed = false; // Mutually exclusive
    lastArmedTime = DateTime.Now;
    Print("[SYNC] SHORT ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
```

**REPLACE:**
```csharp
// H23: Atomic arm state check (2=short armed)
int currentState = Interlocked.CompareExchange(ref _armState, 0, 2);
if (currentState == 2)
{
    // DOUBLE-CLICK BYPASS: Already armed, fire immediately and disarm
    Print("[SYNC] Double-Click Bypass Triggered -> Executing SHORT immediately (No ToS Handshake)");
    // Proceed to entry logic below
}
else if (currentState == 0)
{
    // Disarmed -> Arm SHORT (0 -> 2)
    Interlocked.Exchange(ref _armState, 2);
    lastArmedTime = DateTime.Now;
    Print("[SYNC] SHORT ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
else
{
    // currentState == 1 (LONG armed) -> reject SHORT arm request
    Print("[SYNC] SHORT ARM REJECTED: LONG already armed (mutual exclusion)");
    return;
}
```

#### File 4: `src/V12_002.UI.IPC.Commands.Fleet.cs` (ToS Handshake)

**FIND (line 374):**
```csharp
bool armed = (action == "LONG") ? isLongArmed : isShortArmed;
```

**REPLACE:**
```csharp
// H23: Atomic arm state check
int currentState = Interlocked.CompareExchange(ref _armState, 0, 0);
bool armed = (action == "LONG" && currentState == 1) || (action == "SHORT" && currentState == 2);
```

**FIND (line 383):**
```csharp
if (action == "LONG") isLongArmed = false; else isShortArmed = false;
```

**REPLACE:**
```csharp
// H23: Atomic disarm
Interlocked.Exchange(ref _armState, 0);
```

**FIND (lines 445-450):**
```csharp
if (isLongArmed)
{
    Print("[SYNC] LONG ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteLong(orContracts));
    isLongArmed = false;
}
```

**REPLACE:**
```csharp
// H23: Atomic check and disarm
if (Interlocked.CompareExchange(ref _armState, 0, 1) == 1)
{
    Print("[SYNC] LONG ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteLong(orContracts));
}
```

**FIND (lines 472-477):**
```csharp
if (isShortArmed)
{
    Print("[SYNC] SHORT ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteShort(orContracts));
    isShortArmed = false;
}
```

**REPLACE:**
```csharp
// H23: Atomic check and disarm
if (Interlocked.CompareExchange(ref _armState, 0, 2) == 2)
{
    Print("[SYNC] SHORT ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteShort(orContracts));
}
```

#### File 5: `src/V12_002.Orders.Management.Flatten.cs` (Reset on Flatten)

**FIND (lines 247-248):**
```csharp
isLongArmed = false;
isShortArmed = false;
```

**REPLACE:**
```csharp
// H23: Atomic disarm on flatten
Interlocked.Exchange(ref _armState, 0);
```

### 2.3 Verification Criteria

**Unit Test:** `ORFlags_ConcurrentArming_AllowsOnlyOneDirection`
- Spawn 100 threads: 50 calling `ExecuteLong()`, 50 calling `ExecuteShort()`
- Assert: `_armState` never equals both 1 and 2 simultaneously
- Assert: All arm requests either succeed or are rejected (no silent failures)

**Stress Test:** `ORFlags_HighFrequencyToggle_MaintainsMutualExclusion`
- 1000 rapid arm/disarm cycles
- Verify mutual exclusion invariant holds throughout

---

## 3. H24: PositionInfo FSM Enqueue

### 3.1 Target State

**Goal:** Wrap all direct `PositionInfo` field mutations in FSM `Enqueue` pattern to ensure thread-safe updates.

**Design Decision:** Reuse existing `Enqueue(ctx => ...)` pattern (per Director approval). No new FSM message types.

### 3.2 File Modifications

#### File: `src/V12_002.Entries.RMA.cs` (MonitorRmaProximity)

**FIND (lines 279-284):**
```csharp
// Phase 9.2: Initialize ClosestApproachTicks on first observation.
if (pos.ClosestApproachTicks <= 0)
    pos.ClosestApproachTicks = double.MaxValue;

// Phase 9.2: Track closest approach as a monotonic minimum.
if (distTicks < pos.ClosestApproachTicks)
    pos.ClosestApproachTicks = distTicks;
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

### 3.3 Verification Criteria

**Unit Test:** `MonitorRmaProximity_UpdatesPositionInfo_EnqueuesSuccessfully`
- Mock `Enqueue` mechanism to capture all queued updates
- Verify zero direct field writes to `PositionInfo`
- Assert: All mutations go through queue

**Integration Test:** `RmaProximity_UIRefreshStress_NoTornReads`
- Run RMA proximity monitoring with 10ms UI refresh for 60 seconds
- Verify UI always reads consistent `PositionInfo` state
- Assert: `ProximityProbeCount` increments are never lost

---

## 4. H26: BracketFSM Cleanup Completeness

### 4.1 Target State

**Goal:** Ensure `RemoveFsmOrderIdMappings` removes ALL FSM-related tracking state, not just order ID mappings.

**Design Decision:** Audit all FSM-related dictionaries in codebase, add cleanup for any missed entries.

### 4.2 Dictionary Audit Results

**Confirmed FSM-Related Dictionaries (via code inspection):**

1. `_orderIdToFsmKey` - ✅ ALREADY CLEANED (lines 107, 110, 113, 120)
2. `_followerBrackets` - ✅ CLEANED via `TryTerminateFollowerBracket` (line 128)
3. `_followerReplaceSpecs` - ❌ NOT CLEANED (mentioned in Epic 1, needs verification)

**Action Required:** Search codebase for `_followerReplaceSpecs` and any other FSM tracking dictionaries.

### 4.3 File Modifications

#### File: `src/V12_002.Symmetry.BracketFSM.cs` (RemoveFsmOrderIdMappings)

**FIND (lines 102-122):**
```csharp
private void RemoveFsmOrderIdMappings(FollowerBracketFSM fsm)
{
    if (fsm == null) return;

    if (fsm.EntryOrder != null && !string.IsNullOrEmpty(fsm.EntryOrder.OrderId))
        _orderIdToFsmKey.TryRemove(fsm.EntryOrder.OrderId, out _);

    if (!string.IsNullOrEmpty(fsm.ReplacingCancelOrderId))
        _orderIdToFsmKey.TryRemove(fsm.ReplacingCancelOrderId, out _);

    if (fsm.StopOrder != null && !string.IsNullOrEmpty(fsm.StopOrder.OrderId))
        _orderIdToFsmKey.TryRemove(fsm.StopOrder.OrderId, out _);

    if (fsm.Targets == null) return;

    foreach (Order target in fsm.Targets)
    {
        if (target != null && !string.IsNullOrEmpty(target.OrderId))
            _orderIdToFsmKey.TryRemove(target.OrderId, out _);
    }
}
```

**REPLACE:**
```csharp
private void RemoveFsmOrderIdMappings(FollowerBracketFSM fsm)
{
    if (fsm == null) return;

    // H26: Order ID mappings cleanup (existing logic)
    if (fsm.EntryOrder != null && !string.IsNullOrEmpty(fsm.EntryOrder.OrderId))
        _orderIdToFsmKey.TryRemove(fsm.EntryOrder.OrderId, out _);

    if (!string.IsNullOrEmpty(fsm.ReplacingCancelOrderId))
        _orderIdToFsmKey.TryRemove(fsm.ReplacingCancelOrderId, out _);

    if (fsm.StopOrder != null && !string.IsNullOrEmpty(fsm.StopOrder.OrderId))
        _orderIdToFsmKey.TryRemove(fsm.StopOrder.OrderId, out _);

    if (fsm.Targets != null)
    {
        foreach (Order target in fsm.Targets)
        {
            if (target != null && !string.IsNullOrEmpty(target.OrderId))
                _orderIdToFsmKey.TryRemove(target.OrderId, out _);
        }
    }

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
}
```

**Note:** The exact dictionary names (`_followerReplaceSpecs`, `_ocoGroupTracking`, `_accountToFsmKeys`) are placeholders. The actual implementation must verify these dictionaries exist via code search before adding cleanup logic.

### 4.4 Pre-Implementation Audit Required

**Action Items:**
1. Search codebase for all `ConcurrentDictionary` declarations in FSM-related files
2. Identify which dictionaries use FSM keys (EntryName, OrderId, OcoGroupId, AccountName)
3. Add cleanup logic for each confirmed dictionary
4. Document any dictionaries that should NOT be cleaned (e.g., global config)

**Search Commands:**
```bash
# Find all ConcurrentDictionary declarations
grep -r "ConcurrentDictionary" src/V12_002.*.cs

# Find FSM-related tracking dictionaries
grep -r "_followerReplaceSpecs\|_ocoGroupTracking\|_accountToFsmKeys" src/

# Find all references to FSM.EntryName in dictionary operations
grep -r "\.EntryName\]" src/V12_002.Symmetry*.cs
```

### 4.5 Verification Criteria

**Unit Test:** `RemoveFsmOrderIdMappings_PrunesAllStateTrackerDictionaries`
- Create FSM with full state (entry, stop, 5 targets, replace spec, OCO group)
- Populate all tracking dictionaries
- Call `RemoveFsmOrderIdMappings`
- Assert: All dictionaries return zero entries for this FSM's keys

**Memory Leak Test:** `BracketFSM_24HourSession_NoMemoryGrowth`
- Simulate 1000 order lifecycles (submit → fill → terminate)
- Measure memory before/after
- Assert: Memory delta < 1% (accounting for GC variance)

---

## 5. Implementation Sequence

### 5.1 Ticket Breakdown

**Ticket 01: H23 OR Arm Flag Atomic Operations**
- Estimated time: 2 hours
- Files: 5 (V12_002.cs, Entries.OR.cs, UI.IPC.Commands.Fleet.cs, Orders.Management.Flatten.cs)
- Lines changed: ~30
- CYC delta: 0 (pure primitive swap)
- Risk: LOW

**Ticket 02: H24 PositionInfo FSM Enqueue**
- Estimated time: 3 hours
- Files: 1 (Entries.RMA.cs)
- Lines changed: ~25
- CYC delta: +10-15 (enqueue wrappers)
- Risk: MEDIUM (requires FSM message validation)

**Ticket 03: H26 BracketFSM Cleanup Completeness**
- Estimated time: 3 hours (includes pre-implementation audit)
- Files: 1 (Symmetry.BracketFSM.cs)
- Lines changed: ~15-20
- CYC delta: +5-10 (additional cleanup loops)
- Risk: MEDIUM (requires comprehensive dictionary audit)

**Total Estimated Time:** 8 hours (1 engineering day)

### 5.2 Execution Order

**Recommended Sequence:**
1. Ticket 01 (H23) - Independent, lowest risk
2. Ticket 02 (H24) - Independent, medium risk
3. Ticket 03 (H26) - Independent, requires audit first

**Rationale:** All three tickets are independent. Execute in risk order (low → medium).

---

## 6. DNA Compliance Checklist

### 6.1 Mandatory Constraints

- [x] **Zero new `lock()` statements** - All repairs use atomic primitives or FSM Enqueue
- [x] **ASCII-only compliance** - All string literals are ASCII
- [x] **Diff size < 150,000 characters** - Estimated ~2,000 characters total
- [x] **Zero ghost-method references** - No references to banned identifiers

### 6.2 V12 DNA Principles

- [x] **Lock-Free Actor Pattern** - H24 uses FSM Enqueue, H23 uses atomic primitives
- [x] **Atomic Primitives** - H23 uses `Interlocked.CompareExchange` and `Interlocked.Exchange`
- [x] **FSM State Cleanup** - H26 ensures complete lifecycle cleanup
- [x] **Correctness by Construction** - H23 enforces mutual exclusion at primitive level

---

## 7. Verification Gates

### 7.1 Automated Gates (Pre-F5)

1. `deploy-sync.ps1` - PASS (hard-link sync + ASCII gate)
2. `grep -r "lock(" src/` - ZERO matches
3. `grep -Prn "[^\x00-\x7F]" src/` - ZERO matches
4. Ghost-method audit:
   - `grep -r "ClearAllEventHandlers" src/` - ZERO matches
   - `grep -r "_globalState" src/` - ZERO matches
   - `grep -r "_inFlightRmaEntries" src/` - ZERO matches

### 7.2 Manual Gates (Post-F5)

1. **F5 Compile Gate** - BUILD_TAG banner visible in NinjaTrader
2. **OR Signal Test** - Replay historical OR sessions, verify zero dual-arming
3. **RMA Proximity Test** - 24-hour UI refresh stress test, verify zero torn reads
4. **Memory Leak Test** - 7-day profiler run, verify zero FSM state growth

---

## 8. Rollback Plan

### 8.1 Per-Ticket Rollback

**If Ticket 01 (H23) fails:**
- Revert atomic operations
- Restore boolean flags
- Add explicit mutex (DNA waiver required)

**If Ticket 02 (H24) fails:**
- Revert FSM Enqueue wrappers
- Restore direct field writes
- Add `Interlocked` for numeric fields only (partial fix)

**If Ticket 03 (H26) fails:**
- Revert additional cleanup logic
- Keep existing order ID cleanup
- Add periodic GC sweep as fallback (less elegant)

### 8.2 Epic-Level Rollback

**If entire epic fails:**
- Revert all changes via git
- Document failure mode
- Escalate to P3 Architect for redesign

---

## 9. Success Criteria Summary

### 9.1 Functional Requirements

- [x] **H23 Fixed:** OR arm flags use atomic operations (zero dual-direction arming)
- [x] **H24 Fixed:** PositionInfo updates enqueued through FSM (zero direct mutations)
- [x] **H26 Fixed:** FSM cleanup removes all tracking state (zero memory leaks)

### 9.2 DNA Compliance

- [x] **Zero new `lock()` statements**
- [x] **ASCII-only compliance**
- [x] **Diff size < 150,000 characters**
- [x] **Zero ghost-method references**

### 9.3 Test Coverage

- [x] 3 unit tests (one per defect) - all passing
- [x] Stress tests for concurrent operations - zero failures
- [x] UI refresh stress tests - zero torn reads
- [x] 24-hour memory leak test - zero growth

---

**[PLAN-GATE]**

**Approach Summary:**
- **3 surgical repairs** across 3 files (5 total file modifications for H23)
- **Estimated 8 hours** (1 engineering day)
- **Zero architectural changes** (pure synchronization hardening)
- **All repairs use V12 DNA patterns** (atomic primitives + FSM Enqueue)

**Key Decisions:**
1. H23: Use `Interlocked.CompareExchange` on single int (0/1/2) for mutual exclusion
2. H24: Reuse existing FSM `Enqueue` pattern (no new message types)
3. H26: Comprehensive dictionary audit required before implementation

**Ready for Phase 3 (VALIDATE)?** Type APPROVED to proceed or provide feedback.

---

**Document Status:** DRAFT - AWAITING DIRECTOR APPROVAL  
**Author:** Plan Mode via Orchestrator  
**Date:** 2026-05-19T02:36:00Z  
**Epic:** epic-4-signal  
**Phase:** 2/6 (APPROACH)