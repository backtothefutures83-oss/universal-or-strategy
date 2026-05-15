# EXECUTION GUIDE: phase7-ui Epic
**Epic ID**: phase7-ui  
**Created**: 2026-05-14  
**Total Tickets**: 5 (1 design + 4 execution)  
**Estimated CYC Reduction**: 161 → 25 (88% reduction)

---

## Epic Overview

This epic extracts 4 high-complexity UI methods to achieve CYC < 20 compliance:
- **T-C**: AttachPanelHandlers (CYC 39 → 2)
- **T-D + T-F**: OnSyncAllClick + UpdateContextualUI (CYC 37+36 → 3+4)
- **T-A**: OnKeyDown (CYC 49 → 3)
- **T-B**: ProcessIpc_MatchSymbol (CYC 49 → 3)

**Total Impact**: 210 CYC → 25 CYC (88% reduction, 185 CYC eliminated)

---

## Execution Order (STRICT DEPENDENCY CHAIN)

```
T-C (AttachPanelHandlers)
  ↓ F5 GATE
T-D + T-F (OnSyncAllClick + UpdateContextualUI) [BUNDLED]
  ↓ F5 GATE
T-A + T-B Design (Joint Architecture Specification)
  ↓ DESIGN GATE
T-A (OnKeyDown Execution)
  ↓ F5 GATE
T-B (ProcessIpc_MatchSymbol Execution)
  ↓ F5 GATE
EPIC COMPLETE
```

**Critical Rule**: Each ticket MUST complete its F5 gate before the next ticket begins. No parallel execution.

---

## Ticket Execution Instructions

### Ticket 1: T-C (AttachPanelHandlers)
**File**: [`ticket-01-attach-panel-handlers.md`](ticket-01-attach-panel-handlers.md)  
**Agent**: Bob CLI (v12-engineer mode)  
**Complexity**: CYC 39 → 2  
**Helpers**: 7 per-control-group methods

**Execution Command**:
```bash
bob /ticket @docs/brain/phase7-ui/ticket-01-attach-panel-handlers.md
```

**F5 Validation**:
1. Press F5 in NinjaTrader IDE
2. Verify BUILD_TAG banner
3. Test all 60+ panel controls (buttons, toggles, dropdowns)
4. Confirm all handlers fire correctly

**Gate Criteria**:
- [ ] Residual CYC ≤ 5 (target: 2)
- [ ] All 7 helpers CYC ≤ 19
- [ ] All 60+ controls functional
- [ ] deploy-sync.ps1 PASS
- [ ] complexity_audit.py shows CYC 39 → 2

---

### Ticket 2: T-D + T-F (OnSyncAllClick + UpdateContextualUI)
**File**: [`ticket-02-sync-and-contextual-ui.md`](ticket-02-sync-and-contextual-ui.md)  
**Agent**: Bob CLI (v12-engineer mode)  
**Complexity**: CYC 37+36 → 3+4  
**Helpers**: 6 total (3 for T-D, 3 for T-F)

**Execution Command**:
```bash
bob /ticket @docs/brain/phase7-ui/ticket-02-sync-and-contextual-ui.md
```

**F5 Validation**:
1. Press F5 in NinjaTrader IDE
2. Verify BUILD_TAG banner
3. Test "Sync All" button across all 7 modes (ORB, RMA, RETEST, MOMO, FFMA, TREND, MNL)
4. Verify CONFIG string format unchanged
5. Test mode switching (all 7 modes render correctly)

**Gate Criteria**:
- [ ] T-D residual CYC ≤ 5 (target: 3)
- [ ] T-F residual CYC ≤ 5 (target: 4)
- [ ] All 6 helpers CYC ≤ 19
- [ ] Sync All functional across all modes
- [ ] Mode switching renders correctly
- [ ] deploy-sync.ps1 PASS
- [ ] complexity_audit.py shows CYC 37+36 → 3+4

---

### Ticket 3: T-A + T-B Design (Joint Architecture)
**File**: [`ticket-03-command-pattern-design.md`](ticket-03-command-pattern-design.md)  
**Agent**: Bob CLI (v12-engineer mode)  
**Output**: Unified Command Pattern specification for both T-A and T-B

