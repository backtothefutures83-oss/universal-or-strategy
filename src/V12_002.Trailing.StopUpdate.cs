// Build 971: Trailing.StopUpdate -- CleanupStalePendingReplacements, UpdateStopOrder, CalculateStopForLevel
// V12 Trailing Module (Extracted)
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
        #region Trailing Stop Update

        private void CleanupStalePendingReplacements()
        {
            DateTime now = DateTime.Now;

            // V8.30: Safe iteration with snapshot
            foreach (var kvp in pendingStopReplacements.ToArray())
            {
                if ((now - kvp.Value.CreatedTime).TotalSeconds > 5)
                {
                    if (pendingStopReplacements.TryRemove(kvp.Key, out var pending))
                    {
                        Interlocked.Decrement(ref pendingReplacementCount);
                        Print(string.Format("V8.30: Stale pending replacement REMOVED for {0} (>5sec old)", kvp.Key));

                        // If position still exists and needs protection, create emergency stop
                        if (activePositions.TryGetValue(kvp.Key, out var pos) && pos.EntryFilled && pos.RemainingContracts > 0)
                        {
                            Print(string.Format("[1104.2] Recovery: force-initiating stop for {0}", kvp.Key));
                            // V12.1101E [F-02]: Use live RemainingContracts under stateLock instead of stale pending.Quantity
                            int replacementQty;
                            replacementQty = pos.RemainingContracts;
                            CreateNewStopOrder(kvp.Key, replacementQty, pending.StopPrice, pending.Direction, isRecovery: true);
                            // Build 950: Also restore bracket targets after V8.30 emergency stop.
                            if (pending.BracketRestorationNeeded && pending.CapturedTargets != null)
                            {
                                TargetSnapshot[] _tSnap = pending.CapturedTargets;
                                string _tKey = kvp.Key;
                                TriggerCustomEvent(o => RestoreCascadedTargets(_tKey, _tSnap), null);
                            }
                        }
                    }
                }
            }
        }

        // V12.44: ChangeStop() removed -- dead code, only caller was MoveStopsToBreakevenPlusOne (also removed)

        private void UpdateStopOrder(string entryName, PositionInfo pos, double newStopPrice, int newTrailLevel)
        {
            // V8.30: Thread-safe check using TryGetValue
            if (!stopOrders.TryGetValue(entryName, out var currentStop)) return;

            try
            {
                double validatedStopPrice = ValidateStopPrice(pos.Direction, newStopPrice, newTrailLevel, pos.EntryPrice);

                // Check for stale pending replacement
                if (pendingStopReplacements.TryGetValue(entryName, out var existingPending))
                {
                    double pendingAgeSeconds = (DateTime.Now - existingPending.CreatedTime).TotalSeconds;
                    if (pendingAgeSeconds > STALE_PENDING_FAST_PATH_SEC)
                    {
                        HandleStalePendingReplacement(entryName, pos, validatedStopPrice, newTrailLevel);
                        return;
                    }
                }

                // Route to appropriate handler based on order state
                if (currentStop != null && (currentStop.OrderState == OrderState.CancelPending || currentStop.OrderState == OrderState.Submitted))
                {
                    UpdateExistingPendingReplacement(entryName, pos, currentStop, validatedStopPrice, newTrailLevel);
                    return;
                }

                if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                {
                    InitiateStopReplacement(entryName, pos, currentStop, validatedStopPrice, newTrailLevel);
                    return;
                }

                // No existing stop or not in a cancellable state - create directly
                CreateDirectStopOrder(entryName, pos, validatedStopPrice, newTrailLevel);
            }
            catch (Exception ex)
            {
                HandleUpdateException(entryName, pos, ex);
            }
        }

        private void HandleStalePendingReplacement(string entryName, PositionInfo pos, double validatedStopPrice, int newTrailLevel)
        {
            if (pendingStopReplacements.TryRemove(entryName, out var existingPending))
            {
                Interlocked.Decrement(ref pendingReplacementCount);
                double pendingAgeSeconds = (DateTime.Now - existingPending.CreatedTime).TotalSeconds;
                Print(string.Format("[1104.2] Stale pending purged for {0} ({1:F1}s). Re-initiating stop move.",
                    entryName, pendingAgeSeconds));
            }

            // Update position state
            pos.CurrentStopPrice = validatedStopPrice;
            pos.CurrentTrailLevel = newTrailLevel;
            MarkStickyDirty();
        }

        private void UpdateExistingPendingReplacement(string entryName, PositionInfo pos, Order currentStop, double validatedStopPrice, int newTrailLevel)
        {
            // Build 955: Snapshot targets BEFORE TryAdd so any callback sees a fully-initialized record
            var _b955TargetsA = CaptureTargetSnapshot(entryName);

            var newPending = new PendingStopReplacement
            {
                EntryName                 = entryName,
                Quantity                  = pos.RemainingContracts,
                StopPrice                 = validatedStopPrice,
                Direction                 = pos.Direction,
                OldOrder                  = currentStop,
                CreatedTime               = DateTime.Now,
                CapturedTargets           = _b955TargetsA,
                BracketRestorationNeeded  = _b955TargetsA != null && _b955TargetsA.Length > 0
            };

            // V8.30: Thread-safe add or update
            if (pendingStopReplacements.TryAdd(entryName, newPending))
            {
                int currentCount = Interlocked.Increment(ref pendingReplacementCount);
                if (currentCount >= CIRCUIT_BREAKER_THRESHOLD && !circuitBreakerActive)
                {
                    circuitBreakerActive = true;
                    circuitBreakerActivatedTime = DateTime.Now;
                    Print(string.Format("V8.30: CIRCUIT BREAKER ACTIVATED - {0} pending replacements (threshold: {1})",
                        currentCount, CIRCUIT_BREAKER_THRESHOLD));
                }
            }
            else if (pendingStopReplacements.TryGetValue(entryName, out var pending))
            {
                // Just update the pending price
                pending.StopPrice = validatedStopPrice;
                // Build 950: Refresh CapturedTargets on the live pending record if not yet populated
                if (!pending.BracketRestorationNeeded)
                {
                    var _b950Refresh = RefreshTargetSnapshot(entryName);
                    pending.CapturedTargets = _b950Refresh;
                    pending.BracketRestorationNeeded = _b950Refresh != null && _b950Refresh.Length > 0;
                }
            }

            pos.CurrentStopPrice = validatedStopPrice;
            pos.CurrentTrailLevel = newTrailLevel;
            MarkStickyDirty();
            Print(string.Format("V8.12: Stop update queued for {0} (current state: {1})", entryName, currentStop.OrderState));
        }

        private TargetSnapshot[] CaptureTargetSnapshot(string entryName)
        {
            var _b955TargetsA = new System.Collections.Generic.List<TargetSnapshot>();
            for (int _tA = 1; _tA <= 5; _tA++)
            {
                var _tDA = GetTargetOrdersDictionary(_tA);
                Order _tOA;
                if (_tDA != null && _tDA.TryGetValue(entryName, out _tOA) && _tOA != null
                    && (_tOA.OrderState == OrderState.Working || _tOA.OrderState == OrderState.Accepted))
                    _b955TargetsA.Add(new TargetSnapshot { TargetNum = _tA, Price = _tOA.LimitPrice, Qty = _tOA.Quantity, CapturedOrder = _tOA });
            }
            return _b955TargetsA.Count > 0 ? _b955TargetsA.ToArray() : null;
        }

        private TargetSnapshot[] RefreshTargetSnapshot(string entryName)
        {
            var _b950Refresh = new System.Collections.Generic.List<TargetSnapshot>();
            for (int _t2 = 1; _t2 <= 5; _t2++)
            {
                var _tD2 = GetTargetOrdersDictionary(_t2);
                Order _tO2;
                if (_tD2 != null && _tD2.TryGetValue(entryName, out _tO2) && _tO2 != null
                    && (_tO2.OrderState == OrderState.Working || _tO2.OrderState == OrderState.Accepted))
                    _b950Refresh.Add(new TargetSnapshot { TargetNum = _t2, Price = _tO2.LimitPrice, Qty = _tO2.Quantity, CapturedOrder = _tO2 });
            }
            return _b950Refresh.Count > 0 ? _b950Refresh.ToArray() : null;
        }

        private void InitiateStopReplacement(string entryName, PositionInfo pos, Order currentStop, double validatedStopPrice, int newTrailLevel)
        {
            // Build 955: Snapshot targets BEFORE TryAdd so any callback sees a fully-initialized record
            var _b955TargetsB = new System.Collections.Generic.List<TargetSnapshot>();
            for (int _tB = 1; _tB <= 5; _tB++)
            {
                var _tDB = GetTargetOrdersDictionary(_tB);
                Order _tOB;
                if (_tDB != null && _tDB.TryGetValue(entryName, out _tOB) && _tOB != null
                    && (_tOB.OrderState == OrderState.Working || _tOB.OrderState == OrderState.Accepted))
                    _b955TargetsB.Add(new TargetSnapshot { TargetNum = _tB, Price = _tOB.LimitPrice, Qty = _tOB.Quantity, CapturedOrder = _tOB });
            }

            var newPending = new PendingStopReplacement
            {
                EntryName                 = entryName,
                Quantity                  = pos.RemainingContracts,
                StopPrice                 = validatedStopPrice,
                Direction                 = pos.Direction,
                OldOrder                  = currentStop,
                CreatedTime               = DateTime.Now,
                CapturedTargets           = _b955TargetsB.Count > 0 ? _b955TargetsB.ToArray() : null,
                BracketRestorationNeeded  = _b955TargetsB.Count > 0
            };

            // V8.30: Thread-safe add
            if (pendingStopReplacements.TryAdd(entryName, newPending))
            {
                int currentCount = Interlocked.Increment(ref pendingReplacementCount);
                if (currentCount >= CIRCUIT_BREAKER_THRESHOLD && !circuitBreakerActive)
                {
                    circuitBreakerActive = true;
                    circuitBreakerActivatedTime = DateTime.Now;
                    Print(string.Format("V8.30: CIRCUIT BREAKER ACTIVATED - {0} pending replacements", currentCount));
                }
            }

            CancelOrderForReplace(currentStop, pos);
            pos.CurrentStopPrice = validatedStopPrice;
            pos.CurrentTrailLevel = newTrailLevel;
            MarkStickyDirty();

            string levelName = newTrailLevel <= 0 ? "Initial" : (newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1));
            Print(string.Format("STOP UPDATED: {0} -> {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName));
        }

        private void CreateDirectStopOrder(string entryName, PositionInfo pos, double validatedStopPrice, int newTrailLevel)
        {
            Order newStop = null;

            if (pos.ExecutingAccount != null)
            {
                newStop = pos.ExecutingAccount.CreateOrder(Instrument, pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                    OrderType.StopMarket, TimeInForce.Gtc, pos.RemainingContracts, 0, validatedStopPrice, "Stop_" + entryName, "Stop_" + entryName, null);
                pos.ExecutingAccount.Submit(new[] { newStop });
                // A1-1: B966 -- Enqueue to flow through actor pipeline
                { var _en966 = entryName; var _ns966 = newStop; Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; }); }
            }
            else
            {
                // V12.3: Truncate signal name to stay under 50-char NinjaTrader limit
                string suffix = (DateTime.Now.Ticks % 100000000).ToString();
                string stopSigName = "S_" + entryName + "_" + suffix;
                if (stopSigName.Length > 50) stopSigName = stopSigName.Substring(0, 50);
                OrderAction stopExitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                newStop = SubmitOrderUnmanaged(0, stopExitAction, OrderType.StopMarket, pos.RemainingContracts, 0, validatedStopPrice, "", stopSigName);

                // A1-1: B966 -- Enqueue to flow through actor pipeline
                if (newStop != null) { var _en966 = entryName; var _ns966 = newStop; Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; }); }
            }

            if (newStop == null)
            {
                HandleStopSubmissionFailure(entryName, pos, validatedStopPrice);
                return;
            }

            // A3-3: Reset circuit breaker counter on successful stop submission
            {
                PositionInfo cbReset;
                if (activePositions.TryGetValue(entryName, out cbReset) && cbReset != null)
                    cbReset.FlattenAttemptCount = 0;
            }

            pos.CurrentStopPrice = validatedStopPrice;
            pos.CurrentTrailLevel = newTrailLevel;
            MarkStickyDirty();

            string levelName2 = newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1);
            Print(string.Format("STOP UPDATED: {0} -> {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName2));
        }

        private void HandleStopSubmissionFailure(string entryName, PositionInfo pos, double validatedStopPrice)
        {
            Print(string.Format("(!) CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
            Print(string.Format("(!) POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}",
                pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                pos.RemainingContracts,
                pos.EntryPrice));
            Print(string.Format("(!) Attempted stop price: {0:F2} | Current price: {1:F2}", validatedStopPrice, Close[0]));

            // A3-3: Circuit breaker -- cap consecutive flatten attempts to 3
            PositionInfo cbPos;
            bool circuitOpen = false;
            if (activePositions.TryGetValue(entryName, out cbPos) && cbPos != null)
            {
                cbPos.FlattenAttemptCount++;
                if (cbPos.FlattenAttemptCount > 3) circuitOpen = true;
                if (circuitOpen)
                {
                    Print(string.Format("[CIRCUIT BREAKER] Emergency flatten halted after 3 consecutive failures for {0}. Manual intervention required.", entryName));
                    return;
                }
            }
            Print(string.Format("(!) Attempting emergency flatten for {0}...", entryName));
            FlattenPositionByName(entryName);
        }

        private void HandleUpdateException(string entryName, PositionInfo pos, Exception ex)
        {
            Print(string.Format("(!) ERROR UpdateStopOrder for {0}: {1}", entryName, ex.Message));
            Print(string.Format("(!) POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));

            // A3-3: Circuit breaker -- cap consecutive flatten attempts to 3
            PositionInfo exCbPos;
            bool flattenBlocked = false;
            if (activePositions.TryGetValue(entryName, out exCbPos) && exCbPos != null)
            {
                exCbPos.FlattenAttemptCount++;
                if (exCbPos.FlattenAttemptCount > 3) flattenBlocked = true;
                if (flattenBlocked)
                    Print(string.Format("[CIRCUIT BREAKER] Emergency flatten halted after 3 consecutive failures for {0}. Manual intervention required.", entryName));
            }
            if (!flattenBlocked)
            {
                try
                {
                    Print(string.Format("(!) Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                }
                catch (Exception flattenEx)
                {
                    Print(string.Format("(!)(!) EMERGENCY FLATTEN FAILED: {0}", flattenEx.Message));
                }
            }
        }

        // V12.10: Fleet Symmetry -- calculates the correct stop price for a given trail level
        // using the position's own entry/extreme prices. Pure calculation, no side effects.
        private double CalculateStopForLevel(PositionInfo pos, int level)
        {
            bool isLong = (pos.Direction == MarketPosition.Long);
            switch (level)
            {
                case 1: // Breakeven
                    return isLong
                        ? pos.EntryPrice + (BreakEvenOffsetTicks * tickSize)
                        : pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);
                case 2: // Trail 1
                    return isLong
                        ? pos.ExtremePriceSinceEntry - Trail1DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail1DistancePoints;
                case 3: // Trail 2
                    return isLong
                        ? pos.ExtremePriceSinceEntry - Trail2DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail2DistancePoints;
                case 4: // Trail 3
                    return isLong
                        ? pos.ExtremePriceSinceEntry - Trail3DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail3DistancePoints;
                default:
                    return pos.CurrentStopPrice; // No change
            }
        }


        #endregion
    }
}
