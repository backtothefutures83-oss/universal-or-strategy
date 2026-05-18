# Epic: epic-1-dna -- Dependency Analysis & Risk Assessment

## Executive Summary

This analysis maps the dependencies, shared state mutations, and risk hotspots for the 4 target methods identified in the scope document. All targets exhibit Build 981 protocol violations (async `Enqueue` usage for `stopOrders` writes) or TOCTOU race conditions (async-add/sync-remove pattern in `activePositions`).

**Key Findings:**
- Zero `lock()` statements in execution paths (DNA compliant)
- All targets use ConcurrentDictionary for thread-safe collection access
- REAPER audit system has 173 references across 15 files -- critical dependency
- Test coverage: F5 compile gate + integration tests (MetricsIntegrationTests.cs, OrchestrationIntegrationTests.cs)
- Complexity: ExecuteRetestEntry (CYC=26) and ExecuteRetestManualEntry (CYC=17) exceed DNA target (<20)

---

## 1. Target Method Dependency Map

### H05: CreateNewStopOrder() (src/V12_002.Orders.Management.StopSync.cs)

**Method Signature:**
```csharp
private void CreateNewStopOrder(string entryName, PositionInfo pos, double stopPrice)
```

**Complexity Metrics:**
- Cyclomatic Complexity: 10 (MEDIUM)
- Max Nesting: 4
- Parameter Count: 3
- Lines: 45

**Direct Callers (3 files, 3 call sites):**
1. `src/V12_002.Orders.Callbacks.cs` (line unknown) -- Called during order callback processing
2. `src/V12_002.Orders.Callbacks.AccountOrders.cs` (line unknown) -- Called during account order updates
3. `src/V12_002.Trailing.StopUpdate.cs` (line unknown) -- Called during trailing stop updates

**Shared State Mutations:**
- **Line 320 (VIOLATION):** `Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; });`
  - Async write to `stopOrders` ConcurrentDictionary
  - Creates ghost-order window if flatten occurs before actor drain
  - Build 981 mandates synchronous write here

**FSM Coupling:**
- Reads `activePositions` dictionary to retrieve PositionInfo
- Writes to `stopOrders` dictionary (FSM truth source for stop tracking)
- No direct FSM state machine interaction

**REAPER Dependencies:**
- REAPER audit reads `stopOrders` to detect naked positions (position with no working stop)
- REAPER naked-position logic: 173 references across 15 files
- Grace window: 5 seconds (NakedPositionGraceSec property, minimum enforced)

---

### H08: CreateDirectStopOrder() (src/V12_002.Trailing.StopUpdate.cs)

**Method Context:**
- Located in Trailing.StopUpdate.cs (trailing stop update logic)
- Lines 264, 276: Uses `Enqueue` for stop replacement mapping
- Called during trailing stop updates when stop price moves

**Shared State Mutations:**
- **Line 264 (VIOLATION):** `Enqueue(ctx => { ctx._followerTargetReplaceSpecs[...] = ...; });`
- **Line 276 (VIOLATION):** `Enqueue(ctx => { ctx.stopOrders[...] = ...; });`
  - Both async writes create ghost-order windows
  - Build 981 mandates synchronous writes

**REAPER Dependencies:**
- REAPER audit reads `stopOrders` for position protection verification
- StampReaperMoveGrace() called before cancel to suppress false desync during replace gap
- REAPER ChangePending guard provides second layer of protection (AuditApexPositions line 193)

---

### H21: ExecuteRetestEntry() (src/V12_002.Entries.Retest.cs)

**Method Signature:**
```csharp
private void ExecuteRetestEntry(string entryName, MarketPosition direction, int qty, double entryPrice, string reason)
```

**Complexity Metrics:**
- Cyclomatic Complexity: 26 (HIGH -- exceeds DNA target of <20)
- Max Nesting: 5
- Parameter Count: 5
- Lines: 172

**Direct Callers (2 files, 2+ call sites):**
1. `src/V12_002.Lifecycle.cs` -- Called during strategy lifecycle events
2. `src/V12_002.UI.IPC.Commands.Fleet.cs` -- Called from IPC command layer

**Shared State Mutations:**
- **Line 173 (RACE CONDITION):** `Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });`
  - Async add to `activePositions` dictionary
- **Line 187 (RACE CONDITION):** `activePositions.TryRemove(entryName, out _);`
  - Synchronous remove from `activePositions` dictionary
  - **TOCTOU Race:** If broker submission fails, sync remove executes before queued addition
  - Result: Permanent FSM state leak (ghost position in `activePositions`)

**FSM Coupling:**
- Writes to `activePositions` dictionary (FSM truth source for position tracking)
- Writes to `entryOrders` dictionary (entry order tracking)
- REAPER audit relies on `activePositions` for position verification

**REAPER Dependencies:**
- REAPER reads `activePositions` to calculate expected position state
- REAPER compares `activePositions` count against broker position
- False desync if ghost position exists: actualQty=0, expectedQty!=0 → Emergency Flatten

