# Codex Architecture, Security, and Quality Audit

## Executive Summary

- Baseline audited: `main` at commit `5763f197395d78ae04b56c780fa6a11fd92b3fa7` (`HEAD == origin/main`).
- Scope: static forensic audit of `src/` against `GEMINI.md`, with emphasis on lock discipline, follower replace protocol, cleanup correctness, and live-thread execution.
- Build/test note: this repo does not contain a `.sln` or `.csproj`; this is a code-trace audit, not a compile-verified review.
- Overall stability score: **3/10** for live multi-account trading reliability.

The codebase contains many hardening comments and several correct patterns, but the implementation does not consistently honor its own zero-trust contract. The most serious issues are: shared state is still mutated outside `lock(stateLock)`, follower order replacement is only partially migrated to the Build 947 FSM, and queued work can outlive SIMA lifecycle shutdown and execute after supervision has been dismantled.

## Prioritized Findings

| ID | Severity | Area | Class | File:Line | Evidence | Failure mode | Hardening direction |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F1 | Critical | Area 1 | protocol drift | `src/V12_002.Entries.FFMA.cs:172`, `src/V12_002.Entries.MOMO.cs:146`, `src/V12_002.Entries.OR.cs:206`, `src/V12_002.Entries.RMA.cs:93`, `src/V12_002.Entries.Trend.cs:207`, `src/V12_002.Orders.Callbacks.cs:961`, `src/V12_002.SIMA.cs:661`, `src/V12_002.Trailing.cs:637` | Confirmed: `activePositions`, `entryOrders`, `stopOrders`, and target dictionaries are still written and removed outside `lock(stateLock)`. | REAPER, callbacks, and UI/IPC flows can observe half-committed state across multiple dictionaries. | Move all shared-state mutations and multi-dictionary teardown under one lock protocol. |
| F2 | Critical | Area 1 | protocol drift | `src/V12_002.Orders.Management.cs:832-843` | Confirmed: follower CIT still does raw `Cancel()` -> `CreateOrder()` -> `Submit()` and then swaps `entryOrders`. | Direct ghost-order window on follower entry replacement; violates `GEMINI.md` Build 947 rule. | Route CIT follower nudges through the same two-phase replace FSM as entry move-sync. |
| F3 | Critical | Area 3 | systemic architecture flaw | `src/V12_002.SIMA.cs:503-521`, `src/V12_002.SIMA.cs:551-559`, `src/V12_002.SIMA.cs:638-676`, `src/V12_002.SIMA.cs:815-822` | Confirmed: `_pendingFleetDispatches` survives SIMA disable; `PumpFleetDispatch()` has no `EnableSIMA`, `State`, or flatten guard. | Queued follower orders can be submitted after `SET_SIMA|OFF` or termination, after handlers and REAPER are stopped. | Drain or cancel pending dispatches on lifecycle shutdown and gate the pump before submit. |
| F4 | High | Area 1 | protocol drift | `src/V12_002.Orders.Callbacks.cs:1334-1349`, `src/V12_002.Symmetry.cs:555-577`, `src/V12_002.Trailing.cs:617-637`, `src/V12_002.Trailing.cs:979-990` | Confirmed: several follower stop/target move paths still cancel and resubmit directly. | Build 947 FSM is not the repo-wide truth; follower order behavior differs by call site. | Either broaden FSM coverage to all follower replace classes or document separate safe FSMs per order type. |
| F5 | High | Area 3 | race/backpressure | `src/V12_002.REAPER.cs:253-289`, `src/V12_002.REAPER.cs:313-320`, `src/V12_002.REAPER.cs:429-523`, `src/V12_002.REAPER.cs:635-636` | Confirmed: REAPER enqueues repairs before `_repairInFlight` is set and enqueues flattens with no in-flight dedupe at all. | Strategy-thread stalls can accumulate duplicate repair or flatten waves for the same account. | Set in-flight state at enqueue time and dedupe flatten queue entries. |
| F6 | High | Area 2 | null/cleanup defect | `src/V12_002.Entries.MOMO.cs:146-163`, `src/V12_002.Entries.FFMA.cs:172-184`, `src/V12_002.Entries.RMA.cs:400-415` | Confirmed: several entry paths pre-register state, then continue after `SubmitOrderUnmanaged()` returns `null`; MOMO still writes `entryOrders[entryName] = entryOrder` after a null rollback. | `ConcurrentDictionary` null writes can throw, leaving orphaned `activePositions` with no live order. | Abort and fully roll back all staged state on null submit before any dictionary assignment. |
| F7 | High | Area 2 | null/cleanup defect | `src/V12_002.Entries.FFMA.cs:172-184`, `src/V12_002.Entries.FFMA.cs:307-313`, `src/V12_002.Entries.FFMA.cs:446-452`, `src/V12_002.REAPER.cs:310-320`, `src/V12_002.REAPER.cs:398-405` | Confirmed: FFMA entry paths do not reserve `expectedPositions` before submit. | Legitimate FFMA fills can appear to REAPER as `actual != 0 && expected == 0`, which is treated as critical desync. | Reserve master expected state consistently across all entry modes. |
| F8 | High | Area 2 | null/cleanup defect | `src/V12_002.Orders.Callbacks.cs:96-128`, `src/V12_002.Orders.Callbacks.cs:1020-1029`, `src/V12_002.Orders.Callbacks.cs:1070-1079`, `src/V12_002.Orders.Callbacks.cs:784-793` | Confirmed: final-target and trim-close paths drop `activePositions` immediately after requesting lifecycle-safe stop cancel. | If stop cancel is delayed or rejected, later callbacks lose `PositionInfo` context and cleanup degrades to ghost handling. | Keep metadata until broker-confirmed terminal cleanup completes. |
| F9 | High | Area 3 | race/backpressure | `src/V12_002.cs:124`, `src/V12_002.cs:127`, `src/V12_002.UI.Compliance.cs:426-477`, `src/V12_002.Orders.Callbacks.cs:463-499` | Confirmed: account execution and account order queues are unbounded and reschedule themselves aggressively during flatten/backlog. | Large stale-event backlogs can replay after the live position state has moved on. | Bound the queues, coalesce wakeups, and stop re-enqueue churn during flatten. |
| F10 | Medium-High | Area 2 | race/backpressure | `src/V12_002.Orders.Management.cs:401-450`, `src/V12_002.Trailing.cs:501-568`, `src/V12_002.Orders.Callbacks.cs:368-383`, `src/V12_002.Orders.Callbacks.cs:599-611` | Confirmed: `pendingStopReplacements` values are mutated in place and read on other paths without immutable snapshots. | Mixed stop quantity/price/target snapshots can generate stale or malformed replacement stops under bursty ticks. | Make pending replacement records immutable per generation or guard all reads/writes under the same lock. |
| F11 | Medium-High | Area 2 | race/backpressure | `src/V12_002.Orders.Management.cs:407-414`, `src/V12_002.Orders.Management.cs:421-450` | Confirmed: `UpdateStopQuantity()` uses `ContainsKey()` and then indexes `stopOrders[entryName]` while other paths remove the same key. | TOCTOU can throw inside the stop-resize path and leave the position temporarily unprotected. | Use one `TryGetValue()` snapshot and keep the stop lifecycle entirely atomic. |
| F12 | Medium | Area 3 | systemic architecture flaw | `src/V12_002.REAPER.cs:211-214`, `src/V12_002.REAPER.cs:330-334`, `src/V12_002.REAPER.cs:456-474`, `src/V12_002.Orders.Management.cs:1451-1456` | Confirmed: REAPER and orphan sweeps dereference broker objects without full null guards and read live broker collections off-thread. | A partially adopted/manual order can abort scans; false desync and false naked-position decisions remain plausible. | Snapshot broker collections on the strategy thread and null-guard `Instrument` and `Name` before access. |

