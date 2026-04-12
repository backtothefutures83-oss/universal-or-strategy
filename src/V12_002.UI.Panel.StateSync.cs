// Build 1105: V12_001 panel port -- live state sync from strategy fields
using System;
using NinjaTrader.Cbi;
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
            UIStateSnapshot snapshot = GetUiSnapshot();

            double price = snapshot.LastPrice;
            if (lastPriceText != null)
            {
                lastPriceText.Text = price > 0
                    ? Instrument.MasterInstrument.FormatPrice(price)
                    : "--";

                MarketPosition mp = snapshot.MasterMarketPosition;
                lastPriceText.Foreground = mp == MarketPosition.Long ? GreenFg
                    : mp == MarketPosition.Short ? RedFg
                    : TextPrimary;
            }

            string mode = string.IsNullOrEmpty(snapshot.Mode) ? "ORB" : snapshot.Mode;

            if (!string.Equals(_panelLastSyncedMode, mode, StringComparison.OrdinalIgnoreCase))
            {
                _panelLastSyncedMode = mode;
                SyncModeChipVisuals(mode);
                UpdateContextualUI(mode);
            }

            if (snapshot.ConfigRevision != _panelAppliedConfigRevision)
            {
                SyncPanelConfigFromSnapshot(snapshot);
                _panelAppliedConfigRevision = snapshot.ConfigRevision;
            }

            int count = Math.Max(1, Math.Min(5, snapshot.TargetCount));
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

            UpdateRmaButtonVisual(snapshot.IsRmaModeActive);
            if (trendRmaToggle != null) trendRmaToggle.Opacity = snapshot.IsTrendRmaMode ? 1.0 : 0.5;
            if (retestRmaToggle != null) retestRmaToggle.Opacity = snapshot.IsRetestRmaMode ? 1.0 : 0.5;

            UpdateHubStatusLed(snapshot);
            UpdateTelemetryDisplay(snapshot);
            UpdateComplianceDisplay(snapshot);
            UpdateTrendIndicator(snapshot);

            UILivePositionSnapshot livePosition = snapshot.LivePosition;
            if (livePosition != null && livePosition.HasLivePosition)
            {
                SetConfigTargetButtonsVisible(false, count);
                _currentLiveEntryName = livePosition.EntryName;
                SyncLiveTargetRows(livePosition);
                if (liveStopRow != null)
                    liveStopRow.Visibility = System.Windows.Visibility.Visible;
                return;
            }

            if (_currentLiveEntryName != null)
            {
                _currentLiveEntryName = null;
                SetLiveTargetRowsVisible(false);
                if (liveStopRow != null) liveStopRow.Visibility = System.Windows.Visibility.Collapsed;
                UpdateTargetVisibility(count);
            }
        }

        private void SetConfigTargetButtonsVisible(bool visible, int count)
        {
            if (visible)
            {
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

        private void SyncLiveTargetRows(UILivePositionSnapshot livePosition)
        {
            for (int t = 1; t <= 5; t++)
            {
                UILiveTargetSnapshot target = livePosition.Targets[t - 1];
                bool active = target != null && target.IsVisible;
                SetLiveTargetRowVisible(t, active);
                if (!active || target == null) continue;

                TextBox priceBox = GetLiveTargetPriceBox(t);
                if (priceBox != null && !priceBox.IsFocused)
                {
                    priceBox.Text = target.Price > 0
                        ? Instrument.MasterInstrument.FormatPrice(target.Price)
                        : "--";
                }

                TextBlock ctsBlock = GetLiveTargetCtsBlock(t);
                if (ctsBlock != null)
                {
                    ctsBlock.Text = target.RemainingContracts + " cts";
                    ctsBlock.Foreground = target.IsWorking ? GreenFg : TextMuted;
                }
            }

            if (liveStopRow != null)
            {
                if (liveStopPrice != null)
                    liveStopPrice.Text = livePosition.StopPrice > 0
                        ? Instrument.MasterInstrument.FormatPrice(livePosition.StopPrice)
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

        private void UpdateHubStatusLed(UIStateSnapshot snapshot)
        {
            if (hubStatusLed == null) return;

            if (_isTerminating)
                hubStatusLed.Background = RedFg;
            else if (snapshot.MasterMarketPosition != MarketPosition.Flat)
                hubStatusLed.Background = GreenFg;
            else
                hubStatusLed.Background = CyanFg;

            hubStatusLed.ToolTip = snapshot.StatusMessage ?? string.Empty;
        }

        private void UpdateTelemetryDisplay(UIStateSnapshot snapshot)
        {
            string hiText = "--";
            string loText = "--";
            string rangeText = "--";

            if (snapshot.OrHigh > 0 && snapshot.OrLow > 0)
            {
                hiText = Instrument.MasterInstrument.FormatPrice(snapshot.OrHigh);
                loText = Instrument.MasterInstrument.FormatPrice(snapshot.OrLow);
                rangeText = snapshot.OrRange.ToString("0.##");
            }

            UpdateOrText(or5Text, "OR5", hiText, loText, rangeText);
            UpdateOrText(or15Text, "OR15", hiText, loText, rangeText);

            UpdateEmaText(ema9Text, "9:", GetPriceText(snapshot.Ema9Value), TextPrimary);
            UpdateEmaText(ema15Text, "15:", GetPriceText(snapshot.Ema15Value), TextPrimary);
            UpdateEmaText(ema30Text, "30:", GetPriceText(snapshot.Ema30Value), GreenFg);
            UpdateEmaText(ema65Text, "65:", GetPriceText(snapshot.Ema65Value), TextPrimary);
            UpdateEmaText(ema200Text, "200:", GetPriceText(snapshot.Ema200Value), PurpleFg);

            if (atrText != null)
            {
                atrText.Text = snapshot.AtrValue > 0
                    ? "ATR: " + snapshot.AtrValue.ToString("0.##")
                    : "ATR: --";
            }
        }

        private void UpdateComplianceDisplay(UIStateSnapshot snapshot)
        {
            UIComplianceSnapshot compliance = snapshot.Compliance ?? new UIComplianceSnapshot();
            string accountName = string.IsNullOrEmpty(compliance.AccountName) ? "--" : compliance.AccountName;
            double dailyProfit = compliance.DailyProfit;
            double totalProfit = compliance.TotalProfit;
            int tradeCount = compliance.TradeCount;
            int uniqueDays = compliance.UniqueDays;
            double maxDrawdown = compliance.MaxDrawdown;

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
                if (compliance.PayoutMinProfit > 0)
                {
                    double payoutPct = totalProfit / compliance.PayoutMinProfit * 100.0;
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
                double ddBuffer = compliance.TrailingDrawdownLimit > 0
                    ? compliance.TrailingDrawdownLimit - maxDrawdown
                    : 0;
                complianceDrawdownText.Text = "DD BUFFER: $" + ddBuffer.ToString("0");
                complianceDrawdownText.Foreground = ddBuffer > 0 ? GreenFg : RedFg;
            }
        }

        private void UpdateTrendIndicator(UIStateSnapshot snapshot)
        {
            if (trendText == null || trendIndicator == null) return;

            double currentPrice = snapshot.LastPrice;
            double ema9Value = snapshot.Ema9Value;

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

        private string GetPriceText(double value)
        {
            if (double.IsNaN(value) || value <= 0)
                return "--";
            return Instrument.MasterInstrument.FormatPrice(value);
        }

        private void SyncPanelConfigFromSnapshot(UIStateSnapshot snapshot)
        {
            UIConfigSnapshot config = snapshot.Config ?? new UIConfigSnapshot();
            if (svT1Val != null) svT1Val.Text = FormatPanelDouble(config.Target1Value);
            if (svT2Val != null) svT2Val.Text = FormatPanelDouble(config.Target2Value);
            if (svT3Val != null) svT3Val.Text = FormatPanelDouble(config.Target3Value);
            if (svT4Val != null) svT4Val.Text = FormatPanelDouble(config.Target4Value);
            if (svT5Val != null) svT5Val.Text = FormatPanelDouble(config.Target5Value);

            if (svT1Type != null) SetComboSelection(svT1Type, GetPanelTargetModeText(config.Target1Type));
            if (svT2Type != null) SetComboSelection(svT2Type, GetPanelTargetModeText(config.Target2Type));
            if (svT3Type != null) SetComboSelection(svT3Type, GetPanelTargetModeText(config.Target3Type));
            if (svT4Type != null) SetComboSelection(svT4Type, GetPanelTargetModeText(config.Target4Type));
            if (svT5Type != null) SetComboSelection(svT5Type, GetPanelTargetModeText(config.Target5Type));

            if (strVal != null)
                strVal.Text = FormatPanelDouble(config.StopValue);
            if (maxVal != null)
                maxVal.Text = FormatPanelDouble(config.MaxRiskValue);
            if (citVal != null)
                citVal.Text = string.IsNullOrEmpty(config.ChaseIfTouchPoints) ? "0" : config.ChaseIfTouchPoints;
            if (svStrType != null)
                SetComboSelection(svStrType, string.Equals(snapshot.Mode, "ORB", StringComparison.OrdinalIgnoreCase) ? "OR" : "ATR");

            int count = Math.Max(1, Math.Min(5, snapshot.TargetCount));
            _panelLastSyncedTargetCount = count;
            SyncCountChipVisuals(count);
            UpdateTargetVisibility(count);
        }

        #endregion
    }
}
