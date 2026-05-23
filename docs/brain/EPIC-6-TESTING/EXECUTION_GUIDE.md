# EPIC-6 Execution Guide

**Epic ID:** EPIC-6-TESTING  
**Build Tag:** 1111.011-epic6-testing  
**Phase:** EXECUTION  
**Date:** 2026-05-23  
**Agent:** Bob CLI (v12-engineer)

---

## EXECUTION OVERVIEW

This guide defines the ticket execution order for EPIC-6 Phase 1 (Performance Lock-In). Tickets are sequenced to respect compile dependencies and minimize rework. Total: 10 tickets across 3 tiers.

**Critical Path:** Ticket 01 (INinjaTraderMocks.cs) MUST be completed first - it is a hard compile dependency for all subsequent tickets.

---

## TICKET DEPENDENCY GRAPH

```
Tier 0: Foundation (BLOCKING)
┌─────────────────────────────────────┐
│ T01: INinjaTraderMocks.cs           │ ← MUST BE FIRST (compile dependency)
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│ T02: Project Setup                  │ ← Depends on T01
│   - V12_Performance.Benchmarks.csproj│
│   - V12_Performance.Tests.csproj    │
└─────────────────────────────────────┘
              ↓
Tier 1: Infrastructure Tests (PARALLEL after T02)
┌─────────────────────────────────────┐
│ T03: LatencyProbeTests.cs           │ ← Gap remediation (HIGH)
└─────────────────────────────────────┘
┌─────────────────────────────────────┐
│ T04: LogBufferThreadStaticTests.cs  │ ← Gap remediation (MEDIUM)
└─────────────────────────────────────┘

Tier 2: Performance Harnesses (PARALLEL after T02)
┌─────────────────────────────────────┐
│ T05: HotPathAllocationBenchmarks.cs │
└─────────────────────────────────────┘
┌─────────────────────────────────────┐
│ T06: LatencyBenchmarks.cs           │
└─────────────────────────────────────┘
┌─────────────────────────────────────┐
│ T07: MemoryPressureBenchmarks.cs    │
└─────────────────────────────────────┘

Tier 3: Unit Test Suites (PARALLEL after T02)
┌─────────────────────────────────────┐
│ T08: Pool Health Tests              │
│   - UISnapshotPoolTests.cs          │
│   - OrderArrayPoolTests.cs          │
└─────────────────────────────────────┘
┌─────────────────────────────────────┐
│ T09: FSM/Actor Pattern Tests        │
│   - EnqueueSerializationTests.cs    │
│   - StateTransitionTests.cs         │
└─────────────────────────────────────┘

Tier 4: CI/CD Integration (FINAL)
┌─────────────────────────────────────┐
│ T10: GitHub Actions Workflow        │ ← Depends on ALL previous tickets
│   - .github/workflows/test.yml      │
│   - scripts/run_tests.ps1           │
└─────────────────────────────────────┘
```

---

## TICKET EXECUTION ORDER

### Ticket 01: INinjaTraderMocks.cs (FOUNDATION - BLOCKING)

**Priority:** CRITICAL  
**Effort:** 1 hour  
**Dependencies:** None  
**Blocks:** ALL subsequent tickets

**Scope:**
Create mock interfaces and struct implementations for NinjaTrader API isolation.

**Deliverables:**
- `tests/V12_Performance.Tests/Mocks/INinjaTraderMocks.cs`
- Mock interfaces: `IBar`, `IOrder`, `IExecution`, `IAccount`
- Mock structs: `MockBar`, `MockOrder`, `MockExecution`, `MockAccount`
- `OrderState` enum

**Acceptance Criteria:**
- [ ] All mock interfaces defined
- [ ] All mock structs implement interfaces
- [ ] OrderState enum matches NT8 values
- [ ] File compiles without errors
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all structs are trivial, CYC 1)

**Verification:**
```bash
dotnet build tests/V12_Performance.Tests.csproj
# Expected: Build succeeds
```

---

### Ticket 02: Project Setup (FOUNDATION)

**Priority:** HIGH  
**Effort:** 2 hours  
**Dependencies:** T01 (INinjaTraderMocks.cs)  
**Blocks:** T03-T09

**Scope:**
Create BenchmarkDotNet and xUnit project files with NuGet dependencies.

**Deliverables:**
- `benchmarks/V12_Performance.Benchmarks.csproj`
- `tests/V12_Performance.Tests.csproj`
- NuGet package references (BenchmarkDotNet, xUnit, Moq)
- Project directory structure

**Acceptance Criteria:**
- [ ] Both projects target net6.0
- [ ] BenchmarkDotNet 0.13.12 installed
- [ ] xUnit 2.6.6 installed
- [ ] Projects compile without errors
- [ ] Reference to INinjaTraderMocks.cs resolves

