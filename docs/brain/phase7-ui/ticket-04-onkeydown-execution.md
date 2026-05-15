# TICKET T-A: OnKeyDown Extraction (Execution)
**Epic**: phase7-ui  
**Priority**: P5 (Surgical Execution)  
**Ticket ID**: T-A  
**Agent**: Bob CLI (v12-engineer mode)  
**Estimated Sessions**: 1 extraction session

---

## Mission Brief

Extract [`OnKeyDown()`](../../../src/V12_002.UI.Callbacks.cs:337) from CYC 49 → ≤5 using the unified Command Pattern architecture defined in [`ticket-03-command-pattern-design.md`](ticket-03-command-pattern-design.md).

**Critical Requirement**: This ticket MUST follow the design specification from ticket-03. Do NOT deviate from the approved architecture.

---

## Prerequisites

**MUST BE COMPLETE BEFORE STARTING**:
- [x] T-C (AttachPanelHandlers) complete and F5-validated
- [x] T-D + T-F (OnSyncAllClick + UpdateContextualUI) complete and F5-validated
- [x] T-A + T-B joint design (ticket-03) approved by Director

**Dependency Chain**: T-C → T-D+T-F → T-A+T-B Design → **T-A Execution** → T-B Execution

---

## Target Method

**File**: `src/V12_002.UI.Callbacks.cs`  
**Method**: `OnKeyDown(object sender, KeyEventArgs e)`  
**Line**: 337  
**Current CYC**: 49  
**Target CYC**: ≤5 (target: 3)

### Current Complexity Drivers
- 21-branch if/else chain
- Nested modifier checks (Keyboard.IsKeyDown)
- Hard-coded key → action mappings
- Duplicate patterns (T1 and T2 blocks structurally identical)
- No extensibility

---

## Extraction Plan

### Step 1: Create InitKeyCommandRegistry() Helper
**Location**: `src/V12_002.UI.Lifecycle.cs` (or State.cs if preferred)  
**Called From**: `OnStateChange(State.DataLoaded)` or `InitializePanel()`  
**CYC**: ~3  
**LOC**: ~25

```csharp
private Dictionary<Key, Action> _keyCommands;

private void InitKeyCommandRegistry()
{
    _keyCommands = new Dictionary<Key, Action>
    {
        // Basic hotkeys (no modifiers)
        // NOTE: Lambda closures allocate on heap. For hot-path optimization, consider
        // method references (e.g., [Key.L] = ExecuteLongHotkey) to avoid closure allocation.
        // Current approach acceptable as existing pattern, but evaluate if profiling shows impact.
        [Key.L] = () => 
        { 
            double orStopDist = CalculateORStopDistance(); 
            int orContracts = CalculatePositionSize(orStopDist); 
            Enqueue(ctx => ctx.ExecuteLong(orContracts)); 
        },
        [Key.S] = () => 
        { 
            double orStopDist = CalculateORStopDistance(); 
            int orContracts = CalculatePositionSize(orStopDist); 
            Enqueue(ctx => ctx.ExecuteShort(orContracts)); 
        },
        [Key.F] = () => FlattenAll()
    };
}
```

### Step 2: Create HandleTargetAction() Helper
**Location**: `src/V12_002.UI.Callbacks.cs` (near OnKeyDown)  
**CYC**: ~7 (6 cases + default)  
**LOC**: ~18

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
```

### Step 3: Create HandleRunnerAction() Helper
**Location**: `src/V12_002.UI.Callbacks.cs` (near OnKeyDown)  
**CYC**: ~7 (6 cases + default)  
**LOC**: ~18

```csharp
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

### Step 4: Rewrite OnKeyDown() Residual
**Target CYC**: 3  
**LOC**: ~25

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

---

## CYC Targets

| Method | Before | After | Status |
|--------|--------|-------|--------|
| OnKeyDown | 49 | ≤5 (target: 3) | ⏳ |
| InitKeyCommandRegistry | N/A | ~3 | ⏳ |
| HandleTargetAction | N/A | ~7 | ⏳ |
| HandleRunnerAction | N/A | ~7 | ⏳ |

**Total Reduction**: 49 → 20 (residual 3 + helpers 17) = **-29 CYC**

---

## Test Matrix

### Basic Hotkeys (3 tests)
- [ ] Press L → ExecuteLong() called with calculated contracts
- [ ] Press S → ExecuteShort() called with calculated contracts
- [ ] Press F → FlattenAll() called

### T1 Actions (12 tests: 6 keys × 2 modifier variants)
- [ ] Press 1+M → ExecuteTargetAction("T1", "market") called
- [ ] Press 1+O → ExecuteTargetAction("T1", "1point") called
- [ ] Press 1+W → ExecuteTargetAction("T1", "2point") called
- [ ] Press 1+K → ExecuteTargetAction("T1", "marketprice") called
- [ ] Press 1+B → ExecuteTargetAction("T1", "breakeven") called
- [ ] Press 1+C → ExecuteTargetAction("T1", "cancel") called
- [ ] Press NumPad1+M → ExecuteTargetAction("T1", "market") called (same as 1+M)
- [ ] Press NumPad1+O → ExecuteTargetAction("T1", "1point") called (same as 1+O)
- [ ] Press NumPad1+W → ExecuteTargetAction("T1", "2point") called (same as 1+W)
- [ ] Press NumPad1+K → ExecuteTargetAction("T1", "marketprice") called (same as 1+K)
- [ ] Press NumPad1+B → ExecuteTargetAction("T1", "breakeven") called (same as 1+B)
- [ ] Press NumPad1+C → ExecuteTargetAction("T1", "cancel") called (same as 1+C)

