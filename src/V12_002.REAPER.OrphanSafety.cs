// <copyright file="V12_002.REAPER.OrphanSafety.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12 REAPER Orphan FSM Detection & Self-Heal Module
// Build 1111.007-reaper-t2: Extracted from REAPER.Audit.cs and REAPER.Repair.cs
// Jane Street Alignment: Atomic state transitions, wait-free progress, bounded latency
using System;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Orphan FSM Detection & Self-Heal

        // Orphan FSM grace tracking (key = FSM entry name)
        private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen =
            new ConcurrentDictionary<string, DateTime>();

        // Orphan repair attempt counter (key = account name)
        private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount =
            new ConcurrentDictionary<string, int>();

        /// <summary>
        /// Detects orphaned FSM positions (broker flat but activePositions entry exists) after 10s grace.
        /// Diagnostic only - logs warning but does NOT trigger flatten (non-fatal assertion).
        /// Jane Street Alignment: Atomic state transitions via GetOrAdd (TOCTOU-safe).
        /// </summary>
        /// <param name="entryName">FSM entry name (key in activePositions).</param>
        /// <param name="accountName">Account name.</param>
        /// <param name="actualQty">Broker position quantity (should be 0 for orphan detection).</param>
        /// <param name="activePositions">Position info dictionary.</param>
        /// <returns>True if orphan detected and logged, false if grace active or position live.</returns>
        private bool DetectOrphanFSM(
            string entryName,
            string accountName,
            int actualQty,
            ConcurrentDictionary<string, PositionInfo> activePositions
        )
        {
            // Only detect orphans when broker is flat AND activePositions entry exists
            if (actualQty != 0 || !activePositions.ContainsKey(entryName))
            {
                // Position is live or activePositions is clean -- clear first-seen timestamp
                _orphanedPositionFirstSeen.TryRemove(entryName, out _);
                return false;
            }

            // Check if grace period has expired (10 seconds)
            DateTime firstSeen = _orphanedPositionFirstSeen.GetOrAdd(entryName, DateTime.UtcNow);
            double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;

            if (graceElapsed > 10.0)
            {
                // Grace expired -- log diagnostic warning
                Print(
                    string.Format(
                        "[REAPER][DIAGNOSTIC] Orphaned FSM position detected: {0} entry={1}. "
                            + "Broker flat but activePositions entry exists after {2:F1}s grace. "
                            + "This may indicate a TOCTOU race in entry rollback logic.",
                        accountName,
                        entryName,
                        graceElapsed
                    )
                );

                // Clear first-seen timestamp to avoid log spam
                _orphanedPositionFirstSeen.TryRemove(entryName, out _);
                return true;
            }

            return false; // Grace active
        }

        /// <summary>
        /// Validates repair eligibility with orphan self-heal logic.
        /// Increments orphan counter on PositionInfo lookup failure.
        /// Triggers force-zero self-heal after 3 failed attempts.
        /// Jane Street Alignment: Atomic state transitions via AddOrUpdate (CAS operation).
        /// </summary>
        /// <param name="accountName">Account name.</param>
        /// <param name="activePositions">Position info dictionary.</param>
        /// <param name="repairPos">Output: PositionInfo if found.</param>
        /// <param name="repairEntryName">Output: Entry name if found.</param>
        /// <returns>True if PositionInfo found (repair can proceed), false if orphaned.</returns>
        private bool ValidateRepairEligibility_OrphanCheck(
            string accountName,
            ConcurrentDictionary<string, PositionInfo> activePositions,
            out PositionInfo repairPos,
            out string repairEntryName
        )
        {
            repairPos = null;
            repairEntryName = null;

            // 1. Find the stored PositionInfo for this account in activePositions
            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pi = kvp.Value;
                if (pi.IsFollower && pi.ExecutingAccount != null && pi.ExecutingAccount.Name == accountName)
                {
                    repairPos = pi;
                    repairEntryName = kvp.Key;
                    break;
                }
            }

            if (repairPos == null)
            {
                // Orphan detected - increment counter
                int orphanCount = _reaperOrphanRepairCount.AddOrUpdate(accountName, 1, (k, v) => v + 1);
                Print(
                    string.Format(
                        "[REAPER REPAIR] x No PositionInfo found for {0} -- cannot repair. (orphan attempt {1}/3)",
                        accountName,
                        orphanCount
                    )
                );

                if (orphanCount >= 3)
                {
                    // Threshold reached - trigger self-heal
                    ExecuteOrphanSelfHeal(accountName);
                }
                return false;
            }

            // Clear orphan counter on successful PositionInfo resolution
            _reaperOrphanRepairCount.TryRemove(accountName, out _);
            return true;
        }

        /// <summary>
        /// Executes orphan self-heal: force-zeros expectedPositions and resets orphan counter.
        /// SCOPE: Account-level (clears expected position for entire account, not per-FSM).
        /// Jane Street Alignment: Atomic state transition via SetExpectedPositionLocked.
        /// </summary>
        /// <param name="accountName">Account name.</param>
        /// <remarks>
        /// SCOPE BLAST: This method clears the expected position for the ENTIRE ACCOUNT,
        /// not just the orphaned FSM entry. This is INTENTIONAL. Rationale:
        /// 1. Orphan state indicates severe desync (broker flat but FSM active)
        /// 2. 3-attempt threshold ensures this is rare (only after repeated self-heal failures)
        /// 3. Account-level force-zero is the safest recovery path to prevent ghost positions
        /// 4. Alternative (per-entry clear) risks leaving partial state that could cause
        ///    future desyncs or order submission errors
        /// This is a nuclear option triggered only when all other repair attempts have failed.
        /// </remarks>
        private void ExecuteOrphanSelfHeal(string accountName)
        {
            Print(
                string.Format(
                    "[REAPER] SELF-HEAL: {0} has no PositionInfo after 3 attempts. Force-zeroing expectedPositions to unblock repair loop.",
                    accountName
                )
            );

            // SetExpectedPositionLocked(..., 0) already removes from _dispatchSyncPendingExpKeys internally.
            SetExpectedPositionLocked(ExpKey(accountName), 0);

            // Reset orphan counter
            _reaperOrphanRepairCount.TryRemove(accountName, out _);
        }

        #endregion
    }
}

// Made with Bob
