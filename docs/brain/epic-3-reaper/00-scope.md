---
# EPIC-3-REAPER: INTAKE SCOPE DOCUMENT
# Epic: REAPER & Lifecycle Defenses
# Phase: INTAKE (Phase 1 of 6)
# Status: DRAFT - AWAITING GATE 1 APPROVAL
---

## 1. Epic Identity

**Epic Slug:** `epic-3-reaper`  
**Epic Title:** REAPER & Lifecycle Defenses -- S4/S5 Concurrency Hardening  
**Epic Type:** Concurrency Hardening (Thread-Safety + Lifecycle Ordering)  
**Target Clusters:** S4 (REAPER Defense), S5 (Kernel State)  
**Build Tag Prefix:** `BUILD-REAPER-`  
**Priority:** HIGH (7 validated defects blocking production deployment)

---

## 2. Mission Statement

Eliminate all validated S4 and S5 concurrency defects identified in the V12 Photon Kernel Bug Bounty Grid Sweep. This epic surgically hardens the REAPER audit system and kernel lifecycle sequences against thread-safety violations, TOCTOU races, and shutdown ordering defects.

**Core Objective:** Achieve 100% thread-safe collection iteration in REAPER audit cycles and guarantee correct lifecycle ordering during teardown sequences.

---

## 3. Defect Catalog (7 Validated Targets)

### S4 Cluster: REAPER Defense (4 defects)

#### H13: BUG-S4-001 - Naked Position Audit Scans Live Account.Orders
- **Severity:** High
- **Location:** `src/V12_002.REAPER.Audit.cs::AuditMaster_HandleNakedPosition`
- **Root Cause:** Direct iteration over live `Account.Orders` collection without thread-safe snapshot. NinjaTrader updates this collection asynchronously on UI thread, causing `InvalidOperationException` when read from background audit thread.
- **V12 DNA Violation:** Multi-Threaded Collection Iteration
- **Impact:** REAPER audit crashes under high order activity, leaving positions unprotected

#### H14: BUG-S4-002 - Flatten Cancel Loop Scans Live targetAcct.Orders
- **Severity:** High
- **Location:** `src/V12_002.REAPER.Audit.cs::ProcessReaperFlatten_CancelWorkingOrders`
- **Root Cause:** Identical to H13 - iterating live account working orders list from audit thread during flatten event causes concurrent modification exceptions
- **V12 DNA Violation:** Unsafe Collection Iteration
- **Impact:** Emergency flatten operations abort mid-execution, leaving orphaned working orders

#### H15: BUG-S4-003 - Flatten Close Loop Scans Live targetAcct.Positions
- **Severity:** High
- **Location:** `src/V12_002.REAPER.Audit.cs::ProcessReaperFlatten_ClosePositions`
- **Root Cause:** Audits iterate live positions collections directly without safety snapshots
- **V12 DNA Violation:** Unsafe Collection Iteration
- **Impact:** Position closure crashes under high market activity, leaving open positions unmanaged

#### H16: BUG-S4-005 - TOCTOU in REAPER In-Flight Guards
- **Severity:** High
- **Location:** `src/V12_002.REAPER.Audit.cs::EnqueueReaperRepairCandidate`
- **Root Cause:** Double-check pattern (`ContainsKey` then `TryAdd`) on `_inFlightRepairCandidates`. Parallel audit cycles can enqueue same repair candidate twice.
- **V12 DNA Violation:** Atomic Logic Gate TOCTOU
- **Impact:** Redundant duplicate order submissions (double-fills) during repair operations

### S5 Cluster: Kernel State (3 defects)

#### H17: BUG-S5-001 - Teardown Precedes Actor Drain
- **Severity:** Critical
- **Location:** `src/V12_002.Lifecycle.cs::ShutdownUiAndServices`
- **Root Cause:** State tracking dictionaries and telemetry bridges disposed before background actor queue fully drained. Queued execution commands attempt to log to disposed bridges.
- **V12 DNA Violation:** Lifecycle Ordering Safety
- **Impact:** Unhandled exceptions during application exit, potential data loss from undrained commands

#### H18: BUG-S5-002 - StickyState Background Serialization Race
- **Severity:** High
- **Location:** `src/V12_002.StickyState.cs::MarkStickyDirty` & `SerializeStickyState`
- **Root Cause:** Background serialization task reads from `_stickyState` dictionary while main thread directly mutates values
- **V12 DNA Violation:** Shared State Serialization Race
- **Impact:** Corrupt serialized configuration files, thread crashes during state persistence

#### H20: BUG-S5-004 - Teardown Overflow Discard Drops Queue
- **Severity:** Medium
- **Location:** `src/V12_002.Lifecycle.cs::DrainQueuesForShutdown`
- **Root Cause:** When teardown encounters full queue, discards overflow work without sending cancellations or status updates to followers
- **V12 DNA Violation:** Lifecycle State Integrity
- **Impact:** Followers left with un-synchronized live states after shutdown

