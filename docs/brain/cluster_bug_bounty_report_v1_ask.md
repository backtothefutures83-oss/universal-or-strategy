# V12 Universal OR Strategy - Cluster Bug Bounty Report
**Generated**: 2026-05-17  
**Mission**: 7-Agent Parallel Forensic Scan  
**Scope**: All V12_002 clusters (SIMA, Execution, UI, REAPER, Kernel, Signals, Infrastructure)

---

## Executive Summary

### Total Bugs Discovered: 35 Unique Issues
- **Critical**: 7 bugs (20%)
- **High**: 15 bugs (43%)
- **Medium**: 11 bugs (31%)
- **Low**: 2 bugs (6%)

### Filter Transparency
All bugs reported by the 7 agents have been included in this report. No filtering was applied based on severity or complexity. The deduplication analysis below identifies cross-cluster duplicates that represent the same underlying issue.

### Cluster Health Overview
| Cluster | Bugs Found | Status |
|---------|-----------|--------|
| S1 (SIMA Core) | 3 | Medium Risk |
| S2 (Execution Engine) | 8 | High Risk |
| S3 (UI & Photon IO) | 6 | High Risk |
| S4 (REAPER Defense) | 7 | Critical Risk |
| S5 (Kernel State) | 0 | ✅ PLATINUM STANDARD |
| S6 (Signals & Entries) | 3 | Medium Risk |
| S7 (Kernel Infrastructure) | 8 | High Risk |

---

## Deduplication Analysis

### Duplicate Group 1: Pool Release Without Finally Protection
**Canonical Bug**: BUG-POOL-001 (Critical)  
**Instances**:
- BUG-S1-001 (High): V12_002.SIMA.Fleet.cs:284-302
- BUG-S2-004 (High): V12_002.SIMA.Dispatch.cs:647,651,777 + V12_002.SIMA.Fleet.cs:70-81,298,350

**Root Cause**: PhotonPool.ReleaseByIndex() calls not protected by finally blocks, creating resource leak risk on exception.

**Unified Fix**: Wrap all pool release operations in try-finally blocks across SIMA subsystem.

---

### Duplicate Group 2: Ghost Order Window
**Canonical Bug**: BUG-GHOST-001 (Critical)  
**Instances**:
- BUG-S2-002 (Critical): V12_002.Orders.Management.cs:173
- BUG-S4-003 (Critical): V12_002.REAPER.NakedStop.cs:60-68

**Root Cause**: Order dictionary registration or guard clearing happens BEFORE broker confirmation, creating a window where the system believes an order exists but the broker hasn't confirmed it yet.

**Unified Fix**: Implement two-phase commit pattern - register order in "pending" state, transition to "active" only after broker confirmation.

---

### Duplicate Group 3: Race Condition in Dictionary Snapshot Iteration
**Canonical Bug**: BUG-SNAPSHOT-001 (Critical)  
**Instances**:
- BUG-S2-001 (Critical): V12_002.Orders.Callbacks.cs:210,342,443,462
- BUG-S7-005 (High): V12_002.LogicAudit.cs:289

**Root Cause**: TOCTOU between ContainsKey and TryGetValue, or enumeration without ToArray() snapshot.

**Unified Fix**: Always use ToArray() snapshot before iterating ConcurrentDictionary in hot paths.

---

### Duplicate Group 4: Re-entrancy Flood Risk
**Canonical Bug**: BUG-REENTRY-001 (Critical)  
**Instances**:
- BUG-S2-003 (Critical): V12_002.Orders.Callbacks.Propagation.cs:39
- BUG-S3-003 (High): V12_002.UI.Panel.Lifecycle.cs:62-84
- BUG-S4-002 (High): V12_002.REAPER.cs:135-152

**Root Cause**: Event callbacks or timer callbacks can re-enter before the finally block clears the guard flag, causing flood conditions.

**Unified Fix**: Set guard flag BEFORE any callback invocation, clear in finally. Add re-entrancy detection with early return.

---

### Duplicate Group 5: Null Reference After TryGetValue
**Canonical Bug**: BUG-NULL-001 (High)  
**Instances**:
- BUG-S1-003 (Medium): V12_002.SIMA.Shadow.cs:148
- BUG-S2-005 (High): V12_002.Orders.Callbacks.Propagation.cs:75,88,106
- BUG-S3-002 (High): V12_002.UI.Callbacks.cs:212-239

**Root Cause**: Property access on out variable without null check after TryGetValue returns true.

**Unified Fix**: Always null-check the out variable before property access, even when TryGetValue returns true (defensive programming against concurrent removal).

