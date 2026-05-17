# P3 Implementation Plan: S5 Configuration & Persistence Test Suite

**Cluster**: S5 - Configuration & Persistence System  
**Files**: 5 files (Properties.cs, StickyState.cs, UI.IPC.Commands.Config.cs, Lifecycle.cs, V12_002.cs)  
**Build Tag Baseline**: 1111.007-phase7-tQ1_S4_REAPER_TESTS_COMPLETE  
**Test File**: `tests/ConfigurationIntegrationTests.cs`  
**Reference Template**: `tests/REAPERDefenseIntegrationTests.cs`  
**Architect**: Bob CLI (v12-engineer)  
**Date**: 2026-05-17

---

## Executive Summary

The S5 Configuration & Persistence cluster implements V12's **two-tier persistence system** and **IPC-driven configuration management**. This test suite documents the current behavior of:

1. **NinjaTrader XML Properties** (cold start) - 80+ properties loaded from workspace XML
2. **StickyState INI Persistence** (warm start) - Runtime config persisted to `.v12state` files
3. **IPC Configuration Commands** - Real-time config updates from Control Surface
4. **Mode Profile System** - Per-mode config snapshots (OR, RMA, TREND, RETEST, MOMO, FFMA)
5. **Lifecycle Integration** - Config loading sequence across State.Configure → State.DataLoaded → State.Realtime

**Key Characteristics**:
- **Two-Tier Persistence**: NinjaTrader XML (cold) + StickyState INI (warm, 50ms debounced async writes)
- **Atomic Write Pattern**: `.tmp` → rename for corruption-free persistence
- **Lock-Free Concurrency**: Interlocked gates for debounce, volatile flags for dirty tracking
- **IPC Config Sync**: Real-time updates from Control Surface with validation and rejection logging
- **Mode Profile Switching**: Per-mode config snapshots with automatic hydration on mode change

**Test Strategy**: 25 tests documenting current persistence behavior, IPC config updates, mode profile switching, and lifecycle integration.

---

## File Inventory

### 1. V12_002.Properties.cs (423 lines)
**Purpose**: NinjaTrader property definitions - XML serialization layer  
**Key Components**:
- 80+ `[NinjaScriptProperty]` decorated properties
- 14 property groups (Session, Risk, Targets, Stops, Trailing, Display, RMA, TREND, RETEST, MOMO, FFMA, SIMA, Compliance, RMA Intelligence)
- Enums: `ORTimeframeType`, `TargetMode`
- Property validation: `[Range]`, `[Display]`, `[PropertyEditor]`
- Backward compatibility stubs: `ReducedRiskPerTrade`, `EnablePhotonAffinityBind`, `CpuAffinityMask`

**V12 DNA Compliance**:
- ✅ ASCII-Only: All property names and descriptions use ASCII characters
- ✅ No lock() statements (properties are simple getters/setters)
- ✅ XML serialization via NinjaTrader framework (no custom serialization)

### 2. V12_002.StickyState.cs (680 lines)
**Purpose**: Runtime persistence engine - INI-based warm start  
**Key Components**:
- `MarkStickyDirty()` - Debounced async write trigger (50ms coalescing)
- `SerializeStickyState()` - INI serialization (4 sections: CONFIG, FLEET, ANCHOR, POSITIONS, CONFIG_*)
- `LoadStickyState()` - INI deserialization with section dispatch
- `AtomicWriteFile()` - Corruption-free write (`.tmp` → rename)
- `EnrichTrailStateFromSticky()` - Position state hydration after SIMA reconnect
- `ApplyPendingStickyFleetToggles()` - Deferred fleet toggle application
- `SnapshotCurrentConfig()` / `HydrateFromProfile()` - Mode profile capture/restore

**V12 DNA Compliance**:
- ✅ Zero lock() - Interlocked gate for `_stickyWritePending` (0=idle, 1=write scheduled)
- ✅ Atomic primitives - `Interlocked.CompareExchange` for coalescing gate
- ✅ Volatile flag - `_stickyStateDirty` for dirty tracking
- ✅ Async debounce - `Task.Run` + `Task.Delay(50ms)` for write coalescing

**Critical Logic**:
- **Debounce Pattern**: Only one pending write at a time via `Interlocked.CompareExchange`
- **Atomic Write**: `.tmp` file → `File.Move` (atomic on NTFS same-volume)
- **Section Dispatch**: `[CONFIG]`, `[FLEET]`, `[ANCHOR]`, `[POSITIONS]`, `[CONFIG_*]`
- **Mode Profiles**: Per-mode snapshots (Build 1106) - OR, RMA, TREND, RETEST, MOMO, FFMA
- **Safety Gate**: Click-trader modes (RMA, TREND, RETEST, MOMO, FFMA) never auto-rearm on startup (Build 1108.002)

### 3. V12_002.UI.IPC.Commands.Config.cs (423 lines)
**Purpose**: IPC configuration command handlers  
**Key Components**:
- `HandleConfigCommand()` - Main CONFIG sync handler (parses `CONFIG|Mode|COUNT:3;T1:1.0;...`)
- `TryApplyConfigTargets()` - Target value/type/count updates (T1-T5, COUNT, CIT)
- `TryApplyConfigRisk()` - Risk parameter updates (STR, MAX)
- `TryApplyConfigMode()` - Mode flag updates (TRMA, RRMA)
- `HandleToggleAccountCommand()` - Fleet account enable/disable (Build 935 alias resolution)
- `HandleTrimCommand()` - Position trim (25%/50%) with fleet routing
- `ValidateIpcMultiplier()` - IPC input validation with rejection logging

