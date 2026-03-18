# Phase 3 Implementation Instructions
# Engineer: Gemini | Architect: Claude | Date: 2026-03-16
# Build Tag: Increment to 983 on delivery

---

## Overview

7 sequential steps. Complete each step, compile and verify before proceeding.
Pre-conditions (D1, D2, D3) must be done before the REAPER predicate work.

**Files modified:**
1. `src/V12_002.Symmetry.BracketFSM.cs`  -- D2, D3, O(1) lookup wiring
2. `src/V12_002.cs`                       -- O(1) map declaration, remove dead reaperThread
3. `src/V12_002.SIMA.Fleet.cs`            -- D1: FSM creation on dispatch
4. `src/V12_002.REAPER.cs`               -- Thread migration (background -> timer)
5. `src/V12_002.REAPER.Audit.cs`         -- FSM shadow predicate (audit comparison)

---

## Step 1: D3 Fix -- Null Guard on evt.SignalName
**File:** `src/V12_002.Symmetry.BracketFSM.cs`
**Line:** 157

**Old code (single line inside the Filled/PartFilled case):**
```csharp
                    if (evt.SignalName.StartsWith("Stop_") || evt.SignalName.StartsWith("S_") || evt.SignalName.StartsWith("T"))
```

**New code:**
```csharp
                    if (!string.IsNullOrEmpty(evt.SignalName) &&
                        (evt.SignalName.StartsWith("Stop_") || evt.SignalName.StartsWith("S_") ||
                         evt.SignalName.StartsWith("T1_") || evt.SignalName.StartsWith("T2_") ||
                         evt.SignalName.StartsWith("T3_") || evt.SignalName.StartsWith("T4_") ||
                         evt.SignalName.StartsWith("T5_")))
```

This simultaneously fixes D3 (null guard) and D2 (T-prefix precision). One edit, two defects closed.

**After change, line 154-166 should read:**
```csharp
                case OrderState.Filled:
                case OrderState.PartFilled:
                    // If it's a target or stop filling, the bracket is closing
                    if (!string.IsNullOrEmpty(evt.SignalName) &&
                        (evt.SignalName.StartsWith("Stop_") || evt.SignalName.StartsWith("S_") ||
                         evt.SignalName.StartsWith("T1_") || evt.SignalName.StartsWith("T2_") ||
                         evt.SignalName.StartsWith("T3_") || evt.SignalName.StartsWith("T4_") ||
                         evt.SignalName.StartsWith("T5_")))
                    {
                        fsm.State = FollowerBracketState.Filled;
                    }
                    else if (fsm.State == FollowerBracketState.Accepted || fsm.State == FollowerBracketState.Submitted)
                    {
                        // Entry filled -> Bracket is now ACTIVE
                        fsm.State = FollowerBracketState.Active;
                    }
                    break;
```

**Compile gate:** Zero errors before Step 2.

---

## Step 2: O(1) Map Declaration
**File:** `src/V12_002.cs`
**Location:** After line 524 (after `_accountMailbox` declaration)

**Insert after:**
```csharp
        // Phase 2: Actor Mailbox for account events
        private readonly ConcurrentQueue<AccountEvent>
            _accountMailbox = new ConcurrentQueue<AccountEvent>();
```

**Insert this block:**
```csharp
        // Phase 3: O(1) order-id to FSM key lookup (eliminates O(N) scan in ProcessBracketEvent)
        private readonly ConcurrentDictionary<string, string> _orderIdToFsmKey
            = new ConcurrentDictionary<string, string>();
```

**Also in V12_002.cs, remove dead declarations at lines 400-401:**
```csharp
        private Thread reaperThread;
        private volatile bool isReaperRunning;
```
These are replaced by the timer in Step 5. Remove both lines.

**Compile gate:** Zero errors before Step 3.

---

## Step 3: D1 Fix -- FSM Creation on Dispatch (BLOCKER)
**File:** `src/V12_002.SIMA.Fleet.cs`
**Location:** Inside `PumpFleetDispatch()`, after the successful Submit block