---

## 4. Scope Boundaries

### IN SCOPE

**Files to Modify:**
1. `src/V12_002.REAPER.Audit.cs` (H13, H14, H15, H16)
2. `src/V12_002.Lifecycle.cs` (H17, H20)
3. `src/V12_002.StickyState.cs` (H18)

**Surgical Changes:**
- Add `.ToArray()` snapshots to all NinjaTrader collection iterations in REAPER
- Replace `ContainsKey` + `TryAdd` with atomic `TryAdd` check in repair enqueue logic
- Re-order teardown sequence: stop intake → drain actor queue → dispose resources
- Clone sticky state dictionary before background serialization
- Add explicit follower state cleanup on queue overflow discard

**Testing Requirements:**
- Unit tests for each defect (7 red-to-green test cases)
- Stress tests for concurrent collection modification scenarios
- Lifecycle termination regression tests
- Integration tests for REAPER audit cycle under load

### OUT OF SCOPE

**Excluded from this Epic:**
- S1/S2 defects (Epic 1: Build 981 Concurrency Hardening)
- S3 defects (Epic 2: Visual/Command Pipeline Hardening)
- S6/S7 defects (Epic 4: Signal & State Decoupling)
- REAPER logic modifications (only thread-safety hardening)
- FSM state machine changes
- Order submission pathways (covered in Epic 1)
- UI refresh logic (covered in Epic 2)

**Architectural Constraints:**
- No changes to REAPER audit frequency or grace windows
- No modifications to emergency flatten decision logic
- No changes to actor queue capacity or overflow thresholds
- Master account handling unchanged
- FSM event routing unchanged

---

## 5. Success Criteria

### Functional Requirements
1. **H13-H15 Fixed:** All REAPER collection iterations use thread-safe snapshots
2. **H16 Fixed:** Repair candidate enqueue is atomic (zero duplicate submissions)
3. **H17 Fixed:** Teardown sequence guarantees actor drain before resource disposal
4. **H18 Fixed:** Sticky state serialization uses isolated snapshot (zero corruption)
5. **H20 Fixed:** Queue overflow discard triggers explicit follower cleanup

### DNA Compliance
1. **Zero new `lock()` statements** in execution paths
2. **ASCII-only compliance** in all string literals
3. **Diff size < 150,000 characters** (DIFF GUARD pass)
4. **Zero ghost-method references** (`ClearAllEventHandlers`, `_globalState`, `_inFlightRmaEntries`)

### Test Coverage
1. 7 unit tests (one per defect) - all passing
2. Stress tests for concurrent modification scenarios - zero crashes
3. Lifecycle termination tests - zero unhandled exceptions
4. Integration tests - REAPER audit cycle stable under 1000+ order/sec load

### Verification Gates
1. `deploy-sync.ps1` - PASS (hard-link sync + ASCII gate)
2. `grep -r "lock(" src/` - ZERO matches
3. `grep -Prn "[^\x00-\x7F]" src/` - ZERO matches
4. Ghost-method audit - CLEAN (all 3 identifiers return zero matches)
5. F5 compile gate - BUILD_TAG banner visible in NinjaTrader

---

## 6. Risk Assessment

### Critical Risks

**Risk 1: REAPER False Flatten (HIGH)**
- **Scenario:** Snapshot logic introduces timing window where position state diverges from broker truth
- **Mitigation:** Maintain existing grace windows (5 seconds for naked position, per-account fill grace)
- **Validation:** Manual testing with forced broker delays to verify grace windows still suppress false positives

**Risk 2: Actor Drain Timeout (MEDIUM)**
- **Scenario:** Reordered teardown sequence causes shutdown to hang waiting for actor drain
- **Mitigation:** Maintain existing 50-command drain limit with timeout
- **Validation:** Stress test with 1000+ queued commands, verify graceful timeout

**Risk 3: Sticky State Snapshot Overhead (LOW)**
- **Scenario:** Dictionary cloning on every dirty mark introduces performance regression
- **Mitigation:** Clone is shallow (references only), minimal overhead
- **Validation:** Benchmark serialization frequency, verify < 1ms overhead per snapshot

### Dependency Risks

**External Dependencies:**
- NinjaTrader 8 API collection behavior (Orders, Positions, Account.All)
- ThreadPool availability for background serialization
- File system I/O for sticky state persistence

**Internal Dependencies:**
- Actor queue implementation (`_cmdQueue`, `ipcCommandQueue`)
- REAPER timer infrastructure (`_reaperTimer`, audit cycle frequency)
- FSM tracking dictionaries (`_followerBrackets`, `_inFlightRepairCandidates`)

---

## 7. Estimated Complexity

### Ticket Breakdown (Preliminary)

