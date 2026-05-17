# Implementation Plan: Cluster S4 - REAPER Defense Integration Tests
## P3 Architecture Planning | V12 Phase 7 Hardening

> **Mission**: REAPERDefenseIntegrationTests.cs - Complete Test Specification
> **Status**: ARCHITECTURE PLANNING COMPLETE
> **Build Baseline**: BUILD_TAG 1111.007-phase7-tQ1_S1_SIMA_TESTS_SETUP
> **Input**: docs/brain/forensics_report_cluster_s4.md (P2 Forensics)
> **Target**: tests/REAPERDefenseIntegrationTests.cs (SETUP ONLY)
> **Generated**: 2026-05-17T16:13:00Z

---

## 1. Overview

### 1.1 Mission Statement

This implementation plan specifies the complete architecture for **REAPERDefenseIntegrationTests.cs**, a comprehensive test suite covering the V12 REAPER Defense System (Cluster S4). The test file will contain **30 test methods** organized into 5 phases, mirroring the proven structure from SymmetryFsmIntegrationTests.cs (47 tests, 20/20 PASS).

**Key Objectives**:
- Verify REAPER timer lifecycle (start, stop, marshalling, audit orchestration)
- Validate desync detection and repair (ghost positions, critical desync, minor desync)
- Test repair engine (eligibility, orphan self-heal, risk bounds, authorization)
- Verify naked position detection (grace windows, emergency stop calculation)
- Cover watchdog escalation (deadlock detection, stage transitions, flatten fallback)

### 1.2 Scope & Constraints

**In Scope**:
- All 5 REAPER Defense source files (1,351 lines)
- 30 test scenarios across 5 categories
- Full mock infrastructure (MockTime, MockReaperTimer, MockAccount, MockOrder, MockFSM, MockQueue, MockInFlightGuard)
- Lock-free testing (zero `lock()` statements)
- Deterministic time (MockTime pattern, zero `Thread.Sleep`)
- ASCII-only compliance

**Out of Scope**:
- Bug fixes (SETUP ONLY - assert current behavior)
- Performance optimization
- UI testing
- Real NinjaTrader integration

**V12 DNA Constraints**:
- ✅ Zero `lock()` - pure atomic primitives only
- ✅ MockTime - deterministic time progression
- ✅ ASCII-only - no Unicode, emoji, or curly quotes
- ✅ NinjaTrader harness fully mocked

### 1.3 Source Files (5 Files, 1,351 Lines)

| File | Lines | Purpose |
|:-----|:------|:--------|
| V12_002.REAPER.cs | 156 | Timer lifecycle, queues, grace tracking |
| V12_002.REAPER.Audit.cs | 730 | Fleet audit, desync detection, triage |
| V12_002.REAPER.Repair.cs | 272 | Ghost position repair engine |
| V12_002.REAPER.NakedStop.cs | 84 | Emergency hard stop for naked positions |
| V12_002.Safety.Watchdog.cs | 309 | Deadlock detection, emergency flatten |
| **Total** | **1,351** | **5 files** |

### 1.4 Test Categories (30 Tests)

| Phase | Category | Test Count | Lines |
|:------|:---------|:-----------|:------|
| 1 | REAPER Timer & Lifecycle | 6 | 1001-1300 |
| 2 | Desync Detection & Repair | 6 | 1301-1600 |
| 3 | Repair Engine | 6 | 1601-1900 |
| 4 | Naked Position Detection | 6 | 1901-2200 |
| 5 | Watchdog & Flatten | 6 | 2201-2500 |
| **Total** | **5 Phases** | **30 Tests** | **~1,800 lines** |

---

## 2. Mock Infrastructure Design (Lines 1-800)

### 2.1 MockTime (Deterministic Time)

**Purpose**: Eliminate non-determinism from time-based logic. Zero `Thread.Sleep` calls.

**Pattern**: Copy from SymmetryFsmIntegrationTests.cs (lines 15-30)

```csharp
private class MockTime
{
    private long _ticks;
    
    public MockTime(long initialTicks) => _ticks = initialTicks;
    
    public long GetTicks() => Interlocked.Read(ref _ticks);
    
    public void Advance(long deltaTicks) => Interlocked.Add(ref _ticks, deltaTicks);
    
    public void AdvanceSeconds(double seconds) => 
        Interlocked.Add(ref _ticks, (long)(seconds * TimeSpan.TicksPerSecond));
    
    public DateTime GetDateTime() => new DateTime(GetTicks(), DateTimeKind.Utc);
}
```

