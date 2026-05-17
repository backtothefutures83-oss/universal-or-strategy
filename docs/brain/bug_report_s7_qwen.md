# Bug Bounty Report: Agent-S7 (Kernel Infrastructure Cluster)

| Field       | Value |
|-------------|-------|
| **Cluster** | S7 - Kernel Infrastructure |
| **Runner**  | Qwen 3.6 Max Preview |
| **Date**    | 2026-05-17 |
| **Scope**   | 11 files in V12_002 kernel core + SignalBroadcaster |
| **Mode**    | READ-ONLY forensic scan |

## Files Scanned

1. `src/V12_002.cs` (999 lines - main kernel: fields, actor, IPC client, SIMA internals, FSM classes)
2. `src/V12_002.Constants.cs` (22 lines - static constants)
3. `src/V12_002.LogicAudit.cs` (380 lines - 9 audit test cases)
4. `src/V12_002.DrawingHelpers.cs` (162 lines - OR box drawing, timezone conversion, stable hash)
5. `src/V12_002.AccountUpdate.cs` (18 lines - placeholder data class)
6. `src/V12_002.BarUpdate.cs` (270 lines - OnBarUpdate entry point, OR window management)
7. `src/V12_002.Atm.cs` (17 lines - placeholder ATM enum)
8. `src/V12_002.PureLogic.cs` (90 lines - extracted pure math kernels)
9. `src/V12_002.Data.cs` (12 lines - placeholder)
10. `src/V12_002.PositionInfo.cs` (320 lines - PositionInfo class, target ladder guard, pending stop replacement)
11. `src/SignalBroadcaster.cs` (395 lines - static event broadcaster for Master/Slave IPC)

## Executive Summary

| Severity | Count |
|----------|-------|
| Critical | 1 |
| High     | 3 |
| Medium   | 3 |
| Low      | 3 |
| **Total** | **10** |

---

## Critical Findings

### BUG-S7-001
**Title**: ZeroAllocOrderIdMap.TryAdd TOCTOU race: CAS on hash followed by non-atomic field writes
**Severity**: Critical
**Location**: `V12_002.cs` - `ZeroAllocOrderIdMap.TryAdd` (lines 704-748)
**Root Cause**: The method uses `Interlocked.CompareExchange` on `_table[idx].OrderIdHash` to claim a slot, but then writes `FsmKeyIndex` and `Generation` to the same `_table[idx]` entry AFTER the CAS succeeds (lines 740-742). A concurrent `TryGet` call on another thread can observe `OrderIdHash` as non-zero (valid) and immediately read `FsmKeyIndex` and `Generation` before the post-CAS writes complete, returning stale/default values (0 for int). This produces a valid-looking but incorrect FSM key lookup.

**Evidence**:
```
// Line 736-742: CAS succeeds, then fields written AFTER
if (Interlocked.CompareExchange(ref _table[idx].OrderIdHash, hash, 0) == 0)
{
    _table[idx].FsmKeyIndex = entry.FsmKeyIndex;  // NOT atomic with CAS
    _table[idx].Generation = entry.Generation;     // NOT atomic with CAS
    return true;
}
```
Meanwhile TryGet (lines 756-770) reads all three fields independently with only `Volatile.Read` on the hash -- no barrier on FsmKeyIndex/Generation.

**Test Impact**: Concurrent stress test: simultaneous TryAdd/TryGet on different threads with colliding hashes would return wrong FSM key or generation 0, causing broker callback to misroute to wrong FSM.

---

## High Findings