**Current code at lines 68-72:**
```csharp
                req.Account.Submit(req.Orders);
                ClearDispatchSyncPending(req.ExpectedKey);
                syncCleared = true;
                Print(string.Format("[PUMP] Submitted {0} orders for {1} | {2}",
                    req.Orders.Length, req.FleetEntryName, req.Account.Name));
```

**Replace with:**
```csharp
                req.Account.Submit(req.Orders);
                ClearDispatchSyncPending(req.ExpectedKey);
                syncCleared = true;
                Print(string.Format("[PUMP] Submitted {0} orders for {1} | {2}",
                    req.Orders.Length, req.FleetEntryName, req.Account.Name));

                // Phase 3 [D1-FIX]: Register FSM after confirmed Submit
                // entryOrders/stopOrders/targetNOrders are already populated by ExecuteSmartDispatchEntry
                // before PumpFleetDispatch runs -- safe to read from the dicts here.
                var fsm3 = new FollowerBracketFSM
                {
                    AccountName = req.Account.Name,
                    EntryName   = req.FleetEntryName,
                    State       = FollowerBracketState.Submitted
                };
                Order fsmEntry; entryOrders.TryGetValue(req.FleetEntryName, out fsmEntry);
                fsm3.EntryOrder = fsmEntry;
                Order fsmStop; stopOrders.TryGetValue(req.FleetEntryName, out fsmStop);
                fsm3.StopOrder = fsmStop;
                for (int fsmT = 1; fsmT <= 5; fsmT++)
                {
                    var fsmTDict = GetTargetOrdersDictionary(fsmT);
                    Order fsmTOrd;
                    if (fsmTDict != null && fsmTDict.TryGetValue(req.FleetEntryName, out fsmTOrd))
                        fsm3.Targets[fsmT - 1] = fsmTOrd;
                }
                _followerBrackets[req.FleetEntryName] = fsm3;
                // Index all order IDs for O(1) ProcessBracketEvent lookup
                if (fsmEntry != null && !string.IsNullOrEmpty(fsmEntry.OrderId))
                    _orderIdToFsmKey[fsmEntry.OrderId] = req.FleetEntryName;
                if (fsmStop != null && !string.IsNullOrEmpty(fsmStop.OrderId))
                    _orderIdToFsmKey[fsmStop.OrderId] = req.FleetEntryName;
                for (int fsmT = 0; fsmT < 5; fsmT++)
                    if (fsm3.Targets[fsmT] != null && !string.IsNullOrEmpty(fsm3.Targets[fsmT].OrderId))
                        _orderIdToFsmKey[fsm3.Targets[fsmT].OrderId] = req.FleetEntryName;
                Print(string.Format("[FSM] Registered FSM for {0} on {1} (State=Submitted, Orders indexed={2})",
                    req.FleetEntryName, req.Account.Name, req.Orders.Length));
```

**Also in the catch block** (lines 82-91), after the existing dict cleanup, add FSM cleanup:
```csharp
                // Phase 3: Clean up FSM and order index on Submit failure
                _followerBrackets.TryRemove(req.FleetEntryName, out _);
                // Note: _orderIdToFsmKey entries are harmless to leave stale -- they point to a removed FSM key
```

**Compile gate:** Zero errors before Step 4.

---

## Step 4: O(1) Lookup Wiring in ProcessBracketEvent
**File:** `src/V12_002.Symmetry.BracketFSM.cs`
**Location:** `ProcessBracketEvent()` -- replace the existing FSM lookup block (lines 108-141)