**V12 DNA Compliance**:
- ✅ Zero lock() - ConcurrentDictionary for `activeFleetAccounts` (lock-free reads/writes)
- ✅ Validation gates - `ValidateIpcMultiplier()` rejects invalid inputs before mutation
- ✅ Rejection logging - All validation failures logged with reason
- ✅ Atomic updates - Single-property mutations (no multi-step transactions)

**Critical Logic**:
- **CONFIG Format**: `CONFIG|Mode|COUNT:3;T1:1.0;T1TYPE:Points;T2:0.5;T2TYPE:ATR;...`
- **Validation**: `ValidateIpcMultiplier()` checks range/sanity before applying
- **Mode Profile Update**: After CONFIG sync, updates `_modeProfiles[currentMode]` (Build 1106)
- **Fleet Toggle**: Resolves UI aliases (F01, F02) via `ResolveAccountName()` before dict write (Build 935)
- **Sticky Trigger**: All config mutations call `MarkStickyDirty()` for persistence

### 4. V12_002.Lifecycle.cs (773 lines)
**Purpose**: Strategy lifecycle orchestration  
**Key Components**:
- `OnStateChangeSetDefaults()` - Property defaults (80+ properties initialized)
- `OnStateChangeConfigure()` - Collection initialization, AddDataSeries, MMIO setup
- `OnStateChangeDataLoaded()` - Instrument config, indicators, session logging, services (IPC + StickyState)
- `OnStateChangeRealtime()` - Watchdog start, SIMA startup, hotkeys, panel creation
- `OnStateChangeTerminated()` - Shutdown sequence (watchdog stop, IPC stop, REAPER stop, cleanup)
- `Init_Services()` - StickyState load + IPC server start (Build 1103 ordering)

**V12 DNA Compliance**:
- ✅ Zero lock() - All lifecycle state uses atomic primitives or volatile flags
- ✅ Ordered initialization - Critical sequence: InstrumentConfig → TargetConfig → Indicators → SessionLogging → Services
- ✅ Atomic gates - `_configureComplete`, `_dataLoadedComplete`, `_startupReadinessLogEmitted`
- ✅ Graceful degradation - BarsArray[1] guard, MMIO mirror failure non-fatal

**Critical Logic**:
- **Initialization Order**: SetDefaults → Configure → DataLoaded → Realtime (MUST be preserved)
- **StickyState Timing**: Load in `Init_Services()` BEFORE `StartIpcServer()` so GET_LAYOUT serves persisted state
- **Backward Compat**: `ConfiguredTargetCount=0` auto-detects from TargetValue fields (Build 984)
- **Termination Sequence**: `_isTerminating=true` → `StopWatchdog()` → shutdown services → cleanup (INV-7.1, INV-7.2)

### 5. V12_002.cs (partial - main strategy file)
**Purpose**: Strategy entry point and field declarations  
**Key Components**:
- Field declarations for all runtime state
- Mode profile dictionary: `_modeProfiles` (ConcurrentDictionary<string, ModeConfigProfile>)
- Sticky state fields: `_stickyStatePath`, `_stickyStateDirty`, `_stickyWritePending`
- Config mode helpers: `GetCurrentConfigMode()`, `TryParseTargetMode()`

**V12 DNA Compliance**:
- ✅ Zero lock() - All state uses atomic primitives or concurrent collections
- ✅ Volatile flags - `_stickyStateDirty` for dirty tracking
- ✅ Interlocked gates - `_stickyWritePending` for debounce coalescing

---

## Test Suite Architecture

### Test Class Structure
```csharp
public class ConfigurationIntegrationTests
{
    #region Mock NinjaTrader Types (enums, minimal types)
    #region Mock Infrastructure (MockTime, MockFileSystem, MockIpcQueue, MockNinjaTraderXml)
    #region Test Helpers (Assertion, Verification, Simulation, Creation)
    #region Phase 1: Property Loading Tests (T01-T05)
    #region Phase 2: StickyState Persistence Tests (T06-T10)
    #region Phase 3: IPC Config Updates Tests (T11-T15)
    #region Phase 4: Mode Profile Tests (T16-T20)
    #region Phase 5: Concurrency & Edge Cases Tests (T21-T25)
}
```

### Mock Harness Design

#### MockFileSystem
**Purpose**: Simulate `.v12state` file I/O without touching disk  
**Methods**:
- `WriteFile(path, content)` - Simulates atomic write (`.tmp` → rename)
- `ReadFile(path)` → `string` - Returns persisted content or null
- `FileExists(path)` → `bool` - Check if file exists in mock
- `DeleteFile(path)` - Remove file from mock
- `CorruptFile(path)` - Simulate partial write (for corruption tests)
- `GetWriteCount(path)` → `int` - Track write frequency for debounce tests

