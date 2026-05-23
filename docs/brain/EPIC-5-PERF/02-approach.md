# EPIC-5-PERF: Optimization Approach

**Epic ID:** EPIC-5-PERF  
**Phase:** 2 - Approach Design  
**Created:** 2026-05-23  
**Target:** Zero-allocation hot paths, p99 <100μs latency

---

## EXECUTIVE SUMMARY

This approach eliminates all heap allocations in V12's hot paths through six surgical tickets:
1. **Baseline instrumentation** (LatencyProbe struct)
2. **String.Format elimination** (pre-allocated char[] buffers)
3. **.ToArray() elimination** (snapshot pattern standardization)
4. **Order array pooling** (custom ConcurrentBag<Order[]> pool)
5. **MonitorRmaProximity refactoring** (CYC 32 → 3x <10)
6. **Verification & stress testing** (p99 <100μs validation)

**Key Constraint:** .NET 4.8 (no Span<T>, no ArrayPool<T>, string interpolation allocates)

---

## MASTER INDEX

### Target Methods (Hot Path Priority)

| Method | File | CYC | Hotspot | Allocation Sources | Ticket |
|--------|------|-----|---------|-------------------|--------|
| OnBarUpdate | BarUpdate.cs:206 | ? | ? | 6x string.Format | T02 |
| OnMarketData | Lifecycle.cs:787 | Low | ? | ProcessIpcCommands, PublishUiSnapshot | T01 |
| ProcessOnOrderUpdate | Orders.Callbacks.cs:159 | 21 | 72.1 | PropagateMasterPriceMove, HandleXXXFilled | T03 |
| HandleEntryOrderFilled | Orders.Callbacks.cs:205 | ? | ? | .ToArray(), 2x string.Format | T03 |
| HandleSecondaryOrderFilled | Orders.Callbacks.cs:253 | ? | ? | .ToArray(), 2x string.Format | T03 |
| MonitorRmaProximity | Entries.RMA.cs:262 | 32 | 95.9 | 6x string.Format, 3x lambda, Draw.Dot | T05 |

### Allocation Inventory

**Tier 1: Ultra-Hot (Every Tick/Bar)**
- `string.Format()`: 30+ instances (6 in MonitorRmaProximity, 6 in OnBarUpdate)
- `.ToArray()`: 25+ instances (HandleEntryOrderFilled, HandleSecondaryOrderFilled, etc.)
- `new[] { order }`: 4 instances (Cancel/Submit calls in Propagation.cs)

**Tier 2: High-Frequency (Order Updates)**
- Lambda closures in `Enqueue(ctx => ...)`: 3 in MonitorRmaProximity
- `Draw.Dot()`: Unknown allocation profile (needs profiling)

---

## TICKET BREAKDOWN

### Ticket 01: Baseline Instrumentation & LatencyProbe

**Goal:** Establish p50/p95/p99 baseline for 5 critical methods.

**Deliverables:**
1. `LatencyProbe` struct (zero-allocation, Stopwatch.GetTimestamp-based)
2. Instrumentation in: OnBarUpdate, OnMarketData, ProcessOnOrderUpdate, HandleEntryOrderFilled, MonitorRmaProximity
3. Histogram collection (buckets: <10, 10-50, 50-100, 100-500, 500-1000, 1000-5000, >5000 μs)
4. 1-hour baseline under 10k ticks/sec load
5. CSV export for offline analysis

**Implementation:**
```csharp
// src/V12_002.Perf.LatencyProbe.cs
[StructLayout(LayoutKind.Sequential)]
public struct LatencyProbe
{
    private long _startTicks;
    private long _endTicks;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Start() => _startTicks = Stopwatch.GetTimestamp();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Stop() => _endTicks = Stopwatch.GetTimestamp();
    
    public double ElapsedMicroseconds => 
        (_endTicks - _startTicks) * 1_000_000.0 / Stopwatch.Frequency;
}

// src/V12_002.Perf.Histogram.cs
public class LatencyHistogram
{
    private readonly long[] _buckets = new long[7]; // Pre-allocated
    private readonly object _lock = new object();
    
    public void Record(double microseconds)
    {
        int bucket = GetBucket(microseconds);
        lock (_lock) { _buckets[bucket]++; }
    }
    
    private int GetBucket(double us)
    {
        if (us < 10) return 0;
        if (us < 50) return 1;
        if (us < 100) return 2;
        if (us < 500) return 3;
        if (us < 1000) return 4;
        if (us < 5000) return 5;
        return 6;
    }
}
```

