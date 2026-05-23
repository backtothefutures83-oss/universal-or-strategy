# EPIC-5-PERF: Technical Analysis

**Epic ID:** EPIC-5-PERF  
**Phase:** 2 - Analysis  
**Created:** 2026-05-23  
**Constraint:** .NET 4.8 (string interpolation allocates)

---

## ALLOCATION HOTSPOT ANALYSIS

### Critical Discovery: .ToArray() Epidemic

**25 instances found** across hot paths, creating massive allocation pressure:

#### Tier 1: Ultra-Hot (Called Every Tick/Bar)
1. **MonitorRmaProximity** (CYC 32, hotspot 95.9)
   - `foreach (var kvp in entryOrders)` - NO allocation (good)
   - BUT: 6x `string.Format()` calls inside proximity logic
   - Impact: Called on every bar when RMA positions active

2. **HandleEntryOrderFilled** (CYC unknown, 47 lines)
   - `activePositions.ToArray()` at line 207
   - Called on EVERY entry fill (critical path)
   - Allocates array + enumerator on heap

3. **HandleSecondaryOrderFilled** (CYC unknown, 55 lines)
   - `activePositions.ToArray()` at line 263
   - Called on EVERY target/stop fill
   - Allocates array + enumerator on heap

#### Tier 2: High-Frequency (Order Updates)
4. **ProcessAccountOrder_EnqueueTerminalUpdate** (V12_002.Orders.Callbacks.AccountOrders.cs)
   - `activePositions.ToArray()` at line 841 (snapshot pattern)
   - Comment: "eliminating the second activePositions.ToArray() allocation"
   - **GOOD PATTERN**: Single snapshot reused, but still allocates

5. **ExecuteFollowerCascadeCleanup**
   - Receives pre-computed snapshot (line 658 comment)
   - **BEST PRACTICE**: Avoids duplicate allocation

#### Tier 3: Moderate-Frequency (Lifecycle Events)
6. **DrainQueuesForShutdown** (Lifecycle.cs:95)
   - `activeFleetAccounts.ToArray()`
   - `fleetAcct.Orders.ToArray().Where(...).ToArray()` - **DOUBLE ALLOCATION**

7. **LogicAudit** methods (2 instances)
   - `activePositions.ToArray()` at lines 289, 339
   - Called during audit cycles

### String.Format() Allocation Map

**Total: 30+ instances** (from previous search)

**Hot Path Offenders:**
1. **MonitorRmaProximity** (6 instances)
   - Lines 296, 301, 318, 323 in proximity/exhaustion logic
   - Format: `string.Format("[SENTINEL] Probe #{0}...", ...)`

2. **HandleEntryOrderFilled** (2 instances)
   - Line 224: `string.Format("[PRICE_GUARD] CRITICAL: averageFillPrice=0...")`
   - Line 242: `string.Format("{0} ENTRY FILLED: {1} {2} @ {3:F2}")`

3. **HandleSecondaryOrderFilled** (2 instances)
   - Line 269: `string.Format("T{0} FILLED ({1}): {2} contracts @ {3:F2}...")`
   - Line 285: `string.Format("STOP FILLED: {0} contracts @ {1:F2}")`

4. **OnBarUpdate** (6 instances)
   - Lines 106, 126, 141, 163, 165 - session/OR logging

### Array Instantiation Patterns

**new[] { order }** pattern found in:
- `V12_002.Orders.Callbacks.Propagation.cs`:
  - Line 335: `pos.ExecutingAccount.Cancel(new[] { tOrder });`
  - Line 349: `pos.ExecutingAccount.Submit(new[] { replacement });`
  - Line 482: `acct.Cancel(new[] { currentEntry });`
  - Line 579: `acct.Submit(new[] { newEntry });`

**Impact:** Every order cancel/submit allocates a single-element array.

---

## LATENCY PROFILE ESTIMATION

### Current State (Estimated)

Based on allocation patterns and hotspot scores:

