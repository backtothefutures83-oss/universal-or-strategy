# Bug Bounty Report: Agent-S2 (Execution Engine Cluster)

**Cluster**: Execution Engine -- Orders / Symmetry / Trailing
**Scope**: 14 files across 3 sub-modules
**Date**: 2026-05-17
**Runner**: Qwen 3.6 Max Preview
**Mode**: READ-ONLY forensic scan (no src/ edits)

## Files Scanned

| Module | File |
|--------|------|
| Orders.Callbacks | `V12_002.Orders.Callbacks.cs` |
| Orders.Callbacks | `V12_002.Orders.Callbacks.AccountOrders.cs` (778 lines) |
| Orders.Callbacks | `V12_002.Orders.Callbacks.Execution.cs` |
| Orders.Callbacks | `V12_002.Orders.Callbacks.Propagation.cs` (675 lines) |
| Orders.Management | `V12_002.Orders.Management.cs` |
| Orders.Management | `V12_002.Orders.Management.Cleanup.cs` |
| Orders.Management | `V12_002.Orders.Management.Flatten.cs` |
| Orders.Management | `V12_002.Orders.Management.StopSync.cs` (655 lines) |
| Orders.Gateway | `V12_002.Orders.CancelGateway.cs` |
| Symmetry | `V12_002.Symmetry.BracketFSM.cs` |
| Symmetry | `V12_002.Symmetry.Follower.cs` |
| Symmetry | `V12_002.Symmetry.Replace.cs` |
| Trailing | `V12_002.Trailing.Breakeven.cs` |
| Trailing | `V12_002.Trailing.StopUpdate.cs` |

---

## Executive Summary

**Total Bugs Found**: 11
| Severity | Count |
|----------|-------|
| Critical | 1 |
| High | 4 |
| Medium | 4 |
| Low | 2 |

---

## Findings (Ordered Critical -> High -> Med -> Low)

### BUG-S2-001
**Title**: Ghost order window: stop pre-registered in dictionary before broker Submit completes
**Severity**: Critical
**Location**: `V12_002.Orders.Management.cs` lines 133-141 (`SubmitStopOrderSafe`, follower path)
**Root Cause**: `stopOrders[entryName] = sOrd` writes the stop reference into the tracking dictionary BEFORE `pos.ExecutingAccount.Submit(new[] { sOrd })` is called. If the Submit throws, the catch block does `TryRemove`, but there is a window between the dictionary write and the Submit call where other code paths (e.g., `HasActiveOrPendingOrderForEntry`, `CancelAllOrdersForEntry`) can observe a stop entry that has not been submitted to the broker. This creates a false-positive "protected position" signal -- the system believes the position has a live stop when it does not.
**Evidence**:
```
stopOrders[entryName] = sOrd;           // Line 133: pre-register
pos.ExecutingAccount.Submit(new[] { sOrd }); // Line 134: async broker call
```
Compare with the master path (lines 156-157) which submits first then writes -- the correct ordering. The follower path reverses this order. If `Submit` is slow or hangs (broker latency), the window can be hundreds of milliseconds, during which `FlattenAll` or `CancelAllOrdersForEntry` may try to cancel an order the broker has never seen, resulting in a silent no-op and a position left unprotected.
**Test Impact**: Integration test with injected Submit latency would expose the window; unit test on dictionary state vs broker state divergence.

---

### BUG-S2-002
**Title**: Non-atomic check-then-act on FSM state in HandleMatchedFollower_PendingCancelReplace
**Severity**: High
**Location**: `V12_002.Orders.Callbacks.AccountOrders.cs` lines 420-456 (`HandleMatchedFollower_PendingCancelReplace`)
**Root Cause**: The method reads `fsm.State` at line 424 to check for `PendingCancel`, then writes `fsm.State = FollowerBracketState.Submitting` at line 453. This is a non-atomic check-then-act on a field that can be concurrently modified by `DrainAccountMailbox` (which also writes `fsm.State`). Between the check and the write, another broker event processed through the mailbox could transition the FSM to a different state (e.g., `Filled`, `Cancelled`), and the unconditional overwrite to `Submitting` at line 453 would obliterate that transition. The `FollowerBracketFSM.TryTransition` method uses CAS for safe transitions, but this code path bypasses it entirely and uses a naked property write.
**Evidence**: Line 453: `fsm.State = FollowerBracketState.Submitting;` -- direct assignment, not `TryTransition`. Compare with `ProcessBracketEvent` (BracketFSM.cs line 384) which also uses direct assignment but runs inside the serial drain. The AccountOrders.cs path runs from `ProcessQueuedAccountOrder` via `TriggerCustomEvent`, which is also strategy-thread-serialized, so concurrency with DrainAccountMailbox is mitigated by NT8 single-threading. HOWEVER, the FSM state is also read from `GetFsmExpectedPosition` (BracketFSM.cs line 420) which iterates `_followerBrackets` and reads `f.State` without any synchronization. If the state write at line 453 happens concurrently with `GetFsmExpectedPosition` reading, the Interlocked-based property accessor provides atomicity for individual reads/writes, but `GetFsmExpectedPosition` reads the state multiple times per FSM (once for the state check, then again for the entry action), creating a TOCTOU window within the iteration.
**Test Impact**: Concurrent FSM state mutation test with rapid fill+cancel events would expose inconsistent state observations in `GetFsmExpectedPosition`.