---

### Duplicate Group 6: FSM State Leak
**Canonical Bug**: BUG-FSM-LEAK-001 (High)  
**Instances**:
- BUG-S2-006 (High): V12_002.Symmetry.BracketFSM.cs (implied from Replace.cs:611-618)
- BUG-S4-004 (High): V12_002.REAPER.Audit.cs:143-152

**Root Cause**: Guard flags or queue entries cleared but FSM state not fully reset, leaving orphaned state.

**Unified Fix**: Implement atomic FSM reset operation that clears all related state in a single transaction.

---

### Duplicate Group 7: Non-Atomic Read-Modify-Write on Volatile
**Canonical Bug**: BUG-VOLATILE-001 (Critical)  
**Instances**:
- BUG-S7-001 (Critical): V12_002.cs:139 (retestFiredThisSession)
- BUG-S7-002 (Critical): V12_002.Data.cs:8 (_uiSnapshotTickCounter)

**Root Cause**: Increment or check-then-set operations on volatile fields are not atomic, creating race conditions.

**Unified Fix**: Replace volatile bool with Interlocked.CompareExchange, replace volatile int with Interlocked.Increment.

---

## Severity Ranking & Repair Sequence

### Phase 1: Critical Bugs (Immediate Repair Required)
1. **BUG-GHOST-001** (Ghost Order Window) - 2 instances
   - Impact: Order state corruption, potential double-fills
   - Priority: P0 - Blocks production deployment
   
2. **BUG-SNAPSHOT-001** (Dictionary Snapshot Race) - 2 instances
   - Impact: Null reference exceptions in hot paths
   - Priority: P0 - Causes runtime crashes
   
3. **BUG-REENTRY-001** (Re-entrancy Flood) - 3 instances
   - Impact: Event storm, system freeze
   - Priority: P0 - Causes system instability
   
4. **BUG-VOLATILE-001** (Non-Atomic Volatile Operations) - 2 instances
   - Impact: Race conditions in kernel state
   - Priority: P0 - Violates lock-free guarantees
   
5. **BUG-S4-009** (Use-After-Free in Flatten Queue) - 1 instance
   - Impact: Guard cleared regardless of success/failure
   - Priority: P0 - Data corruption risk

### Phase 2: High Severity Bugs (Next Sprint)
6. **BUG-POOL-001** (Pool Release Without Finally) - 2 instances
   - Impact: Resource leaks on exception
   - Priority: P1 - Memory leak risk
   
7. **BUG-NULL-001** (Null Reference After TryGetValue) - 3 instances
   - Impact: Potential null reference exceptions
   - Priority: P1 - Defensive programming gap
   
8. **BUG-FSM-LEAK-001** (FSM State Leak) - 2 instances
   - Impact: Orphaned FSM state
   - Priority: P1 - State corruption risk
   
9. **BUG-S3-001** (Panel Timer Disposal Race) - 1 instance
   - Impact: TOCTOU on _isTerminating
   - Priority: P1 - UI stability
   
10. **BUG-S4-001** (Watchdog Timer Disposal Race) - 1 instance
    - Impact: Timer callback during Dispose()
    - Priority: P1 - Safety system integrity
    
11. **BUG-S4-008** (Naked Position Grace Tracking Race) - 1 instance
    - Impact: Non-atomic check-then-act
    - Priority: P1 - REAPER audit accuracy
    
12. **BUG-S6-001** (RMA Proximity Mutation Race) - 1 instance
    - Impact: Direct mutation outside Actor pattern
    - Priority: P1 - Violates FSM/Actor mandate
    
13. **BUG-S7-003** (SignalBroadcaster Event Handler Race) - 1 instance
    - Impact: Event handler nulled between check and invocation
    - Priority: P1 - Signal propagation failure
    
14. **BUG-S7-004** (DrawObjects Enumeration Without Copy) - 1 instance
    - Impact: Collection modified during enumeration
    - Priority: P1 - UI rendering crash

### Phase 3: Medium Severity Bugs (Backlog)
15. **BUG-S1-002** (Proactive FSM Creation Race) - 1 instance
    - Impact: TOCTOU between ContainsKey and TryAdd
    - Priority: P2 - Low probability race
    
16. **BUG-S2-007** (Dictionary Iteration During Modification) - 1 instance
    - Impact: Potential enumeration exception
    - Priority: P2 - ConcurrentDictionary may handle gracefully
    
17. **BUG-S2-008** (Expected Position Delta Rollback) - 1 instance
    - Impact: Multi-step mutation without atomicity
    - Priority: P2 - Rare edge case
    
