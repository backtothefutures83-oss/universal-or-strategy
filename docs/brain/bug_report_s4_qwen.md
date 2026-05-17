# Bug Report: Agent-S4 (REAPER Defense Cluster)

**Cluster**: REAPER Defense Cluster (Safety Hub + Watchdog)
**Scope**: 5 files, 1,150+ lines
**Date**: 2026-05-17
**Runner**: Qwen 3.6 Max Preview
**Mode**: READ-ONLY forensic scan. No src/ edits.

## Files Scanned

| File | Lines | Role |
|------|-------|------|
| `src/V12_002.REAPER.cs` | 152 | Shared state declarations, timer lifecycle, fill-grace logic |
| `src/V12_002.REAPER.Audit.cs` | 731 | Fleet/master position audit, desync detection, flatten processing |
| `src/V12_002.REAPER.NakedStop.cs` | 78 | Naked-position emergency stop submission |
| `src/V12_002.REAPER.Repair.cs` | 241 | Ghost-position repair order re-issue engine |
| `src/V12_002.Safety.Watchdog.cs` | 305 | Deadlock detection watchdog with two-stage escalation |

---

## Executive Summary

**Total Bugs Found**: 12

| Severity | Count |
|----------|-------|
| Critical | 2 |
| High | 4 |
| Medium | 4 |
| Low | 2 |

---

## Findings (Ordered Critical -> High -> Medium -> Low)

---

### BUG-S4-001
**Title**: Watchdog stage-2 escalation runs broker API calls on background timer thread
**Severity**: Critical
**Location**: `V12_002.Safety.Watchdog.cs` .OnWatchdogTimer (line 87) -> ExecuteWatchdogDirectFallback (lines 221-241)
**Root Cause**: Stage-2 escalation calls `ExecuteWatchdogDirectFallback()` directly from the `System.Threading.Timer` callback (`OnWatchdogTimer`). This method invokes `masterAccount.Cancel(ordersToCancel.ToArray())` (line 267) and `masterAccount.Submit(new[] { closeOrder })` (line 304) on the background timer thread. By contrast, stage-1 correctly marshals via `Enqueue(ctx => ctx.ExecuteWatchdogLeadAccountFlatten())` (line 69). This contradicts the V12.17 REAPER threading fix that moved ALL broker API calls from background threads to the strategy thread via `TriggerCustomEvent`.
**Evidence**: Line 87: `ExecuteWatchdogDirectFallback();` is called directly inside the timer callback with no `Enqueue()` or `TriggerCustomEvent()` wrapper. Compare with line 69 which correctly uses `Enqueue(ctx => ctx.ExecuteWatchdogLeadAccountFlatten());`.
**Test Impact**: Concurrency stress test that triggers watchdog stage-2 escalation while broker callbacks are firing. Would manifest as intermittent `InvalidOperationException` or order corruption.

---

### BUG-S4-002
**Title**: Repair order created before authorization guard check -- orphan order on rejection
**Severity**: Critical
**Location**: `V12_002.REAPER.Repair.cs` .SubmitRepairOrderWithAuthorization (lines 155-225)
**Root Cause**: `targetAcct.CreateOrder(...)` is called at line 196 to create the repair order object, but `MetadataGuardRepairAuthorized(accountName, "ExecuteReaperRepair")` is not checked until line 212. If the guard returns false (e.g., FSM self-healed to Active state between the earlier `hasActiveFsm` check and this point), the method returns without submitting the order. The `Order` object created by `CreateOrder` may have already registered with the NinjaTrader internal order tracking, leaving an orphaned unsent order in memory.
**Evidence**: Line 196: `Order repairEntry = targetAcct.CreateOrder(...)` creates the order. Line 212: `if (!MetadataGuardRepairAuthorized(accountName, "ExecuteReaperRepair")) return;` can abort after creation. The `repairEntry` object is neither submitted nor explicitly cleaned up.
**Test Impact**: Rapid fill/repair race scenario where FSM transitions to Active between the `hasActiveFsm` check (line 187) and the `MetadataGuardRepairAuthorized` check (line 212). Would show orphan order objects in memory and potential duplicate repair submissions on next audit cycle.

