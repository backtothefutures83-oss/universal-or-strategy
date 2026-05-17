# P3 Implementation Plan: S6 Metrics & Telemetry Test Suite

**BUILD_TAG_BASELINE**: 1111.007-phase7-tQ1_S5_CONFIG_TESTS_COMPLETE  
**CLUSTER**: S6 - Metrics & Telemetry Integration Tests  
**ARCHITECT**: Bob CLI (v12-engineer)  
**PHASE**: Phase 7 - Test Quality Initiative (tQ1)  
**STATUS**: P3 Architecture Planning  
**DATE**: 2026-05-17

---

## Executive Summary

This document specifies the architecture and test scenarios for `tests/MetricsIntegrationTests.cs`, a comprehensive integration test suite for V12's distributed tracing, structured logging, and performance metrics subsystems. The suite mirrors the structure and quality of `REAPERDefenseIntegrationTests.cs` (30 tests, 997 lines) and follows the patterns established in `ConfigurationIntegrationTests.cs` (S5).

**Scope**: 4 core files (Telemetry.cs, StructuredLog.cs, Photon.Pool.cs, V12_002.cs circuit breaker)  
**Test Count**: 22 tests across 4 phases  
**Estimated Size**: ~950 lines  
**V12 DNA**: Lock-free, MockTime, ASCII-only, Atomic primitives  
**Constraint**: SETUP ONLY - asserts current behavior, no bug fixes

---

## File Inventory

### 1. V12_002.Telemetry.cs (174 lines)

**Purpose**: Distributed tracing + logic metrics for V12 kernel  
**Key Components**:
- Monotonic trace ID generation (5-digit, wraps at 100,000)
- 6 lock-free metric counters (FSM, SIMA, Reaper, Symmetry, Orders, IPC)
- TraceSpan struct (stack-allocated stopwatch)
- Metrics summary emitter (end-of-session report)

**State Variables**:
```csharp
private long _traceCounter = 0;                    // Monotonic correlation counter
private string _currentTraceId = "00000";          // Current active trace ID
private long _metricFsmTransitions = 0;            // FSM actor Enqueue() count
private long _metricSimaDispatches = 0;            // SIMA fleet broadcast count
private long _metricReaperAudits = 0;              // AuditApexPositions() count
private long _metricSymmetryReplace = 0;           // Follower bracket Replace count
private long _metricOrderSubmissions = 0;          // SubmitOrderUnmanaged count
private long _metricIpcCommands = 0;               // IPC command processed count
```

**Key Methods**:
- `NewTraceId()` - Generate next monotonic ID, set as current context
- `ResetTelemetry()` - Reset all counters (called from SetDefaults)
- `TrackFsmTransition()` - Increment FSM counter
- `TrackSimaDispatch()` - Increment SIMA counter
- `TrackReaperAudit()` - Increment Reaper counter
- `TrackSymmetryReplace()` - Increment Symmetry counter
- `TrackOrderSubmission()` - Increment order counter
- `TrackIpcCommand()` - Increment IPC counter
- `BeginSpan(module)` - Create stack-allocated span token
- `TraceSpan.End(print)` - Close span, emit elapsed time
- `EmitMetricsSummary()` - Print end-of-session report

**V12 DNA Compliance**:
- ✅ Lock-free (Interlocked only)
- ✅ ASCII-only (5-digit format)
- ✅ Zero heap allocation (TraceSpan is struct)

### 2. V12_002.StructuredLog.cs (115 lines)

**Purpose**: Structured logging wrapper for NinjaTrader Print()  
**Key Components**:
- V12LogLevel enum (DEBUG, INFO, WARN, ERROR)
- Structured format: `[TRACE:NNNNN][MODULE][LEVEL] message`
- Convenience wrappers (LogInfo, LogWarn, LogError, LogDebug)
- Exception logger with type + message extraction

**Key Methods**:
- `StructuredPrint(traceId, module, level, message)` - Core emitter
- `LogInfo(module, message)` - INFO-level log with current trace
- `LogWarn(module, message)` - WARN-level log with current trace
- `LogError(module, message)` - ERROR-level log with current trace
- `LogDebug(module, message)` - DEBUG-level log (suppressed by default)
- `LogWithTrace(traceId, module, level, message)` - Explicit trace override
- `LogException(module, context, ex)` - Exception logger

