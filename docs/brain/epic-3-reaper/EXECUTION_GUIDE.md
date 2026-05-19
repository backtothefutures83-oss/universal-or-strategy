---
# EPIC-3-REAPER: EXECUTION GUIDE
# Epic: REAPER & Lifecycle Defenses
# Total Tickets: 7
# Estimated Total Time: 2 hours
---

## 1. Executive Summary

This guide orchestrates the execution of 7 concurrency hardening tickets for the REAPER audit system and kernel lifecycle sequences. All tickets are independent except Ticket 07 which should run after Ticket 05 for correct teardown sequence context.

**Epic Scope:**
- 7 validated defects (H13-H20) + Watchdog hardening
- 4 files modified
- ~30 lines changed (surgical edits only)
- +8-10 CYC (H20 cleanup loop only)
- Zero architectural changes

---

## 2. Ticket Dependency Matrix

```
Ticket 01 (H13) ──┐
Ticket 02 (H14) ──┤
Ticket 03 (H15) ──┼──> All Independent (can run in any order)
Ticket 04 (H16) ──┤
Ticket 06 (H18) ──┘

Ticket 05 (H17) ──> Must run BEFORE Ticket 07
                    (Ticket 07 assumes reordered teardown sequence)

Ticket 07 (H20+Watchdog) ──> Must run AFTER Ticket 05
```

**Recommended Execution Order:**
1. Ticket 01 (H13) - 5 min
2. Ticket 02 (H14) - 5 min
3. Ticket 03 (H15) - 5 min
4. Ticket 04 (H16) - 15 min (3 sites)
5. Ticket 05 (H17) - 20 min (2 files, 5 edits)
6. Ticket 06 (H18) - 30 min (6 methods)
7. Ticket 07 (H20+Watchdog) - 25 min (2 files, 4 edits)

**Total Estimated Time:** 105 minutes (~2 hours)

---

## 3. Per-Ticket Execution Protocol

For each ticket, follow this exact sequence:

### Phase A: Planning (v12-engineer mode)
1. Read ticket file completely
2. Write extraction plan with:
   - Sub-method names and signatures (if applicable)
   - Caller impact analysis
   - CYC before/after estimate
3. STOP at [TICKET-GATE]
4. Wait for Director approval

### Phase B: Execution (v12-engineer mode)
1. Execute approved plan (ticket Steps 4 AND 5)
2. Run full post-edit DNA audit:
   - `powershell -File .\deploy-sync.ps1` (hard-link sync + ASCII gate)
   - `grep -r "lock(" src/` (lock regression check)
   - `grep -Prn "[^\x00-\x7F]" src/` (Unicode regression check)
   - For Ticket 07 only: `python scripts/complexity_audit.py` (CYC verification)
3. Emit [SELF-AUDIT-DONE] when all audits pass
4. HALT (orchestrator dispatches independent verification)

### Phase C: Independent Verification (Advanced mode)
1. Re-run ALL gates from clean context:
   - `powershell -File .\deploy-sync.ps1`
   - `grep -r "lock(" src/`
   - `grep -Prn "[^\x00-\x7F]" src/`
   - CYC verification (Ticket 07 only)
2. Report OVERALL: PASS or FAIL
3. If FAIL: HALT, report to Director
4. If PASS: proceed to F5 gate

### Phase D: F5 Gate (Director manual action)
1. Press F5 in NinjaTrader IDE
2. Verify BUILD_TAG banner appears
3. Type: `F5 done [BUILD_TAG]`

### Phase E: Auto-Commit (Advanced mode)
1. `git add -A`
2. `git commit -m "[epic-3-reaper] ticket-XX: [description] -- [BUILD_TAG]"`
3. Report commit hash

---

## 4. Ticket Summaries

### Ticket 01: H13 - Naked Position Audit Snapshot
**File:** `V12_002.REAPER.Audit.cs` (line 522)  
**Change:** Add `.ToArray()` snapshot before `.Any()` call  
**Risk:** LOW (proven pattern)  
**CYC Delta:** 0

### Ticket 02: H14 - Flatten Cancel Loop Snapshot
**File:** `V12_002.REAPER.Audit.cs` (line 698)  
**Change:** Add `.ToArray()` snapshot before `foreach`  
**Risk:** LOW (critical path hardening)  
**CYC Delta:** 0

### Ticket 03: H15 - Flatten Close Loop Snapshot
**File:** `V12_002.REAPER.Audit.cs` (line 720)  
**Change:** Add `.ToArray()` snapshot before `foreach`  
**Risk:** LOW (critical path hardening)  
**CYC Delta:** 0

