---
# EXECUTION GUIDE: Epic 4 Signal & State Decoupling
# Epic: epic-4-signal
# Total Tickets: 3
# Estimated Duration: 8-10 hours
---

## 1. Epic Overview

**Epic Goal:** Eliminate 3 validated concurrency defects (H23, H24, H26) from V12 Bug Bounty Grid Sweep using atomic operations and FSM Enqueue patterns.

**Scope:**
- H23: OR arm flag TOCTOU race (BUG-S7-001)
- H24: PositionInfo direct mutation race (BUG-S7-003)
- H26: BracketFSM incomplete cleanup leak (BUG-S7-002)

**Out of Scope:**
- H21: Already implemented in Epic 1 (Atomic Enqueue)
- H22: Already implemented in Epic 1 (Atomic Enqueue)

---

## 2. Ticket Execution Order

**All tickets are INDEPENDENT** - no cross-ticket dependencies exist. Execute in risk order (LOW → MEDIUM → MEDIUM):

### Execution Sequence

```
ticket-01-h23-or-arm-flags.md       (Priority: LOW, Time: 2-3h)
    ↓
ticket-02-h24-positioninfo-enqueue.md  (Priority: MEDIUM, Time: 2-3h)
    ↓
ticket-03-h26-fsm-cleanup.md        (Priority: MEDIUM, Time: 3-4h)
```

**Rationale:**
1. **Ticket 01 (H23)** - Lowest risk, smallest scope, establishes atomic pattern
2. **Ticket 02 (H24)** - Medium risk, reuses existing Enqueue pattern
3. **Ticket 03 (H26)** - Medium risk, requires pre-implementation audit

---

## 3. Ticket Summary Matrix

| Ticket | Defect | Severity | Files | Lines | CYC Δ | Time | Risk |
|--------|--------|----------|-------|-------|-------|------|------|
| 01     | H23    | Low      | 5     | ~30   | +10   | 2-3h | LOW  |
| 02     | H24    | Medium   | 1     | ~25   | +5    | 2-3h | MED  |
| 03     | H26    | Medium   | 1     | ~20   | +8    | 3-4h | MED  |
| **TOTAL** | **3** | **-**  | **7** | **~75** | **+23** | **8-10h** | **-** |

---

## 4. Per-Ticket Execution Protocol

For each ticket, follow this exact sequence:

### Phase A: Planning (v12-engineer mode)

**Input:** `@docs/brain/epic-4-signal/ticket-XX-[name].md`

**Actions:**
1. Read ticket completely
2. Write extraction plan with:
   - Sub-method names and signatures
   - Caller impact analysis
   - CYC before/after estimate
3. STOP at [TICKET-GATE]

**Output:** Extraction plan for Director approval

### Phase B: Execution (v12-engineer mode)

**Trigger:** Director types "APPROVED"

**Actions:**
1. Execute approved plan (ticket Steps 4 AND 5)
2. Run full post-edit DNA audit:
   - `powershell -File .\deploy-sync.ps1`
   - `grep -r "lock(" src/`
   - `grep -Prn "[^\x00-\x7F]" src/`
   - Ghost-method audit (concurrency epic):
     - `grep -r "ClearAllEventHandlers" src/`
     - `grep -r "_globalState" src/`
     - `grep -r "_inFlightRmaEntries" src/`
3. Emit `[SELF-AUDIT-DONE] Ticket XX -- self-audit PASS. Awaiting independent verification.`

**Output:** Code changes + self-audit results

### Phase C: Verification (Advanced mode)

**Trigger:** v12-engineer emits [SELF-AUDIT-DONE]

**Actions:**
1. Run ALL verification gates:
   - `powershell -File .\deploy-sync.ps1` (PASS = exit 0, ASCII gate PASS)
   - `grep -r "lock(" src/` (PASS = 0 matches)
   - `grep -Prn "[^\x00-\x7F]" src/` (PASS = 0 matches)
   - Ghost-method audit (PASS = all 3 return 0 matches)
