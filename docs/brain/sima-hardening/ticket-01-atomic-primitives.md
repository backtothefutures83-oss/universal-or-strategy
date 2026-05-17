# Ticket 01: Atomic Primitives

**Epic**: SIMA Subgraph Hardening  
**Phase**: Foundation (Week 1)  
**Estimated Effort**: 3 hours  
**Risk Level**: HIGH (foundation changes, blocks all other tickets)

---

## Objective

Create lock-free atomic primitives for FSM state management and OrderId mapping to eliminate torn reads and allocation overhead.

---

## Scope

### IN SCOPE
- Create `FsmPackedState` struct with `Pack`/`Unpack` methods for 64-bit atomic state
- Modify `FollowerBracketFSM` to use `_packedState` field instead of separate State/Generation
- Create `ZeroAllocOrderIdMap` class with `TryAdd`/`TryGet`/`Remove` methods
- Add `FsmGeneration` field to `FleetDispatchSideband` struct
- Add `FnvHash64` helper method for zero-allocation string hashing

### OUT OF SCOPE
- Pre-submit registration logic (Ticket 02)
- Callback routing updates (Ticket 05)
- Circuit breaker implementation (Ticket 04)

---

## Context References

**Analysis**: [`docs/brain/sima-hardening/01-analysis.md`](./01-analysis.md)
- Section 4.1 (P0 Critical Hotspots): H1 - FSM state torn reads
- Section 7.1 (Bug Registry Mapping): Compound Trap #1 (BUG-019)

**Approach**: [`docs/brain/sima-hardening/02-approach.md`](./02-approach.md)
- Section 1.1 (lines 25-111): Atomic FSM State with 64-bit packing
- Section 1.3 (lines 238-389): Zero-Allocation Hash Map implementation

---

## Implementation Instructions

### Step 1: Create FsmPackedState Struct

Add to `V12_002.cs` (near other FSM-related code):

```csharp
// V12 Phase 8: Atomic FSM State (64-bit packing)
// Layout: [State: 8 bits][Pending: 1 bit][Generation: 55 bits]
private struct FsmPackedState
{
    private const int StateShift = 56;
    private const int PendingShift = 55;
    private const long PendingMask = 1L << PendingShift;
    private const long GenerationMask = (1L << 55) - 1;

    public static long Pack(byte state, bool pending, long generation)
    {
        var gen = generation & GenerationMask;
        var pend = pending ? PendingMask : 0;
        return ((long)state << StateShift) | pend | gen;
    }

    public static void Unpack(long value, out byte state, out bool pending, out long generation)
    {
        state = (byte)(value >> StateShift);
        pending = (value & PendingMask) != 0;
        generation = value & GenerationMask;
    }
}
```

**Reference**: Approach doc section 1.1, lines 32-54

**Wrap Safety**: 55 bits = 36,028,797,018,963,968 values. At 1M ops/sec, wrap occurs after **347 years**.

### Step 2: Modify FollowerBracketFSM Class

Locate `FollowerBracketFSM` class in `V12_002.cs`.

**REPLACE** the existing `State` and `Generation` fields with:

```csharp
private long _packedState;  // Atomic state + pending + generation
```

**ADD** property accessors:

```csharp
public FollowerBracketState State
{
    get
    {
        FsmPackedState.Unpack(Interlocked.Read(ref _packedState), 
            out byte state, out _, out _);
        return (FollowerBracketState)state;
    }
}

public long Generation
{
    get
    {
        FsmPackedState.Unpack(Interlocked.Read(ref _packedState), 
            out _, out _, out long gen);
        return gen;
    }
}
```

**Reference**: Approach doc section 1.1, lines 56-79

### Step 3: Create ZeroAllocOrderIdMap Class

Add to `V12_002.cs` (near other data structures):