18. **BUG-S3-004** (Photon Pool Slot Leak) - 1 instance
    - Impact: _freeTop not restored on exception
    - Priority: P2 - Resource leak risk
    
19. **BUG-S3-005** (MMIO Mirror Disposal Race) - 1 instance
    - Impact: _disposed check followed by unsafe access
    - Priority: P2 - Low probability race
    
20. **BUG-S4-006** (O(N²) Fleet Audit Loop) - 1 instance
    - Impact: Performance degradation
    - Priority: P2 - Performance, not correctness
    
21. **BUG-S6-002** (TryRemove Cleanup Null Check) - 1 instance
    - Impact: TryRemove success not checked
    - Priority: P2 - Defensive programming gap
    
22. **BUG-S6-003** (Retest Session Latch Not Reset) - 1 instance
    - Impact: Latch persists across restart
    - Priority: P2 - Strategy restart edge case
    
23. **BUG-S7-007** (IpcClientSession Semaphore Leak) - 1 instance
    - Impact: Semaphore usage without verified finally
    - Priority: P2 - Potential resource leak

### Phase 4: Low Severity Bugs (Technical Debt)
24. **BUG-S3-006** (O(N²) Panel Handler Attachment) - 1 instance
    - Impact: Performance issue, not correctness
    - Priority: P3 - Optimization opportunity
    
25. **BUG-S7-008** (Dummy stateLock Retained) - 1 instance
    - Impact: Code smell, not functional bug
    - Priority: P3 - Cleanup task

---

## Per-Cluster Breakdown

### Cluster S1: SIMA Core (3 Bugs)

| Bug ID | Severity | Location | Root Cause |
|--------|----------|----------|------------|
| BUG-S1-001 | High | V12_002.SIMA.Fleet.cs:284-302 | Pool release without finally |
| BUG-S1-002 | Medium | V12_002.SIMA.Dispatch.cs:547-572 | TOCTOU in proactive FSM creation |
| BUG-S1-003 | Medium | V12_002.SIMA.Shadow.cs:148 | Null reference after TryGetValue |

**Cluster Risk**: Medium - Core dispatch logic has race conditions but no critical bugs.

---

### Cluster S2: Execution Engine (8 Bugs)

| Bug ID | Severity | Location | Root Cause |
|--------|----------|----------|------------|
| BUG-S2-001 | Critical | V12_002.Orders.Callbacks.cs:210,342,443,462 | Snapshot iteration race |
| BUG-S2-002 | Critical | V12_002.Orders.Management.cs:173 | Ghost order window |
| BUG-S2-003 | Critical | V12_002.Orders.Callbacks.Propagation.cs:39 | Re-entrancy flood risk |
| BUG-S2-004 | High | Multiple SIMA files | Pool release without finally |
| BUG-S2-005 | High | V12_002.Orders.Callbacks.Propagation.cs:75,88,106 | Null reference in TryGetValue chain |
| BUG-S2-006 | High | V12_002.Symmetry.BracketFSM.cs | FSM state leak on cancellation |
| BUG-S2-007 | Medium | V12_002.Orders.Management.Cleanup.cs:106,249 | Dictionary iteration during modification |
| BUG-S2-008 | Medium | V12_002.SIMA.Fleet.cs:284-298 | Non-atomic delta rollback |

**Cluster Risk**: High - Multiple critical bugs in order execution hot paths.

---

### Cluster S3: UI & Photon IO (6 Bugs)

| Bug ID | Severity | Location | Root Cause |
|--------|----------|----------|------------|
| BUG-S3-001 | High | V12_002.UI.Panel.Lifecycle.cs:20-60 | Panel timer disposal race |
| BUG-S3-002 | High | V12_002.UI.Callbacks.cs:212-239 | Null reference in chart click |
| BUG-S3-003 | High | V12_002.UI.Panel.Lifecycle.cs:62-84 | Re-entrancy flood in UpdatePanelState |
| BUG-S3-004 | Medium | V12_002.Photon.Pool.cs:102-117 | Pool slot leak on exception |
| BUG-S3-005 | Medium | V12_002.Photon.MmioMirror.cs:83-107 | MMIO disposal race |
| BUG-S3-006 | Low | V12_002.UI.Panel.Handlers.cs:31-42 | O(N²) handler attachment |

**Cluster Risk**: High - UI stability issues and Photon resource leaks.

---

### Cluster S4: REAPER Defense (7 Bugs)

