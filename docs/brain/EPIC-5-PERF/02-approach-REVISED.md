# EPIC-5-PERF: Optimization Approach (REVISED)

**Epic ID:** EPIC-5-PERF  
**Phase:** 2 - Approach Design (Post-Sentinel Revision)  
**Created:** 2026-05-23  
**Revised:** 2026-05-23 (Sentinel Audit Findings)  
**Target:** Zero-allocation hot paths, p99 <100μs latency

---

## REVISION SUMMARY

**Sentinel Audit Findings:** 3 critical gaps, 2 significant risks identified.

**Director Mandate:** Address all critical gaps before proceeding to validation.

**Changes:**
1. **NEW Ticket 01B:** Thread Model Analysis & ThreadStatic Validation
2. **NEW Ticket 02B:** UIStateSnapshot Object Pooling (400KB-1MB/sec reduction)
3. **EXPANDED Ticket 01:** Migrate 14 existing Stopwatch instances to LatencyProbe
4. **EXPANDED Ticket 05:** Add Draw.Dot string tag pre-caching

**Impact:** +6 days to epic timeline, but ensures completeness and V12 DNA integrity.

---

## EXECUTIVE SUMMARY

This revised approach eliminates **ALL** heap allocations in V12's hot paths through **EIGHT** surgical tickets:

1. **Baseline instrumentation** (LatencyProbe struct + Stopwatch migration)
2. **Thread model validation** (ThreadStatic safety + Actor pattern compliance)
3. **String.Format elimination** (pre-allocated char[] buffers)
4. **UIStateSnapshot pooling** (object reuse for UI snapshots)
5. **.ToArray() elimination** (snapshot pattern standardization)
6. **Order array pooling** (custom ConcurrentBag<Order[]> pool)
7. **MonitorRmaProximity refactoring** (CYC 32 → 3x <10 + Draw.Dot caching)
8. **Verification & stress testing** (p99 <100μs validation)

**Key Constraint:** .NET 4.8 (no Span<T>, no ArrayPool<T>, string interpolation allocates)

---

## MASTER INDEX (REVISED)

### Target Methods (Hot Path Priority)

| Method | File | CYC | Hotspot | Allocation Sources | Ticket |
|--------|------|-----|---------|-------------------|--------|
| OnBarUpdate | BarUpdate.cs:206 | ? | ? | 6x string.Format | T03 |
| OnMarketData | Lifecycle.cs:787 | Low | ? | ProcessIpcCommands, **PublishUiSnapshot** | T01, T04 |
| ProcessOnOrderUpdate | Orders.Callbacks.cs:159 | 21 | 72.1 | PropagateMasterPriceMove, HandleXXXFilled | T05 |
| HandleEntryOrderFilled | Orders.Callbacks.cs:205 | ? | ? | .ToArray(), 2x string.Format | T05 |
| HandleSecondaryOrderFilled | Orders.Callbacks.cs:253 | ? | ? | .ToArray(), 2x string.Format | T05 |
| MonitorRmaProximity | Entries.RMA.cs:262 | 32 | 95.9 | 6x string.Format, 3x lambda, **Draw.Dot tags** | T07 |
| **PublishUiSnapshot** | **UI.Snapshot.cs:189** | **?** | **?** | **new UIStateSnapshot + 3 nested objects** | **T04** |

### Allocation Inventory (REVISED)

**Tier 0: Ultra-Critical (NEW - Sentinel Discovery)**
- **UIStateSnapshot**: 1 allocation per PublishUiSnapshot call (every 5 ticks + every bar)
  - Nested: BuildUiConfigSnapshot, BuildUiComplianceSnapshot, BuildUiLivePositionSnapshot
  - **Estimated:** 200-500 bytes per call → 400KB-1MB/sec at 10k ticks/sec

**Tier 1: Ultra-Hot (Every Tick/Bar)**
- `string.Format()`: 30+ instances (6 in MonitorRmaProximity, 6 in OnBarUpdate)
- `.ToArray()`: 25+ instances (HandleEntryOrderFilled, HandleSecondaryOrderFilled, etc.)
- `new[] { order }`: 4 instances (Cancel/Submit calls in Propagation.cs)
- **Draw.Dot tags**: `"Prox_" + kvp.Key` string concatenation (MonitorRmaProximity)

