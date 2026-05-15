# ANALYSIS: phase7-ui
**Epic ID**: phase7-ui  
**Created**: 2026-05-14  
**Scope**: [`00-scope.md`](00-scope.md)

---

## Complexity Hotspot Analysis

### T-C: AttachPanelHandlers (CYC=39)
**File**: `src/V12_002.UI.Panel.Handlers.cs:17-77`  
**Type**: UI Initialization  
**LOC**: 61

#### Complexity Drivers
1. **60+ null-guarded handler attachments** (39 branches)
2. **7 control groups** requiring separate attachment logic:
   - Execution buttons (OR, RMA, RETEST, MOMO, FFMA, TREND, M)
   - Target buttons (T1-T5) with dropdown menus
   - Action buttons (TRIM_50, BE, TRAIL, CANCEL, FLATTEN)
   - Sync buttons (MKT_SYNC, SYNC_ALL)
   - Config mode buttons (6 modes)
   - Target count buttons (1-5)
   - Live target handlers (separate method call)

#### Risk Assessment
- **CRITICAL**: UI initialization failure blocks entire strategy
- **Blast Radius**: All panel controls depend on this method
- **Testing Surface**: 60+ controls must render and respond correctly
- **Rollback Complexity**: Single-method extraction enables clean revert

#### Current Architecture Issues
1. **Monolithic initialization**: All control groups wired in single method
2. **Repetitive patterns**: Each control follows `if (control != null) control.Click += handler` pattern
3. **Mixed abstraction levels**: Some handlers are inline lambdas, others are method references
4. **No grouping**: Related controls (e.g., all target buttons) not grouped logically

#### Extraction Opportunities
- **Per-control-group helpers**: `AttachExecutionPanelHandlers()`, `AttachTargetButtonHandlers()`, etc.
- **Residual coordinator**: Pure orchestrator calling 7 helper methods (CYC=2)
- **15-LOC floor**: Each helper will exceed minimum extraction size

---

### T-D: OnSyncAllClick (CYC=37)
**File**: `src/V12_002.UI.Panel.Handlers.cs:238-273`  
**LOC**: 36

#### Complexity Drivers
1. **Mode resolution logic** (3 branches)
2. **5 target type extractions** from ComboBoxes (5 ternary chains)
3. **StringBuilder assembly** with 15+ field concatenations
4. **Null-safe field access** across 10+ UI controls

#### Risk Assessment
- **HIGH**: Fleet synchronization logic, critical for multi-chart coordination
- **Blast Radius**: All fleet charts receive sync command
- **Testing Surface**: 6 modes × 5 target counts × multiple field combinations
- **Data Flow**: Reads 15+ UI fields, builds CONFIG string, sends via PanelCommand

#### Current Architecture Issues
1. **Mixed responsibilities**: Mode resolution + field extraction + string building + command dispatch
2. **Repetitive null checks**: Each field access requires null guard
3. **Magic string construction**: CONFIG protocol built inline with no abstraction
4. **No validation**: Field values not validated before sync

#### Extraction Opportunities
- **Mode resolver**: `ResolveEffectiveSyncMode()` → string
- **Field extractor**: `ExtractTargetConfiguration()` → struct/class
- **Config builder**: `BuildConfigString(mode, config)` → string
- **Residual coordinator**: Orchestrates 3 helpers + PanelCommand call (CYC=3)

---

### T-F: UpdateContextualUI (CYC=36)
**File**: `src/V12_002.UI.Panel.Handlers.cs:427-491`  
**LOC**: 65

#### Complexity Drivers
1. **Mode normalization** (2 branches)
2. **Initial collapse phase** (10 null-guarded visibility sets)
3. **Mode-specific switch** (7 cases + default)
4. **Direction combo population** (2 branches with 4 item additions)

#### Risk Assessment
- **MEDIUM**: UI rendering logic, visual verification required
- **Blast Radius**: Panel appearance changes based on mode
- **Testing Surface**: 7 modes × 2 direction combos = 14 visual states
- **Performance**: Runs on UI thread, must be fast

#### Current Architecture Issues
1. **Repetitive collapse logic**: 10 identical null-check + visibility patterns
2. **Switch statement**: 7 cases with similar structure (show specific controls)
3. **Mixed concerns**: Mode normalization + visibility management + combo population
4. **No state encapsulation**: Direct control manipulation throughout

#### Extraction Opportunities
- **State Pattern**: Per-mode update methods (`UpdateUI_ORB()`, `UpdateUI_RMA()`, etc.)
- **Collapse helper**: `CollapseAllExecutionControls()` → void
- **Combo helper**: `PopulateDirectionCombo(mode)` → void
- **Residual coordinator**: Mode normalization + helper dispatch (CYC=3)

---

### T-A: OnKeyDown (CYC=49)
**File**: `src/V12_002.UI.Callbacks.cs:337-379`  
**LOC**: 43

