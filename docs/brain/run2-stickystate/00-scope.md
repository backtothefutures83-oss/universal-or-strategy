# Run 2 Epic Intake -- Decouple StickyState & IPC

**Epic ID:** run2-stickystate  
**Mission:** Extract serialization and IPC send/receive logic from V12_002.StickyState.cs into a standalone pure C# service  
**BUILD_TAG:** Run2-StickyService  
**Branch:** refactor/stickystate-service  
**Epic Type:** Architectural Refactoring (Testability Enhancement)

---

## 1. PROBLEM STATEMENT

### Current State
The `V12_002.StickyState.cs` file (773 lines) contains:
- Serialization logic (INI format generation)
- Deserialization logic (INI parsing and state hydration)
- Atomic file I/O operations
- Direct coupling to NinjaTrader Strategy base class
- IPC command handlers that trigger state persistence via `MarkStickyDirty()`

### Pain Points
1. **Zero Testability**: Cannot run `dotnet test` on serialization logic without NinjaTrader runtime
2. **Tight Coupling**: Serialization logic is embedded in the Strategy partial class
3. **Hidden Dependencies**: State persistence is triggered via `MarkStickyDirty()` calls scattered across IPC command handlers
4. **Maintenance Burden**: Any change to persistence format requires full NinjaTrader IDE rebuild cycle

### Business Impact
- Slow feedback loop for persistence logic changes (requires F5 in NinjaTrader)
- No unit test coverage for critical state serialization paths
- Risk of data corruption bugs that can only be caught in production

---

## 2. GOAL

Extract all serialization, deserialization, and file I/O logic into a **pure C# service** that:
1. Has **zero NinjaTrader dependencies**
2. Can be instantiated in `dotnet test` without the NinjaTrader runtime
3. Accepts all state via **constructor injection** (no global statics)
4. Maintains **100% functional equivalence** with current behavior

---

## 3. SCOPE BOUNDARIES

