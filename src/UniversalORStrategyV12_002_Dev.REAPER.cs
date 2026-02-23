// V12.17 THREADING FIX: Reaper (Safety Hub) Module
// REAPER Module (Extracted)
// FIX: acct.Flatten() calls moved from background thread → strategy thread via TriggerCustomEvent
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class UniversalORStrategyV12_002_Dev : Strategy
    {
        #region V12 REAPER Audit Logic

        // V12.17: Queue for flatten requests marshaled from background thread → strategy thread
        private ConcurrentQueue<string> _reaperFlattenQueue = new ConcurrentQueue<string>();

        // V12.Phase8.2: Queue for repair requests marshaled from background thread → strategy thread
        private ConcurrentQueue<string> _reaperRepairQueue = new ConcurrentQueue<string>();
        // V12.Phase8.2: Prevents double-repair for the same account while an order is in-flight
        private readonly HashSet<string> _repairInFlight = new HashSet<string>();

        // Build 1102R: Queue for naked-position emergency stop requests (background → strategy thread)
        private ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)> _reaperNakedStopQueue
            = new ConcurrentQueue<(string, MarketPosition, int)>();
        // Build 1102R: Prevents duplicate emergency stops while broker confirmation is pending (mirrors _repairInFlight)
        private readonly HashSet<string> _reaperNakedStopInFlight = new HashSet<string>();

        /// <summary>
        /// V12 SIMA: Start the Reaper audit background thread
        /// </summary>
        private void StartReaperAudit()
        {
            if (isReaperRunning) return;

            isReaperRunning = true;
            reaperThread = new Thread(ReaperLoop)
            {
                IsBackground = true,
                Name = "V12_Reaper_Audit"
            };
            reaperThread.Start();
            Print("[REAPER] Audit thread STARTED - interval: " + ReaperIntervalMs + "ms");
        }

        /// <summary>
        /// V12 SIMA: Stop the Reaper audit background thread
        /// </summary>
        private void StopReaperAudit()
        {
            if (!isReaperRunning) return;

            isReaperRunning = false;
            try
            {
                if (reaperThread != null && reaperThread.IsAlive)
                {
                    reaperThread.Join(2000); // Wait up to 2 seconds
                }
            }
            catch { }
            Print("[REAPER] Audit thread STOPPED");
        }

        /// <summary>
        /// V12 SIMA: Reaper main loop - audits positions every ReaperIntervalMs
        /// </summary>
        private void ReaperLoop()
        {
            Print("[REAPER] Loop started - monitoring account positions...");

            // V12.Phase8 [F-05]: On cold start or reload the isFlattenRunning guard resets to false
            // even if a broker-side flatten is still in flight from the previous strategy instance.
            // Skip the very first audit cycle to allow any in-flight flatten to settle before
            // Reaper evaluates position state. This prevents false CRITICAL DESYNC on reload.
            bool firstCycle = true;

            while (isReaperRunning)
            {
                try
                {
                    Thread.Sleep(ReaperIntervalMs);
                    if (!isReaperRunning) break;

                    // V12.Phase8 [F-05]: Skip first cycle after startup — grace period for in-flight flattens.
                    if (firstCycle)
                    {
                        firstCycle = false;
                        Print("[REAPER] Startup grace: skipping first audit cycle to allow in-flight flattens to settle.");
                        continue;
                    }

                    // V12.8: Pause auditing while a flatten is actively running to prevent race conditions
                    if (isFlattenRunning) continue;

                    // V12.Hardening: Only audit in live/realtime — skip historical replay
                    if (State != State.Realtime) continue;

                    AuditApexPositions();
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Print("[REAPER] ERROR: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// V12 SIMA: Audit all Apex account positions for desync
        /// If any account has a position that doesn't match expected, log it
        /// </summary>
        private void AuditApexPositions()
        {
            // Throttle logging to once per 30 seconds
            bool shouldLog = (DateTime.Now - lastReaperLog).TotalSeconds >= 30;
            int auditedCount = 0;
            int activeCount = 0;

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    auditedCount++;

                    // Get actual position on this instrument
                    Position pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                    int actualQty = 0;

                    if (pos != null && pos.MarketPosition != MarketPosition.Flat)
                    {
                        actualQty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
                    }

                    // Compare with expected
                    int expectedQty = 0;
                    expectedPositions.TryGetValue(acct.Name, out expectedQty);

                    // V12.9: Only log individual accounts when they have non-zero state (reduces spam)
                    if (shouldLog && (expectedQty != 0 || actualQty != 0))
                    {
                        Print($"[REAPER] {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                        activeCount++;
                    }

                    // Desync detection (V12.1 Path B: Hybrid Recovery)
                    if (expectedQty != actualQty)
                    {
                        // V12.1: Filter Legal Desyncs
                        // If Follower is FLAT but Master is POSITIVE (Expected), this is a "Legal Pull" (Path B target hit).
                        // We do NOT flatten or panic here.
                        if (actualQty == 0 && expectedQty != 0)
                        {
                            // V12.Phase8.2: Follower is FLAT but expected position exists — candidate for auto-repair.
                            // V12.Phase8.3: Repair identity includes instrument to prevent cross-chart collisions
                            string repairKey = acct.Name + "_" + Instrument.FullName;
                            bool alreadyInFlight;
                            lock (stateLock) { alreadyInFlight = _repairInFlight.Contains(repairKey); }

                            if (!alreadyInFlight)
                            {
                                // Check: is there already a working entry order for this account? (no double-entries)
                                // [M8.2 REPAIR-01]: Track blocking order identity for zombie diagnostics.
                                bool hasWorkingEntry     = false;
                                string blockingOrderName  = null;
                                OrderState blockingState  = OrderState.Unknown;

                                foreach (var kvp in entryOrders.ToArray())
                                {
                                    Order ord = kvp.Value;
                                    if (ord == null) continue;

                                    OrderState ordState = ord.OrderState;
                                    // [M8.2 REPAIR-01]: Skip terminal/zombie states — they must never block a
                                    // legitimate repair even if the order object is still in the dictionary.
                                    if (ordState == OrderState.Cancelled || ordState == OrderState.Rejected
                                        || ordState == OrderState.Filled)
                                        continue;

                                    if (activePositions.TryGetValue(kvp.Key, out var pi)
                                        && pi.IsFollower && pi.ExecutingAccount != null
                                        && pi.ExecutingAccount.Name == acct.Name
                                        && (ordState == OrderState.Working
                                            || ordState == OrderState.Submitted
                                            || ordState == OrderState.Accepted))
                                    {
                                        hasWorkingEntry   = true;
                                        blockingOrderName  = string.IsNullOrEmpty(ord.Name) ? kvp.Key : ord.Name;
                                        blockingState      = ordState;
                                        break;
                                    }
                                }

                                if (!hasWorkingEntry)
                                {
                                    if (shouldLog) Print($"[REAPER] \U0001f527 REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
                                    _reaperRepairQueue.Enqueue(acct.Name);
                                    try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); } catch { }
                                }
                                else
                                {
                                    // [M8.2 REPAIR-01]: Always emit blocking identity to expose zombie triggers.
                                    Print($"[REAPER] Repair BLOCKED by {blockingOrderName} in state {blockingState}");
                                }
                            }
                            else
                            {
                                if (shouldLog) Print($"[REAPER] {acct.Name} repair already in-flight — skipping.");
                            }
                            continue;
                        }

                        // CRITICAL: Opposite direction or Ghost position (Active but shouldn't be)
                        bool isCriticalDesync = (actualQty != 0 && expectedQty == 0) || (Math.Sign(actualQty) != Math.Sign(expectedQty) && expectedQty != 0);

                        if (isCriticalDesync)
                        {
                            // V12.8: Throttle CRITICAL DESYNC logging to same shouldLog cadence to prevent output spam
                            if (shouldLog)
                                Print($"[REAPER] 🚨 CRITICAL DESYNC on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");

                            if (AutoFlattenDesync)
                            {
                                if (shouldLog)
                                    Print($"[REAPER] 💀 QUEUING FLATTEN for {acct.Name} - Emergency Re-sync!");
                                // V12.17 FIX: Queue flatten for strategy thread (was: acct.Flatten() on background thread = DEADLOCK)
                                _reaperFlattenQueue.Enqueue(acct.Name);
                                try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                            }
                        }
                        else
                        {
                            // Minor qty mismatch or other non-critical state
                            if (shouldLog) Print($"[REAPER] Minor Desync on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                        }
                    }

                    // ── NAKED POSITION AUDIT (Build 1102R) ──────────────────────────────────
                    // A position is "naked" if the broker holds contracts but has no working stop.
                    // acct.Orders is broker-side — safe to read from REAPER background thread without stateLock.
                    if (actualQty != 0)
                    {
                        bool hasWorkingStop = acct.Orders.Any(o =>
                            o.Instrument?.FullName == Instrument?.FullName &&
                            (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
                            (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
                            (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));

                        if (!hasWorkingStop)
                        {
                            // BUG-M2: Dedup guard — prevents duplicate emergency stops across REAPER cycles
                            bool alreadyNakedInFlight;
                            lock (stateLock) { alreadyNakedInFlight = _reaperNakedStopInFlight.Contains(acct.Name); }

                            if (!alreadyNakedInFlight)
                            {
                                lock (stateLock) { _reaperNakedStopInFlight.Add(acct.Name); }
                                Print(string.Format(
                                    "[REAPER][NAKED_POSITION] {0}: {1} contracts with NO working stop. Queuing emergency hard stop.",
                                    acct.Name, actualQty));
                                _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                                // BUG-I2: TriggerCustomEvent wrapped in try/catch (matches repair/flatten pattern)
                                try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                                catch (Exception tcEx) { Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed: {0}", tcEx.Message)); }
                            }
                        }
                    }
                }
            }

            // V12.12: Explicitly audit the Master account if it was NOT covered by the prefix filter.
            // Bug fix: Master "Sim101" with AccountPrefix "Apex" was invisible to the Reaper.
            bool masterAudited = Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!masterAudited)
            {
                auditedCount++;

                Position masterPos = Account.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                int masterActualQty = 0;
                if (masterPos != null && masterPos.MarketPosition != MarketPosition.Flat)
                {
                    masterActualQty = masterPos.MarketPosition == MarketPosition.Long ? masterPos.Quantity : -masterPos.Quantity;
                }

                int masterExpectedQty = 0;
                expectedPositions.TryGetValue(Account.Name, out masterExpectedQty);

                if (shouldLog && (masterExpectedQty != 0 || masterActualQty != 0))
                {
                    Print($"[REAPER] {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                    activeCount++;
                }

                if (masterExpectedQty != masterActualQty)
                {
                    if (masterActualQty == 0 && masterExpectedQty != 0)
                    {
                        if (shouldLog) Print($"[REAPER] {Account.Name} (Master) is Flat (Target/Stop hit). Expected was {masterExpectedQty}.");
                    }
                    else
                    {
                        bool isCriticalDesync = (masterActualQty != 0 && masterExpectedQty == 0) || (Math.Sign(masterActualQty) != Math.Sign(masterExpectedQty) && masterExpectedQty != 0);

                        if (isCriticalDesync)
                        {
                            if (shouldLog)
                                Print($"[REAPER] CRITICAL DESYNC on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");

                            if (AutoFlattenDesync)
                            {
                                if (shouldLog)
                                    Print($"[REAPER] QUEUING FLATTEN for {Account.Name} (Master) - Emergency Re-sync!");
                                // V12.17 FIX: Queue flatten for strategy thread (was: Account.Flatten() on background thread = DEADLOCK)
                                _reaperFlattenQueue.Enqueue(Account.Name);
                                try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                            }
                        }
                        else
                        {
                            if (shouldLog) Print($"[REAPER] Minor Desync on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                        }
                    }
                }
            }

            if (shouldLog)
            {
                // V12.9: Single summary line instead of 12 "Expected=0, Actual=0" per cycle
                if (activeCount == 0)
                    Print($"[REAPER] Heartbeat: All {auditedCount} accounts flat.");
                else
                    Print($"[REAPER] Heartbeat: {activeCount}/{auditedCount} accounts with positions.");
                lastReaperLog = DateTime.Now;
            }
        }

        /// <summary>
        /// V12.17 FIX: Processes queued flatten requests on the strategy thread.
        /// Called via TriggerCustomEvent from the Reaper background thread.
        /// This is the SAFE way to call Account.Flatten() — same pattern as IPC.
        /// </summary>
        private void ProcessReaperFlattenQueue()
        {
            string accountName;
            while (_reaperFlattenQueue.TryDequeue(out accountName))
            {
                try
                {
                    // Find the account by name
                    Account targetAcct = null;
                    foreach (Account acct in Account.All)
                    {
                        if (acct.Name == accountName)
                        {
                            targetAcct = acct;
                            break;
                        }
                    }

                    // Also check if it's the Master account
                    if (targetAcct == null && Account.Name == accountName)
                        targetAcct = Account;

                    if (targetAcct != null)
                    {
                        // [V12.Phase9] REAPER FIX: Use manual unmanaged close instead of broken targetAcct.Flatten().
                        // 1. Cancel all working orders for this instrument
                        List<Order> ordersToCancel = new List<Order>();
                        foreach (Order order in targetAcct.Orders)
                        {
                            if (order != null && order.Instrument.FullName == Instrument.FullName &&
                                (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
                                 order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending))
                            {
                                ordersToCancel.Add(order);
                            }
                        }
                        if (ordersToCancel.Count > 0)
                        {
                            targetAcct.Cancel(ordersToCancel);
                            Print($"[REAPER] Emergency Cancel: {ordersToCancel.Count} orders on {accountName}");
                        }

                        // 2. Proactively close positions via unmanaged market orders
                        foreach (Position position in targetAcct.Positions)
                        {
                            if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat) continue;
                            
                            int qty = position.Quantity;
                            string signalName = "ReaperFlatten_" + position.MarketPosition.ToString();
                            
                            if (targetAcct == this.Account)
                            {
                                // Master Account
                                if (position.MarketPosition == MarketPosition.Long)
                                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qty, 0, 0, "", signalName);
                                else
                                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", signalName);
                            }
                            else
                            {
                                // Fleet Account
                                OrderAction closeAction = position.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                                Order closeOrder = targetAcct.CreateOrder(Instrument, closeAction, OrderType.Market, TimeInForce.Gtc, qty, 0, 0, "", signalName, null);
                                targetAcct.Submit(new[] { closeOrder });
                            }
                            Print($"[REAPER] ✓ Emergency Market Close: {qty} contracts on {accountName}");
                        }

                        // V12.1101E [F-06]: Serialize expectedPositions mutation under stateLock.
                        SetExpectedPositionLocked(accountName, 0);
                        Print($"[REAPER] ✓ MARSHAL-FLATTEN (Unmanaged) executed on strategy thread for {accountName}");
                    }
                    else
                    {
                        Print($"[REAPER] ✗ Could not find account '{accountName}' for marshal-flatten");
                    }
                }
                catch (Exception ex)
                {
                    Print($"[REAPER] ✗ MARSHAL-FLATTEN FAILED for {accountName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// V12.Phase8.2: Processes queued repair requests on the strategy thread.
        /// Re-issues the original entry order for a desynced follower account.
        /// Safety: market price must be within RepairTickFence ticks of original EntryPrice.
        /// Uses Market order (user decision: priority = fleet sync, accept slippage).
        /// </summary>
        private void ProcessReaperRepairQueue()
        {
            string accountName;
            while (_reaperRepairQueue.TryDequeue(out accountName))
            {
                try
                {
                    // 1. Find the stored PositionInfo for this account in activePositions
                    PositionInfo repairPos = null;
                    string repairEntryName = null;
                    foreach (var kvp in activePositions.ToArray())
                    {
                        PositionInfo pi = kvp.Value;
                        if (pi.IsFollower && pi.ExecutingAccount != null
                            && pi.ExecutingAccount.Name == accountName)
                        {
                            repairPos = pi;
                            repairEntryName = kvp.Key;
                            break;
                        }
                    }

                    if (repairPos == null)
                    {
                        Print($"[REAPER REPAIR] \u2717 No PositionInfo found for {accountName} — cannot repair.");
                        continue;
                    }

                    // 2. Safety Fence: current market price must be within RepairTickFence ticks of original entry
                    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                    double priceDiff = Math.Abs(currentPrice - repairPos.EntryPrice);
                    double fenceDistance = RepairTickFence * tickSize;

                    if (priceDiff > fenceDistance)
                    {
                        // V12.Phase8.3: Actionable diagnostic — tells trader how to override if needed
                        Print($"[REAPER] REPAIR BLOCKED: Price fence exceeded for {accountName}. " +
                              $"Current={currentPrice:F2}, Entry={repairPos.EntryPrice:F2}, " +
                              $"Diff={priceDiff:F4} > Fence={fenceDistance:F4} ({RepairTickFence} ticks). " +
                              $"Adjust RepairTickFence if you want to force entry.");
                        continue;
                    }

                    // 3. Resolve account object
                    Account targetAcct = repairPos.ExecutingAccount;
                    if (targetAcct == null)
                    {
                        Print($"[REAPER REPAIR] \u2717 ExecutingAccount is null for {accountName}");
                        continue;
                    }

                    // 4. Mark in-flight to prevent double-repair
                    // V12.Phase8.3: Key includes instrument for cross-chart uniqueness
                    string repairKey = accountName + "_" + Instrument.FullName;
                    lock (stateLock) { _repairInFlight.Add(repairKey); }

                    // 5. Re-issue entry order using the SIMA acct.CreateOrder + acct.Submit pattern
                    OrderAction action = repairPos.Direction == MarketPosition.Long
                        ? OrderAction.Buy : OrderAction.SellShort;
                    int quantity = repairPos.TotalContracts;
                    string repairSignal = "Repair_" + repairPos.SignalName;

                    Order repairEntry = targetAcct.CreateOrder(
                        Instrument,
                        action,
                        OrderType.Market,
                        TimeInForce.Gtc,
                        quantity,
                        0, 0,
                        "",
                        repairSignal,
                        null);

                    if (repairEntry == null)
                    {
                        Print($"[REAPER REPAIR] \u2717 CreateOrder returned null for {accountName}");
                        lock (stateLock) { _repairInFlight.Remove(repairKey); }
                        continue;
                    }

                    // NOTE: expectedPositions are ALREADY preserved from Phase 8.1 (the original
                    // dispatch reserved them and the Ghost Fix intentionally kept them). Do NOT
                    // call AddExpectedPositionDeltaLocked here — that would double-count.

                    // V12.Phase8.2 [RACE-GUARD]: Re-verify expectedPositions immediately before
                    // order submission. A manual flatten may have zeroed expectedPositions while
                    // this repair was sitting in the queue — submitting into a flat account would
                    // create a "Phantom Repair" (ghost long/short with no corresponding Risk stop).
                    int currentExpected = 0;
                    expectedPositions.TryGetValue(accountName, out currentExpected);
                    if (currentExpected == 0)
                    {
                        Print($"[REAPER REPAIR] ⚠ RACE GUARD ABORT for {accountName}: " +
                              $"expectedPositions cleared to 0 while repair was in queue. Discarding repair order.");
                        lock (stateLock) { _repairInFlight.Remove(repairKey); }
                        continue;
                    }

                    targetAcct.Submit(new[] { repairEntry });

                    Print($"[REAPER REPAIR] \u2713 Repair order submitted for {accountName}: " +
                          $"{action} {quantity} @ Market (original entry={repairPos.EntryPrice:F2})");

                    // 6. Clear DESYNC chart label for this account (defensive — label may or may not exist)
                    try
                    {
                        string desyncTag = "SIMA_DESYNC_" + accountName;
                        RemoveDrawObject(desyncTag);
                    }
                    catch { }

                    // 7. Clear in-flight flag (order is now submitted; execution callbacks manage state)
                    lock (stateLock) { _repairInFlight.Remove(repairKey); }
                }
                catch (Exception ex)
                {
                    Print($"[REAPER REPAIR] \u2717 FAILED for {accountName}: {ex.Message}");
                    // Roll back in-flight flag so retry is possible on next audit cycle
                    lock (stateLock) { _repairInFlight.Remove(repairKey); }
                }
            }
        }

        /// <summary>
        /// Build 1102R: Processes queued naked-position emergency stop requests on the strategy thread.
        /// Called via TriggerCustomEvent from the Reaper background thread.
        /// Submits a StopMarket order at MaximumStop ticks from current close to protect the naked position.
        /// </summary>
        private void ProcessReaperNakedStopQueue()
        {
            while (_reaperNakedStopQueue.TryDequeue(out var item))
            {
                try
                {
                    Account acct = Account.All.FirstOrDefault(a => a.Name == item.AccountName);
                    if (acct == null)
                    {
                        Print(string.Format("[REAPER][NAKED_STOP] Account {0} not found — skipping.", item.AccountName));
                        continue;
                    }

                    // Compute emergency stop price: MaximumStop ticks from current close.
                    // Close[0] is safe here — ProcessReaperNakedStopQueue runs on strategy thread
                    // via TriggerCustomEvent.
                    double stopPrice;
                    OrderAction closeAction;

                    if (item.Direction == MarketPosition.Long)
                    {
                        stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] - MaximumStop);
                        closeAction = OrderAction.Sell;
                    }
                    else
                    {
                        stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] + MaximumStop);
                        closeAction = OrderAction.BuyToCover;
                    }

                    string signalName = "EMERGENCY_STOP_" + item.AccountName;
                    Order emergencyStop = acct.CreateOrder(
                        Instrument, closeAction, OrderType.StopMarket,
                        TimeInForce.Gtc, item.Qty,
                        0, stopPrice, "", signalName, null);

                    acct.Submit(new[] { emergencyStop });

                    // BUG-M2: Clear in-flight guard after successful submission
                    lock (stateLock) { _reaperNakedStopInFlight.Remove(item.AccountName); }
                    Print(string.Format(
                        "[REAPER][EMERGENCY_STOP] Submitted StopMarket for {0}: {1} {2}ct @ {3:F2}",
                        item.AccountName, closeAction, item.Qty, stopPrice));
                }
                catch (Exception ex)
                {
                    // BUG-M2: Clear in-flight guard on failure so next cycle can retry
                    lock (stateLock) { _reaperNakedStopInFlight.Remove(item.AccountName); }
                    Print(string.Format("[REAPER][EMERGENCY_STOP_FAIL] {0}: {1}", item.AccountName, ex.Message));
                }
            }
        }

        #endregion
    }
}
