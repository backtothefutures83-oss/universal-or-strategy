# P2 Forensic Report: S4 REAPER Defense Cluster

**Cluster**: S4 - REAPER Defense System  
**Files**: 5 files, 1,351 total lines  
**Analysis Date**: 2026-05-17  
**Analyst**: Bob CLI (v12-engineer)

---

## Executive Summary

The S4 REAPER Defense cluster implements V12's **safety watchdog and emergency response system**. This is a **mission-critical defense layer** that detects position desyncs, naked positions (missing stops), and deadlocks, then executes emergency repairs, flattens, or hard stops to protect capital.

**Key Characteristics**:
- **Background Timer Architecture**: System.Timers.Timer on background thread → TriggerCustomEvent → strategy thread execution
- **Three Emergency Queues**: Repair (ghost positions), Flatten (critical desync), NakedStop (missing protection)
- **Grace Windows**: Fill grace (2s), naked position grace (5-10s), Position Pass grace (10s)
- **Atomic Guards**: ConcurrentDictionary in-flight guards prevent duplicate emergency actions
- **Zero lock() Compliance**: Pure atomic primitives throughout

**Test Strategy**: 30 tests documenting current emergency response behavior, grace window logic, and queue processing patterns.

---

## File Inventory

### 1. V12_002.REAPER.cs (156 lines)
**Purpose**: Core REAPER infrastructure - timer lifecycle, queues, grace tracking  
**Key Components**:
- `StartReaperAudit()` / `StopReaperAudit()` - Timer lifecycle
- `OnReaperTimerElapsed()` - Background timer → TriggerCustomEvent marshalling
- `_reaperFlattenQueue`, `_reaperRepairQueue`, `_reaperNakedStopQueue` - Emergency action queues
- `_repairInFlight`, `_reaperFlattenInFlight`, `_reaperNakedStopInFlight` - Atomic dedup guards
- `_accountFillGraceTicks` - Per-account fill grace (Build 935 fix)
- `_nakedPositionFirstSeen` - Naked position grace tracking
- `_positionPassFailedFirstSeen` - Position Pass grace (Build 999)

**V12 DNA Compliance**:
- ✅ Zero lock() - ConcurrentQueue + ConcurrentDictionary only
- ✅ Atomic guards - TryAdd pattern for in-flight dedup
- ✅ Timer marshalling - TriggerCustomEvent for thread safety

### 2. V12_002.REAPER.Audit.cs (730 lines)
**Purpose**: Fleet position audit engine - desync detection and emergency triage  
**Key Components**:
- `AuditApexPositions()` - Top-level audit orchestrator (all accounts)
- `AuditSingleFleetAccount()` - Per-account audit dispatcher (Build 935 refactor)
- `AuditMasterAccountIfNeeded()` - Master account audit (separate path)
- `AuditFleet_CalculateExpectedActual()` - FSM-based expected position calculation
- `AuditFleet_HandleDesyncRepair()` - Ghost position (actual=0, expected!=0) repair logic
- `AuditFleet_CheckPositionPassGrace()` - 10s grace for reconnect stop-replace
- `AuditFleet_HandleCriticalDesyncFlatten()` - Sign mismatch or unexpected position flatten
- `AuditFleet_HandleNakedPosition()` - Missing stop detection + emergency stop queue
- `EnqueueReaperRepairCandidate()` - Repair queue with in-flight guard
- `EnqueueReaperFlattenCandidate()` - Flatten queue with in-flight guard
- `EnqueueReaperNakedStopCandidate()` - Naked stop queue with grace window
- `ProcessReaperFlattenQueue()` - Strategy-thread flatten execution
- `TerminateFsmsForAccount()` - FSM cleanup on flatten

**V12 DNA Compliance**:
- ✅ Zero lock() - ConcurrentDictionary.TryAdd for guards
- ✅ FSM Authority - GetFsmExpectedPosition() is sole source of truth
- ✅ Grace Windows - Per-account fill grace, naked grace, Position Pass grace
- ✅ Atomic Enqueue - TryAdd before Enqueue prevents double-queue

