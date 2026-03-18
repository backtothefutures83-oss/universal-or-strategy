# Phase 6: FSM Promotion & MetadataGuard
# Detailed Technical Design Specification

**Date**: 2026-03-17
**Architect**: Claude (P3)
**Build Target**: 1002
**Hardened Path**: `artifacts/design/phase6_design_spec.md`
**Status**: DESIGN ONLY -- No implementation. For Engineer (Codex) ingestion.

---

## 1. Executive Summary

The `FollowerBracketFSM` currently runs in **Shadow Mode** (Phase 2/3/4 stubs
noted throughout the codebase). It observes broker events and logs divergences
but is NOT the system-wide source of truth for follower account state.

Seven discrete code paths still use `expectedPositions` as the primary
authority for follower account state. These are the promotion targets.

**Phase 6 Goal**: Promote `FollowerBracketFSM` to full primary authority for
all follower lifecycle operations (Cancel, Flatten, Cleanup, Replace). Remove
fallback dependencies on `expectedPositions` for follower accounts. Add a
`MetadataGuard` static validation layer at FSM ingestion and IPC dispatch
boundaries. Redesign REAPER Audit/Repair logic to be FSM-driven.

**Master account is explicitly excluded from FSM promotion.**
Master uses `expectedPositions` + `Account.Positions` (broker truth) as
designed. The FSM only governs the 18 follower accounts.

---

## 2. Current State Diagnosis

### 2.1 FSM Shadow Mode Confirmation

`src/V12_002.Symmetry.BracketFSM.cs` contains the FSM definition with Phase 2
comment markers. The `ProcessBracketEvent()` method observes and logs but drives
no real behavior. `DrainAccountMailbox()` calls `ProcessBracketEvent()` but the
output is only shadow diagnostics.

**FSM IS partially promoted in one location**:
`AuditSingleFleetAccount()` in `REAPER.Audit.cs` lines 58-78 has a
`// Phase 4: Promote BracketFSM to primary authority` block that computes
`fsmExpectedQty` from FSM state. This is correct in intent but has a
hydration flaw (see Section 2.3).

### 2.2 Critical Gap: CleanupPosition() Does Not Close FSM

**File**: `src/V12_002.Orders.Management.Cleanup.cs` line 37

`CleanupPosition(entryName)` removes entries from `activePositions`,
`entryOrders`, `stopOrders`, `target1Orders..target5Orders`.
It does NOT touch `_followerBrackets[entryName]` or `_orderIdToFsmKey`.

**Effect**: After position teardown, the FSM remains in its last state
(e.g., `Active`, `Submitted`). The REAPER query
`_followerBrackets.Values.Where(f => f.AccountName == acct.Name)` then
counts this stale FSM as contributing to `fsmExpectedQty`. A stale Active
FSM causes REAPER to permanently believe the account has an open position,
triggering an infinite repair loop.

This is the **highest-priority gap** in the system.

### 2.3 Hydrated Active FSM -- Circular `actualQty` Fallback

**File**: `src/V12_002.REAPER.Audit.cs` lines 70-76

When an Active FSM has no `EntryOrder` reference (hydrated after restart),
the code falls back to `fsmExpectedQty += actualQty`. This is circular: if
`actualQty == 0` (position already closed), the FSM contributes 0 and no
desync fires -- but the stale Active FSM lives forever consuming state.

### 2.4 REAPER.Repair Race Guard Uses `expectedPositions`

**File**: `src/V12_002.REAPER.Repair.cs` lines 169-177

The "RACE-GUARD" before repair submission reads `expectedPositions`:
```
if (currentExpected == 0) { abort repair }
```
If the FSM has already resolved (e.g., fill arrived before repair executed),
but `expectedPositions` was not yet zeroed, the repair fires anyway. Conversely,
if `expectedPositions` was zeroed by a fast path but FSM is still Active, the
repair is incorrectly aborted. FSM state is the correct authority here.

### 2.5 CANCEL_ALL Bracket Preservation Uses `expectedPositions`

**File**: `src/V12_002.UI.IPC.Commands.Fleet.cs` lines 133-142

