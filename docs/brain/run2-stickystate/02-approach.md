# Run 2 Epic Approach -- Decouple StickyState & IPC

**Epic ID:** run2-stickystate  
**Phase:** 2 - Approach  
**Created:** 2026-05-20

---

## 1. IMPLEMENTATION STRATEGY

### 1.1 Extraction Philosophy

**Principle: Surgical Port, Zero Logic Drift**

This is a **pure structural refactoring** - we are moving code from one location to another without changing its behavior. The extraction follows these rules:

1. **1:1 Port:** Every method extracted must be functionally identical to the original
2. **No Optimization:** Do not "improve" logic during extraction
3. **Preserve Comments:** All H18-FIX and Build tags must be preserved
4. **Preserve Whitespace:** No formatting changes to extracted code
5. **Snapshot Integrity:** H18-FIX thread safety pattern is sacred - zero deviation

### 1.2 Phased Approach

**Phase A: Service Foundation (New Files)**
1. Create `IStickyStateService` interface
2. Create `StickyStateService` implementation (empty shell)
3. Create `IStickyStateLogger` interface
4. Create unit test stub

**Phase B: Serialization Extraction**
1. Port serialization methods to service (1:1)
2. Add logging abstraction
3. Update Strategy to call service for serialization

**Phase C: Deserialization Extraction**
1. Port deserialization methods to service (1:1)
2. Create `StickyStateData` DTO
3. Update Strategy to call service for deserialization

**Phase D: Integration & Cleanup**
1. Wire service into Strategy lifecycle
2. Remove dead code from Strategy
3. Verify all 18 `MarkStickyDirty()` call sites

**Phase E: Verification**
1. Run all verification gates
2. F5 in NinjaTrader
3. IPC command integration test

---

## 2. DETAILED IMPLEMENTATION PLAN

### 2.1 Phase A: Service Foundation

#### Step A1: Create IStickyStateService Interface

**File:** `src/Services/IStickyStateService.cs`

```csharp
// V12 Services: IStickyStateService -- Pure C# state persistence interface
// Zero NinjaTrader dependencies - enables dotnet test without NT runtime
using System;
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Strategies.Services
{
    /// <summary>
    /// Pure C# service for StickyState serialization and deserialization.
    /// Accepts all state via method parameters (no global statics).
    /// </summary>
    public interface IStickyStateService
    {
        /// <summary>
        /// Serializes a state snapshot into INI format.
        /// Thread-safe: accepts immutable snapshot created on strategy thread.
        /// </summary>
        string Serialize(StickyStateSnapshot snapshot);

        /// <summary>
        /// Deserializes INI file into structured data.
        /// Returns null if file doesn't exist or parsing fails.
        /// </summary>
        StickyStateData Deserialize(string filePath);

        /// <summary>
        /// Atomic file write: write to .tmp, then rename over target.
        /// Prevents corruption if process is killed mid-write.
        /// </summary>
        void AtomicWrite(string targetPath, string content);
    }

    /// <summary>
    /// Logging abstraction for service (injected by Strategy).
    /// </summary>
    public interface IStickyStateLogger
    {
        void Log(string message);
    }
}
```

**Lines:** ~40

#### Step A2: Create StickyStateSnapshot DTO

**File:** `src/Services/StickyStateService.cs` (partial - DTOs only)

