# Phase 5.1 Implementation Instructions
# Engineer: Gemini | Architect: Claude | Date: 2026-03-16
# Build Tag: Increment to 986 on delivery
# Status: P0 Re-delivery -- Active Position Hydration Fix

---

## Why This Re-delivery Is Required

Build 985 delivered `HydrateFSMsFromWorkingOrders()` but the method never creates an `Active`
FSM for positions that were already fully filled at restart time.

**Root cause:** `HydrateWorkingOrdersFromBroker()` scans `acct.Orders`. Filled entry orders
are terminal broker states -- they are no longer in `acct.Orders`. They were never added to
`entryOrders`. The `HydrateFSMsFromWorkingOrders()` first pass iterates only `entryOrders`,
so the `Filled/PartFilled -> Active` branch is dead code.

**Consequence:** On mid-trade restart (the scenario Phase 5 was built to handle), no `Active`
FSM is created for open positions. REAPER sees `fsmExpectedQty=0`, `actualQty!=0`,
fires `isCriticalDesync=true` -- potential emergency flatten on a live protected position.

---

## Step 1: Add Position-Pass to HydrateFSMsFromWorkingOrders()
**File:** `src/V12_002.SIMA.Lifecycle.cs`

Find the existing `HydrateFSMsFromWorkingOrders()` method (line ~341).

### 1a. Add `ChangeSubmitted` to the Submitted branch

Find the existing state mapping block:
```csharp
        else if (entryState == OrderState.Working
              || entryState == OrderState.Submitted
              || entryState == OrderState.Initialized
              || entryState == OrderState.ChangePending)
            hydrationState = FollowerBracketState.Submitted;
```

Replace it with:
```csharp
        else if (entryState == OrderState.Working
              || entryState == OrderState.Submitted
              || entryState == OrderState.Initialized
              || entryState == OrderState.ChangePending
              || entryState == OrderState.ChangeSubmitted)
            hydrationState = FollowerBracketState.Submitted;
```

### 1b. Add the Position Pass at the end of HydrateFSMsFromWorkingOrders()

Find the closing lines of `HydrateFSMsFromWorkingOrders()`:
```csharp
    Print(string.Format("[SIMA] Phase 5 FSM Hydration: {0} FSMs created, {1} order IDs indexed.",
        fsmCreated, ordersIndexed));
}
```

Replace with:
```csharp
    Print(string.Format("[SIMA] Phase 5 FSM Hydration: {0} FSMs created, {1} order IDs indexed.",
        fsmCreated, ordersIndexed));

    // Position Pass: create Active FSMs for filled positions not covered by the first pass.
    // Filled entry orders are terminal -- not in acct.Orders, not in entryOrders.
    // Anchor: use stopOrders (Working post-fill) to recover the entryKey for each open position.
    int activeFsmCreated = 0;
    foreach (Account acct in Account.All)
    {
        if (!IsFleetAccount(acct)) continue;

        // Check if this account already has an FSM (covered by first pass or prior reconnect)
        bool alreadyHasFsm = false;
        foreach (var kv in _followerBrackets)
        {
            if (kv.Value.AccountName == acct.Name
                && (kv.Value.State == FollowerBracketState.Active
                    || kv.Value.State == FollowerBracketState.Accepted
                    || kv.Value.State == FollowerBracketState.Submitted))
            { alreadyHasFsm = true; break; }
        }
        if (alreadyHasFsm) continue;

        // Confirm there is an open position
        bool hasOpenPosition = false;
        try
        {
            foreach (Position pos in acct.Positions.ToArray())
            {
                if (pos != null && pos.Instrument != null
                    && pos.Instrument.FullName == Instrument.FullName
                    && pos.MarketPosition != MarketPosition.Flat)
                { hasOpenPosition = true; break; }
            }
        }
        catch { }
        if (!hasOpenPosition) continue;

        // Recover entryKey via stopOrders: stop key = entryKey (e.g. "Fleet_ApexF01_MOMO_1")
        string recoveredKey = null;
        Order recoveredStop = null;
        foreach (var kvp2 in stopOrders.ToArray())
        {
            PositionInfo pi2;
            if (!activePositions.TryGetValue(kvp2.Key, out pi2)) continue;
            if (!pi2.IsFollower) continue;
            if (pi2.ExecutingAccount == null) continue;
            if (pi2.ExecutingAccount.Name != acct.Name) continue;
            recoveredKey = kvp2.Key;
            recoveredStop = kvp2.Value;
            break;
        }

        if (recoveredKey == null)
        {
            Print(string.Format("[SIMA] Phase 5 Position Pass: WARNING -- open position on {0} but no stopOrders key found. FSM not created.", acct.Name));
            continue;
        }

        // Idempotent guard
        if (_followerBrackets.ContainsKey(recoveredKey)) continue;

        PositionInfo recPi;
        if (!activePositions.TryGetValue(recoveredKey, out recPi) || recPi.ExecutingAccount == null)
        {
            Print(string.Format("[SIMA] Phase 5 Position Pass: WARNING -- no activePositions entry for {0}. FSM not created.", recoveredKey));
            continue;
        }

        var activeFsm = new FollowerBracketFSM
        {
            AccountName = acct.Name,
            EntryName = recoveredKey,
            State = FollowerBracketState.Active,
            LastUpdateUtc = DateTime.UtcNow,
            EntryOrder = null // Filled entry not in acct.Orders -- intentionally null
        };

        // Link stop order
        if (recoveredStop != null)
        {
            activeFsm.StopOrder = recoveredStop;
            if (!string.IsNullOrEmpty(recoveredStop.OrderId))
            { _orderIdToFsmKey[recoveredStop.OrderId] = recoveredKey; ordersIndexed++; }
        }

        // Link target orders
        Order tgtOrd;
        if (target1Orders.TryGetValue(recoveredKey, out tgtOrd) && tgtOrd != null)
        {
            activeFsm.Targets[0] = tgtOrd;
            if (!string.IsNullOrEmpty(tgtOrd.OrderId))
            { _orderIdToFsmKey[tgtOrd.OrderId] = recoveredKey; ordersIndexed++; }
        }
        if (target2Orders.TryGetValue(recoveredKey, out tgtOrd) && tgtOrd != null)
        {
            activeFsm.Targets[1] = tgtOrd;
            if (!string.IsNullOrEmpty(tgtOrd.OrderId))
            { _orderIdToFsmKey[tgtOrd.OrderId] = recoveredKey; ordersIndexed++; }
        }
        if (target3Orders.TryGetValue(recoveredKey, out tgtOrd) && tgtOrd != null)
        {
            activeFsm.Targets[2] = tgtOrd;
            if (!string.IsNullOrEmpty(tgtOrd.OrderId))
            { _orderIdToFsmKey[tgtOrd.OrderId] = recoveredKey; ordersIndexed++; }
        }
        if (target4Orders.TryGetValue(recoveredKey, out tgtOrd) && tgtOrd != null)
        {
            activeFsm.Targets[3] = tgtOrd;
            if (!string.IsNullOrEmpty(tgtOrd.OrderId))
            { _orderIdToFsmKey[tgtOrd.OrderId] = recoveredKey; ordersIndexed++; }
        }
        if (target5Orders.TryGetValue(recoveredKey, out tgtOrd) && tgtOrd != null)
        {
            activeFsm.Targets[4] = tgtOrd;
            if (!string.IsNullOrEmpty(tgtOrd.OrderId))
            { _orderIdToFsmKey[tgtOrd.OrderId] = recoveredKey; ordersIndexed++; }
        }

        _followerBrackets.TryAdd(recoveredKey, activeFsm);
        activeFsmCreated++;
        Print(string.Format("[SIMA] Phase 5 Position Pass: Active FSM hydrated for {0} on {1}.",
            recoveredKey, acct.Name));
    }

    if (activeFsmCreated > 0)
        Print(string.Format("[SIMA] Phase 5 FSM Hydration (Position Pass): {0} Active FSMs created from open positions.",
            activeFsmCreated));
}
```