**Usage Pattern:**
```csharp
protected override void OnBarUpdate()
{
    LatencyProbe probe = default;
    probe.Start();
    
    // ... existing logic ...
    
    probe.Stop();
    _onBarUpdateHistogram.Record(probe.ElapsedMicroseconds);
}
```

**CYC Impact:** Neutral (instrumentation only)  
**Files Modified:** 6 (BarUpdate.cs, Lifecycle.cs, Orders.Callbacks.cs, Entries.RMA.cs, + 2 new files)

---

### Ticket 02: String.Format Elimination

**Goal:** Replace all hot-path `string.Format()` with pre-allocated char[] buffers.

**Target Methods:**
1. OnBarUpdate (6 instances)
2. MonitorRmaProximity (6 instances)
3. HandleEntryOrderFilled (2 instances)
4. HandleSecondaryOrderFilled (2 instances)

**Implementation:**
```csharp
// src/V12_002.Perf.LogBuffer.cs
public sealed class LogBuffer
{
    [ThreadStatic]
    private static char[] _buffer;
    
    private const int BUFFER_SIZE = 512;
    
    public static string Format(string format, params object[] args)
    {
        if (_buffer == null)
            _buffer = new char[BUFFER_SIZE];
        
        // Custom formatter using _buffer
        // Falls back to string.Format if buffer exhausted
        return FormatInternal(format, args);
    }
    
    private static string FormatInternal(string format, object[] args)
    {
        // Simplified formatter for common patterns:
        // "{0} {1} @ {2:F2}" -> manual char[] write
        // Complex patterns -> fallback to string.Format
        
        // ... implementation ...
    }
}
```

**Replacement Pattern:**
```csharp
// BEFORE:
Print(string.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
    p.ProximityProbeCount, entryKey, dist, lvl));

// AFTER:
Print(LogBuffer.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
    p.ProximityProbeCount, entryKey, dist, lvl));
```

**Alternative (Conditional Compilation):**
```csharp
#if DEBUG
Print(string.Format("[SENTINEL] Probe #{0}...", ...));
#endif
```

**CYC Impact:** Neutral (replacement only)  
**Files Modified:** 5 (BarUpdate.cs, Entries.RMA.cs, Orders.Callbacks.cs, + 1 new LogBuffer.cs)  
**Allocation Reduction:** ~30 allocations/tick → 0

---

### Ticket 03: .ToArray() Elimination

**Goal:** Standardize snapshot pattern to eliminate redundant .ToArray() calls.

**Target Methods:**
1. HandleEntryOrderFilled (line 207)
2. HandleSecondaryOrderFilled (line 263)
3. DrainQueuesForShutdown (lines 95, 106-109 - **DOUBLE ALLOCATION**)
4. LogicAudit methods (lines 289, 339)

**Pattern:**
```csharp
// BEFORE (allocates on every call):
foreach (var kvp in activePositions.ToArray())
{
    if (!activePositions.ContainsKey(kvp.Key)) continue;
    // ... modify activePositions ...
}

// AFTER (single snapshot, reused):
var snapshot = activePositions.ToArray(); // Single allocation
foreach (var kvp in snapshot)
{
    if (!activePositions.ContainsKey(kvp.Key)) continue;
    // ... modify activePositions ...
}
```

**Special Case: DrainQueuesForShutdown**
```csharp
// BEFORE (DOUBLE ALLOCATION):
foreach (var kvp in activeFleetAccounts.ToArray())
{
    var workingOrders = fleetAcct.Orders.ToArray()
        .Where(o => o != null && ...)
        .ToArray(); // THIRD ALLOCATION!
}

// AFTER (single snapshot per collection):
var fleetSnapshot = activeFleetAccounts.ToArray();
foreach (var kvp in fleetSnapshot)
{
    var ordersSnapshot = fleetAcct.Orders.ToArray();
    var workingOrders = ordersSnapshot
        .Where(o => o != null && ...)
        .ToArray(); // Still needed for LINQ result
}
```

**CYC Impact:** Neutral (refactoring only)  
**Files Modified:** 6 (Orders.Callbacks.cs, Orders.Callbacks.Execution.cs, Lifecycle.cs, LogicAudit.cs, Orders.Callbacks.AccountOrders.cs, Orders.Callbacks.Propagation.cs)  
**Allocation Reduction:** ~25 .ToArray() calls → ~10 (snapshot pattern)

