# EPIC-6 Phase 1: Performance Lock-In (Automated Testing)

**Epic ID:** EPIC-6-TESTING  
**Build Tag:** 1111.011-epic6-testing  
**Status:** INTAKE  
**Date:** 2026-05-23  
**Agent:** Bob CLI (v12-engineer)

---

## EXECUTIVE SUMMARY

EPIC-6 Phase 1 establishes automated test harnesses to lock in the Epic 5 performance gains (0 B allocation, <300μs latency) and provide a TDD safety net for future refactoring. This epic creates the testing infrastructure that prevents performance regression and validates V12 DNA compliance.

**Mission:** Build BenchmarkDotNet harnesses and unit tests to assert Epic 5's zero-allocation and sub-300μs latency achievements, ensuring these gains are preserved across all future development.

---

## CONTEXT: EPIC-5 ACHIEVEMENTS TO LOCK IN

### Performance Gains (from T07 Report)

**Allocation Elimination:**
- **43M+ allocations/year eliminated** across 8 tickets
- String.Format elimination: ~10M+/year
- UISnapshot pooling: ~31M+/year  
- Order array pooling: ~1.5M+/year
- .ToArray() consolidation: ~547K/year

**Latency Improvements (Projected):**
- OnBarUpdate: P50=100μs (was 120μs), P99=380μs (was 450μs)
- ProcessOnOrderUpdate: P50=65μs (was 80μs), P99=270μs (was 320μs)

**V12 DNA Compliance:**
- ✅ Zero `lock()` statements introduced
- ✅ ASCII-only strings verified
- ✅ CYC impact minimal (+5 net)
- ✅ Correctness by construction (snapshot pattern)

### Existing Test Infrastructure

**Available Assets:**
1. **LatencyProbe struct** ([`V12_002.Perf.LatencyProbe.cs`](src/V12_002.Perf.LatencyProbe.cs:1)) - Zero-allocation Stopwatch-based measurement
2. **ThreadStaticSafetyTest** ([`tests/ThreadStaticSafetyTest.cs`](tests/ThreadStaticSafetyTest.cs:1)) - Thread model validation (4 scenarios, all passed)
3. **T04 Snapshot Pattern Test** ([`tests/T04_SnapshotPattern_ConcurrentModification_Test.cs`](tests/T04_SnapshotPattern_ConcurrentModification_Test.cs:1)) - Concurrent modification safety
4. **amal_harness.py** ([`scripts/amal_harness.py`](scripts/amal_harness.py:1)) - BenchmarkDotNet automation pattern
5. **SpscRing.Benchmarks.csproj** ([`benchmarks/SpscRing.Benchmarks.csproj`](benchmarks/SpscRing.Benchmarks.csproj:1)) - Existing benchmark project (net6.0)

---

## OBJECTIVES

### Primary Goals

1. **Performance Lock-In Harness**
   - Create BenchmarkDotNet tests asserting `Allocated = 0 B` for hot paths
   - Assert `Mean Latency < 300μs` for critical methods
   - Validate Epic 5 optimizations remain effective

2. **TDD Safety Net**
   - Unit tests covering FSM/Actor `Enqueue` model
   - Lock-free execution path validation
   - Snapshot pattern correctness tests
   - Pool health monitoring tests

3. **V12 DNA Compliance Gates**
   - Automated ASCII-only validation
   - Lock-free pattern verification
   - CYC threshold enforcement (≤15 per method, Jane Street alignment)

### Success Criteria

- [ ] BenchmarkDotNet harness runs in CI/CD pipeline
- [ ] Zero-allocation assertion passes for all hot paths
- [ ] Latency assertions pass (p50 <100μs, p99 <300μs)
- [ ] Unit test coverage ≥80% for Epic 5 optimizations
- [ ] All tests pass in `deploy-sync.ps1` verification
- [ ] F5 gate passes in NinjaTrader IDE

---

## SCOPE

### In-Scope

**1. BenchmarkDotNet Performance Harnesses**
- Hot path allocation benchmarks:
  - OnBarUpdate execution
  - ProcessOnOrderUpdate execution
  - UISnapshot pooling (PublishUiSnapshot)
  - Order array pooling (Cancel/Submit operations)
  - LogBuffer string formatting
- Latency benchmarks:
  - OnBarUpdate p50/p95/p99
  - ProcessOnOrderUpdate p50/p95/p99
  - SIMA dispatch latency
- Memory pressure benchmarks:
  - GC collection frequency
  - Gen0/Gen1/Gen2 promotion rates

