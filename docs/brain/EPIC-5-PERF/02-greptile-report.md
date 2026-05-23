# EPIC-5-PERF: Sentinel Audit (Semantic Scan)

**Epic ID:** EPIC-5-PERF  
**Phase:** 2.3 - Sentinel Adversarial Review  
**Created:** 2026-05-23  
**Tool:** jCodemunch-MCP (Greptile unavailable)

---

## EXECUTIVE SUMMARY

Sentinel audit using jCodemunch-MCP semantic analysis reveals **3 CRITICAL GAPS** and **2 SIGNIFICANT RISKS** not addressed in the approach document. The approach is fundamentally sound but requires revisions before proceeding to validation.

**Verdict:** **REVISION REQUIRED**

---

## SEMANTIC GAP ANALYSIS

### GAP 1: PublishUiSnapshot Allocates UIStateSnapshot on EVERY Call (CRITICAL)

**Discovery:** `PublishUiSnapshot()` (src/V12_002.UI.Snapshot.cs:189) creates a **new UIStateSnapshot** object on every invocation.

**Evidence:**
```csharp
UIStateSnapshot snapshot = new UIStateSnapshot  // LINE 194 - HEAP ALLOCATION
{
    EmaValue = ema9Value,
    AtrValue = currentATR > 0 ? currentATR : 0,
    // ... 30+ field assignments ...
    Config = BuildUiConfigSnapshot(mode),        // Nested allocation
    Compliance = BuildUiComplianceSnapshot(),    // Nested allocation
    LivePosition = BuildUiLivePositionSnapshot() // Nested allocation
};
```

**Impact:**
- Called from `OnMarketData` (rate-gated every 5 ticks)
- Called from `OnBarUpdate` (every bar)
- **Estimated allocation:** 200-500 bytes per call
- **At 10k ticks/sec:** 200-500 bytes × 2000 calls/sec = 400KB-1MB/sec
- **Nested allocations:** BuildUiConfigSnapshot, BuildUiComplianceSnapshot, BuildUiLivePositionSnapshot create additional objects

**Gap in Approach:**
- Ticket 01 mentions "profile PublishUiSnapshot" but does NOT include it in optimization scope
- Ticket 02-05 do NOT address UIStateSnapshot allocation
- **MISSING:** Object pooling or pre-allocated snapshot reuse strategy

**Recommendation:**
- Add **Ticket 02B: UIStateSnapshot Pooling**
- Pre-allocate UIStateSnapshot and reuse via field updates
- Pool nested snapshot objects (Config, Compliance, LivePosition)

---

### GAP 2: Existing Stopwatch Usage NOT Analyzed (SIGNIFICANT)

**Discovery:** Codebase already uses `System.Diagnostics.Stopwatch` in 3 files:
1. `SignalBroadcaster.cs:209` - Latency tracking for event fanout
2. `V12_002.SIMA.Dispatch.cs:132` - Fleet dispatch latency (7 instances)
3. `V12_002.SIMA.Execution.cs:48` - RMA execution latency (6 instances)

**Evidence:**
```csharp
// SignalBroadcaster.cs:209
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... event invocation ...
if (sw.Elapsed.TotalMilliseconds > 1.0)
    NinjaTrader.Code.Output.Process(string.Format("[LATENCY_FANOUT] {0}: {1:F2}ms...", 
        typeof(T).Name, sw.Elapsed.TotalMilliseconds), PrintTo.OutputTab1);
```

**Impact:**
- Existing latency tracking uses `Stopwatch.StartNew()` (allocates Stopwatch instance)
- Approach proposes `LatencyProbe` struct but does NOT address existing Stopwatch usage
- **Duplication risk:** Two latency measurement systems (Stopwatch vs LatencyProbe)

**Gap in Approach:**
- Ticket 01 does NOT mention migrating existing Stopwatch usage to LatencyProbe
- **MISSING:** Audit of existing latency tracking patterns
- **MISSING:** Migration strategy for SignalBroadcaster, SIMA.Dispatch, SIMA.Execution

**Recommendation:**
- Expand Ticket 01 scope to include:
  - Audit existing Stopwatch usage (3 files, 14 instances)
  - Migrate to LatencyProbe struct where applicable
  - Document which Stopwatch usages remain (if any)

---

### GAP 3: No ThreadStatic Usage Exists - LogBuffer Pattern Unproven (SIGNIFICANT)

**Discovery:** Zero instances of `[ThreadStatic]` or `ThreadLocal<T>` found in codebase.

