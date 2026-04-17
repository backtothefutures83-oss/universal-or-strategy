using System;
using System.Runtime.InteropServices;
using System.Threading;

public unsafe sealed class MpmcPipeline
{
    private const int CacheLineBytes = 64;
    private const int ParkThreshold = 16;
    private const int StealBurst = 4;

    private readonly int _laneCount;
    private readonly int _laneCapacity;
    private readonly int _mask;
    private readonly ArenaLayout _layout;

    private long _headersRaw;
    private long _cachesRaw;
    private long _slotsRaw;
    private IntPtr _arenaHandle;

    public MpmcPipeline(int laneCount, int laneCapacity)
    {
        if (laneCount <= 0)
            throw new ArgumentOutOfRangeException("laneCount");

        if (laneCapacity < 2 || (laneCapacity & (laneCapacity - 1)) != 0)
            throw new ArgumentException("laneCapacity must be a power of two.", "laneCapacity");

        _laneCount = laneCount;
        _laneCapacity = laneCapacity;
        _mask = laneCapacity - 1;
        _layout = BuildArenaLayout(laneCount, laneCapacity);

        int allocationBytes = _layout.TotalBytes + CacheLineBytes - 1;
        _arenaHandle = Marshal.AllocHGlobal(allocationBytes);

        byte* raw = (byte*)_arenaHandle;
        byte* aligned = (byte*)AlignUp((long)raw, CacheLineBytes);

        _headersRaw = (long)aligned;
        _cachesRaw = (long)(aligned + _layout.HeaderBytes);
        _slotsRaw = (long)(aligned + _layout.HeaderBytes + _layout.CacheBytes);

        InitializeArena();
    }