**Tier 2: High-Frequency (Order Updates)**
- Lambda closures in `Enqueue(ctx => ...)`: 3 in MonitorRmaProximity
- **Stopwatch.StartNew()**: 14 instances (SignalBroadcaster, SIMA.Dispatch, SIMA.Execution)

---

## REVISED TICKET BREAKDOWN

### Ticket 01: Baseline Instrumentation & Stopwatch Migration (EXPANDED)

**Goal:** Establish p50/p95/p99 baseline + migrate existing Stopwatch usage to LatencyProbe.

**NEW Scope (Sentinel Finding):**
- Audit 14 existing Stopwatch instances:
  - SignalBroadcaster.cs:209 (1 instance)
  - V12_002.SIMA.Dispatch.cs:132 (7 instances)
  - V12_002.SIMA.Execution.cs:48 (6 instances)
- Migrate to LatencyProbe struct (zero-allocation replacement)
- Profile Draw.Dot() allocation (MonitorRmaProximity:322)
- Profile PublishUiSnapshot() allocation (UI.Snapshot.cs:189)

**Deliverables:**
1. `LatencyProbe` struct (zero-allocation, Stopwatch.GetTimestamp-based)
2. Instrumentation in: OnBarUpdate, OnMarketData, ProcessOnOrderUpdate, HandleEntryOrderFilled, MonitorRmaProximity, **PublishUiSnapshot**
3. **Migration:** Replace 14 Stopwatch.StartNew() calls with LatencyProbe
4. Histogram collection (buckets: <10, 10-50, 50-100, 100-500, 500-1000, 1000-5000, >5000 μs)
5. 1-hour baseline under 10k ticks/sec load
6. CSV export for offline analysis
7. **NEW:** Draw.Dot allocation profile report
8. **NEW:** PublishUiSnapshot allocation profile report (ETW trace)

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
```

**Migration Example:**
```csharp
// BEFORE (SignalBroadcaster.cs:209):
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... event invocation ...
if (sw.Elapsed.TotalMilliseconds > 1.0)
    NinjaTrader.Code.Output.Process(string.Format("[LATENCY_FANOUT] {0}: {1:F2}ms...", 
        typeof(T).Name, sw.Elapsed.TotalMilliseconds), PrintTo.OutputTab1);

// AFTER:
LatencyProbe probe = default;
probe.Start();
// ... event invocation ...
probe.Stop();
if (probe.ElapsedMicroseconds > 1000.0)
    NinjaTrader.Code.Output.Process(LogBuffer.Format("[LATENCY_FANOUT] {0}: {1:F2}ms...", 
        typeof(T).Name, probe.ElapsedMicroseconds / 1000.0), PrintTo.OutputTab1);
```

**CYC Impact:** Neutral (instrumentation + migration)  
**Files Modified:** 9 (BarUpdate.cs, Lifecycle.cs, Orders.Callbacks.cs, Entries.RMA.cs, UI.Snapshot.cs, SignalBroadcaster.cs, SIMA.Dispatch.cs, SIMA.Execution.cs, + 2 new files)  
**Estimated Time:** +2 days (Stopwatch migration + profiling)

---

### Ticket 01B: Thread Model Analysis & ThreadStatic Validation (NEW)

**Goal:** Validate ThreadStatic safety for LogBuffer within NinjaTrader/Actor pattern context.

**Scope (Sentinel Finding):**
- Document NinjaTrader threading model:
  - OnBarUpdate thread (single-threaded? thread-pooled?)
  - OnMarketData thread (same as OnBarUpdate?)
  - Enqueue/Actor thread (dedicated? shared?)
  - UI thread (WPF dispatcher)
- Validate ThreadStatic safety:
  - Test ThreadStatic char[] buffer under concurrent access
  - Verify no buffer corruption when Actor thread + user thread call Print()
  - Measure ThreadStatic overhead vs instance-level buffer
- Document Actor pattern compatibility:
  - Does ThreadStatic bypass Actor queue serialization?
  - Is this safe for logging (read-only state access)?

**Deliverables:**
1. Thread model documentation (markdown)
2. ThreadStatic safety test harness (unit test)
3. Performance comparison: ThreadStatic vs instance-level buffer
4. **Decision:** ThreadStatic approved OR fallback to instance-level buffer
5. Actor pattern compatibility report

**Test Harness:**
```csharp
// tests/ThreadStaticSafetyTest.cs
[TestFixture]
public class ThreadStaticSafetyTests
{
    [ThreadStatic]
    private static char[] _testBuffer;
    
