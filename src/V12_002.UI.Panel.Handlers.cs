// Build 1105: V12_001 panel port -- handlers rewired through PanelCommand
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Handler Structs

        private struct TargetConfig
        {
            public string T1Type,
                T2Type,
                T3Type,
                T4Type,
                T5Type;
            public string T1Val,
                T2Val,
                T3Val,
                T4Val,
                T5Val;
            public string Str,
                Max,
                Cit;
            public bool TrendRma,
                RetestRma;
            public int Count;
        }

        #endregion

        #region Panel Handlers

        private void AttachPanelHandlers()
        {
            InitKeyCommandRegistry();
            AttachMiscellaneousHandlers();
            AttachExecutionPanelHandlers();
            AttachTargetButtonHandlers();
            AttachActionButtonHandlers();
            AttachSyncButtonHandlers();
            AttachConfigModeHandlers();
            AttachTargetCountHandlers();
            AttachLiveTargetHandlers();
        }

        // [Phase7-UI T-A] Initialize command registry for basic hotkeys (CYC 3)
        // NOTE: Lambda closures allocate on heap. Acceptable as existing pattern.
        // If profiling shows impact, consider method references (e.g., [Key.L] = ExecuteLongHotkey).
        private void InitKeyCommandRegistry()
        {
            _keyCommands = new Dictionary<Key, Action>
            {
                // Basic hotkeys (no modifiers)
                [Key.L] = () =>
                {
                    double orStopDist = CalculateORStopDistance();
                    int orContracts = CalculatePositionSize(orStopDist);
                    Enqueue(ctx => ctx.ExecuteLong(orContracts));
                },
                [Key.S] = () =>
                {
                    double orStopDist = CalculateORStopDistance();
                    int orContracts = CalculatePositionSize(orStopDist);
                    Enqueue(ctx => ctx.ExecuteShort(orContracts));
                },
                // V12.1101E [PH5-COLLIDE-01]: Panic hotkey routes through lifecycle-safe flatten pipeline
                [Key.F] = () => FlattenAll(),
            };
        }

        private void AttachMiscellaneousHandlers()
        {
            if (floatingAnchor != null)
                floatingAnchor.Click += ToggleLayout_Click;

            if (fleetSelectButton != null)
                fleetSelectButton.Click += (_, e) =>
                {
                    if (fleetPopup != null)
                        fleetPopup.IsOpen = !fleetPopup.IsOpen;
                };

            if (submitButton != null)
                submitButton.Click += OnSubmitClick;
        }

        private void AttachExecutionPanelHandlers()
        {
            if (orLongButton != null)
                orLongButton.Click += (_, e) =>
                {
                    PanelCommand("OR_LONG");
                    ResetExecutionMode();
                    TriggerGlow(CyanAccent);
                };
            if (orShortButton != null)
                orShortButton.Click += (_, e) =>
                {
                    PanelCommand("OR_SHORT");
                    ResetExecutionMode();
                    TriggerGlow(PinkFg);
                };
            if (retestButton != null)
                retestButton.Click += OnRetestClick;
            if (retestRmaToggle != null)
                retestRmaToggle.Click += OnRetestRmaToggleClick;
            if (rmaButton != null)
                rmaButton.Click += OnRmaClick;
            if (momoButton != null)
                momoButton.Click += (_, e) =>
                {
                    PanelCommand("MODE_MOMO");
                    ResetExecutionMode();
                    TriggerGlow(GreenFg);
                };
            if (ffmaButton != null)
                ffmaButton.Click += (_, e) =>
                {
                    PanelCommand("MODE_FFMA");
                    ResetExecutionMode();
                    TriggerGlow(PinkFg);
                };
            if (ffmaManualButton != null)
                ffmaManualButton.Click += (_, e) =>
                {
                    PanelCommand("FFMA_MANUAL_MARKET");
                    ResetExecutionMode();
                    TriggerGlow(PinkFg);
                };
            if (mButton != null)
                mButton.Click += (_, e) =>
                {
                    PanelCommand("MODE_M");
                    TriggerGlow(OrangeFg);
                };
            if (trendButton != null)
                trendButton.Click += OnTrendClick;
            if (trendRmaToggle != null)
                trendRmaToggle.Click += OnTrendRmaToggleClick;
        }

        private void AttachTargetButtonHandlers()
        {
            if (t1Button != null)
                AttachTargetDropdown(t1Button, 1, GreenFg);
            if (t2Button != null)
                AttachTargetDropdown(t2Button, 2, YellowFg);
            if (t3Button != null)
                AttachTargetDropdown(t3Button, 3, OrangeFg);
            if (t4Button != null)
                AttachTargetDropdown(t4Button, 4, RedFg);
            if (t5Button != null)
                AttachTargetDropdown(t5Button, 5, PinkFg);
        }

        private void AttachActionButtonHandlers()
        {
            if (trim50Button != null)
                trim50Button.Click += (_, e) =>
                {
                    PanelCommand("TRIM_50");
                    TriggerGlow(OrangeFg);
                };
            if (beButton != null)
                beButton.Click += OnBeClick;
            if (trailButton != null)
                trailButton.Click += OnTrailClick;
            if (cancelButton != null)
                cancelButton.Click += (_, e) =>
                {
                    PanelCommand("CANCEL_ALL");
                    TriggerGlow(RedFg);
                };
            if (flattenButton != null)
                flattenButton.Click += (_, e) =>
                {
                    PanelCommand("FLATTEN_ONLY");
                    TriggerGlow(RedFg);
                };
        }

        private void AttachSyncButtonHandlers()
        {
            if (mktSyncButton != null)
                mktSyncButton.Click += (_, e) => PanelCommand("MKT_SYNC");
            if (syncAllButton != null)
                syncAllButton.Click += OnSyncAllClick;
        }

        private void AttachConfigModeHandlers()
        {
            if (modeOrbButton != null)
                modeOrbButton.Click += (_, e) => SelectConfigMode("ORB", modeOrbButton);
            if (modeRmaButton != null)
                modeRmaButton.Click += (_, e) => SelectConfigMode("RMA", modeRmaButton);
            if (modeRetestButton != null)
                modeRetestButton.Click += (_, e) => SelectConfigMode("RETEST", modeRetestButton);
            if (modeMomoButton != null)
                modeMomoButton.Click += (_, e) => SelectConfigMode("MOMO", modeMomoButton);
            if (modeFfmaButton != null)
                modeFfmaButton.Click += (_, e) => SelectConfigMode("FFMA", modeFfmaButton);
            if (modeTrendButton != null)
                modeTrendButton.Click += (_, e) => SelectConfigMode("TREND", modeTrendButton);
        }

        private void AttachTargetCountHandlers()
        {
            if (cnt1 != null)
                cnt1.Click += (_, e) => SelectTargetCount(1, cnt1);
            if (cnt2 != null)
                cnt2.Click += (_, e) => SelectTargetCount(2, cnt2);
            if (cnt3 != null)
                cnt3.Click += (_, e) => SelectTargetCount(3, cnt3);
            if (cnt4 != null)
                cnt4.Click += (_, e) => SelectTargetCount(4, cnt4);
            if (cnt5 != null)
                cnt5.Click += (_, e) => SelectTargetCount(5, cnt5);
        }

        private void DetachPanelHandlers()
        {
            if (floatingAnchor != null)
                floatingAnchor.Click -= ToggleLayout_Click;
        }

        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            if (_placementMode == PanelPlacement.Hijack && _chartTraderElement != null)
            {
                bool isV12Active = contentBody != null && contentBody.Visibility == Visibility.Visible;
                if (isV12Active)
                {
                    contentBody.Visibility = Visibility.Collapsed;
                    _chartTraderElement.Visibility = Visibility.Visible;
                    Print("V12 PANEL: Switched to Native View");
                }
                else
                {
                    contentBody.Visibility = Visibility.Visible;
                    _chartTraderElement.Visibility = Visibility.Collapsed;
                    Print("V12 PANEL: Switched to V12 View");
                }
            }
            else if (contentBody != null)
            {
                bool isVisible = contentBody.Visibility == Visibility.Visible;
                contentBody.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
                Print("V12 PANEL: Panel " + (isVisible ? "minimized" : "restored"));
            }
        }

        private void OnSubmitClick(object sender, RoutedEventArgs e)
        {
            string direction =
                (directionCombo != null && directionCombo.SelectedItem is ComboBoxItem directionItem)
                    ? (directionItem.Content as string ?? "OR LONG")
                    : "OR LONG";
            string price = priceInput != null ? priceInput.Text.Trim() : string.Empty;
            string mode = _panelLastSyncedMode;
            if (string.IsNullOrEmpty(mode))
                mode = GetCurrentConfigMode();
            if (string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase))
                mode = "ORB";

            string symbol =
                Instrument != null && Instrument.MasterInstrument != null
                    ? Instrument.MasterInstrument.Name
                    : string.Empty;
            string dir = direction.IndexOf("SHORT", StringComparison.OrdinalIgnoreCase) >= 0 ? "SHORT" : "LONG";
            string cmd;

            if (string.Equals(mode, "TREND", StringComparison.OrdinalIgnoreCase))
            {
                cmd = "TREND_MANUAL_LIMIT|" + symbol + "|" + dir + "|" + price;
            }
            else if (string.Equals(mode, "RETEST", StringComparison.OrdinalIgnoreCase))
            {
                cmd = "RETEST_MANUAL_LIMIT|" + symbol + "|" + dir + "|" + price;
            }
            else if (string.Equals(mode, "FFMA", StringComparison.OrdinalIgnoreCase))
            {
                cmd = "FFMA_MANUAL_LIMIT|" + symbol + "|" + dir + "|" + price;
            }
            else
            {
                cmd = dir == "LONG" ? "OR_LONG" : "OR_SHORT";
                cmd += "|" + symbol;
                if (!string.IsNullOrEmpty(price) && price != "0.00")
                    cmd += "|" + price;
            }

            PanelCommand(cmd);
            TriggerGlow(GreenFg);
        }

        private void OnRetestClick(object sender, RoutedEventArgs e)
        {
            retestCycleState = (retestCycleState + 1) % 3;
            switch (retestCycleState)
            {
                case 0:
                    if (retestButton != null)
                        retestButton.Content = "RETEST";
                    PanelCommand("EXEC_RETEST");
                    break;
                case 1:
                    if (retestButton != null)
                        retestButton.Content = "RET +";
                    PanelCommand("EXEC_RETEST_PLUS");
                    break;
                default:
                    if (retestButton != null)
                        retestButton.Content = "RET -";
                    PanelCommand("EXEC_RETEST_MINUS");
                    break;
            }

            if (isRMAModeActive)
            {
                isRMAModeActive = false;
                PanelCommand("SET_RMA_MODE|OFF");
                ClearClickTraderBorderIfInactive();
                UpdateRmaButtonVisual(false);
            }

            TriggerGlow(OrangeFg);
        }

        private void OnRetestRmaToggleClick(object sender, RoutedEventArgs e)
        {
            isRetestRmaToggle = !isRetestRmaToggle;
            if (retestRmaToggle != null)
                retestRmaToggle.Opacity = isRetestRmaToggle ? 1.0 : 0.5;
            PanelCommand(isRetestRmaToggle ? "MODE_RETEST_RMA" : "MODE_RETEST_STD");
        }

        private void OnRmaClick(object sender, RoutedEventArgs e)
        {
            isRMAModeActive = !isRMAModeActive;
            if (isRMAModeActive)
            {
                UpdateRmaButtonVisual(true);
                PanelCommand("SET_RMA_MODE|ON");
            }
            else
            {
                UpdateRmaButtonVisual(false);
                PanelCommand("SET_RMA_MODE|OFF");
                ClearClickTraderBorderIfInactive();
            }
            TriggerGlow(PurpleFg);
        }

        private void OnTrendClick(object sender, RoutedEventArgs e)
        {
            PanelCommand(isTrendRmaToggle ? "EXEC_TREND_RMA" : "EXEC_TREND");
            ResetExecutionMode();
            TriggerGlow(CyanAccent);
        }

        private void OnTrendRmaToggleClick(object sender, RoutedEventArgs e)
        {
            isTrendRmaToggle = !isTrendRmaToggle;
            if (trendRmaToggle != null)
                trendRmaToggle.Opacity = isTrendRmaToggle ? 1.0 : 0.5;
            PanelCommand(isTrendRmaToggle ? "MODE_TREND_RMA" : "MODE_TREND_STD");
        }

        private void OnBeClick(object sender, RoutedEventArgs e)
        {
            string ticks = beOffsetInput != null ? beOffsetInput.Text.Trim() : "2";
            if (string.IsNullOrEmpty(ticks))
                ticks = "2";
            PanelCommand("BE_CUSTOM|" + ticks);
            TriggerGlow(CyanFg);
        }

        private void OnTrailClick(object sender, RoutedEventArgs e)
        {
            string dist = trailDistInput != null ? trailDistInput.Text.Trim() : "1.0";
            if (string.IsNullOrEmpty(dist))
                dist = "1.0";
            PanelCommand("SET_TRAIL|" + dist);
            TriggerGlow(CyanFg);
        }

        private void OnSyncAllClick(object sender, RoutedEventArgs e)
        {
            string mode = ResolveEffectiveSyncMode();
            TargetConfig config = ExtractTargetConfiguration();
            string configString = BuildConfigString(mode, config);

            PanelCommand(configString);
            Print("V12 PANEL: SYNC ALL -> " + mode + " / count " + config.Count);
        }

        private string ResolveEffectiveSyncMode()
        {
            string mode = _panelLastSyncedMode;
            if (string.IsNullOrEmpty(mode))
                mode = GetCurrentConfigMode();
            if (string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase))
                mode = "ORB";
            return mode;
        }

        private TargetConfig ExtractTargetConfiguration()
        {
            var config = new TargetConfig();

            config.T1Type =
                (svT1Type != null && svT1Type.SelectedItem is ComboBoxItem t1Item)
                    ? (t1Item.Content as string ?? "ATR")
                    : "ATR";
            config.T2Type =
                (svT2Type != null && svT2Type.SelectedItem is ComboBoxItem t2Item)
                    ? (t2Item.Content as string ?? "ATR")
                    : "ATR";
            config.T3Type =
                (svT3Type != null && svT3Type.SelectedItem is ComboBoxItem t3Item)
                    ? (t3Item.Content as string ?? "ATR")
                    : "ATR";
            config.T4Type =
                (svT4Type != null && svT4Type.SelectedItem is ComboBoxItem t4Item)
                    ? (t4Item.Content as string ?? "ATR")
                    : "ATR";
            config.T5Type =
                (svT5Type != null && svT5Type.SelectedItem is ComboBoxItem t5Item)
                    ? (t5Item.Content as string ?? "ATR")
                    : "ATR";

            config.T1Val = svT1Val != null ? svT1Val.Text : "0";
            config.T2Val = svT2Val != null ? svT2Val.Text : "0";
            config.T3Val = svT3Val != null ? svT3Val.Text : "0";
            config.T4Val = svT4Val != null ? svT4Val.Text : "0";
            config.T5Val = svT5Val != null ? svT5Val.Text : "0";

            config.Str = strVal != null ? strVal.Text : "0";
            config.Cit = citVal != null ? citVal.Text : "0";

            string maxText = maxVal != null ? maxVal.Text : string.Empty;
            if (maxText == null)
                maxText = string.Empty;
            config.Max = maxText.Replace("$", string.Empty).Replace(" ", string.Empty);

            config.TrendRma = isTrendRmaMode;
            config.RetestRma = isRetestRmaMode;
            config.Count = Math.Max(
                1,
                Math.Min(5, _panelLastSyncedTargetCount > 0 ? _panelLastSyncedTargetCount : activeTargetCount)
            );

            return config;
        }

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

        private void AttachTargetDropdown(Button btn, int targetNum, SolidColorBrush glowColor)
        {
            ContextMenu menu = new ContextMenu
            {
                Background = BgSlate,
                BorderBrush = BorderSlate,
                Foreground = TextPrimary,
            };

            MenuItem closeItem = new MenuItem
            {
                Header = "Liquidate T" + targetNum + " at Market",
                Foreground = RedFg,
                Background = BgSlate,
                FontFamily = ConsolasFont,
                FontSize = 10,
            };
            closeItem.Click += (s, e) =>
            {
                PanelCommand("CLOSE_T" + targetNum);
                TriggerGlow(glowColor);
                Print("V12 PANEL: Liquidate T" + targetNum + " requested");
            };
            menu.Items.Add(closeItem);

            menu.Items.Add(new Separator { Background = BorderSlate });

            MenuItem move1PtItem = new MenuItem
            {
                Header = "Move T" + targetNum + " to +1pt",
                Foreground = GreenFg,
                Background = BgSlate,
                FontFamily = ConsolasFont,
                FontSize = 10,
            };
            move1PtItem.Click += (s, e) =>
            {
                PanelCommand("MOVE_TARGET|T" + targetNum + "|1pt");
                TriggerGlow(CyanAccent);
                Print("V12 PANEL: Move T" + targetNum + " to +1pt requested");
            };
            menu.Items.Add(move1PtItem);

            MenuItem move2PtItem = new MenuItem
            {
                Header = "Move T" + targetNum + " to +2pt",
                Foreground = YellowFg,
                Background = BgSlate,
                FontFamily = ConsolasFont,
                FontSize = 10,
            };
            move2PtItem.Click += (s, e) =>
            {
                PanelCommand("MOVE_TARGET|T" + targetNum + "|2pt");
                TriggerGlow(CyanAccent);
                Print("V12 PANEL: Move T" + targetNum + " to +2pt requested");
            };
            menu.Items.Add(move2PtItem);

            menu.PlacementTarget = btn;
            menu.Placement = PlacementMode.Bottom;
            btn.ContextMenu = menu;
            btn.Click += (s, e) =>
            {
                if (btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.Placement = PlacementMode.Bottom;
                    btn.ContextMenu.IsOpen = true;
                }
            };
        }

        private void ResetExecutionMode()
        {
            if (isRMAModeActive)
            {
                isRMAModeActive = false;
                PanelCommand("SET_RMA_MODE|OFF");
                ClearClickTraderBorderIfInactive();
            }
            UpdateRmaButtonVisual(false);

            retestCycleState = 0;
            if (retestButton != null)
                retestButton.Content = "RETEST";

            if (isTrendRmaToggle)
            {
                isTrendRmaToggle = false;
                PanelCommand("MODE_TREND_STD");
                if (trendRmaToggle != null)
                    trendRmaToggle.Opacity = 0.5;
            }

            if (isRetestRmaToggle)
            {
                isRetestRmaToggle = false;
                PanelCommand("MODE_RETEST_STD");
                if (retestRmaToggle != null)
                    retestRmaToggle.Opacity = 0.5;
            }

            Print("V12 PANEL: ResetExecutionMode -- one click = one order");
        }

        private void SelectConfigMode(string mode, Button clickedBtn)
        {
            string uiMode = string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase)
                ? "ORB"
                : mode.ToUpperInvariant();
            string transportMode = string.Equals(uiMode, "ORB", StringComparison.OrdinalIgnoreCase) ? "OR" : uiMode;

            foreach (
                Button btn in new[]
                {
                    modeOrbButton,
                    modeRmaButton,
                    modeRetestButton,
                    modeMomoButton,
                    modeFfmaButton,
                    modeTrendButton,
                }
            )
            {
                if (btn == null)
                    continue;
                btn.Background = BtnBg;
                btn.Foreground = TextMuted;
                btn.BorderBrush = BtnBorder;
            }

            if (clickedBtn != null)
            {
                clickedBtn.Background = CyanBg;
                clickedBtn.Foreground = CyanFg;
                clickedBtn.BorderBrush = CyanBorder;
            }

            _panelLastSyncedMode = uiMode;
            PanelCommand("SET_MODE|" + transportMode);
            UpdateContextualUI(uiMode);
        }

        private void SelectTargetCount(int count, Button clickedBtn)
        {
            _panelLastSyncedTargetCount = Math.Max(1, Math.Min(5, count));
            _panelChipClickTicks = DateTime.UtcNow.Ticks;
            PanelCommand("SET_TARGETS|" + _panelLastSyncedTargetCount);

            foreach (Button btn in new[] { cnt1, cnt2, cnt3, cnt4, cnt5 })
            {
                if (btn == null)
                    continue;
                btn.Background = BtnBg;
                btn.Foreground = TextPrimary;
                btn.BorderBrush = BtnBorder;
            }

            if (clickedBtn != null)
            {
                clickedBtn.Background = CyanBg;
                clickedBtn.Foreground = CyanFg;
                clickedBtn.BorderBrush = CyanBorder;
            }

            UpdateTargetVisibility(_panelLastSyncedTargetCount);
        }

        private void UpdateContextualUI(string mode)
        {
            string upperMode = string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase)
                ? "ORB"
                : (mode ?? "ORB").ToUpperInvariant();

            CollapseAllExecutionControls();
            ShowModeSpecificControls(upperMode);
            PopulateDirectionCombo(upperMode);
        }

        private void CollapseAllExecutionControls()
        {
            if (execRetestRow != null)
                execRetestRow.Visibility = Visibility.Collapsed;
            if (execTrendRow != null)
                execTrendRow.Visibility = Visibility.Collapsed;
            if (rmaButton != null)
                rmaButton.Visibility = Visibility.Collapsed;
            if (momoButton != null)
                momoButton.Visibility = Visibility.Collapsed;
            if (ffmaButton != null)
                ffmaButton.Visibility = Visibility.Collapsed;
            if (ffmaManualButton != null)
                ffmaManualButton.Visibility = Visibility.Collapsed;
            if (mButton != null)
                mButton.Visibility = Visibility.Collapsed;
            if (orLongButton != null)
                orLongButton.Visibility = Visibility.Collapsed;
            if (orShortButton != null)
                orShortButton.Visibility = Visibility.Collapsed;
            if (manualEntryRow != null)
                manualEntryRow.Visibility = Visibility.Visible;
        }

        private void ShowModeSpecificControls(string mode)
        {
            switch (mode)
            {
                case "ORB":
                    if (orLongButton != null)
                        orLongButton.Visibility = Visibility.Visible;
                    if (orShortButton != null)
                        orShortButton.Visibility = Visibility.Visible;
                    break;
                case "RMA":
                    if (rmaButton != null)
                        rmaButton.Visibility = Visibility.Visible;
                    break;
                case "RETEST":
                    if (execRetestRow != null)
                        execRetestRow.Visibility = Visibility.Visible;
                    break;
                case "MOMO":
                    if (momoButton != null)
                        momoButton.Visibility = Visibility.Visible;
                    break;
                case "FFMA":
                    if (ffmaButton != null)
                        ffmaButton.Visibility = Visibility.Visible;
                    if (ffmaManualButton != null)
                        ffmaManualButton.Visibility = Visibility.Visible;
                    if (manualEntryRow != null)
                        manualEntryRow.Visibility = Visibility.Collapsed;
                    break;
                case "TREND":
                    if (execTrendRow != null)
                        execTrendRow.Visibility = Visibility.Visible;
                    break;
                case "MNL":
                    if (mButton != null)
                        mButton.Visibility = Visibility.Visible;
                    break;
                default:
                    if (orLongButton != null)
                        orLongButton.Visibility = Visibility.Visible;
                    if (orShortButton != null)
                        orShortButton.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void PopulateDirectionCombo(string mode)
        {
            if (directionCombo == null)
                return;

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

        public void UpdateTargetVisibility(int count)
        {
            if (svT2Val != null)
                svT2Val.IsEnabled = count >= 2;
            if (svT2Type != null)
                svT2Type.IsEnabled = count >= 2;
            if (svT3Val != null)
                svT3Val.IsEnabled = count >= 3;
            if (svT3Type != null)
                svT3Type.IsEnabled = count >= 3;
            if (svT4Val != null)
                svT4Val.IsEnabled = count >= 4;
            if (svT4Type != null)
                svT4Type.IsEnabled = count >= 4;
            if (svT5Val != null)
                svT5Val.IsEnabled = count >= 5;
            if (svT5Type != null)
                svT5Type.IsEnabled = count >= 5;

            if (t2Row != null)
                t2Row.Visibility = count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            if (t3Row != null)
                t3Row.Visibility = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
            if (t4Row != null)
                t4Row.Visibility = count >= 4 ? Visibility.Visible : Visibility.Collapsed;
            if (t5Row != null)
                t5Row.Visibility = count >= 5 ? Visibility.Visible : Visibility.Collapsed;

            // Build 1107: In live mode, Section 1 buttons stay collapsed (live rows replace them).
            // Only manage button visibility when in config mode (flat).
            if (_currentLiveEntryName == null)
            {
                if (t1Button != null)
                    t1Button.Visibility = Visibility.Visible;
                if (t2Button != null)
                    t2Button.Visibility = count >= 2 ? Visibility.Visible : Visibility.Collapsed;
                if (t3Button != null)
                    t3Button.Visibility = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
                if (t4Button != null)
                    t4Button.Visibility = count >= 4 ? Visibility.Visible : Visibility.Collapsed;
                if (t5Button != null)
                    t5Button.Visibility = count >= 5 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateRmaButtonVisual(bool active)
        {
            if (rmaButton == null)
                return;
            if (active)
            {
                rmaButton.Background = PurpleFg;
                rmaButton.Foreground = Brushes.White;
                rmaButton.BorderBrush = PurpleFg;
                rmaButton.Content = "RMA [>]";
            }
            else
            {
                rmaButton.Background = BtnBg;
                rmaButton.Foreground = PurpleFg;
                rmaButton.BorderBrush = PurpleFg;
                rmaButton.Content = "RMA";
            }
        }

        // Build 1107: Attach Enter-key commit handlers to live target price TextBoxes.
        // Enter = commit price change. Escape = cancel (next sync restores current price).
        private void AttachLiveTargetHandlers()
        {
            AttachLiveTargetPriceHandler(liveT1Price, 1);
            AttachLiveTargetPriceHandler(liveT2Price, 2);
            AttachLiveTargetPriceHandler(liveT3Price, 3);
            AttachLiveTargetPriceHandler(liveT4Price, 4);
            AttachLiveTargetPriceHandler(liveT5Price, 5);
        }

        private void AttachLiveTargetPriceHandler(TextBox priceBox, int targetNum)
        {
            if (priceBox == null)
                return;
            priceBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitLiveTargetPrice(targetNum, priceBox.Text);
                    Keyboard.ClearFocus();
                }
                else if (e.Key == Key.Escape)
                {
                    // Don't commit -- just blur. Next 250ms sync restores current order price.
                    Keyboard.ClearFocus();
                }
            };
        }

        private void CommitLiveTargetPrice(int targetNum, string priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText) || priceText == "--")
                return;
            double testParse;
            if (!double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out testParse))
                return;
            if (testParse <= 0)
                return;
            PanelCommand(
                string.Format(CultureInfo.InvariantCulture, "SET_TARGET_PRICE|T{0}|{1}", targetNum, testParse)
            );
            TriggerGlow(CyanAccent);
            Print(string.Format("V12 PANEL: SET_TARGET_PRICE T{0} -> {1}", targetNum, priceText));
        }

        private void PanelCommand(string command)
        {
            string captured = command;
            long senderTicks = DateTime.UtcNow.Ticks;
            Enqueue(ctx =>
            {
                string[] parts = captured.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return;
                string action = parts[0].Trim().ToUpperInvariant();

                Print("V12 PANEL: Dispatch -> " + action);

                if (ctx.TryHandleModeCommand(action, parts))
                    return;
                if (ctx.TryHandleRiskCommand(action, parts))
                    return;
                if (ctx.TryHandleFleetCommand(action, parts, senderTicks))
                    return;
                if (ctx.TryHandleConfigCommand(action, parts))
                    return;
            });
        }

        #endregion
    }
}