## Area 1 - Blueprint and Zero-Trust

`GEMINI.md` requires two things that matter most here:

1. All state mutations for `activePositions` and `expectedPositions` must be guarded by `lock(stateLock)`.
2. Any follower cancel-and-resubmit flow must use the Build 947 two-phase replace FSM.

### Mutation Inventory

| Shared state | Compliant examples | Non-compliant examples | Verdict |
| --- | --- | --- | --- |
| `expectedPositions` | `src/V12_002.SIMA.cs:79-140`, `src/V12_002.REAPER.cs:497-500` | Reads still occur outside the lock in some paths, for example `src/V12_002.UI.Compliance.cs:540-555` | Partial compliance on writes, incomplete on full state discipline |
| `activePositions` | `src/V12_002.Entries.Retest.cs:174-193`, `src/V12_002.SIMA.cs:490-500`, `src/V12_002.Symmetry.cs:496-500` | `src/V12_002.Entries.FFMA.cs:172`, `src/V12_002.Entries.MOMO.cs:146`, `src/V12_002.Entries.OR.cs:206`, `src/V12_002.Orders.Callbacks.cs:972`, `src/V12_002.SIMA.cs:661` | Non-compliant |
| `entryOrders` | `src/V12_002.Entries.Retest.cs:193`, `src/V12_002.Orders.Callbacks.cs:1520-1528` | `src/V12_002.Entries.FFMA.cs:184`, `src/V12_002.Entries.MOMO.cs:163`, `src/V12_002.Entries.Trend.cs:234`, `src/V12_002.Orders.Callbacks.cs:974`, `src/V12_002.SIMA.cs:662` | Non-compliant |
| `stopOrders` and target dicts | `src/V12_002.Symmetry.cs:496-500` | `src/V12_002.Trailing.cs:637-665`, `src/V12_002.Orders.Callbacks.cs:961`, `src/V12_002.Orders.Callbacks.cs:1028-1029` | Non-compliant |
| `_followerReplaceSpecs` | Created under lock at `src/V12_002.Orders.Callbacks.cs:1445-1477` | Transitioned and removed outside the same lock at `src/V12_002.Orders.Callbacks.cs:541-583` | Partial compliance only |
| Mode and lifecycle flags | `src/V12_002.UI.IPC.cs:875-906` (`SET_MODE`) | Direct flag writes at `src/V12_002.UI.IPC.cs:763-764`, `src/V12_002.UI.IPC.cs:835-857` | Mixed discipline |