### Ticket 04: H16 - Atomic TryAdd (3 Sites)
**File:** `V12_002.REAPER.Audit.cs` (lines 337, 420, 622)  
**Change:** Replace `ContainsKey` + `TryAdd` with atomic `if (!TryAdd(...))`  
**Risk:** LOW (proven pattern from line 367)  
**CYC Delta:** 0  
**Note:** Site 1 (line 337) includes `TryRemove` for `hasWorkingEntry` case

### Ticket 05: H17 - Teardown Reorder + Termination Guards
**Files:** `V12_002.Lifecycle.cs` + `V12_002.REAPER.Audit.cs`  
**Changes:**
- Reorder shutdown: Stop intake → Drain → Cancel
- Add `if (_isTerminating) return false;` to 4 REAPER enqueue methods  
**Risk:** MEDIUM (lifecycle ordering change)  
**CYC Delta:** 0

### Ticket 06: H18 - StickyState Complete Snapshot
**File:** `V12_002.StickyState.cs` (6 methods)  
**Change:** Snapshot ALL mutable state (dictionaries + header config) before background serialization  
**Risk:** LOW (snapshot pattern)  
**CYC Delta:** 0

### Ticket 07: H20 + Watchdog Snapshots
**Files:** `V12_002.Lifecycle.cs` + `V12_002.Safety.Watchdog.cs`  
**Changes:**
- H20: Add follower cleanup loop on queue overflow
- Watchdog: Add `.ToArray()` snapshots at 3 sites  
**Risk:** LOW (Watchdog) + MEDIUM (H20 cleanup adds CYC)  
**CYC Delta:** +8-10 (H20 cleanup loop only)  
**Dependency:** Must run AFTER Ticket 05

---

## 5. Files Modified Summary

| File | Tickets | Total Edits | CYC Delta |
|:---|:---|:---|:---|
| `V12_002.REAPER.Audit.cs` | 01, 02, 03, 04, 05 | ~15 edits | 0 |
| `V12_002.Lifecycle.cs` | 05, 07 | ~3 edits | +8-10 |
| `V12_002.StickyState.cs` | 06 | ~6 methods | 0 |
| `V12_002.Safety.Watchdog.cs` | 07 | ~3 edits | 0 |

**Total:** 4 files, ~27 edits, +8-10 CYC

---

## 6. Cumulative Verification Gates

After ALL 7 tickets complete, run cumulative verification:

### Gate 1: Compile + Sync
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS + ASCII gate PASS

### Gate 2: Lock Regression (All Files)
```bash
grep -r "lock(" src/V12_002.REAPER.Audit.cs
grep -r "lock(" src/V12_002.Lifecycle.cs
grep -r "lock(" src/V12_002.StickyState.cs
grep -r "lock(" src/V12_002.Safety.Watchdog.cs
```
Expected: 0 matches across all files

### Gate 3: Unicode Regression (All Files)
```bash
grep -Prn "[^\x00-\x7F]" src/V12_002.REAPER.Audit.cs
grep -Prn "[^\x00-\x7F]" src/V12_002.Lifecycle.cs
grep -Prn "[^\x00-\x7F]" src/V12_002.StickyState.cs
grep -Prn "[^\x00-\x7F]" src/V12_002.Safety.Watchdog.cs
```
Expected: 0 matches across all files

### Gate 4: Ghost-Method Audit
```bash
grep -r "ClearAllEventHandlers" src/
grep -r "_globalState" src/
grep -r "_inFlightRmaEntries" src/
```
Expected: 0 matches (no banned identifiers)

### Gate 5: CYC Floor
```bash
python scripts/complexity_audit.py
```
Expected: `DrainQueuesForShutdown()` CYC < 30 (acceptable increase from H20)

### Gate 6: Final F5
Press F5 in NinjaTrader IDE after all 7 tickets complete.  
Expected: BUILD_TAG banner, strategy loads without errors

---

## 7. Success Criteria (Epic-Level)

### Functional Requirements
- ✅ H13-H15 + Watchdog: Zero `InvalidOperationException` in logs
- ✅ H16: Zero duplicate repair submissions (verify via queue logs)
- ✅ H17: Zero ghost orders after shutdown (verify via broker)
- ✅ H18: Zero config corruption (verify via .v12state integrity)
- ✅ H20: Follower cleanup triggered on overflow (verify via logs)

### Performance Requirements
- ✅ REAPER audit cycle < 10ms (allow 2x overhead from snapshots)
- ✅ Watchdog cycle < 5ms (allow 2x overhead from snapshots)
- ✅ Serialization frequency < 20/sec (50ms debounce enforced)
- ✅ Shutdown time < 5 seconds (allow 2x overhead from cleanup)