---

### BUG-S4-003
**Title**: Naked stop in-flight guard cleared immediately after submission, allowing duplicate emergency stops
**Severity**: High
**Location**: `V12_002.REAPER.NakedStop.cs` .ProcessReaperNakedStopQueue (line 68)
**Root Cause**: After `acct.Submit(new[] { emergencyStop })` succeeds at line 65, the in-flight guard `_reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _)` is called immediately at line 68. The comment says this is intentional ("Clears guard for immediate retry if broker update latches"), but it creates a window where the next REAPER audit cycle (every `ReaperIntervalMs`) can re-detect the naked position and enqueue another emergency stop before the broker confirms the first one. The naked position still exists at this point -- the stop order was just submitted but not yet working.
**Evidence**: Line 68 clears the guard immediately after line 65 submits. The `EnqueueReaperNakedStopCandidate` method in `V12_002.REAPER.Audit.cs` (line 388) checks `_reaperNakedStopInFlight.ContainsKey(expectedKey)` -- if the guard was already cleared, it will pass and enqueue a duplicate. The grace period in `_nakedPositionFirstSeen` does not reset on order submission.
**Test Impact**: Scenario where `ReaperIntervalMs` < broker confirmation latency. Would result in duplicate emergency stop orders submitted for the same account.

---

### BUG-S4-004
**Title**: Unsafety iteration of live `targetAcct.Orders` collection during flatten
**Severity**: High
**Location**: `V12_002.REAPER.Audit.cs` .ProcessReaperFlatten_CancelWorkingOrders (line 666)
**Root Cause**: The method iterates `targetAcct.Orders` directly via `foreach` without taking a `.ToArray()` snapshot. If a broker order update callback fires during iteration (e.g., an order transitions from Working to Filled), the collection may be modified mid-enumeration, throwing `InvalidOperationException`. By contrast, `AuditFleet_CheckWorkingStop` at line 346 correctly uses `acct.Orders.ToArray()` before iterating.
**Evidence**: Line 666: `foreach (Order order in targetAcct.Orders)` -- no `.ToArray()`. Compare with line 346: `var orders = acct.Orders.ToArray();` which is the safe pattern.
**Test Impact**: Flatten execution during active order fills. Would throw `InvalidOperationException` and abort the flatten mid-execution, leaving positions partially closed.

---

### BUG-S4-005
**Title**: Master naked position check iterates `Account.Orders` without snapshot
**Severity**: High
**Location**: `V12_002.REAPER.Audit.cs` .AuditMaster_HandleNakedPosition (line 490)
**Root Cause**: The master account naked-position check uses `Account.Orders.Any(...)` directly without `.ToArray()` snapshot. The `.Any()` LINQ method iterates the underlying collection, and if an order event callback modifies the collection during enumeration, it throws. The fleet version of this check (`AuditFleet_CheckWorkingStop` at line 346) correctly uses `.ToArray()`, but the master version does not.
**Evidence**: Line 490: `bool masterHasWorkingStop = Account.Orders.Any(o => ...)` -- no `.ToArray()`. Compare with line 346: `var orders = acct.Orders.ToArray();` in the fleet equivalent.
**Test Impact**: Master account naked position detection during rapid order events. Intermittent `InvalidOperationException` would suppress naked-position protection.

---