#### Complexity Drivers
1. **Basic hotkeys** (3 keys: L, S, F)
2. **T1 actions** (6 key combinations: 1+M, 1+O, 1+W, 1+K, 1+B, 1+C)
3. **T2 actions** (6 key combinations: 2+M, 2+O, 2+W, 2+K, 2+B, 2+C)
4. **Runner actions** (6 key combinations: 3+M, 3+O, 3+W, 3+B, 3+P, 3+D)
5. **Modifier key checks** (Keyboard.IsKeyDown for D1/D2/D3 + NumPad variants)

#### Risk Assessment
- **CRITICAL**: Command routing regression affects all keyboard interactions
- **Blast Radius**: All keyboard shortcuts (21 combinations)
- **Testing Surface**: 21 shortcuts × 2 modifier variants (D1 vs NumPad1) = 42 test cases
- **Hot Path**: Runs on every keypress in NinjaTrader

#### Current Architecture Issues
1. **Massive if/else chain**: 21 branches with nested modifier checks
2. **Repetitive patterns**: Each target level (T1/T2/Runner) has identical structure
3. **Hard-coded mappings**: Key → Action mapping embedded in control flow
4. **No extensibility**: Adding new shortcut requires editing if/else chain
5. **Duplicate logic**: T1 and T2 blocks are structurally identical

#### Extraction Opportunities
- **Command Pattern**: Dictionary<Key, Action> registry
- **Modifier-aware keys**: Composite key struct (Key + Modifiers)
- **Registry initialization**: `InitKeyCommandRegistry()` called once at startup
- **Residual dispatcher**: Dictionary lookup + invoke (CYC=2)

---

### T-B: ProcessIpc_MatchSymbol (CYC=49)
**File**: `src/V12_002.UI.IPC.cs:325-371`  
**LOC**: 47

#### Complexity Drivers
1. **Global command whitelist** (17 action checks with || chain)
2. **Symbol normalization** (3 ToUpperInvariant calls)
3. **Symbol matching logic** (11 conditions with || chain)
4. **Special case handling** (MES/ES, MYM/YM, MGC/GC micro-futures)

#### Risk Assessment
- **CRITICAL**: IPC command routing, affects all remote control operations
- **Blast Radius**: All IPC commands (FLATTEN, SYNC_ALL, MOVE_TARGET, etc.)
- **Testing Surface**: 17 global commands + 10 symbol patterns = 27 test cases
- **Hot Path**: Runs on every IPC message received

#### Current Architecture Issues
1. **Massive boolean expression**: 17-term OR chain for global commands
2. **Duplicate symbol logic**: 11-term OR chain for symbol matching
3. **Hard-coded command list**: Adding new global command requires editing boolean
4. **No abstraction**: Symbol matching logic embedded in routing method
5. **Mixed concerns**: Command classification + symbol matching + logging

#### Extraction Opportunities
- **Command Pattern**: Dictionary<string, Action<string[]>> registry
- **Global command set**: HashSet<string> for O(1) lookup
- **Symbol matcher**: Separate method `IsSymbolMatch(target)` → bool
- **Residual dispatcher**: Dictionary lookup + invoke (CYC=2)

---

## Architectural Coupling Analysis

### T-A + T-B: Unified Command Pattern Requirement

**Critical Insight**: Both methods are **command routers** with identical architectural problems:
- Massive if/else or boolean chains
- Hard-coded command mappings
- No extensibility
- Duplicate patterns

**Risk of Independent Design**:
1. **Divergent architectures**: T-A uses Dictionary, T-B uses switch → maintenance burden
2. **Inconsistent patterns**: Future commands added differently in each router
3. **Code duplication**: Similar registry initialization logic duplicated

**Unified Architecture Benefits**:
1. **Single pattern**: Both routers use Dictionary-based Command Pattern
2. **Consistent extensibility**: New commands added identically (one registry line)
3. **Shared infrastructure**: Common registry initialization pattern
4. **Reduced cognitive load**: Developers learn one pattern, apply everywhere

**Design Session Requirement**:
- **P3 Claude ARCHITECT** must design both T-A and T-B together
- Output: Single `implementation_plan.md` covering both tickets
- Execution: Bob implements T-A first (validate), then T-B (validate)

---

## Dependency Graph

```
T-C (AttachPanelHandlers)
  ↓ [F5 validation required]
T-D + T-F (OnSyncAllClick + UpdateContextualUI)
  ↓ [Both complete]
T-A + T-B (OnKeyDown + ProcessIpc_MatchSymbol)
  ↑ [Joint P3 design session required BEFORE execution]
```

**Critical Path**:
1. T-C must complete and F5-validate FIRST (UI init is foundational)
2. T-D + T-F can proceed after T-C validation (same file, bundle for efficiency)
3. T-A + T-B require joint design BEFORE any execution (architectural coupling)

---

## V12 DNA Compliance Audit

### Lock-Free Verification
- **Current state**: All 4 methods run on NT UI thread (single-threaded)
- **Risk**: ZERO (no concurrency, no locks possible)
- **Verification**: `grep -r "lock(" src/V12_002.UI.*` must return 0 matches