**Execution Command**:
```bash
bob /ticket @docs/brain/phase7-ui/ticket-03-command-pattern-design.md
```

**Design Gate Criteria**:
- [ ] Unified Command Pattern specified for both routers
- [ ] Dictionary-based dispatch architecture defined
- [ ] Registry initialization strategy documented
- [ ] CYC targets specified for all methods
- [ ] Test matrices defined (39 tests for T-A, 30 tests for T-B)
- [ ] V12 DNA compliance verified (lock-free, ASCII-only, zero-allocation)

**Output**: Design specification embedded in ticket-03 (no separate implementation_plan.md needed)

---

### Ticket 4: T-A (OnKeyDown Execution)
**File**: [`ticket-04-onkeydown-execution.md`](ticket-04-onkeydown-execution.md)  
**Agent**: Bob CLI (v12-engineer mode)  
**Complexity**: CYC 49 → 3  
**Helpers**: 3 (InitKeyCommandRegistry, HandleTargetAction, HandleRunnerAction)

**Execution Command**:
```bash
bob /ticket @docs/brain/phase7-ui/ticket-04-onkeydown-execution.md
```

**F5 Validation**:
1. Press F5 in NinjaTrader IDE
2. Verify BUILD_TAG banner
3. Test all 39 keyboard shortcuts:
   - 3 basic hotkeys (L, S, F)
   - 12 T1 actions (1+M, 1+O, 1+W, 1+K, 1+B, 1+C × 2 modifier variants)
   - 12 T2 actions (2+M, 2+O, 2+W, 2+K, 2+B, 2+C × 2 modifier variants)
   - 12 Runner actions (3+M, 3+O, 3+W, 3+B, 3+P, 3+D × 2 modifier variants)
4. Confirm all shortcuts function identically to pre-extraction

**Gate Criteria**:
- [ ] Residual CYC ≤ 5 (target: 3)
- [ ] All 3 helpers CYC ≤ 19
- [ ] All 39 keyboard shortcuts functional
- [ ] deploy-sync.ps1 PASS
- [ ] complexity_audit.py shows CYC 49 → 3

---

### Ticket 5: T-B (ProcessIpc_MatchSymbol Execution)
**File**: [`ticket-05-process-ipc-match-symbol.md`](ticket-05-process-ipc-match-symbol.md)  
**Agent**: Bob CLI (v12-engineer mode)  
**Complexity**: CYC 49 → 3  
**Helpers**: 1 (IsSymbolMatch) + static readonly HashSet

**Execution Command**:
```bash
bob /ticket @docs/brain/phase7-ui/ticket-05-process-ipc-match-symbol.md
```

**F5 Validation**:
1. Press F5 in NinjaTrader IDE
2. Verify BUILD_TAG banner
3. Send IPC commands from fleet master:
   - 17 global commands (SYNC_ALL, CANCEL_ALL, FLATTEN, etc.)
   - 10 symbol matching patterns (GLOBAL, ALL, ES, MES, ORB, RMA, etc.)
   - 3 edge cases (no symbol, unknown command, wrong symbol)
4. Verify "For Me?" logic matches pre-extraction behavior
5. Check Print() output format unchanged

**Gate Criteria**:
- [ ] Residual CYC ≤ 5 (target: 3)
- [ ] IsSymbolMatch CYC ≤ 19 (target: 12)
- [ ] All 30 IPC test cases pass
- [ ] deploy-sync.ps1 PASS
- [ ] complexity_audit.py shows CYC 49 → 3

---

## Epic Completion Checklist

### All Tickets Complete ✅
- [ ] T-C: AttachPanelHandlers (CYC 39 → 2)
- [ ] T-D + T-F: OnSyncAllClick + UpdateContextualUI (CYC 37+36 → 3+4)
- [ ] T-A + T-B: Joint Design approved
- [ ] T-A: OnKeyDown (CYC 49 → 3)
- [ ] T-B: ProcessIpc_MatchSymbol (CYC 49 → 3)

