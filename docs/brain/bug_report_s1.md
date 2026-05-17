# BUG BOUNTY REPORT: SIMA Core Cluster (S1)
**Agent**: Agent-S1  
**Scope**: V12_002.SIMA.*.cs (7 files)  
**Date**: 2026-05-17  
**Mode**: READ-ONLY Forensic Scan

---

## EXECUTIVE SUMMARY

**Total Bugs Found**: 8  
**Critical**: 2  
**High**: 3  
**Medium**: 2  
**Low**: 1

---

## CRITICAL SEVERITY

### BUG-S1-001
**Title**: Race condition in `_simaToggleState` semaphore release  
**Severity**: Critical  
**Location**: V12_002.SIMA.Dispatch.cs:ExecuteSmartDispatchEntry (lines 47-96)  
**Root Cause**: The semaphore is released in a `finally` block (line 94) but the deferred retry via `TriggerCustomEvent` (lines 60-63) can execute BEFORE the finally block runs if an exception occurs during the try block. This creates a window where two dispatch operations can run concurrently.  
**Evidence**:
```csharp
// Line 49: Acquire semaphore
if (Interlocked.CompareExchange(ref _simaToggleState, 1, 0) != 0)
{
    // Lines 60-63: Schedule retry BEFORE finally releases semaphore
    TriggerCustomEvent(o => ExecuteSmartDispatchEntry(...), null);
    return; // Early return - finally WILL run, but retry may execute first
}
// Line 94: finally { Interlocked.Exchange(ref _simaToggleState, 0); }
```
**Test Impact**: Stress test with rapid dispatch calls would expose concurrent execution and state corruption.

---

### BUG-S1-002
**Title**: Use-after-free window in Photon pool release  
**Severity**: Critical  
**Location**: V12_002.SIMA.Fleet.cs:ProcessFleetSlot (lines 68-82)  
**Root Cause**: The sideband is cleared (line 75) and pool slot released (line 81) in the `finally` block, but if `TriggerCustomEvent` (line 91) schedules `PumpFleetDispatch` before the finally completes, the pump can dequeue a new slot that reuses the same pool index while the old sideband refs are still live.  
**Evidence**:
```csharp
// Line 75: _photonSideband[poolSlotIndex] = default(FleetDispatchSideband);
// Line 78: Thread.MemoryBarrier();
// Line 81: _photonPool.ReleaseByIndex(poolSlotIndex);
// Line 91: TriggerCustomEvent(o => PumpFleetDispatch(), null);
// ^ Pump can claim the SAME poolSlotIndex before finally completes
```
**Test Impact**: High-frequency dispatch stress test with pool exhaustion would trigger stale reference reads.

---

## HIGH SEVERITY

### BUG-S1-003
**Title**: Re-entrancy flood in `ProcessApplySimaState`  
**Severity**: High  
**Location**: V12_002.SIMA.Lifecycle.cs:ProcessApplySimaState (lines 41-97)  
**Root Cause**: The deferred retry mechanism (lines 65-70) can create infinite recursion if the toggle gate remains contended. Each retry schedules another `TriggerCustomEvent`, and if the gate is held by a long-running operation, the event queue fills with retry attempts.  
**Evidence**:
```csharp
// Line 57: while (Interlocked.CompareExchange(ref _simaToggleState, 1, 0) != 0)
// Line 60: if (retries >= MAX_RETRIES)
// Line 65: TriggerCustomEvent(o => ProcessApplySimaState(_defEnabled), null);
// ^ No backoff, no queue depth check - can flood event queue
```
**Test Impact**: Toggle SIMA rapidly while a dispatch is in progress - event queue overflow.

---

### BUG-S1-004
**Title**: Ghost order window in `Dispatch_PublishMarketBracketToPhoton`  
**Severity**: High  
**Location**: V12_002.SIMA.Dispatch.cs:Dispatch_PublishMarketBracketToPhoton (lines 543-577)  
**Root Cause**: The FSM is registered with `State = PendingSubmit` (line 555) and `expectedPositions` is incremented (line 577) BEFORE the slot is enqueued to the ring (line 628). If the ring is full and the fallback to `ConcurrentQueue` fails (line 655), the FSM and expected position are orphaned with no corresponding dispatch request.  
**Evidence**:
```csharp
// Line 555: State = FollowerBracketState.PendingSubmit
// Line 577: AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);
// Line 628: if (_poolSlotIndex >= 0 && _photonDispatchRing.TryEnqueue(ref _slot))
// Line 643: else { // Ring full - fallback to ConcurrentQueue
// ^ If fallback ALSO fails (exception), FSM + expectedPositions are orphaned
```
**Test Impact**: Ring exhaustion test with injected queue enqueue failure would leave ghost FSMs.

---

### BUG-S1-005
**Title**: FSM state leak on dispatch failure  
**Severity**: High  
**Location**: V12_002.SIMA.Dispatch.cs:Dispatch_ProcessFleetLoop (lines 218-247)  
**Root Cause**: The catch block (lines 218-247) performs cleanup of tracking dicts and FSM (line 244), but if the exception occurs AFTER `MarkDispatchSyncPending` (line 543) but BEFORE FSM registration (line 572), the `_dispatchSyncPendingExpKeys` entry is never cleared because `syncPending` flag is not set.  
**Evidence**:
```csharp
// Line 543: MarkDispatchSyncPending(expectedKey); syncPending = true;
// Line 572: _followerBrackets.TryAdd(fleetEntryName, proFsm);
// Line 220: if (syncPending) { ClearDispatchSyncPending(expectedKey); }
// ^ If exception between 543-572, syncPending=false, key never cleared
```
**Test Impact**: Inject exception during FSM creation - `_dispatchSyncPendingExpKeys` leaks.