**Evidence:**
```bash
# jCodemunch search_text result:
"result_count": 0
```

**Impact:**
- Approach proposes `[ThreadStatic]` char[] buffer for LogBuffer (Ticket 02)
- **Unproven pattern:** No existing ThreadStatic usage to validate thread safety
- **Risk:** NinjaTrader threading model may not be compatible with ThreadStatic
- **Risk:** ThreadStatic buffers may leak memory if threads are pooled

**Gap in Approach:**
- Ticket 02 assumes ThreadStatic is safe without validation
- **MISSING:** Thread model analysis (single-threaded? thread-pooled? actor-based?)
- **MISSING:** Fallback strategy if ThreadStatic proves unsafe

**Recommendation:**
- Add **Ticket 01B: Thread Model Analysis**
  - Document NinjaTrader threading model (OnBarUpdate, OnMarketData, Enqueue)
  - Validate ThreadStatic safety via test harness
  - If unsafe, use instance-level char[] buffer instead

---

## INTEGRATION RISKS

### RISK 1: Draw.Dot() Allocation Profile Unknown (MEDIUM)

**Discovery:** `Draw.Dot()` called in MonitorRmaProximity (line 322) - allocation profile unknown.

**Evidence:**
```csharp
// V12_002.Entries.RMA.cs:322
Draw.Dot(this, "Prox_" + kvp.Key, false, 0, level, Brushes.Cyan);
```

**Impact:**
- Called on every proximity entry (potentially multiple times per bar)
- NinjaTrader drawing API may allocate internally
- **String concatenation:** `"Prox_" + kvp.Key` allocates on every call

**Gap in Approach:**
- Ticket 01 mentions "profile Draw.Dot" but does NOT include mitigation
- Ticket 05 (MonitorRmaProximity refactoring) does NOT address Draw.Dot allocation