### BUG-S7-002
**Title**: FollowerBracketFSM non-packed fields lack memory barriers for cross-thread visibility
**Severity**: High
**Location**: `V12_002.Symmetry.BracketFSM.cs` (referenced from `V12_002.cs` lines 673-676, `_followerBrackets` dictionary) and FSM field declarations (BracketFSM.cs lines 72-77, 124-131)
**Root Cause**: The FSM class correctly uses atomic `_packedState` (long) for State/Generation/Pending via Interlocked operations. However, the remaining mutable fields -- `RemainingContracts` (int), `EntryOrder`, `StopOrder`, `Targets` (Order refs), `IsInSync` (bool), `LastBrokerError` (string), `ExpectedEntryPrice`, `ExpectedStopPrice`, `ExpectedTargetPrices` (doubles) -- are all plain non-volatile fields. These are read from the REAPER timer thread (V12_002.REAPER.Audit.cs scans `_followerBrackets.Values`), written from broker callback threads (OnAccountOrderUpdate, OnAccountExecutionUpdate), and read/written from the strategy thread. Without `volatile` or `Interlocked` guards, the C# memory model does not guarantee that writes from one thread are visible to readers on other threads. A REAPER audit cycle could observe a stale `RemainingContracts` value and trigger a false repair.

**Evidence**: `RemainingContracts` is plain `int` (not `volatile int`), yet read in REAPER.Audit.cs line 310 and written in broker callbacks. Compare to `PositionInfo.RemainingContracts` which IS declared `volatile` (PositionInfo.cs line 56).

**Test Impact**: REAPER desync test: set RemainingContracts on strategy thread, read from timer thread after 100ms -- could observe stale value triggering false repair.

### BUG-S7-003
**Title**: `_subscribedAccountNames` HashSet is not thread-safe
**Severity**: High
**Location**: `V12_002.cs` line 538 (field declaration), `V12_002.SIMA.Lifecycle.cs` line 176 (Add), `V12_002.SIMA.Fleet.cs` lines 502/523 (foreach/Clear)
**Root Cause**: `_subscribedAccountNames` is declared as `HashSet<string>` -- a non-thread-safe collection. It is written to via `.Add()` during SIMA initialization (Lifecycle.cs line 176), iterated via `foreach` during unsubscribe (SIMA.Fleet.cs line 502), and cleared (SIMA.Fleet.cs line 523). While the actor model serializes most access, the `HashSet` has no memory barriers, meaning a thread reading the collection's internal buckets array may observe a partially-updated state if an `Add` is in progress on another thread. More importantly, if SIMA initialization and cleanup overlap (e.g., rapid toggle on/off), the `Clear` could corrupt the internal state while a `foreach` is iterating, causing `InvalidOperationException` or silent data corruption.

**Evidence**:
```csharp
// V12_002.cs line 538:
private readonly HashSet<string> _subscribedAccountNames = new HashSet<string>();

// SIMA.Lifecycle.cs line 176 (write):
_subscribedAccountNames.Add(acct.Name);

// SIMA.Fleet.cs line 502 (read):
foreach (string acctName in _subscribedAccountNames)

// SIMA.Fleet.cs line 523 (write):
_subscribedAccountNames.Clear();
```

**Test Impact**: Rapid SIMA toggle on/off cycle test -- concurrent Add + foreach/Clear would throw or corrupt internal state. Fix: use `ConcurrentDictionary<string, byte>` or wrap in Interlocked-protected snapshot.

### BUG-S7-004
**Title**: PositionInfo non-volatile fields mutated across threads without memory barriers
**Severity**: High
**Location**: `V12_002.PositionInfo.cs` lines 38-96 (class definition)
**Root Cause**: `PositionInfo` has exactly one `volatile` field (`RemainingContracts` at line 56). All other mutable fields -- `CurrentStopPrice`, `BracketSubmitted`, `PendingCleanup`, `EntryFilled`, `T1Filled` through `T5Filled`, `CurrentTrailLevel`, `FlattenAttemptCount`, `IsRMATrade`, `IsTRENDTrade`, etc. -- are plain fields. These objects are stored in `ConcurrentDictionary<string, PositionInfo> activePositions` and accessed from: (1) strategy thread via Enqueue actor closures, (2) broker callback threads (OnAccountOrderUpdate, OnAccountExecutionUpdate), and (3) REAPER timer thread. While the ConcurrentDictionary provides safe add/remove semantics, once a PositionInfo reference is obtained via TryGetValue, all field access is unprotected. A broker callback thread writing `pos.T1Filled = true` is not guaranteed to be visible to the strategy thread reading `pos.T1Filled` on the next OnBarUpdate call.

