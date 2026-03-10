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
            if (!activePositions.ContainsKey(entryName)) return;

            int cancelledStops = 0;
            int cancelledTargets = 0;
            int cancelledEntries = 0;

            // Build 1102U [BUG-2a]: Fleet followers must use Account.Cancel() -- not CancelOrder() which only
            // works for orders submitted through this strategy instance's NinjaScript order management.
            // Follower orders are submitted via acct.Submit(), so they require the broker-level cancel API.
            bool isFollowerForCleanup = activePositions.TryGetValue(entryName, out var cleanupPosRef)
                && cleanupPosRef.IsFollower && cleanupPosRef.ExecutingAccount != null;

            // Stop: TryGetValue only; remove only if terminal; otherwise cancel and keep ref
            if (stopOrders.TryGetValue(entryName, out var stopOrder))
            {
                if (stopOrder != null)
                {
                    if (IsOrderTerminal(stopOrder.OrderState))
                        stopOrders.TryRemove(entryName, out _);
                    else
                    {
                        if (isFollowerForCleanup)
                            cleanupPosRef.ExecutingAccount.Cancel(new[] { stopOrder });
                        else
                            CancelOrder(stopOrder);
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
                            if (isFollowerForCleanup)
                                cleanupPosRef.ExecutingAccount.Cancel(new[] { tOrder });
                            else
                                CancelOrder(tOrder);
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
                        if (isFollowerForCleanup)
                            cleanupPosRef.ExecutingAccount.Cancel(new[] { eOrder });
                        else
                            CancelOrder(eOrder);
                        cancelledEntries++;
                    }
                }
                else
                {
                    entryOrders.TryRemove(entryName, out _);
                    _citNudgedKeys.TryRemove(entryName, out _);
                }
            }

            if (pendingStopReplacements.TryRemove(entryName, out _)) Interlocked.Decrement(ref pendingReplacementCount);

            if (cancelledStops > 0 || cancelledTargets > 0 || cancelledEntries > 0)
                Print(string.Format("CLEANUP SUMMARY for {0}: Stops={1} Targets={2} Entries={3}",
                    entryName, cancelledStops, cancelledTargets, cancelledEntries));

            // V12.Phase8.2 [META-GUARD]: Pre-compute followerExpected before any purge decision.
            // If the Reaper has a non-zero expectedPositions for this account, a Repair Hook is planning
            // to re-issue the entry. Purging now would destroy the PositionInfo metadata
            // (price/qty/direction) that the Repair Hook reads to reconstruct the order.
            int followerExpected = 0;
            if (activePositions.TryGetValue(entryName, out var metaGuardCheck)
                && metaGuardCheck.IsFollower
                && metaGuardCheck.ExecutingAccount != null)
            {
                string followerAcctName = metaGuardCheck.ExecutingAccount.Name;
                // Build 1102U [BUG-1]: Must use composite key to match new ExpKey scheme.
                expectedPositions.TryGetValue(ExpKey(followerAcctName), out followerExpected);
                if (followerExpected != 0)
                {
                    Print(string.Format("[META-GUARD] {0}: Broker is flat but expectedPositions={1}. " +
                        "Retaining activePositions metadata for Repair Hook. Will purge after repair completes.",
                        entryName, followerExpected));
                    // [Phase 8.2 Part 3] Explicit early-return: prevent fall-through into FIX-ZP-02
                    // which would forcibly purge activePositions even when the Repair Hook is pending.
                    return;
                }
            }

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
        private void RemoveGhostOrderRef(Order order, string reason)
        {
            if (order == null) return;

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
                // V12.17: Dual match - reference equality OR OrderId string match
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

            // [FIX-ZP-01]: After any terminal order ref is removed, re-evaluate position purge eligibility.
            // Deliberately NOT calling CleanupPosition here to avoid cancelling live remaining orders
            // (e.g. T2-T5 still working after T1 fills). HasActiveOrPendingOrderForEntry is the safe gate.
            if (foundInDict && !string.IsNullOrEmpty(removedKey))
            {
                if (!HasActiveOrPendingOrderForEntry(removedKey))
                {
                    // [1102G] Guard: Never purge a position that still holds open contracts.
                    if (activePositions.TryGetValue(removedKey, out var purgeCheck) && purgeCheck.RemainingContracts > 0)
                        return;

                    // V12.Phase8.2 [META-GUARD]: If this is a follower with a pending repair,
                    // preserve activePositions metadata so the Repair Hook can reconstruct the order.
                    if (activePositions.TryGetValue(removedKey, out var ghostMetaCheck)
                        && ghostMetaCheck.IsFollower
                        && ghostMetaCheck.ExecutingAccount != null)
                    {
                        string ghostAcctName = ghostMetaCheck.ExecutingAccount.Name;
                        int ghostExpected = 0;
                        // Build 1102U [BUG-1]: Composite key parity -- must match ExpKey scheme.
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
                            CancelOrder(order);
                            foundOrphans = true;
                        }
                    }
                }

                // === V12.18 REVERSE AUDIT: Strategy -> Broker ===
                // For each tracked order ref, verify it still exists as Working/Accepted
                // in the broker's order collection. If it doesn't, it's a ghost -- purge it.
                Print(string.Format("[GHOST_FIX] REVERSE AUDIT START ({0})", reason));
                int reverseGhosts = 0;

                // Build a HashSet of live broker OrderIds for O(1) lookup
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

                // Check all strategy order dictionaries against live broker orders
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
