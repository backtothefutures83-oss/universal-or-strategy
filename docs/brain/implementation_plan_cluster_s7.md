# P3 Architecture Planning: S7 Orchestration & Integration Test Suite

**Cluster**: S7 - Orchestration & Integration (Lifecycle, Actor, SIMA Toggle, FSM, Initialization)  
**Files**: 5 core files (Lifecycle.cs, V12_002.cs, SIMA.Lifecycle.cs, Symmetry.BracketFSM.cs, SIMA.cs)  
**Planning Date**: 2026-05-17  
**Architect**: Bob CLI (v12-engineer)  
**BUILD_TAG_BASELINE**: 1111.007-phase7-tQ1_S6_METRICS_TESTS_COMPLETE  
**TARGET_BUILD_TAG**: 1111.007-phase7-tQ1_S7_ORCHESTRATION_TESTS_COMPLETE

---

## Executive Summary

The S7 Orchestration & Integration cluster tests the **core lifecycle state machine**, **Actor pattern command queue**, **SIMA toggle gate**, **FSM state transitions**, and **initialization sequence** that orchestrate V12's multi-account fleet execution. This is the **control plane** that coordinates all other subsystems.

**Key Characteristics**:
- **Lifecycle FSM**: SetDefaults → Configure → DataLoaded → Realtime → Terminated
- **Actor Pattern**: Lock-free `ConcurrentQueue<StrategyCommand>` with `TryDrain()` execution
- **SIMA Toggle**: Atomic spin-wait gate (`_simaToggleState`) with max 3 retries
- **FSM State Packing**: 64-bit atomic (State:8 + Pending:1 + Generation:55)
- **Initialization Sequence**: InstrumentConfig → TargetConfig → Indicators → SessionLogging → Services
- **Zero lock() Compliance**: Pure atomic primitives throughout

**Test Strategy**: 28 tests documenting current orchestration behavior, state transitions, actor execution order, SIMA toggle mechanics, and initialization dependencies.

---

## 1. Test Suite Architecture

### 1.1 Test Class Structure

```csharp
public class OrchestrationIntegrationTests
{
    #region Mock NinjaTrader Types (Lines 25-31)
    // Enums: State, MarketPosition, OrderAction, OrderState, OrderType
    
    #region Mock Infrastructure (Lines 33-450)
    // MockTime, MockAccount, MockOrder, MockExecution, MockActorQueue, MockFSM
    
    #region Test Helpers (Lines 451-650)
    // 12 Assertion helpers, 6 Verification helpers, 6 Simulation helpers, 3 Creation helpers
    
    #region Phase 1: Lifecycle State Transitions (T01-T06)
    #region Phase 2: Actor Pattern Execution (T07-T12)
    #region Phase 3: SIMA Lifecycle Toggle (T13-T18)
    #region Phase 4: FSM State Transitions (T19-T24)
    #region Phase 5: Initialization Sequence & Shutdown (T25-T28)
}
```

### 1.2 Mock Harness Components

**Core Mocks**:
1. **MockTime**: Deterministic time simulation (zero `Thread.Sleep`)
2. **MockAccount**: Fleet account enumeration and subscription tracking
3. **MockOrder**: Broker order lifecycle simulation
4. **MockExecution**: Fill event simulation
5. **MockActorQueue**: Command queue with execution log
6. **MockFSM**: 64-bit atomic state packing simulation

**Total Mock Infrastructure**: ~400 lines

---

## 2. Test Scenarios Summary (28 Tests)

### Phase 1: Lifecycle State Transitions (T01-T06) - 6 tests
- T01: SetDefaults initializes collections
- T02: Configure creates data series
- T03: DataLoaded initializes indicators
- T04: Realtime starts services
- T05: Terminated shutdown sequence
- T06: State progression validation

### Phase 2: Actor Pattern Execution (T07-T12) - 6 tests
- T07: Enqueue adds to queue
- T08: TryDrain executes commands
- T09: DrainToken prevents re-entrant
- T10: Broker call budget yields after 5 calls
- T11: Time budget yields after 10ms
- T12: Queue saturation logs warning

### Phase 3: SIMA Lifecycle Toggle (T13-T18) - 6 tests
- T13: Enable enumerates accounts
- T14: Disable unsubscribes accounts
- T15: Spin-wait acquires gate
- T16: Pending retry mechanism
- T17: REAPER gate pauses during toggle
- T18: Mid-session reconnect re-adopts orders

### Phase 4: FSM State Transitions (T19-T24) - 6 tests
- T19: Packed state atomic 64-bit
- T20: TryTransition atomic state change
- T21: ResolveFsm 3-tier lookup
- T22: HandleFilled updates remaining contracts
- T23: GetFsmExpectedPosition sums non-terminal
- T24: TerminateBracket removes OrderId mappings

### Phase 5: Initialization & Shutdown (T25-T28) - 4 tests
- T25: InstrumentConfig sets MES defaults
- T26: TargetConfiguration backward-compat
- T27: Services starts IPC and watchdog
- T28: Shutdown drains queues before cleanup

---

## 3. V12 DNA Compliance

