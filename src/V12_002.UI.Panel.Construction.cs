// Build 1105: V12_001 panel port -- full construction + 3-path placement
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Fields

        // Layout architecture
        private Grid rootContainer;
        private Border contentBody;
        private Button floatingAnchor;
        private ScrollViewer panelScrollViewer;
        private StackPanel mainStack;

        // Placement tracking
        private enum PanelPlacement { None, Hijack, Injected, Fallback }
        private PanelPlacement _placementMode = PanelPlacement.None;
        private FrameworkElement _chartTraderElement;
        private Grid _placementGrid;

        // Section 0: Identity
        private Border hubStatusLed;
        private ComboBox leaderAccountCombo;
        private Button fleetSelectButton;
        private Popup fleetPopup;
        private StackPanel fleetCheckboxPanel;
        private List<string> selectedFleetAccounts = new List<string>();
        private ComboBox directionCombo;
        private TextBox priceInput;
        private Button submitButton;
        private Grid manualEntryRow;

        // Section 1: Execution
        private Button orLongButton, orShortButton;
        private Button retestButton, rmaButton;
        private Button momoButton, ffmaButton, ffmaManualButton, mButton;
        private Button trendButton, trendRmaToggle, retestRmaToggle;
        private Grid execRetestRow, execTrendRow;
        private int retestCycleState;
        private bool isTrendRmaToggle, isRetestRmaToggle;

        // Section 1: Targets + Management
        private Button t1Button, t2Button, t3Button, t4Button, t5Button;
        private Button trim50Button, beButton, trailButton;
        private TextBox trailDistInput, beOffsetInput;
        private Button flattenButton, cancelButton;
        private TextBlock lastPriceText;
        // Build 1107: Live target control rows (visible when in position, collapsed when flat)
        private Grid liveT1Row, liveT2Row, liveT3Row, liveT4Row, liveT5Row;
        private TextBox liveT1Price, liveT2Price, liveT3Price, liveT4Price, liveT5Price;
        private TextBlock liveT1Cts, liveT2Cts, liveT3Cts, liveT4Cts, liveT5Cts;
        private Grid liveStopRow;
        private TextBlock liveStopPrice;
        private string _currentLiveEntryName;

        // Section 1.5: Risk Manager / Compliance
        private TextBlock complianceSummaryText;
        private TextBlock complianceConsistencyText, compliancePayoutText, complianceDrawdownText;

        // Section 2: Telemetry
        private TextBlock or5Text, or15Text;
        private TextBlock ema9Text, ema15Text, ema30Text, ema65Text, ema200Text;
        private TextBlock atrText;
        private Button mktSyncButton;
        private Border trendIndicator;
        private TextBlock trendText;

        // Section 3: Config
        private Button modeOrbButton, modeRmaButton, modeRetestButton;
        private Button modeMomoButton, modeFfmaButton, modeTrendButton;
        private Button cnt1, cnt2, cnt3, cnt4, cnt5;
        private TextBox svT1Val, svT2Val, svT3Val, svT4Val, svT5Val;
        private ComboBox svT1Type, svT2Type, svT3Type, svT4Type, svT5Type, svStrType;
        private TextBox strVal, maxVal, citVal;
        private StackPanel t2Row, t3Row, t4Row, t5Row;
        private Button syncAllButton;

        private string _panelLastSyncedMode;
        private int _panelLastSyncedTargetCount;
        private int _panelAppliedConfigRevision;
        private long _panelChipClickTicks;
        private int _placementRetryCount;
        private System.Windows.Threading.DispatcherTimer _placementRetryTimer;

        #endregion

        #region Panel Construction

        private void CreatePanel()
        {
            if (rootContainer != null) return;
            UIStateSnapshot snapshot = GetUiSnapshot();

            rootContainer = new Grid
            {
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            contentBody = new Border
            {
                Background = BgDeep,
                BorderBrush = BorderSlate,
                BorderThickness = new Thickness(1, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true
            };

            panelScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            mainStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            mainStack.Children.Add(CreateSection0_Identity());
            mainStack.Children.Add(CreateSection1_Execution());
            mainStack.Children.Add(CreateSection1_5_RiskManager());
            mainStack.Children.Add(CreateSection2_Telemetry());
            mainStack.Children.Add(CreateSection3_Config());

            panelScrollViewer.Content = mainStack;
            contentBody.Child = panelScrollViewer;
            rootContainer.Children.Add(contentBody);

            floatingAnchor = new Button
            {
                Content = "[^]",
                Width = 30,
                Background = CyanBg,
                Foreground = CyanFg,
                BorderBrush = CyanBorder,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 2, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Toggle V12 / Native Chart Trader"
            };
            rootContainer.Children.Add(floatingAnchor);

            AttachPanelHandlers();
            UpdateContextualUI(_panelLastSyncedMode ?? "ORB");
            int initCount = _panelLastSyncedTargetCount > 0 ? _panelLastSyncedTargetCount : Math.Max(1, Math.Min(5, snapshot.TargetCount));
            UpdateTargetVisibility(initCount);
            SyncCountChipVisuals(initCount);
            UpdateRmaButtonVisual(snapshot.IsRmaModeActive);
            if (trendRmaToggle != null) trendRmaToggle.Opacity = snapshot.IsTrendRmaMode ? 1.0 : 0.5;
            if (retestRmaToggle != null) retestRmaToggle.Opacity = snapshot.IsRetestRmaMode ? 1.0 : 0.5;
            _panelAppliedConfigRevision = snapshot.ConfigRevision;
            UpdatePanelState();

            // Run visual tree diagnostic (once, on first creation)
            DumpVisualTree();

            // Attempt sidebar placement (with retry on failure)
            PlacePanel();
        }

        private void PlacePanel()
        {
            if (rootContainer == null || _placementMode != PanelPlacement.None) return;

            _chartTraderElement = FindChartTrader();

            if (_chartTraderElement != null && _chartTraderElement.Parent is Grid traderGrid)
            {
                int col = Grid.GetColumn(_chartTraderElement);
                int row = Grid.GetRow(_chartTraderElement);
                int rSpan = Grid.GetRowSpan(_chartTraderElement);
                int cSpan = Grid.GetColumnSpan(_chartTraderElement);

                Grid.SetColumn(rootContainer, col);
                Grid.SetRow(rootContainer, row);
                if (rSpan > 1) Grid.SetRowSpan(rootContainer, rSpan);
                if (cSpan > 1) Grid.SetColumnSpan(rootContainer, cSpan);

                traderGrid.Children.Add(rootContainer);
                _placementGrid = traderGrid;
                _chartTraderElement.Visibility = Visibility.Collapsed;
                contentBody.Visibility = Visibility.Visible;
                _placementMode = PanelPlacement.Hijack;
                Print("V12 PANEL: Hijacked Chart Trader slot (Col=" + col + ", Row=" + row + ")");
                return;
            }

            _chartTraderElement = null;
            _placementGrid = FindChartTabGrid(ChartControl);
            if (_placementGrid != null)
            {
                _placementGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(210) });
                int panelCol = _placementGrid.ColumnDefinitions.Count - 1;

                Grid.SetColumn(rootContainer, panelCol);
                Grid.SetRow(rootContainer, 0);
                if (_placementGrid.RowDefinitions.Count > 1)
                    Grid.SetRowSpan(rootContainer, _placementGrid.RowDefinitions.Count);

                rootContainer.HorizontalAlignment = HorizontalAlignment.Stretch;
                rootContainer.Width = double.NaN;

                _placementGrid.Children.Add(rootContainer);
                _placementMode = PanelPlacement.Injected;
                Print("V12 PANEL: Injected at column " + panelCol);
                return;
            }

            // Discovery failed -- retry up to 3 times before accepting fallback
            if (_placementRetryCount < 3)
            {
                _placementRetryCount++;
                Print("V12 PANEL: Discovery failed, scheduling retry "
                    + _placementRetryCount + "/3");

                if (_placementRetryTimer == null)
                {
                    _placementRetryTimer = new System.Windows.Threading.DispatcherTimer();
                    _placementRetryTimer.Interval = System.TimeSpan.FromMilliseconds(500);
                    _placementRetryTimer.Tick += (s, e) =>
                    {
                        _placementRetryTimer.Stop();
                        if (_isTerminating || rootContainer == null) return;
                        Print("V12 PANEL: Retry " + _placementRetryCount
                            + " -- re-running discovery");
                        DumpVisualTree();
                        PlacePanel();
                    };
                }
                _placementRetryTimer.Start();
                return;
            }

            // PATH 3: FALLBACK (after all retries exhausted)
            Print("V12 PANEL: All retries exhausted. Fallback to UserControlCollection.");
            UserControlCollection.Add(rootContainer);
            _placementMode = PanelPlacement.Fallback;
        }

        private void DestroyPanel()
        {
            if (rootContainer == null) return;

            // Build 1106-C: Restore chart keyboard input on panel destruction.
            // Prevents permanent input lock if a TextBox/ComboBox had focus when strategy was removed.
            // if (ChartControl != null)
            //     ChartControl.IsKeyboardInputEnabled = true;

            DetachPanelHandlers();

            try
            {
                if (_chartTraderElement != null)
                    _chartTraderElement.Visibility = Visibility.Visible;

                switch (_placementMode)
                {
                    case PanelPlacement.Fallback:
                        try { UserControlCollection.Remove(rootContainer); } catch { }
                        break;

                    case PanelPlacement.Injected:
                        if (_placementGrid != null)
                        {
                            if (_placementGrid.Children.Contains(rootContainer))
                                _placementGrid.Children.Remove(rootContainer);
                            if (_placementGrid.ColumnDefinitions.Count > 0)
                            {
                                var lastCol = _placementGrid.ColumnDefinitions[
                                    _placementGrid.ColumnDefinitions.Count - 1];
                                if (lastCol.Width.IsAbsolute && Math.Abs(lastCol.Width.Value - 210) < 1)
                                    _placementGrid.ColumnDefinitions.RemoveAt(
                                        _placementGrid.ColumnDefinitions.Count - 1);
                            }
                        }
                        break;

                    case PanelPlacement.Hijack:
                        if (_placementGrid != null && _placementGrid.Children.Contains(rootContainer))
                            _placementGrid.Children.Remove(rootContainer);
                        break;
                }
            }
            catch (Exception ex)
            {
                Print("V12 PANEL: Removal error -- " + ex.Message);
            }

            rootContainer = null;
            contentBody = null;
            floatingAnchor = null;
            panelScrollViewer = null;
            mainStack = null;
            _chartTraderElement = null;
            _placementGrid = null;
            _placementMode = PanelPlacement.None;
            if (_placementRetryTimer != null)
            {
                _placementRetryTimer.Stop();
                _placementRetryTimer = null;
            }
            _placementRetryCount = 0;

            hubStatusLed = null;
            leaderAccountCombo = null;
            fleetSelectButton = null;
            fleetPopup = null;
            fleetCheckboxPanel = null;
            selectedFleetAccounts = new List<string>();
            directionCombo = null;
            priceInput = null;
            submitButton = null;
            manualEntryRow = null;

            orLongButton = null; orShortButton = null;
            retestButton = null; rmaButton = null;
            momoButton = null; ffmaButton = null; ffmaManualButton = null; mButton = null;
            trendButton = null; trendRmaToggle = null; retestRmaToggle = null;
            execRetestRow = null; execTrendRow = null;

            t1Button = null; t2Button = null; t3Button = null; t4Button = null; t5Button = null;
            // Build 1107: Live target row cleanup
            liveT1Row = null; liveT2Row = null; liveT3Row = null; liveT4Row = null; liveT5Row = null;
            liveT1Price = null; liveT2Price = null; liveT3Price = null; liveT4Price = null; liveT5Price = null;
            liveT1Cts = null; liveT2Cts = null; liveT3Cts = null; liveT4Cts = null; liveT5Cts = null;
            liveStopRow = null; liveStopPrice = null;
            _currentLiveEntryName = null;
            trim50Button = null; beButton = null; trailButton = null;
            trailDistInput = null; beOffsetInput = null;
            flattenButton = null; cancelButton = null;
            lastPriceText = null;

            complianceSummaryText = null;
            complianceConsistencyText = null; compliancePayoutText = null; complianceDrawdownText = null;

            or5Text = null; or15Text = null;
            ema9Text = null; ema15Text = null; ema30Text = null; ema65Text = null; ema200Text = null;
            atrText = null; mktSyncButton = null;
            trendIndicator = null; trendText = null;

            modeOrbButton = null; modeRmaButton = null; modeRetestButton = null;
            modeMomoButton = null; modeFfmaButton = null; modeTrendButton = null;
            cnt1 = null; cnt2 = null; cnt3 = null; cnt4 = null; cnt5 = null;
            svT1Val = null; svT2Val = null; svT3Val = null; svT4Val = null; svT5Val = null;
            svT1Type = null; svT2Type = null; svT3Type = null; svT4Type = null; svT5Type = null; svStrType = null;
            strVal = null; maxVal = null; citVal = null;
            t2Row = null; t3Row = null; t4Row = null; t5Row = null;
            syncAllButton = null;

            _panelLastSyncedMode = null;
            _panelLastSyncedTargetCount = 0;
            _panelAppliedConfigRevision = 0;
        }

        private Border CreateSection0_Identity()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel
            {
                Margin = new Thickness(2, 2, 2, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            stack.Children.Add(CreateSectionHeader("SECTION 0: IDENTITY"));

            Grid row1 = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row1.HorizontalAlignment = HorizontalAlignment.Stretch;
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            hubStatusLed = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = TextMuted,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "Strategy Status"
            };
            Grid.SetColumn(hubStatusLed, 0);
            row1.Children.Add(hubStatusLed);

            string leaderDisplay = "LEADER: " + (Account != null ? Account.Name : "--");
            leaderAccountCombo = CreateCombo(0, leaderDisplay);
            leaderAccountCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            leaderAccountCombo.IsHitTestVisible = false;
            leaderAccountCombo.Focusable = false;
            Grid.SetColumn(leaderAccountCombo, 1);
            row1.Children.Add(leaderAccountCombo);
            stack.Children.Add(row1);

            Grid fleetRow = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            fleetRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            fleetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            fleetSelectButton = new Button
            {
                Content = "FLEET: Select Accounts [v]",
                Height = 22,
                FontSize = 10,
                FontFamily = ConsolasFont,
                Background = BgSlate,
                Foreground = TextPrimary,
                BorderBrush = BorderSlate,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(4, 0, 4, 0)
            };
            Grid.SetColumn(fleetSelectButton, 0);
            fleetRow.Children.Add(fleetSelectButton);

            fleetPopup = new Popup
            {
                StaysOpen = false,
                Placement = PlacementMode.Bottom,
                PlacementTarget = fleetSelectButton,
                AllowsTransparency = true
            };

            Border popupBorder = new Border
            {
                Background = BgDeep,
                BorderBrush = CyanAccent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4),
                MinWidth = 180
            };

            StackPanel popupStack = new StackPanel();

            CheckBox selectAllCheck = new CheckBox
            {
                Content = "Select ALL Accounts",
                Foreground = CyanAccent,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            selectAllCheck.Checked += (s, e) =>
            {
                if (fleetCheckboxPanel == null) return;
                foreach (var child in fleetCheckboxPanel.Children)
                {
                    CheckBox cb = child as CheckBox;
                    if (cb != null)
                        cb.IsChecked = true;
                }
            };
            selectAllCheck.Unchecked += (s, e) =>
            {
                if (fleetCheckboxPanel == null) return;
                foreach (var child in fleetCheckboxPanel.Children)
                {
                    CheckBox cb = child as CheckBox;
                    if (cb != null)
                        cb.IsChecked = false;
                }
            };
            popupStack.Children.Add(selectAllCheck);
            popupStack.Children.Add(new Border { Height = 1, Background = BorderSlate, Margin = new Thickness(0, 2, 0, 4) });

            fleetCheckboxPanel = new StackPanel();
            selectedFleetAccounts.Clear();

            var fleetAccounts = GetFleetAccountsSnapshot();
            foreach (var acct in fleetAccounts)
            {
                bool isActive = false;
                activeFleetAccounts.TryGetValue(acct.Name, out isActive);
                if (isActive && !selectedFleetAccounts.Contains(acct.Name))
                    selectedFleetAccounts.Add(acct.Name);

                CheckBox cb = new CheckBox
                {
                    Content = acct.Name,
                    Tag = acct.Name,
                    Foreground = TextPrimary,
                    FontSize = 10,
                    FontFamily = ConsolasFont,
                    Margin = new Thickness(0, 1, 0, 1),
                    IsChecked = isActive
                };
                cb.Checked += (s, e) =>
                {
                    string accountName = cb.Tag as string;
                    if (string.IsNullOrEmpty(accountName)) return;
                    if (!selectedFleetAccounts.Contains(accountName))
                        selectedFleetAccounts.Add(accountName);
                    UpdateFleetButtonText();
                    PanelCommand("TOGGLE_ACCOUNT|" + accountName + "|1");
                };
                cb.Unchecked += (s, e) =>
                {
                    string accountName = cb.Tag as string;
                    if (string.IsNullOrEmpty(accountName)) return;
                    selectedFleetAccounts.Remove(accountName);
                    UpdateFleetButtonText();
                    PanelCommand("TOGGLE_ACCOUNT|" + accountName + "|0");
                };
                fleetCheckboxPanel.Children.Add(cb);
            }
            popupStack.Children.Add(fleetCheckboxPanel);
            popupBorder.Child = popupStack;
            fleetPopup.Child = popupBorder;
            UpdateFleetButtonText();

            stack.Children.Add(fleetRow);

            Grid row3 = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            manualEntryRow = row3;
            row3.HorizontalAlignment = HorizontalAlignment.Stretch;
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            directionCombo = CreateCombo(0, "OR LONG", "OR SHORT");
            directionCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(directionCombo, 0);
            row3.Children.Add(directionCombo);

            string priceDefault = lastKnownPrice > 0
                ? Instrument.MasterInstrument.FormatPrice(lastKnownPrice)
                : "0.00";
            priceInput = CreateTextBox(priceDefault, 0);
            priceInput.Margin = new Thickness(4, 0, 4, 0);
            Grid.SetColumn(priceInput, 1);
            row3.Children.Add(priceInput);

            submitButton = CreateButton("SUBMIT", 60, GreenBg, GreenFg, GreenBorder);
            submitButton.Height = 22;
            Grid.SetColumn(submitButton, 2);
            row3.Children.Add(submitButton);

            stack.Children.Add(row3);

            section.Child = stack;
            return section;
        }

        private Border CreateSection1_Execution()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel
            {
                Margin = new Thickness(2, 2, 2, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            stack.Children.Add(CreateSectionHeader("SECTION 1: EXECUTION"));

            Grid mainGrid = new Grid { Margin = new Thickness(2, 1, 2, 1) };
            mainGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel leftCol = new StackPanel
            {
                Margin = new Thickness(0, 0, 1, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            orLongButton = CreateDashedButton("OR L", CyanAccent);
            leftCol.Children.Add(orLongButton);

            orShortButton = CreateDashedButton("OR S", PinkFg);
            orShortButton.Margin = new Thickness(0, 2, 0, 0);
            leftCol.Children.Add(orShortButton);

            execRetestRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            execRetestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            execRetestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            retestButton = CreateButton("RETEST", 0, OrangeBg, OrangeFg, OrangeBorder);
            Grid.SetColumn(retestButton, 0);
            execRetestRow.Children.Add(retestButton);

            retestRmaToggle = CreateButton("R", 36, BtnBg, PurpleFg, PurpleFg);
            retestRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            retestRmaToggle.Opacity = 0.5;
            Grid.SetColumn(retestRmaToggle, 1);
            execRetestRow.Children.Add(retestRmaToggle);
            leftCol.Children.Add(execRetestRow);

            rmaButton = CreateButton("RMA", 0, BtnBg, PurpleFg, PurpleFg);
            rmaButton.Margin = new Thickness(0, 2, 0, 0);
            leftCol.Children.Add(rmaButton);

            momoButton = CreateButton("MOMO", 0, GreenBg, GreenFg, GreenBorder);
            momoButton.Margin = new Thickness(0, 2, 0, 0);
            leftCol.Children.Add(momoButton);

            ffmaButton = CreateButton("A.FFMA", 0, PinkBg, PinkFg, PinkBorder);
            ffmaButton.Margin = new Thickness(0, 2, 0, 0);
            leftCol.Children.Add(ffmaButton);

            ffmaManualButton = CreateButton("M.FFMA", 0, PinkBg, PinkFg, PinkBorder);
            ffmaManualButton.Margin = new Thickness(0, 2, 0, 0);
            leftCol.Children.Add(ffmaManualButton);

            mButton = CreateButton("MNL", 0, OrangeBg, OrangeFg, OrangeBorder);
            mButton.Margin = new Thickness(0, 2, 0, 0);
            leftCol.Children.Add(mButton);

            execTrendRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            execTrendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            execTrendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            trendButton = CreateButton("TREND", 0, BtnBg, TextPrimary, BtnBorder);
            Grid.SetColumn(trendButton, 0);
            execTrendRow.Children.Add(trendButton);

            trendRmaToggle = CreateButton("R", 36, BtnBg, PurpleFg, PurpleFg);
            trendRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            trendRmaToggle.Opacity = 0.5;
            Grid.SetColumn(trendRmaToggle, 1);
            execTrendRow.Children.Add(trendRmaToggle);
            leftCol.Children.Add(execTrendRow);

            Grid.SetColumn(leftCol, 0);
            mainGrid.Children.Add(leftCol);

            StackPanel rightCol = new StackPanel
            {
                Margin = new Thickness(1, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            t1Button = CreateButton("T1", 0, GreenBg, GreenFg, GreenBorder);
            rightCol.Children.Add(t1Button);
            liveT1Row = CreateLiveTargetRow(1, out liveT1Price, out liveT1Cts);
            rightCol.Children.Add(liveT1Row);

            t2Button = CreateButton("T2", 0, YellowBg, YellowFg, YellowBorder);
            t2Button.Margin = new Thickness(0, 2, 0, 0);
            rightCol.Children.Add(t2Button);
            liveT2Row = CreateLiveTargetRow(2, out liveT2Price, out liveT2Cts);
            rightCol.Children.Add(liveT2Row);

            t3Button = CreateButton("T3", 0, OrangeBg, OrangeFg, OrangeBorder);
            t3Button.Margin = new Thickness(0, 2, 0, 0);
            rightCol.Children.Add(t3Button);
            liveT3Row = CreateLiveTargetRow(3, out liveT3Price, out liveT3Cts);
            rightCol.Children.Add(liveT3Row);

            t4Button = CreateButton("T4", 0, RedBg, RedFg, RedBorder);
            t4Button.Margin = new Thickness(0, 2, 0, 0);
            rightCol.Children.Add(t4Button);
            liveT4Row = CreateLiveTargetRow(4, out liveT4Price, out liveT4Cts);
            rightCol.Children.Add(liveT4Row);

            t5Button = CreateButton("T5", 0, PinkBg, PinkFg, PinkBorder);
            t5Button.Margin = new Thickness(0, 2, 0, 0);
            rightCol.Children.Add(t5Button);
            liveT5Row = CreateLiveTargetRow(5, out liveT5Price, out liveT5Cts);
            rightCol.Children.Add(liveT5Row);

            // Build 1107: Live stop row (read-only price display)
            liveStopRow = new Grid { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 2, 0, 0), Height = 22 };
            liveStopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            liveStopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock stopLabel = new TextBlock
            {
                Text = "STOP",
                Foreground = RedFg,
                FontFamily = ConsolasFont,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(stopLabel, 0);
            liveStopRow.Children.Add(stopLabel);
            liveStopPrice = new TextBlock
            {
                Text = "--",
                Foreground = RedFg,
                FontFamily = ConsolasFont,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(liveStopPrice, 1);
            liveStopRow.Children.Add(liveStopPrice);
            rightCol.Children.Add(liveStopRow);

            trim50Button = CreateButton("50%", 0, OrangeBg, OrangeFg, OrangeBorder);
            trim50Button.Margin = new Thickness(0, 2, 0, 0);
            rightCol.Children.Add(trim50Button);

            Grid beRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            beRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            beRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            beOffsetInput = CreateTextBox("2", 0);
            beOffsetInput.Height = 22;
            beOffsetInput.FontSize = 10;
            beOffsetInput.ToolTip = "BE offset in ticks (e.g., 2 = move stop +2 ticks above entry)";
            Grid.SetColumn(beOffsetInput, 0);
            beRow.Children.Add(beOffsetInput);

            beButton = CreateButton("BE", 0, CyanBg, CyanFg, CyanBorder);
            beButton.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetColumn(beButton, 1);
            beRow.Children.Add(beButton);
            rightCol.Children.Add(beRow);

            Grid trailRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            trailDistInput = CreateTextBox("1.0", 0);
            trailDistInput.Height = 22;
            trailDistInput.FontSize = 10;
            trailDistInput.ToolTip = "Trail distance in points";
            Grid.SetColumn(trailDistInput, 0);
            trailRow.Children.Add(trailDistInput);

            trailButton = CreateButton("TRAIL", 0, BtnBg, TextPrimary, BtnBorder);
            trailButton.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetColumn(trailButton, 1);
            trailRow.Children.Add(trailButton);
            rightCol.Children.Add(trailRow);

            Grid cancelFlattenRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            cancelFlattenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cancelFlattenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            cancelButton = CreateButton("CANCEL", 0, RedBg, RedFg, RedBorder);
            cancelButton.FontWeight = FontWeights.Bold;
            Grid.SetColumn(cancelButton, 0);
            cancelFlattenRow.Children.Add(cancelButton);

            flattenButton = CreateButton("FLATTEN", 0, RedBg, RedFg, RedBorder);
            flattenButton.FontWeight = FontWeights.Bold;
            flattenButton.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetColumn(flattenButton, 1);
            cancelFlattenRow.Children.Add(flattenButton);
            rightCol.Children.Add(cancelFlattenRow);

            Grid.SetColumn(rightCol, 1);
            mainGrid.Children.Add(rightCol);

            stack.Children.Add(mainGrid);

            lastPriceText = new TextBlock
            {
                Text = "--",
                Foreground = TextPrimary,
                FontSize = 18,
                FontFamily = ConsolasFont,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 3, 0, 0)
            };
            stack.Children.Add(lastPriceText);

            section.Child = stack;
            return section;
        }

        private Border CreateSection1_5_RiskManager()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel
            {
                Margin = new Thickness(2, 2, 2, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            stack.Children.Add(CreateSectionHeader("SECTION 1.5: RISK"));

            complianceSummaryText = new TextBlock
            {
                Text = "ACCT: -- / TRADES: 0 / DAYS: 0 / MAXDD: $0",
                Foreground = TextMuted,
                FontFamily = ConsolasFont,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 1, 0, 1)
            };
            stack.Children.Add(complianceSummaryText);

            UniformGrid row = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 0) };

            complianceConsistencyText = new TextBlock
            {
                Text = "CONSISTENCY: --",
                Foreground = TextMuted,
                FontFamily = ConsolasFont,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            compliancePayoutText = new TextBlock
            {
                Text = "PAYOUT: --",
                Foreground = TextMuted,
                FontFamily = ConsolasFont,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            complianceDrawdownText = new TextBlock
            {
                Text = "DD BUFFER: --",
                Foreground = TextMuted,
                FontFamily = ConsolasFont,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            row.Children.Add(complianceConsistencyText);
            row.Children.Add(compliancePayoutText);
            row.Children.Add(complianceDrawdownText);
            stack.Children.Add(row);

            section.Child = stack;
            return section;
        }

        private Border CreateSection2_Telemetry()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel
            {
                Margin = new Thickness(2, 2, 2, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            stack.Children.Add(CreateSectionHeader("SECTION 2: TELEMETRY"));

            or5Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = ConsolasFont,
                Margin = new Thickness(0, 3, 0, 1)
            };
            or5Text.Inlines.Add(new System.Windows.Documents.Run("OR5: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("--") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("--") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" (R: --)") { Foreground = TextMuted });
            stack.Children.Add(or5Text);

            or15Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = ConsolasFont,
                Margin = new Thickness(0, 0, 0, 2)
            };
            or15Text.Inlines.Add(new System.Windows.Documents.Run("OR15: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("--") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("--") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" (R: --)") { Foreground = TextMuted });
            stack.Children.Add(or15Text);

            StackPanel emaRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
            ema9Text = CreateEmaLabel("9:", "--", TextPrimary);
            ema15Text = CreateEmaLabel("15:", "--", TextPrimary);
            ema30Text = CreateEmaLabel("30:", "--", GreenFg);
            emaRow1.Children.Add(ema9Text);
            emaRow1.Children.Add(ema15Text);
            emaRow1.Children.Add(ema30Text);
            stack.Children.Add(emaRow1);

            StackPanel emaRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            ema65Text = CreateEmaLabel("65:", "--", TextPrimary);
            ema200Text = CreateEmaLabel("200:", "--", PurpleFg);
            atrText = new TextBlock
            {
                Text = "ATR: --",
                Foreground = TextMuted,
                FontSize = 10,
                FontFamily = ConsolasFont,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            emaRow2.Children.Add(ema65Text);
            emaRow2.Children.Add(ema200Text);
            emaRow2.Children.Add(atrText);
            stack.Children.Add(emaRow2);

            Grid syncRow = new Grid();
            syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            mktSyncButton = CreateButton("MKT SYNC", 70, CyanBg, CyanFg, CyanBorder);
            mktSyncButton.Height = 24;
            mktSyncButton.FontSize = 9;
            Grid.SetColumn(mktSyncButton, 0);
            syncRow.Children.Add(mktSyncButton);

            trendIndicator = new Border
            {
                Background = GreenBg,
                BorderBrush = GreenBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(8, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Height = 24
            };
            trendText = new TextBlock
            {
                Text = "BULLISH",
                Foreground = GreenFg,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                FontFamily = ConsolasFont,
                VerticalAlignment = VerticalAlignment.Center
            };
            trendIndicator.Child = trendText;
            Grid.SetColumn(trendIndicator, 1);
            syncRow.Children.Add(trendIndicator);

            stack.Children.Add(syncRow);

            section.Child = stack;
            return section;
        }

        private Border CreateSection3_Config()
        {
            UIStateSnapshot snapshot = GetUiSnapshot();
            UIConfigSnapshot config = snapshot.Config ?? new UIConfigSnapshot();
            Border section = CreateSectionBorder();
            section.BorderThickness = new Thickness(0);
            StackPanel stack = new StackPanel
            {
                Margin = new Thickness(2, 2, 2, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            stack.Children.Add(CreateSectionHeader("SECTION 3: CONFIG"));

            Grid modeCountGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            modeCountGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string currentMode = string.IsNullOrEmpty(snapshot.Mode) ? "ORB" : snapshot.Mode;
            int currentCount = Math.Max(1, Math.Min(5, snapshot.TargetCount));

            StackPanel modeColumn = new StackPanel { Margin = new Thickness(0, 0, 1, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            modeOrbButton = CreateModeChip("ORB", string.Equals(currentMode, "ORB", StringComparison.OrdinalIgnoreCase));
            modeRmaButton = CreateModeChip("RMA", string.Equals(currentMode, "RMA", StringComparison.OrdinalIgnoreCase));
            modeRetestButton = CreateModeChip("RETEST", string.Equals(currentMode, "RETEST", StringComparison.OrdinalIgnoreCase));
            modeMomoButton = CreateModeChip("MOMO", string.Equals(currentMode, "MOMO", StringComparison.OrdinalIgnoreCase));
            modeFfmaButton = CreateModeChip("FFMA", string.Equals(currentMode, "FFMA", StringComparison.OrdinalIgnoreCase));
            modeTrendButton = CreateModeChip("TREND", string.Equals(currentMode, "TREND", StringComparison.OrdinalIgnoreCase));
            modeColumn.Children.Add(modeOrbButton);
            modeColumn.Children.Add(modeRmaButton);
            modeColumn.Children.Add(modeRetestButton);
            modeColumn.Children.Add(modeMomoButton);
            modeColumn.Children.Add(modeFfmaButton);
            modeColumn.Children.Add(modeTrendButton);
            Grid.SetColumn(modeColumn, 0);
            modeCountGrid.Children.Add(modeColumn);

            StackPanel countColumn = new StackPanel { Margin = new Thickness(1, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            cnt1 = CreateCountChip("1");
            cnt2 = CreateCountChip("2");
            cnt3 = CreateCountChip("3");
            cnt4 = CreateCountChip("4");
            cnt5 = CreateCountChip("5");
            countColumn.Children.Add(cnt1);
            countColumn.Children.Add(cnt2);
            countColumn.Children.Add(cnt3);
            countColumn.Children.Add(cnt4);
            countColumn.Children.Add(cnt5);
            Grid.SetColumn(countColumn, 1);
            modeCountGrid.Children.Add(countColumn);
            stack.Children.Add(modeCountGrid);

            StackPanel svRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            svRow1.Children.Add(new TextBlock { Text = "SV:", Foreground = TextPrimary, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });

            svRow1.Children.Add(new TextBlock { Text = "T1", Foreground = GreenFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT1Val = CreateTextBox(FormatPanelDouble(config.Target1Value), 30); svT1Val.Height = 20; svT1Val.FontSize = 9;
            svRow1.Children.Add(svT1Val);
            svT1Type = CreateCombo(42, "ATR", "Ticks", "Pts", "Runner"); svT1Type.Height = 20; svT1Type.FontSize = 8; svT1Type.Margin = new Thickness(2, 0, 6, 0);
            SetComboSelection(svT1Type, GetPanelTargetModeText(config.Target1Type));
            svRow1.Children.Add(svT1Type);

            svRow1.Children.Add(new TextBlock { Text = "T2", Foreground = YellowFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT2Val = CreateTextBox(FormatPanelDouble(config.Target2Value), 30); svT2Val.Height = 20; svT2Val.FontSize = 9;
            svRow1.Children.Add(svT2Val);
            svT2Type = CreateCombo(42, "ATR", "Ticks", "Pts", "Runner"); svT2Type.Height = 20; svT2Type.FontSize = 8;
            SetComboSelection(svT2Type, GetPanelTargetModeText(config.Target2Type));
            svRow1.Children.Add(svT2Type);
            stack.Children.Add(svRow1);

            t3Row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            t3Row.Children.Add(new TextBlock { Text = "       T3", Foreground = OrangeFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT3Val = CreateTextBox(FormatPanelDouble(config.Target3Value), 30); svT3Val.Height = 20; svT3Val.FontSize = 9;
            t3Row.Children.Add(svT3Val);
            svT3Type = CreateCombo(42, "ATR", "Ticks", "Pts", "Runner"); svT3Type.Height = 20; svT3Type.FontSize = 8; svT3Type.Margin = new Thickness(2, 0, 0, 0);
            SetComboSelection(svT3Type, GetPanelTargetModeText(config.Target3Type));
            t3Row.Children.Add(svT3Type);
            stack.Children.Add(t3Row);

            t4Row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2), Visibility = Visibility.Collapsed };
            t4Row.Children.Add(new TextBlock { Text = "       T4", Foreground = RedFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT4Val = CreateTextBox(FormatPanelDouble(config.Target4Value), 30); svT4Val.Height = 20; svT4Val.FontSize = 9;
            t4Row.Children.Add(svT4Val);
            svT4Type = CreateCombo(42, "ATR", "Ticks", "Pts", "Runner"); svT4Type.Height = 20; svT4Type.FontSize = 8; svT4Type.Margin = new Thickness(2, 0, 0, 0);
            SetComboSelection(svT4Type, GetPanelTargetModeText(config.Target4Type));
            t4Row.Children.Add(svT4Type);
            stack.Children.Add(t4Row);

            t5Row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3), Visibility = Visibility.Collapsed };
            t5Row.Children.Add(new TextBlock { Text = "       T5", Foreground = PinkFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT5Val = CreateTextBox(FormatPanelDouble(config.Target5Value), 30); svT5Val.Height = 20; svT5Val.FontSize = 9;
            t5Row.Children.Add(svT5Val);
            svT5Type = CreateCombo(42, "ATR", "Ticks", "Pts", "Runner"); svT5Type.Height = 20; svT5Type.FontSize = 8; svT5Type.Margin = new Thickness(2, 0, 0, 0);
            SetComboSelection(svT5Type, GetPanelTargetModeText(config.Target5Type));
            t5Row.Children.Add(svT5Type);
            stack.Children.Add(t5Row);

            Grid riskRow = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            riskRow.HorizontalAlignment = HorizontalAlignment.Left;
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            riskRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock strLabel = new TextBlock { Text = "STR:", Foreground = OrangeFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
            Grid.SetColumn(strLabel, 0);
            riskRow.Children.Add(strLabel);

            strVal = CreateTextBox(FormatPanelDouble(config.StopValue), 33); strVal.Height = 20; strVal.FontSize = 9; strVal.Foreground = OrangeFg;
            svStrType = CreateCombo(40, "ATR", "Ticks", "Pts", "OR");
            svStrType.Height = 20; svStrType.FontSize = 8;
            if (string.Equals(currentMode, "ORB", StringComparison.OrdinalIgnoreCase))
                SetComboSelection(svStrType, "OR");
            else
                SetComboSelection(svStrType, "ATR");

            StackPanel strStack = new StackPanel { Orientation = Orientation.Horizontal };
            strStack.Children.Add(strVal);
            svStrType.Margin = new Thickness(2, 0, 0, 0);
            strStack.Children.Add(svStrType);
            Grid.SetColumn(strStack, 1);
            riskRow.Children.Add(strStack);

            TextBlock maxLabel = new TextBlock { Text = "MAX:", Foreground = OrangeFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) };
            Grid.SetColumn(maxLabel, 2);
            riskRow.Children.Add(maxLabel);

            maxVal = CreateTextBox(FormatPanelDouble(config.MaxRiskValue), 55); maxVal.Height = 20; maxVal.FontSize = 9; maxVal.Foreground = OrangeFg;
            Grid.SetColumn(maxVal, 3);
            riskRow.Children.Add(maxVal);
            stack.Children.Add(riskRow);

            Grid citRow = new Grid { Margin = new Thickness(0, 2, 0, 3) };
            citRow.HorizontalAlignment = HorizontalAlignment.Left;
            citRow.Height = 24;
            citRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            citRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock citLabel = new TextBlock { Text = "CHASE:", Foreground = OrangeFg, FontSize = 9, FontFamily = ConsolasFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            Grid.SetColumn(citLabel, 0);
            citRow.Children.Add(citLabel);

            citVal = CreateTextBox(string.IsNullOrEmpty(config.ChaseIfTouchPoints) ? "0" : config.ChaseIfTouchPoints, 55); citVal.Height = 20; citVal.FontSize = 10; citVal.Foreground = OrangeFg; citVal.FontWeight = FontWeights.Bold;
            citVal.ToolTip = "Chase If Touch: Points offset (0 = disabled)";
            Grid.SetColumn(citVal, 1);
            citRow.Children.Add(citVal);
            stack.Children.Add(citRow);

            syncAllButton = CreateButton("SYNC ALL", 0, CyanBg, CyanFg, CyanBorder);
            syncAllButton.Height = 24;
            syncAllButton.FontWeight = FontWeights.Bold;
            stack.Children.Add(syncAllButton);

            section.Child = stack;

            _panelLastSyncedMode = currentMode;
            _panelLastSyncedTargetCount = currentCount;
            _panelAppliedConfigRevision = snapshot.ConfigRevision;
            return section;
        }

        private Border CreateSectionBorder()
        {
            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 20, 25)),
                BorderThickness = new Thickness(0, 0, 0, 0.5),
                Background = BgDeep,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = CyanAccent,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                FontFamily = ConsolasFont,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private void SetComboSelection(ComboBox combo, string desiredText)
        {
            if (combo == null || string.IsNullOrEmpty(desiredText)) return;
            foreach (var item in combo.Items)
            {
                ComboBoxItem cbItem = item as ComboBoxItem;
                if (cbItem != null && string.Equals(cbItem.Content as string, desiredText, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = cbItem;
                    return;
                }
            }
        }

        private string GetPanelTargetModeText(TargetMode mode)
        {
            switch (mode)
            {
                case TargetMode.ATR:
                    return "ATR";
                case TargetMode.Ticks:
                    return "Ticks";
                case TargetMode.Points:
                    return "Pts";
                case TargetMode.Runner:
                    return "Runner";
                default:
                    return "ATR";
            }
        }

        private string FormatPanelDouble(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void UpdateFleetButtonText()
        {
            if (fleetSelectButton == null) return;

            int count = selectedFleetAccounts.Count;
            int total = fleetCheckboxPanel != null ? fleetCheckboxPanel.Children.Count : 0;

            if (count <= 0)
                fleetSelectButton.Content = "FLEET: None Selected [v]";
            else if (count >= total && total > 0)
                fleetSelectButton.Content = "FLEET: ALL Accounts [v]";
            else
                fleetSelectButton.Content = "FLEET: " + count + " of " + total + " [v]";
        }

        #endregion
    }
}
