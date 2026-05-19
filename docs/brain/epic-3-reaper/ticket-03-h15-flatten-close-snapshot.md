---
# TICKET-03: H15 - Flatten Close Loop Collection Snapshot
# Epic: epic-3-reaper
# Defect: BUG-S4-003
# Priority: HIGH
# Estimated CYC Delta: 0 (no new complexity)
---

## 1. Ticket Summary

**Defect:** H15 - Flatten close loop scans live `targetAcct.Positions` without snapshot  
**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)  
**Location:** Line 720  
**Method:** `ProcessReaperFlatten_ClosePositions()`

**Root Cause:** Direct `foreach` on live `targetAcct.Positions` collection throws `InvalidOperationException` when broker fill callbacks update collection during emergency flatten.

**Fix:** Add `.ToArray()` snapshot before iteration.

---

## 2. Current Code

```csharp
// Line 720
foreach (Position position in targetAcct.Positions)
{
    if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat)
    {
        continue;
    }
    // ... submit market close order
}
```

---

## 3. Surgical Repair

```csharp
// Line 720
// H15-FIX: Snapshot broker positions before iteration to prevent collection-modified exception
// during emergency flatten when broker fill callbacks update positions concurrently.
var accountPositions = targetAcct.Positions.ToArray();
foreach (Position position in accountPositions)
{
    if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat)
    {
        continue;
    }
    // ... submit market close order
}
```

---

## 4. Implementation Steps

### Step 1: Locate Target Code
```bash
# Verify line number
grep -n "foreach (Position position in targetAcct.Positions)" src/V12_002.REAPER.Audit.cs
```
Expected: Line 720

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
grep -n "var accountPositions = targetAcct.Positions.ToArray()" src/V12_002.REAPER.Audit.cs
```
Expected: 1 match at line ~720

---

## 6. Testing

### Unit Test
```csharp
[Test]
public void Test_H15_FlattenClose_ConcurrentPositionModification_CompletesSuccessfully()
{
    // Arrange: Mock targetAcct.Positions with concurrent modification
    // Act: Call ProcessReaperFlatten_ClosePositions() during position update
    // Assert: All positions closed, no exception
}
```

### Manual Verification
1. Trigger emergency flatten with open positions
2. Monitor logs for `InvalidOperationException`
3. Verify all positions are closed with market orders
4. Expected: Zero exceptions, complete position closure

---

## 7. Success Criteria

- ✅ Snapshot line added at line ~720
- ✅ No compile errors
- ✅ Zero new `lock()` statements
- ✅ ASCII gate passes
- ✅ Hard-link sync succeeds
- ✅ Unit test passes
- ✅ Emergency flatten closes all positions without exceptions

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