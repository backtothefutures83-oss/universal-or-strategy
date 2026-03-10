# Live Freeze Forensic Brief

Build: `960`  
Authority: current source under `src/V12_002.*.cs`  
Artifacts reviewed: `trace_target.txt`, `trace_tail2.txt`, `trace_tail3.txt`, `bug_tail.txt`

## Executive Answer

**Primary hypothesis:** the freeze most likely occurred in the master target-fill path when the strategy thread held `stateLock` and synchronously called `CancelOrder(currentStop)` from [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L404).  
**Secondary hypothesis:** a separate credible stall exists in `OnPositionUpdate()`, where the strategy thread synchronously writes and flushes IPC socket traffic immediately after the position reduction, via [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L839) and [`src/V12_002.UI.IPC.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.cs#L1665).

**Direct evidence:** the V12 guards do prevent double-processing of fills through `ApplyTargetFill()` first-writer-wins behavior and `OnExecutionUpdate` execution-ID dedup in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L42) and [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L971).  
**Direct evidence:** those guards do not cover lock ordering, synchronous broker API calls while `stateLock` is held, or synchronous IPC `Write()` / `Flush()` calls in `OnPositionUpdate()`.  
**Inference:** SIMA can still contribute to the freeze indirectly by keeping fleet/repair state active around the incident window, but the current code does not prove a direct follower callback deadlock on `ctx.Sync` plus `stateLock`.

## Ranked Hypotheses

### 1. Primary: `stateLock` held across stop cancel during master target-fill

**Direct evidence:** the broker trace shows the master target order `T1_RMA_639086355153855264` reaches filled state before the execution callback:

- `06:54:07.864` `Cbi.Account.OrderUpdateCallback` for `T1_RMA_639086355153855264` becomes `Filled`.
- `06:54:07.865` `Cbi.Account.ExecutionUpdateCallback` fires for the same order.
- `06:54:07.868` `Cbi.Account.Cancel0` appears for `Stop_RMA_639086355153855264`.

This ordering is visible in `trace_target.txt` and `trace_tail2.txt`.

**Direct evidence:** because `OnOrderUpdate` fires first in the trace, the first live strategy path is:

`OnOrderUpdate()` -> `HandleSecondaryOrderFilled()` -> `ApplyTargetFill()` -> `UpdateStopQuantity()`

That path is implemented in:

- [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L158)
- [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L253)
- [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L42)
- [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L404)

**Direct evidence:** `HandleSecondaryOrderFilled()` calls `ApplyTargetFill()` and then immediately calls `UpdateStopQuantity()` when a target order fills in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L268).

**Direct evidence:** `UpdateStopQuantity()` acquires `stateLock` and remains inside that lock while it stages the pending stop replacement and calls `CancelOrder(currentStop)` at [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L408) and [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L453).

**Direct evidence:** the broker trace’s `Cancel0` event at `06:54:07.868` is the exact external signature expected from that `CancelOrder(currentStop)` call. No other nearby master target-fill path in the code emits a stop cancel this directly.

**Direct evidence:** the parallel `OnExecutionUpdate()` target branch exists in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L1104), but it is a duplicate/guarded path. It re-enters `ApplyTargetFill()` and returns early if the target was already processed, which is consistent with `OnOrderUpdate` having already done the real decrement and stop-reduce work.

**Direct evidence:** no replacement stop submission is visible in the broker traces after the `06:54:07.868` cancel request and before user intervention. The traces show the cancel request, the reduced master position at `06:54:07.888`, and later forced cancellation activity, but no subsequent stop-create or stop-submit event for the replacement stop.

**Inference:** the most likely freeze point is therefore the `CancelOrder(currentStop)` call while `stateLock` is held, not later code after broker confirmation.

**Inference:** the theoretical deadly embrace is:

- Strategy callback thread: `stateLock` -> `CancelOrder()` -> NinjaTrader internal order/account lock.
- Competing thread: background REAPER or another NT account/broker callback observes broker/account state first, then later needs `stateLock`.

**Direct evidence:** REAPER reads broker account state before taking `stateLock` in [`src/V12_002.REAPER.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.REAPER.cs#L209). It reads `acct.Positions` first, then takes `stateLock` at [`src/V12_002.REAPER.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.REAPER.cs#L220).

**Inference:** because NinjaTrader’s internal account/order locks are opaque to this repository, the inner NT8 lock cannot be source-proven here. The lock is inferred from the synchronous broker callback timing, the `Cancel0` transition, and the fact that the platform froze without throwing.

### 2. Secondary: synchronous IPC write from `OnPositionUpdate()`

**Direct evidence:** the broker trace shows `Rithmic.Adapter.OnPositionUpdate` for the master account at `06:54:07.888`, immediately followed by `AccountItemUpdateCallback` activity in `trace_target.txt` and `trace_tail2.txt`.

**Direct evidence:** `OnPositionUpdate()` always calls `BroadcastSyncTargetState()` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L839).

**Direct evidence:** `BroadcastSyncTargetState()` always calls `SendResponseToRemote($"SYNC_TARGET_STATE|{syncCount}")` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L948).