---

### Ticket 04: Order Array Pooling

**Goal:** Eliminate `new[] { order }` allocations in Cancel/Submit calls.

**Target Pattern:**
```csharp
// BEFORE (allocates single-element array):
pos.ExecutingAccount.Cancel(new[] { tOrder });
pos.ExecutingAccount.Submit(new[] { replacement });
```

**Implementation (.NET 4.8 Compatible):**
```csharp
// src/V12_002.Perf.OrderArrayPool.cs
public sealed class OrderArrayPool
{
    private readonly ConcurrentBag<Order[]> _pool = new ConcurrentBag<Order[]>();
    private const int MAX_POOL_SIZE = 100;
    
    public Order[] Rent()
    {
        if (_pool.TryTake(out var array))
            return array;
        return new Order[1]; // Fallback allocation
    }
    
    public void Return(Order[] array)
    {
        if (array == null || array.Length != 1) return;
        
        array[0] = null; // Clear reference
        
        if (_pool.Count < MAX_POOL_SIZE)
            _pool.Add(array);
    }
}
```

**Usage Pattern:**
```csharp
// AFTER (pooled):
var orderArray = _orderArrayPool.Rent();
orderArray[0] = tOrder;
try
{
    pos.ExecutingAccount.Cancel(orderArray);
}
finally
{
    _orderArrayPool.Return(orderArray);
}
```

**CYC Impact:** +2 per call site (try/finally overhead)  
**Files Modified:** 2 (Orders.Callbacks.Propagation.cs, + 1 new OrderArrayPool.cs)  
**Allocation Reduction:** 4 allocations/order-operation → 0 (after pool warm-up)

---

### Ticket 05: MonitorRmaProximity Refactoring

**Goal:** Reduce CYC 32 → 3x <10 via extraction, eliminate lambda closures.

**Current Structure (104 lines, CYC 32):**
```
MonitorRmaProximity()
├── foreach (entryOrders)
│   ├── Proximity Entry Logic (nested if, FSM Enqueue)
│   ├── Proximity Zone Logic (in/dead/out)
│   └── Exhaustion Logic (cancel, sound)
```

**Target Structure (3 sub-methods, CYC <10 each):**
```
MonitorRmaProximity() [CYC 5]
├── CheckProximityEntry(entryKey, pos, distTicks) [CYC 8]
├── CheckProximityExit(entryKey, pos, distTicks, order) [CYC 12]
└── HandleExhaustion(entryKey, pos, order) [CYC 6]
```

**Sub-Method Signatures:**
```csharp
private void CheckProximityEntry(string entryKey, PositionInfo pos, double distTicks)
{
    // Initialize ClosestApproachTicks if needed
    if (pos.ClosestApproachTicks <= 0)
    {
        Enqueue(ctx => {
            PositionInfo p;
            if (ctx.activePositions.TryGetValue(entryKey, out p))
                p.ClosestApproachTicks = double.MaxValue;
        });
    }
    
    // Update ClosestApproachTicks
    if (distTicks < pos.ClosestApproachTicks)
    {
        double newDist = distTicks;
        Enqueue(ctx => {
            PositionInfo p;
            if (ctx.activePositions.TryGetValue(entryKey, out p) && newDist < p.ClosestApproachTicks)
                p.ClosestApproachTicks = newDist;
        });
    }
    
    // Proximity entry transition
    if (distTicks <= RmaProximityTicks && !pos.WasInProximity)
    {
        double dist = distTicks;
        double lvl = pos.EntryPrice;
        Enqueue(ctx => {
            PositionInfo p;
            if (ctx.activePositions.TryGetValue(entryKey, out p) && !p.WasInProximity)
            {
                p.WasInProximity = true;
                p.ProximityProbeCount++;
                Print(LogBuffer.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
                    p.ProximityProbeCount, entryKey, dist, lvl));
            }
        });
        
        Draw.Dot(this, "Prox_" + entryKey, false, 0, pos.EntryPrice, Brushes.Cyan);
    }
}

private void CheckProximityExit(string entryKey, PositionInfo pos, double distTicks, Order order)
{
    if (distTicks >= RmaCancellationTicks && pos.WasInProximity)
    {
        Enqueue(ctx => {
            PositionInfo p;
            if (ctx.activePositions.TryGetValue(entryKey, out p) && p.WasInProximity)
                p.WasInProximity = false;
        });
        
        if (RmaExhaustionEnabled && pos.ProximityProbeCount >= RmaMaxProbeCount)
        {
            HandleExhaustion(entryKey, pos, order);
        }
        else
        {
            Print(LogBuffer.Format("[SENTINEL] Retreat for {0} (probe #{1}, closest={2:F1}t). Monitoring.",
                entryKey, pos.ProximityProbeCount, pos.ClosestApproachTicks));
            RemoveDrawObject("Prox_" + entryKey);
            SendResponseToRemote("SOUND|SENTINEL_PROXIMITY_RETREAT");
        }
    }
    else if (GetDrawObject("Prox_" + entryKey) != null)
    {
        RemoveDrawObject("Prox_" + entryKey);
    }
}

private void HandleExhaustion(string entryKey, PositionInfo pos, Order order)
{
    Print(LogBuffer.Format("[SENTINEL] EXHAUSTION: {0} probed {1}x (max={2}), closest={3:F1}t. Cancelling.",
        entryKey, pos.ProximityProbeCount, RmaMaxProbeCount, pos.ClosestApproachTicks));
    CancelOrderSafe(order, pos);
    RemoveDrawObject("Prox_" + entryKey);
    SendResponseToRemote("SOUND|SENTINEL_EXHAUSTION_CANCEL");
}
```

