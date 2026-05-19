---
# TICKET-06: H18 - StickyState Complete Serialization Snapshot
# Epic: epic-3-reaper
# Defect: BUG-S5-002
# Priority: HIGH
# Estimated CYC Delta: 0 (no new complexity)
---

## 1. Ticket Summary

**Defect:** H18 - Background serialization reads mutable state without isolation  
**File:** [`src/V12_002.StickyState.cs`](src/V12_002.StickyState.cs)  
**Location:** Lines 40-76 (MarkStickyDirty, SerializeStickyState, 4 helper methods)

**Root Cause:** `Task.Run()` spawns background thread that reads strategy properties directly. No synchronization with strategy thread mutations. Causes torn reads and dictionary corruption.

**Fix:** Snapshot ALL mutable state on strategy thread before spawning background task.

---

## 2. Current Code - MarkStickyDirty() (Lines 40-61)

```csharp
private void MarkStickyDirty()
{
    _stickyStateDirty = true;

    if (Interlocked.CompareExchange(ref _stickyWritePending, 1, 0) == 0)
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(STICKY_DEBOUNCE_MS);
                _stickyStateDirty = false;
                string payload = SerializeStickyState();  // Reads mutable state
                AtomicWriteFile(_stickyStatePath, payload);
            }
            catch (Exception ex)
            {
                Print("[STICKY] Save failed (best-effort): " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _stickyWritePending, 0);
                if (_stickyStateDirty)
                    MarkStickyDirty();
            }
        });
    }
}
```

---

## 3. Surgical Repair - MarkStickyDirty()

```csharp
private void MarkStickyDirty()
{
    _stickyStateDirty = true;

    if (Interlocked.CompareExchange(ref _stickyWritePending, 1, 0) == 0)
    {
        // H18-FIX: Capture snapshot of ALL mutable state on strategy thread BEFORE spawning background task.
        // This prevents race conditions where background serialization reads state that's being mutated
        // by IPC commands on the strategy thread. Must snapshot EVERYTHING read by SerializeStickyState().
        
        // Snapshot dictionaries and collections
        var modeProfilesSnapshot = new Dictionary<string, ModeConfigProfile>(_modeProfiles);
        var activeFleetSnapshot = activeFleetAccounts != null 
            ? new Dictionary<string, bool>(activeFleetAccounts) 
            : null;
        var activePositionsSnapshot = activePositions != null
            ? activePositions.ToArray()
            : null;
        
        // H18-FIX: Snapshot header config properties (CRITICAL - eliminates torn reads)
        var headerConfigSnapshot = new {
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

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(STICKY_DEBOUNCE_MS);
                _stickyStateDirty = false;
                string payload = SerializeStickyState(headerConfigSnapshot, modeProfilesSnapshot, activeFleetSnapshot, activePositionsSnapshot);
                AtomicWriteFile(_stickyStatePath, payload);
            }
            catch (Exception ex)
            {
                Print("[STICKY] Save failed (best-effort): " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _stickyWritePending, 0);
                if (_stickyStateDirty)
                    MarkStickyDirty();
            }
        });
    }
}
```

---

## 4. Surgical Repair - SerializeStickyState() Signature

**Current:**
```csharp
private string SerializeStickyState()
{
    var sb = new StringBuilder(1024);
    SerializeSticky_WriteHeaderConfig(sb);
    SerializeSticky_WriteFleetAnchor(sb);
    SerializeSticky_WriteModeProfiles(sb);
    SerializeSticky_WritePositions(sb);
    return sb.ToString();
}
```

**New:**
```csharp
private string SerializeStickyState(
    dynamic headerConfigSnapshot,
    Dictionary<string, ModeConfigProfile> modeProfilesSnapshot,
    Dictionary<string, bool> activeFleetSnapshot,
    KeyValuePair<string, PositionInfo>[] activePositionsSnapshot)
{
    var sb = new StringBuilder(1024);
    SerializeSticky_WriteHeaderConfig(sb, headerConfigSnapshot);
    SerializeSticky_WriteFleetAnchor(sb, activeFleetSnapshot);
    SerializeSticky_WriteModeProfiles(sb, modeProfilesSnapshot);
    SerializeSticky_WritePositions(sb, activePositionsSnapshot);
    return sb.ToString();
}
```

---

## 5. Surgical Repair - Helper Method Signatures

### SerializeSticky_WriteHeaderConfig()

**Update signature and body to read from snapshot:**
```csharp
private void SerializeSticky_WriteHeaderConfig(StringBuilder sb, dynamic headerConfigSnapshot)
{
    // Header
    sb.AppendLine("# V12 StickyState v1");
    sb.AppendLine("# Symbol: " + (Instrument != null ? Instrument.FullName : "unknown"));
    sb.AppendLine("# Updated: " + DateTime.UtcNow.ToString("o"));
    sb.AppendLine("# Build: " + BUILD_TAG);
    sb.AppendLine();

    // [CONFIG] - H18-FIX: Read from snapshot instead of live properties
    sb.AppendLine("[CONFIG]");
    string mode = "OR";
    if (headerConfigSnapshot.IsRMAModeActive) mode = "RMA";
    else if (headerConfigSnapshot.IsTRENDModeActive) mode = "TREND";
    else if (headerConfigSnapshot.IsRetestModeActive) mode = "RETEST";
    else if (headerConfigSnapshot.IsMOMOModeActive) mode = "MOMO";
    else if (headerConfigSnapshot.IsFFMAModeArmed) mode = "FFMA";
    sb.AppendLine("MODE=" + mode);
    sb.AppendLine("COUNT=" + headerConfigSnapshot.ActiveTargetCount.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", headerConfigSnapshot.Target1Value));
    sb.AppendLine("T1TYPE=" + headerConfigSnapshot.T1Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", headerConfigSnapshot.Target2Value));
    sb.AppendLine("T2TYPE=" + headerConfigSnapshot.T2Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", headerConfigSnapshot.Target3Value));
    sb.AppendLine("T3TYPE=" + headerConfigSnapshot.T3Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", headerConfigSnapshot.Target4Value));
    sb.AppendLine("T4TYPE=" + headerConfigSnapshot.T4Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", headerConfigSnapshot.Target5Value));
    sb.AppendLine("T5TYPE=" + headerConfigSnapshot.T5Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}",
        headerConfigSnapshot.IsRMAModeActive ? headerConfigSnapshot.RMAStopATRMultiplier : headerConfigSnapshot.StopMultiplier));
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", headerConfigSnapshot.MaxRiskAmount));
    sb.AppendLine("CIT=" + (headerConfigSnapshot.ChaseIfTouchPoints ?? "0"));
    sb.AppendLine("TRMA=" + (headerConfigSnapshot.IsTrendRmaMode ? "1" : "0"));
    sb.AppendLine("RRMA=" + (headerConfigSnapshot.IsRetestRmaMode ? "1" : "0"));
    sb.AppendLine();
}
```

