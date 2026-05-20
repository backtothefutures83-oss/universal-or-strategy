# Run 2 Epic Analysis -- Decouple StickyState & IPC

**Epic ID:** run2-stickystate  
**Phase:** 2 - Analysis  
**Created:** 2026-05-20

---

## 1. CODEBASE ANALYSIS

### 1.1 Current File Structure

**Target File:** `src/V12_002.StickyState.cs` (773 lines)

**Regions:**
1. **Sticky State Fields** (lines 18-55)
   - `_stickyStatePath`, `_stickyStateDirty`, `_stickyWritePending`
   - `HeaderConfigSnapshot` struct (29 lines)
   - Constants: `STICKY_DEBOUNCE_MS = 50`

2. **Save -- Serialize + Atomic Write** (lines 57-356)
   - `MarkStickyDirty()` - debounced write trigger (70 lines)
   - `SerializeStickyState()` - main serializer (13 lines)
   - `SerializeSticky_WriteHeaderConfig()` - [CONFIG] section (36 lines)
   - `SerializeSticky_WriteFleetAnchor()` - [FLEET] + [ANCHOR] sections (17 lines)
   - `SerializeSticky_WriteModeProfiles()` - [CONFIG_*] sections (49 lines)
   - `SerializeSticky_WritePositions()` - [POSITIONS] section (22 lines)
   - `SnapshotCurrentConfig()` - mode profile capture (18 lines)
   - `HydrateFromProfile()` - mode profile restore (20 lines)
   - `AnchorTypeToString()` - enum serializer (13 lines)
   - `AtomicWriteFile()` - atomic file write (10 lines)

3. **Load -- Deserialize + Apply** (lines 358-771)
   - `LoadStickyState()` - main deserializer (40 lines)
   - `LoadStickyState_ReadLines()` - file reader (3 lines)
   - `LoadStickyState_DispatchSection()` - section router (23 lines)
   - `LoadStickyState_LogOutcome()` - logging (6 lines)
   - `ApplyStickyConfig()` - [CONFIG] parser (11 lines)
   - `ApplyStickyConfig_ModeSafetyGate()` - mode safety (13 lines)
   - `ApplyStickyConfig_TargetValues()` - target value parser (31 lines)
   - `ApplyStickyConfig_TargetTypes()` - target type parser (11 lines)
   - `ApplyStickyConfig_RiskAndFlags()` - risk/flag parser (28 lines)
   - `ApplyStickyModeProfile()` - [CONFIG_*] parser (19 lines)
   - `ApplyStickyModeProfile_TargetValues()` - profile target values (31 lines)
   - `ApplyStickyModeProfile_TargetTypes()` - profile target types (11 lines)
   - `ApplyStickyModeProfile_Risk()` - profile risk params (14 lines)
   - `ApplyStickyFleet()` - [FLEET] parser (19 lines)
   - `ApplyStickyAnchor()` - [ANCHOR] parser (18 lines)
   - `EnrichTrailStateFromSticky()` - [POSITIONS] parser (47 lines)
   - `ApplyPendingStickyFleetToggles()` - deferred fleet apply (18 lines)
   - `ParseTargetMode()` - enum parser (10 lines)

**Total Extractable Logic:** ~600 lines (serialization + deserialization + file I/O)

### 1.2 Integration Points

**MarkStickyDirty() Call Sites (18 occurrences):**

1. **IPC Config Commands** (src/V12_002.UI.IPC.Commands.Config.cs)
   - Line 139: `HandleConfigCommand()` - after CONFIG sync
   - Line 386: `HandleToggleAccountCommand()` - after fleet toggle

