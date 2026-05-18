# Adversarial Audit v1 -- Build 1109.001 Repair Plan

## Executive Verdict

**Verdict: BLOCKED**

The proposed edits in [docs/brain/implementation_plan.md](../docs/brain/implementation_plan.md) do remove some direct waiting, but the plan is **not architecturally safe enough for P4 execution**. I found multiple failure paths that can leave flatten state stuck, drop live flatten work, or trigger off-thread emergency closes on a healthy strategy.

**Gate Result: GATE 1 CONSENSUS DENIED**

The plan must be revised before implementation. It is not correct to issue `GATE 1 CONSENSUS: READY FOR P4`.

## Scope And Sources

Primary plan under audit:

- [docs/brain/implementation_plan.md](../docs/brain/implementation_plan.md)

Current repo behavior used as ground truth:

- [src/V12_002.SIMA.Dispatch.cs](../src/V12_002.SIMA.Dispatch.cs)
- [src/V12_002.SIMA.Lifecycle.cs](../src/V12_002.SIMA.Lifecycle.cs)
- [src/V12_002.SIMA.Flatten.cs](../src/V12_002.SIMA.Flatten.cs)
- [src/V12_002.SIMA.Fleet.cs](../src/V12_002.SIMA.Fleet.cs)
- [src/V12_002.cs](../src/V12_002.cs)
- [src/V12_002.BarUpdate.cs](../src/V12_002.BarUpdate.cs)
- [src/V12_002.Safety.Watchdog.cs](../src/V12_002.Safety.Watchdog.cs)
- [src/V12_002.Orders.Callbacks.AccountOrders.cs](../src/V12_002.Orders.Callbacks.AccountOrders.cs)
- [src/V12_002.UI.Compliance.cs](../src/V12_002.UI.Compliance.cs)

## Logical Proofs Of Failure

### 1. Phase 3 can wedge the strategy in a permanent flatten state if `TriggerCustomEvent` fails

**Claim**

The proposed chunked flatten queue is not self-healing. If scheduling the pump fails after enqueue, `_pendingFlattenOps` remains populated and `isFlattenRunning` remains `true` with no guaranteed recovery path.

**Proof trace**

- The plan sets `isFlattenRunning = true` before any work is scheduled at plan lines 227 and 412.
- The initial pump handoff is best-effort only: plan lines 260-267 and 443-449 use `try { TriggerCustomEvent(...) } catch { }`.
- The chain handoff is also best-effort only: plan lines 389-396 use the same swallow-and-continue pattern.
- There is no `_flattenPumpScheduled` guard, no retry counter, no queue drain on scheduling failure, and no fallback path that clears `isFlattenRunning`.

**Failure sequence**

1. `FlattenAllApexAccounts()` or `ClosePositionsOnlyApexAccounts()` enqueues 1..N `FlattenWorkItem`s.
2. `isFlattenRunning` becomes `true`.
3. `TriggerCustomEvent` throws once during kickoff or chain scheduling.
4. The queue remains non-empty, but no future `PumpFlattenOps()` invocation is guaranteed.
5. `isFlattenRunning` remains `true`, which suppresses entry and callback pumps across the strategy.

**Impact**

- Manual flatten request can stall in a half-started state.
- [src/V12_002.Orders.Callbacks.AccountOrders.cs](../src/V12_002.Orders.Callbacks.AccountOrders.cs) lines 155-178 and [src/V12_002.UI.Compliance.cs](../src/V12_002.UI.Compliance.cs) lines 294-324 buffer broker callbacks while `isFlattenRunning` is true, so a wedged flag can starve recovery processing.
- This is a hard blocker.

### 2. Phase 3 has no single-flight guard, so overlapping flatten batches can interleave

**Claim**

The plan introduces a shared `_pendingFlattenOps` queue but does not serialize flatten requests by batch or generation.

**Proof trace**

- The plan adds one global queue and one global `FlattenWorkItem` type at lines 197-208.
- No request id, no generation token, no `_flattenPumpScheduled`, and no "reject if already running" gate are present in the proposed code.
- Existing code already has nested flatten entry points and flatten suppression centered on `isFlattenRunning`; see [src/V12_002.SIMA.Flatten.cs](../src/V12_002.SIMA.Flatten.cs) lines 38-179 and 255-390 plus [src/V12_002.Orders.Management.Flatten.cs](../src/V12_002.Orders.Management.Flatten.cs) lines 154-215.