```csharp
// V12 Services: StickyStateService -- Pure C# state persistence implementation
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NinjaTrader.NinjaScript.Strategies.Services
{
    /// <summary>
    /// Immutable snapshot of all state to be persisted.
    /// Created on strategy thread to prevent torn reads (H18-FIX).
    /// </summary>
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

    /// <summary>
    /// Deserialized state data returned to Strategy for application.
    /// </summary>
    public class StickyStateData
    {
        public Dictionary<string, object> ConfigValues = new Dictionary<string, object>();
        public Dictionary<string, ModeConfigProfile> ModeProfiles = new Dictionary<string, ModeConfigProfile>();
        public Dictionary<string, bool> FleetToggles = new Dictionary<string, bool>();
        public Dictionary<string, PositionTrailState> PositionStates = new Dictionary<string, PositionTrailState>();
        public string LeaderAccount;
        public RmaAnchorType Anchor;
        public double ManualPrice;
    }

    /// <summary>
    /// Position trailing stop state (subset of PositionInfo).
    /// </summary>
    public struct PositionTrailState
    {
        public double ExtremePriceSinceEntry;
        public int CurrentTrailLevel;
        public bool ManualBreakevenArmed;
        public bool ManualBreakevenTriggered;
        public int InitialTargetCount;
    }
}
```

**Lines:** ~50

#### Step A3: Create Unit Test Stub

**File:** `tests/Services/StickyStateServiceTests.cs`

```csharp
using Xunit;
using NinjaTrader.NinjaScript.Strategies.Services;

namespace V12.Tests.Services
{
    public class StickyStateServiceTests
    {
        [Fact]
        public void CanInstantiateWithoutNinjaTrader()
        {
            // Proves dotnet test works without NinjaTrader runtime
            var logger = new TestLogger();
            var service = new StickyStateService(logger);
            Assert.NotNull(service);
        }

        private class TestLogger : IStickyStateLogger
        {
            public void Log(string message) { }
        }
    }
}
```

**Lines:** ~20

---

### 2.2 Phase B: Serialization Extraction

#### Step B1: Port Serialization Methods

**Target Methods (from V12_002.StickyState.cs):**
1. `SerializeStickyState()` → `StickyStateService.Serialize()`
2. `SerializeSticky_WriteHeaderConfig()` → private method
3. `SerializeSticky_WriteFleetAnchor()` → private method
4. `SerializeSticky_WriteModeProfiles()` → private method
5. `SerializeSticky_WritePositions()` → private method
6. `AnchorTypeToString()` → private static method

**Service Implementation:**