**Implementation**:
```csharp
private class MockFileSystem
{
    private ConcurrentDictionary<string, string> _files;
    private ConcurrentDictionary<string, int> _writeCounts;
    
    public void WriteFile(string path, string content)
    {
        _files[path] = content;
        _writeCounts.AddOrUpdate(path, 1, (k, v) => v + 1);
    }
    
    public string ReadFile(string path)
    {
        return _files.TryGetValue(path, out var content) ? content : null;
    }
    
    public bool FileExists(string path) => _files.ContainsKey(path);
    
    public void CorruptFile(string path)
    {
        if (_files.ContainsKey(path))
            _files[path] = _files[path].Substring(0, _files[path].Length / 2);
    }
    
    public int GetWriteCount(string path)
    {
        return _writeCounts.TryGetValue(path, out var count) ? count : 0;
    }
}
```

#### MockIpcQueue
**Purpose**: Simulate IPC command queue for config updates  
**Methods**:
- `Enqueue(command)` - Add IPC command to queue
- `TryDequeue()` → `string` - Dequeue next command
- `Count` → `int` - Queue depth
- `Clear()` - Drain queue

**Implementation**:
```csharp
private class MockIpcQueue
{
    private ConcurrentQueue<string> _queue;
    
    public void Enqueue(string command) => _queue.Enqueue(command);
    public bool TryDequeue(out string command) => _queue.TryDequeue(out command);
    public int Count => _queue.Count;
    public void Clear() { while (_queue.TryDequeue(out _)) { } }
}
```

#### MockTime
**Purpose**: Deterministic time simulation for debounce testing  
**Methods**:
- `GetTicks()` → `long` - Current mock time
- `Advance(deltaTicks)` - Fast-forward time
- `AdvanceMilliseconds(ms)` - Fast-forward by milliseconds
- `GetDateTime()` → `DateTime` - Current mock DateTime

**Implementation**: (Same as REAPERDefenseIntegrationTests.cs)

#### MockNinjaTraderXml
**Purpose**: Simulate NinjaTrader XML property loading  
**Methods**:
- `SetProperty(name, value)` - Set property value
- `GetProperty(name)` → `object` - Get property value
- `LoadDefaults()` - Load default property values
- `SimulateWorkspaceLoad(properties)` - Bulk load from workspace XML

**Implementation**:
```csharp
private class MockNinjaTraderXml
{
    private Dictionary<string, object> _properties;
    
    public void SetProperty(string name, object value)
    {
        _properties[name] = value;
    }
    
    public object GetProperty(string name)
    {
        return _properties.TryGetValue(name, out var value) ? value : null;
    }
    
    public void LoadDefaults()
    {
        // Simulate OnStateChangeSetDefaults()
        _properties["Target1Value"] = 1.0;
        _properties["Target2Value"] = 0.5;
        _properties["StopMultiplier"] = 0.5;
        // ... (80+ properties)
    }
    
    public void SimulateWorkspaceLoad(Dictionary<string, object> properties)
    {
        foreach (var kvp in properties)
            _properties[kvp.Key] = kvp.Value;
    }
}
```

---

## Test Scenarios (25 Tests Across 5 Phases)

### Phase 1: Property Loading (T01-T05)

#### T01_PropertyLoading_ColdStart_LoadsDefaults
**Purpose**: Verify OnStateChangeSetDefaults() initializes all 80+ properties  
**Setup**:
- MockNinjaTraderXml with no workspace XML
- Call `LoadDefaults()`

**Actions**:
1. Simulate State.SetDefaults
2. Load default property values

**Assertions**:
- `Target1Value == 1.0`
- `Target2Value == 0.5`
- `StopMultiplier == 0.5`
- `MinimumStop == 4.0` (Build 1102Z-A F2)
- `MaximumStop == 15.0`
- `ConfiguredTargetCount == 5`
- `EnableSIMA == false` (safety default)
- `ReaperAuditEnabled == true`
- `NakedPositionGraceSec == 5`

**Edge Cases**:
- All 80+ properties have valid defaults
- No null or uninitialized properties

---

#### T02_PropertyLoading_WarmStart_LoadsFromXml
**Purpose**: Verify workspace XML overrides defaults  
**Setup**:
- MockNinjaTraderXml with workspace XML
- Set custom values: `Target1Value=2.0`, `StopMultiplier=0.75`

**Actions**:
1. Load defaults
2. Simulate workspace XML load
3. Apply XML overrides

**Assertions**:
- `Target1Value == 2.0` (XML override)
- `StopMultiplier == 0.75` (XML override)
- `Target2Value == 0.5` (default, not in XML)

**Edge Cases**:
- Partial XML (some properties missing) uses defaults for missing
- Invalid XML values ignored (defaults retained)

---

#### T03_PropertyLoading_BackwardCompat_ConfiguredTargetCount
**Purpose**: Verify backward compatibility for `ConfiguredTargetCount=0` (Build 984)  
**Setup**:
- MockNinjaTraderXml with `ConfiguredTargetCount=0`
- Set `Target1Value=1.0`, `Target2Value=0.5`, `Target3Value=0`, `Target4Value=0`, `Target5Value=0`

**Actions**:
1. Load properties
2. Detect `ConfiguredTargetCount=0`
3. Auto-detect from TargetValue fields

**Assertions**:
- `activeTargetCount == 2` (T1 and T2 have values > 0)
- `ConfiguredTargetCount == 2` (auto-updated)
- Log message: `[COMPAT] ConfiguredTargetCount was 0 -- auto-detected 2 targets`

