# SIMA Hardening: Phase 2 Architectural Analysis

**Epic**: SIMA Subgraph Hardening  
**Build Tag**: V12.002 (Build 971)  
**Analysis Date**: 2026-05-16  
**Analyst**: Bob CLI (v12-engineer)

---

## Executive Summary

This analysis examines the **80+ compound traps** identified in the V12 Photon Kernel's SIMA (Single-Instance Multi-Account) subsystem during Phase 7 forensic audit. The Red Team analysis in [`docs/arena_response2.txt`](../../arena_response2.txt) exposed **5 catastrophic architectural vulnerabilities** that cascade into systemic failure under production load:

1. **64-bit Packing Race**: FSM State + Generation must update atomically; 32-bit generations overflow in 4.9 days
2. **Callback-Only Deadlock**: 50-500ms event loss window where async partial-fills drop into the void
3. **Compound Callback Race**: Slot reuse before delayed callbacks arrive orphans orders
4. **ConcurrentDictionary Allocation Violation**: Thread-safe `_orderIdToFsmKey` violates zero-allocation mandate
5. **Missing Circuit Breaker**: No global kill switch guarantees infinite retry loops during broker disconnects

---

## 1. Subsystem Architecture Map

### 1.1 Core Components

The SIMA subsystem spans **7 partial class files** with **4 critical hot paths**:

| Component | File | LOC | Role |
|-----------|------|-----|------|
| **Dispatch Engine** | `V12_002.SIMA.Dispatch.cs` | ~750 | Entry point orchestration, fleet loop, Photon slot packing |
| **Execution Engine** | `V12_002.SIMA.Execution.cs` | ~600 | Market/bracket order submission, RMA target logic |
| **Fleet Pump** | `V12_002.SIMA.Fleet.cs` | ~450 | `PumpFleetDispatch()`, `ProcessFleetSlot()`, integrity verification |
| **Lifecycle** | `V12_002.SIMA.Lifecycle.cs` | ~300 | Account subscription, SIMA enable/disable gates |
| **Photon Ring** | `V12_002.Photon.Ring.cs` | ~80 | Lock-free SPSC ring buffer (64-slot capacity) |
| **Photon Pool** | `V12_002.Photon.Pool.cs` | ~200 | Zero-allocation Order[] pool, XorShadow integrity |
| **Core State** | `V12_002.cs` | ~700 | Shared dictionaries, FSM declarations, Actor infrastructure |

### 1.2 Critical Data Structures

**Shared Mutable State** (all accessed from strategy thread + broker callbacks):

```csharp
// Line 673-674: FSM registry (ConcurrentDictionary)
private readonly ConcurrentDictionary<string, FollowerBracketFSM> _followerBrackets;

// Line 681-682: OrderId → FSM routing (ConcurrentDictionary) 
// BUG-041: NOT thread-safe despite concurrent broker/strategy access
private readonly ConcurrentDictionary<string, string> _orderIdToFsmKey;

// Line 555: Dispatch sync barrier (ConcurrentDictionary)
private readonly ConcurrentDictionary<string, byte> _dispatchSyncPendingExpKeys;

// Line 589: Pending dispatch counter (volatile int)
private volatile int _pendingFleetDispatchCount = 0;

// Line 334-336: Photon infrastructure
private PhotonOrderPool _photonPool;                    // 64-slot Order[] pool
private SPSCRing<FleetDispatchSlot> _photonDispatchRing; // Lock-free ring
private FleetDispatchSideband[] _photonSideband;        // Parallel managed refs
```

**FSM State Machine** (FollowerBracketFSM):
- **States**: `PendingSubmit`, `Submitted`, `Accepted`, `Active`, `Replacing`, `Cancelled`
- **Critical Fields**: `EntryOrder`, `StopOrder`, `Targets[5]`, `RemainingContracts`, `OcoGroupId`
- **Thread Safety**: **NONE** - struct fields mutated without atomic guards (BUG-042)

---

## 2. Dependency Map & Coupling Analysis

### 2.1 Import Graph

**SIMA.Dispatch.cs** is the **architectural nexus**:

