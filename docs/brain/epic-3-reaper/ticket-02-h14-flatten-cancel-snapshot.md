---
# TICKET-02: H14 - Flatten Cancel Loop Collection Snapshot
# Epic: epic-3-reaper
# Defect: BUG-S4-002
# Priority: HIGH
# Estimated CYC Delta: 0 (no new complexity)
---

## 1. Ticket Summary

**Defect:** H14 - Flatten cancel loop scans live `targetAcct.Orders` without snapshot  
**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)  
**Location:** Line 698  
**Method:** `ProcessReaperFlatten_CancelWorkingOrders()`

**Root Cause:** Direct `foreach` on live `targetAcct.Orders` collection throws `InvalidOperationException` when broker callbacks update collection during emergency flatten.

**Fix:** Add `.ToArray()` snapshot before iteration.

---

## 2. Current Code

```csharp
// Line 698
List<Order> ordersToCancel = new List<Order>();
foreach (Order order in targetAcct.Orders)
{
    if (order != null && order.Instrument.FullName == Instrument.FullName &&
        (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
         order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending))
    {
        ordersToCancel.Add(order);
    }
}
```

---

## 3. Surgical Repair

```csharp
// Line 698
// H14-FIX: Snapshot broker orders before iteration to prevent collection-modified exception
// during emergency flatten when broker callbacks update order states concurrently.
List<Order> ordersToCancel = new List<Order>();
var accountOrders = targetAcct.Orders.ToArray();
foreach (Order order in accountOrders)
{
    if (order != null && order.Instrument.FullName == Instrument.FullName &&
        (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
         order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending))
    {
        ordersToCancel.Add(order);
    }
}
```

---

## 4. Implementation Steps

### Step 1: Locate Target Code
```bash
# Verify line number
grep -n "foreach (Order order in targetAcct.Orders)" src/V12_002.REAPER.Audit.cs
```
Expected: Line 698

### Step 2: Apply Surgical Edit
Use `apply_diff` tool to add snapshot line before the `foreach` loop.

### Step 3: Verify Syntax
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS

---

## 5. Self-Audit Checklist

### DNA Compliance
```bash
grep -n "lock(" src/V12_002.REAPER.Audit.cs
```
Expected: 0 matches

```bash
grep -Prn "[^\x00-\x7F]" src/V12_002.REAPER.Audit.cs
```
Expected: 0 matches

### Hard-Link Sync
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS + ASCII gate PASS

### Verification
```bash
grep -n "var accountOrders = targetAcct.Orders.ToArray()" src/V12_002.REAPER.Audit.cs
```
Expected: 1 match at line ~699

---

## 6. Testing

### Unit Test
```csharp
[Test]
public void Test_H14_FlattenCancel_ConcurrentOrderModification_CompletesSuccessfully()
{
    // Arrange: Mock targetAcct.Orders with concurrent modification
    // Act: Call ProcessReaperFlatten_CancelWorkingOrders() during order update
    // Assert: All orders cancelled, no exception
}
```

### Manual Verification
1. Trigger emergency flatten during active trading
2. Monitor logs for `InvalidOperationException`
3. Verify all working orders are cancelled
4. Expected: Zero exceptions, complete flatten

---

## 7. Success Criteria

- ✅ Snapshot line added at line ~699
- ✅ No compile errors
- ✅ Zero new `lock()` statements
- ✅ ASCII gate passes
- ✅ Hard-link sync succeeds
- ✅ Unit test passes
- ✅ Emergency flatten completes without exceptions

---

## 8. Rollback Plan

```bash
git checkout HEAD~1 -- src/V12_002.REAPER.Audit.cs
powershell -File .\deploy-sync.ps1
```

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** None (independent of other tickets)  
**Estimated Time:** 5 minutes  
**Risk Level:** LOW (proven pattern, critical path hardening)