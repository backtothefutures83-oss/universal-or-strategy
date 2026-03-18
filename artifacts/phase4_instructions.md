# Phase 4 Implementation Instructions
# Engineer: Gemini | Architect: Claude | Date: 2026-03-16
# Build Tag: Increment to 984 on delivery

---

## BLOCKER: Complete Phase 3 Step 5 Before Any Phase 4 Work

Code inspection confirmed `ReaperLoop()` is still in `src/V12_002.REAPER.cs` (lines 122-168)
using `Thread.Sleep`. The declarations `reaperThread` and `isReaperRunning` are still in
`src/V12_002.cs` (lines 400-401). Phase 3 Step 5 was NOT delivered.

**Complete Phase 3 Step 5 first.** Full instructions are in `artifacts/phase3_instructions.md`
Step 5 (REAPER Thread Migration). This is identical to what was planned -- no new direction.

After completing Step 5, verify with:
```bash
grep -rn "reaperThread\|isReaperRunning\|ReaperLoop\|Thread.Sleep" src/V12_002.REAPER.cs src/V12_002.cs
```
Expected: zero matches. Only then proceed to Step 1 below.

---

## Step 1: Promote FSM Predicate in AuditSingleFleetAccount()
**File:** `src/V12_002.REAPER.Audit.cs`

### 1a. Remove the FSM shadow block (Phase 3 artifact, now superseded)

Find and delete the entire Phase 3 shadow block that was inserted after the `syncPending` line.
It is bounded by the comment `// Phase 3 [FSM-SHADOW]:` and ends at the last
`Print(string.Format("[FSM-AUDIT-SHADOW]` call. Remove all of it.

**Verify removed:** `grep -n "FSM-AUDIT-SHADOW" src/V12_002.REAPER.Audit.cs` must return zero results.

### 1b. Remove the expectedPositions primary read and its entire predicate block

**Find and remove these lines** (currently around line 62, then lines 67-172):

```csharp
            int expectedQty = 0;
            expectedPositions.TryGetValue(expectedKey, out expectedQty);
```

Then find and remove the entire block:
```csharp
            bool hasState = expectedQty != 0 || actualQty != 0;
            if (shouldLog && hasState)
                Print($"[REAPER] {acct.Name}: Expected={expectedQty}, Actual={actualQty}");

            if (expectedQty != actualQty)
            {
                // ... entire block through line 172 ...
            }
```

The block ends just before the line:
```csharp
            // ?? NAKED POSITION AUDIT (Build 1102R)
```

**Keep lines 63-65** (`syncPending` and `inFillGrace`) -- these are timing guards that survive.

### 1c. Insert the FSM-primary predicate block

**Insert the following block AFTER the `inFillGrace` line and BEFORE the naked-position
audit section** (the `// ?? NAKED POSITION AUDIT` comment):