**Evidence**: Only `RemainingContracts` has `volatile` (line 56). Compare to 30+ other mutable state fields without any thread-safety annotation.

**Test Impact**: Partial fill race: broker thread sets T1Filled=true, strategy thread reads stale false and submits duplicate target order.

---

## Medium Findings

### BUG-S7-005
**Title**: LogicAudit Case 9 writes expectedPositions directly, bypassing REAPER grace stamp
**Severity**: Medium
**Location**: `V12_002.LogicAudit.cs` lines 338-365 (AuditCase9_ReaperDesync)
**Root Cause**: The drift probe writes `ctx.expectedPositions[acctName] = driftedQty` directly via the ConcurrentDictionary indexer (lines 348, 351), bypassing `SetExpectedPositionLocked` which performs critical side effects: (a) `Interlocked.Exchange(ref _lastExpectedPositionSetTicks, ...)` to set the 5-second REAPER grace window, (b) `_dispatchSyncPendingExpKeys.TryRemove(...)` cleanup, and (c) `StampAccountFillGrace(accountName)`. The comment on line 350 says "this is a read-only probe" but it IS writing to shared state. While the Enqueue wrapper serializes execution to the strategy thread, the direct write means the REAPER grace timestamp is not updated. If the audit runs and the REAPER timer fires between the drift and restore (within the same actor cycle -- unlikely but the Enqueue closures are executed sequentially), the REAPER would see the drifted value WITHOUT the grace protection and could trigger a false repair.

**Evidence**:
```csharp
// LogicAudit.cs line 348: Direct write, no grace stamp
ctx.expectedPositions[acctName] = driftedQty;
// ... vs SetExpectedPositionLocked (SIMA.cs line 108-119) which does:
// expectedPositions[accountName] = value;
// Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
// StampAccountFillGrace(accountName);
```

**Test Impact**: Audit case 9 with active REAPER and positions -- could trigger false desync repair if timing aligns.

### BUG-S7-006
**Title**: FollowerReplaceSpec mutable fields accessed concurrently without synchronization
**Severity**: Medium
**Location**: `V12_002.cs` lines 630-644 (class declaration), `_followerReplaceSpecs` ConcurrentDictionary usage across multiple files
**Root Cause**: `FollowerReplaceSpec` is a mutable class with fields `State`, `CancellingOrderId`, `PendingQty`, `PendingPrice`, `LastSubmitError`, etc. Stored in `ConcurrentDictionary<string, FollowerReplaceSpec> _followerReplaceSpecs`. The dictionary operations (TryAdd, TryRemove, TryGetValue) are thread-safe, but the fields of the retrieved object are NOT. Multiple code paths read and write these fields from different threads: broker callbacks (OnAccountOrderUpdate) read `spec.State` and `spec.CancellingOrderId`, while strategy thread code writes `spec.State = FollowerReplaceState.Submitting`. No volatile, Interlocked, or lock protects these field accesses. A reader could observe a torn `State` value (e.g., halfway between `PendingCancel` and `Submitting`).

**Evidence**: `FollowerReplaceSpec.State` is plain `FollowerReplaceState` enum (backed by int, not volatile). Read in AccountOrders.cs line 344 (`spec.State != FollowerReplaceState.PendingCancel`) and written elsewhere during replace initiation.

**Test Impact**: Concurrent broker order callback + strategy thread replace state transition could observe inconsistent state and skip ghost-order cleanup.

### BUG-S7-007
**Title**: SignalBroadcaster.SafeInvoke silently swallows subscriber exceptions with zero diagnostics
**Severity**: Medium
**Location**: `SignalBroadcaster.cs` lines 203-222 (SafeInvoke method)
**Root Cause**: The `SafeInvoke` method catches all exceptions from individual subscribers and silently discards them (line 214: `catch (Exception) { }`). While this is a valid subscriber-isolation pattern (prevents one bad handler from breaking the fan-out), the complete absence of logging means a subscriber that consistently fails will never be detected. In a trading system, a silently failing subscriber could mean a slave strategy never receives stop-update signals, leaving positions unprotected. The latency logging on line 218 only fires if total fan-out exceeds 1ms, which won't catch a fast-throwing handler.