### DNA Compliance
- ✅ Zero new `lock()` statements
- ✅ Zero non-ASCII characters
- ✅ Diff size < 150,000 characters
- ✅ Zero ghost-method references

---

## 8. Rollback Strategy

### Per-Ticket Rollback
```bash
# Rollback specific ticket (replace XX with ticket number)
git checkout HEAD~1 -- src/V12_002.REAPER.Audit.cs
git checkout HEAD~1 -- src/V12_002.Lifecycle.cs
git checkout HEAD~1 -- src/V12_002.StickyState.cs
git checkout HEAD~1 -- src/V12_002.Safety.Watchdog.cs
powershell -File .\deploy-sync.ps1
```

### Full Epic Rollback
```bash
# Rollback all 7 tickets
git checkout HEAD~7 -- src/V12_002.REAPER.Audit.cs
git checkout HEAD~7 -- src/V12_002.Lifecycle.cs
git checkout HEAD~7 -- src/V12_002.StickyState.cs
git checkout HEAD~7 -- src/V12_002.Safety.Watchdog.cs
powershell -File .\deploy-sync.ps1
```

### Dependency-Aware Rollback
If rolling back Ticket 05 (H17), MUST also rollback Ticket 07 (H20):
```bash
git checkout HEAD~2 -- src/V12_002.Lifecycle.cs
git checkout HEAD~1 -- src/V12_002.Safety.Watchdog.cs
powershell -File .\deploy-sync.ps1
```

---

## 9. Stress Testing (Post-Epic)

After all 7 tickets complete and pass F5 gate:

### Stress Test 1: REAPER Audit Under Load
```bash
# Simulate 1000+ orders/sec with concurrent REAPER audit cycles
# Run for 5 minutes, verify zero InvalidOperationException
```

### Stress Test 2: Rapid Shutdown
```bash
# Start strategy, submit 100 orders, shutdown immediately
# Repeat 100 times, verify zero ghost orders
```

### Stress Test 3: Concurrent Serialization
```bash
# Send 100 rapid IPC config changes (10ms interval)
# Verify .v12state file integrity after each change
```

---

## 10. Epic Completion Checklist

- [ ] All 7 tickets executed in order
- [ ] All per-ticket F5 gates passed
- [ ] Cumulative verification gates passed
- [ ] Final F5 compile successful
- [ ] Stress tests passed
- [ ] Zero production exceptions over 24-hour soak test
- [ ] PR description generated via `generate_description_from_diff`
- [ ] Branch ready for merge

---

## 11. Commit Message Format

Each ticket commit should follow this format:
```
[epic-3-reaper] ticket-XX: [short description] -- [BUILD_TAG]

Defect: [BUG-ID]
File: [filename]
Change: [one-line summary]
CYC: [before] -> [after]
```

Example:
```
[epic-3-reaper] ticket-01: H13 naked position audit snapshot -- BUILD_1109_20260519

Defect: BUG-S4-001
File: V12_002.REAPER.Audit.cs
Change: Add .ToArray() snapshot before .Any() call (line 522)
CYC: 0 -> 0
```

---

## 12. Final PR Description

After all 7 tickets complete, generate PR description:
```bash
# Generate description from diff
git diff main..HEAD > epic-3-reaper.diff

# Use generate_description_from_diff tool
# This will create a comprehensive PR description with:
# - Summary of all changes
# - Files modified
# - CYC delta
# - Testing performed
# - DNA compliance verification
```

---

**Document Status:** READY FOR EXECUTION  
**Author:** Bob CLI (v12-engineer) via Plan Mode  
**Date:** 2026-05-19T01:06:00Z  
**Epic:** epic-3-reaper  
**Phase:** 4/6 (TICKETS)  
**Next:** Phase 5 (EXECUTION) - Switch to v12-engineer mode for ticket-by-ticket implementation

---

**[TICKETS-GATE]**

**7 tickets ready for execution:**
- Ticket 01: H13 - Naked Position Audit Snapshot (5 min)
- Ticket 02: H14 - Flatten Cancel Loop Snapshot (5 min)
- Ticket 03: H15 - Flatten Close Loop Snapshot (5 min)
- Ticket 04: H16 - Atomic TryAdd (3 sites) (15 min)
- Ticket 05: H17 - Teardown Reorder + Guards (20 min)
- Ticket 06: H18 - StickyState Complete Snapshot (30 min)
- Ticket 07: H20 + Watchdog Snapshots (25 min)

**Total Estimated Time:** 105 minutes (~2 hours)

Type **RUN** to begin execution or **ADJUST** to modify tickets.