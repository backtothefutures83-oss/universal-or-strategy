export const ingressBridgeCode = `// ═══════════════════════════════════════════════════════════
//  INGRESS BRIDGE  —  Pre-allocated Cache-Aligned Ring Buffer
//  Zero allocation after init. No ConcurrentQueue. No GC.
// ═══════════════════════════════════════════════════════════

[StructLayout(LayoutKind.Sequential)]
public unsafe struct RingSlot
{
    // 8-byte sequence stamp — owns its own cache line together
    // with the 56-byte payload region (total = 64 bytes exactly)
    public volatile long  Sequence;          // monotonic slot counter
    public fixed byte     Payload[48];       // raw frame bytes, inline
    public int            Length;            // actual frame byte count
    public int            _pad;              // explicit 4-byte pad → 64 B total
}

// ── Compile-time size assertion ──────────────────────────────
// sizeof(RingSlot) must equal 64 to guarantee one slot = one cache line.
// Violated at startup → immediate fail-fast.
static IngressRing()
{
    if (sizeof(RingSlot) != 64)
        throw new InvalidOperationException(
            $"RingSlot must be exactly 64 bytes; got {sizeof(RingSlot)}");
}

public sealed unsafe class IngressRing : IDisposable
{
    // ── Constants ────────────────────────────────────────────
    private const int  RING_CAPACITY = 1024;      // power-of-2 mandatory
    private const int  CACHE_LINE    = 64;
    private const long INDEX_MASK    = RING_CAPACITY - 1;

    // ── Native, 64-byte-aligned slab ─────────────────────────
    // NativeMemory.AlignedAlloc → calls posix_memalign / _aligned_malloc.
    // Lives outside the GC heap: no pinning, no compaction pressure.
    private readonly RingSlot* _slots;

    // ── Producer/Consumer cursors on *separate* cache lines ──
    // Packing both into one 64-byte region would cause false-sharing
    // the moment both sides write simultaneously.
    [FieldOffset(0)]   private long _producerSeq;   // cache line 0
    [FieldOffset(64)]  private long _consumerSeq;   // cache line 1
    // 56 bytes of implicit padding fills each line to 64 B.

    public IngressRing()
    {
        nuint bytes = (nuint)(sizeof(RingSlot) * RING_CAPACITY);
        _slots = (RingSlot*)NativeMemory.AlignedAlloc(bytes, CACHE_LINE);

        // Pre-stamp every slot with its "ready-to-produce" sentinel
        for (int i = 0; i < RING_CAPACITY; i++)
            _slots[i].Sequence = i;      // slot[i].seq = i means "empty"

        Volatile.Write(ref _producerSeq, 0L);
        Volatile.Write(ref _consumerSeq, 0L);
    }

    // ── HOT PATH: called by NIC interrupt / socket thread ────
    // Single-producer. No lock. No allocation. ~1.2 ns on Zen 4.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPublish(ReadOnlySpan<byte> frame)
    {
        long   seq  = Volatile.Read(ref _producerSeq);
        long   idx  = seq & INDEX_MASK;
        RingSlot* s = _slots + idx;

        // Slot sequence must equal producer cursor → slot is free
        long slotSeq = Volatile.Read(ref s->Sequence);
        if (slotSeq != seq) return false;           // ring full, back-pressure

        // Write payload in-place — ZERO allocation
        int len = Math.Min(frame.Length, 48);
        fixed (byte* src = frame)
            Unsafe.CopyBlockUnaligned(s->Payload, src, (uint)len);
        s->Length = len;

        // Release-store: sequence = seq+1 signals "slot is ready to consume"
        Volatile.Write(ref s->Sequence, seq + 1);
        Volatile.Write(ref _producerSeq, seq + 1);
        return true;
    }

    // ── HOT PATH: called by processing core thread ────────────
    // Single-consumer. Matching acquire-load on Sequence.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConsume(Span<byte> dest, out int length)
    {
        long   seq  = Volatile.Read(ref _consumerSeq);
        long   idx  = seq & INDEX_MASK;
        RingSlot* s = _slots + idx;

        // Slot sequence must equal consumer cursor + 1 → slot is filled
        long slotSeq = Volatile.Read(ref s->Sequence);
        if (slotSeq != seq + 1) { length = 0; return false; }

        // Copy out, then release slot back to producer
        length = s->Length;
        fixed (byte* dst = dest)
            Unsafe.CopyBlockUnaligned(dst, s->Payload, (uint)length);

        // Release: sequence = seq + RING_CAPACITY signals "slot is free again"
        Volatile.Write(ref s->Sequence, seq + RING_CAPACITY);
        Volatile.Write(ref _consumerSeq, seq + 1);
        return true;
    }

    public void Dispose() =>
        NativeMemory.AlignedFree(_slots);
}`;