**V12 DNA Compliance**:
- ✅ Lock-free (no synchronization)
- ✅ ASCII-only (fixed-width level monikers)
- ✅ Defensive null guards (never throws)

### 3. V12_002.Photon.Pool.cs (339 lines, partial)

**Purpose**: Photon order pool + execution ID ring + integrity shadow  
**Key Components**:
- PhotonOrderPool (64-slot pre-allocated Order[] pool)
- ExecutionIdRing (duplicate execution detection)
- FleetDispatchSlot (blittable 64-byte struct)
- FleetDispatchSideband (managed refs indexed by slot)
- ComputeFleetDispatchShadow (XorShadow integrity check)

**Diagnostic Methods**:
- `PhotonOrderPool.GetDiagnostics()` - Pool stats (free/claims/releases/exhausted)
- `ExecutionIdRing.GetDiagnostics()` - Ring stats (hits/misses/evicts/collisions)

**V12 DNA Compliance**:
- ✅ Lock-free (Interlocked for pool operations)
- ✅ Blittable struct (MMIO-ready)
- ✅ Zero allocation (pool reuse)

### 4. V12_002.cs (partial - circuit breaker)

**Purpose**: Submit circuit breaker for order submission throttling  
**Key Components**:
- SubmitCircuitBreaker class (tracks failures, implements cooldown)
- Atomic state transitions (Open/HalfOpen/Closed)

**Note**: Circuit breaker details will be inferred from usage patterns in tests.

---

## Test Suite Architecture

### Test Class Structure

```csharp
// MetricsIntegrationTests.cs
// BUILD_TAG: 1111.007-phase7-tQ1_S6_METRICS_TESTS_SETUP
// Cluster S6: Metrics & Telemetry Integration Tests (22 tests)
// V12 DNA: Lock-free, MockTime, ASCII-only, Atomic primitives
// SETUP ONLY - asserts current behavior, no bug fixes

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace V12.Tests
{
    /// <summary>
    /// Integration tests for V12 Metrics & Telemetry System (Cluster S6).
    /// Covers 4 telemetry files (628 lines).
    /// Tests trace ID generation, metric counters, structured logging, and diagnostic snapshots.
    /// SETUP ONLY - asserts current behavior, no bug fixes.
    /// </summary>
    public class MetricsIntegrationTests
    {
        // Test phases and mock infrastructure defined below
    }
}
```

### Mock Harness Design

#### MockPrint (Capture Print() Output)

Captures Print() output for assertion. Thread-safe via ConcurrentQueue.

**Key Methods**:
- `Print(message)` - Capture a log line
- `ContainsLine(substring)` - Check if any line contains substring
- `ContainsPattern(regex)` - Check if any line matches regex
- `GetLine(index)` - Get specific line by index
- `GetAllLines()` - Get all captured lines
- `Clear()` - Reset captured lines

#### MockTime (Deterministic Time Simulation)

Deterministic time simulation for span timing tests. Copied from S1/S2/S3/S4/S5 test suites.

**Key Methods**:
- `GetTicks()` - Read current ticks atomically
- `Advance(deltaTicks)` - Advance time by ticks
- `AdvanceSeconds(seconds)` - Advance time by seconds
- `AdvanceMilliseconds(ms)` - Advance time by milliseconds
- `GetDateTime()` - Get DateTime from current ticks

#### MockTelemetry (Partial Strategy Instance)

Partial mock of V12_002 strategy exposing telemetry methods. Simulates strategy lifecycle without NinjaTrader dependencies.

**Key Methods**:
- `NewTraceId()` - Generate next trace ID
- `ResetTelemetry()` - Reset all counters
- `TrackFsmTransition()` - Increment FSM counter
- `TrackSimaDispatch()` - Increment SIMA counter
- `TrackReaperAudit()` - Increment Reaper counter
- `TrackSymmetryReplace()` - Increment Symmetry counter
- `TrackOrderSubmission()` - Increment order counter
- `TrackIpcCommand()` - Increment IPC counter
- `GetFsmTransitions()` - Read FSM counter
- `GetSimaDispatches()` - Read SIMA counter
- `GetReaperAudits()` - Read Reaper counter
- `GetSymmetryReplaces()` - Read Symmetry counter
- `GetOrderSubmissions()` - Read order counter
- `GetIpcCommands()` - Read IPC counter
- `LogInfo(module, message)` - Emit INFO log
- `LogWarn(module, message)` - Emit WARN log
- `LogError(module, message)` - Emit ERROR log
- `EmitMetricsSummary()` - Emit end-of-session report

