---
# TICKET-01: H13 - Naked Position Audit Collection Snapshot
# Epic: epic-3-reaper
# Defect: BUG-S4-001
# Priority: HIGH
# Estimated CYC Delta: 0 (no new complexity)
---

## 1. Ticket Summary

**Defect:** H13 - Naked Position Audit scans live `Account.Orders` without snapshot  
**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)  
**Location:** Line 522  
**Method:** `AuditMaster_HandleNakedPosition()`

**Root Cause:** Direct LINQ `.Any()` on live `Account.Orders` collection throws `InvalidOperationException` when broker updates collection during iteration.

**Fix:** Add `.ToArray()` snapshot before iteration (mirrors existing pattern at line 378).

---

## 2. Current Code

```csharp
// Line 522
bool masterHasWorkingStop = Account.Orders.Any(o =>
    o.Instrument?.FullName == Instrument?.FullName &&
    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
```

---

## 3. Surgical Repair

```csharp
// Line 522
// H13-FIX: Snapshot broker orders before iteration to prevent InvalidOperationException
// when NinjaTrader updates Account.Orders collection from UI thread during audit.
var masterOrders = Account.Orders.ToArray();
bool masterHasWorkingStop = masterOrders.Any(o =>
    o.Instrument?.FullName == Instrument?.FullName &&
    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
```

---

## 4. Implementation Steps

### Step 1: Locate Target Code
```bash
# Verify line number
grep -n "bool masterHasWorkingStop = Account.Orders.Any" src/V12_002.REAPER.Audit.cs
```
Expected: Line 522

### Step 2: Apply Surgical Edit
Use `apply_diff` tool to add snapshot line before the `.Any()` call.

### Step 3: Verify Syntax
```bash
# Compile check
powershell -File .\deploy-sync.ps1
```
Expected: PASS (no compile errors)

---

## 5. Self-Audit Checklist

After implementation, run these checks:

### DNA Compliance
```bash
# Lock regression check
grep -n "lock(" src/V12_002.REAPER.Audit.cs
```
Expected: 0 matches

```bash
# ASCII compliance check
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
# Confirm snapshot added
grep -n "var masterOrders = Account.Orders.ToArray()" src/V12_002.REAPER.Audit.cs
```
Expected: 1 match at line ~522

---

## 6. Testing

### Unit Test
```csharp
[Test]
public void Test_H13_NakedPositionAudit_ConcurrentOrderModification_NoException()
{
    // Arrange: Mock Account.Orders with concurrent modification
    // Act: Call AuditMaster_HandleNakedPosition() during order update
    // Assert: No InvalidOperationException thrown
}
```

### Manual Verification
1. Run strategy under live market conditions
2. Monitor logs for `InvalidOperationException` during REAPER audit
3. Expected: Zero exceptions over 1-hour test

---

## 7. Success Criteria

- ✅ Snapshot line added at line ~522
- ✅ No compile errors
- ✅ Zero new `lock()` statements
- ✅ ASCII gate passes
- ✅ Hard-link sync succeeds
- ✅ Unit test passes
- ✅ No `InvalidOperationException` in production logs

---

## 8. Rollback Plan

```bash
git checkout HEAD~1 -- src/V12_002.REAPER.Audit.cs
powershell -File .\deploy-sync.ps1
```

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** None (can execute first)  
**Estimated Time:** 5 minutes  
**Risk Level:** LOW (proven pattern, minimal change)