### BUG-S4-006
**Title**: TOCTOU race in `_reaperNakedStopInFlight` check-then-add pattern
**Severity**: High
**Location**: `V12_002.REAPER.Audit.cs` .EnqueueReaperNakedStopCandidate (lines 388-393)
**Root Cause**: The code checks `alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(expectedKey)` at line 388, and if false, calls `_reaperNakedStopInFlight.TryAdd(expectedKey, 0)` at line 391. While `ConcurrentDictionary` operations are individually thread-safe, the check-then-act pattern is not atomic. Two concurrent audit cycles (possible if `TriggerCustomEvent` is still scheduling the previous audit when the timer fires again) could both pass the `ContainsKey` check before either calls `TryAdd`, resulting in duplicate queue entries.
**Evidence**: Lines 388-393: Separate `ContainsKey` + `TryAdd` instead of a single atomic `TryAdd` that checks the result. Compare with `EnqueueReaperFlattenCandidate` (line 334) which correctly uses `if (!_reaperFlattenInFlight.TryAdd(flattenKey, 0)) { return false; }` -- the atomic pattern.
**Test Impact**: High-frequency audit scenario with rapid naked position detection. Would result in duplicate emergency stop queue entries.

---

### BUG-S4-007
**Title**: Repair `entryOrders` write bypasses Actor Enqueue pattern
**Severity**: Medium
**Location**: `V12_002.REAPER.Repair.cs` .SubmitRepairOrderWithAuthorization (line 217)
**Root Cause**: The code writes `entryOrders[repairEntryName] = repairEntry;` directly on the strategy thread (via `TriggerCustomEvent`). However, all other code paths that write to `entryOrders` use the Actor `Enqueue` pattern: `Enqueue(ctx => { ctx.entryOrders[key] = value; })`. While `ConcurrentDictionary` indexer is thread-safe for individual writes, this direct write bypasses the Actor queue's serialization guarantee. If the REAPER strategy-thread write races with a concurrent `Enqueue`-based write to the same key, the final value is non-deterministic.
**Evidence**: REAPER.Repair.cs line 217: `entryOrders[repairEntryName] = repairEntry;` (direct write). Compare with `V12_002.Entries.MOMO.cs` line 162: `Enqueue(ctx => { ctx.entryOrders[_en966] = _eo966; });` (Actor pattern). All 12 entry-point files use the `Enqueue` pattern; only REAPER.Repair.cs and SIMA files write directly.
**Test Impact**: Concurrent repair + entry execution on same account. Non-deterministic `entryOrders` key value depending on which write wins.

---

### BUG-S4-008
**Title**: `_repairBlockedLastLogged` declared but never read or written -- dead code
**Severity**: Medium
**Location**: `V12_002.REAPER.cs` (lines 49-52)
**Root Cause**: The field `private ConcurrentDictionary<string, DateTime> _repairBlockedLastLogged` is declared with a comment explaining its purpose (throttling "Repair BLOCKED" log messages), but no code in the entire codebase reads from or writes to this dictionary. It was likely introduced for a logging throttle feature that was either removed or never implemented.
**Evidence**: Declaration at lines 49-52. Grep for `_repairBlockedLastLogged` returns exactly 1 match (the declaration). Zero usages anywhere in `src/`.
**Test Impact**: None functional, but wastes memory and adds confusion. Clean removal is safe.

---

### BUG-S4-009
**Title**: Redundant FSM state check creates TOCTOU window in repair authorization
**Severity**: Medium
**Location**: `V12_002.REAPER.Repair.cs` .SubmitRepairOrderWithAuthorization (line 187) + `V12_002.MetadataGuard.cs` .MetadataGuardRepairAuthorized (line 140)
**Root Cause**: `SubmitRepairOrderWithAuthorization` checks `hasActiveFsm` at line 187 by scanning `_followerBrackets.Values` for Active/Accepted/Submitted/Replacing states. Then `MetadataGuardRepairAuthorized` (called at line 212) re-checks `_followerBrackets.Values` for Active state at line 140. Between these two reads, the FSM could transition (e.g., from Accepted to Active via a broker callback), causing inconsistent behavior: the first check authorizes (no Active FSM), but the second check suppresses (now Active FSM). The order has already been `CreateOrder`-ed at this point (see BUG-S4-002).
**Evidence**: Repair.cs line 187-195: Checks for Active, Accepted, Submitted, Replacing. MetadataGuard.cs line 140-143: Checks only for Active. The gap between these checks includes `CreateOrder` (line 196), creating a window where broker callbacks can change FSM state.
**Test Impact**: Repair submission during broker order acceptance. FSM transitions from Accepted -> Active between the two checks, causing the repair order to be created but not submitted (orphan).

