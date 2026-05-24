# Testing Pyramid - V12 DNA Alignment

## Overview
The V12 testing strategy follows the Testing Pyramid pattern to ensure comprehensive coverage with optimal efficiency.

## Pyramid Structure

```
        /\
       /E2E\      10% - End-to-End Tests
      /------\
     /  INT   \   20% - Integration Tests
    /----------\
   /   UNIT     \ 70% - Unit Tests
  /--------------\
```

## Distribution Targets

### Unit Tests (70%)
**Purpose:** Test individual components in isolation
**Scope:** Single class, single method
**Speed:** < 1ms per test
**Coverage Target:** 80%+ of src/ files

**Examples:**
- FSM state transitions
- Lock-free queue operations
- RMA proximity calculations
- Order validation logic

**Template:** [`UnitTestTemplate.cs`](../../tests/V12_Performance.Tests/Templates/UnitTestTemplate.cs)

### Integration Tests (20%)
**Purpose:** Test interactions between components
**Scope:** Multiple classes, cross-component workflows
**Speed:** < 100ms per test
**Coverage Target:** Critical workflows

**Examples:**
- Order lifecycle (creation → execution → cleanup)
- SIMA dispatch → execution flow
- Bar update → signal generation → order placement

**Template:** [`IntegrationTestTemplate.cs`](../../tests/V12_Performance.Tests/Templates/IntegrationTestTemplate.cs)

### End-to-End Tests (10%)
**Purpose:** Test complete system behavior
**Scope:** Full strategy execution in NinjaTrader
**Speed:** < 5s per test
**Coverage Target:** Happy path + critical edge cases

**Examples:**
- Full strategy lifecycle (State → Initialize → Execute → Terminate)
- Multi-bar processing with order management
- Error recovery and cleanup

**Implementation:** Manual F5 testing in NinjaTrader IDE

## V12 DNA Compliance

### Performance Targets (Epic 5)
- **Allocation:** 0 B for hot path
- **Latency:** < 300μs for critical operations
- **Lock-Free:** Zero `lock()` statements

### Validation Tools
- **BenchmarkDotNet:** Performance regression detection
- **PerformanceAssertions:** Custom assertions for V12 targets
- **Semgrep:** V12 DNA pattern enforcement

## Anti-Patterns (Ice Cream Cone)

**AVOID:**
```
        /\
       /UNIT\      10% - Unit Tests (TOO FEW)
      /------\
     /  INT   \   20% - Integration Tests
    /----------\
   /    E2E     \ 70% - E2E Tests (TOO MANY)
  /--------------\
```

**Problems:**
- Slow test suite (E2E tests are expensive)
- Brittle tests (E2E tests break easily)
- Poor isolation (hard to debug failures)
- Low coverage (E2E tests miss edge cases)

## Measurement

### Current State
```powershell
# Count tests by type
$unitTests = (Get-ChildItem -Path "tests" -Filter "*Tests.cs" -Recurse | Select-String -Pattern "\[Fact\]" -SimpleMatch).Count
$integrationTests = (Get-ChildItem -Path "tests" -Filter "*IntegrationTests.cs" -Recurse | Select-String -Pattern "\[Fact\]" -SimpleMatch).Count
$e2eTests = 1 # Manual F5 testing

$total = $unitTests + $integrationTests + $e2eTests
$unitPercent = ($unitTests / $total) * 100
$integrationPercent = ($integrationTests / $total) * 100
$e2ePercent = ($e2eTests / $total) * 100

Write-Host "Unit: $unitPercent% (target: 70%)"
Write-Host "Integration: $integrationPercent% (target: 20%)"
Write-Host "E2E: $e2ePercent% (target: 10%)"
```

### Target State
- **Unit Tests:** 70+ tests (70%)
- **Integration Tests:** 20+ tests (20%)
- **E2E Tests:** 10+ scenarios (10%)

## Best Practices

### Unit Tests
- Test one thing per test
- Use descriptive names: `MethodName_Scenario_ExpectedResult`
- Arrange-Act-Assert pattern
- No external dependencies (mock/stub)

### Integration Tests
- Test realistic workflows
- Use minimal setup (avoid over-mocking)
- Test error paths (not just happy path)
- Clean up resources in teardown

### E2E Tests
- Test critical user journeys
- Use production-like data
- Verify end-to-end behavior
- Document manual test steps

## References
- [TDD Hardening Protocol](TDD_HARDENING_PROTOCOL.md)
- [Test Templates](../../tests/V12_Performance.Tests/Templates/)
- [Performance Assertions](../../tests/V12_Performance.Tests/Utilities/PerformanceAssertions.cs)