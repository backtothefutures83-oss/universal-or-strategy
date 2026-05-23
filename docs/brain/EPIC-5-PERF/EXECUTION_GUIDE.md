# EPIC-5-PERF: Execution Guide

**Epic ID:** EPIC-5-PERF  
**Status:** Ready for Execution  
**Created:** 2026-05-23  
**Total Tickets:** 8 (T01, T01B, T02, T03, T04, T05, T06, T07)  
**Estimated Duration:** 17 days

---

## EXECUTION SUMMARY

This epic eliminates ALL heap allocations in V12's hot paths through 8 surgical tickets. Each ticket is independently testable and revertible.

**Target Outcome:** Zero allocations, p99 <100μs latency, zero GC pauses during 1-hour stress test.

---

## TICKET OVERVIEW

| Ticket | Name | Duration | Dependencies | CYC Impact | Files Modified |
|--------|------|----------|--------------|------------|----------------|
| T01 | Baseline Instrumentation & Stopwatch Migration | 4 days | None | Neutral | 9 |
| T01B | Thread Model Analysis & ThreadStatic Validation | 1 day | T01 | Neutral | 0 (docs/tests) |
| T02 | String.Format Elimination | 2 days | T01B | Neutral | 8 |
| T03 | UIStateSnapshot Object Pooling | 3 days | T01 | +3 | 2 |
| T04 | .ToArray() Elimination | 2 days | T01 | Neutral | 6 |
| T05 | Order Array Pooling | 1 day | T01 | +2 | 2 |
| T06 | MonitorRmaProximity Refactoring | 2 days | T01 | 32→31 | 1 |
| T07 | Verification & Stress Testing | 2 days | T01-T06 | Neutral | 0 (testing) |

---

## EXECUTION ORDER

### Phase 1: Foundation (Days 1-5)
```
T01 (Baseline + Stopwatch Migration) [4 days]
  ↓
T01B (Thread Model Analysis) [1 day]
```

**Gate 1:** Baseline metrics established, ThreadStatic safety validated.

### Phase 2: Parallel Optimization (Days 6-12)
```
T02 (String.Format) [2 days] ← depends on T01B
T03 (UISnapshot Pool) [3 days]
T04 (.ToArray()) [2 days]
T05 (Order Pool) [1 day]
T06 (MonitorRma) [2 days]
```

**Gate 2:** All optimizations complete, individual F5 gates passed.

### Phase 3: Verification (Days 13-17)
```
T07 (Verification & Stress Testing) [2 days]
```

**Gate 3:** p99 <100μs validated, zero GC pauses confirmed.

---

## TICKET DETAILS

### T01: Baseline Instrumentation & Stopwatch Migration

**Goal:** Establish p50/p95/p99 baseline + migrate 14 existing Stopwatch instances to LatencyProbe.

**Scope:**
1. Create `LatencyProbe` struct (zero-allocation, Stopwatch.GetTimestamp-based)
2. Add `LatencyProbe.IsValid` property (validation mitigation)
3. Instrument 6 methods: OnBarUpdate, OnMarketData, ProcessOnOrderUpdate, HandleEntryOrderFilled, MonitorRmaProximity, PublishUiSnapshot
4. Migrate 14 Stopwatch.StartNew() calls:
   - SignalBroadcaster.cs:209 (1 instance)
   - V12_002.SIMA.Dispatch.cs:132 (7 instances)
   - V12_002.SIMA.Execution.cs:48 (6 instances)
5. Create `LatencyHistogram` class (pre-allocated buckets)
6. Profile Draw.Dot() allocation (MonitorRmaProximity:322)
7. Profile PublishUiSnapshot() allocation (UI.Snapshot.cs:189)
8. Run 1-hour baseline under 10k ticks/sec load
9. Export CSV for offline analysis

**Deliverables:**
- `src/V12_002.Perf.LatencyProbe.cs` (new)
- `src/V12_002.Perf.Histogram.cs` (new)
- Instrumentation in 6 methods
- Migration of 14 Stopwatch instances
- Baseline CSV report
- Draw.Dot allocation profile
- PublishUiSnapshot allocation profile (ETW trace)