| Bug ID | Severity | Location | Root Cause |
|--------|----------|----------|------------|
| BUG-S4-001 | High | V12_002.Safety.Watchdog.cs:25-34 | Watchdog timer disposal race |
| BUG-S4-002 | High | V12_002.REAPER.cs:135-152 | Re-entrancy flood in timer callback |
| BUG-S4-003 | Critical | V12_002.REAPER.NakedStop.cs:60-68 | Ghost order window |
| BUG-S4-004 | High | V12_002.REAPER.Audit.cs:143-152 | FSM state leak on TriggerCustomEvent failure |
| BUG-S4-006 | Medium | V12_002.REAPER.Audit.cs:16-57 | O(N²) fleet audit loop |
| BUG-S4-008 | High | V12_002.REAPER.Audit.cs:377-401 | Naked position grace tracking race |
| BUG-S4-009 | Critical | V12_002.REAPER.Audit.cs:609-639 | Use-after-free in flatten queue |

**Cluster Risk**: Critical - Safety system has multiple critical bugs that could compromise risk management.

---

### Cluster S5: Kernel State (0 Bugs)

**Status**: ✅ **PLATINUM STANDARD**

This cluster serves as the reference implementation for the rest of the codebase. All other clusters should aspire to this level of quality.

**Key Success Factors**:
- Strict Actor pattern adherence
- Comprehensive null checks
- Proper resource management
- No lock-based synchronization

---

### Cluster S6: Signals & Entries (3 Bugs)

| Bug ID | Severity | Location | Root Cause |
|--------|----------|----------|------------|
| BUG-S6-001 | High | V12_002.Entries.RMA.cs:262-334 | Direct mutation outside Actor pattern |
| BUG-S6-002 | Medium | V12_002.Entries.RMA.cs:168-170 + Trend.cs:353-355 | TryRemove success not checked |
| BUG-S6-003 | Medium | V12_002.Entries.Retest.cs:65-69 | Session latch not reset on restart |

**Cluster Risk**: Medium - Entry logic has architectural violations but no critical bugs.

---

### Cluster S7: Kernel Infrastructure (8 Bugs)

| Bug ID | Severity | Location | Root Cause |
|--------|----------|----------|------------|
| BUG-S7-001 | Critical | V12_002.cs:139 | Non-atomic volatile bool operation |
| BUG-S7-002 | Critical | V12_002.Data.cs:8 | Non-atomic volatile int increment |
| BUG-S7-003 | High | SignalBroadcaster.cs:206-230 | Event handler null-ref race |
| BUG-S7-004 | High | V12_002.DrawingHelpers.cs:186-193 | DrawObjects enumeration without copy |
| BUG-S7-005 | High | V12_002.LogicAudit.cs:289 | Dictionary enumeration without snapshot |
| BUG-S7-006 | Medium | V12_002.LogicAudit.cs:339-356 | Nested loop closure allocation |
| BUG-S7-007 | Medium | V12_002.cs:497 | Semaphore potential leak |
| BUG-S7-008 | Low | V12_002.cs:230 | Dummy stateLock retained |

**Cluster Risk**: High - Kernel infrastructure has critical volatile operation bugs.

---

## /epic-tdd Ticket Blocks

### TICKET-001: Fix Ghost Order Window (Critical)
```markdown
**Title**: Fix Ghost Order Window in Order Submission Flow

**Severity**: Critical (P0)

**Affected Files**:
- V12_002.Orders.Management.cs:173
- V12_002.REAPER.NakedStop.cs:60-68

**Root Cause**:
Order dictionary registration or guard clearing happens BEFORE broker confirmation, creating a window where the system believes an order exists but the broker hasn't confirmed it yet.

**Reproduction**:
1. Submit order via SubmitOrderUnprotected()
2. Broker rejects order due to margin/connection issue
3. System still has order in activeOrders dictionary
4. Subsequent logic operates on ghost order

**Expected Behavior**:
Order should only be registered in activeOrders AFTER broker confirmation.

**Proposed Fix**:
Implement two-phase commit pattern:
1. Register order in "pending" state
2. Transition to "active" only after OnOrderUpdate confirmation
3. Remove from pending on rejection

**Test Coverage Required**:
- Unit test: Order submission with broker rejection
- Integration test: Race between submission and rejection
- Stress test: Rapid submit/reject cycles

**Acceptance Criteria**:
- [ ] No ghost orders in activeOrders after broker rejection
- [ ] All tests pass
- [ ] No performance regression
```

---