### Follower Replace FSM Status

Confirmed-compliant path:

- `PropagateMasterEntryMove()` -> `PropagateFollowerEntryReplace()` -> `HandleMatchedFollowerOrder()` -> `SubmitFollowerReplacement()`
- Code: `src/V12_002.Orders.Callbacks.cs:1370-1430`, `src/V12_002.Orders.Callbacks.cs:1437-1491`, `src/V12_002.Orders.Callbacks.cs:539-585`, `src/V12_002.Orders.Callbacks.cs:1496-1523`

Confirmed non-compliant paths:

- CIT follower nudge: `src/V12_002.Orders.Management.cs:826-843`
- Follower target move-sync: `src/V12_002.Orders.Callbacks.cs:1318-1349`
- Symmetry follower target replace: `src/V12_002.Symmetry.cs:547-577`
- Follower trailing stop replace: `src/V12_002.Trailing.cs:615-637`
- Follower trailing target replace: `src/V12_002.Trailing.cs:976-990`

Conclusion:

- The Build 947 FSM exists, but it is not the single source of truth for follower replacement.
- The repo therefore fails the `GEMINI.md` "never cancel+submit directly" rule as written.

One area that is implemented correctly:

- `_simaToggleSem` release discipline appears consistent. `ExecuteSmartDispatchEntry()` and `ApplySimaState()` both release in `finally`: `src/V12_002.SIMA.cs:243-627`, `src/V12_002.SIMA.cs:799-828`.

