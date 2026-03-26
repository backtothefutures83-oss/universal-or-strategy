// Build 1105: V12_001 panel port -- live state sync from strategy fields
using System;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows.Controls;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel State Sync

        private void UpdatePanelState()
        {
            if (rootContainer == null || _isTerminating) return;

            double price = lastKnownPrice;
            if (lastPriceText != null)
            {
                lastPriceText.Text = price > 0
                    ? Instrument.MasterInstrument.FormatPrice(price)
                    : "--";

                MarketPosition mp = Position != null ? Position.MarketPosition : MarketPosition.Flat;
                lastPriceText.Foreground = mp == MarketPosition.Long ? GreenFg
                    : mp == MarketPosition.Short ? RedFg
                    : TextPrimary;
            }

            string mode = GetCurrentConfigMode();
            if (string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase))
                mode = "ORB";

            if (!string.Equals(_panelLastSyncedMode, mode, StringComparison.OrdinalIgnoreCase))
            {
                _panelLastSyncedMode = mode;
                SyncModeChipVisuals(mode);
                UpdateContextualUI(mode);
            }

            // Build 1106: Reverse-sync panel config fields when mode switch hydrated a new profile
            if (_configSyncNeeded)
            {
                _configSyncNeeded = false;
                SyncPanelConfigFromStrategy();
            }

            int count = Math.Max(1, Math.Min(5, activeTargetCount));
            if (_panelLastSyncedTargetCount != count)
            {
                long elapsedTicks = DateTime.UtcNow.Ticks - _panelChipClickTicks;
                bool guardActive = elapsedTicks >= 0 && elapsedTicks < TimeSpan.TicksPerSecond;
                if (!guardActive)
                {
                    _panelLastSyncedTargetCount = count;
                    SyncCountChipVisuals(count);
                    UpdateTargetVisibility(count);
                }
            }

            UpdateRmaButtonVisual(isRMAModeActive);
            if (trendRmaToggle != null) trendRmaToggle.Opacity = isTrendRmaMode ? 1.0 : 0.5;
            if (retestRmaToggle != null) retestRmaToggle.Opacity = isRetestRmaMode ? 1.0 : 0.5;

            UpdateHubStatusLed();
            UpdateTelemetryDisplay();
            UpdateComplianceDisplay();
            UpdateTrendIndicator();

            // Build 1107: Live position control center -- dual mode switch
            MarketPosition posMP = Position != null ? Position.MarketPosition : MarketPosition.Flat;
            if (posMP != MarketPosition.Flat && activePositions != null)
            {
                PositionInfo livePos = FindMasterPosition(out string entryName);
                if (livePos != null && livePos.EntryFilled && livePos.RemainingContracts > 0)
                {
                    if (_currentLiveEntryName == null)
                    {
                        // Entering live mode: hide config buttons
                        SetConfigTargetButtonsVisible(false);
                    }
                    _currentLiveEntryName = entryName;
                    SyncLiveTargetRows(entryName, livePos);
                    if (liveStopRow != null) liveStopRow.Visibility = System.Windows.Visibility.Visible;
                    return;
                }
            }
            // Flat or no valid position: exit live mode
            if (_currentLiveEntryName != null)
            {
                _currentLiveEntryName = null;
                SetLiveTargetRowsVisible(false);
                if (liveStopRow != null) liveStopRow.Visibility = System.Windows.Visibility.Collapsed;
                int restoreCount = Math.Max(1, Math.Min(5, activeTargetCount));
                UpdateTargetVisibility(restoreCount);
            }
        }

        // Build 1107: Find the primary master position for live display.
        // Returns the first non-follower, non-cleanup, filled position with remaining contracts.
        private PositionInfo FindMasterPosition(out string entryName)
        {
            entryName = null;
            if (activePositions == null) return null;
            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pi = kvp.Value;
                if (pi != null && !pi.IsFollower && !pi.PendingCleanup
                    && pi.EntryFilled && pi.RemainingContracts > 0)
                {
                    entryName = kvp.Key;
                    return pi;
                }
            }
            return null;
        }

        private void SetConfigTargetButtonsVisible(bool visible)
        {
            if (visible)
            {
                // Restore based on target count (UpdateTargetVisibility handles this)
                int count = Math.Max(1, Math.Min(5, activeTargetCount));
                UpdateTargetVisibility(count);
            }
            else
            {
                if (t1Button != null) t1Button.Visibility = System.Windows.Visibility.Collapsed;
                if (t2Button != null) t2Button.Visibility = System.Windows.Visibility.Collapsed;
                if (t3Button != null) t3Button.Visibility = System.Windows.Visibility.Collapsed;
                if (t4Button != null) t4Button.Visibility = System.Windows.Visibility.Collapsed;
                if (t5Button != null) t5Button.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void SetLiveTargetRowsVisible(bool visible)
        {
            if (!visible)
            {
                if (liveT1Row != null) liveT1Row.Visibility = System.Windows.Visibility.Collapsed;
                if (liveT2Row != null) liveT2Row.Visibility = System.Windows.Visibility.Collapsed;
                if (liveT3Row != null) liveT3Row.Visibility = System.Windows.Visibility.Collapsed;
                if (liveT4Row != null) liveT4Row.Visibility = System.Windows.Visibility.Collapsed;
                if (liveT5Row != null) liveT5Row.Visibility = System.Windows.Visibility.Collapsed;
            }
            // When visible=true, individual rows are controlled by SyncLiveTargetRows
        }

        private void SetLiveTargetRowVisible(int t, bool visible)
        {
            Grid row = null;
            switch (t)
            {
                case 1: row = liveT1Row; break;
                case 2: row = liveT2Row; break;
                case 3: row = liveT3Row; break;
                case 4: row = liveT4Row; break;
                case 5: row = liveT5Row; break;
            }
            if (row != null)
                row.Visibility = visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void SyncLiveTargetRows(string entryName, PositionInfo pos)
        {
            for (int t = 1; t <= 5; t++)
            {
                bool active = t <= pos.InitialTargetCount && !IsTargetFilled(pos, t);
                SetLiveTargetRowVisible(t, active);
                if (!active) continue;

                // Read live order data
                var targetDict = GetTargetOrdersDictionary(t);
                Order targetOrder = null;
                if (targetDict != null) targetDict.TryGetValue(entryName, out targetOrder);

                double price = GetTargetPrice(pos, t);
                if (targetOrder != null && targetOrder.LimitPrice > 0)
                    price = targetOrder.LimitPrice;

                int contracts = GetTargetContracts(pos, t);
                int filled = GetTargetFilledQuantity(pos, t);
                bool isWorking = targetOrder != null &&
                    (targetOrder.OrderState == OrderState.Working || targetOrder.OrderState == OrderState.Accepted);

                // Update price (skip if user is editing)
                TextBox priceBox = GetLiveTargetPriceBox(t);
                if (priceBox != null && !priceBox.IsFocused)
                    priceBox.Text = Instrument.MasterInstrument.FormatPrice(price);

                // Update contract count + status color
                TextBlock ctsBlock = GetLiveTargetCtsBlock(t);
                if (ctsBlock != null)
                {
                    ctsBlock.Text = (contracts - filled) + " cts";
                    ctsBlock.Foreground = isWorking ? GreenFg : TextMuted;
                }
            }

            // Stop row
            if (liveStopRow != null)
            {
                Order stopOrder = null;
                if (stopOrders != null) stopOrders.TryGetValue(entryName, out stopOrder);
                double stopPrice = pos.CurrentStopPrice;
                if (stopOrder != null && stopOrder.StopPrice > 0)
                    stopPrice = stopOrder.StopPrice;
                if (liveStopPrice != null)
                    liveStopPrice.Text = stopPrice > 0
                        ? Instrument.MasterInstrument.FormatPrice(stopPrice)
                        : "--";
                liveStopRow.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private TextBox GetLiveTargetPriceBox(int t)
        {
            switch (t)
            {
                case 1: return liveT1Price;
                case 2: return liveT2Price;
                case 3: return liveT3Price;
                case 4: return liveT4Price;
                case 5: return liveT5Price;
                default: return null;
            }
        }

        private TextBlock GetLiveTargetCtsBlock(int t)
        {
            switch (t)
            {
                case 1: return liveT1Cts;
                case 2: return liveT2Cts;
                case 3: return liveT3Cts;
                case 4: return liveT4Cts;
                case 5: return liveT5Cts;
                default: return null;
            }
        }

        private void UpdateHubStatusLed()
        {
            if (hubStatusLed == null) return;

            if (_isTerminating)
                hubStatusLed.Background = RedFg;
            else if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                hubStatusLed.Background = GreenFg;
            else
                hubStatusLed.Background = CyanFg;
        }

        private void UpdateTelemetryDisplay()
        {
            string hiText = "--";
            string loText = "--";
            string rangeText = "--";

            if (sessionHigh != double.MinValue && sessionLow != double.MaxValue)
            {
                double range = sessionHigh - sessionLow;
                hiText = Instrument.MasterInstrument.FormatPrice(sessionHigh);
                loText = Instrument.MasterInstrument.FormatPrice(sessionLow);
                rangeText = range.ToString("0.##");
            }

            UpdateOrText(or5Text, "OR5", hiText, loText, rangeText);
            UpdateOrText(or15Text, "OR15", hiText, loText, rangeText);

            UpdateEmaText(ema9Text, "9:", GetPriceText(GetIndicatorValue(ema9)), TextPrimary);
            UpdateEmaText(ema15Text, "15:", GetPriceText(GetIndicatorValue(ema15)), TextPrimary);
            UpdateEmaText(ema30Text, "30:", GetPriceText(GetIndicatorValue(ema30)), GreenFg);
            UpdateEmaText(ema65Text, "65:", GetPriceText(GetIndicatorValue(ema65)), TextPrimary);
            UpdateEmaText(ema200Text, "200:", GetPriceText(GetIndicatorValue(ema200)), PurpleFg);

            if (atrText != null)
            {
                atrText.Text = currentATR > 0
                    ? "ATR: " + currentATR.ToString("0.##")
                    : "ATR: --";
            }
        }

        private void UpdateComplianceDisplay()
        {
            string accountName = Account != null ? Account.Name : "--";
            double dailyProfit = accountDailyProfit.TryGetValue(accountName, out var daily) ? daily : 0;
            double totalProfit = accountTotalProfit.TryGetValue(accountName, out var total) ? total : 0;
            int tradeCount = accountTradeCount.TryGetValue(accountName, out var trades) ? trades : 0;
            int uniqueDays = GetUniqueTradingDays(accountName);
            double maxDrawdown = accountMaxDrawdown.TryGetValue(accountName, out var dd) ? dd : 0;

            if (complianceSummaryText != null)
            {
                complianceSummaryText.Text = "ACCT: " + accountName
                    + " / TRADES: " + tradeCount
                    + " / DAYS: " + uniqueDays
                    + " / MAXDD: $" + maxDrawdown.ToString("0");
            }

            double consistencyPct = totalProfit > 0
                ? Math.Abs(dailyProfit) / Math.Abs(totalProfit) * 100.0
                : 0;
            if (complianceConsistencyText != null)
            {
                complianceConsistencyText.Text = "CONSISTENCY: " + consistencyPct.ToString("0") + "%";
                complianceConsistencyText.Foreground = consistencyPct <= 30 ? GreenFg
                    : consistencyPct <= 50 ? YellowFg
                    : RedFg;
            }

            if (compliancePayoutText != null)
            {
                if (PayoutMinProfit > 0)
                {
                    double payoutPct = totalProfit / PayoutMinProfit * 100.0;
                    compliancePayoutText.Text = "PAYOUT: " + payoutPct.ToString("0") + "%";
                    compliancePayoutText.Foreground = payoutPct >= 100 ? GreenFg
                        : payoutPct >= 50 ? YellowFg
                        : TextMuted;
                }
                else
                {
                    compliancePayoutText.Text = "PAYOUT: --";
                    compliancePayoutText.Foreground = TextMuted;
                }
            }

            if (complianceDrawdownText != null)
            {
                double ddBuffer = TrailingDrawdownLimit > 0 ? TrailingDrawdownLimit - maxDrawdown : 0;
                complianceDrawdownText.Text = "DD BUFFER: $" + ddBuffer.ToString("0");
                complianceDrawdownText.Foreground = ddBuffer > 0 ? GreenFg : RedFg;
            }
        }

        private void UpdateTrendIndicator()
        {
            if (trendText == null || trendIndicator == null) return;

            double currentPrice = lastKnownPrice > 0
                ? lastKnownPrice
                : (CurrentBar >= 0 ? Close[0] : 0);
            double ema9Value = GetIndicatorValue(ema9);

            if (currentPrice <= 0 || double.IsNaN(ema9Value) || ema9Value <= 0)
            {
                trendText.Text = "--";
                trendText.Foreground = TextMuted;
                trendIndicator.Background = BgSlate;
                trendIndicator.BorderBrush = BorderSlate;
                return;
            }

            bool bullish = currentPrice >= ema9Value;
            trendText.Text = bullish ? "BULLISH" : "BEARISH";
            trendText.Foreground = bullish ? GreenFg : RedFg;
            trendIndicator.Background = bullish ? GreenBg : RedBg;
            trendIndicator.BorderBrush = bullish ? GreenBorder : RedBorder;
        }

        private void SyncModeChipVisuals(string mode)
        {
            foreach (Button btn in new[] { modeOrbButton, modeRmaButton, modeRetestButton, modeMomoButton, modeFfmaButton, modeTrendButton })
            {
                if (btn == null) continue;
                btn.Background = BtnBg;
                btn.Foreground = TextMuted;
                btn.BorderBrush = BtnBorder;
            }

            Button activeButton = null;
            switch ((mode ?? "ORB").ToUpperInvariant())
            {
                case "RMA":
                    activeButton = modeRmaButton;
                    break;
                case "RETEST":
                    activeButton = modeRetestButton;
                    break;
                case "MOMO":
                    activeButton = modeMomoButton;
                    break;
                case "FFMA":
                    activeButton = modeFfmaButton;
                    break;
                case "TREND":
                    activeButton = modeTrendButton;
                    break;
                default:
                    activeButton = modeOrbButton;
                    break;
            }

            if (activeButton != null)
            {
                activeButton.Background = CyanBg;
                activeButton.Foreground = CyanFg;
                activeButton.BorderBrush = CyanBorder;
            }
        }

        private void SyncCountChipVisuals(int count)
        {
            Button[] countButtons = { cnt1, cnt2, cnt3, cnt4, cnt5 };
            for (int i = 0; i < countButtons.Length; i++)
            {
                Button btn = countButtons[i];
                if (btn == null) continue;

                bool isActive = (i + 1) == count;
                btn.Background = isActive ? CyanBg : BtnBg;
                btn.Foreground = isActive ? CyanFg : TextPrimary;
                btn.BorderBrush = isActive ? CyanBorder : BtnBorder;
            }
        }

        private void UpdateOrText(TextBlock target, string label, string hiText, string loText, string rangeText)
        {
            if (target == null) return;
            target.Inlines.Clear();
            target.Inlines.Add(new System.Windows.Documents.Run(label + ": ") { Foreground = OrangeFg, FontWeight = System.Windows.FontWeights.Bold });
            target.Inlines.Add(new System.Windows.Documents.Run(hiText) { Foreground = OrangeFg });
            target.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            target.Inlines.Add(new System.Windows.Documents.Run(loText) { Foreground = OrangeFg });
            target.Inlines.Add(new System.Windows.Documents.Run(" (R: " + rangeText + ")") { Foreground = TextMuted });
        }

        private void UpdateEmaText(TextBlock target, string label, string value, SolidColorBrush valueColor)
        {
            if (target == null) return;
            target.Inlines.Clear();
            target.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            target.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = valueColor });
        }

        private double GetIndicatorValue(EMA indicator)
        {
            try
            {
                if (indicator == null || CurrentBar < 0)
                    return double.NaN;
                return indicator[0];
            }
            catch
            {
                return double.NaN;
            }
        }

        private string GetPriceText(double value)
        {
            if (double.IsNaN(value) || value <= 0)
                return "--";
            return Instrument.MasterInstrument.FormatPrice(value);
        }

        // Build 1106: One-shot reverse sync of panel config controls from strategy globals.
        // Triggered ONLY after a mode switch hydrates a stored profile. Not called on every tick.
        // Uses existing helpers: FormatPanelDouble (Construction.cs:1109), GetPanelTargetModeText
        // (Construction.cs:1092), SetComboSelection (Construction.cs:1078).
        private void SyncPanelConfigFromStrategy()
        {
            // Target values
            if (svT1Val != null) svT1Val.Text = FormatPanelDouble(Target1Value);
            if (svT2Val != null) svT2Val.Text = FormatPanelDouble(Target2Value);
            if (svT3Val != null) svT3Val.Text = FormatPanelDouble(Target3Value);
            if (svT4Val != null) svT4Val.Text = FormatPanelDouble(Target4Value);
            if (svT5Val != null) svT5Val.Text = FormatPanelDouble(Target5Value);

            // Target types
            if (svT1Type != null) SetComboSelection(svT1Type, GetPanelTargetModeText(T1Type));
            if (svT2Type != null) SetComboSelection(svT2Type, GetPanelTargetModeText(T2Type));
            if (svT3Type != null) SetComboSelection(svT3Type, GetPanelTargetModeText(T3Type));
            if (svT4Type != null) SetComboSelection(svT4Type, GetPanelTargetModeText(T4Type));
            if (svT5Type != null) SetComboSelection(svT5Type, GetPanelTargetModeText(T5Type));

            // Stop multiplier (mode-aware)
            if (strVal != null)
            {
                double stopValue = isRMAModeActive ? RMAStopATRMultiplier : StopMultiplier;
                strVal.Text = FormatPanelDouble(stopValue);
            }

            // Max risk
            if (maxVal != null) maxVal.Text = FormatPanelDouble(MaxRiskAmount);

            // Count chips + target row visibility
            int count = Math.Max(1, Math.Min(5, activeTargetCount));
            _panelLastSyncedTargetCount = count;
            SyncCountChipVisuals(count);
            UpdateTargetVisibility(count);
        }

        #endregion
    }
}
