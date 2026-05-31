// V12.44 MODULAR: Order Callbacks Module (Split from Orders.cs)
// Contains: OnOrderUpdate, OnAccountOrderUpdate, OnPositionUpdate, OnExecutionUpdate
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
        #region Order Callbacks

        /// <summary>
        /// Applies a target fill in a partial-fill-safe way.
        /// - Uses cumulative filled quantity to avoid over/under-decrement when callbacks race.
        /// - Marks target as filled only when complete (or when caller forces completion from a terminal order event).
        /// </summary>
        private void ApplyTargetFill(
            PositionInfo pos,
            int targetNumber,
            int fillQty,
            bool forceComplete,
            out bool alreadyProcessed,
            out int appliedQty,
            out int remainingContractsAfter
        )
        {
            alreadyProcessed = false;
            appliedQty = 0;
            remainingContractsAfter = 0;

            alreadyProcessed = IsTargetFilled(pos, targetNumber);
            if (alreadyProcessed)
            {
                remainingContractsAfter = pos.RemainingContracts;
                return;
            }

            int targetContracts = Math.Max(0, GetTargetContracts(pos, targetNumber));
            int filledQty = Math.Max(0, GetTargetFilledQuantity(pos, targetNumber));
            int remainingTargetQty = Math.Max(0, targetContracts - filledQty);

            int requestedFillQty = Math.Max(0, fillQty);
            appliedQty = Math.Min(requestedFillQty, remainingTargetQty);

            if (appliedQty > 0)
            {
                filledQty += appliedQty;
                SetTargetFilledQuantity(pos, targetNumber, filledQty);
                pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - appliedQty);
            }

            bool isComplete = forceComplete || filledQty >= targetContracts;
            if (isComplete)
            {
                SetTargetFilledQuantity(pos, targetNumber, Math.Max(filledQty, targetContracts));
                MarkTargetFilled(pos, targetNumber);
            }

            remainingContractsAfter = pos.RemainingContracts;
        }

        // V12.1101E [F-07]: Request stop cancellation without dropping dictionary state early.
        // We only remove references after broker-confirmed terminal states.
        // Build 1104 routes stop cancels through the gateway so follower orders use Account.Cancel()
        // while master orders continue to use the NinjaScript managed cancel path.
        private void RequestStopCancelLifecycleSafe(string entryName)
        {
            if (string.IsNullOrEmpty(entryName))
                return;
            if (!stopOrders.TryGetValue(entryName, out var stopOrder) || stopOrder == null)
                return;

            // V12.1101H [COLLIDE-01]: Include ChangePending/ChangeSubmitted -- stops in these transient
            // states were previously ignored by this function, leaving them live at the broker after FlattenAll.
            if (
                stopOrder.OrderState == OrderState.Working
                || stopOrder.OrderState == OrderState.Accepted
                || stopOrder.OrderState == OrderState.ChangePending
                || stopOrder.OrderState == OrderState.ChangeSubmitted
            )
            {
                PositionInfo posRef;
                activePositions.TryGetValue(entryName, out posRef);
                CancelOrderSafe(stopOrder, posRef);
                return;
            }

            if (
                stopOrder.OrderState == OrderState.Cancelled
                || stopOrder.OrderState == OrderState.Filled
                || stopOrder.OrderState == OrderState.Rejected
                || stopOrder.OrderState == OrderState.Unknown
            )
            {
                stopOrders.TryRemove(entryName, out _);
            }
        }

        // V12.1101E [F-07]: Broker-confirmed target cleanup fallback when position state was already torn down.
        private bool TryRemoveTargetReferenceByOrder(ConcurrentDictionary<string, Order> dict, Order order)
        {
            if (dict == null || order == null)
                return false;
            // [EPIC-5-PERF-T02] Single snapshot allocation at method start
            var snapshot = dict.ToArray();
            foreach (var kvp in snapshot)
            {
                // Re-check existence (mutation safety)
                if (!dict.ContainsKey(kvp.Key))
                    continue;
                if (kvp.Value == order)
                {
                    dict.TryRemove(kvp.Key, out _);
                    return true;
                }
            }
            return false;
        }

        // V12.1101E [F-07]: Removes terminal target refs using broker-confirmed order object identity.
        private void RemoveTargetReferenceOnTerminalFill(Order order)
        {
            if (order == null)
                return;
            if (TryRemoveTargetReferenceByOrder(target1Orders, order))
                return;
            if (TryRemoveTargetReferenceByOrder(target2Orders, order))
                return;
            if (TryRemoveTargetReferenceByOrder(target3Orders, order))
                return;
            if (TryRemoveTargetReferenceByOrder(target4Orders, order))
                return;
            TryRemoveTargetReferenceByOrder(target5Orders, order);
        }

        // V12.962 INLINE ACTOR: Thin-shell entry point. Captures order-object reference and all
        // primitive args before Enqueue. ProcessOnOrderUpdate runs lock-free inside the drain.
        protected override void OnOrderUpdate(
            Order order,
            double limitPrice,
            double stopPrice,
            int quantity,
            int filled,
            double averageFillPrice,
            OrderState orderState,
            DateTime time,
            ErrorCode error,
            string nativeError
        )
        {
            // Order reference is stable (NT8 managed object); capture primitives to avoid
            // any potential race between callback return and drain execution.
            Order _o = order;
            double _lp = limitPrice;
            double _sp = stopPrice;
            int _q = quantity;
            int _f = filled;
            double _af = averageFillPrice;
            OrderState _os = orderState;
            DateTime _t = time;
            string _ne = nativeError ?? string.Empty;
            Enqueue(ctx => ctx.ProcessOnOrderUpdate(_o, _lp, _sp, _q, _f, _af, _os, _t, _ne));
        }

        private void ProcessOnOrderUpdate(
            Order order,
            double limitPrice,
            double stopPrice,
            int quantity,
            int filled,
            double averageFillPrice,
            OrderState orderState,
            DateTime time,
            string nativeError
        )
        {
            // [EPIC-5-PERF] Latency instrumentation
            var probe = LatencyProbe.Start();

            try
            {
                if (
                    order.Account == this.Account
                    && (
                        orderState == OrderState.Working
                        || orderState == OrderState.Accepted
                        || orderState == OrderState.ChangeSubmitted
                    )
                )
                {
                    PropagateMasterPriceMove(order, limitPrice, stopPrice, quantity);
                }

                bool handled = false;

                if (orderState == OrderState.Filled)
                {
                    if (entryOrders.Values.Contains(order))
                        handled = HandleEntryOrderFilled(order, quantity, filled, averageFillPrice, time);
                    else
                        handled = HandleSecondaryOrderFilled(order, averageFillPrice);
                }
                else if (orderState == OrderState.Rejected)
                {
                    handled = HandleOrderRejected(order, nativeError);
                }
                else if (orderState == OrderState.Cancelled)
                {
                    handled = HandleOrderCancelled(order);
                }
                else if (orderState == OrderState.Accepted || orderState == OrderState.Working)
                {
                    handled = HandleOrderPriceOrQuantityChanged(order, limitPrice, stopPrice, quantity);
                }

                // Terminal catch-all
                if (
                    !handled
                    && (
                        orderState == OrderState.Cancelled
                        || orderState == OrderState.Rejected
                        || orderState == OrderState.Unknown
                    )
                )
                {
                    RemoveGhostOrderRef(order, orderState.ToString().ToUpper());
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnOrderUpdate: " + ex.Message);
            }
            finally
            {
                // [EPIC-5-PERF] Record latency
                probe = probe.Stop();
                _histProcessOnOrderUpdate.Record(probe);
            }
        }

        private bool HandleEntryOrderFilled(
            Order order,
            int quantity,
            int filled,
            double averageFillPrice,
            DateTime time
        )
        {
            // [EPIC-5-PERF-T02] Single snapshot allocation at method start
            var snapshot = activePositions.ToArray();
            foreach (var kvp in snapshot)
            {
                if (!activePositions.ContainsKey(kvp.Key))
                    continue;
                if (
                    entryOrders.TryGetValue(kvp.Key, out var entryOrder)
                    && entryOrder == order
                    && !kvp.Value.EntryFilled
                )
                {
                    PositionInfo pos = kvp.Value;
                    if (!pos.IsFollower)
                    {
                        int masterFillQty = filled > 0 ? filled : quantity;
                        SymmetryGuardOnMasterFill(
                            kvp.Key,
                            pos,
                            averageFillPrice,
                            masterFillQty,
                            time.ToUniversalTime()
                        );
                        // Build 1001: Seed expectedPositions[master] immediately on fill to prevent desync in CANCEL_ALL/REAPER.
                        SetExpectedPositionLocked(
                            ExpKey(Account.Name),
                            (pos.Direction == MarketPosition.Long ? masterFillQty : -masterFillQty)
                        );
                    }

                    if (averageFillPrice <= 0)
                    {
                        pos.EntryFilled = true;
                        pos.InitialTargetCount = activeTargetCount;
                        Print(
                            LogBuffer.Format(
                                "[PRICE_GUARD] CRITICAL: averageFillPrice=0 for {0}. Keeping intended price {1:F2}. NOT re-anchoring.",
                                kvp.Key,
                                pos.EntryPrice
                            )
                        );
                        SubmitBracketOrders(kvp.Key, pos);
                        return true;
                    }

                    pos.EntryFilled = true;
                    pos.InitialTargetCount = activeTargetCount;
                    pos.EntryPrice = averageFillPrice;
                    pos.ExtremePriceSinceEntry = averageFillPrice;
                    // Recalculate targets and stop
                    double stopDistance = pos.IsRMATrade
                        ? currentATR * RMAStopATRMultiplier
                        : Math.Abs(pos.InitialStopPrice - pos.EntryPrice);
                    pos.Target1Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 1);
                    pos.Target2Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 2);
                    pos.Target3Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 3);
                    pos.Target4Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 4);
                    pos.Target5Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 5);
                    stopDistance = Math.Min(stopDistance, 12.0);
                    pos.InitialStopPrice =
                        pos.Direction == MarketPosition.Long
                            ? averageFillPrice - stopDistance
                            : averageFillPrice + stopDistance;
                    pos.CurrentStopPrice = pos.InitialStopPrice;
                    ApplyTargetLadderGuard(pos);

                    Print(
                        LogBuffer.Format(
                            "{0} ENTRY FILLED: {1} {2} @ {3:F2}",
                            pos.IsRMATrade ? "RMA" : "OR",
                            pos.Direction,
                            pos.TotalContracts,
                            averageFillPrice
                        )
                    );
                    SubmitBracketOrders(kvp.Key, pos);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Handles target order fills (T1-T5). Extracted from HandleSecondaryOrderFilled.
        /// Phase 7 NEW-1: Complexity reduction (CYC 17 -> 4).
        /// </summary>
        /// <param name="order">The filled order</param>
        /// <param name="averageFillPrice">Average fill price</param>
        /// <param name="snapshot">Pre-allocated snapshot of active positions</param>
        /// <returns>True if order was a target and was handled</returns>
        private bool HandleSecondaryOrderFilled_Target(
            Order order,
            double averageFillPrice,
            KeyValuePair<string, PositionInfo>[] snapshot
        )
        {
            // Targets 1-5
            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (tDict != null && tDict.Values.Contains(order))
                {
                    foreach (var kvp in snapshot)
                    {
                        // Re-check existence (mutation safety)
                        if (!activePositions.ContainsKey(kvp.Key))
                            continue;
                        if (tDict.TryGetValue(kvp.Key, out var tOrder) && tOrder == order)
                        {
                            PositionInfo pos = kvp.Value;
                            ApplyTargetFill(
                                pos,
                                tNum,
                                GetTargetContracts(pos, tNum),
                                true,
                                out _,
                                out int appQty,
                                out int rem
                            );
                            Print(
                                LogBuffer.Format(
                                    "T{0} FILLED ({1}): {2} contracts @ {3:F2} | Remaining: {4}",
                                    tNum,
                                    kvp.Key,
                                    appQty,
                                    averageFillPrice,
                                    rem
                                )
                            );
                            UpdateStopQuantity(kvp.Key, pos);
                            tDict.TryRemove(kvp.Key, out _);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Handles stop order fills with dictionary lookup and name-based fallback.
        /// Extracted from HandleSecondaryOrderFilled.
        /// Phase 7 NEW-1: Complexity reduction (CYC 17 -> 4).
        /// CRITICAL: Mutation-safety guard prevents double-cleanup race condition.
        /// If platform delivers callbacks from multiple threads OR another actor message
        /// races between snapshot and cleanup, we must verify the key still exists.
        /// </summary>
        /// <param name="order">The filled order.</param>
        /// <param name="orderName">Order name for fallback matching.</param>
        /// <param name="averageFillPrice">Average fill price.</param>
        /// <param name="snapshot">Pre-allocated snapshot of active positions.</param>
        /// <returns>True if order was a stop and was handled.</returns>
        private bool HandleSecondaryOrderFilled_Stop(
            Order order,
            string orderName,
            double averageFillPrice,
            KeyValuePair<string, PositionInfo>[] snapshot
        )
        {
            // Stop filled.
            if (!orderName.StartsWith("Stop_") && !orderName.StartsWith("S_"))
            {
                return false;
            }

            foreach (var kvp in snapshot)
            {
                // Single TryGetValue for mutation safety + lookup efficiency.
                if (stopOrders.TryGetValue(kvp.Key, out var sOrder) && sOrder == order)
                {
                    // CRITICAL: Re-check existence before cleanup (mutation-safety guard).
                    // Prevents double-cleanup if another thread/actor removed the position.
                    if (activePositions.ContainsKey(kvp.Key))
                    {
                        Print(
                            LogBuffer.Format(
                                "STOP FILLED: {0} contracts @ {1:F2}",
                                kvp.Value.RemainingContracts,
                                averageFillPrice
                            )
                        );
                        CleanupPosition(kvp.Key);
                    }
                    return true;
                }
            }
            // Fallback by name.
            string entryName = ExtractEntryNameFromStop(orderName);
            if (activePositions.TryGetValue(entryName, out var pos))
            {
                Print(
                    LogBuffer.Format(
                        "STOP FILLED (by name): {0} contracts @ {1:F2}",
                        pos.RemainingContracts,
                        averageFillPrice
                    )
                );
                CleanupPosition(entryName);
                return true;
            }

            return false;
        }

        // Phase 7 NEW-1: Centralized prefix constants (Sourcery AI recommendation)
        private static readonly string[] TerminalTargetPrefixes = { "T1_", "T2_", "T3_", "T4_", "T5_", "Runner_" };

        /// <summary>
        /// Handles terminal target cleanup for T1-T5 and Runner orders.
        /// Extracted from HandleSecondaryOrderFilled.
        /// Phase 7 NEW-1: Complexity reduction (CYC 17 -> 4).
        /// </summary>
        /// <param name="order">The filled order.</param>
        /// <param name="orderName">Order name for prefix matching.</param>
        /// <returns>True if order was a terminal target and was handled.</returns>
        private bool HandleSecondaryOrderFilled_TerminalCleanup(Order order, string orderName)
        {
            foreach (var prefix in TerminalTargetPrefixes)
            {
                if (orderName.StartsWith(prefix))
                {
                    RemoveTargetReferenceOnTerminalFill(order);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Routes secondary order fills to specialized handlers.
        /// Refactored in Phase 7 NEW-1 to reduce complexity (CYC 17 -> 4).
        /// </summary>
        private bool HandleSecondaryOrderFilled(Order order, double averageFillPrice)
        {
            string orderName = order.Name;

            // [EPIC-5-PERF-T02] Single snapshot allocation at method start
            var snapshot = activePositions.ToArray();

            // Route to target handler
            if (HandleSecondaryOrderFilled_Target(order, averageFillPrice, snapshot))
            {
                return true;
            }

            // Route to stop handler
            if (HandleSecondaryOrderFilled_Stop(order, orderName, averageFillPrice, snapshot))
            {
                return true;
            }

            // Route to terminal cleanup handler
            if (HandleSecondaryOrderFilled_TerminalCleanup(order, orderName))
            {
                return true;
            }

            return false;
        }

        private string ExtractEntryNameFromStop(string orderName)
        {
            string stopPrefix = orderName.StartsWith("Stop_") ? "Stop_" : "S_";
            string entryNameFromOrder = orderName.Substring(stopPrefix.Length);
            int lastUnderscore = entryNameFromOrder.LastIndexOf('_');
            if (lastUnderscore > 0 && entryNameFromOrder.Length - lastUnderscore > 10)
                entryNameFromOrder = entryNameFromOrder.Substring(0, lastUnderscore);
            return entryNameFromOrder;
        }

        private bool HandleOrderRejected(Order order, string nativeError)
        {
            string orderName = order.Name;
            Print(LogBuffer.Format("ORDER REJECTED: {0} | Error: {1}", orderName, nativeError));

            // T04: Single snapshot for both stop and entry rejection paths
            var snapshot = activePositions.ToArray();

            if (stopOrders.Values.Contains(order))
            {
                foreach (var kvp in snapshot)
                {
                    if (!activePositions.ContainsKey(kvp.Key))
                        continue;
                    if (stopOrders.TryGetValue(kvp.Key, out var sOrder) && sOrder == order)
                    {
                        Print(LogBuffer.Format("(!) CRITICAL: Stop REJECTED for {0}. Re-submitting...", kvp.Key));
                        stopOrders.TryRemove(kvp.Key, out _);
                        CreateNewStopOrder(
                            kvp.Key,
                            kvp.Value.RemainingContracts,
                            kvp.Value.CurrentStopPrice,
                            kvp.Value.Direction
                        );
                        return true;
                    }
                }
            }

            if (entryOrders.Values.Contains(order))
            {
                foreach (var kvp in snapshot)
                {
                    if (!activePositions.ContainsKey(kvp.Key))
                        continue;
                    if (entryOrders.TryGetValue(kvp.Key, out var eOrder) && eOrder == order && !kvp.Value.EntryFilled)
                    {
                        Print(LogBuffer.Format("[ZOMBIE-FIX] Entry REJECTED: {0}. Tearing down.", orderName));
                        RollbackExpectedPosition(kvp.Key, kvp.Value);
                        CleanupPosition(kvp.Key);
                        return true;
                    }
                }
            }

            RemoveGhostOrderRef(order, "REJECTED");
            return true;
        }

        private void RollbackExpectedPosition(string entryName, PositionInfo pos)
        {
            string acctName =
                (pos.IsFollower && pos.ExecutingAccount != null) ? pos.ExecutingAccount.Name : Account.Name;
            int delta = (pos.Direction == MarketPosition.Long) ? -pos.TotalContracts : pos.TotalContracts;
            DeltaExpectedPositionLocked(ExpKey(acctName), delta);
            ClearDispatchSyncPending(ExpKey(acctName));
        }

        private bool HandleOrderCancelled(Order order)
        {
            string orderName = order.Name;
            bool handled = false;

            // Stop replacement check
            if (orderName.StartsWith("Stop_") || orderName.StartsWith("S_"))
            {
                handled = HandleOrderCancelled_ProcessStopReplacement(order);
                if (!handled)
                    HandleOrderCancelled_PurgePendingCleanup(order);
            }

            if (!handled && HandleOrderCancelled_RollbackUnfilledEntry(order))
                return true;

            RemoveGhostOrderRef(order, "CANCELLED");
            return true;
        }

        private bool HandleOrderCancelled_ProcessStopReplacement(Order order)
        {
            foreach (var kvp in pendingStopReplacements.ToArray())
            {
                if (
                    (
                        kvp.Value.OldOrder == order
                        || (kvp.Value.OldOrder != null && kvp.Value.OldOrder.OrderId == order.OrderId)
                    ) && activePositions.TryGetValue(kvp.Key, out var pos)
                )
                {
                    // Build 955: Snapshot qty under stateLock -- single atomic read for both check and use.
                    int _stopQty;
                    _stopQty = pos.RemainingContracts;
                    if (_stopQty > 0)
                    {
                        CreateNewStopOrder(kvp.Key, _stopQty, kvp.Value.StopPrice, kvp.Value.Direction);
                        // Build 950: Restore OCO-cascade-cancelled targets after stop replacement.
                        if (kvp.Value.BracketRestorationNeeded && kvp.Value.CapturedTargets != null)
                        {
                            TargetSnapshot[] _mSnap = kvp.Value.CapturedTargets;
                            string _mKey = kvp.Key;
                            TriggerCustomEvent(o => RestoreCascadedTargets(_mKey, _mSnap), null);
                        }
                    }
                    if (pendingStopReplacements.TryRemove(kvp.Key, out _))
                        Interlocked.Decrement(ref pendingReplacementCount);
                    return true;
                }
            }

            return false;
        }

        private void HandleOrderCancelled_PurgePendingCleanup(Order order)
        {
            // A2-2: Deferred PendingCleanup purge -- master stop terminal (Build 960 audit fix).
            // If no pendingStopReplacement matched, check if this stop cancel completes a
            // final-target/trim close where activePositions was intentionally kept alive.
            foreach (var kvp in stopOrders.ToArray())
            {
                if (kvp.Value == order)
                {
                    PositionInfo cleanupPos;
                    if (
                        activePositions.TryGetValue(kvp.Key, out cleanupPos)
                        && cleanupPos != null
                        && cleanupPos.PendingCleanup
                        && cleanupPos.RemainingContracts <= 0
                    )
                    {
                        stopOrders.TryRemove(kvp.Key, out _);
                        activePositions.TryRemove(kvp.Key, out _);
                        SymmetryGuardForgetEntry(kvp.Key);
                        Print("[A2-2] Deferred PendingCleanup purge (master stop cancel): " + kvp.Key);
                    }
                    break;
                }
            }
        }

        private bool HandleOrderCancelled_RollbackUnfilledEntry(Order order)
        {
            if (entryOrders.Values.Contains(order))
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (entryOrders.TryGetValue(kvp.Key, out var eOrder) && eOrder == order && !kvp.Value.EntryFilled)
                    {
                        if (EnableSIMA && !kvp.Value.IsFollower)
                            SymmetryGuardCascadeFollowerCleanup(kvp.Key);
                        RollbackExpectedPosition(kvp.Key, kvp.Value);
                        CleanupPosition(kvp.Key);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HandleOrderPriceOrQuantityChanged(Order order, double limitPrice, double stopPrice, int quantity)
        {
            if (entryOrders.Values.Contains(order))
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (entryOrders.TryGetValue(kvp.Key, out var eOrder) && eOrder == order && !kvp.Value.EntryFilled)
                    {
                        double newPrice = limitPrice > 0 ? limitPrice : stopPrice;
                        if (newPrice > 0 && Math.Abs(newPrice - kvp.Value.EntryPrice) > tickSize * 0.5)
                        {
                            kvp.Value.EntryPrice = newPrice;
                            Print(LogBuffer.Format("V12: Entry order MOVED: {0} to {1:F2}", kvp.Key, newPrice));
                        }
                        int _totalContracts;
                        _totalContracts = kvp.Value.TotalContracts;
                        if (quantity > 0 && quantity != _totalContracts)
                        {
                            // [937-FIX] Sync expectedPositions with broker-confirmed qty.
                            // Without this, RollbackExpectedPosition uses stale TotalContracts -> desync.
                            int qtyDiff = quantity - _totalContracts;
                            string fixAcct =
                                (kvp.Value.IsFollower && kvp.Value.ExecutingAccount != null)
                                    ? kvp.Value.ExecutingAccount.Name
                                    : Account.Name;
                            int expDelta = (kvp.Value.Direction == MarketPosition.Long) ? qtyDiff : -qtyDiff;
                            DeltaExpectedPositionLocked(ExpKey(fixAcct), expDelta);
                            Print(
                                LogBuffer.Format(
                                    "[937-FIX] expectedPositions adjusted on qty change: {0} delta={1}",
                                    fixAcct,
                                    expDelta
                                )
                            );
                            kvp.Value.TotalContracts = quantity;
                            kvp.Value.RemainingContracts = quantity;
                            GetTargetDistribution(
                                quantity,
                                out kvp.Value.T1Contracts,
                                out kvp.Value.T2Contracts,
                                out kvp.Value.T3Contracts,
                                out kvp.Value.T4Contracts,
                                out kvp.Value.T5Contracts
                            );
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