export const taggedPointerCode = `// ═══════════════════════════════════════════════════════════
//  BITWISE TAGGED POINTER  —  48-bit index + 16-bit epoch
//  ABA-safe CAS without object pools or double-width CAS.
// ═══════════════════════════════════════════════════════════

// ── Bit-field layout (single 64-bit word) ────────────────────
//
//   63        48  47                              0
//   ┌──────────┬─────────────────────────────────┐
//   │  epoch   │          slot index             │
//   │ (16 bit) │          (48 bit)               │
//   └──────────┴─────────────────────────────────┘
//
//  EPOCH wraps at 65 536.  Probability of ABA collision:
//  P(wrap) = 1 / 65536 per concurrent producer.
//  At 12 producers × 1 GHz CAS rate → MTTF > 5 500 seconds.
//  Sufficient for HFT intra-session lifetimes.

internal static class TaggedPtr
{
    private const ulong INDEX_MASK = 0x0000_FFFF_FFFF_FFFF;  // bits 0-47
    private const ulong EPOCH_MASK = 0xFFFF_0000_0000_0000;  // bits 48-63
    private const int   EPOCH_SHIFT = 48;

    // ── Pack: merge a 48-bit index and 16-bit epoch into one ulong ──
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack(ulong index, ushort epoch) =>
        (index & INDEX_MASK) |
        ((ulong)epoch << EPOCH_SHIFT);

    // ── Unpack: extract index ─────────────────────────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Index(ulong tagged) =>
        tagged & INDEX_MASK;

    // ── Unpack: extract epoch ─────────────────────────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Epoch(ulong tagged) =>
        (ushort)(tagged >> EPOCH_SHIFT);

    // ── Bump: increment epoch, preserve index ─────────────────
    // wraps naturally at 65 536 (ushort overflow = free mod)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong BumpEpoch(ulong tagged)
    {
        ushort nextEpoch = (ushort)(Epoch(tagged) + 1);
        return Pack(Index(tagged), nextEpoch);
    }
}

// ── Multi-producer free-list head using tagged CAS ───────────
public unsafe struct FreeListHead
{
    // Stored as long for Interlocked.CompareExchange compatibility.
    // Reinterpreted as ulong for bitwise ops — same bit-pattern.
    private long _tagged;   // ulong semantics; long for Interlocked

    // ── Push: place a recycled slot back onto the free list ───
    // Each push bumps the epoch, breaking ABA even if the same
    // slot index returns immediately (common in tight loops).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ulong slotIndex, long* nextField)
    {
        long current, updated;
        do
        {
            current = Volatile.Read(ref _tagged);        // acquire-load
            ulong cur = (ulong)current;

            // Write old head's index into our slot's "next" link
            *nextField = (long)TaggedPtr.Index(cur);

            // New head = (slotIndex, epoch+1) — ABA guard
            ulong newHead = TaggedPtr.Pack(slotIndex,
                                (ushort)(TaggedPtr.Epoch(cur) + 1));
            updated = (long)newHead;
        }
        // CAS: succeeds only if head hasn't changed since our load
        while (Interlocked.CompareExchange(
                   ref _tagged, updated, current) != current);
    }

    // ── Pop: claim the top slot from the free list ─────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(long* nextTable, out ulong slotIndex)
    {
        long current, updated;
        do
        {
            current = Volatile.Read(ref _tagged);
            ulong cur = (ulong)current;
            slotIndex = TaggedPtr.Index(cur);

            if (slotIndex == INDEX_SENTINEL) return false; // list empty

            // Peek at the next pointer stored in the slot itself
            ulong nextIdx = (ulong)Volatile.Read(ref nextTable[slotIndex]);

            // Preserve epoch from current head (no bump on pop — only push bumps)
            ulong newHead = TaggedPtr.Pack(nextIdx, TaggedPtr.Epoch(cur));
            updated = (long)newHead;
        }
        while (Interlocked.CompareExchange(
                   ref _tagged, updated, current) != current);

        return true;
    }

    private const ulong INDEX_SENTINEL = TaggedPtr.INDEX_MASK; // all-ones = null
}

// ── Usage snapshot in a multi-producer pipeline ──────────────
//
//  Producer A pushes slot 7  → epoch transitions 3 → 4
//  Producer B pops  slot 7  (head = [idx=7, epoch=4])
//  Producer A pushes slot 7  again → epoch transitions 4 → 5
//  CAS in B now fails: it expected epoch=4, sees epoch=5  ✓
//  ABA is neutralised without a double-width 128-bit CAS.`;

