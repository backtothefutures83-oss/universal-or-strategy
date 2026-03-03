// V12.Phase7 MODULAR: FFMA Entry Node (Split from Entries.cs -- Phase 7 Partition)
// Contains: CheckFFMAConditions, ExecuteFFMAEntry, DeactivateFFMAMode,
//           ExecuteFFMALimitEntry, ExecuteFFMAManualMarketEntry
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
        #region FFMA Entry Logic (V8.7)

        /// <summary>
        /// V8.7: Check FFMA conditions and execute on reversal candle
        /// SHORT: RSI > 80 + price 10+ pts above 9 EMA + RED candle
        /// LONG: RSI < 20 + price 10+ pts below 9 EMA + GREEN candle
        /// </summary>
        private void CheckFFMAConditions()
        {
            if (!isFFMAModeArmed || !FFMAEnabled) return;
            if (ema9 == null || rsiIndicator == null || currentATR <= 0) return;
            if (CurrentBar < 20) return;

            try
            {
                double ema9Value = ema9[0];
                double rsiValue = rsiIndicator[0];
                double currentPrice = Close[0];
                double distanceFromEMA = currentPrice - ema9Value;

                bool isGreenCandle = Close[0] > Open[0];
                bool isRedCandle = Close[0] < Open[0];

                // SHORT SETUP: RSI > 80 + Price far ABOVE EMA + RED reversal candle
                if (rsiValue > FFMARSIOverbought && distanceFromEMA >= FFMAEMADistance && isRedCandle)
                {
                    Print(string.Format("FFMA SHORT TRIGGERED: RSI={0:F1} > {1} | Distance={2:F2}pts > {3}pts | RED candle",
                        rsiValue, FFMARSIOverbought, distanceFromEMA, FFMAEMADistance));
                    ExecuteFFMAEntry(MarketPosition.Short);
                    return;
                }

                // LONG SETUP: RSI < 20 + Price far BELOW EMA + GREEN reversal candle
                if (rsiValue < FFMARSIOversold && distanceFromEMA <= -FFMAEMADistance && isGreenCandle)
                {
                    Print(string.Format("FFMA LONG TRIGGERED: RSI={0:F1} < {1} | Distance={2:F2}pts (below by {3}pts) | GREEN candle",
                        rsiValue, FFMARSIOversold, distanceFromEMA, FFMAEMADistance));
                    ExecuteFFMAEntry(MarketPosition.Long);
                    return;
                }
            }
            catch (Exception ex)
            {
                Print("ERROR CheckFFMAConditions: " + ex.Message);
            }
        }

        /// <summary>
        /// V8.7: Execute FFMA market order with entry candle high/low as stop
        /// Uses same target system as RMA (T1-T5)
        /// </summary>
        private void ExecuteFFMAEntry(MarketPosition direction)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate -- abort if drawdown or daily cap breached.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            try
            {
                double entryPrice = Close[0];  // Market order at current price

                // Stop at entry candle high (short) or low (long)
                double stopPrice = direction == MarketPosition.Long ? Low[0] : High[0];
                double stopDistance = Math.Min(Math.Abs(entryPrice - stopPrice), MaximumStop); // V8.31: Use MaximumStop

                // Validate stop distance
                if (stopDistance < tickSize * 2)
                {
                    Print(string.Format("FFMA: Stop too tight ({0:F2}pts) - using 2 tick minimum", stopDistance));
                    stopPrice = direction == MarketPosition.Long
                        ? entryPrice - (tickSize * 2)
                        : entryPrice + (tickSize * 2);
                    stopDistance = tickSize * 2;
                }

                // V12.Hardening: Final stop-distance guard -- prevent CalculatePositionSize(0) -> ? contracts
                if (stopDistance <= 0)
                {
                    Print("[FFMA REJECT] Stop distance is zero (doji candle or tickSize=0). Aborting entry.");
                    return;
                }

                // V12.Phase6 [TICK-01]: Round all prices to valid tick increments before order submission
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);

                // Universal Ladder: T(n)Type dropdown drives all target pricing.
                double target1Price = CalculateTargetPrice(direction, entryPrice, 1);
                double target2Price = CalculateTargetPrice(direction, entryPrice, 2);
                double target3Price = CalculateTargetPrice(direction, entryPrice, 3);
                double target4Price = CalculateTargetPrice(direction, entryPrice, 4);
                double target5Price = CalculateTargetPrice(direction, entryPrice, 5);

                // Calculate position size based on ATR stop
                int contracts = CalculatePositionSize(stopDistance);

                // 5-target distribution
                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string signalName = direction == MarketPosition.Long ? "FFMALong" : "FFMAShort";
                string entryName = signalName + "_" + timestamp;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    T5Contracts = t5Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    Target4Price = target4Price,
                    Target5Price = target5Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    EntryOrderType = OrderType.Market,
                    IsRMATrade = false,
                    IsFFMATrade = true,
                    // Build 936 [FIX-2]: Deterministic OCO group ID for broker-native bracket protection.
                    OcoGroupId = "V12_" + entryName.GetHashCode().ToString("X8")
                };
                activePositions[entryName] = pos;

                // V12.13-D: Notify connected panel clients of position entry
                string syncMsg = string.Format("POSITION_ENTERED|FFMA|{0}", contracts);
                SendResponseToRemote(syncMsg);


                // Submit MARKET order (immediate execution)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contracts, 0, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, contracts, 0, 0, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(string.Format("FFMA MARKET ORDER: {0} {1}@MARKET | Stop: {2:F2} (candle {3})",
                    signalName, contracts, stopPrice, direction == MarketPosition.Long ? "low" : "high"));
                Print(string.Format("FFMA TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2} | T4:{6}@{7:F2} | T5:{8}@{9:F2} (Runner targets trail-only)",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price, t4Qty, target4Price, t5Qty, target5Price));

                // V12 SIMA: Dispatch to fleet (replaces legacy slave broadcast)
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("FFMA", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Market, entryName);
                }

                // Disarm FFMA after execution (one-shot)
                DeactivateFFMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteFFMAEntry: " + ex.Message);
            }
        }

        private void DeactivateFFMAMode()
        {
            isFFMAModeArmed = false;
            // V12.24: Notify panel to reset FFMA Smart Toggle visual
            SendResponseToRemote("FFMA_DISARMED");
            Print("V12.24: FFMA disarmed -- sent FFMA_DISARMED to panel");
        }

        #endregion

        #region FFMA Manual Entry Methods (V12.27)

        /// <summary>
        /// V12.27: FFMA manual entry using Limit Order at user-specified price.
        /// Uses ATR-based stop (same as standard FFMA but with Limit instead of Market).
        /// </summary>
        private void ExecuteFFMALimitEntry(double manualPrice, MarketPosition direction)
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (currentATR <= 0)
            {
                Print("V12.27 FFMA_LIMIT: Ignored - ATR not available");
                return;
            }

            try
            {
                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(manualPrice);

                // V12.27: ATR-based stop (mirrors standard FFMA but won't use candle high/low since manual)
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
                double stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance);

                if (stopDistance < tickSize * 2)
                {
                    Print(string.Format("V12.27 FFMA_LIMIT: Stop too tight ({0:F2}pts) - using 2 tick minimum", stopDistance));
                    stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                        ? entryPrice - (tickSize * 2)
                        : entryPrice + (tickSize * 2));
                    stopDistance = tickSize * 2;
                }

                // V12.44: Final stop-distance guard -- prevent CalculatePositionSize(0) -> ? contracts
                if (stopDistance <= 0)
                {
                    Print("[FFMA_LIMIT REJECT] Stop distance is zero after ATR calc. Aborting entry.");
                    return;
                }

                // Universal Ladder: T(n)Type dropdown drives all target pricing.
                double target1Price = CalculateTargetPrice(direction, entryPrice, 1);
                double target2Price = CalculateTargetPrice(direction, entryPrice, 2);
                double target3Price = CalculateTargetPrice(direction, entryPrice, 3);
                double target4Price = CalculateTargetPrice(direction, entryPrice, 4);
                double target5Price = CalculateTargetPrice(direction, entryPrice, 5);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string signalName = direction == MarketPosition.Long ? "FFMAMnlLong" : "FFMAMnlShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    T5Contracts = t5Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    Target4Price = target4Price,
                    Target5Price = target5Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    EntryOrderType = OrderType.Limit,
                    IsRMATrade = false,
                    IsFFMATrade = true,
                    // Build 936 [FIX-2]: Deterministic OCO group ID for broker-native bracket protection.
                    OcoGroupId = "V12_" + entryName.GetHashCode().ToString("X8")
                };
                activePositions[entryName] = pos;

                // V12.27: Submit LIMIT order (not Market like standard FFMA)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);
                entryOrders[entryName] = entryOrder;

                Print(string.Format("V12.27 FFMA_LIMIT: {0} {1}@{2:F2} LIMIT | Stop: {3:F2} | ATR-based",
                    direction, contracts, entryPrice, stopPrice));
                Print(string.Format("V12.27 FFMA_LIMIT TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2} | T4:{6}@{7:F2} | T5:{8}@{9:F2}",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price, t4Qty, target4Price, t5Qty, target5Price));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("FFMA_MNL", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Limit, entryName);
                }

                DeactivateFFMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteFFMALimitEntry: " + ex.Message);
            }
        }

        /// <summary>
        /// V12.27: FFMA Manual Market entry -- instant market order, direction toward 9 EMA.
        /// Stop at entry candle high/low (same as Auto FFMA).
        /// </summary>
        private void ExecuteFFMAManualMarketEntry()
        {
            // V12.Phase7 [C-09]: Compliance enforcement gate.
            if (!IsOrderAllowed()) return;
            // V12.Phase6 [FLATTEN-GUARD]: Prevent order submission during active flatten
            if (isFlattenRunning) return;

            if (currentATR <= 0)
            {
                Print("V12.27 FFMA_MANUAL_MARKET: Ignored - ATR not available");
                return;
            }

            if (ema9 == null)
            {
                Print("V12.27 FFMA_MANUAL_MARKET: Ignored - EMA9 not initialized");
                return;
            }

            try
            {
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                double ema9Value = ema9[0];

                // V12.27: Direction always toward 9 EMA
                // Price below EMA9 = LONG (price moving up toward EMA)
                // Price above EMA9 = SHORT (price moving down toward EMA)
                MarketPosition direction;
                if (currentPrice < ema9Value)
                {
                    direction = MarketPosition.Long;
                    Print(string.Format("V12.27 FFMA_MANUAL_MARKET: Price below EMA9 ({0:F2} < {1:F2}) = LONG toward EMA",
                        currentPrice, ema9Value));
                }
                else
                {
                    direction = MarketPosition.Short;
                    Print(string.Format("V12.27 FFMA_MANUAL_MARKET: Price above EMA9 ({0:F2} > {1:F2}) = SHORT toward EMA",
                        currentPrice, ema9Value));
                }

                double entryPrice = currentPrice; // Market order

                // Stop at entry candle high/low (same as Auto FFMA)
                double stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long ? Low[0] : High[0]);
                double stopDistance = Math.Min(Math.Abs(entryPrice - stopPrice), MaximumStop);

                if (stopDistance < tickSize * 2)
                {
                    Print(string.Format("V12.27 FFMA_MANUAL_MARKET: Stop too tight ({0:F2}pts) - using 2 tick minimum", stopDistance));
                    stopPrice = Instrument.MasterInstrument.RoundToTickSize(direction == MarketPosition.Long
                        ? entryPrice - (tickSize * 2)
                        : entryPrice + (tickSize * 2));
                    stopDistance = tickSize * 2;
                }

                // V12.44: Final stop-distance guard -- prevent CalculatePositionSize(0) -> ? contracts
                if (stopDistance <= 0)
                {
                    Print("[FFMA_MANUAL_MARKET REJECT] Stop distance is zero (doji candle?). Aborting entry.");
                    return;
                }

                // Universal Ladder: T(n)Type dropdown drives all target pricing.
                double target1Price = CalculateTargetPrice(direction, entryPrice, 1);
                double target2Price = CalculateTargetPrice(direction, entryPrice, 2);
                double target3Price = CalculateTargetPrice(direction, entryPrice, 3);
                double target4Price = CalculateTargetPrice(direction, entryPrice, 4);
                double target5Price = CalculateTargetPrice(direction, entryPrice, 5);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                string signalName = direction == MarketPosition.Long ? "FFMAMnlMktLong" : "FFMAMnlMktShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    T5Contracts = t5Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    Target4Price = target4Price,
                    Target5Price = target5Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    EntryOrderType = OrderType.Market,
                    IsRMATrade = false,
                    IsFFMATrade = true,
                    // Build 936 [FIX-2]: Deterministic OCO group ID for broker-native bracket protection.
                    OcoGroupId = "V12_" + entryName.GetHashCode().ToString("X8")
                };
                activePositions[entryName] = pos;

                // Submit MARKET order (immediate execution)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contracts, 0, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, contracts, 0, 0, "", entryName);
                entryOrders[entryName] = entryOrder;

                Print(string.Format("V12.27 FFMA_MANUAL_MARKET: {0} {1}@MARKET | Stop: {2:F2} (candle {3}) | Toward EMA9={4:F2}",
                    direction, contracts, stopPrice, direction == MarketPosition.Long ? "low" : "high", ema9Value));
                Print(string.Format("V12.27 FFMA_MANUAL_MARKET TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2} | T4:{6}@{7:F2} | T5:{8}@{9:F2}",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price, t4Qty, target4Price, t5Qty, target5Price));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("FFMA_MNL_MKT", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Market, entryName);
                }

                DeactivateFFMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteFFMAManualMarketEntry: " + ex.Message);
            }
        }

        #endregion
    }
}