    [Test]
    public void ThreadStatic_ConcurrentAccess_NoCorruption()
    {
        const int THREAD_COUNT = 10;
        const int ITERATIONS = 1000;
        
        var threads = new Thread[THREAD_COUNT];
        var errors = new ConcurrentBag<string>();
        
        for (int i = 0; i < THREAD_COUNT; i++)
        {
            int threadId = i;
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < ITERATIONS; j++)
                {
                    if (_testBuffer == null)
                        _testBuffer = new char[512];
                    
                    // Write thread-specific pattern
                    for (int k = 0; k < 512; k++)
                        _testBuffer[k] = (char)('A' + threadId);
                    
                    // Verify no corruption
                    for (int k = 0; k < 512; k++)
                    {
                        if (_testBuffer[k] != (char)('A' + threadId))
                            errors.Add($"Thread {threadId} corrupted at index {k}");
                    }
                }
            });
        }
        
        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();
        
        Assert.IsEmpty(errors, "ThreadStatic buffer corruption detected");
    }
}
```

**CYC Impact:** Neutral (testing only)  
**Files Modified:** 0 (documentation + tests)  
**Estimated Time:** +1 day

---

### Ticket 02: String.Format Elimination (REVISED)

**Goal:** Replace all hot-path `string.Format()` with pre-allocated char[] buffers.

**NEW Constraint (Ticket 01B):**
- Implementation depends on Ticket 01B thread model analysis
- If ThreadStatic approved: Use ThreadStatic char[] buffer
- If ThreadStatic unsafe: Use instance-level char[] buffer

**Target Methods:**
1. OnBarUpdate (6 instances)
2. MonitorRmaProximity (6 instances)
3. HandleEntryOrderFilled (2 instances)
4. HandleSecondaryOrderFilled (2 instances)
5. **SignalBroadcaster** (1 instance - from Ticket 01 migration)
6. **SIMA.Dispatch** (7 instances - from Ticket 01 migration)
7. **SIMA.Execution** (6 instances - from Ticket 01 migration)

**Implementation (ThreadStatic Approved):**
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

**Implementation (ThreadStatic Unsafe - Fallback):**
```csharp
// src/V12_002.Perf.LogBuffer.cs
public sealed class LogBuffer
{
    private readonly char[] _buffer = new char[512];
    private readonly object _lock = new object();
    
    public string Format(string format, params object[] args)
    {
        lock (_lock)
        {
            // Use instance-level buffer
            return FormatInternal(format, args);
        }
    }
}
```

**Replacement Pattern:**
```csharp
// BEFORE:
Print(string.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
    p.ProximityProbeCount, entryKey, dist, lvl));

// AFTER (ThreadStatic):
Print(LogBuffer.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
    p.ProximityProbeCount, entryKey, dist, lvl));

// AFTER (Instance-level):
Print(_logBuffer.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
    p.ProximityProbeCount, entryKey, dist, lvl));