**Success Criteria:**
- LatencyProbe.IsValid returns false for invalid usage
- All 14 Stopwatch instances migrated
- Baseline p50/p95/p99 established for 6 methods
- Draw.Dot allocation profile documented
- PublishUiSnapshot allocation profile documented

**CYC Impact:** Neutral (instrumentation + migration)  
**Files Modified:** 9 (BarUpdate.cs, Lifecycle.cs, Orders.Callbacks.cs, Entries.RMA.cs, UI.Snapshot.cs, SignalBroadcaster.cs, SIMA.Dispatch.cs, SIMA.Execution.cs, + 2 new)

---

### T01B: Thread Model Analysis & ThreadStatic Validation

**Goal:** Validate ThreadStatic safety for LogBuffer within NinjaTrader/Actor pattern context.

**Scope:**
1. Document NinjaTrader threading model:
   - OnBarUpdate thread (single-threaded? thread-pooled?)
   - OnMarketData thread (same as OnBarUpdate?)
   - Enqueue/Actor thread (dedicated? shared?)
   - UI thread (WPF dispatcher)
2. Create ThreadStatic safety test harness
3. Test concurrent access (10 threads × 1000 iterations)
4. Measure ThreadStatic overhead vs instance-level buffer
5. Document Actor pattern compatibility
6. **Decision:** ThreadStatic approved OR fallback to instance-level buffer

**Deliverables:**
- `docs/brain/EPIC-5-PERF/thread-model.md` (new)
- `tests/ThreadStaticSafetyTest.cs` (new)
- Performance comparison report
- Actor pattern compatibility report
- **Decision document:** ThreadStatic approved/rejected

**Success Criteria:**
- Thread model documented
- ThreadStatic test passes 1000 iterations with zero corruption
- Performance overhead <5% vs instance-level buffer
- Actor pattern compatibility confirmed
- Decision made: ThreadStatic or instance-level

**CYC Impact:** Neutral (testing only)  
**Files Modified:** 0 (documentation + tests)

---

### T02: String.Format Elimination

**Goal:** Replace all hot-path `string.Format()` with pre-allocated char[] buffers.

**Scope:**
1. Implement `LogBuffer` class (ThreadStatic or instance-level based on T01B decision)
2. Add overflow counter (validation mitigation)
3. Replace string.Format in 30+ instances:
   - OnBarUpdate (6 instances)
   - MonitorRmaProximity (6 instances)
   - HandleEntryOrderFilled (2 instances)
   - HandleSecondaryOrderFilled (2 instances)
   - SignalBroadcaster (1 instance)
   - SIMA.Dispatch (7 instances)
   - SIMA.Execution (6 instances)
4. Add unit tests for LogBuffer
5. Verify zero allocation via ETW trace

**Deliverables:**
- `src/V12_002.Perf.LogBuffer.cs` (new)
- 30+ string.Format replacements
- LogBuffer unit tests
- ETW allocation profile (zero allocations confirmed)

**Success Criteria:**
- LogBuffer.Format returns correct strings
- Overflow counter = 0 during 1-hour test
- ETW trace shows zero allocations in LogBuffer.Format
- All Print() calls use LogBuffer.Format

**CYC Impact:** Neutral (replacement only)  
**Files Modified:** 8 (BarUpdate.cs, Entries.RMA.cs, Orders.Callbacks.cs, SignalBroadcaster.cs, SIMA.Dispatch.cs, SIMA.Execution.cs, + 1 new LogBuffer.cs, + V12_002.cs if instance-level)  
**Allocation Reduction:** ~30 allocations/tick → 0

---

### T03: UIStateSnapshot Object Pooling

**Goal:** Eliminate UIStateSnapshot allocation on every PublishUiSnapshot call.

**Scope:**
1. Implement `UISnapshotPool` class (ConcurrentBag-based)
2. Add pool metrics: RentCount, ReturnCount, FallbackCount (validation mitigation)
3. Modify PublishUiSnapshot to use pooling
4. Modify BuildUiConfigSnapshot, BuildUiComplianceSnapshot, BuildUiLivePositionSnapshot to use pooling
5. Add volatile write unit test (validation mitigation)
6. Verify zero allocation via ETW trace

