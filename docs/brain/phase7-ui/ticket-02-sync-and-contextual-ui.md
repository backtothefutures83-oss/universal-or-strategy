# TICKET T-D + T-F: OnSyncAllClick + UpdateContextualUI Extraction
**Epic**: phase7-ui  
**Priority**: HIGH  
**Ticket ID**: T-D + T-F (bundled)  
**Agent**: Bob CLI (v12-engineer)  
**Estimated Sessions**: 1

---

## Mission Brief

Extract two methods in the same file (`V12_002.UI.Panel.Handlers.cs`) in a single session:
- **T-D**: [`OnSyncAllClick()`](../../../src/V12_002.UI.Panel.Handlers.cs:238) — CYC 37 → ≤5
- **T-F**: [`UpdateContextualUI()`](../../../src/V12_002.UI.Panel.Handlers.cs:427) — CYC 36 → ≤5

**Rationale for Bundling**: Same file, shared UI state context, single deploy-sync + single F5 validation pass.

---

## T-D: OnSyncAllClick Extraction

### Current State
**File**: `src/V12_002.UI.Panel.Handlers.cs:238-273`  
**Method**: `OnSyncAllClick(object sender, RoutedEventArgs e)`  
**CYC**: 37  
**LOC**: 36  
**Complexity Driver**: Mode resolution + 5 target type extractions + StringBuilder assembly + null-safe field access

### Target State
**Residual CYC**: ≤5 (target: 3)  
**Helpers**: 3 extraction methods

### Extraction Plan

#### Helper 1: ResolveEffectiveSyncMode()
**Returns**: string (normalized mode)  
**LOC**: ~8  
**CYC**: ~3

**Note**: Below 15-LOC floor, but acceptable for clarity.

```csharp
private string ResolveEffectiveSyncMode()
{
    string mode = _panelLastSyncedMode;
    if (string.IsNullOrEmpty(mode))
        mode = GetCurrentConfigMode();
    if (string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase))
        mode = "ORB";
    return mode;
}
```

#### Helper 2: ExtractTargetConfiguration()
**Returns**: TargetConfig struct  
**LOC**: ~35  
**CYC**: ~10

```csharp
private struct TargetConfig
{
    public string T1Type, T2Type, T3Type, T4Type, T5Type;
    public string T1Val, T2Val, T3Val, T4Val, T5Val;
    public string Str, Max, Cit;
    public bool TrendRma, RetestRma;
    public int Count;
}

private TargetConfig ExtractTargetConfiguration()
{
    var config = new TargetConfig();
    
    config.T1Type = (svT1Type != null && svT1Type.SelectedItem is ComboBoxItem t1Item) ? (t1Item.Content as string ?? "ATR") : "ATR";
    config.T2Type = (svT2Type != null && svT2Type.SelectedItem is ComboBoxItem t2Item) ? (t2Item.Content as string ?? "ATR") : "ATR";
    config.T3Type = (svT3Type != null && svT3Type.SelectedItem is ComboBoxItem t3Item) ? (t3Item.Content as string ?? "ATR") : "ATR";
    config.T4Type = (svT4Type != null && svT4Type.SelectedItem is ComboBoxItem t4Item) ? (t4Item.Content as string ?? "ATR") : "ATR";
    config.T5Type = (svT5Type != null && svT5Type.SelectedItem is ComboBoxItem t5Item) ? (t5Item.Content as string ?? "ATR") : "ATR";
    
    config.T1Val = svT1Val != null ? svT1Val.Text : "0";
    config.T2Val = svT2Val != null ? svT2Val.Text : "0";
    config.T3Val = svT3Val != null ? svT3Val.Text : "0";
    config.T4Val = svT4Val != null ? svT4Val.Text : "0";
    config.T5Val = svT5Val != null ? svT5Val.Text : "0";
    
    config.Str = strVal != null ? strVal.Text : "0";
    config.Cit = citVal != null ? citVal.Text : "0";
    
    string maxText = maxVal != null ? maxVal.Text : string.Empty;
    if (maxText == null) maxText = string.Empty;
    config.Max = maxText.Replace("$", string.Empty).Replace(" ", string.Empty);
    
    config.TrendRma = isTrendRmaMode;
    config.RetestRma = isRetestRmaMode;
    config.Count = Math.Max(1, Math.Min(5, _panelLastSyncedTargetCount > 0 ? _panelLastSyncedTargetCount : activeTargetCount));
    
    return config;
}
```

#### Helper 3: BuildConfigString()
**Returns**: string (CONFIG protocol string)  
**LOC**: ~20  
**CYC**: ~2

