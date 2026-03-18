# Forensic Diagnosis: V12 Order Lifecycle Failure During 06:43-06:44 on March 16, 2026

## Executive Verdict
- **Proven:** Order `627a44b35ff842499f34b036908dce17` was cancelled only after an explicit client-side cancel sequence entered `CancelPending`. It was not cancelled by a broker rejection, timeout, or later connection loss.
- **Strong inference:** The top-level trigger was most likely `IPC CANCEL_ALL`, not `FLATTEN`, because the pattern matches simultaneous master `CancelOrder()` plus follower `acct.Cancel()` behavior and the code immediately zeroes `expectedPositions` for the fleet.
- **Classification:** This was a strategy/client logic failure. If forced into the binary requested by the case brief, the correct label is **Strategy Bug**.

## Runtime Facts From the Live MES Strategy Instance
These values were decoded from `Strategies.Userdata` for strategy instance `382220972` in workspace `New` inside [NinjaTrader.sqlite](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/db/NinjaTrader.sqlite).

- `EnableSIMA=true`
- `RmaIntelligenceEnabled=false`
- `AccountPrefix=Apex`
- `ConnectionLossHandling=Recalculate`
- `DisconnectDelaySeconds=10`
- `Calculate=OnPriceChange`
- `AutoFlattenDesync=false`
- `RepairTickFence=8`