**Deliverables:**
- `src/V12_002.Perf.UISnapshotPool.cs` (new)
- Modified PublishUiSnapshot (UI.Snapshot.cs)
- Pool metrics exposed via GetPoolMetrics()
- Volatile write unit test
- ETW allocation profile (zero allocations confirmed)

**Success Criteria:**
- UISnapshotPool.RentSnapshot returns pooled object (after warm-up)
- FallbackCount <10% of RentCount during 1-hour test
- Volatile write test passes 1000 iterations
- ETW trace shows zero allocations in PublishUiSnapshot

**CYC Impact:** +3 (pool rent/return logic)  
**Files Modified:** 2 (UI.Snapshot.cs, + 1 new UISnapshotPool.cs)  
**Allocation Reduction:** 400KB-1MB/sec → 0 (after pool warm-up)

---

### T04: .ToArray() Elimination

**Goal:** Standardize snapshot pattern to eliminate redundant .ToArray() calls.

**Scope:**
1. Audit activePositions concurrent access patterns (validation mitigation)
2. Replace inline .ToArray() with snapshot pattern in:
   - HandleEntryOrderFilled (line 207)
   - HandleSecondaryOrderFilled (line 263)
   - DrainQueuesForShutdown (lines 95, 106-109 - DOUBLE ALLOCATION)
   - LogicAudit methods (lines 289, 339)
3. Add concurrent modification unit tests
4. Verify zero additional allocations via ETW trace

**Deliverables:**
- Snapshot pattern applied to 6 files
- activePositions access audit document
- Concurrent modification unit tests
- ETW allocation profile (reduced allocations confirmed)

**Success Criteria:**
- All inline .ToArray() replaced with snapshot pattern
- Concurrent modification tests pass 1000 iterations
- ETW trace shows ~15 fewer .ToArray() allocations
- No collection-modified exceptions during stress test

**CYC Impact:** Neutral (refactoring only)  
**Files Modified:** 6 (Orders.Callbacks.cs, Orders.Callbacks.Execution.cs, Lifecycle.cs, LogicAudit.cs, Orders.Callbacks.AccountOrders.cs, Orders.Callbacks.Propagation.cs)  
**Allocation Reduction:** ~25 .ToArray() calls → ~10 (snapshot pattern)

---

### T05: Order Array Pooling

**Goal:** Eliminate `new[] { order }` allocations in Cancel/Submit calls.

**Scope:**
1. Implement `OrderArrayPool` class (ConcurrentBag-based)
2. Add pool metrics: RentCount, ReturnCount, FallbackCount (validation mitigation)
3. Replace `new[] { order }` with pooled arrays in:
   - V12_002.Orders.Callbacks.Propagation.cs (4 instances)
4. Move orderArray[0] assignment inside try block (validation fix)
5. Add pool exhaustion unit test
6. Verify zero allocation via ETW trace

**Deliverables:**
- `src/V12_002.Perf.OrderArrayPool.cs` (new)
- 4 `new[] { order }` replacements
- Pool metrics exposed via GetPoolMetrics()
- Pool exhaustion unit test
- ETW allocation profile (zero allocations confirmed)

**Success Criteria:**
- OrderArrayPool.Rent returns pooled array (after warm-up)
- FallbackCount <10% of RentCount during 1-hour test
- Pool exhaustion test passes (array returned even on exception)
- ETW trace shows zero allocations in Cancel/Submit calls

**CYC Impact:** +2 per call site (try/finally overhead)  
**Files Modified:** 2 (Orders.Callbacks.Propagation.cs, + 1 new OrderArrayPool.cs)  
**Allocation Reduction:** 4 allocations/order-operation → 0 (after pool warm-up)

---

### T06: MonitorRmaProximity Refactoring

**Goal:** Reduce CYC 32 → 31 via extraction, eliminate lambda closures, cache Draw.Dot tags.