**Ticket 01: REAPER Collection Snapshots (H13, H14, H15)**
- **Scope:** Add `.ToArray()` to 3 collection iteration sites in REAPER.Audit.cs
- **Estimated CYC Impact:** Zero (pure safety wrapper, no logic change)
- **Estimated Time:** 1-2 hours
- **Risk:** LOW

**Ticket 02: REAPER Atomic Enqueue (H16)**
- **Scope:** Replace `ContainsKey` + `TryAdd` with atomic `TryAdd` check
- **Estimated CYC Impact:** Zero (single-line change per site)
- **Estimated Time:** 1 hour
- **Risk:** LOW

**Ticket 03: Lifecycle Teardown Reorder (H17)**
- **Scope:** Re-sequence shutdown operations in `ShutdownUiAndServices`
- **Estimated CYC Impact:** Zero (reordering only, no new logic)
- **Estimated Time:** 2-3 hours
- **Risk:** MEDIUM (requires careful validation of drain completion)

**Ticket 04: StickyState Snapshot (H18)**
- **Scope:** Clone dictionary before background serialization in `MarkStickyDirty`
- **Estimated CYC Impact:** Zero (snapshot creation, no logic change)
- **Estimated Time:** 1-2 hours
- **Risk:** LOW

**Ticket 05: Queue Overflow Cleanup (H20)**
- **Scope:** Add follower state cleanup on queue discard in `DrainQueuesForShutdown`
- **Estimated CYC Impact:** +5-10 (new cleanup loop)
- **Estimated Time:** 2-3 hours
- **Risk:** MEDIUM (requires follower state tracking)

**Ticket 06: Unit Tests**
- **Scope:** Create 7 red-to-green unit tests (one per defect)
- **Estimated Time:** 3-4 hours
- **Risk:** LOW

**Ticket 07: Integration & Stress Tests**
- **Scope:** REAPER audit stress tests, lifecycle termination tests
- **Estimated Time:** 2-3 hours
- **Risk:** MEDIUM

**Total Estimated Time:** 12-18 hours (2-3 engineering days)

---

## 8. Open Questions for Director

### Clarification Needed

1. **H16 Scope:** Should we also fix the sister sites (`EnqueueReaperNakedStopCandidate`, `EnqueueReaperMasterNakedStop`) mentioned in bug_report_s4_gemini.md, or only `EnqueueReaperRepairCandidate`?

2. **H17 Drain Limit:** Should we increase the 50-command drain limit during shutdown, or maintain existing threshold?

3. **H18 Serialization Frequency:** Should we add throttling to `MarkStickyDirty` to reduce snapshot overhead, or accept current frequency?

4. **H20 Follower Cleanup:** Should queue overflow discard trigger full `CANCEL_ALL` on affected followers, or just mark them as stale?

5. **Test Coverage:** Should we add REAPER audit cycle benchmarks to track performance impact of snapshots, or rely on manual F5 testing?

6. **BUG-S4-004 (Watchdog):** The bug report mentions H14 also affects `V12_002.Safety.Watchdog.cs` (3 methods). Should we include Watchdog fixes in this epic, or defer to a separate ticket?

---

## 9. Dependencies & Prerequisites

### Required Before Start
1. Epic 1 (Build 981 Concurrency Hardening) - COMPLETE
2. Epic 2 (Visual/Command Pipeline Hardening) - COMPLETE (if H10 IPC shutdown affects H17)
3. Current codebase at latest BUILD_TAG with zero lock() statements
4. Test harness infrastructure for stress testing

### Blocking Issues
- None identified (all prerequisites met per master_roadmap.md)

---

## 10. Handoff Checklist

### Intake Complete When:
- [x] All 7 defects cataloged with severity, location, root cause
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
- Mermaid diagrams for lifecycle sequence changes

---

**[INTAKE-GATE]**

**Scope Summary:**
- **7 validated defects** across S4 (REAPER) and S5 (Kernel State)
- **3 files to modify** (REAPER.Audit.cs, Lifecycle.cs, StickyState.cs)
- **Estimated 12-18 hours** (2-3 engineering days)
- **Zero architectural changes** (pure thread-safety hardening)
- **HIGH priority** (blocking production deployment)

**Key Decisions Required:**
1. Include Watchdog fixes (BUG-S4-004) in this epic or defer?
2. Fix all 3 REAPER enqueue sites (H16) or only repair candidate?
3. Increase actor drain limit during shutdown or maintain 50-command threshold?

**Does this scope match your intent? Reply YES to proceed to Phase 2 (PLAN) or provide corrections.**

---

**Document Status:** DRAFT - AWAITING DIRECTOR APPROVAL  
**Author:** Bob CLI (v12-engineer) via Orchestrator  
**Date:** 2026-05-19T00:38:00Z  
**Epic:** epic-3-reaper  
**Phase:** 1/6 (INTAKE)