**Critical Logic**:
- **Ghost Position**: actual=0, expected!=0 → repair (re-issue entry)
- **Critical Desync**: sign mismatch OR (actual!=0, expected=0 after grace) → flatten
- **Minor Desync**: magnitude mismatch only → log, no action
- **Naked Position**: position exists, no working stop, grace expired → emergency hard stop

### 3. V12_002.REAPER.Repair.cs (272 lines)
**Purpose**: Ghost position repair engine - re-issues missed entry orders  
**Key Components**:
- `ProcessReaperRepairQueue()` - Strategy-thread repair queue drain
- `ExecuteReaperRepair()` - Single-repair orchestrator (Build 935 extraction)
- `ValidateRepairEligibility()` - Flatten check, PositionInfo lookup, orphan self-heal
- `CalculateRepairOrderPrices()` - OrderType-based price calculation
- `ValidateRepairRiskBounds()` - ATR-derived hard bound + legacy tick fence
- `SubmitRepairOrderWithAuthorization()` - FSM/dispatch authorization + order submission

**V12 DNA Compliance**:
- ✅ Zero lock() - ConcurrentDictionary for _repairInFlight
- ✅ Atomic Cleanup - finally block guarantees _repairInFlight.TryRemove
- ✅ Authorization Chain - FSM → dispatch reservation → position fallback
- ✅ Risk Bounds - ATR-derived limit + legacy tick fence (dual guard)

**Critical Logic**:
- **Orphan Self-Heal**: 3 failed repair attempts (no PositionInfo) → force-zero expectedPositions
- **Repair Authorization**: Requires FSM OR dispatch reservation OR active position
- **Risk Fence**: Repair blocked if price moved > ATR limit OR > RepairTickFence ticks
- **Metadata Guard**: MetadataGuardRepairAuthorized() prevents unauthorized repairs

### 4. V12_002.REAPER.NakedStop.cs (84 lines)
**Purpose**: Emergency hard stop for naked positions (missing protection)  
**Key Components**:
- `ProcessReaperNakedStopQueue()` - Strategy-thread naked stop queue drain
- Emergency stop calculation: MaximumStop OR ATR bound (whichever is smaller)
- StopMarket order submission at calculated distance from Close[0]

**V12 DNA Compliance**:
- ✅ Zero lock() - ConcurrentDictionary for _reaperNakedStopInFlight
- ✅ Atomic Cleanup - TryRemove on success AND failure (Build 969.3)
- ✅ ATR Bound - CalculateATRStopDistance() caps emergency stop distance
- ✅ Strategy Thread - Close[0] safe (runs via TriggerCustomEvent)

**Critical Logic**:
- **Emergency Distance**: MIN(MaximumStop, ATR bound, MinimumStop fallback)
- **Long Position**: stopPrice = Close[0] - emergencyStopDist
- **Short Position**: stopPrice = Close[0] + emergencyStopDist
- **In-Flight Clear**: TryRemove on both success and failure (prevents lockout)

### 5. V12_002.Safety.Watchdog.cs (309 lines)
**Purpose**: Deadlock detection and emergency flatten (last-resort safety)  
**Key Components**:
- `StartWatchdog()` / `StopWatchdog()` - Watchdog timer lifecycle
- `OnWatchdogTimer()` - Heartbeat age check + escalation logic
- `TouchStrategyHeartbeat()` - Heartbeat stamp (called from strategy thread)
- `ExecuteWatchdogLeadAccountFlatten()` - Stage 1: Enqueue emergency flatten
- `ExecuteWatchdogDirectFallback()` - Stage 2: Direct Account.Cancel + Account.Submit
- `HasWatchdogLeadAccountWorkingOrder()` - Exposure check (only fire if orders exist)