**Verification:**
```bash
dotnet restore benchmarks/V12_Performance.Benchmarks.csproj
dotnet restore tests/V12_Performance.Tests.csproj
dotnet build benchmarks/V12_Performance.Benchmarks.csproj
dotnet build tests/V12_Performance.Tests.csproj
# Expected: All commands succeed
```

---

### Ticket 03: LatencyProbeTests.cs (GAP REMEDIATION)

**Priority:** HIGH  
**Effort:** 2 hours  
**Dependencies:** T02 (Project Setup)  
**Blocks:** None (parallel with T04-T09)

**Scope:**
Implement unit tests validating LatencyProbe struct correctness.

**Deliverables:**
- `tests/V12_Performance.Tests/Infrastructure/LatencyProbeTests.cs`
- 4 tests:
  1. `Start_Stop_ValidProbe()`
  2. `Stop_WithoutStart_InvalidProbe()`
  3. `ElapsedMicroseconds_Accuracy()`
  4. `MultipleStops_LastStopWins()`

**Acceptance Criteria:**
- [ ] All 4 tests implemented
- [ ] All tests pass locally
- [ ] 100% coverage of LatencyProbe struct
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all tests CYC 1-2)

**Verification:**
```bash
dotnet test tests/V12_Performance.Tests.csproj --filter "FullyQualifiedName~LatencyProbeTests"
# Expected: 4 tests passed
```

---

### Ticket 04: LogBufferThreadStaticTests.cs (GAP REMEDIATION)

**Priority:** MEDIUM  
**Effort:** 2 hours  
**Dependencies:** T02 (Project Setup)  
**Blocks:** None (parallel with T03, T05-T09)

**Scope:**
Implement unit tests validating LogBuffer ThreadStatic safety.

**Deliverables:**
- `tests/V12_Performance.Tests/Infrastructure/LogBufferThreadStaticTests.cs`
- 3 tests:
  1. `Format_ConcurrentThreads_NoContamination()`
  2. `Format_ThreadReuse_NoLeaks()`
  3. `Format_RapidContextSwitch_NoCorruption()`

**Acceptance Criteria:**
- [ ] All 3 tests implemented
- [ ] All tests pass locally
- [ ] Uses `Interlocked.Increment` (no `lock()`)
- [ ] 100% coverage of ThreadStatic safety
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all tests CYC 2)

**Verification:**
```bash
dotnet test tests/V12_Performance.Tests.csproj --filter "FullyQualifiedName~LogBufferThreadStaticTests"
# Expected: 3 tests passed

grep -r "lock(" tests/V12_Performance.Tests/Infrastructure/LogBufferThreadStaticTests.cs
# Expected: 0 matches
```

---

### Ticket 05: HotPathAllocationBenchmarks.cs

**Priority:** HIGH  
**Effort:** 3 hours  
**Dependencies:** T02 (Project Setup)  
**Blocks:** None (parallel with T03-T04, T06-T09)

**Scope:**
Implement BenchmarkDotNet harness for hot path allocation validation.

**Deliverables:**
- `benchmarks/V12_Performance.Benchmarks/HotPathAllocationBenchmarks.cs`
- 5 benchmarks:
  1. `OnBarUpdate_Allocation()`
  2. `ProcessOnOrderUpdate_Allocation()`
  3. `PublishUiSnapshot_Allocation()`
  4. `OrderArrayPool_RentReturn_Allocation()`
  5. `LogBuffer_Format_Allocation()`
- Test fixtures (struct-based)

**Acceptance Criteria:**
- [ ] All 5 benchmarks implemented
- [ ] `[MemoryDiagnoser]` + `[SimpleJob]` attributes applied
- [ ] All benchmarks assert `Allocated = 0 B`
- [ ] Benchmarks execute in <2 minutes
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all benchmarks CYC 1-2)

**Verification:**
```bash
dotnet run --project benchmarks/V12_Performance.Benchmarks.csproj -c Release --filter "*Allocation*"
# Expected: All benchmarks show "Allocated = 0 B"
```

---

### Ticket 06: LatencyBenchmarks.cs

**Priority:** HIGH  
**Effort:** 2 hours  
**Dependencies:** T02 (Project Setup)  
**Blocks:** None (parallel with T03-T05, T07-T09)

**Scope:**
Implement BenchmarkDotNet harness for latency validation.

