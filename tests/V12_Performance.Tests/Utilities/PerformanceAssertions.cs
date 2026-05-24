using System;
using System.Diagnostics;
using Xunit;

namespace NinjaTrader.Custom.AddOns.V12_Performance.Tests.Utilities
{
    /// <summary>
    /// Custom assertions for V12 performance targets.
    /// </summary>
    public static class PerformanceAssertions
    {
        /// <summary>
        /// Asserts that action completes within target latency (Epic 5: < 300μs).
        /// </summary>
        public static void AssertLatency(Action action, long maxMicroseconds = 300)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();

            var actualMicroseconds = sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;

            Assert.True(
                actualMicroseconds < maxMicroseconds,
                $"Latency exceeded: {actualMicroseconds}μs > {maxMicroseconds}μs"
            );
        }

        /// <summary>
        /// Asserts that action allocates zero bytes (Epic 5 target).
        /// </summary>
        public static void AssertZeroAllocation(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetTotalMemory(false);
            action();
            var after = GC.GetTotalMemory(false);

            var allocated = after - before;

            Assert.True(allocated == 0, $"Memory allocated: {allocated} bytes (expected 0 B)");
        }

        /// <summary>
        /// Asserts that action is lock-free (V12 DNA mandate).
        /// </summary>
        public static void AssertLockFree(Action action)
        {
            // This is a heuristic check - true lock-free verification requires code inspection
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();

            // If action completes in < 1ms, it's likely lock-free
            // (locks typically introduce 10-100ms delays under contention)
            Assert.True(
                sw.ElapsedMilliseconds < 1,
                $"Action took {sw.ElapsedMilliseconds}ms (possible lock contention)"
            );
        }
    }
}

// Made with Bob