2. **IPC Mode Commands** (src/V12_002.UI.IPC.Commands.Mode.cs)
   - Line 61: `TryHandleModeCommand()` - after SET_RMA_MODE
   - Line 154: `TryHandleModeCommand()` - after SYNC_MODE
   - Line 248: `TryHandleModeCommand()` - after SET_CIT
   - Line 289: `TryHandleModeCommand()` - after SET_MAX_RISK
   - Line 307: `TryHandleModeCommand()` - after SET_ANCHOR
   - Line 330: `TryHandleModeCommand()` - after SET_TARGETS
   - Line 352: `TryHandleModeCommand()` - after SET_MANUAL_PRICE

3. **IPC Misc Commands** (src/V12_002.UI.IPC.Commands.Misc.cs)
   - Line 153: `HandleMiscCommand()` - after SET_LEADER_ACCOUNT

4. **Trailing Stop Updates** (src/V12_002.Trailing.StopUpdate.cs)
   - Line 129: `UpdateStopForPosition()` - after trail level change
   - Line 176: `UpdateStopForPosition()` - after trail level change
   - Line 248: `UpdateStopForPosition()` - after trail level change
   - Line 298: `UpdateStopForPosition()` - after trail level change

5. **Breakeven Logic** (src/V12_002.Trailing.Breakeven.cs)
   - Line 100: `HandleBreakevenCommand()` - after follower BE trigger
   - Line 141: `HandleBreakevenCommand()` - after leader BE trigger

6. **Self-Recursion** (src/V12_002.StickyState.cs)
   - Line 128: `MarkStickyDirty()` - recursive call if dirtied during write

**Key Observation:** All call sites are in IPC command handlers or trailing stop logic. No direct calls from core strategy logic (entries, orders, etc.).

### 1.3 Dependencies Analysis

**NinjaTrader-Specific Dependencies in StickyState.cs:**
1. `NinjaTrader.Cbi` - for `Account` type (not used in serialization logic)
2. `NinjaTrader.NinjaScript.Strategies.Strategy` - base class (for `Print()` and `Instrument`)
3. `Instrument.FullName` - used in header comment (line 156)
4. `BUILD_TAG` - strategy constant (line 158)
5. `Print()` - logging method (lines 121, 372, 402, 440, 467, 724, 728, 752)

**Pure C# Dependencies (Already Present):**
- `System.Collections.Generic` - Dictionary, KeyValuePair
- `System.Globalization` - CultureInfo.InvariantCulture
- `System.Text` - StringBuilder, Encoding.UTF8
- `System.Threading` - Interlocked, volatile
- `System.Threading.Tasks` - Task.Run, Task.Delay
- `System.IO.File` - ReadAllLines, WriteAllText, Exists, Delete, Move

**Strategy State Dependencies (Must Be Injected):**
- `_stickyStatePath` - file path (string)
- `_stickyLeaderAccount` - leader account name (string)
- `currentRmaAnchor` - RMA anchor type (enum)
- `cachedMnlPrice` - manual price (double)
- `_modeProfiles` - mode config profiles (Dictionary<string, ModeConfigProfile>)
- `activeFleetAccounts` - fleet toggles (Dictionary<string, bool>)
- `activePositions` - position state (ConcurrentDictionary<string, PositionInfo>)
- Various strategy properties (isRMAModeActive, Target1Value, etc.)

### 1.4 Thread Safety Analysis

**Current Thread Safety Mechanisms:**
1. **Snapshot Pattern (H18-FIX):**
   - Lines 74-108: Captures ALL mutable state on strategy thread BEFORE spawning Task.Run
   - Prevents torn reads during background serialization
   - Critical for correctness - MUST be preserved

2. **Atomic Primitives:**
   - `volatile bool _stickyStateDirty` - coalescing dirty flag
   - `Interlocked.CompareExchange(ref _stickyWritePending, 1, 0)` - write gate
   - `Interlocked.Exchange(ref _stickyWritePending, 0)` - gate release

3. **Debouncing:**
   - 50ms delay via `Task.Delay(STICKY_DEBOUNCE_MS)`
   - Coalesces rapid mutations into single write
   - Recursive call if dirtied during write (line 128)

