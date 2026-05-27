using System;
using System.Threading;

// v28.0 Sovereign Photon: lock-free SPSC ring, XorShadow integrity in-slot
// ADR-012 + ADR-016: zero-allocation, zero-GC, single-producer/single-consumer
//
// Integrity contract: T must reserve its LAST 8 bytes as a `ulong Shadow` field
// populated by the caller BEFORE enqueue (via ComputeFleetDispatchShadow or equivalent).
// The ring does not inspect, compute, or verify Shadow -- the caller owns integrity.
// Rationale: keeping the ring agnostic of T's shape lets us reuse the class for any
// blittable slot type without re-plumbing the checksum pipeline.

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private sealed class SPSCRing<T>
            where T : struct
        {
            private readonly T[] _buffer;
            private readonly int _mask;

            // Cache-line isolation: 7 long pads between cursors. False-sharing hurts
            // only throughput, not correctness. Both cursors are Volatile-fenced.
            private long _producerCursor;
#pragma warning disable 0169
            private long _pad1,
                _pad2,
                _pad3,
                _pad4,
                _pad5,
                _pad6,
                _pad7;
#pragma warning restore 0169
            private long _consumerCursor;

            public int Capacity
            {
                get { return _buffer.Length; }
            }

            public int Count
            {
                get { return (int)(Volatile.Read(ref _producerCursor) - Volatile.Read(ref _consumerCursor)); }
            }

            public bool IsEmpty
            {
                get { return Volatile.Read(ref _producerCursor) == Volatile.Read(ref _consumerCursor); }
            }

            public SPSCRing(int capacityPowerOf2)
            {
                if (capacityPowerOf2 < 2 || (capacityPowerOf2 & (capacityPowerOf2 - 1)) != 0)
                    throw new ArgumentException("Capacity must be power of 2", "capacityPowerOf2");
                _buffer = new T[capacityPowerOf2];
                _mask = capacityPowerOf2 - 1;
                _producerCursor = 0;
                _consumerCursor = 0;
            }

            public bool TryEnqueue(ref T item)
            {
                long prod = Volatile.Read(ref _producerCursor);
                long cons = Volatile.Read(ref _consumerCursor);
                if (prod - cons >= _buffer.Length)
                    return false; // ring full
                int idx = (int)(prod & _mask);
                _buffer[idx] = item;
                Volatile.Write(ref _producerCursor, prod + 1); // publish barrier
                return true;
            }

            public bool TryDequeue(out T item)
            {
                long cons = Volatile.Read(ref _consumerCursor);
                long prod = Volatile.Read(ref _producerCursor);
                if (cons >= prod)
                {
                    item = default(T);
                    return false; // ring empty
                }
                int idx = (int)(cons & _mask);
                item = _buffer[idx];
                Volatile.Write(ref _consumerCursor, cons + 1); // consume barrier
                return true;
            }
        }
    }
}