## Area 2 - Tactical Logic

### Confirmed defects

- **Null-submit orphan state**
  - MOMO stages `activePositions`, reserves expected state, then rolls expected back on null submit, but still executes `entryOrders[entryName] = entryOrder`: `src/V12_002.Entries.MOMO.cs:146-163`.
  - FFMA stages `activePositions` and always writes `entryOrders[entryName] = entryOrder` with no null guard: `src/V12_002.Entries.FFMA.cs:172-184`.
  - RMA custom stages `activePositions`, rolls expected back on null submit, but never removes the staged position: `src/V12_002.Entries.RMA.cs:400-415`.

- **FFMA ledger gap**
  - FFMA is the outlier: it stages positions but does not reserve `expectedPositions` first.
  - REAPER classifies `actual != 0 && expected == 0` as critical desync: `src/V12_002.REAPER.cs:310-320`, `src/V12_002.REAPER.cs:398-405`.

- **Lifecycle-safe cancel contract is broken by early metadata purge**
  - `RequestStopCancelLifecycleSafe()` explicitly tries to preserve stop references until broker-confirmed terminal state: `src/V12_002.Orders.Callbacks.cs:96-128`.
  - Final-target and trim-close flows immediately remove `activePositions` anyway: `src/V12_002.Orders.Callbacks.cs:1020-1023`, `src/V12_002.Orders.Callbacks.cs:1070-1079`.
  - Confirmed effect: later `OnPositionUpdate` and orphan logic already anticipate missing `activePositions` by dropping into reconciliation mode: `src/V12_002.Orders.Callbacks.cs:789-793`.

- **Stop resize race**
  - `UpdateStopQuantity()` locks `stateLock`, but then performs `ContainsKey()` followed by indexer access on `stopOrders`: `src/V12_002.Orders.Management.cs:405-414`.
  - That lock does not serialize every `stopOrders` mutation elsewhere, so the method still has a TOCTOU gap.

- **Mutable pending stop replacement records**
  - Trailing logic mutates `existingPending.StopPrice`, `existingPending.Quantity`, `CapturedTargets`, and `BracketRestorationNeeded` in place: `src/V12_002.Trailing.cs:501-568`.
  - Cancel callbacks read and act on those same objects later: `src/V12_002.Orders.Callbacks.cs:368-383`, `src/V12_002.Orders.Callbacks.cs:599-611`.

- **Null-unsafe broker-object sweeps**
  - Orphan scan assumes `order.Instrument` and `order.Name` are non-null: `src/V12_002.Orders.Management.cs:1451-1456`.
  - REAPER flatten assumes `order.Instrument` and `position.Instrument` are non-null: `src/V12_002.REAPER.cs:456-474`.

### Tactical conclusion

`ConcurrentDictionary` protects individual operations, not the business invariant that position metadata, expected quantity, live orders, and cleanup state must move together. The tactical bug pattern throughout Area 2 is not "missing thread-safe collections"; it is "multi-object invariants updated in pieces."

## Area 3 - Deep Architecture

### Live threading model reconstructed

- Strategy thread:
  - `OnBarUpdate()`, `OnMarketData()`, most order management, `TriggerCustomEvent()` consumers.
- Broker callback marshaling:
  - `_accountExecutionQueue` from `OnAccountExecutionUpdate()`: `src/V12_002.UI.Compliance.cs:420-477`
  - `_accountOrderQueue` from `OnAccountOrderUpdate()`: `src/V12_002.Orders.Callbacks.cs:462-499`
- SIMA deferred submit pump:
  - `_pendingFleetDispatches` and `PumpFleetDispatch()`: `src/V12_002.SIMA.cs:503-521`, `src/V12_002.SIMA.cs:638-676`
- REAPER background thread:
  - `StartReaperAudit()` and `ReaperLoop()`: `src/V12_002.REAPER.cs:81-150`
