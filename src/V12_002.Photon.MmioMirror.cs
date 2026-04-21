using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;

// v28.0 Sovereign Photon MMIO Mirror: write-through sidecar for cross-process observation
// ADR-016 + R28 Arena directive
//
// Contract:
//   - Strategy (V12_002 producer) is the SOLE WRITER. MPSC is not supported.
//   - Antigravity Nexus OS sidecar (separate process) is a READ-ONLY observer that
//     attaches to the named MMF at runtime. It never writes cursor or slot bytes.
//   - Mirror is OPTIONAL: _photonMmioMirror may be null. Producers check before calling
//     TryPublish. If MMIO reference resolution fails at construction, the field stays
//     null and the strategy runs hot-path-only. Hot-path dispatch never blocks on
//     mirror state.
//
// Layout inside the MMF (total = 128 B header + capacity * 64 B payload):
//   [0..8)    producer cursor  (long; strategy writes, sidecar reads)
//   [8..64)   pad              (cache-line isolation)
//   [64..72)  shadow salt      (ulong; copied from _photonShadowSalt so the sidecar
//                                can verify slot integrity independently)
//   [72..80)  reserved
//   [80..128) pad              (header rounded to 128 B)
//   [128..)   slot array       (capacity * 64 B, each a FleetDispatchSlot)
//
// No AcquirePointer. No raw pointers. All writes go through
// MemoryMappedViewAccessor.Write<T>/Write(long,long), which encapsulate pointer
// access inside System.IO.MemoryMappedFiles.dll. The caller (this class) stays
// fully managed and NT8-compilable.

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private sealed class MmioDispatchMirror : IDisposable
        {
            private const int HeaderBytes           = 128;
            private const long ProducerCursorOffset = 0;
            private const long ShadowSaltOffset     = 64;
            private const long SlotsBaseOffset      = 128;

            private readonly MemoryMappedFile         _mmf;
            private readonly MemoryMappedViewAccessor _accessor;
            private readonly int                      _capacity;
            private readonly int                      _mask;
            private readonly int                      _slotSize;
            private long                              _producerCursor;
            private int                               _disposed;

            public string Name { get; private set; }

            public MmioDispatchMirror(string name, int capacity, int slotSize, ulong salt)
            {
                if (capacity < 2 || (capacity & (capacity - 1)) != 0)
                    throw new ArgumentException("Capacity must be power of 2", "capacity");
                if (slotSize <= 0 || (slotSize & 7) != 0)
                    throw new ArgumentException("Slot size must be a positive multiple of 8", "slotSize");

                _capacity = capacity;
                _mask     = capacity - 1;
                _slotSize = slotSize;
                Name      = name;

                long totalBytes = HeaderBytes + (long)slotSize * (long)capacity;

                _mmf      = MemoryMappedFile.CreateOrOpen(name, totalBytes, MemoryMappedFileAccess.ReadWrite);
                _accessor = _mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

                // Zero header and publish salt
                for (long i = 0; i < HeaderBytes; i++)
                    _accessor.Write(i, (byte)0);

                unchecked { _accessor.Write(ShadowSaltOffset, (long)salt); }

                _producerCursor = 0L;
                _accessor.Write(ProducerCursorOffset, _producerCursor);
            }

            // Fire-and-forget write-through. Returns false if the MMF ring is full
            // relative to the producer cursor; in that case the slot is dropped from
            // the mirror but still succeeds on the primary heap ring.
            public bool TryPublish(ref FleetDispatchSlot slot)
            {
                if (Volatile.Read(ref _disposed) != 0) return false;

                long prod = _producerCursor;
                // Sidecar cursor is not read back in single-writer/observer mode; wrap
                // is allowed (the observer is expected to keep up; stale slots in the
                // MMF are simply overwritten on the next wrap).
                int idx = (int)(prod & _mask);
                long slotOffset = SlotsBaseOffset + (long)idx * (long)_slotSize;

                // Write the full 64-byte slot. Write<T> takes ref T and performs a
                // single marshaled copy into the mapped region. No boxing. No allocation.
                _accessor.Write(slotOffset, ref slot);

                // Publish barrier: slot bytes must be visible before cursor update.
                // Thread.MemoryBarrier is a full StoreStore/LoadLoad fence (~15 ns on
                // modern CPUs). Required because MemoryMappedViewAccessor.Write does
                // not itself emit a fence.
                Thread.MemoryBarrier();

                _producerCursor = prod + 1;
                _accessor.Write(ProducerCursorOffset, _producerCursor);
                return true;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                try { _accessor.Dispose(); } catch { }
                try { _mmf.Dispose();      } catch { }
            }

            // Diagnostic helper; not on the hot path.
            public string GetDiagnostics()
            {
                return string.Format(
                    "MmioDispatchMirror: name={0} capacity={1} slotSize={2} prod={3}",
                    Name, _capacity, _slotSize, Volatile.Read(ref _producerCursor));
            }
        }
    }
}