**Recommendation:**
- Ticket 01: Add Draw.Dot to profiling scope
- Ticket 05: If Draw.Dot allocates, consider:
  - Pre-allocate tag strings (e.g., `_proxTagCache[entryKey]`)
  - Conditional compilation (#if DEBUG) for visual feedback
  - Replace with lightweight telemetry counter

---

### RISK 2: activePositions Blast Radius Not Quantified (LOW)

**Discovery:** `activePositions` dictionary has unknown blast radius (jCodemunch returned empty result).

**Evidence:**
```bash
# jCodemunch get_blast_radius result:
"confirmed": [], "potential": []
```

**Impact:**
- Ticket 03 proposes snapshot pattern for activePositions.ToArray()
- **Unknown:** How many methods read/write activePositions concurrently?
- **Unknown:** Are there hidden race conditions in snapshot pattern?

**Gap in Approach:**
- Ticket 03 assumes snapshot pattern is safe without blast radius analysis
- **MISSING:** Concurrent access audit for activePositions

**Recommendation:**
- Ticket 03: Add manual audit of activePositions usage
  - Grep for `activePositions.` across all files
  - Document read/write patterns
  - Verify snapshot pattern eliminates all race conditions

---

## DNA VIOLATION DETECTION

### VIOLATION 1: LogBuffer ThreadStatic May Violate Actor Pattern (MEDIUM)

**Analysis:** V12 DNA mandates lock-free Actor pattern via `Enqueue(ctx => ...)`. ThreadStatic buffers bypass the Actor queue, potentially creating race conditions.

**Evidence:**
- Approach proposes ThreadStatic char[] buffer (Ticket 02)
- Actor pattern ensures single-threaded access to state
- ThreadStatic creates per-thread state, bypassing Actor serialization

**Risk:**
- If multiple threads call Print() concurrently, ThreadStatic buffers are safe
- BUT: If Actor thread calls Print() while user thread also calls Print(), buffer corruption possible

**Recommendation:**
- Ticket 02: Validate LogBuffer thread safety against Actor pattern
- Alternative: Use instance-level buffer protected by Actor queue

---

### VIOLATION 2: OrderArrayPool Lifetime May Violate Bounded Latency (LOW)

**Analysis:** Ticket 04 proposes ConcurrentBag<Order[]> pool with try/finally cleanup. If exception occurs between Rent() and Return(), pool leaks arrays.

**Evidence:**
```csharp
// Proposed pattern (Ticket 04):
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

**Risk:**
- If Cancel() throws exception, finally block runs (safe)
- BUT: If exception occurs BEFORE try block (e.g., in orderArray[0] assignment), finally never runs
- **Pool leak:** Array never returned, pool exhausted over time

**Recommendation:**
- Ticket 04: Move orderArray[0] assignment INSIDE try block
- Add pool exhaustion metrics (rent count, return count, leak count)

---

## SENTINEL VERDICT

**Status:** **REVISION REQUIRED**

### Critical Issues (Must Fix Before Validation)

1. **PublishUiSnapshot Allocation** (GAP 1)
   - Add Ticket 02B: UIStateSnapshot pooling
   - Estimated impact: 400KB-1MB/sec reduction

2. **Existing Stopwatch Migration** (GAP 2)
   - Expand Ticket 01 to migrate 14 existing Stopwatch instances
   - Prevents duplication of latency tracking systems

3. **ThreadStatic Safety Validation** (GAP 3)
   - Add Ticket 01B: Thread model analysis
   - Validate ThreadStatic compatibility with NinjaTrader/Actor pattern

### Significant Issues (Should Fix Before Validation)

4. **Draw.Dot Allocation** (RISK 1)
   - Add Draw.Dot profiling to Ticket 01
   - Add mitigation to Ticket 05 if allocation confirmed

5. **LogBuffer Actor Pattern Compliance** (VIOLATION 1)
   - Validate ThreadStatic safety against Actor pattern in Ticket 02
   - Document thread safety guarantees

### Minor Issues (Can Address During Execution)

6. **activePositions Blast Radius** (RISK 2)
   - Manual audit in Ticket 03 (grep-based)

7. **OrderArrayPool Lifetime** (VIOLATION 2)
   - Fix try/finally scope in Ticket 04

---

## REVISED TICKET STRUCTURE

### Recommended Changes

**Ticket 01 (Baseline) - EXPAND SCOPE:**
- Add: Audit existing Stopwatch usage (SignalBroadcaster, SIMA.Dispatch, SIMA.Execution)
- Add: Migrate existing Stopwatch to LatencyProbe where applicable
- Add: Profile Draw.Dot() allocation
- Add: Profile PublishUiSnapshot() allocation (nested objects)

**Ticket 01B (NEW) - Thread Model Analysis:**
- Document NinjaTrader threading model
- Validate ThreadStatic safety
- Validate Actor pattern compatibility with ThreadStatic buffers
- Deliverable: Thread safety report

**Ticket 02 (String.Format) - ADD VALIDATION:**
- Add: ThreadStatic safety validation (reference Ticket 01B)
- Add: Fallback strategy if ThreadStatic unsafe (instance-level buffer)

**Ticket 02B (NEW) - UIStateSnapshot Pooling:**
- Pre-allocate UIStateSnapshot and nested objects
- Implement field-level updates instead of new object creation
- Target: PublishUiSnapshot, BuildUiConfigSnapshot, BuildUiComplianceSnapshot, BuildUiLivePositionSnapshot
- Estimated reduction: 400KB-1MB/sec

**Ticket 03 (.ToArray()) - ADD AUDIT:**
- Add: Manual audit of activePositions concurrent access patterns
- Add: Document read/write patterns
- Add: Verify snapshot pattern safety

**Ticket 04 (Order Pool) - FIX LIFETIME:**
- Move orderArray[0] assignment inside try block
- Add pool exhaustion metrics

**Ticket 05 (MonitorRma) - ADD DRAW.DOT MITIGATION:**
- If Draw.Dot allocates (from Ticket 01 profiling):
  - Pre-allocate tag strings OR
  - Conditional compilation (#if DEBUG) OR
  - Replace with telemetry counter

**Ticket 06 (Verification) - NO CHANGES**

---

## NEXT STEPS

**[SENTINEL-GATE]** Semantic scan complete. Verdict: **REVISION REQUIRED**.

**Required Actions:**
1. Director reviews this report
2. Planner revises 02-approach.md to address 3 critical gaps
3. Re-run `/epic-scan` to verify gaps closed (optional)
4. Proceed to `/epic-validate` only after gaps addressed

**Estimated Impact of Revisions:**
- Ticket 01: +2 days (Stopwatch migration, Draw.Dot profiling)
- Ticket 01B: +1 day (thread model analysis)
- Ticket 02B: +3 days (UIStateSnapshot pooling)
- **Total:** +6 days to epic timeline

**Alternative (Fast-Track):**
- Accept GAP 1 (PublishUiSnapshot) as known limitation
- Proceed with Tickets 01-06 as-is
- Add Ticket 07 (UIStateSnapshot pooling) as follow-up epic
- **Risk:** Miss 400KB-1MB/sec optimization opportunity