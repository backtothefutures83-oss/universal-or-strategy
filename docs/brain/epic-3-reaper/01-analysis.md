---
# EPIC-3-REAPER: FORENSIC ANALYSIS
# Epic: REAPER & Lifecycle Defenses
# Phase: ANALYSIS (Phase 2 of 6)
# Status: DRAFT - AWAITING PLAN COMPLETION
---

## 1. Executive Summary

This forensic analysis examines 7 validated concurrency defects across the REAPER audit system (S4) and kernel lifecycle sequences (S5). All defects involve unsafe collection iteration, TOCTOU races, or lifecycle ordering violations that can cause crashes, data corruption, or ghost state leaks under production load.

**Key Findings:**
- **4 collection iteration defects** (H13-H15 + Watchdog) - Direct enumeration of live NinjaTrader collections without thread-safe snapshots
- **1 TOCTOU defect** (H16) - Check-then-act pattern on in-flight guards allows duplicate enqueues
- **2 lifecycle ordering defects** (H17, H20) - Teardown sequence violations and queue overflow handling gaps
- **1 serialization race** (H18) - Background thread reads mutable state without isolation

**Blast Radius:**
- 3 files modified: [`REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs), [`Lifecycle.cs`](src/V12_002.Lifecycle.cs), [`StickyState.cs`](src/V12_002.StickyState.cs)
- 1 file added to scope per Director: [`Safety.Watchdog.cs`](src/V12_002.Safety.Watchdog.cs)
- Zero architectural changes (pure thread-safety hardening)

---

## 2. Defect-by-Defect Forensic Analysis

### H13: BUG-S4-001 - Naked Position Audit Scans Live Account.Orders

**Location:** [`src/V12_002.REAPER.Audit.cs:522`](src/V12_002.REAPER.Audit.cs:522)

**Current Implementation:**
```csharp
bool masterHasWorkingStop = Account.Orders.Any(o =>
    o.Instrument?.FullName == Instrument?.FullName &&
    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
```

**Root Cause Analysis:**
- `Account.Orders` is a live collection maintained by NinjaTrader on the UI thread
- REAPER audit runs on background timer thread (`_reaperTimer`, 1000ms interval)
- NinjaTrader updates `Account.Orders` asynchronously when broker events arrive
- Direct LINQ `.Any()` enumeration throws `InvalidOperationException` if collection modified during iteration
- Exception kills the audit cycle, leaving positions unprotected

**Evidence from Code:**
- Line 522: Direct `.Any()` call on `Account.Orders` without snapshot
- Line 16-57: `AuditApexPositions()` runs on timer thread (not strategy thread)
- Line 518-553: `AuditMaster_HandleNakedPosition()` called from audit cycle

**Failure Mode:**
1. REAPER timer fires → calls `AuditMasterAccountIfNeeded()`
2. Broker sends order update → NT8 modifies `Account.Orders` on UI thread
3. Audit thread iterates `Account.Orders` → collection modified exception
4. Audit cycle crashes → naked position grace window never expires
5. Position remains unprotected indefinitely

**Frequency:** High under active trading (10+ orders/sec)

**Sister Site:** Line 378 in `AuditFleet_CheckWorkingStop()` - already fixed with `.ToArray()` snapshot (Build 1108.003)

---

### H14: BUG-S4-002 - Flatten Cancel Loop Scans Live targetAcct.Orders

**Location:** [`src/V12_002.REAPER.Audit.cs:698`](src/V12_002.REAPER.Audit.cs:698)

**Current Implementation:**
```csharp
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

**Root Cause Analysis:**
- Emergency flatten triggered by critical desync detection
- `ProcessReaperFlatten_CancelWorkingOrders()` runs on strategy thread via `TriggerCustomEvent`
- Fleet account orders can be updated by broker callbacks during iteration
- Direct `foreach` on `targetAcct.Orders` throws if collection modified
- Exception aborts flatten → orphaned working orders remain active

**Evidence from Code:**
- Line 698: Direct `foreach` on `targetAcct.Orders` without snapshot
- Line 641-671: `ProcessReaperFlattenQueue()` dequeues and processes flatten requests
- Line 652: Calls `ProcessReaperFlatten_CancelWorkingOrders()` for each account

**Failure Mode:**
1. Critical desync detected → flatten enqueued
2. Strategy thread processes flatten → iterates `targetAcct.Orders`
3. Broker callback updates order state → collection modified
4. Exception thrown → flatten aborts mid-execution
5. Some orders cancelled, others remain Working → partial flatten state

**Frequency:** Medium (flatten events are rare but critical)

**Impact:** CRITICAL - Partial flatten leaves account in undefined state with orphaned orders

---

### H15: BUG-S4-003 - Flatten Close Loop Scans Live targetAcct.Positions

**Location:** [`src/V12_002.REAPER.Audit.cs:720`](src/V12_002.REAPER.Audit.cs:720)

**Current Implementation:**
```csharp
foreach (Position position in targetAcct.Positions)
{
    if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat)
    {
        continue;
    }
    // ... submit market close order
}
```

**Root Cause Analysis:**
- Identical pattern to H14 - emergency flatten position closure
- `targetAcct.Positions` updated by broker fill callbacks
- Direct `foreach` iteration without snapshot
- Exception aborts position closure → open positions remain unmanaged

**Evidence from Code:**
- Line 720: Direct `foreach` on `targetAcct.Positions` without snapshot
- Line 717-751: `ProcessReaperFlatten_ClosePositions()` called after order cancellation
- Line 653: Called from `ProcessReaperFlattenQueue()` for each account

**Failure Mode:**
1. Flatten continues after order cancellation
2. Strategy thread iterates `targetAcct.Positions` to close positions
3. Broker fill callback updates position → collection modified
4. Exception thrown → position closure aborts
5. Some positions closed, others remain open → partial flatten

**Frequency:** Medium (same trigger as H14)

**Impact:** CRITICAL - Open positions without protection orders after partial flatten

---

### H16: BUG-S4-005 - TOCTOU in REAPER In-Flight Guards

**Location:** [`src/V12_002.REAPER.Audit.cs:337`](src/V12_002.REAPER.Audit.cs:337), plus 2 sister sites

**Current Implementation (Repair Candidate):**
```csharp
bool alreadyInFlight;
alreadyInFlight = _repairInFlight.ContainsKey(repairKey); // [Build 968]

if (!alreadyInFlight)
{
    // ... check hasWorkingEntry
    _repairInFlight.TryAdd(repairKey, 0); // [Build 968]
    _reaperRepairQueue.Enqueue(acct.Name);
    return true;
}
```

**Root Cause Analysis:**
- Check-then-act pattern: `ContainsKey` → `TryAdd` is not atomic
- Two audit cycles can both pass `ContainsKey` check before either calls `TryAdd`
- Both threads enqueue the same repair candidate → duplicate repair submissions
- Duplicate repairs cause double-fills (two entry orders for same position)

**Evidence from Code:**
- Line 337: `ContainsKey` check separated from `TryAdd` by 14 lines
- Line 351: `TryAdd` called without checking return value
- Line 364-373: `EnqueueReaperFlattenCandidate()` uses correct atomic pattern: `if (!_reaperFlattenInFlight.TryAdd(...))`

**Sister Sites (per Director decision to fix all 3):**
1. **Line 420:** `EnqueueReaperNakedStopCandidate()` - `_reaperNakedStopInFlight.ContainsKey()` → `TryAdd()`
2. **Line 622:** `EnqueueReaperMasterNakedStop()` - `_reaperNakedStopInFlight.ContainsKey()` → `TryAdd()`

**Failure Mode:**
1. Audit cycle 1 checks `ContainsKey(repairKey)` → false
2. Audit cycle 2 checks `ContainsKey(repairKey)` → false (cycle 1 hasn't called `TryAdd` yet)
3. Both cycles pass guard → both call `TryAdd` and `Enqueue`
4. Repair queue processes both entries → submits two entry orders
5. Account receives double-fill → position size 2x expected

**Frequency:** Low (requires precise timing between audit cycles)

**Impact:** HIGH - Double-fills violate risk limits and can trigger compliance violations

**Correct Pattern (from line 367):**
```csharp
if (!_reaperFlattenInFlight.TryAdd(flattenKey, 0))
{
    return false; // Already in flight
}
```

---

### Watchdog Collection Iteration (BUG-S4-004 - Added per Director)

**Locations:** [`src/V12_002.Safety.Watchdog.cs`](src/V12_002.Safety.Watchdog.cs)
- Line 99: `HasWatchdogLeadAccountPosition()` - iterates `masterAccount.Positions`
- Line 167: `FlattenWatchdogPositions()` - iterates `masterAccount.Positions`
- Line 274: `FlattenDirectFallbackPositions()` - iterates `masterAccount.Positions`

**Current Implementation (Line 99):**
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

**Root Cause Analysis:**
- Watchdog timer runs on background thread (2000ms interval)
- Direct iteration of `masterAccount.Positions` without snapshot
- Broker fill callbacks update positions asynchronously
- Exception crashes watchdog → deadlock detection fails

**Evidence from Code:**
- Line 36-89: `OnWatchdogTimer()` runs on timer thread (not strategy thread)
- Line 99, 167, 274: Three sites iterate `Positions` without `.ToArray()`
- Line 120: `HasWatchdogLeadAccountWorkingOrder()` already uses `.ToArray()` snapshot (correct pattern)

**Failure Mode:**
1. Watchdog timer fires → checks for positions
2. Broker fill callback updates position → collection modified
3. Exception thrown → watchdog cycle crashes
4. Deadlock detection disabled → strategy thread hangs undetected
5. Positions remain open without protection

**Frequency:** Medium (watchdog runs every 2 seconds)

**Impact:** CRITICAL - Watchdog is last-resort safety mechanism; failure leaves no protection

**Note:** Line 120 shows the correct pattern is already known and used in the same file:
```csharp
foreach (Order order in masterAccount.Orders.ToArray())
```

---

### H17: BUG-S5-001 - Teardown Precedes Actor Drain

**Location:** [`src/V12_002.Lifecycle.cs:102-143`](src/V12_002.Lifecycle.cs:102)

**Current Implementation:**
```csharp
private void ShutdownUiAndServices()
{
    // ... UI teardown
    CancelAllV12GtcOrders(false);  // Line 128
    DrainQueuesForShutdown();       // Line 130
    EmitMetricsSummary();           // Line 131
    StopIpcServer();                // Line 134
    StopReaperAudit();              // Line 137
    UnsubscribeFromFleetAccounts(); // Line 142
}
```

**Root Cause Analysis:**
- `CancelAllV12GtcOrders()` runs BEFORE `DrainQueuesForShutdown()`
- Actor queue drain executes pending commands via `cmd.Execute(this)` (line 74)
- Drained commands can submit new orders to broker
- New orders bypass the cancel sweep → ghost orders remain active after shutdown

**Evidence from Code:**
- Line 128: `CancelAllV12GtcOrders(false)` cancels all tracked orders
- Line 130: `DrainQueuesForShutdown()` executes up to 50 queued commands
- Line 74: `cmd.Execute(this)` can call any strategy method, including order submission
- Line 689-695: `OnStateChangeTerminated()` calls `ShutdownUiAndServices()` before dictionary cleanup

**Failure Mode:**
1. Shutdown initiated → `CancelAllV12GtcOrders()` cancels all tracked orders
2. `DrainQueuesForShutdown()` executes queued commands
3. Queued command submits new bracket order (e.g., delayed fleet dispatch)
4. New order bypasses cancel sweep → remains active after shutdown
5. Strategy terminates → order is unmanaged (ghost order)

**Frequency:** Low (requires queued order submission command at shutdown)

**Impact:** CRITICAL - Ghost orders can execute after strategy termination, causing unmanaged positions

**Correct Sequence:**
1. Stop intake (prevent new commands from entering queue)
2. Drain actor queue completely (execute all pending commands)
3. Cancel all orders (sweep includes any orders submitted during drain)
4. Dispose resources

---

### H18: BUG-S5-002 - StickyState Background Serialization Race

**Location:** [`src/V12_002.StickyState.cs:40-61`](src/V12_002.StickyState.cs:40)

**Current Implementation:**
```csharp
Task.Run(async () =>
{
    try
    {
        await Task.Delay(STICKY_DEBOUNCE_MS);
        _stickyStateDirty = false;
        string payload = SerializeStickyState();  // Line 46
        AtomicWriteFile(_stickyStatePath, payload);
    }
    // ...
});
```

**Serialization reads mutable state (Line 68-76, 135-166):**
```csharp
private string SerializeStickyState()
{
    var sb = new StringBuilder(1024);
    SerializeSticky_WriteHeaderConfig(sb);      // Reads isRMAModeActive, Target1Value, etc.
    SerializeSticky_WriteFleetAnchor(sb);       // Reads activeFleetAccounts
    SerializeSticky_WriteModeProfiles(sb);      // Mutates _modeProfiles[activeMode]
    SerializeSticky_WritePositions(sb);         // Reads activePositions
    return sb.ToString();
}
```

**Root Cause Analysis:**
- `MarkStickyDirty()` spawns background `Task.Run` thread
- Background thread reads strategy properties directly (not volatile, not atomic)
- Strategy thread mutates same properties via IPC commands
- No synchronization between reader and writer threads
- Race conditions cause:
  1. Stale reads (serialized state doesn't match current state)
  2. Torn reads (partial updates, e.g., T1 value from old config, T2 from new)
  3. Dictionary corruption (line 144: `_modeProfiles[activeMode] = ...` mutates shared dict)

**Evidence from Code:**
- Line 40: `Task.Run()` spawns background thread (ThreadPool)
- Line 46: `SerializeStickyState()` reads non-atomic properties
- Line 90-113: Reads `isRMAModeActive`, `Target1Value`, `T1Type`, etc. (not volatile)
- Line 144: **CRITICAL** - Mutates `_modeProfiles` dictionary from background thread
- Line 121-125: Reads `activeFleetAccounts` (ConcurrentDictionary, but `.ToArray()` not used)

**Failure Mode:**
1. IPC command updates `Target1Value = 2.0` on strategy thread
2. Background serialization reads `Target1Value` → gets 1.5 (stale)
3. IPC command updates `Target2Value = 1.0` on strategy thread
4. Background serialization reads `Target2Value` → gets 1.0 (current)
5. Serialized state has inconsistent config (T1=1.5, T2=1.0 instead of T1=2.0, T2=1.0)
6. On restart, strategy loads inconsistent state

**Dictionary Mutation (Line 144):**
```csharp
_modeProfiles[activeMode] = SnapshotCurrentConfig();
```
- `_modeProfiles` is a regular `Dictionary<string, ModeConfigProfile>` (not thread-safe)
- Background thread writes to dictionary while strategy thread may be reading
- Can cause `InvalidOperationException` or corrupt dictionary internal state

**Frequency:** High (serialization fires on every config change, 50ms debounce)

**Impact:** HIGH - Corrupt persisted state causes incorrect strategy behavior on restart

---

### H20: BUG-S5-004 - Teardown Overflow Discard Drops Queue

**Location:** [`src/V12_002.Lifecycle.cs:81-83`](src/V12_002.Lifecycle.cs:81)

**Current Implementation:**
```csharp
StrategyCommand overflowCmd;
while (_cmdQueue.TryDequeue(out overflowCmd))
    actorOverflow++;
```

**Root Cause Analysis:**
- Actor queue drain limited to 50 commands (line 72)
- Overflow commands (51+) are dequeued and counted but NOT executed
- Discarded commands may contain critical state updates (e.g., follower bracket submissions)
- Followers left with stale state after shutdown

**Evidence from Code:**
- Line 72: `while (actorDrained < 50 && _cmdQueue.TryDequeue(out cmd))`
- Line 74: First 50 commands executed via `cmd.Execute(this)`
- Line 81-83: Overflow commands dequeued but NOT executed
- Line 85: Log reports overflow count but no cleanup action

**Failure Mode:**
1. High-volume trading session → 100+ commands queued
2. Shutdown initiated → drain processes first 50 commands
3. Remaining 50 commands dequeued and discarded
4. Discarded commands include follower bracket submissions
5. Followers never receive bracket orders → positions unprotected
6. Strategy terminates → followers have open positions without stops

**Frequency:** Low (requires >50 queued commands at shutdown)

**Impact:** MEDIUM - Followers left with un-synchronized state (missing brackets)

**Director Decision:** Trigger `CANCEL_ALL` on affected followers when overflow detected

---

## 3. Cross-Cutting Analysis

### Thread Safety Patterns

**Correct Patterns (Already in Codebase):**

1. **Collection Snapshot (Line 378):**
```csharp
var orders = acct.Orders.ToArray();
return orders.Any(o => ...);
```

2. **Atomic TryAdd (Line 367):**
```csharp
if (!_reaperFlattenInFlight.TryAdd(flattenKey, 0))
{
    return false;
}
```

3. **Watchdog Orders Snapshot (Line 120):**
```csharp
foreach (Order order in masterAccount.Orders.ToArray())
```

**Incorrect Patterns (To Fix):**

1. **Direct Collection Iteration:**
   - H13: `Account.Orders.Any(o => ...)`
   - H14: `foreach (Order order in targetAcct.Orders)`
   - H15: `foreach (Position position in targetAcct.Positions)`
   - Watchdog: `foreach (Position position in masterAccount.Positions)` (3 sites)

2. **Check-Then-Act TOCTOU:**
   - H16: `ContainsKey()` → `TryAdd()` (3 sites)

3. **Unprotected Shared State:**
   - H18: Background thread reads mutable properties without isolation

### Lifecycle Ordering Dependencies

**Current Teardown Sequence (Incorrect):**
```
1. CancelAllV12GtcOrders()     ← Cancels tracked orders
2. DrainQueuesForShutdown()    ← Can submit NEW orders
3. EmitMetricsSummary()        ← Reads disposed state
4. StopIpcServer()
5. StopReaperAudit()
6. UnsubscribeFromFleetAccounts()
```

**Correct Teardown Sequence (H17 Fix):**
```
1. Stop intake (IPC, REAPER, Watchdog)  ← Prevent new commands
2. DrainQueuesForShutdown()             ← Execute pending work
3. CancelAllV12GtcOrders()              ← Sweep ALL orders (including drain-submitted)
4. EmitMetricsSummary()                 ← Safe to read state
5. Dispose resources
```

### Dictionary Thread Safety

**Thread-Safe Collections (Already Used):**
- `ConcurrentDictionary<K,V>` for all order/position tracking
- `ConcurrentQueue<T>` for actor and IPC queues

**Non-Thread-Safe Collections (H18):**
- `_modeProfiles` is `Dictionary<string, ModeConfigProfile>` (line 144 mutation from background thread)
- Must clone before background serialization

---

## 4. Impact Assessment

### Severity Matrix

| Defect | Severity | Frequency | Blast Radius | Production Risk |
|:---|:---|:---|:---|:---|
| H13 | High | High | REAPER audit crash | Position unprotected |
| H14 | High | Medium | Partial flatten | Orphaned orders |
| H15 | High | Medium | Partial flatten | Open positions |
| H16 | High | Low | Duplicate repairs | Double-fills |
| H17 | Critical | Low | Ghost orders | Unmanaged positions |
| H18 | High | High | State corruption | Incorrect restart |
| H20 | Medium | Low | Follower desync | Missing brackets |
| Watchdog | Critical | Medium | Watchdog crash | No deadlock detection |

### Failure Cascades

**Cascade 1: REAPER Audit Crash (H13)**
```
H13 exception → Audit cycle crashes → Naked position grace never expires
→ No emergency stop submitted → Position remains unprotected
→ Stop-loss never triggers → Unlimited loss exposure
```

**Cascade 2: Partial Flatten (H14 + H15)**
```
H14 exception → Order cancellation aborts → Some orders remain Working
→ H15 exception → Position closure aborts → Some positions remain open
→ Open positions with Working orders → Undefined state
→ Manual intervention required
```

**Cascade 3: Ghost Order Window (H17)**
```
Cancel sweep runs → Drain executes queued commands → New order submitted
→ Shutdown completes → Order remains active unmanaged
→ Order fills after termination → Unmanaged position
→ No stop protection → Unlimited loss exposure
```

**Cascade 4: Watchdog Failure (Watchdog defects)**
```
Watchdog timer fires → Position iteration crashes → Watchdog disabled
→ Strategy thread deadlocks → No detection → Positions remain open
→ No emergency flatten → Manual intervention required
```

---

## 5. Risk Mitigation Strategy

### Defect Prioritization

**Tier 1 (CRITICAL - Fix First):**
- H17: Teardown ordering (ghost order window)
- Watchdog: Collection iteration (last-resort safety)

**Tier 2 (HIGH - Fix Second):**
- H13, H14, H15: REAPER collection iteration (audit crashes)
- H16: TOCTOU in-flight guards (duplicate submissions)
- H18: StickyState serialization race (state corruption)

**Tier 3 (MEDIUM - Fix Third):**
- H20: Queue overflow cleanup (follower desync)

### Testing Strategy

**Unit Tests (7 tests):**
1. `Test_H13_NakedPositionAudit_ConcurrentOrderModification_NoException()`
2. `Test_H14_FlattenCancel_ConcurrentOrderModification_CompletesSuccessfully()`
3. `Test_H15_FlattenClose_ConcurrentPositionModification_CompletesSuccessfully()`
4. `Test_H16_RepairEnqueue_ConcurrentAuditCycles_EnqueuesExactlyOnce()`
5. `Test_H17_Shutdown_DrainBeforeCancel_NoGhostOrders()`
6. `Test_H18_StickyState_ConcurrentMutation_NoCorruption()`
7. `Test_H20_QueueOverflow_TriggersFollowerCleanup()`

**Stress Tests:**
- 1000+ orders/sec with concurrent REAPER audit cycles
- Rapid shutdown during high-volume trading
- Concurrent config changes during serialization

**Integration Tests:**
- Full REAPER audit cycle under load
- Emergency flatten with concurrent broker callbacks
- Watchdog deadlock detection with position updates

---

## 6. Dependencies & Constraints

### External Dependencies

**NinjaTrader 8 API:**
- `Account.Orders` - Live collection, updated on UI thread
- `Account.Positions` - Live collection, updated on broker callbacks
- `Account.All` - Static collection, safe to iterate

**Threading Model:**
- REAPER timer: Background thread (1000ms interval)
- Watchdog timer: Background thread (2000ms interval)
- Strategy thread: NT8 managed thread (OnBarUpdate, OnMarketData, etc.)
- IPC thread: Background thread (socket listener)
- Serialization thread: ThreadPool thread (Task.Run)

### Internal Dependencies

**Shared State:**
- `_repairInFlight`, `_reaperNakedStopInFlight`, `_reaperFlattenInFlight` - In-flight guards
- `activePositions`, `entryOrders`, `stopOrders` - Order tracking dictionaries
- `_modeProfiles` - Per-mode config snapshots
- `_cmdQueue`, `ipcCommandQueue` - Actor queues

**Lifecycle Gates:**
- `_isTerminating` - Shutdown flag
- `_orderAdoptionComplete` - REAPER gate after reconnect
- `_configureComplete`, `_dataLoadedComplete` - Startup gates

---

## 7. Architectural Constraints

### V12 DNA Compliance

**MUST Maintain:**
1. **Zero new `lock()` statements** - All fixes use lock-free patterns
2. **ASCII-only strings** - All log messages use ASCII characters only
3. **Diff size < 150K** - Changes are surgical (snapshots + reordering only)
4. **Ghost-method ban** - No references to `ClearAllEventHandlers`, `_globalState`, `_inFlightRmaEntries`

**Lock-Free Patterns:**
- `.ToArray()` snapshots for collection iteration
- `TryAdd()` atomic check-and-set for in-flight guards
- `Interlocked` primitives for flags and counters
- `ConcurrentDictionary` for shared state

### Build 981 Protocol

**Exemptions (Not Applicable):**
- This epic does NOT touch order submission paths
- Build 981 synchronous write protocol applies to bracket submissions only
- REAPER audit is read-only (no order submissions in audit cycle)

**Compliance:**
- All changes are thread-safety hardening (no logic modifications)
- No changes to FSM state machines or order submission flows

---

## 8. Verification Strategy

### Pre-Deployment Gates

**Gate 1: Compile + Sync**
```powershell
powershell -File .\deploy-sync.ps1
```
- PASS: Hard-link sync succeeds
- PASS: ASCII gate shows zero non-ASCII characters
- PASS: DIFF GUARD < 150,000 characters

**Gate 2: Lock Regression**
```powershell
grep -r "lock(" src/V12_002.REAPER.Audit.cs
grep -r "lock(" src/V12_002.Lifecycle.cs
grep -r "lock(" src/V12_002.StickyState.cs
grep -r "lock(" src/V12_002.Safety.Watchdog.cs
```
- PASS: Zero matches (no new locks introduced)

**Gate 3: Ghost-Method Audit**
```powershell
grep -r "ClearAllEventHandlers" src/
grep -r "_globalState" src/
grep -r "_inFlightRmaEntries" src/
```
- PASS: Zero matches (no banned identifiers)

**Gate 4: Unit Tests**
- PASS: All 7 unit tests green
- PASS: Existing integration tests pass (no regressions)

**Gate 5: F5 Compile Gate**
- PASS: BUILD_TAG banner visible in NinjaTrader
- PASS: Strategy loads without errors

### Post-Deployment Validation

**Stress Test Scenarios:**
1. **REAPER Audit Stress:** 1000+ orders/sec, verify zero audit crashes
2. **Flatten Stress:** Trigger 10 concurrent flattens, verify all complete
3. **Shutdown Stress:** Shutdown with 100+ queued commands, verify no ghost orders
4. **Serialization Stress:** 100 rapid config changes, verify no corruption

**Manual Verification:**
1. Run strategy for 1 hour under live market conditions
2. Monitor logs for `InvalidOperationException` (should be zero)
3. Verify REAPER audit cycle completes every 1000ms
4. Verify watchdog heartbeat every 2000ms
5. Shutdown cleanly, verify no ghost orders in broker

---

## 9. Rollback Plan

### Failure Scenarios

**Scenario 1: REAPER audit regression**
- **Symptom:** False flatten events increase
- **Action:** Revert REAPER.Audit.cs changes
- **Command:**
```powershell
git checkout HEAD~1 -- src/V12_002.REAPER.Audit.cs
powershell -File .\deploy-sync.ps1
```

**Scenario 2: Shutdown hangs**
- **Symptom:** Strategy fails to terminate cleanly
- **Action:** Revert Lifecycle.cs changes
- **Command:**
```powershell
git checkout HEAD~1 -- src/V12_002.Lifecycle.cs
powershell -File .\deploy-sync.ps1
```

**Scenario 3: Config corruption**
- **Symptom:** Strategy loads incorrect config on restart
- **Action:** Revert StickyState.cs changes + delete .v12state file
- **Command:**
```powershell
git checkout HEAD~1 -- src/V12_002.StickyState.cs
Remove-Item "$env:USERPROFILE\Documents\NinjaTrader 8\SIMA_Logs\*.v12state"
powershell -File .\deploy-sync.ps1
```

### Emergency Rollback (Full Epic)

```powershell
git checkout HEAD~N -- src/V12_002.REAPER.Audit.cs
git checkout HEAD~N -- src/V12_002.Lifecycle.cs
git checkout HEAD~N -- src/V12_002.StickyState.cs
git checkout HEAD~N -- src/V12_002.Safety.Watchdog.cs
powershell -File .\deploy-sync.ps1
```

Where N = number of commits in this epic (typically 7, one per ticket)

---

## 10. Open Questions & Risks

### Resolved (Director Decisions)

1. ✅ **Include Watchdog fixes?** YES - Add all 3 Watchdog sites
2. ✅ **Fix all 3 REAPER enqueue sites?** YES - Fix repair, naked stop, and master naked stop
3. ✅ **Increase actor drain limit?** NO - Maintain 50-command threshold
4. ✅ **Queue overflow action?** Trigger `CANCEL_ALL` on affected followers

### Remaining Risks

**Risk 1: Snapshot Performance Overhead**
- **Concern:** `.ToArray()` allocates new array on every audit cycle
- **Mitigation:** Arrays are small (typically <10 orders/positions per account)
- **Validation:** Benchmark audit cycle time before/after (expect <1ms increase)

**Risk 2: Teardown Sequence Timing**
- **Concern:** Reordered shutdown may expose new race conditions
- **Mitigation:** Maintain existing gates (`_isTerminating`, `_orderAdoptionComplete`)
- **Validation:** Stress test with 100+ rapid shutdown cycles

**Risk 3: StickyState Snapshot Overhead**
- **Concern:** Dictionary clone on every dirty mark may impact performance
- **Mitigation:** Clone is shallow (references only), 50ms debounce reduces frequency
- **Validation:** Benchmark serialization frequency under high IPC load

---

## 11. Success Criteria

### Functional Requirements

1. **H13-H15 + Watchdog Fixed:** Zero `InvalidOperationException` in REAPER/Watchdog logs
2. **H16 Fixed:** Zero duplicate repair submissions (verify via repair queue logs)
3. **H17 Fixed:** Zero ghost orders after shutdown (verify via broker order list)
4. **H18 Fixed:** Zero config corruption on restart (verify via .v12state file integrity)
5. **H20 Fixed:** Follower cleanup triggered on queue overflow (verify via logs)

### Performance Requirements

1. REAPER audit cycle time < 10ms (current ~5ms, allow 2x overhead)
2. Watchdog cycle time < 5ms (current ~2ms, allow 2x overhead)
3. Serialization frequency < 20/sec (50ms debounce enforces this)
4. Shutdown time < 5 seconds (current ~2 seconds, allow 2x overhead)

### DNA Compliance

1. Zero new `lock()` statements
2. Zero non-ASCII characters in string literals
3. Diff size < 150,000 characters
4. Zero ghost-method references

---

**Document Status:** COMPLETE - READY FOR APPROACH GENERATION  
**Author:** Bob CLI (v12-engineer) via Plan Mode  
**Date:** 2026-05-19T00:48:00Z  
**Epic:** epic-3-reaper  
**Phase:** 2/6 (ANALYSIS)  
**Next:** Generate [`02-approach.md`](docs/brain/epic-3-reaper/02-approach.md)