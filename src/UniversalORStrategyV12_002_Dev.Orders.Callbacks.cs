// V12.44 MODULAR: Order Callbacks Module (Split from Orders.cs)
// Contains: OnOrderUpdate, OnAccountOrderUpdate, OnPositionUpdate, OnExecutionUpdate
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
    public partial class UniversalORStrategyV12_002_Dev : Strategy
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
            out int remainingContractsAfter)
        {
            alreadyProcessed = false;
            appliedQty = 0;
            remainingContractsAfter = 0;

            lock (stateLock)
            {
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
        }

        // V12.1101E [F-07]: Request stop cancellation without dropping dictionary state early.
        // We only remove references after broker-confirmed terminal states.
        // [BUILD 925 - P1 Fix]: Route follower stop cancels through pos.ExecutingAccount.Cancel()
        // instead of the master-local CancelOrder() API. CancelOrder() is a NinjaScript-managed
        // call that only works for orders submitted via SubmitOrderUnmanaged(). Fleet follower
        // stops are submitted via acct.Submit(), so they require the broker-level Account.Cancel()
        // API — identical to the pattern already proven correct in CleanupPosition() [BUG-2a].
        private void RequestStopCancelLifecycleSafe(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return;
            if (!stopOrders.TryGetValue(entryName, out var stopOrder) || stopOrder == null) return;

            // V12.1101H [COLLIDE-01]: Include ChangePending/ChangeSubmitted — stops in these transient
            // states were previously ignored by this function, leaving them live at the broker after FlattenAll.
            if (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted
                || stopOrder.OrderState == OrderState.ChangePending || stopOrder.OrderState == OrderState.ChangeSubmitted)
            {
                // [BUILD 925 - P1 Fix]: Check if this is a fleet follower — use its account context.
                bool isFollowerStop = activePositions.TryGetValue(entryName, out var posRef)
                    && posRef != null && posRef.IsFollower && posRef.ExecutingAccount != null;

                if (isFollowerStop)
                {
                    // Fleet follower stop: must use Account API — CancelOrder() targets master account only.
                    Print(string.Format("[925-P1] Follower stop cancel routed via ExecutingAccount.Cancel() for {0} on {1}",
                        entryName, posRef.ExecutingAccount.Name));
                    posRef.ExecutingAccount.Cancel(new[] { stopOrder });
                }
                else
                {
                    // Master/local stop: use the standard NinjaScript managed cancel.
                    CancelOrder(stopOrder);
                }
                return;
            }

            if (stopOrder.OrderState == OrderState.Cancelled || stopOrder.OrderState == OrderState.Filled ||
                stopOrder.OrderState == OrderState.Rejected || stopOrder.OrderState == OrderState.Unknown)
            {
                stopOrders.TryRemove(entryName, out _);
            }
        }

        // V12.1101E [F-07]: Broker-confirmed target cleanup fallback when position state was already torn down.
        private bool TryRemoveTargetReferenceByOrder(ConcurrentDictionary<string, Order> dict, Order order)
        {
            if (dict == null || order == null) return false;
            foreach (var kvp in dict.ToArray())
            {
                if (kvp.Value == order)
                    return dict.TryRemove(kvp.Key, out _);
            }
            return false;
        }

        // V12.1101E [F-07]: Removes terminal target refs using broker-confirmed order object identity.
        private void RemoveTargetReferenceOnTerminalFill(Order order)
        {
            if (order == null) return;
            if (TryRemoveTargetReferenceByOrder(target1Orders, order)) return;
            if (TryRemoveTargetReferenceByOrder(target2Orders, order)) return;
            if (TryRemoveTargetReferenceByOrder(target3Orders, order)) return;
            if (TryRemoveTargetReferenceByOrder(target4Orders, order)) return;
            TryRemoveTargetReferenceByOrder(target5Orders, order);
        }

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice, OrderState orderState,
            DateTime time, ErrorCode error, string nativeError)
        {
            try
            {
                string orderName = order.Name;

                if (order.Account == this.Account && 
                    (orderState == OrderState.Working || orderState == OrderState.Accepted || orderState == OrderState.ChangeSubmitted))
                {
                    PropagateMasterPriceMove(order, limitPrice, stopPrice, quantity);
                }

                // Entry filled
                if (entryOrders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        // V8.30: Verify position still exists
                        if (!activePositions.ContainsKey(entryName)) continue;

                        // V8.30: Thread-safe check
                        if (entryOrders.TryGetValue(entryName, out var entryOrder) && entryOrder == order && !pos.EntryFilled)
                        {
                            pos.EntryFilled = true;
                            pos.InitialTargetCount = activeTargetCount;  // Build 1102Y-V2 [U-03]: snapshot at fill time
                            if (!pos.IsFollower)
                            {
                                int masterFillQty = filled > 0 ? filled : quantity;
                                SymmetryGuardOnMasterFill(entryName, pos, averageFillPrice, masterFillQty, time.ToUniversalTime());
                            }

                            // Store intended entry price for slippage calculation
                            double intendedEntryPrice = pos.EntryPrice;

                            // V12.18 PRICE INTEGRITY: Guard against zero fill price (NT8 race condition)
                            if (averageFillPrice <= 0)
                            {
                                Print(string.Format("[PRICE_GUARD] CRITICAL: averageFillPrice={0} for {1}. Keeping intended price {2:F2}. NOT re-anchoring.",
                                    averageFillPrice, entryName, intendedEntryPrice));
                                SubmitBracketOrders(entryName, pos);
                                continue;  // Skip all price re-anchoring, use intended prices
                            }

                            string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                            if (pos.IsMOMOTrade) tradeType = "MOMO"; // V8.22: Logging
                            if (pos.IsFFMATrade) tradeType = "FFMA";
                            if (pos.IsTRENDTrade) tradeType = "TREND";
                            if (pos.IsRetestTrade) tradeType = "RETEST";

                            Print(string.Format("{0} ENTRY FILLED: {1} {2} @ {3:F2} (intended: {4:F2})",
                                tradeType,
                                pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                pos.TotalContracts,
                                averageFillPrice,
                                intendedEntryPrice));

                            // V8.22: UNIVERSAL STOP CAP FIX
                            // Determine the intended stop distance
                            double stopDistance = 0;

                            if (pos.IsRMATrade)
                            {
                                // For RMA, use current ATR to be precise
                                Print(string.Format("DIAGNOSTIC: RMA Entry Filled. Raw ATR used: {0:F4} | Multiplier: {1:F2} | Calc Stop: {2:F4} pts",
                                    currentATR, RMAStopATRMultiplier, currentATR * RMAStopATRMultiplier));
                                stopDistance = currentATR * RMAStopATRMultiplier;
                            }
                            else
                            {
                                // For other trades, use the distance from the intended setup
                                stopDistance = Math.Abs(pos.InitialStopPrice - intendedEntryPrice);
                            }

                            // Recalculate all five targets from actual fill price.
                            // Universal Ladder: CalculateTargetPriceFromPos delegates to CalculateTargetPrice —
                            // T(n)Type dropdown × Target(n)Value is the sole pricing oracle for all modes.
                            pos.Target1Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 1);
                            pos.Target2Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 2);
                            pos.Target3Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 3);
                            pos.Target4Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 4);
                            pos.Target5Price = CalculateTargetPriceFromPos(pos.Direction, averageFillPrice, pos, 5);

                            // Build 1102Y-V3 [LG-02]: Re-validate the target staircase after fill-price re-anchoring.
                            // A fill at a different price than the limit can cause ATR-based slots to invert
                            // relative to the fixed Scalp. Guard corrects this before brackets are submitted.
                            ApplyTargetLadderGuard(pos);
                            Print(string.Format("[LADDER_GUARD] Post-fill ladder validated for {0} @ fill={1:F4}", entryName, averageFillPrice));

                            // GLOBAL SAFETY CAP: Absolutely NO stop > 8.0 points
                            double originalDist = stopDistance;
                            stopDistance = Math.Min(stopDistance, 12.0);

                            if (stopDistance < originalDist)
                            {
                                Print(string.Format("CRITICAL: {0} Stop capped at 12.0 pts (Calculated: {1:F2} pts)",
                                    tradeType, originalDist));
                            }

                            // Re-anchor stop to ACTUAL fill price
                            pos.InitialStopPrice = pos.Direction == MarketPosition.Long
                                ? averageFillPrice - stopDistance
                                : averageFillPrice + stopDistance;
                            pos.CurrentStopPrice = pos.InitialStopPrice;

                            if (Math.Abs(averageFillPrice - intendedEntryPrice) > tickSize)
                            {
                                Print(string.Format("{0} PRICES ADJUSTED for fill slippage: Stop={1:F2} (Dist={2:F2})",
                                    tradeType, pos.InitialStopPrice, stopDistance));
                            }

                            // Update to actual fill price
                            pos.EntryPrice = averageFillPrice;
                            pos.ExtremePriceSinceEntry = averageFillPrice;

                            SubmitBracketOrders(entryName, pos);
                        }
                    }
                }

                // Target 1-5 filled: unified loop
                // V12.Phase7 [H-04]: atomic TryGetValue to eliminate TOCTOU race.
                // V12.1101E [SK-01/A-1]: First-Writer-Wins guard prevents double-decrement.
                if (orderState == OrderState.Filled)
                {
                    for (int tNum = 1; tNum <= 5; tNum++)
                    {
                        var tDict = GetTargetOrdersDictionary(tNum);
                        if (tDict == null || !tDict.Values.Contains(order)) continue;

                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!activePositions.TryGetValue(kvp.Key, out PositionInfo pos)) continue;
                            if (!tDict.TryGetValue(kvp.Key, out var tOrder) || tOrder != order) continue;

                            int targetContracts = GetTargetContracts(pos, tNum);
                            bool alreadyProcessed;
                            int appliedQty;
                            int remainingAfter;
                            ApplyTargetFill(pos, tNum, targetContracts, true, out alreadyProcessed, out appliedQty, out remainingAfter);
                            if (alreadyProcessed)
                            {
                                Print(string.Format("[1101E GUARD] T{0} already processed for {1} — skipping duplicate OnOrderUpdate fill", tNum, kvp.Key));
                                break;
                            }

                            Print(string.Format("T{0} FILLED ({1}): {2}/{3} contracts @ {4:F2} | Remaining: {5}",
                                tNum, kvp.Key, appliedQty, targetContracts, averageFillPrice, remainingAfter));

                            UpdateStopQuantity(kvp.Key, pos);
                            // V12.1101E [F-07]: Remove target ref only after broker-confirmed Filled event.
                            tDict.TryRemove(kvp.Key, out _);
                            break;
                        }
                    }
                }

                // V12.1101E [F-07]: Terminal target cleanup fallback keyed by broker-filled state.
                if (orderState == OrderState.Filled &&
                    (orderName.StartsWith("T1_") || orderName.StartsWith("T2_") || orderName.StartsWith("T3_") ||
                     orderName.StartsWith("T4_") || orderName.StartsWith("T5_")))
                {
                    RemoveTargetReferenceOnTerminalFill(order);
                }

                // Stop filled - position closed
                // V8.2 FIX: Check both by object reference AND by order name prefix
                // This handles trailed stops that have DateTime.Ticks suffix in their name
                if (orderState == OrderState.Filled && orderName.StartsWith("Stop_"))
                {
                    // Try exact object match first
                    bool foundByReference = false;
                    if (stopOrders.Values.Contains(order))
                    {
                        // V8.30: Thread-safe snapshot iteration
                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!activePositions.ContainsKey(kvp.Key)) continue;
                            if (stopOrders.TryGetValue(kvp.Key, out var stopOrder) && stopOrder == order)
                            {
                                PositionInfo pos = kvp.Value;
                                Print(string.Format("STOP FILLED: {0} contracts @ {1:F2}", pos.RemainingContracts, averageFillPrice));
                                CleanupPosition(kvp.Key);
                                foundByReference = true;
                                break;
                            }
                        }
                    }

                    // V8.2 FIX: Fallback - match by order name prefix
                    // Order name format: "Stop_TREND_175232_E2_12345678" - extract "TREND_175232_E2"
                    if (!foundByReference)
                    {
                        // Extract entry name from stop order name (removes "Stop_" prefix and optional "_timestamp" suffix)
                        string stopPrefix = "Stop_";
                        string entryNameFromOrder = orderName.Substring(stopPrefix.Length);
                        // Remove timestamp suffix if present (format: _123456789012345)
                        int lastUnderscore = entryNameFromOrder.LastIndexOf('_');
                        if (lastUnderscore > 0 && entryNameFromOrder.Length - lastUnderscore > 10)
                        {
                            entryNameFromOrder = entryNameFromOrder.Substring(0, lastUnderscore);
                        }

                        // V8.30: Thread-safe access
                        if (activePositions.TryGetValue(entryNameFromOrder, out var pos))
                        {
                            Print(string.Format("STOP FILLED (by name): {0} contracts @ {1:F2}", pos.RemainingContracts, averageFillPrice));
                            CleanupPosition(entryNameFromOrder);
                        }
                    }
                }

                // Order rejected
                if (orderState == OrderState.Rejected)
                {
                    Print(string.Format("ORDER REJECTED: {0} | Error: {1}", orderName, nativeError));

                    // CRITICAL v5.8: Check if this was a stop order rejection
                    if (stopOrders.Values.Contains(order))
                    {
                        Print(string.Format("âš ï¸ CRITICAL: Stop order REJECTED: {0}", orderName));

                        // V8.30: Thread-safe snapshot iteration
                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!activePositions.ContainsKey(kvp.Key)) continue;
                            if (stopOrders.TryGetValue(kvp.Key, out var stopOrder) && stopOrder == order)
                            {
                                PositionInfo pos = kvp.Value;
                                Print(string.Format("âš ï¸ Position {0} is UNPROTECTED: {1} {2} contracts @ {3:F2}",
                                    kvp.Key,
                                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                    pos.RemainingContracts,
                                    pos.EntryPrice));

                                // V12.12: Remove stale rejected stop, then re-submit directly
                                // Cannot use UpdateStopQuantity â€" it early-exits if stopOrders is empty (line 3044)
                                // and the cancel-replace flow doesn't apply to a rejected (non-working) order.
                                Print(string.Format("Attempting to re-submit stop for {0}...", kvp.Key));
                                stopOrders.TryRemove(kvp.Key, out _);
                                CreateNewStopOrder(kvp.Key, pos.RemainingContracts, pos.CurrentStopPrice, pos.Direction);
                                break;
                            }
                        }
                    }

                    // ZOMBIE-FIX: Entry order rejected — zero expectedPositions to prevent REAPER ghost-repair loop.
                    // Cancellation already does this (Callbacks.cs ~line 591) but rejection takes a
                    // different code path with no memory cleanup, leaving Expected > Actual indefinitely.
                    if (entryOrders.Values.Contains(order))
                    {
                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (entryOrders.TryGetValue(kvp.Key, out var rejEntryOrder) && rejEntryOrder == order && !kvp.Value.EntryFilled)
                            {
                                string rejAcctName = (kvp.Value.IsFollower && kvp.Value.ExecutingAccount != null)
                                    ? kvp.Value.ExecutingAccount.Name
                                    : Account.Name;
                                Print(string.Format("[ZOMBIE-FIX] Entry REJECTED: {0}. Tearing down memory for acct={1}.", orderName, rejAcctName));
                                CleanupPosition(kvp.Key);
                                SetExpectedPositionLocked(ExpKey(rejAcctName), 0);
                                Print(string.Format("[ZOMBIE-FIX] expectedPositions zeroed for {0} after rejection.", ExpKey(rejAcctName)));
                                break;
                            }
                        }
                    }

                    // V12.12: Target order rejected - remove stale reference from dictionary
                    Print(string.Format("[GHOST_FIX] Order {0} terminated (REJECTED). Nullifying reference.", orderName));
                    RemoveGhostOrderRef(order, "REJECTED");
                }

                // V12: Entry order price changed
                // This detects when user drags the order line to a new price
                if (entryOrders.Values.Contains(order) && (orderState == OrderState.Accepted || orderState == OrderState.Working))
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        // V8.30: Thread-safe check
                        if (entryOrders.TryGetValue(entryName, out var entryOrd) && entryOrd == order && !pos.EntryFilled)
                        {
                            // Get the new price from the order (limit orders use limitPrice, stop orders use stopPrice)
                            double newPrice = limitPrice > 0 ? limitPrice : stopPrice;

                            // V12.18 PRICE INTEGRITY: Guard against zero price from NT8 race condition
                            if (newPrice <= 0)
                            {
                                Print(string.Format("[PRICE_GUARD] Entry-moved: newPrice={0} for {1}. Skipping re-anchor.", newPrice, entryName));
                                break;
                            }

                            // Check if price changed (with tick tolerance)
                            if (Math.Abs(newPrice - pos.EntryPrice) > tickSize * 0.5)
                            {
                                double oldPrice = pos.EntryPrice;
                                pos.EntryPrice = newPrice;

                                Print(string.Format("V12: Entry order MOVED: {0} | {1:F2} â†' {2:F2}", entryName, oldPrice, newPrice));

                                // V12 SIMA: Legacy slave broadcast removed
                            }

                            // [VOLATILITY-01]: Broker-confirmed quantity sync.
                            // SyncPendingOrders defers TotalContracts updates until ChangeOrder is confirmed here.
                            // quantity == 0 on some NT8 callbacks; guard prevents accidental zeroing.
                            if (quantity > 0 && quantity != pos.TotalContracts)
                            {
                                int oldQty = pos.TotalContracts;
                                lock (stateLock)
                                {
                                    pos.TotalContracts = quantity;
                                    int t1, t2, t3, t4, t5;
                                    GetTargetDistribution(quantity, out t1, out t2, out t3, out t4, out t5);
                                    pos.T1Contracts = t1;
                                    pos.T2Contracts = t2;
                                    pos.T3Contracts = t3;
                                    pos.T4Contracts = t4;
                                    pos.T5Contracts = t5;
                                    pos.RemainingContracts = quantity;
                                }
                                Print(string.Format("[V12.6.2 QTY-SYNC] {0}: TotalContracts {1}→{2} confirmed by broker", entryName, oldQty, quantity));
                            }
                            break;
                        }
                    }
                }

                // V12.13: Coordination flag â€" prevents redundant ghost-scan after explicit handling
                bool handledByExplicitCleanup = false;

                // V8.11: Stop order cancelled - check for pending replacement
                // V12.13: Extended to also match "S_" prefix (replacement stops from CreateNewStopOrder)
                // V14 GHOST FIX: Added check for RemainingContracts > 0 to prevent ghost stop resurrection
                if ((orderName.StartsWith("Stop_") || orderName.StartsWith("S_")) && orderState == OrderState.Cancelled)
                {
                    // V8.30: Thread-safe snapshot iteration with TryRemove
                    foreach (var kvp in pendingStopReplacements.ToArray())
                    {
                        string entryName = kvp.Key;
                        PendingStopReplacement pending = kvp.Value;

                        // V8.24 FIX: REMOVED recursive 'Contains' check. STRICT object match only.
                        if (activePositions.TryGetValue(entryName, out var pos) && pending.OldOrder == order)
                        {
                            // V14 GHOST FIX: CRITICAL CHECK
                            // Only create replacement if we actually hold a position!
                            int replacementQty;
                            lock (stateLock)
                            {
                                replacementQty = pos.RemainingContracts;
                            }
                            if (replacementQty > 0)
                            {
                                // V12.Audit [S-008]: Re-verify position still exists before submitting replacement stop.
                                // A concurrent target fill may have called CleanupPosition between our lock acquisition
                                // and this point, making the position flat.
                                if (!activePositions.TryGetValue(entryName, out PositionInfo replacementPos) || replacementPos.RemainingContracts <= 0)
                                {
                                    Print(string.Format("[STOP_GUARD] Position {0} closed before replacement stop could be submitted. Skipping.", entryName));
                                    // Clean up any pending replacement state
                                    pendingStopReplacements.TryRemove(entryName, out _);
                                    handledByExplicitCleanup = true;
                                    break;
                                }

                                Print(string.Format("STOP CANCELLED (confirmed): {0} | Creating replacement...", entryName));

                                // V12.1101E [F-01]: Use live RemainingContracts, not pending.Quantity.
                                // Prevents over-size stop when T1+T2 fill simultaneously and T2's UpdateStopQuantity
                                // has not yet run when T1's cancel confirms.
                                CreateNewStopOrder(entryName, replacementQty, pending.StopPrice, pending.Direction);
                            }
                            else
                            {
                                Print(string.Format("V14 GHOST FIX: Stop cancelled for FLAT position {0}. Discarding replacement.", entryName));
                            }

                            // V8.30: Thread-safe removal with count decrement
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }
                            handledByExplicitCleanup = true;
                            break;
                        }
                        else if (!activePositions.ContainsKey(entryName))
                        {
                            Print(string.Format("STOP CANCELLED: {0} ignored (position already closed/cleaned)", entryName));
                            // V8.30: Thread-safe removal with count decrement
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }
                            handledByExplicitCleanup = true;
                            break;
                        }
                    }
                }

                // V12.13c: Manual stop cancel â€" clean up dictionary ref, no auto re-submission
                // User can cancel their own stops; position becomes unprotected by design
                if (!handledByExplicitCleanup && orderState == OrderState.Cancelled &&
                    (orderName.StartsWith("Stop_") || orderName.StartsWith("S_")))
                {
                    foreach (var kvp in stopOrders.ToArray())
                    {
                        if (kvp.Value == order)
                        {
                            string entryName = kvp.Key;
                            if (stopOrders.TryRemove(entryName, out _))
                            {
                                Print(string.Format("[GHOST_FIX] Order Stop_{0} terminated (CANCELLED). Nullifying reference.", entryName));
                                handledByExplicitCleanup = true;
                            }
                            break;
                        }
                    }
                }

                // V12.13: Manual target cancel â€" user cancelled target from chart
                if (!handledByExplicitCleanup && orderState == OrderState.Cancelled)
                {
                    var targetDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
                    {
                        (target1Orders, "T1"), (target2Orders, "T2"),
                        (target3Orders, "T3"), (target4Orders, "T4"), (target5Orders, "T5"),
                    };
                    foreach (var (dict, label) in targetDicts)
                    {
                        foreach (var kvp in dict.ToArray())
                        {
                            if (kvp.Value == order)
                            {
                                if (dict.TryRemove(kvp.Key, out _))
                                {
                                    Print(string.Format("[GHOST_FIX] Order {0}_{1} terminated (CANCELLED). Nullifying reference.", label, kvp.Key));
                                    handledByExplicitCleanup = true;
                                }
                                break;
                            }
                        }
                        if (handledByExplicitCleanup) break;
                    }
                }

                // V12: Entry order cancelled
                if (entryOrders.Values.Contains(order) && orderState == OrderState.Cancelled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        if (entryOrders.TryGetValue(entryName, out var entryOrder) && entryOrder == order && !pos.EntryFilled)
                        {
                            Print(string.Format("[GHOST_FIX] Order Entry_{0} terminated (CANCELLED). Nullifying reference. Full teardown.", entryName));

                            // Clean up local state
                            CleanupPosition(entryName);

                            // Build 1102U [BUG-2b]: Clear Ghost Memory so REAPER does not see Expected(1) > Actual(0)
                            // and trigger an infinite repair loop (re-submitting the just-cancelled order).
                            string cancelAcctName = (pos.IsFollower && pos.ExecutingAccount != null)
                                ? pos.ExecutingAccount.Name
                                : Account.Name;
                            SetExpectedPositionLocked(ExpKey(cancelAcctName), 0);
                            Print(string.Format("[1102U] Ghost Memory cleared for {0} after entry cancel.", ExpKey(cancelAcctName)));

                            handledByExplicitCleanup = true;
                            break;
                        }
                    }
                }

                // V12.13: Terminal catch-all â€" ONLY fires if no explicit handler above already cleaned this order
                if (!handledByExplicitCleanup &&
                    (orderState == OrderState.Cancelled || orderState == OrderState.Rejected ||
                     orderState == OrderState.Unknown))
                {
                    string reason = orderState.ToString().ToUpper();
                    RemoveGhostOrderRef(order, reason);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnOrderUpdate: " + ex.Message);
            }
        }

        private void OnAccountOrderUpdate(object sender, OrderEventArgs e)
        {
            if (e == null || e.Order == null) return;

            Order order = e.Order;
            if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

            if (order.OrderState != OrderState.Cancelled && order.OrderState != OrderState.Rejected &&
                order.OrderState != OrderState.Unknown)
            {
                return;
            }

            // V12.1101E [TM-01]: Marshal broker-thread callback to strategy thread before mutating strategy state.
            _accountOrderQueue.Enqueue(new QueuedAccountOrderUpdate
            {
                Account = sender as Account,
                EventArgs = e
            });
            try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
        }

        private void ProcessAccountOrderQueue()
        {
            // V12.Phase7 [THREAD-01a]: Buffer-and-wait during flatten (symmetric with ProcessAccountExecutionQueue).
            // Keep queued order events intact and retry when flatten releases.
            if (isFlattenRunning)
            {
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
                return;
            }

            QueuedAccountOrderUpdate item;
            while (_accountOrderQueue.TryDequeue(out item))
            {
                if (isFlattenRunning)
                {
                    _accountOrderQueue.Enqueue(item);
                    try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
                    return;
                }

                try
                {
                    if (item.EventArgs == null || item.EventArgs.Order == null) continue;

                    Order order = item.EventArgs.Order;
                    if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) continue;

                    if (order.OrderState != OrderState.Cancelled && order.OrderState != OrderState.Rejected &&
                        order.OrderState != OrderState.Unknown)
                    {
                        continue;
                    }

                    Account acct = item.Account;
                    string acctName = acct != null ? acct.Name : "UNKNOWN";
                    string reason = order.OrderState.ToString().ToUpper();
                    string orderId = order.OrderId ?? "NULL";

                    // V12.17: Enhanced trace logging
                    Print(string.Format("[GHOST-AUDIT] OnAccountOrderUpdate ENTRY: Name={0} | Id={1} | State={2} | Acct={3}",
                        order.Name, orderId, reason, acctName));

                    // V12.17: Match by reference OR by OrderId (NT8 may pass a different object for the same logical order)
                    string matchedEntry = null;
                    string matchedBy = "NONE";
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        PositionInfo pos = kvp.Value;
                        if (!pos.IsFollower || pos.ExecutingAccount == null) continue;
                        if (acct != null && pos.ExecutingAccount != acct) continue;

                        // V12.17: Dual match - reference equality OR OrderId string match
                        if ((entryOrders.TryGetValue(kvp.Key, out var eOrder) && (eOrder == order || (eOrder != null && eOrder.OrderId == order.OrderId))) ||
                            (stopOrders.TryGetValue(kvp.Key, out var sOrder) && (sOrder == order || (sOrder != null && sOrder.OrderId == order.OrderId))) ||
                            (target1Orders.TryGetValue(kvp.Key, out var t1Order) && (t1Order == order || (t1Order != null && t1Order.OrderId == order.OrderId))) ||
                            (target2Orders.TryGetValue(kvp.Key, out var t2Order) && (t2Order == order || (t2Order != null && t2Order.OrderId == order.OrderId))) ||
                            (target3Orders.TryGetValue(kvp.Key, out var t3Order) && (t3Order == order || (t3Order != null && t3Order.OrderId == order.OrderId))) ||
                            (target4Orders.TryGetValue(kvp.Key, out var t4Order) && (t4Order == order || (t4Order != null && t4Order.OrderId == order.OrderId))) ||
                            (target5Orders.TryGetValue(kvp.Key, out var t5Order) && (t5Order == order || (t5Order != null && t5Order.OrderId == order.OrderId))))
                        {
                            matchedEntry = kvp.Key;
                            matchedBy = "DUAL";
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(matchedEntry) && activePositions.TryGetValue(matchedEntry, out var matchedPos))
                    {
                        Print(string.Format("[GHOST-AUDIT] MATCHED: Entry={0} | MatchBy={1} | Acct={2}", matchedEntry, matchedBy, acctName));

                        if (matchedPos.IsFollower && matchedPos.ExecutingAccount != null)
                        {
                            if (entryOrders.TryGetValue(matchedEntry, out var entryOrder) &&
                                (entryOrder == order || (entryOrder != null && entryOrder.OrderId == order.OrderId)) &&
                                !matchedPos.EntryFilled)
                            {
                                // [M8.1 GF-01] Follower entry cancelled — determine if intentional or genuine desync.
                                // Remove the dead entry order ref in all cases.
                                entryOrders.TryRemove(matchedEntry, out _);

                                // [GF-01 GRACEFUL FLUSH]: If expectedPositions is already 0, the SIMA cascade
                                // already cleaned up this position synchronously before this confirmation arrived.
                                // Skipping the alarm prevents the persistent "Ghost Label" on the chart.
                                int gfExpected = 0;
                                lock (stateLock) { expectedPositions.TryGetValue(ExpKey(acctName), out gfExpected); }

                                if (gfExpected == 0)
                                {
                                    Print(string.Format("[GF-01] Graceful flush: {0} on {1} — cascade-cleared. Alarm suppressed.", matchedEntry, acctName));
                                    try { RemoveDrawObject("SIMA_DESYNC_" + acctName); } catch { }
                                    continue;
                                }

                                // expectedPositions != 0 → genuine/unintentional desync. Preserve for REAPER.
                                string desyncMsg = string.Format(
                                    "[SIMA] Follower desync: {0} on {1} ({2}) — entry cancelled. Reaper monitoring. Phase 8.2 re-entry pending.",
                                    matchedEntry, acctName, reason);
                                Print(desyncMsg);

                                // Visual alert — persistent red label top-left on strategy chart
                                Draw.TextFixed(this, "SIMA_DESYNC_" + acctName,
                                    string.Format("⚠ FOLLOWER DESYNC: {0} on {1}", matchedEntry, acctName),
                                    TextPosition.TopLeft, Brushes.Red, new SimpleFont("Arial", 11),
                                    Brushes.Transparent, Brushes.Transparent, 50);

                                // Audio alert — rearms every 10 seconds while condition persists
                                Alert("SIMA_DESYNC_" + acctName, Priority.High, desyncMsg,
                                    "Alert1.wav", 10,
                                    Brushes.Red, Brushes.White);

                                continue; // activePositions and expectedPositions preserved for Reaper
                            }

                            Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, orderId));
                            RemoveGhostOrderRef(order, reason);
                            continue;
                        }
                    }
                    else
                    {
                        Print(string.Format("[GHOST-AUDIT] NO MATCH in activePositions for OrderId={0} Name={1} on {2}", orderId, order.Name, acctName));

                        // V12.18 SIMA CASCADE: If a master-account order was cancelled,
                        // check if any follower positions share the same base signal and tear them down.
                        // [MOVE-SYNC Guard]: Only fire for master-account cancels. Follower cancels
                        // triggered by PropagateMasterPriceMove (intentional resubmit) must NOT reach
                        // this path — they would cause premature CleanupPosition (friendly fire).
                        if (EnableSIMA && order.OrderState == OrderState.Cancelled && order.Account == this.Account)
                        {
                            string orderSignal = order.Name;
                            foreach (var kvp in activePositions.ToArray())
                            {
                                PositionInfo cascadePos = kvp.Value;
                                if (!cascadePos.IsFollower) continue;
                                // Match if the follower's signal name contains the order's signal
                                if (kvp.Key.Contains(orderSignal) || orderSignal.Contains(kvp.Key))
                                {
                                    // Only tear down if the follower's entry hasn't filled yet
                                    if (!cascadePos.EntryFilled)
                                    {
                                        string cascadeAcctName = cascadePos.ExecutingAccount != null
                                            ? cascadePos.ExecutingAccount.Name : "NULL";
                                        Print(string.Format("[GHOST_FIX] SIMA CASCADE: Master cancel of {0} triggers follower teardown for {1} on {2}",
                                            orderSignal, kvp.Key, cascadeAcctName));
                                        CleanupPosition(kvp.Key);

                                        // Build 1102U [BUG-2b]: Clear Ghost Memory after cascade teardown so REAPER
                                        // does not see Expected(1) > Actual(0) and trigger an infinite repair loop.
                                        if (cascadePos.ExecutingAccount != null)
                                        {
                                            SetExpectedPositionLocked(ExpKey(cascadeAcctName), 0);
                                            Print(string.Format("[1102U] Ghost Memory cleared for {0} after SIMA cascade cancel.", ExpKey(cascadeAcctName)));
                                            // [GF-01 SWEEP]: Remove any DESYNC label drawn before the cascade fired (race edge case).
                                            try { RemoveDrawObject("SIMA_DESYNC_" + cascadeAcctName); } catch { }
                                        }
                                    }
                                    else
                                    {
                                        // DEAD-01: Follower entry is ALREADY FILLED — master order cancelled.
                                        // CleanupPosition alone cannot remove a live broker position.
                                        // Emit emergency fleet kill: cancel working orders + submit Market close.
                                        string cascadeAcctName = cascadePos.ExecutingAccount != null
                                            ? cascadePos.ExecutingAccount.Name : "NULL";
                                        Print(string.Format("[DEAD-01] CASCADE-FILLED: Master cancel {0} — follower {1} on {2} is FILLED. Issuing emergency flatten.",
                                            orderSignal, kvp.Key, cascadeAcctName));
                                        if (cascadePos.ExecutingAccount != null)
                                        {
                                            Account filledFollowerAcct = cascadePos.ExecutingAccount;
                                            TriggerCustomEvent(o => EmergencyFlattenSingleFleetAccount(filledFollowerAcct), null);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fallback: clear any stale reference for terminal follower order states
                    RemoveGhostOrderRef(order, reason);
                }
                catch (Exception ex)
                {
                    Print("ERROR ProcessAccountOrderQueue: " + ex.Message);
                }
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            try
            {
                // Check for EXTERNAL close (position went flat from outside strategy)
                if (marketPosition == MarketPosition.Flat)
                {
                    // EMERGENCY FIX [H-14]: Sync expectedPositions when this account's position goes flat.
                    // Prevents dispatch loop from seeing stale expected=1 and re-entering on every bar.
                    string flatAcctName = position?.Account?.Name;
                    if (!string.IsNullOrEmpty(flatAcctName))
                    {
                        // Build 1102U [BUG-1]: Composite key — only clears THIS instrument's slot on this account.
                        SetExpectedPositionLocked(ExpKey(flatAcctName), 0);
                        Print($"[OnPositionUpdate] expectedPositions cleared for {ExpKey(flatAcctName)} (position flat)");
                    }

                    // V8.22: Even if activePositions is empty (strategy restart), we should scan for orphans
                    if (activePositions.Count == 0)
                    {
                        Print("EXTERNAL CLOSE/RESTART DETECTED - Scanning for orphaned bracket orders...");
                        ReconcileOrphanedOrders("Position went flat");
                        return;
                    }

                    // Check if we still have any positions that think they're filled
                    List<string> positionsToCleanup = new List<string>();

                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        PositionInfo pos = kvp.Value;
                        if (pos.EntryFilled && pos.RemainingContracts > 0)
                        {
                            Print("EXTERNAL CLOSE DETECTED - Position went flat. Cancelling orphaned orders...");

                            // V8.30: Thread-safe order access
                            if (stopOrders.TryGetValue(kvp.Key, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                }
                            }

                            // Cancel orphaned target orders (T1-T5)
                            for (int tNum = 1; tNum <= 5; tNum++)
                            {
                                var tDict = GetTargetOrdersDictionary(tNum);
                                if (tDict != null && tDict.TryGetValue(kvp.Key, out var tOrder))
                                {
                                    if (tOrder != null && (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted))
                                        CancelOrder(tOrder);
                                }
                            }

                            positionsToCleanup.Add(kvp.Key);
                        }
                    }

                    // REMOVED v5.7: DO NOT cancel unrelated pending entry orders!
                    // The old logic here cancelled ALL pending entries when position went flat,
                    // which incorrectly cancelled opposite-side OR entries (e.g., ORShort when ORLong closed)
                    // Pending entries should remain active - they are independent trades!

                    // Clean up positions
                    foreach (string key in positionsToCleanup)
                    {
                        CleanupPosition(key);
                    }

                    if (positionsToCleanup.Count > 0)
                    {
                        Print("Cleanup complete - Strategy still running, ready for new entries.");
                    }
                }

                // V14 ADAPTIVE VISIBILITY: Broadcast current position size to panel
                // This allows the panel to hide/show T1-T5 buttons based on trade size
                // V14 FIX: Use State check instead of Connection.Status to avoid CS0120
                if (State == State.Realtime)
                {
                    // Build 1102Y-V2 [U-04]: Broadcast InitialTargetCount from live master position when in trade;
                    // fall back to dashboard activeTargetCount when flat.
                    int syncCount = activeTargetCount;
                    if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                    {
                        foreach (var kvp in activePositions.ToArray())
                        {
                            PositionInfo p = kvp.Value;
                            if (!p.IsFollower && p.EntryFilled && p.RemainingContracts > 0 && p.InitialTargetCount > 0)
                            {
                                syncCount = p.InitialTargetCount;
                                break;
                            }
                        }
                    }
                    SendResponseToRemote($"SYNC_TARGET_STATE|{syncCount}");
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnPositionUpdate: " + ex.Message);
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            try
            {
                if (execution == null || execution.Order == null) return;

                string orderName = execution.Order.Name;
                if (string.IsNullOrEmpty(orderName)) return;

                // V12.Phase7 [C-01]: Dedup guard — prevent double-decrement if OnOrderUpdate + OnExecutionUpdate both fire for same fill.
                // CRITICAL FIX: Use stateLock (same lock as ApplyTargetFill/OnOrderUpdate) to ensure mutual exclusion.
                // Previously used separate executionDeduplicateLock which allowed both threads to proceed concurrently.
                if (!string.IsNullOrEmpty(executionId))
                {
                    lock (stateLock)
                    {
                        if (!processedExecutionIds.Add(executionId))
                        {
                            Print(string.Format("[DEDUP] Skipping duplicate execution {0} for {1}", executionId, orderName));
                            return;
                        }
                        // Bounded pruning: keep at most MaxProcessedExecutionIds entries
                        processedExecutionIdQueue.Enqueue(executionId);
                        while (processedExecutionIdQueue.Count > MaxProcessedExecutionIds)
                            processedExecutionIds.Remove(processedExecutionIdQueue.Dequeue());
                    }
                }
                else
                {
                    // V12.1101E [F-08]: Fallback dedup key when executionId is missing: (Order, FilledQuantity).
                    // Uses runtime order object identity + cumulative filled quantity.
                    int dedupOrderIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(execution.Order);
                    int dedupFilledQuantity = execution.Order.Filled > 0 ? execution.Order.Filled : Math.Max(0, quantity);
                    string fallbackKey = string.Format("{0}|{1}", dedupOrderIdentity, dedupFilledQuantity);

                    lock (stateLock)
                    {
                        if (!processedExecutionFallbackKeys.Add(fallbackKey))
                        {
                            Print(string.Format("[DEDUP] Skipping duplicate fallback execution {0} for {1}", fallbackKey, orderName));
                            return;
                        }
                        processedExecutionFallbackQueue.Enqueue(fallbackKey);
                        while (processedExecutionFallbackQueue.Count > MaxProcessedExecutionIds)
                            processedExecutionFallbackKeys.Remove(processedExecutionFallbackQueue.Dequeue());
                    }
                }

                // V12.12: Compliance tracking for single-account mode
                if (EnableComplianceHub && !EnableSIMA)
                {
                    TrackTradeEntry(Account, execution);
                    UpdateAccountMetricsFromAccount(Account);
                    LogApexPerformance();
                }

                // Helper: Extract entry name from order name (removes prefix and optional timestamp suffix)
                Func<string, string, string> extractEntryName = (name, prefix) =>
                {
                    if (!name.StartsWith(prefix)) return "";
                    string entryPart = name.Substring(prefix.Length);
                    // Strip timestamp suffix if present (format: _123456789012345)
                    int lastUnderscore = entryPart.LastIndexOf('_');
                    if (lastUnderscore > 0 && entryPart.Length - lastUnderscore > 10)
                        entryPart = entryPart.Substring(0, lastUnderscore);
                    return entryPart;
                };

                // ============================================================
                // 1. STOP LOSS FILL - Manual OCO: Cancel all remaining targets
                // ============================================================
                if (orderName.StartsWith("Stop_"))
                {
                    string entryName = extractEntryName(orderName, "Stop_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        int remainingAfterStop;
                        lock (stateLock)
                        {
                            pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - Math.Max(0, quantity));
                            remainingAfterStop = pos.RemainingContracts;
                        }

                        Print(string.Format("STOP FILLED: {0} @ {1:F2}. Cancelling targets.", quantity, price));

                        // Manual OCO: Cancel all remaining profit targets immediately
                        // V12.1101E [F-07]: Keep target dictionary refs until terminal broker confirmation.
                        int cancelledTargets = 0;
                        for (int tNum = 1; tNum <= 5; tNum++)
                        {
                            var tDict = GetTargetOrdersDictionary(tNum);
                            if (tDict != null && tDict.TryGetValue(entryName, out var tOrder))
                            {
                                if (tOrder != null && (tOrder.OrderState == OrderState.Working || tOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(tOrder);
                                    cancelledTargets++;
                                }
                            }
                        }

                        if (cancelledTargets > 0)
                        {
                            Print(string.Format("OCO: Cancelled {0} target orders for {1}", cancelledTargets, entryName));
                        }

                        // Remove stop order reference
                        stopOrders.TryRemove(entryName, out _);

                        // Clean up pending replacements if any
                        if (pendingStopReplacements.TryRemove(entryName, out _))
                        {
                            Interlocked.Decrement(ref pendingReplacementCount);
                        }

                        // If position is fully closed, remove from activePositions
                        if (remainingAfterStop <= 0)
                        {
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                            entryOrders.TryRemove(entryName, out _);
                            Print(string.Format("Position {0} fully closed by stop.", entryName));
                        }
                    }
                }

                // ============================================================
                // 2. TARGET 1-5 FILL - Reduce stop quantity (unified loop)
                // V12.1101E [SK-01/A-1]: First-Writer-Wins guard prevents double-decrement.
                // ============================================================
                else if (orderName.StartsWith("T1_") || orderName.StartsWith("T2_") || orderName.StartsWith("T3_") ||
                         orderName.StartsWith("T4_") || orderName.StartsWith("T5_"))
                {
                    // Extract target number from prefix (T1_, T2_, etc.)
                    int targetNum = orderName[1] - '0';
                    string targetPrefix = "T" + targetNum + "_";
                    string entryName = extractEntryName(orderName, targetPrefix);

                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        bool terminalFill = execution.Order.OrderState == OrderState.Filled;
                        bool alreadyProcessed;
                        int appliedQty;
                        int remainingAfter;
                        ApplyTargetFill(pos, targetNum, quantity, terminalFill, out alreadyProcessed, out appliedQty, out remainingAfter);
                        if (alreadyProcessed)
                        {
                            Print(string.Format("[1101E GUARD] T{0} already processed for {1} — skipping duplicate OnExecutionUpdate fill", targetNum, entryName));
                            if (terminalFill)
                            {
                                var tDict = GetTargetOrdersDictionary(targetNum);
                                if (tDict != null) tDict.TryRemove(entryName, out _);
                            }
                            return;
                        }

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            appliedQty, price, remainingAfter));

                        if (remainingAfter > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            RequestStopCancelLifecycleSafe(entryName);
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }

                        // V12.1101E [F-07]: Clear target ref only after broker confirms Filled.
                        if (terminalFill)
                        {
                            var tDict = GetTargetOrdersDictionary(targetNum);
                            if (tDict != null) tDict.TryRemove(entryName, out _);
                        }
                    }
                }

                // ============================================================
                // 5. TRIM EXECUTION - V10.3.1: Enhanced Stop Integrity
                // ============================================================
                // ðŸ"¥ CRITICAL: When a TRIM executes, we MUST reduce the stop order quantity
                // to match the new position size. If we don't, hitting the stop after a trim
                // would close more contracts than we hold, creating an unintended REVERSE position.
                // Example: Long 4 contracts, stop at 4. Trim 2 (now Long 2). If stop stays at 4,
                // getting stopped out would SELL 4 (close 2 + go SHORT 2) = DISASTER.
                else if (orderName.StartsWith("Trim_"))
                {
                    string entryName = extractEntryName(orderName, "Trim_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        int previousQty;
                        int remainingAfterTrim;
                        lock (stateLock)
                        {
                            previousQty = pos.RemainingContracts;
                            pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - Math.Max(0, quantity));
                            remainingAfterTrim = pos.RemainingContracts;
                        }

                        Print(string.Format("TRIM EXECUTION: {0} contracts closed for {1}. Position: {2} â†' {3}",
                            quantity, entryName, previousQty, remainingAfterTrim));

                        // V10.3.1 FIX: MANDATORY stop quantity reduction to prevent reverse position
                        if (remainingAfterTrim > 0)
                        {
                            Print(string.Format("STOP INTEGRITY: Reducing stop quantity from {0} to {1} for {2}",
                                previousQty, remainingAfterTrim, entryName));
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed by trim, cancel stop
                            Print(string.Format("TRIM FLATTEN: Position {0} fully closed. Cancelling stop.", entryName));
                            RequestStopCancelLifecycleSafe(entryName);

                            // Also clean up any pending replacements
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }

                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print("Error OnExecutionUpdate: " + ex.Message);
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC [Build 1102U]: Propagates Master price changes (entry/stop/target) to all
        /// linked follower accounts. Triggered from Master's OnOrderUpdate.
        ///
        /// Root-cause fixes vs prior implementation:
        ///   1. Object-identity lookup replaces fragile signal-name substring matching.
        ///   2. Stop moves delegate to UpdateStopOrder (cancel/resubmit via follower Account API).
        ///   3. Target moves use pos.ExecutingAccount.Cancel + CreateOrder + Submit (not ChangeOrder).
        ///   4. Entry (Limit, pre-fill) moves implemented via cancel/resubmit.
        /// </summary>
        private void PropagateMasterPriceMove(Order masterOrder, double newLimit, double newStop, int newMasterQty = 0)
        {
            if (!EnableSIMA || masterOrder == null || masterOrder.Account != this.Account) return;

            // [BUILD 924 – Fix C] Raise propagation flag before dispatch; finally block clears it.
            _propagationActive = true;
            try
            {

            // --- Step 1: Identify master position and move type via object identity ---
            string masterEntryName = null;
            bool isEntryMove  = false;
            bool isStopMove   = false;
            bool isTargetMove = false;
            int  masterTargetNum = 0;

            foreach (var kvp in entryOrders)
            {
                if (kvp.Value == masterOrder &&
                    activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                {
                    masterEntryName = kvp.Key;
                    isEntryMove = true;
                    break;
                }
            }

            if (masterEntryName == null)
            {
                foreach (var kvp in stopOrders)
                {
                    if (kvp.Value == masterOrder &&
                        activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                    {
                        masterEntryName = kvp.Key;
                        isStopMove = true;
                        break;
                    }
                }
            }

            if (masterEntryName == null)
            {
                for (int t = 1; t <= 5 && masterEntryName == null; t++)
                {
                    var tDict = GetTargetOrdersDictionary(t);
                    if (tDict == null) continue;
                    foreach (var kvp in tDict)
                    {
                        if (kvp.Value == masterOrder &&
                            activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                        {
                            masterEntryName  = kvp.Key;
                            isTargetMove     = true;
                            masterTargetNum  = t;
                            break;
                        }
                    }
                }
            }

            if (masterEntryName == null) return; // Not a tracked master order

            // --- Step 2: Resolve follower entry names via Symmetry dispatch context ---

            // [BUILD 926 – Codex P1 Fix]: Derive master TradeType from boolean flags.
            // Master boolean flags ARE accurate (master positions set IsTRENDTrade, IsRMATrade etc. correctly).
            // Only FOLLOWER flags are contaminated (IsRMATrade=true on ALL followers for trailing behavior).
            // Follower type discrimination uses SignalName parsing instead — see fallback scan below.
            string masterTradeType = null;
            if (activePositions.TryGetValue(masterEntryName, out var masterPosForType))
            {
                if (masterPosForType.IsTRENDTrade)       masterTradeType = "TREND";
                else if (masterPosForType.IsRMATrade)    masterTradeType = "RMA";
                else if (masterPosForType.IsMOMOTrade)   masterTradeType = "MOMO";
                else if (masterPosForType.IsFFMATrade)   masterTradeType = "FFMA";
                else if (masterPosForType.IsRetestTrade) masterTradeType = "RETEST";
                else                                     masterTradeType = "OR";
            }

            IEnumerable<string> followerEntryNames;
            if (symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out string dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                string[] snapshot;
                lock (ctx.Sync) { snapshot = ctx.FollowerEntries.ToArray(); }
                followerEntryNames = snapshot;
            }
            else
            {
                // [BUILD 926 – Codex P1 Fix]: Fallback type match now uses SignalName parsing.
                //
                // ROOT CAUSE: IsRMATrade=true is stamped on ALL fleet followers (ExecuteSmartDispatchEntry
                // line 434) to enforce point-based trailing. Using IsRMATrade as a type discriminator
                // caused OR followers to fail the !IsRMATrade predicate and be excluded from OR
                // propagation, and incorrectly included in RMA propagation.
                //
                // FIX: Fleet entry names are stamped with the trade type at dispatch time:
                //   Format: "Fleet_<AccountName>_<TRADETYPE>_<Index>"
                //   Example: "Fleet_PA-APEX-422136-05_OR_0", "Fleet_APEX-09_RMA_1"
                //
                // [BUILD 927 – Codex P2 Fix]: Do NOT use Contains("_TYPE_") — if an account name
                // itself contains a trade-type substring (e.g. _RMA_, _OR_), Contains() misclassifies
                // the follower by matching the account name token instead of the TRADETYPE segment.
                //
                // SAFE APPROACH: Extract TRADETYPE by segment position.
                // TRADETYPE is always the second-to-last underscore-delimited segment:
                //   lastUnderscore      = before the numeric Index
                //   secondLastUnderscore = before the TRADETYPE token
                // Example: "Fleet_SimApexSim_02_OR_0"
                //   lastUs  → before "0"    → remaining = "Fleet_SimApexSim_02_OR"
                //   typeUs  → before "OR"   → extracted = "OR"  ✓
                var fallback = new List<string>();
                foreach (var kvp in activePositions)
                {
                    if (!kvp.Value.IsFollower || kvp.Value.ExecutingAccount == null) continue;
                    if (masterTradeType == null)
                    {
                        fallback.Add(kvp.Key);
                        continue;
                    }

                    // --- Segment-position extraction ---
                    string sig = kvp.Value.SignalName ?? kvp.Key;
                    string followerType = null;
                    int lastUs = sig.LastIndexOf('_');
                    if (lastUs > 0)
                    {
                        int typeUs = sig.LastIndexOf('_', lastUs - 1);
                        if (typeUs >= 0)
                        {
                            string extracted = sig.Substring(typeUs + 1, lastUs - typeUs - 1);
                            // Validate against known set — rejects garbage from unusual account names
                            if (extracted == "OR"     || extracted == "RMA"  ||
                                extracted == "TREND"  || extracted == "RETEST" ||
                                extracted == "MOMO"   || extracted == "FFMA")
                                followerType = extracted;
                        }
                    }

                    // Fallback: segment parsing failed — use boolean flags (RMA/OR ambiguity defaults to RMA)
                    if (followerType == null)
                    {
                        if      (kvp.Value.IsTRENDTrade)  followerType = "TREND";
                        else if (kvp.Value.IsRetestTrade)  followerType = "RETEST";
                        else if (kvp.Value.IsMOMOTrade)    followerType = "MOMO";
                        else if (kvp.Value.IsFFMATrade)    followerType = "FFMA";
                        else                               followerType = "RMA";
                    }

                    if (followerType == masterTradeType)
                        fallback.Add(kvp.Key);
                }
                followerEntryNames = fallback;
            }

            // --- Step 3: Apply move to each linked follower ---
            foreach (string fleetEntryName in followerEntryNames)
            {
                if (!activePositions.TryGetValue(fleetEntryName, out var pos)) continue;
                if (!pos.IsFollower || pos.ExecutingAccount == null) continue;

                if (isEntryMove)
                {
                    // [FIX-PM-02]: For StopMarket/StopLimit entries limitPrice=0 always; price lives in stopPrice.
                    // Passing newLimit=0 to PropagateMasterEntryMove caused the tick guard to silently no-op
                    // on every user-drag, and historically resubmitted Limit followers at price 0.
                    double effectiveEntryPrice = newLimit > 0 ? newLimit : newStop;
                    if (effectiveEntryPrice <= 0) continue; // both zero — NT8 callback race, skip safely
                    PropagateMasterEntryMove(fleetEntryName, pos, effectiveEntryPrice, newMasterQty);
                }
                else if (isStopMove)
                    PropagateMasterStopMove(fleetEntryName, pos, newStop);
                else if (isTargetMove)
                    PropagateMasterTargetMove(fleetEntryName, pos, masterTargetNum, newLimit);
            }
            } // end try
            finally
            {
                // [BUILD 924 – Fix C] Always clear propagation flag, even on exception.
                _propagationActive = false;
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC: Propagate master stop price move to follower.
        /// Delegates to UpdateStopOrder which uses cancel/resubmit via follower Account API
        /// (per V12.10 pattern — ChangeOrder is master-local only).
        /// </summary>
        private void PropagateMasterStopMove(string fleetEntryName, PositionInfo pos, double newStop)
        {
            if (newStop <= 0) return;
            // [FIX-PM-03]: Skip stop propagation for followers whose entry hasn't filled yet.
            // When the master bracket stop first becomes Working (after master fill), this fires for
            // all dispatched followers. Unfilled followers have no live stop order to move, and the
            // log noise ("Stop move: A -> B" at dispatch time) was incorrectly suggesting a problem.
            if (!pos.EntryFilled) return;
            double roundedStop = Instrument.MasterInstrument.RoundToTickSize(newStop);
            if (Math.Abs(pos.CurrentStopPrice - roundedStop) <= tickSize / 2) return;

            Print(string.Format("[MOVE-SYNC] Stop move: {0} on {1}: {2:F2} -> {3:F2}",
                fleetEntryName, pos.ExecutingAccount.Name, pos.CurrentStopPrice, roundedStop));

            UpdateStopOrder(fleetEntryName, pos, roundedStop, pos.CurrentTrailLevel);
        }

        /// <summary>
        /// V12.MOVE-SYNC: Propagate master target price move to follower via cancel+resubmit.
        /// Mirrors SymmetryGuardReplaceExistingFollowerTarget (Symmetry.cs:504) pattern.
        /// </summary>
        private void PropagateMasterTargetMove(string fleetEntryName, PositionInfo pos, int targetNum, double newLimit)
        {
            if (newLimit <= 0) return;
            var targetDict = GetTargetOrdersDictionary(targetNum);
            if (targetDict == null) return;
            if (!targetDict.TryGetValue(fleetEntryName, out var tOrder) || tOrder == null) return;
            if (tOrder.OrderState != OrderState.Working && tOrder.OrderState != OrderState.Accepted) return;

            double roundedLimit = Instrument.MasterInstrument.RoundToTickSize(newLimit);
            if (Math.Abs(tOrder.LimitPrice - roundedLimit) <= tickSize / 2) return;

            Print(string.Format("[MOVE-SYNC] T{0} move: {1} on {2}: {3:F2} -> {4:F2}",
                targetNum, fleetEntryName, pos.ExecutingAccount.Name, tOrder.LimitPrice, roundedLimit));

            try
            {
                pos.ExecutingAccount.Cancel(new[] { tOrder });

                int qty = tOrder.Quantity;
                OrderAction exitAction = pos.Direction == MarketPosition.Long
                    ? OrderAction.Sell : OrderAction.BuyToCover;
                string signalName = SymmetryTrim("T" + targetNum + "_" + fleetEntryName, 40);

                Order replacement = pos.ExecutingAccount.CreateOrder(
                    Instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    qty, roundedLimit, 0,
                    // [923A-P1b-GUID]: 8-char GUID fragment replaces Ticks — eliminates collision risk at high resubmit frequency
                    "MGT_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    signalName, null);

                pos.ExecutingAccount.Submit(new[] { replacement });
                targetDict[fleetEntryName] = replacement;

                Print(string.Format("[MOVE-SYNC] T{0} resubmitted: {1} @ {2:F2}",
                    targetNum, fleetEntryName, roundedLimit));
            }
            catch (Exception ex)
            {
                Print(string.Format("[MOVE-SYNC] ERROR PropagateMasterTargetMove T{0} {1}: {2}",
                    targetNum, fleetEntryName, ex.Message));
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC / 1102Z-D: Propagate master entry price move to follower (pre-fill orders).
        /// Account.Change() removed — it completes silently on Apex/Tradovate but is a broker-side no-op.
        /// Cancel + CreateOrder + Submit is the sole path, consistent with PropagateMasterTargetMove
        /// and UpdateStopOrder throughout this codebase.
        /// StampReaperMoveGrace() is called before Cancel to open a 5-second REAPER suppression window
        /// covering the cancel gap. REAPER's ChangePending guard (AuditApexPositions line 193) provides
        /// a second layer of protection.
        /// </summary>
        private void PropagateMasterEntryMove(string fleetEntryName, PositionInfo pos, double newLimit, int newMasterQty = 0)
        {
            if (!entryOrders.TryGetValue(fleetEntryName, out var fEntry) || fEntry == null) return;
            if (fEntry.OrderState != OrderState.Working && fEntry.OrderState != OrderState.Accepted) return;

            double roundedLimit = Instrument.MasterInstrument.RoundToTickSize(newLimit);
            // [FIX-PM-02b]: For StopMarket/StopLimit orders price lives in StopPrice (LimitPrice is always 0).
            bool isStopTypeEntry = fEntry.OrderType == OrderType.StopMarket || fEntry.OrderType == OrderType.StopLimit;
            double fEffectivePrice = isStopTypeEntry ? fEntry.StopPrice : fEntry.LimitPrice;

            // [QTY-SYNC]: Scale master quantity for this follower.
            // Fallback to fEntry.Quantity if no quantity signal (pure price-change callback, or qty=0 noise).
            // [923A-P2a-OVF]: checked{} forces explicit OverflowException vs silent int truncation on extreme parity ratios
            // (e.g., 1 NQ → 10 MES with very high master qty). Clamps to maxContracts on overflow.
            int scaledQty;
            try
            {
                scaledQty = (newMasterQty > 0 && FleetParityMultiplier > 0)
                    ? checked((int)Math.Max(1L, (long)newMasterQty * FleetParityMultiplier))  // [922Z-OVF+923A]: long cast + checked int
                    : fEntry.Quantity;
            }
            catch (OverflowException)
            {
                Print(string.Format("[923A-OVF] Parity scalar overflow for {0} — clamping to maxContracts ({1})", fleetEntryName, maxContracts));
                scaledQty = maxContracts;
            }

            bool priceChanged    = Math.Abs(fEffectivePrice - roundedLimit) > tickSize / 2;
            bool quantityChanged = scaledQty != fEntry.Quantity;
            if (!priceChanged && !quantityChanged) return;

            Print(string.Format("[MOVE-SYNC] Entry move: {0} on {1}: {2:F2} -> {3:F2} x{4}",
                fleetEntryName, pos.ExecutingAccount.Name, fEffectivePrice, roundedLimit, scaledQty));

            // 1102Z-D: Stamp grace BEFORE Cancel — opens 5-second REAPER suppression window covering the cancel gap.
            StampReaperMoveGrace();

            // 1102Z-D [Protected Resubmit]: Cancel + CreateOrder + Submit is the sole path.
            // Account.Change() was removed — it is a silent no-op on Apex/Tradovate.
            try
            {
                pos.ExecutingAccount.Cancel(new[] { fEntry });

                OrderAction entryAction = pos.Direction == MarketPosition.Long
                    ? OrderAction.Buy : OrderAction.SellShort;
                // [GHOST-FIX-1 Build 922Z]: Preserve original fleetEntryName as signal name.
                // The identity chain MUST be: activePositions key == entryOrders key == order signal name.
                // The old code appended a random "_MGE_" + timestamp suffix, which broke the chain:
                //   → OnAccountExecutionUpdate could not find the key in entryOrders on fill
                //   → SubmitBracketOrders was never called → position was naked (no stop, no target)
                //   → REAPER saw naked position and fired emergency flatten, causing the ghost entry cascade.
                // Using fleetEntryName directly restores the chain and ensures brackets are submitted on fill.
                string signalName = fleetEntryName;

                // [FIX-PM-02c]: Preserve original order type so StopMarket followers remain StopMarket.
                double limitPx = (!isStopTypeEntry) ? roundedLimit : 0;
                double stopPx  = ( isStopTypeEntry) ? roundedLimit : 0;
                Order newEntry = pos.ExecutingAccount.CreateOrder(
                    Instrument, entryAction, fEntry.OrderType, TimeInForce.Gtc,
                    scaledQty, limitPx, stopPx,
                    // [923A-P1-GUID]: 8-char hex GUID fragment replaces Ticks — eliminates collision risk
                    // at extreme resubmit frequency. ocoId only; signalName = fleetEntryName unchanged (GHOST-FIX-1).
                    "MGE_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    signalName, null);

                pos.ExecutingAccount.Submit(new[] { newEntry });
                entryOrders[fleetEntryName] = newEntry;

                // [QTY-SYNC]: Sync PositionInfo to new size so SubmitBracketOrders sum-assertion passes.
                lock (stateLock)
                {
                    pos.TotalContracts     = scaledQty;
                    pos.RemainingContracts = scaledQty;
                    int ft1, ft2, ft3, ft4, ft5;
                    GetTargetDistribution(scaledQty, out ft1, out ft2, out ft3, out ft4, out ft5);
                    pos.T1Contracts = ft1;
                    pos.T2Contracts = ft2;
                    pos.T3Contracts = ft3;
                    pos.T4Contracts = ft4;
                    pos.T5Contracts = ft5;
                }

                Print(string.Format("[MOVE-SYNC] Entry resubmitted (protected): {0} @ {1:F2} x{2}",
                    fleetEntryName, roundedLimit, scaledQty));
            }
            catch (Exception ex)
            {
                Print(string.Format("[MOVE-SYNC] ERROR PropagateMasterEntryMove {0}: {1}",
                    fleetEntryName, ex.Message));
            }
        }

        #endregion
    }
}