### IN SCOPE
- **New Files to Create:**
  - `src/Services/IStickyStateService.cs` (interface)
  - `src/Services/StickyStateService.cs` (pure C# implementation)
  - `tests/Services/StickyStateServiceTests.cs` (unit test stub proving NinjaTrader-free instantiation)

- **Files to Modify:**
  - `src/V12_002.StickyState.cs` (replace inline logic with service calls)
  - Any IPC command handlers that call `MarkStickyDirty()` (verify they still trigger persistence)

- **Logic to Extract:**
  - `SerializeStickyState()` and all sub-methods (`SerializeSticky_Write*`)
  - `LoadStickyState()` and all sub-methods (`ApplySticky*`, `LoadStickyState_*`)
  - `AtomicWriteFile()` (atomic file write with .tmp rename)
  - `MarkStickyDirty()` (debounced async write trigger)
  - `HeaderConfigSnapshot` struct (snapshot for thread-safe serialization)

### OUT OF SCOPE
- **NOT changing:**
  - INI file format (must remain byte-for-byte compatible)
  - Persistence triggers (IPC commands still call `MarkStickyDirty()`)
  - File path resolution logic
  - Any UI panel code
  - Any SIMA/Fleet/Symmetry logic

- **NOT adding:**
  - New features or format changes
  - Performance optimizations beyond current behavior
  - Additional persistence mechanisms

---

## 4. SURGICAL TARGETS

### 4.1 New Files (Pure C# Service Layer)

#### `src/Services/IStickyStateService.cs`
```csharp
public interface IStickyStateService
{
    void MarkDirty();
    string Serialize(HeaderConfigSnapshot config, 
                     Dictionary<string, ModeConfigProfile> profiles,
                     Dictionary<string, bool> fleet,
                     KeyValuePair<string, PositionInfo>[] positions);
    bool Load(string filePath, out Dictionary<string, object> state);
    void AtomicWrite(string targetPath, string content);
}
```

#### `src/Services/StickyStateService.cs`
- Pure C# class (no NinjaTrader base class inheritance)
- All dependencies injected via constructor
- Thread-safe debounced write logic (preserves existing `Task.Run` pattern)
- Extracted serialization methods (1:1 port from V12_002.StickyState.cs)

#### `tests/Services/StickyStateServiceTests.cs`
- Minimal unit test stub proving instantiation without NinjaTrader
- Example: `new StickyStateService()` succeeds in `dotnet test`

### 4.2 Modified Files

#### `src/V12_002.StickyState.cs`
**Before:** 773 lines with inline serialization logic  
**After:** ~200 lines with service delegation

**Changes:**
1. Add field: `private IStickyStateService _stickyStateService;`
2. Instantiate service in `State.DataLoaded` lifecycle method
3. Replace `SerializeStickyState()` call with `_stickyStateService.Serialize(...)`
4. Replace `LoadStickyState()` call with `_stickyStateService.Load(...)`
5. Replace `AtomicWriteFile()` call with `_stickyStateService.AtomicWrite(...)`
6. Remove now-dead inline serialization methods (lines 134-354)
7. Remove now-dead inline deserialization methods (lines 358-730)

**Preserved:**
- `MarkStickyDirty()` public API (IPC commands still call this)
- `_stickyStatePath` field (file path resolution stays in Strategy)
- `HeaderConfigSnapshot` struct (may move to service or keep as shared DTO)

---

## 5. INVARIANTS (MUST PRESERVE)

### 5.1 Functional Equivalence
- **INI Format:** Output must be byte-for-byte identical to current implementation
- **Load Behavior:** Deserialization must apply state in the same order
- **Thread Safety:** Snapshot-based serialization must prevent torn reads (H18-FIX)
- **Debouncing:** 50ms coalescing window must be preserved
- **Atomic Writes:** .tmp file + rename pattern must be preserved

### 5.2 Architectural Constraints
- **No Global Statics:** Service must accept all state via constructor/method parameters
- **ASCII-Only:** No Unicode in string literals (V12 DNA mandate)
- **No Locks:** Use existing atomic primitives (`Interlocked`, `volatile`)
- **DIFF Limit:** Total PR diff must stay under 500 lines (whitespace mutation banned)

### 5.3 Verification Gates
1. **Build Gate:** `powershell -File .\deploy-sync.ps1` must pass (ASCII gate)
2. **Lock Audit:** `grep -r "lock(" src/` must return 0 matches
3. **Unicode Audit:** `grep -Prn "[^\x00-\x7F]" src/` must return 0 matches
4. **Test Gate:** `dotnet test` must instantiate `StickyStateService` without NinjaTrader
5. **F5 Gate:** NinjaTrader must load strategy and persist state on IPC command

---

## 6. RISK ASSESSMENT

### High Risk Areas
1. **Thread Safety:** Snapshot logic must be preserved exactly (H18-FIX prevents race conditions)
2. **IPC Integration:** All `MarkStickyDirty()` call sites must still trigger persistence
3. **File I/O:** Atomic write pattern must prevent corruption on process kill

### Mitigation Strategy
- **Surgical Extraction:** Port logic line-by-line, no "improvements"
- **Snapshot Preservation:** Keep `HeaderConfigSnapshot` struct and snapshot pattern
- **Integration Testing:** Verify IPC commands still persist state via F5 gate

### Rollback Plan
- Single-commit extraction allows clean revert
- Branch isolation prevents main contamination

---

## 7. SUCCESS CRITERIA

### Phase 1: Extraction Complete
- [ ] `StickyStateService.cs` compiles without NinjaTrader references
- [ ] `dotnet test` instantiates service successfully
- [ ] All serialization logic removed from `V12_002.StickyState.cs`

### Phase 2: Integration Verified
- [ ] `deploy-sync.ps1` passes (ASCII gate)
- [ ] Lock audit clean (0 matches)
- [ ] Unicode audit clean (0 matches)
- [ ] F5 in NinjaTrader loads strategy without errors

### Phase 3: Functional Equivalence
- [ ] IPC CONFIG command persists state to .v12state file
- [ ] Strategy restart hydrates state from .v12state file
- [ ] INI format byte-for-byte identical to pre-refactor

---

## 8. DEPENDENCIES & BLOCKERS

### Prerequisites
- Run 1 (CS0656 fix) must be merged to main
- Branch must be 0-delta from main after Run 1 PR

### External Dependencies
- None (pure refactoring, no new libraries)

### Known Blockers
- None identified

---

## 9. COMMIT MESSAGE TEMPLATE

```
refactor: extract StickyState & IPC into StickyStateService (Epic 1) [Run2-StickyService]

- NEW: src/Services/IStickyStateService.cs (interface)
- NEW: src/Services/StickyStateService.cs (pure C# service, 0 NinjaTrader deps)
- NEW: tests/Services/StickyStateServiceTests.cs (proves dotnet test works)
- MODIFIED: src/V12_002.StickyState.cs (delegate to service, remove inline logic)

DIFF: ~450 lines (within 500-line limit)
GATES: deploy-sync PASS, lock audit CLEAN, unicode audit CLEAN
VERIFICATION: F5 in NinjaTrader + IPC CONFIG command persists state
```

---

## 10. NEXT STEPS

**[INTAKE-GATE]**

This scope document is now complete. Director review required before proceeding to Phase 2 (PLAN).

**Questions for Director:**
1. Does this scope match your intent for decoupling StickyState?
2. Should `HeaderConfigSnapshot` struct move to the service or stay as a shared DTO?
3. Any additional verification gates beyond the 5 listed in section 5.3?

**Reply YES to proceed to Phase 2 (PLAN) or provide corrections.**