```csharp
// V12 Phase 8: Zero-Allocation OrderId -> FSM Map
private struct OrderIdMapEntry
{
    public long OrderIdHash;   // FNV-1a 64-bit hash (0 = empty)
    public int FsmKeyIndex;    // Index into _fsmKeyPool
    public long Generation;    // FSM generation at registration
}

private sealed class ZeroAllocOrderIdMap
{
    private readonly OrderIdMapEntry[] _table;
    private readonly string[] _fsmKeyPool;  // Pre-allocated FSM key strings
    private readonly int _mask;
    private int _fsmKeyPoolIndex;
    
    public ZeroAllocOrderIdMap(int capacity)
    {
        if ((capacity & (capacity - 1)) != 0)
            throw new ArgumentException("Capacity must be power of 2");
        
        _table = new OrderIdMapEntry[capacity];
        _fsmKeyPool = new string[capacity];
        _mask = capacity - 1;
        _fsmKeyPoolIndex = 0;
    }
    
    public bool TryAdd(string orderId, string fsmKey, long generation)
    {
        long hash = FnvHash64(orderId);
        if (hash == 0) return false;  // Invalid hash
        
        int idx = (int)(hash & _mask);
        int probeCount = 0;
        
        while (probeCount < _table.Length)
        {
            long currentHash = Volatile.Read(ref _table[idx].OrderIdHash);
            
            if (currentHash == 0)  // Empty slot
            {
                // Claim FSM key pool slot
                int keyIdx = Interlocked.Increment(ref _fsmKeyPoolIndex) - 1;
                if (keyIdx >= _fsmKeyPool.Length)
                {
                    Interlocked.Decrement(ref _fsmKeyPoolIndex);
                    return false;  // Pool exhausted
                }
                
                _fsmKeyPool[keyIdx] = fsmKey;
                
                // Publish entry atomically
                var entry = new OrderIdMapEntry
                {
                    OrderIdHash = hash,
                    FsmKeyIndex = keyIdx,
                    Generation = generation
                };
                
                // CAS on OrderIdHash field (acts as lock)
                if (Interlocked.CompareExchange(ref _table[idx].OrderIdHash, hash, 0) == 0)
                {
                    _table[idx].FsmKeyIndex = entry.FsmKeyIndex;
                    _table[idx].Generation = entry.Generation;
                    return true;
                }
            }
            
            idx = (idx + 1) & _mask;  // Linear probe
            probeCount++;
        }
        
        return false;  // Table full
    }
    
    public bool TryGet(string orderId, out string fsmKey, out long generation)
    {
        long hash = FnvHash64(orderId);
        int idx = (int)(hash & _mask);
        int probeCount = 0;
        
        while (probeCount < _table.Length)
        {
            long currentHash = Volatile.Read(ref _table[idx].OrderIdHash);
            
            if (currentHash == 0)
            {
                fsmKey = null;
                generation = 0;
                return false;  // Not found
            }
            
            if (currentHash == hash)
            {
                int keyIdx = _table[idx].FsmKeyIndex;
                fsmKey = _fsmKeyPool[keyIdx];
                generation = _table[idx].Generation;
                return true;
            }
            
            idx = (idx + 1) & _mask;
            probeCount++;
        }
        
        fsmKey = null;
        generation = 0;
        return false;
    }
    
    public void Remove(string orderId)
    {
        long hash = FnvHash64(orderId);
        int idx = (int)(hash & _mask);
        int probeCount = 0;
        
        while (probeCount < _table.Length)
        {
            long currentHash = Volatile.Read(ref _table[idx].OrderIdHash);
            
            if (currentHash == hash)
            {
                // Zero out entry (atomic write)
                Interlocked.Exchange(ref _table[idx].OrderIdHash, 0);
                return;
            }
            
            if (currentHash == 0) return;  // Not found
            
            idx = (idx + 1) & _mask;
            probeCount++;
        }
    }
    
    // FNV-1a 64-bit hash (zero-allocation)
    private static long FnvHash64(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;
        
        const long FnvPrime = 0x100000001b3;
        const long FnvOffsetBasis = unchecked((long)0xcbf29ce484222325);
        
        long hash = FnvOffsetBasis;
        for (int i = 0; i < str.Length; i++)
        {
            hash ^= str[i];
            hash *= FnvPrime;
        }
        
        return hash == 0 ? 1 : hash;  // Avoid 0 (reserved for empty)
    }
}
```

**Reference**: Approach doc section 1.3, lines 244-376

**Sizing**: 64 slots × 12 accounts × 7 orders = 5,376 entries. Use 8,192 (2^13) for 65% load factor.

**ADD** field declaration in strategy class:

```csharp
private ZeroAllocOrderIdMap _orderIdToFsmMap = new ZeroAllocOrderIdMap(8192);
```

### Step 4: Add FsmGeneration to FleetDispatchSideband

Locate `FleetDispatchSideband` struct in `V12_002.cs`.

**ADD** field:

```csharp
public long FsmGeneration;  // Matches FSM generation at enqueue time
```

**Reference**: Approach doc section 1.2, lines 119-126

---

## V12 DNA Guardrails

### Zero-Lock Compliance
- ✅ Uses `Interlocked.CompareExchange` and `Volatile.Read` for atomic operations
- ✅ `FsmPackedState` uses bit-packing for single-word atomicity
- ❌ NO `lock()` statements permitted

### Zero-Allocation Compliance
- ✅ `FsmPackedState` is a struct (stack-allocated)
- ✅ `ZeroAllocOrderIdMap` uses fixed-size arrays (no GC pressure)
- ✅ `FnvHash64` operates on string chars directly (no substring allocation)
- ❌ NO `new` keyword in hot path after initialization

### ASCII-Only Compliance
- ✅ All string literals use ASCII characters only
- ❌ NO Unicode, emoji, or curly quotes

---

## Post-Edit Verification

```powershell
powershell -File .\deploy-sync.ps1
python scripts/complexity_audit.py
grep -r "lock(" src/
grep -Prn "[^\x00-\x7F]" src/
```

---

## Acceptance Criteria

### Functional
- [ ] `FsmPackedState` struct created with `Pack`/`Unpack` methods
- [ ] `FollowerBracketFSM._packedState` field replaces separate State/Generation
- [ ] `ZeroAllocOrderIdMap` class created with all three methods
- [ ] `FleetDispatchSideband.FsmGeneration` field added
- [ ] `FnvHash64` helper method implemented

### Compilation
- [ ] Code compiles without errors in NinjaTrader IDE (F5)
- [ ] BUILD_TAG banner displays correctly

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" src/` returns ZERO matches

### Performance
- [ ] `FsmPackedState.Pack`/`Unpack` are inline-eligible (< 32 bytes IL)
- [ ] `ZeroAllocOrderIdMap` initialization completes in < 1ms

---

## Dependencies

**Blocks**: Ticket 02, Ticket 05  
**Blocked By**: None (foundation ticket)

---

## Notes

- **Generation Wrap Safety**: 55-bit counter wraps after 347 years at 1M ops/sec
- **Hash Collision Handling**: Linear probing with 65% load factor (industry standard)
- **Memory Footprint**: 8,192 entries × 24 bytes = 196 KB (fixed, no GC)
- **Threading Model**: `_packedState` is thread-safe; all other FSM fields are strategy-thread-only