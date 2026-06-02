// Build 1105: Shadow Mode -- Autonomous follower stop/flatten propagation
// Complements fleet symmetry sync (Trailing.cs) which syncs by trail LEVEL.
// Shadow syncs by stop PRICE and auto-propagates leader flatten.
using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Shadow Engine

        /// <summary>
        /// Core Shadow check -- called from ManageTrailingStops() after fleet sync pass.
        /// Reuses existing UpdateStopOrder/FlattenAllApexAccounts infrastructure.
        /// </summary>
        private void ShadowEngineCheck()
        {
            if (!EnableSIMA || !ShadowModeEnabled)
                return;
            if (_isTerminating || isFlattenRunning)
                return;

            ShadowPropagateStopMoves();
            ShadowPropagateLeaderFlatten();
        }

        /// <summary>
        /// Watches leader stop prices. When a leader stop moves (breakeven, trail, manual),
        /// propagates exact price to all follower FSMs tracking the same entry signal.
        /// Complements fleet symmetry sync which syncs by trail LEVEL (not price).
        /// </summary>
        private void ShadowPropagateStopMoves()
        {
            foreach (var kvp in activePositions.ToArray())
            {
                Order leaderStop;
                if (!ValidateLeaderPosition(kvp.Value, kvp.Key, out leaderStop))
                    continue;

                double lastKnown;
                if (
                    !DetectStopPriceChange(kvp.Key, leaderStop.StopPrice, _leaderLastStopPrice, tickSize, out lastKnown)
                )
                    continue;

                PropagateAndCacheStopPrice(kvp.Key, leaderStop.StopPrice, _leaderLastStopPrice);
            }

            foreach (var cacheKvp in _leaderLastStopPrice.ToArray())
            {
                PositionInfo livePos;
                Order liveStop;
                if (
                    !activePositions.TryGetValue(cacheKvp.Key, out livePos)
                    || livePos == null
                    || livePos.IsFollower
                    || !livePos.EntryFilled
                    || livePos.RemainingContracts <= 0
                    || !stopOrders.TryGetValue(cacheKvp.Key, out liveStop)
                    || liveStop == null
                    || liveStop.StopPrice <= 0
                )
                {
                    _leaderLastStopPrice.TryRemove(cacheKvp.Key, out _);
                }
            }
        }

        /// <summary>
        /// Validates leader position eligibility for stop propagation.
        /// Returns true if position is a filled leader with a valid stop order.
        /// </summary>
        /// <param name="pos">Position to validate</param>
        /// <param name="entryKey">Entry key for stop order lookup</param>
        /// <param name="leaderStop">Output: leader stop order if valid</param>
        /// <returns>True if position is eligible for propagation</returns>
        internal bool ValidateLeaderPosition(PositionInfo pos, string entryKey, out Order leaderStop)
        {
            leaderStop = null;

            if (pos == null || pos.IsFollower)
            {
                Print("[SHADOW] Validation: Position is null or follower");
                return false;
            }
            if (!pos.EntryFilled || pos.RemainingContracts <= 0)
            {
                Print("[SHADOW] Validation: Position not filled or no contracts");
                return false;
            }

            if (!stopOrders.TryGetValue(entryKey, out leaderStop))
            {
                Print("[SHADOW] Validation: Stop order not found");
                return false;
            }
            if (leaderStop == null || leaderStop.StopPrice <= 0)
            {
                Print(
                    string.Format("[SHADOW] Validation: Stop order invalid (price={0:F2})", leaderStop?.StopPrice ?? 0)
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Detects if leader stop price changed beyond noise threshold.
        /// Uses half-tick threshold to filter out insignificant price movements.
        /// </summary>
        /// <param name="entryKey">Entry key for cache lookup</param>
        /// <param name="currentStopPrice">Current stop price from order</param>
        /// <param name="leaderLastStopPrice">Cache dictionary for price tracking</param>
        /// <param name="tickSize">Tick size for noise threshold calculation</param>
        /// <param name="lastKnownPrice">Output: last known price from cache</param>
        /// <returns>True if price changed beyond threshold</returns>
        internal bool DetectStopPriceChange(
            string entryKey,
            double currentStopPrice,
            ConcurrentDictionary<string, double> leaderLastStopPrice,
            double tickSize,
            out double lastKnownPrice
        )
        {
            leaderLastStopPrice.TryGetValue(entryKey, out lastKnownPrice);

            if (Math.Abs(currentStopPrice - lastKnownPrice) < tickSize * 0.5)
            {
                Print(
                    string.Format(
                        "[SHADOW] Detection: Price change {0:F2} within noise threshold (last={1:F2})",
                        currentStopPrice,
                        lastKnownPrice
                    )
                );
                return false;
            }

            Print(
                string.Format(
                    "[SHADOW] Detection: Price changed {0:F2} -> {1:F2} (delta={2:F2} ticks)",
                    lastKnownPrice,
                    currentStopPrice,
                    Math.Abs(currentStopPrice - lastKnownPrice) / tickSize
                )
            );
            return true;
        }

        /// <summary>
        /// Propagates stop price to followers and updates cache on success.
        /// Cache is only updated if propagation succeeds (all followers ready).
        /// </summary>
        /// <param name="leaderEntryKey">Leader entry key</param>
        /// <param name="newStopPrice">New stop price to propagate</param>
        /// <param name="leaderLastStopPrice">Cache dictionary for price tracking</param>
        internal void PropagateAndCacheStopPrice(
            string leaderEntryKey,
            double newStopPrice,
            ConcurrentDictionary<string, double> leaderLastStopPrice
        )
        {
            if (ShadowMoveFollowerStops(leaderEntryKey, newStopPrice))
            {
                leaderLastStopPrice[leaderEntryKey] = newStopPrice;
                Print(
                    string.Format("[SHADOW] Propagation: Success - cached {0:F2} for {1}", newStopPrice, leaderEntryKey)
                );
            }
            else
            {
                Print(string.Format("[SHADOW] Propagation: Failed - followers not ready for {0}", leaderEntryKey));
            }
        }

        /// <summary>
        /// Validates leader entry key and retrieves associated dispatch context.
        /// </summary>
        private bool ShadowValidateDispatchContext(string leaderEntryKey, out SymmetryDispatchContext ctx)
        {
            ctx = null;
            string dispatchId;
            if (
                string.IsNullOrEmpty(leaderEntryKey)
                || !symmetryMasterEntryToDispatch.TryGetValue(leaderEntryKey, out dispatchId)
                || !symmetryDispatchById.TryGetValue(dispatchId, out ctx)
                || ctx == null
            )
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Builds complete list of follower entries linked to the dispatch context.
        /// ADR-019: Uses Volatile.Read snapshot for lock-free access.
        /// </summary>
        private System.Collections.Generic.List<string> ShadowBuildFollowerEntryList(
            SymmetryDispatchContext ctx,
            string dispatchId
        )
        {
            // ADR-019: snapshot via Volatile.Read on immutable string[] -- zero-alloc, lock-free.
            string[] followerSnapshot = ctx.Followers;
            var followerEntryNames = new System.Collections.Generic.List<string>(followerSnapshot.Length);

            foreach (string followerEntryName in followerSnapshot)
            {
                if (string.IsNullOrEmpty(followerEntryName))
                    continue;
                if (!symmetryFleetEntryToDispatch.TryGetValue(followerEntryName, out var linkedDispatch))
                    continue;
                if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                    continue;
                followerEntryNames.Add(followerEntryName);
            }

            foreach (var kvp in symmetryFleetEntryToDispatch.ToArray())
            {
                if (!string.Equals(kvp.Value, dispatchId, StringComparison.Ordinal))
                    continue;
                if (followerEntryNames.Contains(kvp.Key))
                {
                    continue;
                }
                followerEntryNames.Add(kvp.Key);
            }

            return followerEntryNames;
        }

        /// <summary>
        /// Processes stop update for a single follower entry.
        /// Returns true if follower found, sets waitingOnFollower if not ready.
        /// </summary>
        private bool ShadowProcessFollowerStopUpdate(
            string followerEntryName,
            double newStopPrice,
            out bool waitingOnFollower
        )
        {
            waitingOnFollower = false;

            FollowerBracketFSM fsm;
            bool hasFsm = _followerBrackets.TryGetValue(followerEntryName, out fsm) && fsm != null;
            PositionInfo followerPos;
            bool hasFollowerPos =
                activePositions.TryGetValue(followerEntryName, out followerPos) && followerPos != null;

            if (!hasFsm && !hasFollowerPos)
                return false;

            if (!hasFollowerPos || !followerPos.EntryFilled || !followerPos.BracketSubmitted)
            {
                waitingOnFollower = true;
                return true;
            }

            if (!hasFsm || fsm.State != FollowerBracketState.Active || fsm.StopOrder == null)
            {
                waitingOnFollower = true;
                return true;
            }

            // Skip if follower stop is already at the target price
            if (Math.Abs(fsm.StopOrder.StopPrice - newStopPrice) < tickSize * 0.5)
                return true;

            // Use existing stop update infrastructure (two-phase Replace FSM)
            Print(
                string.Format(
                    "[SHADOW] Propagating stop {0:F2} -> {1} on {2}",
                    newStopPrice,
                    followerEntryName,
                    fsm.AccountName
                )
            );
            UpdateStopOrder(followerEntryName, followerPos, newStopPrice, followerPos.CurrentTrailLevel);

            return true;
        }

        /// <summary>
        /// Propagates a leader stop price to all followers tracking the same master entry.
        /// Uses symmetry dispatch context to find the followers linked to this leader entry.
        /// </summary>
        private bool ShadowMoveFollowerStops(string leaderEntryKey, double newStopPrice)
        {
            SymmetryDispatchContext ctx;
            if (!ShadowValidateDispatchContext(leaderEntryKey, out ctx))
                return false;

            string dispatchId;
            symmetryMasterEntryToDispatch.TryGetValue(leaderEntryKey, out dispatchId);

            var followerEntryNames = ShadowBuildFollowerEntryList(ctx, dispatchId);

            bool foundAnyFollower = false;
            bool waitingOnFollower = false;
            foreach (string followerEntryName in followerEntryNames)
            {
                bool waitingOnThis;
                if (ShadowProcessFollowerStopUpdate(followerEntryName, newStopPrice, out waitingOnThis))
                {
                    foundAnyFollower = true;
                    if (waitingOnThis)
                        waitingOnFollower = true;
                }
            }

            return foundAnyFollower && !waitingOnFollower;
        }

        /// <summary>
        /// Detects when the leader goes flat and propagates flatten to all followers.
        /// Uses edge detection: fires only on the transition from in-position to flat.
        /// </summary>
        private void ShadowPropagateLeaderFlatten()
        {
            bool leaderHasOpenPosition = false;
            foreach (var kvp in activePositions)
            {
                PositionInfo pos = kvp.Value;
                if (pos != null && !pos.IsFollower && pos.EntryFilled && pos.RemainingContracts > 0)
                {
                    leaderHasOpenPosition = true;
                    break;
                }
            }

            if (_leaderWasInPosition && !leaderHasOpenPosition)
            {
                Print("[SHADOW] Leader position closed -- propagating flatten to fleet");
                FlattenAllApexAccounts();

                // Clear cached stop prices (no leader position to track)
                _leaderLastStopPrice.Clear();
            }

            _leaderWasInPosition = leaderHasOpenPosition;
        }

        #endregion
    }
}