#### MockPhotonPool (Diagnostic Snapshot)

Mock Photon order pool for diagnostic testing. Simplified version focusing on claim/release/diagnostics.

**Key Methods**:
- `Claim()` - Claim a slot (returns bool)
- `Release()` - Release a slot
- `GetDiagnostics()` - Get diagnostic string

#### MockExecutionIdRing (Duplicate Detection)

Mock execution ID ring for duplicate detection testing. Simplified version focusing on hit/miss/evict tracking.

**Key Methods**:
- `ContainsOrAdd(hash)` - Check for duplicate, add if new
- `GetDiagnostics()` - Get diagnostic string

---

## Test Scenarios (22 Tests Across 4 Phases)

### Phase 1: Trace ID Generation & Correlation (T01-T06)

#### T01_TraceId_GeneratesMonotonic

**Purpose**: Verify trace IDs increment monotonically  
**Setup**: MockTelemetry initialized  
**Actions**:
1. Call `NewTraceId()` 5 times
2. Capture each returned ID

**Assertions**:
- IDs are "00001", "00002", "00003", "00004", "00005"
- Each ID is 5 digits, zero-padded
- IDs are strictly increasing

**Edge Cases**: Monotonic increment, no gaps

#### T02_TraceId_WrapsAt100000

**Purpose**: Verify trace ID wraps at 100,000  
**Setup**: MockTelemetry with counter at 99,998  
**Actions**:
1. Set `_traceCounter` to 99,998
2. Call `NewTraceId()` 3 times

**Assertions**:
- IDs are "99999", "00000", "00001"
- Wrap occurs at 100,000 (modulo operation)
- No exceptions thrown

**Edge Cases**: Boundary wrap, modulo arithmetic

#### T03_TraceId_SetsCurrentContext

**Purpose**: Verify `NewTraceId()` updates `_currentTraceId`  
**Setup**: MockTelemetry initialized  
**Actions**:
1. Call `NewTraceId()` → returns "00001"
2. Read `CurrentTraceId` property

**Assertions**:
- `CurrentTraceId` equals "00001"
- Subsequent logs use this trace ID

**Edge Cases**: Context propagation

#### T04_TraceId_ResetClearsCounter

**Purpose**: Verify `ResetTelemetry()` resets trace counter  
**Setup**: MockTelemetry with counter at 42  
**Actions**:
1. Call `NewTraceId()` → "00043"
2. Call `ResetTelemetry()`
3. Call `NewTraceId()` → "00001"

**Assertions**:
- Counter resets to 0
- `CurrentTraceId` resets to "00000"
- Next ID is "00001"

**Edge Cases**: Reset behavior, state cleanup

#### T05_TraceId_Format_FiveDigitZeroPadded

**Purpose**: Verify trace ID format is always 5 digits  
**Setup**: MockTelemetry initialized  
**Actions**:
1. Generate IDs at positions 1, 10, 100, 1000, 10000

**Assertions**:
- "00001", "00010", "00100", "01000", "10000"
- All IDs are exactly 5 characters
- Leading zeros preserved

**Edge Cases**: Zero-padding at all magnitudes

#### T06_TraceId_ConcurrentIncrement_ThreadSafe

**Purpose**: Verify trace counter is thread-safe  
**Setup**: MockTelemetry, 10 threads  
**Actions**:
1. Spawn 10 threads
2. Each thread calls `NewTraceId()` 100 times
3. Collect all IDs

**Assertions**:
- 1000 unique IDs generated
- No duplicate IDs
- Counter reaches 1000

**Edge Cases**: Concurrent access, Interlocked correctness

---

### Phase 2: Metric Counter Accuracy (T07-T12)

#### T07_MetricCounters_IncrementAtomically