```
ExecuteSmartDispatchEntry (entry point)
  ├─→ Dispatch_ProcessFleetLoop
  │    ├─→ Dispatch_EnqueueFleetAccount (Photon path)
  │    │    ├─→ _photonPool.Claim()
  │    │    ├─→ _photonDispatchRing.TryEnqueue()
  │    │    └─→ _photonSideband[i] = {...}  // BUG-043: Torn writes
  │    └─→ _pendingFleetDispatches.Enqueue() (legacy path)
  │
  └─→ TriggerCustomEvent(PumpFleetDispatch)

PumpFleetDispatch (SIMA.Fleet.cs)
  ├─→ _photonDispatchRing.TryDequeue()
  ├─→ VerifyPhotonSlotIntegrity()  // BUG-004: XorShadow contradiction
  ├─→ ProcessValidPhotonSlot()
  │    └─→ ProcessFleetSlot()
  │         ├─→ InitializeFollowerBracketFSM()  // BUG-005: Non-atomic check-then-set
  │         ├─→ SubmitAndRegisterFleetOrders()
  │         │    ├─→ acct.Submit()  // BUG-046: No exception rollback
  │         │    └─→ _orderIdToFsmKey[orderId] = key  // BUG-078: Registration race
  │         └─→ _photonPool.ReleaseByIndex()  // BUG-003: Use-after-free window
  │
  └─→ TriggerCustomEvent(PumpFleetDispatch)  // BUG-002: Re-entrancy flood
```

### 2.2 Cross-File Coupling

**High Coupling (>10 references)**:
- `_followerBrackets`: 47 references across 8 files (REAPER, Orders.Callbacks, SIMA.*)
- `_orderIdToFsmKey`: 23 references across 4 files (Orders.Callbacks.*, SIMA.Fleet)
- `_dispatchSyncPendingExpKeys`: 18 references across 3 files (REAPER, MetadataGuard, SIMA.*)

**Circular Dependencies**:
- `SIMA.Dispatch` ↔ `SIMA.Fleet` (via `TriggerCustomEvent(PumpFleetDispatch)`)
- `SIMA.Fleet` ↔ `Orders.Callbacks.Propagation` (via `_orderIdToFsmKey` mutations)
- `REAPER.Audit` ↔ `SIMA.*` (via `_followerBrackets` reads for expected position calculation)

---

## 3. Blast Radius Analysis

### 3.1 Critical Method Impact

**`PumpFleetDispatch()` Blast Radius**:
- **Direct Callers**: 3 sites (`ProcessFleetSlot` finally, `VerifyPhotonSlotIntegrity` rollback, `Dispatch_EnqueueFleetAccount` pump prime)
- **Indirect Triggers**: `TriggerCustomEvent` re-entrancy (BUG-002, BUG-055)
- **Shared State Mutations**: 
  - `_photonDispatchRing` (dequeue)
  - `_photonSideband[i]` (read + clear)
  - `_photonPool` (release)
  - `_pendingFleetDispatchCount` (decrement)
  - `_followerBrackets` (add via `InitializeFollowerBracketFSM`)
  - `_orderIdToFsmKey` (add via `SubmitAndRegisterFleetOrders`)

**`ProcessFleetSlot()` Blast Radius**:
- **Callers**: 2 paths (Photon ring consumer, legacy queue consumer)
- **Exception Surface**: 4 catch blocks, 3 rollback paths
- **State Rollback Scope**: 7 dictionaries (`activePositions`, `entryOrders`, `stopOrders`, `target1-5Orders`, `_followerBrackets`)

### 3.2 Shared State Contention Hotspots

**`_orderIdToFsmKey` Contention** (BUG-041, BUG-078):
- **Writers**: Strategy thread (2 sites: `SubmitAndRegisterFleetOrders`, `Orders.Callbacks.Propagation`)
- **Readers**: Broker thread (1 site: `OnAccountOrderUpdate` callback)
- **Race Window**: OrderId registered **before** broker ACK → callback arrives **before** mapping exists
- **Impact**: 100% of follower order callbacks route through this dictionary

**`_followerBrackets` Contention** (BUG-020, BUG-058):
- **Concurrent Iteration**: `ShouldSkipFleet_RunHealthCheck` enumerates while `InitializeFollowerBracketFSM` adds
- **Mutation During Drain**: `DrainAllDispatchQueuesOnAbort` clears FSMs while `PumpFleetDispatch` creates them
- **Impact**: REAPER audit, health checks, and dispatch all touch this dictionary

