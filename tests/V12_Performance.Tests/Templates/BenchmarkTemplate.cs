using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace NinjaTrader.Custom.AddOns.V12_Performance.Tests.Templates
{
    /// <summary>
    /// Template for BenchmarkDotNet performance tests.
    /// Validates Epic 5 performance targets: 0 B allocation, < 300μs latency.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class BenchmarkTemplate
    {
        private object _sut;

        [GlobalSetup]
        public void Setup()
        {
            // Initialize system under test
            _sut = new object();
        }

        [Benchmark]
        public void MethodName_HotPath()
        {
            // Execute hot path code
            // Target: 0 B allocation, < 300μs latency
        }

        [Benchmark]
        public void MethodName_ColdPath()
        {
            // Execute cold path code
            // Less strict performance requirements
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Clean up resources
        }
    }
}

// Made with Bob
