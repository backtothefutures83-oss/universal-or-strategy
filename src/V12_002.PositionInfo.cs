// Build 971: V12_002 PositionInfo nested class
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
        #region Position Info Class

        private class PositionInfo
        {
            public string SignalName;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts; // v5.13: 20% - Fixed 1pt quick profit
            public int T2Contracts; // v5.13: 30% - 0.5x ATR
            public int T3Contracts; // v5.13: 30% - 1.0x ATR
            public int T4Contracts;
            public int T5Contracts;
            public int InitialTargetCount; // Build 1102Y-V2 [U-03]: activeTargetCount snapshot at entry fill time
            public volatile int RemainingContracts; // V12.1101E [SK-08]: volatile -- written from OnOrderUpdate, OnExecutionUpdate, OnBarUpdate threads
            public double EntryPrice;
            public OrderType EntryOrderType = OrderType.Market;
            public double InitialStopPrice;
            public double CurrentStopPrice;
            public double Target1Price; // v5.13: Fixed 1pt
            public double Target2Price; // v5.13: 0.5x ATR
            public double Target3Price; // v5.13: 1.0x ATR
            public double Target4Price;
            public double Target5Price;
            public bool EntryFilled;
            public bool T1Filled;
            public bool T2Filled;
            public bool T3Filled; // v5.13: New flag
            public bool T4Filled;
            public bool T5Filled;
            public int T1FilledQuantity; // V12.1101E hardening: cumulative executed quantity for partial-fill-safe accounting
            public int T2FilledQuantity;
            public int T3FilledQuantity;
            public int T4FilledQuantity;
            public int T5FilledQuantity;
            public bool BracketSubmitted;
            public double ExtremePriceSinceEntry;
            public int CurrentTrailLevel;
            public bool IsRMATrade; // Flag to identify RMA trades
            public bool ManualBreakevenArmed; // Manual breakeven button clicked
            public bool ManualBreakevenTriggered; // Manual breakeven has executed
            public int TicksSinceEntry; // v5.13: Tick counter for frequency-based trailing

            // V8.2: TREND trade tracking
            public bool IsTRENDTrade; // Flag for TREND trades
            public bool IsTRENDEntry1; // True if this is the 9 EMA entry (1/3)
            public bool IsTRENDEntry2; // True if this is the 15 EMA entry (2/3)
            public string LinkedTRENDGroup; // Links Entry1 and Entry2 together
            public bool Entry1TrailActivated; // V8.2: True when E1 switches from fixed stop to EMA9 trail

            // V8.4: RETEST trade tracking
            public bool IsRetestTrade; // Flag for RETEST trades
            public bool RetestTrailActivated; // V8.4: True when retest switches from fixed stop to 9 EMA trail

            // V8.6: MOMO trade tracking
            public bool IsMOMOTrade; // Flag for MOMO trades

            // V8.7: FFMA trade tracking
            public bool IsFFMATrade; // Flag for FFMA trades

            // V12.1: SIMA Multi-Account tracking
            public Account ExecutingAccount; // The account this position belongs to (null = Master)
            public bool IsFollower; // True if this is a SIMA follower position

            // Build 936 [FIX-2]: Broker-level OCO group ID linking stop + all targets into a protective bracket.
            // Set at position creation using entryName hash -- deterministic within session.
            // Broker (Rithmic/Apex/Tradovate) uses this to cancel remaining orders when one fills,
            // protecting the position natively even during NT8 restarts.
            public string OcoGroupId;

            // Build 960 [A2-2]: Deferred metadata purge -- set true when stop cancel is requested on
            // final-target/trim close. Actual activePositions.TryRemove deferred to OnAccountOrderUpdate
            // or HandleOrderCancelled when broker confirms stop terminal state.
            public bool PendingCleanup;

            // Build 960 [A3-3]: Circuit breaker counter for emergency flatten attempts from null stop submit.
            // Incremented each call to FlattenPositionByName triggered by null stop. Halts after 3 failures.
            public int FlattenAttemptCount;

            // Phase 9.2 [PROBE]: RMA Proximity Intelligence -- stateful probe tracking.
            // Draw.Dot remains visual-only; these fields are the logic source of truth.
            public bool WasInProximity;
            public int ProximityProbeCount;
            public double ClosestApproachTicks;
        }

        private TargetMode GetTargetMode(int targetNumber)
        {
            switch (targetNumber)
            {
                case 1:
                    return T1Type;
                case 2:
                    return T2Type;
                case 3:
                    return T3Type;
                case 4:
                    return T4Type;
                case 5:
                    return T5Type;
                default:
                    return TargetMode.ATR;
            }
        }

        private bool IsRunnerTarget(int targetNumber)
        {
            return GetTargetMode(targetNumber) == TargetMode.Runner;
        }

        // Universal Ladder: single-arg magnitude lookup -- T(n)Value is the sole source of truth.
        private double GetConfiguredTargetMagnitude(int targetNumber)
        {
            switch (targetNumber)
            {
                case 1:
                    return Target1Value;
                case 2:
                    return Target2Value;
                case 3:
                    return Target3Value;
                case 4:
                    return Target4Value;
                case 5:
                    return Target5Value;
                default:
                    return 0.0;
            }
        }

        // Universal Ladder: single pricing oracle -- reads T(n)Type + Target(n)Value, no role branching.
        // CYC = 8 (was 9: inlined offset calculation, removed one ternary)
        private double CalculateTargetPrice(MarketPosition direction, double entryPrice, int targetNumber)
        {
            TargetMode mode = GetTargetMode(targetNumber);
            if (mode == TargetMode.Runner)
                return Instrument.MasterInstrument.RoundToTickSize(entryPrice);

            double value = GetConfiguredTargetMagnitude(targetNumber);
            if (value <= 0)
            {
                Print(
                    $"[PRICE_GUARD] T{targetNumber} value={value:F4} is non-positive. Using MinimumStop fallback to prevent price inversion."
                );
                value = MinimumStop;
            }

            double offset = value; // Default: Points mode
            if (mode == TargetMode.ATR)
                offset = currentATR > 0 ? currentATR * value : value;
            else if (mode == TargetMode.Ticks)
                offset = value * tickSize;

            double rawPrice = direction == MarketPosition.Long ? entryPrice + offset : entryPrice - offset;
            return Instrument.MasterInstrument.RoundToTickSize(rawPrice);
        }

        /// <summary>
        /// Build 1102Y-V3 [LG-01]: Target Ladder Guard -- "The Staircase Rule."
        /// Iterates T1 ? T5 and ensures every rung is at least one tick FURTHER from entry
        /// than the rung before it. In low volatility the ATR-based T2 can be tighter than
        /// the fixed Scalp (T1), causing price inversion and incorrect order slotting.
        /// Call this after computing target prices and again after fill-price re-anchoring.
        /// Slots that are zero (unused/runner) are skipped.
        /// CYC = 8 (was 11: combined continue checks, inlined inversion logic)
        /// </summary>
        private void ApplyTargetLadderGuard(PositionInfo pos)
        {
            if (pos == null)
                return;
            bool isLong = pos.Direction == MarketPosition.Long;

            double[] prices = new double[]
            {
                pos.Target1Price,
                pos.Target2Price,
                pos.Target3Price,
                pos.Target4Price,
                pos.Target5Price,
            };

            bool anyFixed = false;
            for (int i = 1; i < prices.Length; i++)
            {
                // Skip if current or previous slot is unused/runner
                if (prices[i] <= 0 || prices[i - 1] <= 0)
                    continue;

                double minValid = isLong ? prices[i - 1] + tickSize : prices[i - 1] - tickSize;
                bool inverted = isLong ? (prices[i] < minValid) : (prices[i] > minValid);

                if (inverted)
                {
                    double fixedPrice = Instrument.MasterInstrument.RoundToTickSize(minValid);
                    Print(
                        string.Format(
                            "[LADDER_GUARD] T{0}={1:F4} is inside T{2}={3:F4} for {4}. Pushing T{0} to {5:F4}.",
                            i + 1,
                            prices[i],
                            i,
                            prices[i - 1],
                            pos.SignalName,
                            fixedPrice
                        )
                    );
                    prices[i] = fixedPrice;
                    anyFixed = true;
                }
            }

            pos.Target1Price = prices[0];
            pos.Target2Price = prices[1];
            pos.Target3Price = prices[2];
            pos.Target4Price = prices[3];
            pos.Target5Price = prices[4];

            if (anyFixed)
                Print(
                    string.Format(
                        "[LADDER_GUARD] Ladder corrected for {0}: T1={1:F4} T2={2:F4} T3={3:F4} T4={4:F4} T5={5:F4}",
                        pos.SignalName,
                        pos.Target1Price,
                        pos.Target2Price,
                        pos.Target3Price,
                        pos.Target4Price,
                        pos.Target5Price
                    )
                );
        }

        // Universal Ladder: pure delegation -- T(n)Type dropdown drives all pricing for all trade types.
        private double CalculateTargetPriceFromPos(
            MarketPosition direction,
            double entryPrice,
            PositionInfo pos,
            int targetNumber
        )
        {
            return CalculateTargetPrice(direction, entryPrice, targetNumber);
        }

        // Switch-based accessors (V12 DNA: Zero-allocation hot paths)
        // CYC = 11 each (acceptable per Jane Street threshold <=15)
        // Reverted from array-based to eliminate heap allocation per AMAL harness requirement
        private int GetTargetContracts(PositionInfo pos, int targetNumber) =>
            targetNumber == 1 ? pos.T1Contracts
            : targetNumber == 2 ? pos.T2Contracts
            : targetNumber == 3 ? pos.T3Contracts
            : targetNumber == 4 ? pos.T4Contracts
            : targetNumber == 5 ? pos.T5Contracts
            : 0;

        private double GetTargetPrice(PositionInfo pos, int targetNumber) =>
            targetNumber == 1 ? pos.Target1Price
            : targetNumber == 2 ? pos.Target2Price
            : targetNumber == 3 ? pos.Target3Price
            : targetNumber == 4 ? pos.Target4Price
            : targetNumber == 5 ? pos.Target5Price
            : 0.0;

        private bool IsTargetFilled(PositionInfo pos, int targetNumber) =>
            targetNumber == 1 ? pos.T1Filled
            : targetNumber == 2 ? pos.T2Filled
            : targetNumber == 3 ? pos.T3Filled
            : targetNumber == 4 ? pos.T4Filled
            : targetNumber == 5 ? pos.T5Filled
            : false;

        private void MarkTargetFilled(PositionInfo pos, int targetNumber)
        {
            if (targetNumber < 1 || targetNumber > 5)
                return;
            if (targetNumber == 1)
            {
                pos.T1Filled = true;
            }
            else if (targetNumber == 2)
            {
                pos.T2Filled = true;
            }
            else if (targetNumber == 3)
            {
                pos.T3Filled = true;
            }
            else if (targetNumber == 4)
            {
                pos.T4Filled = true;
            }
            else if (targetNumber == 5)
            {
                pos.T5Filled = true;
            }
        }

        private int GetTargetFilledQuantity(PositionInfo pos, int targetNumber) =>
            targetNumber == 1 ? pos.T1FilledQuantity
            : targetNumber == 2 ? pos.T2FilledQuantity
            : targetNumber == 3 ? pos.T3FilledQuantity
            : targetNumber == 4 ? pos.T4FilledQuantity
            : targetNumber == 5 ? pos.T5FilledQuantity
            : 0;

        private void SetTargetFilledQuantity(PositionInfo pos, int targetNumber, int filledQuantity)
        {
            if (targetNumber < 1 || targetNumber > 5)
                return;
            int safeQty = Math.Max(0, filledQuantity);
            if (targetNumber == 1)
            {
                pos.T1FilledQuantity = safeQty;
            }
            else if (targetNumber == 2)
            {
                pos.T2FilledQuantity = safeQty;
            }
            else if (targetNumber == 3)
            {
                pos.T3FilledQuantity = safeQty;
            }
            else if (targetNumber == 4)
            {
                pos.T4FilledQuantity = safeQty;
            }
            else if (targetNumber == 5)
            {
                pos.T5FilledQuantity = safeQty;
            }
        }

        // V8.11: Struct to track pending stop replacements (V12 Round 11: converted from class for zero-allocation)
        // V8.30: Added CreatedTime for timeout support
        // V12 Round 11: Converted to struct to eliminate heap allocation in hot path (Jane Street principle)
        // WARNING: Mutable struct - copy-by-value semantics apply. Do NOT mutate properties after
        // retrieving from ConcurrentDictionary. Always re-insert a new instance to update state.
        private struct PendingStopReplacement
        {
            public string EntryName { get; set; }

            public int Quantity { get; set; }

            public double StopPrice { get; set; }

            public MarketPosition Direction { get; set; }

            public Order OldOrder { get; set; } // Track the old order being cancelled

            public DateTime CreatedTime { get; set; } // V8.30: Timeout support - clean up stale replacements

            // Build 950: Bracket restoration -- populated before stop cancel is sent.
            public TargetSnapshot[] CapturedTargets { get; set; } // null if no Working targets at cancel time

            public bool BracketRestorationNeeded { get; set; } // true when CapturedTargets is non-null
        }

        // V8.22: Thread-Safe UI Snapshot Struct
        // Decouples UI thread from Strategy thread to prevent "Collection moved" or race conditions
        public struct PositionDisplayInfo
        {
            public string TradeType;
            public string Direction;
            public double EntryPrice;
            public double StopPrice;
            public int RemainingContracts;
            public bool EntryFilled;
            public bool ManualBreakevenArmed;
            public bool ManualBreakevenTriggered;
        }

        #endregion
    }
}
