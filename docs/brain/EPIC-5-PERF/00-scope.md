# EPIC-5-PERF: Zero-Allocation Hot Path Optimization and Bounded Latency Verification

**Epic ID:** EPIC-5-PERF  
**Status:** INTAKE  
**Created:** 2026-05-23  
**Priority:** P2 (Performance Critical)

---

## EXECUTIVE SUMMARY

This epic targets **zero-allocation hot path optimization** and **bounded latency verification** for V12's high-frequency trading engine. The goal is to eliminate all heap allocations in critical execution paths (OnBarUpdate, OnMarketData, ProcessOnOrderUpdate) and establish microsecond-level latency guarantees aligned with Jane Street HFT standards.

**Target Outcome:** Sub-100μs p99 latency for order execution paths with zero GC pressure during active trading.

---

## SCOPE DEFINITION

### In-Scope

1. **Hot Path Identification**
   - OnBarUpdate() - Primary bar processing (309 lines, CYC unknown)
   - OnMarketData() - Tick-level processing (23 lines, minimal complexity)
   - ProcessOnOrderUpdate() - Order state machine (45 lines, CYC 21, hotspot score 72.1)
   - ProcessIpcCommands() - Real-time command processing
   - ManageTrailingStops() - Position management hot loop

2. **Allocation Sources**
   - `string.Format()` calls (30+ instances found in src/)
   - `new Dictionary<>()` / `new List<>()` instantiations (20+ instances)
   - `StringBuilder` allocations in serialization paths
   - LINQ `.ToList()` / `.ToArray()` operations
   - Implicit boxing in logging/telemetry

3. **Latency Verification**
   - Establish baseline p50/p95/p99 latency metrics
   - Implement microsecond-precision instrumentation
   - Create stress test harness for 10k ticks/sec load
   - Verify <100μs p99 for order execution path

4. **Jane Street Alignment**
   - Apply zero-allocation patterns from `docs/intel/jane-street/`
   - Implement object pooling for hot-path structs
   - Use `Span<T>` and `stackalloc` for temporary buffers
   - Replace `string.Format()` with interpolated strings or pre-allocated buffers

### Out-of-Scope

- Cold paths (startup, configuration, UI rendering)
- Non-critical logging (debug/trace level)
- Historical data processing
- Compliance reporting (already throttled)

---

## CURRENT STATE ANALYSIS

### Hot Path Inventory

**Critical Methods (from hotspot analysis):**

1. **ProcessOnOrderUpdate** (CYC 21, hotspot 72.1)
   - File: `src/V12_002.Orders.Callbacks.cs:159-203`
   - Churn: 30 commits in 90 days
   - Issues: Order state machine with multiple allocations

2. **MonitorRmaProximity** (CYC 32, hotspot 95.9)
   - File: `src/V12_002.Entries.RMA.cs:262`
   - Highest complexity in codebase
   - Likely allocation-heavy due to proximity calculations

3. **OnBarUpdate** (CYC unknown, 309 lines)
   - File: `src/V12_002.BarUpdate.cs:206-303`
   - 6x `string.Format()` calls found
   - Processes every bar tick

4. **OnMarketData** (CYC low, 23 lines)
   - File: `src/V12_002.Lifecycle.cs:787-809`
   - Minimal complexity but called on EVERY tick
   - Rate-gated UI snapshot (every 5 ticks)

### Allocation Hotspots (from search_text)

**string.Format() Usage:**
- `src/V12_002.BarUpdate.cs`: 6 instances (lines 106, 126, 141, 163, 165)
- `src/Services/StickyStateService.cs`: 12 instances (serialization path)
- `src/V12_002.Entries.FFMA.cs`: 3 instances (entry logic)
- `src/SignalBroadcaster.cs`: 1 instance (latency logging)

**Dictionary/List Allocations:**
- `src/Services/StickyStateService.cs`: 4x `new Dictionary<>()` (lines 113-116)
- `src/V12_002.UI.IPC.cs`: `BuildFleetAliasMap()` creates new Dictionary
- `src/V12_002.StickyState.cs`: Multiple dictionary instantiations in serialization

**StringBuilder Usage:**
- `src/Services/StickyStateService.cs`: Heavy StringBuilder usage in serialization
- `src/V12_002.UI.IPC.Server.cs`: Line buffer processing

### Existing Optimizations

**Already Implemented:**
- Pre-allocated `_keyCommands` dictionary (zero allocation on hot path)
- ConcurrentDictionary for O(1) lookups (_orderIdToFsmKey, symmetryFleetEntryToDispatch)
- Rate-gated UI snapshots (every 5 ticks in OnMarketData)
- Throttled DrawORBox updates (DRAW_ORBOX_THROTTLE_MS)

---

## RISK ASSESSMENT

### Technical Risks

1. **Measurement Overhead** (MEDIUM)
   - Adding instrumentation may itself introduce allocations
   - Mitigation: Use `Stopwatch` struct, avoid string concatenation in hot path

