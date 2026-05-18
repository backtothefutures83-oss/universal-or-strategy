# Epic: epic-1-dna -- Scope Alignment

## Code Area

**Target Files:**
- `src/V12_002.Orders.Management.StopSync.cs` - Stop order registration and synchronization
- `src/V12_002.Symmetry.Follower.cs` - Follower bracket submission
- `src/V12_002.Entries.Retest.cs` - Retest entry rollback logic
- `src/V12_002.Trailing.StopUpdate.cs` - Stop replacement during trailing updates

**Specific Methods:**
1. **H05 (BUG-S2-001)**: `CreateNewStopOrder()` in StopSync.cs (line 295-339)
   - Current: Uses `Enqueue` to write stop order to `stopOrders` dictionary (line 320)
   - Issue: Asynchronous registration creates ghost-order window during shutdown

2. **H08 (BUG-S2-005)**: `CreateDirectStopOrder()` in Trailing.StopUpdate.cs
   - Current: Uses `Enqueue` for stop replacement mapping
   - Issue: Same ghost-order window as H05 during flatten operations

3. **H21 (BUG-S6-001)**: `ExecuteRetestEntry()` in Entries.Retest.cs (line 50-221)
   - Current: Asynchronous `Enqueue` add to `activePositions` (line 173), synchronous `TryRemove` rollback (line 187)
   - Issue: Race condition - if broker submission fails, synchronous removal misses queued addition, leaking ghost position

4. **H22 (BUG-S6-001b)**: `ExecuteRetestManualEntry()` in Entries.Retest.cs (line 233-351)
   - Current: Same pattern as H21 - async add (line 310), sync rollback (line 324)
   - Issue: Identical race condition on manual retest entries

## Validated Problem

**Motivation (Confirmed via Code Analysis):**

The stated problem **MATCHES CODE REALITY**. Analysis confirms:

1. **Build 981 Protocol Violations Confirmed:**
   - Found 2 instances of `Enqueue` usage for `stopOrders` registration:
     - `CreateNewStopOrder()`: Line 320 in StopSync.cs
     - `SymmetryGuardSubmitFollowerBracket()`: Line 101 in Symmetry.Follower.cs
   - Both violate the Build 981 mandate requiring synchronous direct writes during bracket submission
   - Ghost-order tracking window exists: if flatten occurs before actor mailbox drains, unmapped stops become orphaned at broker

2. **Async-Add/Sync-Remove Race Confirmed:**
   - Both Retest entry methods use the anti-pattern:
     ```csharp
     Enqueue(ctx => { ctx.activePositions[entryName] = pos; });  // Async add
     // ... broker submission ...
     if (entryOrder == null) {
         activePositions.TryRemove(entryName, out _);  // Sync remove
     }
     ```
   - If submission fails, the synchronous `TryRemove` executes before the queued addition, causing permanent FSM state leak

3. **Lock-Free Status Verified:**
   - Grep search confirms ZERO `lock(` statements in execution paths
   - All 8 matches in codebase are in comments or method names (e.g., `AddExpectedPositionDeltaLocked`)
   - V12 DNA compliance already achieved for lock-free requirement

## Scope Boundaries

### IN SCOPE:
1. **H05**: Convert `CreateNewStopOrder()` line 320 from `Enqueue` to synchronous atomic write
2. **H08**: Convert stop replacement in `CreateDirectStopOrder()` from `Enqueue` to synchronous atomic write
3. **H21**: Fix `ExecuteRetestEntry()` rollback - either make add synchronous OR make rollback async-aware
4. **H22**: Fix `ExecuteRetestManualEntry()` rollback - same pattern as H21

### OUT OF SCOPE:
- Other `Enqueue` usages for non-stop-order state (e.g., `entryOrders`, `targetOrders`) - these remain async per design
- Lock-free migration (already complete - zero locks found)
- ASCII compliance verification (separate audit)
- Performance optimization beyond the concurrency fix
- Test file modifications (tests will validate fixes)
- Documentation updates (handled in separate phase)

