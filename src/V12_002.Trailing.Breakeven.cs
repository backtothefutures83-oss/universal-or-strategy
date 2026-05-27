// Build 971: Trailing.Breakeven -- MoveStopsToBreakevenWithOffset, MoveSpecificTarget + Stop Management Helpers
// V12 Trailing Module (Extracted)
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

                    if (!pos.EntryFilled || pos.RemainingContracts <= 0)
                        continue;

                    MoveStop_SinglePosition(entryName, pos, offsetPoints, lastKnownPrice);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR MoveStopsToBreakevenWithOffset: " + ex.Message);
            }
        }

        /// <summary>
        /// [Phase7-M2-A] Helper: Processes single position breakeven logic.
        /// Handles Master/Follower routing and ARM GUARD logic (V12.12).
        /// Zero new heap allocations (hot-path critical).
        /// </summary>
        private void MoveStop_SinglePosition(
            string entryName,
            PositionInfo pos,
            double offsetPoints,
            double lastKnownPrice
        )
        {
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
                bool isBetterF =
                    (pos.Direction == MarketPosition.Long && newStopPrice > pos.CurrentStopPrice)
                    || (pos.Direction == MarketPosition.Short && newStopPrice < pos.CurrentStopPrice);
                if (isBetterF)
                {
                    UpdateStopOrder(entryName, pos, newStopPrice, 1);
                    pos.ManualBreakevenTriggered = true;
                    MarkStickyDirty();
                    Print(
                        string.Format(
                            "BE+{0} MOVED (follower): {1} Stop -> {2:F2}",
                            offsetPoints,
                            entryName,
                            newStopPrice
                        )
                    );
                }
                return;
            }

            // [V12.12] ARM GUARD: If price hasn't cleared the BE threshold yet, arm instead of executing.
            // ManageTrailingStops() will call UpdateStopOrder when price crosses the threshold.
            if (lastKnownPrice <= 0)
            {
                Print(string.Format("[BE_ABORT] {0}: Price data stale (0). Waiting for next tick.", entryName));
                return;
            }
            double referencePrice = lastKnownPrice;
            bool priceCleared =
                pos.Direction == MarketPosition.Long ? referencePrice >= newStopPrice : referencePrice <= newStopPrice;

            if (!priceCleared)
            {
                pos.ManualBreakevenArmed = true;
                pos.ManualBreakevenTriggered = false;
                Print(
                    string.Format(
                        "[V12] BE Armed: {0} Price has not reached threshold. Shielding entry once cleared.",
                        entryName
                    )
                );
                return;
            }

            // Only move stop if it's a better price (profit-protecting direction)
            bool isBetter =
                (pos.Direction == MarketPosition.Long && newStopPrice > pos.CurrentStopPrice)
                || (pos.Direction == MarketPosition.Short && newStopPrice < pos.CurrentStopPrice);

            if (!isBetter)
            {
                Print(
                    string.Format(
                        "BE+{0}: Stop already better for {1}. Current={2:F2}, Request={3:F2}",
                        offsetPoints,
                        entryName,
                        pos.CurrentStopPrice,
                        newStopPrice
                    )
                );
                return;
            }

            // V12.10: Use UpdateStopOrder for proper Master/Follower routing
            // (ChangeOrder only works for Master -- followers were silently skipped)
            UpdateStopOrder(entryName, pos, newStopPrice, 1);
            pos.ManualBreakevenTriggered = true;
            MarkStickyDirty(); // Build 1103: Persist breakeven state
            Print(string.Format("BE+{0} MOVED: {1} Stop -> {2:F2}", offsetPoints, entryName, newStopPrice));
        }

        // [Phase7-S5-T05] Helper 1: Validate move target request
        private bool ValidateMoveTargetRequest(int targetNum, out string errorMsg)
        {
            errorMsg = null;

            if (targetNum < 1 || targetNum > 5)
            {
                errorMsg = $"[V14] MoveSpecificTarget: Invalid target number {targetNum}";
                return false;
            }

            if (activePositions == null || activePositions.Count == 0)
            {
                errorMsg = $"[V14] MoveSpecificTarget: No active positions to move target T{targetNum}";
                return false;
            }

            return true;
        }

        // [Phase7-S5-T05] Helper 2: Find target order for position
        private Order FindTargetOrderForPosition(
            PositionInfo pos,
            string entryName,
            int targetNum,
            out string notFoundReason
        )
        {
            notFoundReason = null;

            if (!pos.EntryFilled)
            {
                notFoundReason = $"[V14] MoveSpecificTarget T{targetNum}: Skipping {entryName} - entry not filled";
                return null;
            }

            // [1102Z-F]: Search the correct account -- follower orders live on their own account,
            // not on the Master account from which Account.Orders is sourced.
            string targetOrderName = $"T{targetNum}_{entryName}";
            var searchAcct = (pos.IsFollower && pos.ExecutingAccount != null) ? pos.ExecutingAccount : Account;

            foreach (Order order in searchAcct.Orders)
            {
                if (
                    order != null
                    && order.Name == targetOrderName
                    && order.Instrument.FullName == Instrument.FullName
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
                )
                {
                    return order;
                }
            }

            notFoundReason =
                $"[V14] MoveSpecificTarget T{targetNum}: No working order found for {entryName} (may already be filled)";
            return null;
        }

        // [Phase7-S5-T05] Helper 3: Calculate and validate new target price
        private bool CalculateAndValidateNewTargetPrice(
            PositionInfo pos,
            double profitPoints,
            int targetNum,
            out double newTargetPrice,
            out string rejectionReason
        )
        {
            rejectionReason = null;
            double entryPrice = pos.EntryPrice;

            // Calculate new target price: Entry Price + Profit Points
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

            // Validate direction safety
            if (pos.Direction == MarketPosition.Long)
            {
                // Long: Target should be above entry, but below or at market is OK (just fills immediately)
                if (newTargetPrice < entryPrice)
                {
                    rejectionReason =
                        $"[V14] MoveSpecificTarget T{targetNum}: REJECTED - Long target {newTargetPrice:F2} below entry {entryPrice:F2}";
                    return false;
                }
            }
            else // Short
            {
                // Short: Target should be below entry
                if (newTargetPrice > entryPrice)
                {
                    rejectionReason =
                        $"[V14] MoveSpecificTarget T{targetNum}: REJECTED - Short target {newTargetPrice:F2} above entry {entryPrice:F2}";
                    return false;
                }
            }

            return true;
        }

        // [Phase7-S5-T05] Helper 4: Execute follower target move via FSM
        private void ExecuteFollowerTargetMove(
            PositionInfo pos,
            string entryName,
            int targetNum,
            Order targetOrder,
            double newTargetPrice
        )
        {
            // B957/C1: Two-phase FSM for follower target replacement (banned Cancel+Submit replaced).
            // Record spec in _followerTargetReplaceSpecs, cancel only -- submission deferred to
            // broker cancel confirmation in OnAccountOrderUpdate / SubmitFollowerTargetReplacement().
            OrderAction exitAct = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

            string targetOrderName = $"T{targetNum}_{entryName}";
            var tSpec = new FollowerTargetReplaceSpec
            {
                EntryName = entryName,
                TargetNum = targetNum,
                NewTargetPrice = newTargetPrice,
                Quantity = targetOrder.Quantity,
                ExitAction = exitAct,
                TargetAccount = pos.ExecutingAccount,
                CancellingOrderId = targetOrder.OrderId,
            };

            _followerTargetReplaceSpecs[targetOrderName] = tSpec;
            // A1-2: Stamp REAPER grace window before cancel to suppress false desync during replace gap.
            StampReaperMoveGrace();
            pos.ExecutingAccount.Cancel(new[] { targetOrder });

            double profitFromEntry = Math.Abs(newTargetPrice - pos.EntryPrice);
            Print(
                $"[SIMA] MoveSpecificTarget T{targetNum}: Follower {entryName} on {pos.ExecutingAccount.Name} -> FSM PendingCancel -> {newTargetPrice:F2} (+{profitFromEntry:F2})"
            );
        }

        // [Phase7-S5-T05] Helper 5: Execute master target move via ChangeOrder
        private void ExecuteMasterTargetMove(
            PositionInfo pos,
            string entryName,
            int targetNum,
            Order targetOrder,
            double newTargetPrice
        )
        {
            // Master path -- ChangeOrder is fine for NinjaScript-managed orders
            ChangeOrder(targetOrder, targetOrder.Quantity, newTargetPrice, 0);

            double profitFromEntry = Math.Abs(newTargetPrice - pos.EntryPrice);
            Print(
                $"[V14] MoveSpecificTarget T{targetNum}: {entryName} -> {newTargetPrice:F2} (+{profitFromEntry:F2} from entry {pos.EntryPrice:F2})"
            );
        }

        /// <summary>
        /// V14: Moves a specific target to a new profit level (Entry + X points)
        /// [Phase7-S5-T05] Refactored: CYC 37->8, extracted 5 helpers
        /// </summary>
        /// <param name="targetNum">Target number (1-5)</param>
        /// <param name="profitPoints">Points of profit from entry (1.0 or 2.0)</param>
        private void MoveSpecificTarget(int targetNum, double profitPoints)
        {
            // Step 1: Validate request
            if (!ValidateMoveTargetRequest(targetNum, out string errorMsg))
            {
                Print(errorMsg);
                return;
            }

            int movedCount = 0;

            // Step 2: Iterate through all active positions
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key))
                    continue;

                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;

                // Step 3: Find target order
                Order targetOrder = FindTargetOrderForPosition(pos, entryName, targetNum, out string notFoundReason);
                if (targetOrder == null)
                {
                    if (notFoundReason != null)
                        Print(notFoundReason);
                    continue;
                }

                // Step 4: Calculate and validate new price
                if (
                    !CalculateAndValidateNewTargetPrice(
                        pos,
                        profitPoints,
                        targetNum,
                        out double newTargetPrice,
                        out string rejectionReason
                    )
                )
                {
                    if (rejectionReason != null)
                        Print(rejectionReason);
                    continue;
                }

                // Step 5: Execute move (follower FSM vs master ChangeOrder)
                try
                {
                    if (pos.IsFollower && pos.ExecutingAccount != null)
                    {
                        ExecuteFollowerTargetMove(pos, entryName, targetNum, targetOrder, newTargetPrice);
                    }
                    else
                    {
                        ExecuteMasterTargetMove(pos, entryName, targetNum, targetOrder, newTargetPrice);
                    }
                    movedCount++;
                }
                catch (Exception ex)
                {
                    Print($"[V14] MoveSpecificTarget T{targetNum}: Move FAILED for {entryName} - {ex.Message}");
                }
            }

            // Step 6: Summary reporting
            if (movedCount > 0)
            {
                Print(
                    $"[V14] MoveSpecificTarget T{targetNum}: Moved {movedCount} target(s) to +{profitPoints}pt profit"
                );
            }
            else
            {
                Print($"[V14] MoveSpecificTarget T{targetNum}: No targets were moved (no active working orders found)");
            }
        }

        /// <summary>
        /// [Phase7-S5-T11] Helper 1: Validates request to move target to absolute price.
        /// </summary>
        private bool ValidateTargetMoveAbsoluteRequest(int targetNum, double absolutePrice)
        {
            if (targetNum < 1 || targetNum > 5)
            {
                return false;
            }

            if (absolutePrice <= 0)
            {
                return false;
            }

            if (activePositions == null || activePositions.Count == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// [Phase7-S5-T11] Helper 2: Finds the working target order for absolute price move.
        /// </summary>
        private Order FindTargetOrderForAbsoluteMove(
            PositionInfo pos,
            string entryName,
            int targetNum,
            out Account searchAcct
        )
        {
            string targetOrderName = string.Format("T{0}_{1}", targetNum, entryName);
            searchAcct = (pos.IsFollower && pos.ExecutingAccount != null) ? pos.ExecutingAccount : Account;

            foreach (Order order in searchAcct.Orders)
            {
                if (
                    order != null
                    && order.Name == targetOrderName
                    && order.Instrument.FullName == Instrument.FullName
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
                )
                {
                    return order;
                }
            }

            return null;
        }

        /// <summary>
        /// [Phase7-S5-T11] Helper 3: Executes the absolute price move with direction validation.
        /// </summary>
        private bool ExecuteTargetAbsoluteMove(
            PositionInfo pos,
            Order targetOrder,
            int targetNum,
            double absolutePrice,
            string entryName,
            Account searchAcct
        )
        {
            double newPrice = Instrument.MasterInstrument.RoundToTickSize(absolutePrice);

            // Direction safety validation
            if (pos.Direction == MarketPosition.Long && newPrice <= pos.EntryPrice)
            {
                Print(
                    string.Format(
                        "[V12] SET_TARGET_PRICE T{0}: REJECTED -- Long target {1:F2} at/below entry {2:F2}",
                        targetNum,
                        newPrice,
                        pos.EntryPrice
                    )
                );
                return false;
            }

            if (pos.Direction == MarketPosition.Short && newPrice >= pos.EntryPrice)
            {
                Print(
                    string.Format(
                        "[V12] SET_TARGET_PRICE T{0}: REJECTED -- Short target {1:F2} at/above entry {2:F2}",
                        targetNum,
                        newPrice,
                        pos.EntryPrice
                    )
                );
                return false;
            }

            try
            {
                if (pos.IsFollower && pos.ExecutingAccount != null)
                {
                    // Follower: Two-phase FSM (DNA-compliant, no raw Cancel+Submit)
                    OrderAction exitAct =
                        pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                    string targetOrderName = string.Format("T{0}_{1}", targetNum, entryName);
                    var tSpec = new FollowerTargetReplaceSpec
                    {
                        EntryName = entryName,
                        TargetNum = targetNum,
                        NewTargetPrice = newPrice,
                        Quantity = targetOrder.Quantity,
                        ExitAction = exitAct,
                        TargetAccount = pos.ExecutingAccount,
                        CancellingOrderId = targetOrder.OrderId,
                    };
                    _followerTargetReplaceSpecs[targetOrderName] = tSpec;
                    StampReaperMoveGrace();
                    pos.ExecutingAccount.Cancel(new[] { targetOrder });
                    Print(
                        string.Format(
                            "[V12] SET_TARGET_PRICE T{0}: Follower FSM queued on {1} -> {2:F2}",
                            targetNum,
                            pos.ExecutingAccount.Name,
                            newPrice
                        )
                    );
                }
                else
                {
                    // Master: ChangeOrder for atomic in-place modification
                    ChangeOrder(targetOrder, targetOrder.Quantity, newPrice, 0);
                    Print(
                        string.Format("[V12] SET_TARGET_PRICE T{0}: Master ChangeOrder -> {1:F2}", targetNum, newPrice)
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                Print(string.Format("[V12] SET_TARGET_PRICE T{0} error: {1}", targetNum, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Build 1107: Moves a specific target to an absolute price (from live control center).
        /// [Phase7-S5-T11] Refactored: CYC 28->6, extracted 3 helpers
        /// Mirrors MoveSpecificTarget structure: finds working order on correct account,
        /// validates direction safety, uses ChangeOrder for master and FSM for follower.
        /// </summary>
        private void MoveSpecificTargetAbsolute(int targetNum, double absolutePrice)
        {
            // Step 1: Validate request
            if (!ValidateTargetMoveAbsoluteRequest(targetNum, absolutePrice))
            {
                return;
            }

            // Step 2: Iterate through all active positions
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key))
                    continue;

                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;

                if (!pos.EntryFilled || pos.PendingCleanup)
                    continue;

                // Step 3: Find target order
                Account searchAcct;
                Order targetOrder = FindTargetOrderForAbsoluteMove(pos, entryName, targetNum, out searchAcct);

                if (targetOrder == null)
                {
                    Print(string.Format("[V12] SET_TARGET_PRICE T{0}: No working order for {1}", targetNum, entryName));
                    continue;
                }

                // Step 4: Execute move with direction validation
                ExecuteTargetAbsoluteMove(pos, targetOrder, targetNum, absolutePrice, entryName, searchAcct);
            }
        }

        #endregion
    }
}
