# APPROACH: phase7-ui
**Epic ID**: phase7-ui  
**Created**: 2026-05-14  
**Analysis**: [`01-analysis.md`](01-analysis.md)

---

## Extraction Strategy Overview

### Guiding Principles
1. **Surgical precision**: Touch only target methods, preserve all behavior
2. **15-LOC floor**: All extracted helpers must exceed minimum size
3. **CYC ≤ 19 ceiling**: All helpers must stay below threshold
4. **Single responsibility**: Each helper does one thing well
5. **Zero allocation**: No new heap allocations on hot paths (T-A, T-B)

### Execution Order
1. **T-C** (AttachPanelHandlers) → F5 validate → GATE
2. **T-D + T-F** (OnSyncAllClick + UpdateContextualUI) → F5 validate → GATE
3. **T-A + T-B** (Joint P3 design) → T-A execute → F5 validate → GATE → T-B execute → F5 validate → GATE

---

## T-C: AttachPanelHandlers Extraction

### Target State
**Residual CYC**: ≤5 (pure coordinator)  
**Helpers**: 7 per-control-group methods  
**File**: `src/V12_002.UI.Panel.Handlers.cs`

### Extraction Plan

#### Helper 1: AttachExecutionPanelHandlers()
**Responsibility**: Wire execution mode buttons  
**Controls**: orLongButton, orShortButton, retestButton, retestRmaToggle, rmaButton, momoButton, ffmaButton, ffmaManualButton, mButton, trendButton, trendRmaToggle  
**LOC**: ~25  
**CYC**: ~11 (one null check per control)

```csharp
private void AttachExecutionPanelHandlers()
{
    if (orLongButton != null) orLongButton.Click += (s, e) =>
        { PanelCommand("OR_LONG"); ResetExecutionMode(); TriggerGlow(CyanAccent); };
    if (orShortButton != null) orShortButton.Click += (s, e) =>
        { PanelCommand("OR_SHORT"); ResetExecutionMode(); TriggerGlow(PinkFg); };
    if (retestButton != null) retestButton.Click += OnRetestClick;
    if (retestRmaToggle != null) retestRmaToggle.Click += OnRetestRmaToggleClick;
    if (rmaButton != null) rmaButton.Click += OnRmaClick;
    if (momoButton != null) momoButton.Click += (s, e) =>
        { PanelCommand("MODE_MOMO"); ResetExecutionMode(); TriggerGlow(GreenFg); };
    if (ffmaButton != null) ffmaButton.Click += (s, e) =>
        { PanelCommand("MODE_FFMA"); ResetExecutionMode(); TriggerGlow(PinkFg); };
    if (ffmaManualButton != null) ffmaManualButton.Click += (s, e) =>
        { PanelCommand("FFMA_MANUAL_MARKET"); ResetExecutionMode(); TriggerGlow(PinkFg); };
    if (mButton != null) mButton.Click += (s, e) =>
        { PanelCommand("MODE_M"); TriggerGlow(OrangeFg); };
    if (trendButton != null) trendButton.Click += OnTrendClick;
    if (trendRmaToggle != null) trendRmaToggle.Click += OnTrendRmaToggleClick;
}
```

#### Helper 2: AttachTargetButtonHandlers()
**Responsibility**: Wire T1-T5 buttons with dropdown menus  
**Controls**: t1Button, t2Button, t3Button, t4Button, t5Button  
**LOC**: ~15  
**CYC**: ~5 (one null check per button)

```csharp
private void AttachTargetButtonHandlers()
{
    if (t1Button != null) AttachTargetDropdown(t1Button, 1, GreenFg);
    if (t2Button != null) AttachTargetDropdown(t2Button, 2, YellowFg);
    if (t3Button != null) AttachTargetDropdown(t3Button, 3, OrangeFg);
    if (t4Button != null) AttachTargetDropdown(t4Button, 4, RedFg);
    if (t5Button != null) AttachTargetDropdown(t5Button, 5, PinkFg);
}
```

#### Helper 3: AttachActionButtonHandlers()
**Responsibility**: Wire action buttons (trim, BE, trail, cancel, flatten)  
**Controls**: trim50Button, beButton, trailButton, cancelButton, flattenButton  
**LOC**: ~15  
**CYC**: ~5