```csharp
            // Phase 4: FSM-primary repair predicate (replaces expectedPositions for REPAIR path)
            // Strategy thread only -- _followerBrackets is safe to iterate here because
            // REAPER timer marshals AuditApexPositions via TriggerCustomEvent (Phase 3 Step 5).
            FollowerBracketFSM activeFsmForAcct = null;
            FollowerBracketFSM pendingFsmForAcct = null;
            foreach (var kv in _followerBrackets)
            {
                if (kv.Value.AccountName != acct.Name) continue;
                if (kv.Value.State == FollowerBracketState.Active)
                    activeFsmForAcct = kv.Value;
                else if (kv.Value.State == FollowerBracketState.Accepted
                      || kv.Value.State == FollowerBracketState.Submitted)
                    pendingFsmForAcct = kv.Value;
            }

            bool hasState = activeFsmForAcct != null || pendingFsmForAcct != null || actualQty != 0;
            if (shouldLog && hasState)
                Print(string.Format("[REAPER] {0}: FSM={1} Actual={2}",
                    acct.Name,
                    activeFsmForAcct != null ? "Active" :
                        pendingFsmForAcct != null ? "Pending" : "None",
                    actualQty));

            // REPAIR path: FSM says position is Active but broker says flat
            if (activeFsmForAcct != null && actualQty == 0)
            {
                if (syncPending || inFillGrace)
                {
                    if (shouldLog)
                    {
                        string reason = syncPending ? "dispatch sync pending" : "fill grace active";
                        Print(string.Format("[REAPER] {0}: repair deferred ({1}) FSM=Active but flat.",
                            acct.Name, reason));
                    }
                    // fall through to naked-position audit
                }
                else
                {
                    // Master account uses SubmitOrderUnmanaged -- not the follower path, skip
                    if (acct.Name != Account.Name)
                    {
                        string repairKey = acct.Name + "_" + Instrument.FullName;
                        bool alreadyInFlight = _repairInFlight.ContainsKey(repairKey);
                        if (!alreadyInFlight)
                        {
                            bool hasWorkingEntry = false;
                            string blockingOrderName = null;
                            OrderState blockingState = OrderState.Unknown;
                            var activeSnapshot = new Dictionary<string, PositionInfo>(activePositions);
                            foreach (var kvp in entryOrders.ToArray())
                            {
                                Order ord = kvp.Value;
                                if (ord == null) continue;
                                OrderState ordState = ord.OrderState;
                                if (IsOrderTerminal(ordState)) continue;
                                if (activeSnapshot.TryGetValue(kvp.Key, out var pi)
                                    && pi.IsFollower && pi.ExecutingAccount != null
                                    && pi.ExecutingAccount.Name == acct.Name
                                    && (ordState == OrderState.Working || ordState == OrderState.Submitted
                                        || ordState == OrderState.Accepted
                                        || ordState == OrderState.ChangePending
                                        || ordState == OrderState.Unknown
                                        || ordState == OrderState.Initialized))
                                {
                                    hasWorkingEntry = true;
                                    blockingOrderName = string.IsNullOrEmpty(ord.Name)
                                        ? kvp.Key : ord.Name;
                                    blockingState = ordState;
                                    break;
                                }
                            }
                            if (!hasWorkingEntry)
                            {
                                if (shouldLog) Print(string.Format(
                                    "[REAPER] * REPAIR CANDIDATE: {0} FSM=Active but Flat. FSM key={1}. Enqueuing repair.",
                                    acct.Name, activeFsmForAcct.EntryName));
                                _repairInFlight.TryAdd(repairKey, 0);
                                _reaperRepairQueue.Enqueue(acct.Name);
                                try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
                                catch (Exception repairTriggerEx)
                                {
                                    _repairInFlight.TryRemove(repairKey, out _);
                                    Print("[REAPER] TriggerCustomEvent failed for " + repairKey
                                        + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                                }
                            }
                            else
                            {
                                string throttleKey = blockingOrderName ?? acct.Name;
                                DateTime lastLogged;
                                bool shouldLogBlocked =
                                    !_repairBlockedLastLogged.TryGetValue(throttleKey, out lastLogged)
                                    || (DateTime.UtcNow - lastLogged).TotalSeconds >= 30;
                                if (shouldLogBlocked)
                                {
                                    _repairBlockedLastLogged[throttleKey] = DateTime.UtcNow;
                                    Print(string.Format(
                                        "[REAPER] Repair BLOCKED by {0} in state {1} (throttled: next log in 30s)",
                                        blockingOrderName, blockingState));
                                }
                            }
                        }
                        else if (shouldLog)
                            Print(string.Format("[REAPER] {0} repair already in-flight -- skipping.", acct.Name));
                    }
                    else if (shouldLog)
                        Print(string.Format("[REAPER] {0} is Master -- skipping follower repair.", acct.Name));

                    return hasState;
                }
            }

            // DEFERRED path: bracket submitted/accepted, broker flat -- entry is in-flight, wait
            if (pendingFsmForAcct != null && actualQty == 0 && activeFsmForAcct == null)
            {
                if (shouldLog) Print(string.Format("[REAPER] {0}: FSM={1}, flat -- entry pending, no action.",
                    acct.Name, pendingFsmForAcct.State));
                // fall through to naked-position audit
            }

            // CRITICAL DESYNC path: unexpected position with no FSM claiming it.
            // Uses expectedPositions as secondary backstop (Phase 5 will migrate this path too).
            int expectedQty = 0;
            expectedPositions.TryGetValue(expectedKey, out expectedQty);
            bool isCriticalDesync =
                (actualQty != 0 && expectedQty == 0
                    && activeFsmForAcct == null && pendingFsmForAcct == null)
                || (Math.Sign(actualQty) != Math.Sign(expectedQty) && expectedQty != 0);
            if (isCriticalDesync)
            {
                if (shouldLog) Print(string.Format(
                    "[REAPER] * CRITICAL DESYNC on {0}: Expected={1}, Actual={2}, FSM={3}",
                    acct.Name, expectedQty, actualQty,
                    activeFsmForAcct != null ? "Active" :
                        pendingFsmForAcct != null ? "Pending" : "None"));
                if (AutoFlattenDesync)
                {
                    if (shouldLog) Print(string.Format(
                        "[REAPER] * QUEUING FLATTEN for {0} - Emergency Re-sync!", acct.Name));
                    _reaperFlattenQueue.Enqueue(acct.Name);
                    try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                }
            }
```