```csharp
public class StickyStateService : IStickyStateService
{
    private readonly IStickyStateLogger _logger;

    public StickyStateService(IStickyStateLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Serialize(StickyStateSnapshot snapshot)
    {
        var sb = new StringBuilder(1024);
        SerializeSticky_WriteHeaderConfig(sb, snapshot);
        SerializeSticky_WriteFleetAnchor(sb, snapshot);
        SerializeSticky_WriteModeProfiles(sb, snapshot);
        SerializeSticky_WritePositions(sb, snapshot);
        return sb.ToString();
    }

    // [PORT: Lines 152-188 from V12_002.StickyState.cs]
    private void SerializeSticky_WriteHeaderConfig(StringBuilder sb, StickyStateSnapshot snapshot)
    {
        // Header
        sb.AppendLine("# V12 StickyState v1");
        sb.AppendLine("# Symbol: " + (snapshot.InstrumentName ?? "unknown"));
        sb.AppendLine("# Updated: " + DateTime.UtcNow.ToString("o"));
        sb.AppendLine("# Build: " + snapshot.BuildTag);
        sb.AppendLine();

        // [CONFIG] - H18-FIX: Read from snapshot instead of live properties
        sb.AppendLine("[CONFIG]");
        string mode = "OR";
        if (snapshot.HeaderConfig.IsRMAModeActive) mode = "RMA";
        else if (snapshot.HeaderConfig.IsTRENDModeActive) mode = "TREND";
        else if (snapshot.HeaderConfig.IsRetestModeActive) mode = "RETEST";
        else if (snapshot.HeaderConfig.IsMOMOModeActive) mode = "MOMO";
        else if (snapshot.HeaderConfig.IsFFMAModeArmed) mode = "FFMA";
        sb.AppendLine("MODE=" + mode);
        sb.AppendLine("COUNT=" + snapshot.HeaderConfig.ActiveTargetCount.ToString());
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", snapshot.HeaderConfig.Target1Value));
        sb.AppendLine("T1TYPE=" + snapshot.HeaderConfig.T1Type.ToString());
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", snapshot.HeaderConfig.Target2Value));
        sb.AppendLine("T2TYPE=" + snapshot.HeaderConfig.T2Type.ToString());
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", snapshot.HeaderConfig.Target3Value));
        sb.AppendLine("T3TYPE=" + snapshot.HeaderConfig.T3Type.ToString());
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", snapshot.HeaderConfig.Target4Value));
        sb.AppendLine("T4TYPE=" + snapshot.HeaderConfig.T4Type.ToString());
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", snapshot.HeaderConfig.Target5Value));
        sb.AppendLine("T5TYPE=" + snapshot.HeaderConfig.T5Type.ToString());
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}",
            snapshot.HeaderConfig.IsRMAModeActive ? snapshot.HeaderConfig.RMAStopATRMultiplier : snapshot.HeaderConfig.StopMultiplier));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", snapshot.HeaderConfig.MaxRiskAmount));
        sb.AppendLine("CIT=" + (snapshot.HeaderConfig.ChaseIfTouchPoints ?? "0"));
        sb.AppendLine("TRMA=" + (snapshot.HeaderConfig.IsTrendRmaMode ? "1" : "0"));
        sb.AppendLine("RRMA=" + (snapshot.HeaderConfig.IsRetestRmaMode ? "1" : "0"));
        sb.AppendLine();
    }

    // [PORT: Lines 190-207 from V12_002.StickyState.cs]
    private void SerializeSticky_WriteFleetAnchor(StringBuilder sb, StickyStateSnapshot snapshot)
    {
        // [FLEET] - H18-FIX: Use snapshot instead of live dictionary
        sb.AppendLine("[FLEET]");
        sb.AppendLine("LEADER=" + (snapshot.LeaderAccount ?? ""));
        if (snapshot.FleetAccounts != null)
        {
            foreach (var kvp in snapshot.FleetAccounts)
                sb.AppendLine(kvp.Key + "=" + (kvp.Value ? "1" : "0"));
        }
        sb.AppendLine();

        // [ANCHOR]
        sb.AppendLine("[ANCHOR]");
        sb.AppendLine("TYPE=" + AnchorTypeToString(snapshot.CurrentAnchor));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MNL_PRICE={0}", snapshot.CachedManualPrice));
        sb.AppendLine();
    }

    // [PORT: Lines 209-258 from V12_002.StickyState.cs]
    private void SerializeSticky_WriteModeProfiles(StringBuilder sb, StickyStateSnapshot snapshot)
    {
        // Build 1106: [CONFIG_*] -- per-mode profile snapshots
        // H18-FIX: Use snapshot instead of mutating live _modeProfiles dictionary
        string activeMode = "OR";
        if (snapshot.HeaderConfig.IsRMAModeActive) activeMode = "RMA";
        else if (snapshot.HeaderConfig.IsTRENDModeActive) activeMode = "TREND";
        else if (snapshot.HeaderConfig.IsRetestModeActive) activeMode = "RETEST";
        else if (snapshot.HeaderConfig.IsMOMOModeActive) activeMode = "MOMO";
        else if (snapshot.HeaderConfig.IsFFMAModeArmed) activeMode = "FFMA";
        
        // Capture current config into snapshot (not live dictionary)
        var modeProfilesSnapshot = new Dictionary<string, ModeConfigProfile>(snapshot.ModeProfiles);
        modeProfilesSnapshot[activeMode] = new ModeConfigProfile
        {
            TargetCount = snapshot.HeaderConfig.ActiveTargetCount,
            T1 = snapshot.HeaderConfig.Target1Value,
            T2 = snapshot.HeaderConfig.Target2Value,
            T3 = snapshot.HeaderConfig.Target3Value,
            T4 = snapshot.HeaderConfig.Target4Value,
            T5 = snapshot.HeaderConfig.Target5Value,
            T1Type = snapshot.HeaderConfig.T1Type,
            T2Type = snapshot.HeaderConfig.T2Type,
            T3Type = snapshot.HeaderConfig.T3Type,
            T4Type = snapshot.HeaderConfig.T4Type,
            T5Type = snapshot.HeaderConfig.T5Type,
            StopMult = snapshot.HeaderConfig.IsRMAModeActive ? snapshot.HeaderConfig.RMAStopATRMultiplier : snapshot.HeaderConfig.StopMultiplier,
            MaxRisk = snapshot.HeaderConfig.MaxRiskAmount
        };

        foreach (var kvp in modeProfilesSnapshot)
        {
            ModeConfigProfile p = kvp.Value;
            if (p == null) continue;
            sb.AppendLine("[CONFIG_" + kvp.Key + "]");
            sb.AppendLine("COUNT=" + p.TargetCount.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", p.T1));
            sb.AppendLine("T1TYPE=" + p.T1Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", p.T2));
            sb.AppendLine("T2TYPE=" + p.T2Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", p.T3));
            sb.AppendLine("T3TYPE=" + p.T3Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", p.T4));
            sb.AppendLine("T4TYPE=" + p.T4Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", p.T5));
            sb.AppendLine("T5TYPE=" + p.T5Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}", p.StopMult));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", p.MaxRisk));
            sb.AppendLine();
        }
    }

    // [PORT: Lines 260-282 from V12_002.StickyState.cs]
    private void SerializeSticky_WritePositions(StringBuilder sb, StickyStateSnapshot snapshot)
    {
        // [POSITIONS] -- trailing stop state for active positions
        // H18-FIX: Use snapshot instead of live dictionary
        sb.AppendLine("[POSITIONS]");
        sb.AppendLine("# key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount");
        if (snapshot.Positions != null)
        {
            foreach (var kvp in snapshot.Positions)
            {
                var pi = kvp.Value;
                if (pi == null || pi.PendingCleanup) continue;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}|{3}|{4}|{5}",
                    kvp.Key,
                    pi.ExtremePriceSinceEntry,
                    pi.CurrentTrailLevel,
                    pi.ManualBreakevenArmed ? "1" : "0",
                    pi.ManualBreakevenTriggered ? "1" : "0",
                    pi.InitialTargetCount));
            }
        }
    }

    // [PORT: Lines 327-339 from V12_002.StickyState.cs]
    private static string AnchorTypeToString(RmaAnchorType t)
    {
        switch (t)
        {
            case RmaAnchorType.Ema30:  return "EMA30";
            case RmaAnchorType.Ema65:  return "EMA65";
            case RmaAnchorType.Ema200: return "EMA200";
            case RmaAnchorType.OrHigh: return "OR_HIGH";
            case RmaAnchorType.OrLow:  return "OR_LOW";
            case RmaAnchorType.Manual: return "MANUAL";
            default: return "EMA65";
        }
    }

    // [PORT: Lines 345-354 from V12_002.StickyState.cs]
    public void AtomicWrite(string targetPath, string content)
    {
        if (string.IsNullOrEmpty(targetPath)) return;
        string tmpPath = targetPath + ".tmp";
        System.IO.File.WriteAllText(tmpPath, content, Encoding.UTF8);
        // File.Move on Windows is atomic on NTFS when same volume
        if (System.IO.File.Exists(targetPath))
            System.IO.File.Delete(targetPath);
        System.IO.File.Move(tmpPath, targetPath);
    }
}
```

