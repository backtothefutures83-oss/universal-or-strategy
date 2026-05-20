# Ticket 02: Serialization Extraction

**Epic:** run2-stickystate  
**Phase:** B - Serialization Extraction  
**Assignee:** v12-engineer  
**Estimated Complexity:** MEDIUM  
**Dependencies:** ticket-01 (Service Foundation)

---

## OBJECTIVE

Port all serialization logic from [`V12_002.StickyState.cs`](src/V12_002.StickyState.cs) into [`StickyStateService`](src/Services/StickyStateService.cs). This is a **1:1 port** - zero logic drift, preserve all comments, maintain byte-for-byte INI format compatibility.

---

## SCOPE

### Files to Modify

1. **src/Services/StickyStateService.cs** (~220 lines added)
   - Implement `Serialize()` method
   - Implement `AtomicWrite()` method
   - Port 5 serialization sub-methods
   - Port `AnchorTypeToString()` helper

2. **src/V12_002.StickyState.cs** (no changes yet - modification comes in ticket-04)

---

## IMPLEMENTATION STEPS

### Step 1: Implement Serialize() Method

**File:** `src/Services/StickyStateService.cs`

Replace the `Serialize()` method with:

```csharp
public string Serialize(StickyStateSnapshot snapshot)
{
    var sb = new StringBuilder(1024);
    SerializeSticky_WriteHeaderConfig(sb, snapshot);
    SerializeSticky_WriteFleetAnchor(sb, snapshot);
    SerializeSticky_WriteModeProfiles(sb, snapshot);
    SerializeSticky_WritePositions(sb, snapshot);
    return sb.ToString();
}
```

### Step 2: Port SerializeSticky_WriteHeaderConfig()

**Source:** Lines 152-188 from `V12_002.StickyState.cs`

```csharp
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
```

### Step 3: Port SerializeSticky_WriteFleetAnchor()

**Source:** Lines 190-207 from `V12_002.StickyState.cs`

```csharp
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
```

### Step 4: Port SerializeSticky_WriteModeProfiles()

**Source:** Lines 209-258 from `V12_002.StickyState.cs`

```csharp
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
```

### Step 5: Port SerializeSticky_WritePositions()

**Source:** Lines 260-282 from `V12_002.StickyState.cs`

```csharp
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
```

### Step 6: Port AnchorTypeToString()

**Source:** Lines 327-339 from `V12_002.StickyState.cs`

```csharp
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
```

### Step 7: Port AtomicWriteFile()

**Source:** Lines 345-354 from `V12_002.StickyState.cs`

```csharp
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
```

---

## VERIFICATION CHECKLIST

### Build Verification
- [ ] `dotnet build` succeeds
- [ ] No compilation errors in service
- [ ] All using statements present (System.IO, System.Text, System.Globalization)

### Test Verification
- [ ] `dotnet test` still passes
- [ ] Service instantiation test still works
- [ ] No NinjaTrader dependencies introduced

### Code Quality
- [ ] All H18-FIX comments preserved
- [ ] All Build tags preserved (Build 1106, etc.)
- [ ] ASCII-only compliance (no Unicode)
- [ ] CultureInfo.InvariantCulture used for all number formatting

### Logic Verification
- [ ] Serialization methods are 1:1 ports (zero logic drift)
- [ ] StringBuilder usage identical to original
- [ ] Null checks preserved
- [ ] Conditional logic preserved (if/else chains)

---

## ACCEPTANCE CRITERIA

1. ✅ `Serialize()` method implemented
2. ✅ `SerializeSticky_WriteHeaderConfig()` ported (1:1)
3. ✅ `SerializeSticky_WriteFleetAnchor()` ported (1:1)
4. ✅ `SerializeSticky_WriteModeProfiles()` ported (1:1)
5. ✅ `SerializeSticky_WritePositions()` ported (1:1)
6. ✅ `AnchorTypeToString()` ported (1:1)
7. ✅ `AtomicWrite()` implemented
8. ✅ All H18-FIX comments preserved
9. ✅ All Build tags preserved
10. ✅ Zero compilation errors
11. ✅ `dotnet test` passes
12. ✅ ASCII-only compliance verified

---

## CRITICAL PRESERVATION POINTS

### H18-FIX Thread Safety
- ✅ All methods read from `snapshot` parameter (not live state)
- ✅ Comments preserved: "H18-FIX: Use snapshot instead of live dictionary"
- ✅ No direct property access (all via snapshot)

### INI Format Compatibility
- ✅ Section headers unchanged ([CONFIG], [FLEET], [ANCHOR], [POSITIONS])
- ✅ Key=Value format unchanged
- ✅ CultureInfo.InvariantCulture for all numbers
- ✅ Boolean serialization unchanged (1/0)

### Atomic Write Pattern
- ✅ .tmp file creation
- ✅ Delete + Move sequence
- ✅ UTF8 encoding
- ✅ Null/empty path check

---

## NOTES

- This ticket does NOT modify V12_002.StickyState.cs yet
- Strategy integration comes in ticket-04
- Dead code removal comes in ticket-04
- Service remains testable (dotnet test works)

---

## ESTIMATED TIME

**2 hours** (careful 1:1 porting, verification of each method)