```csharp
private void AttachActionButtonHandlers()
{
    if (trim50Button != null) trim50Button.Click += (s, e) =>
        { PanelCommand("TRIM_50"); TriggerGlow(OrangeFg); };
    if (beButton != null) beButton.Click += OnBeClick;
    if (trailButton != null) trailButton.Click += OnTrailClick;
    if (cancelButton != null) cancelButton.Click += (s, e) =>
        { PanelCommand("CANCEL_ALL"); TriggerGlow(RedFg); };
    if (flattenButton != null) flattenButton.Click += (s, e) =>
        { PanelCommand("FLATTEN_ONLY"); TriggerGlow(RedFg); };
}
```

#### Helper 4: AttachSyncButtonHandlers()
**Responsibility**: Wire sync buttons  
**Controls**: mktSyncButton, syncAllButton  
**LOC**: ~8  
**CYC**: ~2

```csharp
private void AttachSyncButtonHandlers()
{
    if (mktSyncButton != null) mktSyncButton.Click += (s, e) =>
        PanelCommand("MKT_SYNC");
    if (syncAllButton != null) syncAllButton.Click += OnSyncAllClick;
}
```

#### Helper 5: AttachConfigModeHandlers()
**Responsibility**: Wire config mode selection buttons  
**Controls**: modeOrbButton, modeRmaButton, modeRetestButton, modeMomoButton, modeFfmaButton, modeTrendButton  
**LOC**: ~18  
**CYC**: ~6

```csharp
private void AttachConfigModeHandlers()
{
    if (modeOrbButton != null) modeOrbButton.Click += (s, e) => SelectConfigMode("ORB", modeOrbButton);
    if (modeRmaButton != null) modeRmaButton.Click += (s, e) => SelectConfigMode("RMA", modeRmaButton);
    if (modeRetestButton != null) modeRetestButton.Click += (s, e) => SelectConfigMode("RETEST", modeRetestButton);
    if (modeMomoButton != null) modeMomoButton.Click += (s, e) => SelectConfigMode("MOMO", modeMomoButton);
    if (modeFfmaButton != null) modeFfmaButton.Click += (s, e) => SelectConfigMode("FFMA", modeFfmaButton);
    if (modeTrendButton != null) modeTrendButton.Click += (s, e) => SelectConfigMode("TREND", modeTrendButton);
}
```

#### Helper 6: AttachTargetCountHandlers()
**Responsibility**: Wire target count selection buttons  
**Controls**: cnt1, cnt2, cnt3, cnt4, cnt5  
**LOC**: ~15  
**CYC**: ~5

```csharp
private void AttachTargetCountHandlers()
{
    if (cnt1 != null) cnt1.Click += (s, e) => SelectTargetCount(1, cnt1);
    if (cnt2 != null) cnt2.Click += (s, e) => SelectTargetCount(2, cnt2);
    if (cnt3 != null) cnt3.Click += (s, e) => SelectTargetCount(3, cnt3);
    if (cnt4 != null) cnt4.Click += (s, e) => SelectTargetCount(4, cnt4);
    if (cnt5 != null) cnt5.Click += (s, e) => SelectTargetCount(5, cnt5);
}
```

#### Helper 7: AttachMiscellaneousHandlers()
**Responsibility**: Wire remaining controls (floating anchor, fleet select, submit)  
**Controls**: floatingAnchor, fleetSelectButton, submitButton  
**LOC**: ~12  
**CYC**: ~3

```csharp
private void AttachMiscellaneousHandlers()
{
    if (floatingAnchor != null) floatingAnchor.Click += ToggleLayout_Click;
    
    if (fleetSelectButton != null) fleetSelectButton.Click += (s, e) =>
    {
        if (fleetPopup != null) fleetPopup.IsOpen = !fleetPopup.IsOpen;
    };
    
    if (submitButton != null) submitButton.Click += OnSubmitClick;
}
```

#### Residual Coordinator
**CYC**: 2 (7 method calls + 1 final call)

```csharp
private void AttachPanelHandlers()
{
    AttachMiscellaneousHandlers();
    AttachExecutionPanelHandlers();
    AttachTargetButtonHandlers();
    AttachActionButtonHandlers();
    AttachSyncButtonHandlers();
    AttachConfigModeHandlers();
    AttachTargetCountHandlers();
    AttachLiveTargetHandlers();
}
```