The guard for skipping fleet stop/target cancellations:
```
expectedPositions.TryGetValue(ExpKey(acct.Name), out chkQty);
if (chkQty != 0) continue;
```
`expectedPositions[fleet]` is not updated at every fill event (it depends on
the execution path). FSM Active state is authoritative for "this account has a
live position with a bracket protecting it."

### 2.6 H-13 Skip Logic Uses `expectedPositions`

**File**: `src/V12_002.SIMA.Fleet.cs` in `ShouldSkipFleetAccount()` lines ~175-216

The "stale expectedPositions reconciliation" check uses `expectedPositions.TryGetValue`
to detect stale reservation. An active FSM should be an additional guard
preventing the zero-reset -- a live FSM means the position is real.

### 2.7 ProcessReaperFlattenQueue Does Not Terminate FSMs

**File**: `src/V12_002.REAPER.Audit.cs` in `ProcessReaperFlattenQueue()`

After `SetExpectedPositionLocked(ExpKey(accountName), 0)` (line 407), FSMs
for the flattened account remain in Active state. On the next audit cycle,
REAPER sees Active FSMs with `fsmExpectedQty != 0` vs broker `actualQty == 0`
(flatten fill not yet confirmed) and immediately re-queues a repair.

### 2.8 BracketFSM Cancel Handling -- Replace Cycle Regression

**File**: `src/V12_002.Symmetry.BracketFSM.cs` lines 190-193

Any `Cancelled` broker event transitions FSM to `Cancelled` state (terminal).
This is wrong for MOVE-SYNC replace cycles: the cancel of the old entry order
is intentional and the FSM should survive to track the replacement order.
Currently, the FSM transitions to terminal `Cancelled` and the replacement
order arrives as an untracked ghost.

---

## 3. Change Inventory

| ID | Description | File | Priority |
|----|-------------|------|----------|
| C1 | CleanupPosition FSM teardown | Orders.Management.Cleanup.cs | P0 |
| C2 | BracketFSM `Replacing` state + ProcessBracketEvent cancel fix | Symmetry.BracketFSM.cs | P0 |
| C2e | Wire `SetFsmReplacing()` at replace initiation point | Symmetry.Replace.cs | P0 |
| C3 | `TerminateFsmsForAccount()` on emergency flatten | REAPER.Audit.cs | P1 |
| C4 | REAPER Repair: FSM race guard replaces `expectedPositions` check | REAPER.Repair.cs | P1 |
| C5 | CANCEL_ALL bracket preservation: FSM Active state as authority | UI.IPC.Commands.Fleet.cs | P1 |
| C6 | H-13 skip logic: add FSM Active guard | SIMA.Fleet.cs | P2 |
| C7 | AuditSingleFleetAccount: fix hydrated Active FSM circular fallback | REAPER.Audit.cs | P1 |
| N1 | New file: V12_002.MetadataGuard.cs | (new) | P2 |

---

## 4. Change C1: CleanupPosition FSM Teardown [P0]

### Target
`src/V12_002.Orders.Management.Cleanup.cs` -- `CleanupPosition(string entryName)` method.

### Design Rule
`CleanupPosition` is the canonical teardown path for all position lifecycle ends:
stop fill, target fill, cascade cancel, flatten, CANCEL_ALL unfilled cleanup.
It MUST also be the canonical FSM teardown path.

### Logic to Add
At the END of `CleanupPosition`, after all stop/target/entry dictionary operations,
before the method returns:

1. Call `_followerBrackets.TryRemove(entryName, out removedFsm)`.
2. If found, iterate `removedFsm.EntryOrder`, `removedFsm.StopOrder`,
   `removedFsm.Targets[0..4]` -- for each non-null order, call
   `_orderIdToFsmKey.TryRemove(order.OrderId, out _)`.
3. Print a `[FSM-C1] Terminated FSM for {entryName} (was {state})` log line.

### Invariant Guaranteed
After C1: Every `CleanupPosition` call results in complete FSM removal.
No stale FSMs can survive position teardown.

---

## 5. Change C2: BracketFSM `Replacing` State [P0]

### 5.1 New Enum Value