- IPC:
  - dedicated listener thread plus fire-and-forget client tasks: `src/V12_002.UI.IPC.cs:76-126`, `src/V12_002.UI.IPC.cs:172-176`
  - inline queue draining also runs on every last tick and every bar: `src/V12_002.cs:972-983`, `src/V12_002.cs:1009-1017`, `src/V12_002.UI.IPC.cs:491-587`

### Cascading failure narratives

1. **Late follower submit after SIMA shutdown**
   - Confirmed steps:
     - Dispatch path enqueues follower work and primes the pump: `src/V12_002.SIMA.cs:503-521`, `src/V12_002.SIMA.cs:599-601`
     - SIMA disable only stops REAPER and unsubscribes handlers: `src/V12_002.SIMA.cs:815-822`
     - Pump later dequeues and submits anyway: `src/V12_002.SIMA.cs:640-676`
   - Inference:
     - A queued follower order can be submitted after the control plane has already been partially dismantled.

2. **Duplicate REAPER actions under strategy-thread stall**
   - Confirmed steps:
     - REAPER checks `_repairInFlight` before enqueue: `src/V12_002.REAPER.cs:253-255`
     - It enqueues repairs before `ExecuteReaperRepair()` sets the flag: `src/V12_002.REAPER.cs:287-289`, `src/V12_002.REAPER.cs:635-636`
     - Flatten queue has no comparable in-flight guard: `src/V12_002.REAPER.cs:313-320`, `src/V12_002.REAPER.cs:429-511`
   - Inference:
     - A busy strategy thread can turn one desync into multiple repair or close attempts against already-changing broker state.

3. **Backpressure amplification during flatten**
   - Confirmed steps:
     - Broker callbacks always enqueue and schedule another `TriggerCustomEvent()`: `src/V12_002.UI.Compliance.cs:426-439`, `src/V12_002.Orders.Callbacks.cs:463-468`
     - During flatten, processors do not drain; they reschedule, and mid-loop they re-enqueue the current item: `src/V12_002.UI.Compliance.cs:455-477`, `src/V12_002.Orders.Callbacks.cs:478-499`
     - The queues are unbounded: `src/V12_002.cs:124`, `src/V12_002.cs:127`
   - Inference:
     - A long flatten window can accumulate stale execution and order events that replay after the position state is already obsolete.

4. **False desync from weaker off-thread broker reads**
   - Confirmed steps:
     - REAPER audits live `acct.Positions` and `acct.Orders` from a dedicated background thread: `src/V12_002.REAPER.cs:122-150`, `src/V12_002.REAPER.cs:211-214`, `src/V12_002.REAPER.cs:330-334`
   - Inference:
     - This thread is making flatten and naked-stop decisions from a weaker snapshot model than the rest of the codebase, which mostly uses `ToArray()` snapshots or strategy-thread marshaling.

### Architecture conclusion

The long-term stability threat is not a single race. It is the combination of:

- partial state serialization,
- direct follower replace exceptions to the FSM,
- queues that can outlive lifecycle boundaries,
- and background decisions made from live broker collections without a single authoritative state snapshot.

That combination creates a system where each local safety patch can still be invalidated by another asynchronous path.

## Final Remediation Priority

1. Make `stateLock` the real invariant boundary for all shared trade state, including teardown and FSM state.
2. Remove every remaining follower `Cancel()` -> immediate `Submit()` path that is not broker-confirmation-driven.
3. Gate and drain all queued work on SIMA disable, flatten, reconnect, and termination.
4. Replace mutable `PendingStopReplacement` and similar records with immutable snapshots or fully locked mutation.
5. Standardize null-submit rollback so staged metadata is never left behind.
6. Move REAPER to stable snapshots or strategy-thread fed account state instead of live off-thread collection reads.

## Audit Method

- Static code audit only.
- Evidence classified as:
  - **Confirmed**: directly visible in current baseline source.
  - **Inference**: failure sequence derived from confirmed control flow and queue/lifecycle behavior.
- No source edits were made. Only this report file was created.