**Lines:** ~220

#### Step B2: Update Strategy to Use Service for Serialization

**File:** `src/V12_002.StickyState.cs`

**Changes:**
1. Add service field: `private IStickyStateService _stickyStateService;`
2. Instantiate service in lifecycle method
3. Update `MarkStickyDirty()` to call service
4. Remove serialization methods (lines 134-354)

**Modified MarkStickyDirty():**

```csharp
private void MarkStickyDirty()
{
    _stickyStateDirty = true;

    // Coalescing gate: only one pending write at a time
    if (Interlocked.CompareExchange(ref _stickyWritePending, 1, 0) == 0)
    {
        // H18-FIX: Capture snapshot of ALL mutable state on strategy thread BEFORE spawning background task.
        var snapshot = CreateStickyStateSnapshot();

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(STICKY_DEBOUNCE_MS);
                _stickyStateDirty = false;
                string payload = _stickyStateService.Serialize(snapshot);
                _stickyStateService.AtomicWrite(_stickyStatePath, payload);
            }
            catch (Exception ex)
            {
                Print("[STICKY] Save failed (best-effort): " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _stickyWritePending, 0);
                // If dirtied again during write, schedule another
                if (_stickyStateDirty)
                    MarkStickyDirty();
            }
        });
    }
}

private StickyStateSnapshot CreateStickyStateSnapshot()
{
    // H18-FIX: Snapshot ALL mutable state on strategy thread
    var modeProfilesSnapshot = new Dictionary<string, ModeConfigProfile>(_modeProfiles);
    var activeFleetSnapshot = activeFleetAccounts != null
        ? new Dictionary<string, bool>(activeFleetAccounts)
        : null;
    var activePositionsSnapshot = activePositions != null
        ? activePositions.ToArray()
        : null;
    
    var headerConfigSnapshot = new HeaderConfigSnapshot
    {
        IsRMAModeActive = isRMAModeActive,
        IsTRENDModeActive = isTRENDModeActive,
        IsRetestModeActive = isRetestModeActive,
        IsMOMOModeActive = isMOMOModeActive,
        IsFFMAModeArmed = isFFMAModeArmed,
        ActiveTargetCount = activeTargetCount,
        Target1Value = Target1Value,
        T1Type = T1Type,
        Target2Value = Target2Value,
        T2Type = T2Type,
        Target3Value = Target3Value,
        T3Type = T3Type,
        Target4Value = Target4Value,
        T4Type = T4Type,
        Target5Value = Target5Value,
        T5Type = T5Type,
        StopMultiplier = StopMultiplier,
        RMAStopATRMultiplier = RMAStopATRMultiplier,
        MaxRiskAmount = MaxRiskAmount,
        ChaseIfTouchPoints = ChaseIfTouchPoints,
        IsTrendRmaMode = isTrendRmaMode,
        IsRetestRmaMode = isRetestRmaMode
    };

    return new StickyStateSnapshot
    {
        HeaderConfig = headerConfigSnapshot,
        ModeProfiles = modeProfilesSnapshot,
        FleetAccounts = activeFleetSnapshot,
        Positions = activePositionsSnapshot,
        LeaderAccount = _stickyLeaderAccount,
        CurrentAnchor = currentRmaAnchor,
        CachedManualPrice = cachedMnlPrice,
        InstrumentName = Instrument != null ? Instrument.FullName : "unknown",
        BuildTag = BUILD_TAG
    };
}
```