---

## 4. Risk Hotspots (Prioritized by Severity)

### 4.1 P0 Critical (System Failure)

| ID | Hotspot | Bug IDs | Failure Mode | MTBF Estimate |
|----|---------|---------|--------------|---------------|
| **H1** | `_orderIdToFsmKey` non-concurrent access | BUG-041, BUG-078 | Broker callbacks read torn/missing mappings → orphaned orders | 2-4 hours under load |
| **H2** | `_photonSideband` torn writes | BUG-043 | Broker thread reads partial struct → null ref crash | 30-60 minutes |
| **H3** | Pool release before sideband clear | BUG-003, BUG-054 | Slot reused while sideband refs stale → account cross-contamination | 1-2 hours |
| **H4** | `TriggerCustomEvent` re-entrancy | BUG-002, BUG-055 | Stack overflow from recursive pump priming | 15-30 minutes under signal spam |
| **H5** | No circuit breaker | BUG-070 | Infinite retry loop on broker disconnect → OutOfMemory | Immediate on disconnect |

### 4.2 P1 High (Data Corruption)

| ID | Hotspot | Bug IDs | Failure Mode | Impact |
|----|---------|---------|--------------|--------|
| **H6** | FSM state mutation without atomics | BUG-037, BUG-042 | Race between state transitions → FSM desync | Silent position drift |
| **H7** | XorShadow zeroing contradiction | BUG-004, BUG-027 | Shadow set to 0 before recompute → false integrity failures | 5-10% dispatch drop rate |
| **H8** | `_pendingFleetDispatchCount` double-decrement | BUG-018, BUG-029 | Counter goes negative → pump stalls permanently | Dispatch freeze |
| **H9** | Dictionary growth without cleanup | BUG-023, BUG-024 | `_orderIdToFsmKey` leaks 100+ entries/day → GC pressure | Gradual performance decay |

### 4.3 P2 Medium (Performance Degradation)

| ID | Hotspot | Bug IDs | Failure Mode | Impact |
|----|---------|---------|--------------|--------|
| **H10** | `acct.Positions.ToArray()` in loop | BUG-036, BUG-062 | O(N²) allocation in health check → GC spikes | 200ms+ latency per dispatch |
| **H11** | Linear FSM search | BUG-077 | O(N) iteration over `_followerBrackets` → CPU burn | Scales poorly beyond 20 accounts |
| **H12** | Repeated dictionary lookups | BUG-073 | Same key looked up 3-5 times → cache thrashing | 10-15% CPU overhead |

---

## 5. Change Surface Area

### 5.1 Files Requiring Modification

**Core Hardening** (7 files, ~2,100 LOC):
1. `V12_002.Photon.Pool.cs` - Add generation counter to `FleetDispatchSlot` struct
2. `V12_002.SIMA.Dispatch.cs` - Implement pre-submit OrderId registration
3. `V12_002.SIMA.Fleet.cs` - Fix pool release ordering, add circuit breaker
4. `V12_002.cs` - Replace `_orderIdToFsmKey` with zero-allocation hash map
5. `V12_002.Orders.Callbacks.Propagation.cs` - Update OrderId registration sites
6. `V12_002.REAPER.Audit.cs` - Update FSM enumeration to snapshot pattern
7. `V12_002.Lifecycle.cs` - Add circuit breaker initialization

**Test Coverage** (new files):
- `SimaFleetAbaPropertyTests.cs` - FsCheck properties for ABA immunity
- `PhotonIntegrityStressTest.cs` - Concurrent slot allocation/release
- `CircuitBreakerBehaviorTests.cs` - State machine transitions

### 5.2 Backward Compatibility

**Breaking Changes**: NONE  
**API Surface**: All changes internal to `V12_002` partial class  
**Data Migration**: Existing FSMs remain compatible (generation field defaults to 0)

---

## 6. Test Coverage Gaps

### 6.1 Missing Test Scenarios

**Concurrency Tests** (0% coverage):
- ❌ Broker callback arriving before OrderId mapping exists
- ❌ Slot freed and reallocated during delayed callback
- ❌ Concurrent `_followerBrackets` enumeration + mutation
- ❌ `_photonSideband` torn read under race