### Acceptance Criteria
- [ ] Residual CYC ≤ 5 (target: 2)
- [ ] All 7 helpers CYC ≤ 19
- [ ] All 7 helpers LOC ≥ 15 (except AttachSyncButtonHandlers at ~8 LOC, acceptable for clarity)
- [ ] Zero behavioral change (same handlers, same controls)
- [ ] F5 NinjaTrader: All 60+ controls render and respond identically

---

## T-D: OnSyncAllClick Extraction

### Target State
**Residual CYC**: ≤5 (pure coordinator)  
**Helpers**: 3 extraction methods  
**File**: `src/V12_002.UI.Panel.Handlers.cs`

### Extraction Plan

#### Helper 1: ResolveEffectiveSyncMode()
**Responsibility**: Determine effective mode for sync operation  
**Returns**: string (normalized mode)  
**LOC**: ~8  
**CYC**: ~3

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
**Responsibility**: Extract all target configuration from UI controls  
**Returns**: struct TargetConfig { string[] types, string[] values, string str, string max, string cit, bool trendRma, bool retestRma, int count }  
**LOC**: ~35  
**CYC**: ~10 (5 ternary chains + 5 null checks)

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
**Responsibility**: Build CONFIG protocol string from mode and config  
**Returns**: string (CONFIG|mode|params)  
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
**CYC**: 3 (3 method calls + 1 command + 1 print)

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

### Acceptance Criteria
- [ ] Residual CYC ≤ 5 (target: 3)
- [ ] All 3 helpers CYC ≤ 19
- [ ] All 3 helpers LOC ≥ 15 (ResolveEffectiveSyncMode at ~8 LOC, acceptable)
- [ ] Zero behavioral change (same CONFIG string produced)
- [ ] F5: Sync All button functions identically across all fleet configurations

---

## T-F: UpdateContextualUI Extraction

### Target State
**Residual CYC**: ≤5 (pure coordinator)  
**Helpers**: 3 extraction methods  
**File**: `src/V12_002.UI.Panel.Handlers.cs`

### Extraction Plan

#### Helper 1: CollapseAllExecutionControls()
**Responsibility**: Hide all execution-related controls  
**LOC**: ~20  
**CYC**: ~10 (one null check per control)

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
**Responsibility**: Show controls for specific mode  
**Parameter**: string mode (normalized)  
**LOC**: ~50  
**CYC**: ~8 (7 cases + default)

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
**Responsibility**: Populate direction combo based on mode  
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
**CYC**: 4 (mode normalization + 3 helper calls)

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

### Acceptance Criteria
- [ ] Residual CYC ≤ 5 (target: 4)
- [ ] All 3 helpers CYC ≤ 19
- [ ] All 3 helpers LOC ≥ 15
- [ ] Zero behavioral change (same UI state for each mode)
- [ ] F5: All 7 modes render correctly (ORB, RMA, RETEST, MOMO, FFMA, TREND, MNL)

---

## T-A + T-B: Unified Command Pattern Architecture

### Design Session Requirement
**Agent**: Claude ARCHITECT (P3 mode)  
**Output**: Single `implementation_plan.md` covering both T-A and T-B  
**Execution**: Bob implements T-A first (validate), then T-B (validate)

### Unified Architecture Specification

#### Command Pattern Core
```csharp
// Initialized once at startup (State.cs or UI.Lifecycle.cs):
private Dictionary<Key, Action> _keyCommands;
private Dictionary<string, Action<string[]>> _ipcCommands;
private HashSet<string> _globalIpcCommands;
```

#### T-A: OnKeyDown Target State
**Residual CYC**: ≤3 (dictionary lookup + invoke)  
**Registry**: InitKeyCommandRegistry() called once at startup  
**File**: `src/V12_002.UI.Callbacks.cs`