Add `Replacing` to `FollowerBracketState` enum in `Symmetry.BracketFSM.cs`:

```
Replacing  // In-flight two-phase cancel+resubmit (MOVE-SYNC FSM active)
```

Position in enum: between `Active` and `Modifying`.

### 5.2 New Field on FollowerBracketFSM

Add to the `FollowerBracketFSM` class:
```
public string ReplacingCancelOrderId;
```
Set when entering `Replacing` state. Cleared (null) when replacement is
submitted and FSM advances to `Submitted`.

### 5.3 New Helper: `SetFsmReplacing(fleetEntryName, cancelOrderId)`

Private method in `#region BracketFSM Logic`:
- Looks up FSM by `fleetEntryName` in `_followerBrackets`.
- Sets `fsm.State = FollowerBracketState.Replacing`.
- Sets `fsm.ReplacingCancelOrderId = cancelOrderId`.
- Updates `fsm.LastUpdateUtc`.
- Prints `[FSM-C2] {entryName} -> Replacing (cancelId={cancelOrderId})`.

### 5.4 Fixed Cancel Handling in `ProcessBracketEvent`

Replace the current `case OrderState.Cancelled:` block (which unconditionally
sets `fsm.State = FollowerBracketState.Cancelled`) with:

**Rule**: If `fsm.State == Replacing` AND `fsm.ReplacingCancelOrderId == evt.OrderId`:
- Do NOT transition to Cancelled.
- Stay in Replacing state.
- Print `[FSM-C2] Replace-cycle cancel absorbed -- FSM stays Replacing`.

**Otherwise**: Transition to `Cancelled` as normal.

### 5.5 Change C2e: Wire SetFsmReplacing() at Replace Initiation

**File**: `src/V12_002.Symmetry.Replace.cs` (wherever `_followerReplaceSpecs[key]`
is first assigned with `State = FollowerReplaceState.PendingCancel`).

Immediately after assigning the `PendingCancel` spec, call:
```
SetFsmReplacing(fleetEntryName, cancellingOrder.OrderId);
```

This ensures the BracketFSM transitions to `Replacing` before the Cancelled
broker event arrives, so the cancel is correctly absorbed rather than
terminating the FSM.

### 5.6 FSM Lifecycle Through Replace Cycle

```
Active
  |
  [PendingCancel initiated in Replace FSM]
  --> SetFsmReplacing() called
  |
Replacing (ReplacingCancelOrderId = "abc123")
  |
  [Broker: Cancelled event for order "abc123"]
  --> ProcessBracketEvent: cancel absorbed (stay Replacing)
  |
  [SubmitFollowerReplacement() runs]
  --> new FollowerBracketFSM created for same fleetEntryName
  --> State = PendingSubmit (then Submitted, Accepted, Active normally)
```

---

## 6. Change C3: TerminateFsmsForAccount on Flatten [P1]

### Target
`src/V12_002.REAPER.Audit.cs` -- `ProcessReaperFlattenQueue()`.

### New Helper Method: `TerminateFsmsForAccount(string accountName)`

Logic:
1. Iterate `_followerBrackets.ToArray()` for entries where `f.AccountName == accountName`.
2. For each: `_followerBrackets.TryRemove(entryName, out fsm)`.
3. Remove all OrderId -> key mappings (same pattern as C1).
4. Print `[FSM-C3] Terminated FSM {entryName} for {accountName} (flatten)`.

### Call Site
Call `TerminateFsmsForAccount(accountName)` immediately after
`SetExpectedPositionLocked(ExpKey(accountName), 0)` in `ProcessReaperFlattenQueue`.

### Why Before vs After SetExpectedPosition
Calling after ensures `expectedPositions` is already zeroed when FSMs are
removed. The next audit cycle will then find `fsmExpectedQty == 0` AND
`expectedQty == 0` -- no spurious repair triggered.

---

## 7. Change C4: REAPER Repair FSM Race Guard [P1]

### Target
`src/V12_002.REAPER.Repair.cs` -- `ExecuteReaperRepair(string accountName)`,
lines 169-177 (the "RACE-GUARD" block).

