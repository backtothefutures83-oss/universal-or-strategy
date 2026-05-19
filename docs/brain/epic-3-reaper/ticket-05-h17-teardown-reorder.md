---
# TICKET-05: H17 - Teardown Sequence Reorder + Termination Guards
# Epic: epic-3-reaper
# Defect: BUG-S5-001
# Priority: CRITICAL
# Estimated CYC Delta: 0 (no new complexity)
---

## 1. Ticket Summary

**Defect:** H17 - Teardown sequence allows ghost orders (cancel runs before drain)  
**Files:**
- [`src/V12_002.Lifecycle.cs`](src/V12_002.Lifecycle.cs) - Lines 102-143
- [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs) - 4 enqueue methods

**Root Cause:** `CancelAllV12GtcOrders()` runs BEFORE `DrainQueuesForShutdown()`. Drained commands can submit new orders that bypass the cancel sweep, creating ghost orders.

**Fix:**
1. Reorder shutdown sequence: Stop intake → Drain → Cancel
2. Add termination guards to 4 REAPER enqueue methods

---

## 2. Current Code - Lifecycle.cs (Lines 102-143)

```csharp
private void ShutdownUiAndServices()
{
    _configureComplete = false;
    _dataLoadedComplete = false;
    Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

    StopPanelRefresh();

    if (ChartControl != null)
    {
        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            if (!_isTerminating) return;
            DetachHotkeys();
            DetachChartClickHandler();
            DestroyPanel();
        });
    }

    // [BUILD 984] GTC Cancel Sweep
    Print(string.Format("[SHUTDOWN] GTC sweep: cancelling {0} tracked + broker-scanned orders",
        (entryOrders?.Count ?? 0) + (stopOrders?.Count ?? 0)));
    CancelAllV12GtcOrders(false);  // Line 128

    DrainQueuesForShutdown();       // Line 130
    EmitMetricsSummary();

    StopIpcServer();                // Line 134
    StopReaperAudit();              // Line 137
    UnsubscribeFromFleetAccounts();
}
```

---

## 3. Surgical Repair - Lifecycle.cs

```csharp
private void ShutdownUiAndServices()
{
    _configureComplete = false;
    _dataLoadedComplete = false;
    Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

    StopPanelRefresh();

    if (ChartControl != null)
    {
        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            if (!_isTerminating) return;
            DetachHotkeys();
            DetachChartClickHandler();
            DestroyPanel();
        });
    }

    // H17-FIX: Stop intake BEFORE draining queues to prevent new commands from entering.
    // This ensures DrainQueuesForShutdown processes a bounded set of commands.
    StopIpcServer();
    StopReaperAudit();

    // H17-FIX: Drain queues BEFORE cancel sweep so any queued order submissions are executed
    // and then included in the subsequent cancel sweep. This prevents ghost orders that would
    // bypass the cancel sweep if submitted after it runs.
    DrainQueuesForShutdown();

    // [BUILD 984] GTC Cancel Sweep -- cancel all tracked/broker V12 orders after drain.
    // Now sweeps ALL orders including any submitted during drain.
    Print(string.Format("[SHUTDOWN] GTC sweep: cancelling {0} tracked + broker-scanned orders",
        (entryOrders?.Count ?? 0) + (stopOrders?.Count ?? 0)));
    CancelAllV12GtcOrders(false);

    EmitMetricsSummary();
    UnsubscribeFromFleetAccounts();
}
```

---

## 4. Termination Guards - REAPER.Audit.cs (4 Methods)

### Guard 1: EnqueueReaperRepairCandidate() (Line ~320)

**Add at method start:**
```csharp
private bool EnqueueReaperRepairCandidate(Account acct, int expectedQty, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

### Guard 2: EnqueueReaperNakedStopCandidate() (Line ~400)

**Add at method start:**
```csharp
private bool EnqueueReaperNakedStopCandidate(Account acct, Position pos, DateTime firstSeen, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

### Guard 3: EnqueueReaperMasterNakedStop() (Line ~600)

**Add at method start:**
```csharp
private bool EnqueueReaperMasterNakedStop(Position masterPos, DateTime masterFirstSeen, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

### Guard 4: EnqueueReaperFlattenCandidate() (Line ~360)

**Add at method start:**
```csharp
private bool EnqueueReaperFlattenCandidate(Account acct, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

---

## 5. Implementation Steps

### Step 1: Reorder Lifecycle.cs
```bash
# Verify current order
grep -n "CancelAllV12GtcOrders\|DrainQueuesForShutdown\|StopIpcServer\|StopReaperAudit" src/V12_002.Lifecycle.cs
```
Expected: Cancel at 128, Drain at 130, StopIpc at 134, StopReaper at 137

Use `apply_diff` to reorder: StopIpc → StopReaper → Drain → Cancel

### Step 2: Add Termination Guards (4 methods)
```bash
# Locate all 4 enqueue methods
grep -n "private bool EnqueueReaper" src/V12_002.REAPER.Audit.cs
```
Expected: 4 matches

Use `insert_content` to add guard at the start of each method body.

### Step 3: Verify Syntax
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS

---

## 6. Self-Audit Checklist

### DNA Compliance
```bash
grep -n "lock(" src/V12_002.Lifecycle.cs
grep -n "lock(" src/V12_002.REAPER.Audit.cs
```
Expected: 0 matches

```bash
grep -Prn "[^\x00-\x7F]" src/V12_002.Lifecycle.cs
grep -Prn "[^\x00-\x7F]" src/V12_002.REAPER.Audit.cs
```
Expected: 0 matches

### Hard-Link Sync
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS + ASCII gate PASS

### Verification - Lifecycle Sequence
```bash
# Verify new order
grep -n "StopIpcServer\|StopReaperAudit\|DrainQueuesForShutdown\|CancelAllV12GtcOrders" src/V12_002.Lifecycle.cs
```
Expected: StopIpc → StopReaper → Drain → Cancel (in that order)

### Verification - Termination Guards
```bash
# Confirm all 4 guards added
grep -n "if (_isTerminating) return false;" src/V12_002.REAPER.Audit.cs
```
Expected: 4 matches (one per enqueue method)

---

## 7. Testing

### Unit Test
```csharp
[Test]
public void Test_H17_Shutdown_DrainBeforeCancel_NoGhostOrders()
{
    // Arrange: Queue contains order submission command
    // Act: Call ShutdownUiAndServices()
    // Assert: Order submitted during drain is cancelled by sweep
}
```

### Manual Verification
1. Start strategy, submit 100 orders, shutdown immediately
2. Query broker for unmanaged orders after shutdown
3. Repeat 100 times
4. Expected: Zero ghost orders across all iterations

---

## 8. Success Criteria

- ✅ Lifecycle sequence reordered: Stop intake → Drain → Cancel
- ✅ All 4 termination guards added to REAPER enqueue methods
- ✅ No compile errors
- ✅ Zero new `lock()` statements
- ✅ ASCII gate passes
- ✅ Hard-link sync succeeds
- ✅ Unit test passes
- ✅ Zero ghost orders in production

---

## 9. Rollback Plan

```bash
git checkout HEAD~1 -- src/V12_002.Lifecycle.cs
git checkout HEAD~1 -- src/V12_002.REAPER.Audit.cs
powershell -File .\deploy-sync.ps1
```

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** None (independent of other tickets)  
**Estimated Time:** 20 minutes (2 files, 5 edits)  
**Risk Level:** MEDIUM (lifecycle ordering change, but well-tested pattern)