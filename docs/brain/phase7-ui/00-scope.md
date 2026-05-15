# EPIC SCOPE: phase7-ui
**Epic ID**: phase7-ui  
**Created**: 2026-05-14  
**Protocol**: V12 Photon Kernel — Phase 7 Complexity Extraction  
**Current BUILD_TAG**: `1111.007-phase7-t16`  
**Epic Brief**: `docs/brain/phase7_complexity_epic_brief.md`

---

## Epic Mission

Extract UI subgraph complexity hotspots from the V12 Photon Kernel to achieve CYC < 20 compliance across all UI event handlers and command routers. This epic focuses on the **UI interaction layer** — panel handlers, keyboard shortcuts, and IPC command processing.

**Target Scope**: 4 UI methods with combined CYC of 161 (39 + 37 + 36 + 49)

---

## In-Scope Tickets

### T-C: AttachPanelHandlers Extraction
- **File**: `src/V12_002.UI.Panel.Handlers.cs:17`
- **Current CYC**: 39
- **Target CYC**: ≤ 5 (residual coordinator)
- **Complexity Driver**: 60+ control handler attachments in single method
- **Strategy**: Extract per-control-group attachment helpers
- **Risk**: HIGH — UI initialization, must preserve all handler wiring

### T-D: OnSyncAllClick Extraction
- **File**: `src/V12_002.UI.Panel.Handlers.cs:238`
- **Current CYC**: 37
- **Target CYC**: ≤ 5 (residual coordinator)
- **Complexity Driver**: Multi-pathway sync orchestration
- **Strategy**: Extract per-pathway sync helpers (SyncOrchestrator set)
- **Risk**: HIGH — Fleet synchronization logic, critical for multi-chart coordination

### T-F: UpdateContextualUI Extraction
- **File**: `src/V12_002.UI.Panel.Handlers.cs:427`
- **Current CYC**: 36
- **Target CYC**: ≤ 5 (residual coordinator)
- **Complexity Driver**: State-dependent UI updates across multiple modes
- **Strategy**: State Pattern — extract per-mode update methods
- **Risk**: MEDIUM — UI rendering logic, visual verification required

### T-A + T-B: Unified Command Pattern Architecture
**T-A: OnKeyDown** (CYC=49) + **T-B: ProcessIpc_MatchSymbol** (CYC=49)
- **Files**: 
  - `src/V12_002.UI.Callbacks.cs:337` (T-A)
  - `src/V12_002.UI.IPC.cs:325` (T-B)
- **Current CYC**: 49 each (98 combined)
- **Target CYC**: ≤ 5 each (residual dispatchers)
- **Complexity Driver**: Massive if/else chains for command routing
- **Strategy**: Dictionary-based Command Pattern with registry initialization
- **Risk**: CRITICAL — Both are command routers; must be designed together to prevent architectural divergence
- **Special Requirement**: Joint P3 design session (Claude ARCHITECT) before Bob execution

---

## Out-of-Scope

### Explicitly Excluded
- **T-Q1/T-Q2**: DNA compliance housekeeping (empty catch blocks, IPC polling comments) — separate epic or pre-work
- **T-E**: `ManageTrail_RunPerTradeBranches` (CYC=36) — Trailing subgraph, not UI
- **T-G**: `ExecuteSmartDispatchEntry` (CYC=22/33) — SIMA Dispatch subgraph, not UI
- **T-H**: `ValidateStopPrice` (CYC=33) — StopSync subgraph, not UI
- **MEDIUM tier** (CYC 21-29): 30+ symbols deferred until HIGH tier complete

### Rationale
This epic isolates the **UI interaction layer** to enable focused testing and validation. UI changes have high visual verification requirements (F5 in NinjaTrader) and benefit from isolation from backend logic changes.

---

## Dependencies

### Prerequisites
- **T-Q1 complete**: Empty catch blocks logged (if included in Phase 7 scope)
- **Current state**: All prior Phase 7 tickets (T2, T3, T4, T13, T14, T15, T16) complete
- **BUILD_TAG**: `1111.007-phase7-t16` verified in NinjaTrader

### Execution Order
1. **T-C** (AttachPanelHandlers) — MUST complete and F5-validate FIRST
   - Rationale: UI initialization is foundational; any regression blocks all subsequent UI testing
2. **T-D + T-F** (OnSyncAllClick + UpdateContextualUI) — Bundle in single session
   - Rationale: Same file, shared UI state context, single F5 validation pass
3. **T-A + T-B** (Unified Command Pattern) — Joint design, sequential execution
   - Rationale: Architectural coupling requires unified design to prevent divergence

### Inter-Ticket Constraints
- **T-D and T-F** depend on **T-C** F5 validation (UI must initialize correctly before testing sync/update logic)
- **T-A and T-B** require joint P3 design session BEFORE any execution
- Each ticket requires independent F5 validation in NinjaTrader before proceeding to next

---

## Success Criteria

### Quantitative
- [ ] All 4 target methods reduced to CYC ≤ 5 (residual coordinators)
- [ ] All extracted helpers CYC ≤ 19
- [ ] Zero new heap allocations on hot path
- [ ] Zero executable `lock()` statements introduced
- [ ] ASCII-only compliance in all string literals

### Qualitative
- [ ] UI panel renders identically in NinjaTrader (visual regression test)
- [ ] All keyboard shortcuts function identically (L, S, F, 1+M, 2+M, 3+M, etc.)
- [ ] All IPC commands function identically (FLATTEN, SYNC_ALL, MOVE_TARGET, etc.)
- [ ] Sync All button functions correctly across all fleet configurations
- [ ] Contextual UI updates correctly for all modes (ORB, RMA, RETEST, MOMO, FFMA, TREND)