**Risk Areas:**
- Snapshot logic is CRITICAL - any deviation breaks thread safety
- Must preserve exact snapshot pattern when extracting to service

---

## 2. EXTRACTION COMPLEXITY ASSESSMENT

### 2.1 Extraction Difficulty: MODERATE

**Easy Aspects:**
- Serialization methods are pure functions (StringBuilder in, string out)
- Deserialization methods are mostly pure (string in, bool out)
- File I/O is isolated in `AtomicWriteFile()`
- No complex state machines or callbacks

**Moderate Aspects:**
- Snapshot pattern must be preserved exactly (H18-FIX)
- `MarkStickyDirty()` orchestrates Task.Run - needs careful extraction
- 18 call sites must continue to work without changes
- `Print()` calls need abstraction (logging interface)

**Hard Aspects:**
- `Instrument.FullName` and `BUILD_TAG` are strategy-specific (need injection)
- Deserialization applies state directly to strategy properties (need callback pattern)
- `SetRmaAnchorFromIpc()` call in `ApplyStickyAnchor()` (line 668) - external dependency

### 2.2 Cyclomatic Complexity Analysis

**Current Complexity (Estimated):**
- `MarkStickyDirty()`: CYC ~5 (if/try/finally)
- `SerializeStickyState()`: CYC ~1 (straight-line)
- `SerializeSticky_WriteHeaderConfig()`: CYC ~6 (if/else chain for mode)
- `SerializeSticky_WriteFleetAnchor()`: CYC ~3 (null checks, foreach)
- `SerializeSticky_WriteModeProfiles()`: CYC ~7 (if/else chain + foreach)
- `SerializeSticky_WritePositions()`: CYC ~4 (null checks, foreach, if)
- `LoadStickyState()`: CYC ~5 (if/try/foreach)
- `ApplyStickyConfig()`: CYC ~5 (if chain)
- `ApplyStickyConfig_ModeSafetyGate()`: CYC ~2 (switch)
- `ApplyStickyConfig_TargetValues()`: CYC ~7 (switch with 6 cases)
- `ApplyStickyConfig_TargetTypes()`: CYC ~6 (switch with 5 cases)
- `ApplyStickyConfig_RiskAndFlags()`: CYC ~8 (switch with 7 cases)
- Similar patterns for mode profile parsers

**Post-Extraction Target:**
- All methods should remain under CYC 20 (V12 standard)
- No method should increase in complexity
- Service methods should be 1:1 ports (no optimization)

### 2.3 Risk Hotspots

**HIGH RISK:**
1. **Snapshot Logic (lines 74-108)**
   - CRITICAL for thread safety
   - Must be preserved byte-for-byte
   - Any deviation causes race conditions

2. **Task.Run Orchestration (lines 110-131)**
   - Debouncing + coalescing + recursion
   - Complex async pattern
   - Must preserve exact behavior

3. **Deserialization State Application**
   - Directly mutates strategy properties
   - Need callback pattern or state DTO return

**MEDIUM RISK:**
1. **Print() Abstraction**
   - 18 call sites in serialization/deserialization
   - Need logging interface injection

2. **External Dependencies**
   - `Instrument.FullName` (line 156)
   - `BUILD_TAG` (line 158)
   - `SetRmaAnchorFromIpc()` (line 668)

**LOW RISK:**
1. **Pure Serialization Methods**
   - StringBuilder-based, no side effects
   - Easy to extract and test

2. **File I/O**
   - Already isolated in `AtomicWriteFile()`
   - Straightforward extraction

---

## 3. ARCHITECTURAL DECISIONS

### 3.1 Service Interface Design

**Option A: Minimal Interface (RECOMMENDED)**
```csharp
public interface IStickyStateService
{
    void MarkDirty();
    string Serialize(StickyStateSnapshot snapshot);
    StickyStateData Deserialize(string filePath);
    void AtomicWrite(string targetPath, string content);
}
```