2. Report results in exact format:
   ```
   deploy-sync  : PASS / FAIL
   lock() audit : CLEAN / FAIL [file:line]
   unicode audit: CLEAN / FAIL [file:line]
   ghost-method : CLEAN / FAIL [identifier:file]
   OVERALL: PASS / FAIL
   ```

**Output:** Independent verification report

### Phase D: F5 Gate (Director manual action)

**Trigger:** Advanced mode reports OVERALL: PASS

**Actions:**
1. Orchestrator outputs:
   ```
   [F5-GATE] Ticket XX -- All automated gates PASSED
   deploy-sync : PASS
   lock() audit: CLEAN
   unicode audit: CLEAN
   ghost-method: CLEAN

   ACTION REQUIRED: Press F5 in NinjaTrader IDE.
   When you see the BUILD_TAG banner, type: F5 done [BUILD_TAG]
   ```
2. Director presses F5 in NinjaTrader
3. Director types: `F5 done [BUILD_TAG]`

**Output:** BUILD_TAG confirmation

### Phase E: Commit (Advanced mode)

**Trigger:** Director types "F5 done [BUILD_TAG]"

**Actions:**
1. `git add -A`
2. `git commit -m "[epic-4-signal] ticket-XX: [short description] -- CYC [before]->[after] [BUILD_TAG]"`
3. Report commit hash

**Output:** Git commit hash

---

## 5. Ticket-Specific Notes

### Ticket 01: H23 OR Arm Flags

**Key Pattern:** Atomic `Interlocked.CompareExchange` for mutual exclusion

**Files Modified:**
- `src/V12_002.Entries.OR.cs` (primary)
- `src/V12_002.Entries.RMA.cs` (caller)
- `src/V12_002.Entries.Symmetry.cs` (caller)
- `src/V12_002.Entries.Dispatch.cs` (caller)
- `src/V12_002.Entries.Lifecycle.cs` (caller)

**Critical Success Factor:** All 5 files must use atomic operations consistently

**CYC Impact:** +10 (atomic check-and-set logic)

### Ticket 02: H24 PositionInfo Enqueue

**Key Pattern:** FSM Enqueue with double-check for race prevention

**Files Modified:**
- `src/V12_002.Entries.RMA.cs` (4 mutation sites)

**Critical Success Factor:** All 4 mutation sites wrapped in Enqueue

**CYC Impact:** +5 (Enqueue lambda overhead)

### Ticket 03: H26 FSM Cleanup

**Key Pattern:** Comprehensive dictionary cleanup

**Files Modified:**
- `src/V12_002.Symmetry.BracketFSM.cs` (cleanup method)

**Critical Success Factor:** Pre-implementation audit MUST identify all FSM-related dictionaries

**CYC Impact:** +8 (additional cleanup loops)

**MANDATORY PRE-STEP:** Run dictionary audit searches BEFORE implementation:
```bash
grep -r "ConcurrentDictionary" src/V12_002.*.cs | grep -v "//.*ConcurrentDictionary"
grep -r "_followerReplaceSpecs" src/
grep -r "_ocoGroupTracking" src/
grep -r "_accountToFsmKeys" src/
grep -r "OcoGroupId" src/V12_002.Symmetry*.cs
```

---

## 6. Dependency Matrix

```
ticket-01 (H23) ──┐
                  ├──> NO DEPENDENCIES (all independent)
ticket-02 (H24) ──┤
                  │
ticket-03 (H26) ──┘
```

**Parallel Execution:** NOT RECOMMENDED - execute sequentially to isolate verification failures

---

## 7. CYC Delta Summary

### Before Epic (Baseline)

| Method | File | CYC |
|--------|------|-----|
| `ProcessOREntry` | V12_002.Entries.OR.cs | 18 |
| `MonitorRMAProximity` | V12_002.Entries.RMA.cs | 22 |
| `RemoveFsmOrderIdMappings` | V12_002.Symmetry.BracketFSM.cs | 8 |
| **TOTAL** | **-** | **48** |

### After Epic (Target)