### Process
- [ ] Each ticket: `deploy-sync.ps1` PASS before F5
- [ ] Each ticket: F5 validation in NinjaTrader with BUILD_TAG verification
- [ ] Each ticket: Independent git commit with BUILD_TAG in message
- [ ] Living Document Registry updated with all ticket entries
- [ ] `master_roadmap.md` Phase 7 section updated to reflect UI epic completion

---

## Risk Assessment

### HIGH RISK: T-C (AttachPanelHandlers)
- **Impact**: UI initialization failure blocks entire strategy
- **Mitigation**: Execute first, F5-validate before any other UI tickets
- **Rollback**: Single-ticket isolation enables clean revert

### HIGH RISK: T-A + T-B (Command Pattern)
- **Impact**: Command routing regression affects all keyboard/IPC interactions
- **Mitigation**: Joint P3 design session ensures architectural consistency
- **Validation**: Comprehensive keyboard shortcut test matrix + IPC command test suite

### MEDIUM RISK: T-D + T-F (Sync + Update)
- **Impact**: Fleet sync or UI rendering issues
- **Mitigation**: Bundle in single session for atomic validation
- **Validation**: Multi-chart fleet configuration testing

---

## Architectural Context

### UI Subgraph Cluster
The 4 target methods form a cohesive **UI interaction layer**:
- **AttachPanelHandlers**: Initialization (wires all event handlers)
- **OnKeyDown**: Keyboard input router
- **ProcessIpc_MatchSymbol**: IPC command router
- **OnSyncAllClick**: Fleet synchronization orchestrator
- **UpdateContextualUI**: State-dependent rendering

### Command Pattern Target Architecture
```csharp
// Initialized once at startup:
Dictionary<Key, Action> _keyCommands;
Dictionary<string, Action<string[]>> _ipcCommands;

// OnKeyDown residual (CYC ≤ 3):
private void OnKeyDown(object sender, KeyEventArgs e)
{
    if (_keyCommands.TryGetValue(e.Key, out var cmd)) cmd();
}

// ProcessIpc_MatchSymbol residual (CYC ≤ 3):
private bool ProcessIpc_MatchSymbol(string action, string[] parts)
{
    if (_ipcCommands.TryGetValue(action, out var cmd)) { cmd(parts); return true; }
    return false;
}
```

### V12 DNA Compliance
All extractions must preserve:
- **Lock-Free**: Zero `lock()` statements (UI runs on NT UI thread, no concurrency)
- **ASCII-Only**: All `Print()` calls and string literals ASCII-only
- **Zero-Allocation**: No new heap allocations in hot paths (keyboard/IPC are hot)
- **Photon Publish Triple**: Preserve sideband → MemoryBarrier → TryEnqueue pattern

---

## Verification Protocol

### Per-Ticket Checklist
1. **Pre-execution**: Run `python scripts/complexity_audit.py` to establish CYC baseline
2. **Post-extraction**: Verify residual CYC ≤ 5, all helpers CYC ≤ 19
3. **ASCII audit**: `grep -r "lock(" src/` returns 0 matches
4. **Deploy sync**: `powershell -File .\deploy-sync.ps1` exits 0, ASCII gate PASS
5. **F5 validation**: Press F5 in NinjaTrader, verify BUILD_TAG banner
6. **Functional test**: Execute test matrix for affected UI components
7. **Commit**: `git commit -m "[phase7-ui] ticket-XX: [description] -- CYC [before]->[after] [BUILD_TAG]"`

### UI Test Matrix (per ticket)
- **T-C**: All panel controls render, all buttons clickable, no null reference exceptions
- **T-D**: Sync All button functions for 1-chart, 2-chart, 3-chart fleet configurations
- **T-F**: Switch between all 6 modes (ORB, RMA, RETEST, MOMO, FFMA, TREND), verify UI updates
- **T-A**: Test all keyboard shortcuts (L, S, F, 1+M, 2+M, 3+M, 1+O, 2+O, 3+O, etc.)
- **T-B**: Test all IPC commands (FLATTEN, SYNC_ALL, MOVE_TARGET, LOCK_50, SET_TARGETS, etc.)

---

## Epic Completion Definition

**DONE** when:
1. All 4 tickets (T-C, T-D, T-F, T-A, T-B) Director-accepted
2. Zero C# symbols in UI files with CYC ≥ 30
3. All changes live in NinjaTrader (F5 verified per ticket)
4. Living Document Registry updated
5. `master_roadmap.md` Phase 7 UI section marked COMPLETE

**NOT DONE** if:
- Any ticket fails F5 validation
- Any keyboard shortcut or IPC command regresses
- Any UI rendering issue observed
- Any `lock()` statement introduced
- Any Unicode/non-ASCII string literal introduced

---

## Agent Assignments

| Agent | Role | Tickets |
|:---|:---|:---|
| **Antigravity** (Orchestrator) | Epic coordination, handoffs, acceptance gates | All |
| **Claude ARCHITECT** (P3) | Joint design session for Command Pattern | T-A + T-B design |
| **Bob CLI** (v12-engineer) | Surgical extraction execution | T-C, T-D, T-F, T-A (exec), T-B (exec) |
| **Advanced Mode** | Verification (deploy-sync, complexity audit, lock audit) | All tickets |

---

## References

- **Epic Brief**: `docs/brain/phase7_complexity_epic_brief.md`
- **Complexity Audit**: `docs/brain/complexity_audit_cyc20_report.md`
- **Living Document Registry**: `docs/brain/Living_Document_Registry.md`
- **Master Roadmap**: `docs/brain/master_roadmap.md`
- **V12 DNA Protocol**: `AGENTS.md` Section 2

---

[INTAKE-GATE]