**Rationale:**
- Keeps service focused on serialization/deserialization
- Strategy retains orchestration logic (snapshot creation, state application)
- Minimal surface area = easier testing

**Option B: Full Orchestration Interface**
```csharp
public interface IStickyStateService
{
    void MarkDirty();
    Task SaveAsync(StickyStateSnapshot snapshot, string filePath);
    bool Load(string filePath, Action<StickyStateData> applyCallback);
}
```

**Rejected Because:**
- Moves too much orchestration into service
- Harder to test (async, callbacks)
- Violates single responsibility (serialization + orchestration)

**DECISION: Use Option A (Minimal Interface)**

### 3.2 Snapshot Pattern Preservation

**Strategy:**
1. Keep `HeaderConfigSnapshot` struct in Strategy (shared DTO)
2. Create new `StickyStateSnapshot` struct in service that wraps all snapshots
3. Strategy creates snapshot on strategy thread (preserves H18-FIX)
4. Service receives immutable snapshot for serialization

**Struct Definition:**
```csharp
public struct StickyStateSnapshot
{
    public HeaderConfigSnapshot HeaderConfig;
    public Dictionary<string, ModeConfigProfile> ModeProfiles;
    public Dictionary<string, bool> FleetAccounts;
    public KeyValuePair<string, PositionInfo>[] Positions;
    public string LeaderAccount;
    public RmaAnchorType CurrentAnchor;
    public double CachedManualPrice;
    public string InstrumentName;
    public string BuildTag;
}
```

### 3.3 Logging Abstraction

**Option A: ILogger Interface (RECOMMENDED)**
```csharp
public interface IStickyStateLogger
{
    void Log(string message);
}
```

**Strategy Implementation:**
```csharp
private class StrategyLogger : IStickyStateLogger
{
    private readonly V12_002 _strategy;
    public StrategyLogger(V12_002 strategy) => _strategy = strategy;
    public void Log(string message) => _strategy.Print(message);
}
```

**Option B: Action<string> Callback**
```csharp
public StickyStateService(Action<string> logCallback)
```

**DECISION: Use Option A (ILogger Interface)**
- More testable (can mock interface)
- Clearer intent
- Easier to extend (log levels, etc.)

### 3.4 Deserialization State Application

**Option A: Return DTO (RECOMMENDED)**
```csharp
public class StickyStateData
{
    public Dictionary<string, object> ConfigValues;
    public Dictionary<string, ModeConfigProfile> ModeProfiles;
    public Dictionary<string, bool> FleetToggles;
    public Dictionary<string, PositionTrailState> PositionStates;
    public string LeaderAccount;
    public RmaAnchorType Anchor;
    public double ManualPrice;
}
```

**Strategy applies state:**
```csharp
var data = _stickyStateService.Deserialize(_stickyStatePath);
if (data != null)
{
    ApplyConfigValues(data.ConfigValues);
    ApplyModeProfiles(data.ModeProfiles);
    // etc.
}
```

**Option B: Callback Pattern**
```csharp
public bool Load(string filePath, Action<string, string> applyConfig)
```

**DECISION: Use Option A (Return DTO)**
- Cleaner separation of concerns
- Easier to test
- Strategy retains control over state application

### 3.5 External Dependency Handling

**Instrument.FullName (line 156):**
- Inject via `StickyStateSnapshot.InstrumentName`
- Strategy provides value when creating snapshot

**BUILD_TAG (line 158):**
- Inject via `StickyStateSnapshot.BuildTag`
- Strategy provides value when creating snapshot

**SetRmaAnchorFromIpc() (line 668):**
- Keep in Strategy (not part of serialization logic)
- Service returns `RmaAnchorType` enum value
- Strategy calls `SetRmaAnchorFromIpc()` after deserialization

---

## 4. DIFF IMPACT ANALYSIS

### 4.1 Lines to Extract

