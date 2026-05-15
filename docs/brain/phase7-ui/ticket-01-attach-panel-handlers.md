# TICKET T-C: AttachPanelHandlers Extraction
**Epic**: phase7-ui  
**Priority**: HIGH  
**Ticket ID**: T-C  
**Agent**: Bob CLI (v12-engineer)  
**Estimated Sessions**: 1

---

## Mission Brief

Extract [`AttachPanelHandlers()`](../../../src/V12_002.UI.Panel.Handlers.cs:17) into 7 per-control-group helper methods to reduce cyclomatic complexity from 39 to ≤5.

**Critical Constraint**: This is UI initialization. Any regression blocks the entire strategy. Execute FIRST and F5-validate before proceeding to other UI tickets.

---

## Current State

**File**: `src/V12_002.UI.Panel.Handlers.cs:17-77`  
**Method**: `AttachPanelHandlers()`  
**CYC**: 39  
**LOC**: 61  
**Complexity Driver**: 60+ null-guarded handler attachments in single method

---

## Target State

**Residual CYC**: ≤5 (target: 2)  
**Helpers**: 7 per-control-group methods  
**Pattern**: Pure coordinator calling helper methods

### Residual Coordinator
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
    AttachLiveTargetHandlers();  // Existing method, keep as-is
}
```

---

## Extraction Plan

### Helper 1: AttachMiscellaneousHandlers()
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

### Helper 2: AttachExecutionPanelHandlers()
**Controls**: orLongButton, orShortButton, retestButton, retestRmaToggle, rmaButton, momoButton, ffmaButton, ffmaManualButton, mButton, trendButton, trendRmaToggle  
**LOC**: ~25  
**CYC**: ~11

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

### Helper 3: AttachTargetButtonHandlers()
**Controls**: t1Button, t2Button, t3Button, t4Button, t5Button  
**LOC**: ~15  
**CYC**: ~5

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

### Helper 4: AttachActionButtonHandlers()
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

### Helper 5: AttachSyncButtonHandlers()
**Controls**: mktSyncButton, syncAllButton  
**LOC**: ~8  
**CYC**: ~2

**Note**: Below 15-LOC floor, but acceptable for clarity (only 2 controls).

```csharp
private void AttachSyncButtonHandlers()
{
    if (mktSyncButton != null) mktSyncButton.Click += (s, e) =>
        PanelCommand("MKT_SYNC");
    if (syncAllButton != null) syncAllButton.Click += OnSyncAllClick;
}
```

### Helper 6: AttachConfigModeHandlers()
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

### Helper 7: AttachTargetCountHandlers()
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

---

## V12 DNA Compliance

### Lock-Free ✅
- All methods run on NT UI thread (single-threaded)
- No concurrency, no locks possible
- Verification: `grep -r "lock(" src/V12_002.UI.Panel.Handlers.cs` → 0 matches

### ASCII-Only ✅
- No Print() calls in extracted helpers
- No string literals (only method calls)

### Zero-Allocation ✅
- No new heap allocations
- All handlers are event wiring (no runtime allocation)

---

## Test Matrix

### Pre-Execution Baseline
1. Launch NinjaTrader with V12 strategy
2. Verify all 60+ controls render without exceptions
3. Document current behavior (screenshot or checklist)

### Post-Extraction Verification
Execute ALL tests below. Any failure is a BLOCKER.

#### Execution Panel (11 controls)
- [ ] OR LONG button: Click → entry command sent, cyan glow
- [ ] OR SHORT button: Click → entry command sent, pink glow
- [ ] Retest button: Click → handler fires
- [ ] Retest RMA toggle: Click → toggle state changes
- [ ] RMA button: Click → handler fires
- [ ] MOMO button: Click → mode switches, green glow
- [ ] FFMA button: Click → mode switches, pink glow
- [ ] FFMA Manual button: Click → manual entry, pink glow
- [ ] M button: Click → mode switches, orange glow
- [ ] Trend button: Click → handler fires
- [ ] Trend RMA toggle: Click → toggle state changes

#### Target Buttons (5 controls)
- [ ] T1 button: Click → dropdown menu appears
- [ ] T2 button: Click → dropdown menu appears
- [ ] T3 button: Click → dropdown menu appears
- [ ] T4 button: Click → dropdown menu appears
- [ ] T5 button: Click → dropdown menu appears

#### Action Buttons (5 controls)
- [ ] TRIM 50 button: Click → command sent, orange glow
- [ ] BE button: Click → handler fires
- [ ] Trail button: Click → handler fires
- [ ] Cancel button: Click → cancel command sent, red glow
- [ ] Flatten button: Click → flatten command sent, red glow

#### Sync Buttons (2 controls)
- [ ] MKT SYNC button: Click → sync command sent
- [ ] SYNC ALL button: Click → sync all command sent

#### Config Mode Buttons (6 controls)
- [ ] ORB mode button: Click → mode switches to ORB
- [ ] RMA mode button: Click → mode switches to RMA
- [ ] RETEST mode button: Click → mode switches to RETEST
- [ ] MOMO mode button: Click → mode switches to MOMO
- [ ] FFMA mode button: Click → mode switches to FFMA
- [ ] TREND mode button: Click → mode switches to TREND

#### Target Count Buttons (5 controls)
- [ ] Count 1 button: Click → target count = 1, visibility updates
- [ ] Count 2 button: Click → target count = 2, visibility updates
- [ ] Count 3 button: Click → target count = 3, visibility updates
- [ ] Count 4 button: Click → target count = 4, visibility updates
- [ ] Count 5 button: Click → target count = 5, visibility updates

#### Miscellaneous (3 controls)
- [ ] Floating anchor: Click → layout toggles
- [ ] Fleet select button: Click → fleet popup opens/closes
- [ ] Submit button: Click → submit handler fires

---

## Acceptance Criteria

### Quantitative
- [ ] Residual `AttachPanelHandlers()` CYC ≤ 5 (target: 2)
- [ ] All 7 helpers CYC ≤ 19
- [ ] All 7 helpers LOC ≥ 15 (except AttachSyncButtonHandlers at ~8 LOC, acceptable)
- [ ] `python scripts/complexity_audit.py` shows CYC reduction: 39 → ≤5

### Qualitative
- [ ] Zero behavioral change (all 60+ controls function identically)
- [ ] No null reference exceptions during panel initialization
- [ ] All test matrix items PASS

### Process
- [ ] `powershell -File .\deploy-sync.ps1` exits 0, ASCII gate PASS
- [ ] `grep -r "lock(" src/V12_002.UI.Panel.Handlers.cs` returns 0 matches
- [ ] F5 in NinjaTrader: BUILD_TAG banner appears
- [ ] Git commit: `[phase7-ui] T-C: AttachPanelHandlers extraction -- CYC 39->2 [BUILD_TAG]`

---

## Execution Notes for Bob

### Critical Path
1. **Read current implementation** (lines 17-77)
2. **Create 7 helper methods** (place immediately after `AttachPanelHandlers()`)
3. **Replace body of `AttachPanelHandlers()`** with 8 method calls
4. **Verify compilation** (no syntax errors)
5. **Run complexity audit** (verify CYC 39 → ≤5)
6. **Run deploy-sync** (verify ASCII gate PASS)
7. **F5 in NinjaTrader** (verify BUILD_TAG, test all controls)
8. **Commit** (with BUILD_TAG in message)

### Common Pitfalls
- **Don't reorder handler attachments** (preserve exact order)
- **Don't modify lambda bodies** (copy-paste exactly)
- **Don't skip null checks** (every control needs `if (control != null)`)
- **Don't forget `AttachLiveTargetHandlers()`** (existing method, keep as final call)

### Rollback Strategy
If any test fails:
1. `git reset --hard HEAD~1` (revert commit)
2. `powershell -File .\deploy-sync.ps1` (re-sync)
3. F5 in NinjaTrader (verify rollback successful)
4. Report failure to Director

---

## Dependencies

**Prerequisite**: None (T-C executes FIRST)  
**Blocks**: T-D, T-F (must F5-validate T-C before proceeding)

---

## References

- **Epic Scope**: [`00-scope.md`](00-scope.md)
- **Analysis**: [`01-analysis.md`](01-analysis.md) (T-C section)
- **Approach**: [`02-approach.md`](02-approach.md) (T-C section)
- **Validation**: [`03-validation.md`](03-validation.md)
- **Source File**: `src/V12_002.UI.Panel.Handlers.cs:17-77`

---

[TICKET-GATE]