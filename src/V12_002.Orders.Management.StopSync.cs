// Build 971: Orders.Management.StopSync -- RefreshActivePositionOrders, UpdateStopQuantity, CreateNewStopOrder, RestoreCascadedTargets, ValidateStopPrice [Build 971] Group >400 lines -- future refactor candidate
// V12 Orders.Management Module (Extracted)
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
        #region Orders Management Stop Sync

        private void RefreshActivePositionOrders()
        {
            if (activePositions == null || activePositions.IsEmpty)
            {
                Print("[SYNC_ALL] No active positions to refresh.");
                return;
            }

            // Snapshot under stateLock -- satisfies stateLock invariant for dict reads
            List<KeyValuePair<string, PositionInfo>> snapshot;
            snapshot = activePositions.ToList();

            int refreshed = 0;
            foreach (var kvp in snapshot)
            {
                string entryName = kvp.Key;
                PositionInfo pos = kvp.Value;

                // Guard: entry must be filled and position open
                if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;

                // Guard: skip SIMA followers -- fleet dispatch is out of scope for Phase 9.1
                if (pos.IsFollower)
                {
                    Print(string.Format("[SYNC_ALL] Skipping follower position {0}", entryName));
                    continue;
                }

                for (int targetNum = 1; targetNum <= 5; targetNum++)
                {
                    // Skip already-filled targets
                    if (IsTargetFilled(pos, targetNum)) continue;

                    int targetQty = GetTargetContracts(pos, targetNum);
                    if (targetQty <= 0) continue;

                    var targetDict = GetTargetOrdersDictionary(targetNum);
                    if (targetDict == null) continue;

                    // Check if a live limit order exists for this target slot
                    Order existingOrder = null;
                    bool hasWorkingOrder = targetDict.TryGetValue(entryName, out existingOrder) &&
                                           existingOrder != null &&
                                           (existingOrder.OrderState == OrderState.Working ||
                                            existingOrder.OrderState == OrderState.Accepted);

                    // [C-06 parity]: Skip ChangePending orders to avoid broker race
                    if (existingOrder != null && existingOrder.OrderState == OrderState.ChangePending)
                    {
                        Print(string.Format("[SYNC_ALL] T{0} {1}: ChangePending -- skipping", targetNum, entryName));
                        continue;
                    }

                    bool isNowRunner = IsRunnerTarget(targetNum);

                    if (isNowRunner)
                    {
                        // Runner targets must have NO limit order -- cancel any existing one
                        if (hasWorkingOrder)
                        {
                            try
                            {
                                CancelOrderSafe(existingOrder, pos);
                                // B957: Do NOT TryRemove from targetDict here -- the cancel is async.
                                // The broker-confirmed terminal callback will perform the removal under stateLock
                                // once confirmed, preventing premature cleanup before the cancel is acknowledged.
                                Print(string.Format("[SYNC_ALL] T{0} {1}: Limit cancel requested -> now Runner (awaiting broker confirm)", targetNum, entryName));
                                refreshed++;
                            }
                            catch (Exception ex)
                            {
                                Print(string.Format("[SYNC_ALL] T{0} {1}: CancelOrder failed -- {2}", targetNum, entryName, ex.Message));
                            }
                        }
                        continue;
                    }

                    // Limit/ATR/Ticks/Points: recalculate price from live ATR and entry
                    // Build 1102Y [P-06]: Role-aware reprice -- RMA/SIMA positions use stamped role; others use slot-based.
                    double newPrice = CalculateTargetPriceFromPos(pos.Direction, pos.EntryPrice, pos, targetNum);
                    if (newPrice <= 0)
                    {
                        Print(string.Format("[SYNC_ALL] T{0} {1}: Calculated price invalid ({2:F2}) -- skipped", targetNum, entryName, newPrice));
                        continue;
                    }

                    if (hasWorkingOrder)
                    {
                        // Shift existing limit if it moved by >= 1 tick
                        if (Math.Abs(existingOrder.LimitPrice - newPrice) >= tickSize)
                        {
                            try
                            {
                                ChangeOrder(existingOrder, existingOrder.Quantity, newPrice, 0);
                                switch (targetNum)
                                {
                                    case 1: pos.Target1Price = newPrice; break;
                                    case 2: pos.Target2Price = newPrice; break;
                                    case 3: pos.Target3Price = newPrice; break;
                                    case 4: pos.Target4Price = newPrice; break;
                                    case 5: pos.Target5Price = newPrice; break;
                                }
                                Print(string.Format("[SYNC_ALL] T{0} {1}: Repriced -> {2:F2}", targetNum, entryName, newPrice));
                                refreshed++;
                            }
                            catch (Exception ex)
                            {
                                Print(string.Format("[SYNC_ALL] T{0} {1}: ChangeOrder failed -- {2}", targetNum, entryName, ex.Message));
                            }
                        }
                        else
                        {
                            Print(string.Format("[SYNC_ALL] T{0} {1}: Price unchanged at {2:F2} -- no action", targetNum, entryName, newPrice));
                        }
                    }
                    else
                    {
                        // No working order (e.g. Runner->Limit swap): submit a fresh limit order
                        try
                        {
                            Order newLimit = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, targetQty, newPrice, 0, "", "T" + targetNum + "_" + entryName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, targetQty, newPrice, 0, "", "T" + targetNum + "_" + entryName);

                            if (newLimit != null)
                            {
                                targetDict[entryName] = newLimit;
                                switch (targetNum)
                                {
                                    case 1: pos.Target1Price = newPrice; break;
                                    case 2: pos.Target2Price = newPrice; break;
                                    case 3: pos.Target3Price = newPrice; break;
                                    case 4: pos.Target4Price = newPrice; break;
                                    case 5: pos.Target5Price = newPrice; break;
                                }
                                Print(string.Format("[SYNC_ALL] T{0} {1}: New limit submitted @ {2:F2} qty={3}", targetNum, entryName, newPrice, targetQty));
                                refreshed++;
                            }
                            else
                            {
                                Print(string.Format("[SYNC_ALL] T{0} {1}: SubmitOrderUnmanaged returned null @ {2:F2}", targetNum, entryName, newPrice));
                            }
                        }
                        catch (Exception ex)
                        {
                            Print(string.Format("[SYNC_ALL] T{0} {1}: Submit failed -- {2}", targetNum, entryName, ex.Message));
                        }
                    }
                }
            }

            Print(string.Format("[SYNC_ALL] Complete. Positions scanned: {0} | Actions taken: {1}", snapshot.Count, refreshed));
        }

        /// <summary>
        /// Updates the stop order quantity after a partial target fill.
        /// </summary>
        /// <remarks>
        /// V12.Audit [C-08]: Callers MUST ensure the <paramref name="pos"/> reference is
        /// read under <c>stateLock</c> or from within a callback that is already serialized
        /// by the NinjaTrader dispatch thread. Passing a stale <paramref name="pos"/> can
        /// result in the stop being undersized relative to actual remaining contracts.
        /// </remarks>
        private void UpdateStopQuantity(string entryName, PositionInfo pos)
        {
            // V12.Hardening [RISK-01]: Atomic update guard
            // Locks stateLock to prevent dirty reads of pos.RemainingContracts while ApplyTargetFill is modifying it
            if (!stopOrders.ContainsKey(entryName)) return;
            if (pos.RemainingContracts <= 0) return;
            // V12.41: No trailing/updates before entry fill is confirmed
            if (!pos.EntryFilled) return;

            try
            {
                Order currentStop = stopOrders[entryName];

                // V8.11 FIX: Store pending replacement BEFORE cancelling
                // This ensures we only create a new stop when the old one is confirmed cancelled
                if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                {
                    // V8.31: Check if there's already a pending replacement to prevent duplicates
                    if (pendingStopReplacements.TryGetValue(entryName, out var existingPendingQty))
                    {
                        // Build 1104.2: Staleness fast-path -- purge stale pending and re-initiate
                        double pendingAgeSeconds = (DateTime.Now - existingPendingQty.CreatedTime).TotalSeconds;
                        if (pendingAgeSeconds > STALE_PENDING_FAST_PATH_SEC)
                        {
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                                Interlocked.Decrement(ref pendingReplacementCount);
                            Print(string.Format("[1104.2] Stale pending purged for {0} ({1:F1}s). Re-initiating stop resize.",
                                entryName, pendingAgeSeconds));
                        }
                        else
                        {
                            existingPendingQty.Quantity = pos.RemainingContracts;
                            Print(string.Format("V8.31: Updated existing pending replacement for {0} to {1} contracts", entryName, pos.RemainingContracts));
                            return;
                        }
                    }

                    // Store the replacement info
                    var newPending = new PendingStopReplacement
                    {
                        EntryName = entryName,
                        Quantity = pos.RemainingContracts,
                        StopPrice = pos.CurrentStopPrice,
                        Direction = pos.Direction,
                        OldOrder = currentStop,
                        CreatedTime = DateTime.Now  // V8.31: Added for timeout support
                    };

                    // V8.31: Thread-safe add
                    if (pendingStopReplacements.TryAdd(entryName, newPending))
                    {
                        Interlocked.Increment(ref pendingReplacementCount);
                    }

                    // Cancel old stop - replacement will be created in OnOrderUpdate when confirmed
                    CancelOrderForReplace(currentStop, pos);
                    Print(string.Format("STOP CANCEL PENDING: {0} | Will replace with {1} contracts @ {2:F2}",
                        entryName, pos.RemainingContracts, pos.CurrentStopPrice));
                }
                else
                {
                    // No existing stop to cancel, create new one directly
                    // V12.41: Pass the entry name for stricter validation
                    CreateNewStopOrder(entryName, pos.RemainingContracts, pos.CurrentStopPrice, pos.Direction);
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("(!) ERROR UpdateStopQuantity for {0}: {1}", entryName, ex.Message));
                Print(string.Format("(!) POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
            }
        }

        // V8.11: Helper method to create a new stop order
        // V8.31: Added guard to prevent duplicate stop creation
        private void CreateNewStopOrder(string entryName, int quantity, double stopPrice, MarketPosition direction, bool isRecovery = false)
        {
            try
            {
                // V12.41 ZOMBIE GUARD: Block stop creation if position is flat or entry not filled
                if (activePositions.TryGetValue(entryName, out var targetPos))
                {
                    if (targetPos.RemainingContracts <= 0)
                    {
                        Print(string.Format("[STOP_GUARD] BLOCKED zombie stop for {0} - Position is FLAT (Remaining=0)", entryName));
                        return;
                    }
                    if (!targetPos.EntryFilled)
                    {
                        Print(string.Format("[STOP_GUARD] BLOCKED early stop for {0} - Fill not yet confirmed", entryName));
                        return;
                    }
                }
                else
                {
                    Print(string.Format("[STOP_GUARD] BLOCKED orphan stop for {0} - No tracking record found", entryName));
                    return;
                }

                // V12.Phase7 [C-06]: Check if any live stop already exists for this entry (Working, Accepted,
                // ChangePending, or ChangeSubmitted). Without ChangePending guard, a ChangeOrder in flight
                // causes a second stop to be created -- leading to stacked stops that can reverse the position.
                if (stopOrders.TryGetValue(entryName, out var existingStop))
                {
                    if (existingStop != null && (
                        existingStop.OrderState == OrderState.Working ||
                        existingStop.OrderState == OrderState.Accepted ||
                        existingStop.OrderState == OrderState.ChangePending ||
                        existingStop.OrderState == OrderState.ChangeSubmitted))
                    {
                        if (isRecovery)
                        {
                            // Build 1104.2: Recovery mode -- stale tracked stop may be phantom at broker.
                            // Force-cancel and clear reference to allow fresh stop submission.
                            Print(string.Format("[1104.2] Recovery: force-cancelling phantom stop for {0} (state={1})",
                                entryName, existingStop.OrderState));
                            PositionInfo recoveryPos;
                            activePositions.TryGetValue(entryName, out recoveryPos);
                            CancelOrderSafe(existingStop, recoveryPos);
                            stopOrders.TryRemove(entryName, out _);
                        }
                        else
                        {
                            Print(string.Format("V12.Phase7: SKIPPING duplicate stop for {0} -- existing stop state={1}", entryName, existingStop.OrderState));
                            return;
                        }
                    }
                }

                // V12.Phase7 [C-04]: Round stop price to valid tick boundary.
                // CreateNewStopOrder receives raw prices that may not be tick-aligned.
                // Off-tick prices are rejected by the broker, leaving the position unprotected.
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);

                Order newStop = null;
                OrderAction exitAction = direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                // V12.3: Route to correct account (fleet follower vs local)
                if (activePositions.TryGetValue(entryName, out var pos) && pos.IsFollower && pos.ExecutingAccount != null)
                {
                    // Build 950: Re-link replacement stop to broker OCO bracket.
                    string _b950OcoId;
                    _b950OcoId = pos.OcoGroupId ?? string.Empty;
                    // Fleet follower: use Account API
                    string sigName = "S_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = pos.ExecutingAccount.CreateOrder(Instrument, exitAction,
                        OrderType.StopMarket, TimeInForce.Gtc, quantity, 0, stopPrice, _b950OcoId, sigName, null);
                    // B957: Guard against null CreateOrder and Submit throws to prevent unprotected position.
                    if (newStop == null)
                    {
                        Print(string.Format("[STOP_GUARD] CreateOrder returned null for follower {0}. Flattening.", entryName));
                        FlattenPositionByName(entryName);
                        return;
                    }
                    try { pos.ExecutingAccount.Submit(new[] { newStop }); }
                    catch (Exception submitEx)
                    {
                        Print(string.Format("[STOP_GUARD] Submit threw for follower {0}: {1}. Flattening.", entryName, submitEx.Message));
                        FlattenPositionByName(entryName);
                        return;
                    }
                }
                else
                {
                    // Build 950: Re-link replacement stop to broker OCO bracket.
                    string _b950OcoId;
                    _b950OcoId = pos != null ? (pos.OcoGroupId ?? string.Empty) : string.Empty;
                    // Local: use SubmitOrderUnmanaged with truncated signal name
                    string suffix = (DateTime.Now.Ticks % 100000000).ToString();
                    string sigName = "S_" + entryName + "_" + suffix;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = SubmitOrderUnmanaged(0, exitAction, OrderType.StopMarket, quantity, 0, stopPrice, _b950OcoId, sigName);
                }

                if (newStop == null)
                {
                    Print(string.Format("(!) CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(string.Format("(!) POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}",
                        direction == MarketPosition.Long ? "LONG" : "SHORT", quantity, stopPrice));

                    // Attempt to flatten position immediately
                    Print(string.Format("(!) Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                // A1-1: B966 -- Enqueue actor pipeline (was naked stateLock write)
                { var _en966 = entryName; var _ns966 = newStop; Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; }); }

                // [LATENCY_AUDIT] Measure OCO turnaround: CreatedTime was stamped in UpdateStopQuantity() when
                // the target fill triggered the pending stop replacement. The delta = Target Fill -> Stop Cancel
                // confirmed -> new stop submitted -- the full OCO lifecycle round-trip.
                if (pendingStopReplacements.TryGetValue(entryName, out var pendingForLatency))
                {
                    double ocoLatencyMs = (DateTime.Now - pendingForLatency.CreatedTime).TotalMilliseconds;
                    Print(string.Format("[LATENCY_AUDIT] Target Fill -> Stop Cancel Delta: {0:F1}ms (Entry: {1})",
                        ocoLatencyMs, entryName));
                }

                Print(string.Format("STOP QTY UPDATED: {0} contracts @ {1:F2} (Order: {2})",
                    quantity, stopPrice, newStop.Name));
            }
            catch (Exception ex)
            {
                Print(string.Format("(!) ERROR CreateNewStopOrder for {0}: {1}", entryName, ex.Message));
            }
        }

        // Build 950: Re-submit profit targets that were OCO-cascade-cancelled during stop replacement.
        // Runs on strategy thread via TriggerCustomEvent. Checks Order.OrderState directly on the
        // captured Order object -- avoids dict-timing races with RemoveGhostOrderRef.
        private void RestoreCascadedTargets(string entryName, TargetSnapshot[] capturedTargets)
        {
            if (capturedTargets == null || capturedTargets.Length == 0) return;

            PositionInfo pos;
            if (!activePositions.TryGetValue(entryName, out pos)) return;

            bool entryFilled;
            int remainingContracts;
            MarketPosition direction;
            bool isFollower;
            Account executingAccount;
            string ocoGroupId;

            entryFilled        = pos.EntryFilled;
            remainingContracts = pos.RemainingContracts;
            direction          = pos.Direction;
            isFollower         = pos.IsFollower;
            executingAccount   = pos.ExecutingAccount;
            ocoGroupId         = pos.OcoGroupId;

            if (!entryFilled || remainingContracts <= 0) return;

            OrderAction exitAction = direction == MarketPosition.Long
                ? OrderAction.Sell : OrderAction.BuyToCover;
            string bracketOcoId = ocoGroupId ?? string.Empty;

            foreach (TargetSnapshot snap in capturedTargets)
            {
                if (snap == null || snap.CapturedOrder == null) continue;

                // Only restore targets the broker OCO cascade-cancelled.
                // Filled targets have OrderState.Filled -- skip them.
                if (snap.CapturedOrder.OrderState != OrderState.Cancelled
                    && snap.CapturedOrder.OrderState != OrderState.Rejected)
                    continue;

                double restoredPrice = Instrument.MasterInstrument.RoundToTickSize(snap.Price);
                Order newTarget = null;

                if (isFollower && executingAccount != null)
                {
                    string tSig = SymmetryTrim("T" + snap.TargetNum + "_" + entryName, 40);
                    Order tOrd = executingAccount.CreateOrder(
                        Instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                        snap.Qty, restoredPrice, 0, bracketOcoId, tSig, null);
                    if (tOrd != null)
                    {
                        executingAccount.Submit(new[] { tOrd });
                        newTarget = tOrd;
                    }
                }
                else
                {
                    string tSig = "T" + snap.TargetNum + "_" + entryName;
                    newTarget = direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit,
                            snap.Qty, restoredPrice, 0, bracketOcoId, tSig)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit,
                            snap.Qty, restoredPrice, 0, bracketOcoId, tSig);
                }

                var tDict = GetTargetOrdersDictionary(snap.TargetNum);
                if (tDict != null)
                {
                    if (newTarget != null)
                    {
                        tDict[entryName] = newTarget;
                        Print(string.Format("[B950] Target T{0} restored for {1} @ {2:F2} qty={3}",
                            snap.TargetNum, entryName, restoredPrice, snap.Qty));
                    }
                    else
                    {
                        Print(string.Format("[B950] WARN: Target T{0} restore NULL for {1}",
                            snap.TargetNum, entryName));
                    }
                }
            }
        }

        private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice, int level = 0, double entryPrice = 0)
        {
            // V12.41: Use real-time price instead of stale bar Close[0]
            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
            double tickSize = Instrument.MasterInstrument.TickSize;
            
            // [V12.1102E] RELAXED SAFETY: For Manual BE (Level 1), allow zero-tick distance from market.
            // This prevents the safety guard from pulling back a BE stop that price has just reached.
            // Standard trailing (Level > 1) still enforces a 2-tick buffer.
            double minDistance = (level == 1) ? 0 : (2 * tickSize);

            double resultStop = desiredStopPrice;

            if (direction == MarketPosition.Long)
            {
                // For BE (Level 1), only adjust if stop is STRICTLY above market (illegal).
                // Equality is allowed for BE to prevent safety pull-back on the threshold cross.
                bool isIllegal = (level == 1) ? (desiredStopPrice > currentPrice) : (desiredStopPrice >= currentPrice);

                if (isIllegal)
                {
                    if (level == 1 && entryPrice > 0)
                    {
                        // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                        // Do NOT snap to current market -- that drags the stop into negative territory.
                        resultStop = entryPrice;
                        Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped LONG stop from {0:F2} to entry floor {1:F2}",
                            desiredStopPrice, resultStop));
                    }
                    else
                    {
                        resultStop = currentPrice - (level == 1 ? 0 : minDistance);
                        Print(string.Format("STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                            desiredStopPrice, resultStop, level, (level == 1 ? "above" : "at/above")));
                    }
                }
            }
            else
            {
                bool isIllegal = (level == 1) ? (desiredStopPrice < currentPrice) : (desiredStopPrice <= currentPrice);

                if (isIllegal)
                {
                    if (level == 1 && entryPrice > 0)
                    {
                        // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                        // Do NOT snap to current market -- that drags the stop into negative territory.
                        resultStop = entryPrice;
                        Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped SHORT stop from {0:F2} to entry floor {1:F2}",
                            desiredStopPrice, resultStop));
                    }
                    else
                    {
                        resultStop = currentPrice + (level == 1 ? 0 : minDistance);
                        Print(string.Format("STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                            desiredStopPrice, resultStop, level, (level == 1 ? "below" : "at/below")));
                    }
                }
            }

            // [Build 1102H] Profit Floor: secondary backstop -- ensures resultStop never crosses
            // below entry for Long (or above entry for Short) regardless of how resultStop was set.
            if (level == 1 && entryPrice > 0)
            {
                if (direction == MarketPosition.Long && resultStop < entryPrice)
                    resultStop = entryPrice;
                else if (direction == MarketPosition.Short && resultStop > entryPrice)
                    resultStop = entryPrice;
            }

            // V12.Phase7 [C-04]: Always round to valid tick boundary before returning.
            return Instrument.MasterInstrument.RoundToTickSize(resultStop);
        }


        #endregion
    }
}