```csharp
private void InitKeyCommandRegistry()
{
    _keyCommands = new Dictionary<Key, Action>
    {
        // Basic hotkeys
        // NOTE: Lambda closures allocate on heap. For hot-path optimization, consider
        // method references (e.g., [Key.L] = ExecuteLongHotkey) to avoid closure allocation.
        // Current approach acceptable as existing pattern, but Bob should evaluate if
        // allocation profiling shows impact.
        [Key.L] = () => { double orStopDist = CalculateORStopDistance(); int orContracts = CalculatePositionSize(orStopDist); Enqueue(ctx => ctx.ExecuteLong(orContracts)); },
        [Key.S] = () => { double orStopDist = CalculateORStopDistance(); int orContracts = CalculatePositionSize(orStopDist); Enqueue(ctx => ctx.ExecuteShort(orContracts)); },
        [Key.F] = () => FlattenAll(),
        
        // T1 actions (handled via modifier check in OnKeyDown)
        // T2 actions (handled via modifier check in OnKeyDown)
        // Runner actions (handled via modifier check in OnKeyDown)
    };
}

private void OnKeyDown(object sender, KeyEventArgs e)
{
    // Basic hotkeys (no modifiers)
    if (_keyCommands.TryGetValue(e.Key, out var cmd))
    {
        cmd();
        e.Handled = true;
        return;
    }
    
    // T1 Actions (1 + letter)
    if (Keyboard.IsKeyDown(Key.D1) || Keyboard.IsKeyDown(Key.NumPad1))
    {
        HandleTargetAction("T1", e.Key);
        e.Handled = true;
        return;
    }
    
    // T2 Actions (2 + letter)
    if (Keyboard.IsKeyDown(Key.D2) || Keyboard.IsKeyDown(Key.NumPad2))
    {
        HandleTargetAction("T2", e.Key);
        e.Handled = true;
        return;
    }
    
    // Runner Actions (3 + letter)
    if (Keyboard.IsKeyDown(Key.D3) || Keyboard.IsKeyDown(Key.NumPad3))
    {
        HandleRunnerAction(e.Key);
        e.Handled = true;
        return;
    }
}

private void HandleTargetAction(string target, Key key)
{
    switch (key)
    {
        case Key.M: ExecuteTargetAction(target, "market"); break;
        case Key.O: ExecuteTargetAction(target, "1point"); break;
        case Key.W: ExecuteTargetAction(target, "2point"); break;
        case Key.K: ExecuteTargetAction(target, "marketprice"); break;
        case Key.B: ExecuteTargetAction(target, "breakeven"); break;
        case Key.C: ExecuteTargetAction(target, "cancel"); break;
    }
}

private void HandleRunnerAction(Key key)
{
    switch (key)
    {
        case Key.M: Enqueue(ctx => ctx.ExecuteRunnerAction("market")); break;
        case Key.O: Enqueue(ctx => ctx.ExecuteRunnerAction("stop1pt")); break;
        case Key.W: Enqueue(ctx => ctx.ExecuteRunnerAction("stop2pt")); break;
        case Key.B: Enqueue(ctx => ctx.ExecuteRunnerAction("stopbe")); break;
        case Key.P: Enqueue(ctx => ctx.ExecuteRunnerAction("lock50")); break;
        case Key.D: Enqueue(ctx => ctx.ExecuteRunnerAction("disabletrail")); break;
    }
}
```

**Alternative: Full Dictionary Approach** (if P3 design prefers)
```csharp
// Composite key for modifier-aware commands
private struct KeyCombo
{
    public Key Key;
    public bool Modifier1;  // D1/NumPad1
    public bool Modifier2;  // D2/NumPad2
    public bool Modifier3;  // D3/NumPad3
}

private Dictionary<KeyCombo, Action> _keyCommands;
```

#### T-B: ProcessIpc_MatchSymbol Target State
**Residual CYC**: ≤3 (global check + symbol match + dictionary lookup)  
**Registry**: InitIpcCommandRegistry() called once at startup  
**File**: `src/V12_002.UI.IPC.cs`