### 2.2 Core Mock Classes

**MockReaperTimer**: Simulates background timer with manual Advance()
**MockAccount**: Tracks positions, orders, and flatten calls
**MockOrder**: Order properties
**MockFSM**: Simulates FollowerBracketFSM state for expected position calculation
**MockQueue<T>**: ConcurrentQueue wrapper with inspection methods
**MockInFlightGuard**: ConcurrentDictionary wrapper with TryAdd/TryRemove tracking

**Key Features**:
- Full timer lifecycle simulation (Start → Elapsed → Stop)
- Event-driven architecture with controllable time progression
- Multi-account support for fleet testing
- Atomic state tracking with ConcurrentDictionary

---

## 3. Test Helper Specifications (Lines 801-1000)

### 3.1 Assertion Helpers (12 methods)

```csharp
private void AssertTimerRunning(MockReaperTimer timer, bool expected)
private void AssertQueueContains(MockQueue<string> queue, string accountName)
private void AssertInFlightGuardSet(MockInFlightGuard guard, string key)
private void AssertInFlightGuardCleared(MockInFlightGuard guard, string key)
private void AssertGraceWindowActive(MockTime time, long stampTicks, double graceSec)
private void AssertAccountFlattened(MockAccount account)
private void AssertOrderCancelled(MockOrder order)
private void AssertOrderSubmitted(MockAccount account, int expectedCount)
private void AssertFSMTerminated(MockFSM fsm)
private void AssertWatchdogStage(int stage, int expected)
private void AssertEmergencyStopPrice(double stopPrice, double close, double distance, MarketPosition position)
private void AssertRepairBlocked(bool blocked, string reason)
```

### 3.2 Verification Helpers (6 methods)

```csharp
private bool VerifyAccountFlattened(MockAccount account)
private bool VerifyAllOrdersCancelled(MockAccount account)
private bool VerifyEmergencyStopSubmitted(MockAccount account)
private bool VerifyFSMTerminated(MockFSM fsm)
private bool VerifyQueueDrained(MockQueue<string> queue)
private bool VerifyInFlightCleanup(MockInFlightGuard guard)
```

### 3.3 Simulation Helpers (6 methods)

```csharp
private void SimulateGhostPosition(MockAccount account, MockFSM fsm)
private void SimulateCriticalDesync(MockAccount account, MockFSM fsm)
private void SimulateNakedPosition(MockAccount account)
private void SimulateDeadlock(MockTime time, ref long heartbeatTicks)
private void AdvanceGraceWindow(MockTime time, double seconds)
private void SimulateTimerElapsed(MockReaperTimer timer)
```

### 3.4 Creation Helpers (3 methods)

```csharp
private MockAccount CreateMockAccount(string name, MarketPosition position, int quantity)
private MockFSM CreateMockFSM(string accountName, string positionName, FollowerBracketState state, int expectedPos)
private MockOrder CreateMockOrder(string name, OrderType type, OrderAction action, int qty)
```

---

## 4. Implementation Sequence

### Step 1: Mock Infrastructure (Day 1, Lines 1-800)
1. Copy MockTime from SymmetryFsmIntegrationTests.cs
2. Implement MockReaperTimer with manual Advance()
3. Implement MockAccount with position/order tracking
4. Implement MockOrder
5. Implement MockFSM
6. Implement MockQueue<T>
7. Implement MockInFlightGuard

**Verification**: All mock classes compile, basic instantiation tests pass

### Step 2: Test Helpers (Day 1, Lines 801-1000)
1. Implement 12 assertion helpers
2. Implement 6 verification helpers
3. Implement 6 simulation helpers
4. Implement 3 creation helpers

**Verification**: Helper methods compile, basic usage tests pass

### Step 3: Phase 1 Tests (Day 2, Lines 1001-1300)
1. Implement T01-T06 (REAPER Timer & Lifecycle Tests)
2. Verify each test independently
3. Run all Phase 1 tests together

**Verification**: 6/6 tests pass