| Path | Estimated p99 | Allocation Sources |
|------|---------------|-------------------|
| OnBarUpdate | 500-1000μs | 6x string.Format, DrawORBox, Print calls |
| OnMarketData | 50-100μs | ProcessIpcCommands, PublishUiSnapshot (rate-gated) |
| ProcessOnOrderUpdate | 200-500μs | PropagateMasterPriceMove, HandleXXXFilled |
| HandleEntryOrderFilled | 300-600μs | .ToArray(), 2x string.Format, SubmitBracketOrders |
| MonitorRmaProximity | 1000-2000μs | CYC 32, 6x string.Format, Draw.Dot, Enqueue lambdas |

**Critical Finding:** MonitorRmaProximity is the **#1 latency risk** (hotspot 95.9, CYC 32).

### Allocation Budget (Per Tick)

Assuming 10k ticks/sec target:
- **Current:** ~500 bytes/tick × 10k = 5 MB/sec → Gen0 GC every 200ms
- **Target:** 0 bytes/tick × 10k = 0 MB/sec → Zero GC pressure

---

## ROOT CAUSE ANALYSIS

### Why .ToArray() Everywhere?

**Pattern:** Defensive copying to avoid collection-modified-during-enumeration exceptions.

**Example from HandleEntryOrderFilled:207:**
```csharp
foreach (var kvp in activePositions.ToArray())
{
    if (!activePositions.ContainsKey(kvp.Key)) continue; // Re-check after snapshot
    // ... modify activePositions inside loop
}
```

**Problem:** ConcurrentDictionary supports concurrent reads, but .ToArray() defeats this.

**Root Cause:** Fear of `InvalidOperationException` from modifying collection during enumeration.

### Why string.Format() Everywhere?

**Pattern:** Legacy .NET 4.8 logging without allocation awareness.

**Example from MonitorRmaProximity:296:**
```csharp
Print(string.Format("[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
    p.ProximityProbeCount, entryKey, dist, lvl));
```

**Problem:** 
1. `string.Format()` allocates format string + boxed arguments
2. `Print()` allocates another string for output
3. **Total:** 2-3 allocations per log statement

### Why new[] { order } Pattern?

**Pattern:** NinjaTrader API requires `IEnumerable<Order>` for Cancel/Submit.

**Example from Propagation.cs:335:**
```csharp
pos.ExecutingAccount.Cancel(new[] { tOrder });
```

**Problem:** Single-element array allocated on every order operation.

**Solution:** Pre-allocate reusable single-element array or use ArrayPool.

---

## COMPLEXITY HOTSPOTS

### MonitorRmaProximity (CYC 32, 104 lines)

**Complexity Drivers:**
1. Nested conditionals (proximity zones: in/dead/out)
2. FSM state transitions (WasInProximity, ProximityProbeCount)
3. Exhaustion logic (RmaMaxProbeCount threshold)
4. Visual feedback (Draw.Dot, RemoveDrawObject)

**Allocation Sources:**
- 6x `string.Format()` in Print statements
- 3x lambda closures in `Enqueue(ctx => ...)` (captures: entryKey, newDist, dist, lvl)
- `Draw.Dot()` - unknown allocation (likely minimal)

**Refactoring Strategy:**
- Extract sub-methods: `CheckProximityEntry`, `CheckProximityExit`, `HandleExhaustion`
- Pre-allocate format buffers for logging
- Reduce lambda captures (pass primitives, not closures)

### ProcessOnOrderUpdate (CYC 21, 45 lines)

**Complexity Drivers:**
1. Order state switch (Filled/Rejected/Cancelled/Accepted/Working)
2. Entry vs secondary order classification
3. Terminal state catch-all

**Allocation Sources:**
- `PropagateMasterPriceMove()` - unknown (needs profiling)
- `HandleEntryOrderFilled()` - .ToArray() + 2x string.Format
- `HandleSecondaryOrderFilled()` - .ToArray() + 2x string.Format

**Refactoring Strategy:**
- Eliminate .ToArray() via snapshot pattern (already used in AccountOrders.cs:841)
- Replace string.Format with pre-allocated buffers

---

## EXISTING OPTIMIZATIONS (PRESERVE)

### Good Patterns Already Implemented

1. **Pre-allocated Command Dictionary** (V12_002.UI.Callbacks.cs:42)
   ```csharp
   private Dictionary<Key, Action> _keyCommands; // [Phase7-UI T-A] zero allocation on hot path
   ```