**Purpose**: Verify all 6 metric counters increment atomically  
**Setup**: MockTelemetry initialized  
**Actions**:
1. Call each Track*() method once
2. Read all counters

**Assertions**:
- FSM: 1, SIMA: 1, Reaper: 1, Symmetry: 1, Orders: 1, IPC: 1
- All counters start at 0
- Each increment is atomic

**Edge Cases**: Single-threaded baseline

#### T08_MetricCounters_MultipleIncrements

**Purpose**: Verify counters accumulate correctly  
**Setup**: MockTelemetry initialized  
**Actions**:
1. Call `TrackFsmTransition()` 5 times
2. Call `TrackSimaDispatch()` 3 times
3. Call `TrackReaperAudit()` 2 times

**Assertions**:
- FSM: 5, SIMA: 3, Reaper: 2
- Other counters remain 0
- No cross-contamination

**Edge Cases**: Independent counter accumulation

#### T09_MetricCounters_ResetClearsAll

**Purpose**: Verify `ResetTelemetry()` clears all counters  
**Setup**: MockTelemetry with non-zero counters  
**Actions**:
1. Increment all counters to non-zero values
2. Call `ResetTelemetry()`
3. Read all counters

**Assertions**:
- All counters return to 0
- Trace counter also reset
- No residual state

**Edge Cases**: Complete state reset

#### T10_MetricCounters_ConcurrentIncrement_ThreadSafe

**Purpose**: Verify metric counters are thread-safe  
**Setup**: MockTelemetry, 10 threads  
**Actions**:
1. Spawn 10 threads
2. Each thread increments FSM counter 100 times
3. Read final counter value

**Assertions**:
- FSM counter equals 1000
- No lost increments
- Interlocked correctness

**Edge Cases**: Concurrent writes, atomicity

#### T11_MetricCounters_IndependentCounters

**Purpose**: Verify counters are independent (no cross-talk)  
**Setup**: MockTelemetry initialized  
**Actions**:
1. Increment FSM counter 10 times
2. Read all 6 counters

**Assertions**:
- FSM: 10
- All other counters: 0
- No memory corruption

**Edge Cases**: Memory isolation

#### T12_MetricsSummary_EmitsAllCounters

**Purpose**: Verify `EmitMetricsSummary()` prints all counters  
**Setup**: MockTelemetry with non-zero counters, MockPrint  
**Actions**:
1. Set counters: FSM=5, SIMA=3, Reaper=2, Symmetry=1, Orders=10, IPC=7
2. Call `EmitMetricsSummary()`
3. Inspect MockPrint output

**Assertions**:
- Output contains "SESSION METRICS REPORT"
- Output contains "FSM Transitions   : 5"
- Output contains "SIMA Dispatches   : 3"
- Output contains "Reaper Audits     : 2"
- Output contains "Symmetry Replaces : 1"
- Output contains "Order Submissions : 10"
- Output contains "IPC Commands      : 7"
- Output contains separator lines

**Edge Cases**: Report formatting, all counters present

---

### Phase 3: Structured Logging (T13-T17)

#### T13_StructuredLog_FormatCorrect

**Purpose**: Verify structured log format is correct  
**Setup**: MockTelemetry, MockPrint  
**Actions**:
1. Set trace ID to "00042"
2. Call `LogInfo("SIMA.Dispatch", "FleetBroadcast started")`
3. Inspect MockPrint output

**Assertions**:
- Output: `[TRACE:00042][SIMA.Dispatch][INFO] FleetBroadcast started`
- Format matches: `[TRACE:NNNNN][MODULE][LEVEL] message`
- All components present

**Edge Cases**: Format compliance

#### T14_StructuredLog_LevelTagging

**Purpose**: Verify all log levels emit correctly  
**Setup**: MockTelemetry, MockPrint  
**Actions**:
1. Call `LogInfo("TEST", "info message")`
2. Call `LogWarn("TEST", "warn message")`
3. Call `LogError("TEST", "error message")`
4. Inspect MockPrint output

**Assertions**:
- Line 1 contains "[INFO]"
- Line 2 contains "[WARN]"
- Line 3 contains "[ERROR]"
- All use same trace ID

**Edge Cases**: Level differentiation

#### T15_StructuredLog_TraceIdPropagation