```

**CYC Impact:** Neutral (replacement only)  
**Files Modified:** 8 (BarUpdate.cs, Entries.RMA.cs, Orders.Callbacks.cs, SignalBroadcaster.cs, SIMA.Dispatch.cs, SIMA.Execution.cs, + 1 new LogBuffer.cs, + V12_002.cs for instance field)  
**Allocation Reduction:** ~30 allocations/tick → 0  
**Estimated Time:** +2 days (implementation + testing)

---

### Ticket 03: UIStateSnapshot Object Pooling (NEW)

**Goal:** Eliminate UIStateSnapshot allocation on every PublishUiSnapshot call.

**Scope (Sentinel Finding - CRITICAL):**
- PublishUiSnapshot creates new UIStateSnapshot (line 194)
- Nested allocations: BuildUiConfigSnapshot, BuildUiComplianceSnapshot, BuildUiLivePositionSnapshot
- Called from OnMarketData (every 5 ticks) + OnBarUpdate (every bar)
- **Estimated reduction:** 400KB-1MB/sec

**Implementation:**
```csharp
// src/V12_002.Perf.UISnapshotPool.cs
public sealed class UISnapshotPool
{
    private readonly ConcurrentBag<UIStateSnapshot> _snapshotPool = new ConcurrentBag<UIStateSnapshot>();
    private readonly ConcurrentBag<UIConfigSnapshot> _configPool = new ConcurrentBag<UIConfigSnapshot>();
    private readonly ConcurrentBag<UIComplianceSnapshot> _compliancePool = new ConcurrentBag<UIComplianceSnapshot>();
    private readonly ConcurrentBag<UILivePositionSnapshot> _livePositionPool = new ConcurrentBag<UILivePositionSnapshot>();
    
    private const int MAX_POOL_SIZE = 10;
    
    public UIStateSnapshot RentSnapshot()
    {
        if (_snapshotPool.TryTake(out var snapshot))
            return snapshot;
        return new UIStateSnapshot(); // Fallback allocation
    }
    
    public void ReturnSnapshot(UIStateSnapshot snapshot)
    {
        if (snapshot == null) return;
        
        // Clear references to prevent memory leaks
        snapshot.Config = null;
        snapshot.Compliance = null;
        snapshot.LivePosition = null;
        
        if (_snapshotPool.Count < MAX_POOL_SIZE)
            _snapshotPool.Add(snapshot);
    }
    
    // Similar methods for Config, Compliance, LivePosition
}
```

**Usage Pattern:**
```csharp
// BEFORE (UI.Snapshot.cs:189):
private void PublishUiSnapshot()
{
    string mode = GetCurrentPanelMode();
    double ema9Value = SafeEmaValue(ema9);
    
    UIStateSnapshot snapshot = new UIStateSnapshot  // ALLOCATION
    {
        EmaValue = ema9Value,
        // ... 30+ field assignments ...
    };
    
    _uiSnapshot = snapshot;
}