**Edge Cases**:
- All TargetValues=0 → `activeTargetCount=1` (minimum)
- All TargetValues>0 → `activeTargetCount=5` (maximum)

---

#### T04_PropertyLoading_Validation_RangeConstraints
**Purpose**: Verify property range validation  
**Setup**:
- MockNinjaTraderXml with out-of-range values
- `MESMinimum=-5`, `MESMaximum=200`, `BoxOpacity=300`

**Actions**:
1. Load properties
2. Apply range constraints

**Assertions**:
- `MESMinimum == 1` (clamped to min)
- `MESMaximum == 100` (clamped to max)
- `BoxOpacity == 255` (clamped to max)

**Edge Cases**:
- Negative values clamped to minimum
- Excessive values clamped to maximum

---

#### T05_PropertyLoading_InstrumentConfig_SymbolDetection
**Purpose**: Verify instrument-specific config (MES vs MGC)  
**Setup**:
- MockNinjaTraderXml with `Instrument.MasterInstrument.Name = "MES 03-25"`
- `MESMinimum=2`, `MESMaximum=30`

**Actions**:
1. Simulate State.DataLoaded
2. Call `Init_InstrumentConfig("MES 03-25")`

**Assertions**:
- `minContracts == 2` (MES minimum)
- `maxContracts == 30` (MES maximum)
- `tickSize` set from instrument
- `pointValue` set from instrument

**Edge Cases**:
- MGC symbol → uses MGC min/max
- Unknown symbol → uses conservative defaults (min=1, max=20)

---

### Phase 2: StickyState Persistence (T06-T10)

#### T06_StickyState_Save_SerializesConfig
**Purpose**: Verify `SerializeStickyState()` produces valid INI format  
**Setup**:
- MockFileSystem
- Set runtime config: `Target1Value=2.0`, `activeTargetCount=3`, `isRMAModeActive=true`

**Actions**:
1. Call `SerializeStickyState()`
2. Parse output INI

**Assertions**:
- Contains `[CONFIG]` section
- `MODE=RMA`
- `COUNT=3`
- `T1=2.0`
- `T1TYPE=Points`
- Contains `[FLEET]` section
- Contains `[ANCHOR]` section
- Contains `[POSITIONS]` section

**Edge Cases**:
- Empty activePositions → `[POSITIONS]` section empty
- No fleet accounts → `[FLEET]` section minimal

---

#### T07_StickyState_Load_DeserializesConfig
**Purpose**: Verify `LoadStickyState()` hydrates from INI  
**Setup**:
- MockFileSystem with `.v12state` file
- INI content: `[CONFIG]\nMODE=OR\nCOUNT=4\nT1=1.5\n...`

**Actions**:
1. Call `LoadStickyState()`
2. Check runtime config

**Assertions**:
- `activeTargetCount == 4`
- `Target1Value == 1.5`
- `isRMAModeActive == false` (MODE=OR, safety gate Build 1108.002)
- Log message: `[STICKY] Loaded N settings from StickyState_*.v12state`

**Edge Cases**:
- Missing file → returns false, uses defaults
- Corrupt INI → catches exception, uses defaults
- Partial INI → applies valid lines, ignores invalid

---

#### T08_StickyState_Debounce_CoalescesWrites
**Purpose**: Verify 50ms debounce coalesces multiple mutations  
**Setup**:
- MockFileSystem
- MockTime
- Set `_stickyWritePending=0`

**Actions**:
1. Call `MarkStickyDirty()` (mutation 1)
2. Advance time 10ms
3. Call `MarkStickyDirty()` (mutation 2)
4. Advance time 10ms
5. Call `MarkStickyDirty()` (mutation 3)
6. Advance time 50ms (debounce expires)

**Assertions**:
- `MockFileSystem.GetWriteCount() == 1` (single write, not 3)
- `_stickyWritePending == 0` (gate cleared after write)

**Edge Cases**:
- Rapid mutations (10 in 100ms) → single write at 50ms
- Mutation during write → schedules another write after completion

---

#### T09_StickyState_AtomicWrite_CorruptionFree
**Purpose**: Verify `.tmp` → rename pattern prevents corruption  
**Setup**:
- MockFileSystem
- Simulate process kill mid-write

**Actions**:
1. Start `AtomicWriteFile()`
2. Write to `.tmp` file
3. Simulate crash before rename
4. Verify `.tmp` exists, target file unchanged