**Evidence**:
```csharp
// SignalBroadcaster.cs lines 212-215:
try
{
    ((EventHandler<T>)d).Invoke(null, args);
}
catch (Exception)
{
    // Swallow -- subscriber isolation; don't break fan-out for other listeners
}
```

**Test Impact**: Subscriber that throws on every signal will never be detected. A compliance audit logging subscriber that fails would silently stop recording. Add at minimum a `Print` or counter for exception frequency per handler.

---

## Low Findings

### BUG-S7-008
**Title**: ZeroAllocOrderIdMap.TryAdd wastes FSM key pool slot on CAS failure
**Severity**: Low
**Location**: `V12_002.cs` - `ZeroAllocOrderIdMap.TryAdd` (lines 719-728)
**Root Cause**: The method claims a pool slot via `Interlocked.Increment(ref _fsmKeyPoolIndex)` (line 720) BEFORE the CAS on `OrderIdHash` (line 736). If the CAS fails (another thread claimed the slot first due to hash collision), the method continues probing but the pool slot is already consumed -- `Interlocked.Decrement` is only called if `keyIdx >= _fsmKeyPool.Length` (line 724). Under sustained hash collisions, this wastes pool slots. The pool capacity equals the hash table capacity, so in theory there are enough slots, but a pathological collision pattern could exhaust the pool prematurely while the table still has empty slots.

**Evidence**: Line 720: `int keyIdx = Interlocked.Increment(ref _fsmKeyPoolIndex) - 1;` happens inside the probe loop before CAS. No rollback of pool index on CAS failure.

**Test Impact**: Pool exhaustion test with 512 concurrent inserts of colliding keys -- could fail TryAdd before table is actually full.

### BUG-S7-009
**Title**: activePositions[fleetKey] direct indexer overwrites without existence check
**Severity**: Low
**Location**: `V12_002.SIMA.Execution.cs` line 481 (`activePositions[fleetKey] = fleetFollowerPos`)
**Root Cause**: The fleet follower entry uses the ConcurrentDictionary indexer (`activePositions[fleetKey] = ...`) instead of `TryAdd`. The indexer will silently overwrite an existing entry if one exists. The comment on line 480 says "dicts registered atomically" but there is no check for a pre-existing entry. If a fleet entry key somehow already exists (e.g., from a previous incomplete cleanup), the old PositionInfo is silently discarded -- its orders become orphaned with no stop coverage.

**Evidence**: Line 481-482:
```csharp
activePositions[fleetKey] = fleetFollowerPos; // FIRST: dicts registered atomically
entryOrders[fleetKey] = fEntry;
```
No `TryAdd` or `ContainsKey` guard. Compare to master entry paths which typically use TryAdd.

**Test Impact**: Fleet dispatch with stale key from incomplete cleanup would orphan old position. Add `TryAdd` with logging on failure.

### BUG-S7-010
**Title**: SignalBroadcaster.ClearAllSubscribers is not atomic across all events
**Severity**: Low
**Location**: `SignalBroadcaster.cs` lines 383-392 (ClearAllSubscribers method)
**Root Cause**: `ClearAllSubscribers` nullifies 9 events sequentially (lines 385-392). There is no atomicity guarantee across the set. If a broadcast method (e.g., `BroadcastTradeSignal`) is called concurrently while `ClearAllSubscribers` is midway through nullifying events, some events will be null and others will still have subscribers. This creates a partial-clear state where some listeners receive the final signal and others don't. While each individual event's `= null` is atomic (reference assignment), the compound operation across all events is not. In practice this is unlikely to cause issues since ClearAllSubscribers is typically called during shutdown, but it violates the "correctness by construction" principle.

**Evidence**:
```csharp
// SignalBroadcaster.cs lines 385-392:
OnTradeSignal = null;       // If broadcast fires here...
OnTrailUpdate = null;
OnTargetAction = null;
OnFlattenAll = null;        // ...these still have subscribers
OnBreakevenRequest = null;
OnStopUpdate = null;
OnEntryUpdate = null;
OnOrderCancel = null;
OnExternalCommand = null;
```

