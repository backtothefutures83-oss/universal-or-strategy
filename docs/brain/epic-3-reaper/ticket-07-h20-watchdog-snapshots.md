---
# TICKET-07: H20 - Queue Overflow Cleanup + Watchdog Snapshots
# Epic: epic-3-reaper
# Defects: BUG-S5-004 (H20) + BUG-S4-004 (Watchdog)
# Priority: MEDIUM (H20) + CRITICAL (Watchdog)
# Estimated CYC Delta: +8-10 (H20 cleanup loop only)
---

## 1. Ticket Summary

**Defect H20:** Queue overflow discards commands without follower cleanup  
**File:** [`src/V12_002.Lifecycle.cs`](src/V12_002.Lifecycle.cs)  
**Location:** Lines 81-86  
**Method:** `DrainQueuesForShutdown()`

**Defect Watchdog:** Collection iteration without snapshots (3 sites)  
**File:** [`src/V12_002.Safety.Watchdog.cs`](src/V12_002.Safety.Watchdog.cs)  
**Locations:** Lines 99, 167, 274

**Root Causes:**
- H20: Overflow commands (51+) dequeued and discarded without executing, leaving followers with stale state
- Watchdog: Direct iteration of `masterAccount.Positions` throws `InvalidOperationException` when broker updates collection

**Fixes:**
- H20: Trigger `CANCEL_ALL` on all fleet accounts when overflow detected
- Watchdog: Add `.ToArray()` snapshots at 3 sites

---

## 2. Current Code - H20 (Lines 81-86)

```csharp
StrategyCommand overflowCmd;
while (_cmdQueue.TryDequeue(out overflowCmd))
    actorOverflow++;

Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds, {1} Actor cmds. Overflow discarded: {2}.",
    ipcDrained, actorDrained, actorOverflow));
```

---

## 3. Surgical Repair - H20

```csharp
// H20-FIX: Trigger CANCEL_ALL on all fleet accounts when overflow detected.
// Discarded commands may include follower bracket submissions, leaving followers
// with open positions but no protection orders. CANCEL_ALL ensures clean state.
StrategyCommand overflowCmd;
while (_cmdQueue.TryDequeue(out overflowCmd))
    actorOverflow++;

if (actorOverflow > 0)
{
    Print(string.Format("[SHUTDOWN] Overflow detected: {0} commands discarded. Triggering fleet CANCEL_ALL for safety.", actorOverflow));
    
    // Enqueue CANCEL_ALL for each fleet account to ensure clean shutdown state
    if (EnableSIMA && activeFleetAccounts != null)
    {
        foreach (var kvp in activeFleetAccounts.ToArray())
        {
            if (kvp.Value) // Account is enabled
            {
                try
                {
                    string accountName = kvp.Key;
                    Account fleetAcct = Account.All.FirstOrDefault(a => a.Name == accountName);
                    if (fleetAcct != null)
                    {
                        // Cancel all working orders for this account
                        var workingOrders = fleetAcct.Orders.ToArray()
                            .Where(o => o != null && o.Instrument?.FullName == Instrument?.FullName &&
                                       !IsOrderTerminal(o.OrderState))
                            .ToArray();
                        
                        if (workingOrders.Length > 0)
                        {
                            fleetAcct.Cancel(workingOrders);
                            Print(string.Format("[SHUTDOWN] Overflow cleanup: Cancelled {0} orders on {1}", 
                                workingOrders.Length, accountName));
                        }
                    }
                }
                catch (Exception exCleanup)
                {
                    Print("[SHUTDOWN] Overflow cleanup failed for " + kvp.Key + ": " + exCleanup.Message);
                }
            }
        }
    }
}

Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds, {1} Actor cmds. Overflow discarded: {2}.",
    ipcDrained, actorDrained, actorOverflow));
```

---

## 4. Current Code - Watchdog Site 1 (Line 99)

```csharp
foreach (Position position in masterAccount.Positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition != MarketPosition.Flat)
        return true;
}
```

---

## 5. Surgical Repair - Watchdog Site 1

```csharp
// WATCHDOG-FIX: Snapshot positions before iteration to prevent InvalidOperationException
// when broker fill callbacks update Positions collection from UI thread during watchdog check.
var positions = masterAccount.Positions.ToArray();
foreach (Position position in positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition != MarketPosition.Flat)
        return true;
}
```

---

## 6. Current Code - Watchdog Site 2 (Line 167)

