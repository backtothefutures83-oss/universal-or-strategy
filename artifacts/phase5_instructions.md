# Phase 5 Implementation Plan

## Step 0: Phase 4 Compliance Fix
**File:** `src/V12_002.cs`
- Increment `BUILD_TAG` to 984

**File:** `src/V12_002.REAPER.Audit.cs`
- Remove lines 74-82 entirely (the `[FSM-AUDIT-ACTIVE]` block).
- KEEP `string expectedKey = ExpKey(acct.Name);` as it is used later.
- The assignment `int expectedQty = fsmExpectedQty;` MUST become the line immediately after `expectedKey`.

Verify Step 0:
```bash
grep -n "FSM-AUDIT-ACTIVE\|legacyExpectedQty" src/V12_002.REAPER.Audit.cs  # must be 0 matches
grep -n "BUILD_TAG" src/V12_002.cs  # must show 984
```

## Step 1: Add HydrateFSMsFromWorkingOrders() to SIMA.Lifecycle.cs
**File:** `src/V12_002.SIMA.Lifecycle.cs`
Add the new method immediately before or after `HydrateWorkingOrdersFromBroker()`.

```csharp
/// <summary>
/// Phase 5: Rebuilds _followerBrackets and _orderIdToFsmKey from already-adopted
/// working orders. Called from HydrateWorkingOrdersFromBroker() before the
/// adoption-complete gate is set. Idempotent -- safe to call on every reconnect.
/// </summary>
private void HydrateFSMsFromWorkingOrders()
{
    int fsmCreated = 0;
    int ordersIndexed = 0;

    foreach (var kvp in entryOrders.ToArray())
    {
        string entryKey = kvp.Key;
        Order entryOrder = kvp.Value;
        if (entryOrder == null) continue;

        // Skip master account entries
        PositionInfo pi;
        if (!activePositions.TryGetValue(entryKey, out pi) || !pi.IsFollower) continue;
        if (pi.ExecutingAccount == null) continue;

        // Idempotent: skip if FSM already exists (safe on repeated reconnects)
        if (_followerBrackets.ContainsKey(entryKey)) continue;

        // Map broker order state to FSM state
        FollowerBracketState hydrationState;
        OrderState entryState = entryOrder.OrderState;
        if (entryState == OrderState.Filled || entryState == OrderState.PartFilled)
            hydrationState = FollowerBracketState.Active;
        else if (entryState == OrderState.Accepted)
            hydrationState = FollowerBracketState.Accepted;
        else if (entryState == OrderState.Working
              || entryState == OrderState.Submitted
              || entryState == OrderState.Initialized
              || entryState == OrderState.ChangePending)
            hydrationState = FollowerBracketState.Submitted;
        else
            continue; // Terminal state -- FSM not needed

        var fsm = new FollowerBracketFSM
        {
            AccountName = pi.ExecutingAccount.Name,
            EntryName = entryKey,
            State = hydrationState,
            LastUpdateUtc = DateTime.UtcNow,
            EntryOrder = entryOrder
        };

        // Link stop order
        Order stopOrd;
        if (stopOrders.TryGetValue(entryKey, out stopOrd) && stopOrd != null)
        {
            fsm.StopOrder = stopOrd;
            if (!string.IsNullOrEmpty(stopOrd.OrderId))
            { _orderIdToFsmKey[stopOrd.OrderId] = entryKey; ordersIndexed++; }
        }

        // Link target orders (match exact property names on FollowerBracketFSM)
        // Engineer: verify the FSM target properties against Symmetry.BracketFSM.cs
        // and link from target1Orders..target5Orders by entryKey using the same pattern.
        Order targetOrd;
        if (target1Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
        {
            fsm.Target1Order = targetOrd;
            if (!string.IsNullOrEmpty(targetOrd.OrderId))
            { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
        }
        if (target2Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
        {
            fsm.Target2Order = targetOrd;
            if (!string.IsNullOrEmpty(targetOrd.OrderId))
            { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
        }
        if (target3Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
        {
            fsm.Target3Order = targetOrd;
            if (!string.IsNullOrEmpty(targetOrd.OrderId))
            { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
        }

        _followerBrackets.TryAdd(entryKey, fsm);

        if (!string.IsNullOrEmpty(entryOrder.OrderId))
        { _orderIdToFsmKey[entryOrder.OrderId] = entryKey; ordersIndexed++; }

        fsmCreated++;
    }

    Print(string.Format("[SIMA] Phase 5 FSM Hydration: {0} FSMs created, {1} order IDs indexed.",
        fsmCreated, ordersIndexed));
}
```

## Step 2: Call Site in HydrateWorkingOrdersFromBroker()
**File:** `src/V12_002.SIMA.Lifecycle.cs`
Add ONE call before the `_orderAdoptionComplete = true` line:

```csharp
// Phase 5: Rebuild FSMs from adopted orders before enabling REAPER
HydrateFSMsFromWorkingOrders();

_orderAdoptionComplete = true;
Print("[SIMA] Order adoption complete -- REAPER enabled.");
```

## Step 3: Build Tag
**File:** `src/V12_002.cs`
Increment `BUILD_TAG` to 985

## Step 4: Self-Audit (Engineer Required)
Run all checks before delivery. All must pass:

```bash
# 1. Phase 4 shadow log fully removed
grep -n "FSM-AUDIT-ACTIVE\|legacyExpectedQty" src/V12_002.REAPER.Audit.cs

# 2. FSM hydration call present in Lifecycle
grep -n "HydrateFSMsFromWorkingOrders" src/V12_002.SIMA.Lifecycle.cs

# 3. Hydration called before _orderAdoptionComplete = true (line order check)
grep -n "HydrateFSMsFromWorkingOrders\|_orderAdoptionComplete = true" src/V12_002.SIMA.Lifecycle.cs

# 4. No new lock() in modified files
grep -n "lock(" src/V12_002.SIMA.Lifecycle.cs src/V12_002.REAPER.Audit.cs

# 5. ASCII gate
grep -Pn "[^\x00-\x7F]" src/V12_002.SIMA.Lifecycle.cs src/V12_002.REAPER.Audit.cs

# 6. Build tag
grep -n "BUILD_TAG" src/V12_002.cs
```
Checks 1, 4, 5 must return ZERO matches. Check 2 must return at least ONE match. Check 3 must show `HydrateFSMsFromWorkingOrders` on a LOWER line number than `_orderAdoptionComplete = true`. Check 6 must show 985.