**Failure sequence**

1. A `FlattenAll` batch is enqueued.
2. Before the batch drains, a `ClosePositionsOnly` request also enqueues into the same global queue.
3. `PumpFlattenOps()` processes a mixed stream of work items with different semantics.
4. Duplicate cancel/close submissions become possible against the same account and instrument.

**Impact**

- Mixed flatten semantics are not prevented.
- Duplicate market-close submissions become possible.
- The plan does not meet the repo's existing flatten-scope discipline.

### 3. The `ClosePositionsOnly` refactor is internally inconsistent and cancels more than advertised

**Claim**

The plan text says "`CancelOnly=true` triggers zombie sweep but still submits market close", but the proposed enqueue code sets `CancelOnly=false`. Combined with the cancel condition in `PumpFlattenOps`, the code cancels all working orders for `ClosePositionsOnly`, not just zombie targets.

**Proof trace**

- The `FlattenWorkItem` comment says `CancelOnly` means zombie sweep only at plan line 205.
- The `ClosePositionsOnly` enqueue comment says `CancelOnly=true` at plan line 420.
- The actual code enqueues `CancelOnly = false` at plan lines 421-425 and 433-437.
- The cancel branch runs when `!item.CancelOnly || item.Source.Contains("ClosePositions")` at plan lines 294-296.

**Logical consequence**

For `Source = "ClosePositionsOnly"`:

- `item.Source.Contains("ClosePositions") == true`
- therefore the cancel branch runs even if `CancelOnly == false`
- because `isZombieSweep = item.CancelOnly`, the inner zombie filter is disabled
- all non-terminal working orders for the instrument are cancelled

**Impact**

- The proposed behavior does not match the stated design.
- Protection-preserving "close positions only" semantics are not reliably preserved.

### 4. Chunking reduces batch size but does not prove "freeze-proof"

**Claim**

The plan still executes synchronous broker calls on the strategy thread inside each pump cycle, so the repair does not eliminate UI freeze risk. It only bounds multi-account stalls.

**Proof trace**

- The proposed `PumpFlattenOps()` performs `acct.Cancel(...)` at plan lines 328-335 and `acct.Submit(...)` at lines 368-370 on the strategy thread.
- The plan explicitly routes the pump through `TriggerCustomEvent`, so the work still runs on the strategy thread.
- Existing live code already shows the same risk pattern in [src/V12_002.SIMA.Flatten.cs](../src/V12_002.SIMA.Flatten.cs) lines 63-95 and 283-321.

**Failure sequence**

1. One account has a slow `Cancel` or `Submit` call due to broker/network latency.
2. `PumpFlattenOps()` is on the strategy thread.
3. The strategy thread still blocks for the full duration of that broker call.

**Impact**

- The plan addresses N-account freeze amplification.
- It does **not** justify the claim that "absolutely nothing else" in the edits can freeze the UI.
- Single-call strategy-thread stalls remain possible.

### 5. Broker disconnect mid-pump causes silent work loss

**Claim**

If cancel or submit throws after dequeue, the account item is discarded with logging only. There is no retry, requeue, or terminal abort path.

**Proof trace**

- `PumpFlattenOps()` dequeues first at plan lines 277-283.
- Any later exception is swallowed by the outer `catch` at lines 382-385.
- The `finally` block advances to the next item or completes at lines 387-396.

**Failure sequence**

1. The queue dequeues account `A`.
2. Broker disconnect occurs during `acct.Cancel` or `acct.Submit`.
3. The outer catch logs `[FLATTEN_PUMP] ERROR`.
4. Account `A` is not requeued and is not marked failed in any durable state.
5. The pump continues and may print a normal completion banner.

**Impact**

- Flatten can report completion with live positions still open.
- This is incompatible with a safety-critical flatten path.

### 6. Phase 3 ignores the repo's current flatten-scope contract

**Claim**

The plan writes raw `isFlattenRunning` directly instead of using the existing `EnterFlattenScope()` / `ExitFlattenScope()` pair.

