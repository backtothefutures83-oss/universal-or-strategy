// Build 971: V12_002 BarUpdate -- OnBarUpdate
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            // Only process primary series
            if (BarsInProgress != 0) return;
            if (CurrentBar < 5) return;

            try
            {
                TouchStrategyHeartbeat();

                // Update last known price for UI events
                lastKnownPrice = Close[0];

                // V12.12: Daily summary roll-over (throttled)
                if (EnableComplianceHub)
                {
                    DateTime nowInZone = GetComplianceNow();
                    if ((nowInZone - lastDailySummaryCheck).TotalSeconds >= 30)
                    {
                        List<Account> complianceAccounts = GetComplianceAccounts();
                        if (complianceAccounts.Count > 0)
                            MaybeFinalizeDailySummaries(nowInZone, complianceAccounts);
                    }
                }

                // V8.21: Reduced log volume - OR buildings and updates are handled via DrawORBox and UpdateDisplay

                // Process IPC Commands
                ProcessIpcCommands();

                // Phase 2: Drain Follower Bracket FSM Mailbox
                DrainAccountMailbox();

                // CIT Logic
                ManageCIT();

                // Monitor RMA Proximity and Exhaustion (Phase 9.2)
                MonitorRmaProximity();

                // V8.2 FIX: Process pending TREND entry (deferred from button click)
                if (pendingTRENDEntry)
                {
                    double trendDist   = CalculateTRENDStopDistance();
                    int trendContracts = CalculatePositionSize(trendDist);
                    ExecuteTRENDEntry(trendContracts);
                }

                // Update ATR value from 5-min bars
                if (BarsArray[1] != null && BarsArray[1].Count > RMAATRPeriod)
                {
                    currentATR = atrIndicator[0];
                }

                // V11: Update Telemetry Cache (Thread-safe for UI)
                _ema9Val = (float)ema9[0];

                // CRITICAL FIX: Convert from LOCAL timezone (PC) to selected timezone
                DateTime barTimeInZone = ConvertToSelectedTimeZone(Time[0]);
                TimeSpan currentTime = barTimeInZone.TimeOfDay;
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;

                // Calculate OR end time based on session start + timeframe
                TimeSpan orEndTime = sessionStartTime.Add(TimeSpan.FromMinutes((int)ORTimeframe));

                // Detect if session crosses midnight (e.g. 21:00 to 07:00)
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                // V11: Draw MNL Anchor Line if active
                if (currentRmaAnchor == RmaAnchorType.Manual && cachedMnlPrice > 0)
                {
                    NinjaTrader.NinjaScript.DrawingTools.Draw.HorizontalLine(this, "MNL_Line", cachedMnlPrice, Brushes.Magenta, DashStyleHelper.Dash, 2);
                }
                else
                {
                    RemoveDrawObject("MNL_Line");
                }
                
                // Smart reset logic - only reset at NEW SESSION START
                bool shouldReset = false;

                if (sessionCrossesMidnight)
                {
                    // For overnight sessions: only reset at session start
                    if (currentTime >= sessionStartTime && currentTime < sessionStartTime.Add(TimeSpan.FromMinutes(10)))
                    {
                        if (barTimeInZone.Date != lastResetDate)
                        {
                            shouldReset = true;
                        }
                    }
                }
                else
                {
                    // For regular sessions: reset when date changes AFTER session ends
                    if (barTimeInZone.Date != lastResetDate && currentTime >= sessionStartTime)
                    {
                        shouldReset = true;
                    }
                }

                if (shouldReset)
                {
                    ResetOR();
                    lastResetDate = barTimeInZone.Date;
                    Print(string.Format("Session Reset: {0} at {1} {2}",
                        barTimeInZone.Date.ToShortDateString(), currentTime, SelectedTimeZone));
                }

                // Build OR during window
                if (currentTime > sessionStartTime && currentTime <= orEndTime)
                {
                    if (!isInORWindow)
                    {
                        Print(string.Format("OR WINDOW START: {0} (Bar time in {1})",
                            barTimeInZone.ToString("MM/dd/yyyy HH:mm:ss"), SelectedTimeZone));
                    }

                    isInORWindow = true;
                    sessionHigh = Math.Max(sessionHigh, High[0]);
                    sessionLow = Math.Min(sessionLow, Low[0]);
                    sessionRange = sessionHigh - sessionLow;
                    sessionMid = (sessionHigh + sessionLow) / 2.0;

                    if (orStartDateTime == DateTime.MinValue)
                    {
                        orStartDateTime = Time[0];
                        sessionStartDateTime = Time[0];
                        orStartBarIndex = CurrentBar;
                        Print(string.Format("OR Start tracked - Bar {0}", CurrentBar));
                    }
                }

                // Mark OR complete when the last bar of the window closes
                if (currentTime >= orEndTime && !orComplete && orStartBarIndex > 0)
                {
                    isInORWindow = false;
                    orComplete = true;
                    orEndDateTime = Time[0];
                    orEndBarIndex = CurrentBar;

                    Print(string.Format("OR COMPLETE at {0}: H={1:F2} L={2:F2} M={3:F2} R={4:F2}",
                        barTimeInZone.ToString("HH:mm:ss"), sessionHigh, sessionLow, sessionMid, sessionRange));
                    Print(string.Format("OR Targets: T1={0}({1}) T2={2}({3}) Stop=-{4:F2}",
                        Target1Value, T1Type, Target2Value, T2Type, CalculateORStopDistance()));

                    // V8.30: Always draw immediately when OR completes (important event)
                    DrawORBox();
                    lastDrawORBoxTime = DateTime.UtcNow;
                }

                // Update box if OR complete
                bool inActiveSession = false;
                if (sessionCrossesMidnight)
                {
                    inActiveSession = (currentTime >= sessionStartTime || currentTime <= sessionEndTime);
                }
                else
                {
                    inActiveSession = (currentTime >= sessionStartTime && currentTime <= sessionEndTime);
                }

                // V8.30: Throttle DrawORBox updates to prevent chart saturation
                if (orComplete && sessionHigh != double.MinValue && inActiveSession)
                {
                    if ((DateTime.UtcNow - lastDrawORBoxTime).TotalMilliseconds >= DRAW_ORBOX_THROTTLE_MS)
                    {
                        DrawORBox();
                        lastDrawORBoxTime = DateTime.UtcNow;
                    }
                }

                // Position sync check
                SyncPositionState();
                SymmetryGuardProcessPendingFollowerFills();

                // Manage trailing stops - NOW CALLED ON EVERY PRICE CHANGE!
                if (activePositions.Count > 0)
                {
                    Enqueue(ctx => ctx.ManageTrailingStops());
                    Enqueue(ctx => ctx.ManageCIT());
                }

                // V8.7: Check FFMA conditions when armed
                if (isFFMAModeArmed && FFMAEnabled)
                {
                    CheckFFMAConditions();
                }

                SyncPendingOrders();  // V12.30: Real-time sizing synchronization
                PublishUiSnapshot();
            }
            catch (Exception ex)
            {
                Print("ERROR OnBarUpdate: " + ex.Message);
            }
        }

        #endregion
    }
}
