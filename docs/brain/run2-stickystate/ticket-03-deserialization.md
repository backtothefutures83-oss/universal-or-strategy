# Ticket 03: Deserialization Extraction

**Epic:** run2-stickystate  
**Phase:** C - Deserialization Extraction  
**Assignee:** v12-engineer  
**Estimated Complexity:** HIGH  
**Dependencies:** ticket-02 (Serialization)

---

## OBJECTIVE

Port all deserialization logic from [`V12_002.StickyState.cs`](src/V12_002.StickyState.cs) into [`StickyStateService`](src/Services/StickyStateService.cs). Convert all `ApplySticky*()` methods to parse into `StickyStateData` DTO instead of directly mutating strategy properties. This is a **1:1 port with DTO adaptation** - preserve all parsing logic, change only the output mechanism.

---

## SCOPE

### Files to Modify

1. **src/Services/StickyStateService.cs** (~380 lines added)
   - Implement `Deserialize()` method
   - Port all parsing methods (convert to DTO population)
   - Port `ParseTargetMode()` helper

2. **src/V12_002.StickyState.cs** (no changes yet - modification comes in ticket-04)

---

## IMPLEMENTATION STEPS

### Step 1: Implement Deserialize() Method

**File:** `src/Services/StickyStateService.cs`

Replace the `Deserialize()` method with:

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
```

### Step 2: Implement ParseSection() Router

```csharp
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
```

### Step 3: Port ParseConfig() Method

**Source:** Lines 444-551 from `V12_002.StickyState.cs` (ApplyStickyConfig + sub-methods)

```csharp
// [PORT: Lines 444-551 from V12_002.StickyState.cs]
// Converted from ApplyStickyConfig() to populate DTO instead of mutating strategy
private void ParseConfig(string line, StickyStateData data)
{
    int eq = line.IndexOf('=');
    if (eq < 1) return;
    string key = line.Substring(0, eq).ToUpperInvariant();
    string val = line.Substring(eq + 1);

    // MODE - Build 1108.002 SAFETY GATE: Click-trader modes never auto-rearm on startup
    if (key == "MODE")
    {
        // Always force to OR (safety gate) - store original value for logging
        data.ConfigValues["MODE_ORIGINAL"] = val;
        data.ConfigValues["MODE"] = "OR";
        return;
    }

    // Target count
    if (key == "COUNT")
    {
        if (int.TryParse(val, out int cnt))
            data.ConfigValues["COUNT"] = Math.Max(1, Math.Min(5, cnt));
        return;
    }

    // Target values
    if (key == "T1" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t1))
    {
        data.ConfigValues["T1"] = t1;
        return;
    }
    if (key == "T2" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t2))
    {
        data.ConfigValues["T2"] = t2;
        return;
    }
    if (key == "T3" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t3))
    {
        data.ConfigValues["T3"] = t3;
        return;
    }
    if (key == "T4" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t4))
    {
        data.ConfigValues["T4"] = t4;
        return;
    }
    if (key == "T5" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t5))
    {
        data.ConfigValues["T5"] = t5;
        return;
    }

    // Target types
    if (key == "T1TYPE")
    {
        data.ConfigValues["T1TYPE"] = ParseTargetMode(val);
        return;
    }
    if (key == "T2TYPE")
    {
        data.ConfigValues["T2TYPE"] = ParseTargetMode(val);
        return;
    }
    if (key == "T3TYPE")
    {
        data.ConfigValues["T3TYPE"] = ParseTargetMode(val);
        return;
    }
    if (key == "T4TYPE")
    {
        data.ConfigValues["T4TYPE"] = ParseTargetMode(val);
        return;
    }
    if (key == "T5TYPE")
    {
        data.ConfigValues["T5TYPE"] = ParseTargetMode(val);
        return;
    }

    // Risk and flags
    if (key == "STR" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double str))
    {
        data.ConfigValues["STR"] = str;
        return;
    }
    if (key == "MAX" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
    {
        data.ConfigValues["MAX"] = max;
        return;
    }
    if (key == "CIT")
    {
        data.ConfigValues["CIT"] = val;
        return;
    }
    if (key == "TRMA")
    {
        data.ConfigValues["TRMA"] = (val == "1");
        return;
    }
    if (key == "RRMA")
    {
        data.ConfigValues["RRMA"] = (val == "1");
        return;
    }
}
```

### Step 4: Port ParseModeProfile() Method

**Source:** Lines 554-636 from `V12_002.StickyState.cs` (ApplyStickyModeProfile + sub-methods)

```csharp
// [PORT: Lines 554-636 from V12_002.StickyState.cs]
// Converted from ApplyStickyModeProfile() to populate DTO
private void ParseModeProfile(string mode, string line, StickyStateData data)
{
    int eq = line.IndexOf('=');
    if (eq < 1) return;
    string key = line.Substring(0, eq).ToUpperInvariant();
    string val = line.Substring(eq + 1);

    ModeConfigProfile profile;
    if (!data.ModeProfiles.TryGetValue(mode, out profile))
    {
        profile = new ModeConfigProfile();
        data.ModeProfiles[mode] = profile;
    }

    // Target count
    if (key == "COUNT" && int.TryParse(val, out int cnt))
    {
        profile.TargetCount = Math.Max(1, Math.Min(5, cnt));
        return;
    }

    // Target values
    if (key == "T1" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t1))
    {
        profile.T1 = t1;
        return;
    }
    if (key == "T2" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t2))
    {
        profile.T2 = t2;
        return;
    }
    if (key == "T3" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t3))
    {
        profile.T3 = t3;
        return;
    }
    if (key == "T4" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t4))
    {
        profile.T4 = t4;
        return;
    }
    if (key == "T5" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t5))
    {
        profile.T5 = t5;
        return;
    }

    // Target types
    if (key == "T1TYPE")
    {
        profile.T1Type = ParseTargetMode(val);
        return;
    }
    if (key == "T2TYPE")
    {
        profile.T2Type = ParseTargetMode(val);
        return;
    }
    if (key == "T3TYPE")
    {
        profile.T3Type = ParseTargetMode(val);
        return;
    }
    if (key == "T4TYPE")
    {
        profile.T4Type = ParseTargetMode(val);
        return;
    }
    if (key == "T5TYPE")
    {
        profile.T5Type = ParseTargetMode(val);
        return;
    }

    // Risk
    if (key == "STR" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double str))
    {
        profile.StopMult = str;
        return;
    }
    if (key == "MAX" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
    {
        profile.MaxRisk = max;
        return;
    }
}
```

### Step 5: Port ParseFleet() Method

**Source:** Lines 638-657 from `V12_002.StickyState.cs` (ApplyStickyFleet)

```csharp
// [PORT: Lines 638-657 from V12_002.StickyState.cs]
private void ParseFleet(string line, StickyStateData data)
{
    int eq = line.IndexOf('=');
    if (eq < 1) return;
    string key = line.Substring(0, eq);
    string val = line.Substring(eq + 1);

    if (key.ToUpperInvariant() == "LEADER")
    {
        data.LeaderAccount = val;
        return;
    }

    // Account toggle: "Apex_F01_12345=1"
    data.FleetToggles[key] = (val == "1");
}
```

### Step 6: Port ParseAnchor() Method

**Source:** Lines 659-678 from `V12_002.StickyState.cs` (ApplyStickyAnchor)

```csharp
// [PORT: Lines 659-678 from V12_002.StickyState.cs]
private void ParseAnchor(string line, StickyStateData data)
{
    int eq = line.IndexOf('=');
    if (eq < 1) return;
    string key = line.Substring(0, eq).ToUpperInvariant();
    string val = line.Substring(eq + 1);

    if (key == "TYPE")
    {
        data.Anchor = ParseAnchorType(val);
        return;
    }
    if (key == "MNL_PRICE")
    {
        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double p))
            data.ManualPrice = p;
        return;
    }
}

