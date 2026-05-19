---
# EPIC-4-SIGNAL: INTAKE SCOPE DOCUMENT
# Epic: Signal & State Decoupling
# Phase: INTAKE (Phase 1 of 6)
# Status: DRAFT - AWAITING GATE 1 APPROVAL
---

## 1. Epic Identity

**Epic Slug:** `epic-4-signal`  
**Epic Title:** Signal & State Decoupling -- S6/S7 Concurrency Hardening  
**Epic Type:** Concurrency Hardening (Signal Synchronization + FSM State Cleanup)  
**Target Clusters:** S6 (Signals & Entries), S7 (Infrastructure)  
**Build Tag Prefix:** `BUILD-SIGNAL-`  
**Priority:** HIGH (3 validated defects, H21/H22 pre-implemented in Epic 1)

---

## 2. Mission Statement

Eliminate the remaining S6 and S7 concurrency defects identified in the V12 Photon Kernel Bug Bounty Grid Sweep. This epic surgically hardens signal flag synchronization, shared state mutation patterns, and FSM state cleanup to achieve complete signal-to-execution decoupling.

**Core Objective:** Achieve 100% atomic signal flag operations and guarantee complete FSM state cleanup on order lifecycle termination.

**IMPORTANT NOTE:** Per the epic-run command and master_roadmap.md, **H21 and H22 are already implemented in Epic 1 DNA**. This epic focuses ONLY on the remaining 3 tickets: H23, H24, and H26.

---

## 3. Defect Catalog (3 Active Targets)

### S6 Cluster: Signals & Entries (2 defects)

#### H23: BUG-S6-003 - Race Condition on Shared Sync Flags
- **Severity:** High
- **Location:** `src/V12_002.Entries.OR.cs::ExecuteLong` / `ExecuteShort`
- **Root Cause:** Opening range arm flags (`isLongArmed` / `isShortArmed`) are read and mutated directly by multiple market data threads without synchronization. Can result in both directions being armed simultaneously under extreme market volatility.
- **V12 DNA Violation:** Unsafe Flag Mutation
- **Impact:** Dual-direction entry signals can trigger conflicting orders, violating single-direction OR strategy logic
- **Repair Strategy:** Protect arm flag state transitions using atomic integer comparisons (`Interlocked.Exchange`)

#### H24: BUG-S6-004 - Unsafe Direct Mutation of PositionInfo
- **Severity:** High
- **Location:** `src/V12_002.Entries.RMA.cs::MonitorRmaProximity`
- **Root Cause:** Entry module directly mutates `PositionInfo` structure of active positions to update tracking values. Since `PositionInfo` is shared with UI and audit threads, this un-synchronized mutation causes race conditions and corrupt tracking UI data.
- **V12 DNA Violation:** Direct Shared State Mutation
- **Impact:** UI displays incorrect position tracking data, audit threads read torn/inconsistent state
- **Repair Strategy:** Wrap all `PositionInfo` updates in FSM/Actor `Enqueue` model

### S7 Cluster: Infrastructure (1 defect)

#### H26: BUG-S7-002 - FSM Leak in FollowerBracketFSM Removal
- **Severity:** Medium (upgraded from Low due to long-running session impact)
- **Location:** `src/V12_002.Symmetry.BracketFSM.cs::RemoveFsmOrderIdMappings`
- **Root Cause:** When follower order is cancelled or filled, order ID mappings are removed, but key FSM configuration states are left in tracking dictionary. Under long-running sessions, residual tracking state leaks memory and degrades performance.
- **V12 DNA Violation:** FSM State Leak / Memory Leak
- **Impact:** Memory bloat over multi-day sessions, eventual performance degradation
- **Repair Strategy:** Thoroughly clean up all tracking, metadata, and FSM config mappings within `RemoveFsmOrderIdMappings` when order reaches terminal status

### Pre-Implemented (Epic 1 DNA - SKIP)

#### H21: BUG-S6-001 - Add vs Remove Race in Retest Rollback (Auto)
- **Status:** ✅ ALREADY IMPLEMENTED in Epic 1
- **Location:** `src/V12_002.Entries.Retest.cs::ExecuteRetestEntry`
- **Note:** Synchronous atomic enum status synchronization already applied in Epic 1 DNA repairs

#### H22: BUG-S6-001b - Add vs Remove Race in Retest Rollback (Manual)
- **Status:** ✅ ALREADY IMPLEMENTED in Epic 1
- **Location:** `src/V12_002.Entries.Retest.cs::ExecuteRetestManualEntry`
- **Note:** Synchronous atomic enum status synchronization already applied in Epic 1 DNA repairs

---

## 4. Scope Boundaries