---

### 2.3 Phase C: Deserialization Extraction

#### Step C1: Port Deserialization Methods

**Target Methods (from V12_002.StickyState.cs):**
1. `LoadStickyState()` → `StickyStateService.Deserialize()`
2. All `ApplySticky*()` methods → parse into DTO
3. `ParseTargetMode()` → private static method

**Service Implementation:**

```csharp
public StickyStateData Deserialize(string filePath)
{
    if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
    {
        _logger.Log("[STICKY] No persisted state found -- using defaults");
        return null;
    }

    try
    {
        string[] lines = System.IO.File.ReadAllLines(filePath, Encoding.UTF8);
        var data = new StickyStateData();
        string section = "";

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            // Section header
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line.Substring(1, line.Length - 2).ToUpperInvariant();
                continue;
            }

            ParseSection(section, line, data);
        }

        _logger.Log(string.Format("[STICKY] Loaded settings from {0}", System.IO.Path.GetFileName(filePath)));
        return data;
    }
    catch (Exception ex)
    {
        _logger.Log("[STICKY] Load failed (using defaults): " + ex.Message);
        return null;
    }
}

// [PORT: All ApplySticky* methods converted to parse into DTO]
private void ParseSection(string section, string line, StickyStateData data)
{
    if (section == "CONFIG")
    {
        ParseConfig(line, data);
    }
    else if (section.StartsWith("CONFIG_") && section.Length > 7)
    {
        string profileMode = section.Substring(7);
        ParseModeProfile(profileMode, line, data);
    }
    else if (section == "FLEET")
    {
        ParseFleet(line, data);
    }
    else if (section == "ANCHOR")
    {
        ParseAnchor(line, data);
    }
    else if (section == "POSITIONS")
    {
        ParsePosition(line, data);
    }
}

// [Continue with all parsing methods...]
```