### T2 Actions (12 tests: 6 keys × 2 modifier variants)
- [ ] Press 2+M → ExecuteTargetAction("T2", "market") called
- [ ] Press 2+O → ExecuteTargetAction("T2", "1point") called
- [ ] Press 2+W → ExecuteTargetAction("T2", "2point") called
- [ ] Press 2+K → ExecuteTargetAction("T2", "marketprice") called
- [ ] Press 2+B → ExecuteTargetAction("T2", "breakeven") called
- [ ] Press 2+C → ExecuteTargetAction("T2", "cancel") called
- [ ] Press NumPad2+M → ExecuteTargetAction("T2", "market") called (same as 2+M)
- [ ] Press NumPad2+O → ExecuteTargetAction("T2", "1point") called (same as 2+O)
- [ ] Press NumPad2+W → ExecuteTargetAction("T2", "2point") called (same as 2+W)
- [ ] Press NumPad2+K → ExecuteTargetAction("T2", "marketprice") called (same as 2+K)
- [ ] Press NumPad2+B → ExecuteTargetAction("T2", "breakeven") called (same as 2+B)
- [ ] Press NumPad2+C → ExecuteTargetAction("T2", "cancel") called (same as 2+C)

### Runner Actions (12 tests: 6 keys × 2 modifier variants)
- [ ] Press 3+M → ExecuteRunnerAction("market") called
- [ ] Press 3+O → ExecuteRunnerAction("stop1pt") called
- [ ] Press 3+W → ExecuteRunnerAction("stop2pt") called
- [ ] Press 3+B → ExecuteRunnerAction("stopbe") called
- [ ] Press 3+P → ExecuteRunnerAction("lock50") called
- [ ] Press 3+D → ExecuteRunnerAction("disabletrail") called
- [ ] Press NumPad3+M → ExecuteRunnerAction("market") called (same as 3+M)
- [ ] Press NumPad3+O → ExecuteRunnerAction("stop1pt") called (same as 3+O)
- [ ] Press NumPad3+W → ExecuteRunnerAction("stop2pt") called (same as 3+W)
- [ ] Press NumPad3+B → ExecuteRunnerAction("stopbe") called (same as 3+B)
- [ ] Press NumPad3+P → ExecuteRunnerAction("lock50") called (same as 3+P)
- [ ] Press NumPad3+D → ExecuteRunnerAction("disabletrail") called (same as 3+D)

**Total Test Cases**: 39 (3 basic + 12 T1 + 12 T2 + 12 Runner)

---

## Acceptance Criteria

### Complexity Targets ✅
- [ ] OnKeyDown residual CYC ≤ 5 (target: 3)
- [ ] InitKeyCommandRegistry CYC ≤ 19 (target: 3)
- [ ] HandleTargetAction CYC ≤ 19 (target: 7)
- [ ] HandleRunnerAction CYC ≤ 19 (target: 7)

### Behavioral Preservation ✅
- [ ] All 39 keyboard shortcuts function identically
- [ ] No new Print() calls with Unicode
- [ ] No new lock() statements
- [ ] No new heap allocations on hot path (lambda note documented)

### F5 Validation ✅
- [ ] Press F5 in NinjaTrader IDE
- [ ] BUILD_TAG banner appears
- [ ] Test all 39 shortcuts in live chart
- [ ] No exceptions, no behavioral changes

### DNA Compliance ✅
- [ ] `powershell -File .\deploy-sync.ps1` → PASS
- [ ] `python scripts/complexity_audit.py` → OnKeyDown CYC ≤ 5
- [ ] `grep -r "lock(" src/` → 0 matches

---

## Execution Notes for Bob

### Registry Initialization
**Decision Required**: Where to call `InitKeyCommandRegistry()`?
- **Option A**: `OnStateChange(State.DataLoaded)` in `src/V12_002.State.cs`
- **Option B**: `InitializePanel()` in `src/V12_002.UI.Lifecycle.cs`

**Recommendation**: Option B (InitializePanel) for clear UI ownership.

### Lambda Allocation Note
The design document notes that lambda closures allocate on heap. Current approach is acceptable as existing pattern, but if profiling shows impact, consider method references:
```csharp
// Instead of:
[Key.L] = () => { ... }

// Use:
[Key.L] = ExecuteLongHotkey
private void ExecuteLongHotkey() { ... }
```

### Modifier Key Handling
The hybrid approach (Dictionary for basic keys, switch for modifiers) balances simplicity and extensibility. If full Dictionary approach is preferred, see ticket-03 for KeyCombo struct specification.

---

## Dependencies

**Prerequisite**: T-D + T-F complete and F5-validated  
**Blocks**: T-B execution (ProcessIpc_MatchSymbol)

---

## References

- **Epic Scope**: [`00-scope.md`](00-scope.md)
- **Analysis**: [`01-analysis.md`](01-analysis.md) (T-A section)
- **Approach**: [`02-approach.md`](02-approach.md) (T-A + T-B section)
- **Validation**: [`03-validation.md`](03-validation.md)
- **Joint Design**: [`ticket-03-command-pattern-design.md`](ticket-03-command-pattern-design.md)
- **Source File**: `src/V12_002.UI.Callbacks.cs:337-379`

---

[TICKET-GATE]