**V12 DNA Compliance**:
- ✅ Zero lock() - Interlocked for _watchdogStage, _strategyHeartbeatTicks
- ✅ Atomic Stage - CompareExchange for stage transitions (0→1→2)
- ✅ Timer Lifecycle - Interlocked.Exchange for _watchdogTimer disposal
- ✅ Escalation Path - Stage 1 (Enqueue) → Stage 2 (Direct API) if Stage 1 fails

**Critical Logic**:
- **Timeout**: 5 seconds (WatchdogTimeoutTicks = 50,000,000 ticks)
- **Stage 0**: Normal operation (heartbeat fresh)
- **Stage 1**: Deadlock detected → Enqueue lead account flatten
- **Stage 2**: Stage 1 failed → Direct Account.Cancel + Account.Submit fallback
- **Exposure Check**: Only fire if HasWatchdogLeadAccountWorkingOrder() = true

---

## Concurrency Patterns

### Background Timer → Strategy Thread Marshalling
```csharp
// Pattern: Background timer → TriggerCustomEvent → strategy thread execution
private void OnReaperTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
{
    try
    {
        TriggerCustomEvent(o => AuditApexPositions(), null);
    }
    catch (Exception ex)
    {
        Print("[REAPER] Timer Marshalling Error: " + ex.Message);
    }
}
```

### Atomic In-Flight Guard Pattern
```csharp
// Pattern: TryAdd before Enqueue, TryRemove in finally
string repairKey = acct.Name + "_" + Instrument.FullName;
if (_repairInFlight.TryAdd(repairKey, 0))
{
    _reaperRepairQueue.Enqueue(acct.Name);
    try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
    catch (Exception ex)
    {
        _repairInFlight.TryRemove(repairKey, out _); // Clear on failure
    }
}
// ExecuteReaperRepair() has finally { _repairInFlight.TryRemove(repairKey, out _); }
```

### Per-Account Fill Grace (Build 935 Fix)
```csharp
// OLD: Single global _lastExpectedPositionSetTicks blocked ALL repairs
// NEW: Per-account grace allows Account A fill while Account B repairs
private void StampAccountFillGrace(string expKey)
{
    _accountFillGraceTicks[expKey] = DateTime.UtcNow.Ticks;
}

private bool IsReaperFillGraceActive(string expKey)
{
    if (_accountFillGraceTicks.TryGetValue(expKey, out long stampTicks))
    {
        return stampTicks > 0 && (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;
    }
    // Fallback to global stamp for master account
    long globalStamp = Interlocked.Read(ref _lastExpectedPositionSetTicks);
    return globalStamp > 0 && (DateTime.UtcNow.Ticks - globalStamp) < ReaperFillGraceTicks;
}
```

---

## V12 DNA Compliance Audit

### ✅ Zero lock() Statements
**Status**: PASS  
**Evidence**: All concurrency uses atomic primitives:
- `ConcurrentQueue<T>` for emergency action queues
- `ConcurrentDictionary<K,V>` for in-flight guards and grace tracking
- `Interlocked.Read/Exchange/CompareExchange` for watchdog stage and heartbeat
- `Volatile.Read` for _watchdogStage reads

### ✅ Atomic In-Flight Guards
**Status**: PASS  
**Evidence**: TryAdd → Enqueue → TryRemove pattern prevents duplicate emergency actions:
- `_repairInFlight` - Repair dedup
- `_reaperFlattenInFlight` - Flatten dedup
- `_reaperNakedStopInFlight` - Naked stop dedup

### ✅ Thread-Safe Marshalling
**Status**: PASS  
**Evidence**: All background timer actions use TriggerCustomEvent:
- `OnReaperTimerElapsed()` → `TriggerCustomEvent(o => AuditApexPositions(), null)`
- Repair/Flatten/NakedStop enqueue → `TriggerCustomEvent(o => ProcessQueue(), null)`
- Watchdog escalation → `TriggerCustomEvent(o => ExecuteWatchdogLeadAccountFlatten(), null)`

