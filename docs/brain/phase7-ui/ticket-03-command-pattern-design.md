# TICKET T-A + T-B: Unified Command Pattern Design
**Epic**: phase7-ui  
**Priority**: CRITICAL  
**Ticket ID**: T-A + T-B (Joint Design)  
**Agent**: Claude ARCHITECT (P3 mode)  
**Estimated Sessions**: 1 design session

---

## Mission Brief

Design a unified Command Pattern architecture for two command routers:
- **T-A**: [`OnKeyDown()`](../../../src/V12_002.UI.Callbacks.cs:337) — CYC 49 → ≤5
- **T-B**: [`ProcessIpc_MatchSymbol()`](../../../src/V12_002.UI.IPC.cs:325) — CYC 49 → ≤5

**Critical Requirement**: Both methods MUST use the same architectural pattern to prevent divergence. This is a **DESIGN-ONLY** ticket. Execution happens in separate tickets (T-A execution, T-B execution).

---

## Problem Statement

### Current Architecture Issues

#### T-A: OnKeyDown (CYC=49)
- Massive if/else chain (21 branches)
- Nested modifier checks (Keyboard.IsKeyDown)
- Hard-coded key → action mappings
- Duplicate patterns (T1 and T2 blocks are structurally identical)
- No extensibility (adding new shortcut requires editing if/else chain)

#### T-B: ProcessIpc_MatchSymbol (CYC=49)
- 17-term OR chain for global commands
- 11-term OR chain for symbol matching
- Hard-coded command list
- No abstraction
- Mixed concerns (command classification + symbol matching + logging)

### Architectural Coupling
Both methods are **command routers** with identical problems. If designed independently:
- **Risk**: Divergent architectures (T-A uses Dictionary, T-B uses switch)
- **Impact**: Maintenance burden, inconsistent patterns, code duplication
- **Solution**: Unified Command Pattern designed together

---

## Design Goals

### Primary Goals
1. **Unified Pattern**: Both routers use Dictionary-based Command Pattern
2. **Extensibility**: New commands added via one-line registry entries
3. **Complexity Reduction**: Both residuals CYC ≤ 3
4. **Zero Allocation**: Pre-allocate dictionaries at startup, use TryGetValue

### Secondary Goals
5. **Testability**: Command handlers can be tested independently
6. **Maintainability**: Clear separation of concerns
7. **Performance**: O(1) lookup, no allocation on hot path

---

## Design Constraints

### V12 DNA Compliance
- **Lock-Free**: Both methods run on NT UI thread (single-threaded), no locks
- **ASCII-Only**: No new Print() calls with Unicode
- **Zero-Allocation**: Pre-allocate dictionaries, avoid closures on hot path

### Hot Path Considerations
- **OnKeyDown**: Runs on every keypress (hot path)
- **ProcessIpc_MatchSymbol**: Runs on every IPC message (hot path)
- **Implication**: Dictionary lookups must be allocation-free (use TryGetValue)

---

## Design Decisions Required

### Decision 1: Registry Initialization Location
**Question**: Where should command registries be initialized?

**Options**:
- A: In `State.cs` (OnStateChange State.DataLoaded)
- B: In `UI.Lifecycle.cs` (InitializePanel)
- C: Lazy initialization (first use)

**Recommendation**: Option B (InitializePanel)
- **Rationale**: UI-related registries belong in UI lifecycle
- **Benefit**: Clear ownership, single initialization point

### Decision 2: Modifier Key Handling (T-A)
**Question**: How should modifier keys (1+M, 2+M, 3+M) be handled?

**Options**:
- A: Composite KeyCombo struct with full Dictionary
  ```csharp
  struct KeyCombo { Key Key; bool Mod1; bool Mod2; bool Mod3; }
  Dictionary<KeyCombo, Action> _keyCommands;
  ```
