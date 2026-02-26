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

        // GHOST-FIX-2 [Build 922Z]: Tracks when an account first appeared as "naked" (position with no working stop).
        // REAPER only fires emergency stop after NakedPositionGraceSec have elapsed, preventing race-condition
        // triggers during the normal bracket-confirmation window immediately after a fill.
        private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen
            = new ConcurrentDictionary<string, DateTime>();

        // [922Z-THROTTLE]: Prevents "Repair BLOCKED" from printing every second during intentional long-sitting orders.
        // Key = blocking order name; Value = last time the message was printed.
        private ConcurrentDictionary<string, DateTime> _repairBlockedLastLogged
            = new ConcurrentDictionary<string, DateTime>();

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
                    // Build 1102U [BUG-1]: Composite key + stateLock guard (reads were previously unguarded on background thread).
                    int expectedQty = 0;
                    lock (stateLock) { expectedPositions.TryGetValue(ExpKey(acct.Name), out expectedQty); }

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
                            // GHOST-FIX-3 [Build 922Z]: Skip repair for the Master chart account.
                            // When AccountPrefix matches the Master account name (e.g. "PA-APEX-422136-06" contains "Apex"),
                            // REAPER incorrectly treats it as a desynced follower and tries to repair it every second.
                            // The Master uses SubmitOrderUnmanaged — there is never a IsFollower=true PositionInfo for it —
                            // so ProcessReaperRepairQueue always prints "No PositionInfo found" and loops forever.
                            if (acct.Name == Account.Name)
                            {
                                if (shouldLog) Print($"[REAPER] {acct.Name} is the Master account — skipping follower repair (uses SubmitOrderUnmanaged path).");
                                continue;
                            }

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
                                            || ordState == OrderState.Accepted
                                            || ordState == OrderState.ChangePending  // 1102Z-C [RR-2a]: Order alive during Account.Change() round-trip — no cancel gap
                                            || ordState == OrderState.Unknown      // V12.Phantom-Fix [FIX-2]: guards T1→T3 race residual (entry in dict before broker ACK)
                                            || ordState == OrderState.Initialized)) // V12.Phantom-Fix [FIX-2]: guards very-new orders before first state transition
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
                                    // [922Z-THROTTLE]: Only log once per 30s per blocking order to avoid Output flood
                                    // during intentional long-sitting limit orders (e.g. test orders far from market).
                                    string throttleKey = blockingOrderName ?? acct.Name;
                                    DateTime lastLogged;
                                    bool shouldLogBlocked = !_repairBlockedLastLogged.TryGetValue(throttleKey, out lastLogged)
                                                            || (DateTime.UtcNow - lastLogged).TotalSeconds >= 30;
                                    if (shouldLogBlocked)
                                    {
                                        _repairBlockedLastLogged[throttleKey] = DateTime.UtcNow;
                                        Print($"[REAPER] Repair BLOCKED by {blockingOrderName} in state {blockingState} (throttled: next log in 30s)");
                                    }
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
                            // GHOST-FIX-2 [Build 922Z]: Grace-delay guard before emergency action.
                            // Bracket orders (stop + targets) take 0–3 seconds to reach the broker after a fill.
                            // Firing immediately causes false EF_ / EMERGENCY_STOP_ during that normal window.
                            // Only trigger after NakedPositionGraceSec seconds of confirmed naked state.
                            DateTime firstSeen;
                            int graceSeconds = (NakedPositionGraceSec > 0) ? NakedPositionGraceSec : 3;
                            if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
                            {
                                // First time we see this account naked — start the grace clock.
                                _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;
                                Print(string.Format(
                                    "[REAPER][NAKED_POSITION] {0}: {1}ct naked — starting {2}s grace window (bracket confirmation delay).",
                                    acct.Name, actualQty, graceSeconds));
                            }
                            else if ((DateTime.UtcNow - firstSeen).TotalSeconds >= graceSeconds)
                            {
                                // Grace window expired — naked state is real and persistent. Fire emergency.
                                // BUG-M2: Dedup guard — prevents duplicate emergency stops across REAPER cycles
                                bool alreadyNakedInFlight;
                                lock (stateLock) { alreadyNakedInFlight = _reaperNakedStopInFlight.Contains(acct.Name); }

                                if (!alreadyNakedInFlight)
                                {
                                    lock (stateLock) { _reaperNakedStopInFlight.Add(acct.Name); }
                                    Print(string.Format(
                                        "[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                        acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
                                    _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                                    // BUG-I2: TriggerCustomEvent wrapped in try/catch (matches repair/flatten pattern)
                                    try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                                    catch (Exception tcEx) { Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed: {0}", tcEx.Message)); }
                                }
                            }
                            // else: still within grace window — wait, do not fire
                        }
                        else
                        {
                            // Position now has a working stop — clear the naked grace clock for this account.
                            // This resets the timer so a future naked episode gets a full fresh grace window.
                            _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
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
                // Build 1102U [BUG-1]: Composite key + stateLock guard.
                lock (stateLock) { expectedPositions.TryGetValue(ExpKey(Account.Name), out masterExpectedQty); }

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
                        // REAP-01: Suppress critical-desync evaluation within ReaperFillGraceTicks of a
                        // fresh expectedPositions reservation. During the broker-fill confirmation lag
                        // (expectedPositions = N, broker actual still = 0), REAPER would otherwise see
                        // a ghost position and queue a spurious flatten of the Master account.
                        long stampTicks = Interlocked.Read(ref _lastExpectedPositionSetTicks);
                        bool inFillGrace = stampTicks > 0 &&
                            (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;

                        bool isCriticalDesync = !inFillGrace &&
                            ((masterActualQty != 0 && masterExpectedQty == 0) ||
                             (Math.Sign(masterActualQty) != Math.Sign(masterExpectedQty) && masterExpectedQty != 0));

                        if (inFillGrace && shouldLog)
                            Print($"[REAPER] {Account.Name} (Master): Fill grace active ({(DateTime.UtcNow.Ticks - stampTicks) / TimeSpan.TicksPerMillisecond}ms elapsed) — desync check suppressed.");

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
                        // Build 1102U [BUG-1]: Composite key for instrument-scoped clear.
                        SetExpectedPositionLocked(ExpKey(accountName), 0);
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
        /// Uses the stored EntryOrderType for symmetrical repairs.
        /// RepairTickFence is enforced only for Market repairs (chasing price).
        /// </summary>
        private void ProcessReaperRepairQueue()
        {
            string accountName;
            while (_reaperRepairQueue.TryDequeue(out accountName))
            {
                string repairKey = accountName + "_" + Instrument.FullName;
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

                    OrderType repairOrderType = repairPos.EntryOrderType;
                    double repairEntryPrice = Instrument.MasterInstrument.RoundToTickSize(repairPos.EntryPrice);
                    double repairLimitPrice = 0;
                    double repairStopPrice = 0;

                    if (repairOrderType == OrderType.Limit)
                    {
                        repairLimitPrice = repairEntryPrice;
                    }
                    else if (repairOrderType == OrderType.StopMarket)
                    {
                        repairStopPrice = repairEntryPrice;
                    }
                    else if (repairOrderType == OrderType.StopLimit)
                    {
                        repairLimitPrice = repairEntryPrice;
                        repairStopPrice = repairEntryPrice;
                    }

                    // 2. Safety Fence: enforce only when repair submits a Market order.
                    if (repairOrderType == OrderType.Market)
                    {
                        double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                        double priceDiff = Math.Abs(currentPrice - repairEntryPrice);
                        double fenceDistance = RepairTickFence * tickSize;

                        if (priceDiff > fenceDistance)
                        {
                            // V12.Phase8.3: Actionable diagnostic — tells trader how to override if needed
                            Print($"[REAPER] REPAIR BLOCKED: Price fence exceeded for {accountName}. " +
                                  $"Current={currentPrice:F2}, Entry={repairEntryPrice:F2}, " +
                                  $"Diff={priceDiff:F4} > Fence={fenceDistance:F4} ({RepairTickFence} ticks). " +
                                  $"Adjust RepairTickFence if you want to force entry.");
                            continue;
                        }
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
                    lock (stateLock) { _repairInFlight.Add(repairKey); }

                    // 5. Re-issue entry order using the SIMA acct.CreateOrder + acct.Submit pattern
                    OrderAction action = repairPos.Direction == MarketPosition.Long
                        ? OrderAction.Buy : OrderAction.SellShort;
                    int quantity = repairPos.TotalContracts;
                    // FIX-C1 [Build 1102Z]: Use the original activePositions key as the NT8 signal name.
                    // The old "Repair_" prefix diverged the signal name from the key, breaking the
                    // identity chain: activePositions key == entryOrders key == order signal name.
                    // OnOrderUpdate.entryOrders.Values.Contains(order) would return false on fill,
                    // SubmitBracketOrders would never be called, and the position would be naked.
                    string repairSignal = repairEntryName;

                    Order repairEntry = targetAcct.CreateOrder(
                        Instrument,
                        action,
                        repairOrderType,
                        TimeInForce.Gtc,
                        quantity,
                        repairLimitPrice,
                        repairStopPrice,
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
                    // Build 1102U [BUG-1]: Composite key + stateLock guard (was unguarded on background thread).
                    int currentExpected = 0;
                    lock (stateLock) { expectedPositions.TryGetValue(ExpKey(accountName), out currentExpected); }
                    if (currentExpected == 0)
                    {
                        Print($"[REAPER REPAIR] ⚠ RACE GUARD ABORT for {accountName}: " +
                              $"expectedPositions cleared to 0 while repair was in queue. Discarding repair order.");
                        lock (stateLock) { _repairInFlight.Remove(repairKey); }
                        continue;
                    }

                    // FIX-C2 [Build 1102Z]: Register repair order under the ORIGINAL activePositions key
                    // and reset BracketSubmitted so OnOrderUpdate will call SubmitBracketOrders on fill.
                    //
                    // Without this:
                    //   1. entryOrders[repairEntryName] still references the OLD (cancelled/missing) entry.
                    //   2. OnOrderUpdate's entryOrders.Values.Contains(repairEntry) returns false.
                    //   3. SubmitBracketOrders is never called → position is naked (no stop, no targets).
                    //
                    // BracketSubmitted = false is required for Market-entry repair: the original bracket
                    // orders were co-submitted at dispatch time and are now gone (account was desync'd).
                    // For Limit/StopMarket repairs it was already false, so this is a safe no-op there.
                    lock (stateLock)
                    {
                        repairPos.BracketSubmitted = false;
                        entryOrders[repairEntryName] = repairEntry;
                    }

                    targetAcct.Submit(new[] { repairEntry });

                    Print($"[REAPER REPAIR] \u2713 Repair order submitted for {accountName} under key={repairEntryName}: " +
                          $"{action} {quantity} {repairOrderType} " +
                          $"{(repairOrderType == OrderType.Market ? "@ Market" : "@ " + repairEntryPrice.ToString("F2"))} " +
                          $"(original entry={repairEntryPrice:F2})");

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