---

### H22: ExecuteRetestManualEntry() (src/V12_002.Entries.Retest.cs)

**Method Signature:**
```csharp
private void ExecuteRetestManualEntry(string entryName, MarketPosition direction, int qty, double entryPrice)
```

**Complexity Metrics:**
- Cyclomatic Complexity: 17 (HIGH)
- Max Nesting: 5
- Parameter Count: 4
- Lines: 119

**Direct Callers (1 file, 1+ call sites):**
1. `src/V12_002.UI.IPC.Commands.Fleet.cs` -- Called from IPC command layer (manual entry commands)

**Shared State Mutations:**
- **Line 310 (RACE CONDITION):** `Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });`
  - Async add to `activePositions` dictionary
- **Line 324 (RACE CONDITION):** `activePositions.TryRemove(entryName, out _);`
  - Synchronous remove from `activePositions` dictionary
  - **TOCTOU Race:** Identical pattern to H21

**FSM Coupling:**
- Identical to H21 (writes to `activePositions` and `entryOrders`)

**REAPER Dependencies:**
- Identical to H21 (REAPER audit reads `activePositions` for position verification)

---

## 2. Shared State Analysis

### ConcurrentDictionary Usage (Thread-Safe Collections)

All target methods use ConcurrentDictionary for thread-safe access:

1. **stopOrders** (ConcurrentDictionary<string, Order>)
   - Maps entry names to stop order objects
   - Read by: REAPER audit, order callbacks, trailing logic
   - Written by: H05, H08 (via `Enqueue` -- VIOLATION)

2. **activePositions** (ConcurrentDictionary<string, PositionInfo>)
   - Maps entry names to position metadata structs
   - Read by: REAPER audit, order callbacks, cleanup logic
   - Written by: H21, H22 (async-add/sync-remove -- RACE)

3. **expectedPositions** (ConcurrentDictionary<string, int>)
   - Maps account names to expected position quantities
   - Read by: REAPER audit (173 references across 15 files)
   - Written by: Order callbacks, SIMA execution, cleanup logic
   - **Critical Dependency:** REAPER compares `expectedPositions` against broker position to detect desync

### Actor Mailbox Pattern

**Current Implementation:**
- `Enqueue(Action<V12_002>)` queues mutations for sequential execution
- `TryDrain()` processes mailbox synchronously on strategy thread
- Actor mailbox defined at lines 345-352 in src/V12_002.cs
- Drain infrastructure at lines 397-490 in src/V12_002.cs

**Build 981 Exemption:**
- Synchronous writes for `stopOrders` during bracket submission
- Prevents ghost-order tracking windows during shutdown/flatten
- Exemption must be documented with inline comments

---

## 3. REAPER Audit System Dependencies

### REAPER Architecture (173 references across 15 files)

**Core Files:**
1. `src/V12_002.REAPER.cs` -- Main audit logic and timer infrastructure
2. `src/V12_002.REAPER.Audit.cs` -- Fleet position audit and desync detection
3. `src/V12_002.REAPER.Repair.cs` -- Re-issue missed entry orders for desynced followers
4. `src/V12_002.REAPER.NakedStop.cs` -- Emergency stop protection for naked positions

**Key REAPER Functions:**
- `AuditApexPositions()` -- Main audit cycle (1000ms interval)
- `AuditSingleFleetAccount()` -- Per-account audit logic
- `AuditMasterAccountIfNeeded()` -- Master account audit
- `ProcessReaperRepairQueue()` -- Repair queue processing
- `ProcessReaperNakedStopQueue()` -- Emergency stop queue processing

**REAPER State Tracking:**
- `_lastExpectedPositionSetTicks` -- Global grace window timestamp
- `_reaperAccountFillGrace` -- Per-account fill grace timestamps (Build 935)
- `_nakedPositionFirstSeen` -- Naked position grace tracking
- `_positionPassFailedFirstSeen` -- Position Pass grace tracking (Build 999)

**Grace Windows:**
- Fill grace: 5 seconds (prevents false desync during broker-confirm lag)
- Naked position grace: 5 seconds minimum (NakedPositionGraceSec property)
- Position Pass grace: 10 seconds (reconnect FSM rebuild window)

---

## 4. Risk Assessment

### Risk Level: **CORE COMPONENT**

**Rationale:**
- All 4 targets are in execution-critical paths (order submission, stop registration, entry rollback)
- REAPER audit system has 173 references across 15 files -- widespread coupling
- `stopOrders` and `activePositions` are FSM truth sources for position tracking
- Incorrect mutations can trigger false REAPER flattens (Emergency Re-sync)

### Risk Hotspots

**1. REAPER False Flatten Risk (CRITICAL)**
- **Scenario:** Ghost position in `activePositions` after H21/H22 rollback failure
- **Impact:** REAPER sees actualQty=0, expectedQty!=0 → Emergency Flatten
- **Mitigation:** Synchronous add + synchronous rollback (Option A from Director)

