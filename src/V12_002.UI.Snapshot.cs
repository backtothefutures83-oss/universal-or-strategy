// V12_002.UI.Snapshot.cs -- Build 1108.004 UI snapshot publisher
// Strategy-thread only snapshot build. Dispatcher/UI code must read only from _uiSnapshot.
using System;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private UIStateSnapshot GetUiSnapshot()
        {
            UIStateSnapshot snapshot = _uiSnapshot;
            return snapshot ?? new UIStateSnapshot();
        }

        private void TouchStrategyHeartbeat()
        {
            Interlocked.Exchange(ref _strategyHeartbeatTicks, DateTime.UtcNow.Ticks);
        }

        private int BumpUiConfigRevision()
        {
            return Interlocked.Increment(ref _uiConfigRevision);
        }

        private string GetCurrentPanelMode()
        {
            string mode = GetCurrentConfigMode();
            return string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase) ? "ORB" : mode;
        }

        private double SafeEmaValue(EMA indicator)
        {
            try
            {
                if (indicator == null)
                    return 0;
                return indicator[0];
            }
            catch
            {
                return 0;
            }
        }

        private UIConfigSnapshot BuildUiConfigSnapshot(string mode)
        {
            return new UIConfigSnapshot
            {
                Target1Value = Target1Value,
                Target2Value = Target2Value,
                Target3Value = Target3Value,
                Target4Value = Target4Value,
                Target5Value = Target5Value,
                Target1Type = T1Type,
                Target2Type = T2Type,
                Target3Type = T3Type,
                Target4Type = T4Type,
                Target5Type = T5Type,
                StopValue = string.Equals(mode, "RMA", StringComparison.OrdinalIgnoreCase)
                    ? RMAStopATRMultiplier
                    : StopMultiplier,
                MaxRiskValue = MaxRiskAmount,
                ChaseIfTouchPoints = string.IsNullOrEmpty(ChaseIfTouchPoints) ? "0" : ChaseIfTouchPoints
            };
        }

        private UIComplianceSnapshot BuildUiComplianceSnapshot()
        {
            string accountName = Account != null ? Account.Name : "--";
            return new UIComplianceSnapshot
            {
                AccountName = accountName,
                DailyProfit = accountDailyProfit.TryGetValue(accountName, out double daily) ? daily : 0,
                TotalProfit = accountTotalProfit.TryGetValue(accountName, out double total) ? total : 0,
                TradeCount = accountTradeCount.TryGetValue(accountName, out int trades) ? trades : 0,
                UniqueDays = GetUniqueTradingDays(accountName),
                MaxDrawdown = accountMaxDrawdown.TryGetValue(accountName, out double maxDd) ? maxDd : 0,
                PayoutMinProfit = PayoutMinProfit,
                TrailingDrawdownLimit = TrailingDrawdownLimit
            };
        }

        private UILivePositionSnapshot BuildUiLivePositionSnapshot()
        {
            UILivePositionSnapshot live = new UILivePositionSnapshot();
            if (activePositions == null || activePositions.Count == 0)
                return live;

            PositionInfo masterPos = null;
            string entryName = null;

            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo candidate = kvp.Value;
                if (candidate == null || candidate.IsFollower || candidate.PendingCleanup)
                    continue;
                if (!candidate.EntryFilled || candidate.RemainingContracts <= 0)
                    continue;

                masterPos = candidate;
                entryName = kvp.Key;
                break;
            }

            if (masterPos == null)
                return live;

            live.HasLivePosition = true;
            live.EntryName = entryName;
            live.Direction = masterPos.Direction;

            for (int targetNum = 1; targetNum <= 5; targetNum++)
            {
                UILiveTargetSnapshot target = live.Targets[targetNum - 1];
                bool isVisible = targetNum <= masterPos.InitialTargetCount && !IsTargetFilled(masterPos, targetNum);
                target.IsVisible = isVisible;
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
                target.Price = price;
                target.RemainingContracts = Math.Max(0, contracts - filled);
                target.IsWorking = targetOrder != null
                    && (targetOrder.OrderState == OrderState.Working || targetOrder.OrderState == OrderState.Accepted);
            }

            Order stopOrder = null;
            if (stopOrders != null)
                stopOrders.TryGetValue(entryName, out stopOrder);

            live.StopPrice = masterPos.CurrentStopPrice;
            if (stopOrder != null && stopOrder.StopPrice > 0)
                live.StopPrice = stopOrder.StopPrice;

            return live;
        }

        private string BuildUiStatusMessage(UIStateSnapshot snapshot)
        {
            if (_isTerminating)
                return "Terminating";

            if (snapshot != null && snapshot.LivePosition != null && snapshot.LivePosition.HasLivePosition)
            {
                string entryName = string.IsNullOrEmpty(snapshot.LivePosition.EntryName)
                    ? "MASTER"
                    : snapshot.LivePosition.EntryName;
                return string.Format("LIVE {0} {1}", snapshot.LivePosition.Direction, entryName);
            }

            string mode = snapshot != null && !string.IsNullOrEmpty(snapshot.Mode)
                ? snapshot.Mode
                : "ORB";
            return "MODE " + mode;
        }

        private void PublishUiSnapshot()
        {
            string mode = GetCurrentPanelMode();
            double ema9Value = SafeEmaValue(ema9);

            UIStateSnapshot snapshot = new UIStateSnapshot
            {
                EmaValue = ema9Value,
                AtrValue = currentATR > 0 ? currentATR : 0,
                LastUpdateTicks = DateTime.UtcNow.Ticks,
                LastPrice = lastKnownPrice,
                Mode = mode,
                TargetCount = Math.Max(1, Math.Min(5, activeTargetCount)),
                IsRmaModeActive = isRMAModeActive,
                IsTrendRmaMode = isTrendRmaMode,
                IsRetestRmaMode = isRetestRmaMode,
                ConfigRevision = Volatile.Read(ref _uiConfigRevision),
                OrHigh = sessionHigh != double.MinValue ? sessionHigh : 0,
                OrLow = sessionLow != double.MaxValue ? sessionLow : 0,
                OrRange = (sessionHigh != double.MinValue && sessionLow != double.MaxValue)
                    ? (sessionHigh - sessionLow)
                    : 0,
                Ema9Value = ema9Value,
                Ema15Value = SafeEmaValue(ema15),
                Ema30Value = SafeEmaValue(ema30),
                Ema65Value = SafeEmaValue(ema65),
                Ema200Value = SafeEmaValue(ema200),
                Config = BuildUiConfigSnapshot(mode),
                Compliance = BuildUiComplianceSnapshot(),
                LivePosition = BuildUiLivePositionSnapshot()
            };

            snapshot.MasterMarketPosition = snapshot.LivePosition != null && snapshot.LivePosition.HasLivePosition
                ? snapshot.LivePosition.Direction
                : (Position != null ? Position.MarketPosition : MarketPosition.Flat);
            snapshot.StatusMessage = BuildUiStatusMessage(snapshot);

            _uiSnapshot = snapshot;
        }
    }
}