### DNA Audit (Final) ✅
- [ ] `powershell -File .\deploy-sync.ps1` → ALL PASS
- [ ] `python scripts/complexity_audit.py` → ALL targets below CYC 20
- [ ] `grep -r "lock(" src/` → 0 matches
- [ ] `check_ascii.py` → ALL PASS

### Behavioral Validation ✅
- [ ] All 60+ panel controls functional
- [ ] All 7 execution modes render correctly
- [ ] All 39 keyboard shortcuts functional
- [ ] All 30 IPC commands route correctly
- [ ] No exceptions in NinjaTrader Output window
- [ ] No behavioral changes detected

### Metrics ✅
- [ ] Total CYC reduction: 210 → 25 (88% reduction)
- [ ] Sub-methods added: 19 (7 + 6 + 3 + 1 + 2 helpers)
- [ ] Files modified: 3 (UI.Panel.Handlers.cs, UI.Callbacks.cs, UI.IPC.cs)
- [ ] Test coverage: 169 test cases (60 + 40 + 39 + 30)

---

## Rollback Strategy

If any ticket fails F5 validation:

### Immediate Rollback
```bash
git reset --hard HEAD~1
powershell -File .\deploy-sync.ps1
```

### Partial Rollback (Keep Previous Tickets)
```bash
# Revert only the failed ticket's commit
git revert <commit-hash>
powershell -File .\deploy-sync.ps1
```

### Full Epic Rollback
```bash
# Revert all commits in the epic
git revert <first-commit>..<last-commit>
powershell -File .\deploy-sync.ps1
```

---

## Success Criteria Summary

### Complexity Targets (ALL MUST PASS)
| Method | Before | After | Target | Status |
|--------|--------|-------|--------|--------|
| AttachPanelHandlers | 39 | 2 | ≤5 | ⏳ |
| OnSyncAllClick | 37 | 3 | ≤5 | ⏳ |
| UpdateContextualUI | 36 | 4 | ≤5 | ⏳ |
| OnKeyDown | 49 | 3 | ≤5 | ⏳ |
| ProcessIpc_MatchSymbol | 49 | 3 | ≤5 | ⏳ |
| **TOTAL** | **210** | **25** | **88% reduction** | ⏳ |

### V12 DNA Compliance (ALL MUST PASS)
- [ ] Lock-free: 0 `lock()` statements in src/
- [ ] ASCII-only: 0 Unicode violations
- [ ] Zero-allocation: No new heap allocations on hot paths (T-A, T-B)
- [ ] Behavioral preservation: 100% functional equivalence

### F5 Validation (ALL MUST PASS)
- [ ] BUILD_TAG banner appears on every F5
- [ ] All 169 test cases pass
- [ ] No exceptions in NinjaTrader Output window
- [ ] No behavioral changes detected

---

## References

- **Epic Scope**: [`00-scope.md`](00-scope.md)
- **Analysis**: [`01-analysis.md`](01-analysis.md)
- **Approach**: [`02-approach.md`](02-approach.md)
- **Validation**: [`03-validation.md`](03-validation.md)
- **Ticket 1**: [`ticket-01-attach-panel-handlers.md`](ticket-01-attach-panel-handlers.md)
- **Ticket 2**: [`ticket-02-sync-and-contextual-ui.md`](ticket-02-sync-and-contextual-ui.md)
- **Ticket 3**: [`ticket-03-command-pattern-design.md`](ticket-03-command-pattern-design.md)
- **Ticket 4**: [`ticket-04-onkeydown-execution.md`](ticket-04-onkeydown-execution.md)
- **Ticket 5**: [`ticket-05-process-ipc-match-symbol.md`](ticket-05-process-ipc-match-symbol.md)

---

## Epic Orchestrator Notes

This guide is designed for the V12 Epic Orchestrator (epic-run command). Each ticket follows the standard YOLO pipeline:
1. Switch to v12-engineer mode
2. Hand off ticket with `/ticket` command
3. Wait for [TICKET-GATE]
4. Switch to Advanced mode for verification
5. Wait for F5 gate (Director presses F5)
6. Auto-commit with BUILD_TAG
7. Advance to next ticket

**No manual intervention required except F5 validation.**

---

[EXECUTION-GUIDE-COMPLETE]