### Current Logic (to replace)
```
expectedPositions.TryGetValue(ExpKey(accountName), out currentExpected);
if (currentExpected == 0) { abort }
```

### New Design

**Primary check**: Query `_followerBrackets` for any FSM in
`Active | Accepted | Submitted | Replacing` state for this account.
- If found: FSM has active state -- the repair is valid.
- If NOT found: FSM has resolved (terminal or gone). Fall through to
  `expectedPositions` legacy fallback.

**Legacy fallback** (only when no FSM found):
- Read `expectedPositions.TryGetValue(ExpKey(accountName), out legacyExpected)`.
- If `legacyExpected == 0`: abort repair with
  `[FSM-RACE GUARD ABORT] no active FSM and expectedPos=0`.
- If `legacyExpected != 0`: allow repair with log
  `[FSM-RACE GUARD] no FSM -- legacy expectedPositions fallback`.

### Design Rationale
The FSM is promoted to primary authority. `expectedPositions` remains as a
legacy safety net only for accounts where an FSM was never created (pre-FSM
repair jobs, edge cases on restart before FSM hydrates).

---

## 8. Change C5: CANCEL_ALL Bracket Preservation FSM [P1]

### Target
`src/V12_002.UI.IPC.Commands.Fleet.cs`, inside the fleet account loop
within the CANCEL_ALL SIMA block, at the `Stop_/S_/T1_-T5_` skip logic.

### Current Logic (to replace)
```
expectedPositions.TryGetValue(ExpKey(acct.Name), out chkQty);
if (chkQty != 0) continue;
```

### New Design

**Query**: Does any FSM for this account have `State == Active`?
```
bool acctHasActiveFsm = _followerBrackets.Values.Any(f =>
    f.AccountName == acct.Name &&
    f.State == FollowerBracketState.Active);
if (acctHasActiveFsm) continue;
```

**Fallback**: If no FSMs exist for this account (pre-FSM or disconnected),
fall back to `expectedPositions` check.

### Why FSM Active Only (not Accepted/Submitted)
- `Active` state = entry order has filled. The bracket (stop + target) is
  genuinely protecting a live broker position. Preserve it.