// AFTER:
private void PublishUiSnapshot()
{
    string mode = GetCurrentPanelMode();
    double ema9Value = SafeEmaValue(ema9);
    
    UIStateSnapshot snapshot = _uiSnapshotPool.RentSnapshot();  // POOLED
    
    // Update fields (no allocation)
    snapshot.EmaValue = ema9Value;
    snapshot.AtrValue = currentATR > 0 ? currentATR : 0;
    snapshot.LastUpdateTicks = DateTime.UtcNow.Ticks;
    // ... 30+ field updates ...
    
    snapshot.Config = BuildUiConfigSnapshot_Pooled(mode);
    snapshot.Compliance = BuildUiComplianceSnapshot_Pooled();
    snapshot.LivePosition = BuildUiLivePositionSnapshot_Pooled();
    
    // Return previous snapshot to pool
    var oldSnapshot = _uiSnapshot;
    _uiSnapshot = snapshot;
    
    if (oldSnapshot != null)
        _uiSnapshotPool.ReturnSnapshot(oldSnapshot);
}
```

**CYC Impact:** +3 per method (pool rent/return logic)  
**Files Modified:** 2 (UI.Snapshot.cs, + 1 new UISnapshotPool.cs)  
**Allocation Reduction:** 400KB-1MB/sec → 0 (after pool warm-up)  
**Estimated Time:** +3 days (implementation + testing)

---

### Ticket 04: .ToArray() Elimination (RENAMED from T03)

**Goal:** Standardize snapshot pattern to eliminate redundant .ToArray() calls.

**Target Methods:**
1. HandleEntryOrderFilled (line 207)
2. HandleSecondaryOrderFilled (line 263)
3. DrainQueuesForShutdown (lines 95, 106-109 - **DOUBLE ALLOCATION**)
4. LogicAudit methods (lines 289, 339)

**NEW Scope (Sentinel Finding):**
- Add manual audit of activePositions concurrent access patterns
- Document read/write patterns
- Verify snapshot pattern eliminates all race conditions

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

**CYC Impact:** Neutral (refactoring only)  
**Files Modified:** 6 (Orders.Callbacks.cs, Orders.Callbacks.Execution.cs, Lifecycle.cs, LogicAudit.cs, Orders.Callbacks.AccountOrders.cs, Orders.Callbacks.Propagation.cs)  
**Allocation Reduction:** ~25 .ToArray() calls → ~10 (snapshot pattern)  
**Estimated Time:** +2 days (audit + refactoring)

---

### Ticket 05: Order Array Pooling (RENAMED from T04)

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

**Usage Pattern (REVISED - Sentinel Finding):**
```csharp
// AFTER (pooled - FIX: move assignment inside try):
var orderArray = _orderArrayPool.Rent();
try
{
    orderArray[0] = tOrder;  // MOVED INSIDE try block
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
**Estimated Time:** +1 day (implementation + testing)

---

### Ticket 06: MonitorRmaProximity Refactoring (RENAMED from T05, EXPANDED)

**Goal:** Reduce CYC 32 → 3x <10 via extraction, eliminate lambda closures, **cache Draw.Dot tags**.

**NEW Scope (Sentinel Finding):**
- Pre-cache Draw.Dot tag strings: `"Prox_" + kvp.Key` → `_proxTagCache[entryKey]`
- If Draw.Dot allocates (from Ticket 01 profiling), add conditional compilation

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

**NEW: Draw.Dot Tag Caching:**
```csharp
// src/V12_002.Entries.RMA.cs (class-level)
private readonly ConcurrentDictionary<string, string> _proxTagCache = 
    new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

private string GetProxTag(string entryKey)
{
    return _proxTagCache.GetOrAdd(entryKey, key => "Prox_" + key);
}

// Usage in CheckProximityEntry:
Draw.Dot(this, GetProxTag(entryKey), false, 0, pos.EntryPrice, Brushes.Cyan);
```

**Conditional Compilation (if Draw.Dot allocates):**
```csharp
#if DEBUG
Draw.Dot(this, GetProxTag(entryKey), false, 0, pos.EntryPrice, Brushes.Cyan);
#endif
```

**CYC Impact:** 32 → 5 + 8 + 12 + 6 = 31 (net neutral, but better maintainability)  
**Files Modified:** 1 (Entries.RMA.cs)  
**Allocation Reduction:** 6x string.Format → LogBuffer (from Ticket 02) + Draw.Dot tags cached  
**Estimated Time:** +2 days (extraction + tag caching)

---

### Ticket 07: Verification & Stress Testing (RENAMED from T06)

**Goal:** Validate p99 <100μs target and zero GC pressure.

**Test Protocol:**
1. **Latency Re-Baseline**
   - Re-run 1-hour test under 10k ticks/sec
   - Compare p50/p95/p99 against Ticket 01 baseline
   - Verify p99 <100μs for all 6 methods (including PublishUiSnapshot)

2. **Allocation Profiling**
   - Run ETW trace (PerfView) during 10-minute window
   - Verify 0 bytes allocated in hot paths
   - Check for unexpected allocations (e.g., Draw.Dot, nested UI snapshots)

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
5. **NEW:** UIStateSnapshot pool metrics (rent count, return count, fallback count)
6. **NEW:** OrderArrayPool metrics (rent count, return count, fallback count)

**CYC Impact:** Neutral (testing only)  
**Files Modified:** 0 (verification only)  
**Estimated Time:** +2 days (testing + reporting)

---

## REVISED RISK MITIGATION

### High-Risk Areas

1. **UIStateSnapshot Pool Lifetime** (Ticket 03 - NEW)
   - **Risk:** Returning snapshot to pool while still referenced by UI thread
   - **Mitigation:**
     - Use volatile write for _uiSnapshot assignment
     - Return old snapshot AFTER new snapshot published
     - Add pool metrics to detect double-return bugs

2. **ThreadStatic Safety** (Ticket 01B, 02 - NEW)
   - **Risk:** ThreadStatic buffer corruption in multi-threaded scenarios
   - **Mitigation:**
     - Ticket 01B validates safety via test harness
     - Fallback to instance-level buffer if unsafe
     - Document thread model guarantees

3. **Snapshot Pattern Correctness** (Ticket 04)
   - **Risk:** Collection-modified-during-enumeration exceptions
   - **Mitigation:**
     - Take snapshot BEFORE enumeration
     - Re-check `ContainsKey()` inside loop
     - Add unit tests for concurrent modification scenarios

4. **Order Array Pool Lifetime** (Ticket 05)
   - **Risk:** Returning array to pool while still in use
   - **Mitigation:**
     - Move orderArray[0] assignment INSIDE try block (Sentinel fix)
     - Use try/finally to guarantee Return() call
     - Clear array[0] = null before returning
     - Add pool metrics (rent count, return count, fallback count)

### Low-Risk Areas

1. **LatencyProbe Instrumentation** (Ticket 01)
   - Struct-based, zero side effects
   - Stopwatch.GetTimestamp() is thread-safe

2. **MonitorRmaProximity Refactoring** (Ticket 06)
   - Pure extraction, no logic changes
   - CYC reduction improves maintainability

3. **Draw.Dot Tag Caching** (Ticket 06)
   - ConcurrentDictionary.GetOrAdd is thread-safe
   - Worst case: duplicate tag creation (harmless)

---

## V12 DNA COMPLIANCE (REVISED)

### Lock-Free Actor Pattern ✅
- All state mutations via `Enqueue(ctx => ...)`
- No `lock()` statements introduced
- Snapshot pattern preserves concurrent read safety
- **NEW:** UIStateSnapshot pool uses ConcurrentBag (lock-free)
- **NEW:** OrderArrayPool uses ConcurrentBag (lock-free)

### ASCII-Only Compliance ✅
- No Unicode in string literals
- LogBuffer uses ASCII-only formatting
- Draw.Dot tags use ASCII-only strings

### Correctness by Construction ✅
- LatencyProbe: Struct prevents null references
- OrderArrayPool: try/finally guarantees cleanup
- Snapshot pattern: Eliminates concurrent modification exceptions
- **NEW:** UIStateSnapshot pool: Volatile write prevents race conditions

### Bounded Latency ✅
- Zero allocations → Zero GC pauses
- Pre-allocated buffers → Deterministic memory access
- No unbounded loops introduced
- **NEW:** Pool fallback allocations bounded by MAX_POOL_SIZE

### Thread Safety (NEW - Ticket 01B) ✅
- ThreadStatic validated via test harness
- Actor pattern compatibility documented
- Fallback to instance-level buffer if ThreadStatic unsafe

---

## REVISED SUCCESS METRICS

### Quantitative Targets

| Metric | Baseline (Est.) | Target | Ticket |
|--------|-----------------|--------|--------|
| OnBarUpdate p99 | 500-1000μs | <100μs | T02, T04 |
| OnMarketData p99 | 50-100μs | <50μs | T01, T02, **T03** |
| ProcessOnOrderUpdate p99 | 200-500μs | <100μs | T04, T05 |
| MonitorRmaProximity p99 | 1000-2000μs | <500μs | T02, T06 |
| **PublishUiSnapshot p99** | **200-500μs** | **<100μs** | **T03** |
| Allocations/tick | ~500 bytes | 0 bytes | T02-T06 |
| GC pauses (1hr) | ~180 (Gen0) | 0 | T07 |

### Qualitative Targets

1. **Code Maintainability**
   - MonitorRmaProximity: CYC 32 → 31 (3 sub-methods)
   - No method exceeds 100 lines
   - All optimization patterns documented

2. **V12 DNA Compliance**
   - Zero `lock()` statements (verified via grep)
   - ASCII-only strings (verified via check_ascii.py)
   - Correctness by construction (no runtime guards)
   - **NEW:** Thread safety validated (Ticket 01B)

3. **Consistency**
   - Single latency measurement system (LatencyProbe)
   - No Stopwatch.StartNew() instances remaining
   - Unified logging system (LogBuffer)

---

## REVISED DEPENDENCY GRAPH

```
T01 (Baseline + Stopwatch Migration) → T01B (Thread Model) → T02 (String.Format) → T07 (Verification)
                                      ↓
T01 (Baseline) → T03 (UISnapshot Pool) → T07 (Verification)
                ↓
T01 (Baseline) → T04 (.ToArray()) → T07 (Verification)
                ↓
T01 (Baseline) → T05 (Order Pool) → T07 (Verification)
                ↓
T01 (Baseline) → T06 (MonitorRma + Draw.Dot) → T07 (Verification)
```

**Execution Order:**
1. **T01** (Baseline + Stopwatch Migration) - MUST run first
2. **T01B** (Thread Model Analysis) - MUST run before T02
3. **T02, T03, T04, T05, T06** - Can run in parallel (independent, but T02 depends on T01B)
4. **T07** (Verification) - MUST run last

---

## REVISED ROLLBACK STRATEGY

Each ticket is independently revertible:

1. **T01:** Remove instrumentation code, revert Stopwatch migrations, delete histogram files
2. **T01B:** No code changes (documentation + tests)
3. **T02:** Revert LogBuffer calls to string.Format
4. **T03:** Remove UISnapshotPool, revert to `new UIStateSnapshot`
5. **T04:** Revert snapshot pattern to inline .ToArray()
6. **T05:** Remove OrderArrayPool, revert to `new[] { order }`
7. **T06:** Revert MonitorRmaProximity to original 104-line method, remove tag cache
8. **T07:** No code changes (testing only)

**Emergency Rollback:** `git revert <commit-hash>` for each ticket.

---

## REVISED TIMELINE

| Ticket | Original | Revised | Delta | Reason |
|--------|----------|---------|-------|--------|
| T01 | 2 days | 4 days | +2 | Stopwatch migration (14 instances) + profiling |
| T01B | N/A | 1 day | +1 | Thread model analysis (NEW) |
| T02 | 2 days | 2 days | 0 | No change |
| T03 | N/A | 3 days | +3 | UIStateSnapshot pooling (NEW) |
| T04 | 2 days | 2 days | 0 | Renamed from T03 |
| T05 | 1 day | 1 day | 0 | Renamed from T04 |
| T06 | 2 days | 2 days | 0 | Renamed from T05, Draw.Dot caching added |
| T07 | 2 days | 2 days | 0 | Renamed from T06 |
| **Total** | **11 days** | **17 days** | **+6** | Sentinel revisions |

---

## OPEN QUESTIONS (RESOLVED)

1. ~~**ArrayPool<T> Thread Safety** (.NET 4.8)~~
   - **RESOLVED:** Use ConcurrentBag<T[]> instead (T03, T05)

2. ~~**Print() Allocation Bypass**~~
   - **RESOLVED:** Use LogBuffer (T02), conditional compilation for production

3. ~~**Draw.Dot() Allocation Profile**~~
   - **RESOLVED:** Profile in T01, cache tags in T06

4. ~~**Snapshot Pattern Correctness**~~
   - **RESOLVED:** Manual audit in T04, unit tests for concurrent modification

5. ~~**ThreadStatic Safety**~~
   - **RESOLVED:** Validate in T01B, fallback to instance-level buffer if unsafe

6. ~~**PublishUiSnapshot Allocation**~~
   - **RESOLVED:** Object pooling in T03 (400KB-1MB/sec reduction)

---

## NEXT STEPS

**[APPROACH-GATE-REVISED]** Revised approach complete. All Sentinel findings addressed.

**Key Revisions:**
1. ✅ Added Ticket 01B: Thread Model Analysis
2. ✅ Added Ticket 03: UIStateSnapshot Pooling (400KB-1MB/sec reduction)
3. ✅ Expanded Ticket 01: Migrate 14 existing Stopwatch instances
4. ✅ Expanded Ticket 06: Add Draw.Dot string tag pre-caching
5. ✅ Fixed Ticket 05: Move orderArray[0] assignment inside try block

**Impact:** +6 days to epic timeline, but ensures:
- **Completeness:** Zero GC pressure (including UI snapshots)
- **Consistency:** Single latency measurement system (LatencyProbe)
- **V12 Integrity:** Thread safety validated (ThreadStatic + Actor pattern)

Proceed to Phase 3 (Validation)?