**Old lookup block:**
```csharp
            // 1. Find the FSM by OrderId or SignalName
            FollowerBracketFSM fsm = null;

            // Try matching by SignalName first (most reliable for our fleet naming convention)
            if (!string.IsNullOrEmpty(evt.SignalName))
            {
                // Signal names are like "Stop_Fleet_Apex_1" or "T1_Fleet_Apex_1"
                // The fleetEntryName is the part after the first underscore.
                int firstUnder = evt.SignalName.IndexOf('_');
                if (firstUnder >= 0 && firstUnder < evt.SignalName.Length - 1)
                {
                    string fleetEntryName = evt.SignalName.Substring(firstUnder + 1);
                    _followerBrackets.TryGetValue(fleetEntryName, out fsm);
                }
            }

            // Fallback: search all FSMs if SignalName match failed (e.g. entry orders might not have the prefix)
            if (fsm == null)
            {
                foreach (var f in _followerBrackets.Values)
                {
                    if (f.AccountName != evt.AccountAlias) continue;

                    if (f.StopOrder != null && f.StopOrder.OrderId == evt.OrderId) { fsm = f; break; }
                    bool foundT = false;
                    for (int i = 0; i < 5; i++)
                    {
                        if (f.Targets[i] != null && f.Targets[i].OrderId == evt.OrderId) { fsm = f; foundT = true; break; }
                    }
                    if (foundT) break;
                    if (f.EntryOrder != null && f.EntryOrder.OrderId == evt.OrderId) { fsm = f; break; }
                }
            }
```

**Replace with:**
```csharp
            // 1. Find the FSM by OrderId or SignalName
            FollowerBracketFSM fsm = null;

            // Phase 3: O(1) lookup by OrderId (covers all orders indexed at Submit time)
            if (!string.IsNullOrEmpty(evt.OrderId))
            {
                string fsmKey;
                if (_orderIdToFsmKey.TryGetValue(evt.OrderId, out fsmKey))
                    _followerBrackets.TryGetValue(fsmKey, out fsm);
            }

            // Fallback: SignalName-based lookup (covers signals without an indexed OrderId)
            if (fsm == null && !string.IsNullOrEmpty(evt.SignalName))
            {
                // Signal names are like "Stop_Fleet_Apex_1" or "T1_Fleet_Apex_1"
                // The fleetEntryName is the part after the first underscore.
                int firstUnder = evt.SignalName.IndexOf('_');
                if (firstUnder >= 0 && firstUnder < evt.SignalName.Length - 1)
                {
                    string fleetEntryName = evt.SignalName.Substring(firstUnder + 1);
                    _followerBrackets.TryGetValue(fleetEntryName, out fsm);
                }
            }

            // Last resort: O(N) scan for stop/target orders placed after initial index was built
            if (fsm == null)
            {
                foreach (var f in _followerBrackets.Values)
                {
                    if (f.AccountName != evt.AccountAlias) continue;
                    if (f.StopOrder != null && f.StopOrder.OrderId == evt.OrderId) { fsm = f; break; }
                    bool foundT = false;
                    for (int i = 0; i < 5; i++)
                    {
                        if (f.Targets[i] != null && f.Targets[i].OrderId == evt.OrderId) { fsm = f; foundT = true; break; }
                    }
                    if (foundT) break;
                    if (f.EntryOrder != null && f.EntryOrder.OrderId == evt.OrderId) { fsm = f; break; }
                }
            }
```

**Compile gate:** Zero errors before Step 5.

---

## Step 5: REAPER Thread Migration (Background Thread -> Timer)
**File:** `src/V12_002.REAPER.cs`

This replaces the `reaperThread` background thread pattern with a `System.Timers.Timer` that
fires audit cycles on the strategy thread via `TriggerCustomEvent`. This closes the race window
where `ReaperLoop()` accesses `_followerBrackets` on a non-strategy thread.

**Add this declaration** at the top of the `#region V12 REAPER Audit Logic` block (before the
existing queue declarations, around line 18):
```csharp
        // Phase 3: Timer replaces background thread -- fires AuditApexPositions on strategy thread
        private System.Timers.Timer _reaperTimer;
```