    ~MpmcPipeline()
    {
        if (_arenaHandle != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_arenaHandle);
            _arenaHandle = IntPtr.Zero;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TrySend(int laneId, double item)
    {
        int normalizedLaneId = NormalizeLane(laneId);
        LaneHeader* header = GetHeader(normalizedLaneId);

        long readSeqPublished = Volatile.Read(ref header->ReadSeqPublished);
        long writeSeq = header->WriteSeq;

        if (writeSeq - readSeqPublished >= _laneCapacity)
            return false;

        int slotIndex = (int)(writeSeq & _mask);
        long ticket = writeSeq + 1;
        Slot* slot = GetSlot(normalizedLaneId, slotIndex);

        slot->Item = item;
        slot->Shadow = ((ulong)ticket) ^ header->LaneSalt;
        Volatile.Write(ref slot->Stamp, ticket);

        header->WriteSeq = ticket;
        Volatile.Write(ref header->PublishedWrite, ticket);
        header->HintXor = ComposeHint(header->LaneSalt, ticket, readSeqPublished);

        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryReceive(int laneId, out double item)
    {
        int normalizedLaneId = NormalizeLane(laneId);
        ConsumerCache* cache = GetCache(normalizedLaneId);

        if (TryPopCached(cache, out item))
            return true;

        LaneHeader* homeHeader = GetHeader(normalizedLaneId);
        long ownerToken = OwnerToken(normalizedLaneId);

        if (Volatile.Read(ref homeHeader->DrainOwner) == ownerToken)
        {
            if (TryReceiveOwned(normalizedLaneId, homeHeader, out item))
                return true;
        }
        else
        {
            if (TryReacquireHomeLane(normalizedLaneId, homeHeader, ownerToken))
            {
                if (TryReceiveOwned(normalizedLaneId, homeHeader, out item))
                    return true;
            }
        }

        if (TrySteal(normalizedLaneId, cache, homeHeader))
            return TryPopCached(cache, out item);

        item = default(double);
        return false;
    }

    private void InitializeArena()
    {
        for (int laneId = 0; laneId < _laneCount; laneId++)
        {
            LaneHeader* header = GetHeader(laneId);
            ConsumerCache* cache = GetCache(laneId);
            ulong laneSalt = CreateLaneSalt(laneId);

            header->WriteSeq = 0;
            header->PublishedWrite = 0;
            header->ReadSeqPublished = 0;
            header->DrainOwner = OwnerToken(laneId);
            header->ParkMisses = 0;
            header->LastVictim = -1;
            header->LaneSalt = laneSalt;
            header->HintXor = laneSalt;

            cache->Head = 0;
            cache->Tail = 0;

            for (int slotIndex = 0; slotIndex < _laneCapacity; slotIndex++)
            {
                Slot* slot = GetSlot(laneId, slotIndex);
                slot->Item = 0.0d;
                slot->Stamp = 0;
                slot->Shadow = 0;
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryReceiveOwned(int laneId, LaneHeader* header, out double item)
    {
        long readSeqPublished = header->ReadSeqPublished;
        long publishedWrite = Volatile.Read(ref header->PublishedWrite);

        if (publishedWrite == readSeqPublished)
        {
            int misses = header->ParkMisses + 1;
            header->ParkMisses = misses;

            if (misses >= ParkThreshold)
                Volatile.Write(ref header->DrainOwner, 0);

            item = default(double);
            return false;
        }

        long expectedTicket = readSeqPublished + 1;
        Slot* slot = GetSlot(laneId, (int)(readSeqPublished & _mask));

        if (Volatile.Read(ref slot->Stamp) != expectedTicket)
        {
            item = default(double);
            return false;
        }

        if (slot->Shadow != (((ulong)expectedTicket) ^ header->LaneSalt))
        {
            item = default(double);
            return false;
        }

        item = slot->Item;
        Volatile.Write(ref header->ReadSeqPublished, expectedTicket);
        header->HintXor = ComposeHint(header->LaneSalt, publishedWrite, expectedTicket);
        header->ParkMisses = 0;
        return true;
    }

    private bool TryReacquireHomeLane(int laneId, LaneHeader* header, long ownerToken)
    {
        if (Volatile.Read(ref header->DrainOwner) != 0)
            return false;

        long readSeqPublished = Volatile.Read(ref header->ReadSeqPublished);
        long publishedWrite = Volatile.Read(ref header->PublishedWrite);

        if (publishedWrite == readSeqPublished)
            return false;

        if (Interlocked.CompareExchange(ref header->DrainOwner, ownerToken, 0) != 0)
            return false;

        header->ParkMisses = 0;
        return true;
    }

    private bool TrySteal(int thiefLaneId, ConsumerCache* cache, LaneHeader* thiefHeader)
    {
        int preferredVictim = thiefHeader->LastVictim;

        if (IsStealCandidate(thiefLaneId, preferredVictim))
        {
            if (TryStealFromVictim(thiefLaneId, preferredVictim, cache, thiefHeader))
                return true;
        }

        for (int offset = 1; offset < _laneCount; offset++)
        {
            int candidateLaneId = thiefLaneId + offset;
            if (candidateLaneId >= _laneCount)
                candidateLaneId -= _laneCount;

            if (candidateLaneId == preferredVictim)
                continue;

            if (!IsStealCandidate(thiefLaneId, candidateLaneId))
                continue;

            if (TryStealFromVictim(thiefLaneId, candidateLaneId, cache, thiefHeader))
                return true;
        }

        return false;
    }

    private bool TryStealFromVictim(int thiefLaneId, int victimLaneId, ConsumerCache* cache, LaneHeader* thiefHeader)
    {
        LaneHeader* victimHeader = GetHeader(victimLaneId);

        if (Volatile.Read(ref victimHeader->DrainOwner) != 0)
            return false;

        if (victimHeader->HintXor == victimHeader->LaneSalt)
            return false;

        long thiefToken = OwnerToken(thiefLaneId);
        if (Interlocked.CompareExchange(ref victimHeader->DrainOwner, thiefToken, 0) != 0)
            return false;

        try
        {
            long readSeqPublished = Volatile.Read(ref victimHeader->ReadSeqPublished);
            long publishedWrite = Volatile.Read(ref victimHeader->PublishedWrite);

            if (publishedWrite == readSeqPublished)
                return false;

            int drainedCount = 0;

            while (drainedCount < StealBurst && readSeqPublished != publishedWrite)
            {
                long expectedTicket = readSeqPublished + 1;
                Slot* slot = GetSlot(victimLaneId, (int)(readSeqPublished & _mask));

                if (Volatile.Read(ref slot->Stamp) != expectedTicket)
                    break;

                if (slot->Shadow != (((ulong)expectedTicket) ^ victimHeader->LaneSalt))
                    break;

                SetCacheItem(cache, drainedCount, slot->Item);
                drainedCount++;
                readSeqPublished = expectedTicket;
            }

            if (drainedCount == 0)
                return false;

            cache->Head = 0;
            cache->Tail = drainedCount;

            Volatile.Write(ref victimHeader->ReadSeqPublished, readSeqPublished);
            victimHeader->HintXor = ComposeHint(victimHeader->LaneSalt, publishedWrite, readSeqPublished);
            thiefHeader->LastVictim = victimLaneId;
            return true;
        }
        finally
        {
            Volatile.Write(ref victimHeader->DrainOwner, 0);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryPopCached(ConsumerCache* cache, out double item)
    {
        int head = cache->Head;
        int tail = cache->Tail;

        if (head == tail)
        {
            item = default(double);
            return false;
        }

        item = GetCacheItem(cache, head);
        head++;

        if (head >= tail)
        {
            cache->Head = 0;
            cache->Tail = 0;
        }
        else
        {
            cache->Head = head;
        }

        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double GetCacheItem(ConsumerCache* cache, int index)
    {
        switch (index)
        {
            case 0:
                return cache->Item0;
            case 1:
                return cache->Item1;
            case 2:
                return cache->Item2;
            default:
                return cache->Item3;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SetCacheItem(ConsumerCache* cache, int index, double value)
    {
        switch (index)
        {
            case 0:
                cache->Item0 = value;
                return;
            case 1:
                cache->Item1 = value;
                return;
            case 2:
                cache->Item2 = value;
                return;
            default:
                cache->Item3 = value;
                return;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool IsStealCandidate(int thiefLaneId, int victimLaneId)
    {
        return victimLaneId >= 0 && victimLaneId < _laneCount && victimLaneId != thiefLaneId;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private LaneHeader* GetHeader(int laneId)
    {
        return ((LaneHeader*)_headersRaw) + laneId;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private ConsumerCache* GetCache(int laneId)
    {
        return ((ConsumerCache*)_cachesRaw) + laneId;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Slot* GetSlot(int laneId, int slotIndex)
    {
        return ((Slot*)_slotsRaw) + (laneId * _laneCapacity) + slotIndex;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long OwnerToken(int laneId)
    {
        return laneId + 1L;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ulong CreateLaneSalt(int laneId)
    {
        return 0x9E3779B97F4A7C15UL ^ ((ulong)(uint)(laneId + 1) * 0xBF58476D1CE4E5B9UL);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ulong ComposeHint(ulong laneSalt, long publishedWrite, long readSeqPublished)
    {
        return laneSalt ^ (ulong)publishedWrite ^ (ulong)readSeqPublished;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private int NormalizeLane(int laneId)
    {
        if ((uint)laneId >= (uint)_laneCount)
            throw new ArgumentOutOfRangeException("laneId");

        return laneId;
    }

    private static ArenaLayout BuildArenaLayout(int laneCount, int laneCapacity)
    {
        ArenaLayout layout = new ArenaLayout();
        layout.HeaderBytes = AlignUp(sizeof(LaneHeader) * laneCount, CacheLineBytes);
        layout.CacheBytes = AlignUp(sizeof(ConsumerCache) * laneCount, CacheLineBytes);
        layout.SlotBytes = AlignUp(sizeof(Slot) * laneCount * laneCapacity, CacheLineBytes);
        layout.TotalBytes = layout.HeaderBytes + layout.CacheBytes + layout.SlotBytes;
        return layout;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int AlignUp(int value, int alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long AlignUp(long value, int alignment)
    {
        return (value + alignment - 1) & ~((long)alignment - 1L);
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct LaneHeader
    {
        [FieldOffset(0)] public long WriteSeq;
        [FieldOffset(8)] public long PublishedWrite;
        [FieldOffset(16)] public long ReadSeqPublished;
        [FieldOffset(24)] public long DrainOwner;
        [FieldOffset(32)] public int ParkMisses;
        [FieldOffset(36)] public int LastVictim;
        [FieldOffset(40)] public ulong LaneSalt;
        [FieldOffset(48)] public ulong HintXor;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct ConsumerCache
    {
        [FieldOffset(0)] public double Item0;
        [FieldOffset(8)] public double Item1;
        [FieldOffset(16)] public double Item2;
        [FieldOffset(24)] public double Item3;
        [FieldOffset(32)] public int Head;
        [FieldOffset(36)] public int Tail;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct Slot
    {
        [FieldOffset(0)] public double Item;
        [FieldOffset(8)] public long Stamp;
        [FieldOffset(16)] public ulong Shadow;
    }

    private struct ArenaLayout
    {
        public int HeaderBytes;
        public int CacheBytes;
        public int SlotBytes;
        public int TotalBytes;
    }
}