**2. Unit Test Safety Net**
- FSM/Actor pattern tests:
  - Enqueue serialization correctness
  - State transition validation
  - Queue overflow handling
- Lock-free execution tests:
  - Atomic operation correctness
  - Race condition detection
  - ThreadStatic safety validation
- Pool health tests:
  - UISnapshotPool rent/return cycles
  - OrderArrayPool rent/return cycles
  - Fallback behavior under stress
- Snapshot pattern tests:
  - Concurrent modification safety
  - ContainsKey re-check validation
  - .ToArray() elimination verification

**3. V12 DNA Compliance Tests**
- ASCII-only string validation
- Lock-free pattern verification (`grep -r "lock(" src/` = 0 matches)
- CYC threshold enforcement (complexity_audit.py integration)
- Hard-link integrity validation

### Out-of-Scope

- ETW trace automation (requires Windows + PerfView + live trading)
- Live trading session stress tests (requires market data feed)
- NinjaTrader IDE integration tests (manual F5 gate remains)
- Performance profiling tools (PerfView, dotTrace)
- Cross-platform testing (Windows-only for NinjaTrader)

---

## CONSTRAINTS

### Technical Constraints

1. **NinjaTrader Dependency**
   - Tests must run without NinjaTrader assemblies (use mocks/stubs)
   - Benchmark harness must isolate V12 logic from NT8 API
   - F5 gate remains manual (cannot automate IDE compilation)

2. **Build Environment**
   - .NET Framework 4.8 for production code
   - .NET 6.0+ for benchmark/test projects (BenchmarkDotNet requirement)
   - PowerShell 5.1+ for automation scripts

3. **CI/CD Integration**
   - Tests must complete in <5 minutes
   - Zero external dependencies (no network calls)
   - Deterministic results (no flaky tests)

### V12 DNA Constraints

1. **Lock-Free Mandate**
   - Test harness MUST NOT introduce `lock()` statements
   - Use atomic primitives or Actor pattern for synchronization

2. **ASCII-Only Compliance**
   - All test code and output MUST be ASCII-only
   - No Unicode, emoji, or curly quotes

3. **Zero-Allocation Requirement**
   - Benchmark harness itself MUST NOT allocate in hot paths
   - Use struct-based measurement (LatencyProbe pattern)

---

## DEPENDENCIES

### Upstream Dependencies (Epic 5)

- ✅ T01: LatencyProbe instrumentation complete
- ✅ T02: LogBuffer string.Format elimination complete
- ✅ T03: UISnapshotPool implementation complete
- ✅ T04: .ToArray() elimination complete
- ✅ T05: OrderArrayPool implementation complete
- ✅ T08: StickyState migration fix complete

### Downstream Dependencies

- **EPIC-7 (Future):** Continuous performance monitoring dashboard
- **EPIC-8 (Future):** Automated ETW trace integration
- **Production Deployment:** Requires Epic 6 test suite passing

---

## RISKS & MITIGATIONS

### High-Risk Items

1. **Risk:** BenchmarkDotNet may introduce allocations in measurement overhead
   - **Mitigation:** Use MemoryDiagnoser with `[MemoryDiagnoser(false)]` to exclude diagnoser allocations
   - **Mitigation:** Validate with manual ETW trace spot-check

2. **Risk:** Unit tests may not catch real-world race conditions
   - **Mitigation:** Include stress test scenarios (1000+ iterations)
   - **Mitigation:** Use ThreadStatic safety test pattern from T01B

3. **Risk:** CI/CD pipeline may timeout on slow hardware
   - **Mitigation:** Set benchmark iteration limits (10 warmup, 20 target)
   - **Mitigation:** Use `[SimpleJob]` attribute for faster execution

### Medium-Risk Items

1. **Risk:** Mocking NinjaTrader API may miss integration issues
   - **Mitigation:** Keep F5 gate as final manual verification
   - **Mitigation:** Document which tests require live NT8 environment

2. **Risk:** Latency assertions may be flaky on different hardware
   - **Mitigation:** Use percentile-based assertions (p99 <300μs) not absolute values
   - **Mitigation:** Allow 10% tolerance for CI/CD environment variance

---

## ACCEPTANCE CRITERIA

### Functional Requirements

- [ ] BenchmarkDotNet harness executes successfully
- [ ] Zero-allocation assertion passes for all hot paths
- [ ] Latency assertions pass (p50 <100μs, p99 <300μs with 10% tolerance)
- [ ] Unit tests achieve ≥80% coverage of Epic 5 optimizations
- [ ] All tests pass in local development environment
- [ ] All tests pass in CI/CD pipeline (if configured)