## Risk Level

**CORE / HIGH RISK**

**Rationale:**
- Stop order registration is **mission-critical** - affects position protection across entire fleet
- Retest entries are **high-frequency** operations in production
- Changes touch the **Actor/FSM concurrency model** - the heart of V12 architecture
- Blast radius: 4 methods across 3 files, but each is called from multiple entry points
- **Mitigation**: Changes are surgical (single-line modifications), well-documented in bug reports, and covered by existing integration tests

## V12 DNA Constraints

### Build 981 Mandate:
- **CYC Target**: < 20 per method (all target methods currently compliant)
- **Lock-Free**: ✅ ALREADY ACHIEVED (zero `lock()` statements in src/)
- **Synchronous Transactional Writes**: ❌ VIOLATED - must convert `Enqueue` to direct atomic writes for `stopOrders`
- **ASCII-Only**: Not validated in this phase (separate audit required)
- **Extraction Floor**: >= 15 LOC per sub-method (not applicable - changes are single-line fixes)

### PR Diff Constraint:
- **Hard Limit**: < 150,000 characters
- **Expected Impact**: ~4 single-line changes + rollback logic refactor = estimated 200-300 lines total
- **Risk**: LOW - well within budget

### Atomic Write Pattern (Build 981):
```csharp
// BEFORE (H05, H08):
Enqueue(ctx => { ctx.stopOrders[entryName] = newStop; });

// AFTER (Build 981 compliant):
stopOrders[entryName] = newStop;  // Direct synchronous write
```

### Rollback Pattern Fix (H21, H22):
```csharp
// OPTION A: Make add synchronous
activePositions[entryName] = pos;  // Direct write
// ... broker submission ...
if (entryOrder == null) {
    activePositions.TryRemove(entryName, out _);  // Now safe
}

// OPTION B: Make rollback async-aware
Enqueue(ctx => { ctx.activePositions[entryName] = pos; });
// ... broker submission ...
if (entryOrder == null) {
    Enqueue(ctx => { ctx.activePositions.TryRemove(entryName, out _); });  // Queued removal
}
```

**Recommendation**: Option A (synchronous add) - simpler, matches Build 981 pattern for bracket submission phase.

---

## Architecture Context

**V12 Inline Actor Model:**
- All state mutations normally route through `Enqueue(Action<V12_002>)` for sequential consistency
- **Exception**: Build 981 explicitly exempts `stopOrders` writes during bracket submission to prevent ghost-order windows
- The exemption exists because stop registration must be **transactionally coupled** with broker submission
- If flatten occurs between `Enqueue` and actor drain, the stop is unmapped → ghost order at broker

**FSM State Integrity:**
- `activePositions` dictionary is the FSM truth source for position tracking
- Async add + sync remove creates a TOCTOU race that permanently corrupts FSM state
- REAPER audit relies on `activePositions` accuracy - ghost positions trigger false "Critical Desync" alerts

---

## Discovery Notes

**Positive Findings:**
1. Lock-free migration already complete - no `lock()` statements to remove
2. Bug reports are accurate - all 4 issues confirmed in code
3. Existing `Enqueue` infrastructure is well-tested - changes are additive (exemptions), not architectural rewrites
4. Integration tests already cover these paths - validation will be straightforward

**Concerns:**
1. Build 981 mandate is not documented in code comments - only in bug reports
2. No existing examples of synchronous `stopOrders` writes to reference (all current code uses `Enqueue`)
3. Rollback pattern fix requires careful sequencing to avoid introducing new races

**Questions for Director:**
- Should we add inline comments documenting the Build 981 exemption at each synchronous write site?
- Preference for rollback fix: Option A (sync add) or Option B (async rollback)?
- Should we add a runtime assertion to detect ghost positions in REAPER audit?