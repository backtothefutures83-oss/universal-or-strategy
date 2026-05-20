# Ticket 04: Strategy Integration

**Epic:** run2-stickystate  
**Phase:** D - Strategy Integration  
**Assignee:** v12-engineer  
**Estimated Complexity:** HIGH  
**Dependencies:** ticket-03 (Deserialization)

---

## OBJECTIVE

Wire [`StickyStateService`](src/Services/StickyStateService.cs) into [`V12_002.StickyState.cs`](src/V12_002.StickyState.cs) lifecycle. Replace direct serialization/deserialization calls with service delegation. Remove dead code (lines 134-354, 407-769). Verify all 18 call sites to `MarkStickyDirty()` remain unchanged.

---

## SCOPE

### Files to Modify

1. **src/V12_002.StickyState.cs** (~400 lines removed, ~150 lines added)
   - Add service field + logger wrapper
   - Wire service in State.DataLoaded
   - Modify `MarkStickyDirty()` to call service
   - Add `CreateStickyStateSnapshot()` method
   - Add `ApplyStickyStateData()` method
   - Remove dead serialization/deserialization code

---

## IMPLEMENTATION STEPS

### Step 1: Add Service Field and Logger Wrapper

**File:** `src/V12_002.StickyState.cs`

Add after line 62 (before `MarkStickyDirty()` method):

```csharp
// Build 1108.002 -- StickyState service extraction
private StickyStateService _stickyService;

// Logger wrapper for service
private class StrategyLogger : IStickyStateLogger
{
    private readonly V12_002 _strategy;
    public StrategyLogger(V12_002 strategy) { _strategy = strategy; }
    public void Log(string message) { _strategy.Print(message); }
}
```

### Step 2: Wire Service in State.DataLoaded

**File:** `src/V12_002.StickyState.cs`

Find the `State.DataLoaded` handler (around line 30-40 in main strategy file). Add service initialization:

```csharp
protected override void OnStateChange()
{
    if (State == State.DataLoaded)
    {
        // ... existing initialization code ...

        // Build 1108.002 -- Initialize StickyState service
        _stickyService = new StickyStateService(new StrategyLogger(this));

        // Load persisted state
        string stickyPath = GetStickyFilePath();
        StickyStateData data = _stickyService.Deserialize(stickyPath);
        if (data != null)
        {
            ApplyStickyStateData(data);
        }

        // ... rest of initialization ...
    }
}
```

### Step 3: Modify MarkStickyDirty() to Call Service

**File:** `src/V12_002.StickyState.cs` (lines 63-132)

Replace the entire `MarkStickyDirty()` method with:

```csharp
// Build 1108.002 -- Sticky state persistence with H18-FIX snapshot pattern
// CRITICAL: Snapshot ALL mutable state on strategy thread BEFORE Task.Run
private void MarkStickyDirty()
{
    if (_stickyService == null) return;

    // H18-FIX: Capture snapshot on strategy thread (prevents race conditions)
    StickyStateSnapshot snapshot = CreateStickyStateSnapshot();

    // Debounce: If already writing, mark dirty and let recursive call handle it
    if (_stickyWriteInProgress)
    {
        _stickyDirtyDuringWrite = true;
        return;
    }

    _stickyWriteInProgress = true;
    _stickyDirtyDuringWrite = false;

    // Background write (snapshot is immutable, safe to pass to Task.Run)
    Task.Run(() =>
    {
        try
        {
            string path = GetStickyFilePath();
            _stickyService.Serialize(snapshot, path);

            // 50ms debounce window
            System.Threading.Thread.Sleep(50);

            // Recursive call if dirtied during write
            if (_stickyDirtyDuringWrite)
            {
                _stickyWriteInProgress = false;
                MarkStickyDirty();
            }
            else
            {
                _stickyWriteInProgress = false;
            }
        }
        catch (Exception ex)
        {
            Print("[STICKY] Write failed: " + ex.Message);
            _stickyWriteInProgress = false;
        }
    });
}

// Helper: Get sticky file path
private string GetStickyFilePath()
{
    string dir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NinjaTrader 8", "strategies", "v12_sticky"
    );
    if (!System.IO.Directory.Exists(dir))
        System.IO.Directory.CreateDirectory(dir);

    string filename = string.Format("sticky_{0}.txt", Instrument.FullName.Replace(" ", "_"));
    return System.IO.Path.Combine(dir, filename);
}
```

