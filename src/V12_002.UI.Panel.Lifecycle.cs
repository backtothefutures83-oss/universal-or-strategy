// Build 1105: V12_001 panel port -- refresh timer + glow timer lifecycle
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Panel Timer

        private System.Timers.Timer _panelRefreshTimer;
        private System.Windows.Threading.DispatcherTimer _glowTimer;
        private const int PanelRefreshMs = 250;
        private volatile int _panelUpdateInProgress = 0;

        private void StartPanelRefresh()
        {
            if (_isTerminating || _panelRefreshTimer != null) return;

            System.Timers.Timer newTimer = new System.Timers.Timer(PanelRefreshMs);
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

            InitGlowTimer();

            if (_isTerminating)
                StopPanelRefresh();
        }

        private void StopPanelRefresh()
        {
            StopGlowTimer();

            System.Timers.Timer timer = Interlocked.Exchange(ref _panelRefreshTimer, null);
            if (timer == null) return;
            timer.Elapsed -= OnPanelRefreshElapsed;
            timer.Stop();
            timer.Dispose();
        }

        private void OnPanelRefreshElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_isTerminating || rootContainer == null) return;
            // Build 1109 [FREEZE-PROOF]: Skip if previous UpdatePanelState hasn't completed.
            // Prevents WPF dispatcher queue backup under system stress.
            if (Volatile.Read(ref _panelUpdateInProgress) != 0) return;
            try
            {
                if (ChartControl != null)
                {
                    Interlocked.Exchange(ref _panelUpdateInProgress, 1);
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        try { UpdatePanelState(); }
                        finally { Interlocked.Exchange(ref _panelUpdateInProgress, 0); }
                    });
                }
            }
            catch
            {
                Interlocked.Exchange(ref _panelUpdateInProgress, 0);
            }
        }

        private void InitGlowTimer()
        {
            if (_glowTimer != null)
                return;

            _glowTimer = new System.Windows.Threading.DispatcherTimer();
            _glowTimer.Interval = System.TimeSpan.FromMilliseconds(500);
            _glowTimer.Tick += (s, e) =>
            {
                if (contentBody != null)
                {
                    contentBody.BorderBrush = BorderSlate;
                    contentBody.BorderThickness = new Thickness(1, 0, 0, 0);
                }
                _glowTimer.Stop();
            };
        }

        private void TriggerGlow(SolidColorBrush color)
        {
            if (contentBody != null)
            {
                contentBody.BorderBrush = color;
                contentBody.BorderThickness = new Thickness(2, 0, 0, 0);
                if (_glowTimer != null)
                {
                    _glowTimer.Stop();
                    _glowTimer.Start();
                }
            }
        }

        private void StopGlowTimer()
        {
            if (_glowTimer != null)
            {
                _glowTimer.Stop();
                _glowTimer = null;
            }
        }

        #endregion
    }
}