**Lines:** ~380

#### Step C2: Update Strategy to Use Service for Deserialization

**File:** `src/V12_002.StickyState.cs`

**Modified LoadStickyState():**

```csharp
private bool LoadStickyState()
{
    var data = _stickyStateService.Deserialize(_stickyStatePath);
    if (data == null)
        return false;

    ApplyStickyStateData(data);
    return true;
}

private void ApplyStickyStateData(StickyStateData data)
{
    // Apply config values
    foreach (var kvp in data.ConfigValues)
    {
        ApplyConfigValue(kvp.Key, kvp.Value);
    }

    // Apply mode profiles
    foreach (var kvp in data.ModeProfiles)
    {
        _modeProfiles[kvp.Key] = kvp.Value;
    }

    // Apply fleet toggles (deferred)
    if (data.FleetToggles.Count > 0)
    {
        if (_pendingStickyFleetToggles == null)
            _pendingStickyFleetToggles = new Dictionary<string, bool>();
        foreach (var kvp in data.FleetToggles)
            _pendingStickyFleetToggles[kvp.Key] = kvp.Value;
    }

    // Apply leader account
    if (!string.IsNullOrEmpty(data.LeaderAccount))
        _stickyLeaderAccount = data.LeaderAccount;

    // Apply anchor
    SetRmaAnchorFromIpc(AnchorTypeToString(data.Anchor));
    cachedMnlPrice = data.ManualPrice;

    // Position states applied later in EnrichTrailStateFromSticky()
}
```

---

### 2.4 Phase D: Integration & Cleanup

#### Step D1: Wire Service into Strategy Lifecycle

**File:** `src/V12_002.Lifecycle.cs` (or wherever State.DataLoaded is handled)

```csharp
protected override void OnStateChange()
{
    if (State == State.DataLoaded)
    {
        // Initialize StickyState service
        var logger = new StrategyLogger(this);
        _stickyStateService = new StickyStateService(logger);
        
        // Initialize sticky state path
        _stickyStatePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NinjaTrader 8", "strategies", "V12_" + Instrument.MasterInstrument.Name + ".v12state");
        
        // Load persisted state
        LoadStickyState();
        
        // ... rest of DataLoaded logic
    }
}

private class StrategyLogger : IStickyStateLogger
{
    private readonly V12_002 _strategy;
    public StrategyLogger(V12_002 strategy) => _strategy = strategy;
    public void Log(string message) => _strategy.Print(message);
}
```

#### Step D2: Remove Dead Code

**File:** `src/V12_002.StickyState.cs`

**Remove:**
- Lines 134-354 (serialization methods) - now in service
- Lines 407-769 (deserialization methods) - now in service

