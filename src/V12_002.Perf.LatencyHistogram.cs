using System;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Zero-allocation latency histogram with pre-allocated buckets.
    /// Thread-safe via Interlocked operations (lock-free).
    /// Buckets: [0-10us, 10-50us, 50-100us, 100-500us, 500-1000us, 1000-5000us, 5000+us]
    /// </summary>
    public sealed class LatencyHistogram
    {
        private readonly string _name;
        private readonly long[] _buckets;
        private long _totalSamples;
        private long _invalidSamples;

        // Bucket boundaries in microseconds
        private static readonly long[] BucketBoundaries = { 10, 50, 100, 500, 1000, 5000 };

        public LatencyHistogram(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _buckets = new long[BucketBoundaries.Length + 1]; // +1 for overflow bucket
            _totalSamples = 0;
            _invalidSamples = 0;
        }

        /// <summary>
        /// Record a latency sample. Thread-safe via Interlocked.Increment.
        /// </summary>
        public void Record(LatencyProbe probe)
        {
            if (!probe.IsValid)
            {
                Interlocked.Increment(ref _invalidSamples);
                return;
            }

            long micros = probe.ElapsedMicroseconds;
            int bucketIndex = GetBucketIndex(micros);

            Interlocked.Increment(ref _buckets[bucketIndex]);
            Interlocked.Increment(ref _totalSamples);
        }

        /// <summary>
        /// Get snapshot of histogram data. Returns copy to avoid race conditions.
        /// </summary>
        public HistogramSnapshot GetSnapshot()
        {
            long[] bucketsCopy = new long[_buckets.Length];
            for (int i = 0; i < _buckets.Length; i++)
            {
                bucketsCopy[i] = Interlocked.Read(ref _buckets[i]);
            }

            return new HistogramSnapshot(
                _name,
                bucketsCopy,
                Interlocked.Read(ref _totalSamples),
                Interlocked.Read(ref _invalidSamples)
            );
        }

        /// <summary>
        /// Reset all counters to zero. Thread-safe.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _buckets.Length; i++)
            {
                Interlocked.Exchange(ref _buckets[i], 0);
            }

            Interlocked.Exchange(ref _totalSamples, 0);
            Interlocked.Exchange(ref _invalidSamples, 0);
        }

        private static int GetBucketIndex(long micros)
        {
            for (int i = 0; i < BucketBoundaries.Length; i++)
            {
                if (micros < BucketBoundaries[i])
                {
                    return i;
                }
            }

            return BucketBoundaries.Length; // Overflow bucket
        }
    }

    /// <summary>
    /// Immutable snapshot of histogram state at a point in time.
    /// </summary>
    public sealed class HistogramSnapshot
    {
        public string Name { get; }
        public long[] Buckets { get; }
        public long TotalSamples { get; }
        public long InvalidSamples { get; }

        public HistogramSnapshot(string name, long[] buckets, long totalSamples, long invalidSamples)
        {
            Name = name;
            Buckets = buckets;
            TotalSamples = totalSamples;
            InvalidSamples = invalidSamples;
        }

        /// <summary>
        /// Calculate percentile from histogram buckets.
        /// Returns -1 if insufficient samples.
        /// </summary>
        public long GetPercentile(double percentile)
        {
            if (TotalSamples == 0 || percentile < 0 || percentile > 100)
            {
                return -1;
            }

            long targetCount = (long)(TotalSamples * (percentile / 100.0));
            long cumulativeCount = 0;

            long[] boundaries = { 10, 50, 100, 500, 1000, 5000, long.MaxValue };

            for (int i = 0; i < Buckets.Length; i++)
            {
                cumulativeCount += Buckets[i];
                if (cumulativeCount >= targetCount)
                {
                    return boundaries[i];
                }
            }

            return -1;
        }

        /// <summary>
        /// Format histogram as ASCII string for logging.
        /// </summary>
        public string ToAsciiString()
        {
            if (TotalSamples == 0)
            {
                return string.Format("{0}: No samples", Name);
            }

            string[] labels = { "0-10us", "10-50us", "50-100us", "100-500us", "500-1000us", "1000-5000us", "5000+us" };
            string result = string.Format("{0} (n={1}, invalid={2}):\n", Name, TotalSamples, InvalidSamples);

            for (int i = 0; i < Buckets.Length; i++)
            {
                double pct = (Buckets[i] * 100.0) / TotalSamples;
                result += string.Format("  {0}: {1} ({2:F1}%)\n", labels[i], Buckets[i], pct);
            }

            result += string.Format(
                "  p50: {0}us, p95: {1}us, p99: {2}us",
                GetPercentile(50),
                GetPercentile(95),
                GetPercentile(99)
            );

            return result;
        }
    }
}

// Made with Bob