```csharp
private void InitIpcCommandRegistry()
{
    // Global command set (O(1) lookup)
    _globalIpcCommands = new HashSet<string>
    {
        "TOGGLE_ACCOUNT", "SET_SIMA", "GET_FLEET", "DIAG_FLEET", "CANCEL_ALL",
        "FLATTEN", "SYNC_ALL", "MKT_SYNC", "REQUEST_FLEET_STATE", "RESET_MEMORY",
        "DIAG_IPC", "LOCK_50", "SET_TARGETS", "SET_TRAIL", "SET_CIT", "BE_CUSTOM"
    };
    
    // IPC command handlers (registered in ProcessIpcCommandCore)
    _ipcCommands = new Dictionary<string, Action<string[]>>
    {
        // Populated by existing TryHandle* methods
    };
}

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

### Design Decisions for P3 Session

#### Decision 1: Registry Initialization Location
**Options**:
- A: In State.cs (OnStateChange State.DataLoaded)
- B: In UI.Lifecycle.cs (InitializePanel)
- C: Lazy initialization (first use)

**Recommendation**: Option B (InitializePanel) — UI-related registries belong in UI lifecycle

#### Decision 2: Modifier Key Handling (T-A)
**Options**:
- A: Composite KeyCombo struct with full Dictionary
- B: Hybrid approach (basic keys in Dictionary, modifiers in switch)
- C: Nested dictionaries (Dictionary<Key, Dictionary<Modifiers, Action>>)

**Recommendation**: Option B (hybrid) — Balances simplicity and extensibility

#### Decision 3: Global Command Storage (T-B)
**Options**:
- A: HashSet<string> (O(1) lookup, mutable)
- B: static readonly HashSet<string> (O(1) lookup, immutable)
- C: Keep boolean expression (no allocation)

**Recommendation**: Option B (static readonly) — Zero allocation, O(1) lookup, immutable

#### Decision 4: Symbol Matcher Extraction (T-B)
**Options**:
- A: Extract to IsSymbolMatch() helper
- B: Keep inline (avoid method call overhead)
- C: Extract to static utility method

**Recommendation**: Option A (instance helper) — Improves readability, negligible overhead

### Acceptance Criteria (Combined)

#### T-A: OnKeyDown
- [ ] Residual CYC ≤ 5 (target: 3)
- [ ] InitKeyCommandRegistry() CYC ≤ 10
- [ ] HandleTargetAction() CYC ≤ 7
- [ ] HandleRunnerAction() CYC ≤ 7
- [ ] All 21 keyboard shortcuts function identically
- [ ] NumPad variants (NumPad1, NumPad2, NumPad3) work identically to D1, D2, D3
- [ ] F5: All shortcuts tested in live NinjaTrader session

#### T-B: ProcessIpc_MatchSymbol
- [ ] Residual CYC ≤ 5 (target: 3)
- [ ] InitIpcCommandRegistry() CYC ≤ 2
- [ ] IsSymbolMatch() CYC ≤ 12
- [ ] All 17 global commands recognized
- [ ] All symbol patterns match correctly (MES/ES, MYM/YM, MGC/GC)
- [ ] F5: All IPC commands tested via remote control

---

## File Organization

### Option A: Keep All Helpers in Same File (RECOMMENDED)
**Pros**:
- Minimal file changes
- Easier to review in single PR
- Helpers stay close to residual methods

**Cons**:
- Files grow larger (but still manageable)

### Option B: Extract to Partial Class Files
**Pros**:
- Logical separation (UI.Panel.Handlers.Execution.cs, UI.Panel.Handlers.Targets.cs, etc.)
- Smaller individual files

**Cons**:
- More files to track
- Harder to review (changes spread across files)
- Partial class complexity

**Decision**: Option A (same file) — Simplicity wins for this epic

---

## V12 DNA Compliance Strategy

### Lock-Free Verification
**Pre-execution**: `grep -r "lock(" src/V12_002.UI.*` → 0 matches  
**Post-execution**: Same command → 0 matches  
**Rationale**: All UI methods run on NT UI thread (single-threaded), no locks possible

### ASCII-Only Verification
**Pre-execution**: `deploy-sync.ps1` ASCII gate → PASS  
**Post-execution**: Same command → PASS  
**Rationale**: No new Print() calls, no dynamic string generation

### Zero-Allocation Verification
**Hot paths**: OnKeyDown, ProcessIpc_MatchSymbol  
**Strategy**:
- Pre-allocate all dictionaries at startup (InitKeyCommandRegistry, InitIpcCommandRegistry)
- Use TryGetValue (no allocation on hit)
- Avoid LINQ, avoid string.Format in hot paths
- Reuse existing StringBuilder in BuildConfigString (T-D)

---

## Testing Strategy

### Per-Ticket Test Matrix

#### T-C: AttachPanelHandlers
1. Launch NinjaTrader with V12 strategy
2. Verify all 60+ controls render without exceptions
3. Click each execution button (OR, RMA, RETEST, MOMO, FFMA, TREND, M)
4. Click each target button (T1-T5), verify dropdown menus appear
5. Click each action button (TRIM_50, BE, TRAIL, CANCEL, FLATTEN)
6. Click sync buttons (MKT_SYNC, SYNC_ALL)
7. Click config mode buttons, verify mode switches
8. Click target count buttons, verify visibility updates

#### T-D: OnSyncAllClick
1. Single chart: Click Sync All, verify CONFIG command sent
2. Two charts: Click Sync All, verify both receive CONFIG
3. Three charts: Click Sync All, verify all receive CONFIG
4. Switch modes (ORB → RMA → RETEST → MOMO → FFMA → TREND), click Sync All each time
5. Change target count (1 → 2 → 3 → 4 → 5), click Sync All each time
6. Modify field values, click Sync All, verify values propagate

#### T-F: UpdateContextualUI
1. Switch to ORB mode, verify OR LONG + OR SHORT buttons visible
2. Switch to RMA mode, verify RMA button visible
3. Switch to RETEST mode, verify Retest row visible
4. Switch to MOMO mode, verify MOMO button visible
5. Switch to FFMA mode, verify FFMA + FFMA Manual buttons visible, manual entry row collapsed
6. Switch to TREND mode, verify Trend row visible
7. Switch to MNL mode, verify M button visible
8. Verify direction combo: ORB shows "OR LONG/OR SHORT", others show "LONG/SHORT"

#### T-A: OnKeyDown
1. Press L → verify long entry
2. Press S → verify short entry
3. Press F → verify flatten
4. Press 1+M → verify T1 market
5. Press 1+O → verify T1 +1pt
6. Press 1+W → verify T1 +2pt
7. Press 1+K → verify T1 market price
8. Press 1+B → verify T1 breakeven
9. Press 1+C → verify T1 cancel
10. Repeat steps 4-9 for 2+letter (T2) and 3+letter (Runner)
11. Test NumPad1+M, NumPad2+M, NumPad3+M (verify identical to D1, D2, D3)

#### T-B: ProcessIpc_MatchSymbol
1. Send FLATTEN|ALL → verify all charts respond
2. Send SYNC_ALL|Global → verify all charts respond
3. Send CANCEL_ALL|Global → verify all charts respond
4. Send MOVE_TARGET|T1|1pt → verify charts with positions respond
5. Send LOCK_50|Global → verify charts with positions respond
6. Send SET_TARGETS|5 → verify all charts respond
7. Test symbol matching: Send command|MES → verify ES chart responds
8. Test symbol matching: Send command|MYM → verify YM chart responds
9. Test symbol matching: Send command|MGC → verify GC chart responds
10. Send SET_RMA_MODE|ON → verify all charts respond

---

## Rollback Strategy

### Per-Ticket Rollback
Each ticket is independently revertible:
- **T-C**: Single commit, single file
- **T-D**: Single commit, single file (bundled with T-F)
- **T-F**: Single commit, single file (bundled with T-D)
- **T-A**: Single commit, single file
- **T-B**: Single commit, single file

### Epic Rollback
If entire epic must be reverted:
1. Identify all commits with `[phase7-ui]` prefix
2. Revert in reverse order (T-B → T-A → T-F → T-D → T-C)
3. Run `deploy-sync.ps1` after each revert
4. F5 validate after each revert

---

## Open Questions (Resolved)

1. **T-C**: Helper methods private (not internal) — no external testability needed
2. **T-D**: Use StringBuilder (existing pattern, zero allocation)
3. **T-F**: Use switch dispatch (simpler than Dictionary for 7 cases)
4. **T-A**: Hybrid approach (basic keys in Dictionary, modifiers in switch)
5. **T-B**: static readonly HashSet for global commands (zero allocation)
6. **All**: Keep helpers in same file (simplicity wins)

---

[PLAN-GATE]