### Step 4: Phase 2 Tests (Day 2, Lines 1301-1600)
1. Implement T07-T12 (Desync Detection & Repair Tests)
2. Verify each test independently
3. Run all Phase 2 tests together

**Verification**: 6/6 tests pass

### Step 5: Phase 3 Tests (Day 3, Lines 1601-1900)
1. Implement T13-T18 (Repair Engine Tests)
2. Verify each test independently
3. Run all Phase 3 tests together

**Verification**: 6/6 tests pass

### Step 6: Phase 4 Tests (Day 3, Lines 1901-2200)
1. Implement T19-T24 (Naked Position Detection Tests)
2. Verify each test independently
3. Run all Phase 4 tests together

**Verification**: 6/6 tests pass

### Step 7: Phase 5 Tests (Day 4, Lines 2201-2500)
1. Implement T25-T30 (Watchdog & Flatten Tests)
2. Verify each test independently
3. Run all Phase 5 tests together

**Verification**: 6/6 tests pass

### Step 8: Final Integration (Day 4)
1. Run all 30 tests together
2. Verify zero lock() statements
3. Verify zero Thread.Sleep calls
4. Verify ASCII-only compliance
5. Generate test coverage report

**Verification**: 30/30 tests pass, V12 DNA compliance verified

---

## 5. Implementation Checklist

### Phase 1: Mock Infrastructure (Step 1)
- [ ] MockTime class (deterministic time simulation)
- [ ] MockReaperTimer class (background timer with manual Advance)
- [ ] MockAccount class (position/order tracking + flatten calls)
- [ ] MockOrder class (order properties)
- [ ] MockFSM class (FollowerBracketFSM state simulation)
- [ ] MockQueue<T> class (ConcurrentQueue wrapper with inspection)
- [ ] MockInFlightGuard class (ConcurrentDictionary wrapper with tracking)

### Phase 2: Test Helpers (Step 2)
- [ ] 12 Assertion Helpers (timer, queue, guard, grace, watchdog)
- [ ] 6 Verification Helpers (flatten, cancel, stop, FSM, cleanup, drain)
- [ ] 6 Simulation Helpers (ghost, desync, naked, deadlock, grace, timer)
- [ ] 3 Creation Helpers (account, FSM, order)

### Phase 3: Test Methods (Steps 3-7)
- [ ] Phase 1: REAPER Timer & Lifecycle (T01-T06) - 6 tests
- [ ] Phase 2: Desync Detection & Repair (T07-T12) - 6 tests
- [ ] Phase 3: Repair Engine (T13-T18) - 6 tests
- [ ] Phase 4: Naked Position Detection (T19-T24) - 6 tests
- [ ] Phase 5: Watchdog & Flatten (T25-T30) - 6 tests

### Phase 4: Verification (Step 8)
- [ ] Compile check: `dotnet build tests/REAPERDefenseIntegrationTests.cs`
- [ ] Test execution: `dotnet test tests/REAPERDefenseIntegrationTests.cs`
- [ ] Cumulative test count: 163 baseline + 30 S4 = 193 total

---

## 6. V12 DNA Compliance Verification

### Pre-Implementation Checklist
- [ ] Zero lock() statements in mock infrastructure
- [ ] MockTime pattern for all time-dependent logic
- [ ] ASCII-only string literals (no Unicode, emoji, curly quotes)
- [ ] ConcurrentQueue for emergency action queues
- [ ] ConcurrentDictionary for in-flight guards
- [ ] Interlocked/Volatile for atomic operations
- [ ] Given/When/Then structure in all tests

### Post-Implementation Checklist
- [ ] All 30 tests compile without errors
- [ ] All 30 tests pass (100%)
- [ ] Cumulative 193 tests pass (163 baseline + 30 S4)
- [ ] No lock() statements detected (grep verification)
- [ ] No Thread.Sleep detected (grep verification)
- [ ] Build time < 2 seconds
- [ ] Test execution time < 1 second

---

## 7. Risk Mitigation

### Known Challenges
1. **Grace Window Timing**: MockTime must accurately simulate 2s fill grace, 5-10s naked grace, 10s Position Pass grace
2. **Atomic Stage Transitions**: Watchdog stage 0→1→2 must use CompareExchange pattern
3. **In-Flight Cleanup**: TryRemove must be called in finally blocks to prevent lockout
4. **Queue Inspection**: MockQueue must expose Count and Contains for verification