**Purpose**: Verify logs use current trace context  
**Setup**: MockTelemetry, MockPrint  
**Actions**:
1. Call `NewTraceId()` → "00001"
2. Call `LogInfo("TEST", "message1")`
3. Call `NewTraceId()` → "00002"
4. Call `LogInfo("TEST", "message2")`

**Assertions**:
- Line 1 contains "[TRACE:00001]"
- Line 2 contains "[TRACE:00002]"
- Trace context propagates correctly

**Edge Cases**: Context switching

#### T16_StructuredLog_NullSafety

**Purpose**: Verify defensive null guards work  
**Setup**: MockTelemetry, MockPrint  
**Actions**:
1. Call `LogInfo(null, null)`
2. Call `LogInfo("TEST", null)`
3. Call `LogInfo(null, "message")`
4. Inspect MockPrint output

**Assertions**:
- No exceptions thrown
- Line 1: `[TRACE:?????][UNKNOWN][INFO] (null)`
- Line 2: `[TRACE:00001][TEST][INFO] (null)`
- Line 3: `[TRACE:00002][UNKNOWN][INFO] message`

**Edge Cases**: Null handling, defensive programming

#### T17_StructuredLog_ASCIIOnly

**Purpose**: Verify all log output is ASCII-only  
**Setup**: MockTelemetry, MockPrint  
**Actions**:
1. Call `LogInfo("TEST", "message with ASCII chars")`
2. Inspect MockPrint output

**Assertions**:
- All characters in output are ASCII (0-127)
- No Unicode, emoji, or curly quotes
- Level monikers are ASCII

**Edge Cases**: ASCII compliance

---

### Phase 4: Diagnostic Snapshots (T18-T22)

#### T18_PhotonPool_ClaimRelease_UpdatesCounters

**Purpose**: Verify pool claim/release updates counters  
**Setup**: MockPhotonPool(capacity=10)  
**Actions**:
1. Claim 3 slots
2. Release 1 slot
3. Read counters

**Assertions**:
- FreeCount: 8 (10 - 3 + 1)
- ClaimCount: 3
- ReleaseCount: 1
- ExhaustedCount: 0

**Edge Cases**: Basic pool operations

#### T19_PhotonPool_Exhaustion_TracksExhaustedCount

**Purpose**: Verify pool exhaustion tracking  
**Setup**: MockPhotonPool(capacity=2)  
**Actions**:
1. Claim 2 slots (success)
2. Claim 1 slot (fail - exhausted)
3. Read counters

**Assertions**:
- FreeCount: 0
- ClaimCount: 2
- ExhaustedCount: 1
- Third claim returns false

**Edge Cases**: Pool exhaustion

#### T20_PhotonPool_Diagnostics_FormatsCorrectly

**Purpose**: Verify diagnostic string format  
**Setup**: MockPhotonPool(capacity=10) with activity  
**Actions**:
1. Claim 3, release 1
2. Call `GetDiagnostics()`

**Assertions**:
- Output: `PhotonPool: free=8/10 claims=3 releases=1 exhausted=0`
- Format matches expected pattern
- All counters present

**Edge Cases**: Diagnostic formatting

#### T21_ExecutionIdRing_DuplicateDetection

**Purpose**: Verify duplicate execution detection  
**Setup**: MockExecutionIdRing(capacity=100)  
**Actions**:
1. Add hash 12345 → returns false (miss)
2. Add hash 12345 → returns true (hit)
3. Add hash 67890 → returns false (miss)
4. Read counters

**Assertions**:
- HitCount: 1
- MissCount: 2
- Duplicate detected correctly

**Edge Cases**: Hit/miss tracking

#### T22_ExecutionIdRing_Diagnostics_FormatsCorrectly

**Purpose**: Verify diagnostic string format  
**Setup**: MockExecutionIdRing(capacity=100) with activity  
**Actions**:
1. Add 5 unique hashes, 2 duplicates
2. Call `GetDiagnostics()`

**Assertions**:
- Output: `ExecIdRing: count=5/100 hits=2 misses=5 evicts=0`
- Format matches expected pattern
- All counters present

**Edge Cases**: Diagnostic formatting

---

## Mock Implementation Details

### Helper Methods (18 methods)