**Scope:**
1. Extract 3 sub-methods from MonitorRmaProximity:
   - CheckProximityEntry (CYC 8)
   - CheckProximityExit (CYC 12)
   - HandleExhaustion (CYC 6)
2. Implement Draw.Dot tag cache with size limit (validation mitigation)
3. Apply LogBuffer.Format (from T02) to 6 string.Format calls
4. Verify CYC reduction via complexity_audit.py

**Deliverables:**
- 3 extracted sub-methods in Entries.RMA.cs
- _proxTagCache with MAX_CACHE_SIZE = 1000
- 6 string.Format → LogBuffer.Format replacements
- Complexity audit report (CYC 32 → 31)

**Success Criteria:**
- MonitorRmaProximity CYC = 5
- CheckProximityEntry CYC ≤ 8
- CheckProximityExit CYC ≤ 12
- HandleExhaustion CYC ≤ 6
- _proxTagCache.Count ≤ 1000 during 1-hour test
- No logic changes (pure extraction)

**CYC Impact:** 32 → 31 (5 + 8 + 12 + 6)  
**Files Modified:** 1 (Entries.RMA.cs)  
**Allocation Reduction:** 6x string.Format → LogBuffer (from T02) + Draw.Dot tags cached

---

### T07: Verification & Stress Testing

**Goal:** Validate p99 <100μs target and zero GC pressure.

**Scope:**
1. **Latency Re-Baseline:**
   - Re-run 1-hour test under 10k ticks/sec
   - Compare p50/p95/p99 against T01 baseline
   - Verify p99 <100μs for all 6 methods

2. **Allocation Profiling:**
   - Run ETW trace (PerfView) during 10-minute window
   - Verify 0 bytes allocated in hot paths
   - Check for unexpected allocations

3. **GC Pause Validation:**
   - Monitor PerfMon GC metrics during 1-hour test
   - Verify 0 Gen0 collections during active trading
   - Verify 0 Gen1/Gen2 collections

4. **Pool Metrics Validation:**
   - UISnapshotPool: FallbackCount <10% of RentCount
   - OrderArrayPool: FallbackCount <10% of RentCount
   - LogBuffer: OverflowCount = 0
   - Draw.Dot tag cache: Count ≤ 1000

5. **Stress Test:**
   - 10k ticks/sec sustained load
   - 1-hour duration
   - Monitor CPU, memory, latency histograms

6. **Regression Testing:**
   - F5 gate (NinjaTrader compile + load)
   - `deploy-sync.ps1` (hard-link integrity)
   - `complexity_audit.py` (CYC verification)
   - `grep -r "lock(" src/` (zero matches)

**Deliverables:**
- Latency comparison report (before/after CSV)
- ETW allocation profile (PerfView screenshots)
- GC metrics (PerfMon CSV export)
- Pool metrics report
- Stress test summary (p50/p95/p99, CPU%, memory)
- Regression test results

**Success Criteria:**
- OnBarUpdate p99 <100μs
- OnMarketData p99 <50μs
- ProcessOnOrderUpdate p99 <100μs
- MonitorRmaProximity p99 <500μs
- PublishUiSnapshot p99 <100μs
- Zero Gen0/Gen1/Gen2 collections during 1-hour test
- All pool FallbackCounts <10%
- LogBuffer OverflowCount = 0
- All regression tests PASS

**CYC Impact:** Neutral (testing only)  
**Files Modified:** 0 (verification only)

---

## DEPENDENCY GRAPH

```
T01 (Baseline + Stopwatch Migration) [4d]
  ↓
T01B (Thread Model Analysis) [1d]
  ↓
T02 (String.Format Elimination) [2d] ──┐
                                        │
T01 ──→ T03 (UISnapshot Pooling) [3d] ─┤
                                        │
T01 ──→ T04 (.ToArray() Elimination) [2d] ─┤
                                            │
T01 ──→ T05 (Order Array Pooling) [1d] ────┤
                                            │
T01 ──→ T06 (MonitorRma Refactoring) [2d] ─┤
                                            ↓
                                    T07 (Verification) [2d]
```