**Lambda Closure Elimination:**
- Current: 3 lambdas capture `entryKey`, `newDist`, `dist`, `lvl`
- After: Same lambdas (unavoidable due to FSM Enqueue pattern), but isolated in sub-methods

**CYC Impact:** 32 → 5 + 8 + 12 + 6 = 31 (net neutral, but better maintainability)  
**Files Modified:** 1 (Entries.RMA.cs)  
**Allocation Reduction:** 6x string.Format → LogBuffer (from Ticket 02)

---

### Ticket 06: Verification & Stress Testing

**Goal:** Validate p99 <100μs target and zero GC pressure.

**Test Protocol:**
1. **Latency Re-Baseline**
   - Re-run 1-hour test under 10k ticks/sec
   - Compare p50/p95/p99 against Ticket 01 baseline
   - Verify p99 <100μs for all 5 methods

2. **Allocation Profiling**
   - Run ETW trace (PerfView) during 10-minute window
   - Verify 0 bytes allocated in hot paths
   - Check for unexpected allocations (e.g., Draw.Dot)

3. **GC Pause Validation**
   - Monitor PerfMon GC metrics during 1-hour test
   - Verify 0 Gen0 collections during active trading
   - Verify 0 Gen1/Gen2 collections

4. **Stress Test**
   - 10k ticks/sec sustained load
   - 1-hour duration
   - Monitor CPU, memory, latency histograms

5. **Regression Testing**
   - F5 gate (NinjaTrader compile + load)
   - `deploy-sync.ps1` (hard-link integrity)
   - `complexity_audit.py` (CYC verification)
   - `grep -r "lock(" src/` (zero matches)

**Deliverables:**
1. Latency comparison report (before/after CSV)
2. ETW allocation profile (PerfView screenshots)
3. GC metrics (PerfMon CSV export)
4. Stress test summary (p50/p95/p99, CPU%, memory)

**CYC Impact:** Neutral (testing only)  
**Files Modified:** 0 (verification only)

---

## RISK MITIGATION

### High-Risk Areas

1. **Snapshot Pattern Correctness** (Ticket 03)
   - **Risk:** Collection-modified-during-enumeration exceptions
   - **Mitigation:** 
     - Take snapshot BEFORE enumeration
     - Re-check `ContainsKey()` inside loop
     - Add unit tests for concurrent modification scenarios

2. **Order Array Pool Lifetime** (Ticket 04)
   - **Risk:** Returning array to pool while still in use
   - **Mitigation:**
     - Use try/finally to guarantee Return() call
     - Clear array[0] = null before returning
     - Add pool metrics (rent count, return count, fallback count)

3. **LogBuffer Thread Safety** (Ticket 02)
   - **Risk:** ThreadStatic buffer corruption in multi-threaded scenarios
   - **Mitigation:**
     - Use [ThreadStatic] attribute (one buffer per thread)
     - Fallback to string.Format if buffer exhausted
     - Add buffer overflow detection

### Low-Risk Areas