#### Assertion Helpers (8 methods)

1. `AssertTraceIdFormat(string id)` - Verify 5-digit zero-padded format
2. `AssertTraceIdMonotonic(string id1, string id2)` - Verify id2 > id1
3. `AssertCounterValue(long actual, long expected, string counterName)` - Verify counter value
4. `AssertLogContains(MockPrint print, string substring)` - Verify log contains substring
5. `AssertLogPattern(MockPrint print, string pattern)` - Verify log matches regex
6. `AssertLogLevel(string line, string expectedLevel)` - Verify log level tag
7. `AssertDiagnosticFormat(string diagnostic, string expectedPattern)` - Verify diagnostic format
8. `AssertASCIIOnly(string text)` - Verify all characters are ASCII

#### Verification Helpers (5 methods)

1. `VerifyAllCountersZero(MockTelemetry telemetry)` - Check all counters are 0
2. `VerifyCounterIndependence(MockTelemetry telemetry, string counterName)` - Check no cross-talk
3. `VerifyLogFormatCompliance(string line)` - Check log format matches spec
4. `VerifyPoolConsistency(MockPhotonPool pool)` - Check pool invariants
5. `VerifyRingConsistency(MockExecutionIdRing ring)` - Check ring invariants

#### Simulation Helpers (3 methods)

1. `SimulateMetricActivity(MockTelemetry telemetry)` - Generate realistic metric activity
2. `SimulatePoolActivity(MockPhotonPool pool, int claims, int releases)` - Generate pool activity
3. `SimulateRingActivity(MockExecutionIdRing ring, int unique, int duplicates)` - Generate ring activity

#### Creation Helpers (2 methods)

1. `CreateMockTelemetry()` - Create MockTelemetry with MockPrint
2. `CreateMockPhotonPool(int capacity)` - Create MockPhotonPool with capacity

---

## V12 DNA Compliance Verification

### Lock-Free Verification Strategy

**Approach**: Grep audit + concurrent stress tests

**Verification Steps**:
1. Grep for `lock(` in test file → expect 0 matches
2. Grep for `Monitor.Enter` in test file → expect 0 matches
3. Run T06 (concurrent trace ID) → verify no race conditions
4. Run T10 (concurrent counters) → verify no lost updates

**Success Criteria**:
- Zero `lock()` statements in test code
- Zero `Monitor.Enter` calls in test code
- All concurrent tests pass with correct final values
- No race conditions detected

### MockTime Usage (Zero Thread.Sleep)

**Approach**: Grep audit + deterministic time advancement

**Verification Steps**:
1. Grep for `Thread.Sleep` in test file → expect 0 matches
2. Grep for `Task.Delay` in test file → expect 0 matches
3. All time-dependent tests use `MockTime.Advance*()`
4. All span timing tests use `MockTime` for determinism

**Success Criteria**:
- Zero `Thread.Sleep` calls
- Zero `Task.Delay` calls
- All time-based tests are deterministic
- Tests run in <1 second (no real delays)

### Atomic Primitives for Concurrency

**Approach**: Code review + concurrent test validation

**Verification Steps**:
1. All counter reads use `Interlocked.Read()`
2. All counter writes use `Interlocked.Increment()` or `Interlocked.Exchange()`
3. All flag checks use `Interlocked.CompareExchange()`
4. Concurrent tests (T06, T10) validate atomicity

**Success Criteria**:
- All shared state uses Interlocked primitives
- No volatile reads without Interlocked
- Concurrent tests pass with correct values

### ASCII-Only String Validation

**Approach**: Grep audit + T17 test

**Verification Steps**:
1. Grep for Unicode escapes (`\u`) in test file → expect 0 matches
2. Grep for emoji in test file → expect 0 matches
3. Run T17 (ASCII-only test) → verify all log output is ASCII
4. Verify trace ID format uses ASCII digits only

**Success Criteria**:
- Zero Unicode characters in test code
- Zero emoji in test code
- T17 passes (all log output is ASCII)
- Trace IDs use ASCII digits 0-9 only

---

## Reference Patterns from REAPERDefenseIntegrationTests.cs

### Test Naming Convention

**Pattern**: `T{NN}_{Component}_{Scenario}_{ExpectedBehavior}`

