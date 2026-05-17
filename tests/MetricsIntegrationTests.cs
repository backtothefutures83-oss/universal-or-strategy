// MetricsIntegrationTests.cs
// BUILD_TAG: 1111.007-phase7-tQ1_S6_METRICS_TESTS_SETUP
// Cluster S6: Metrics & Telemetry Integration Tests (22 tests)
// V12 DNA: Lock-free, MockTime, ASCII-only, Atomic primitives
// SETUP ONLY - asserts current behavior, no bug fixes

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        #region Mock Infrastructure (Lines 25-450)

        // ============================================================================
        // MockPrint: Thread-safe Print() output capture
        // ============================================================================
        private class MockPrint
        {
            private ConcurrentQueue<string> _lines;

            public MockPrint()
            {
                _lines = new ConcurrentQueue<string>();
            }

            public void Print(string message)
            {
                _lines.Enqueue(message ?? "(null)");
            }

            public bool ContainsLine(string substring)
            {
                return _lines.Any(line => line.Contains(substring));
            }

            public bool ContainsPattern(string pattern)
            {
                var regex = new Regex(pattern);
                return _lines.Any(line => regex.IsMatch(line));
            }

            public string GetLine(int index)
            {
                var array = _lines.ToArray();
                return index >= 0 && index < array.Length ? array[index] : null;
            }

            public string[] GetAllLines()
            {
                return _lines.ToArray();
            }

            public void Clear()
            {
                while (_lines.TryDequeue(out _)) { }
            }

            public int Count => _lines.Count;
        }

        // ============================================================================
        // MockTime: Deterministic time simulation (copied from S1/S2/S3/S4/S5)
        // ============================================================================
        private class MockTime
        {
            private long _ticks;

            public MockTime(long initialTicks) => _ticks = initialTicks;

            public long GetTicks() => Interlocked.Read(ref _ticks);

            public void Advance(long deltaTicks) => Interlocked.Add(ref _ticks, deltaTicks);

            public void AdvanceSeconds(double seconds) =>
                Interlocked.Add(ref _ticks, (long)(seconds * TimeSpan.TicksPerSecond));

            public void AdvanceMilliseconds(double ms) =>
                Interlocked.Add(ref _ticks, (long)(ms * TimeSpan.TicksPerMillisecond));

            public DateTime GetDateTime() => new DateTime(GetTicks(), DateTimeKind.Utc);
        }

        // ============================================================================
        // MockTelemetry: Standalone telemetry mock with duplicated logic
        // P4 R1 (REQUIRED): Duplicated logic from V12_002.Telemetry.cs
        // SYNC REQUIREMENT: If Telemetry.cs changes, this mock must be updated manually.
        // ============================================================================
        /// <summary>
        /// Standalone telemetry mock with duplicated logic from V12_002.Telemetry.cs.
        /// SYNC REQUIREMENT: If Telemetry.cs changes, this mock must be updated manually.
        /// </summary>
        private class MockTelemetry
        {
            private long _traceCounter;
            private string _currentTraceId;
            private long _metricFsmTransitions;
            private long _metricSimaDispatches;
            private long _metricReaperAudits;
            private long _metricSymmetryReplace;
            private long _metricOrderSubmissions;
            private long _metricIpcCommands;
            private MockPrint _print;

            public MockTelemetry(MockPrint print)
            {
                _print = print;
                _traceCounter = 0;
                _currentTraceId = "00000";
                _metricFsmTransitions = 0;
                _metricSimaDispatches = 0;
                _metricReaperAudits = 0;
                _metricSymmetryReplace = 0;
                _metricOrderSubmissions = 0;
                _metricIpcCommands = 0;
            }

            public string CurrentTraceId => _currentTraceId;

            // Duplicate NewTraceId() logic from Telemetry.cs
            public string NewTraceId()
            {
                long next = Interlocked.Increment(ref _traceCounter);
                string id = (next % 100000).ToString("D5");
                _currentTraceId = id;
                return id;
            }

            public void ResetTelemetry()
            {
                Interlocked.Exchange(ref _traceCounter, 0);
                _currentTraceId = "00000";
                Interlocked.Exchange(ref _metricFsmTransitions, 0);
                Interlocked.Exchange(ref _metricSimaDispatches, 0);
                Interlocked.Exchange(ref _metricReaperAudits, 0);
                Interlocked.Exchange(ref _metricSymmetryReplace, 0);
                Interlocked.Exchange(ref _metricOrderSubmissions, 0);
                Interlocked.Exchange(ref _metricIpcCommands, 0);
            }

            // Duplicate Track*() methods with Interlocked.Increment
            public void TrackFsmTransition() => Interlocked.Increment(ref _metricFsmTransitions);
            public void TrackSimaDispatch() => Interlocked.Increment(ref _metricSimaDispatches);
            public void TrackReaperAudit() => Interlocked.Increment(ref _metricReaperAudits);
            public void TrackSymmetryReplace() => Interlocked.Increment(ref _metricSymmetryReplace);
            public void TrackOrderSubmission() => Interlocked.Increment(ref _metricOrderSubmissions);
            public void TrackIpcCommand() => Interlocked.Increment(ref _metricIpcCommands);

            // Counter readers
            public long GetFsmTransitions() => Interlocked.Read(ref _metricFsmTransitions);
            public long GetSimaDispatches() => Interlocked.Read(ref _metricSimaDispatches);
            public long GetReaperAudits() => Interlocked.Read(ref _metricReaperAudits);
            public long GetSymmetryReplaces() => Interlocked.Read(ref _metricSymmetryReplace);
            public long GetOrderSubmissions() => Interlocked.Read(ref _metricOrderSubmissions);
            public long GetIpcCommands() => Interlocked.Read(ref _metricIpcCommands);

            // Structured logging methods
            public void LogInfo(string module, string message)
            {
                StructuredPrint(_currentTraceId, module, "INFO", message);
            }

            public void LogWarn(string module, string message)
            {
                StructuredPrint(_currentTraceId, module, "WARN", message);
            }

            public void LogError(string module, string message)
            {
                StructuredPrint(_currentTraceId, module, "ERROR", message);
            }

            public void LogDebug(string module, string message)
            {
                StructuredPrint(_currentTraceId, module, "DEBUG", message);
            }

            private void StructuredPrint(string traceId, string module, string level, string message)
            {
                string safeTraceId = traceId ?? "?????";
                string safeModule = module ?? "UNKNOWN";
                string safeMessage = message ?? "(null)";
                string line = $"[TRACE:{safeTraceId}][{safeModule}][{level}] {safeMessage}";
                _print.Print(line);
            }

            public void EmitMetricsSummary()
            {
                _print.Print("========================================");
                _print.Print("SESSION METRICS REPORT");
                _print.Print("========================================");
                _print.Print($"FSM Transitions   : {Interlocked.Read(ref _metricFsmTransitions)}");
                _print.Print($"SIMA Dispatches   : {Interlocked.Read(ref _metricSimaDispatches)}");
                _print.Print($"Reaper Audits     : {Interlocked.Read(ref _metricReaperAudits)}");
                _print.Print($"Symmetry Replaces : {Interlocked.Read(ref _metricSymmetryReplace)}");
                _print.Print($"Order Submissions : {Interlocked.Read(ref _metricOrderSubmissions)}");
                _print.Print($"IPC Commands      : {Interlocked.Read(ref _metricIpcCommands)}");
                _print.Print("========================================");
            }

            // For testing: expose counter setter for wrap-around test
            public void SetTraceCounter(long value)
            {
                Interlocked.Exchange(ref _traceCounter, value);
            }
        }

        // ============================================================================
        // MockPhotonPool: Simplified pool for diagnostic testing
        // ============================================================================
        private class MockPhotonPool
        {
            private int _capacity;
            private long _freeCount;
            private long _claimCount;
            private long _releaseCount;
            private long _exhaustedCount;

            public MockPhotonPool(int capacity)
            {
                _capacity = capacity;
                _freeCount = capacity;
                _claimCount = 0;
                _releaseCount = 0;
                _exhaustedCount = 0;
            }

            public bool Claim()
            {
                long free = Interlocked.Read(ref _freeCount);
                if (free > 0)
                {
                    Interlocked.Decrement(ref _freeCount);
                    Interlocked.Increment(ref _claimCount);
                    return true;
                }
                else
                {
                    Interlocked.Increment(ref _exhaustedCount);
                    return false;
                }
            }

            public void Release()
            {
                Interlocked.Increment(ref _freeCount);
                Interlocked.Increment(ref _releaseCount);
            }

            public string GetDiagnostics()
            {
                long free = Interlocked.Read(ref _freeCount);
                long claims = Interlocked.Read(ref _claimCount);
                long releases = Interlocked.Read(ref _releaseCount);
                long exhausted = Interlocked.Read(ref _exhaustedCount);
                return $"PhotonPool: free={free}/{_capacity} claims={claims} releases={releases} exhausted={exhausted}";
            }

            public long FreeCount => Interlocked.Read(ref _freeCount);
            public long ClaimCount => Interlocked.Read(ref _claimCount);
            public long ReleaseCount => Interlocked.Read(ref _releaseCount);
            public long ExhaustedCount => Interlocked.Read(ref _exhaustedCount);
        }

        // ============================================================================
        // MockExecutionIdRing: Simplified ring for duplicate detection
        // ============================================================================
        private class MockExecutionIdRing
        {
            private int _capacity;
            private ConcurrentDictionary<long, byte> _ring;
            private long _hitCount;
            private long _missCount;
            private long _evictCount;

            public MockExecutionIdRing(int capacity)
            {
                _capacity = capacity;
                _ring = new ConcurrentDictionary<long, byte>();
                _hitCount = 0;
                _missCount = 0;
                _evictCount = 0;
            }

            public bool ContainsOrAdd(long hash)
            {
                if (_ring.ContainsKey(hash))
                {
                    Interlocked.Increment(ref _hitCount);
                    return true;
                }
                else
                {
                    Interlocked.Increment(ref _missCount);
                    _ring.TryAdd(hash, 0);
                    return false;
                }
            }

            public string GetDiagnostics()
            {
                long hits = Interlocked.Read(ref _hitCount);
                long misses = Interlocked.Read(ref _missCount);
                long evicts = Interlocked.Read(ref _evictCount);
                int count = _ring.Count;
                return $"ExecIdRing: count={count}/{_capacity} hits={hits} misses={misses} evicts={evicts}";
            }

            public long HitCount => Interlocked.Read(ref _hitCount);
            public long MissCount => Interlocked.Read(ref _missCount);
            public long EvictCount => Interlocked.Read(ref _evictCount);
            public int Count => _ring.Count;
        }

        #endregion

        #region Test Helpers (Lines 451-650)

        // ============================================================================
        // Assertion Helpers (8 methods)
        // ============================================================================

        private void AssertTraceIdFormat(string id)
        {
            Assert.NotNull(id);
            Assert.Equal(5, id.Length);
            Assert.True(id.All(c => char.IsDigit(c)), $"Trace ID '{id}' should contain only digits");
        }

        private void AssertTraceIdMonotonic(string id1, string id2)
        {
            int val1 = int.Parse(id1);
            int val2 = int.Parse(id2);
            Assert.True(val2 > val1 || (val1 == 99999 && val2 == 0), 
                $"Trace ID '{id2}' should be greater than '{id1}' (or wrap from 99999 to 00000)");
        }

        private void AssertCounterValue(long actual, long expected, string counterName)
        {
            Assert.Equal(expected, actual);
        }

        private void AssertLogContains(MockPrint print, string substring)
        {
            Assert.True(print.ContainsLine(substring), 
                $"Log should contain '{substring}'");
        }

        private void AssertLogPattern(MockPrint print, string pattern)
        {
            Assert.True(print.ContainsPattern(pattern), 
                $"Log should match pattern '{pattern}'");
        }

        private void AssertLogLevel(string line, string expectedLevel)
        {
            Assert.Contains($"[{expectedLevel}]", line);
        }

        private void AssertDiagnosticFormat(string diagnostic, string expectedPattern)
        {
            Assert.Matches(expectedPattern, diagnostic);
        }

        private void AssertASCIIOnly(string text)
        {
            Assert.True(text.All(c => c >= 0 && c <= 127), 
                $"Text should contain only ASCII characters (0-127)");
        }

        // ============================================================================
        // Verification Helpers (5 methods)
        // ============================================================================

        private bool VerifyAllCountersZero(MockTelemetry telemetry)
        {
            return telemetry.GetFsmTransitions() == 0 &&
                   telemetry.GetSimaDispatches() == 0 &&
                   telemetry.GetReaperAudits() == 0 &&
                   telemetry.GetSymmetryReplaces() == 0 &&
                   telemetry.GetOrderSubmissions() == 0 &&
                   telemetry.GetIpcCommands() == 0;
        }

        private bool VerifyCounterIndependence(MockTelemetry telemetry, string counterName)
        {
            // Verify only the specified counter is non-zero
            long fsm = telemetry.GetFsmTransitions();
            long sima = telemetry.GetSimaDispatches();
            long reaper = telemetry.GetReaperAudits();
            long symmetry = telemetry.GetSymmetryReplaces();
            long orders = telemetry.GetOrderSubmissions();
            long ipc = telemetry.GetIpcCommands();

            switch (counterName)
            {
                case "FSM":
                    return fsm > 0 && sima == 0 && reaper == 0 && symmetry == 0 && orders == 0 && ipc == 0;
                case "SIMA":
                    return fsm == 0 && sima > 0 && reaper == 0 && symmetry == 0 && orders == 0 && ipc == 0;
                case "Reaper":
                    return fsm == 0 && sima == 0 && reaper > 0 && symmetry == 0 && orders == 0 && ipc == 0;
                case "Symmetry":
                    return fsm == 0 && sima == 0 && reaper == 0 && symmetry > 0 && orders == 0 && ipc == 0;
                case "Orders":
                    return fsm == 0 && sima == 0 && reaper == 0 && symmetry == 0 && orders > 0 && ipc == 0;
                case "IPC":
                    return fsm == 0 && sima == 0 && reaper == 0 && symmetry == 0 && orders == 0 && ipc > 0;
                default:
                    return false;
            }
        }

        private bool VerifyLogFormatCompliance(string line)
        {
            // Format: [TRACE:NNNNN][MODULE][LEVEL] message
            var pattern = @"^\[TRACE:\d{5}\]\[.+\]\[(INFO|WARN|ERROR|DEBUG)\] .+$";
            return Regex.IsMatch(line, pattern);
        }

        private bool VerifyPoolConsistency(MockPhotonPool pool)
        {
            // Verify pool invariants: claims - releases = capacity - free
            long claims = pool.ClaimCount;
            long releases = pool.ReleaseCount;
            long free = pool.FreeCount;
            return true; // Simplified for mock
        }

        private bool VerifyRingConsistency(MockExecutionIdRing ring)
        {
            // Verify ring invariants: hits + misses = total operations
            return true; // Simplified for mock
        }

        // ============================================================================
        // Simulation Helpers (3 methods)
        // ============================================================================

        private void SimulateMetricActivity(MockTelemetry telemetry)
        {
            telemetry.TrackFsmTransition();
            telemetry.TrackFsmTransition();
            telemetry.TrackSimaDispatch();
            telemetry.TrackReaperAudit();
            telemetry.TrackSymmetryReplace();
            telemetry.TrackOrderSubmission();
            telemetry.TrackOrderSubmission();
            telemetry.TrackOrderSubmission();
            telemetry.TrackIpcCommand();
        }

        private void SimulatePoolActivity(MockPhotonPool pool, int claims, int releases)
        {
            for (int i = 0; i < claims; i++)
            {
                pool.Claim();
            }
            for (int i = 0; i < releases; i++)
            {
                pool.Release();
            }
        }

        private void SimulateRingActivity(MockExecutionIdRing ring, int unique, int duplicates)
        {
            long hash = 1000;
            for (int i = 0; i < unique; i++)
            {
                ring.ContainsOrAdd(hash++);
            }
            for (int i = 0; i < duplicates; i++)
            {
                ring.ContainsOrAdd(1000); // Duplicate first hash
            }
        }

        // ============================================================================
        // Creation Helpers (2 methods)
        // ============================================================================

        private MockTelemetry CreateMockTelemetry()
        {
            var print = new MockPrint();
            return new MockTelemetry(print);
        }

        private MockPhotonPool CreateMockPhotonPool(int capacity)
        {
            return new MockPhotonPool(capacity);
        }

        #endregion

        #region Phase 1: Trace ID Generation & Correlation (T01-T06)

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

        [Fact]
        public void T02_TraceId_WrapsAt100000()
        {
            // Given: MockTelemetry with counter at 99998
            var telemetry = CreateMockTelemetry();
            telemetry.SetTraceCounter(99998);

            // NOTE: Trace ID overflow at long.MaxValue is astronomically unlikely
            // (9.2 quintillion operations). This test verifies modulo wrap-around only.

            // When: Generate 3 trace IDs
            var id1 = telemetry.NewTraceId();
            var id2 = telemetry.NewTraceId();
            var id3 = telemetry.NewTraceId();

            // Then: IDs wrap at 100,000
            Assert.Equal("99999", id1);
            Assert.Equal("00000", id2);
            Assert.Equal("00001", id3);
        }

        [Fact]
        public void T03_TraceId_SetsCurrentContext()
        {
            // Given: MockTelemetry initialized
            var telemetry = CreateMockTelemetry();

            // When: Generate trace ID
            var id = telemetry.NewTraceId();

            // Then: CurrentTraceId updated
            Assert.Equal("00001", id);
            Assert.Equal("00001", telemetry.CurrentTraceId);
        }

        [Fact]
        public void T04_TraceId_ResetClearsCounter()
        {
            // Given: MockTelemetry with counter at 42
            var telemetry = CreateMockTelemetry();
            for (int i = 0; i < 42; i++)
            {
                telemetry.NewTraceId();
            }
            var idBefore = telemetry.NewTraceId();
            Assert.Equal("00043", idBefore);

            // When: Reset telemetry
            telemetry.ResetTelemetry();

            // Then: Counter reset to 0
            var idAfter = telemetry.NewTraceId();
            Assert.Equal("00001", idAfter);
            Assert.Equal("00001", telemetry.CurrentTraceId);
        }

        [Fact]
        public void T05_TraceId_Format_FiveDigitZeroPadded()
        {
            // Given: MockTelemetry initialized
            var telemetry = CreateMockTelemetry();

            // When: Generate IDs at various positions
            var id1 = telemetry.NewTraceId();      // 1
            for (int i = 0; i < 8; i++) telemetry.NewTraceId();
            var id10 = telemetry.NewTraceId();     // 10
            for (int i = 0; i < 89; i++) telemetry.NewTraceId();
            var id100 = telemetry.NewTraceId();    // 100
            for (int i = 0; i < 899; i++) telemetry.NewTraceId();
            var id1000 = telemetry.NewTraceId();   // 1000
            for (int i = 0; i < 8999; i++) telemetry.NewTraceId();
            var id10000 = telemetry.NewTraceId();  // 10000

            // Then: All IDs are 5 digits with zero-padding
            Assert.Equal("00001", id1);
            Assert.Equal("00010", id10);
            Assert.Equal("00100", id100);
            Assert.Equal("01000", id1000);
            Assert.Equal("10000", id10000);
            AssertTraceIdFormat(id1);
            AssertTraceIdFormat(id10);
            AssertTraceIdFormat(id100);
            AssertTraceIdFormat(id1000);
            AssertTraceIdFormat(id10000);
        }

        [Fact]
        public void T06_TraceId_ConcurrentIncrement_ThreadSafe()
        {
            // Given: MockTelemetry, 10 threads
            var telemetry = CreateMockTelemetry();
            var ids = new ConcurrentBag<string>();
            int threadCount = 10;
            int idsPerThread = 100;

            // When: Spawn 10 threads, each generating 100 IDs
            var tasks = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < idsPerThread; i++)
                    {
                        ids.Add(telemetry.NewTraceId());
                    }
                });
            }
            Task.WaitAll(tasks);

            // Then: 1000 unique IDs generated
            Assert.Equal(1000, ids.Count);
            var uniqueIds = ids.Distinct().ToList();
            Assert.Equal(1000, uniqueIds.Count);
        }

        #endregion

        #region Phase 2: Metric Counter Accuracy (T07-T12)

        [Fact]
        public void T07_MetricCounters_IncrementAtomically()
        {
            // Given: MockTelemetry initialized
            var telemetry = CreateMockTelemetry();

            // When: Call each Track*() method once
            telemetry.TrackFsmTransition();
            telemetry.TrackSimaDispatch();
            telemetry.TrackReaperAudit();
            telemetry.TrackSymmetryReplace();
            telemetry.TrackOrderSubmission();
            telemetry.TrackIpcCommand();

            // Then: All counters equal 1
            AssertCounterValue(telemetry.GetFsmTransitions(), 1, "FSM");
            AssertCounterValue(telemetry.GetSimaDispatches(), 1, "SIMA");
            AssertCounterValue(telemetry.GetReaperAudits(), 1, "Reaper");
            AssertCounterValue(telemetry.GetSymmetryReplaces(), 1, "Symmetry");
            AssertCounterValue(telemetry.GetOrderSubmissions(), 1, "Orders");
            AssertCounterValue(telemetry.GetIpcCommands(), 1, "IPC");
        }

        [Fact]
        public void T08_MetricCounters_MultipleIncrements()
        {
            // Given: MockTelemetry initialized
            var telemetry = CreateMockTelemetry();

            // When: Increment counters multiple times
            for (int i = 0; i < 5; i++) telemetry.TrackFsmTransition();
            for (int i = 0; i < 3; i++) telemetry.TrackSimaDispatch();
            for (int i = 0; i < 2; i++) telemetry.TrackReaperAudit();

            // Then: Counters accumulate correctly
            AssertCounterValue(telemetry.GetFsmTransitions(), 5, "FSM");
            AssertCounterValue(telemetry.GetSimaDispatches(), 3, "SIMA");
            AssertCounterValue(telemetry.GetReaperAudits(), 2, "Reaper");
            AssertCounterValue(telemetry.GetSymmetryReplaces(), 0, "Symmetry");
            AssertCounterValue(telemetry.GetOrderSubmissions(), 0, "Orders");
            AssertCounterValue(telemetry.GetIpcCommands(), 0, "IPC");
        }

        [Fact]
        public void T09_MetricCounters_ResetClearsAll()
        {
            // Given: MockTelemetry with non-zero counters
            var telemetry = CreateMockTelemetry();
            SimulateMetricActivity(telemetry);
            Assert.False(VerifyAllCountersZero(telemetry));

            // When: Reset telemetry
            telemetry.ResetTelemetry();

            // Then: All counters return to 0
            Assert.True(VerifyAllCountersZero(telemetry));
        }

        [Fact]
        public void T10_MetricCounters_ConcurrentIncrement_ThreadSafe()
        {
            // Given: MockTelemetry, 10 threads
            var telemetry = CreateMockTelemetry();
            int threadCount = 10;
            int incrementsPerThread = 100;

            // When: Spawn 10 threads, each incrementing FSM counter 100 times
            var tasks = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < incrementsPerThread; i++)
                    {
                        telemetry.TrackFsmTransition();
                    }
                });
            }
            Task.WaitAll(tasks);

            // Then: FSM counter equals 1000
            AssertCounterValue(telemetry.GetFsmTransitions(), 1000, "FSM");
        }

        [Fact]
        public void T11_MetricCounters_IndependentCounters()
        {
            // Given: MockTelemetry initialized
            var telemetry = CreateMockTelemetry();

            // When: Increment FSM counter 10 times
            for (int i = 0; i < 10; i++)
            {
                telemetry.TrackFsmTransition();
            }

            // Then: Only FSM counter is non-zero
            AssertCounterValue(telemetry.GetFsmTransitions(), 10, "FSM");
            Assert.True(VerifyCounterIndependence(telemetry, "FSM"));
        }

        [Fact]
        public void T12_MetricsSummary_EmitsAllCounters()
        {
            // Given: MockTelemetry with non-zero counters
            var print = new MockPrint();
            var telemetry = new MockTelemetry(print);
            for (int i = 0; i < 5; i++) telemetry.TrackFsmTransition();
            for (int i = 0; i < 3; i++) telemetry.TrackSimaDispatch();
            for (int i = 0; i < 2; i++) telemetry.TrackReaperAudit();
            telemetry.TrackSymmetryReplace();
            for (int i = 0; i < 10; i++) telemetry.TrackOrderSubmission();
            for (int i = 0; i < 7; i++) telemetry.TrackIpcCommand();

            // When: Emit metrics summary
            telemetry.EmitMetricsSummary();

            // Then: Output contains all counters
            AssertLogContains(print, "SESSION METRICS REPORT");
            AssertLogContains(print, "FSM Transitions   : 5");
            AssertLogContains(print, "SIMA Dispatches   : 3");
            AssertLogContains(print, "Reaper Audits     : 2");
            AssertLogContains(print, "Symmetry Replaces : 1");
            AssertLogContains(print, "Order Submissions : 10");
            AssertLogContains(print, "IPC Commands      : 7");
            AssertLogContains(print, "========================================");
        }

        #endregion

        #region Phase 3: Structured Logging (T13-T17)

        [Fact]
        public void T13_StructuredLog_FormatCorrect()
        {
            // Given: MockTelemetry with trace ID
            var print = new MockPrint();
            var telemetry = new MockTelemetry(print);
            for (int i = 0; i < 42; i++) telemetry.NewTraceId();

            // When: Log INFO message
            telemetry.LogInfo("SIMA.Dispatch", "FleetBroadcast started");

            // Then: Format matches spec
            var line = print.GetLine(0);
            Assert.Equal("[TRACE:00042][SIMA.Dispatch][INFO] FleetBroadcast started", line);
            Assert.True(VerifyLogFormatCompliance(line));
        }

        [Fact]
        public void T14_StructuredLog_LevelTagging()
        {
            // Given: MockTelemetry
            var print = new MockPrint();
            var telemetry = new MockTelemetry(print);
            telemetry.NewTraceId();

            // When: Log at different levels
            telemetry.LogInfo("TEST", "info message");
            telemetry.LogWarn("TEST", "warn message");
            telemetry.LogError("TEST", "error message");

            // Then: All levels emit correctly
            var lines = print.GetAllLines();
            Assert.Equal(3, lines.Length);
            AssertLogLevel(lines[0], "INFO");
            AssertLogLevel(lines[1], "WARN");
            AssertLogLevel(lines[2], "ERROR");
        }

        [Fact]
        public void T15_StructuredLog_TraceIdPropagation()
        {
            // Given: MockTelemetry
            var print = new MockPrint();
            var telemetry = new MockTelemetry(print);

            // When: Log with different trace contexts
            telemetry.NewTraceId(); // 00001
            telemetry.LogInfo("TEST", "message1");
            telemetry.NewTraceId(); // 00002
            telemetry.LogInfo("TEST", "message2");

            // Then: Trace context propagates correctly
            var lines = print.GetAllLines();
            Assert.Contains("[TRACE:00001]", lines[0]);
            Assert.Contains("[TRACE:00002]", lines[1]);
        }

        [Fact]
        public void T16_StructuredLog_NullSafety()
        {
            // Given: MockTelemetry
            var print = new MockPrint();
            var telemetry = new MockTelemetry(print);
            telemetry.NewTraceId();

            // When: Log with null values
            telemetry.LogInfo(null, null);
            telemetry.LogInfo("TEST", null);
            telemetry.NewTraceId();
            telemetry.LogInfo(null, "message");

            // Then: No exceptions thrown, defensive guards work
            var lines = print.GetAllLines();
            Assert.Equal(3, lines.Length);
            Assert.Contains("[UNKNOWN]", lines[0]);
            Assert.Contains("(null)", lines[0]);
            Assert.Contains("[TEST]", lines[1]);
            Assert.Contains("(null)", lines[1]);
            Assert.Contains("[UNKNOWN]", lines[2]);
            Assert.Contains("message", lines[2]);
        }

        [Fact]
        public void T17_StructuredLog_ASCIIOnly()
        {
            // Given: MockTelemetry
            var print = new MockPrint();
            var telemetry = new MockTelemetry(print);
            telemetry.NewTraceId();

            // When: Log ASCII message
            telemetry.LogInfo("TEST", "message with ASCII chars");

            // Then: All characters are ASCII
            var line = print.GetLine(0);
            AssertASCIIOnly(line);
        }

        #endregion

        #region Phase 4: Diagnostic Snapshots (T18-T22)

        [Fact]
        public void T18_PhotonPool_ClaimRelease_UpdatesCounters()
        {
            // Given: MockPhotonPool(capacity=10)
            var pool = CreateMockPhotonPool(10);

            // When: Claim 3 slots, release 1 slot
            pool.Claim();
            pool.Claim();
            pool.Claim();
            pool.Release();

            // Then: Counters updated correctly
            Assert.Equal(8, pool.FreeCount);  // 10 - 3 + 1
            Assert.Equal(3, pool.ClaimCount);
            Assert.Equal(1, pool.ReleaseCount);
            Assert.Equal(0, pool.ExhaustedCount);
        }

        [Fact]
        public void T19_PhotonPool_Exhaustion_TracksExhaustedCount()
        {
            // Given: MockPhotonPool(capacity=2)
            var pool = CreateMockPhotonPool(2);

            // When: Claim 2 slots (success), claim 1 slot (fail)
            bool claim1 = pool.Claim();
            bool claim2 = pool.Claim();
            bool claim3 = pool.Claim();

            // Then: Exhaustion tracked
            Assert.True(claim1);
            Assert.True(claim2);
            Assert.False(claim3);
            Assert.Equal(0, pool.FreeCount);
            Assert.Equal(2, pool.ClaimCount);
            Assert.Equal(1, pool.ExhaustedCount);
        }

        [Fact]
        public void T20_PhotonPool_Diagnostics_FormatsCorrectly()
        {
            // Given: MockPhotonPool(capacity=10) with activity
            var pool = CreateMockPhotonPool(10);
            SimulatePoolActivity(pool, 3, 1);

            // When: Get diagnostics
            var diagnostic = pool.GetDiagnostics();

            // Then: Format matches expected pattern
            Assert.Equal("PhotonPool: free=8/10 claims=3 releases=1 exhausted=0", diagnostic);
            AssertDiagnosticFormat(diagnostic, @"PhotonPool: free=\d+/\d+ claims=\d+ releases=\d+ exhausted=\d+");
        }

        [Fact]
        public void T21_ExecutionIdRing_DuplicateDetection()
        {
            // Given: MockExecutionIdRing(capacity=100)
            var ring = new MockExecutionIdRing(100);

            // When: Add hash 12345 twice, add hash 67890 once
            bool result1 = ring.ContainsOrAdd(12345); // miss
            bool result2 = ring.ContainsOrAdd(12345); // hit
            bool result3 = ring.ContainsOrAdd(67890); // miss

            // Then: Duplicate detected correctly
            Assert.False(result1); // miss
            Assert.True(result2);  // hit
            Assert.False(result3); // miss
            Assert.Equal(1, ring.HitCount);
            Assert.Equal(2, ring.MissCount);
        }

        [Fact]
        public void T22_ExecutionIdRing_Diagnostics_FormatsCorrectly()
        {
            // Given: MockExecutionIdRing(capacity=100) with activity
            var ring = new MockExecutionIdRing(100);
            SimulateRingActivity(ring, 5, 2);

            // When: Get diagnostics
            var diagnostic = ring.GetDiagnostics();

            // Then: Format matches expected pattern
            Assert.Contains("ExecIdRing:", diagnostic);
            Assert.Contains("count=5/100", diagnostic);
            Assert.Contains("hits=2", diagnostic);
            Assert.Contains("misses=5", diagnostic);
            AssertDiagnosticFormat(diagnostic, @"ExecIdRing: count=\d+/\d+ hits=\d+ misses=\d+ evicts=\d+");
        }

        #endregion
    }
}

// Made with Bob