2. **Regression Risk** (HIGH)
   - Aggressive optimization may break existing logic
   - Mitigation: Comprehensive stress testing, A/B comparison with baseline

3. **Complexity Increase** (MEDIUM)
   - Object pooling adds lifecycle management complexity
   - Mitigation: Encapsulate pooling logic in dedicated classes

### Performance Risks

1. **GC Pressure** (CURRENT STATE)
   - Frequent allocations in OnBarUpdate/OnMarketData trigger Gen0 collections
   - Impact: Latency spikes during active trading

2. **Lock Contention** (RESOLVED)
   - V12 DNA mandates lock-free Actor pattern
   - No `lock()` statements found in hot paths (verified via grep)

---

## SUCCESS CRITERIA

### Quantitative Metrics

1. **Zero Allocations**
   - 0 bytes allocated per OnBarUpdate call (measured via ETW/PerfView)
   - 0 bytes allocated per OnMarketData call
   - 0 bytes allocated per ProcessOnOrderUpdate call

2. **Latency Bounds**
   - p50 < 10μs for order execution path
   - p95 < 50μs for order execution path
   - p99 < 100μs for order execution path
   - Max latency < 500μs (no outliers beyond 5x p99)

3. **Throughput**
   - Sustain 10,000 ticks/sec with <5% CPU increase
   - Zero GC pauses during 1-hour stress test

### Qualitative Criteria

1. **Code Maintainability**
   - Optimization patterns documented in inline comments
   - No increase in cyclomatic complexity (maintain CYC < 20 per method)

2. **V12 DNA Compliance**
   - ASCII-only strings (no Unicode)
   - Lock-free Actor pattern preserved
   - No `string.Format()` in hot paths

---

## DEPENDENCIES

### Internal Dependencies

- **EPIC-4-STICKY-STATE-IPC** (COMPLETE)
  - IPC hardening provides stable baseline for performance testing
  
- **REAPER-EXPANSION** (COMPLETE)
  - Safety audit ensures no regressions during optimization

### External Dependencies

- **Jane Street Knowledge Base**
  - Query `scripts/query_kb.py` for zero-allocation patterns
  - Reference: HFT latency optimization techniques

- **Benchmarking Infrastructure**
  - `benchmarks/SpscRing.Benchmarks.csproj` for ring buffer perf
  - `scripts/test_stress.ps1` for load testing

---

## CONSTRAINTS

### Hard Constraints

1. **No Breaking Changes**
   - All existing functionality must remain intact
   - F5 gate must pass after every ticket

2. **V12 DNA Mandates**
   - ASCII-only compliance (no Unicode in string literals)
   - Lock-free Actor pattern (no `lock()` statements)
   - Correctness by construction (no runtime guards for invalid states)

3. **Build Integrity**
   - `deploy-sync.ps1` must pass (hard-link sync)
   - `complexity_audit.py` must show CYC reduction or neutral
   - Zero `lock()` audit violations

### Soft Constraints

1. **Code Readability**
   - Optimization should not obscure intent
   - Use helper methods to encapsulate pooling logic

2. **Incremental Delivery**
   - Each ticket must be independently testable
   - No "big bang" refactoring

---

## OPEN QUESTIONS

1. **Baseline Latency Metrics**
   - Q: What is the current p99 latency for order execution?
   - A: Requires instrumentation (Ticket 1 deliverable)

2. **Object Pooling Strategy**
   - Q: Should we use ArrayPool<T> or custom pool implementation?
   - A: Evaluate both in Ticket 2, prefer ArrayPool for simplicity

3. **String Interpolation vs. Pre-allocated Buffers**
   - Q: Is C# string interpolation zero-allocation in .NET 6+?
   - A: Verify via BenchmarkDotNet, fallback to `Span<char>` if needed

4. **Telemetry Impact**
   - Q: Does `PublishUiSnapshot()` introduce allocations?
   - A: Profile in Ticket 1, consider batching or pooling

---

## NEXT STEPS

1. **Director Approval** (GATE 1)
   - Review this scope document
   - Confirm alignment with V12 roadmap priorities

2. **Phase 2: Planning**
   - Generate detailed analysis (`01-analysis.md`)
   - Design optimization approach (`02-approach.md`)
   - Run Sentinel audit (`02-greptile-report.md`)

3. **Phase 3: Validation**
   - Validate approach against V12 DNA
   - Identify edge cases and failure modes

4. **Phase 4: Ticket Generation**
   - Break down into surgical tickets (target: 4-6 tickets)
   - Establish dependency order
   - Estimate CYC reduction per ticket

---

## REFERENCES

- **Jane Street Intel:** `docs/intel/jane-street/` (HFT patterns)
- **V12 DNA:** `AGENTS.md` (Platinum Standard, lock-free mandate)
- **Hotspot Analysis:** jCodemunch `get_hotspots` output (25 methods, CYC 5+)
- **Allocation Scan:** `search_text` results (30+ string.Format instances)

---

**[INTAKE-GATE]**

Scope complete. Does this match your intent? Reply **YES** to proceed to Phase 2 (Planning) or provide corrections.