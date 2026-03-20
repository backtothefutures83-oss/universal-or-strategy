// Build 1105: Monolith Panel -- Timer Lifecycle
// Manages the panel refresh timer. Timer fires on ThreadPool, marshals
// to UI thread via Dispatcher for WPF control updates.
// Pattern follows REAPER audit timer (V12_002.REAPER.cs:93-138).
using System.Threading;
using System.Timers;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Timer

        private Timer _panelRefreshTimer;
        private const int PanelRefreshMs = 250;

        private void StartPanelRefresh()
        {
            if (_isTerminating || _panelRefreshTimer != null) return;

            Timer newTimer = new Timer(PanelRefreshMs);
            newTimer.AutoReset = true;
            newTimer.Elapsed += OnPanelRefreshElapsed;
            newTimer.Start();

            if (_isTerminating)
            {
                newTimer.Elapsed -= OnPanelRefreshElapsed;
                newTimer.Stop();
                newTimer.Dispose();
                return;
            }

            if (Interlocked.CompareExchange(ref _panelRefreshTimer, newTimer, null) != null)
            {
                newTimer.Elapsed -= OnPanelRefreshElapsed;
                newTimer.Stop();
                newTimer.Dispose();
                return;
            }

            if (_isTerminating)
                StopPanelRefresh();
        }

        private void StopPanelRefresh()
        {
            Timer timer = Interlocked.Exchange(ref _panelRefreshTimer, null);
            if (timer == null) return;
            timer.Elapsed -= OnPanelRefreshElapsed;
            timer.Stop();
            timer.Dispose();
        }

        private void OnPanelRefreshElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isTerminating || _panelRoot == null) return;
            try
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.InvokeAsync(() => UpdatePanelState());
            }
            catch { }
        }

        #endregion
    }
}