- B: Hybrid approach (basic keys in Dictionary, modifiers in switch)
  ```csharp
  Dictionary<Key, Action> _keyCommands;  // Basic keys (L, S, F)
  HandleTargetAction(string target, Key key);  // T1/T2 via switch
  HandleRunnerAction(Key key);  // Runner via switch
  ```
- C: Nested dictionaries
  ```csharp
  Dictionary<Key, Dictionary<Modifiers, Action>> _keyCommands;
  ```

**Recommendation**: Option B (hybrid)
- **Rationale**: Balances simplicity and extensibility
- **Benefit**: Basic keys get O(1) lookup, modifiers use simple switch (6 cases each)
- **Tradeoff**: Not fully Dictionary-based, but avoids KeyCombo complexity

### Decision 3: Global Command Storage (T-B)
**Question**: How should global IPC commands be stored?

**Options**:
- A: HashSet<string> (O(1) lookup, mutable)
- B: static readonly HashSet<string> (O(1) lookup, immutable)
- C: Keep boolean expression (no allocation)

**Recommendation**: Option B (static readonly)
- **Rationale**: Zero allocation, O(1) lookup, immutable (thread-safe)
- **Benefit**: No runtime initialization cost, no mutation risk

### Decision 4: Symbol Matcher Extraction (T-B)
**Question**: Should symbol matching logic be extracted?

**Options**:
- A: Extract to `IsSymbolMatch()` helper
- B: Keep inline (avoid method call overhead)
- C: Extract to static utility method

**Recommendation**: Option A (instance helper)
- **Rationale**: Improves readability, negligible overhead
- **Benefit**: Residual method becomes pure router (CYC=3)

### Decision 5: Lambda Allocation (T-A)
**Question**: How to handle lambda closures in key command registry?

**Current approach**:
```csharp
[Key.L] = () => { double orStopDist = CalculateORStopDistance(); ... }
```

**Issue**: Lambda closures allocate on heap

**Options**:
- A: Use method references (no closure)
  ```csharp
  [Key.L] = ExecuteLongHotkey  // Method reference
  private void ExecuteLongHotkey() { ... }
  ```
- B: Accept lambda allocation (existing pattern)
- C: Use static methods (no closure, but less flexible)

**Recommendation**: Option B (accept allocation) with note for Bob
- **Rationale**: Existing pattern, acceptable tradeoff
- **Note**: Bob should evaluate if profiling shows impact, then consider Option A

---

## Target Architecture Specification

### T-A: OnKeyDown

#### Command Registry
```csharp
// Initialized once at startup (UI.Lifecycle.cs or State.cs):
private Dictionary<Key, Action> _keyCommands;

private void InitKeyCommandRegistry()
{
    _keyCommands = new Dictionary<Key, Action>
    {
        // Basic hotkeys
        // NOTE: Lambda closures allocate on heap. For hot-path optimization, consider
        // method references (e.g., [Key.L] = ExecuteLongHotkey) to avoid closure allocation.
        // Current approach acceptable as existing pattern, but Bob should evaluate if
        // allocation profiling shows impact.
        [Key.L] = () => { double orStopDist = CalculateORStopDistance(); int orContracts = CalculatePositionSize(orStopDist); Enqueue(ctx => ctx.ExecuteLong(orContracts)); },
        [Key.S] = () => { double orStopDist = CalculateORStopDistance(); int orContracts = CalculatePositionSize(orStopDist); Enqueue(ctx => ctx.ExecuteShort(orContracts)); },
        [Key.F] = () => FlattenAll()
    };
}
```