```csharp
foreach (Position position in masterAccount.Positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit flatten order
}
```

---

## 7. Surgical Repair - Watchdog Site 2

```csharp
// WATCHDOG-FIX: Snapshot positions before iteration (same pattern as Site 1).
var positions = masterAccount.Positions.ToArray();
foreach (Position position in positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit flatten order
}
```

---

## 8. Current Code - Watchdog Site 3 (Line 274)

```csharp
foreach (Position position in masterAccount.Positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit direct fallback order
}
```

---

## 9. Surgical Repair - Watchdog Site 3

```csharp
// WATCHDOG-FIX: Snapshot positions before iteration (same pattern as Sites 1-2).
var positions = masterAccount.Positions.ToArray();
foreach (Position position in positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit direct fallback order
}
```

---

## 10. Implementation Steps

### Step 1: H20 - Add Overflow Cleanup (Lifecycle.cs)
```bash
# Verify line number
grep -n "while (_cmdQueue.TryDequeue(out overflowCmd))" src/V12_002.Lifecycle.cs
```
Expected: Line 81

Use `apply_diff` to add cleanup logic after overflow loop.

### Step 2: Watchdog - Add Snapshots (3 sites)
```bash
# Locate all 3 sites
grep -n "foreach (Position position in masterAccount.Positions)" src/V12_002.Safety.Watchdog.cs
```
Expected: Lines 99, 167, 274

Use `apply_diff` to add snapshot before each `foreach` loop.

### Step 3: Verify Syntax
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS

---

## 11. Self-Audit Checklist

### DNA Compliance
```bash
grep -n "lock(" src/V12_002.Lifecycle.cs
grep -n "lock(" src/V12_002.Safety.Watchdog.cs
```
Expected: 0 matches

```bash
grep -Prn "[^\x00-\x7F]" src/V12_002.Lifecycle.cs
grep -Prn "[^\x00-\x7F]" src/V12_002.Safety.Watchdog.cs
```
Expected: 0 matches

### Hard-Link Sync
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS + ASCII gate PASS

### Verification - H20
```bash
grep -n "Overflow cleanup" src/V12_002.Lifecycle.cs
```
Expected: 2 matches (log messages)

### Verification - Watchdog
```bash
grep -n "var positions = masterAccount.Positions.ToArray()" src/V12_002.Safety.Watchdog.cs
```
Expected: 3 matches (lines ~99, ~167, ~274)

### CYC Check
```bash
python scripts/complexity_audit.py
```
Expected: `DrainQueuesForShutdown()` CYC increased by ~8-10 (acceptable)

---

## 12. Testing

### Unit Test - H20
```csharp
[Test]
public void Test_H20_QueueOverflow_TriggersFollowerCleanup()
{
    // Arrange: 100 commands in queue
    // Act: Call DrainQueuesForShutdown()
    // Assert: CANCEL_ALL triggered for all fleet accounts
}
```

### Unit Test - Watchdog
```csharp
[Test]
public void Test_Watchdog_ConcurrentPositionModification_NoException()
{
    // Arrange: Mock masterAccount.Positions with concurrent modification
    // Act: Call HasWatchdogLeadAccountPosition() during position update
    // Assert: No InvalidOperationException thrown
}
```

### Manual Verification
1. H20: Enqueue 100 commands, shutdown, verify follower orders cancelled
2. Watchdog: Run strategy for 1 hour, monitor for watchdog exceptions
3. Expected: Zero exceptions, clean shutdown

---

## 13. Success Criteria

- ✅ H20 cleanup logic added to Lifecycle.cs
- ✅ All 3 Watchdog snapshots added
- ✅ No compile errors
- ✅ Zero new `lock()` statements
- ✅ ASCII gate passes
- ✅ Hard-link sync succeeds
- ✅ CYC increase acceptable (+8-10)
- ✅ Unit tests pass
- ✅ Zero watchdog exceptions in production
- ✅ Follower cleanup triggered on overflow

---

## 14. Rollback Plan

```bash
git checkout HEAD~1 -- src/V12_002.Lifecycle.cs
git checkout HEAD~1 -- src/V12_002.Safety.Watchdog.cs
powershell -File .\deploy-sync.ps1
```

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** Should run AFTER Ticket 05 (H17) for correct teardown sequence  
**Estimated Time:** 25 minutes (2 files, 4 edits)  
**Risk Level:** LOW (Watchdog) + MEDIUM (H20 cleanup loop adds CYC)