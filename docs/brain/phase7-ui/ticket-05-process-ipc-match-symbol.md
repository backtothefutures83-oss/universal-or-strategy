# TICKET T-B: ProcessIpc_MatchSymbol Extraction (Execution)
**Epic**: phase7-ui  
**Priority**: P5 (Surgical Execution)  
**Ticket ID**: T-B  
**Agent**: Bob CLI (v12-engineer mode)  
**Estimated Sessions**: 1 extraction session

---

## Mission Brief

Extract [`ProcessIpc_MatchSymbol()`](../../../src/V12_002.UI.IPC.cs:325) from CYC 49 → ≤5 using the unified Command Pattern architecture defined in [`ticket-03-command-pattern-design.md`](ticket-03-command-pattern-design.md).

**Critical Requirement**: This ticket MUST follow the design specification from ticket-03 and use the same architectural pattern as T-A (OnKeyDown). Do NOT deviate from the approved architecture.

---

## Prerequisites

**MUST BE COMPLETE BEFORE STARTING**:
- [x] T-C (AttachPanelHandlers) complete and F5-validated
- [x] T-D + T-F (OnSyncAllClick + UpdateContextualUI) complete and F5-validated
- [x] T-A + T-B joint design (ticket-03) approved by Director
- [x] T-A (OnKeyDown) complete and F5-validated

**Dependency Chain**: T-C → T-D+T-F → T-A+T-B Design → T-A Execution → **T-B Execution**

---

## Target Method

**File**: `src/V12_002.UI.IPC.cs`  
**Method**: `ProcessIpc_MatchSymbol(string action, string[] parts)`  
**Line**: 325  
**Current CYC**: 49  
**Target CYC**: ≤5 (target: 3)

### Current Complexity Drivers
- 17-term OR chain for global commands
- 11-term OR chain for symbol matching
- Hard-coded command list
- No abstraction
- Mixed concerns (command classification + symbol matching + logging)

---

## Extraction Plan

### Step 1: Create Global Command Registry
**Location**: `src/V12_002.UI.Lifecycle.cs` (or State.cs if preferred)  
**Called From**: `OnStateChange(State.DataLoaded)` or `InitializePanel()`  
**Type**: `static readonly HashSet<string>` (zero allocation, O(1) lookup)

```csharp
private static readonly HashSet<string> _globalIpcCommands = new HashSet<string>
{
    "TOGGLE_ACCOUNT", "SET_SIMA", "GET_FLEET", "DIAG_FLEET", "CANCEL_ALL",
    "FLATTEN", "SYNC_ALL", "MKT_SYNC", "REQUEST_FLEET_STATE", "RESET_MEMORY",
    "DIAG_IPC", "LOCK_50", "SET_TARGETS", "SET_TRAIL", "SET_CIT", "BE_CUSTOM"
};
```

**Note**: `static readonly` means:
- Initialized once at class load time (no runtime cost)
- Immutable (thread-safe)
- Zero allocation on hot path
- O(1) lookup via HashSet.Contains()

### Step 2: Create IsSymbolMatch() Helper
**Location**: `src/V12_002.UI.IPC.cs` (near ProcessIpc_MatchSymbol)  
**CYC**: ~12 (11 OR terms + 1 return)  
**LOC**: ~20

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

### Step 3: Rewrite ProcessIpc_MatchSymbol() Residual
**Target CYC**: 3  
**LOC**: ~15

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

---

## CYC Targets

| Method | Before | After | Status |
|--------|--------|-------|--------|
| ProcessIpc_MatchSymbol | 49 | ≤5 (target: 3) | ⏳ |
| IsSymbolMatch | N/A | ~12 | ⏳ |

**Total Reduction**: 49 → 15 (residual 3 + helper 12) = **-34 CYC**

---

## Test Matrix

### Global Commands (17 tests)
- [ ] "TOGGLE_ACCOUNT" → isGlobalCommand=true, isForMe=true
- [ ] "SET_SIMA" → isGlobalCommand=true, isForMe=true
- [ ] "GET_FLEET" → isGlobalCommand=true, isForMe=true
- [ ] "DIAG_FLEET" → isGlobalCommand=true, isForMe=true
- [ ] "CANCEL_ALL" → isGlobalCommand=true, isForMe=true
- [ ] "FLATTEN" → isGlobalCommand=true, isForMe=true
- [ ] "SYNC_ALL" → isGlobalCommand=true, isForMe=true
- [ ] "MKT_SYNC" → isGlobalCommand=true, isForMe=true
- [ ] "REQUEST_FLEET_STATE" → isGlobalCommand=true, isForMe=true
- [ ] "RESET_MEMORY" → isGlobalCommand=true, isForMe=true
- [ ] "DIAG_IPC" → isGlobalCommand=true, isForMe=true
- [ ] "LOCK_50" → isGlobalCommand=true, isForMe=true
- [ ] "SET_TARGETS" → isGlobalCommand=true, isForMe=true
- [ ] "SET_TRAIL" → isGlobalCommand=true, isForMe=true
- [ ] "SET_CIT" → isGlobalCommand=true, isForMe=true
- [ ] "BE_CUSTOM" → isGlobalCommand=true, isForMe=true
- [ ] "MOVE_TARGET_T1" → isGlobalCommand=true (StartsWith check), isForMe=true