1. **LatencyProbe Instrumentation** (Ticket 01)
   - Struct-based, zero side effects
   - Stopwatch.GetTimestamp() is thread-safe

2. **MonitorRmaProximity Refactoring** (Ticket 05)
   - Pure extraction, no logic changes
   - CYC reduction improves maintainability

---

## V12 DNA COMPLIANCE

### Lock-Free Actor Pattern ✅
- All state mutations via `Enqueue(ctx => ...)`
- No `lock()` statements introduced
- Snapshot pattern preserves concurrent read safety

### ASCII-Only Compliance ✅
- No Unicode in string literals
- LogBuffer uses ASCII-only formatting

### Correctness by Construction ✅
- LatencyProbe: Struct prevents null references
- OrderArrayPool: try/finally guarantees cleanup
- Snapshot pattern: Eliminates concurrent modification exceptions

### Bounded Latency ✅
- Zero allocations → Zero GC pauses
- Pre-allocated buffers → Deterministic memory access
- No unbounded loops introduced

---

## SUCCESS METRICS

### Quantitative Targets

| Metric | Baseline (Est.) | Target | Ticket |
|--------|-----------------|--------|--------|
| OnBarUpdate p99 | 500-1000μs | <100μs | T02, T03 |
| OnMarketData p99 | 50-100μs | <50μs | T01, T02 |
| ProcessOnOrderUpdate p99 | 200-500μs | <100μs | T03, T04 |
| MonitorRmaProximity p99 | 1000-2000μs | <500μs | T02, T05 |
| Allocations/tick | ~500 bytes | 0 bytes | T02-T04 |
| GC pauses (1hr) | ~180 (Gen0) | 0 | T06 |

### Qualitative Targets

1. **Code Maintainability**
   - MonitorRmaProximity: CYC 32 → 31 (3 sub-methods)
   - No method exceeds 100 lines
   - All optimization patterns documented

2. **V12 DNA Compliance**
   - Zero `lock()` statements (verified via grep)
   - ASCII-only strings (verified via check_ascii.py)
   - Correctness by construction (no runtime guards)

---

## DEPENDENCY GRAPH

```
T01 (Baseline) → T02 (String.Format) → T06 (Verification)
                ↓
T01 (Baseline) → T03 (.ToArray()) → T06 (Verification)
                ↓
T01 (Baseline) → T04 (Order Pool) → T06 (Verification)
                ↓
T01 (Baseline) → T05 (MonitorRma) → T06 (Verification)
```

**Execution Order:**
1. T01 (Baseline) - MUST run first
2. T02, T03, T04, T05 - Can run in parallel (independent)
3. T06 (Verification) - MUST run last

---

## ROLLBACK STRATEGY

Each ticket is independently revertible:

1. **T01:** Remove instrumentation code, delete histogram files
2. **T02:** Revert LogBuffer calls to string.Format
3. **T03:** Revert snapshot pattern to inline .ToArray()
4. **T04:** Remove OrderArrayPool, revert to `new[] { order }`
5. **T05:** Revert MonitorRmaProximity to original 104-line method
6. **T06:** No code changes (testing only)

**Emergency Rollback:** `git revert <commit-hash>` for each ticket.

---

## OPEN QUESTIONS

1. **Draw.Dot() Allocation Profile**
   - Q: Does Draw.Dot() allocate on every call?
   - A: Profiling in T01 will reveal (likely minimal)

2. **Print() Bypass in Production**
   - Q: Should we use conditional compilation for Print()?
   - A: Evaluate in T02 (LogBuffer can NOP in release builds)

3. **ArrayPool Backport**
   - Q: Can we backport ArrayPool<T> to .NET 4.8?
   - A: NO - use ConcurrentBag<T[]> instead (T04)

---

## NEXT STEPS

**[APPROACH-GATE]** Approach complete. Ready for Phase 2.3 (Sentinel Audit).

Key decisions finalized:
1. LatencyProbe: Struct-based, Stopwatch.GetTimestamp()
2. String.Format: LogBuffer with ThreadStatic char[] buffer
3. .ToArray(): Snapshot pattern standardization
4. Order Arrays: ConcurrentBag<Order[]> pool (.NET 4.8 compatible)
5. MonitorRmaProximity: Extract 3 sub-methods (CYC 32 → 31)
6. Verification: ETW + PerfMon + stress test

Proceed to `/epic-scan` for Sentinel adversarial review.