### TICKET-002: Fix Dictionary Snapshot Race Conditions (Critical)
```markdown
**Title**: Fix Race Conditions in Dictionary Snapshot Iteration

**Severity**: Critical (P0)

**Affected Files**:
- V12_002.Orders.Callbacks.cs:210,342,443,462
- V12_002.LogicAudit.cs:289

**Root Cause**:
TOCTOU between ContainsKey and TryGetValue, or enumeration without ToArray() snapshot, causing null reference exceptions when dictionary is modified during iteration.

**Reproduction**:
1. Start iterating activePositions
2. Concurrent thread removes position
3. TryGetValue returns false but code assumes success
4. Null reference exception

**Expected Behavior**:
Always use ToArray() snapshot before iterating ConcurrentDictionary in hot paths.

**Proposed Fix**:
Replace all patterns:
```csharp
// BEFORE
foreach (var kvp in activePositions) { ... }

// AFTER
foreach (var kvp in activePositions.ToArray()) { ... }
```

**Test Coverage Required**:
- Unit test: Concurrent modification during iteration
- Stress test: High-frequency position updates during iteration

**Acceptance Criteria**:
- [ ] All dictionary iterations use ToArray() snapshot
- [ ] No null reference exceptions in hot paths
- [ ] Performance impact < 5%
```

---

### TICKET-003: Fix Re-entrancy Flood Risk (Critical)
```markdown
**Title**: Fix Re-entrancy Flood Risk in Event Callbacks

**Severity**: Critical (P0)

**Affected Files**:
- V12_002.Orders.Callbacks.Propagation.cs:39
- V12_002.UI.Panel.Lifecycle.cs:62-84
- V12_002.REAPER.cs:135-152

**Root Cause**:
Event callbacks or timer callbacks can re-enter before the finally block clears the guard flag, causing flood conditions and system freeze.

**Reproduction**:
1. TriggerCustomEvent invokes callback
2. Callback triggers another event
3. Re-enters before finally clears _propagationInFlight
4. Infinite recursion or event storm

**Expected Behavior**:
Set guard flag BEFORE any callback invocation, clear in finally. Add re-entrancy detection with early return.

**Proposed Fix**:
```csharp
// BEFORE
try {
    TriggerCustomEvent(...);
} finally {
    _propagationInFlight = false;
}

// AFTER
if (_propagationInFlight) return; // Early exit
_propagationInFlight = true;
try {
    TriggerCustomEvent(...);
} finally {
    _propagationInFlight = false;
}
```

**Test Coverage Required**:
- Unit test: Recursive event triggering
- Integration test: Event storm scenario
- Stress test: Rapid event firing

**Acceptance Criteria**:
- [ ] No re-entrancy floods
- [ ] Early exit on re-entry attempt
- [ ] All tests pass
```

---

### TICKET-004: Fix Non-Atomic Volatile Operations (Critical)
```markdown
**Title**: Fix Non-Atomic Read-Modify-Write on Volatile Fields

**Severity**: Critical (P0)

**Affected Files**:
- V12_002.cs:139 (retestFiredThisSession)
- V12_002.Data.cs:8 (_uiSnapshotTickCounter)

**Root Cause**:
Increment or check-then-set operations on volatile fields are not atomic, creating race conditions that violate lock-free guarantees.

**Reproduction**:
1. Thread A reads retestFiredThisSession (false)
2. Thread B reads retestFiredThisSession (false)
3. Both threads set to true
4. Both threads fire retest signal (duplicate)

**Expected Behavior**:
Use Interlocked operations for atomic read-modify-write.

**Proposed Fix**:
```csharp
// BEFORE
private volatile bool retestFiredThisSession = false;
if (!retestFiredThisSession) {
    retestFiredThisSession = true;
    // fire signal
}

// AFTER
private int retestFiredThisSession = 0;
if (Interlocked.CompareExchange(ref retestFiredThisSession, 1, 0) == 0) {
    // fire signal
}

// BEFORE
private volatile int _uiSnapshotTickCounter = 0;
_uiSnapshotTickCounter++;

// AFTER
private int _uiSnapshotTickCounter = 0;
Interlocked.Increment(ref _uiSnapshotTickCounter);
```

**Test Coverage Required**:
- Unit test: Concurrent flag setting
- Stress test: High-frequency concurrent access

**Acceptance Criteria**:
- [ ] All volatile operations replaced with Interlocked
- [ ] No race conditions in kernel state
- [ ] All tests pass
```

---