### 3.1 Zero lock() Statements
**Verification**: `grep -r "lock(" tests/OrchestrationIntegrationTests.cs` → Zero matches

**Enforcement**:
- All concurrency uses atomic primitives
- `ConcurrentQueue`, `ConcurrentDictionary` for collections
- `Interlocked.CompareExchange/Exchange/Read` for state
- `Volatile.Read/Write` for visibility

### 3.2 MockTime Pattern (Zero Thread.Sleep)
**Verification**: `grep -r "Thread.Sleep" tests/OrchestrationIntegrationTests.cs` → Zero matches

**Enforcement**:
- All time-based tests use `MockTime.Advance()`
- Grace windows tested via explicit time advancement
- No real-time delays

### 3.3 ASCII-Only Strings
**Verification**: No Unicode, emoji, or curly quotes in test code

**Enforcement**:
- All string literals use ASCII characters only
- Test names use underscores (not em-dashes)

---

## 4. Key Architectural Decisions

### 4.1 Actor Pattern Testing
**Decision**: Use `MockActorQueue` with execution log to verify command order.

**Rationale**: Production Actor pattern is lock-free and order-dependent. Execution log provides deterministic verification without instrumenting production code.

### 4.2 FSM State Packing
**Decision**: Mirror production 64-bit packing in `MockFSM`.

**Rationale**: FSM state packing is critical for atomicity. Mock must match production bit layout to catch packing bugs.

### 4.3 SIMA Toggle Spin-Wait
**Decision**: Test spin-wait gate with concurrent threads.

**Rationale**: SIMA toggle gate is a critical concurrency primitive. Must verify spin-wait behavior and retry limit.

### 4.4 Lifecycle State Progression
**Decision**: Test full state progression (SetDefaults → Terminated) in single test.

**Rationale**: Lifecycle states are interdependent. Full progression test catches state-skipping bugs.

---

## 5. Critical Findings from Source Analysis

### 5.1 Lifecycle Initialization Sequence
**Source**: [`OnStateChangeDataLoaded()`](src/V12_002.Lifecycle.cs:418)

**Strict Order**:
1. `Init_InstrumentConfig()` - Sets `tickSize`, `pointValue`
2. `Init_TargetConfiguration()` - Depends on instrument config
3. `Init_Indicators()` - Depends on `BarsArray[1]`
4. `Init_SessionLogging()` - Depends on instrument config
5. `Init_Services()` - Starts IPC, Watchdog

**Test Coverage**: T03, T25, T26, T27

### 5.2 Actor Pattern Budget System
**Source**: [`DrainActor()`](src/V12_002.cs:462)

**Dual Budget**:
1. **Broker Call Budget**: Max 5 calls per cycle
2. **Time Budget**: Max 10ms per cycle

**Test Coverage**: T10, T11

### 5.3 SIMA Toggle Atomic Cluster
**Source**: [`ProcessApplySimaState()`](src/V12_002.SIMA.Lifecycle.cs:41)

**Pattern**: Spin-wait with max 3 retries, sets `_simaTogglePending` on contention

**Test Coverage**: T15, T16

### 5.4 FSM 3-Tier Lookup Strategy
**Source**: [`ResolveFsmFromEvent()`](src/V12_002.Symmetry.BracketFSM.cs:329)

**Tiers**:
1. OrderId Map (O(1))
2. SignalName Parsing (O(1))
3. Full Scan (O(N))

**Test Coverage**: T21

### 5.5 Shutdown Sequence Atomic Cluster
**Source**: [`SetTerminatingAndStopWatchdog()`](src/V12_002.Lifecycle.cs:96)

**Invariant**: `_isTerminating` MUST be set BEFORE `StopWatchdog()` (INV-7.1/7.2)

**Test Coverage**: T05

---

## 6. Estimated Test File Size

| Section | Lines |
|---------|-------|
| Mock Infrastructure | 400 |
| Test Helpers | 200 |
| Phase 1 Tests (6) | 300 |
| Phase 2 Tests (6) | 300 |
| Phase 3 Tests (6) | 300 |
| Phase 4 Tests (6) | 300 |
| Phase 5 Tests (4) | 200 |
| **Total** | **2,000** |

---

## 7. Next Steps (P4 Implementation)

1. Create `tests/OrchestrationIntegrationTests.cs` skeleton
2. Implement mock infrastructure (MockTime, MockAccount, MockActorQueue, MockFSM)
3. Implement test helpers (12 assertion, 6 verification, 6 simulation, 3 creation)
4. Implement Phase 1 tests (T01-T06)
5. Implement Phase 2 tests (T07-T12)
6. Implement Phase 3 tests (T13-T18)
7. Implement Phase 4 tests (T19-T24)
8. Implement Phase 5 tests (T25-T28)
9. Run full test suite and verify all pass
10. Update BUILD_TAG to `1111.007-phase7-tQ1_S7_ORCHESTRATION_TESTS_COMPLETE`

---

**P3 Architecture Planning Complete**  
**Status**: ✅ READY FOR P4 IMPLEMENTATION  
**Confidence**: HIGH (Clear orchestration patterns, atomic concurrency, strict initialization sequence)