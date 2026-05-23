using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Zero-allocation latency measurement probe using Stopwatch.GetTimestamp().
    /// MUST be used in Start/Stop pairs. IsValid property detects misuse.
    /// Thread-safe via immutable state after Start().
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LatencyProbe
    {
        private readonly long _startTicks;
        private readonly long _stopTicks;

        /// <summary>
        /// Validates probe was used correctly (Start then Stop called).
        /// </summary>
        public bool IsValid => _startTicks > 0 && _stopTicks > _startTicks;

        /// <summary>
        /// Elapsed time in microseconds. Returns -1 if probe is invalid.
        /// </summary>
        public long ElapsedMicroseconds
        {
            get
            {
                if (!IsValid)
                {
                    return -1;
                }

                long elapsedTicks = _stopTicks - _startTicks;
                return (elapsedTicks * 1_000_000) / Stopwatch.Frequency;
            }
        }

        /// <summary>
        /// Start latency measurement. Returns new probe instance.
        /// MUST be followed by Stop() call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LatencyProbe Start()
        {
            return new LatencyProbe(Stopwatch.GetTimestamp(), 0);
        }

        /// <summary>
        /// Stop latency measurement. Returns new probe instance with stop time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LatencyProbe Stop()
        {
            return new LatencyProbe(_startTicks, Stopwatch.GetTimestamp());
        }

        private LatencyProbe(long startTicks, long stopTicks)
        {
            _startTicks = startTicks;
            _stopTicks = stopTicks;
        }
    }
}

// Made with Bob