### Step 4: Add CreateStickyStateSnapshot() Method

**File:** `src/V12_002.StickyState.cs`

Add after `MarkStickyDirty()`:

```csharp
// Build 1108.002 -- Create immutable snapshot for service
// CRITICAL: Called on strategy thread, captures ALL mutable state
private StickyStateSnapshot CreateStickyStateSnapshot()
{
    var snapshot = new StickyStateSnapshot
    {
        InstrumentFullName = Instrument.FullName,
        BuildTag = "1108.002",
        
        // Config section
        Mode = _mode,
        TargetCount = _targetCount,
        T1 = _t1,
        T2 = _t2,
        T3 = _t3,
        T4 = _t4,
        T5 = _t5,
        T1Type = _t1Type,
        T2Type = _t2Type,
        T3Type = _t3Type,
        T4Type = _t4Type,
        T5Type = _t5Type,
        StopMult = _stopMult,
        MaxRisk = _maxRisk,
        CIT = _cit,
        TrailingRmaEnabled = _trailingRmaEnabled,
        ReverseRmaEnabled = _reverseRmaEnabled,
        
        // Fleet section
        LeaderAccount = _leaderAccount,
        FleetToggles = new Dictionary<string, bool>(_fleetToggles),
        
        // Anchor section
        Anchor = _anchor,
        ManualPrice = _manualPrice,
        
        // Mode profiles
        ModeProfiles = new Dictionary<string, ModeConfigProfile>()
    };

    // Copy mode profiles
    foreach (var kvp in _modeProfiles)
    {
        snapshot.ModeProfiles[kvp.Key] = new ModeConfigProfile
        {
            TargetCount = kvp.Value.TargetCount,
            T1 = kvp.Value.T1,
            T2 = kvp.Value.T2,
            T3 = kvp.Value.T3,
            T4 = kvp.Value.T4,
            T5 = kvp.Value.T5,
            T1Type = kvp.Value.T1Type,
            T2Type = kvp.Value.T2Type,
            T3Type = kvp.Value.T3Type,
            T4Type = kvp.Value.T4Type,
            T5Type = kvp.Value.T5Type,
            StopMult = kvp.Value.StopMult,
            MaxRisk = kvp.Value.MaxRisk
        };
    }

    // Copy position trail states
    snapshot.PositionStates = new Dictionary<string, PositionTrailState>();
    foreach (var kvp in _trailStates)
    {
        snapshot.PositionStates[kvp.Key] = new PositionTrailState
        {
            ExtremePriceSinceEntry = kvp.Value.ExtremePriceSinceEntry,
            CurrentTrailLevel = kvp.Value.CurrentTrailLevel,
            ManualBreakevenArmed = kvp.Value.ManualBreakevenArmed,
            ManualBreakevenTriggered = kvp.Value.ManualBreakevenTriggered,
            InitialTargetCount = kvp.Value.InitialTargetCount
        };
    }

    return snapshot;
}
```

### Step 5: Add ApplyStickyStateData() Method

**File:** `src/V12_002.StickyState.cs`

Add after `CreateStickyStateSnapshot()`:

```csharp
// Build 1108.002 -- Apply deserialized data to strategy state
// CRITICAL: Called on strategy thread during State.DataLoaded
private void ApplyStickyStateData(StickyStateData data)
{
    if (data == null) return;

    // Config section
    if (data.ConfigValues.ContainsKey("MODE"))
    {
        string modeStr = data.ConfigValues["MODE"] as string;
        if (modeStr == "OR") _mode = StrategyMode.OR;
        else if (modeStr == "CT") _mode = StrategyMode.CT;
        else if (modeStr == "CTMA") _mode = StrategyMode.CTMA;
        
        // Log original mode if it was forced to OR
        if (data.ConfigValues.ContainsKey("MODE_ORIGINAL"))
        {
            string orig = data.ConfigValues["MODE_ORIGINAL"] as string;
            if (orig != modeStr)
                Print(string.Format("[STICKY] Mode forced {0} -> {1} (safety gate)", orig, modeStr));
        }
    }
    
    if (data.ConfigValues.ContainsKey("COUNT"))
        _targetCount = (int)data.ConfigValues["COUNT"];
    if (data.ConfigValues.ContainsKey("T1"))
        _t1 = (double)data.ConfigValues["T1"];
    if (data.ConfigValues.ContainsKey("T2"))
        _t2 = (double)data.ConfigValues["T2"];
    if (data.ConfigValues.ContainsKey("T3"))
        _t3 = (double)data.ConfigValues["T3"];
    if (data.ConfigValues.ContainsKey("T4"))
        _t4 = (double)data.ConfigValues["T4"];
    if (data.ConfigValues.ContainsKey("T5"))
        _t5 = (double)data.ConfigValues["T5"];
    if (data.ConfigValues.ContainsKey("T1TYPE"))
        _t1Type = (TargetMode)data.ConfigValues["T1TYPE"];
    if (data.ConfigValues.ContainsKey("T2TYPE"))
        _t2Type = (TargetMode)data.ConfigValues["T2TYPE"];
    if (data.ConfigValues.ContainsKey("T3TYPE"))
        _t3Type = (TargetMode)data.ConfigValues["T3TYPE"];
    if (data.ConfigValues.ContainsKey("T4TYPE"))
        _t4Type = (TargetMode)data.ConfigValues["T4TYPE"];
    if (data.ConfigValues.ContainsKey("T5TYPE"))
        _t5Type = (TargetMode)data.ConfigValues["T5TYPE"];
    if (data.ConfigValues.ContainsKey("STR"))
        _stopMult = (double)data.ConfigValues["STR"];
    if (data.ConfigValues.ContainsKey("MAX"))
        _maxRisk = (double)data.ConfigValues["MAX"];
    if (data.ConfigValues.ContainsKey("CIT"))
        _cit = data.ConfigValues["CIT"] as string;
    if (data.ConfigValues.ContainsKey("TRMA"))
        _trailingRmaEnabled = (bool)data.ConfigValues["TRMA"];
    if (data.ConfigValues.ContainsKey("RRMA"))
        _reverseRmaEnabled = (bool)data.ConfigValues["RRMA"];

    // Fleet section
    _leaderAccount = data.LeaderAccount;
    _fleetToggles = new Dictionary<string, bool>(data.FleetToggles);

    // Anchor section
    _anchor = data.Anchor;
    _manualPrice = data.ManualPrice;

    // Mode profiles
    _modeProfiles = new Dictionary<string, ModeConfigProfile>();
    foreach (var kvp in data.ModeProfiles)
    {
        _modeProfiles[kvp.Key] = new ModeConfigProfile
        {
            TargetCount = kvp.Value.TargetCount,
            T1 = kvp.Value.T1,
            T2 = kvp.Value.T2,
            T3 = kvp.Value.T3,
            T4 = kvp.Value.T4,
            T5 = kvp.Value.T5,
            T1Type = kvp.Value.T1Type,
            T2Type = kvp.Value.T2Type,
            T3Type = kvp.Value.T3Type,
            T4Type = kvp.Value.T4Type,
            T5Type = kvp.Value.T5Type,
            StopMult = kvp.Value.StopMult,
            MaxRisk = kvp.Value.MaxRisk
        };
    }

    // Position trail states
    foreach (var kvp in data.PositionStates)
    {
        if (_trailStates.ContainsKey(kvp.Key))
        {
            _trailStates[kvp.Key].ExtremePriceSinceEntry = kvp.Value.ExtremePriceSinceEntry;
            _trailStates[kvp.Key].CurrentTrailLevel = kvp.Value.CurrentTrailLevel;
            _trailStates[kvp.Key].ManualBreakevenArmed = kvp.Value.ManualBreakevenArmed;
            _trailStates[kvp.Key].ManualBreakevenTriggered = kvp.Value.ManualBreakevenTriggered;
            _trailStates[kvp.Key].InitialTargetCount = kvp.Value.InitialTargetCount;
        }
    }
}
```