```csharp
private string BuildConfigString(string mode, TargetConfig config)
{
    StringBuilder sb = new StringBuilder();
    sb.Append("CONFIG|");
    sb.Append(string.Equals(mode, "ORB", StringComparison.OrdinalIgnoreCase) ? "OR" : mode);
    sb.Append("|");
    sb.Append("COUNT:").Append(config.Count).Append(";");
    sb.Append("T1:").Append(config.T1Val).Append(";T1TYPE:").Append(config.T1Type).Append(";");
    sb.Append("T2:").Append(config.T2Val).Append(";T2TYPE:").Append(config.T2Type).Append(";");
    sb.Append("T3:").Append(config.T3Val).Append(";T3TYPE:").Append(config.T3Type).Append(";");
    sb.Append("T4:").Append(config.T4Val).Append(";T4TYPE:").Append(config.T4Type).Append(";");
    sb.Append("T5:").Append(config.T5Val).Append(";T5TYPE:").Append(config.T5Type).Append(";");
    sb.Append("STR:").Append(config.Str).Append(";");
    sb.Append("MAX:").Append(config.Max).Append(";");
    sb.Append("CIT:").Append(config.Cit).Append(";");
    sb.Append("TRMA:").Append(config.TrendRma ? "1" : "0").Append(";");
    sb.Append("RRMA:").Append(config.RetestRma ? "1" : "0").Append(";");
    return sb.ToString();
}
```

#### Residual Coordinator
**CYC**: 3

```csharp
private void OnSyncAllClick(object sender, RoutedEventArgs e)
{
    string mode = ResolveEffectiveSyncMode();
    TargetConfig config = ExtractTargetConfiguration();
    string configString = BuildConfigString(mode, config);
    
    PanelCommand(configString);
    Print("V12 PANEL: SYNC ALL -> " + mode + " / count " + config.Count);
}
```

---

## T-F: UpdateContextualUI Extraction

### Current State
**File**: `src/V12_002.UI.Panel.Handlers.cs:427-491`  
**Method**: `UpdateContextualUI(string mode)`  
**CYC**: 36  
**LOC**: 65  
**Complexity Driver**: Mode normalization + 10 null-guarded visibility sets + 7-case switch + direction combo population

### Target State
**Residual CYC**: ≤5 (target: 4)  
**Helpers**: 3 extraction methods

### Extraction Plan

#### Helper 1: CollapseAllExecutionControls()
**LOC**: ~20  
**CYC**: ~10

```csharp
private void CollapseAllExecutionControls()
{
    if (execRetestRow != null) execRetestRow.Visibility = Visibility.Collapsed;
    if (execTrendRow != null) execTrendRow.Visibility = Visibility.Collapsed;
    if (rmaButton != null) rmaButton.Visibility = Visibility.Collapsed;
    if (momoButton != null) momoButton.Visibility = Visibility.Collapsed;
    if (ffmaButton != null) ffmaButton.Visibility = Visibility.Collapsed;
    if (ffmaManualButton != null) ffmaManualButton.Visibility = Visibility.Collapsed;
    if (mButton != null) mButton.Visibility = Visibility.Collapsed;
    if (orLongButton != null) orLongButton.Visibility = Visibility.Collapsed;
    if (orShortButton != null) orShortButton.Visibility = Visibility.Collapsed;
    if (manualEntryRow != null) manualEntryRow.Visibility = Visibility.Visible;
}
```

#### Helper 2: ShowModeSpecificControls()
**Parameter**: string mode (normalized)  
**LOC**: ~50  
**CYC**: ~8

```csharp
private void ShowModeSpecificControls(string mode)
{
    switch (mode)
    {
        case "ORB":
            if (orLongButton != null) orLongButton.Visibility = Visibility.Visible;
            if (orShortButton != null) orShortButton.Visibility = Visibility.Visible;
            break;
        case "RMA":
            if (rmaButton != null) rmaButton.Visibility = Visibility.Visible;
            break;
        case "RETEST":
            if (execRetestRow != null) execRetestRow.Visibility = Visibility.Visible;
            break;
        case "MOMO":
            if (momoButton != null) momoButton.Visibility = Visibility.Visible;
            break;
        case "FFMA":
            if (ffmaButton != null) ffmaButton.Visibility = Visibility.Visible;
            if (ffmaManualButton != null) ffmaManualButton.Visibility = Visibility.Visible;
            if (manualEntryRow != null) manualEntryRow.Visibility = Visibility.Collapsed;
            break;
        case "TREND":
            if (execTrendRow != null) execTrendRow.Visibility = Visibility.Visible;
            break;
        case "MNL":
            if (mButton != null) mButton.Visibility = Visibility.Visible;
            break;
        default:
            if (orLongButton != null) orLongButton.Visibility = Visibility.Visible;
            if (orShortButton != null) orShortButton.Visibility = Visibility.Visible;
            break;
    }
}
```

