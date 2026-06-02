// <copyright file="V12_002.Orders.Management.Flatten.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// Build 971: Orders.Management.Flatten -- SyncPositionState, ManageCIT, FlattenAll, FlattenPositionByName, IsOrderTerminal, HasActiveOrPendingOrderForEntry
// V12 Orders.Management Module (Extracted)
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
        #region Orders Management Flatten

        private void SyncPositionState()
        {
            List<string> toRemove = new List<string>();

            // V8.30: Thread-safe snapshot iteration
            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pos = kvp.Value;
                if (pos.EntryFilled && pos.RemainingContracts <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string key in toRemove)
            {
                CleanupPosition(key);
            }
        }

        /// <summary>
        /// V12 SIMA: Chase If Touch - iterates the unified entryOrders dictionary which contains
        /// BOTH local and fleet follower limit orders. When price touches a working limit entry
        /// that was not filled, the limit is nudged N ticks toward market (citOffset * TickSize)
        /// exactly once per order lifetime. Local orders: ChangeOrder() to new limit price.
        /// Follower orders: cancel + resubmit as OrderType.Limit at new price via ExecutingAccount.
        /// Re-nudging is prevented by _citNudgedKeys one-shot guard, cleared on fill or cancel.
        /// </summary>
        private void ManageCIT()
        {
            if (!ValidateCitConfiguration(out double citOffset))
            {
                return;
            }

            int _citBrokerBudget = MaxBrokerCallsPerCycle; // 5 calls max per cycle (constant at V12_002.cs:303)
            // Iterate ALL entry orders in the unified dictionary (local + every fleet account)
            foreach (var kvp in entryOrders.ToArray())
            {
                string key = kvp.Key;
                Order order = kvp.Value;

                if (!ShouldChaseOrder(order, key))
                {
                    continue;
                }

                // Determine local vs follower
                PositionInfo pos = null;
                activePositions.TryGetValue(key, out pos);
                bool isFollower = pos != null && pos.IsFollower && pos.ExecutingAccount != null;

                try
                {
                    double newLimitPrice = CalculateNudgedPrice(order.OrderAction, order.LimitPrice, citOffset);

                    if (isFollower)
                    {
                        if (
                            !ExecuteFollowerNudge(
                                key,
                                order,
                                newLimitPrice,
                                citOffset,
                                pos.ExecutingAccount,
                                ref _citBrokerBudget
                            )
                        )
                        {
                            return; // Budget exhausted - stop iteration (original behavior)
                        }
                    }
                    else
                    {
                        ExecuteLocalNudge(key, order, newLimitPrice, citOffset);
                    }
                    _citNudgedKeys.TryAdd(key, true); // [BUILD 949] one-shot: mark as nudged
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("ChangeOrder"))
                {
                    Print($"[CIT] WARNING chasing {key} (known quirk): {ex.Message}");
                }
                catch (Exception ex)
                {
                    Print($"[CIT] CRITICAL chasing {key}: {ex.ToString()}");
                    // Do NOT rethrow - remaining fleet accounts still need flattening
                }
            }
        }

        /// <summary>
        /// Executes a local account nudge by calling ChangeOrder.
        /// </summary>
        private void ExecuteLocalNudge(string key, Order order, double newLimitPrice, double citOffset)
        {
            Print(
                $"[CIT] LOCAL nudge: {key} | {order.LimitPrice:F2} -> {newLimitPrice:F2} ({citOffset} ticks toward mkt)"
            );
            ChangeOrder(order, order.Quantity, newLimitPrice, 0);
        }

        /// <summary>
        /// Executes a follower account nudge by canceling and resubmitting the order.
        /// Handles budget exhaustion by self-enqueuing for deferred execution.
        /// Returns false if budget exhausted (signals caller to stop iteration), true if nudge succeeded.
        /// </summary>
        private bool ExecuteFollowerNudge(
            string key,
            Order order,
            double newLimitPrice,
            double citOffset,
            Account followerAcct,
            ref int citBrokerBudget
        )
        {
            Print(
                $"[CIT] FLEET nudge: {key} on {followerAcct.Name} | {order.LimitPrice:F2} -> {newLimitPrice:F2} ({citOffset} ticks toward mkt)"
            );

            // Build 1109 [FREEZE-PROOF]: Ensure 2 slots available BEFORE consuming (Cancel + Submit)
            if (citBrokerBudget < 2)
            {
                Print("[CIT] Broker budget exhausted -- deferring remaining nudges");
                Enqueue(ctx => ctx.ManageCIT());
                return false; // Signal caller to stop iteration
            }
            citBrokerBudget -= 2; // Cancel + Submit = 2 broker calls

            followerAcct.Cancel(new[] { order });

            Order nudgedOrder = followerAcct.CreateOrder(
                Instrument,
                order.OrderAction,
                OrderType.Limit,
                TimeInForce.Gtc,
                order.Quantity,
                newLimitPrice,
                0,
                "",
                "CIT_" + key,
                null
            );
            if (nudgedOrder == null)
            {
                Print($"[CIT] ERROR: CreateOrder returned null for {key} on {followerAcct.Name} -- nudge aborted");
                return false; // Signal failure without marking as nudged
            }
            followerAcct.Submit(new[] { nudgedOrder });

            // B966: No Enqueue needed -- ManageCIT is always called via Enqueue(ctx => ctx.ManageCIT())
            // from OnBarUpdate (Phase C), so this write is already inside the actor drain.
            entryOrders[key] = nudgedOrder;
            return true; // Nudge succeeded
        }

        /// <summary>
        /// Determines if an order should be chased based on validation and price touch logic.
        /// Returns false if order is invalid, not a working limit, already nudged, or price hasn't touched.
        /// </summary>
        private bool ShouldChaseOrder(Order order, string key)
        {
            if (order == null || order.OrderState != OrderState.Working)
                return false;
            if (order.OrderType != OrderType.Limit)
                return false; // only chase limit entries
            if (_citNudgedKeys.ContainsKey(key))
                return false; // [BUILD 949] one-shot: already nudged

            // [BUILD 984 CIT FIX] Correct directional bar-price logic:
            // - LONG entry (Buy): price must DROP DOWN to the limit -> compare Low[0] <= limitPrice
            // - SHORT entry (Sell): price must RISE UP to the limit -> compare High[0] >= limitPrice
            // Previous bug: Short used Low[0] <= limitPrice which is ALWAYS true when clicking
            // far above the current market, causing instant market conversion on every click.
            double currentPrice = (order.OrderAction == OrderAction.Buy) ? Low[0] : High[0];
            double limitPrice = order.LimitPrice;

            bool triggerChase =
                (order.OrderAction == OrderAction.Buy)
                    ? (currentPrice <= limitPrice) // Long: bar low touched or pierced the limit
                    : (currentPrice >= limitPrice); // Short: bar high touched or pierced the limit

            return triggerChase;
        }

        /// <summary>
        /// Calculates the nudged limit price by moving N ticks toward market.
        /// Long orders: nudge UP (add ticks). Short orders: nudge DOWN (subtract ticks).
        /// </summary>
        private double CalculateNudgedPrice(OrderAction action, double limitPrice, double citOffset)
        {
            double tickSize = Instrument.MasterInstrument.TickSize;
            double nudgeDistance = citOffset * tickSize;
            return (action == OrderAction.Buy)
                ? Instrument.MasterInstrument.RoundToTickSize(limitPrice + nudgeDistance)
                : Instrument.MasterInstrument.RoundToTickSize(limitPrice - nudgeDistance);
        }

        /// <summary>
        /// Validates CIT configuration and returns parsed offset.
        /// Returns false if CIT should be skipped (no positions, invalid config, or propagation active).
        /// </summary>
        private bool ValidateCitConfiguration(out double citOffset)
        {
            citOffset = 0;

            if (activePositions.Count == 0 && entryOrders.Count == 0)
                return false;
            if (string.IsNullOrEmpty(ChaseIfTouchPoints) || ChaseIfTouchPoints == "0")
                return false;

            // [BUILD 924 -- Fix C] Suppress CIT during price-move propagation to prevent
            // race-fire on freshly resubmitted follower limit orders before sync cycle completes.
            if (_propagationActive)
            {
                Print("[CIT] Suppressed during price-move propagation (Build 924 Fix C)");
                return false;
            }

            if (!double.TryParse(ChaseIfTouchPoints, out citOffset))
                return false;

            return true;
        }

        private void FlattenAll()
        {
            // V1101E HOT-PATCH: Serialize entire flatten pipeline to prevent overlap with Reaper/order callbacks.
            isFlattenRunning = true; // V12.13b: Suppress stop re-submit during flatten
            try
            {
                HandleGhostPositionCleanup();

                if (activePositions.Count == 0 && Position.MarketPosition == MarketPosition.Flat)
                {
                    Print("FLATTEN: No active positions to close");
                    if (EnableSIMA)
                        DispatchFleetFlatten();
                    return;
                }

                Print("FLATTEN: Closing all positions...");

                // Phase 1: Cancel master entry orders (with known quirk handling)
                try
                {
                    CancelMasterEntryOrders();
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("CancelOrder"))
                {
                    Print("WARNING: Known quirk in CancelMasterEntryOrders: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Print("CRITICAL: Unexpected exception in CancelMasterEntryOrders: " + ex.ToString());
                }

                // Phase 2: Dispatch fleet flatten (with known quirk handling)
                if (EnableSIMA)
                {
                    try
                    {
                        DispatchFleetFlatten();
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("TriggerCustomEvent"))
                    {
                        Print("WARNING: Known NT8 quirk in TriggerCustomEvent: " + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Print("CRITICAL: Unexpected exception in DispatchFleetFlatten: " + ex.ToString());
                    }
                }

                // Phase 3: Reset sync state (always execute)
                try
                {
                    ResetSyncStateAndPurgeFollowers();
                }
                catch (Exception ex)
                {
                    Print("CRITICAL: Unexpected exception in ResetSyncStateAndPurgeFollowers: " + ex.ToString());
                }

                // Phase 4: Flatten filled positions (always execute)
                try
                {
                    FlattenFilledMasterPositions();
                }
                catch (Exception ex)
                {
                    Print("CRITICAL: Unexpected exception in FlattenFilledMasterPositions: " + ex.ToString());
                }

                // Phase 5: Cancel unfilled entries (always execute)
                try
                {
                    CancelUnfilledMasterEntries();
                }
                catch (Exception ex)
                {
                    Print("CRITICAL: Unexpected exception in CancelUnfilledMasterEntries: " + ex.ToString());
                }
            }
            finally
            {
                // V1101E HOT-PATCH: Release flatten guard only after serialized flatten pipeline exits.
                isFlattenRunning = false; // V12.13b: Always release guard
            }
        }

        private void HandleGhostPositionCleanup()
        {
            // V10 GHOST FIX: Scan for actual live position even if activePositions is empty
            int liveQty = 0;
            MarketPosition liveDir = MarketPosition.Flat;
            if (Position != null)
            {
                liveQty = Position.Quantity;
                liveDir = Position.MarketPosition;
            }

            if (activePositions.Count == 0 && liveQty > 0)
            {
                Print(string.Format("FLATTEN GHOST: Closing ORPHANED position of {0} contracts", liveQty));
                if (liveDir == MarketPosition.Long)
                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, liveQty, 0, 0, "", "Flatten_Ghost");
                else
                    SubmitOrderUnmanaged(
                        0,
                        OrderAction.BuyToCover,
                        OrderType.Market,
                        liveQty,
                        0,
                        0,
                        "",
                        "Flatten_Ghost"
                    );
            }
        }

        private void CancelMasterEntryOrders()
        {
            // V12.13b: Removed ExitLong/ExitShort block (managed-mode methods incompatible with IsUnmanaged=true)
            // Unmanaged flatten via SubmitOrderUnmanaged is handled below at the per-position level

            // Clear all tracked pending entry orders using account-aware routing
            foreach (var entryOrder in entryOrders.Values)
            {
                if (
                    entryOrder != null
                    && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted)
                    && (entryOrder.Account == null || entryOrder.Account == Account)
                )
                    CancelOrderOnAccount(entryOrder, entryOrder.Account);
            }
        }

        private void DispatchFleetFlatten()
        {
            // V1101E HOT-PATCH: Keep flatten guard asserted across nested SIMA flatten call.
            isFlattenRunning = true;
            FlattenAllApexAccounts();
            isFlattenRunning = true;
        }

        private void ResetSyncStateAndPurgeFollowers()
        {
            // V12.2: Reset Sync State
            isLongArmed = false;
            isShortArmed = false;

            // V1102Q [RUNNER-LEAK]: Explicit follower sweep.
            // Purge all follower metadata from memory to prevent ghost entries.
            foreach (var kvp in activePositions.ToArray())
            {
                if (kvp.Value.IsFollower)
                {
                    activePositions.TryRemove(kvp.Key, out _);
                    entryOrders.TryRemove(kvp.Key, out _);
                    Print($"[V1102Q] Follower Sweep: Purged {kvp.Key} from memory");
                }
            }
        }

        private void FlattenFilledMasterPositions()
        {
            // V8.30: Thread-safe snapshot iteration (Master/Main entries)
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key))
                    continue;
                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;

                if (!pos.EntryFilled)
                    continue;

                FlattenSinglePosition(entryName, pos);
            }
        }

        private void FlattenSinglePosition(string entryName, PositionInfo pos)
        {
            Print(
                string.Format(
                    "FLATTEN: Closing filled {0} position",
                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT"
                )
            );

            // V12.1101E [PH5-COLLIDE-01]: Lifecycle-safe stop cancellation.
            // Keep stop dictionary refs until broker-confirmed terminal state.
            RequestStopCancelLifecycleSafe(entryName);
            Print(string.Format("FLATTEN: Requested stop lifecycle cancel for {0}", entryName));

            // V8.31: Also clear any pending stop replacements to prevent orphaned stops
            if (pendingStopReplacements.TryRemove(entryName, out _))
            {
                Interlocked.Decrement(ref pendingReplacementCount);
                Print(string.Format("V8.31: Cleared pending stop replacement for {0}", entryName));
            }

            // Cancel all target orders (T1-T5)
            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (tDict != null && tDict.TryGetValue(entryName, out var tOrder))
                {
                    if (
                        tOrder != null
                        && (
                            tOrder.OrderState == OrderState.Working
                            || tOrder.OrderState == OrderState.Accepted
                            || tOrder.OrderState == OrderState.Submitted
                        )
                    )
                        CancelOrderSafe(tOrder, pos);
                }
            }

            // V8.28 FIX: Use LIVE position quantity instead of cached RemainingContracts
            int livePositionQty = 0;
            try
            {
                if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                    livePositionQty = Position.Quantity;
            }
            catch (Exception pEx)
            {
                Print("Flatten Error reading Position: " + pEx.Message);
            }

            // Use the smaller of cached and live to avoid overselling
            // V10 DIAGNOSTIC: Print values
            Print(
                string.Format(
                    "FLATTEN DIAGNOSTIC: Entry={0} Cached={1} Live={2}",
                    entryName,
                    pos.RemainingContracts,
                    livePositionQty
                )
            );

            // V10 FLATTEN FIX: Trust cached contracts if live is 0 (latency protection)
            // If cached says we have contracts, we close them.
            int flattenQty = pos.RemainingContracts;

            if (livePositionQty > 0)
            {
                // If NinjaTrader agrees we have a position, use the smaller to act safe?
                // No, if real position is smaller, we might be over-closing.
                // But if real is larger, we under-close.
                // Let's stick to closing what we know we opened.
                flattenQty = pos.RemainingContracts;
            }

            // Submit market order to close position
            if (flattenQty > 0)
            {
                Order flattenOrder =
                    pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(
                            0,
                            OrderAction.Sell,
                            OrderType.Market,
                            flattenQty,
                            0,
                            0,
                            "",
                            "Flatten_" + entryName
                        )
                        : SubmitOrderUnmanaged(
                            0,
                            OrderAction.BuyToCover,
                            OrderType.Market,
                            flattenQty,
                            0,
                            0,
                            "",
                            "Flatten_" + entryName
                        );

                if (flattenOrder == null)
                    Print("FLATTEN ERROR: SubmitOrderUnmanaged returned NULL");
                else
                    Print(
                        string.Format(
                            "FLATTEN SENT: {0} {1} contracts",
                            pos.Direction == MarketPosition.Long ? "SELL" : "BUY",
                            flattenQty
                        )
                    );
            }
            else
            {
                Print("FLATTEN SKIPPED: Qty is 0");
            }
        }

        private void CancelUnfilledMasterEntries()
        {
            // V8.30: Thread-safe snapshot iteration (Master/Main entries)
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key))
                    continue;
                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;

                if (pos.EntryFilled)
                    continue;

                // Cancel pending entry order
                if (entryOrders.ContainsKey(entryName))
                {
                    Order entryOrder = entryOrders[entryName];
                    if (
                        entryOrder != null
                        && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted)
                    )
                    {
                        CancelOrderSafe(entryOrder, pos);
                        Print(
                            string.Format(
                                "FLATTEN: Cancelled pending {0} entry order @ {1:F2}",
                                pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                pos.EntryPrice
                            )
                        );
                    }
                }
            }
        }

        private void FlattenPositionByName(string entryName)
        {
            if (!activePositions.TryGetValue(entryName, out var pos))
                return;
            if (!pos.EntryFilled || pos.RemainingContracts <= 0)
                return;

            Print(string.Format("(!) EMERGENCY FLATTEN: Closing {0} position due to stop order failure", entryName));

            CancelAllBracketOrdersForPosition(entryName, pos);

            if (pendingStopReplacements.TryRemove(entryName, out _))
                Interlocked.Decrement(ref pendingReplacementCount);

            SubmitEmergencyFlattenOrder(entryName, pos);
        }

        private void CancelAllBracketOrdersForPosition(string entryName, PositionInfo pos)
        {
            if (stopOrders.TryGetValue(entryName, out var stopOrder) && stopOrder != null)
            {
                if (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted)
                {
                    CancelOrderSafe(stopOrder, pos);
                }
            }

            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (tDict != null && tDict.TryGetValue(entryName, out var tOrder) && tOrder != null)
                {
                    if (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted)
                    {
                        CancelOrderSafe(tOrder, pos);
                    }
                }
            }
        }

        private void SubmitEmergencyFlattenOrder(string entryName, PositionInfo pos)
        {
            bool isFleetFollower = pos.IsFollower && pos.ExecutingAccount != null;
            int flattenQty = pos.RemainingContracts;
            OrderAction flattenAction =
                pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

            Order flattenOrder = null;
            if (isFleetFollower)
            {
                string sigName = "EF_" + entryName;
                if (sigName.Length > 50)
                    sigName = sigName.Substring(0, 50);
                flattenOrder = pos.ExecutingAccount.CreateOrder(
                    Instrument,
                    flattenAction,
                    OrderType.Market,
                    TimeInForce.Gtc,
                    flattenQty,
                    0,
                    0,
                    "",
                    sigName,
                    null
                );
                pos.ExecutingAccount.Submit(new[] { flattenOrder });
            }
            else
            {
                try
                {
                    if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                        flattenQty = Math.Max(flattenQty, Position.Quantity);
                }
                catch
                {
                    // Swallow: Position access may throw if broker connection is lost during flatten
                }

                string sigName = "EF_" + entryName;
                if (sigName.Length > 50)
                    sigName = sigName.Substring(0, 50);
                flattenOrder = SubmitOrderUnmanaged(0, flattenAction, OrderType.Market, flattenQty, 0, 0, "", sigName);
            }

            if (flattenOrder != null)
            {
                Print(
                    string.Format(
                        "Emergency flatten order submitted on {0}: {1} {2} contracts at MARKET",
                        isFleetFollower ? pos.ExecutingAccount.Name : "LOCAL",
                        pos.Direction == MarketPosition.Long ? "SELL" : "BUY",
                        flattenQty
                    )
                );
            }
            else
            {
                Print(string.Format("(!) CRITICAL: Emergency flatten order FAILED for {0}!", entryName));
                Print("(!) MANUAL INTERVENTION REQUIRED - Close position manually in NinjaTrader!");
            }
        }

        // V12.1101E [DESYNC-01]: Terminal-only removal. Returns true if order is Filled, Cancelled, Rejected, or Unknown.
        private static bool IsOrderTerminal(OrderState state)
        {
            return state == OrderState.Filled
                || state == OrderState.Cancelled
                || state == OrderState.Rejected
                || state == OrderState.Unknown;
        }

        // V12.1101E [DESYNC-01]: True if any stop/target/entry dict still holds a non-terminal order for this entry.
        private bool HasActiveOrPendingOrderForEntry(string entryName)
        {
            if (stopOrders.TryGetValue(entryName, out var stop) && stop != null && !IsOrderTerminal(stop.OrderState))
                return true;

            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (
                    tDict != null
                    && tDict.TryGetValue(entryName, out var tOrder)
                    && tOrder != null
                    && !IsOrderTerminal(tOrder.OrderState)
                )
                    return true;
            }

            if (entryOrders.TryGetValue(entryName, out var e) && e != null && !IsOrderTerminal(e.OrderState))
                return true;
            return false;
        }

        /// <summary>
        /// V12.1101E [DESYNC-01]: Terminal-only cleanup. Only TryRemove when order is Filled/Cancelled/Rejected/Unknown;
        /// if Working/Accepted/Pending, call CancelOrder but do NOT remove -- OnOrderUpdate will remove on terminal state.
        /// activePositions is removed only at the end and only when no dict still holds an active/pending order.
        /// </summary>
        #endregion
    }
}