private static RmaAnchorType ParseAnchorType(string val)
{
    string upper = val.ToUpperInvariant();
    if (upper == "EMA30") return RmaAnchorType.Ema30;
    if (upper == "EMA65") return RmaAnchorType.Ema65;
    if (upper == "EMA200") return RmaAnchorType.Ema200;
    if (upper == "OR_HIGH") return RmaAnchorType.OrHigh;
    if (upper == "OR_LOW") return RmaAnchorType.OrLow;
    if (upper == "MANUAL") return RmaAnchorType.Manual;
    return RmaAnchorType.Ema65; // Default
}
```

### Step 7: Port ParsePosition() Method

**Source:** Lines 684-730 from `V12_002.StickyState.cs` (EnrichTrailStateFromSticky)

```csharp
// [PORT: Lines 684-730 from V12_002.StickyState.cs]
// Converted from EnrichTrailStateFromSticky() to populate DTO
private void ParsePosition(string line, StickyStateData data)
{
    if (line.StartsWith("#")) return; // Skip comment line

    // Format: key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount
    string[] parts = line.Split('|');
    if (parts.Length < 6) return;

    string posKey = parts[0];
    var state = new PositionTrailState();

    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double extreme))
        state.ExtremePriceSinceEntry = extreme;
    if (int.TryParse(parts[2], out int trail))
        state.CurrentTrailLevel = trail;
    state.ManualBreakevenArmed = (parts[3] == "1");
    state.ManualBreakevenTriggered = (parts[4] == "1");
    if (int.TryParse(parts[5], out int itc))
        state.InitialTargetCount = itc;

    data.PositionStates[posKey] = state;
}
```

### Step 8: Port ParseTargetMode() Helper

**Source:** Lines 760-769 from `V12_002.StickyState.cs`

```csharp
// [PORT: Lines 760-769 from V12_002.StickyState.cs]
private static TargetMode ParseTargetMode(string val)
{
    if (val == null) return TargetMode.ATR;
    string upper = val.ToUpperInvariant();
    if (upper == "ATR") return TargetMode.ATR;
    if (upper == "TICKS") return TargetMode.Ticks;
    if (upper == "POINTS") return TargetMode.Points;
    if (upper == "RUNNER") return TargetMode.Runner;
    return TargetMode.ATR;
}
```

---

## VERIFICATION CHECKLIST

### Build Verification
- [ ] `dotnet build` succeeds
- [ ] No compilation errors
- [ ] All using statements present

### Test Verification
- [ ] `dotnet test` passes
- [ ] Service instantiation test works
- [ ] No NinjaTrader dependencies

### Code Quality
- [ ] All Build tags preserved (Build 1108.002, etc.)
- [ ] ASCII-only compliance
- [ ] CultureInfo.InvariantCulture for all number parsing

### Logic Verification
- [ ] All parsing logic ported 1:1
- [ ] DTO population instead of property mutation
- [ ] Null checks preserved
- [ ] Default values preserved
- [ ] Safety gates preserved (MODE forced to OR)

---

## ACCEPTANCE CRITERIA

1. ✅ `Deserialize()` method implemented
2. ✅ `ParseSection()` router implemented
3. ✅ `ParseConfig()` ported (populates ConfigValues dict)
4. ✅ `ParseModeProfile()` ported (populates ModeProfiles dict)
5. ✅ `ParseFleet()` ported (populates FleetToggles dict)
6. ✅ `ParseAnchor()` ported (populates Anchor + ManualPrice)
7. ✅ `ParsePosition()` ported (populates PositionStates dict)
8. ✅ `ParseTargetMode()` helper ported
9. ✅ `ParseAnchorType()` helper implemented
10. ✅ All Build tags preserved
11. ✅ Zero compilation errors
12. ✅ `dotnet test` passes

---

## CRITICAL PRESERVATION POINTS

### Safety Gates
- ✅ MODE always forced to OR (Build 1108.002 safety gate)
- ✅ Original MODE value stored for logging
- ✅ Target count clamped 1-5
- ✅ Profile target count clamped 1-5

### Parsing Logic
- ✅ CultureInfo.InvariantCulture for all numbers
- ✅ Boolean parsing (1/0 → true/false)
- ✅ Null checks before parsing
- ✅ Default values on parse failure

### DTO Population
- ✅ ConfigValues dict for config section
- ✅ ModeProfiles dict for CONFIG_* sections
- ✅ FleetToggles dict for fleet section
- ✅ PositionStates dict for positions section
- ✅ Direct properties for anchor data

---

## NOTES

- This ticket does NOT modify V12_002.StickyState.cs yet
- Strategy integration comes in ticket-04
- Dead code removal comes in ticket-04
- Service remains testable (dotnet test works)

---

## ESTIMATED TIME

**3 hours** (complex DTO conversion, many parsing methods)