### ✅ Grace Window Logic
**Status**: PASS  
**Evidence**: Three grace windows prevent false positives:
- **Fill Grace**: 2s (ReaperFillGraceTicks) - per-account since Build 935
- **Naked Position Grace**: 5-10s (NakedPositionGraceSec) - prevents race during bracket confirmation
- **Position Pass Grace**: 10s - defers critical desync during reconnect stop-replace

### ✅ FSM Authority
**Status**: PASS  
**Evidence**: `GetFsmExpectedPosition()` is sole source of truth for expected position (Build 1105)

---

## Test Coverage Strategy (30 Tests)

### Phase 1: REAPER Timer & Lifecycle (T01-T06) - 6 tests
- T01: StartReaperAudit_InitializesTimer
- T02: StopReaperAudit_DisposesTimer
- T03: OnReaperTimerElapsed_SkipsIfFlattenRunning
- T04: OnReaperTimerElapsed_SkipsIfNotRealtime
- T05: OnReaperTimerElapsed_MarshalsToStrategyThread
- T06: AuditApexPositions_IteratesFleetAccounts

### Phase 2: Desync Detection & Repair (T07-T12) - 6 tests
- T07: AuditFleet_GhostPosition_EnqueuesRepair
- T08: AuditFleet_CriticalDesync_SignMismatch_EnqueuesFlatten
- T09: AuditFleet_CriticalDesync_UnexpectedPosition_EnqueuesFlatten
- T10: AuditFleet_MinorDesync_LogsOnly
- T11: AuditFleet_FillGrace_DefersRepair
- T12: AuditFleet_PositionPassGrace_DefersCriticalDesync

### Phase 3: Repair Engine (T13-T18) - 6 tests
- T13: ExecuteReaperRepair_ValidatesEligibility_AbortsIfFlatten
- T14: ExecuteReaperRepair_OrphanSelfHeal_ThreeAttempts
- T15: ExecuteReaperRepair_CalculatesOrderPrices_ByOrderType
- T16: ExecuteReaperRepair_RiskBounds_ATRLimit
- T17: ExecuteReaperRepair_RiskBounds_TickFence
- T18: ExecuteReaperRepair_Authorization_FSMOrDispatch

### Phase 4: Naked Position Detection (T19-T24) - 6 tests
- T19: AuditFleet_NakedPosition_StartsGraceWindow
- T20: AuditFleet_NakedPosition_GraceExpired_EnqueuesEmergencyStop
- T21: ProcessReaperNakedStopQueue_CalculatesEmergencyDistance
- T22: ProcessReaperNakedStopQueue_LongPosition_StopBelowClose
- T23: ProcessReaperNakedStopQueue_ShortPosition_StopAboveClose
- T24: ProcessReaperNakedStopQueue_ClearsInFlightOnSuccess

### Phase 5: Watchdog & Flatten (T25-T30) - 6 tests
- T25: Watchdog_HeartbeatFresh_Stage0
- T26: Watchdog_DeadlockDetected_Stage1_EnqueuesFlatten
- T27: Watchdog_Stage1Failed_Stage2_DirectFallback
- T28: Watchdog_NoExposure_SkipsEscalation
- T29: ProcessReaperFlattenQueue_CancelsOrders_ClosesPositions
- T30: ProcessReaperFlattenQueue_TerminatesFsms

---

## Critical Findings

### 1. Per-Account Fill Grace (Build 935 Fix)
**Issue**: Original global `_lastExpectedPositionSetTicks` blocked ALL account repairs when ANY account filled.  
**Fix**: `_accountFillGraceTicks` dictionary provides per-account grace windows.  
**Impact**: Account A fill no longer blocks Account B repair.

### 2. Orphan Self-Heal (Build 946)
**Issue**: Ghost position with no PositionInfo caused infinite repair loop.  
**Fix**: `_reaperOrphanRepairCount` tracks failed attempts; 3 failures → force-zero expectedPositions.  
**Impact**: Prevents repair lockout when PositionInfo is missing.