**Critical Path:** T01 → T01B → T02 → T07 = 9 days  
**Parallel Path:** T01 → T03/T04/T05/T06 → T07 = 7 days  
**Total Duration:** 17 days (with parallelization)

---

## ROLLBACK STRATEGY

Each ticket is independently revertible via `git revert <commit-hash>`.

| Ticket | Rollback Command | Impact |
|--------|------------------|--------|
| T01 | `git revert <hash>` | Remove instrumentation, revert Stopwatch migrations |
| T01B | N/A | Documentation only |
| T02 | `git revert <hash>` | Revert LogBuffer → string.Format |
| T03 | `git revert <hash>` | Remove UISnapshotPool → new UIStateSnapshot |
| T04 | `git revert <hash>` | Revert snapshot pattern → inline .ToArray() |
| T05 | `git revert <hash>` | Remove OrderArrayPool → new[] { order } |
| T06 | `git revert <hash>` | Revert extraction → original 104-line method |
| T07 | N/A | Testing only |

**Emergency Rollback:** Revert all tickets in reverse order (T06 → T05 → T04 → T03 → T02 → T01).

---

## MONITORING & ALERTS

### Real-Time Metrics (Add to T07)

1. **Pool Exhaustion Alert:**
   - Trigger: FallbackCount > 10% of RentCount over 1-minute window
   - Action: Increase MAX_POOL_SIZE or investigate leak

2. **LogBuffer Overflow Alert:**
   - Trigger: OverflowCount > 0
   - Action: Increase BUFFER_SIZE or investigate format strings

3. **Tag Cache Growth Alert:**
   - Trigger: _proxTagCache.Count > 1000
   - Action: Verify cache eviction logic

4. **Latency Regression Alert:**
   - Trigger: p99 increases by >20% from baseline
   - Action: Profile hot path, identify regression source

### Post-Execution Metrics

- **Total Allocation Reduction:** ~500 bytes/tick → 0 bytes/tick
- **GC Pressure Reduction:** ~180 Gen0 collections/hour → 0
- **Latency Improvement:** p99 500-1000μs → <100μs (5-10x improvement)

---

## SUCCESS CRITERIA (EPIC-LEVEL)

### Quantitative Targets

| Metric | Baseline (Est.) | Target | Actual | Status |
|--------|-----------------|--------|--------|--------|
| OnBarUpdate p99 | 500-1000μs | <100μs | TBD | ⏳ |
| OnMarketData p99 | 50-100μs | <50μs | TBD | ⏳ |
| ProcessOnOrderUpdate p99 | 200-500μs | <100μs | TBD | ⏳ |
| MonitorRmaProximity p99 | 1000-2000μs | <500μs | TBD | ⏳ |
| PublishUiSnapshot p99 | 200-500μs | <100μs | TBD | ⏳ |
| Allocations/tick | ~500 bytes | 0 bytes | TBD | ⏳ |
| GC pauses (1hr) | ~180 (Gen0) | 0 | TBD | ⏳ |

### Qualitative Targets

1. **Code Maintainability:**
   - MonitorRmaProximity: CYC 32 → 31 (3 sub-methods)
   - No method exceeds 100 lines
   - All optimization patterns documented

2. **V12 DNA Compliance:**
   - Zero `lock()` statements (verified via grep)
   - ASCII-only strings (verified via check_ascii.py)
   - Correctness by construction (no runtime guards)
   - Thread safety validated (T01B)

3. **Consistency:**
   - Single latency measurement system (LatencyProbe)
   - No Stopwatch.StartNew() instances remaining
   - Unified logging system (LogBuffer)

---

## NEXT STEPS

**[TICKETS-GATE]** 8 tickets ready for execution.

**Execution Order:**
1. T01 (4 days) - Foundation
2. T01B (1 day) - Thread safety validation
3. T02, T03, T04, T05, T06 (parallel, 3 days max) - Optimizations
4. T07 (2 days) - Verification

**Total Duration:** 17 days (with parallelization)

**Director Action Required:** Type **RUN** to begin execution or **ADJUST** to modify tickets.