**Replace `StartReaperAudit()` (lines 86-98) entirely:**
```csharp
        private void StartReaperAudit()
        {
            if (_reaperTimer != null) return; // Already running
            _reaperTimer = new System.Timers.Timer(ReaperIntervalMs);
            _reaperTimer.AutoReset = true;
            _reaperTimer.Elapsed += OnReaperTimerElapsed;
            _reaperTimer.Start();
            Print("[REAPER] Audit timer STARTED - interval: " + ReaperIntervalMs + "ms");
        }
```

**Replace `StopReaperAudit()` (lines 103-117) entirely:**
```csharp
        private void StopReaperAudit()
        {
            if (_reaperTimer == null) return;
            _reaperTimer.Stop();
            _reaperTimer.Dispose();
            _reaperTimer = null;
            Print("[REAPER] Audit timer STOPPED");
        }
```

**Delete `ReaperLoop()` entirely (lines 122-168)** and replace with:
```csharp
        private void OnReaperTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Timer fires on a ThreadPool thread. All audit logic runs on the strategy thread.
            if (isFlattenRunning || !_orderAdoptionComplete || State != State.Realtime) return;
            try { TriggerCustomEvent(o => AuditApexPositions(), null); } catch { }
        }
```

**Note:** The startup-grace skip-first-cycle logic from `ReaperLoop()` is intentionally removed.
`TriggerCustomEvent` is non-blocking -- there is no "settle time" needed because `AuditApexPositions`
runs on the strategy thread, which has already processed all adoption events before the first
timer fires.

**Compile gate:** Zero errors before Step 6.

---

## Step 6: FSM Shadow Predicate in REAPER.Audit.cs
**File:** `src/V12_002.REAPER.Audit.cs`
**Location:** `AuditSingleFleetAccount()`, after line 63 (after `syncPending` is populated)

This step adds the FSM-based audit predicate in SHADOW MODE. It logs disagreements between
the FSM verdict and the existing `expectedPositions` verdict but does NOT change repair decisions.
After 2 live sessions with clean logs (no disagreements), promote the FSM predicate to primary
(Step 7, deferred).

**Insert after line 63** (`syncPending = _dispatchSyncPendingExpKeys...`):
```csharp
            // Phase 3 [FSM-SHADOW]: Compute FSM-based repair verdict for shadow comparison.
            // Does NOT affect actual repair decisions in this phase -- observation only.
            FollowerBracketFSM auditFsm = null;
            foreach (var kv in _followerBrackets)
            {
                if (kv.Value.AccountName == acct.Name &&
                    (kv.Value.State == FollowerBracketState.Active ||
                     kv.Value.State == FollowerBracketState.Accepted ||
                     kv.Value.State == FollowerBracketState.Submitted))
                {
                    auditFsm = kv.Value;
                    break;
                }
            }
            bool fsmSaysRepair   = auditFsm != null && auditFsm.State == FollowerBracketState.Active  && actualQty == 0;
            bool fsmSaysGhost    = auditFsm != null && actualQty != 0 &&
                                   (auditFsm.State == FollowerBracketState.Cancelled ||
                                    auditFsm.State == FollowerBracketState.Filled);
            bool legacySaysRepair = (actualQty == 0 && expectedQty != 0);
            bool legacySaysGhost  = (actualQty != 0 && expectedQty == 0);
            // Log divergences for shadow validation window
            if (fsmSaysRepair && !legacySaysRepair)
                Print(string.Format("[FSM-AUDIT-SHADOW] {0}: FSM=Active+Flat needs repair but legacy expectedQty=0. FSM key={1}",
                    acct.Name, auditFsm != null ? auditFsm.EntryName : "null"));
            if (!fsmSaysRepair && legacySaysRepair)
                Print(string.Format("[FSM-AUDIT-SHADOW] {0}: Legacy repair triggered (expected={1}) but FSM={2}. Check FSM creation.",
                    acct.Name, expectedQty, auditFsm == null ? "null" : auditFsm.State.ToString()));
            if (fsmSaysGhost && !legacySaysGhost)
                Print(string.Format("[FSM-AUDIT-SHADOW] {0}: FSM=Cancelled with live position -- ghost fill. Legacy did not catch.", acct.Name));
```