### TICKET-005: Fix Use-After-Free in Flatten Queue (Critical)
```markdown
**Title**: Fix Use-After-Free Window in Flatten Queue Processing

**Severity**: Critical (P0)

**Affected Files**:
- V12_002.REAPER.Audit.cs:609-639

**Root Cause**:
Guard cleared in finally regardless of success/failure, allowing concurrent access to queue entry that may still be processing.

**Reproduction**:
1. Start processing flatten queue entry
2. Exception occurs during processing
3. Finally clears guard
4. Concurrent thread accesses same entry
5. Use-after-free or double-processing

**Expected Behavior**:
Guard should only be cleared on successful completion. On failure, entry should remain locked or be marked as failed.

**Proposed Fix**:
```csharp
// BEFORE
try {
    ProcessFlattenQueue();
} finally {
    _flattenInProgress = false;
}

// AFTER
bool success = false;
try {
    ProcessFlattenQueue();
    success = true;
} finally {
    if (success) {
        _flattenInProgress = false;
    } else {
        // Mark entry as failed, don't clear guard
        LogError("Flatten queue processing failed, guard retained");
    }
}
```

**Test Coverage Required**:
- Unit test: Exception during flatten processing
- Integration test: Concurrent flatten attempts
- Stress test: Rapid flatten queue operations

**Acceptance Criteria**:
- [ ] Guard only cleared on success
- [ ] No use-after-free conditions
- [ ] Failed entries properly handled
- [ ] All tests pass
```

---

### TICKET-006: Fix Pool Release Without Finally Protection (High)
```markdown
**Title**: Fix Pool Release Without Finally Block Protection

**Severity**: High (P1)

**Affected Files**:
- V12_002.SIMA.Fleet.cs:284-302
- V12_002.SIMA.Dispatch.cs:647,651,777
- V12_002.SIMA.Fleet.cs:70-81,298,350

**Root Cause**:
PhotonPool.ReleaseByIndex() calls not protected by finally blocks, creating resource leak risk on exception.

**Reproduction**:
1. Acquire pool slot
2. Exception occurs during processing
3. Pool slot never released
4. Pool exhaustion after repeated failures

**Expected Behavior**:
All pool acquisitions must be wrapped in try-finally with guaranteed release.

**Proposed Fix**:
```csharp
// BEFORE
int slot = PhotonPool.AcquireSlot();
// ... processing ...
PhotonPool.ReleaseByIndex(slot);

// AFTER
int slot = PhotonPool.AcquireSlot();
try {
    // ... processing ...
} finally {
    PhotonPool.ReleaseByIndex(slot);
}
```

**Test Coverage Required**:
- Unit test: Exception during pool slot usage
- Stress test: Repeated acquire/release with exceptions
- Resource leak test: Monitor pool exhaustion

**Acceptance Criteria**:
- [ ] All pool releases wrapped in finally
- [ ] No resource leaks on exception
- [ ] Pool exhaustion test passes
- [ ] All tests pass
```

---

### TICKET-007: Fix Null Reference After TryGetValue (High)
```markdown
**Title**: Fix Null Reference Exceptions After TryGetValue

**Severity**: High (P1)

**Affected Files**:
- V12_002.SIMA.Shadow.cs:148
- V12_002.Orders.Callbacks.Propagation.cs:75,88,106
- V12_002.UI.Callbacks.cs:212-239

**Root Cause**:
Property access on out variable without null check after TryGetValue returns true. Concurrent removal can cause null reference.

**Reproduction**:
1. TryGetValue returns true
2. Concurrent thread removes entry
3. Out variable is null
4. Property access throws null reference exception

**Expected Behavior**:
Always null-check the out variable before property access, even when TryGetValue returns true.

**Proposed Fix**:
```csharp
// BEFORE
if (dict.TryGetValue(key, out var value)) {
    var prop = value.Property; // Unsafe
}

// AFTER
if (dict.TryGetValue(key, out var value) && value != null) {
    var prop = value.Property; // Safe
}
```

**Test Coverage Required**:
- Unit test: Concurrent removal during TryGetValue
- Stress test: High-frequency concurrent access

**Acceptance Criteria**:
- [ ] All TryGetValue followed by null check
- [ ] No null reference exceptions
- [ ] All tests pass
```

---