---

### BUG-S2-003
**Title**: Stale pending replacement purge can race with new replacement creation, losing stop protection
**Severity**: High
**Location**: `V12_002.Trailing.StopUpdate.cs` lines 25-53 (`CleanupStalePendingReplacements`)
**Root Cause**: When a stale pending replacement is detected (>5 seconds old), the method calls `TryRemove` on `pendingStopReplacements`, decrements `pendingReplacementCount`, and then calls `CreateNewStopOrder` with `isRecovery: true`. The problem is that between `TryRemove` and `CreateNewStopOrder`, a concurrent call to `UpdateStopQuantity` (from a target fill on the execution callback path) can detect that `pendingStopReplacements` has no entry for this key and create a NEW `PendingStopReplacement` via `TryAdd`. Now two competing stop creation flows are in flight: the recovery path from `CleanupStalePendingReplacements` and the normal path from `UpdateStopQuantity`. The recovery path calls `CreateNewStopOrder(entryName, replacementQty, pending.StopPrice, pending.Direction, isRecovery: true)`, which force-cancels the existing tracked stop (lines 385-392 in StopSync.cs). But the normal path has already stored the old stop order reference in its new pending record. Result: the recovery path cancels the stop, the normal path's pending record references a now-cancelled order, and when the cancel confirmation arrives, both handlers try to create a replacement stop -- potentially creating two stops for the same position.
**Evidence**: `CleanupStalePendingReplacements` (line 43): `CreateNewStopOrder(kvp.Key, replacementQty, pending.StopPrice, pending.Direction, isRecovery: true);` -- this runs after `TryRemove` at line 30. `UpdateStopQuantity` (StopSync.cs lines 260-272): checks `TryGetValue`, then `TryAdd` -- both are atomic on ConcurrentDictionary, but the gap between TryRemove in cleanup and TryAdd in UpdateStopQuantity creates the race window.
**Test Impact**: Stress test with rapid target fills while pending replacements age past 5 seconds would trigger duplicate stop creation.

---

### BUG-S2-004
**Title**: Interlocked counter drift: pendingReplacementCount incremented asynchronously via Enqueue but decremented synchronously
**Severity**: High
**Location**: `V12_002.Orders.Management.StopSync.cs` line 320 vs. `V12_002.Trailing.StopUpdate.cs` line 34
**Root Cause**: In `CreateNewStopOrder`, the stop reference is written via `Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; })` at line 320. This is an asynchronous write -- the Enqueue queues a lambda for the strategy thread drain but returns immediately. However, `pendingReplacementCount` is decremented synchronously via `Interlocked.Decrement` when stale pendings are purged (line 34 in CleanupStalePendingReplacements). More critically, the count is INCREMENTED synchronously in `UpdateStopQuantity` at line 270 via `Interlocked.Increment(ref pendingReplacementCount)` right after `TryAdd`. But the corresponding stop dictionary write is deferred through Enqueue. This means: the counter says there are N pending replacements, but the `pendingStopReplacements` dictionary may have fewer entries because some TryAdd results are still in-flight in the actor queue. Any code that reads `pendingReplacementCount` to make decisions (e.g., circuit breaker logic) gets an inflated count that doesn't match reality.
**Evidence**: StopSync.cs line 270: `int currentCount = Interlocked.Increment(ref pendingReplacementCount);` -- runs synchronously. StopSync.cs line 320: `Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; });` -- runs asynchronously. The counter and the dictionary can diverge.
**Test Impact**: Counter accuracy test under high-velocity stop replacement bursts would show count/dict divergence.

---