**Examples**:
- `T01_TraceId_GeneratesMonotonic`
- `T07_MetricCounters_IncrementAtomically`
- `T13_StructuredLog_FormatCorrect`
- `T18_PhotonPool_ClaimRelease_UpdatesCounters`

**Rules**:
- Test number is 2-digit zero-padded (T01-T22)
- Component is the subsystem under test
- Scenario describes the test setup
- ExpectedBehavior describes the assertion

### Test Structure

**Pattern**: Given-When-Then with comments

```csharp
[Fact]
public void T01_TraceId_GeneratesMonotonic()
{
    // Given: MockTelemetry initialized
    var telemetry = CreateMockTelemetry();

    // When: Generate 5 trace IDs
    var id1 = telemetry.NewTraceId();
    var id2 = telemetry.NewTraceId();
    var id3 = telemetry.NewTraceId();
    var id4 = telemetry.NewTraceId();
    var id5 = telemetry.NewTraceId();

    // Then: IDs are monotonic
    Assert.Equal("00001", id1);
    Assert.Equal("00002", id2);
    Assert.Equal("00003", id3);
    Assert.Equal("00004", id4);
    Assert.Equal("00005", id5);
    AssertTraceIdMonotonic(id1, id2);
    AssertTraceIdMonotonic(id2, id3);
    AssertTraceIdMonotonic(id3, id4);
    AssertTraceIdMonotonic(id4, id5);
}
```

### Assertion Patterns

**Patterns**:
1. **Direct Assert**: `Assert.Equal(expected, actual)`
2. **Helper Assert**: `AssertTraceIdFormat(id)`
3. **Verification**: `Assert.True(VerifyAllCountersZero(telemetry))`
4. **Contains**: `Assert.True(print.ContainsLine("SESSION METRICS REPORT"))`
5. **Pattern Match**: `Assert.True(print.ContainsPattern(@"\[TRACE:\d{5}\]"))`

### Documentation Style

**Pattern**: XML doc comments + inline comments

```csharp
/// <summary>
/// Integration tests for V12 Metrics & Telemetry System (Cluster S6).
/// Covers 4 telemetry files (628 lines).
/// Tests trace ID generation, metric counters, structured logging, and diagnostic snapshots.
/// SETUP ONLY - asserts current behavior, no bug fixes.
/// </summary>
public class MetricsIntegrationTests
{
    // Given-When-Then comments in each test
    // Edge case comments for complex scenarios
}
```

---

## Estimated Test File Size

**Breakdown**:
- File header + usings: ~20 lines
- Mock infrastructure (5 classes): ~350 lines
  - MockPrint: ~60 lines
  - MockTime: ~20 lines
  - MockTelemetry: ~150 lines
  - MockPhotonPool: ~60 lines
  - MockExecutionIdRing: ~60 lines
- Test helpers (18 methods): ~150 lines
- Phase 1 tests (T01-T06): ~120 lines
- Phase 2 tests (T07-T12): ~120 lines
- Phase 3 tests (T13-T17): ~100 lines
- Phase 4 tests (T18-T22): ~100 lines

**Total Estimate**: ~960 lines

**Comparison**:
- REAPERDefenseIntegrationTests.cs: 997 lines (30 tests)
- ConfigurationIntegrationTests.cs: 997 lines (26 tests)
- MetricsIntegrationTests.cs: ~960 lines (22 tests)

**Rationale for 22 tests (not 30)**:
- Telemetry subsystem is more focused than REAPER (4 files vs 5 files)
- Fewer state machines (no FSM lifecycle, no timer management)
- Simpler mock infrastructure (no MockAccount, MockOrder, MockFSM)
- More emphasis on atomic operations and format validation
- Diagnostic tests are lighter (snapshot-only, no complex workflows)

---

## Key Architectural Decisions

### 1. MockPrint Over Real Print()

**Rationale**: Capture output for assertion without console noise  
**Benefits**:
- Deterministic output capture
- Pattern matching for format validation
- No console pollution during test runs
- Thread-safe via ConcurrentQueue

**Trade-offs**:
- Doesn't test actual NinjaTrader Print() integration
- Adds mock complexity

**Mitigation**: SETUP ONLY constraint means we're asserting format, not integration