---

### BUG-S4-010
**Title**: Watchdog `ExecuteWatchdogDirectFallback` lacks `_isTerminating` and `State` guards
**Severity**: Medium
**Location**: `V12_002.Safety.Watchdog.cs` .ExecuteWatchdogDirectFallback (lines 221-241)
**Root Cause**: `ExecuteWatchdogLeadAccountFlatten` (line 190) checks `_isTerminating` and `State != State.Realtime` before proceeding, but `ExecuteWatchdogDirectFallback` (line 221) only checks `masterAccount == null` and `Instrument == null`. If the strategy enters a terminating state between stage-1 and stage-2 escalation, the direct fallback will still attempt broker operations on a shutting-down strategy.
**Evidence**: Line 191: `if (masterAccount == null || Instrument == null || _isTerminating || State != State.Realtime)` vs line 223: `if (masterAccount == null || Instrument == null)` -- missing `_isTerminating` and `State` checks.
**Test Impact**: Strategy shutdown during active watchdog escalation. Could attempt broker operations on a disposed strategy context.

---

### BUG-S4-011
**Title**: `StopReaperAudit` has non-atomic null-check-then-dispose pattern
**Severity**: Low
**Location**: `V12_002.REAPER.cs` .StopReaperAudit (lines 117-130)
**Root Cause**: The method checks `if (_reaperTimer == null)` at line 119, then calls `_reaperTimer.Stop()` at line 124 and `_reaperTimer.Dispose()` at line 126. If two threads call `StopReaperAudit` concurrently, both could pass the null check, and the second thread would attempt to `Stop()`/`Dispose()` an already-disposed timer. While `System.Timers.Timer` is generally tolerant of double-dispose, the `Stop()` call on a disposed timer could throw.
**Evidence**: Lines 119-126: Non-atomic read-check-use pattern. Compare with `StopWatchdog` in `V12_002.Safety.Watchdog.cs` line 27: `System.Threading.Timer timer = Interlocked.Exchange(ref _watchdogTimer, null);` which uses atomic swap to prevent double-dispose.
**Test Impact**: Concurrent calls to StopReaperAudit (e.g., during strategy teardown). Low probability; most callers guard with state checks.

---