### IN SCOPE

**Files to Modify:**
1. `src/V12_002.Entries.OR.cs` (H23)
2. `src/V12_002.Entries.RMA.cs` (H24)
3. `src/V12_002.Symmetry.BracketFSM.cs` (H26)

**Surgical Changes:**
- Replace direct boolean flag mutations with `Interlocked.Exchange` on integer representations (H23)
- Refactor `PositionInfo` mutations to use FSM `Enqueue` pattern (H24)
- Add comprehensive FSM state cleanup in `RemoveFsmOrderIdMappings` (H26)

**Testing Requirements:**
- Unit tests for each defect (3 red-to-green test cases)
- Stress tests for concurrent signal flag operations (H23)
- Integration tests for RMA proximity tracking under UI refresh load (H24)
- Long-running session memory leak tests (H26)

### OUT OF SCOPE

**Excluded from this Epic:**
- S1/S2 defects (Epic 1: Build 981 Concurrency Hardening) - COMPLETE
- S3 defects (Epic 2: Visual/Command Pipeline Hardening) - COMPLETE
- S4/S5 defects (Epic 3: REAPER & Lifecycle Defenses) - COMPLETE
- H21/H22 (already implemented in Epic 1 DNA)
- OR strategy logic modifications (only flag synchronization)
- RMA entry logic changes (only state mutation pattern)
- Bracket FSM decision logic (only cleanup completeness)

**Architectural Constraints:**
- No changes to OR arm/disarm decision logic
- No modifications to RMA proximity calculation algorithms
- No changes to bracket FSM state machine transitions
- Master account handling unchanged
- Signal routing pathways unchanged

---

## 5. Success Criteria

### Functional Requirements
1. **H23 Fixed:** OR arm flags use atomic operations (zero dual-direction arming)
2. **H24 Fixed:** PositionInfo updates enqueued through FSM (zero direct mutations)
3. **H26 Fixed:** FSM cleanup removes all tracking state (zero memory leaks)

### DNA Compliance
1. **Zero new `lock()` statements** in execution paths
2. **ASCII-only compliance** in all string literals
3. **Diff size < 150,000 characters** (DIFF GUARD pass)
4. **Zero ghost-method references** (`ClearAllEventHandlers`, `_globalState`, `_inFlightRmaEntries`)

### Test Coverage
1. 3 unit tests (one per defect) - all passing
2. Stress tests for concurrent flag operations - zero dual-arming
3. UI refresh stress tests - zero torn PositionInfo reads
4. 24-hour memory leak test - zero FSM state accumulation

### Verification Gates
1. `deploy-sync.ps1` - PASS (hard-link sync + ASCII gate)
2. `grep -r "lock(" src/` - ZERO matches
3. `grep -Prn "[^\x00-\x7F]" src/` - ZERO matches
4. Ghost-method audit - CLEAN (all 3 identifiers return zero matches)
5. F5 compile gate - BUILD_TAG banner visible in NinjaTrader

---

## 6. Risk Assessment

### Critical Risks

**Risk 1: OR Strategy False Signals (MEDIUM)**
- **Scenario:** Atomic flag operations introduce timing window where valid signals are suppressed
- **Mitigation:** Maintain existing arm/disarm logic, only change mutation primitive
- **Validation:** Replay historical OR sessions, verify identical signal counts

**Risk 2: FSM Enqueue Latency (LOW)**
- **Scenario:** Moving PositionInfo updates to actor queue introduces UI lag
- **Mitigation:** Actor queue already handles high-frequency updates (order fills, stop moves)
- **Validation:** Benchmark UI refresh rate under RMA proximity updates

**Risk 3: Incomplete FSM Cleanup (MEDIUM)**
- **Scenario:** Missing a tracking dictionary in cleanup leaves residual leak
- **Mitigation:** Comprehensive audit of all FSM-related dictionaries in BracketFSM.cs
- **Validation:** 24-hour stress test with memory profiler, verify zero growth

### Dependency Risks

**External Dependencies:**
- NinjaTrader 8 market data thread behavior (OR flag mutations)
- UI thread refresh frequency (PositionInfo reads)
- .NET GC behavior for long-running sessions (FSM leak detection)

**Internal Dependencies:**
- Actor queue implementation (`_cmdQueue`, FSM message routing)
- OR strategy state machine (arm/disarm transitions)
- RMA proximity calculation (PositionInfo update frequency)
- Bracket FSM tracking dictionaries (order ID mappings, config state)

---

## 7. Estimated Complexity

### Ticket Breakdown (Preliminary)

