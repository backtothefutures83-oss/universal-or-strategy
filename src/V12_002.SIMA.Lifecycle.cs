// <copyright file="V12_002.SIMA.Lifecycle.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// Build 971: SIMA Lifecycle -- ApplySimaState, EnumerateApexAccounts, Hydrate*, CancelAll*, Sweep*
// V12 SIMA Module (Extracted)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Diagnostics;
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
        #region V12 SIMA Lifecycle

        private void ProcessApplySimaState(bool enabled)
        {
            // V12.Phase7: Lock-free toggle gate via Interlocked.CompareExchange
            // If a previous toggle was contended, attempt retry now.
            if (Volatile.Read(ref _simaTogglePending) == 1)
                Print("[SIMA LIFECYCLE] Retrying previously contended toggle (pending retry flag was set).");

            // Measure lifecycle gate contention because this runs on the actor path
            // and can stall queue drain when SIMA toggles overlap with other work.
            Stopwatch waitTimer = Stopwatch.StartNew();
            
            // Build 1109 [FREEZE-PROOF]: Non-blocking gate with spin-wait + Thread.Yield()
            // Max 3 retries before deferring to next strategy-thread cycle via TriggerCustomEvent.
            int retries = 0;
            const int MAX_RETRIES = 3;
            
            while (Interlocked.CompareExchange(ref _simaToggleState, 1, 0) != 0)
            {
                waitTimer.Stop();
                if (retries >= MAX_RETRIES)
                {
                    Volatile.Write(ref _simaTogglePending, 1);
                    bool _defEnabled = enabled;
                    Print("[SIMA_WARN] Toggle gate contended after 3 retries -- scheduling deferred retry");
                    try { TriggerCustomEvent(o => ProcessApplySimaState(_defEnabled), null); }
                    catch (Exception ex)
                    {
                        if (_diagFleet)
                            Print("[FLEET_CATCH] ApplySimaState toggle retry failed: " + ex.Message);
                    }
                    return;
                }
                retries++;
                Thread.Yield(); // Cooperative yield to other threads
            }
            
            try
            {
                waitTimer.Stop();
                if (waitTimer.Elapsed.TotalMilliseconds >= 25.0)
                    Print(string.Format("[LATENCY] [SIMA LIFECYCLE] Toggle gate spin-wait: {0:F1}ms", waitTimer.Elapsed.TotalMilliseconds));

                if (enabled)
                    ProcessInitializeSIMA();
                else
                    ProcessShutdownSIMA();

                EnableSIMA = enabled;
                // V12.Phase7: Toggle completed successfully -- clear any pending-retry flag.
                Volatile.Write(ref _simaTogglePending, 0);
            }
            finally
            {
                // V12.Phase7 [DNA]: Guaranteed gate release via Interlocked.Exchange in finally block
                Interlocked.Exchange(ref _simaToggleState, 0);
            }
        }

        private void ProcessInitializeSIMA()
        {
            EnumerateApexAccounts(); // Unsubs first (idempotent), then re-subscribes + hydrates
            if (ReaperAuditEnabled)
                StartReaperAudit();
            Print("[SIMA LIFECYCLE] SIMA ENABLED -- fleet enumerated, Reaper started");
        }

        private void ProcessShutdownSIMA()
        {
            CancelAllV12GtcOrders(false); // [BUILD 984] GTC sweep before teardown -- skip accounts with open positions
            StopReaperAudit();
            UnsubscribeFromFleetAccounts();
            // v28.0 shutdown drain: sideband-aware, XorShadow-free (we do not verify on shutdown;
            // we just need to release pool + roll back delta). Sideband entries are zeroed after.
            {
                FleetDispatchSlot ringSlot;
                while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out ringSlot))
                {
                    int _sbIdx = ringSlot.PoolSlotIndex;
                    string _expectedKey = (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                        ? _photonSideband[_sbIdx].ExpectedKey
                        : null;
                    if (ringSlot.ReservedDelta != 0 && _expectedKey != null)
                        AddExpectedPositionDelta(_expectedKey, -ringSlot.ReservedDelta);
                    if (_expectedKey != null)
                        ClearDispatchSyncPending(_expectedKey);
                    if (_sbIdx >= 0)
                    {
                        _photonPool.ReleaseByIndex(_sbIdx);
                        if (_sbIdx < _photonSideband.Length)
                            _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                    }
                }
                Print("[SIMA] Photon ring cleared on shutdown with delta rollback.");
            }
            // A3-1: Drain ghost dispatch queue on SIMA disable (Build 960 audit fix)
            // B957/F2: Rollback ReservedDelta and clear dispatch-sync barrier for each discarded request.
            {
                FleetDispatchRequest ignored;
                while (_pendingFleetDispatches.TryDequeue(out ignored))
                {
                    if (ignored.ReservedDelta != 0)
                        AddExpectedPositionDelta(ignored.ExpectedKey, -ignored.ReservedDelta);
                    ClearDispatchSyncPending(ignored.ExpectedKey);
                }
                Print("[SIMA] Dispatch queue cleared on shutdown with delta rollback.");
            }
            Print("[SIMA LIFECYCLE] SIMA DISABLED -- Reaper stopped, handlers unsubscribed");
        }

        private void EnumerateApexAccounts()
        {
            UnsubscribeFromFleetAccounts(); // V12.1101E [A-4]: Always unsub first -- idempotent guard against handler accumulation
            simaAccountCount = 0;
            Print("[SIMA] ===================================================");
            Print("[SIMA] V12.12 - Fleet Symmetry & Safety Hardening Initializing");
            Print($"[SIMA] Account Prefix Filter: \"{AccountPrefix}\"");
            Print("[SIMA] ---------------------------------------------------");

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    simaAccountCount++;
                    // Build 1105: Only init expectedPositions for master during enumeration.
                    // Follower REAPER audit truth is owned by FSM.
                    if (acct.Name == Account.Name)
                    { var _acct966init = ExpKey(acct.Name); SetExpectedPosition(_acct966init, 0); }
                    accountDailyProfit[acct.Name] = 0; // Initialize daily profit
                    EnsureAccountComplianceTracking(acct.Name, GetComplianceNow());
                    activeFleetAccounts[acct.Name] = false; // V12.8 SIMA: Default to INACTIVE -- wait for Fleet Manager / IPC to enable

                    // V12.7: Always subscribe to execution updates for fleet bracket management
                    // (Also used by ComplianceHub for P/L tracking)
                    acct.ExecutionUpdate += OnAccountExecutionUpdate;
                    acct.OrderUpdate += OnAccountOrderUpdate;
                    _subscribedAccountNames.Add(acct.Name); // V12.Phase6 [UNSUB-TRACK]: Track for deterministic unsubscribe
                    if (EnableComplianceHub)
                    {
                        Print($"[SIMA] [OK] {acct.Name} | COMPLIANCE MONITORING ACTIVE");
                    }
                    else
                    {
                        Print($"[SIMA] #{simaAccountCount}: {acct.Name} | Connected: {acct.Connection?.Status == ConnectionStatus.Connected} | Fleet: INACTIVE (awaiting IPC enable)");
                    }
                }
            }

            Print("[SIMA] ---------------------------------------------------");
            Print($"[SIMA] TOTAL ACCOUNTS DETECTED: {simaAccountCount} | ALL INACTIVE by default");
            Print("[SIMA] FLEET INACTIVE - MANUAL ENABLE REQUIRED"); // V12.Phase10 [DEFAULT-FIX]
            Print("[SIMA] ===================================================");

            // Build 1103: Apply persisted fleet toggles from sticky state file.
            // Must run AFTER enumeration (dict populated) but BEFORE hydration (expected positions).
            ApplyPendingStickyFleetToggles();

            // V12.Phase6 [HYDRATE]: Seed expectedPositions from live broker state
            HydrateExpectedPositionsFromBroker();

            // [BUILD 984] Adopt any working broker orders into tracking dicts; sets _orderAdoptionComplete = true
            HydrateWorkingOrdersFromBroker();

            // Build 1103: Enrich reconstructed positions with persisted trail state.
            // Must run AFTER Phase 3 (activePositions populated) so position keys exist.
            EnrichTrailStateFromSticky();
        }

        /// <summary>
        /// V12.Phase6 [HYDRATE]: Reads actual broker positions for each fleet account and seeds
        /// expectedPositions accordingly. Prevents false Reaper CRITICAL DESYNC alerts when the
        /// strategy restarts while accounts hold open positions.
        /// </summary>
        private void HydrateExpectedPositionsFromBroker()
        {
            int hydratedCount = 0;
            
            // Fleet accounts
            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;
                HydrateSingleAccountExpectedPosition(acct, ref hydratedCount);
            }
            
            if (hydratedCount > 0)
                Print(string.Format("[SIMA HYDRATE] Hydrated {0} account(s) with live broker positions", hydratedCount));
            
            // Build 993: Hydrate master account (mirrors AuditMasterAccountIfNeeded pattern).
            // IsFleetAccount excludes master -- must be handled separately, same as REAPER audit.
            bool masterIsFleet993 = IsFleetAccount(Account);
            if (!masterIsFleet993)
                HydrateSingleAccountExpectedPosition(Account, ref hydratedCount);
        }

        private void HydrateSingleAccountExpectedPosition(Account acct, ref int hydratedCount)
        {
            try
            {
                // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
                foreach (Position pos in acct.Positions.ToArray())
                {
                    if (pos != null && pos.Instrument != null
                        && pos.Instrument.FullName == Instrument.FullName
                        && pos.MarketPosition != MarketPosition.Flat)
                    {
                        int qty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
                        // Build 980 [Nexus]: Route expected position seed through the Actor queue
                        var capturedAcct = acct.Name;
                        var capturedQty = qty;
                        Enqueue(ctx => ctx.AddOrUpdateExpectedPosition(ExpKey(capturedAcct), capturedQty, v => capturedQty));
                        Print(string.Format("[SIMA HYDRATE] {0}: Seeded expected={1} from broker ({2} {3})",
                            acct.Name, qty, pos.MarketPosition, pos.Quantity));
                        hydratedCount++;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("[SIMA HYDRATE] WARNING: Could not read positions for {0}: {1}",
                    acct.Name, ex.Message));
            }
        }

        /// <summary>
        /// Build 984 [FIX-B]: Re-adopt working broker orders into tracking dicts after restart or reconnect.
        /// Derives the original entry key by stripping the well-known order-name prefix (e.g. "Stop_" -> stopOrders).
        /// Sets _orderAdoptionComplete = true when done so REAPER can resume auditing.
        /// MUST be called on the strategy thread (via TriggerCustomEvent when initiated from a callback).
        /// Actor-serialized lifecycle and reconnect paths update tracking dicts on the Ordered Actor Thread.
        /// </summary>
        private void HydrateWorkingOrdersFromBroker()
        {
            int adoptedCount = 0;

            AdoptFleetWorkingOrders(ref adoptedCount);

            // Build 993: Adopt master account bracket orders (mirrors fleet loop; no FSM creation for master).
            // IsFleetAccount excludes master -- must be handled separately.
            bool masterIsFleetForOrders993 = IsFleetAccount(Account);
            if (!masterIsFleetForOrders993)
            {
                AdoptMasterWorkingOrders(ref adoptedCount);
                ReconstructMasterPositionFromBrackets();
            }

            // Phase 5: Rebuild FSMs from adopted orders before enabling REAPER
            HydrateFSMsFromWorkingOrders();

            _orderAdoptionComplete = true;
            if (adoptedCount > 0)
                Print(string.Format("[SIMA HYDRATE] Adopted {0} working order(s) from broker -- adoption complete.", adoptedCount));
            else
                Print("[SIMA HYDRATE] No working orders to adopt -- adoption complete.");
        }

        /// <summary>
        /// Phase 1: Adopt working orders from fleet accounts into tracking dictionaries.
        /// Reconstructs activePositions structs for follower entries.
        /// </summary>
        private void AdoptFleetWorkingOrders(ref int adoptedCount)
        {
            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;
                try
                {
                    foreach (Order ord in acct.Orders.ToArray())
                    {
                        if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                        // [Codex P2] Include all live in-flight states -- Submitted/ChangePending/ChangeSubmitted
                        // can be active during an in-flight FSM replace at reconnect time.
                        // Setting _orderAdoptionComplete=true while these are skipped leaves REAPER
                        // auditing against incomplete order tracking and can fire false repair cycles.
                        if (ord.OrderState != OrderState.Working    &&
                            ord.OrderState != OrderState.Accepted   &&
                            ord.OrderState != OrderState.Submitted  &&
                            ord.OrderState != OrderState.ChangePending &&
                            ord.OrderState != OrderState.ChangeSubmitted) continue;

                        string orderKey;
                        string dictName;
                        ConcurrentDictionary<string, Order> targetDict = ClassifyAndRouteFleetOrder(ord, out orderKey, out dictName);
                        
                        if (targetDict == null || orderKey == null) continue;

                        targetDict[orderKey] = ord;

                        // [Build 980 Nexus] Rebuild activePositions structs so Rehydration does not lead to divergent REAPER audits.
                        if (targetDict == entryOrders && !activePositions.ContainsKey(orderKey))
                        {
                            RebuildActivePositionForFleetEntry(ord, orderKey, acct);
                        }
                        else
                        {
                            SyncExistingPositionMetadata(ord, orderKey, acct);
                        }

                        Print(string.Format("[SIMA HYDRATE] Adopted working order {0} into {1}", ord.Name ?? string.Empty, dictName));
                        adoptedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Print(string.Format("[SIMA HYDRATE] WARNING: Could not read orders for {0}: {1}", acct.Name, ex.Message));
                }
            }
        }

        private ConcurrentDictionary<string, Order> ClassifyAndRouteFleetOrder(Order ord, out string orderKey, out string dictName)
        {
            string name = ord.Name ?? string.Empty;
            ConcurrentDictionary<string, Order> targetDict = null;
            orderKey = null;
            dictName = null;

            if (name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders; orderKey = name.Substring(5); dictName = "stopOrders"; }
            else if (name.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders; orderKey = name.Substring(2); dictName = "stopOrders"; }
            else if (name.StartsWith("T1_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target1Orders; orderKey = name.Substring(3); dictName = "target1Orders"; }
            else if (name.StartsWith("T2_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target2Orders; orderKey = name.Substring(3); dictName = "target2Orders"; }
            else if (name.StartsWith("T3_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target3Orders; orderKey = name.Substring(3); dictName = "target3Orders"; }
            else if (name.StartsWith("T4_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target4Orders; orderKey = name.Substring(3); dictName = "target4Orders"; }
            else if (name.StartsWith("T5_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target5Orders; orderKey = name.Substring(3); dictName = "target5Orders"; }
            // [Codex P1] Adopt Fleet_ prefixed follower entry orders into entryOrders.
            // Without this, broker-resident follower entries are invisible after reconnect.
            // ProcessQueuedExecution finds them by object ref in entryOrders, so a missed
            // adoption means SymmetryGuardOnFollowerFill is bypassed and the new filled
            // position launches without its protective bracket orders.
            else if (name.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase))
            { targetDict = entryOrders; orderKey = name; dictName = "entryOrders"; }

            return targetDict;
        }

        private void RebuildActivePositionForFleetEntry(Order ord, string key, Account acct)
        {
            MarketPosition mp = (ord.OrderAction == OrderAction.Buy || ord.OrderAction == OrderAction.BuyToCover)
                ? MarketPosition.Long
                : MarketPosition.Short;
            double ePrice = ord.LimitPrice != 0
                ? ord.LimitPrice
                : (ord.StopPrice != 0 ? ord.StopPrice : ord.AverageFillPrice);
            
            var pos = new PositionInfo
            {
                SignalName = key,
                Direction = mp,
                TotalContracts = ord.Quantity,
                RemainingContracts = ord.Quantity,
                EntryPrice = ePrice,
                InitialStopPrice = 0,
                CurrentStopPrice = 0,
                EntryOrderType = ord.OrderType,
                EntryFilled = false,
                IsFollower = key.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase),
                ExecutingAccount = acct,
                BracketSubmitted = false,
                ExtremePriceSinceEntry = ePrice,
                CurrentTrailLevel = 0,
                OcoGroupId = "V12_" + GetStableHash(key)
            };
            
            // Get standard distribution
            int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
            GetTargetDistribution(ord.Quantity, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);
            pos.T1Contracts = t1Qty;
            pos.T2Contracts = t2Qty;
            pos.T3Contracts = t3Qty;
            pos.T4Contracts = t4Qty;
            pos.T5Contracts = t5Qty;
            
            // [Build 980 Phase 3]: Reconstruct trade DNA from signal name -- lost across restart.
            // Fleet entry names follow pattern: Fleet_<AcctName>_<TradeType>_<index>
            pos.IsMOMOTrade = key.IndexOf("_MOMO_", StringComparison.OrdinalIgnoreCase) >= 0;
            pos.IsRMATrade = key.IndexOf("_RMA_", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("_TREND_RMA_", StringComparison.OrdinalIgnoreCase) >= 0;
            pos.IsTRENDTrade = key.IndexOf("_TREND_", StringComparison.OrdinalIgnoreCase) >= 0;
            pos.IsRetestTrade = key.IndexOf("_RETEST_", StringComparison.OrdinalIgnoreCase) >= 0;
            if (pos.IsMOMOTrade) pos.IsRMATrade = false; // MOMO overrides generic RMA flag

            activePositions[key] = pos;
            Print(string.Format("[SIMA HYDRATE] Rebuilt activePositions struct for {0} | DNA: IsMOMO={1} IsRMA={2} IsTREND={3} IsRetest={4}",
                key, pos.IsMOMOTrade, pos.IsRMATrade, pos.IsTRENDTrade, pos.IsRetestTrade));
        }

        private void SyncExistingPositionMetadata(Order ord, string key, Account acct)
        {
            // [Build 980 Phase 3]: Force-sync TotalContracts and ExecutingAccount if struct already exists.
            PositionInfo existingPos;
            if (activePositions.TryGetValue(key, out existingPos))
            {
                existingPos.TotalContracts = ord.Quantity;
                existingPos.ExecutingAccount = acct;
                Print(string.Format("[SIMA HYDRATE] Force-synced TotalContracts={0} ExecutingAccount={1} for {2}",
                    ord.Quantity, acct.Name, key));
            }
        }

        /// <summary>
        /// Validates whether an order state qualifies for adoption into tracking dictionaries.
        /// Build 994: Master account also accepts Unknown state (NT8 Sim previous-session orders).
        /// </summary>
        /// <param name="state">Order state to validate</param>
        /// <param name="includeMasterUnknown">If true, also accepts Unknown state for master account orders</param>
        /// <returns>True if order should be adopted</returns>
        private bool IsOrderStateAdoptable(OrderState state, bool includeMasterUnknown)
        {
            if (state == OrderState.Working) return true;
            if (state == OrderState.Accepted) return true;
            if (state == OrderState.Submitted) return true;
            if (state == OrderState.ChangePending) return true;
            if (state == OrderState.ChangeSubmitted) return true;
            if (includeMasterUnknown && state == OrderState.Unknown) return true;
            return false;
        }

        /// <summary>
        /// Phase 2: Adopt working orders from master account into tracking dictionaries.
        /// Master account does not use FSM -- bracket orders only.
        /// </summary>
        private void AdoptMasterWorkingOrders(ref int adoptedCount)
        {
            try
            {
                Account masterBroker996h = Account;
                foreach (Order ord in masterBroker996h.Orders.ToArray())
                {
                    if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                    if (!IsOrderStateAdoptable(ord.OrderState, includeMasterUnknown: true)) continue;

                    string name = ord.Name ?? string.Empty;
                    string key, dictName;
                    ConcurrentDictionary<string, Order> targetDict =
                        ClassifyMasterOrderByPrefix(name, out key, out dictName);

                    if (targetDict == null || key == null) continue;

                    targetDict[key] = ord;
                    adoptedCount++;
                    Print(string.Format("[SIMA HYDRATE] {0} (Master): Adopted {1} -> {2}[{3}]",
                        Account.Name, name, dictName, key));
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("[SIMA HYDRATE] WARNING: Could not adopt orders for {0} (Master): {1}",
                    Account.Name, ex.Message));
            }
        }

        /// <summary>
        /// Classifies a master account order by its name prefix and returns the target tracking dictionary.
        /// Extracts the entry key by stripping the well-known prefix (e.g. "Stop_" -> stopOrders).
        /// </summary>
        /// <param name="orderName">Order name to classify</param>
        /// <param name="key">Output: Entry key (name with prefix stripped)</param>
        /// <param name="dictName">Output: Dictionary name for diagnostics</param>
        /// <returns>Target dictionary, or null if prefix not recognized</returns>
        private ConcurrentDictionary<string, Order> ClassifyMasterOrderByPrefix(
            string orderName,
            out string key,
            out string dictName)
        {
            key = null;
            dictName = null;
            
            if (orderName.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
            { key = orderName.Substring(5); dictName = "stopOrders"; return stopOrders; }
            
            if (orderName.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
            { key = orderName.Substring(2); dictName = "stopOrders"; return stopOrders; }
            
            if (orderName.StartsWith("T1_", StringComparison.OrdinalIgnoreCase))
            { key = orderName.Substring(3); dictName = "target1Orders"; return target1Orders; }
            
            if (orderName.StartsWith("T2_", StringComparison.OrdinalIgnoreCase))
            { key = orderName.Substring(3); dictName = "target2Orders"; return target2Orders; }
            
            if (orderName.StartsWith("T3_", StringComparison.OrdinalIgnoreCase))
            { key = orderName.Substring(3); dictName = "target3Orders"; return target3Orders; }
            
            if (orderName.StartsWith("T4_", StringComparison.OrdinalIgnoreCase))
            { key = orderName.Substring(3); dictName = "target4Orders"; return target4Orders; }
            
            if (orderName.StartsWith("T5_", StringComparison.OrdinalIgnoreCase))
            { key = orderName.Substring(3); dictName = "target5Orders"; return target5Orders; }
            
            return null;
        }

        /// <summary>
        /// Phase 3: Reconstruct master account activePositions from filled positions + bracket orders.
        /// Handles cases where entry order is terminal but position + brackets exist.
        /// </summary>
        private void ReconstructMasterPositionFromBrackets()
        {
            try
            {
                int masterQty;
                double masterAvgPrice;
                MarketPosition masterMP = FindMasterPositionFromBroker(out masterQty, out masterAvgPrice);

                if (masterMP != MarketPosition.Flat && masterQty > 0)
                {
                    foreach (var stopKvp in stopOrders.ToArray())
                    {
                        string key = stopKvp.Key;
                        if (key.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase)) continue;
                        if (activePositions.ContainsKey(key)) continue;

                        Order adoptedStop = stopKvp.Value;
                        double stopPrice = adoptedStop != null ? adoptedStop.StopPrice : 0;

                        PositionInfo pos = BuildMasterPositionInfo(key, masterMP, masterQty, masterAvgPrice, stopPrice);
                        activePositions[key] = pos;

                        Print(string.Format("[SIMA HYDRATE] Reconstructed master position for {0} | Dir={1} Qty={2} AvgPx={3} StopPx={4}",
                            key, masterMP, masterQty, masterAvgPrice, stopPrice));
                    }
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("[SIMA HYDRATE] WARNING: Master position reconstruction failed: {0}", ex.Message));
            }
        }

        private MarketPosition FindMasterPositionFromBroker(out int qty, out double avgPrice)
        {
            qty = 0;
            avgPrice = 0;

            foreach (Position brokerPos in Account.Positions.ToArray())
            {
                if (brokerPos != null && brokerPos.Instrument != null
                    && brokerPos.Instrument.FullName == Instrument.FullName
                    && brokerPos.MarketPosition != MarketPosition.Flat)
                {
                    qty = brokerPos.Quantity;
                    avgPrice = brokerPos.AveragePrice;
                    return brokerPos.MarketPosition;
                }
            }

            return MarketPosition.Flat;
        }

        private PositionInfo BuildMasterPositionInfo(string key, MarketPosition masterMP, int masterQty, double masterAvgPrice, double stopPrice)
        {
            int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
            GetTargetDistribution(masterQty, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

            bool trendMnlMatch = key.StartsWith("TrendMnl", StringComparison.OrdinalIgnoreCase);
            Print(string.Format("[SIMA HYDRATE] Master stop key audit for {0}: TrendMnlStartsWith={1}",
                key, trendMnlMatch));

            var pos = new PositionInfo
            {
                SignalName = key,
                Direction = masterMP,
                TotalContracts = masterQty,
                RemainingContracts = masterQty,
                EntryPrice = masterAvgPrice,
                InitialStopPrice = stopPrice,
                CurrentStopPrice = stopPrice,
                EntryOrderType = OrderType.Market,
                EntryFilled = true,
                IsFollower = false,
                ExecutingAccount = null,
                BracketSubmitted = true,
                ExtremePriceSinceEntry = masterAvgPrice,
                CurrentTrailLevel = 0,
                OcoGroupId = "V12_" + GetStableHash(key),
                T1Contracts = t1Qty,
                T2Contracts = t2Qty,
                T3Contracts = t3Qty,
                T4Contracts = t4Qty,
                T5Contracts = t5Qty
            };

            pos.IsMOMOTrade = key.StartsWith("MOMO", StringComparison.OrdinalIgnoreCase);
            pos.IsTRENDTrade = trendMnlMatch
                || key.StartsWith("TRMA_", StringComparison.OrdinalIgnoreCase);
            pos.IsRetestTrade = key.StartsWith("Retest", StringComparison.OrdinalIgnoreCase);
            pos.IsRMATrade = key.StartsWith("TRMA_", StringComparison.OrdinalIgnoreCase)
                || pos.IsRetestTrade;
            pos.IsFFMATrade = key.StartsWith("FFMA", StringComparison.OrdinalIgnoreCase);
            if (pos.IsMOMOTrade) pos.IsRMATrade = false;

            return pos;
        }

        /// <summary>
        /// Phase 5: Rebuilds _followerBrackets and _orderIdToFsmKey from already-adopted
        /// working orders. Called from HydrateWorkingOrdersFromBroker() before the
        /// adoption-complete gate is set. Idempotent -- safe to call on every reconnect.
        /// </summary>
        /// <summary>
        /// Maps broker OrderState to FollowerBracketState for FSM hydration.
        /// Returns Unknown for terminal states that don't need FSM tracking.
        /// </summary>
        private FollowerBracketState HydrateFSM_MapOrderStateToFsmState(OrderState entryState)
        {
            if (entryState == OrderState.Filled || entryState == OrderState.PartFilled)
                return FollowerBracketState.Active;
            
            if (entryState == OrderState.Accepted)
                return FollowerBracketState.Accepted;
            
            if (entryState == OrderState.Working
                || entryState == OrderState.Submitted
                || entryState == OrderState.Initialized
                || entryState == OrderState.ChangePending
                || entryState == OrderState.ChangeSubmitted)
                return FollowerBracketState.Submitted;
            
            return FollowerBracketState.None; // Terminal state
        }

        /// <summary>
        /// Determines remaining contracts for FSM based on entry order and live position.
        /// For Active state, queries broker position to get actual quantity.
        /// </summary>
        private int HydrateFSM_DetermineRemainingContracts(
            Order entryOrder,
            FollowerBracketState hydrationState,
            Account executingAccount)
        {
            int contracts = Math.Max(0, entryOrder.Quantity);
            
            if (hydrationState == FollowerBracketState.Active)
            {
                Position livePosition = executingAccount.Positions.ToArray().FirstOrDefault(p =>
                    p != null
                    && p.Instrument != null
                    && p.Instrument.FullName == Instrument.FullName
                    && p.MarketPosition != MarketPosition.Flat);
                
                if (livePosition != null)
                    contracts = Math.Abs(livePosition.Quantity);
            }
            
            return contracts;
        }

        /// <summary>
        /// Links stop and target orders (T1-T5) to FSM and indexes OrderIds for event routing.
        /// </summary>
        private void HydrateFSM_LinkBracketOrders(
            string entryKey,
            FollowerBracketFSM fsm,
            ref int ordersIndexed)
        {
            // Link stop order
            Order stopOrd;
            if (stopOrders.TryGetValue(entryKey, out stopOrd) && stopOrd != null)
            {
                fsm.StopOrder = stopOrd;
                if (!string.IsNullOrEmpty(stopOrd.OrderId))
                {
                    _orderIdToFsmKey[stopOrd.OrderId] = entryKey;
                    ordersIndexed++;
                }
            }

            // Link target orders (match exact property names on FollowerBracketFSM)
            var targetOrderSlots = new[]
            {
                target1Orders, target2Orders, target3Orders, target4Orders, target5Orders
            };
            Order targetOrd;
            for (int i = 0; i < targetOrderSlots.Length; i++)
            {
                if (targetOrderSlots[i].TryGetValue(entryKey, out targetOrd) && targetOrd != null)
                {
                    fsm.Targets[i] = targetOrd;
                    if (!string.IsNullOrEmpty(targetOrd.OrderId))
                    {
                        _orderIdToFsmKey[targetOrd.OrderId] = entryKey;
                        ordersIndexed++;
                    }
                }
            }
        }

        /// <summary>
        /// Position Pass Part 1: Finds fleet account with open position but no existing FSM.
        /// Returns null if account already has FSM or has no open position.
        /// </summary>
        private Account RecoverFSM_FindAccountWithPosition()
        {
            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;

                // Do we already have an FSM for this account?
                if (_followerBrackets.Values.Any(f => string.Equals(f.AccountName, acct.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Is there an open position for this instrument in this account?
                Position acctPos = acct.Positions.FirstOrDefault(p =>
                    p.Instrument.FullName == Instrument.FullName && p.MarketPosition != MarketPosition.Flat);
                
                if (acctPos != null)
                    return acct;
            }
            return null;
        }

        /// <summary>
        /// Position Pass Part 2: Scans stopOrders to find entry key belonging to specified account.
        /// Returns (recoveredKey, recoveredStop) or (null, null) if not found.
        /// </summary>
        private void RecoverFSM_ScanStopOrdersForKey(
            Account targetAccount,
            out string recoveredKey,
            out Order recoveredStop)
        {
            recoveredKey = null;
            recoveredStop = null;

            foreach (var stopKvp in stopOrders.ToArray())
            {
                Order stopCand = stopKvp.Value;
                if (stopCand == null) continue;
                if (stopCand.Account == null) continue;

                // If the stop order's original account matches our target account
                if (string.Equals(stopCand.Account.Name, targetAccount.Name, StringComparison.OrdinalIgnoreCase))
                {
                    recoveredKey = stopKvp.Key;
                    recoveredStop = stopCand;
                    break;
                }
            }
        }

        /// <summary>
        /// Position Pass Part 3: Builds FSM for recovered position with terminal entry order.
        /// </summary>
        private FollowerBracketFSM RecoverFSM_BuildRecoveredFSM(
            string recoveredKey,
            Account targetAccount,
            Position acctPos,
            Order recoveredStop)
        {
            var fsm = new FollowerBracketFSM
            {
                AccountName = targetAccount.Name,
                EntryName = recoveredKey,
                State = FollowerBracketState.Active,
                RemainingContracts = Math.Abs(acctPos.Quantity),
                LastUpdateUtc = DateTime.UtcNow,
                EntryOrder = null // Terminal entry order
            };

            // Link stop order
            if (recoveredStop != null)
            {
                fsm.StopOrder = recoveredStop;
            }

            return fsm;
        }

        /// <summary>
        /// Position Pass Part 4: Links target orders to recovered FSM and indexes OrderIds.
        /// </summary>
        private void RecoverFSM_LinkRecoveredBrackets(
            string recoveredKey,
            Order recoveredStop,
            FollowerBracketFSM fsm,
            ref int ordersIndexed)
        {
            // Index stop order ID
            if (recoveredStop != null && !string.IsNullOrEmpty(recoveredStop.OrderId))
            {
                _orderIdToFsmKey[recoveredStop.OrderId] = recoveredKey;
                ordersIndexed++;
            }

            // Link target orders
            var targetOrderSlots = new[]
            {
                target1Orders, target2Orders, target3Orders, target4Orders, target5Orders
            };
            Order tOrd;
            for (int i = 0; i < targetOrderSlots.Length; i++)
            {
                if (targetOrderSlots[i].TryGetValue(recoveredKey, out tOrd) && tOrd != null)
                {
                    fsm.Targets[i] = tOrd;
                    if (!string.IsNullOrEmpty(tOrd.OrderId))
                    {
                        _orderIdToFsmKey[tOrd.OrderId] = recoveredKey;
                        ordersIndexed++;
                    }
                }
            }
        }

        /// <summary>
        /// Position Pass: Handles accounts with open positions but terminal entry orders.
        /// Scans for orphaned positions and reconstructs FSMs from bracket orders.
        /// </summary>
        private void HydrateFSM_RecoverFromOpenPositions(ref int fsmCreated, ref int ordersIndexed)
        {
            int positionFsmCreated = 0;

            while (true)
            {
                Account acct = RecoverFSM_FindAccountWithPosition();
                if (acct == null) break;

                Position acctPos = acct.Positions.FirstOrDefault(p =>
                    p.Instrument.FullName == Instrument.FullName && p.MarketPosition != MarketPosition.Flat);
                if (acctPos == null) break;

                // Scan stopOrders for any entryKey belonging to this account
                string recoveredKey;
                Order recoveredStop;
                RecoverFSM_ScanStopOrdersForKey(acct, out recoveredKey, out recoveredStop);

                if (recoveredKey == null)
                {
                    Print(string.Format(
                        "[SIMA] Phase 5 Position Pass: WARNING -- open position on {0} but no stopOrders key found. FSM not created. REAPER grace window started.",
                        acct.Name));
                    // Build 999: Mark account for REAPER grace window -- defer critical desync up to 10s.
                    _positionPassFailedFirstSeen[acct.Name] = DateTime.UtcNow;
                    break;
                }

                // Idempotent guard
                if (_followerBrackets.ContainsKey(recoveredKey))
                    break;

                var fsm = RecoverFSM_BuildRecoveredFSM(recoveredKey, acct, acctPos, recoveredStop);
                RecoverFSM_LinkRecoveredBrackets(recoveredKey, recoveredStop, fsm, ref ordersIndexed);

                if (_followerBrackets.TryAdd(recoveredKey, fsm))
                {
                    positionFsmCreated++;
                    fsmCreated++;
                    Print(string.Format("[SIMA] Phase 5 Position Pass: Active FSM hydrated for {0} on {1}.",
                        recoveredKey, acct.Name));
                }
                
                break; // Process one account per call to avoid infinite loop
            }

            Print(string.Format("[SIMA] Phase 5 FSM Hydration (Position Pass): {0} Active FSMs created from open positions.",
                positionFsmCreated));
        }

        private void HydrateFSMsFromWorkingOrders()
        {
            int fsmCreated = 0;
            int ordersIndexed = 0;

            foreach (var kvp in entryOrders.ToArray())
            {
                string entryKey = kvp.Key;
                Order entryOrder = kvp.Value;
                if (entryOrder == null) continue;

                // Skip master account entries
                PositionInfo pi;
                if (!activePositions.TryGetValue(entryKey, out pi) || !pi.IsFollower) continue;
                if (pi.ExecutingAccount == null) continue;

                // Idempotent: skip if FSM already exists (safe on repeated reconnects)
                if (_followerBrackets.ContainsKey(entryKey)) continue;

                // Map broker order state to FSM state
                FollowerBracketState hydrationState = HydrateFSM_MapOrderStateToFsmState(entryOrder.OrderState);
                if (hydrationState == FollowerBracketState.None)
                    continue; // Terminal state -- FSM not needed

                int hydratedRemainingContracts = HydrateFSM_DetermineRemainingContracts(
                    entryOrder, hydrationState, pi.ExecutingAccount);

                var fsm = new FollowerBracketFSM
                {
                    AccountName = pi.ExecutingAccount.Name,
                    EntryName = entryKey,
                    State = hydrationState,
                    RemainingContracts = hydratedRemainingContracts,
                    LastUpdateUtc = DateTime.UtcNow,
                    EntryOrder = entryOrder
                };

                // Link bracket orders and index OrderIds
                HydrateFSM_LinkBracketOrders(entryKey, fsm, ref ordersIndexed);

                _followerBrackets.TryAdd(entryKey, fsm);

                if (!string.IsNullOrEmpty(entryOrder.OrderId))
                {
                    _orderIdToFsmKey[entryOrder.OrderId] = entryKey;
                    ordersIndexed++;
                }

                fsmCreated++;
            }

            // Position Pass: handle accounts with open positions but terminal entry orders
            HydrateFSM_RecoverFromOpenPositions(ref fsmCreated, ref ordersIndexed);

            Print(string.Format("[SIMA] Phase 5 FSM Hydration: {0} FSMs created, {1} order IDs indexed.",
                fsmCreated, ordersIndexed));
        }

        /// <summary>
        /// Build 984 [FIX-A]: Sweep and cancel all V12-managed GTC orders before SIMA disable or strategy terminate.
        /// Phase 1 scans tracked order dicts; Phase 2 scans broker order lists for any V12-prefixed orders.
        /// force=true: cancel regardless of open positions (strategy terminate).
        /// force=false: skip accounts that have an open position for this instrument (SIMA disable -- prevent naked accounts).
        /// </summary>
        private void CancelAllV12GtcOrders(bool force)
        {
            int trackedCancels = SweepTrackedOrders(force);
            int brokerCancels  = SweepBrokerOrders(force);
            Print(string.Format("[BUILD 984] GTC sweep: cancelled {0} tracked + {1} broker-scanned orders",
                trackedCancels, brokerCancels));
        }

        /// <summary>Phase 1: cancel orders held in strategy tracking dictionaries.</summary>
        private int SweepTrackedOrders(bool force)
        {
            // Build 990: Semantic separation -- force=false (SIMA disable) cancels only entry orders.
            // Bracket orders (stop/target) are GTC with the broker and must remain to protect live positions.
            // force=true (strategy terminate) cancels all tracked orders.
            var trackedDicts = force
                ? new ConcurrentDictionary<string, Order>[]
                    { entryOrders, stopOrders, target1Orders, target2Orders, target3Orders, target4Orders, target5Orders }
                : new ConcurrentDictionary<string, Order>[]
                    { entryOrders };

            int trackedCancels = 0;
            foreach (var dict in trackedDicts)
            {
                if (dict == null) continue;
                foreach (var kvp in dict.ToArray())
                {
                    Order ord = kvp.Value;
                    if (ord == null) continue;
                    if (ord.OrderState != OrderState.Working    &&
                        ord.OrderState != OrderState.Accepted   &&
                        ord.OrderState != OrderState.Submitted  &&
                        ord.OrderState != OrderState.ChangePending &&
                        ord.OrderState != OrderState.ChangeSubmitted) continue;
                    try
                    {
                        CancelOrderOnAccount(ord, ord.Account);
                        trackedCancels++;
                    }
                    catch (Exception ex)
                    {
                        if (_diagFleet)
                            Print("[FLEET_CATCH] SweepTrackedOrders cancel failed: " + ex.Message);
                    }
                }
            }
            return trackedCancels;
        }

        /// <summary>
        /// Phase 2: broker-level scan to catch V12 orders not held in tracking dicts.
        /// Build 990: Semantic separation -- force=false only targets entry-signal prefixes.
        /// Bracket prefixes (Stop_, S_, T1_-T5_) are excluded on soft disable to protect live positions.
        /// </summary>
        private int SweepBrokerOrders(bool force)
        {
            int brokerCancels = 0;
            // Build 990: Semantic separation -- force=false (SIMA disable) only targets entry-signal prefixes.
            // Bracket prefixes (Stop_, S_, T1_-T5_) are excluded on soft disable to protect live positions.
            var v12Prefixes = force
                ? new[] { "Stop_", "S_", "T1_", "T2_", "T3_", "T4_", "T5_", "Fleet_", "RMA", "Trend", "MOMO", "OR", "RETEST", "FFMA" }
                : new[] { "Fleet_", "RMA", "Trend", "MOMO", "OR", "RETEST", "FFMA" };

            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;
                try
                {
                    foreach (Order ord in acct.Orders.ToArray())
                    {
                        if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                        if (ord.OrderState != OrderState.Working    &&
                        ord.OrderState != OrderState.Accepted   &&
                        ord.OrderState != OrderState.Submitted  &&
                        ord.OrderState != OrderState.ChangePending &&
                        ord.OrderState != OrderState.ChangeSubmitted) continue;
                        
                        string ordName = ord.Name ?? string.Empty;
                        if (!IsV12OrderPrefix(ordName, v12Prefixes)) continue;

                        // [FIX-FF]: Explicit bracket exclusion on soft disable.
                        // Bracket orders protect live positions -- never cancel them during
                        // SIMA disable or soft terminate. Defensive guard against naming drift.
                        if (ShouldProtectBracketOrder(ordName, force, acct.Name)) continue;

                        try { acct.Cancel(new[] { ord }); brokerCancels++; }
                        catch (Exception ex)
                        {
                            if (_diagFleet)
                                Print("[FLEET_CATCH] SweepBrokerOrders per-order cancel failed: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_diagFleet)
                        Print("[FLEET_CATCH] SweepBrokerOrders account iteration failed: " + ex.Message);
                }
            }
            return brokerCancels;
        }

        /// <summary>
        /// Helper: Check if order name matches any V12 prefix.
        /// Extracted from SweepBrokerOrders to reduce cyclomatic complexity.
        /// </summary>
        private bool IsV12OrderPrefix(string orderName, string[] v12Prefixes)
        {
            for (int pi = 0; pi < v12Prefixes.Length; pi++)
            {
                if (orderName.StartsWith(v12Prefixes[pi], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Helper: Determine if bracket order should be protected from cancellation.
        /// Bracket orders (Stop_, S_, T1_-T5_, Target_) protect live positions and must
        /// never be cancelled during soft disable (force=false).
        /// Extracted from SweepBrokerOrders to reduce cyclomatic complexity.
        /// </summary>
        private bool ShouldProtectBracketOrder(string orderName, bool force, string accountName)
        {
            if (force) return false;

            bool isBracketOrder =
                orderName.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase) ||
                orderName.StartsWith("S_", StringComparison.OrdinalIgnoreCase) ||
                orderName.StartsWith("T1_", StringComparison.OrdinalIgnoreCase) ||
                orderName.StartsWith("T2_", StringComparison.OrdinalIgnoreCase) ||
                orderName.StartsWith("T3_", StringComparison.OrdinalIgnoreCase) ||
                orderName.StartsWith("T4_", StringComparison.OrdinalIgnoreCase) ||
                orderName.StartsWith("T5_", StringComparison.OrdinalIgnoreCase) ||
                orderName.StartsWith("Target_", StringComparison.OrdinalIgnoreCase);
            
            if (isBracketOrder)
            {
                Print(string.Format("[FIX-FF] Protected bracket order from sweep: {0} on {1}",
                    orderName, accountName));
                return true;
            }
            return false;
        }


        #endregion
    }
}