### 3. Position Pass Grace (Build 999)
**Issue**: Reconnect with position but no FSM (stop in CancelPending) triggered immediate critical desync.  
**Fix**: `_positionPassFailedFirstSeen` provides 10s grace for stop-replace cycle to complete.  
**Impact**: Prevents false flatten during reconnect recovery.

### 4. Atomic In-Flight Cleanup (Build 969.3)
**Issue**: TriggerCustomEvent failure left in-flight guard set, causing permanent lockout.  
**Fix**: TryRemove in catch block + finally block guarantees cleanup on ALL exit paths.  
**Impact**: Prevents single failure from blocking future emergency actions.

### 5. Watchdog Two-Stage Escalation
**Issue**: Single-stage flatten could fail if strategy thread is deadlocked.  
**Fix**: Stage 1 (Enqueue) → Stage 2 (Direct Account API) provides fallback path.  
**Impact**: Last-resort safety even if strategy thread is unresponsive.

---

## Recommended Test Approach

### Mock Infrastructure
- **MockReaperTimer**: Simulates background timer with manual Advance()
- **MockAccount**: Tracks positions, orders, and flatten calls
- **MockFSM**: Simulates FollowerBracketFSM state for expected position calculation
- **MockQueue**: ConcurrentQueue wrapper with inspection methods
- **MockInFlightGuard**: ConcurrentDictionary wrapper with TryAdd/TryRemove tracking

### Test Helpers
- `SimulateGhostPosition()` - actual=0, expected!=0
- `SimulateCriticalDesync()` - sign mismatch or unexpected position
- `SimulateNakedPosition()` - position with no working stop
- `SimulateDeadlock()` - heartbeat age > 5s
- `AdvanceGraceWindow()` - Fast-forward time for grace expiration
- `AssertRepairEnqueued()` - Verify repair queue contains account
- `AssertFlattenEnqueued()` - Verify flatten queue contains account
- `AssertEmergencyStopEnqueued()` - Verify naked stop queue contains account

### Assertion Patterns
- `AssertInFlightGuardSet()` - Verify TryAdd succeeded
- `AssertInFlightGuardCleared()` - Verify TryRemove succeeded
- `AssertGraceWindowActive()` - Verify grace timestamp within window
- `AssertWatchdogStage()` - Verify atomic stage transition (0→1→2)

---

## File Statistics

| File | Lines | Methods | Complexity | Purpose |
|------|-------|---------|------------|---------|
| V12_002.REAPER.cs | 156 | 6 | Low | Timer lifecycle, queues, grace tracking |
| V12_002.REAPER.Audit.cs | 730 | 20 | High | Fleet audit, desync detection, triage |
| V12_002.REAPER.Repair.cs | 272 | 7 | Medium | Ghost position repair engine |
| V12_002.REAPER.NakedStop.cs | 84 | 1 | Low | Emergency hard stop for naked positions |
| V12_002.Safety.Watchdog.cs | 309 | 10 | Medium | Deadlock detection, emergency flatten |
| **TOTAL** | **1,351** | **44** | **Medium** | **REAPER Defense System** |

---

## Next Steps (P3 Architecture Planning)

1. Design 30-test suite mirroring S1/S2/S3 pattern
2. Define mock infrastructure (MockReaperTimer, MockAccount, MockFSM, MockQueue)
3. Specify test helpers (12 assertion, 6 simulation, 4 verification, 3 creation)
4. Map each test to specific REAPER behavior (grace windows, queue processing, escalation)
5. Ensure V12 DNA compliance (zero lock(), atomic guards, MockTime pattern)

---

**P2 Forensic Intake Complete**  
**Status**: ✅ READY FOR P3 ARCHITECTURE PLANNING  
**Confidence**: HIGH (Clear emergency response patterns, atomic concurrency, grace window logic)