### TICKET-008: Fix FSM State Leak (High)
```markdown
**Title**: Fix FSM State Leak on Failure Paths

**Severity**: High (P1)

**Affected Files**:
- V12_002.Symmetry.BracketFSM.cs (implied from Replace.cs:611-618)
- V12_002.REAPER.Audit.cs:143-152

**Root Cause**:
Guard flags or queue entries cleared but FSM state not fully reset, leaving orphaned state.

**Reproduction**:
1. Start FSM operation
2. Exception or failure occurs
3. Guard cleared but FSM state remains
4. Subsequent operations see stale state

**Expected Behavior**:
Implement atomic FSM reset operation that clears all related state in a single transaction.

**Proposed Fix**:
```csharp
// Create atomic reset method
private void ResetFsmState(string fsmId) {
    // Clear all related state atomically
    _repairInFlight = false;
    _repairQueue.TryRemove(fsmId, out _);
    // Reset FSM internal state
    fsm.Reset();
}

// Use in finally blocks
try {
    ProcessFsm(fsmId);
} catch {
    ResetFsmState(fsmId);
    throw;
}
```

**Test Coverage Required**:
- Unit test: FSM operation with exception
- Integration test: FSM state consistency after failure
- Stress test: Rapid FSM operations with failures

**Acceptance Criteria**:
- [ ] No orphaned FSM state
- [ ] Atomic reset operation
- [ ] All tests pass
```

---

### TICKET-009: Fix Panel Timer Disposal Race (High)
```markdown
**Title**: Fix Race Condition in Panel Refresh Timer Disposal

**Severity**: High (P1)

**Affected Files**:
- V12_002.UI.Panel.Lifecycle.cs:20-60

**Root Cause**:
TOCTOU race on _isTerminating between checks. Timer callback can execute after disposal starts.

**Reproduction**:
1. Timer callback checks _isTerminating (false)
2. Dispose() sets _isTerminating = true
3. Timer callback proceeds with disposed resources
4. Null reference or access violation

**Expected Behavior**:
Use Interlocked.CompareExchange for atomic termination flag.

**Proposed Fix**:
```csharp
// BEFORE
private volatile bool _isTerminating = false;
if (!_isTerminating) {
    // ... timer work ...
}

// AFTER
private int _isTerminating = 0;
if (Interlocked.CompareExchange(ref _isTerminating, 1, 0) == 0) {
    // ... timer work ...
}
```

**Test Coverage Required**:
- Unit test: Concurrent timer callback and disposal
- Stress test: Rapid panel create/destroy cycles

**Acceptance Criteria**:
- [ ] No race condition on termination flag
- [ ] Timer safely disposed
- [ ] All tests pass
```

---

### TICKET-010: Fix Watchdog Timer Disposal Race (High)
```markdown
**Title**: Fix Race Condition in Watchdog Timer Disposal

**Severity**: High (P1)

**Affected Files**:
- V12_002.Safety.Watchdog.cs:25-34

**Root Cause**:
Timer callback may still execute during Dispose(), accessing disposed resources.

**Reproduction**:
1. Watchdog timer fires
2. Dispose() called concurrently
3. Timer callback accesses disposed resources
4. Null reference or access violation

**Expected Behavior**:
Use Timer.Dispose(WaitHandle) to ensure callback completes before disposal.

**Proposed Fix**:
```csharp
// BEFORE
_timer?.Dispose();

// AFTER
using (var waitHandle = new ManualResetEvent(false)) {
    _timer?.Dispose(waitHandle);
    waitHandle.WaitOne();
}
```

**Test Coverage Required**:
- Unit test: Concurrent timer callback and disposal
- Stress test: Rapid watchdog create/destroy cycles

**Acceptance Criteria**:
- [ ] Timer safely disposed with wait
- [ ] No race condition
- [ ] All tests pass
```

---

### TICKET-011: Fix Naked Position Grace Tracking Race (High)
```markdown
**Title**: Fix Race Condition in Naked Position Grace Tracking

**Severity**: High (P1)

**Affected Files**:
- V12_002.REAPER.Audit.cs:377-401

**Root Cause**:
Non-atomic check-then-act on _nakedPositionFirstSeen dictionary.

**Reproduction**:
1. Thread A checks if position in _nakedPositionFirstSeen (false)
2. Thread B checks if position in _nakedPositionFirstSeen (false)
3. Both threads add entry with different timestamps
4. Grace period calculation incorrect

**Expected Behavior**:
Use TryAdd for atomic check-and-add operation.

**Proposed Fix**:
```csharp
// BEFORE
if (!_nakedPositionFirstSeen.ContainsKey(posId)) {
    _nakedPositionFirstSeen[posId] = DateTime.UtcNow;
}

// AFTER
_nakedPositionFirstSeen.TryAdd(posId, DateTime.UtcNow);
```

**Test Coverage Required**:
- Unit test: Concurrent naked position detection
- Stress test: High-frequency position updates

**Acceptance Criteria**:
- [ ] Atomic check-and-add operation
- [ ] No race condition
- [ ] All tests pass
```