---

## Step 2: Build Tag
**File:** `src/V12_002.cs`
Increment `BUILD_TAG` to `986`.

---

## Step 3: Self-Audit (All 8 Checks Must Pass)

```bash
# 1. Phase 4 shadow log fully removed (regression check)
grep -n "FSM-AUDIT-ACTIVE\|legacyExpectedQty" src/V12_002.REAPER.Audit.cs

# 2. FSM hydration call present in Lifecycle
grep -n "HydrateFSMsFromWorkingOrders" src/V12_002.SIMA.Lifecycle.cs

# 3. Hydration called before _orderAdoptionComplete = true
grep -n "HydrateFSMsFromWorkingOrders\|_orderAdoptionComplete = true" src/V12_002.SIMA.Lifecycle.cs

# 4. No new lock() in modified files
grep -n "lock(" src/V12_002.SIMA.Lifecycle.cs src/V12_002.REAPER.Audit.cs

# 5. ASCII gate
grep -Pn "[^\x00-\x7F]" src/V12_002.SIMA.Lifecycle.cs src/V12_002.REAPER.Audit.cs

# 6. Build tag
grep -n "BUILD_TAG" src/V12_002.cs

# 7. Position Pass log present (new in 985.1)
grep -n "Position Pass" src/V12_002.SIMA.Lifecycle.cs

# 8. ChangeSubmitted in FSM mapper (P1 fix)
grep -n "ChangeSubmitted" src/V12_002.SIMA.Lifecycle.cs
```

Checks 1, 4, 5 must return ZERO matches.
Checks 2, 7, 8 must return at least ONE match each.
Check 3 must show `HydrateFSMsFromWorkingOrders` BEFORE `_orderAdoptionComplete = true`.
Check 6 must show `986`.

---

## Step 4: Live Validation

After loading NT8 mid-trade (with an existing open position):

**PASS -- Position Pass fired, Active FSM created:**
```
[SIMA] Phase 5 FSM Hydration: 0 FSMs created, 3 order IDs indexed.
[SIMA] Phase 5 Position Pass: Active FSM hydrated for Fleet_ApexF01_MOMO_1 on ApexF01.
[SIMA] Phase 5 FSM Hydration (Position Pass): 1 Active FSMs created from open positions.
[SIMA] Order adoption complete -- REAPER enabled.
```

**PASS -- REAPER sees Active FSM, no desync:**
```
[REAPER] ApexF01: Expected=1, Actual=1
[REAPER] Heartbeat: 1/18 accounts with positions.
```

**FAIL -- Position Pass not reached or stop key not found:**
```
[REAPER] * CRITICAL DESYNC on ApexF01: Expected=0, Actual=1
```
If this fires after a restart with a known open position, escalate to Architect immediately.
Do NOT set `AutoFlattenDesync=true` until Scenario A proof passes in live conditions.

---

## What Is NOT Changing in Phase 5.1

- No changes to `expectedPositions` write sites
- No changes to `REAPER.Audit.cs`
- No changes to `Orders.*` files
- No changes to `UI.*` files
- No changes to `SIMA.Fleet.cs`

Only `src/V12_002.SIMA.Lifecycle.cs` and `src/V12_002.cs` are modified.