**2. Ghost Order Tracking Risk (HIGH)**
- **Scenario:** Flatten occurs before actor mailbox drains in H05/H08
- **Impact:** Stop orders unmapped at broker but still exist → orphaned protection orders
- **Mitigation:** Synchronous atomic writes for `stopOrders` (Build 981 mandate)

**3. FSM State Leak Risk (HIGH)**
- **Scenario:** Async-add/sync-remove race in H21/H22 leaves permanent ghost position
- **Impact:** `activePositions` count diverges from broker truth → REAPER desync loop
- **Mitigation:** Synchronous add pattern eliminates TOCTOU window

**4. Complexity Overshoot Risk (MEDIUM)**
- **Scenario:** ExecuteRetestEntry (CYC=26) exceeds DNA target (<20)
- **Impact:** Harder to test, maintain, and reason about
- **Mitigation:** OUT OF SCOPE for this epic (defer to future refactoring)

---

## 5. Test Coverage Analysis

### Existing Test Infrastructure

**Integration Tests:**
1. `tests/MetricsIntegrationTests.cs` -- Metrics and telemetry validation
2. `tests/OrchestrationIntegrationTests.cs` -- Multi-agent orchestration tests

**F5 Compile Gate:**
- All src/ files must compile without errors
- NinjaTrader hard-link synchronization via `deploy-sync.ps1`

**Manual Testing:**
- F5 in NinjaTrader + BUILD_TAG verification
- Live broker testing on Sim accounts

### Test Coverage Gaps

**Gap 1: No unit tests for target methods**
- H05, H08, H21, H22 have no isolated unit tests
- Integration tests cover end-to-end flows but not edge cases

**Gap 2: No REAPER audit tests**
- REAPER logic has 173 references but no dedicated test suite
- Manual testing required to verify grace windows and desync detection

**Gap 3: No race condition tests**
- H21/H22 TOCTOU race not covered by automated tests
- Requires stress testing with concurrent broker submissions

---

## 6. Dependency Graph Summary

### H05 & H08 (Stop Order Registration)

```
CreateNewStopOrder / CreateDirectStopOrder
  ├─ Reads: activePositions (PositionInfo lookup)
  ├─ Writes: stopOrders (async via Enqueue -- VIOLATION)
  ├─ Called by: Orders.Callbacks.cs, Orders.Callbacks.AccountOrders.cs, Trailing.StopUpdate.cs
  └─ REAPER Dependencies:
      ├─ REAPER.Audit.cs reads stopOrders for naked position detection
      ├─ REAPER.NakedStop.cs submits emergency stops if no working stop found
      └─ Grace window: 5 seconds (NakedPositionGraceSec)
```

### H21 & H22 (Retest Entry Rollback)

```
ExecuteRetestEntry / ExecuteRetestManualEntry
  ├─ Writes: activePositions (async-add via Enqueue -- RACE)
  ├─ Writes: activePositions (sync-remove via TryRemove -- RACE)
  ├─ Writes: entryOrders (entry order tracking)
  ├─ Called by: Lifecycle.cs, UI.IPC.Commands.Fleet.cs
  └─ REAPER Dependencies:
      ├─ REAPER.Audit.cs reads activePositions for position verification
      ├─ REAPER.Repair.cs re-issues missed entries if desync detected
      └─ False desync if ghost position: actualQty=0, expectedQty!=0 → Emergency Flatten
```

---

## 7. Complexity Metrics Summary

| Method | File | CYC | Nesting | Params | Lines | Assessment |
|--------|------|-----|---------|--------|-------|------------|
| CreateNewStopOrder | Orders.Management.StopSync.cs | 10 | 4 | 3 | 45 | MEDIUM |
| CreateDirectStopOrder | Trailing.StopUpdate.cs | N/A | N/A | N/A | N/A | UNKNOWN |
| ExecuteRetestEntry | Entries.Retest.cs | 26 | 5 | 5 | 172 | HIGH |
| ExecuteRetestManualEntry | Entries.Retest.cs | 17 | 5 | 4 | 119 | HIGH |

**DNA Compliance:**
- CYC target: <20 per method
- H21 (CYC=26) and H22 (CYC=17) exceed target
- **OUT OF SCOPE:** Complexity reduction deferred to future epic

---

## 8. Conclusion

All 4 target methods exhibit the stated problems:
- **H05 & H08:** Build 981 protocol violations (async `Enqueue` for `stopOrders` writes)
- **H21 & H22:** TOCTOU race conditions (async-add/sync-remove pattern in `activePositions`)

The REAPER audit system is the critical dependency with 173 references across 15 files. Incorrect mutations can trigger false Emergency Flattens, making this a **CORE COMPONENT** risk level.

**Next Steps:**
- Proceed to Part 2 (Approach Design) to draft refactoring strategy
- Incorporate Director's approved decisions:
  1. Synchronous atomic writes for H05/H08
  2. Synchronous add + synchronous rollback for H21/H22
  3. Inline comments documenting Build 981 exemption
  4. REAPER diagnostic assertion design