### BUG-S2-005
**Title**: Master entry resolution uses bidirectional Contains() substring matching, risking false-positive master identification
**Severity**: High
**Location**: `V12_002.Orders.Callbacks.Propagation.cs` lines 401-407 (`PropagateMasterEntryMove`)
**Root Cause**: The code searches for the master signal name using bidirectional `Contains`:
```csharp
if (!kvp.Value.IsFollower &&
    (fleetEntryName.Contains(kvp.Key) || kvp.Key.Contains(fleetEntryName)))
```
This is the EXACT pattern that BUILD 927 and BUILD 984 explicitly warned against in other locations. BUILD 927 comment: "Do NOT use Contains('_TYPE_') -- if an account name itself contains a trade-type substring, Contains() misclassifies." BUILD 984 [FIX-B]: "Bidirectional .Contains() caused accidental cascade of unrelated positions: e.g. signal 'OR' matched 'Fleet_Apex_RETEST_OR_1' incidentally." If a master entry name is "OR_1" and a follower entry name is "Fleet_Apex_RETEST_OR_1", the bidirectional Contains will match, and `masterSignalName` will be set to "OR_1" even though the follower is linked to a different master. This causes the FSM replacement spec to carry the wrong master signal name, which then affects `expectedPositions` re-assertion logic in `SubmitFollowerReplacement_ReassertExpected`.
**Evidence**: Propagation.cs line 403-404: `fleetEntryName.Contains(kvp.Key) || kvp.Key.Contains(fleetEntryName)`. Compare with the corrected pattern in Replace.cs line 139: `kvp.Key == orderSignal || kvp.Key.Contains("_" + orderSignal + "_") || kvp.Key.EndsWith("_" + orderSignal)` -- delimiter-anchored matching.
**Test Impact**: Integration test with overlapping signal name substrings (e.g., "OR_1" and "RETEST_OR_1") would show incorrect master linking.

---

### BUG-S2-006
**Title**: CIT follower nudge writes directly to entryOrders dictionary outside Enqueue context
**Severity**: Medium
**Location**: `V12_002.Orders.Management.Flatten.cs` line 152 (`ManageCIT`, follower path)
**Root Cause**: After cancelling and resubmitting a follower limit entry via the account API, the code writes `entryOrders[key] = nudgedOrder;` directly at line 152. The comment at line 148 states: "B966: No Enqueue needed -- ManageCIT is always called via Enqueue(ctx => ctx.ManageCIT()) from OnBarUpdate." This is correct IF ManageCIT is always called through Enqueue. However, if ManageCIT is ever called directly (e.g., from a future code change, IPC command handler, or test), this dictionary write would occur outside the actor drain, creating a concurrent mutation with the strategy thread. The comment is the sole guarantee, and there is no enforcement mechanism. Furthermore, the re-schedule path at line 132 does `Enqueue(ctx => ctx.ManageCIT()); return;` -- when the rescheduled call executes, it re-enters ManageCIT from the top, re-scanning all entryOrders. Orders already nudged in the previous partial run are correctly skipped by `_citNudgedKeys`, but the budget resets to MaxBrokerCallsPerCycle, so the re-entry processes the same set of orders again with a fresh budget. This is not a correctness bug but means the budget mechanism is not actually a per-cycle cap -- it's a per-invocation cap, and rescheduling can result in more than MaxBrokerCallsPerCycle total broker calls across re-entries within a single OnBarUpdate cycle.
**Evidence**: Line 152: `entryOrders[key] = nudgedOrder;` -- direct dictionary indexer write. Compare with the follower stop submission path (StopSync.cs line 320): `Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; });` which correctly wraps the write.
**Test Impact**: Direct invocation of ManageCIT (bypassing Enqueue) would demonstrate concurrent dictionary mutation risk.

---

### BUG-S2-007
**Title**: Mutable FollowerReplaceSpec fields (PendingQty/PendingPrice) updated outside Enqueue on potentially concurrent path
**Severity**: Medium
**Location**: `V12_002.Orders.Callbacks.Propagation.cs` lines 449-452 (`PropagateFollowerEntryReplace`)
**Root Cause**: When an in-flight replacement spec exists, the code updates `existing.PendingQty = newQty; existing.PendingPrice = newPrice;` directly without any synchronization. These fields are later read inside `TriggerCustomEvent` in `HandleMatchedFollower_PendingCancelReplace` at line 471 (`fsmCapture.PendingPrice`, `fsmCapture.PendingQty`). While the NT8 strategy thread serializes OnOrderUpdate callbacks, the read happens inside a `TriggerCustomEvent` lambda which is scheduled for a future strategy-thread execution. Between the write (at line 450-451) and the scheduled read (at line 471), the original `TriggerCustomEvent` that called `SubmitFollowerReplacement` could have already run and read stale values. The comment at line 467 acknowledges this: "A TR tick absorption may have updated PendingPrice/PendingQty after the lambda was scheduled -- using stale captures would submit wrong values." The fix uses `fsmCapture.PendingPrice` to re-read at execution time, which is correct, but the underlying issue remains: the spec fields are mutable non-atomic fields accessed across scheduled boundaries without synchronization.
**Evidence**: Lines 450-451: `existing.PendingQty = newQty; existing.PendingPrice = newPrice;` -- plain field assignments. Line 467 comment explicitly describes the staleness concern.
**Test Impact**: Rapid ATR tick absorption during high volatility (multiple price moves before cancel confirmation) could result in replacement orders submitted with stale prices.