### Step 6: Remove Dead Code

**File:** `src/V12_002.StickyState.cs`

Delete the following line ranges (after service integration is complete):

1. **Lines 134-354**: Old serialization methods
   - `WriteStickyConfig()`
   - `WriteStickyModeProfile()`
   - `WriteStickyFleet()`
   - `WriteStickyAnchor()`
   - `WriteStickyPositions()`

2. **Lines 407-769**: Old deserialization methods
   - `LoadStickyState()`
   - `ApplyStickyConfig()`
   - `ApplyStickyModeProfile()`
   - `ApplyStickyFleet()`
   - `ApplyStickyAnchor()`
   - `EnrichTrailStateFromSticky()`
   - `ParseTargetMode()`

**CRITICAL**: Verify line numbers before deletion. Use search to confirm method names.

### Step 7: Verify Call Sites Unchanged

**File:** `src/V12_002.StickyState.cs` + other files

Verify all 18 call sites to `MarkStickyDirty()` remain unchanged:

```bash
grep -rn "MarkStickyDirty()" src/
```

Expected call sites (from 01-analysis.md):
- V12_002.UI.IPC.Commands.Config.cs (6 sites)
- V12_002.UI.IPC.Commands.Fleet.cs (3 sites)
- V12_002.UI.IPC.Commands.Mode.cs (2 sites)
- V12_002.UI.IPC.Commands.Misc.cs (2 sites)
- V12_002.Trailing.Breakeven.cs (2 sites)
- V12_002.Trailing.StopUpdate.cs (3 sites)

All should still call `MarkStickyDirty()` with no parameters.

---

## VERIFICATION CHECKLIST

### Build Verification
- [ ] `powershell -File .\deploy-sync.ps1` succeeds
- [ ] ASCII gate passes
- [ ] No compilation errors

### Integration Verification
- [ ] Service initialized in State.DataLoaded
- [ ] `MarkStickyDirty()` calls service
- [ ] Snapshot created on strategy thread
- [ ] Deserialization applied on strategy thread
- [ ] All 18 call sites unchanged

### Dead Code Verification
- [ ] Lines 134-354 removed (serialization)
- [ ] Lines 407-769 removed (deserialization)
- [ ] No references to removed methods
- [ ] File compiles after removal

### Logic Verification
- [ ] H18-FIX snapshot pattern preserved
- [ ] Debouncing logic preserved
- [ ] Task.Run orchestration preserved
- [ ] Logger wrapper works

---

## ACCEPTANCE CRITERIA

1. ✅ Service field added
2. ✅ Logger wrapper implemented
3. ✅ Service initialized in State.DataLoaded
4. ✅ `MarkStickyDirty()` modified to call service
5. ✅ `CreateStickyStateSnapshot()` implemented
6. ✅ `ApplyStickyStateData()` implemented
7. ✅ Dead code removed (lines 134-354, 407-769)
8. ✅ All 18 call sites verified unchanged
9. ✅ `deploy-sync.ps1` passes
10. ✅ ASCII gate passes
11. ✅ Zero compilation errors

---

## CRITICAL PRESERVATION POINTS

### Thread Safety (H18-FIX)
- ✅ Snapshot created on strategy thread
- ✅ Snapshot passed to Task.Run (immutable)
- ✅ No direct property access in background thread

### Debouncing Pattern
- ✅ `_stickyWriteInProgress` flag preserved
- ✅ `_stickyDirtyDuringWrite` flag preserved
- ✅ 50ms sleep window preserved
- ✅ Recursive call logic preserved

### Service Integration
- ✅ Service is null-checked before use
- ✅ Logger wrapper delegates to Print()
- ✅ Deserialization returns null on failure
- ✅ ApplyStickyStateData handles null gracefully

### Call Site Integrity
- ✅ All 18 call sites unchanged
- ✅ No new parameters added to MarkStickyDirty()
- ✅ Public API preserved

---

## NOTES

- This ticket completes the service extraction
- Verification (ticket-05) will test all 11 IPC commands
- F5 gate will verify restart hydration
- Performance verification will confirm no regression

---

## ESTIMATED TIME

**2 hours** (integration + dead code removal + verification)