**From V12_002.StickyState.cs:**
- Serialization methods: ~220 lines
- Deserialization methods: ~380 lines
- File I/O: ~10 lines
- **Total Extracted:** ~610 lines

**Remaining in V12_002.StickyState.cs:**
- Fields: ~10 lines
- `MarkStickyDirty()` orchestration: ~30 lines (modified to call service)
- Snapshot creation: ~40 lines (stays in Strategy)
- State application: ~50 lines (new methods to apply DTO)
- **Total Remaining:** ~130 lines

### 4.2 New Files

**src/Services/IStickyStateService.cs:**
- Interface definition: ~20 lines

**src/Services/StickyStateService.cs:**
- Service implementation: ~650 lines (extracted logic + DTOs)

**tests/Services/StickyStateServiceTests.cs:**
- Unit test stub: ~30 lines

**Total New Lines:** ~700 lines

### 4.3 Modified Files

**src/V12_002.StickyState.cs:**
- Before: 773 lines
- After: ~200 lines (orchestration + state application)
- **Net Change:** -573 lines

**src/V12_002.UI.IPC.Commands.Config.cs:**
- No changes (calls `MarkStickyDirty()` which stays in Strategy)

**src/V12_002.UI.IPC.Commands.Mode.cs:**
- No changes (calls `MarkStickyDirty()` which stays in Strategy)

**src/V12_002.UI.IPC.Commands.Misc.cs:**
- No changes (calls `MarkStickyDirty()` which stays in Strategy)

**src/V12_002.Trailing.StopUpdate.cs:**
- No changes (calls `MarkStickyDirty()` which stays in Strategy)

**src/V12_002.Trailing.Breakeven.cs:**
- No changes (calls `MarkStickyDirty()` which stays in Strategy)

### 4.4 Total Diff Estimate

**Additions:** +700 lines (new service + tests)  
**Deletions:** -573 lines (extracted from Strategy)  
**Net Change:** +127 lines  
**Modified Lines:** ~200 lines (Strategy orchestration rewrite)

**Total PR Diff:** ~327 lines (well under 500-line limit)

---

## 5. TESTING STRATEGY

### 5.1 Unit Tests (New)

**StickyStateServiceTests.cs:**
1. **Instantiation Test:**
   - Verify service can be created without NinjaTrader
   - Proves `dotnet test` works

2. **Serialization Tests:**
   - Round-trip test: serialize → deserialize → compare
   - Null handling tests
   - Empty collection tests

3. **Deserialization Tests:**
   - Valid INI format parsing
   - Invalid format handling
   - Missing section handling

4. **File I/O Tests:**
   - Atomic write verification (.tmp file creation)
   - Corruption prevention (process kill simulation)

### 5.2 Integration Tests (Existing)

**F5 Gate (NinjaTrader):**
1. Load strategy in NinjaTrader
2. Send IPC CONFIG command
3. Verify .v12state file created
4. Restart strategy
5. Verify state hydrated from file

**IPC Command Tests:**
- All 18 `MarkStickyDirty()` call sites must still trigger persistence
- Verify no regression in IPC command handling

### 5.3 Verification Gates

1. **Build Gate:** `deploy-sync.ps1` (ASCII compliance)
2. **Lock Audit:** `grep -r "lock(" src/` (0 matches)
3. **Unicode Audit:** `grep -Prn "[^\x00-\x7F]" src/` (0 matches)
4. **Unit Test Gate:** `dotnet test` (service instantiation)
5. **F5 Gate:** NinjaTrader load + IPC command + restart

---

## 6. ROLLBACK PLAN

### 6.1 Single-Commit Strategy

**Commit Structure:**
```
refactor: extract StickyState & IPC into StickyStateService (Epic 1) [Run2-StickyService]

- NEW: src/Services/IStickyStateService.cs
- NEW: src/Services/StickyStateService.cs
- NEW: tests/Services/StickyStateServiceTests.cs
- MODIFIED: src/V12_002.StickyState.cs (delegate to service)
```