**Proof trace**

- Current repo flatten-scope contract is in [src/V12_002.cs](../src/V12_002.cs) lines 525-540.
- `ExitFlattenScope()` resumes buffered order and execution callback pumps at lines 537-540 and 678-688.
- The proposal toggles bare `isFlattenRunning` at plan lines 227, 265, 280, 394, 412, and 447 without scope depth accounting.

**Failure sequence**

1. Watchdog or another flatten path enters flatten scope using the current repo contract.
2. Proposed chunked flatten also sets `isFlattenRunning = true` directly.
3. Proposed pump reaches its local completion branch and sets `isFlattenRunning = false`.
4. The outer flatten scope still exists, but the shared flag is now cleared early.

**Impact**

- REAPER/callback suppression can be released too early.
- Buffered account callback pumps may not be resumed through the intended central path.
- This is a design regression against the current repo.

### 7. Phase 1 removes blocking waits but leaves a toggle-intent race unresolved

**Claim**

The `Wait(0)` conversion removes direct waiting, but the deferred retry design still has a correctness gap for lifecycle toggles.

**Proof trace**

- Plan lines 95-102 defer `ProcessApplySimaState(_defEnabled)` via `TriggerCustomEvent`.
- Current repo only stores `_simaTogglePending` as a bool at [src/V12_002.cs](../src/V12_002.cs) line 512 and [src/V12_002.SIMA.Lifecycle.cs](../src/V12_002.SIMA.Lifecycle.cs) lines 40-57.
- There is no durable "pending desired state" field in either the plan or current code.

**Failure sequence**

1. Toggle request `ON` arrives while semaphore is held.
2. Plan sets `_simaTogglePending = true` and tries to defer.
3. Before the deferred callback runs, another caller requests `OFF`.
4. The plan has no durable arbitration state beyond a boolean pending flag.

**Impact**

- The plan removes the 500ms wait stall.
- It does **not** prove correct toggle ordering under contention.
- This is not the worst blocker in the set, but it is not "solid" as written.

### 8. Phase 4 would false-trigger on a healthy strategy during quiet periods

**Claim**

The proposed heartbeat source is too sparse for a "stall means dead thread" conclusion.

**Proof trace**

- The plan updates heartbeat from `DrainActor()` and `OnBarUpdate` only at plan lines 619-620 and 631.
- Current repo heartbeat is likewise touched in [src/V12_002.cs](../src/V12_002.cs) line 434 and [src/V12_002.BarUpdate.cs](../src/V12_002.BarUpdate.cs) line 44.
- `OnBarUpdate` runs under `Calculate.OnPriceChange`, not on a fixed timer.

**Failure sequence**

1. Strategy is in realtime with an open position.
2. Market is quiet or feed is sparse for >10s.
3. No bar update and no actor drain occurs during that interval.
4. Proposed watchdog interprets stale heartbeat as strategy-thread death.
5. The watchdog scans and can submit emergency closes even though the strategy is healthy.

**Impact**

- False-positive emergency flatten is possible.
- A heartbeat tied only to market activity and actor drains is not enough to support a 10s dead-thread detector.

### 9. Phase 4 stop detection can flatten a protected position

**Claim**

The proposed stop scan is both thread-unsafe and semantically too narrow.

**Proof trace**

- Plan lines 563-567 call `acct.Orders.Any(...)` directly on the watchdog thread.
- The plan catches all exceptions and leaves `hasStop` at `false` at lines 560-569.
- The plan treats only `Working` and `Accepted` stop orders as protection at lines 566-567.
- Current live watchdog correctly treats `Submitted`, `ChangePending`, and `ChangeSubmitted` as live order states in [src/V12_002.Safety.Watchdog.cs](../src/V12_002.Safety.Watchdog.cs) lines 142-146 and 208-212.

**Failure sequence A: collection mutation**

1. Broker mutates `acct.Orders` while the watchdog thread is enumerating it.
2. Enumeration throws.
3. The catch block suppresses the exception and keeps `hasStop = false`.
4. The watchdog interprets the position as naked and submits a market close.

**Failure sequence B: live replace window**