**Stress Tests** (0% coverage):
- ❌ 1M ops/sec generation counter wrap-around
- ❌ 64-slot ring saturation (65+ concurrent dispatches)
- ❌ Broker disconnect during active dispatch queue
- ❌ 100+ FSMs with linear search performance

**Property Tests** (0% coverage):
- ❌ ABA immunity: `(gen1, slot1) != (gen2, slot1)` for all reuse cycles
- ❌ Ordering invariant: `OrderId registered → callback routable`
- ❌ Cleanup invariant: `Pool released → sideband cleared`

---

## 7. Forensic Evidence Cross-Reference

### 7.1 Bug Registry Mapping

**Compound Trap #1 (64-bit Packing Race)**:
- BUG-005: Non-atomic FSM creation
- BUG-037: Unprotected FSM state mutation
- BUG-042: Torn read on `FollowerBracketFSM.EntryOrder`

**Compound Trap #2 (Callback-Only Deadlock)**:
- BUG-015: Async ID mapping failure
- BUG-078: OrderId registration race
- BUG-088: Null pool reference risk

**Compound Trap #3 (Compound Callback Race)**:
- BUG-003: Use-after-free window
- BUG-054: Pool release before sideband clear
- BUG-080: ABA / stale sideband read

**Compound Trap #4 (Allocation Violation)**:
- BUG-023: Unbounded `_orderIdToFsmKey` growth
- BUG-024: Incomplete rollback orphans dictionary entries
- BUG-041: Non-concurrent dictionary access

**Compound Trap #5 (Missing Circuit Breaker)**:
- BUG-046: `acct.Submit()` lacks exception rollback
- BUG-070: Missing submit circuit breaker
- BUG-033: Silent pump failure

### 7.2 Red Team Architectural Designs

The forensic evidence in [`docs/arena_response2.txt`](../../arena_response2.txt) contains **3 independent architectural repair designs** from GPT-5.3 Codex, GPT-5.2 Codex, and Qwen 3.6 Max. All three converge on:

1. **64-bit Packed State**: `(State: 8 bits | Pending: 1 bit | Generation: 55 bits)`
2. **Pre-Submit Registration**: Publish `Pending=true` state before broker dispatch
3. **Zero-Allocation Hash Map**: Fixed-size open-addressing table with FNV-1a hash
4. **Circuit Breaker FSM**: `Closed → HalfOpen → Open` with failure threshold + cooldown

---

## 8. Recommendations

### 8.1 Immediate Actions (P0)

1. **Halt Production Deployment** until circuit breaker implemented (BUG-070)
2. **Emergency Patch**: Add `ConcurrentDictionary` to `_orderIdToFsmKey` (BUG-041) - violates DNA but prevents orphaned orders
3. **Monitoring**: Add telemetry for `_photonCrcFailures`, `_pendingFleetDispatchCount`, circuit breaker state

### 8.2 Phase 2 Scope

**IN SCOPE**:
- Implement 64-bit packed FSM state with generation counter
- Add pre-submit OrderId registration lifecycle
- Replace `_orderIdToFsmKey` with zero-allocation hash map
- Implement global submit circuit breaker
- Fix pool release ordering (sideband clear before release)
- Add FsCheck property tests for ABA immunity

**OUT OF SCOPE** (defer to Phase 3):
- General performance optimizations (H10-H12)
- UI/Frontend modifications
- REAPER audit refactoring (separate epic)

---

## 9. Success Criteria

**Functional**:
- ✅ Zero orphaned orders under 1M ops/sec stress test
- ✅ Zero ABA failures across 10M slot reuse cycles
- ✅ Circuit breaker halts submissions within 100ms of broker disconnect
- ✅ All 80 bugs in registry resolved or mitigated

**Performance**:
- ✅ Dispatch latency < 5ms (p99) under 12-account fleet
- ✅ Zero GC allocations in hot path (verified via ETW trace)
- ✅ Ring saturation handled gracefully (fallback to legacy queue)

**DNA Compliance**:
- ✅ Zero `lock(stateLock)` statements added
- ✅ Zero heap allocations in `PumpFleetDispatch` → `ProcessFleetSlot` path
- ✅ ASCII-only string literals (verified via `check_ascii.py`)

---

**Next Step**: Proceed to [`02-approach.md`](./02-approach.md) for architectural design decisions and implementation strategy.