**Direct evidence:** `SendResponseToRemote()` performs synchronous `TcpClient.GetStream().Write()` and `Flush()` calls in [`src/V12_002.UI.IPC.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.cs#L1684) and [`src/V12_002.UI.IPC.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.cs#L1685).

**Inference:** if a connected remote panel client stopped draining its socket or blocked on its end, this path could freeze the strategy/UI thread without an exception and without any additional broker trace activity.

**Inference:** this matches the incident timing well because the observed silence begins right after the broker position-update window at `06:54:07.888`, which is exactly when `OnPositionUpdate()` would have run.

## Incident Timeline

**Direct evidence:** the master entry had already filled and the strategy submitted `T1_RMA_639086355153855264` before the freeze window. This is visible in `trace_tail2.txt`, `trace_tail3.txt`, and `bug_tail.txt`.

**Direct evidence:** at `06:53:58`, follower account `SimApexSim_02` submitted and filled an `EF_*` market order, ending flat. The `EF_` prefix maps to `FlattenPositionByName()` in [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L1061), where follower emergency flatten orders are created as `EF_ + entryName` in [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L1110).

**Direct evidence:** at `06:54:07.861-865`, the master `T1_RMA_639086355153855264` fill arrives, first as an order filled transition, then as an execution callback.

**Direct evidence:** at `06:54:07.868`, the master stop `Stop_RMA_639086355153855264` enters `CancelPending`.

**Direct evidence:** at `06:54:07.888`, broker position and account-item callbacks still occur, including a reduced master quantity from 4 to 2.

**Direct evidence:** no replacement stop submission is visible after that cancel request and before the later user-driven cancellation window around `06:54:26`.

## Master Execution Path Reconstruction

### First path actually taken

**Direct evidence:** the first strategy path reached by the `T1` fill is `OnOrderUpdate()`, not `OnExecutionUpdate()`, because the broker trace shows order-filled state before execution callback.

**Direct evidence:** the code flow is:

1. `OnOrderUpdate()` dispatches filled non-entry orders to `HandleSecondaryOrderFilled()` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L172).
2. `HandleSecondaryOrderFilled()` matches `T1` through the target dictionary in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L257).
3. It calls `ApplyTargetFill()` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L268).
4. `ApplyTargetFill()` decrements `RemainingContracts` under `stateLock` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L55).
5. Control returns to `HandleSecondaryOrderFilled()`, which calls `UpdateStopQuantity()` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L270).
6. `UpdateStopQuantity()` enters `stateLock`, creates a pending replacement record, and calls `CancelOrder(currentStop)` before releasing the lock in [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L408) and [`src/V12_002.Orders.Management.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.cs#L453).

### Parallel guarded path

**Direct evidence:** `OnExecutionUpdate()` later sees the same `T1` fill in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L971).

**Direct evidence:** that path first applies execution-ID dedup under `stateLock` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L985).

**Direct evidence:** it then re-enters the target branch in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L1104) and calls `ApplyTargetFill()` again in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L1118).

**Direct evidence:** if the target was already completed by the earlier `OnOrderUpdate()` path, `alreadyProcessed` becomes true and the method returns before another stop-reduction call in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L1119).

**Direct evidence:** this is why the V12 guards address duplicate accounting but do not remove the original blocking risk from the first `OnOrderUpdate()` path.

## SIMA / Follower Assessment

**Direct evidence:** follower execution callbacks are marshalled to the strategy thread in `OnAccountExecutionUpdate()` and `ProcessAccountExecutionQueue()` in [`src/V12_002.UI.Compliance.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.UI.Compliance.cs#L420) and [`src/V12_002.UI.Compliance.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.UI.Compliance.cs#L451).

**Direct evidence:** follower order terminal callbacks are also marshalled to the strategy thread in `OnAccountOrderUpdate()` and `ProcessAccountOrderQueue()` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L484) and [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L510).

**Direct evidence:** because of that marshalling, the current code does not prove a direct follower callback deadlock on `ctx.Sync` plus `stateLock`.

**Direct evidence:** the earlier `EF_*` follower flatten proves SIMA/repair state was already active just before the master `T1` fill.

**Direct evidence:** [`src/V12_002.Symmetry.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.cs#L718) contains a lock-order smell in `SymmetryGuardPruneDispatches()`, where `ctx.Sync` is held and `stateLock` is then taken in [`src/V12_002.Symmetry.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.cs#L736) and [`src/V12_002.Symmetry.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.cs#L745).

**Inference:** that symmetry lock ordering is a background structural risk, but it does not fit the incident timeline as tightly as the master stop-cancel path or the immediate `OnPositionUpdate()` IPC write.

## Interfaces and Blocking Boundaries

**Direct evidence:** the master callback surface for this incident is `OnOrderUpdate()`, `OnExecutionUpdate()`, and `OnPositionUpdate()` in [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L158).

**Direct evidence:** the SIMA marshalled callback surface is `OnAccountExecutionUpdate()`, `ProcessAccountExecutionQueue()`, `OnAccountOrderUpdate()`, and `ProcessAccountOrderQueue()` in [`src/V12_002.UI.Compliance.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.UI.Compliance.cs#L420) and [`src/V12_002.Orders.Callbacks.cs`](/c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L484).

**Direct evidence:** the external blocking boundaries in play are NinjaTrader broker/account APIs such as `CancelOrder()`, `Account.Cancel()`, and `Account.Submit()`, plus remote IPC socket writes in `SendResponseToRemote()`.

## Conclusion

**Direct evidence:** the V12 guard set protects fill deduplication and state consistency around target accounting.  
**Direct evidence:** the V12 guard set does not protect against synchronous broker/API work occurring while `stateLock` is held, and it does not protect the `OnPositionUpdate()` IPC `Write()` / `Flush()` path.

**Inference:** the most likely failure mechanism is a lock-based freeze in the master target-fill stop-reduction path, centered on `UpdateStopQuantity()` holding `stateLock` across `CancelOrder(currentStop)`.  
**Inference:** the strongest competing explanation is a synchronous IPC stall during `OnPositionUpdate()` immediately after the broker reduced the master position to 2 contracts.  
**Inference:** SIMA was operationally involved in the session state, but the current code does not prove that follower callback matching itself created the deadlock.