1. A stop is live but currently in `Submitted`, `ChangePending`, or `ChangeSubmitted`.
2. Proposed scan excludes that state.
3. The position is misclassified as naked.
4. The watchdog submits a redundant or conflicting emergency flatten.

**Impact**

- The plan can flatten a protected position.
- This is a direct safety regression relative to the current staged watchdog.

### 10. `_watchdogFlattenFired` is a sticky one-shot and can suppress later real emergencies

**Claim**

The proposed watchdog does not reset its fire gate on recovery or on a no-op scan path.

**Proof trace**

- The flag is set once by `Interlocked.CompareExchange` at plan lines 535-540.
- It is reset only in `StartWatchdog()` and the outer critical-error catch at lines 497 and 598-601.
- There is no reset on successful scan, no reset on recovery heartbeat, and no reset when no submit occurs.

**Failure sequence**

1. Watchdog sees one apparent stall and sets `_watchdogFlattenFired = 1`.
2. Scan finds no actionable close, or every submit fails at the per-account level.
3. Strategy later recovers and then experiences a real second stall.
4. The second event is suppressed as "already fired".

**Impact**

- The proposed watchdog is not repeatable across multiple incidents in one session.
- That is unacceptable for a safety net.

### 11. Phase 4 regresses the current watchdog architecture instead of improving it

**Claim**

The repo already contains a staged watchdog. The plan is stale and would replace a safer design with a riskier one.

**Proof trace**

- Current repo watchdog starts a threadpool timer at [src/V12_002.Safety.Watchdog.cs](../src/V12_002.Safety.Watchdog.cs) lines 16-23.
- It first enqueues a strategy-thread flatten at lines 64-76 and only escalates to direct broker calls at lines 84-88.
- Strategy-thread flatten uses `EnterFlattenScope()` and live-order snapshots at lines 130-188.
- Direct fallback is a secondary escalation only at lines 191-262.
- The plan instead makes direct off-thread broker scanning and `Account.Submit` the primary response at plan lines 542-603.

**Impact**

- The plan's premise "there is zero independent mechanism" is factually outdated for this repo.
- Phase 4 as written is a regression, not a net improvement.

## Non-Blocking Observations

- The `Account.All.ToArray()` snapshot for unsubscribe in Phase 2 is reasonable and does address the enumerator-mutation crash in [src/V12_002.SIMA.Fleet.cs](../src/V12_002.SIMA.Fleet.cs) lines 255-279.
- The `Wait(0)` conversion in Phase 1 does remove the direct 200ms/500ms strategy-thread stalls from [src/V12_002.SIMA.Dispatch.cs](../src/V12_002.SIMA.Dispatch.cs) lines 47-52 and [src/V12_002.SIMA.Lifecycle.cs](../src/V12_002.SIMA.Lifecycle.cs) lines 48-58.
- I did not find an additional independent UI-freeze vector in Phases 6-8 that is as severe as the blockers above. The gating failures are concentrated in Phases 3 and 4, with a correctness gap remaining in Phase 1.

## Required Revisions Before P4

1. Replace the proposed flatten pump with a **single-flight** design:
   - explicit pump-scheduled flag
   - batch/generation id
   - kickoff/chain scheduling failure rollback
   - no mixed `FlattenAll` and `ClosePositionsOnly` work in one global queue

2. Integrate flatten orchestration with the existing flatten-scope contract:
   - use `EnterFlattenScope()` / `ExitFlattenScope()`
   - never toggle raw `isFlattenRunning` directly in the new code

3. Define disconnect semantics for chunked flatten:
   - requeue with bounded retry, or
   - abort batch with explicit failed-account reporting
   - never log success after dropped work

4. Revise Phase 4 to preserve the current staged watchdog model:
   - threadpool timer may detect
   - strategy-thread flatten should remain primary
   - direct off-thread broker calls must stay fallback only

5. Strengthen watchdog truth sources:
   - snapshot broker collections before enumerating
   - treat in-flight stop states as protective
   - reset the watchdog stage after recovery
   - do not infer dead-thread state solely from sparse market-driven heartbeats

## Final Gate

**Result: BLOCKED**

This plan is not ready for implementation. The correct handoff status is:

`GATE 1 CONSENSUS: BLOCKED -- REVISE PHASES 3 AND 4 BEFORE P4`