---

### TICKET-012: Fix RMA Proximity Mutation Race (High)
```markdown
**Title**: Fix Race Condition in RMA Proximity Monitoring

**Severity**: High (P1)

**Affected Files**:
- V12_002.Entries.RMA.cs:262-334

**Root Cause**:
Direct mutation of PositionInfo fields outside Actor pattern, violating FSM/Actor mandate.

**Reproduction**:
1. MonitorRmaProximity directly mutates pos.RmaProximityState
2. Concurrent thread reads pos.RmaProximityState
3. Race condition on state transition

**Expected Behavior**:
All state mutations must use Enqueue wrapper to maintain Actor pattern.

**Proposed Fix**:
```csharp
// BEFORE
pos.RmaProximityState = newState;

// AFTER
EnqueueStateUpdate(() => {
    pos.RmaProximityState = newState;
});
```

**Test Coverage Required**:
- Unit test: Concurrent RMA proximity updates
- Integration test: Actor pattern compliance
- Stress test: High-frequency proximity monitoring

**Acceptance Criteria**:
- [ ] All mutations use Enqueue wrapper
- [ ] Actor pattern compliance verified
- [ ] All tests pass
```

---

### TICKET-013: Fix SignalBroadcaster Event Handler Race (High)
```markdown
**Title**: Fix Race Condition in SignalBroadcaster Event Handler

**Severity**: High (P1)

**Affected Files**:
- SignalBroadcaster.cs:206-230

**Root Cause**:
Event handler could be nulled between check and invocation.

**Reproduction**:
1. Check if event handler != null
2. Concurrent thread unsubscribes (sets to null)
3. Invoke null handler
4. Null reference exception

**Expected Behavior**:
Use local copy pattern for thread-safe event invocation.

**Proposed Fix**:
```csharp
// BEFORE
if (OnSignal != null) {
    OnSignal(args);
}

// AFTER
var handler = OnSignal;
if (handler != null) {
    handler(args);
}
```

**Test Coverage Required**:
- Unit test: Concurrent subscribe/unsubscribe
- Stress test: High-frequency event firing

**Acceptance Criteria**:
- [ ] Local copy pattern used
- [ ] No null reference exceptions
- [ ] All tests pass
```

---

### TICKET-014: Fix DrawObjects Enumeration Without Copy (High)
```markdown
**Title**: Fix Collection Modified During Enumeration in DrawObjects

**Severity**: High (P1)

**Affected Files**:
- V12_002.DrawingHelpers.cs:186-193

**Root Cause**:
Collection modified during enumeration without defensive copy.

**Reproduction**:
1. Start enumerating DrawObjects
2. Concurrent thread adds/removes drawing
3. Collection modified exception

**Expected Behavior**:
Always use ToArray() or ToList() before enumerating mutable collections.

**Proposed Fix**:
```csharp
// BEFORE
foreach (var drawing in DrawObjects) { ... }

// AFTER
foreach (var drawing in DrawObjects.ToArray()) { ... }
```

**Test Coverage Required**:
- Unit test: Concurrent drawing modification
- Stress test: High-frequency drawing updates

**Acceptance Criteria**:
- [ ] Defensive copy before enumeration
- [ ] No collection modified exceptions
- [ ] All tests pass
```

---

## Conclusion

This consolidated report represents the findings from 7 parallel forensic scans across all V12_002 clusters. The deduplication analysis identified 7 major bug families affecting multiple clusters, reducing the 35 individual bugs to approximately 25 unique root causes.

**Immediate Action Required**:
- Phase 1 (Critical): 7 bugs requiring immediate repair before production deployment
- Phase 2 (High): 14 bugs to be addressed in the next sprint
- Phase 3 (Medium): 11 bugs for backlog prioritization
- Phase 4 (Low): 2 technical debt items for cleanup

**Cluster S5 (Kernel State)** serves as the PLATINUM STANDARD reference implementation. All repair work should follow the patterns established in this cluster.

**Next Steps**:
1. Director approval of repair sequence
2. Create GitHub issues from ticket blocks
3. Assign to appropriate agents (Bob CLI for src/ work)
4. Execute Phase 1 repairs with mandatory Arena AI adjudication
5. Verify fixes with stress testing and forensic re-scan

---

**Report Generated By**: Orchestrator (Gemini CLI)  
**Validation Status**: Awaiting Director Sign-off  
**Deployment Readiness**: BLOCKED until Phase 1 repairs complete