---

### BUG-S2-008
**Title**: FollowerBracketFSM.RemainingContracts field is non-atomic and mutated from strategy thread while read from GetFsmExpectedPosition iteration
**Severity**: Medium
**Location**: `V12_002.Symmetry.BracketFSM.cs` line 357 (`HandleFsmFilled`) and line 432 (`GetFsmExpectedPosition`)
**Root Cause**: `FollowerBracketFSM.RemainingContracts` is a plain `int` field (line 76) without `Interlocked` or `volatile` protection. It is written in `HandleFsmFilled` (line 357: `fsm.RemainingContracts = Math.Max(0, fsm.RemainingContracts - Math.Max(0, evt.FilledQty));`) and read in `GetFsmExpectedPosition` (via the `f.EntryOrder.Quantity` read at line 441). While both paths run on the strategy thread (DrainAccountMailbox and direct calls), `GetFsmExpectedPosition` iterates `_followerBrackets` without any snapshot protection. If a fill event modifies `fsm.RemainingContracts` during the iteration, the read could observe a torn or stale value. On x86/x64, int reads are atomic at the hardware level, but the C# memory model does not guarantee visibility across threads. More importantly, the compound read-modify-write at line 357 (`fsm.RemainingContracts = ... fsm.RemainingContracts - ...`) is not atomic, so a second fill event arriving before the first completes could lose a decrement.
**Evidence**: Line 76: `public int RemainingContracts;` -- plain field. Line 357: `fsm.RemainingContracts = Math.Max(0, fsm.RemainingContracts - Math.Max(0, evt.FilledQty));` -- compound RMW. Contrast with `FollowerBracketFSM.State` which uses Interlocked-backed `_packedState`.
**Test Impact**: Concurrent fill events for the same follower bracket (rapid stop + target fills) could result in incorrect RemainingContracts, causing premature Filled state transition.

---

### BUG-S2-009
**Title**: SymmetryGuardSubmitFollowerBracket writes target dictionaries outside Enqueue context
**Severity**: Medium
**Location**: `V12_002.Symmetry.Follower.cs` lines 318-322 (`SymmetryGuardSubmitFollowerBracket`)
**Root Cause**: The stop order write at line 319 correctly uses `Enqueue`: `{ var _fen966 = fleetEntryName; var _s966 = stop; Enqueue(ctx => { ctx.stopOrders[_fen966] = _s966; }); }`. However, the target dictionary writes at line 321 are direct: `foreach (var (targetNum, order) in stagedTargets) GetTargetOrdersDictionary(targetNum)[fleetEntryName] = order;` -- these are NOT wrapped in Enqueue. If `SymmetryGuardSubmitFollowerBracket` is called from `SymmetryGuardOnFollowerFill` (which is called from the account callback path via `OnAccountOrderUpdate` -> `ProcessAccountOrder_UpdateFleetExpected`), the target dictionary writes occur outside the actor drain. This is inconsistent with the stop write (which IS enqueued) and creates a window where `HasActiveOrPendingOrderForEntry` could see the stop but not the targets, or vice versa.
**Evidence**: Line 319: `Enqueue(ctx => { ctx.stopOrders[_fen966] = _s966; });` -- correct. Line 321: `GetTargetOrdersDictionary(targetNum)[fleetEntryName] = order;` -- NOT enqueued, direct dictionary write.
**Test Impact**: Rapid follower fill + concurrent flatten could observe partial bracket state (stop present, targets absent).

---

