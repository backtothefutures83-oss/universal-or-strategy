// <copyright file="V12_002.UI.SnapshotPool.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12.44 MODULAR: UI Snapshot Pool Module (EPIC-5-PERF Ticket 03)
// Contains: Zero-allocation object pooling for UIStateSnapshot
using System;
using System.Collections.Concurrent;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region UI Snapshot Pool (EPIC-5-PERF T03)

        private static readonly ConcurrentBag<UIStateSnapshot> _uiSnapshotPool = new ConcurrentBag<UIStateSnapshot>();
        private const int PoolInitialSize = 4;
        private const int PoolMaxSize = 8;
        private static int _pooledSnapshotCount = 0;

        // Pool metrics for telemetry
        private static int _poolRentCount = 0;
        private static int _poolReturnCount = 0;
        private static int _poolFallbackCount = 0;

        /// <summary>
        /// Acquire a UIStateSnapshot from the pool or create a new instance if pool is exhausted.
        /// Zero allocation when pool has available instances.
        /// </summary>
        private UIStateSnapshot GetPooledSnapshot()
        {
            if (_uiSnapshotPool.TryTake(out UIStateSnapshot snapshot))
            {
                Interlocked.Decrement(ref _pooledSnapshotCount);
                Interlocked.Increment(ref _poolRentCount);
                return snapshot;
            }

            // Pool exhausted - create new instance with nested objects pre-allocated
            Interlocked.Increment(ref _poolFallbackCount);
            return new UIStateSnapshot();
        }

        /// <summary>
        /// Return a UIStateSnapshot to the pool for reuse.
        /// CRITICAL: Preserves nested object allocations (Config, Compliance, LivePosition).
        /// </summary>
        private void ReturnPooledSnapshot(UIStateSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            // Clear primitive fields and string references, preserve nested objects
            ClearSnapshotForReuse(snapshot);

            int currentCount = Volatile.Read(ref _pooledSnapshotCount);
            if (currentCount < PoolMaxSize)
            {
                _uiSnapshotPool.Add(snapshot);
                Interlocked.Increment(ref _pooledSnapshotCount);
                Interlocked.Increment(ref _poolReturnCount);
            }
            // If pool is full, let GC collect the snapshot
        }

        /// <summary>
        /// Clear primitive fields for snapshot reuse.
        /// CRITICAL: Does NOT null out nested objects (Config, Compliance, LivePosition).
        /// </summary>
        private void ClearSnapshotForReuse(UIStateSnapshot snapshot)
        {
            // Clear primitive fields
            snapshot.EmaValue = 0;
            snapshot.AtrValue = 0;
            snapshot.StatusMessage = null;
            snapshot.LastUpdateTicks = 0;
            snapshot.LastPrice = 0;
            snapshot.MasterMarketPosition = MarketPosition.Flat;
            snapshot.Mode = null;
            snapshot.TargetCount = 0;
            snapshot.IsRmaModeActive = false;
            snapshot.IsTrendRmaMode = false;
            snapshot.IsRetestRmaMode = false;
            snapshot.ConfigRevision = 0;
            snapshot.OrHigh = 0;
            snapshot.OrLow = 0;
            snapshot.OrRange = 0;
            snapshot.Ema9Value = 0;
            snapshot.Ema15Value = 0;
            snapshot.Ema30Value = 0;
            snapshot.Ema65Value = 0;
            snapshot.Ema200Value = 0;

            // CRITICAL: Do NOT null out nested objects - they remain allocated for reuse
            // snapshot.Config = null;        // BANNED
            // snapshot.Compliance = null;    // BANNED
            // snapshot.LivePosition = null;  // BANNED
        }

        /// <summary>
        /// Deep-copy config fields into pre-allocated UIConfigSnapshot.
        /// DIRECTOR FIX: No reference assignment - field-by-field copy (13 fields).
        /// </summary>
        private void UpdateConfigSnapshot(UIConfigSnapshot target, string mode)
        {
            // CRITICAL: Deep copy into pre-allocated target, NOT reference assignment
            target.Target1Value = Target1Value;
            target.Target2Value = Target2Value;
            target.Target3Value = Target3Value;
            target.Target4Value = Target4Value;
            target.Target5Value = Target5Value;
            target.Target1Type = T1Type;
            target.Target2Type = T2Type;
            target.Target3Type = T3Type;
            target.Target4Type = T4Type;
            target.Target5Type = T5Type;
            target.StopValue = string.Equals(mode, "RMA", StringComparison.OrdinalIgnoreCase)
                ? RMAStopATRMultiplier
                : StopMultiplier;
            target.MaxRiskValue = MaxRiskAmount;
            target.ChaseIfTouchPoints = string.IsNullOrEmpty(ChaseIfTouchPoints) ? "0" : ChaseIfTouchPoints;
        }

        /// <summary>
        /// Deep-copy compliance fields into pre-allocated UIComplianceSnapshot.
        /// Field-by-field assignment (8 fields).
        /// </summary>
        private void UpdateComplianceSnapshot(UIComplianceSnapshot target)
        {
            string accountName = Account != null ? Account.Name : "--";
            target.AccountName = accountName;
            target.DailyProfit = accountDailyProfit.TryGetValue(accountName, out double daily) ? daily : 0;
            target.TotalProfit = accountTotalProfit.TryGetValue(accountName, out double total) ? total : 0;
            target.TradeCount = accountTradeCount.TryGetValue(accountName, out int trades) ? trades : 0;
            target.UniqueDays = GetUniqueTradingDays(accountName);
            target.MaxDrawdown = accountMaxDrawdown.TryGetValue(accountName, out double maxDd) ? maxDd : 0;
            target.PayoutMinProfit = PayoutMinProfit;
            target.TrailingDrawdownLimit = TrailingDrawdownLimit;
        }

        /// <summary>
        /// In-place update of UILivePositionSnapshot.
        /// DIRECTOR FIX: TBD resolved - reuses 5-element UILiveTargetSnapshot array.
        /// </summary>
        private void UpdateLivePositionSnapshot(UILivePositionSnapshot target)
        {
            // Reset state
            target.HasLivePosition = false;
            target.EntryName = null;
            target.Direction = MarketPosition.Flat;
            target.StopPrice = 0;

            // Clear all target slots (reuse existing array instances)
            for (int i = 0; i < 5; i++)
            {
                target.Targets[i].IsVisible = false;
                target.Targets[i].Price = 0;
                target.Targets[i].RemainingContracts = 0;
                target.Targets[i].IsWorking = false;
            }

            // Find master position
            PositionInfo masterPos;
            string entryName;
            if (!FindMasterPosition(out masterPos, out entryName))
                return;

            // Update live position fields
            target.HasLivePosition = true;
            target.EntryName = entryName;
            target.Direction = masterPos.Direction;

            // Update target snapshots (in-place, reusing array elements)
            for (int targetNum = 1; targetNum <= 5; targetNum++)
            {
                UILiveTargetSnapshot targetSlot = target.Targets[targetNum - 1];
                bool isVisible = targetNum <= masterPos.InitialTargetCount && !IsTargetFilled(masterPos, targetNum);
                targetSlot.IsVisible = isVisible;

                if (!isVisible)
                    continue;

                var targetDict = GetTargetOrdersDictionary(targetNum);
                Order targetOrder = null;
                if (targetDict != null)
                    targetDict.TryGetValue(entryName, out targetOrder);

                double price = GetTargetPrice(masterPos, targetNum);
                if (targetOrder != null && targetOrder.LimitPrice > 0)
                    price = targetOrder.LimitPrice;

                int contracts = GetTargetContracts(masterPos, targetNum);
                int filled = GetTargetFilledQuantity(masterPos, targetNum);

                targetSlot.Price = price;
                targetSlot.RemainingContracts = Math.Max(0, contracts - filled);
                targetSlot.IsWorking =
                    targetOrder != null
                    && (targetOrder.OrderState == OrderState.Working || targetOrder.OrderState == OrderState.Accepted);
            }

            // Update stop snapshot
            Order stopOrder = null;
            if (stopOrders != null)
                stopOrders.TryGetValue(entryName, out stopOrder);

            target.StopPrice = masterPos.CurrentStopPrice;
            if (stopOrder != null && stopOrder.StopPrice > 0)
                target.StopPrice = stopOrder.StopPrice;
        }

        /// <summary>
        /// Pre-warm the snapshot pool with initial instances.
        /// Called during OnStateChange(State.DataLoaded).
        /// </summary>
        private void PreWarmSnapshotPool()
        {
            for (int i = 0; i < PoolInitialSize; i++)
            {
                UIStateSnapshot warmInstance = new UIStateSnapshot();
                // Nested objects already allocated by constructor:
                // - warmInstance.Config (new UIConfigSnapshot())
                // - warmInstance.Compliance (new UIComplianceSnapshot())
                // - warmInstance.LivePosition (new UILivePositionSnapshot())
                //   - warmInstance.LivePosition.Targets[0-4] (5 pre-allocated UILiveTargetSnapshot)

                _uiSnapshotPool.Add(warmInstance);
                Interlocked.Increment(ref _pooledSnapshotCount);
            }
        }

        /// <summary>
        /// Get pool health metrics for telemetry.
        /// </summary>
        private string GetPoolHealthMetrics()
        {
            int pooled = Volatile.Read(ref _pooledSnapshotCount);
            int rents = Volatile.Read(ref _poolRentCount);
            int returns = Volatile.Read(ref _poolReturnCount);
            int fallbacks = Volatile.Read(ref _poolFallbackCount);

            return string.Format(
                "[POOL] Snapshots: {0}/{1} | Rents: {2} | Returns: {3} | Fallbacks: {4}",
                pooled,
                PoolMaxSize,
                rents,
                returns,
                fallbacks
            );
        }

        #endregion
    }
}

// Made with Bob
