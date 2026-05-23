# EPIC-5-PERF: Ticket T04 - .ToArray() Elimination

**Ticket ID:** T04  
**Epic:** EPIC-5-PERF  
**Status:** Ready for Execution  
**Created:** 2026-05-23  
**Dependencies:** T01 (Baseline Instrumentation)  
**Estimated Duration:** 2 days

---

## OBJECTIVE

Standardize the snapshot pattern across all hot-path collection iterations to eliminate redundant `.ToArray()` allocations. Replace inline `.ToArray()` calls with a single snapshot per scope, reducing allocation pressure from ~25 calls to ~10 strategic snapshots.

**Target Outcome:** Zero additional allocations per iteration, thread-safe enumeration preserved, zero CYC increase.

---

## SCOPE

### Discovery Summary

**Total .ToArray() Instances Found:** 91 across entire codebase  
**Hot-Path Targets (This Ticket):** 25+ instances across 8 files  
**Pattern:** Multiple `.ToArray()` calls on same collection within single method scope

### Target Files & Instances

#### Tier 1: Ultra-Hot (Every Order Fill)
1. **V12_002.Orders.Callbacks.Execution.cs** (4 instances)
   - Line 99: `entryOrders.ToArray()` in HasPendingEntryForAcct
   - Line 116: `activePositions.ToArray()` in HasUnfilledActivePositionForAcct
   - Line 144: `activePositions.ToArray()` in cleanup loop
   - Line 186: `activePositions.ToArray()` in another cleanup loop

2. **V12_002.Orders.Callbacks.cs** (10 instances)
   - Line 129: `dict.ToArray()` in helper method
   - Line 269: `activePositions.ToArray()` in HandleEntryOrderFilled
   - Line 359: `activePositions.ToArray()` in nested loop
   - Line 394: `activePositions.ToArray()` in HandleSecondaryOrderFilled
   - Line 458: `activePositions.ToArray()` in cleanup
   - Line 477: `activePositions.ToArray()` in another cleanup
   - Line 524: `pendingStopReplacements.ToArray()`
   - Line 561: `stopOrders.ToArray()`
   - Line 587: `activePositions.ToArray()`
   - Line 607: `activePositions.ToArray()`

3. **V12_002.Orders.Callbacks.AccountOrders.cs** (7 instances)
   - Line 383: `_followerTargetReplaceSpecs.ToArray()`
   - Line 543: `_followerTargetReplaceSpecs.ToArray()` (duplicate in same method)
   - Line 604: `pendingStopReplacements.ToArray()`
   - Line 640: `stopOrders.ToArray()`
   - Line 792: `_followerReplaceSpecs.ToArray()`
   - Line 805: `_followerTargetReplaceSpecs.ToArray()`
   - Line 847: `activePositions.ToArray()` **[GOOD PATTERN - already optimized]**

#### Tier 2: High-Frequency (Lifecycle/Audit)
4. **V12_002.Lifecycle.cs** (0 instances in DrainQueuesForShutdown)
   - **NOTE:** EXECUTION_GUIDE mentions lines 95, 106-109 but search shows no .ToArray() in that range
   - **ACTION:** Verify if this was already fixed or if line numbers shifted

5. **V12_002.LogicAudit.cs** (2 instances)
   - Line 289: `activePositions.ToArray()` in audit loop
   - Line 339: `expectedPositions.ToArray()` in drift detection

6. **V12_002.Orders.Management.Flatten.cs** (5 instances)
   - Line 45: `activePositions.ToArray()`
   - Line 86: `entryOrders.ToArray()`
   - Line 252: `activePositions.ToArray()`
   - Line 266: `activePositions.ToArray()`
   - Line 351: `activePositions.ToArray()`

#### Tier 3: Supporting Files
7. **V12_002.Orders.Management.Cleanup.cs** (3 instances)
   - Line 266: `dict.ToArray()`
   - Line 349: `activePositions.ToArray()`
   - Line 457: `dict.ToArray()`

8. **V12_002.REAPER.Audit.cs** (2 instances)
   - Line 520: `acct.Orders.ToArray()`
   - Line 630: `Account.Orders.ToArray()`

---