2. **Rate-Gated UI Snapshots** (Lifecycle.cs:814-816)
   ```csharp
   _uiSnapshotTickCounter = (_uiSnapshotTickCounter + 1) % 5;
   if (_uiSnapshotTickCounter == 0)
       PublishUiSnapshot();
   ```

3. **Snapshot Pattern** (AccountOrders.cs:841-842)
   ```csharp
   // Single snapshot -- reused by both identity search and cascade cleanup
   var snapshot = activePositions.ToArray();
   ```

4. **ConcurrentDictionary for O(1) Lookups**
   - `_orderIdToFsmKey` (V12_002.cs:681)
   - `symmetryFleetEntryToDispatch` (Symmetry.cs:105)
   - `symmetryMasterEntryToDispatch` (Symmetry.cs:108)

### Anti-Patterns to Eliminate

1. **Redundant .ToArray() Calls**
   - Multiple methods call `.ToArray()` on same dictionary in same scope
   - Example: DrainQueuesForShutdown has **double .ToArray()** (line 106-109)

2. **String.Format in Hot Paths**
   - 30+ instances, many in tick-level code
   - Should use pre-allocated buffers or conditional compilation

3. **Single-Element Array Allocations**
   - `new[] { order }` pattern in Cancel/Submit calls
   - Should use ArrayPool or static reusable array

---

## RISK MATRIX

### High-Risk Changes

1. **Eliminating .ToArray() in Enumeration Loops** (RISK: HIGH)
   - **Danger:** Collection-modified-during-enumeration exceptions
   - **Mitigation:** Use snapshot pattern consistently, add unit tests for concurrent modification

2. **Replacing string.Format() with Buffer-Based Logging** (RISK: MEDIUM)
   - **Danger:** Off-by-one errors in buffer management, encoding issues
   - **Mitigation:** Encapsulate in LogBuffer helper class, extensive testing

3. **Object Pooling for Order Arrays** (RISK: MEDIUM)
   - **Danger:** Pool exhaustion, lifetime management bugs
   - **Mitigation:** Use ArrayPool<T> (battle-tested), add pool metrics

### Low-Risk Changes

1. **LatencyProbe Instrumentation** (RISK: LOW)
   - Struct-based, zero-allocation by design
   - Conditional compilation for production builds

2. **Pre-allocated Format Buffers** (RISK: LOW)
   - ThreadStatic or per-instance buffers
   - Fallback to allocation if buffer exhausted

---

## MEASUREMENT STRATEGY

### LatencyProbe Design (.NET 4.8 Compatible)

