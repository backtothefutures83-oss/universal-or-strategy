using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace R28
{
    // R28 MMIO SPSC Ring Buffer.
    //   - Backing:   MemoryMappedFile + pinned byte* via SafeMemoryMappedViewHandle (FIX-D6 lifetime).
    //   - Cursors:   two cache-line-isolated longs at offsets 0 and 64 (Ralph cache-line audit).
    //   - Integrity: XorShadow (ADR-016), final 8 bytes of T, branch-free (FIX-D2).
    //   - Safety:    Volatile.Read/Write on every cursor access (FIX-D7 torn-read guard).
    //   - Lock-free: no Monitor, no SpinLock, no managed locks.
    internal sealed unsafe class MmioSpscRing<T> : IDisposable where T : unmanaged
    {
        private const int HeaderBytes          = 128; // 2 cache lines
        private const int ProducerCursorOffset = 0;
        private const int ConsumerCursorOffset = 64;

        private readonly int   _capacity;
        private readonly int   _mask;
        private readonly int   _slotSize;
        private readonly int   _shadowOffset;
        private readonly ulong _salt;

        private readonly MemoryMappedFile         _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly byte*                    _region;

        private int _disposed;

        public MmioSpscRing(int capacityPowerOf2)
        {
            if (capacityPowerOf2 < 2 || (capacityPowerOf2 & (capacityPowerOf2 - 1)) != 0)
                throw new ArgumentException("Capacity must be power of 2");

            _slotSize = Unsafe.SizeOf<T>();
            if ((_slotSize & 7) != 0)
                throw new InvalidOperationException("T size must be a multiple of 8 bytes");

            _shadowOffset = _slotSize - 8;

            // Enforce XorShadow field placement (FIX-D2).
            int shadowFieldOffset = Marshal.OffsetOf(typeof(T), "XorShadow").ToInt32();
            if (shadowFieldOffset != _shadowOffset)
                throw new InvalidOperationException("T.XorShadow must be the final 8 bytes of T");

            _capacity = capacityPowerOf2;
            _mask     = capacityPowerOf2 - 1;

            long totalBytes = HeaderBytes + (long)_slotSize * _capacity;

            _mmf = MemoryMappedFile.CreateNew(null, totalBytes, MemoryMappedFileAccess.ReadWrite);
            _accessor = _mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

            byte* ptr = null;
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            _region = ptr;

            // Zero the header region (cursors start at 0).
            for (int i = 0; i < HeaderBytes; i++) _region[i] = 0;
            // Zero the slot region so fresh enqueues do not inherit prior-run garbage.
            long slotRegionBytes = (long)_slotSize * _capacity;
            for (long i = 0; i < slotRegionBytes; i++) _region[HeaderBytes + i] = 0;

            // Per-ring 64-bit salt defeats zero-payload shadow collision.
            _salt = unchecked((ulong)Guid.NewGuid().GetHashCode() * 0x9E3779B97F4A7C15UL);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadProducerCursor()
        {
            return Volatile.Read(ref *(long*)(_region + ProducerCursorOffset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadConsumerCursor()
        {
            return Volatile.Read(ref *(long*)(_region + ConsumerCursorOffset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PublishProducerCursor(long value)
        {
            Volatile.Write(ref *(long*)(_region + ProducerCursorOffset), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PublishConsumerCursor(long value)
        {
            Volatile.Write(ref *(long*)(_region + ConsumerCursorOffset), value);
        }

        public int Capacity { get { return _capacity; } }

        public int Count { get { return (int)(ReadProducerCursor() - ReadConsumerCursor()); } }

        public bool IsEmpty { get { return ReadProducerCursor() == ReadConsumerCursor(); } }

        public bool TryEnqueue(ref T item)
        {
            long prod = ReadProducerCursor();
            long cons = ReadConsumerCursor();
            if (prod - cons >= _capacity) return false;

            int idx = (int)(prod & _mask);
            byte* slotPtr = _region + HeaderBytes + (long)idx * _slotSize;

            // Write user payload (the caller's shadow field is discarded and
            // overwritten with the authoritative computed value below).
            Unsafe.WriteUnaligned<T>(slotPtr, item);

            ulong shadow = XorShadow.Compute(slotPtr, _shadowOffset, _salt);
            *(ulong*)(slotPtr + _shadowOffset) = shadow;

            // Publish barrier -- consumer may see the new cursor only after the slot is fully written.
            PublishProducerCursor(prod + 1);
            return true;
        }

        public bool TryDequeue(out T item, out bool shadowValid)
        {
            long cons = ReadConsumerCursor();
            long prod = ReadProducerCursor();
            if (cons >= prod)
            {
                item = default(T);
                shadowValid = false;
                return false;
            }

            int idx = (int)(cons & _mask);
            byte* slotPtr = _region + HeaderBytes + (long)idx * _slotSize;

            ulong stored   = *(ulong*)(slotPtr + _shadowOffset);
            ulong computed = XorShadow.Compute(slotPtr, _shadowOffset, _salt);
            shadowValid = (stored == computed);

            item = Unsafe.ReadUnaligned<T>(slotPtr);

            // Consume barrier -- producer may see the new cursor only after the slot has been read.
            PublishConsumerCursor(cons + 1);
            return true;
        }

        // Test-only backdoor: sandbox harness uses this for the corruption test (test #3).
        // Not emitted into the src/ scaffold -- stripped during port.
        internal byte* DebugRegionPointer() { return _region; }
        internal int   DebugHeaderBytes()   { return HeaderBytes; }
        internal int   DebugSlotSize()      { return _slotSize; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { _accessor.SafeMemoryMappedViewHandle.ReleasePointer(); } catch { }
            try { _accessor.Dispose(); } catch { }
            try { _mmf.Dispose();       } catch { }
        }
    }
}
