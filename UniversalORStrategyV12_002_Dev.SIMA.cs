// V12.12 FLEET SYMMETRY & SAFETY HARDENING - Single-Instance Multi-Account Copy Trading Engine
// SIMA Module (Extracted)
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
        /// <summary>
        /// V12 SIMA: Helper struct to rank accounts by Daily P/L
        /// </summary>
        private struct AccountRankInfo
        {
            public Account Account;
            public double DailyPL;
            public string Name;
        }

        /// <summary>
        /// V12 SIMA: Returns the list of Apex accounts sorted by Daily P/L (Lowest to Highest)
        /// </summary>
        private List<AccountRankInfo> GetSortedAccountFleet()
        {
            List<AccountRankInfo> fleet = new List<AccountRankInfo>();

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                    fleet.Add(new AccountRankInfo { Account = acct, DailyPL = dailyPL, Name = acct.Name });
                }
            }

            // Sort by P/L ascending (Lowest P/L first)
            return fleet.OrderBy(a => a.DailyPL).ToList();
        }

        private void SetRmaAnchorFromIpc(string anchorStr)
        {
            try
            {
                if (anchorStr == "EMA30") currentRmaAnchor = RmaAnchorType.Ema30;
                else if (anchorStr == "EMA65") currentRmaAnchor = RmaAnchorType.Ema65;
                else if (anchorStr == "EMA200") currentRmaAnchor = RmaAnchorType.Ema200;
                else if (anchorStr == "OR_HIGH") currentRmaAnchor = RmaAnchorType.OrHigh;
                else if (anchorStr == "OR_LOW") currentRmaAnchor = RmaAnchorType.OrLow;
                else if (anchorStr == "MANUAL") currentRmaAnchor = RmaAnchorType.Manual;

                Print("IPC SET ANCHOR: " + anchorStr);

                // Refresh UI to show selected anchor (Green Highlight)
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("Error SetRmaAnchorFromIpc: " + ex.Message);
            }
        }

        #region V12 SIMA Multi-Account Execution Engine

        /// <summary>
        /// V12 SIMA: Execute a Smart Dispatched trade across the fleet.
        /// Logic:
        ///   - Signal = TREND: Lowest P/L account gets TREND targets, others get RMA targets.
        ///   - Signal = RMA/OR/MOMO: All accounts get RMA targets.
        /// Accounts use FIXED brackets (Path B) for zero trail lag.
        /// </summary>
        private void ExecuteSmartDispatchEntry(string tradeType, OrderAction action, int quantity, double entryPrice, OrderType entryOrderType = OrderType.Market)
        {
            // V12.2: Diagnostic logging for copy trading troubleshooting
            Print($"[DISPATCH] ExecuteSmartDispatchEntry called: {tradeType} | EnableSIMA={EnableSIMA} | OrderType={entryOrderType}");

            if (!EnableSIMA)
            {
                Print("[DISPATCH] ⚠️ SIMA DISABLED - Enable in strategy parameters to copy trade");
                return;
            }

            List<AccountRankInfo> fleet = GetSortedAccountFleet();

            // V12.2: Log fleet state for diagnostics
            int activeCount = 0;
            foreach (var acct in fleet)
            {
                if (activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) && isActive)
                    activeCount++;
            }
            Print($"[DISPATCH] Fleet: {fleet.Count} total accounts | {activeCount} ACTIVE in Fleet Manager");

            if (fleet.Count == 0)
            {
                Print("[DISPATCH] ⚠️ NO APEX ACCOUNTS DETECTED - Check AccountPrefix setting");
                return;
            }

            if (activeCount == 0)
            {
                Print("[DISPATCH] ⚠️ NO ACCOUNTS ENABLED - Toggle accounts ON in Fleet Manager panel");
            }

            int trendCount = 0;
            int rmaCount = 0;

            for (int i = 0; i < fleet.Count; i++)
            {
                Account acct = fleet[i].Account;

                // V12.1: Skip Master account if its order was already placed by the caller
                if (acct == this.Account) continue;

                // V12.8: Skip accounts NOT registered or disabled in Fleet Manager UI
                if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
                {
                    Print($"[SIMA] Fleet Dispatch: {acct.Name} SKIPPED (Inactive in Fleet Manager)");
                    continue;
                }

                // Consistency Lock Check (Shared logic)
                if (EnableConsistencyLock)
                {
                    if (fleet[i].DailyPL >= MaxDailyProfitCap)
                    {
                        Print($"[DISPATCH] 🔒 SKIPPING {acct.Name} - Consistency Lock Active (${fleet[i].DailyPL:F2})");
                        continue;
                    }
                }

                // V12: Followers ALWAYS use RMA multipliers for point-based trails (User Req)
                bool useRmaForFollower = true;

                double stopMult = useRmaForFollower ? RMAStopATRMultiplier : (tradeType == "TREND" && i == 0 ? TRENDEntry1ATRMultiplier : RMAStopATRMultiplier);
                double t1Price = Target1FixedPoints;
                double t2Mult = useRmaForFollower ? RMAT1ATRMultiplier : (tradeType == "TREND" && i == 0 ? Target3Multiplier : RMAT1ATRMultiplier);
                double t3Mult = useRmaForFollower ? RMAT2ATRMultiplier : (tradeType == "TREND" && i == 0 ? (Target3Multiplier * 1.5) : RMAT2ATRMultiplier);

                // Calculate fixed prices
                double stopDist = Math.Min(currentATR * stopMult, MaximumStop);
                double t2Dist = currentATR * t2Mult;
                double t3Dist = currentATR * t3Mult;

                double stopPrice = (action == OrderAction.Buy) ? entryPrice - stopDist : entryPrice + stopDist;
                double t1TargetPrice = (action == OrderAction.Buy) ? entryPrice + t1Price : entryPrice - t1Price;
                double t2TargetPrice = (action == OrderAction.Buy) ? entryPrice + t2Dist : entryPrice - t2Dist;

                // Rounding
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);
                t1TargetPrice = Instrument.MasterInstrument.RoundToTickSize(t1TargetPrice);
                t2TargetPrice = Instrument.MasterInstrument.RoundToTickSize(t2TargetPrice);

                try
                {
                    string ocoId = tradeType + "_" + DateTime.Now.Ticks + "_" + i;
                    string fleetEntryName = "Fleet_" + acct.Name + "_" + tradeType + "_" + i;

                    // V12.3: Entry uses caller-specified order type (Limit for RMA, Market for MOMO/TREND)
                    double limitPx = (entryOrderType == OrderType.Limit) ? entryPrice : 0;
                    bool isMarketEntry = (entryOrderType == OrderType.Market);
                    Order entry = acct.CreateOrder(Instrument, action, entryOrderType, TimeInForce.Gtc, quantity, limitPx, 0, ocoId, tradeType, null);

                    // V12.7: For Limit entries, defer bracket submission until fill.
                    // For Market entries, submit entry + stop + target together (instant fill expected).
                    Order stop = null;
                    Order target = null;
                    if (isMarketEntry)
                    {
                        stop = acct.CreateOrder(Instrument, action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.StopMarket, TimeInForce.Gtc, quantity, 0, stopPrice, ocoId, "Stop_" + tradeType, null);
                        target = acct.CreateOrder(Instrument, action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.Limit, TimeInForce.Gtc, quantity, t2TargetPrice, 0, ocoId, "Target_" + tradeType, null);
                    }

                    // V12.1: Track Follower Position for Active Trailing Stop Management
                    PositionInfo fleetPos = new PositionInfo
                    {
                        SignalName = fleetEntryName,
                        Direction = action == OrderAction.Buy ? MarketPosition.Long : MarketPosition.Short,
                        TotalContracts = quantity,
                        RemainingContracts = quantity,
                        EntryPrice = entryPrice,
                        InitialStopPrice = stopPrice,
                        CurrentStopPrice = stopPrice,
                        Target1Price = t1TargetPrice,
                        Target2Price = t2TargetPrice,
                        Target3Price = t2TargetPrice, // Simplified for followers
                        ExecutingAccount = acct,
                        IsFollower = true,
                        IsRMATrade = true,          // Enforce Point-Based Trailing for all followers
                        IsTRENDTrade = (tradeType == "TREND"),
                        IsRetestTrade = (tradeType == "RETEST"),
                        EntryFilled = isMarketEntry, // V12.3: Only true for Market entries; Limit waits for fill
                        BracketSubmitted = isMarketEntry, // V12.7: Brackets deferred for Limit entries
                        TicksSinceEntry = 0,
                        ExtremePriceSinceEntry = entryPrice,
                        CurrentTrailLevel = 0
                    };

                    activePositions[fleetEntryName] = fleetPos;
                    entryOrders[fleetEntryName] = entry; // V12.3: Track entry for CIT chase
                    if (stop != null) stopOrders[fleetEntryName] = stop;
                    if (target != null) target2Orders[fleetEntryName] = target;

                    // V12.7: Submit only entry for Limit, full bracket for Market
                    if (isMarketEntry)
                        acct.Submit(new[] { entry, stop, target });
                    else
                        acct.Submit(new[] { entry });

                    int delta = (action == OrderAction.Buy) ? quantity : -quantity;
                    expectedPositions.AddOrUpdate(acct.Name, delta, (k, v) => v + delta);

                    rmaCount++;
                }
                catch (Exception ex)
                {
                    Print($"[DISPATCH] ✗ FAILED on {acct.Name}: {ex.Message}");
                }
            }

            Print($"[DISPATCH] COMPLETED: {trendCount} Trend / {rmaCount} RMA Trades Assigned");
        }

        /// <summary>
        /// V12 SIMA: Enumerate and log all connected accounts matching the AccountPrefix
        /// </summary>
        private void EnumerateApexAccounts()
        {
            simaAccountCount = 0;
            Print("[SIMA] ═══════════════════════════════════════════════════");
            Print("[SIMA] V12.12 - Fleet Symmetry & Safety Hardening Initializing");
            Print($"[SIMA] Account Prefix Filter: \"{AccountPrefix}\"");
            Print("[SIMA] ───────────────────────────────────────────────────");

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    simaAccountCount++;
                    expectedPositions[acct.Name] = 0; // Initialize expected position as flat
                    accountDailyProfit[acct.Name] = 0; // Initialize daily profit
                    EnsureAccountComplianceTracking(acct.Name, GetComplianceNow());
                    activeFleetAccounts[acct.Name] = false; // V12.8 SIMA: Default to INACTIVE — wait for Fleet Manager / IPC to enable

                    // V12.7: Always subscribe to execution updates for fleet bracket management
                    // (Also used by ComplianceHub for P/L tracking)
                    acct.ExecutionUpdate += OnAccountExecutionUpdate;
                    acct.OrderUpdate += OnAccountOrderUpdate;
                    if (EnableComplianceHub)
                    {
                        Print($"[SIMA] ✓ {acct.Name} | COMPLIANCE MONITORING ACTIVE");
                    }
                    else
                    {
                        Print($"[SIMA] #{simaAccountCount}: {acct.Name} | Connected: {acct.Connection?.Status == ConnectionStatus.Connected} | Fleet: INACTIVE (awaiting IPC enable)");
                    }
                }
            }

            Print("[SIMA] ───────────────────────────────────────────────────");
            Print($"[SIMA] TOTAL ACCOUNTS DETECTED: {simaAccountCount} | ALL INACTIVE by default");
            Print("[SIMA] Use Fleet Manager or IPC TOGGLE_ACCOUNT to enable specific accounts");
            Print("[SIMA] ═══════════════════════════════════════════════════");
        }

        /// <summary>
        /// V12 SIMA: Execute a market order across ALL accounts matching the prefix
        /// </summary>
        private void ExecuteMultiAccountMarket(OrderAction action, int quantity, string signalName)
        {
            if (!EnableSIMA) return;

            int successCount = 0;
            int failCount = 0;

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // V12.8: Fleet Active Check — skip accounts NOT registered or disabled
                    if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
                    {
                        Print($"[SIMA] Fleet Dispatch: {acct.Name} SKIPPED (Inactive in Fleet Manager)");
                        continue;
                    }

                    try
                    {
                        // V12.1: Consistency Lock Check
                        if (EnableConsistencyLock)
                        {
                            double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                            if (dailyPL >= MaxDailyProfitCap)
                            {
                                Print($"[SIMA] 🔒 SKIPPING {acct.Name} - Consistency Lock Active (Day P/L: ${dailyPL:F2})");
                                continue;
                            }
                        }

                        Order order = acct.CreateOrder(Instrument, action, OrderType.Market,
                            TimeInForce.Gtc, quantity, 0, 0, "", signalName, null);
                        acct.Submit(new[] { order });

                        int delta = (action == OrderAction.Buy || action == OrderAction.BuyToCover) ? quantity : -quantity;
                        expectedPositions.AddOrUpdate(acct.Name, delta, (k, v) => v + delta);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        Print($"[SIMA] ✗ FAILED on {acct.Name}: {ex.Message}");
                    }
                }
            }
            Print($"[SIMA] BROADCAST: {action} {quantity} | {successCount} OK / {failCount} FAIL");
        }

        /// <summary>
        /// V12 SIMA: Execute a Market Entry + Fixed Target/Stop across ALL accounts (Path B)
        /// Uses true broker-side OCO brackets for each account
        /// </summary>
        private void ExecuteMultiAccountBracket(OrderAction action, int quantity, string signalName, double stopPoints, double targetPoints)
        {
            if (!EnableSIMA) return;

            int successCount = 0;
            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        // V12.1: Consistency Lock Check
                        if (EnableConsistencyLock)
                        {
                            double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                            if (dailyPL >= MaxDailyProfitCap)
                            {
                                Print($"[PATH B] 🔒 SKIPPING {acct.Name} - Consistency Lock Active (Day P/L: ${dailyPL:F2})");
                                continue;
                            }
                        }

                        // 1. Calculate Prices
                        double stopPrice = action == OrderAction.Buy ? currentPrice - stopPoints : currentPrice + stopPoints;
                        double targetPrice = action == OrderAction.Buy ? currentPrice + targetPoints : currentPrice - targetPoints;

                        // Round to nearest tick
                        stopPrice = Math.Round(stopPrice / tickSize) * tickSize;
                        targetPrice = Math.Round(targetPrice / tickSize) * tickSize;

                        // 2. Create Bracket
                        string ocoId = action.ToString() + "_" + DateTime.Now.Ticks;

                        Order entry = acct.CreateOrder(Instrument, action, OrderType.Market, TimeInForce.Gtc, quantity, 0, 0, ocoId, signalName, null);

                        Order stop = acct.CreateOrder(Instrument, action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.StopMarket, TimeInForce.Gtc, quantity, 0, stopPrice, ocoId, "Stop_" + signalName, null);

                        Order target = acct.CreateOrder(Instrument, action == OrderAction.Buy ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.Limit, TimeInForce.Gtc, quantity, targetPrice, 0, ocoId, "Target_" + signalName, null);

                        // 3. Submit as Atomic Group (Broker OCO)
                        acct.Submit(new[] { entry, stop, target });

                        int delta = (action == OrderAction.Buy) ? quantity : -quantity;
                        expectedPositions.AddOrUpdate(acct.Name, delta, (k, v) => v + delta);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA] ✗ BRACKET FAILED on {acct.Name}: {ex.Message}");
                    }
                }
            }
            Print($"[SIMA] PATH B BROADCAST: {successCount} Brackets Submitted");
        }

        /// <summary>
        /// V12 SIMA: Master Flatten - closes local position and broadcasts to the entire fleet
        /// </summary>
        // Duplicate FlattenAll removed - consolidated into line 4387 version

        /// <summary>
        /// V12 SIMA: RMA Entry V2 - Places limit entry + bracket on the local chart account,
        /// then iterates Account.All to place the same order on every fleet account matching AccountPrefix.
        /// CRITICAL: Every account's entry order is registered in entryOrders AND activePositions
        /// with a unique key (accountName + "_RMA") so ManageCIT can chase the entire fleet.
        /// </summary>
        private void ExecuteRMAEntryV2(double price, MarketPosition direction)
        {
            try
            {
                int qty = Math.Max(1, minContracts);

                // Calculate Stops & Targets using V12 RMA Logic
                double stopDist = Math.Min(currentATR * RMAStopATRMultiplier, MaximumStop);
                double t1Dist = Target1FixedPoints;
                double t2Dist = currentATR * RMAT1ATRMultiplier;

                double stopPrice = (direction == MarketPosition.Long) ? price - stopDist : price + stopDist;
                double t1Price = (direction == MarketPosition.Long) ? price + t1Dist : price - t1Dist;
                double t2Price = (direction == MarketPosition.Long) ? price + t2Dist : price - t2Dist;

                stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);
                t1Price = Instrument.MasterInstrument.RoundToTickSize(t1Price);
                t2Price = Instrument.MasterInstrument.RoundToTickSize(t2Price);

                string baseSignal = "RMA_" + DateTime.Now.Ticks;
                OrderAction entryAction = (direction == MarketPosition.Long) ? OrderAction.Buy : OrderAction.SellShort;
                OrderAction exitAction = (direction == MarketPosition.Long) ? OrderAction.Sell : OrderAction.BuyToCover;

                Print($"[SIMA RMA V2] {direction} @ {price} | Stop: {stopPrice} | T1: {t1Price} | T2: {t2Price} | Qty: {qty}");

                // ═══════════════════════════════════════════════════════
                // 1. LOCAL ACCOUNT: SubmitOrderUnmanaged (chart-visible)
                // ═══════════════════════════════════════════════════════
                string localKey = baseSignal;
                Order entryOrder = SubmitOrderUnmanaged(0, entryAction, OrderType.Limit, qty, price, 0, "", localKey);
                if (entryOrder != null)
                {
                    entryOrders[localKey] = entryOrder;

                    PositionInfo pos = new PositionInfo
                    {
                        SignalName = localKey,
                        Direction = direction,
                        TotalContracts = qty,
                        RemainingContracts = qty,
                        EntryPrice = price,
                        InitialStopPrice = stopPrice,
                        CurrentStopPrice = stopPrice,
                        Target1Price = t1Price,
                        Target2Price = t2Price,
                        EntryFilled = false,
                        BracketSubmitted = false, // V12.7: Brackets deferred until entry fills
                        IsRMATrade = true
                    };
                    activePositions[localKey] = pos;

                    // V12.12: Register Master account in expectedPositions (was missing — caused false Reaper desyncs)
                    int localDelta = (direction == MarketPosition.Long) ? qty : -qty;
                    expectedPositions.AddOrUpdate(Account.Name, localDelta, (k, v) => v + localDelta);
                    Print($"[SIMA] Master expectedPositions updated: {Account.Name} delta={localDelta}");

                    // V12.7: Do NOT submit stop/target here — they will be submitted by
                    // SubmitBracketOrders() when the entry limit fills in OnOrderUpdate.
                    // Submitting them now would cause instant fills on marketable targets.

                    Print($"[SIMA RMA V2] LOCAL ENTRY ONLY (Limit): {localKey} | Brackets deferred until fill");
                }
                else
                {
                    Print("[SIMA RMA V2] ERROR: Local entry returned null");
                }

                // ═══════════════════════════════════════════════════════
                // 2. SIMA FLEET: Iterate Account.All for followers
                // ═══════════════════════════════════════════════════════
                if (!EnableSIMA)
                {
                    Print("[SIMA RMA V2] ⚠️ EnableSIMA is FALSE - Fleet dispatch SKIPPED. Enable SIMA in strategy parameters or send SET_SIMA|ON via IPC.");
                    return;
                }

                int fleetOk = 0;
                int fleetSkip = 0;

                foreach (Account acct in Account.All)
                {
                    if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (acct == this.Account) continue; // local already done

                    // V12.8: Fleet Manager toggle — skip if account NOT registered or explicitly disabled
                    if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
                    {
                        Print($"[SIMA] Fleet Dispatch: {acct.Name} SKIPPED (Inactive in Fleet Manager)");
                        fleetSkip++;
                        continue;
                    }

                    // Consistency Lock
                    if (EnableConsistencyLock)
                    {
                        double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                        if (dailyPL >= MaxDailyProfitCap)
                        {
                            Print($"[SIMA RMA V2] SKIP {acct.Name} - ConsistencyLock (${dailyPL:F2})");
                            fleetSkip++;
                            continue;
                        }
                    }

                    try
                    {
                        string fleetKey = acct.Name + "_RMA_" + baseSignal;
                        string ocoId = fleetKey;

                        // V12.10: Submit ENTRY ONLY — brackets deferred until fill (unified with leader)
                        Order fEntry = acct.CreateOrder(Instrument, entryAction, OrderType.Limit,
                            TimeInForce.Gtc, qty, price, 0, ocoId, fleetKey, null);

                        acct.Submit(new[] { fEntry });

                        // Register in unified dictionaries so CIT + trailing works for this account
                        entryOrders[fleetKey] = fEntry;
                        activePositions[fleetKey] = new PositionInfo
                        {
                            SignalName = fleetKey,
                            Direction = direction,
                            TotalContracts = qty,
                            RemainingContracts = qty,
                            EntryPrice = price,
                            InitialStopPrice = stopPrice,
                            CurrentStopPrice = stopPrice,
                            Target1Price = t1Price,
                            Target2Price = t2Price,
                            Target3Price = t2Price,
                            EntryFilled = false,
                            IsRMATrade = true,
                            IsFollower = true,
                            ExecutingAccount = acct,
                            BracketSubmitted = false,   // V12.10: deferred — OnAccountExecutionUpdate submits on fill
                            ExtremePriceSinceEntry = price,
                            CurrentTrailLevel = 0
                        };
                        // stopOrders and target2Orders set by OnAccountExecutionUpdate on fill

                        int delta = (direction == MarketPosition.Long) ? qty : -qty;
                        expectedPositions.AddOrUpdate(acct.Name, delta, (k, v) => v + delta);

                        fleetOk++;
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA RMA V2] FAIL {acct.Name}: {ex.Message}");
                    }
                }

                Print($"[SIMA RMA V2] Fleet: {fleetOk} dispatched, {fleetSkip} skipped");
            }
            catch (Exception ex)
            {
                Print($"[SIMA RMA V2] ERROR: {ex.Message}");
            }
        }

        private void FlattenAllApexAccounts()
        {
            if (!EnableSIMA)
            {
                Print("[SIMA] DISABLED - Using single-account flatten");
                FlattenAll(); // Call consolidated flatten
                return;
            }

            isFlattenRunning = true; // V12.8: Guard for Reaper + OnAccountExecutionUpdate
            try
            {
                Print("[SIMA] ══════ GLOBAL FLATTEN START ══════");
                int flattenCount = 0;
                int totalCount = 0;

                // V12.9: Flatten ALL matching accounts regardless of Fleet Manager status.
                // This is a safety mechanism — "Flatten All" must always be able to close everything.
                foreach (Account acct in Account.All)
                {
                    if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        totalCount++;
                        try
                        {
                            // Collect instruments with open positions on this account
                            List<Instrument> instrumentsToFlatten = new List<Instrument>();
                            foreach (Position position in acct.Positions)
                            {
                                if (position.MarketPosition != MarketPosition.Flat)
                                {
                                    instrumentsToFlatten.Add(position.Instrument);
                                }
                            }

                            if (instrumentsToFlatten.Count > 0)
                            {
                                acct.Flatten(instrumentsToFlatten);
                                flattenCount++;
                                Print($"[SIMA] ✓ Flattened {instrumentsToFlatten.Count} position(s) on {acct.Name}");
                            }

                            // Reset expected position
                            expectedPositions[acct.Name] = 0;
                        }
                        catch (Exception ex)
                        {
                            Print($"[SIMA] ✗ FLATTEN FAILED on {acct.Name}: {ex.Message}");
                        }
                    }
                }

                // V12.12: Explicitly flatten the Master account if it was NOT covered by the prefix filter.
                // Bug fix: If Master is "Sim101" and AccountPrefix is "Apex", the loop above skips it entirely.
                bool masterCovered = Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!masterCovered)
                {
                    totalCount++;
                    try
                    {
                        List<Instrument> masterInstruments = new List<Instrument>();
                        foreach (Position position in Account.Positions)
                        {
                            if (position.MarketPosition != MarketPosition.Flat)
                            {
                                masterInstruments.Add(position.Instrument);
                            }
                        }

                        if (masterInstruments.Count > 0)
                        {
                            Account.Flatten(masterInstruments);
                            flattenCount++;
                            Print($"[SIMA] V12.12 Master flatten: {masterInstruments.Count} position(s) on {Account.Name} (outside prefix filter)");
                        }

                        expectedPositions[Account.Name] = 0;
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA] V12.12 Master FLATTEN FAILED on {Account.Name}: {ex.Message}");
                    }
                }

                Print($"[SIMA] ══════ GLOBAL FLATTEN COMPLETE: {flattenCount} flattened across {totalCount} accounts ══════");
            }
            finally
            {
                isFlattenRunning = false; // V12.8: Always release guard, even on exception
            }
        }

        private void ClosePositionsOnlyApexAccounts()
        {
            if (!EnableSIMA) return;

            // V12.21: FLATTEN_ONLY logic (Closes positions, Preserves pending orders)
            // Does NOT set isFlattenRunning guard because we aren't interfering with order cancellations
            // However, rapid execution updates might occur.
            
            Print("[SIMA] ══════ GLOBAL POSITIONS CLOSE START (Running Limit/Stop Orders Preserved) ══════");
            int closeCount = 0;
            int totalCount = 0;

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    totalCount++;
                    try
                    {
                        foreach (Position position in acct.Positions)
                        {
                            // Filter by instrument
                            if (position.Instrument.FullName != Instrument.FullName) continue;

                            if (position.MarketPosition != MarketPosition.Flat)
                            {
                                int qty = position.Quantity;
                                OrderAction closeAction = position.MarketPosition == MarketPosition.Long 
                                    ? OrderAction.Sell 
                                    : OrderAction.BuyToCover;

                                // Use Market order to close immediately
                                string signalName = "GracefulClose_" + position.MarketPosition.ToString(); // Unique signal name
                                Order closeOrder = acct.CreateOrder(Instrument, closeAction, OrderType.Market, 
                                    TimeInForce.Gtc, qty, 0, 0, "", signalName, null);
                                acct.Submit(new[] { closeOrder });

                                closeCount++;
                                Print($"[SIMA] ✓ Graceful Close: {qty} {position.MarketPosition} on {acct.Name}");
                            }
                        }

                        // Reset expected position (assuming full close)
                        expectedPositions[acct.Name] = 0;
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA] ✗ CLOSE FAILED on {acct.Name}: {ex.Message}");
                    }
                }
            }
            
            // Master fallback safety (if not picked up by prefix)
            bool masterCovered = Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!masterCovered && Account.Positions.Count > 0)
            {
                 foreach (Position position in Account.Positions)
                {
                    if (position.Instrument.FullName != Instrument.FullName) continue;
                    
                    if (position.MarketPosition != MarketPosition.Flat)
                    {
                         int qty = position.Quantity;
                         if (position.MarketPosition == MarketPosition.Long) ExitLong(); else ExitShort();
                         closeCount++;
                         Print($"[SIMA] ✓ Graceful Close: Master {qty} {position.MarketPosition}");
                    }
                }
                expectedPositions[Account.Name] = 0;
            }

            Print($"[SIMA] ══════ GLOBAL POSITIONS CLOSE COMPLETE: {closeCount} positions closed ══════");
        }

        #endregion
    }
}