**Test Impact**: Concurrent ClearAllSubscribers + BroadcastTradeSignal could deliver signal to some handlers but not others, depending on timing.

---

## DNA Compliance Check

| Rule | Status | Details |
|------|--------|---------|
| **No `lock()` statements** | **PASS** | Zero `lock()` blocks found in any of the 11 in-scope files. The `stateLock` field exists (V12_002.cs line 230) but is marked as a "dummy field" retained for compatibility; all grep matches for `lock(` are in comments referencing removed locks. |
| **ASCII-only string literals** | **PASS** | No non-ASCII characters detected in any of the 11 in-scope files (verified via regex scan for `[^\x00-\x7F]`). |
| **No `Thread.Sleep()` in hot path** | **PASS** | No `Thread.Sleep()` calls found in any of the 11 in-scope files. (Two instances exist in `V12_002.UI.IPC.Server.cs` which is out of scope.) |
| **Dictionary writes without atomic guard** | **FAIL** | Three violations identified: (1) `_subscribedAccountNames` is a non-thread-safe `HashSet<string>` (BUG-S7-003). (2) `_pendingStickyFleetToggles` is a `Dictionary<string, bool>` (V12_002.cs line 267) -- though accessed only from the strategy thread during lifecycle transitions, it should be `ConcurrentDictionary` for consistency. (3) `_modeSetFlagsDispatch` and `_modeExecDispatch` are `Dictionary<string, Action>` (V12_002.cs lines 251-252) -- read-only after initialization on strategy thread, so low risk but still non-conformant with the "no Dictionary" spirit. |

---

## Cross-File Dependency Map

```
V12_002.cs (kernel)
  |-- V12_002.BarUpdate.cs        (OnBarUpdate -> ProcessIpcCommands, Enqueue calls)
  |-- V12_002.PositionInfo.cs     (PositionInfo class, referenced by all order files)
  |-- V12_002.LogicAudit.cs       (Audit cases read activePositions, expectedPositions)
  |-- V12_002.DrawingHelpers.cs   (DrawORBox called from BarUpdate)
  |-- V12_002.Constants.cs        (BUILD_TAG, version constants)
  |-- V12_002.AccountUpdate.cs    (Placeholder - no active deps)
  |-- V12_002.Atm.cs              (Placeholder - no active deps)
  |-- V12_002.PureLogic.cs        (Static math kernels - zero NT deps)
  |-- V12_002.Data.cs             (Placeholder - no active deps)
  |
  |-- V12_002.Symmetry.BracketFSM.cs  (FollowerBracketFSM - NOT in scope but referenced)
  |-- V12_002.SIMA.cs                 (expectedPositions wrappers - NOT in scope)
  |-- V12_002.SIMA.Execution.cs       (Fleet entry - NOT in scope)
  |-- V12_002.SIMA.Lifecycle.cs       (SIMA init - NOT in scope)
  |-- V12_002.SIMA.Fleet.cs           (Fleet dispatch - NOT in scope)
  |
SignalBroadcaster.cs (static events - standalone, referenced by out-of-scope files)
```

## Recommendations

1. **BUG-S7-001 (Critical)**: Pack all three fields (OrderIdHash, FsmKeyIndex, Generation) into a single `long` or use a struct with `Interlocked.CompareExchange` on the entire entry. The current split-CAS approach is fundamentally broken.

2. **BUG-S7-002/004 (High)**: Either make all cross-thread mutable fields in `FollowerBracketFSM` and `PositionInfo` volatile, or wrap all access in `Enqueue` closures. The current mixed approach (one volatile field, many non-volatile) is the worst of both worlds -- it signals awareness but incomplete protection.

3. **BUG-S7-003 (High)**: Replace `HashSet<string>` with `ConcurrentDictionary<string, byte>` to eliminate the thread-safety gap.

4. **BUG-S7-005 (Medium)**: Route the LogicAudit Case 9 probe through `SetExpectedPositionLocked` or add a dedicated test-mode flag that suppresses REAPER repairs during audit execution.

5. **BUG-S7-007 (Medium)**: Add an exception counter and periodic warning to `SafeInvoke` so silent subscriber failures become observable.