#### Residual Dispatcher (CYC ≤ 3)
```csharp
private void OnKeyDown(object sender, KeyEventArgs e)
{
    // Basic hotkeys (no modifiers)
    if (_keyCommands.TryGetValue(e.Key, out var cmd))
    {
        cmd();
        e.Handled = true;
        return;
    }
    
    // T1 Actions (1 + letter)
    if (Keyboard.IsKeyDown(Key.D1) || Keyboard.IsKeyDown(Key.NumPad1))
    {
        HandleTargetAction("T1", e.Key);
        e.Handled = true;
        return;
    }
    
    // T2 Actions (2 + letter)
    if (Keyboard.IsKeyDown(Key.D2) || Keyboard.IsKeyDown(Key.NumPad2))
    {
        HandleTargetAction("T2", e.Key);
        e.Handled = true;
        return;
    }
    
    // Runner Actions (3 + letter)
    if (Keyboard.IsKeyDown(Key.D3) || Keyboard.IsKeyDown(Key.NumPad3))
    {
        HandleRunnerAction(e.Key);
        e.Handled = true;
        return;
    }
}
```

#### Helper Methods (CYC ≤ 7 each)
```csharp
private void HandleTargetAction(string target, Key key)
{
    switch (key)
    {
        case Key.M: ExecuteTargetAction(target, "market"); break;
        case Key.O: ExecuteTargetAction(target, "1point"); break;
        case Key.W: ExecuteTargetAction(target, "2point"); break;
        case Key.K: ExecuteTargetAction(target, "marketprice"); break;
        case Key.B: ExecuteTargetAction(target, "breakeven"); break;
        case Key.C: ExecuteTargetAction(target, "cancel"); break;
    }
}

private void HandleRunnerAction(Key key)
{
    switch (key)
    {
        case Key.M: Enqueue(ctx => ctx.ExecuteRunnerAction("market")); break;
        case Key.O: Enqueue(ctx => ctx.ExecuteRunnerAction("stop1pt")); break;
        case Key.W: Enqueue(ctx => ctx.ExecuteRunnerAction("stop2pt")); break;
        case Key.B: Enqueue(ctx => ctx.ExecuteRunnerAction("stopbe")); break;
        case Key.P: Enqueue(ctx => ctx.ExecuteRunnerAction("lock50")); break;
        case Key.D: Enqueue(ctx => ctx.ExecuteRunnerAction("disabletrail")); break;
    }
}
```

### T-B: ProcessIpc_MatchSymbol

#### Command Registry
```csharp
// Initialized once at startup (UI.Lifecycle.cs or State.cs):
private static readonly HashSet<string> _globalIpcCommands = new HashSet<string>
{
    "TOGGLE_ACCOUNT", "SET_SIMA", "GET_FLEET", "DIAG_FLEET", "CANCEL_ALL",
    "FLATTEN", "SYNC_ALL", "MKT_SYNC", "REQUEST_FLEET_STATE", "RESET_MEMORY",
    "DIAG_IPC", "LOCK_50", "SET_TARGETS", "SET_TRAIL", "SET_CIT", "BE_CUSTOM"
};
```

#### Residual Dispatcher (CYC ≤ 3)
```csharp
private bool ProcessIpc_MatchSymbol(string action, string[] parts)
{
    string targetSymbol = parts.Length > 1 ? parts[1] : "Global";
    
    // Check global command set (O(1))
    bool isGlobalCommand = _globalIpcCommands.Contains(action) || action.StartsWith("MOVE_TARGET");
    
    // Symbol matching logic (extracted to helper)
    bool isForMe = isGlobalCommand || IsSymbolMatch(targetSymbol);
    
    Print(string.Format("V12 IPC: Received '{0}' for '{1}'. For Me? {2} (My Symbol: {3}){4}",
        action, targetSymbol, isForMe, Instrument.MasterInstrument.Name, isGlobalCommand ? " [GLOBAL CMD]" : ""));
    
    return isForMe;
}
```