These runtime facts matter because:
- `SimApexSim_02` qualifies as a fleet account when `AccountPrefix=Apex` because [V12_002.cs#L536](C:/WSGTA/universal-or-strategy/src/V12_002.cs#L536) uses substring matching in `IsFleetAccount()`.
- The RMA proximity sentinel was disabled at runtime, so it could not have been the initiating trigger for the `06:43:38` cancel.

## Silent Cancel Paths In The Requested Files
### [V12_002.Symmetry.Replace.cs](C:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.Replace.cs)
- Silent follower target cancel at [V12_002.Symmetry.Replace.cs#L48](C:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.Replace.cs#L48): `pos.ExecutingAccount.Cancel(new[] { staleTarget });`
- Silent follower target cancel at [V12_002.Symmetry.Replace.cs#L65](C:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.Replace.cs#L65): `pos.ExecutingAccount.Cancel(new[] { oldTarget });`
- Logged follower entry cancel at [V12_002.Symmetry.Replace.cs#L203](C:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.Replace.cs#L203): the cancel is preceded by `[CASCADE] Cancelling follower entry...`, so it is **not** silent.

### [V12_002.Orders.Callbacks.AccountOrders.cs](C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.AccountOrders.cs)
- This file contains **no initiating `acct.Cancel()` path**.
- The entrypoint [V12_002.Orders.Callbacks.AccountOrders.cs#L37](C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.AccountOrders.cs#L37) only reacts after `Cancelled`, `Rejected`, or `Unknown` and marshals the terminal event to the strategy thread at [V12_002.Orders.Callbacks.AccountOrders.cs#L44](C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.AccountOrders.cs#L44) through [V12_002.Orders.Callbacks.AccountOrders.cs#L56](C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.AccountOrders.cs#L56).

## Timeline Reconstruction
### Proven chronology
| Time on March 16, 2026 | Evidence | Meaning |
|---|---|---|
| `06:43:28.726-06:43:28.865` | [log.20260316.00001.txt#L244](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L244), [trace.20260316.00001.txt#L2268](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2268) | Master order `2662904801` changed from `6763.5` to `6763.25`. Follower `f1095e6d202b4b518901b6029671f18e` was cancelled and replaced by `13e9ca5af4eb4e0db8f23350666b8b80`. |
| `06:43:29.566-06:43:29.756` | [log.20260316.00001.txt#L252](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L252), [trace.20260316.00001.txt#L2300](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2300) | Master order `2662904801` changed from `6763.25` to `6763`. Follower `13e9ca5af4eb4e0db8f23350666b8b80` was cancelled and replaced by `627a44b35ff842499f34b036908dce17`. |
| `06:43:38.169` | [trace.20260316.00001.txt#L2334](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2334), [trace.20260316.00001.txt#L2337](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2337) | Both the master order `2662904801` and the follower order `627a44...` entered `CancelPending` at the same timestamp. |
| `06:43:38.171-06:43:38.233` | [trace.20260316.00001.txt#L2340](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2340), [trace.20260316.00001.txt#L2352](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2352), [trace.20260316.00001.txt#L2361](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2361), [log.20260316.00001.txt#L260](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L260), [log.20260316.00001.txt#L262](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L262) | Rithmic reported `Cancel received from client` for the master order and then `Cancelled`. This is direct proof that the cancellation was client-initiated. |
| `06:43:38.173-06:43:38.314` | [trace.20260316.00001.txt#L2342](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2342), [trace.20260316.00001.txt#L2343](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2343), [trace.20260316.00001.txt#L2365](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2365), [log.20260316.00001.txt#L261](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L261), [log.20260316.00001.txt#L263](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L263) | The follower order `627a44...` followed the same cancel path and reached `Cancelled` after entering `CancelSubmitted`. |
| `06:44:05.474-06:44:05.884` | [log.20260316.00001.txt#L268](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L268), [trace.20260316.00001.txt#L2369](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2369) | Disconnect began more than 27 seconds after the cancel event. Connectivity loss was later, not causal. |

## Trigger Analysis
### Proven
- The order was not broker-rejected. Both [log.20260316.00001.txt#L261](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L261) and [log.20260316.00001.txt#L263](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/log/log.20260316.00001.txt#L263) show `Error='No error' Native error=''`.
- The order was not timed out by infrastructure. The trace shows an explicit `Cancel0 -> Cancel1 -> CancelSubmitted -> Cancelled` chain before the terminal state at [trace.20260316.00001.txt#L2337](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2337) through [trace.20260316.00001.txt#L2365](C:/Users/Mohammed%20Khalid/Documents/NinjaTrader%208/trace/trace.20260316.00001.txt#L2365).
- The cancellation was not caused by connection loss. Disconnect began at `06:44:05`, after the order had already been cancelled at `06:43:38`.

### Excluded
- **Broker rejection or timeout:** excluded by the no-error fields and the explicit client cancel sequence.
- **Master-terminal cascade paths:** excluded because [V12_002.Orders.Callbacks.cs#L434](C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs#L434) and [V12_002.Symmetry.Replace.cs#L183](C:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.Replace.cs#L183) run after the master is already terminal, but the follower hit `CancelPending` at the same `06:43:38.169` timestamp as the master.
- **RMA proximity sentinel:** excluded because [V12_002.Entries.RMA.cs#L207](C:/WSGTA/universal-or-strategy/src/V12_002.Entries.RMA.cs#L207) requires `RmaIntelligenceEnabled=true`, while the live MES instance had `RmaIntelligenceEnabled=false`.
- **`Orders.Callbacks.AccountOrders` as initiator:** excluded because that file reacts to terminal callbacks only and does not issue broker cancels.

### Strong inference
- The most likely top-level trigger was `IPC CANCEL_ALL` in [V12_002.UI.IPC.Commands.Fleet.cs#L93](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L93).
- Why `CANCEL_ALL` fits best:
  - It cancels master pending entries via `CancelOrder(order)` at [V12_002.UI.IPC.Commands.Fleet.cs#L117](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L117).
  - It cancels follower pending entries via `acct.Cancel(new[] { order })` at [V12_002.UI.IPC.Commands.Fleet.cs#L144](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L144).
  - It emits no per-order prefix before those cancels. The first user-facing message is the batch summary at [V12_002.UI.IPC.Commands.Fleet.cs#L150](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L150).
  - It immediately zeroes `expectedPositions` for all matching accounts at [V12_002.UI.IPC.Commands.Fleet.cs#L182](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L182) through [V12_002.UI.IPC.Commands.Fleet.cs#L191](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L191), which explains the lack of REAPER recovery.
- `FLATTEN` in the same file is the weaker secondary candidate:
  - `FLATTEN` routes to [V12_002.SIMA.Flatten.cs#L38](C:/WSGTA/universal-or-strategy/src/V12_002.SIMA.Flatten.cs#L38).
  - That path is broader and logs before the batch cancel at [V12_002.UI.IPC.Commands.Fleet.cs#L84](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L84) and [V12_002.SIMA.Flatten.cs#L50](C:/WSGTA/universal-or-strategy/src/V12_002.SIMA.Flatten.cs#L50).
  - Without Output-window or IPC-server print capture, `FLATTEN` cannot be excluded absolutely, but it is a weaker fit than `CANCEL_ALL`.

## Why REAPER Did Not Repair
### Proven
- `CANCEL_ALL` explicitly zeroes `expectedPositions` for the master and every fleet account on the instrument at [V12_002.UI.IPC.Commands.Fleet.cs#L182](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L182) through [V12_002.UI.IPC.Commands.Fleet.cs#L191](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L191).
- `CANCEL_ALL` then cleans unfilled in-memory entries through [V12_002.UI.IPC.Commands.Fleet.cs#L194](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L194) through [V12_002.UI.IPC.Commands.Fleet.cs#L200](C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.Commands.Fleet.cs#L200).
- Even in the normal follower cancel callback path, [V12_002.Orders.Callbacks.AccountOrders.cs#L328](C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.AccountOrders.cs#L328) through [V12_002.Orders.Callbacks.AccountOrders.cs#L342](C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.AccountOrders.cs#L342) rolls expected position back toward zero on confirmed non-FSM cancel, despite printing `Reaper monitoring.`.
- REAPER only queues repair when `actualQty == 0` **and** `expectedQty != 0`, as shown at [V12_002.REAPER.Audit.cs#L71](C:/WSGTA/universal-or-strategy/src/V12_002.REAPER.Audit.cs#L71) through [V12_002.REAPER.Audit.cs#L131](C:/WSGTA/universal-or-strategy/src/V12_002.REAPER.Audit.cs#L131).

### Logical consequence
- Once the strategy zeroed the fleet's expected position state, REAPER had no valid repair predicate left to act on.
- The order was "lost" from the broker, but it was also deliberately de-reserved from strategy state. REAPER therefore saw no follower that was both flat and still expected.

## Logical Proof of Failure
1. The terminal `Cancelled` state for order `627a44...` was preceded by explicit cancel requests, not by rejection or timeout.
2. The master-side Rithmic trace literally says `Cancel received from client`, which proves client-side initiation.
3. The follower reached `CancelPending` before the master was terminal, which excludes post-terminal cascade teardown as the initiating event.
4. The live strategy instance had `RmaIntelligenceEnabled=false`, which excludes the RMA proximity sentinel.
5. The remaining high-fit triggers are strategy-side batch cancel controls. Among them, `IPC CANCEL_ALL` is the best fit because it cancels master and follower entries in one control path and then immediately zeroes fleet `expectedPositions`.
6. REAPER did not repair because the same strategy-side control path removed the state that REAPER requires in order to classify the account as repairable.

## Final Diagnosis
- **Exact trigger of cancellation:** The order `627a44b35ff842499f34b036908dce17` was cancelled by an explicit client-side cancel sequence during the `06:43:38` event window. The most likely top-level trigger was `IPC CANCEL_ALL`.
- **Why it failed:** The strategy cancelled both the master and follower pending entries, then cleared expected fleet state, which prevented REAPER from treating the follower as a repair candidate.
- **Failure class:** Strategy/client logic failure, not infrastructure failure.

## Confidence Labels
- **Proven:** explicit client cancel, no broker rejection, no timeout, no connectivity cause, no `acct.Cancel()` initiator in `Orders.Callbacks.AccountOrders.cs`, REAPER requires non-zero expected state to repair.
- **Strong inference:** `IPC CANCEL_ALL` was the top-level trigger.
- **Excluded:** broker rejection, timeout, unhandled connectivity event, master-terminal cascade initiation, RMA proximity sentinel.