**Deliverables:**
- `benchmarks/V12_Performance.Benchmarks/LatencyBenchmarks.cs`
- 3 benchmarks:
  1. `OnBarUpdate_Latency()` (P50 <110μs, P99 <330μs)
  2. `ProcessOnOrderUpdate_Latency()` (P50 <88μs, P99 <352μs)
  3. `SIMA_Dispatch_Latency()` (P50 <50μs, P99 <150μs)

**Acceptance Criteria:**
- [ ] All 3 benchmarks implemented
- [ ] `[SimpleJob]` attribute applied (warmup: 10, target: 100)
- [ ] Uses LatencyProbe for measurement
- [ ] All benchmarks meet latency targets (with 10% tolerance)
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all benchmarks CYC 1-2)

**Verification:**
```bash
dotnet run --project benchmarks/V12_Performance.Benchmarks.csproj -c Release --filter "*Latency*"
# Expected: All benchmarks meet p50/p99 targets
```

---

### Ticket 07: MemoryPressureBenchmarks.cs

**Priority:** MEDIUM  
**Effort:** 2 hours  
**Dependencies:** T02 (Project Setup)  
**Blocks:** None (parallel with T03-T06, T08-T09)

**Scope:**
Implement BenchmarkDotNet harness for GC frequency validation.

**Deliverables:**
- `benchmarks/V12_Performance.Benchmarks/MemoryPressureBenchmarks.cs`
- 3 benchmarks:
  1. `GC_Frequency_1000Bars()` (Gen0 ≤1)
  2. `GC_Frequency_1000Orders()` (Gen0 ≤1)
  3. `Pool_Fallback_Rate()` (Fallback <1%)

**Acceptance Criteria:**
- [ ] All 3 benchmarks implemented
- [ ] `[MemoryDiagnoser]` + `[SimpleJob]` attributes applied
- [ ] All benchmarks meet GC frequency targets
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all benchmarks CYC 2-3)

**Verification:**
```bash
dotnet run --project benchmarks/V12_Performance.Benchmarks.csproj -c Release --filter "*GC*"
# Expected: All benchmarks show Gen0 ≤1, Gen1=0, Gen2=0
```

---

### Ticket 08: Pool Health Tests

**Priority:** MEDIUM  
**Effort:** 3 hours  
**Dependencies:** T02 (Project Setup)  
**Blocks:** None (parallel with T03-T07, T09)

**Scope:**
Implement xUnit tests for UISnapshotPool and OrderArrayPool.

**Deliverables:**
- `tests/V12_Performance.Tests/Pools/UISnapshotPoolTests.cs` (8 tests)
- `tests/V12_Performance.Tests/Pools/OrderArrayPoolTests.cs` (8 tests)
- `tests/V12_Performance.Tests/Pools/PoolStressTests.cs` (4 tests)

**Test Scenarios:**
- Rent/Return cycles (no leaks)
- Pool exhaustion (fallback behavior)
- Concurrent access (thread safety)
- Stress testing (1000+ operations)

**Acceptance Criteria:**
- [ ] 20 tests implemented (8 + 8 + 4)
- [ ] All tests pass locally
- [ ] 100% coverage of pool operations
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all tests CYC 1-3)

**Verification:**
```bash
dotnet test tests/V12_Performance.Tests.csproj --filter "FullyQualifiedName~Pool"
# Expected: 20 tests passed
```

---

### Ticket 09: FSM/Actor Pattern Tests

**Priority:** MEDIUM  
**Effort:** 4 hours  
**Dependencies:** T02 (Project Setup)  
**Blocks:** None (parallel with T03-T08)

**Scope:**
Implement xUnit tests for FSM/Actor Enqueue model.

**Deliverables:**
- `tests/V12_Performance.Tests/FSM/EnqueueSerializationTests.cs` (8 tests)
- `tests/V12_Performance.Tests/FSM/StateTransitionTests.cs` (12 tests)
- `tests/V12_Performance.Tests/FSM/QueueOverflowTests.cs` (4 tests)

**Test Scenarios:**
- Enqueue serialization (concurrent calls maintain order)
- State transitions (FSM correctness)
- Queue overflow (capacity handling)

**Acceptance Criteria:**
- [ ] 24 tests implemented (8 + 12 + 4)
- [ ] All tests pass locally
- [ ] 100% coverage of Actor pattern
- [ ] ASCII-only compliance verified
- [ ] CYC ≤15 (all tests CYC 1-3)

**Verification:**
```bash
dotnet test tests/V12_Performance.Tests.csproj --filter "FullyQualifiedName~FSM"
# Expected: 24 tests passed
```

---

### Ticket 10: GitHub Actions Workflow (CI/CD INTEGRATION)

**Priority:** LOW  
**Effort:** 2 hours  
**Dependencies:** T03-T09 (ALL tests must pass)  
**Blocks:** None (final ticket)