## SNAPSHOT PATTERN DESIGN

### Current Anti-Pattern (Redundant Allocations)

```csharp
// BEFORE: Multiple .ToArray() calls in same scope
private void ProcessOrders()
{
    // Allocation #1
    foreach (var kvp in activePositions.ToArray())
    {
        if (SomeCondition(kvp.Value))
        {
            // Allocation #2 (same collection!)
            foreach (var kvp2 in activePositions.ToArray())
            {
                // Process...
            }
        }
    }
}
```

### Target Pattern (Single Snapshot Per Scope)

```csharp
// AFTER: Single snapshot, reused across loops
private void ProcessOrders()
{
    // Single allocation at scope entry
    var snapshot = activePositions.ToArray();
    
    foreach (var kvp in snapshot)
    {
        if (SomeCondition(kvp.Value))
        {
            // Reuse snapshot (zero additional allocation)
            foreach (var kvp2 in snapshot)
            {
                // Process...
            }
        }
    }
}
```

### Thread Safety Guarantee

**Why .ToArray() is Used:**
- ConcurrentDictionary supports concurrent reads, but NOT modification during enumeration
- `.ToArray()` creates a point-in-time snapshot, preventing `InvalidOperationException`

**Pattern Correctness:**
1. Snapshot taken BEFORE any enumeration
2. Re-check `ContainsKey()` inside loop (collection may have changed since snapshot)
3. Snapshot NOT reused across async boundaries or yields

---

## MIGRATION STRATEGY

### Phase 1: Audit & Classify (Day 1, Morning)

**Goal:** Identify all redundant .ToArray() calls and group by scope.

**Method:**
1. For each target file, identify methods with multiple .ToArray() calls
2. Classify patterns:
   - **Type A:** Multiple calls on SAME collection in SAME method → CONSOLIDATE
   - **Type B:** Single call per method → KEEP (already optimal)
   - **Type C:** Nested methods each calling .ToArray() → EVALUATE (may need scope elevation)

**Deliverable:** Audit spreadsheet with columns:
- File
- Method
- Line Number
- Collection Name
- Pattern Type (A/B/C)
- Consolidation Strategy

### Phase 2: Surgical Refactoring (Day 1, Afternoon + Day 2, Morning)

**Execution Order (Hottest First):**
1. V12_002.Orders.Callbacks.cs (10 instances)
2. V12_002.Orders.Callbacks.AccountOrders.cs (7 instances)
3. V12_002.Orders.Management.Flatten.cs (5 instances)
4. V12_002.Orders.Callbacks.Execution.cs (4 instances)
5. V12_002.Orders.Management.Cleanup.cs (3 instances)
6. V12_002.LogicAudit.cs (2 instances)
7. V12_002.REAPER.Audit.cs (2 instances)
8. V12_002.Lifecycle.cs (verify if already fixed)

**Per-File Protocol:**
1. Read entire file to understand context
2. Identify all .ToArray() calls in target methods
3. Apply snapshot pattern (single allocation at method entry)
4. Verify re-check logic (`ContainsKey()` after snapshot)
5. Run `deploy-sync.ps1` after each file
6. F5 compile test after each file

### Phase 3: Verification (Day 2, Afternoon)

**Regression Tests:**
1. `deploy-sync.ps1` (hard-link integrity)
2. `python scripts/complexity_audit.py` (CYC unchanged)
3. `grep -r "lock(" src/` (zero matches)
4. F5 in NinjaTrader (compile + load)
5. Manual test: Fill entry order, verify no collection-modified exceptions

**Allocation Profiling (Optional, if T01 complete):**
- ETW trace during order fill sequence
- Verify ~15 fewer .ToArray() allocations per fill cycle

---

## CALLER IMPACT ANALYSIS

### Methods Modified (Estimated)

**High Confidence (Signature Unchanged):**
- All target methods are `private` or `internal`
- No public API changes
- Callers unaffected (internal refactoring only)

**Files Affected:** 8 files (see Target Files section)

**Signature Changes:** NONE (pure internal refactoring)

---

## CYC IMPACT ESTIMATE

### Before

