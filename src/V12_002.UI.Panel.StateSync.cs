// Build 1105: Monolith Panel -- State Synchronization
// Reads volatile strategy state and updates WPF controls on the UI thread.
// Called every PanelRefreshMs by the panel timer (V12_002.UI.Panel.Lifecycle.cs).
using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel State Sync

        private void UpdatePanelState()
        {
            if (_panelRoot == null || _isTerminating) return;

            // Price
            double price = lastKnownPrice;
            _priceText.Text = price > 0
                ? Instrument.MasterInstrument.FormatPrice(price)
                : "--";

            // Direction coloring
            var mp = Position != null ? Position.MarketPosition : MarketPosition.Flat;
            if (mp == MarketPosition.Long)
                _priceText.Foreground = GreenFg;
            else if (mp == MarketPosition.Short)
                _priceText.Foreground = RedFg;
            else
                _priceText.Foreground = TextPri;

            // Mode
            string mode = GetCurrentConfigMode();
            _modeText.Text = "Mode: " + mode;

            // Status
            if (mp != MarketPosition.Flat && Position != null)
            {
                int qty = Position.Quantity;
                string dir = mp == MarketPosition.Long ? "LONG" : "SHORT";
                _statusText.Text = string.Format("{0} x{1}", dir, qty);
                _statusText.Foreground = mp == MarketPosition.Long ? GreenFg : RedFg;
            }
            else
            {
                _statusText.Text = "Status: Idle";
                _statusText.Foreground = TextDim;
            }

            UpdatePanelStatusLed(mp);
            UpdatePanelModeChips(mode);
            UpdatePanelCountChips();
        }

        private void UpdatePanelStatusLed(MarketPosition mp)
        {
            if (_statusLed == null) return;
            if (_isTerminating)
                _statusLed.Background = RedFg;
            else if (mp != MarketPosition.Flat)
                _statusLed.Background = CyanFg;
            else
                _statusLed.Background = GreenFg;
        }

        private void UpdatePanelModeChips(string activeMode)
        {
            if (_modeChips == null) return;
            // Chip labels: ORB, RMA, RETEST, MOMO, FFMA, TREND
            // Mode keys:   OR,  RMA, RETEST, MOMO, FFMA, TREND
            string[] modeKeys = { "OR", "RMA", "RETEST", "MOMO", "FFMA", "TREND" };
            for (int i = 0; i < _modeChips.Length && i < modeKeys.Length; i++)
            {
                bool active = string.Equals(activeMode, modeKeys[i], StringComparison.OrdinalIgnoreCase);
                _modeChips[i].Foreground = active ? CyanFg : TextDim;
                _modeChips[i].BorderBrush = active ? CyanBdr : TextDim;
            }
        }

        private void UpdatePanelCountChips()
        {
            if (_countChips == null) return;
            int current = activeTargetCount;
            for (int i = 0; i < _countChips.Length; i++)
            {
                bool active = (i + 1) == current;
                _countChips[i].Foreground = active ? YellowFg : TextDim;
                _countChips[i].BorderBrush = active ? YellowBdr : TextDim;
            }
        }

        #endregion
    }
}