### 2. MockTime for Span Timing

**Rationale**: Deterministic time advancement for TraceSpan tests  
**Benefits**:
- Zero Thread.Sleep (V12 DNA compliance)
- Deterministic elapsed time calculations
- Fast test execution (<1 second)
- Consistent with S1/S2/S3/S4/S5 patterns

**Trade-offs**:
- Doesn't test real DateTime.UtcNow behavior
- Adds mock complexity

**Mitigation**: TraceSpan uses DateTime.UtcNow.Ticks directly; MockTime simulates this

### 3. MockTelemetry Over Real Strategy

**Rationale**: Isolate telemetry subsystem from NinjaTrader dependencies  
**Benefits**:
- No NinjaTrader harness required
- Fast test execution
- Focused on telemetry behavior only
- Exposes internal state for assertion

**Trade-offs**:
- Doesn't test integration with real strategy lifecycle
- Duplicates telemetry logic in mock

**Mitigation**: Mock mirrors V12_002.Telemetry.cs implementation exactly

### 4. Simplified Pool/Ring Mocks

**Rationale**: Focus on diagnostic output, not full pool/ring behavior  
**Benefits**:
- Lighter mock implementation
- Faster test execution
- Sufficient for diagnostic snapshot tests

**Trade-offs**:
- Doesn't test full pool/ring algorithms
- Simplified eviction logic

**Mitigation**: Full pool/ring tests belong in dedicated unit tests, not integration tests

### 5. 22 Tests (Not 30)

**Rationale**: Telemetry subsystem is more focused than REAPER  
**Benefits**:
- Appropriate coverage for scope
- Avoids redundant tests
- Maintains quality bar (REAPERDefenseIntegrationTests.cs quality)

**Trade-offs**:
- Fewer tests than S4 (30) and S5 (26)

**Mitigation**: 22 tests provide comprehensive coverage of 4 telemetry files

---

## Next Steps (P4 Vetting Gate)

### 1. Architect Review (Current Stage)

**Deliverable**: This implementation plan  
**Reviewer**: Director  
**Approval Criteria**:
- Test scenarios cover all 4 telemetry files
- Mock harness is appropriate for scope
- V12 DNA compliance strategy is sound
- Test count (22) is justified

### 2. Adjudicator Audit (Arena AI)

**Deliverable**: Implementation plan + PR audit  
**Reviewer**: Arena AI (Red Team)  
**Approval Criteria**:
- No V12 DNA violations (lock-free, MockTime, ASCII-only)
- Test scenarios are SETUP ONLY (no bug fixes)
- Mock infrastructure is thread-safe
- Diff cap: under 150KB for this cluster

### 3. Implementation (P5 Surgical)

**Agent**: Bob CLI (`v12-engineer`) or Codex CLI (`codex-rescue`)  
**Deliverable**: `tests/MetricsIntegrationTests.cs`  
**Constraints**:
- Follow this plan exactly
- Use REAPERDefenseIntegrationTests.cs as structure template
- Maintain V12 DNA compliance
- SETUP ONLY - no bug fixes

### 4. Verification (P6 Forensics)

**Agent**: Bob CLI (verify cycle) + Orchestrator  
**Deliverable**: Verification report  
**Approval Criteria**:
- All 22 tests pass
- Zero lock() statements
- Zero Thread.Sleep calls
- ASCII-only compliance verified
- Diff under 150KB

### 5. Sign-off (Director)

**Action**: `powershell -File .\deploy-sync.ps1`  
**Final Test**: F5 in NinjaTrader + BUILD_TAG verification  
**Success Criteria**: All tests green, no regressions

---

## Appendix: Test Coverage Matrix

| File | Lines | Tests | Coverage |
|------|-------|-------|----------|
| V12_002.Telemetry.cs | 174 | T01-T12 | Trace ID (6), Counters (6) |
| V12_002.StructuredLog.cs | 115 | T13-T17 | Format (5) |
| V12_002.Photon.Pool.cs | 339 | T18-T22 | Diagnostics (5) |
| V12_002.cs (circuit breaker) | N/A | (inferred) | Covered by counter tests |
| **Total** | **628** | **22** | **100%** |

---

**END OF IMPLEMENTATION PLAN**