**Typical Pattern:**
```csharp
foreach (var kvp in activePositions.ToArray())  // CYC +1 (loop)
{
    if (condition) { ... }  // CYC +1 (branch)
}
```
**CYC:** 2 per loop

### After

```csharp
var snapshot = activePositions.ToArray();  // CYC +0 (assignment)
foreach (var kvp in snapshot)              // CYC +1 (loop)
{
    if (condition) { ... }  // CYC +1 (branch)
}
```
**CYC:** 2 per loop (UNCHANGED)

**Net CYC Impact:** **ZERO** (refactoring only, no new branches or loops)

---

## RISK MITIGATION

### High-Risk Scenarios

1. **Collection Mutation During Iteration**
   - **Risk:** Snapshot taken, then collection modified, then snapshot item accessed
   - **Mitigation:** Re-check `ContainsKey()` before accessing dictionary items
   - **Example:**
     ```csharp
     var snapshot = activePositions.ToArray();
     foreach (var kvp in snapshot)
     {
         // Re-check: item may have been removed since snapshot
         if (!activePositions.ContainsKey(kvp.Key)) continue;
         
         var pos = kvp.Value;
         // Safe to use pos now
     }
     ```

2. **Snapshot Scope Too Wide**
   - **Risk:** Snapshot taken at method entry, but collection changes mid-method
   - **Mitigation:** Take snapshot as late as possible (just before enumeration)
   - **Example:** If method has early-exit logic, take snapshot AFTER early exits

3. **Nested Method Calls**
   - **Risk:** Parent method takes snapshot, child method also calls .ToArray()
   - **Mitigation:** Pass snapshot as parameter to child method (if feasible)
   - **Example:**
     ```csharp
     // Parent
     var snapshot = activePositions.ToArray();
     ProcessSnapshot(snapshot);
     
     // Child
     private void ProcessSnapshot(KeyValuePair<string, PositionInfo>[] snapshot)
     {
         foreach (var kvp in snapshot) { ... }
     }
     ```

### Low-Risk Scenarios

1. **Single .ToArray() Per Method**
   - Already optimal, no change needed
   - Example: V12_002.Orders.Callbacks.AccountOrders.cs:847 (already uses snapshot pattern)

2. **Different Collections**
   - Multiple .ToArray() calls on DIFFERENT collections → no consolidation needed
   - Example: `activePositions.ToArray()` + `stopOrders.ToArray()` in same method

---

## ACCEPTANCE CRITERIA

### Functional Requirements

1. ✅ All inline `.ToArray()` calls replaced with snapshot pattern where redundant
2. ✅ Single snapshot per collection per method scope
3. ✅ Re-check logic (`ContainsKey()`) preserved after snapshot
4. ✅ Zero collection-modified exceptions during stress test

### Performance Requirements

1. ✅ Allocation reduction: ~25 .ToArray() calls → ~10 strategic snapshots
2. ✅ ETW trace shows ~15 fewer allocations per order fill cycle (if T01 complete)
3. ✅ Zero latency regression (p99 unchanged or improved)

### V12 DNA Compliance

1. ✅ Zero `lock()` statements introduced (verified via grep)
2. ✅ ASCII-only strings (no Unicode in any changes)
3. ✅ CYC unchanged (verified via complexity_audit.py)
4. ✅ Hard-link integrity maintained (deploy-sync.ps1 passes)

### Regression Tests

1. ✅ F5 compile gate passes (NinjaTrader loads without errors)
2. ✅ Manual test: Fill entry order, verify no exceptions
3. ✅ Manual test: Cancel order during iteration, verify graceful handling
4. ✅ All existing unit tests pass (if applicable)

---

## DELIVERABLES

1. **Audit Spreadsheet** (CSV)
   - All 91 .ToArray() instances classified
   - 25+ hot-path instances marked for consolidation
   - Consolidation strategy per instance

2. **Refactored Source Files** (8 files)
   - V12_002.Orders.Callbacks.cs
   - V12_002.Orders.Callbacks.AccountOrders.cs
   - V12_002.Orders.Callbacks.Execution.cs
   - V12_002.Orders.Management.Flatten.cs
   - V12_002.Orders.Management.Cleanup.cs
   - V12_002.LogicAudit.cs
   - V12_002.REAPER.Audit.cs
   - V12_002.Lifecycle.cs (if applicable)