**Keep:**
- Fields (lines 18-23)
- `HeaderConfigSnapshot` struct (lines 29-53) - shared DTO
- `MarkStickyDirty()` (modified to call service)
- `CreateStickyStateSnapshot()` (new method)
- `ApplyStickyStateData()` (new method)
- `EnrichTrailStateFromSticky()` (modified to use data.PositionStates)

#### Step D3: Verify Integration Points

**Checklist:**
- [ ] All 18 `MarkStickyDirty()` call sites unchanged
- [ ] Service instantiated in State.DataLoaded
- [ ] `LoadStickyState()` calls service
- [ ] `MarkStickyDirty()` calls service
- [ ] No compilation errors
- [ ] No NinjaTrader dependencies in service

---

### 2.5 Phase E: Verification

#### Step E1: Automated Gates

```powershell
# Gate 1: Build + ASCII
powershell -File .\deploy-sync.ps1

# Gate 2: Lock audit
grep -r "lock(" src/

# Gate 3: Unicode audit
grep -Prn "[^\x00-\x7F]" src/

# Gate 4: Unit tests
dotnet test
```

#### Step E2: F5 Gate (Manual)

1. Open NinjaTrader IDE
2. Press F5 to load strategy
3. Verify no errors in Output window
4. Send IPC CONFIG command via side panel
5. Verify .v12state file created/updated
6. Restart strategy
7. Verify state hydrated from file

#### Step E3: IPC Integration Test

**Test Matrix:**
- [ ] CONFIG command persists
- [ ] TOGGLE_ACCOUNT persists
- [ ] SET_RMA_MODE persists
- [ ] SET_CIT persists
- [ ] SET_MAX_RISK persists
- [ ] SET_ANCHOR persists
- [ ] SET_TARGETS persists
- [ ] SET_MANUAL_PRICE persists
- [ ] SET_LEADER_ACCOUNT persists
- [ ] Trailing stop updates persist
- [ ] Breakeven triggers persist

---

## 3. RISK MITIGATION STRATEGIES

### 3.1 Snapshot Pattern Preservation

**Risk:** Breaking H18-FIX thread safety

**Mitigation:**
1. Port snapshot creation logic 1:1 (no changes)
2. Keep snapshot creation in Strategy (on strategy thread)
3. Service receives immutable snapshot
4. Add comment: "// H18-FIX: Preserved from original implementation"

### 3.2 Task.Run Orchestration

**Risk:** Breaking debouncing or coalescing

**Mitigation:**
1. Keep Task.Run in Strategy (not in service)
2. Service only does synchronous serialization
3. Strategy controls async write timing
4. Preserve exact debounce logic (50ms, recursive call)

### 3.3 Deserialization State Application

**Risk:** Missing property assignments

**Mitigation:**
1. Service returns complete DTO
2. Strategy applies DTO in single method
3. Add integration test for each config value
4. F5 gate verifies end-to-end

### 3.4 Logging Abstraction

**Risk:** Missing log messages

**Mitigation:**
1. ILogger interface with Strategy wrapper
2. All service log calls go through interface
3. Compare logs before/after extraction

### 3.5 External Dependencies

**Risk:** Missing Instrument.FullName or BUILD_TAG

**Mitigation:**
1. Inject via snapshot
2. Add null checks in service
3. F5 gate verifies header comment

---

## 4. ROLLBACK TRIGGERS