### BUG-S4-012
**Title**: Watchdog stage transition uses non-atomic read-then-CAS pattern
**Severity**: Low
**Location**: `V12_002.Safety.Watchdog.cs` .OnWatchdogTimer (lines 61-87)
**Root Cause**: At line 61, `int stage = Volatile.Read(ref _watchdogStage)` reads the stage, then subsequent `Interlocked.CompareExchange` operations use that stale value. If two timer ticks fire close together (possible under GC pressure), the read at line 61 could return 0, the CAS at line 64 could succeed and transition to 1, then a second tick reads the now-1 value at line 61 (after the first tick's CAS) and falls through to the stage-1 escalation at line 84 -- all within a single timer interval. This could accelerate escalation from stage-1 to stage-2 faster than intended.
**Evidence**: Line 61 reads `stage`, then line 64 CAS-es 0->1, and line 84 CAS-es 1->2. A second concurrent timer tick would read the updated value at line 61 and potentially trigger stage-2 escalation immediately rather than waiting for the next timer interval.
**Test Impact**: Timer interval = 2000ms, but under GC pressure two callbacks could overlap. Would cause premature escalation to direct fallback. Low probability due to `AutoReset = true` serializing timer callbacks, but the `System.Threading.Timer` does not guarantee this.

---

## DNA Compliance Check

| Rule | Status | Details |
|------|--------|---------|
| `lock()` statements | **PASS** | Zero matches across all 5 files. |
| Non-ASCII string literals | **PASS** | No curly quotes, emoji, or Unicode in C# string literals. Files have UTF-8 BOM (cosmetic). |
| `Thread.Sleep()` in hot path | **PASS** | Zero matches across all 5 files. |
| `Dictionary<K,V>` writes without atomic guard | **PASS** | All shared dictionaries are `ConcurrentDictionary<K,V>`. The `entryOrders` direct write at REAPER.Repair.cs:217 uses `ConcurrentDictionary` indexer (thread-safe) but bypasses the Actor `Enqueue` pattern (see BUG-S4-007). |

---

## Cross-File Dependency Map

```
V12_002.REAPER.cs (shared state declarations)
  |-- _reaperFlattenQueue, _reaperRepairQueue, _reaperNakedStopQueue
  |-- _repairInFlight, _reaperFlattenInFlight, _reaperNakedStopInFlight
  |-- _nakedPositionFirstSeen, _positionPassFailedFirstSeen
  |-- _accountFillGraceTicks, _reaperOrphanRepairCount
  |-- Timer: _reaperTimer -> OnReaperTimerElapsed -> TriggerCustomEvent -> AuditApexPositions
  |
  +-- V12_002.REAPER.Audit.cs (audit engine)
  |     AuditApexPositions -> AuditSingleFleetAccount / AuditMasterAccountIfNeeded
  |     -> EnqueueReaperRepairCandidate -> _reaperRepairQueue.Enqueue
  |     -> EnqueueReaperFlattenCandidate -> _reaperFlattenQueue.Enqueue
  |     -> EnqueueReaperNakedStopCandidate -> _reaperNakedStopQueue.Enqueue
  |     -> ProcessReaperFlattenQueue (strategy thread via TriggerCustomEvent)
  |
  +-- V12_002.REAPER.Repair.cs (repair engine)
  |     ProcessReaperRepairQueue -> ExecuteReaperRepair
  |     -> ValidateRepairEligibility -> activePositions (read)
  |     -> SubmitRepairOrderWithAuthorization -> entryOrders (write), _followerBrackets (read)
  |     -> _repairInFlight.TryRemove (finally)
  |
  +-- V12_002.REAPER.NakedStop.cs (emergency stop)
  |     ProcessReaperNakedStopQueue -> acct.CreateOrder + acct.Submit
  |     -> _reaperNakedStopInFlight.TryRemove (success + fail)
  |
  +-- V12_002.Safety.Watchdog.cs (independent watchdog)
        OnWatchdogTimer (background thread)
        -> Stage 0->1: Enqueue(ExecuteWatchdogLeadAccountFlatten) [SAFE - Actor queue]
        -> Stage 1->2: ExecuteWatchdogDirectFallback() [UNSAFE - direct broker calls]
```

---

## Threading Model Summary

- **REAPER Audit Timer**: `System.Timers.Timer` fires on thread pool, marshals to strategy thread via `TriggerCustomEvent`. All audit logic runs on strategy thread.
- **REAPER Queues**: `ConcurrentQueue` for flatten/repair/naked-stop requests. Producer = audit (strategy thread via TriggerCustomEvent), Consumer = Process methods (also strategy thread via TriggerCustomEvent).
- **Watchdog Timer**: `System.Threading.Timer` fires on thread pool. Stage-1 escalation uses `Enqueue()` (Actor pattern). Stage-2 escalation runs directly on timer thread (BUG-S4-001).
- **Shared State**: All dictionaries are `ConcurrentDictionary`. In-flight guards use `TryAdd`/`TryRemove` (atomic). Most cross-thread reads use `volatile` or `Interlocked`.