**Rollback Command:**
```bash
git revert <commit-hash>
```

### 6.2 Rollback Triggers

**Automatic Rollback If:**
- Any verification gate fails (deploy-sync, lock audit, unicode audit)
- F5 gate fails (NinjaTrader won't load)
- IPC commands stop persisting state

**Manual Rollback If:**
- Performance regression detected (>10% slower persistence)
- Memory leak detected in service
- Thread safety issue discovered in production

---

## 7. PERFORMANCE CONSIDERATIONS

### 7.1 Expected Performance Impact

**Serialization:**
- Current: Direct StringBuilder manipulation
- After: Service method call + StringBuilder manipulation
- **Impact:** Negligible (<1% overhead from method call)

**Deserialization:**
- Current: Direct property assignment
- After: Service returns DTO + Strategy applies DTO
- **Impact:** Minimal (~2-3% overhead from DTO allocation)

**File I/O:**
- Current: Direct File.WriteAllText
- After: Service method call + File.WriteAllText
- **Impact:** Negligible (<1% overhead)

**Overall:** <5% performance impact (acceptable for testability gain)

### 7.2 Memory Impact

**New Allocations:**
- `StickyStateSnapshot` struct (stack-allocated, ~200 bytes)
- `StickyStateData` DTO (heap-allocated, ~1KB per load)
- Service instance (heap-allocated, ~100 bytes)

**Total Memory Overhead:** <2KB per persistence cycle (negligible)

---

## 8. MIGRATION RISKS

### 8.1 High-Risk Areas

1. **Snapshot Pattern Deviation**
   - Risk: Breaking H18-FIX thread safety
   - Mitigation: 1:1 port of snapshot logic, no changes

2. **Task.Run Orchestration**
   - Risk: Breaking debouncing or coalescing
   - Mitigation: Keep orchestration in Strategy, service only serializes

3. **Deserialization State Application**
   - Risk: Missing property assignments
   - Mitigation: Comprehensive integration test (F5 gate)

### 8.2 Medium-Risk Areas

1. **Logging Abstraction**
   - Risk: Missing log messages
   - Mitigation: ILogger interface with Strategy wrapper

2. **External Dependencies**
   - Risk: Missing Instrument.FullName or BUILD_TAG
   - Mitigation: Inject via snapshot

### 8.3 Low-Risk Areas

1. **Pure Serialization Methods**
   - Risk: Minimal (pure functions)
   - Mitigation: Unit tests

2. **File I/O**
   - Risk: Minimal (isolated logic)
   - Mitigation: Unit tests

---

## 9. SUCCESS METRICS

### 9.1 Functional Metrics

- [ ] All 18 `MarkStickyDirty()` call sites still trigger persistence
- [ ] INI format byte-for-byte identical to pre-refactor
- [ ] State hydration on restart works identically
- [ ] No regression in IPC command handling

### 9.2 Quality Metrics

- [ ] `dotnet test` runs without NinjaTrader
- [ ] Unit test coverage >80% for service
- [ ] All verification gates pass (deploy-sync, lock, unicode)
- [ ] F5 gate passes (NinjaTrader load + IPC + restart)

### 9.3 Performance Metrics

- [ ] Serialization time <5% slower than baseline
- [ ] Deserialization time <5% slower than baseline
- [ ] Memory overhead <2KB per cycle

---

## 10. NEXT STEPS

**[PLAN-GATE]**

This analysis document is now complete. Ready to proceed to approach document (02-approach.md).

**Key Findings:**
1. Extraction is MODERATE complexity (not trivial, but manageable)
2. Snapshot pattern (H18-FIX) is CRITICAL - must preserve exactly
3. Minimal interface design keeps service focused on serialization
4. DTO return pattern cleanest for deserialization
5. Total diff ~327 lines (well under 500-line limit)

**Proceed to 02-approach.md to define implementation strategy.**