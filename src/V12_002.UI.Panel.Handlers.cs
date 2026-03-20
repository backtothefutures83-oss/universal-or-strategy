// Build 1105: Monolith Panel -- Click Handlers
// All actions route through Enqueue() for strategy-thread serialization.
// Complex commands reuse existing IPC TryHandle* routing via PanelCommand().
// Entry handlers match the hotkey pattern (UI.Callbacks.cs:175-176).
using System;
using System.Windows;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Handlers

        private void AttachPanelHandlers()
        {
            if (_btnOrLong != null) _btnOrLong.Click += OnPanelOrLong;
            if (_btnOrShort != null) _btnOrShort.Click += OnPanelOrShort;
            if (_btnRma != null) _btnRma.Click += OnPanelRma;
            if (_btnMomo != null) _btnMomo.Click += OnPanelMomo;
            if (_btnFlatten != null) _btnFlatten.Click += OnPanelFlatten;
            if (_btnCancel != null) _btnCancel.Click += OnPanelCancel;
            if (_btnBe != null) _btnBe.Click += OnPanelBe;
            if (_btnTrail != null) _btnTrail.Click += OnPanelTrail;
            if (_btnTrim != null) _btnTrim.Click += OnPanelTrim;

            // Mode chips -- lambda captures; cleaned up by GC when buttons are nulled
            if (_modeChips != null)
            {
                string[] modes = { "OR", "RMA", "RETEST", "MOMO", "FFMA", "TREND" };
                for (int i = 0; i < _modeChips.Length && i < modes.Length; i++)
                {
                    string mode = modes[i];
                    _modeChips[i].Click += (s, e) => PanelCommand("SET_MODE|" + mode);
                }
            }

            // Count chips
            if (_countChips != null)
            {
                for (int i = 0; i < _countChips.Length; i++)
                {
                    int count = i + 1;
                    _countChips[i].Click += (s, e) => PanelCommand("SET_TARGETS|" + count);
                }
            }
        }

        private void DetachPanelHandlers()
        {
            if (_btnOrLong != null) _btnOrLong.Click -= OnPanelOrLong;
            if (_btnOrShort != null) _btnOrShort.Click -= OnPanelOrShort;
            if (_btnRma != null) _btnRma.Click -= OnPanelRma;
            if (_btnMomo != null) _btnMomo.Click -= OnPanelMomo;
            if (_btnFlatten != null) _btnFlatten.Click -= OnPanelFlatten;
            if (_btnCancel != null) _btnCancel.Click -= OnPanelCancel;
            if (_btnBe != null) _btnBe.Click -= OnPanelBe;
            if (_btnTrail != null) _btnTrail.Click -= OnPanelTrail;
            if (_btnTrim != null) _btnTrim.Click -= OnPanelTrim;
        }

        // Entry handlers -- match hotkey pattern (UI.Callbacks.cs:175-176)
        private void OnPanelOrLong(object sender, RoutedEventArgs e)
        {
            double stopDist = CalculateORStopDistance();
            int contracts = CalculatePositionSize(stopDist);
            Enqueue(ctx => ctx.ExecuteLong(contracts));
        }

        private void OnPanelOrShort(object sender, RoutedEventArgs e)
        {
            double stopDist = CalculateORStopDistance();
            int contracts = CalculatePositionSize(stopDist);
            Enqueue(ctx => ctx.ExecuteShort(contracts));
        }

        private void OnPanelRma(object sender, RoutedEventArgs e)
        {
            PanelCommand("SET_MODE|RMA");
        }

        private void OnPanelMomo(object sender, RoutedEventArgs e)
        {
            PanelCommand("SET_MODE|MOMO");
        }

        // Management handlers -- route through IPC command processing
        private void OnPanelFlatten(object sender, RoutedEventArgs e)
        {
            PanelCommand("FLATTEN");
        }

        private void OnPanelCancel(object sender, RoutedEventArgs e)
        {
            PanelCommand("CANCEL_ALL");
        }

        private void OnPanelBe(object sender, RoutedEventArgs e)
        {
            PanelCommand("BE");
        }

        private void OnPanelTrail(object sender, RoutedEventArgs e)
        {
            PanelCommand("SET_TRAIL|4");
        }

        private void OnPanelTrim(object sender, RoutedEventArgs e)
        {
            PanelCommand("TRIM_50");
        }

        /// <summary>
        /// Routes panel button actions through existing IPC command handlers.
        /// Bypasses IPC queue, symbol filter, and allowlist -- commands execute
        /// immediately on the strategy thread via Enqueue + TryHandle* methods.
        /// Signature refs: TryHandleModeCommand (Mode.cs:37), TryHandleRiskCommand
        /// (Mode.cs:136), TryHandleFleetCommand (Fleet.cs:37), TryHandleConfigCommand
        /// (Misc.cs:37).
        /// </summary>
        private void PanelCommand(string command)
        {
            string captured = command;
            long senderTicks = DateTime.UtcNow.Ticks;
            Enqueue(ctx =>
            {
                string[] parts = captured.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return;
                string action = parts[0].Trim().ToUpperInvariant();

                Print(string.Format("[PANEL] {0}", action));

                if (ctx.TryHandleModeCommand(action, parts)) return;
                if (ctx.TryHandleRiskCommand(action, parts)) return;
                if (ctx.TryHandleFleetCommand(action, parts, senderTicks)) return;
                if (ctx.TryHandleConfigCommand(action, parts)) return;
            });
        }

        #endregion
    }
}