- `Accepted/Submitted` state = entry is pending. If CANCEL_ALL fires, the
  entry should be cancelled (it hasn't filled yet). The bracket was submitted
  speculatively (Market entry path) and should be swept as orphaned.

---

## 9. Change C6: H-13 Skip Logic FSM Guard [P2]

### Target
`src/V12_002.SIMA.Fleet.cs` -- `ShouldSkipFleetAccount()`, Step 2 block.

### Addition
Before the `else` branch that clears `expectedPositions` via H-13, add a
check for active FSMs:

```
bool hasActiveFsmForAcct = _followerBrackets.Values.Any(f =>
    f.AccountName == acct.Name &&
    (f.State == Active || f.State == Accepted ||
     f.State == Submitted || f.State == Replacing));
```

If `hasActiveFsmForAcct` is true: add to the "SKIP -- not resetting" log
branch (do not clear `expectedPositions`). This prevents H-13 from zeroing
`expectedPositions` while a live position is tracked by the FSM.

---

## 10. Change C7: Hydrated Active FSM -- Fix Circular Fallback [P1]

### Target
`src/V12_002.REAPER.Audit.cs` -- `AuditSingleFleetAccount()` lines 70-76.

### Current Logic (broken)
```csharp
else if (f.State == FollowerBracketState.Active)
{
    // Hydrated Active FSM: entry was terminal at restart, no order reference.
    fsmExpectedQty += actualQty;   // <-- CIRCULAR
}
```

### New Design

**Case A**: `actualQty != 0` (broker confirms live position):
- Keep `fsmExpectedQty += actualQty`. Correct and non-circular since
  `actualQty` is broker truth confirming the FSM belief.

**Case B**: `actualQty == 0` (broker flat, FSM is stale Active):
- Auto-terminate: `_followerBrackets.TryRemove(f.EntryName, out _)`.
- Print `[REAPER-C7] Stale Active FSM for {entryName} on {acctName} (broker flat) -- auto-terminating`.
- Do NOT add to `fsmExpectedQty`. The position closed.

**Note**: Use `.ToArray()` before iterating in this method since C7 modifies
`_followerBrackets` during the loop. This is already the pattern in the method
(line 59 uses `.ToList()`).

---

## 11. New File N1: V12_002.MetadataGuard.cs [P2]

### Purpose
Static validation layer that gates incoming events and signals before the FSM
or Execution layers process them. All methods run on the strategy thread.
All methods **fail open** (return true on exception) to avoid strategy freezes.

### File Location
`src/V12_002.MetadataGuard.cs` -- new partial class file.

### 11.1 Configuration Constants (private, in class body)

| Constant | Type | Default | Description |
|----------|------|---------|-------------|
| `MetadataMaxCommandAgeMs` | `long` | `5000` | IPC command max age in milliseconds |
| `MetadataMaxEventAgeMs` | `long` | `30000` | Broker AccountEvent max age in milliseconds |

### 11.2 Duplicate Command Store

```
private readonly ConcurrentDictionary<string, DateTime> _processedCommandIds
```

Key: command nonce/ID string. Value: DateTime of first receipt.
Pruned on each MetadataGuardDuplicate call (entries older than
`MetadataMaxCommandAgeMs * 2` are removed).

### 11.3 Guard G1: Timestamp Freshness (IPC signals)

**Method**: `MetadataGuardTimestamp(long eventTicks, string context) : bool`

**Logic**:
1. Compute `ageTicks = DateTime.UtcNow.Ticks - eventTicks`.
2. If `ageTicks > MetadataMaxCommandAgeMs * TimeSpan.TicksPerMillisecond`:
   - Print `[METADATA-G1] STALE {context}: age={ageMs:F0}ms > max={max}ms -- rejected`.
   - Return false.
3. Return true.

**Fail open**: On exception, return true.

### 11.4 Guard G1b: Broker Event Age

**Method**: `MetadataGuardEventAge(long eventTicks, string context) : bool`

Same logic as G1 but uses `MetadataMaxEventAgeMs` threshold and
`[METADATA-G1b]` log prefix. Used to skip ancient mailbox events that
built up during a disconnection.

### 11.5 Guard G2: FSM State Compatibility Matrix

**Method**: `MetadataGuardStateCompatibility(FollowerBracketState currentState, OrderState incomingEvent, string context) : bool`

**Rules**:

| Current State | Incoming Event | Action |
|---------------|---------------|--------|
| Filled | any | Return false: `[METADATA-G2] Terminal FSM {state} received {event} -- rejected` |
| Cancelled | any | Return false (same) |
| Rejected | any | Return false (same) |
| Any non-terminal | Accepted / Working | Return true (idempotent, allowed) |
| None | Filled | Return true (ghost check is caller's responsibility) |
| All other | Any | Return true (default allow) |

**Design Note**: This guard is intentionally conservative. It only blocks
events that are provably illegal (events arriving at terminal FSMs). All
ambiguous cases are allowed through to prevent false blocking during
reconnection races.

**Fail open**: On exception, return true.

### 11.6 Guard G3: Duplicate IPC Command

**Method**: `MetadataGuardDuplicate(string commandId, string context) : bool`

**Logic**:
1. If `commandId` is null/empty: return true (no ID = cannot deduplicate).
2. Prune `_processedCommandIds` entries older than `MetadataMaxCommandAgeMs * 2`.
3. `_processedCommandIds.TryAdd(commandId, DateTime.UtcNow)`:
   - If added (new): return true.
   - If already present: print `[METADATA-G3] DUPLICATE command {id} for {context} -- rejected`, return false.

**Fail open**: On exception, return true.

### 11.7 Guard G4: Repair Authorization

**Method**: `MetadataGuardRepairAuthorized(string accountName, string context) : bool`

**Logic**:
1. Scan `_followerBrackets.Values` for any FSM where
   `AccountName == accountName && State == Active`.
2. If found: Print `[METADATA-G4] Repair suppressed for {accountName}: FSM Active (self-healed)`, return false.
3. Return true.

**Purpose**: Final gate before REAPER submits a repair order. If the FSM
became Active between when the repair was queued and when it executes (the
desync self-healed via a fill event), the repair must be suppressed.

**Fail open**: On exception, return true.

### 11.8 Composite Gate: MetadataGuardFsmEvent

**Method**: `MetadataGuardFsmEvent(AccountEvent evt, FollowerBracketFSM fsm) : bool`

**Logic**:
1. If `evt.TimestampTicks > 0`: call `MetadataGuardEventAge(evt.TimestampTicks, ...)`. If false, return false.
2. If `fsm != null`: call `MetadataGuardStateCompatibility(fsm.State, evt.NewState, fsm.EntryName)`. If false, return false.
3. Return true.

This is the single call that `ProcessBracketEvent` makes before any FSM
transition, after the FSM has been resolved (non-null).

---

## 12. Integration Points: Where Guards Are Called

### Integration A: ProcessBracketEvent (BracketFSM.cs)

After the FSM lookup block resolves `fsm` (line ~159 where "if (fsm == null) return;"),
add immediately:
```
if (!MetadataGuardFsmEvent(evt, fsm)) return;
```

### Integration B: ExecuteReaperRepair (REAPER.Repair.cs)

After the C4 FSM race guard code, add:
```
if (!MetadataGuardRepairAuthorized(accountName, "ExecuteReaperRepair")) return;
```

### Integration C: IPC Handler (UI.IPC.Commands.Fleet.cs)

Phase 6 defers full IPC timestamp integration (requires protocol upgrade --
IPC clients must begin sending UTC ticks token in command strings).

For Phase 6: wire G3 (duplicate guard) for CANCEL_ALL and FLATTEN commands
using a compound key of `action + minute bucket`:
```
string cmdId = action + "|" + (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute).ToString();
```
This allows 1 CANCEL_ALL per minute maximum, rejecting burst/replay.

Full timestamp guard (`G1`) is deferred to Phase 7 IPC Protocol Upgrade.

---

## 13. REAPER Decision Chain After Phase 6

```
AuditSingleFleetAccount(acct, shouldLog)
|
+-- 1. Query _followerBrackets for acct.Name
|       -> Compute fsmExpectedQty from Active/Accepted/Submitted FSMs
|       -> C7: Auto-terminate stale Active FSMs where broker is flat
|
+-- 2. Compare fsmExpectedQty vs actualQty
|
+-- 3. Flat with fsmExpected != 0 (repair candidate):
|       a. syncPending check (unchanged)
|       b. fillGrace check (unchanged)
|       c. hasWorkingEntry via FSM (already in codebase)
|       d. Enqueue repair
|           -> ExecuteReaperRepair runs
|           -> C4: FSM race guard (primary)
|           -> N1-G4: MetadataGuardRepairAuthorized (final gate)
|
+-- 4. Naked position audit (unchanged -- broker order scan, not FSM)
|
+-- 5. Critical desync (actualQty sign mismatch -> flatten)
        -> C3: TerminateFsmsForAccount() after flatten executes
```

---

## 14. Master Account: Unchanged

`AuditMasterAccountIfNeeded()` is explicitly NOT modified.
Master uses `expectedPositions` + `Account.Positions` (broker truth).
Master has no `FollowerBracketFSM`. The master naked-position audit
(added in Build 998) remains unchanged.

---

## 15. What expectedPositions Is Still Used For (After Phase 6)

`expectedPositions` is NOT eliminated. It remains authoritative for:

1. **Dispatch reservation**: `AddExpectedPositionDeltaLocked` / `DeltaExpectedPositionLocked` in `SIMA.Dispatch.cs` -- unchanged. This is the pre-fill reservation system.
2. **Master account state**: `AuditMasterAccountIfNeeded` -- unchanged.
3. **REAPER orphan self-heal**: The 3-attempt fallback in `ExecuteReaperRepair` that zeroes `expectedPositions` after 3 failed PositionInfo lookups -- unchanged.
4. **Dispatch sync pending barrier**: `_dispatchSyncPendingExpKeys` -- unchanged.
5. **H-13 reconciliation**: Still reads `expectedPositions` in `ShouldSkipFleetAccount` but now additionally guarded by FSM Active check (C6).

**Design philosophy**: FSM = primary authority. `expectedPositions` = secondary
fallback for accounts/states not yet covered by FSM.

---

## 16. Build 1002 File Manifest

| File | Change | Type |
|------|--------|------|
| `src/V12_002.Orders.Management.Cleanup.cs` | C1: FSM teardown in CleanupPosition | Modify |
| `src/V12_002.Symmetry.BracketFSM.cs` | C2: Replacing state, cancel fix, MetadataGuard integration A | Modify |
| `src/V12_002.Symmetry.Replace.cs` | C2e: SetFsmReplacing() wire-up at PendingCancel initiation | Modify |
| `src/V12_002.REAPER.Audit.cs` | C3: TerminateFsmsForAccount + C7: hydration fix | Modify |
| `src/V12_002.REAPER.Repair.cs` | C4: FSM race guard + MetadataGuard integration B | Modify |
| `src/V12_002.UI.IPC.Commands.Fleet.cs` | C5: FSM bracket preservation | Modify |
| `src/V12_002.SIMA.Fleet.cs` | C6: H-13 FSM guard | Modify |
| `src/V12_002.Orders.Callbacks.AccountOrders.cs` | (none -- C2e is in Replace.cs) | No change |
| `src/V12_002.MetadataGuard.cs` | N1: New partial class file | New |
| `src/V12_002.cs` | BUILD_TAG = "1002" | Modify |

---

## 17. Engineer (Codex) P4 Self-Audit Checklist

Before submitting Build 1002 for P5 Architectural Sign-off:

- [ ] **Grep scan**: zero `lock(stateLock)` in all modified files
- [ ] **Thread safety**: all `_followerBrackets` reads/writes on strategy thread only
- [ ] **ASCII Gate**: no emoji or Unicode in any `Print()` strings in MetadataGuard.cs
- [ ] **CleanupPosition**: `_followerBrackets.TryRemove` + `_orderIdToFsmKey` cleanup executes in all exit paths (not inside a try/catch that can suppress it)
- [ ] **Replacing state**: `SetFsmReplacing()` called in `Symmetry.Replace.cs` at `PendingCancel` initiation, NOT only in `HandleMatchedFollowerOrder`
- [ ] **ProcessBracketEvent**: `MetadataGuardFsmEvent` called AFTER fsm is resolved and non-null, BEFORE the state transition switch
- [ ] **C7 loop safety**: REAPER Audit C7 block uses `.ToList()` snapshot before modifying `_followerBrackets` in the loop
- [ ] **MetadataGuard**: all 4 guard methods have `try { ... } catch { return true; }` fail-open wrappers
- [ ] **MetadataGuard partial class**: header is exactly `public partial class V12_002 : Strategy`
- [ ] **BUILD_TAG**: updated to "1002" in `src/V12_002.cs` line 44
- [ ] **No new Enqueue() calls** added inside loops that already execute on the strategy thread

---

## 18. Architectural Rationale Notes

**Why not eliminate `expectedPositions` entirely?**
The dispatch reservation system requires `expectedPositions` for the
pre-fill window: from `acct.Submit()` until broker confirms the fill.
During this window the FSM is in `Submitted` or `Accepted` state but the
position is not yet real. `expectedPositions` provides the REAPER guard
during this window (via `_dispatchSyncPendingExpKeys`). A full elimination
would require extending the FSM to carry reservation state, which is Phase 7
scope.

**Why `Replacing` state instead of reusing `Modifying`?**
`Modifying` is for price changes (trailing stop updates) -- the entry order
and bracket structure are unchanged. `Replacing` is a structural change: the
entry order identity changes. They must be separate states so the audit layer
can distinguish "price modification in flight" from "entry order being replaced."

**Why MetadataGuard fails open?**
A guard that blocks transitions on exception would be more dangerous than a
guard that passes them through. A stuck guard could prevent REAPER from
repairing a naked position. Fail-open is the correct default for a safety
system protecting financial positions.

---

*End of Phase 6 Design Specification*
*For Engineer (Codex) implementation. Not a code commit. P3 Architectural Spec only.*