**Ticket 01: OR Arm Flag Atomic Operations (H23)**
- **Scope:** Replace boolean flags with `Interlocked.Exchange` on int representation
- **Estimated CYC Impact:** Zero (primitive swap, no logic change)
- **Estimated Time:** 1-2 hours
- **Risk:** LOW

**Ticket 02: PositionInfo FSM Enqueue (H24)**
- **Scope:** Refactor direct mutations to FSM `Enqueue` pattern in `MonitorRmaProximity`
- **Estimated CYC Impact:** +5-10 (enqueue wrapper, no core logic change)
- **Estimated Time:** 2-3 hours
- **Risk:** MEDIUM (requires FSM message definition)

**Ticket 03: BracketFSM Cleanup Completeness (H26)**
- **Scope:** Add comprehensive state cleanup in `RemoveFsmOrderIdMappings`
- **Estimated CYC Impact:** +10-15 (additional cleanup loops)
- **Estimated Time:** 2-3 hours
- **Risk:** MEDIUM (requires full FSM state audit)

**Ticket 04: Unit Tests**
- **Scope:** Create 3 red-to-green unit tests (one per defect)
- **Estimated Time:** 2-3 hours
- **Risk:** LOW

**Ticket 05: Integration & Stress Tests**
- **Scope:** OR signal stress tests, UI refresh tests, memory leak tests
- **Estimated Time:** 3-4 hours
- **Risk:** MEDIUM

**Total Estimated Time:** 10-15 hours (1.5-2 engineering days)

---

## 8. Open Questions for Director

### Clarification Needed

1. **H23 Scope:** Should we also audit other signal flag sites (TREND entry, manual entry) for similar TOCTOU patterns, or only fix OR arm flags?

2. **H24 FSM Message:** Should PositionInfo updates use existing FSM message types, or define new `UpdatePositionTrackingCommand`?

3. **H26 Cleanup Scope:** Should we audit ALL FSM cleanup sites (not just BracketFSM), or limit to the specific `RemoveFsmOrderIdMappings` method?

4. **Test Coverage:** Should we add memory profiler integration to CI/CD for automated leak detection, or rely on manual 24-hour stress tests?

5. **H21/H22 Verification:** Should we add explicit verification that H21/H22 fixes from Epic 1 are still present, or trust Epic 1 completion?

---

## 9. Dependencies & Prerequisites

### Required Before Start
1. Epic 1 (Build 981 Concurrency Hardening) - ✅ COMPLETE (includes H21/H22)
2. Epic 2 (Visual/Command Pipeline Hardening) - ✅ COMPLETE
3. Epic 3 (REAPER & Lifecycle Defenses) - ✅ COMPLETE
4. Current codebase at latest BUILD_TAG with zero lock() statements
5. Test harness infrastructure for stress testing

### Blocking Issues
- None identified (all prerequisites met per master_roadmap.md)

---

## 10. Handoff Checklist

### Intake Complete When:
- [x] All 3 active defects cataloged with severity, location, root cause
- [x] H21/H22 pre-implementation status confirmed
- [x] Scope boundaries clearly defined (IN/OUT)
- [x] Success criteria established (functional + DNA + testing)
- [x] Risk assessment completed with mitigations
- [x] Preliminary ticket breakdown estimated
- [x] Open questions documented for Director review
- [ ] Director approval received (GATE 1)

### Next Phase: PLAN
After GATE 1 approval, proceed to Phase 2 (PLAN) to generate:
- `01-analysis.md` - Deep forensic analysis of each defect
- `02-approach.md` - Detailed surgical repair specifications
- Mermaid diagrams for signal flow and FSM cleanup sequences

---

**[INTAKE-GATE]**

**Scope Summary:**
- **3 active defects** across S6 (Signals) and S7 (Infrastructure)
- **H21/H22 already implemented** in Epic 1 DNA (skip in this epic)
- **3 files to modify** (Entries.OR.cs, Entries.RMA.cs, Symmetry.BracketFSM.cs)
- **Estimated 10-15 hours** (1.5-2 engineering days)
- **Zero architectural changes** (pure synchronization hardening)
- **HIGH priority** (final bug bounty epic before PR merge)

**Key Decisions Required:**
1. Audit other signal flag sites beyond OR, or limit to H23 scope?
2. Define new FSM message type for PositionInfo updates, or reuse existing?
3. Expand FSM cleanup audit to all sites, or limit to BracketFSM?

**Does this scope match your intent? Reply YES to proceed to Phase 2 (PLAN) or provide corrections.**

---

**Document Status:** DRAFT - AWAITING DIRECTOR APPROVAL  
**Author:** Plan Mode via Orchestrator  
**Date:** 2026-05-19T02:20:00Z  
**Epic:** epic-4-signal  
**Phase:** 1/6 (INTAKE)