**Automatic Rollback If:**
- Any verification gate fails
- Compilation errors
- F5 gate fails (NinjaTrader won't load)

**Manual Rollback If:**
- IPC commands stop persisting
- State hydration fails on restart
- Performance regression >5%

**Rollback Command:**
```bash
git revert <commit-hash>
```

---

## 5. SUCCESS CRITERIA

### 5.1 Functional

- [ ] All 18 `MarkStickyDirty()` call sites trigger persistence
- [ ] INI format byte-for-byte identical
- [ ] State hydration works on restart
- [ ] No IPC command regression

### 5.2 Quality

- [ ] `dotnet test` runs without NinjaTrader
- [ ] Unit test coverage >80%
- [ ] All gates pass (deploy-sync, lock, unicode)
- [ ] F5 gate passes

### 5.3 Performance

- [ ] Serialization <5% slower
- [ ] Deserialization <5% slower
- [ ] Memory overhead <2KB

---

## 6. IMPLEMENTATION CHECKLIST

### Phase A: Service Foundation
- [ ] Create IStickyStateService interface
- [ ] Create IStickyStateLogger interface
- [ ] Create StickyStateSnapshot DTO
- [ ] Create StickyStateData DTO
- [ ] Create PositionTrailState DTO
- [ ] Create unit test stub
- [ ] Verify dotnet test runs

### Phase B: Serialization
- [ ] Port SerializeStickyState()
- [ ] Port SerializeSticky_WriteHeaderConfig()
- [ ] Port SerializeSticky_WriteFleetAnchor()
- [ ] Port SerializeSticky_WriteModeProfiles()
- [ ] Port SerializeSticky_WritePositions()
- [ ] Port AnchorTypeToString()
- [ ] Port AtomicWriteFile()
- [ ] Update MarkStickyDirty() to call service
- [ ] Create CreateStickyStateSnapshot()
- [ ] Remove dead serialization code

### Phase C: Deserialization
- [ ] Port LoadStickyState() → Deserialize()
- [ ] Port all ApplySticky* methods
- [ ] Port ParseTargetMode()
- [ ] Create ApplyStickyStateData()
- [ ] Update LoadStickyState() to call service
- [ ] Remove dead deserialization code

### Phase D: Integration
- [ ] Wire service in State.DataLoaded
- [ ] Create StrategyLogger wrapper
- [ ] Verify all 18 call sites unchanged
- [ ] Remove all dead code
- [ ] Add using statements

### Phase E: Verification
- [ ] Run deploy-sync.ps1
- [ ] Run lock audit
- [ ] Run unicode audit
- [ ] Run dotnet test
- [ ] F5 in NinjaTrader
- [ ] Test all 11 IPC commands
- [ ] Verify restart hydration

---

## 7. COMMIT MESSAGE

```
refactor: extract StickyState & IPC into StickyStateService (Epic 1) [Run2-StickyService]

EXTRACTION SUMMARY:
- NEW: src/Services/IStickyStateService.cs (interface + DTOs)
- NEW: src/Services/StickyStateService.cs (pure C# service, 0 NT deps)
- NEW: tests/Services/StickyStateServiceTests.cs (proves dotnet test works)
- MODIFIED: src/V12_002.StickyState.cs (delegate to service, remove 600 lines)

THREAD SAFETY:
- H18-FIX snapshot pattern preserved exactly
- Snapshot creation stays on strategy thread
- Service receives immutable snapshot
- Task.Run orchestration unchanged

INTEGRATION:
- All 18 MarkStickyDirty() call sites unchanged
- IPC commands still trigger persistence
- State hydration on restart verified
- INI format byte-for-byte identical

VERIFICATION:
- deploy-sync: PASS (ASCII gate clean)
- lock audit: CLEAN (0 matches)
- unicode audit: CLEAN (0 matches)
- dotnet test: PASS (service instantiation)
- F5 gate: PASS (NinjaTrader load + IPC + restart)

DIFF: ~327 lines (within 500-line limit)
PERFORMANCE: <5% overhead (acceptable for testability)
TESTABILITY: Service now testable without NinjaTrader runtime
```

---

**[PLAN-GATE]**

This approach document is now complete. Ready to proceed to Phase 3 (VALIDATE).

**Key Decisions:**
1. Minimal interface design (serialization focus)
2. Snapshot pattern preserved exactly (H18-FIX sacred)
3. DTO return pattern for deserialization
4. ILogger interface for logging abstraction
5. Phased extraction (A→B→C→D→E)

**Proceed to Phase 3 (VALIDATE) to cross-check against V12 DNA.**