### Symbol Matching (10 tests)
Assume Instrument.MasterInstrument.Name = "ES 03-25" for these tests:

- [ ] targetSymbol="GLOBAL" → IsSymbolMatch=true
- [ ] targetSymbol="ALL" → IsSymbolMatch=true
- [ ] targetSymbol="ON" → IsSymbolMatch=true
- [ ] targetSymbol="OFF" → IsSymbolMatch=true
- [ ] targetSymbol="ES" → IsSymbolMatch=true (mySym.StartsWith)
- [ ] targetSymbol="MES" → IsSymbolMatch=true (special case: MES && mySym.Contains("ES"))
- [ ] targetSymbol="NQ" → IsSymbolMatch=false (no match)
- [ ] targetSymbol="ORB" → IsSymbolMatch=true (mode keyword)
- [ ] targetSymbol="RMA" → IsSymbolMatch=true (mode keyword)
- [ ] targetSymbol="MOMO" → IsSymbolMatch=true (mode keyword)

### Edge Cases (3 tests)
- [ ] parts.Length=1 (no symbol) → targetSymbol="Global", isForMe=true
- [ ] action="UNKNOWN_CMD", targetSymbol="ES" → isGlobalCommand=false, IsSymbolMatch=true, isForMe=true
- [ ] action="UNKNOWN_CMD", targetSymbol="NQ" → isGlobalCommand=false, IsSymbolMatch=false, isForMe=false

**Total Test Cases**: 30 (17 global + 10 symbol + 3 edge)

---

## Acceptance Criteria

### Complexity Targets ✅
- [ ] ProcessIpc_MatchSymbol residual CYC ≤ 5 (target: 3)
- [ ] IsSymbolMatch CYC ≤ 19 (target: 12)
- [ ] _globalIpcCommands initialized as static readonly (zero allocation)

### Behavioral Preservation ✅
- [ ] All 17 global commands recognized identically
- [ ] All 10 symbol matching patterns function identically
- [ ] Print() output format unchanged (for log parsing)
- [ ] No new Unicode in Print() calls
- [ ] No new lock() statements
- [ ] No new heap allocations on hot path

### F5 Validation ✅
- [ ] Press F5 in NinjaTrader IDE
- [ ] BUILD_TAG banner appears
- [ ] Send IPC commands from fleet master (SYNC_ALL, CANCEL_ALL, etc.)
- [ ] Verify "For Me?" logic matches pre-extraction behavior
- [ ] No exceptions, no behavioral changes

### DNA Compliance ✅
- [ ] `powershell -File .\deploy-sync.ps1` → PASS
- [ ] `python scripts/complexity_audit.py` → ProcessIpc_MatchSymbol CYC ≤ 5
- [ ] `grep -r "lock(" src/` → 0 matches

---

## Execution Notes for Bob

### Registry Initialization
The `_globalIpcCommands` HashSet should be declared as `static readonly` at class level. This means:
- No initialization method needed (initialized at class load time)
- Zero runtime cost
- Immutable (cannot be modified after initialization)
- Thread-safe by design

**Location**: Top of `src/V12_002.UI.IPC.cs` class, near other static fields.

### Symbol Matching Logic
The `IsSymbolMatch()` helper consolidates the 11-term OR chain. Key patterns:
- **Exact match**: `mySym == target`
- **Prefix match**: `mySym.StartsWith(target)` or `target.StartsWith(mySym)`
- **Contains match**: `myFull.Contains(target)`
- **Special cases**: MES/MYM/MGC micro contracts
- **Mode keywords**: GLOBAL, ALL, ON, OFF, RMA, ORB, OR, MOMO

### Print() Format Preservation
The Print() call format MUST remain unchanged for log parsing compatibility:
```csharp
Print(string.Format("V12 IPC: Received '{0}' for '{1}'. For Me? {2} (My Symbol: {3}){4}",
    action, targetSymbol, isForMe, Instrument.MasterInstrument.Name, isGlobalCommand ? " [GLOBAL CMD]" : ""));
```

### Architectural Consistency
This extraction follows the same pattern as T-A (OnKeyDown):
- **T-A**: Dictionary<Key, Action> for command dispatch
- **T-B**: HashSet<string> for command classification
- Both use O(1) lookup, zero allocation, extracted helpers

---

## Dependencies

**Prerequisite**: T-A (OnKeyDown) complete and F5-validated  
**Blocks**: None (final ticket in epic)

---

## References

- **Epic Scope**: [`00-scope.md`](00-scope.md)
- **Analysis**: [`01-analysis.md`](01-analysis.md) (T-B section)
- **Approach**: [`02-approach.md`](02-approach.md) (T-A + T-B section)
- **Validation**: [`03-validation.md`](03-validation.md)
- **Joint Design**: [`ticket-03-command-pattern-design.md`](ticket-03-command-pattern-design.md)
- **T-A Execution**: [`ticket-04-onkeydown-execution.md`](ticket-04-onkeydown-execution.md)
- **Source File**: `src/V12_002.UI.IPC.cs:325-371`

---

[TICKET-GATE]