// Build 971: Orders.Management.Cleanup -- CleanupPosition, RemoveGhostOrderRef, ReconcileOrphanedOrders
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
        #region Orders Management Cleanup

        private void CleanupPosition(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return;
            try
            {
                PositionInfo cleanupPosRef;
                activePositions.TryGetValue(entryName, out cleanupPosRef);
                
                var (cancelledStops, cancelledTargets, cancelledEntries) = CancelAllOrdersForEntry(entryName, cleanupPosRef);

                if (pendingStopReplacements.TryRemove(entryName, out _)) Interlocked.Decrement(ref pendingReplacementCount);

                if (cancelledStops > 0 || cancelledTargets > 0 || cancelledEntries > 0)
                    Print(string.Format("CLEANUP SUMMARY for {0}: Stops={1} Targets={2} Entries={3}",
                        entryName, cancelledStops, cancelledTargets, cancelledEntries));

                if (EvaluateFollowerRepairBlock(entryName))
                    return;

                int followerExpected = 0;
                if (activePositions.TryGetValue(entryName, out var metaCheck)
                    && metaCheck.IsFollower
                    && metaCheck.ExecutingAccount != null)
                {
                    expectedPositions.TryGetValue(ExpKey(metaCheck.ExecutingAccount.Name), out followerExpected);
                }
                
                PurgePositionIfEligible(entryName, followerExpected);
            }
            finally
            {
                FollowerBracketFSM removedFsm;
                if (TryTerminateFollowerBracket(entryName, out removedFsm) && removedFsm != null)
                {
                    Print(string.Format("[FSM-C1] Terminated FSM for {0} (was {1})", entryName, removedFsm.State));
                }
            }
        }

        /// <summary>
        /// Cancel all orders (stops, targets T1-T5, entries) for the specified entry name.
        /// Returns cancellation counts for summary logging.
        /// </summary>
        private (int cancelledStops, int cancelledTargets, int cancelledEntries) CancelAllOrdersForEntry(string entryName, PositionInfo cleanupPosRef)
        {
            int cancelledStops = 0;
            int cancelledTargets = 0;
            int cancelledEntries = 0;

            // Stop: TryGetValue only; remove only if terminal; otherwise cancel and keep ref
            if (stopOrders.TryGetValue(entryName, out var stopOrder))
            {
                if (stopOrder != null)
                {
                    if (IsOrderTerminal(stopOrder.OrderState))
                        stopOrders.TryRemove(entryName, out _);
                    else
                    {
                        CancelOrderSafe(stopOrder, cleanupPosRef);
                        cancelledStops++;
                    }
                }
                else
                    stopOrders.TryRemove(entryName, out _);
            }

            // T1-T5: TryGetValue only; remove only if terminal; otherwise cancel and keep ref
            for (int tNum = 1; tNum <= 5; tNum++)
            {
                var tDict = GetTargetOrdersDictionary(tNum);
                if (tDict == null) continue;

                if (tDict.TryGetValue(entryName, out var tOrder))
                {
                    if (tOrder != null)
                    {
                        if (IsOrderTerminal(tOrder.OrderState))
                            tDict.TryRemove(entryName, out _);
                        else
                        {
                            CancelOrderSafe(tOrder, cleanupPosRef);
                            cancelledTargets++;
                        }
                    }
                    else
                    {
                        tDict.TryRemove(entryName, out _);
                    }
                }
            }

            // Entry: TryGetValue only; remove only if terminal; otherwise cancel and keep ref
            if (entryOrders.TryGetValue(entryName, out var eOrder))
            {
                if (eOrder != null)
                {
                    if (IsOrderTerminal(eOrder.OrderState))
                    {
                        entryOrders.TryRemove(entryName, out _);
                        _citNudgedKeys.TryRemove(entryName, out _);
                    }
                    else
                    {
                        CancelOrderSafe(eOrder, cleanupPosRef);
                        cancelledEntries++;
                    }
                }
                else
                {
                    entryOrders.TryRemove(entryName, out _);
                    _citNudgedKeys.TryRemove(entryName, out _);
                }
            }

            return (cancelledStops, cancelledTargets, cancelledEntries);
        }

        /// <summary>
        /// Evaluate if META-GUARD blocks position purge due to active Reaper repair.
        /// Returns true if purge should be blocked (expectedPositions != 0 for follower).
        /// </summary>
        private bool EvaluateFollowerRepairBlock(string entryName)
        {
            if (activePositions.TryGetValue(entryName, out var metaGuardCheck)
                && metaGuardCheck.IsFollower
                && metaGuardCheck.ExecutingAccount != null)
            {
                string followerAcctName = metaGuardCheck.ExecutingAccount.Name;
                int followerExpected = 0;
                expectedPositions.TryGetValue(ExpKey(followerAcctName), out followerExpected);
                if (followerExpected != 0)
                {
                    Print(string.Format("[META-GUARD] {0}: Broker is flat but expectedPositions={1}. " +
                        "Retaining activePositions metadata for Repair Hook. Will purge after repair completes.",
                        entryName, followerExpected));
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Purge activePositions entry if no active/pending orders remain and META-GUARD allows.
        /// Includes FIX-ZP-02 secondary follower purge for broker-confirmed flat positions.
        /// </summary>
        private void PurgePositionIfEligible(string entryName, int followerExpected)
        {
            // V12.1101E [DESYNC-01]: Defer activePositions removal until no dict holds an active/pending order.
            // V12.Phase8.2 [META-GUARD]: Skip purge if Reaper Repair Hook is active (followerExpected != 0).
            if (followerExpected == 0 && !HasActiveOrPendingOrderForEntry(entryName))
            {
                bool removed;
                removed = activePositions.TryRemove(entryName, out _);
                if (removed) SymmetryGuardForgetEntry(entryName);
            }

            // [FIX-ZP-02]: Secondary safety net for SIMA followers -- force purge if broker confirms flat.
            // Guards against lingering non-terminal dict entries preventing HasActiveOrPendingOrderForEntry
            // from returning false even though the actual broker position is already flat.
            if (followerExpected == 0
                && activePositions.TryGetValue(entryName, out var followerCheck)
                && followerCheck.IsFollower
                && followerCheck.ExecutingAccount != null)
            {
                var brokerPos = followerCheck.ExecutingAccount.Positions
                    .FirstOrDefault(p => p.Instrument == Instrument);
                if (brokerPos != null && brokerPos.MarketPosition == MarketPosition.Flat)
                {
                    bool removedFZP;
                    removedFZP = activePositions.TryRemove(entryName, out _);
                    if (removedFZP)
                    {
                        SymmetryGuardForgetEntry(entryName);
                        Print(string.Format("[FIXED_G] Purging {0} - confirmed flat by broker.", entryName));
                    }
                }
            }
        }

        /// <summary>
        /// V12.12: Remove any ghost order reference (targets, stops, entries) when it reaches a terminal state.
        /// This only clears stale references; it does not alter stop quantities or position state.
        /// </summary>
        /// <summary>
        /// V12.12: Remove any ghost order reference (targets, stops, entries) when it reaches a terminal state.
        /// This only clears stale references; it does not alter stop quantities or position state.
        /// Phase 7 refactored: Dispatcher pattern with 3 sub-methods for complexity reduction (37 CYC -> 5 CYC).
        /// </summary>
        private void RemoveGhostOrderRef(Order order, string reason)
        {
            if (order == null) return;

            var (foundInDict, removedLabel, removedKey) = ScanAndRemoveGhostReferences(order, reason);

            if (foundInDict && !string.IsNullOrEmpty(removedKey))
            {
                EvaluateZombiePurgeEligibility(removedKey);
            }

            if (!foundInDict)
            {
                ClassifyOrphanReason(order, reason);
            }
        }

        /// <summary>
        /// Scan all order dictionaries for ghost references matching the order.
        /// Uses dual-match logic (reference equality OR OrderId match).
        /// Includes position protection audit if a STOP is removed.
        /// </summary>
        private (bool foundInDict, string removedLabel, string removedKey) ScanAndRemoveGhostReferences(Order order, string reason)
        {
            var orderDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
            {
                (target1Orders, "T1"),
                (target2Orders, "T2"),
                (target3Orders, "T3"),
                (target4Orders, "T4"),
                (target5Orders, "T5"),
                (stopOrders, "STOP"),
                (entryOrders, "ENTRY"),
            };

            bool foundInDict = false;
            string removedLabel = null;
            string removedKey = null;

            foreach (var (dict, label) in orderDicts)
            {
                foreach (var kvp in dict.ToArray())
                {
                    if (kvp.Value == order ||
                        (kvp.Value != null && order != null && kvp.Value.OrderId == order.OrderId))
                    {
                        bool ghostRemoved;
                        ghostRemoved = dict.TryRemove(kvp.Key, out _);
                        if (ghostRemoved)
                        {
                            string matchType = (kvp.Value == order) ? "REF" : "ORDERID";
                            Print(string.Format("[GHOST_FIX] Order {0}_{1} terminated ({2}). Nullifying reference. (match={3}, OrderId={4})",
                                label, kvp.Key, reason, matchType, order.OrderId ?? "NULL"));
                            foundInDict = true;
                            removedLabel = label;
                            removedKey = kvp.Key;
                        }
                    }
                }
            }

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

            return (foundInDict, removedLabel, removedKey);
        }

        /// <summary>
        /// Evaluate if a position can be purged from activePositions after terminal order removal.
        /// Guards against purging positions with open contracts.
        /// Implements META-GUARD for follower repair scenarios.
        /// </summary>
        private void EvaluateZombiePurgeEligibility(string removedKey)
        {
            if (!HasActiveOrPendingOrderForEntry(removedKey))
            {
                if (activePositions.TryGetValue(removedKey, out var purgeCheck) && purgeCheck.RemainingContracts > 0)
                    return;

                if (activePositions.TryGetValue(removedKey, out var ghostMetaCheck)
                    && ghostMetaCheck.IsFollower
                    && ghostMetaCheck.ExecutingAccount != null)
                {
                    string ghostAcctName = ghostMetaCheck.ExecutingAccount.Name;
                    int ghostExpected = 0;
                    expectedPositions.TryGetValue(ExpKey(ghostAcctName), out ghostExpected);
                    if (ghostExpected != 0)
                    {
                        Print(string.Format("[META-GUARD] {0}: ZOMBIE_PURGE suppressed -- expectedPositions={1} on {2}. " +
                            "Retaining metadata for Repair Hook.",
                            removedKey, ghostExpected, ghostAcctName));
                        return;
                    }
                }

                bool zombieRemoved;
                zombieRemoved = activePositions.TryRemove(removedKey, out _);
                if (zombieRemoved)
                {
                    SymmetryGuardForgetEntry(removedKey);
                    Print(string.Format("[ZOMBIE_PURGE] {0}: all order refs terminal. Purging activePositions.", removedKey));
                }
            }
        }

        /// <summary>
        /// Classify why an order was not found in dictionaries.
        /// Distinguishes expected cascade from suspicious orphan.
        /// </summary>
        private void ClassifyOrphanReason(Order order, string reason)
        {
            if (order.Name.Contains("RMA") || order.Name.Contains("OR") || order.Name.Contains("MOMO") || order.Name.Contains("TREND") ||
                order.Name.Contains("Stop_") || order.Name.Contains("Tgt_") || order.Name.Contains("Fleet_"))
            {
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

        private bool ValidateOrphanedMasterOrders(string reason)
        {
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
                    name.StartsWith("T3_") || name.StartsWith("T4_") || name.StartsWith("T5_") ||
                    name.StartsWith("Flatten_") || name.StartsWith("Trim_"))
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
                        CancelOrderOnAccount(order, order.Account);
                        foundOrphans = true;
                    }
                }
            }
            return foundOrphans;
        }

        private HashSet<string> BuildLiveBrokerOrderIndex()
        {
            HashSet<string> liveBrokerOrderIds = new HashSet<string>();
            foreach (Order brokerOrder in Account.Orders)
            {
                if (brokerOrder != null && !string.IsNullOrEmpty(brokerOrder.OrderId) &&
                    (brokerOrder.OrderState == OrderState.Working || brokerOrder.OrderState == OrderState.Accepted))
                {
                    liveBrokerOrderIds.Add(brokerOrder.OrderId);
                }
            }

            // Also scan fleet accounts if SIMA is enabled
            if (EnableSIMA)
            {
                foreach (Account acct in Account.All)
                {
                    if (IsFleetAccount(acct))
                    {
                        foreach (Order fleetOrder in acct.Orders)
                        {
                            if (fleetOrder != null && !string.IsNullOrEmpty(fleetOrder.OrderId) &&
                                (fleetOrder.OrderState == OrderState.Working || fleetOrder.OrderState == OrderState.Accepted))
                            {
                                liveBrokerOrderIds.Add(fleetOrder.OrderId);
                            }
                        }
                    }
                }
            }
            return liveBrokerOrderIds;
        }

        private int PurgeGhostOrderReferences(string reason, HashSet<string> liveBrokerOrderIds)
        {
            int reverseGhosts = 0;
            var reverseCheckDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
            {
                (stopOrders, "STOP"), (target1Orders, "T1"), (target2Orders, "T2"),
                (target3Orders, "T3"), (target4Orders, "T4"), (target5Orders, "T5"), (entryOrders, "ENTRY"),
            };

            foreach (var (dict, label) in reverseCheckDicts)
            {
                foreach (var kvp in dict.ToArray())
                {
                    Order trackedOrder = kvp.Value;
                    if (trackedOrder == null) continue;

                    // Only audit orders that SHOULD be alive (Working/Accepted)
                    // Terminal orders are cleaned by OnOrderUpdate; this catches leaks
                    bool isTerminal = (trackedOrder.OrderState == OrderState.Cancelled ||
                                       trackedOrder.OrderState == OrderState.Rejected ||
                                       trackedOrder.OrderState == OrderState.Filled ||
                                       trackedOrder.OrderState == OrderState.Unknown);

                    bool notInBroker = !string.IsNullOrEmpty(trackedOrder.OrderId) &&
                                       !liveBrokerOrderIds.Contains(trackedOrder.OrderId);

                    if (isTerminal || notInBroker)
                    {
                        bool reverseRemoved;
                        reverseRemoved = dict.TryRemove(kvp.Key, out _);
                        if (reverseRemoved)
                        {
                            string state = trackedOrder.OrderState.ToString();
                            Print(string.Format("[GHOST_FIX] REVERSE AUDIT: {0} ghost for {1} purged (State={2}, InBroker={3}, OrderId={4})",
                                label, kvp.Key, state, !notInBroker, trackedOrder.OrderId ?? "NULL"));
                            reverseGhosts++;
                        }
                    }
                }
            }
            return reverseGhosts;
        }

        private void ReconcileOrphanedOrders(string reason)
        {
            try
            {
                if (Account == null) return;

                Print(string.Format("[GHOST_FIX] REVERSE AUDIT START ({0})", reason));

                bool foundOrphans = ValidateOrphanedMasterOrders(reason);
                HashSet<string> liveBrokerOrderIds = BuildLiveBrokerOrderIndex();
                int reverseGhosts = PurgeGhostOrderReferences(reason, liveBrokerOrderIds);

                Print(string.Format("[GHOST_FIX] REVERSE AUDIT COMPLETE: {0} ghosts purged", reverseGhosts));

                if (foundOrphans || reverseGhosts > 0)
                    Print("Orphaned order reconciliation complete.");
            }
            catch (Exception ex)
            {
                Print("ERROR ReconcileOrphanedOrders: " + ex.Message);
            }
        }


        #endregion
    }
}