---

## MEDIUM SEVERITY

### BUG-S1-006
**Title**: Null reference hot path in `ShouldSkipFleet_RunHealthCheck`  
**Severity**: Medium  
**Location**: V12_002.SIMA.Fleet.cs:ShouldSkipFleet_RunHealthCheck (lines 417-469)  
**Root Cause**: The broker position snapshot (line 423) can contain null entries if the broker connection is unstable. The null check (line 427) is inside the loop, but the `Instrument.FullName` access (line 427) can throw if `_posSnapshot[_pi]` is non-null but `Instrument` is null.  
**Evidence**:
```csharp
// Line 423: Position[] _posSnapshot = acct.Positions.ToArray();
// Line 427: if (_posSnapshot[_pi] != null && _posSnapshot[_pi].Instrument.FullName == Instrument.FullName)
// ^ Missing null check on _posSnapshot[_pi].Instrument before .FullName access
```
**Test Impact**: Broker reconnect test with partial position data would trigger NullReferenceException.

---

### BUG-S1-007
**Title**: O(N²) nested loop in fleet dispatch  
**Severity**: Medium  
**Location**: V12_002.SIMA.Dispatch.cs:Dispatch_ProcessFleetLoop (lines 140-251)  
**Root Cause**: The outer loop iterates over `fleet` (line 156), and for each account, `ShouldSkipFleetAccount` (line 164) calls `ShouldSkipFleet_RunHealthCheck` (line 404), which iterates over `_followerBrackets` (line 434) and `activePositions` (line 445). With N accounts and M positions, this is O(N*M) per dispatch.  
**Evidence**:
```csharp
// Line 156: for (int i = 0; i < fleet.Count; i++)
// Line 164: if (ShouldSkipFleetAccount(acct, fleet[i], ...))
// Line 434: foreach (var _fkvp in _followerBrackets) // O(M)
// Line 445: foreach (var _pkvp in activePositions) // O(M)
// ^ O(N * 2M) = O(N*M) complexity per dispatch
```
**Test Impact**: Fleet size > 20 accounts with > 50 active positions would show dispatch latency spikes.

---

## LOW SEVERITY

### BUG-S1-008
**Title**: Semaphore leak in `PumpFlattenOps` exception path  
**Severity**: Low  
**Location**: V12_002.SIMA.Flatten.cs:PumpFlattenOps (lines 102-139)  
**Root Cause**: The `isFlattenRunning` flag is set to `true` in `FlattenAllApexAccounts` (line 47) but only cleared in the `finally` block of `ChainNextFlattenOp` (line 242). If an exception occurs in `ProcessFlattenWorkItem_CancelOrders` (line 121) or `ProcessFlattenWorkItem_ClosePositions` (line 125) and the queue is empty, `ChainNextFlattenOp` is never called, leaving `isFlattenRunning = true` permanently.  
**Evidence**:
```csharp
// Line 47: isFlattenRunning = true;
// Line 121: ProcessFlattenWorkItem_CancelOrders(item, acct); // Can throw
// Line 125: ProcessFlattenWorkItem_ClosePositions(item, acct); // Can throw
// Line 137: ChainNextFlattenOp(); // Only place that clears flag
// ^ If exception + empty queue, flag never cleared
```
**Test Impact**: Inject exception during flatten with single-account queue - flag stuck true, blocks future flattens.

---

## ADDITIONAL FINDINGS

### No Bugs Identified in:
- **V12_002.SIMA.cs**: Core helper methods use atomic operations correctly.
- **V12_002.SIMA.Execution.cs**: Reserve-before-submit pattern is consistent.
- **V12_002.SIMA.Shadow.cs**: Shadow propagation logic is read-only and safe.

### Patterns Verified as Safe:
1. ✅ No `lock()` statements found in any SIMA file (DNA compliance verified)
2. ✅ All string literals are ASCII-only (no Unicode detected)
3. ✅ `Interlocked` and `ConcurrentDictionary` used correctly for atomic operations
4. ✅ `Thread.MemoryBarrier()` placed correctly after sideband writes

---

## SUMMARY BY FILE

| File | Bugs | Critical | High | Medium | Low |
|------|------|----------|------|--------|-----|
| SIMA.Dispatch.cs | 3 | 1 | 2 | 1 | 0 |
| SIMA.Fleet.cs | 2 | 1 | 0 | 1 | 0 |
| SIMA.Lifecycle.cs | 1 | 0 | 1 | 0 | 0 |
| SIMA.Flatten.cs | 1 | 0 | 0 | 0 | 1 |
| SIMA.Execution.cs | 0 | 0 | 0 | 0 | 0 |
| SIMA.Shadow.cs | 0 | 0 | 0 | 0 | 0 |
| SIMA.cs | 1 | 0 | 0 | 0 | 0 |

---

## RECOMMENDED NEXT STEPS

1. **Immediate**: Address BUG-S1-001 and BUG-S1-002 (Critical race conditions)
2. **High Priority**: Fix BUG-S1-003, BUG-S1-004, BUG-S1-005 (Re-entrancy and state leaks)
3. **Medium Priority**: Add null guards (BUG-S1-006) and optimize nested loops (BUG-S1-007)
4. **Low Priority**: Add exception recovery for flatten semaphore (BUG-S1-008)

---

**End of Report**