// V12.Phase7 MODULAR: RMA Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: ExecuteTrendSplitEntry, DeactivateRMAMode
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        // V12 SIMA: BroadcastEntrySignal and V8 Copy Trading region removed.
        // Trade copying is replaced by direct Account.All iteration in ExecuteSmartDispatchEntry.
        // SignalBroadcaster is retained for ClearAllSubscribers teardown (Lifecycle.cs).

        #region Trend Split Entry

        // V11: Trend RMA (9/15 Split) Logic
        private void ExecuteTrendSplitEntry(int contracts)
        {
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning)
                return;

            if (currentATR <= 0)
            {
                Print("Cannot execute TREND RMA - ATR not ready");
                return;
            }

            if (ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND RMA - EMA indicators not ready");
                return;
            }

            try
            {
                // M1-B: Orchestrator pattern - delegates to focused helpers (CYC 31 -> <=5)
                var levels = CalculateTrendSplitLevels(contracts);
                var brackets = SubmitTrendSplitBrackets(levels);
                if (brackets == null)
                    return; // Null-abort from bracket submission
                FinalizeTrendSplitEntry(levels, brackets);
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTrendSplitEntry: " + ex.Message);
            }
        }

        // M1-B Helper: Calculate EMA9/EMA15 split levels and quantities
        private TrendSplitLevels CalculateTrendSplitLevels(int contracts)
        {
            // Logic: EMA 9 vs EMA 15 alignment determines trend direction.
            double e9 = Instrument.MasterInstrument.RoundToTickSize(ema9[0]);
            double e15 = Instrument.MasterInstrument.RoundToTickSize(ema15[0]);
            bool isLongTrend = e9 > e15;
            MarketPosition direction = isLongTrend ? MarketPosition.Long : MarketPosition.Short;
            OrderAction entryAction = isLongTrend ? OrderAction.Buy : OrderAction.SellShort;

            // TREND_RMA is risk-sized from MaxRiskAmount (default $200), then split across EMA9/EMA15.
            // V12.1101E [B-1]: Decouple per-leg multipliers -- mirror the standard TREND entry logic.
            // E1 (EMA9 leg) uses TRENDEntry1ATRMultiplier; E2 (EMA15 leg) uses TRENDEntry2ATRMultiplier.
            // When isTrendRmaMode is ON, both legs fall back to RMAStopATRMultiplier (same as standard TREND).
            double e1Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
            double e2Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
            double stop9Dist = CalculateATRStopDistance(e1Mult); // EMA9 leg stop distance
            double stop15Dist = CalculateATRStopDistance(e2Mult); // EMA15 leg stop distance

            // totalQty extracted directly from passed in parameter (contracts) rather than dynamic calculation
            int totalQty = contracts;
            // TREND-SPLIT-FIX: Strict floor -- EMA9 gets ?Total/3?, EMA15 gets remainder.
            // Matches the (1/3, 2/3) weights in weightedStopDist; prevents risk budget overrun.
            int qty9 = Math.Max(1, totalQty / 3);
            int qty15 = Math.Max(0, totalQty - qty9);
            if (totalQty >= 2 && qty15 < 1)
            {
                qty15 = 1;
                qty9 = Math.Max(1, totalQty - qty15);
            }

            int finalTotalQty = qty9 + qty15;
            string timestamp = DateTime.Now.ToString("HHmmssffff");
            string trendGroupId = "TRMA_" + timestamp;

            return new TrendSplitLevels
            {
                E9 = e9,
                E15 = e15,
                Direction = direction,
                EntryAction = entryAction,
                Stop9Dist = stop9Dist,
                Stop15Dist = stop15Dist,
                Qty9 = qty9,
                Qty15 = qty15,
                FinalTotalQty = finalTotalQty,
                TrendGroupId = trendGroupId,
                Entry1Name = trendGroupId + "_E1",
                Entry2Name = trendGroupId + "_E2",
            };
        }

        // M1-B Helper: Submit both bracket legs (Build 981 Protocol: direct stopOrders writes preserved)
        private TrendSplitBrackets SubmitTrendSplitBrackets(TrendSplitLevels levels)
        {
            double stop1Price = Instrument.MasterInstrument.RoundToTickSize(
                levels.Direction == MarketPosition.Long ? levels.E9 - levels.Stop9Dist : levels.E9 + levels.Stop9Dist
            );
            PositionInfo pos1 = CreateTRENDPosition(
                levels.Entry1Name,
                levels.Direction,
                levels.E9,
                stop1Price,
                levels.Qty9,
                true,
                levels.TrendGroupId,
                true
            );

            List<string> masterEntryNames = new List<string> { levels.Entry1Name };

            int masterDeltaE1 = (levels.Direction == MarketPosition.Long) ? levels.Qty9 : -levels.Qty9;
            {
                var _aek966 = ExpKey(Account.Name);
                var _aed966 = (masterDeltaE1);
                Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966));
            }

            Order entryOrder1 =
                levels.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(
                        0,
                        OrderAction.Buy,
                        OrderType.Limit,
                        levels.Qty9,
                        levels.E9,
                        0,
                        "",
                        levels.Entry1Name
                    )
                    : SubmitOrderUnmanaged(
                        0,
                        OrderAction.SellShort,
                        OrderType.Limit,
                        levels.Qty9,
                        levels.E9,
                        0,
                        "",
                        levels.Entry1Name
                    );

            // A1-1/A2-1: Null-abort + stateLock wrap for E1 (Build 960 audit fix)
            if (entryOrder1 == null)
            {
                {
                    var _aek966 = ExpKey(Account.Name);
                    var _aed966 = (-masterDeltaE1);
                    Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966));
                }
                Print(
                    "[ENTRY_ABORT] TrendSplit E1 SubmitOrderUnmanaged returned null for "
                        + levels.Entry1Name
                        + ". Rolling back."
                );
                return null;
            }
            {
                var _en966 = levels.Entry1Name;
                var _p966 = pos1;
                var _eo966 = entryOrder1;
                Enqueue(ctx =>
                {
                    ctx.activePositions[_en966] = _p966;
                    ctx.entryOrders[_en966] = _eo966;
                });
            }

            if (levels.Qty15 > 0)
            {
                double stop2Price = Instrument.MasterInstrument.RoundToTickSize(
                    levels.Direction == MarketPosition.Long
                        ? levels.E15 - levels.Stop15Dist
                        : levels.E15 + levels.Stop15Dist
                );
                PositionInfo pos2 = CreateTRENDPosition(
                    levels.Entry2Name,
                    levels.Direction,
                    levels.E15,
                    stop2Price,
                    levels.Qty15,
                    false,
                    levels.TrendGroupId,
                    true
                );

                linkedTRENDEntries[levels.Entry1Name] = levels.Entry2Name;
                linkedTRENDEntries[levels.Entry2Name] = levels.Entry1Name;

                int masterDeltaE2 = (levels.Direction == MarketPosition.Long) ? levels.Qty15 : -levels.Qty15;
                {
                    var _aek966 = ExpKey(Account.Name);
                    var _aed966 = (masterDeltaE2);
                    Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966));
                }

                Order entryOrder2 =
                    levels.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(
                            0,
                            OrderAction.Buy,
                            OrderType.Limit,
                            levels.Qty15,
                            levels.E15,
                            0,
                            "",
                            levels.Entry2Name
                        )
                        : SubmitOrderUnmanaged(
                            0,
                            OrderAction.SellShort,
                            OrderType.Limit,
                            levels.Qty15,
                            levels.E15,
                            0,
                            "",
                            levels.Entry2Name
                        );

                // A1-1/A2-1: Null-abort + stateLock wrap for E2 (Build 960 audit fix)
                if (entryOrder2 == null)
                {
                    {
                        var _aek966 = ExpKey(Account.Name);
                        var _aed966 = (-masterDeltaE2);
                        Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966));
                    }
                    // Remove partnership references; HandleOrderCancelled will teardown E1 state naturally.
                    string removedPartner;
                    linkedTRENDEntries.TryRemove(levels.Entry1Name, out removedPartner);
                    linkedTRENDEntries.TryRemove(levels.Entry2Name, out removedPartner);
                    if (entryOrder1 != null && !IsOrderTerminal(entryOrder1.OrderState))
                        CancelOrderSafe(entryOrder1, null);
                    Print(
                        "[ENTRY_ABORT] TrendSplit E2 NULL -- E1 cancel issued for "
                            + levels.Entry1Name
                            + "; teardown deferred to cancel callback."
                    );
                    return null;
                }
                {
                    var _en966 = levels.Entry2Name;
                    var _p966 = pos2;
                    var _eo966 = entryOrder2;
                    Enqueue(ctx =>
                    {
                        ctx.activePositions[_en966] = _p966;
                        ctx.entryOrders[_en966] = _eo966;
                    });
                }
                masterEntryNames.Add(levels.Entry2Name);
            }

            return new TrendSplitBrackets { MasterEntryNames = masterEntryNames };
        }

        // M1-B Helper: Finalize entry with weighted calculation, logging, SIMA dispatch, and mode deactivation
        private void FinalizeTrendSplitEntry(TrendSplitLevels levels, TrendSplitBrackets brackets)
        {
            double weightedEntryPrice =
                ((levels.E9 * levels.Qty9) + (levels.E15 * levels.Qty15)) / Math.Max(1, levels.FinalTotalQty);
            weightedEntryPrice = Instrument.MasterInstrument.RoundToTickSize(weightedEntryPrice);

            Print(
                LogBuffer.Format(
                    "TREND RMA SPLIT: {0} | Qty={1} (EMA9={2}, EMA15={3}) | EMA9={4:F2} EMA15={5:F2} | Anchor={6:F2}",
                    levels.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                    levels.FinalTotalQty,
                    levels.Qty9,
                    levels.Qty15,
                    levels.E9,
                    levels.E15,
                    weightedEntryPrice
                )
            );

            if (EnableSIMA)
            {
                ExecuteSmartDispatchEntry(
                    "TREND_RMA",
                    levels.EntryAction,
                    levels.FinalTotalQty,
                    weightedEntryPrice,
                    OrderType.Limit,
                    brackets.MasterEntryNames.ToArray()
                );
            }

            DeactivateTRENDMode();
        }

        // M1-B: Data transfer objects for helper methods
        private class TrendSplitLevels
        {
            public double E9;
            public double E15;
            public MarketPosition Direction;
            public OrderAction EntryAction;
            public double Stop9Dist;
            public double Stop15Dist;
            public int Qty9;
            public int Qty15;
            public int FinalTotalQty;
            public string TrendGroupId;
            public string Entry1Name;
            public string Entry2Name;
        }

        private class TrendSplitBrackets
        {
            public List<string> MasterEntryNames;
        }

        #endregion

        #region RMA Entry Logic


        private void DeactivateRMAMode()
        {
            isRMAModeActive = false;
            isRMAButtonClicked = false;

            // V12.14: Broadcast RMA deactivation to panel
            string deactivateConfig = LogBuffer.Format(
                "CONFIG|OR|COUNT:{0};T1:{1};T1TYPE:{2};T2:{3};T2TYPE:{4};T3:{5};T3TYPE:{6};T4:{7};T4TYPE:{8};T5:{9};T5TYPE:{10};STR:{11};MAX:{12};",
                minContracts,
                Target1Value,
                ToIpcTargetMode(T1Type),
                Target2Value,
                ToIpcTargetMode(T2Type),
                Target3Value,
                ToIpcTargetMode(T3Type),
                Target4Value,
                ToIpcTargetMode(T4Type),
                Target5Value,
                ToIpcTargetMode(T5Type),
                StopMultiplier,
                MaxRiskAmount
            );
            SendResponseToRemote(deactivateConfig);
            Print("V12.14: DeactivateRMAMode - CONFIG broadcast sent");
        }

        #endregion
        #region RMA Intelligence (Phase 9.2)


        // [EPIC-CCN-13] Orchestrator pattern (CYC <= 8)
        private void MonitorRmaProximity()
        {
            var probe = LatencyProbe.Start();
            try
            {
                if (!RmaIntelligenceEnabled)
                    return;

                foreach (var kvp in entryOrders)
                {
                    if (!ShouldMonitorOrder(kvp.Value, kvp.Key, out var pos))
                    {
                        continue;
                    }

                    double distTicks = CalculateProximityDistance(pos, Close[0]);

                    if (distTicks <= RmaProximityTicks)
                    {
                        HandleProximityEntry(kvp.Key, pos, distTicks, pos.EntryPrice);
                    }
                    else if (distTicks >= RmaCancellationTicks)
                    {
                        HandleProximityExit(kvp.Key, kvp.Value, pos);
                    }
                }
            }
            finally
            {
                probe = probe.Stop();
                _histMonitorRmaProximity.Record(probe);
            }
        }

        // [EPIC-CCN-13] Helper: Validate order eligibility (CYC <= 5)
        private bool ShouldMonitorOrder(Order order, string entryName, out PositionInfo pos)
        {
            pos = null;
            if (order == null || order.OrderState != OrderState.Working)
            {
                return false;
            }

            if (!activePositions.TryGetValue(entryName, out pos) || !pos.IsRMATrade)
            {
                return false;
            }

            return true;
        }

        // [EPIC-CCN-13] Helper: Calculate distance + track closest approach (CYC <= 5)
        private double CalculateProximityDistance(PositionInfo pos, double currentPrice)
        {
            double level = pos.EntryPrice;
            double distTicks = Math.Abs(currentPrice - level) / tickSize;

            if (pos.ClosestApproachTicks <= 0)
                pos.ClosestApproachTicks = double.MaxValue;

            if (distTicks < pos.ClosestApproachTicks)
                pos.ClosestApproachTicks = distTicks;

            return distTicks;
        }

        // [EPIC-CCN-13] Helper: Handle proximity zone entry (CYC <= 5)
        private void HandleProximityEntry(string entryName, PositionInfo pos, double distTicks, double level)
        {
            if (!pos.WasInProximity)
            {
                pos.WasInProximity = true;
                pos.ProximityProbeCount++;
                Print(
                    LogBuffer.Format(
                        "[SENTINEL] Probe #{0} for {1} at {2:F1} ticks from {3:F2}",
                        pos.ProximityProbeCount,
                        entryName,
                        distTicks,
                        level
                    )
                );
            }

            Draw.Dot(this, "Prox_" + entryName, false, 0, level, Brushes.Cyan);
        }

        // [EPIC-CCN-13] Helper: Handle proximity zone exit + exhaustion (CYC <= 5)
        private void HandleProximityExit(string entryName, Order order, PositionInfo pos)
        {
            if (pos.WasInProximity)
            {
                pos.WasInProximity = false;

                if (RmaExhaustionEnabled && pos.ProximityProbeCount >= RmaMaxProbeCount)
                {
                    Print(
                        LogBuffer.Format(
                            "[SENTINEL] EXHAUSTION: {0} probed {1}x (max={2}), closest={3:F1}t. Cancelling.",
                            entryName,
                            pos.ProximityProbeCount,
                            RmaMaxProbeCount,
                            pos.ClosestApproachTicks
                        )
                    );
                    CancelOrderSafe(order, pos);
                    RemoveDrawObject("Prox_" + entryName);
                    SendResponseToRemote("SOUND|SENTINEL_EXHAUSTION_CANCEL");
                }
                else
                {
                    Print(
                        LogBuffer.Format(
                            "[SENTINEL] Retreat for {0} (probe #{1}, closest={2:F1}t). Monitoring.",
                            entryName,
                            pos.ProximityProbeCount,
                            pos.ClosestApproachTicks
                        )
                    );
                    RemoveDrawObject("Prox_" + entryName);
                    SendResponseToRemote("SOUND|SENTINEL_PROXIMITY_RETREAT");
                }
            }
            else
            {
                if (GetDrawObject("Prox_" + entryName) != null)
                {
                    RemoveDrawObject("Prox_" + entryName);
                }
            }
        }

        #endregion
    }
}