#### Helper 3: PopulateDirectionCombo()
**Parameter**: string mode (normalized)  
**LOC**: ~18  
**CYC**: ~3

```csharp
private void PopulateDirectionCombo(string mode)
{
    if (directionCombo == null) return;
    
    directionCombo.Items.Clear();
    if (mode == "ORB")
    {
        directionCombo.Items.Add(new ComboBoxItem { Content = "OR LONG", Foreground = TextPrimary });
        directionCombo.Items.Add(new ComboBoxItem { Content = "OR SHORT", Foreground = TextPrimary });
    }
    else
    {
        directionCombo.Items.Add(new ComboBoxItem { Content = "LONG", Foreground = TextPrimary });
        directionCombo.Items.Add(new ComboBoxItem { Content = "SHORT", Foreground = TextPrimary });
    }
    directionCombo.SelectedIndex = 0;
}
```

#### Residual Coordinator
**CYC**: 4

```csharp
private void UpdateContextualUI(string mode)
{
    string upperMode = string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase)
        ? "ORB"
        : (mode ?? "ORB").ToUpperInvariant();
    
    CollapseAllExecutionControls();
    ShowModeSpecificControls(upperMode);
    PopulateDirectionCombo(upperMode);
}
```

---

## V12 DNA Compliance

### Lock-Free ✅
- All methods run on NT UI thread (single-threaded)
- No concurrency, no locks possible

### ASCII-Only ✅
- T-D: Existing Print() preserved, ASCII-only
- T-F: No Print() calls

### Zero-Allocation ✅
- T-D: TargetConfig is struct (stack allocation, acceptable for non-hot-path)
- T-F: No new allocations (visibility changes only)

---

## Test Matrix

### T-D: OnSyncAllClick Tests
Execute in order. Any failure is a BLOCKER.

#### Single Chart Tests
- [ ] 1 chart open: Click Sync All → CONFIG command sent to self
- [ ] Verify Print output: "V12 PANEL: SYNC ALL -> [mode] / count [N]"

#### Multi-Chart Fleet Tests
- [ ] 2 charts open (ES + NQ): Click Sync All on ES → both receive CONFIG
- [ ] 3 charts open (ES + NQ + YM): Click Sync All on ES → all receive CONFIG
- [ ] Verify all charts show same config after sync

#### Mode Switching Tests
- [ ] Start in ORB mode → Click Sync All → CONFIG|OR sent
- [ ] Switch to RMA mode → Click Sync All → CONFIG|RMA sent
- [ ] Switch to RETEST mode → Click Sync All → CONFIG|RETEST sent
- [ ] Switch to MOMO mode → Click Sync All → CONFIG|MOMO sent
- [ ] Switch to FFMA mode → Click Sync All → CONFIG|FFMA sent
- [ ] Switch to TREND mode → Click Sync All → CONFIG|TREND sent

#### Target Count Tests
- [ ] Set count = 1 → Click Sync All → COUNT:1 in CONFIG string
- [ ] Set count = 2 → Click Sync All → COUNT:2 in CONFIG string
- [ ] Set count = 3 → Click Sync All → COUNT:3 in CONFIG string
- [ ] Set count = 4 → Click Sync All → COUNT:4 in CONFIG string
- [ ] Set count = 5 → Click Sync All → COUNT:5 in CONFIG string

#### Field Value Tests
- [ ] Modify T1 value → Click Sync All → T1:[value] in CONFIG string
- [ ] Modify T2 type (ATR → Fixed) → Click Sync All → T2TYPE:Fixed in CONFIG string
- [ ] Modify STR value → Click Sync All → STR:[value] in CONFIG string
- [ ] Modify MAX value → Click Sync All → MAX:[value] in CONFIG string ($ and spaces stripped)
- [ ] Modify CIT value → Click Sync All → CIT:[value] in CONFIG string

#### RMA Toggle Tests
- [ ] Enable Trend RMA → Click Sync All → TRMA:1 in CONFIG string
- [ ] Disable Trend RMA → Click Sync All → TRMA:0 in CONFIG string
- [ ] Enable Retest RMA → Click Sync All → RRMA:1 in CONFIG string
- [ ] Disable Retest RMA → Click Sync All → RRMA:0 in CONFIG string

### T-F: UpdateContextualUI Tests
Execute in order. Any failure is a BLOCKER.

