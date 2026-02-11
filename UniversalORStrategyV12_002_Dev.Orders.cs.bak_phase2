// V12.16 MODULAR: Order Management Module (Extracted from main file)
// Contains: OnOrderUpdate, OnPositionUpdate, Trailing Stops, Position Sync, Stop Management
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
        #region Order Management

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice, OrderState orderState,
            DateTime time, ErrorCode error, string nativeError)
        {
            try
            {
                string orderName = order.Name;

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

                            // Store intended entry price for slippage calculation
                            double intendedEntryPrice = pos.EntryPrice;

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
                                Print(string.Format("ðŸ” DIAGNOSTIC: RMA Entry Filled. Raw ATR used: {0:F4} | Multiplier: {1:F2} | Calc Stop: {2:F4} pts", 
                                    currentATR, RMAStopATRMultiplier, currentATR * RMAStopATRMultiplier));
                                stopDistance = currentATR * RMAStopATRMultiplier;
                                
                                // Recalculate RMA targets based on fill
                                // v5.13 FIX: T1 uses FIXED points, T2/T3 use ATR
                                double t2Distance = currentATR * RMAT1ATRMultiplier;  // 0.5x ATR
                                double t3Distance = currentATR * RMAT2ATRMultiplier;  // 1.0x ATR

                                // T1 = Fixed 1pt (NOT ATR-based)
                                pos.Target1Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + Target1FixedPoints
                                    : averageFillPrice - Target1FixedPoints;
                                // T2 = 0.5x ATR
                                pos.Target2Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t2Distance
                                    : averageFillPrice - t2Distance;
                                // T3 = 1.0x ATR
                                pos.Target3Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t3Distance
                                    : averageFillPrice - t3Distance;
                            }
                            else
                            {
                                // For other trades, use the distance from the intended setup
                                stopDistance = Math.Abs(pos.InitialStopPrice - intendedEntryPrice);
                            }

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

                // Target 1 filled
                if (target1Orders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        if (target1Orders.TryGetValue(kvp.Key, out var t1Order) && t1Order == order)
                        {
                            PositionInfo pos = kvp.Value;
                            pos.T1Filled = true;
                            pos.RemainingContracts -= pos.T1Contracts;
                            // V8.11: Added entry name to logging
                            Print(string.Format("T1 FILLED ({0}): {1} contracts @ {2:F2} | Remaining: {3}",
                                kvp.Key, pos.T1Contracts, averageFillPrice, pos.RemainingContracts));

                            // Update stop quantity
                            UpdateStopQuantity(kvp.Key, pos);
                            break;
                        }
                    }
                }

                // Target 2 filled
                if (target2Orders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        if (target2Orders.TryGetValue(kvp.Key, out var t2Order) && t2Order == order)
                        {
                            PositionInfo pos = kvp.Value;
                            pos.T2Filled = true;
                            pos.RemainingContracts -= pos.T2Contracts;
                            // V8.11: Added entry name to logging
                            Print(string.Format("T2 FILLED ({0}): {1} contracts @ {2:F2} | Remaining: {3}",
                                kvp.Key, pos.T2Contracts, averageFillPrice, pos.RemainingContracts));

                            // Update stop quantity
                            UpdateStopQuantity(kvp.Key, pos);
                            break;
                        }
                    }
                }

                // v5.13: Target 3 filled
                if (target3Orders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        if (target3Orders.TryGetValue(kvp.Key, out var t3Order) && t3Order == order)
                        {
                            PositionInfo pos = kvp.Value;
                            pos.T3Filled = true;
                            pos.RemainingContracts -= pos.T3Contracts;
                            // V8.11: Added entry name to logging
                            Print(string.Format("T3 FILLED ({0}): {1} contracts @ {2:F2} | Remaining: {3} (T4 runner)",
                                kvp.Key, pos.T3Contracts, averageFillPrice, pos.RemainingContracts));

                            // Update stop quantity - only T4 runner remains
                            UpdateStopQuantity(kvp.Key, pos);
                            break;
                        }
                    }
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
                        Print(string.Format("âš ï¸ CRITICAL: Stop order REJECTED: {0}", orderName));

                        // V8.30: Thread-safe snapshot iteration
                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!activePositions.ContainsKey(kvp.Key)) continue;
                            if (stopOrders.TryGetValue(kvp.Key, out var stopOrder) && stopOrder == order)
                            {
                                PositionInfo pos = kvp.Value;
                                Print(string.Format("âš ï¸ Position {0} is UNPROTECTED: {1} {2} contracts @ {3:F2}",
                                    kvp.Key,
                                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                    pos.RemainingContracts,
                                    pos.EntryPrice));

                                // V12.12: Remove stale rejected stop, then re-submit directly
                                // Cannot use UpdateStopQuantity â€” it early-exits if stopOrders is empty (line 3044)
                                // and the cancel-replace flow doesn't apply to a rejected (non-working) order.
                                Print(string.Format("Attempting to re-submit stop for {0}...", kvp.Key));
                                stopOrders.TryRemove(kvp.Key, out _);
                                CreateNewStopOrder(kvp.Key, pos.RemainingContracts, pos.CurrentStopPrice, pos.Direction);
                                break;
                            }
                        }
                    }

                    // V12.12: Target order rejected - remove stale reference from dictionary
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
                            
                            // Check if price changed (with tick tolerance)
                            if (Math.Abs(newPrice - pos.EntryPrice) > tickSize * 0.5)
                            {
                                double oldPrice = pos.EntryPrice;
                                pos.EntryPrice = newPrice;
                                
                                Print(string.Format("V12: Entry order MOVED: {0} | {1:F2} â†’ {2:F2}", entryName, oldPrice, newPrice));
                                
                                // V12 SIMA: Legacy slave broadcast removed
                            }
                            break;
                        }
                    }
                }

                // V12.13: Coordination flag â€” prevents redundant ghost-scan after explicit handling
                bool handledByExplicitCleanup = false;

                // V8.11: Stop order cancelled - check for pending replacement
                // V12.13: Extended to also match "S_" prefix (replacement stops from CreateNewStopOrder)
                if ((orderName.StartsWith("Stop_") || orderName.StartsWith("S_")) && orderState == OrderState.Cancelled)
                {
                    // V8.30: Thread-safe snapshot iteration with TryRemove
                    foreach (var kvp in pendingStopReplacements.ToArray())
                    {
                        string entryName = kvp.Key;
                        PendingStopReplacement pending = kvp.Value;

                        // V8.24 FIX: REMOVED recursive 'Contains' check. STRICT object match only.
                        if (activePositions.ContainsKey(entryName) && pending.OldOrder == order)
                        {
                            Print(string.Format("STOP CANCELLED (confirmed): {0} | Creating replacement...", entryName));

                            // Create the replacement stop
                            CreateNewStopOrder(entryName, pending.Quantity, pending.StopPrice, pending.Direction);

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

                // V12.13c: Manual stop cancel â€” clean up dictionary ref, no auto re-submission
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
                                Print(string.Format("V12.13c: Stop cancelled: {0} â€” removed from tracking", entryName));
                                handledByExplicitCleanup = true;
                            }
                            break;
                        }
                    }
                }

                // V12.13: Manual target cancel â€” user cancelled target from chart
                if (!handledByExplicitCleanup && orderState == OrderState.Cancelled)
                {
                    var targetDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
                    {
                        (target1Orders, "T1"), (target2Orders, "T2"),
                        (target3Orders, "T3"), (target4Orders, "T4"),
                    };
                    foreach (var (dict, label) in targetDicts)
                    {
                        foreach (var kvp in dict.ToArray())
                        {
                            if (kvp.Value == order)
                            {
                                if (dict.TryRemove(kvp.Key, out _))
                                {
                                    Print(string.Format("V12.13: {0} MANUALLY CANCELLED: {1} â€” contracts run with stop", label, kvp.Key));
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
                            Print(string.Format("V12.13: Entry CANCELLED â€” full teardown: {0}", entryName));

                            // Clean up local state
                            CleanupPosition(entryName);
                            handledByExplicitCleanup = true;
                            break;
                        }
                    }
                }

                // V12.13: Terminal catch-all â€” ONLY fires if no explicit handler above already cleaned this order
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
            try
            {
                if (e == null || e.Order == null) return;

                Order order = e.Order;
                if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

                if (order.OrderState != OrderState.Cancelled && order.OrderState != OrderState.Rejected &&
                    order.OrderState != OrderState.Unknown)
                {
                    return;
                }

                Account acct = sender as Account;
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
                        (target4Orders.TryGetValue(kvp.Key, out var t4Order) && (t4Order == order || (t4Order != null && t4Order.OrderId == order.OrderId))))
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
                            Print(string.Format("[SIMA] Follower entry terminal: {0} on {1} ({2}) - tearing down", matchedEntry, acctName, reason));
                            CleanupPosition(matchedEntry);
                            return;
                        }

                        Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, orderId));
                        RemoveGhostOrderRef(order, reason);
                        return;
                    }
                }
                else
                {
                    Print(string.Format("[GHOST-AUDIT] NO MATCH in activePositions for OrderId={0} Name={1} on {2}", orderId, order.Name, acctName));
                }

                // Fallback: clear any stale reference for terminal follower order states
                RemoveGhostOrderRef(order, reason);
            }
            catch (Exception ex)
            {
                Print("ERROR OnAccountOrderUpdate: " + ex.Message);
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            try
            {
                // Check for EXTERNAL close (position went flat from outside strategy)
                if (marketPosition == MarketPosition.Flat)
                {
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

                            // Cancel orphaned target orders
                            if (target1Orders.TryGetValue(kvp.Key, out var t1Order))
                            {
                                if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t1Order);
                                }
                            }

                            if (target2Orders.TryGetValue(kvp.Key, out var t2Order))
                            {
                                if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t2Order);
                                }
                            }

                            // v5.13: Cancel T3/T4 orphaned orders
                            if (target3Orders.TryGetValue(kvp.Key, out var t3Order))
                            {
                                if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t3Order);
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
            }
            catch (Exception ex)
            {
                Print("ERROR OnPositionUpdate: " + ex.Message);
            }
        }

        private void SubmitBracketOrders(string entryName, PositionInfo pos)
        {
            if (pos.BracketSubmitted) return;

            try
            {
                // Validate stop price
                double validatedStopPrice = ValidateStopPrice(pos.Direction, pos.InitialStopPrice);

                // Submit initial stop for all contracts
                Order stopOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, "", "Stop_" + entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, "", "Stop_" + entryName);

                stopOrders[entryName] = stopOrder;

                // Submit T1 limit order ONLY if T1 quantity > 0 AND TotalContracts > 1
                // V8.15: For 1-contract trades, we treat it as a runner (no initial target)
                if (pos.T1Contracts > 0 && pos.TotalContracts > 1)
                {
                    Order t1Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName);

                    target1Orders[entryName] = t1Order;
                }
                else if (pos.TotalContracts == 1)
                {
                    Print(string.Format("V8.15: 1-contract trade detected for {0}. Treating as RUNNER (no initial target).", entryName));
                }

                // Submit T2 limit order ONLY if T2 quantity > 0
                if (pos.T2Contracts > 0)
                {
                    Order t2Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName);

                    target2Orders[entryName] = t2Order;
                }

                // v5.13: Submit T3 limit order ONLY if T3 quantity > 0
                if (pos.T3Contracts > 0)
                {
                    Order t3Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + entryName);

                    target3Orders[entryName] = t3Order;
                }

                // NOTE: T4 (runner) has no limit order - it trails with stop

                pos.BracketSubmitted = true;
                pos.CurrentStopPrice = validatedStopPrice;

                // Build bracket summary message with all 4 targets
                StringBuilder bracketMsg = new StringBuilder();
                string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                bracketMsg.AppendFormat("{0} BRACKET V8.0: Stop@{1:F2}", tradeType, validatedStopPrice);
                if (pos.T1Contracts > 0)
                    bracketMsg.AppendFormat(" | T1:{0}@{1:F2}(+{2}pt)", pos.T1Contracts, pos.Target1Price, Target1FixedPoints);
                if (pos.T2Contracts > 0)
                    bracketMsg.AppendFormat(" | T2:{0}@{1:F2}", pos.T2Contracts, pos.Target2Price);
                if (pos.T3Contracts > 0)
                    bracketMsg.AppendFormat(" | T3:{0}@{1:F2}", pos.T3Contracts, pos.Target3Price);
                if (pos.T4Contracts > 0)
                    bracketMsg.AppendFormat(" | T4:{0}@trail", pos.T4Contracts);

                Print(bracketMsg.ToString());
            }
            catch (Exception ex)
            {
                Print("ERROR SubmitBracketOrders: " + ex.Message);
            }
        }

        private void UpdateStopQuantity(string entryName, PositionInfo pos)
        {
            if (!stopOrders.ContainsKey(entryName)) return;
            if (pos.RemainingContracts <= 0) return;

            try
            {
                Order currentStop = stopOrders[entryName];

                // V8.11 FIX: Store pending replacement BEFORE cancelling
                // This ensures we only create a new stop when the old one is confirmed cancelled
                if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                {
                    // V8.31: Check if there's already a pending replacement to prevent duplicates
                    if (pendingStopReplacements.ContainsKey(entryName))
                    {
                        // Just update the quantity, don't create a new pending
                        if (pendingStopReplacements.TryGetValue(entryName, out var existingPending))
                        {
                            existingPending.Quantity = pos.RemainingContracts;
                            Print(string.Format("V8.31: Updated existing pending replacement for {0} to {1} contracts", entryName, pos.RemainingContracts));
                        }
                        return;
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
                    CancelOrder(currentStop);
                    Print(string.Format("STOP CANCEL PENDING: {0} | Will replace with {1} contracts @ {2:F2}",
                        entryName, pos.RemainingContracts, pos.CurrentStopPrice));
                }
                else
                {
                    // No existing stop to cancel, create new one directly
                    CreateNewStopOrder(entryName, pos.RemainingContracts, pos.CurrentStopPrice, pos.Direction);
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("âš ï¸ ERROR UpdateStopQuantity for {0}: {1}", entryName, ex.Message));
                Print(string.Format("âš ï¸ POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
            }
        }

        // V8.11: Helper method to create a new stop order
        // V8.31: Added guard to prevent duplicate stop creation
        private void CreateNewStopOrder(string entryName, int quantity, double stopPrice, MarketPosition direction)
        {
            try
            {
                // V8.31: Check if a working stop already exists for this entry to prevent duplicates
                if (stopOrders.TryGetValue(entryName, out var existingStop))
                {
                    if (existingStop != null && (existingStop.OrderState == OrderState.Working || existingStop.OrderState == OrderState.Accepted))
                    {
                        Print(string.Format("V8.31: SKIPPING duplicate stop creation for {0} - stop already working", entryName));
                        return;
                    }
                }

                Order newStop = null;
                OrderAction exitAction = direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                // V12.3: Route to correct account (fleet follower vs local)
                if (activePositions.TryGetValue(entryName, out var pos) && pos.IsFollower && pos.ExecutingAccount != null)
                {
                    // Fleet follower: use Account API
                    string sigName = "S_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = pos.ExecutingAccount.CreateOrder(Instrument, exitAction,
                        OrderType.StopMarket, TimeInForce.Gtc, quantity, 0, stopPrice, sigName, sigName, null);
                    pos.ExecutingAccount.Submit(new[] { newStop });
                }
                else
                {
                    // Local: use SubmitOrderUnmanaged with truncated signal name
                    string suffix = (DateTime.Now.Ticks % 100000000).ToString();
                    string sigName = "S_" + entryName + "_" + suffix;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = SubmitOrderUnmanaged(0, exitAction, OrderType.StopMarket, quantity, 0, stopPrice, "", sigName);
                }

                if (newStop == null)
                {
                    Print(string.Format("âš ï¸ CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(string.Format("âš ï¸ POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}",
                        direction == MarketPosition.Long ? "LONG" : "SHORT", quantity, stopPrice));

                    // Attempt to flatten position immediately
                    Print(string.Format("âš ï¸ Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                stopOrders[entryName] = newStop;
                Print(string.Format("STOP QTY UPDATED: {0} contracts @ {1:F2} (Order: {2})",
                    quantity, stopPrice, newStop.Name));
            }
            catch (Exception ex)
            {
                Print(string.Format("âš ï¸ ERROR CreateNewStopOrder for {0}: {1}", entryName, ex.Message));
            }
        }

        private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice)
        {
            double currentPrice = Close[0];
            double minDistance = 2 * tickSize;

            if (direction == MarketPosition.Long)
            {
                if (desiredStopPrice >= currentPrice)
                {
                    double validStop = currentPrice - minDistance;
                    Print(string.Format("STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (was at/above market)",
                        desiredStopPrice, validStop));
                    return validStop;
                }
            }
            else
            {
                if (desiredStopPrice <= currentPrice)
                {
                    double validStop = currentPrice + minDistance;
                    Print(string.Format("STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (was at/below market)",
                        desiredStopPrice, validStop));
                    return validStop;
                }
            }

            return desiredStopPrice;
        }

        #endregion

        #region Trailing Stops

        private void ManageTrailingStops()
        {
            DateTime now = DateTime.Now;

            // V8.30: Adaptive throttle calculation - adjusts based on tick frequency
            tickCountInLastSecond++;
            if ((now - lastTickCountReset).TotalSeconds >= 1)
            {
                // Adjust throttle based on tick frequency
                if (tickCountInLastSecond > 50)
                    adaptiveThrottleMs = Math.Min(500, adaptiveThrottleMs + 50); // Increase throttle under load
                else if (tickCountInLastSecond < 20)
                    adaptiveThrottleMs = Math.Max(100, adaptiveThrottleMs - 25); // Decrease throttle when calm

                tickCountInLastSecond = 0;
                lastTickCountReset = now;
            }

            // V8.30: Use adaptive throttle instead of fixed 100ms
            if ((now - lastStopManagementTime).TotalMilliseconds < adaptiveThrottleMs)
                return;

            lastStopManagementTime = now;

            // V8.30: Clean up stale pending replacements (5-second timeout)
            CleanupStalePendingReplacements();

            // V8.30: Circuit breaker check - pause trailing when too many pending replacements
            if (circuitBreakerActive)
            {
                if ((now - circuitBreakerActivatedTime).TotalSeconds > 2)
                {
                    circuitBreakerActive = false;
                    Print("V8.30: Circuit breaker RESET - trailing stops resumed");
                }
                else
                {
                    return; // Skip trailing stop updates while circuit breaker is active
                }
            }

            // V8.30: Thread-safe snapshot iteration - prevents "Collection was modified" exception
            var positionSnapshot = activePositions.ToArray();
            foreach (var kvp in positionSnapshot)
            {
                string entryName = kvp.Key;
                PositionInfo pos = kvp.Value;

                // V8.30: Verify position still exists (may have been removed by callback thread)
                if (!activePositions.ContainsKey(entryName)) continue;

                if (!pos.EntryFilled || !pos.BracketSubmitted) continue;

                // Increment tick counter on every call
                pos.TicksSinceEntry++;

                // Update extreme price
                if (pos.Direction == MarketPosition.Long)
                    pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, Close[0]);
                else
                    pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, Close[0]);

                // V8.2: TREND Entry 1 - starts with fixed 2pt stop, switches to EMA9 trail when price crosses EMA
                if (pos.IsTRENDTrade && pos.IsTRENDEntry1)
                {
                    // V8.2: Use stored ema9 instance
                    double tickPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                    double ema9Live = ema9 != null ? ema9[0] : Close[0];
                    double currentPrice = tickPrice;
                    
                    // Check if price has crossed EMA9 in our favor
                    bool priceInFavor = pos.Direction == MarketPosition.Long
                        ? currentPrice > ema9Live  // LONG: price above EMA9
                        : currentPrice < ema9Live; // SHORT: price below EMA9

                    // If not yet trailing and price crossed EMA in our favor, activate trailing
                    if (!pos.Entry1TrailActivated && priceInFavor)
                    {
                        pos.Entry1TrailActivated = true;
                        Print(string.Format("TREND E1: Switching to EMA9 trail (Price={0:F2} crossed EMA9={1:F2})",
                            currentPrice, ema9Live));
                    }

                    // If trailing is activated, manage the EMA9 trail
                    if (pos.Entry1TrailActivated)
                    {
                        double trendStop = pos.Direction == MarketPosition.Long
                            ? ema9Live - (currentATR * TRENDEntry1ATRMultiplier)  // V8.31: Uses E1 specific multiplier
                            : ema9Live + (currentATR * TRENDEntry1ATRMultiplier);

                        bool shouldUpdate = pos.Direction == MarketPosition.Long
                            ? trendStop > pos.CurrentStopPrice
                            : trendStop < pos.CurrentStopPrice;

                        if (shouldUpdate)
                        {
                            UpdateStopOrder(entryName, pos, trendStop, pos.CurrentTrailLevel);
                            // Print(string.Format("TREND E1 TRAIL: Stop moved to {0:F2} (EMA9={1:F2} - {2}xATR)",
                            //    trendStop, ema9Live, TRENDEntry2ATRMultiplier));
                        }
                    }
                    continue; // Skip normal trailing logic for TREND E1
                }

                // V8.2: TREND Entry 2 uses EMA15 trailing stop (1.1x ATR from live EMA15)
                if (pos.IsTRENDTrade && pos.IsTRENDEntry2 && !pos.IsRMATrade)
                {
                    // V8.2: Use stored ema15 instance
                    double ema15Live = ema15 != null ? ema15[0] : Close[0];
                    
                    double trendStop = pos.Direction == MarketPosition.Long
                        ? ema15Live - (currentATR * TRENDEntry2ATRMultiplier)
                        : ema15Live + (currentATR * TRENDEntry2ATRMultiplier);

                    bool shouldUpdate = pos.Direction == MarketPosition.Long
                        ? trendStop > pos.CurrentStopPrice
                        : trendStop < pos.CurrentStopPrice;

                    if (shouldUpdate)
                    {
                        UpdateStopOrder(entryName, pos, trendStop, pos.CurrentTrailLevel);
                        Print(string.Format("TREND E2 TRAIL: Stop moved to {0:F2} (EMA15={1:F2} - {2}xATR)", 
                            trendStop, ema15Live, TRENDEntry2ATRMultiplier));
                    }
                    continue; // Skip normal trailing logic for TREND E2
                }

                // V8.4: RETEST trade - Phase 1: Wait for price to cross 9 EMA, Phase 2: Trail at 9 EMA
                if (pos.IsRetestTrade && !pos.IsRMATrade)
                {
                    double tickPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                    double ema9Live = ema9 != null ? ema9[0] : Close[0];
                    double currentPrice = tickPrice;

                    // Phase 1: Wait for price to cross EMA9 in our favor
                    if (!pos.RetestTrailActivated)
                    {
                        bool priceInFavor = pos.Direction == MarketPosition.Long
                            ? currentPrice > ema9Live  // LONG: price above EMA9
                            : currentPrice < ema9Live; // SHORT: price below EMA9

                        if (priceInFavor)
                        {
                            pos.RetestTrailActivated = true;
                            Print(string.Format("RETEST: Switching to EMA9 trail (Price={0:F2} crossed EMA9={1:F2})",
                                currentPrice, ema9Live));
                        }
                        // Stay at fixed stop until price crosses EMA
                        continue;
                    }

                    // Phase 2: Trail at 9 EMA - 1.1x ATR (locked in, only moves favorably)
                    double retestStop = pos.Direction == MarketPosition.Long
                        ? ema9Live - (currentATR * RetestATRMultiplier)
                        : ema9Live + (currentATR * RetestATRMultiplier);

                    // Only update if better than current stop
                    bool shouldUpdate = pos.Direction == MarketPosition.Long
                        ? retestStop > pos.CurrentStopPrice
                        : retestStop < pos.CurrentStopPrice;

                    if (shouldUpdate)
                    {
                        UpdateStopOrder(entryName, pos, retestStop, pos.CurrentTrailLevel);
                        Print(string.Format("RETEST TRAIL: Stop moved to {0:F2} (EMA9={1:F2} - {2}xATR)",
                            retestStop, ema9Live, RetestATRMultiplier));
                    }
                    continue; // Skip normal trailing logic for RETEST
                }

                double profitPoints = pos.Direction == MarketPosition.Long
                    ? pos.ExtremePriceSinceEntry - pos.EntryPrice
                    : pos.EntryPrice - pos.ExtremePriceSinceEntry;

                double newStopPrice = pos.CurrentStopPrice;
                int newTrailLevel = pos.CurrentTrailLevel;

                // MANUAL BREAKEVEN - Check FIRST before automatic trailing
                // This allows user to "arm" breakeven early and it auto-triggers when price reaches threshold
                if (pos.ManualBreakevenArmed && !pos.ManualBreakevenTriggered)
                {
                    double beThreshold = pos.EntryPrice + (ManualBreakevenBuffer * tickSize);
                    bool thresholdReached = false;

                    if (pos.Direction == MarketPosition.Long)
                    {
                        thresholdReached = Close[0] >= beThreshold;
                    }
                    else // Short
                    {
                        beThreshold = pos.EntryPrice - (ManualBreakevenBuffer * tickSize);
                        thresholdReached = Close[0] <= beThreshold;
                    }

                    if (thresholdReached)
                    {
                        // Move stop to breakeven + buffer
                        double manualBEStop = pos.Direction == MarketPosition.Long
                            ? pos.EntryPrice + (ManualBreakevenBuffer * tickSize)
                            : pos.EntryPrice - (ManualBreakevenBuffer * tickSize);

                        // Only move if it's better than current stop
                        bool shouldMove = pos.Direction == MarketPosition.Long
                            ? manualBEStop > pos.CurrentStopPrice
                            : manualBEStop < pos.CurrentStopPrice;

                        if (shouldMove)
                        {
                            newStopPrice = manualBEStop;
                            newTrailLevel = 1; // Same as automatic breakeven
                            pos.ManualBreakevenTriggered = true;
                            Print(string.Format("â˜… MANUAL BREAKEVEN TRIGGERED: {0} â†’ Stop moved to {1:F2} (Entry + {2} tick)", 
                                entryName, manualBEStop, ManualBreakevenBuffer));
                        }
                    }
                }

                // v5.13 FREQUENCY CONTROL: Determine if we should check trailing based on current level
                // BE (level 0-1) and T3 (level 4) = every tick
                // T1 (level 2) and T2 (level 3) = every OTHER tick
                
                bool shouldCheckTrailing = true; // Default: check every tick
                
                // Determine current active level based on profit
                if (profitPoints >= Trail3TriggerPoints && pos.T1Filled && pos.T2Filled)
                {
                    // At T3 level (5+ points) - Check EVERY tick
                    shouldCheckTrailing = true;
                }
                else if (profitPoints >= Trail2TriggerPoints && pos.T1Filled)
                {
                    // At T2 level (4-4.99 points) - Check every OTHER tick
                    shouldCheckTrailing = (pos.TicksSinceEntry % 2 == 0);
                }
                else if (profitPoints >= Trail1TriggerPoints)
                {
                    // At T1 level (3-3.99 points) - Check every OTHER tick
                    shouldCheckTrailing = (pos.TicksSinceEntry % 2 == 0);
                }
                else
                {
                    // At BE level or below (0-2.99 points) - Check EVERY tick
                    shouldCheckTrailing = true;
                }

                // Only proceed with trailing logic if frequency check passes
                if (!shouldCheckTrailing)
                    continue;

                // Trail 3 (highest priority) - At 5 points, trail by 1 point
                // V8.22: Strictly profit based (no target dependencies)
                if (profitPoints >= Trail3TriggerPoints)
                {
                    double trail3Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail3DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail3DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail3Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail3Stop;
                        newTrailLevel = 4; // Level 4 = Trail 3
                    }
                    else if (pos.Direction == MarketPosition.Short && trail3Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail3Stop;
                        newTrailLevel = 4;
                    }
                }
                // Trail 2 - At 4 points, trail by 1.5 points
                else if (profitPoints >= Trail2TriggerPoints && pos.CurrentTrailLevel < 3)
                {
                    double trail2Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail2DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail2DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail2Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail2Stop;
                        newTrailLevel = 3; // Level 3 = Trail 2
                    }
                    else if (pos.Direction == MarketPosition.Short && trail2Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail2Stop;
                        newTrailLevel = 3;
                    }
                }
                // Trail 1 - At 3 points, trail by 2 points
                else if (profitPoints >= Trail1TriggerPoints && pos.CurrentTrailLevel < 2)
                {
                    double trail1Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail1DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail1DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail1Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail1Stop;
                        newTrailLevel = 2; // Level 2 = Trail 1
                    }
                    else if (pos.Direction == MarketPosition.Short && trail1Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail1Stop;
                        newTrailLevel = 2;
                    }
                }
                // Break-even - At 2 points, move to BE +1 tick
                else if (profitPoints >= BreakEvenTriggerPoints && pos.CurrentTrailLevel < 1)
                {
                    double beStop = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice + (BreakEvenOffsetTicks * tickSize)
                        : pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);

                    if (pos.Direction == MarketPosition.Long && beStop > pos.CurrentStopPrice)
                    {
                        newStopPrice = beStop;
                        newTrailLevel = 1;
                    }
                    else if (pos.Direction == MarketPosition.Short && beStop < pos.CurrentStopPrice)
                    {
                        newStopPrice = beStop;
                        newTrailLevel = 1;
                    }
                }

                // V8.21: Check if stop price actually changed by more than 1 tick before updating
                // This prevents redundant "micro-updates" that saturate the order system
                if (Math.Abs(newStopPrice - pos.CurrentStopPrice) < tickSize * 0.9)
                    continue;

                // Update stop if needed
                if (newStopPrice != pos.CurrentStopPrice)
                {
                    UpdateStopOrder(entryName, pos, newStopPrice, newTrailLevel);
                }
            }

            // V12.10: FLEET SYMMETRY SYNC PASS
            // When SIMA is enabled, force followers to match the Leader's trail level.
            // Followers calculate stops relative to their OWN entry prices but are triggered
            // by the Leader's profit progress. This prevents slippage-induced desync.
            if (EnableSIMA)
            {
                // Phase 1: Find the highest trail level among leader positions, by direction
                int leaderLongMaxLevel = 0;
                int leaderShortMaxLevel = 0;

                foreach (var kvp in positionSnapshot)
                {
                    PositionInfo ldr = kvp.Value;
                    if (ldr.IsFollower || !ldr.EntryFilled || !ldr.BracketSubmitted) continue;

                    if (ldr.Direction == MarketPosition.Long)
                        leaderLongMaxLevel = Math.Max(leaderLongMaxLevel, ldr.CurrentTrailLevel);
                    else if (ldr.Direction == MarketPosition.Short)
                        leaderShortMaxLevel = Math.Max(leaderShortMaxLevel, ldr.CurrentTrailLevel);
                }

                // V12.12: Diagnostic â€” log leader trail levels for fleet sync visibility
                if (leaderLongMaxLevel > 0 || leaderShortMaxLevel > 0)
                    Print($"[SIMA] Fleet Sync: Leader trail levels â€” Long={leaderLongMaxLevel}, Short={leaderShortMaxLevel}");

                // Phase 2: Sync lagging followers UP to the leader's level
                if (leaderLongMaxLevel > 0 || leaderShortMaxLevel > 0)
                {
                    foreach (var kvp in positionSnapshot)
                    {
                        string entryName2 = kvp.Key;
                        PositionInfo fol = kvp.Value;

                        if (!fol.IsFollower) continue;
                        if (!fol.EntryFilled || !fol.BracketSubmitted) continue;
                        if (!activePositions.ContainsKey(entryName2)) continue;

                        int targetLevel = (fol.Direction == MarketPosition.Long)
                            ? leaderLongMaxLevel
                            : leaderShortMaxLevel;

                        // V12.12: Guard â€” skip if no leader exists for this direction (targetLevel==0)
                        if (targetLevel == 0) continue;

                        // Only sync UP â€” never regress a follower already at a higher level
                        if (fol.CurrentTrailLevel >= targetLevel) continue;

                        double syncStopPrice = CalculateStopForLevel(fol, targetLevel);

                        // Only move if it's a more protective stop
                        bool isBetter = (fol.Direction == MarketPosition.Long)
                            ? syncStopPrice > fol.CurrentStopPrice
                            : syncStopPrice < fol.CurrentStopPrice;

                        if (isBetter)
                        {
                            UpdateStopOrder(entryName2, fol, syncStopPrice, targetLevel);
                            Print(string.Format("FLEET SYNC: {0} synced to Level {1} -> Stop {2:F2} (Leader advanced)",
                                entryName2, targetLevel, syncStopPrice));
                        }
                    }
                }
            }
        }

        // V8.30: Clean up stale pending replacements that are older than 5 seconds
        // Prevents memory leak and ensures positions remain protected
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
                            Print(string.Format("V8.30: Creating EMERGENCY replacement stop for {0}", kvp.Key));
                            CreateNewStopOrder(kvp.Key, pending.Quantity, pending.StopPrice, pending.Direction);
                        }
                    }
                }
            }
        }

        // V10 Bridge: Wrapper for IPC MoveStopsToBreakevenPlusOne
        private void ChangeStop(string entryName, double newStopPrice)
        {
            if (activePositions.TryGetValue(entryName, out PositionInfo pos))
            {
                UpdateStopOrder(entryName, pos, newStopPrice, 1); // 1 = BE level
            }
        }

        private void UpdateStopOrder(string entryName, PositionInfo pos, double newStopPrice, int newTrailLevel)
        {
            // V8.30: Thread-safe check using TryGetValue
            if (!stopOrders.TryGetValue(entryName, out var currentStop)) return;

            Order newStop = null;

            try
            {
                double validatedStopPrice = ValidateStopPrice(pos.Direction, newStopPrice);

                // V8.30: Thread-safe update using TryGetValue to avoid TOCTOU race
                if (pendingStopReplacements.TryGetValue(entryName, out var existingPending))
                {
                    // Update the pending replacement atomically (pending is a reference type)
                    existingPending.StopPrice = validatedStopPrice;
                    existingPending.Quantity = pos.RemainingContracts;
                    pos.CurrentStopPrice = validatedStopPrice;
                    pos.CurrentTrailLevel = newTrailLevel;
                    return;
                }

                // V8.11 FIX: Store pending replacement BEFORE cancelling
                // V8.12 FIX: Also handle CancelPending and PendingSubmit states to prevent race condition
                // V8.30: Added CreatedTime for timeout support and circuit breaker tracking
                if (currentStop != null && (currentStop.OrderState == OrderState.CancelPending || currentStop.OrderState == OrderState.Submitted))
                {
                    // Order is already being cancelled or submitted - queue the new stop price
                    var newPending = new PendingStopReplacement
                    {
                        EntryName = entryName,
                        Quantity = pos.RemainingContracts,
                        StopPrice = validatedStopPrice,
                        Direction = pos.Direction,
                        OldOrder = currentStop,
                        CreatedTime = DateTime.Now  // V8.30: Timeout support
                    };

                    // V8.30: Thread-safe add or update
                    if (pendingStopReplacements.TryAdd(entryName, newPending))
                    {
                        // V8.30: Track count for circuit breaker
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
                    }

                    pos.CurrentStopPrice = validatedStopPrice;
                    pos.CurrentTrailLevel = newTrailLevel;
                    Print(string.Format("V8.12: Stop update queued for {0} (current state: {1})", entryName, currentStop.OrderState));
                    return;
                }

                if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                {
                    var newPending = new PendingStopReplacement
                    {
                        EntryName = entryName,
                        Quantity = pos.RemainingContracts,
                        StopPrice = validatedStopPrice,
                        Direction = pos.Direction,
                        OldOrder = currentStop,
                        CreatedTime = DateTime.Now  // V8.30: Timeout support
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

                    if (pos.ExecutingAccount != null)
                    {
                        pos.ExecutingAccount.Cancel(new[] { currentStop });
                    }
                    else
                    {
                        CancelOrder(currentStop);
                    }
                    pos.CurrentStopPrice = validatedStopPrice;
                    pos.CurrentTrailLevel = newTrailLevel;

                    string levelName = newTrailLevel <= 0 ? "Initial" : (newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1));
                    Print(string.Format("STOP UPDATED: {0} â†’ {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName));
                    return;
                }

                // No existing stop or not in a cancellable state - create directly
                if (pos.ExecutingAccount != null)
                {
                    newStop = pos.ExecutingAccount.CreateOrder(Instrument, pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover, 
                        OrderType.StopMarket, TimeInForce.Gtc, pos.RemainingContracts, 0, validatedStopPrice, "Stop_" + entryName, "Stop_" + entryName, null);
                    pos.ExecutingAccount.Submit(new[] { newStop });
                    stopOrders[entryName] = newStop;
                }
                else
                {
                    // V12.3: Truncate signal name to stay under 50-char NinjaTrader limit
                    string suffix = (DateTime.Now.Ticks % 100000000).ToString();
                    string stopSigName = "S_" + entryName + "_" + suffix;
                    if (stopSigName.Length > 50) stopSigName = stopSigName.Substring(0, 50);
                    OrderAction stopExitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                    newStop = SubmitOrderUnmanaged(0, stopExitAction, OrderType.StopMarket, pos.RemainingContracts, 0, validatedStopPrice, "", stopSigName);

                    if (newStop != null) stopOrders[entryName] = newStop;
                }

                if (newStop == null)
                {
                    Print(string.Format("âš ï¸ CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(string.Format("âš ï¸ POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}",
                        pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                        pos.RemainingContracts,
                        pos.EntryPrice));
                    Print(string.Format("âš ï¸ Attempted stop price: {0:F2} | Current price: {1:F2}", validatedStopPrice, Close[0]));

                    Print(string.Format("âš ï¸ Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                stopOrders[entryName] = newStop;
                pos.CurrentStopPrice = validatedStopPrice;
                pos.CurrentTrailLevel = newTrailLevel;

                string levelName2 = newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1);
                Print(string.Format("STOP UPDATED: {0} â†’ {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName2));

            }
            catch (Exception ex)
            {
                Print(string.Format("âš ï¸ ERROR UpdateStopOrder for {0}: {1}", entryName, ex.Message));
                Print(string.Format("âš ï¸ POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
                
                // Attempt emergency flatten
                try
                {
                    Print(string.Format("âš ï¸ Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                }
                catch (Exception flattenEx)
                {
                    Print(string.Format("âš ï¸âš ï¸ EMERGENCY FLATTEN FAILED: {0}", flattenEx.Message));
                }
            }
        }

        // V12.10: Fleet Symmetry â€” calculates the correct stop price for a given trail level
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

        private void OnBreakevenButtonClick()
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("BREAKEVEN: No active positions");
                    return;
                }

                // V8.30: Thread-safe snapshot iteration for UI button handler
                var posSnapshot = activePositions.ToArray();

                // Check if any positions are already triggered (can't toggle after trigger)
                bool anyTriggered = false;
                foreach (var kvp in posSnapshot)
                {
                    if (kvp.Value.ManualBreakevenTriggered)
                    {
                        anyTriggered = true;
                        break;
                    }
                }

                if (anyTriggered)
                {
                    Print("BREAKEVEN: Already triggered - cannot toggle");
                    return;
                }

                // Check current state - if any armed, disarm all; if none armed, arm all
                bool anyArmed = false;
                foreach (var kvp in posSnapshot)
                {
                    if (kvp.Value.ManualBreakevenArmed)
                    {
                        anyArmed = true;
                        break;
                    }
                }

                // Toggle: if armed, disarm; if disarmed, arm
                foreach (var kvp in posSnapshot)
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    if (pos.EntryFilled && !pos.ManualBreakevenTriggered)
                    {
                        if (anyArmed)
                        {
                            // Disarm
                            pos.ManualBreakevenArmed = false;
                            Print(string.Format("BREAKEVEN DISARMED: {0}", kvp.Key));
                        }
                        else
                        {
                            // Arm
                            pos.ManualBreakevenArmed = true;
                            Print(string.Format("BREAKEVEN ARMED: {0} - Will trigger at Entry + {1} tick(s)",
                                kvp.Key, ManualBreakevenBuffer));
                        }
                    }
                }

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR OnBreakevenButtonClick: " + ex.Message);
            }
        }

        #endregion

        #region Position Sync

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
        /// BOTH local and fleet follower limit orders. When price touches a working limit,
        /// the order is converted to market so it fills immediately.
        /// Local orders: ChangeOrder(). Follower orders: cancel + resubmit via ExecutingAccount.
        /// </summary>
        private void ManageCIT()
        {
            if (activePositions.Count == 0 && entryOrders.Count == 0) return;
            if (string.IsNullOrEmpty(ChaseIfTouchPoints) || ChaseIfTouchPoints == "0") return;

            double citOffset = 0;
            if (!double.TryParse(ChaseIfTouchPoints, out citOffset)) return;

            // Iterate ALL entry orders in the unified dictionary (local + every fleet account)
            foreach (var kvp in entryOrders.ToArray())
            {
                string key = kvp.Key;
                Order order = kvp.Value;
                if (order == null || order.OrderState != OrderState.Working) continue;
                if (order.OrderType != OrderType.Limit) continue; // only chase limit entries

                double currentPrice = (order.OrderAction == OrderAction.Buy) ? High[0] : Low[0];
                double limitPrice = order.LimitPrice;

                bool triggerChase = (order.OrderAction == OrderAction.Buy)
                    ? (currentPrice >= limitPrice)
                    : (currentPrice <= limitPrice);

                if (!triggerChase) continue;

                // Determine local vs follower
                PositionInfo pos = null;
                activePositions.TryGetValue(key, out pos);
                bool isFollower = pos != null && pos.IsFollower && pos.ExecutingAccount != null;

                try
                {
                    if (isFollower)
                    {
                        // Fleet follower: cancel limit, resubmit as market via account API
                        Account followerAcct = pos.ExecutingAccount;
                        Print($"[CIT] FLEET chase: {key} on {followerAcct.Name} | Limit {limitPrice:F2} -> MKT @ {currentPrice:F2}");

                        followerAcct.Cancel(new[] { order });

                        Order mktOrder = followerAcct.CreateOrder(Instrument, order.OrderAction, OrderType.Market,
                            TimeInForce.Gtc, order.Quantity, 0, 0, "", "CIT_" + key, null);
                        followerAcct.Submit(new[] { mktOrder });

                        entryOrders[key] = mktOrder; // update reference
                    }
                    else
                    {
                        // Local account: ChangeOrder converts limit to market
                        Print($"[CIT] LOCAL chase: {key} | Limit {limitPrice:F2} -> MKT @ {currentPrice:F2}");
                        ChangeOrder(order, order.Quantity, 0, 0);
                    }
                }
                catch (Exception ex)
                {
                    Print($"[CIT] ERROR chasing {key}: {ex.Message}");
                }
            }
        }

        private void FlattenAll()
        {
            isFlattenRunning = true; // V12.13b: Suppress stop re-submit during flatten
            try
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
                         SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, liveQty, 0, 0, "", "Flatten_Ghost");
                     
                     return; 
                }

                if (activePositions.Count == 0 && Position.MarketPosition == MarketPosition.Flat)
                {
                    Print("FLATTEN: No active positions to close");
                    // Still run SIMA flatten just in case of desync
                    if (EnableSIMA) FlattenAllApexAccounts();
                    return;
                }

                Print("FLATTEN: Closing all positions...");

                // V12.13b: Removed ExitLong/ExitShort block (managed-mode methods incompatible with IsUnmanaged=true)
                // Unmanaged flatten via SubmitOrderUnmanaged is handled below at the per-position level

                // 2. Clear all pending entry orders on Master
                foreach (var entryOrder in entryOrders.Values)
                {
                    if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                        CancelOrder(entryOrder);
                }

                // 3. Flatten SIMA Fleet
                if (EnableSIMA)
                {
                    FlattenAllApexAccounts();
                }

                // V12.2: Reset Sync State
                isLongArmed = false;
                isShortArmed = false;

                List<string> positionsToCleanup = new List<string>();

                // V8.30: Thread-safe snapshot iteration
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (pos.EntryFilled)
                    {
                        Print(string.Format("FLATTEN: Closing filled {0} position",
                            pos.Direction == MarketPosition.Long ? "LONG" : "SHORT"));

                        // V8.31: Cancel ALL bracket orders comprehensively
                        // Cancel stop order (may have multiple from rapid trailing)
                        if (stopOrders.TryGetValue(entryName, out var stopOrder))
                        {
                            if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted || stopOrder.OrderState == OrderState.Submitted))
                            {
                                CancelOrder(stopOrder);
                                Print(string.Format("FLATTEN: Cancelling stop for {0}", entryName));
                            }
                        }

                        // V8.31: Also clear any pending stop replacements to prevent orphaned stops
                        if (pendingStopReplacements.TryRemove(entryName, out _))
                        {
                            Interlocked.Decrement(ref pendingReplacementCount);
                            Print(string.Format("V8.31: Cleared pending stop replacement for {0}", entryName));
                        }

                        // Cancel T1 order
                        if (target1Orders.TryGetValue(entryName, out var t1Order))
                        {
                            if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted || t1Order.OrderState == OrderState.Submitted))
                                CancelOrder(t1Order);
                        }

                        // Cancel T2 order
                        if (target2Orders.TryGetValue(entryName, out var t2Order))
                        {
                            if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted || t2Order.OrderState == OrderState.Submitted))
                                CancelOrder(t2Order);
                        }

                        // V8.31: Cancel T3 order
                        if (target3Orders.TryGetValue(entryName, out var t3Order))
                        {
                            if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted || t3Order.OrderState == OrderState.Submitted))
                                CancelOrder(t3Order);
                        }

                        // V8.31: Cancel T4 order
                        if (target4Orders.TryGetValue(entryName, out var t4Order))
                        {
                            if (t4Order != null && (t4Order.OrderState == OrderState.Working || t4Order.OrderState == OrderState.Accepted || t4Order.OrderState == OrderState.Submitted))
                                CancelOrder(t4Order);
                        }

                        // V8.28 FIX: Use LIVE position quantity instead of cached RemainingContracts
                        int livePositionQty = 0;
                        try
                        {
                            if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                                livePositionQty = Position.Quantity;
                        }
                        catch (Exception pEx) { Print("Flatten Error reading Position: " + pEx.Message); }
                        
                        // Use the smaller of cached and live to avoid overselling
                        // V10 DIAGNOSTIC: Print values
                        Print(string.Format("FLATTEN DIAGNOSTIC: Entry={0} Cached={1} Live={2}", entryName, pos.RemainingContracts, livePositionQty));

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
                            Order flattenOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, flattenQty, 0, 0, "", "Flatten_" + entryName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, flattenQty, 0, 0, "", "Flatten_" + entryName);

                            if (flattenOrder == null) Print("FLATTEN ERROR: SubmitOrderUnmanaged returned NULL");
                            else Print(string.Format("FLATTEN SENT: {0} {1} contracts", pos.Direction == MarketPosition.Long ? "SELL" : "BUY", flattenQty));
                        }
                        else
                        {
                             Print("FLATTEN SKIPPED: Qty is 0");
                        }

                        positionsToCleanup.Add(entryName);
                    }
                    else
                    {
                        // Cancel pending entry order
                        if (entryOrders.ContainsKey(entryName))
                        {
                            Order entryOrder = entryOrders[entryName];
                            if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(entryOrder);
                                Print(string.Format("FLATTEN: Cancelled pending {0} entry order @ {1:F2}",
                                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", pos.EntryPrice));
                            }
                        }
                        positionsToCleanup.Add(entryName);
                    }
                }

                foreach (string key in positionsToCleanup)
                {
                    CleanupPosition(key);
                }

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR FlattenAll: " + ex.Message);
            }
            finally
            {
                isFlattenRunning = false; // V12.13b: Always release guard
            }
        }

        private void FlattenPositionByName(string entryName)
        {
            if (!activePositions.TryGetValue(entryName, out var pos)) return;

            if (pos.EntryFilled && pos.RemainingContracts > 0)
            {
                Print(string.Format("âš ï¸ EMERGENCY FLATTEN: Closing {0} position due to stop order failure", entryName));

                // V12.3: Determine if this is a fleet follower or local position
                bool isFleetFollower = pos.IsFollower && pos.ExecutingAccount != null;

                // V8.31: Cancel ALL bracket orders first to prevent race conditions
                // V12.3: Use Account.Cancel for fleet followers, CancelOrder for local
                if (stopOrders.TryGetValue(entryName, out var stopOrder) && stopOrder != null)
                {
                    if (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { stopOrder });
                        else CancelOrder(stopOrder);
                    }
                }
                if (target1Orders.TryGetValue(entryName, out var t1Order) && t1Order != null)
                {
                    if (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t1Order });
                        else CancelOrder(t1Order);
                    }
                }
                if (target2Orders.TryGetValue(entryName, out var t2Order) && t2Order != null)
                {
                    if (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t2Order });
                        else CancelOrder(t2Order);
                    }
                }
                if (target3Orders.TryGetValue(entryName, out var t3Order) && t3Order != null)
                {
                    if (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t3Order });
                        else CancelOrder(t3Order);
                    }
                }
                if (target4Orders.TryGetValue(entryName, out var t4Order) && t4Order != null)
                {
                    if (t4Order.OrderState == OrderState.Working || t4Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t4Order });
                        else CancelOrder(t4Order);
                    }
                }

                // V8.31: Clear pending replacements
                if (pendingStopReplacements.TryRemove(entryName, out _))
                {
                    Interlocked.Decrement(ref pendingReplacementCount);
                }

                int flattenQty = pos.RemainingContracts;
                OrderAction flattenAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                // V12.3: Route flatten order to correct account
                Order flattenOrder = null;
                if (isFleetFollower)
                {
                    // Fleet follower: flatten on the follower's own account
                    string sigName = "EF_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    flattenOrder = pos.ExecutingAccount.CreateOrder(Instrument, flattenAction,
                        OrderType.Market, TimeInForce.Gtc, flattenQty, 0, 0, "", sigName, null);
                    pos.ExecutingAccount.Submit(new[] { flattenOrder });
                }
                else
                {
                    // Local: use SubmitOrderUnmanaged (use live position qty for accuracy)
                    try
                    {
                        if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                            flattenQty = Math.Max(flattenQty, Position.Quantity);
                    }
                    catch { }

                    string sigName = "EF_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    flattenOrder = SubmitOrderUnmanaged(0, flattenAction, OrderType.Market, flattenQty, 0, 0, "", sigName);
                }

                if (flattenOrder != null)
                {
                    Print(string.Format("Emergency flatten order submitted on {0}: {1} {2} contracts at MARKET",
                        isFleetFollower ? pos.ExecutingAccount.Name : "LOCAL",
                        pos.Direction == MarketPosition.Long ? "SELL" : "BUY",
                        flattenQty));
                }
                else
                {
                    Print(string.Format("âš ï¸âš ï¸âš ï¸ CRITICAL: Emergency flatten order FAILED for {0}!", entryName));
                    Print("âš ï¸âš ï¸âš ï¸ MANUAL INTERVENTION REQUIRED - Close position manually in NinjaTrader!");
                }
            }
        }


        private void CleanupPosition(string entryName)
        {
            // V8.17 EMERGENCY FIX: Move removal to TOP to prevent recursion
            // V8.30: Use atomic TryRemove for thread-safe removal
            if (!activePositions.TryRemove(entryName, out _)) return;

            int cancelledStops = 0;
            int cancelledTargets = 0;
            int cancelledEntries = 0;

            // V8.17 FIX: Use explicit dictionary-based cancellation instead of scanning ALL Account.Orders
            // V8.30: Use TryRemove for thread-safe atomic removal

            // Cancel stop order
            if (stopOrders.TryRemove(entryName, out var stopOrder))
            {
                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(stopOrder);
                    cancelledStops++;
                }
            }

            // Cancel T1
            if (target1Orders.TryRemove(entryName, out var t1Order))
            {
                if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(t1Order);
                    cancelledTargets++;
                }
            }

            // Cancel T2
            if (target2Orders.TryRemove(entryName, out var t2Order))
            {
                if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(t2Order);
                    cancelledTargets++;
                }
            }

            // Cancel T3
            if (target3Orders.TryRemove(entryName, out var t3Order))
            {
                if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(t3Order);
                    cancelledTargets++;
                }
            }

            // Cancel T4/Entry
            if (entryOrders.TryRemove(entryName, out var eOrder))
            {
                if (eOrder != null && (eOrder.OrderState == OrderState.Working || eOrder.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(eOrder);
                    cancelledEntries++;
                }
            }

            // V8.30: Thread-safe removal with count decrement for pending replacements
            if (pendingStopReplacements.TryRemove(entryName, out _))
            {
                Interlocked.Decrement(ref pendingReplacementCount);
            }
            target4Orders.TryRemove(entryName, out _);

            // Log cleanup summary
            if (cancelledStops > 0 || cancelledTargets > 0 || cancelledEntries > 0)
            {
                Print(string.Format("CLEANUP SUMMARY for {0}: Stops={1} Targets={2} Entries={3}", 
                    entryName, cancelledStops, cancelledTargets, cancelledEntries));
            }

            UpdateDisplay();
        }

        /// <summary>
        /// V12.12: Remove any ghost order reference (targets, stops, entries) when it reaches a terminal state.
        /// This only clears stale references; it does not alter stop quantities or position state.
        /// </summary>
        private void RemoveGhostOrderRef(Order order, string reason)
        {
            if (order == null) return;

            var orderDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
            {
                (target1Orders, "T1"),
                (target2Orders, "T2"),
                (target3Orders, "T3"),
                (target4Orders, "T4"),
                (stopOrders, "STOP"),
                (entryOrders, "ENTRY"),
            };

            bool foundInDict = false;
            string removedLabel = null;
            string removedKey = null;
            foreach (var (dict, label) in orderDicts)
            {
                // V12.17: Dual match - reference equality OR OrderId string match
                foreach (var kvp in dict.ToArray())
                {
                    if (kvp.Value == order ||
                        (kvp.Value != null && order != null && kvp.Value.OrderId == order.OrderId))
                    {
                        if (dict.TryRemove(kvp.Key, out _))
                        {
                            string matchType = (kvp.Value == order) ? "REF" : "ORDERID";
                            Print(string.Format("V12.17: {0} {1} - removed ghost ref for {2} (match={3}, OrderId={4})",
                                label, reason, kvp.Key, matchType, order.OrderId ?? "NULL"));
                            foundInDict = true;
                            removedLabel = label;
                            removedKey = kvp.Key;
                        }
                    }
                }
            }

            // V12.17: Position protection audit - if we just removed a STOP, check if position is now unprotected
            if (foundInDict && removedLabel == "STOP" && !string.IsNullOrEmpty(removedKey))
            {
                if (activePositions.TryGetValue(removedKey, out var auditPos) && auditPos.EntryFilled && auditPos.RemainingContracts > 0)
                {
                    if (!stopOrders.ContainsKey(removedKey))
                    {
                        Print(string.Format("V12.17: WARNING UNPROTECTED POSITION: {0} has {1} contracts with NO STOP after {2}. Manual intervention may be required.",
                            removedKey, auditPos.RemainingContracts, reason));
                    }
                }
            }

            // V12.17: If it was not in our dictionaries, classify why
            if (!foundInDict)
            {
                // Only log if it is one of our orders (matching prefix) to avoid noise from other strategies
                if (order.Name.Contains("RMA") || order.Name.Contains("OR") || order.Name.Contains("MOMO") || order.Name.Contains("TREND") ||
                    order.Name.Contains("Stop_") || order.Name.Contains("Tgt_") || order.Name.Contains("Fleet_"))
                {
                    // V12.17: Distinguish expected cascade from suspicious orphan
                    bool positionStillActive = false;
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (order.Name.Contains(kvp.Key))
                        {
                            positionStillActive = true;
                            Print(string.Format("V12.17: WARNING {0} {1} - dict ref gone but position {2} still active (orphan risk, OrderId={3})",
                                order.Name, reason, kvp.Key, order.OrderId ?? "NULL"));
                            break;
                        }
                    }
                    if (!positionStillActive)
                    {
                        Print(string.Format("V12.17: {0} {1} - cleaned by upstream handler (expected cascade, OrderId={2})", order.Name, reason, order.OrderId ?? "NULL"));
                    }
                }
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            try
            {
                if (execution == null || execution.Order == null) return;

                string orderName = execution.Order.Name;
                if (string.IsNullOrEmpty(orderName)) return;

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
                        // Decrement RemainingContracts by the filled quantity
                        pos.RemainingContracts -= quantity;

                        Print(string.Format("STOP FILLED: {0} @ {1:F2}. Cancelling targets.", quantity, price));

                        // Manual OCO: Cancel all remaining profit targets immediately
                        int cancelledTargets = 0;

                        // Cancel T1
                        if (target1Orders.TryRemove(entryName, out var t1Order))
                        {
                            if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t1Order);
                                cancelledTargets++;
                            }
                        }

                        // Cancel T2
                        if (target2Orders.TryRemove(entryName, out var t2Order))
                        {
                            if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t2Order);
                                cancelledTargets++;
                            }
                        }

                        // Cancel T3
                        if (target3Orders.TryRemove(entryName, out var t3Order))
                        {
                            if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t3Order);
                                cancelledTargets++;
                            }
                        }

                        // Cancel T4 if present
                        if (target4Orders.TryRemove(entryName, out var t4Order))
                        {
                            if (t4Order != null && (t4Order.OrderState == OrderState.Working || t4Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t4Order);
                                cancelledTargets++;
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
                        if (pos.RemainingContracts <= 0)
                        {
                            activePositions.TryRemove(entryName, out _);
                            entryOrders.TryRemove(entryName, out _);
                            Print(string.Format("Position {0} fully closed by stop.", entryName));
                        }

                        UpdateDisplay();
                    }
                }

                // ============================================================
                // 2. TARGET 1 FILL - Reduce stop quantity
                // ============================================================
                else if (orderName.StartsWith("T1_"))
                {
                    string entryName = extractEntryName(orderName, "T1_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // Decrement RemainingContracts by the filled quantity
                        pos.RemainingContracts -= quantity;
                        pos.T1Filled = true;

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            quantity, price, pos.RemainingContracts));

                        // Update stop quantity to match new position size
                        if (pos.RemainingContracts > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                    Print(string.Format("OCO: Cancelled stop for fully closed position {0}", entryName));
                                }
                            }
                            activePositions.TryRemove(entryName, out _);
                        }

                        // Remove T1 order reference
                        target1Orders.TryRemove(entryName, out _);
                        UpdateDisplay();
                    }
                }

                // ============================================================
                // 3. TARGET 2 FILL - Reduce stop quantity
                // ============================================================
                else if (orderName.StartsWith("T2_"))
                {
                    string entryName = extractEntryName(orderName, "T2_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // Decrement RemainingContracts by the filled quantity
                        pos.RemainingContracts -= quantity;
                        pos.T2Filled = true;

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            quantity, price, pos.RemainingContracts));

                        // Update stop quantity to match new position size
                        if (pos.RemainingContracts > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                    Print(string.Format("OCO: Cancelled stop for fully closed position {0}", entryName));
                                }
                            }
                            activePositions.TryRemove(entryName, out _);
                        }

                        // Remove T2 order reference
                        target2Orders.TryRemove(entryName, out _);
                        UpdateDisplay();
                    }
                }

                // ============================================================
                // 4. TARGET 3 FILL - Reduce stop quantity
                // ============================================================
                else if (orderName.StartsWith("T3_"))
                {
                    string entryName = extractEntryName(orderName, "T3_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // Decrement RemainingContracts by the filled quantity
                        pos.RemainingContracts -= quantity;
                        pos.T3Filled = true;

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            quantity, price, pos.RemainingContracts));

                        // Update stop quantity to match new position size
                        if (pos.RemainingContracts > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                    Print(string.Format("OCO: Cancelled stop for fully closed position {0}", entryName));
                                }
                            }
                            activePositions.TryRemove(entryName, out _);
                        }

                        // Remove T3 order reference
                        target3Orders.TryRemove(entryName, out _);
                        UpdateDisplay();
                    }
                }

                // ============================================================
                // 5. TRIM EXECUTION - V10.3.1: Enhanced Stop Integrity
                // ============================================================
                // ðŸ”¥ CRITICAL: When a TRIM executes, we MUST reduce the stop order quantity
                // to match the new position size. If we don't, hitting the stop after a trim
                // would close more contracts than we hold, creating an unintended REVERSE position.
                // Example: Long 4 contracts, stop at 4. Trim 2 (now Long 2). If stop stays at 4,
                // getting stopped out would SELL 4 (close 2 + go SHORT 2) = DISASTER.
                else if (orderName.StartsWith("Trim_"))
                {
                    string entryName = extractEntryName(orderName, "Trim_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // Track previous quantity for logging
                        int previousQty = pos.RemainingContracts;

                        // Deduct ONLY the execution quantity (handle partial fills correctly)
                        pos.RemainingContracts -= quantity;

                        Print(string.Format("TRIM EXECUTION: {0} contracts closed for {1}. Position: {2} â†’ {3}",
                            quantity, entryName, previousQty, pos.RemainingContracts));

                        // V10.3.1 FIX: MANDATORY stop quantity reduction to prevent reverse position
                        if (pos.RemainingContracts > 0)
                        {
                            Print(string.Format("STOP INTEGRITY: Reducing stop quantity from {0} to {1} for {2}",
                                previousQty, pos.RemainingContracts, entryName));
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed by trim, cancel stop
                            Print(string.Format("TRIM FLATTEN: Position {0} fully closed. Cancelling stop.", entryName));
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                }
                            }

                            // Also clean up any pending replacements
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }

                            activePositions.TryRemove(entryName, out _);
                        }

                        UpdateDisplay();
                    }
                }
            }
            catch (Exception ex)
            {
                Print("Error OnExecutionUpdate: " + ex.Message);
            }
        }

        private void ReconcileOrphanedOrders(string reason)
        {
            try
            {
                if (Account == null) return;

                bool foundOrphans = false;
                foreach (Order order in Account.Orders)
                {
                    if (order == null) continue;
                    
                    // Only look at working orders
                    if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted)
                        continue;

                    // V8.27 CRITICAL FIX: Only process orders for THIS instrument
                    // This prevents cross-instrument cancellation when running multiple strategy instances
                    if (order.Instrument.FullName != Instrument.FullName)
                        continue;

                    // Check if this order has one of our prefix signatures
                    string name = order.Name;
                    if (name.StartsWith("Stop_") || name.StartsWith("T1_") || name.StartsWith("T2_") || 
                        name.StartsWith("T3_") || name.StartsWith("T4_") || name.StartsWith("Flatten_") || name.StartsWith("Trim_"))
                    {
                        // Check if we actually have an active position for this
                        string entryName = "";
                        if (name.Contains("_"))
                        {
                            int firstUnderscore = name.IndexOf('_');
                            entryName = name.Substring(firstUnderscore + 1);
                            // Strip timestamp if present
                            int lastUnderscore = entryName.LastIndexOf('_');
                            if (lastUnderscore > 0 && entryName.Length - lastUnderscore > 10)
                                entryName = entryName.Substring(0, lastUnderscore);
                        }

                        // V10 FIX: Handle TRIM execution state update - MOVED TO OnExecutionUpdate

                        if (string.IsNullOrEmpty(entryName) || !activePositions.ContainsKey(entryName))
                        {
                            Print(string.Format("ORPHANED ORDER DETECTED ({0}): {1} | Cancelling...", reason, name));
                            CancelOrder(order);
                            foundOrphans = true;
                        }
                    }
                }

                if (foundOrphans)
                    Print("Orphaned order reconciliation complete.");
            }
            catch (Exception ex)
            {
                Print("ERROR ReconcileOrphanedOrders: " + ex.Message);
            }
        }

        #endregion

        #region Stop Management Helpers (V11)

        /// <summary>
        /// Moves all active position stops to Breakeven + Offset Points.
        /// If offset is 0, it is pure breakeven.
        /// </summary>
        private void MoveStopsToBreakevenWithOffset(double offsetPoints)
        {
            try
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;

                    double newStopPrice;
                    if (pos.Direction == MarketPosition.Long)
                        newStopPrice = pos.EntryPrice + offsetPoints;
                    else
                        newStopPrice = pos.EntryPrice - offsetPoints;

                    // Round to tick size
                    newStopPrice = Instrument.MasterInstrument.RoundToTickSize(newStopPrice);

                    // Only move stop if it's a better price (profit-protecting direction)
                    bool isBetter = (pos.Direction == MarketPosition.Long && newStopPrice > pos.CurrentStopPrice)
                                 || (pos.Direction == MarketPosition.Short && newStopPrice < pos.CurrentStopPrice);

                    if (!isBetter)
                    {
                        Print(string.Format("BE+{0}: Stop already better for {1}. Current={2:F2}, Request={3:F2}",
                            offsetPoints, entryName, pos.CurrentStopPrice, newStopPrice));
                        continue;
                    }

                    // V12.10: Use UpdateStopOrder for proper Master/Follower routing
                    // (ChangeOrder only works for Master â€” followers were silently skipped)
                    UpdateStopOrder(entryName, pos, newStopPrice, 1);
                    pos.ManualBreakevenTriggered = true;
                    Print(string.Format("BE+{0} MOVED: {1} Stop -> {2:F2}", offsetPoints, entryName, newStopPrice));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR MoveStopsToBreakevenWithOffset: " + ex.Message);
            }
        }

        #endregion
    }
}