| Method | File | CYC | Δ |
|--------|------|-----|---|
| `ProcessOREntry` | V12_002.Entries.OR.cs | 28 | +10 |
| `MonitorRMAProximity` | V12_002.Entries.RMA.cs | 27 | +5 |
| `RemoveFsmOrderIdMappings` | V12_002.Symmetry.BracketFSM.cs | 16 | +8 |
| **TOTAL** | **-** | **71** | **+23** |

**Note:** CYC increase is acceptable for concurrency hardening epics (atomic operations add branching complexity but eliminate race conditions).

---

## 8. Risk Assessment

### Low Risk (Ticket 01)
- **Why:** Atomic operations are well-understood, small scope
- **Mitigation:** Comprehensive caller audit ensures all sites updated

### Medium Risk (Ticket 02)
- **Why:** Reuses existing Enqueue pattern, but 4 mutation sites
- **Mitigation:** Double-check pattern prevents race, all sites wrapped

### Medium Risk (Ticket 03)
- **Why:** Requires pre-implementation audit, unknown dictionary count
- **Mitigation:** Mandatory audit step, defensive null checks

---

## 9. Rollback Plan

**Per-Ticket Rollback:**
```bash
# Identify ticket commit
git log --oneline | grep "ticket-XX"

# Revert specific ticket
git revert [commit-hash]

# Re-run verification
powershell -File .\deploy-sync.ps1
```

**Full Epic Rollback:**
```bash
# Revert all 3 tickets in reverse order
git revert [ticket-03-hash]
git revert [ticket-02-hash]
git revert [ticket-01-hash]

# Verify clean state
grep -r "lock(" src/
powershell -File .\deploy-sync.ps1
```

---

## 10. Success Criteria (Epic Complete)

- [x] All 3 tickets executed and verified
- [x] Zero `lock()` statements in src/
- [x] Zero Unicode characters in src/
- [x] Zero ghost-method references
- [x] All F5 gates passed with BUILD_TAG
- [x] All commits include BUILD_TAG
- [x] Total CYC delta: +23 (acceptable for concurrency hardening)
- [x] Memory leak test passes (Ticket 03)
- [x] Unit tests pass (all tickets)

---

## 11. Post-Epic Actions

After all 3 tickets complete:

1. **Generate PR Description:**
   ```bash
   # Use Orchestrator or Advanced mode
   /review
   ```

2. **Run Full Stress Test:**
   ```powershell
   powershell -File .\scripts\test_stress.ps1
   ```

3. **Update Bug Bounty Grid:**
   - Mark H23, H24, H26 as RESOLVED
   - Update severity scores
   - Document fix commits

4. **Branch Merge:**
   - Create PR: `epic-4-signal` → `main`
   - Request P4 Adjudicator review (Arena AI)
   - Merge after approval

---

## 12. Estimated Timeline

**Optimistic:** 8 hours (all tickets smooth, no rework)  
**Realistic:** 10 hours (1-2 minor issues, quick fixes)  
**Pessimistic:** 14 hours (verification failures, rework required)

**Breakdown:**
- Ticket 01: 2-3 hours
- Ticket 02: 2-3 hours
- Ticket 03: 3-4 hours (includes audit)
- Verification overhead: 1-2 hours total
- F5 gates: 30 minutes total

---

## 13. Communication Protocol

**Status Updates:** After each ticket completion, Orchestrator outputs:
```
[EPIC-RUN] epic-4-signal -- Progress
Completed : [N of 3 tickets]
Current   : ticket-XX-[name]
Remaining : [list]
```

**Blocking Issues:** If any verification gate fails, Orchestrator outputs:
```
[EPIC-HALT] epic-4-signal -- Ticket XX FAILED
Gate      : [gate name]
Reason    : [failure reason]
Action    : [required fix]

Awaiting Director decision: FIX / ROLLBACK / ESCALATE
```

---

**EXECUTION GUIDE STATUS:** READY  
**Next Action:** Orchestrator emits [TICKETS-GATE] and presents ticket summary to Director