### BUG-S2-010
**Title**: Stop replacement circuit breaker count check is not atomic with activation
**Severity**: Low
**Location**: `V12_002.Trailing.StopUpdate.cs` lines 174-179 (`InitiateStopReplacement`)
**Root Cause**: The circuit breaker check reads `pendingReplacementCount` via `Interlocked.Increment` (which is atomic) but then checks `currentCount >= CIRCUIT_BREAKER_THRESHOLD && !circuitBreakerActive` in a non-atomic compound condition. Two concurrent threads could both increment, both see `currentCount >= threshold`, both see `circuitBreakerActive == false`, and both attempt to set `circuitBreakerActive = true` and print the activation message. The practical impact is minor (duplicate print, redundant boolean write), but it violates the "correctness by construction" principle. The fix would use `Interlocked.CompareExchange` on a packed state variable.
**Evidence**: Line 175: `int currentCount = Interlocked.Increment(ref pendingReplacementCount);` followed by line 176: `if (currentCount >= CIRCUIT_BREAKER_THRESHOLD && !circuitBreakerActive)` -- the `!circuitBreakerActive` read is not atomic with the increment.
**Test Impact**: Concurrent stop replacement storms from multiple trailing triggers could activate circuit breaker multiple times with duplicate logging.

---

### BUG-S2-011
**Title**: HandleMatchedFollower_PendingCancelReplace sets FSM state to Submitting but then takes masterFilled early-exit path without reverting state
**Severity**: Low
**Location**: `V12_002.Orders.Callbacks.AccountOrders.cs` lines 453-466
**Root Cause**: At line 453, `fsm.State = FollowerBracketState.Submitting;` is set unconditionally. Then at line 455-465, if `masterFilled` is true, the code removes the spec (`_followerReplaceSpecs.TryRemove`), clears dispatch sync pending, enqueues a reaper repair, and returns true. The FSM object still exists in `_followerBrackets` with state `Submitting`, but the replacement spec has been destroyed. The FSM is never transitioned back to `Active` or any other state. Any subsequent `DrainAccountMailbox` processing that reads `fsm.State` will see `Submitting` for a bracket that is actually active (master filled, follower should be flat/repaired). While the REAPER repair path should eventually handle the position, the FSM state is left in a misleading intermediate state that does not reflect reality.
**Evidence**: Line 453: `fsm.State = FollowerBracketState.Submitting;` -- irreversible. Line 460-464: spec removed, repair enqueued, return -- FSM state never reverted. The FSM comment at BracketFSM.cs line 52 describes `Replacing` as "In-flight two-phase cancel+resubmit" but `Submitting` is not a documented terminal or recovery state for this scenario.
**Test Impact**: FSM state audit after master fill during cancel gap would show inconsistent `Submitting` state for a bracket that should be `Active` or `Cancelled`.

---

## DNA Compliance Section

| Check | Result | Details |
|-------|--------|---------|
| `lock()` statements | **PASS** | Zero `lock(` statements found across all 14 scoped files. All `stateLock` references are in code comments only. |
| Non-ASCII string literals | **PASS** | Zero non-ASCII characters found in C# string literals across all scoped files. |
| `Thread.Sleep()` in hot path | **PASS** | Zero `Thread.Sleep` calls found across all scoped files. |
| `Dictionary<K,V>` writes without atomic guard | **WARN** | Most dictionary writes use `ConcurrentDictionary` with atomic operations (`TryAdd`, `TryRemove`, `TryGetValue`). However, several direct indexer writes (`dict[key] = value`) occur outside `Enqueue` contexts: (1) `entryOrders[key] = nudgedOrder` in Flatten.cs:152, (2) target dictionary writes in Follower.cs:321, (3) `_followerReplaceSpecs[fleetEntryName] = spec` in Propagation.cs:476, (4) `_followerTargetReplaceSpecs[signalName] = tSpec` in Replace.cs:84 and Breakeven.cs:276/465. While ConcurrentDictionary indexer writes are thread-safe internally, they bypass the actor serialization model and can create logical races with Enqueue-deferred operations. |

---

## Recommendations

1. **BUG-S2-001 (Critical)**: Reverse the ordering in `SubmitStopOrderSafe` follower path -- call `Submit` first, then write to `stopOrders` on success. Move `stopOrders[entryName] = sOrd` into the Enqueue that already follows.

2. **BUG-S2-002 (High)**: Replace direct `fsm.State = ...` assignments with `fsm.TryTransition(...)` CAS-based transitions throughout AccountOrders.cs to prevent non-atomic check-then-act.

3. **BUG-S2-005 (High)**: Replace bidirectional `Contains` in `PropagateMasterEntryMove` with delimiter-anchored matching (same pattern as Replace.cs line 139).

4. **BUG-S2-009 (Medium)**: Wrap target dictionary writes in `SymmetryGuardSubmitFollowerBracket` inside `Enqueue` to match the stop write pattern at line 319.

5. **BUG-S2-008 (Medium)**: Use `Interlocked.Exchange` for `RemainingContracts` writes in `HandleFsmFilled`, or pack it into the existing `_packedState` atomic word alongside state and generation.