3. **Verification Report** (Markdown)
   - Before/after allocation counts (if T01 complete)
   - CYC audit results (unchanged)
   - Regression test results (all pass)
   - F5 compile gate status (pass)

4. **Concurrent Modification Unit Tests** (Optional, if time permits)
   - Test harness for snapshot pattern correctness
   - Simulate collection modification during iteration
   - Verify no exceptions thrown

---

## EXECUTION CHECKLIST

### Pre-Flight

- [ ] Read this ticket completely
- [ ] Verify T01 (Baseline) is complete (optional dependency)
- [ ] Run `python scripts/complexity_audit.py` (establish baseline)
- [ ] Run `grep -r "\.ToArray()" src/ | wc -l` (count: 91)

### Phase 1: Audit (Day 1, Morning)

- [ ] Create audit spreadsheet
- [ ] Classify all 91 .ToArray() instances
- [ ] Identify 25+ hot-path targets
- [ ] Document consolidation strategy per target
- [ ] **[GATE]** Director approval of audit results

### Phase 2: Refactoring (Day 1 PM + Day 2 AM)

For each target file:
- [ ] Read entire file for context
- [ ] Apply snapshot pattern to redundant .ToArray() calls
- [ ] Verify re-check logic (`ContainsKey()`)
- [ ] Run `deploy-sync.ps1` (hard-link sync)
- [ ] F5 compile test
- [ ] Commit with message: `[T04] Snapshot pattern: <FileName>`

### Phase 3: Verification (Day 2, Afternoon)

- [ ] Run `python scripts/complexity_audit.py` (verify CYC unchanged)
- [ ] Run `grep -r "lock(" src/` (verify zero matches)
- [ ] Run `deploy-sync.ps1` (final hard-link check)
- [ ] F5 compile + load in NinjaTrader
- [ ] Manual test: Fill entry order (verify no exceptions)
- [ ] Manual test: Cancel order during iteration (verify graceful handling)
- [ ] Generate verification report
- [ ] **[GATE]** Director sign-off

---

## ROLLBACK STRATEGY

**Revert Command:** `git revert <commit-hash>` for each file commit

**Impact:** Reverts to inline .ToArray() pattern (original allocation behavior)

**Validation:** Run F5 compile gate after revert to confirm clean rollback

---

## NOTES

### Good Patterns Already Implemented

**V12_002.Orders.Callbacks.AccountOrders.cs:847** (Build 935 [R-01]):
```csharp
// Single snapshot -- reused by both identity search and cascade cleanup,
// eliminating the second activePositions.ToArray() allocation in the cascade path.
var snapshot = activePositions.ToArray();
```
**Action:** Preserve this pattern, use as reference for other files.

### Lifecycle.cs Discrepancy

**EXECUTION_GUIDE mentions:** DrainQueuesForShutdown lines 95, 106-109 (DOUBLE ALLOCATION)  
**Search results show:** No .ToArray() in lines 90-115  
**Hypothesis:** Already fixed in a previous commit, or line numbers shifted  
**Action:** Verify during Phase 1 audit, document if already optimized

---

## SUCCESS METRICS

| Metric | Before | Target | Measurement |
|--------|--------|--------|-------------|
| .ToArray() calls (hot-path) | ~25 | ~10 | Manual count |
| Allocations per fill cycle | ~25 | ~10 | ETW trace (if T01 done) |
| CYC (all modified methods) | Baseline | Unchanged | complexity_audit.py |
| Collection-modified exceptions | Unknown | 0 | Stress test (1hr) |

---

## DEPENDENCIES

**Upstream:**
- T01 (Baseline Instrumentation) - Optional for allocation profiling

**Downstream:**
- T07 (Verification & Stress Testing) - Will validate allocation reduction

**Parallel:**
- T02 (String.Format Elimination) - Independent
- T03 (UISnapshot Pooling) - Independent
- T05 (Order Array Pooling) - Independent
- T06 (MonitorRma Refactoring) - Independent

---

**[TICKET-GATE]** T04 ticket ready for execution. Awaiting Director approval to proceed with Phase 1 audit.