```csharp
// Zero-allocation latency measurement
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

**Key Features:**
- Struct (stack-allocated, zero heap pressure)
- Stopwatch.GetTimestamp() (high-resolution, no allocation)
- AggressiveInlining (minimize overhead)

### Instrumentation Points

1. **OnBarUpdate** - Full method latency
2. **OnMarketData** - Full method latency
3. **ProcessOnOrderUpdate** - Full method latency
4. **HandleEntryOrderFilled** - Isolated latency
5. **MonitorRmaProximity** - Isolated latency

### Metrics Collection

**Histogram Buckets (μs):**
- <10, 10-50, 50-100, 100-500, 500-1000, 1000-5000, >5000

**Aggregation:**
- p50, p95, p99, max per 1-minute window
- Export to CSV for offline analysis

---

## OPTIMIZATION ROADMAP

### Phase 1: Baseline & Instrumentation (Ticket 01)
- Implement LatencyProbe struct
- Instrument 5 critical methods
- Collect 1-hour baseline under 10k ticks/sec load
- Establish p50/p95/p99 targets

### Phase 2: String.Format Elimination (Ticket 02)
- Replace all hot-path string.Format with pre-allocated buffers
- Target: OnBarUpdate, MonitorRmaProximity, HandleXXXFilled
- Verify zero allocation via ETW/PerfView

### Phase 3: .ToArray() Elimination (Ticket 03)
- Audit all .ToArray() calls, classify as hot/cold
- Replace hot-path .ToArray() with snapshot pattern
- Add concurrent modification tests

### Phase 4: Order Array Pooling (Ticket 04)
- Replace `new[] { order }` with ArrayPool<Order>
- Implement OrderArrayPool helper class
- Verify pool metrics (utilization, exhaustion events)

### Phase 5: MonitorRmaProximity Refactoring (Ticket 05)
- Extract sub-methods (CYC 32 → 3x CYC 10)
- Eliminate lambda closures
- Apply all optimization patterns

### Phase 6: Verification & Stress Testing (Ticket 06)
- Re-run latency baseline
- Verify p99 < 100μs target
- 1-hour stress test at 10k ticks/sec
- Zero GC pauses validation

---

## SUCCESS CRITERIA (REFINED)

### Quantitative Targets

| Metric | Baseline (Est.) | Target | Measurement |
|--------|-----------------|--------|-------------|
| OnBarUpdate p99 | 500-1000μs | <100μs | LatencyProbe |
| OnMarketData p99 | 50-100μs | <50μs | LatencyProbe |
| ProcessOnOrderUpdate p99 | 200-500μs | <100μs | LatencyProbe |
| Allocations/tick | ~500 bytes | 0 bytes | ETW/PerfView |
| GC pauses (1hr) | ~180 (Gen0) | 0 | PerfMon |

### Qualitative Targets

1. **Code Maintainability**
   - MonitorRmaProximity: CYC 32 → <20 (3 sub-methods)
   - No method exceeds 100 lines
   - All optimization patterns documented

2. **V12 DNA Compliance**
   - Zero `lock()` statements (verified via grep)
   - ASCII-only strings (verified via check_ascii.py)
   - Correctness by construction (no runtime guards)

---

## DEPENDENCIES & CONSTRAINTS

### .NET 4.8 Limitations

1. **No Span<T>** - Must use ArrayPool<T> or pre-allocated arrays
2. **String Interpolation Allocates** - Must use StringBuilder or buffer-based formatting
3. **No ValueTask** - Async patterns limited to Task<T>
4. **No ref returns** - Cannot return refs to pooled buffers

### NinjaTrader API Constraints

1. **IEnumerable<Order> Required** - Cancel/Submit methods require collection
2. **Print() Allocates** - No way to avoid allocation in logging
3. **Draw.Dot() Unknown** - May allocate, needs profiling

### V12 DNA Mandates

1. **Lock-Free Actor Pattern** - All state mutations via Enqueue
2. **ASCII-Only** - No Unicode in string literals
3. **Correctness by Construction** - No invalid states possible

---

## OPEN QUESTIONS (UPDATED)

1. **ArrayPool<T> Thread Safety** (.NET 4.8)
   - Q: Is ArrayPool<T> available in .NET 4.8?
   - A: **NO** - ArrayPool<T> introduced in .NET Standard 2.1 / .NET Core 2.1
   - **Solution:** Implement custom pool or use ConcurrentBag<T[]>

2. **Print() Allocation Bypass**
   - Q: Can we bypass Print() allocation in production?
   - A: Use conditional compilation (#if DEBUG) or NOP logger

3. **Draw.Dot() Allocation Profile**
   - Q: Does Draw.Dot() allocate on every call?
   - A: Requires profiling (Ticket 01 deliverable)

4. **Snapshot Pattern Correctness**
   - Q: Does snapshot pattern guarantee no concurrent modification exceptions?
   - A: YES, if snapshot taken before enumeration and not reused across yields

---

## NEXT STEPS

**[PLAN-GATE]** Analysis complete. Key decisions:

1. **LatencyProbe:** Struct-based, Stopwatch.GetTimestamp(), zero-allocation
2. **String.Format:** Replace with pre-allocated char[] buffers + custom formatter
3. **.ToArray():** Snapshot pattern (single allocation, reused in scope)
4. **Order Arrays:** Custom pool (ConcurrentBag<Order[]>) - ArrayPool unavailable in .NET 4.8
5. **MonitorRmaProximity:** Extract 3 sub-methods (CYC 32 → 3x <10)

Proceed to Phase 2.3 (Sentinel Audit) or Phase 3 (Validation)?