#### Helper Method (CYC ≤ 12)
```csharp
private bool IsSymbolMatch(string targetSymbol)
{
    string mySym = Instrument.MasterInstrument.Name.ToUpperInvariant();
    string myFull = Instrument.FullName.ToUpperInvariant();
    string target = targetSymbol.Trim().ToUpperInvariant();
    
    return target == "GLOBAL" ||
           target == "ALL" ||
           target == "ON" || target == "OFF" ||
           target == "RMA" || target == "ORB" || target == "OR" || target == "MOMO" ||
           mySym == target ||
           mySym.StartsWith(target) ||
           target.StartsWith(mySym) ||
           myFull.Contains(target) ||
           (target == "MES" && mySym.Contains("ES")) ||
           (target == "MYM" && mySym.Contains("YM")) ||
           (target == "MGC" && mySym.Contains("GC"));
}
```

---

## Design Output Requirements

### Implementation Plan Document
Create `implementation_plan.md` covering BOTH T-A and T-B with:

1. **Architecture Overview**
   - Unified Command Pattern rationale
   - Dictionary-based dispatch for both routers
   - Registry initialization strategy

2. **T-A Detailed Design**
   - InitKeyCommandRegistry() specification
   - OnKeyDown() residual specification
   - HandleTargetAction() specification
   - HandleRunnerAction() specification
   - CYC targets for each method

3. **T-B Detailed Design**
   - _globalIpcCommands initialization
   - ProcessIpc_MatchSymbol() residual specification
   - IsSymbolMatch() specification
   - CYC targets for each method

4. **Shared Infrastructure**
   - Registry initialization location (UI.Lifecycle.cs)
   - Initialization timing (OnStateChange State.DataLoaded)
   - Error handling strategy

5. **Testing Strategy**
   - T-A: 21 keyboard shortcuts × 2 modifier variants = 42 test cases
   - T-B: 17 global commands + 10 symbol patterns = 27 test cases

6. **Acceptance Criteria**
   - T-A residual CYC ≤ 5 (target: 3)
   - T-B residual CYC ≤ 5 (target: 3)
   - All helpers CYC ≤ 19
   - Zero behavioral change
   - All test cases PASS

---

## Design Validation Checklist

### Architectural Consistency ✅
- [ ] Both routers use Dictionary-based pattern (or justified hybrid)
- [ ] Both use registry initialization at startup
- [ ] Both reduce to CYC ≤ 3 residuals
- [ ] Consistent extensibility model

### V12 DNA Compliance ✅
- [ ] Lock-free (both run on UI thread)
- [ ] ASCII-only (no new Print() with Unicode)
- [ ] Zero-allocation (pre-allocate dictionaries, use TryGetValue)

### Performance ✅
- [ ] O(1) lookup for both routers
- [ ] No allocation on hot path (or justified tradeoff)
- [ ] No unnecessary method calls

### Testability ✅
- [ ] Command handlers can be tested independently
- [ ] Clear test matrix for both routers
- [ ] Rollback strategy defined

---

## Handoff to Execution

After design approval, this ticket splits into TWO execution tickets:

### T-A Execution Ticket
- **Agent**: Bob CLI (v12-engineer)
- **Input**: implementation_plan.md (T-A section)
- **Output**: OnKeyDown extraction complete, F5-validated
- **Dependency**: T-D + T-F complete

### T-B Execution Ticket
- **Agent**: Bob CLI (v12-engineer)
- **Input**: implementation_plan.md (T-B section)
- **Output**: ProcessIpc_MatchSymbol extraction complete, F5-validated
- **Dependency**: T-A complete and F5-validated

---

## Dependencies

**Prerequisite**: T-D + T-F complete and F5-validated  
**Blocks**: T-A execution, T-B execution

---

## References

- **Epic Scope**: [`00-scope.md`](00-scope.md)
- **Analysis**: [`01-analysis.md`](01-analysis.md) (T-A and T-B sections)
- **Approach**: [`02-approach.md`](02-approach.md) (T-A + T-B section)
- **Validation**: [`03-validation.md`](03-validation.md)
- **Source Files**:
  - T-A: `src/V12_002.UI.Callbacks.cs:337-379`
  - T-B: `src/V12_002.UI.IPC.cs:325-371`

---

[DESIGN-GATE]