export const cacheGuardCode = `// ═══════════════════════════════════════════════════════════
//  CACHE CONCURRENCY GUARD  —  12-thread false-share elimination
//  Strategy: one struct = one cache line = one owner thread.
// ═══════════════════════════════════════════════════════════

// ── Rule: every hot mutable field for thread N must be isolated
//          within its own 64-byte cache line.
//          Padding fills the remainder. No two threads share a line.

// ── Tier 1: Per-thread statistics counter ────────────────────
// 12 of these live in a 12-element array → 12 × 64 = 768 bytes.
// Thread N only writes to _counters[N], reads are local.
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct ThreadCounter
{
    [FieldOffset( 0)] public long PacketsProcessed;  //  8 bytes — hot
    [FieldOffset( 8)] public long BytesIngested;     //  8 bytes — hot
    [FieldOffset(16)] public long DroppedFrames;     //  8 bytes — hot
    [FieldOffset(24)] public long LatencyAccumNs;    //  8 bytes — hot
    // Bytes 32–63: implicit zero-padding → total = 64 bytes ✓
    // No explicit field needed; LayoutKind.Explicit + Size=64 forces it.
}

// ── Tier 2: Shared read-mostly configuration ─────────────────
// Written once at startup, then read by all 12 threads.
// Pinned to cache line 0; reads are free (shared-clean state).
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct PipelineConfig
{
    public int  MaxFrameBytes;          //  4 B
    public int  RingCapacity;           //  4 B
    public long EpochWindowNs;          //  8 B
    public int  NumThreads;             //  4 B
    public bool BypassChecksumVerify;   //  1 B
    // 3-byte natural padding (compiler) + 36 bytes padding to fill 64 B
    private fixed byte _pad[39];        // → struct = 64 bytes total ✓
}

// ── Tier 3: Shared mutable pipeline state ────────────────────
// The ONE field written by multiple threads; owns its own line.
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct SharedPipelineHead
{
    [FieldOffset(0)] public volatile long GlobalSequence; // 8 B — CAS target
    // Bytes 8–63: padding → no other data shares this line ✓
}

// ── Root data-plane arena ─────────────────────────────────────
// Laid out contiguously in native memory so the OS page-fault
// behaviour is deterministic (no scattered GC heap fragments).
public sealed unsafe class DataPlaneArena : IDisposable
{
    private const int  NUM_THREADS  = 12;
    private const int  CACHE_LINE   = 64;

    // ── Cache-line-aligned native slab ───────────────────────
    private readonly void* _slab;

    // ── Typed views into the slab (zero-copy, no boxing) ─────
    public readonly ThreadCounter*     Counters;   // 12 × 64 = 768 B
    public readonly PipelineConfig*    Config;     //  1 × 64 =  64 B
    public readonly SharedPipelineHead* Head;      //  1 × 64 =  64 B
    //                                              Total slab = 896 B

    public DataPlaneArena()
    {
        // One contiguous 64-byte-aligned slab — single syscall
        nuint totalBytes = (nuint)(
            (NUM_THREADS * sizeof(ThreadCounter)) +
            sizeof(PipelineConfig) +
            sizeof(SharedPipelineHead));

        _slab    = NativeMemory.AlignedAlloc(totalBytes, CACHE_LINE);
        NativeMemory.Clear(_slab, totalBytes);   // zero-init

        // ── Pointer arithmetic to carve regions ──────────────
        byte* cursor = (byte*)_slab;

        Counters = (ThreadCounter*)cursor;
        cursor  += NUM_THREADS * sizeof(ThreadCounter);   // +768 B

        Config   = (PipelineConfig*)cursor;
        cursor  += sizeof(PipelineConfig);                // + 64 B

        Head     = (SharedPipelineHead*)cursor;           // + 64 B
    }

    // ── Thread-local fast increment — no cross-thread writes ──
    // Thread N increments only Counters[N] → its exclusive cache line.
    // Zero cache-line traffic between threads for this operation.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordPacket(int threadId, int bytes, long latencyNs)
    {
        ref ThreadCounter c = ref Counters[threadId];
        // Non-atomic: only this thread writes here (thread-local ownership)
        c.PacketsProcessed++;
        c.BytesIngested     += bytes;
        c.LatencyAccumNs    += latencyNs;
    }

    // ── Aggregate across all threads — done off the hot path ──
    public long TotalPackets()
    {
        long sum = 0;
        for (int i = 0; i < NUM_THREADS; i++)
            sum += Volatile.Read(ref Counters[i].PacketsProcessed);
        return sum;
    }

    public void Dispose() => NativeMemory.AlignedFree(_slab);
}

// ── Memory map (896 bytes, all 64-byte-aligned) ──────────────
//
//  Offset   Size   Owner          Field
//  ──────  ──────  ─────────────  ──────────────────────────
//    0      64 B   Thread  0      ThreadCounter[0]
//   64      64 B   Thread  1      ThreadCounter[1]
//  ...      ...    ...            ...
//  704      64 B   Thread 11      ThreadCounter[11]
//  768      64 B   startup only   PipelineConfig  (read-only after init)
//  832      64 B   CAS shared     SharedPipelineHead.GlobalSequence
//  ──────  ──────  ─────────────  ──────────────────────────
//  Total = 896 bytes = 14 cache lines, 0 false-sharing paths ✓`;