**Compile gate:** Zero errors.

---

## Step 7: Build Tag + P4 Self-Audit

### Build Tag
In `src/V12_002.Properties.cs`, increment `BUILD_TAG` to `983`.

### P4 Self-Audit (mandatory before delivery)

Run the following grep scans and confirm all pass:

```bash
# 1. No new lock() in strategy files
grep -rn "lock(" src/ --include="*.cs" | grep -v "stateLock\|_repairBlock\|_accountFillGrace" | grep -v "//.*lock("

# 2. No reaperThread references remain
grep -rn "reaperThread\|isReaperRunning" src/ --include="*.cs"

# 3. No StartsWith("T") false-positive pattern remains
grep -rn 'StartsWith("T")' src/ --include="*.cs"

# 4. FSM creation exists in Fleet.cs
grep -n "_followerBrackets\[req.FleetEntryName\]" src/V12_002.SIMA.Fleet.cs

# 5. O(1) map wired in PumpFleetDispatch
grep -n "_orderIdToFsmKey" src/V12_002.SIMA.Fleet.cs

# 6. Timer references in REAPER.cs (not reaperThread)
grep -n "_reaperTimer\|OnReaperTimerElapsed" src/V12_002.REAPER.cs

# 7. ASCII gate -- no non-ASCII in string literals in modified files
grep -Pn "[^\x00-\x7F]" src/V12_002.Symmetry.BracketFSM.cs src/V12_002.SIMA.Fleet.cs src/V12_002.REAPER.cs src/V12_002.REAPER.Audit.cs

# 8. Shadow predicate wired in REAPER.Audit
grep -n "FSM-AUDIT-SHADOW" src/V12_002.REAPER.Audit.cs
```

All 8 must pass (grep #2 must return ZERO matches).

---

## Step 8: Validation Protocol (Live Session)

After deployment, monitor the NT8 Output window for:

**Expected on first dispatch:**
```
[PUMP] Submitted N orders for Fleet_Apex_1 | ApexAccount
[FSM] Registered FSM for Fleet_Apex_1 on ApexAccount (State=Submitted, Orders indexed=N)
[FSM-SHADOW] Fleet_Apex_1 Transition: None -> Accepted | Event=Accepted | Order=...
[FSM-SHADOW] Fleet_Apex_1 Transition: Accepted -> Active | Event=Filled | Order=...
```

**Shadow predicate -- clean session (expected):**
```
(no [FSM-AUDIT-SHADOW] lines at all)
```

**If [FSM-AUDIT-SHADOW] lines appear**, do NOT proceed to Phase 3b (predicate promotion). Log them and route to Architect review.

---

## Phase 3b (Deferred -- After 2 Clean Sessions)

Once shadow logs show zero disagreements for 2 live trading sessions:
1. Remove the `expectedPositions` predicate block from `AuditSingleFleetAccount` (lines 67-172)
2. Promote FSM predicate to primary (remove `[FSM-AUDIT-SHADOW]` shadow logging)
3. FSM `Active + flat = repair` becomes the single source of truth
4. Increment build tag to 984

Phase 3b will be planned by Architect after shadow data is reviewed.

---

## Summary of Defects Closed

| ID | Severity | Fix | File |
|----|----------|-----|------|
| D1 | BLOCKER | FSM created on Submit in PumpFleetDispatch | SIMA.Fleet.cs |
| D2 | MODERATE | StartsWith("T1_") - StartsWith("T5_") | BracketFSM.cs |
| D3 | MODERATE | !string.IsNullOrEmpty(evt.SignalName) guard | BracketFSM.cs |
| -- | INFRA | O(1) _orderIdToFsmKey map declared + wired | V12_002.cs, BracketFSM.cs, SIMA.Fleet.cs |
| -- | SAFETY | REAPER background thread -> strategy-thread timer | REAPER.cs |
| -- | SHADOW | FSM audit predicate alongside legacy predicate | REAPER.Audit.cs |