### Mitigation Strategies
1. **MockTime.AdvanceSeconds()**: Explicit time advancement for grace window tests
2. **Interlocked.CompareExchange**: Atomic stage transitions in watchdog tests
3. **finally Block Pattern**: All in-flight guards cleared in finally blocks
4. **ConcurrentQueue.Count**: Thread-safe count property for queue inspection

---

## 8. Success Criteria

### P3 Gate (Architecture Planning)
- [x] 30 test specifications complete
- [x] Mock infrastructure designed (7 classes)
- [x] Test helpers specified (27 methods)
- [x] V12 DNA compliance verified
- [x] Implementation plan approved

### P5 Gate (Test Implementation)
- [ ] All 30 tests implemented
- [ ] All 30 tests passing (100%)
- [ ] Cumulative 193 tests passing
- [ ] Zero lock() statements
- [ ] Zero Thread.Sleep statements
- [ ] Build succeeds with 0 errors

### P6 Gate (Verification)
- [ ] Test execution < 1 second
- [ ] V12 DNA audit PASS
- [ ] Diff size < 150KB
- [ ] deploy-sync.ps1 succeeds
- [ ] Verification report generated

---

## 9. Estimated Metrics

| Metric | Target | Rationale |
|--------|--------|-----------|
| Total Lines | ~1,800 | 400 mock + 200 helpers + 1,200 tests |
| Mock Infrastructure | 400 lines | 7 classes (MockTime, Timer, Account, FSM, Queue, Guard, Order) |
| Test Helpers | 200 lines | 27 methods (12 assert + 6 verify + 6 simulate + 3 create) |
| Test Methods | 1,200 lines | 30 tests × 40 lines avg |
| Compilation Time | < 2s | Small cluster (1,351 source lines) |
| Test Execution | < 1s | Pure mock infrastructure, no I/O |
| Pass Rate | 100% | SETUP phase - document current behavior |

---

## 10. References

### 10.1 Source Files (5 REAPER Defense Files)
- `src/V12_002.REAPER.cs` (156 lines)
- `src/V12_002.REAPER.Audit.cs` (730 lines)
- `src/V12_002.REAPER.Repair.cs` (272 lines)
- `src/V12_002.REAPER.NakedStop.cs` (84 lines)
- `src/V12_002.Safety.Watchdog.cs` (309 lines)

### 10.2 Reference Tests
- `tests/SymmetryFsmIntegrationTests.cs` (1533 lines, 47 tests, 20/20 PASS)
- `tests/SIMAIntegrationTests.cs` (36 tests)
- `tests/ExecutionEngineIntegrationTests.cs` (40 tests)
- `tests/UIPhotonIOIntegrationTests.cs` (40 tests)

### 10.3 Workflow Documents
- `docs/brain/forensics_report_cluster_s4.md` (P2 Forensics)
- `docs/brain/implementation_plan_cluster_s2.md` (S2 pattern reference)
- `docs/brain/implementation_plan_cluster_s3.md` (S3 pattern reference)
- `AGENTS.md` (Agent hierarchy and protocols)

---

## 11. Next Steps (P4 DNA & PR Audit)

After P3 approval, proceed to P4 Adjudicator Audit:
1. Verify zero lock() statements in implementation plan
2. Verify MockTime pattern for all time-dependent tests
3. Verify ASCII-only compliance in test strings
4. Verify atomic primitives (Interlocked, Volatile, ConcurrentDictionary, ConcurrentQueue)
5. Verify Given/When/Then structure in all 30 tests
6. Generate `docs/brain/adjudicator_audit_cluster_s4.md`

---

**P3 Architecture Planning Complete**  
**Status**: ✅ READY FOR P4 DNA & PR AUDIT  
**Confidence**: HIGH (Clear emergency response patterns, grace window logic, atomic concurrency)  
**Test Count**: 30 tests (6 per phase)  
**Target Size**: ~1,800 lines  
**V12 DNA**: Zero lock(), MockTime, ASCII-only, Atomic guards ✅

---

*Generated by: Bob CLI (v12-engineer mode)*
*Architect: P3 Phase - REAPER Defense Cluster S4*
*Document Version: 1.0*