#### Mode Visibility Tests
- [ ] Switch to ORB mode → OR LONG + OR SHORT buttons visible, others collapsed
- [ ] Switch to RMA mode → RMA button visible, others collapsed
- [ ] Switch to RETEST mode → Retest row visible, others collapsed
- [ ] Switch to MOMO mode → MOMO button visible, others collapsed
- [ ] Switch to FFMA mode → FFMA + FFMA Manual buttons visible, manual entry row collapsed
- [ ] Switch to TREND mode → Trend row visible, others collapsed
- [ ] Switch to MNL mode → M button visible, others collapsed

#### Direction Combo Tests
- [ ] ORB mode → Direction combo shows "OR LONG" and "OR SHORT"
- [ ] RMA mode → Direction combo shows "LONG" and "SHORT"
- [ ] RETEST mode → Direction combo shows "LONG" and "SHORT"
- [ ] MOMO mode → Direction combo shows "LONG" and "SHORT"
- [ ] FFMA mode → Direction combo shows "LONG" and "SHORT"
- [ ] TREND mode → Direction combo shows "LONG" and "SHORT"
- [ ] MNL mode → Direction combo shows "LONG" and "SHORT"

#### Edge Case Tests
- [ ] Pass null mode → Defaults to ORB (OR LONG + OR SHORT visible)
- [ ] Pass "OR" mode → Normalizes to ORB (OR LONG + OR SHORT visible)
- [ ] Pass lowercase "orb" → Normalizes to ORB (OR LONG + OR SHORT visible)

---

## Acceptance Criteria

### Quantitative
- [ ] T-D residual CYC ≤ 5 (target: 3)
- [ ] T-D all 3 helpers CYC ≤ 19
- [ ] T-F residual CYC ≤ 5 (target: 4)
- [ ] T-F all 3 helpers CYC ≤ 19
- [ ] `python scripts/complexity_audit.py` shows:
  - OnSyncAllClick: 37 → ≤5
  - UpdateContextualUI: 36 → ≤5

### Qualitative
- [ ] Zero behavioral change (same CONFIG strings, same UI states)
- [ ] All T-D test matrix items PASS
- [ ] All T-F test matrix items PASS

### Process
- [ ] `powershell -File .\deploy-sync.ps1` exits 0, ASCII gate PASS
- [ ] F5 in NinjaTrader: BUILD_TAG banner appears
- [ ] Git commit: `[phase7-ui] T-D+T-F: OnSyncAllClick + UpdateContextualUI extraction -- CYC 37+36->3+4 [BUILD_TAG]`

---

## Execution Notes for Bob

### Critical Path
1. **Read both methods** (lines 238-273, 427-491)
2. **Create T-D helpers** (ResolveEffectiveSyncMode, ExtractTargetConfiguration, BuildConfigString)
3. **Create TargetConfig struct** (place near top of file with other structs/classes)
4. **Replace OnSyncAllClick body** (3 helper calls + PanelCommand + Print)
5. **Create T-F helpers** (CollapseAllExecutionControls, ShowModeSpecificControls, PopulateDirectionCombo)
6. **Replace UpdateContextualUI body** (mode normalization + 3 helper calls)
7. **Verify compilation**
8. **Run complexity audit** (verify both methods ≤5)
9. **Run deploy-sync**
10. **F5 + test matrix** (all tests must PASS)
11. **Commit**

### Common Pitfalls
- **TargetConfig struct**: Place near top of file, not inside a method
- **StringBuilder**: Reuse existing pattern, don't change to string interpolation
- **Mode normalization**: Preserve exact logic (OR → ORB, null → ORB, ToUpperInvariant)
- **Visibility logic**: Preserve exact order (collapse all, then show mode-specific)
- **Direction combo**: Clear items before adding new ones

### Rollback Strategy
If any test fails:
1. `git reset --hard HEAD~1`
2. `powershell -File .\deploy-sync.ps1`
3. F5 in NinjaTrader
4. Report failure to Director

---

## Dependencies

**Prerequisite**: T-C complete and F5-validated  
**Blocks**: T-A + T-B (must complete T-D + T-F before Command Pattern tickets)

---

## References

- **Epic Scope**: [`00-scope.md`](00-scope.md)
- **Analysis**: [`01-analysis.md`](01-analysis.md) (T-D and T-F sections)
- **Approach**: [`02-approach.md`](02-approach.md) (T-D and T-F sections)
- **Validation**: [`03-validation.md`](03-validation.md)
- **Source File**: `src/V12_002.UI.Panel.Handlers.cs:238-273, 427-491`

---

[TICKET-GATE]