### ASCII-Only Verification
- **Current state**: All Print() calls use ASCII strings
- **Risk**: LOW (UI methods don't generate dynamic strings)
- **Verification**: `deploy-sync.ps1` ASCII gate must PASS

### Zero-Allocation Verification
- **Hot paths**: OnKeyDown (every keypress), ProcessIpc_MatchSymbol (every IPC message)
- **Risk**: MEDIUM (Dictionary lookups allocate on miss, but Command Pattern uses TryGetValue)
- **Mitigation**: Pre-allocate all dictionaries at startup, use TryGetValue (no allocation on hit)

---

## Testing Strategy

### Per-Ticket Verification Matrix

#### T-C: AttachPanelHandlers
- [ ] All 60+ controls render without null reference exceptions
- [ ] All execution buttons clickable (OR, RMA, RETEST, MOMO, FFMA, TREND, M)
- [ ] All target buttons (T1-T5) show dropdown menus on click
- [ ] All action buttons function (TRIM_50, BE, TRAIL, CANCEL, FLATTEN)
- [ ] Sync buttons respond (MKT_SYNC, SYNC_ALL)
- [ ] Config mode buttons switch modes correctly
- [ ] Target count buttons update visibility correctly

#### T-D: OnSyncAllClick
- [ ] 1-chart fleet: Sync All sends CONFIG command
- [ ] 2-chart fleet: Both charts receive sync
- [ ] 3-chart fleet: All charts receive sync
- [ ] Mode switching: ORB → RMA → RETEST → MOMO → FFMA → TREND
- [ ] Target count: 1 → 2 → 3 → 4 → 5 targets sync correctly
- [ ] Field validation: Empty fields don't crash sync

#### T-F: UpdateContextualUI
- [ ] ORB mode: OR LONG + OR SHORT buttons visible
- [ ] RMA mode: RMA button visible
- [ ] RETEST mode: Retest row visible
- [ ] MOMO mode: MOMO button visible
- [ ] FFMA mode: FFMA + FFMA Manual buttons visible, manual entry row collapsed
- [ ] TREND mode: Trend row visible
- [ ] MNL mode: M button visible
- [ ] Direction combo: ORB shows "OR LONG/OR SHORT", others show "LONG/SHORT"

#### T-A: OnKeyDown
- [ ] Basic: L (long), S (short), F (flatten)
- [ ] T1: 1+M, 1+O, 1+W, 1+K, 1+B, 1+C
- [ ] T2: 2+M, 2+O, 2+W, 2+K, 2+B, 2+C
- [ ] Runner: 3+M, 3+O, 3+W, 3+B, 3+P, 3+D
- [ ] NumPad: NumPad1+M, NumPad2+M, NumPad3+M work identically

#### T-B: ProcessIpc_MatchSymbol
- [ ] Global commands: FLATTEN, SYNC_ALL, CANCEL_ALL, MKT_SYNC
- [ ] Target commands: MOVE_TARGET|T1|1pt, LOCK_50, SET_TARGETS
- [ ] Symbol matching: MES matches ES, MYM matches YM, MGC matches GC
- [ ] Mode commands: SET_RMA_MODE|ON, SET_SIMA|ON
- [ ] Broadcast: FLATTEN|ALL, REQUEST_FLEET_STATE|ALL

---

## Risk Mitigation Strategies

### T-C: UI Initialization Failure
- **Mitigation**: Execute first, F5-validate before any other UI tickets
- **Rollback**: Single-ticket isolation enables clean revert
- **Verification**: Visual inspection of all 60+ controls in NinjaTrader

### T-D + T-F: Fleet Sync or Rendering Issues
- **Mitigation**: Bundle in single session for atomic validation
- **Rollback**: Both tickets revert together (same file)
- **Verification**: Multi-chart fleet configuration testing

### T-A + T-B: Command Routing Regression
- **Mitigation**: Joint P3 design session ensures architectural consistency
- **Rollback**: Independent tickets enable selective revert
- **Verification**: Comprehensive keyboard shortcut + IPC command test matrix

---

## Complexity Reduction Targets

| Ticket | Method | Current CYC | Target CYC | Reduction | Helpers |
|:---|:---|---:|---:|---:|---:|
| T-C | AttachPanelHandlers | 39 | ≤5 | -34 | 7 |
| T-D | OnSyncAllClick | 37 | ≤5 | -32 | 3 |
| T-F | UpdateContextualUI | 36 | ≤5 | -31 | 3 |
| T-A | OnKeyDown | 49 | ≤5 | -44 | 1 registry |
| T-B | ProcessIpc_MatchSymbol | 49 | ≤5 | -44 | 1 registry |
| **TOTAL** | **5 methods** | **210** | **≤25** | **-185** | **15+** |

**Epic Impact**: 88% complexity reduction across UI layer

---

## Open Questions for Approach Phase

1. **T-C**: Should helper methods be private or internal? (Affects testability)
2. **T-D**: Should CONFIG string building use StringBuilder or string interpolation?
3. **T-F**: Should State Pattern use switch dispatch or Dictionary<string, Action>?
4. **T-A**: Should Command Pattern support modifier keys (Shift, Ctrl, Alt)?
5. **T-B**: Should global command set be static readonly or instance field?
6. **All**: Should extracted helpers live in same file or separate partial class files?

---

[PLAN-GATE]