**Assertions**:
- `.v12state.tmp` exists with partial content
- `.v12state` unchanged (or doesn't exist)
- On retry: `.tmp` deleted, new `.tmp` created, rename succeeds

**Edge Cases**:
- Target file doesn't exist → `.tmp` → rename creates new file
- Target file exists → `.tmp` → delete target → rename replaces

---

#### T10_StickyState_ModeProfiles_SnapshotRestore
**Purpose**: Verify per-mode config snapshots (Build 1106)  
**Setup**:
- MockFileSystem
- Set OR mode config: `Target1Value=1.0`, `StopMultiplier=0.5`
- Switch to RMA mode
- Set RMA mode config: `Target1Value=2.0`, `RMAStopATRMultiplier=1.1`

**Actions**:
1. Snapshot OR config → `_modeProfiles["OR"]`
2. Switch to RMA mode
3. Snapshot RMA config → `_modeProfiles["RMA"]`
4. Switch back to OR mode
5. Hydrate from `_modeProfiles["OR"]`

**Assertions**:
- After switch to OR: `Target1Value == 1.0`, `StopMultiplier == 0.5`
- After switch to RMA: `Target1Value == 2.0`, `RMAStopATRMultiplier == 1.1`
- `_modeProfiles` contains 2 entries: "OR", "RMA"

**Edge Cases**:
- First switch to mode → creates new profile with current config
- Switch to mode with no profile → uses current config (no hydration)

---

### Phase 3: IPC Config Updates (T11-T15)

#### T11_IpcConfig_HandleConfigCommand_ParsesFormat
**Purpose**: Verify `HandleConfigCommand()` parses CONFIG format  
**Setup**:
- MockIpcQueue
- Enqueue: `CONFIG|OR|COUNT:3;T1:1.5;T1TYPE:Points;T2:0.5;T2TYPE:ATR;STR:0.6;MAX:250`

**Actions**:
1. Dequeue command
2. Parse parts: `["CONFIG", "OR", "COUNT:3;T1:1.5;..."]`
3. Split settings: `["COUNT:3", "T1:1.5", ...]`
4. Apply each setting

**Assertions**:
- `activeTargetCount == 3`
- `Target1Value == 1.5`
- `T1Type == TargetMode.Points`
- `Target2Value == 0.5`
- `T2Type == TargetMode.ATR`
- `StopMultiplier == 0.6`
- `MaxRiskAmount == 250`
- `MarkStickyDirty()` called (persistence triggered)

**Edge Cases**:
- Empty settings → no changes
- Invalid key → ignored
- Invalid value → rejected with log

---

#### T12_IpcConfig_Validation_RejectsInvalidMultipliers
**Purpose**: Verify `ValidateIpcMultiplier()` rejects invalid inputs  
**Setup**:
- MockIpcQueue
- Enqueue: `CONFIG|OR|T1:-1.0;T2:999.0;STR:0.0`

**Actions**:
1. Process CONFIG command
2. Validate each multiplier

**Assertions**:
- `Target1Value` unchanged (rejected: negative)
- `Target2Value` unchanged (rejected: excessive)
- `StopMultiplier` unchanged (rejected: zero)
- Log messages: `[IPC REJECT] T1 value -1.0 rejected: ...`

**Edge Cases**:
- Negative values rejected
- Zero values rejected (for multipliers)
- Excessive values (>100) rejected

---

#### T13_IpcConfig_ToggleAccount_ResolvesAliases
**Purpose**: Verify `HandleToggleAccountCommand()` resolves UI aliases (Build 935)  
**Setup**:
- MockIpcQueue
- Fleet accounts: `{"Apex_F01_12345": true, "Apex_F02_67890": true}`
- Alias map: `{"F01": "Apex_F01_12345", "F02": "Apex_F02_67890"}`
- Enqueue: `TOGGLE_ACCOUNT|F01|0`

**Actions**:
1. Dequeue command
2. Resolve alias "F01" → "Apex_F01_12345"
3. Update `activeFleetAccounts["Apex_F01_12345"] = false`

**Assertions**:
- `activeFleetAccounts["Apex_F01_12345"] == false`
- `activeFleetAccounts["Apex_F02_67890"] == true` (unchanged)
- Log message: `[V12.2] TOGGLE_ACCOUNT: Apex_F01_12345 (resolved from 'F01') | Active=False`
- `MarkStickyDirty()` called

**Edge Cases**:
- Unresolvable alias → rejected with log
- Real account name (no alias) → used directly

---

#### T14_IpcConfig_TrimCommand_CalculatesQuantity
**Purpose**: Verify `HandleTrimCommand()` calculates trim quantity  
**Setup**:
- MockIpcQueue
- Active position: `RemainingContracts=10`
- Enqueue: `TRIM_50`

**Actions**:
1. Dequeue command
2. Calculate trim: `Math.Max(1, (int)Math.Floor(10 * 0.5)) = 5`
3. Verify remaining: `10 - 5 = 5 >= 1` (safety check)
4. Submit trim order

**Assertions**:
- Trim quantity == 5
- Remaining after trim == 5
- Order submitted: `OrderAction.Sell`, `OrderType.Market`, `Quantity=5`

**Edge Cases**:
- `RemainingContracts=1` → trim skipped (log: "only 1 contract")
- `RemainingContracts=2`, `TRIM_50` → trim 1, remaining 1
- Trim would flatten → clamped to leave 1 contract

---

#### T15_IpcConfig_ModeProfileUpdate_AfterConfigSync
**Purpose**: Verify mode profile updated after CONFIG sync (Build 1106)  
**Setup**:
- MockIpcQueue
- Current mode: OR
- Enqueue: `CONFIG|OR|COUNT:4;T1:2.0;STR:0.7`

**Actions**:
1. Process CONFIG command
2. Apply settings
3. Update `_modeProfiles["OR"]` with current config

**Assertions**:
- `_modeProfiles["OR"].TargetCount == 4`
- `_modeProfiles["OR"].T1 == 2.0`
- `_modeProfiles["OR"].StopMult == 0.7`

**Edge Cases**:
- First CONFIG for mode → creates new profile
- Subsequent CONFIG → updates existing profile

---

### Phase 4: Mode Profiles (T16-T20)

#### T16_ModeProfile_Snapshot_CapturesCurrentConfig
**Purpose**: Verify `SnapshotCurrentConfig()` captures all config fields  
**Setup**:
- Set runtime config: `activeTargetCount=3`, `Target1Value=1.5`, `T1Type=Points`, `StopMultiplier=0.6`

**Actions**:
1. Call `SnapshotCurrentConfig()`
2. Inspect returned `ModeConfigProfile`

**Assertions**:
- `profile.TargetCount == 3`
- `profile.T1 == 1.5`
- `profile.T1Type == TargetMode.Points`
- `profile.StopMult == 0.6`
- All 5 targets captured (T1-T5)
- All 5 target types captured

**Edge Cases**:
- RMA mode → captures `RMAStopATRMultiplier` instead of `StopMultiplier`
- OR mode → captures `StopMultiplier`

---

#### T17_ModeProfile_Hydrate_RestoresConfig
**Purpose**: Verify `HydrateFromProfile()` restores config from profile  
**Setup**:
- Create profile: `TargetCount=4`, `T1=2.0`, `T1Type=ATR`, `StopMult=0.8`
- Current config different: `activeTargetCount=3`, `Target1Value=1.0`

**Actions**:
1. Call `HydrateFromProfile(profile, "OR")`
2. Check runtime config

**Assertions**:
- `activeTargetCount == 4`
- `Target1Value == 2.0`
- `T1Type == TargetMode.ATR`
- `StopMultiplier == 0.8`

**Edge Cases**:
- RMA mode → hydrates to `RMAStopATRMultiplier`
- OR mode → hydrates to `StopMultiplier`
- Invalid TargetCount → clamped to 1-5

---

#### T18_ModeProfile_Switch_ORtoRMA_HydratesProfile
**Purpose**: Verify mode switch hydrates profile  
**Setup**:
- OR profile: `T1=1.0`, `StopMult=0.5`
- RMA profile: `T1=2.0`, `StopMult=1.1`
- Current mode: OR

**Actions**:
1. Switch to RMA mode
2. Hydrate from `_modeProfiles["RMA"]`

**Assertions**:
- `Target1Value == 2.0`
- `RMAStopATRMultiplier == 1.1`
- `isRMAModeActive == true`

**Edge Cases**:
- Switch to mode with no profile → uses current config (no hydration)
- Switch back to OR → hydrates OR profile

---

#### T19_ModeProfile_Persistence_SavesAllProfiles
**Purpose**: Verify all mode profiles saved to `.v12state`  
**Setup**:
- MockFileSystem
- Create profiles: OR, RMA, TREND
- Call `SerializeStickyState()`

**Actions**:
1. Serialize state
2. Parse INI output

**Assertions**:
- Contains `[CONFIG_OR]` section
- Contains `[CONFIG_RMA]` section
- Contains `[CONFIG_TREND]` section
- Each section has: `COUNT`, `T1`, `T1TYPE`, `STR`, `MAX`

**Edge Cases**:
- No profiles → no `[CONFIG_*]` sections
- Partial profiles → only existing profiles saved

---

#### T20_ModeProfile_Load_HydratesAllProfiles
**Purpose**: Verify all mode profiles loaded from `.v12state`  
**Setup**:
- MockFileSystem with `.v12state` containing `[CONFIG_OR]`, `[CONFIG_RMA]`
- Call `LoadStickyState()`

**Actions**:
1. Load state
2. Check `_modeProfiles`

**Assertions**:
- `_modeProfiles.ContainsKey("OR") == true`
- `_modeProfiles.ContainsKey("RMA") == true`
- `_modeProfiles["OR"].TargetCount` matches INI
- `_modeProfiles["RMA"].T1` matches INI

**Edge Cases**:
- Missing profile section → profile not created
- Invalid profile data → profile skipped

---

### Phase 5: Concurrency & Edge Cases (T21-T25)

#### T21_Concurrency_DebounceGate_PreventsDuplicateWrites
**Purpose**: Verify `Interlocked.CompareExchange` prevents duplicate writes  
**Setup**:
- MockFileSystem
- MockTime
- Simulate concurrent `MarkStickyDirty()` calls

**Actions**:
1. Thread 1: Call `MarkStickyDirty()` (sets `_stickyWritePending=1`)
2. Thread 2: Call `MarkStickyDirty()` (CAS fails, no duplicate write scheduled)
3. Advance time 50ms
4. Write completes, `_stickyWritePending=0`

**Assertions**:
- Only 1 write scheduled (not 2)
- `MockFileSystem.GetWriteCount() == 1`
- `_stickyWritePending == 0` after write

**Edge Cases**:
- 10 concurrent calls → 1 write
- Mutation during write → schedules another write after completion

---

#### T22_Concurrency_IpcConfigUpdate_ThreadSafe
**Purpose**: Verify IPC config updates are thread-safe  
**Setup**:
- MockIpcQueue
- Simulate concurrent CONFIG commands from multiple IPC clients

**Actions**:
1. Thread 1: Enqueue `CONFIG|OR|T1:1.5`
2. Thread 2: Enqueue `CONFIG|OR|T2:0.8`
3. Process both commands

**Assertions**:
- `Target1Value == 1.5`
- `Target2Value == 0.8`
- No race conditions or lost updates

**Edge Cases**:
- Concurrent updates to same property → last write wins
- Concurrent updates to different properties → both applied

---

#### T23_EdgeCase_CorruptStickyState_FallsBackToDefaults
**Purpose**: Verify corrupt `.v12state` falls back to defaults  
**Setup**:
- MockFileSystem with corrupt `.v12state` (truncated mid-line)
- Call `LoadStickyState()`

**Actions**:
1. Attempt to load state
2. Catch exception
3. Fall back to defaults

**Assertions**:
- `LoadStickyState()` returns false
- Log message: `[STICKY] Load failed (using defaults): ...`
- Runtime config uses defaults (not corrupt values)

**Edge Cases**:
- Missing file → returns false, uses defaults
- Invalid INI syntax → catches exception, uses defaults
- Partial INI → applies valid lines, ignores invalid

---

#### T24_EdgeCase_MissingStickyState_CreatesOnFirstSave
**Purpose**: Verify missing `.v12state` created on first save  
**Setup**:
- MockFileSystem with no `.v12state` file
- Call `MarkStickyDirty()`

**Actions**:
1. Trigger debounced write
2. Advance time 50ms
3. Write completes

**Assertions**:
- `MockFileSystem.FileExists(".v12state") == true`
- File contains valid INI content
- `[CONFIG]` section present

**Edge Cases**:
- First run → creates new file
- Subsequent runs → updates existing file

---

#### T25_EdgeCase_SafetyGate_ClickTraderModesNeverAutoRearm
**Purpose**: Verify click-trader modes never auto-rearm on startup (Build 1108.002)  
**Setup**:
- MockFileSystem with `.v12state` containing `MODE=RMA`
- Call `LoadStickyState()`

**Actions**:
1. Load state
2. Apply `MODE=RMA` from INI
3. Safety gate forces `MODE=OR`

**Assertions**:
- `isRMAModeActive == false` (forced to OR)
- `isRMAButtonClicked == false`
- `isTRENDModeActive == false`
- `isRetestModeActive == false`
- `isMOMOModeActive == false`
- `isFFMAModeArmed == false`
- Log message: `[STICKY] MODE on disk was RMA -- forced to OR (safety gate)`

**Edge Cases**:
- Any click-trader mode (RMA, TREND, RETEST, MOMO, FFMA) → forced to OR
- OR mode → no change

---

## Mock Implementation Details

### Helper Methods (25 methods)

#### Assertion Helpers (12 methods)
```csharp
private void AssertPropertyValue(object actual, object expected, string propertyName)
private void AssertFileExists(MockFileSystem fs, string path)
private void AssertFileContent(MockFileSystem fs, string path, string expectedContent)
private void AssertIniSection(string ini, string sectionName)
private void AssertIniKeyValue(string ini, string section, string key, string expectedValue)
private void AssertModeProfileExists(Dictionary<string, ModeConfigProfile> profiles, string mode)
private void AssertModeProfileValue(ModeConfigProfile profile, string field, object expectedValue)
private void AssertDebounceCoalesced(MockFileSystem fs, string path, int expectedWriteCount)
private void AssertIpcCommandParsed(string command, string[] expectedParts)
private void AssertConfigApplied(string key, object expectedValue)
private void AssertValidationRejected(string logMessage, string expectedReason)
private void AssertStickyDirtyFlagSet(bool expected)
```

#### Verification Helpers (6 methods)
```csharp
private bool VerifyPropertyLoaded(string propertyName, object expectedValue)
private bool VerifyStickyStateValid(string iniContent)
private bool VerifyModeProfileComplete(ModeConfigProfile profile)
private bool VerifyIpcCommandValid(string command)
private bool VerifyAtomicWriteComplete(MockFileSystem fs, string path)
private bool VerifyDebounceGateCleared(long writePending)
```

#### Simulation Helpers (4 methods)
```csharp
private void SimulatePropertyLoad(MockNinjaTraderXml xml, Dictionary<string, object> properties)
private void SimulateStickyStateLoad(MockFileSystem fs, string iniContent)
private void SimulateIpcCommand(MockIpcQueue queue, string command)
private void SimulateModeSwitch(string fromMode, string toMode)
```

#### Creation Helpers (3 methods)
```csharp
private MockFileSystem CreateMockFileSystem()
private MockIpcQueue CreateMockIpcQueue()
private MockNinjaTraderXml CreateMockNinjaTraderXml()
```

---

## V12 DNA Compliance Verification

### Lock-Free Verification Strategy
**Approach**: Static analysis + runtime assertion  
**Tests**:
- T21: Verify `Interlocked.CompareExchange` for debounce gate
- T22: Verify concurrent IPC updates use `ConcurrentDictionary`
- All tests: Assert zero `lock()` statements in source files

### MockTime Usage (Zero Thread.Sleep)
**Approach**: All time-dependent tests use `MockTime.Advance()`  
**Tests**:
- T08: Debounce coalescing uses `MockTime.AdvanceMilliseconds(50)`
- T21: Concurrent debounce uses `MockTime.Advance()`
- No `Thread.Sleep()` in any test

### Atomic Primitives for Concurrency
**Approach**: Verify `Interlocked` and `volatile` usage  
**Tests**:
- T21: `Interlocked.CompareExchange` for `_stickyWritePending`
- T08: `volatile bool _stickyStateDirty` for dirty flag
- T22: `ConcurrentDictionary` for `activeFleetAccounts`

### ASCII-Only String Validation
**Approach**: Verify all property names and INI keys are ASCII  
**Tests**:
- T01-T05: Property names contain only ASCII characters
- T06-T10: INI keys and values contain only ASCII characters
- T25: Safety gate log messages contain only ASCII characters

---

## Reference Patterns from REAPERDefenseIntegrationTests.cs

### Test Naming Convention
**Pattern**: `T{NN}_{Component}_{Scenario}_{ExpectedOutcome}`  
**Examples**:
- `T01_PropertyLoading_ColdStart_LoadsDefaults`
- `T06_StickyState_Save_SerializesConfig`
- `T11_IpcConfig_HandleConfigCommand_ParsesFormat`

### Test Structure
**Pattern**: Given-When-Then with inline comments  
```csharp
[Fact]
public void T01_PropertyLoading_ColdStart_LoadsDefaults()
{
    // Given: MockNinjaTraderXml with no workspace XML
    var xml = CreateMockNinjaTraderXml();
    
    // When: Load defaults
    xml.LoadDefaults();
    
    // Then: All properties initialized
    AssertPropertyValue(xml.GetProperty("Target1Value"), 1.0, "Target1Value");
    AssertPropertyValue(xml.GetProperty("StopMultiplier"), 0.5, "StopMultiplier");
}
```

### Assertion Patterns
**Pattern**: Descriptive assertion messages  
```csharp
Assert.Equal(expected, actual, $"Property {name} should be {expected}");
Assert.True(condition, $"Condition {description} should be true");
```

### Documentation Style
**Pattern**: XML doc comments for test purpose  
```csharp
/// <summary>
/// T01: Verify OnStateChangeSetDefaults() initializes all 80+ properties.
/// Tests cold start scenario with no workspace XML.
/// </summary>
[Fact]
public void T01_PropertyLoading_ColdStart_LoadsDefaults() { ... }
```

---

## Estimated Test File Size

**Line Count Breakdown**:
- Mock Infrastructure: ~400 lines (MockFileSystem, MockIpcQueue, MockTime, MockNinjaTraderXml)
- Test Helpers: ~300 lines (25 methods × 12 lines avg)
- Phase 1 Tests (T01-T05): ~250 lines (5 tests × 50 lines avg)
- Phase 2 Tests (T06-T10): ~300 lines (5 tests × 60 lines avg)
- Phase 3 Tests (T11-T15): ~300 lines (5 tests × 60 lines avg)
- Phase 4 Tests (T16-T20): ~250 lines (5 tests × 50 lines avg)
- Phase 5 Tests (T21-T25): ~300 lines (5 tests × 60 lines avg)
- Documentation & Comments: ~200 lines

**Total Estimated**: ~2,300 lines

**Comparison to REAPERDefenseIntegrationTests.cs**: 997 lines (30 tests)  
**S5 Ratio**: 2,300 / 25 tests = 92 lines/test (vs 33 lines/test for S4)  
**Justification**: Configuration tests require more setup (MockFileSystem, INI parsing) and more assertions (80+ properties, multi-section INI validation)

---

## Key Architectural Decisions

### 1. MockFileSystem Over Real Disk I/O
**Rationale**: Deterministic, fast, no cleanup required  
**Trade-off**: Doesn't test actual file system behavior (atomic rename, permissions)  
**Mitigation**: Document that atomic write pattern is tested in isolation, not end-to-end

### 2. MockTime for Debounce Testing
**Rationale**: Deterministic, no Thread.Sleep, fast tests  
**Trade-off**: Doesn't test actual async Task.Delay behavior  
**Mitigation**: Document that debounce logic is tested, not Task.Delay implementation

### 3. MockIpcQueue Over Real TCP Sockets
**Rationale**: Deterministic, no network dependencies, fast  
**Trade-off**: Doesn't test actual IPC server/client behavior  
**Mitigation**: Document that IPC command parsing is tested, not network layer

### 4. MockNinjaTraderXml Over Real XML Serialization
**Rationale**: Deterministic, no NinjaTrader framework dependencies  
**Trade-off**: Doesn't test actual XML serialization behavior  
**Mitigation**: Document that property loading logic is tested, not XML framework

### 5. 25 Tests (Not 30)
**Rationale**: Configuration cluster has fewer distinct behaviors than REAPER (no emergency queues, no grace windows, no escalation)  
**Trade-off**: Less comprehensive coverage  
**Mitigation**: Focus on critical paths (persistence, IPC, mode profiles) and edge cases (corruption, concurrency)

---

## Next Steps (P4 Vetting Gate)

1. **Arena AI Review**: Adversarial audit of test plan
2. **Director Approval**: Sign-off on test scope and structure
3. **P5 Implementation**: Bob CLI executes test suite creation
4. **P6 Verification**: Forensics audit of implemented tests

---

**P3 Architecture Planning Complete**  
**Status**: ✅ READY FOR P4 VETTING GATE  
**Confidence**: HIGH (Clear persistence patterns, well-defined IPC protocol, mode profile system)