### Non-Functional Requirements

- [ ] Test execution time <5 minutes total
- [ ] Zero flaky tests (100% deterministic results)
- [ ] Test code follows V12 DNA (no locks, ASCII-only, CYC ≤15)
- [ ] Documentation includes setup instructions and troubleshooting guide

### V12 DNA Compliance

- [ ] `deploy-sync.ps1` passes (ASCII GATE, DIFF GUARD, SOVEREIGN AUDIT)
- [ ] `grep -r "lock(" tests/` returns 0 matches
- [ ] `grep -r "lock(" benchmarks/` returns 0 matches
- [ ] `complexity_audit.py` shows all test methods CYC ≤15 (Jane Street threshold)
- [ ] F5 gate passes in NinjaTrader IDE

---

## DELIVERABLES

### Code Artifacts

1. **benchmarks/V12_Performance.Benchmarks.csproj**
   - BenchmarkDotNet project targeting net6.0
   - Hot path allocation benchmarks
   - Latency benchmarks
   - Memory pressure benchmarks

2. **tests/V12_Performance.Tests.csproj**
   - xUnit or NUnit test project targeting net6.0
   - FSM/Actor pattern tests
   - Lock-free execution tests
   - Pool health tests
   - Snapshot pattern tests

3. **scripts/run_benchmarks.ps1**
   - Automation script for benchmark execution
   - Result parsing and assertion validation
   - CI/CD integration hooks

4. **scripts/run_tests.ps1**
   - Automation script for unit test execution
   - Coverage report generation
   - CI/CD integration hooks

### Documentation Artifacts

1. **docs/brain/EPIC-6-TESTING/01-analysis.md**
   - Test architecture design
   - Coverage analysis
   - Risk assessment

2. **docs/brain/EPIC-6-TESTING/02-approach.md**
   - Implementation strategy
   - Ticket breakdown
   - Execution plan

3. **docs/brain/EPIC-6-TESTING/EXECUTION_GUIDE.md**
   - Ticket execution order
   - Dependency graph
   - Verification checklist

4. **docs/testing/BENCHMARK_GUIDE.md**
   - How to run benchmarks locally
   - How to interpret results
   - Troubleshooting common issues

5. **docs/testing/UNIT_TEST_GUIDE.md**
   - How to run unit tests locally
   - How to add new tests
   - Coverage requirements

---

## OPEN QUESTIONS

1. **BenchmarkDotNet Configuration:**
   - Q: Should we use `[SimpleJob]` or `[ShortRunJob]` for CI/CD?
   - A: TBD - benchmark execution time vs accuracy tradeoff

2. **Test Framework Selection:**
   - Q: xUnit vs NUnit vs MSTest for unit tests?
   - A: TBD - prefer xUnit for modern .NET ecosystem

3. **CI/CD Integration:**
   - Q: GitHub Actions, Azure Pipelines, or local-only?
   - A: TBD - depends on repository CI/CD setup

4. **Coverage Tool:**
   - Q: Coverlet, dotCover, or manual coverage tracking?
   - A: TBD - prefer Coverlet for open-source compatibility

---

## NEXT STEPS

1. **Director Review** - Approve scope and objectives
2. **Phase 2: Analysis** - Design test architecture and coverage strategy
3. **Phase 3: Approach** - Create implementation plan and ticket breakdown
4. **Phase 4: Validation** - Verify approach against V12 DNA constraints
5. **Phase 5: Execution** - Implement benchmarks and unit tests
6. **Phase 6: Verification** - Run `deploy-sync.ps1` and F5 gate
7. **Phase 7: Sign-Off** - Director approval for production deployment

---

## REFERENCES

- [Epic 5 Verification Report](docs/brain/EPIC-5-PERF/T07-verification-stress-testing-report.md)
- [LatencyProbe Implementation](src/V12_002.Perf.LatencyProbe.cs)
- [ThreadStatic Safety Test](tests/ThreadStaticSafetyTest.cs)
- [AMAL Harness Pattern](scripts/amal_harness.py)
- [V12 DNA Protocol](docs/protocol/INSTITUTIONAL_WORKFLOW_DNA.md)

---

**[INTAKE-GATE]**

**Status:** SCOPE COMPLETE  
**Next Phase:** Analysis (01-analysis.md)  
**Awaiting:** Director approval to proceed

---

**END OF SCOPE DOCUMENT**