**Compile gate:** Zero errors before Step 2.

---

## Step 2: Build Tag + P4 Self-Audit

**File:** `src/V12_002.Properties.cs` — increment `BUILD_TAG` to `984`

**Run all 6 grep checks. All must pass:**

```bash
# 1. REAPER thread fully removed (from Phase 3 Step 5)
grep -rn "reaperThread\|isReaperRunning\|ReaperLoop\|Thread\.Sleep" src/V12_002.REAPER.cs src/V12_002.cs

# 2. Timer present in REAPER.cs (Phase 3 Step 5 delivered)
grep -n "_reaperTimer\|OnReaperTimerElapsed" src/V12_002.REAPER.cs

# 3. FSM-primary predicate present in REAPER.Audit
grep -n "activeFsmForAcct\|FSM-primary" src/V12_002.REAPER.Audit.cs

# 4. Shadow logging fully removed
grep -n "FSM-AUDIT-SHADOW" src/V12_002.REAPER.Audit.cs

# 5. No new lock() introduced
grep -n "lock(" src/V12_002.REAPER.Audit.cs src/V12_002.REAPER.cs

# 6. ASCII gate
grep -Pn "[^\x00-\x7F]" src/V12_002.REAPER.Audit.cs src/V12_002.REAPER.cs
```

Checks 1, 4, 5, 6 must return ZERO matches.
Checks 2 and 3 must return at least one match each.

---

## Step 3: Live Validation

After loading in NT8, monitor the Output window for a full dispatch+fill cycle.

**PASS: FSM repair triggered correctly**
```
[REAPER] ApexF01: FSM=Active Actual=0
[REAPER] * REPAIR CANDIDATE: ApexF01 FSM=Active but Flat. FSM key=Fleet_Apex_1. Enqueuing repair.
```

**PASS: Pending entry not falsely repaired**
```
[REAPER] ApexF01: FSM=Pending, flat -- entry pending, no action.
```

**PASS: No shadow logs** -- zero `[FSM-AUDIT-SHADOW]` lines.

**FAIL signal: if you see** `[REAPER] ApexF01: FSM=None Actual=0` after a known dispatch,
the FSM is not being created. Escalate to Architect before continuing.

---

## What Is NOT Changing in Phase 4

The following `expectedPositions` consumers are explicitly out of scope. Do not touch them:
- `Orders.Management.Cleanup.cs` lines 143, 265 — META-GUARD
- `Orders.Callbacks.AccountOrders.cs` line 225 — ghost-order rollback
- `SIMA.Fleet.cs` line 181 — H-13 stale reconciliation
- `UI.Compliance.cs` lines 376, 451 — telemetry
- All `AddExpectedPositionDeltaLocked` / `SetExpectedPositionLocked` call sites — writes stay active
- `REAPER.Repair.cs` race guard (line 171) — stays on `expectedPositions`

These will be migrated in Phase 5.