### SerializeSticky_WriteFleetAnchor()

**Update signature to accept snapshot:**
```csharp
private void SerializeSticky_WriteFleetAnchor(StringBuilder sb, Dictionary<string, bool> activeFleetSnapshot)
{
    sb.AppendLine("[FLEET]");
    sb.AppendLine("LEADER=" + (_stickyLeaderAccount ?? ""));
    if (activeFleetSnapshot != null)
    {
        foreach (var kvp in activeFleetSnapshot)
            sb.AppendLine(kvp.Key + "=" + (kvp.Value ? "1" : "0"));
    }
    sb.AppendLine();
}
```

### SerializeSticky_WriteModeProfiles()

**Update signature to accept snapshot:**
```csharp
private void SerializeSticky_WriteModeProfiles(StringBuilder sb, Dictionary<string, ModeConfigProfile> modeProfilesSnapshot)
{
    // Build 1106: [CONFIG_*] -- per-mode profile snapshots
    // H18-FIX: Use snapshot instead of mutating live _modeProfiles dictionary
    string activeMode = "OR";
    if (isRMAModeActive) activeMode = "RMA";
    else if (isTRENDModeActive) activeMode = "TREND";
    else if (isRetestModeActive) activeMode = "RETEST";
    else if (isMOMOModeActive) activeMode = "MOMO";
    else if (isFFMAModeArmed) activeMode = "FFMA";
    
    // Capture current config into snapshot (not live dictionary)
    modeProfilesSnapshot[activeMode] = SnapshotCurrentConfig();

    foreach (var kvp in modeProfilesSnapshot)
    {
        ModeConfigProfile p = kvp.Value;
        if (p == null) continue;
        sb.AppendLine("[CONFIG_" + kvp.Key + "]");
        // ... rest unchanged
    }
}
```

### SerializeSticky_WritePositions()

**Update signature to accept snapshot:**
```csharp
private void SerializeSticky_WritePositions(StringBuilder sb, KeyValuePair<string, PositionInfo>[] activePositionsSnapshot)
{
    sb.AppendLine("[POSITIONS]");
    sb.AppendLine("# key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount");
    if (activePositionsSnapshot != null)
    {
        foreach (var kvp in activePositionsSnapshot)
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

---

## 6. Implementation Steps

### Step 1: Update MarkStickyDirty()
Add complete snapshot capture before `Task.Run()`.

### Step 2: Update SerializeStickyState() Signature
Add 4 parameters for snapshots.

### Step 3: Update 4 Helper Method Signatures
Add snapshot parameters to each helper method.

### Step 4: Update Helper Method Bodies
Replace direct property reads with snapshot reads.

### Step 5: Verify Syntax
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS

---

## 7. Self-Audit Checklist

### DNA Compliance
```bash
grep -n "lock(" src/V12_002.StickyState.cs
```
Expected: 0 matches

```bash
grep -Prn "[^\x00-\x7F]" src/V12_002.StickyState.cs
```
Expected: 0 matches

### Hard-Link Sync
```bash
powershell -File .\deploy-sync.ps1
```
Expected: PASS + ASCII gate PASS

### Verification
```bash
# Confirm snapshot created
grep -n "var headerConfigSnapshot = new" src/V12_002.StickyState.cs
```
Expected: 1 match

---

## 8. Testing

### Unit Test
```csharp
[Test]
public void Test_H18_StickyState_ConcurrentMutation_NoCorruption()
{
    // Arrange: Background serialization in progress
    // Act: IPC command mutates Target1Value
    // Assert: Serialized state is consistent (no torn reads)
}
```

### Manual Verification
1. Send 100 rapid IPC config changes (10ms interval)
2. Parse .v12state file after each change
3. Verify all key-value pairs match last IPC command
4. Expected: Zero corrupted entries

---

## 9. Success Criteria

- ✅ Complete snapshot added (dictionaries + header config)
- ✅ All 4 helper methods updated to accept snapshots
- ✅ No compile errors
- ✅ Zero new `lock()` statements
- ✅ ASCII gate passes
- ✅ Hard-link sync succeeds
- ✅ Unit test passes
- ✅ Zero config corruption in production

---

## 10. Rollback Plan

```bash
git checkout HEAD~1 -- src/V12_002.StickyState.cs
powershell -File .\deploy-sync.ps1
```

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** None (independent of other tickets)  
**Estimated Time:** 30 minutes (1 file, 6 methods)  
**Risk Level:** LOW (snapshot pattern, no logic changes)