// Build 971: Trailing.Breakeven -- MoveStopsToBreakevenWithOffset, MoveSpecificTarget + Stop Management Helpers
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


        #region Stop Management Helpers (V11)

        /// <summary>
        /// Moves all active position stops to Breakeven + Offset Points.
        /// If offset is 0, it is pure breakeven.
        /// </summary>
        private void MoveStopsToBreakevenWithOffset(double offsetPoints)
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("[BE_INFO] No active trades in memory to move.");
                    return;
                }

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

                    // [Build 1108.002-HF1] Master-drives-followers: followers skip priceCleared gate.
                    // BE is an explicit manual action -- threshold logic protects the master only.
                    // UpdateStopOrder handles IsFollower routing (account-level cancel+resubmit).
                    if (pos.IsFollower)
                    {
                        bool isBetterF = (pos.Direction == MarketPosition.Long && newStopPrice > pos.CurrentStopPrice)
                                      || (pos.Direction == MarketPosition.Short && newStopPrice < pos.CurrentStopPrice);
                        if (isBetterF)
                        {
                            UpdateStopOrder(entryName, pos, newStopPrice, 1);
                            pos.ManualBreakevenTriggered = true;
                            MarkStickyDirty();
                            Print(string.Format("BE+{0} MOVED (follower): {1} Stop -> {2:F2}", offsetPoints, entryName, newStopPrice));
                        }
                        continue;
                    }

                    // [V12.12] ARM GUARD: If price hasn't cleared the BE threshold yet, arm instead of executing.
                    // ManageTrailingStops() will call UpdateStopOrder when price crosses the threshold.
                    if (lastKnownPrice <= 0)
                    {
                        Print(string.Format("[BE_ABORT] {0}: Price data stale (0). Waiting for next tick.", entryName));
                        continue;
                    }
                    double referencePrice = lastKnownPrice;
                    bool priceCleared = pos.Direction == MarketPosition.Long
                        ? referencePrice >= newStopPrice
                        : referencePrice <= newStopPrice;

                    if (!priceCleared)
                    {
                        pos.ManualBreakevenArmed = true;
                        pos.ManualBreakevenTriggered = false;
                        Print(string.Format("[V12] BE Armed: {0} Price has not reached threshold. Shielding entry once cleared.", entryName));
                        continue;
                    }

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
                    // (ChangeOrder only works for Master -- followers were silently skipped)
                    UpdateStopOrder(entryName, pos, newStopPrice, 1);
                    pos.ManualBreakevenTriggered = true;
                    MarkStickyDirty(); // Build 1103: Persist breakeven state
                    Print(string.Format("BE+{0} MOVED: {1} Stop -> {2:F2}", offsetPoints, entryName, newStopPrice));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR MoveStopsToBreakevenWithOffset: " + ex.Message);
            }
        }

        /// <summary>
        /// V14: Moves a specific target to a new profit level (Entry + X points)
        /// </summary>
        /// <param name="targetNum">Target number (1-5)</param>
        /// <param name="profitPoints">Points of profit from entry (1.0 or 2.0)</param>
        private void MoveSpecificTarget(int targetNum, double profitPoints)
        {
            if (targetNum < 1 || targetNum > 5)
            {
                Print($"[V14] MoveSpecificTarget: Invalid target number {targetNum}");
                return;
            }
            
            if (activePositions == null || activePositions.Count == 0)
            {
                Print($"[V14] MoveSpecificTarget: No active positions to move target T{targetNum}");
                return;
            }
            
            int movedCount = 0;
            
            // Iterate through all active positions
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                
                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;
                
                if (!pos.EntryFilled)
                {
                    Print($"[V14] MoveSpecificTarget T{targetNum}: Skipping {entryName} - entry not filled");
                    continue;
                }
                
                // Find the target order for this position
                // [1102Z-F]: Search the correct account -- follower orders live on their own account,
                // not on the Master account from which Account.Orders is sourced.
                string targetOrderName = $"T{targetNum}_{entryName}";
                Order targetOrder = null;
                var searchAcct = (pos.IsFollower && pos.ExecutingAccount != null)
                    ? pos.ExecutingAccount
                    : Account;
                
                foreach (Order order in searchAcct.Orders)
                {
                    if (order != null && 
                        order.Name == targetOrderName && 
                        order.Instrument.FullName == Instrument.FullName &&
                        (order.OrderState == OrderState.Working || 
                         order.OrderState == OrderState.Accepted))
                    {
                        targetOrder = order;
                        break;
                    }
                }
                
                if (targetOrder == null)
                {
                    Print($"[V14] MoveSpecificTarget T{targetNum}: No working order found for {entryName} (may already be filled)");
                    continue;
                }
                
                // Calculate new target price: Entry Price + Profit Points
                double entryPrice = pos.EntryPrice;
                double newTargetPrice;
                
                if (pos.Direction == MarketPosition.Long)
                {
                    newTargetPrice = entryPrice + profitPoints;
                }
                else // Short
                {
                    newTargetPrice = entryPrice - profitPoints;
                }
                
                // Round to tick size
                newTargetPrice = Instrument.MasterInstrument.RoundToTickSize(newTargetPrice);
                
                // Validate: Don't move target past current market (would execute immediately)
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                bool isValidMove = true;
                
                if (pos.Direction == MarketPosition.Long)
                {
                    // Long: Target should be above entry, but below or at market is OK (just fills immediately)
                    if (newTargetPrice < entryPrice)
                    {
                        Print($"[V14] MoveSpecificTarget T{targetNum}: REJECTED - Long target {newTargetPrice:F2} below entry {entryPrice:F2}");
                        isValidMove = false;
                    }
                }
                else // Short
                {
                    // Short: Target should be below entry
                    if (newTargetPrice > entryPrice)
                    {
                        Print($"[V14] MoveSpecificTarget T{targetNum}: REJECTED - Short target {newTargetPrice:F2} above entry {entryPrice:F2}");
                        isValidMove = false;
                    }
                }
                
                if (!isValidMove) continue;
                
                // Move the order: Master uses ChangeOrder; followers use cancel+resubmit via account API.
                // ChangeOrder only works for orders submitted through the NinjaScript managed order system.
                // Fleet follower orders are submitted via acct.Submit(), so they require the broker-level API.
                try
                {
                    if (pos.IsFollower && pos.ExecutingAccount != null)
                    {
                        // B957/C1: Two-phase FSM for follower target replacement (banned Cancel+Submit replaced).
                        // Record spec in _followerTargetReplaceSpecs, cancel only -- submission deferred to
                        // broker cancel confirmation in OnAccountOrderUpdate / SubmitFollowerTargetReplacement().
                        OrderAction exitAct = pos.Direction == MarketPosition.Long
                            ? OrderAction.Sell : OrderAction.BuyToCover;
                        var tSpec = new FollowerTargetReplaceSpec
                        {
                            EntryName      = entryName,
                            TargetNum      = targetNum,
                            NewTargetPrice = newTargetPrice,
                            Quantity       = targetOrder.Quantity,
                            ExitAction     = exitAct,
                            TargetAccount  = pos.ExecutingAccount,
                            CancellingOrderId = targetOrder.OrderId
                        };
                        _followerTargetReplaceSpecs[targetOrderName] = tSpec;
                        // A1-2: Stamp REAPER grace window before cancel to suppress false desync during replace gap.
                        StampReaperMoveGrace();
                        pos.ExecutingAccount.Cancel(new[] { targetOrder });
                        movedCount++;
                        double profitFromEntryF = Math.Abs(newTargetPrice - entryPrice);
                        Print($"[SIMA] MoveSpecificTarget T{targetNum}: Follower {entryName} on {pos.ExecutingAccount.Name} -> FSM PendingCancel -> {newTargetPrice:F2} (+{profitFromEntryF:F2})");
                    }
                    else
                    {
                        // Master path -- ChangeOrder is fine for NinjaScript-managed orders
                        ChangeOrder(targetOrder, targetOrder.Quantity, newTargetPrice, 0);
                        movedCount++;

                        double profitFromEntry = Math.Abs(newTargetPrice - entryPrice);
                        Print($"[V14] MoveSpecificTarget T{targetNum}: {entryName} -> {newTargetPrice:F2} (+{profitFromEntry:F2} from entry {entryPrice:F2})");
                    }
                }
                catch (Exception ex)
                {
                    Print($"[V14] MoveSpecificTarget T{targetNum}: Move FAILED for {entryName} - {ex.Message}");
                }
            }
            
            if (movedCount > 0)
            {
                Print($"[V14] MoveSpecificTarget T{targetNum}: Moved {movedCount} target(s) to +{profitPoints}pt profit");
            }
            else
            {
                Print($"[V14] MoveSpecificTarget T{targetNum}: No targets were moved (no active working orders found)");
            }
        }

        // Build 1107: Moves a specific target to an absolute price (from live control center).
        // Mirrors MoveSpecificTarget structure: finds working order on correct account,
        // validates direction safety, uses ChangeOrder for master and FSM for follower.
        private void MoveSpecificTargetAbsolute(int targetNum, double absolutePrice)
        {
            if (targetNum < 1 || targetNum > 5 || absolutePrice <= 0) return;
            if (activePositions == null || activePositions.Count == 0) return;

            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;
                if (!pos.EntryFilled || pos.PendingCleanup) continue;

                // Find working target order on the correct account
                string targetOrderName = string.Format("T{0}_{1}", targetNum, entryName);
                Order targetOrder = null;
                var searchAcct = (pos.IsFollower && pos.ExecutingAccount != null)
                    ? pos.ExecutingAccount : Account;

                foreach (Order order in searchAcct.Orders)
                {
                    if (order != null && order.Name == targetOrderName
                        && order.Instrument.FullName == Instrument.FullName
                        && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted))
                    {
                        targetOrder = order;
                        break;
                    }
                }

                if (targetOrder == null)
                {
                    Print(string.Format("[V12] SET_TARGET_PRICE T{0}: No working order for {1}", targetNum, entryName));
                    continue;
                }

                double newPrice = Instrument.MasterInstrument.RoundToTickSize(absolutePrice);

                // Direction safety validation
                if (pos.Direction == MarketPosition.Long && newPrice <= pos.EntryPrice)
                {
                    Print(string.Format("[V12] SET_TARGET_PRICE T{0}: REJECTED -- Long target {1:F2} at/below entry {2:F2}",
                        targetNum, newPrice, pos.EntryPrice));
                    continue;
                }
                if (pos.Direction == MarketPosition.Short && newPrice >= pos.EntryPrice)
                {
                    Print(string.Format("[V12] SET_TARGET_PRICE T{0}: REJECTED -- Short target {1:F2} at/above entry {2:F2}",
                        targetNum, newPrice, pos.EntryPrice));
                    continue;
                }

                try
                {
                    if (pos.IsFollower && pos.ExecutingAccount != null)
                    {
                        // Follower: Two-phase FSM (DNA-compliant, no raw Cancel+Submit)
                        OrderAction exitAct = pos.Direction == MarketPosition.Long
                            ? OrderAction.Sell : OrderAction.BuyToCover;
                        var tSpec = new FollowerTargetReplaceSpec
                        {
                            EntryName = entryName,
                            TargetNum = targetNum,
                            NewTargetPrice = newPrice,
                            Quantity = targetOrder.Quantity,
                            ExitAction = exitAct,
                            TargetAccount = pos.ExecutingAccount,
                            CancellingOrderId = targetOrder.OrderId
                        };
                        _followerTargetReplaceSpecs[targetOrderName] = tSpec;
                        StampReaperMoveGrace();
                        pos.ExecutingAccount.Cancel(new[] { targetOrder });
                        Print(string.Format("[V12] SET_TARGET_PRICE T{0}: Follower FSM queued on {1} -> {2:F2}",
                            targetNum, pos.ExecutingAccount.Name, newPrice));
                    }
                    else
                    {
                        // Master: ChangeOrder for atomic in-place modification
                        ChangeOrder(targetOrder, targetOrder.Quantity, newPrice, 0);
                        Print(string.Format("[V12] SET_TARGET_PRICE T{0}: Master ChangeOrder -> {1:F2}",
                            targetNum, newPrice));
                    }
                }
                catch (Exception ex)
                {
                    Print(string.Format("[V12] SET_TARGET_PRICE T{0} error: {1}", targetNum, ex.Message));
                }
            }
        }

        #endregion
    }
}