**Scope:**
Create GitHub Actions workflow for automated test execution.

**Deliverables:**
- `.github/workflows/test.yml`
- `scripts/run_tests.ps1` (local test runner)
- Branch protection rules documentation

**Acceptance Criteria:**
- [ ] Workflow file created
- [ ] Workflow runs on push/PR to main/develop
- [ ] All tests execute in <5 minutes
- [ ] V12 DNA compliance gates enforced
- [ ] Artifacts uploaded (benchmark results, test results)
- [ ] ASCII-only compliance verified

**Verification:**
```bash
# Local test
powershell -File .\scripts\run_tests.ps1
# Expected: All tests pass, DNA gates pass

# CI test (after push)
# Expected: GitHub Actions workflow passes
```

---

## EXECUTION CHECKLIST

### Pre-Execution

- [ ] Review all ticket definitions
- [ ] Confirm T01 (INinjaTraderMocks.cs) is FIRST
- [ ] Confirm T02 (Project Setup) blocks T03-T09
- [ ] Confirm T10 (CI/CD) is LAST

### During Execution

- [ ] Complete T01 before starting any other ticket
- [ ] Complete T02 before starting T03-T09
- [ ] Run `deploy-sync.ps1` after each ticket
- [ ] Run `complexity_audit.py` after each ticket
- [ ] Verify ASCII-only compliance after each ticket
- [ ] Commit after each ticket with BUILD_TAG

### Post-Execution

- [ ] All 80 tests passing
- [ ] All 13 benchmarks passing
- [ ] V12 DNA gates passing (ASCII, lock-free, CYC ≤15)
- [ ] CI/CD workflow passing
- [ ] F5 gate passing in NinjaTrader
- [ ] PR submitted with 100/100 PHS

---

## TICKET SUMMARY

| Ticket | Name | Priority | Effort | Dependencies | Test Count |
|--------|------|----------|--------|--------------|------------|
| T01 | INinjaTraderMocks.cs | CRITICAL | 1h | None | 0 (foundation) |
| T02 | Project Setup | HIGH | 2h | T01 | 0 (foundation) |
| T03 | LatencyProbeTests.cs | HIGH | 2h | T02 | 4 |
| T04 | LogBufferThreadStaticTests.cs | MEDIUM | 2h | T02 | 3 |
| T05 | HotPathAllocationBenchmarks.cs | HIGH | 3h | T02 | 5 (benchmarks) |
| T06 | LatencyBenchmarks.cs | HIGH | 2h | T02 | 3 (benchmarks) |
| T07 | MemoryPressureBenchmarks.cs | MEDIUM | 2h | T02 | 3 (benchmarks) |
| T08 | Pool Health Tests | MEDIUM | 3h | T02 | 20 |
| T09 | FSM/Actor Pattern Tests | MEDIUM | 4h | T02 | 24 |
| T10 | GitHub Actions Workflow | LOW | 2h | T03-T09 | 0 (CI/CD) |
| **TOTAL** | **10 tickets** | - | **23h** | - | **80 tests** |

---

## VERIFICATION COMMANDS

### Per-Ticket Verification

```bash
# After each ticket
dotnet build benchmarks/V12_Performance.Benchmarks.csproj
dotnet build tests/V12_Performance.Tests.csproj
dotnet test tests/V12_Performance.Tests.csproj
powershell -File .\deploy-sync.ps1
python scripts/complexity_audit.py
grep -r "lock(" tests/ benchmarks/
```

### Final Verification (After T10)

```bash
# Run full test suite
powershell -File .\scripts\run_tests.ps1

# Expected output:
# ✓ Unit Tests: 67 passed
# ✓ Benchmarks: 13 passed
# ✓ ASCII GATE: PASS
# ✓ DIFF GUARD: PASS
# ✓ SOVEREIGN AUDIT: PASS
# ✓ Complexity Audit: All methods CYC ≤15
# ✓ Lock-Free Verification: 0 lock() statements
# ✓ ALL TESTS PASSED - V12 DNA COMPLIANT
```

---

## ROLLBACK STRATEGY

### Per-Ticket Rollback

```bash
# Revert last commit
git revert HEAD
powershell -File .\deploy-sync.ps1
```

### Full Epic Rollback

```bash
# Revert all EPIC-6 commits
git revert <T10-commit>..<T01-commit>
powershell -File .\deploy-sync.ps1
```

---

**[EXECUTION-READY]**

**Status:** TICKETS DEFINED  
**Next Phase:** Execution (switch to Code mode)  
**Awaiting:** Director approval to begin implementation

---

**END OF EXECUTION GUIDE**