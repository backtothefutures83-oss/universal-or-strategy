// V12 REAPER Audit Module -- Fleet position audit, desync detection, and emergency flatten
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Audit Logic

        private void AuditApexPositions()
        {
            bool shouldLog = (DateTime.UtcNow - lastReaperLog).TotalSeconds >= 30;
            int auditedCount = 0;
            int activeCount = 0;

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    auditedCount++;
                    if (AuditSingleFleetAccount(acct, shouldLog)) activeCount++;
                }
            }

            // V12.12: Explicitly audit the Master account if not covered by the prefix filter.
            bool masterAudited = IsFleetAccount(Account);
            if (!masterAudited)
            {
                auditedCount++;
                if (AuditMasterAccountIfNeeded(shouldLog)) activeCount++;
            }

            if (shouldLog)
            {
                if (activeCount == 0)
                    Print($"[REAPER] Heartbeat: All {auditedCount} accounts flat.");
                else
                    Print($"[REAPER] Heartbeat: {activeCount}/{auditedCount} accounts with positions.");
                lastReaperLog = DateTime.UtcNow;
            }
        }

        // Build 935 [REAPER-B935-003]: Per-account audit logic extracted from AuditApexPositions.
        // Returns true if the account has non-zero state (for heartbeat counter).
        private bool AuditSingleFleetAccount(Account acct, bool shouldLog)
        {
            Position pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
            int actualQty = 0;
            if (pos != null && pos.MarketPosition != MarketPosition.Flat)
                actualQty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;

            // Build 1105: FSM is the SOLE authority for follower expected position.
            var accountFsms = _followerBrackets.Values.Where(f => f.AccountName == acct.Name).ToList();
            int fsmExpectedQty = GetFsmExpectedPosition(acct.Name);

            // Handle hydrated Active FSMs with no order reference (restart edge case)
            foreach (var f in accountFsms)
            {
                if (f.State == FollowerBracketState.Active && f.EntryOrder == null)
                {
                    if (actualQty != 0)
                    {
                        fsmExpectedQty += actualQty;
                    }
                    else
                    {
                        FollowerBracketFSM staleFsm;
                        if (TryTerminateFollowerBracket(f.EntryName, out staleFsm))
                        {
                            Print(string.Format("[REAPER-C7] Stale Active FSM for {0} on {1} (broker flat) -- auto-terminating",
                                f.EntryName, acct.Name));
                        }
                    }
                }
            }

            // Build 999: If Position Pass failed on reconnect but FSM has since been created (replace cycle completed), clear grace.
            if (fsmExpectedQty != 0)
                _positionPassFailedFirstSeen.TryRemove(acct.Name, out _);

            // AUTHORITY: Use FSM state from now on
            string expectedKey = ExpKey(acct.Name);
            int expectedQty = fsmExpectedQty;

            bool syncPending = _dispatchSyncPendingExpKeys.ContainsKey(expectedKey); // [B967-FIX-02]
            // Build 935 [REAPER-B935-002]: Per-account grace prevents Account A fill blocking Account B repair.
            bool inFillGrace = IsReaperFillGraceActive(expectedKey);

            bool hasState = expectedQty != 0 || actualQty != 0;
            if (shouldLog && hasState)
                Print($"[REAPER] {acct.Name}: Expected={expectedQty}, Actual={actualQty}");

            if (expectedQty != actualQty)
            {
                if (actualQty == 0 && expectedQty != 0)
                {
                    // GHOST-FIX-3: Skip repair for Master -- it uses no FollowerBracketFSM -- repair path not applicable.
                    if (acct.Name == Account.Name)
                    {
                        if (shouldLog) Print($"[REAPER] {acct.Name} is the Master account -- skipping follower repair.");
                        return hasState;
                    }

                    if (syncPending || inFillGrace)
                    {
                        if (shouldLog)
                        {
                            string reason = syncPending ? "dispatch sync pending" : "fill grace active";
                            Print($"[REAPER] {acct.Name}: repair deferred ({reason}) while expected={expectedQty}, actual=0.");
                        }
                        return hasState;
                    }

                    string repairKey = acct.Name + "_" + Instrument.FullName;
                    bool alreadyInFlight;
                    alreadyInFlight = _repairInFlight.ContainsKey(repairKey); // [Build 968]

                    if (!alreadyInFlight)
                    {
                        // Phase 4: Use FSM to identify working entry
                        bool hasWorkingEntry = accountFsms.Any(f => f.State == FollowerBracketState.Submitted || f.State == FollowerBracketState.Accepted);

                        if (!hasWorkingEntry)
                        {
                            if (shouldLog) Print($"[REAPER] * REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
                            // A3-2: Mark in-flight BEFORE TriggerCustomEvent to block double-enqueue in next audit cycle (Build 960 audit fix)
                            _repairInFlight.TryAdd(repairKey, 0); // [Build 968]
                            _reaperRepairQueue.Enqueue(acct.Name);
                            // B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
                            try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
                            catch (Exception repairTriggerEx)
                            {
                                _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                                Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                            }
                        }
                    }
                    else if (shouldLog)
                        Print($"[REAPER] {acct.Name} repair already in-flight -- skipping.");

                    return hasState;
                }

                bool isCriticalDesync = (actualQty != 0 && expectedQty == 0)
                    || (Math.Sign(actualQty) != Math.Sign(expectedQty) && expectedQty != 0);

                if (isCriticalDesync)
                {
                    // Build 999: Position Pass grace -- defer critical desync when account failed Phase 5 Position Pass.
                    // Applies only to the case where actualQty!=0 and expectedQty==0 (no FSM created on reconnect).
                    // Does NOT apply when sign mismatch (that is a genuine live desync -- fire immediately).
                    if (actualQty != 0 && expectedQty == 0)
                    {
                        DateTime ppFailedTime;
                        if (_positionPassFailedFirstSeen.TryGetValue(acct.Name, out ppFailedTime))
                        {
                            double graceElapsed = (DateTime.UtcNow - ppFailedTime).TotalSeconds;
                            if (graceElapsed < 10.0)
                            {
                                if (shouldLog)
                                    Print(string.Format("[REAPER] {0}: Position Pass grace ({1:F1}s/10s) -- deferring critical desync. Stop replace in progress.",
                                        acct.Name, graceElapsed));
                                return hasState; // Defer -- check again next audit cycle
                            }
                            // Grace expired -- clear entry and fall through to critical desync
                            _positionPassFailedFirstSeen.TryRemove(acct.Name, out _);
                            Print(string.Format("[REAPER] {0}: Position Pass grace expired ({1:F1}s) -- firing critical desync.",
                                acct.Name, graceElapsed));
                        }
                    }

                    if (shouldLog) Print($"[REAPER] * CRITICAL DESYNC on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                    if (AutoFlattenDesync)
                    {
                        if (shouldLog) Print($"[REAPER] * QUEUING FLATTEN for {acct.Name} - Emergency Re-sync!");
                        _reaperFlattenQueue.Enqueue(acct.Name);
                        try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                    }
                }
                else if (shouldLog)
                    Print($"[REAPER] Minor Desync on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
            }

            // ?? NAKED POSITION AUDIT (Build 1102R) ??????????????????????????????????
            if (actualQty != 0)
            {
                bool hasWorkingStop = acct.Orders.Any(o =>
                    o.Instrument?.FullName == Instrument?.FullName &&
                    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
                    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
                    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));

                if (!hasWorkingStop)
                {
                    DateTime firstSeen;
                    int graceSeconds = (NakedPositionGraceSec > 0) ? NakedPositionGraceSec : 3;
                    if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
                    {
                        _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;
                        Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct naked -- starting {2}s grace window.",
                            acct.Name, actualQty, graceSeconds));
                    }
                    else if ((DateTime.UtcNow - firstSeen).TotalSeconds >= graceSeconds)
                    {
                        bool alreadyNakedInFlight;
                        alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(ExpKey(acct.Name)); // [Build 968]
                        if (!alreadyNakedInFlight)
                        {
                            _reaperNakedStopInFlight.TryAdd(ExpKey(acct.Name), 0); // [Build 968]
                            Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
                            _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                            try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                            catch (Exception tcEx)
                            {
                                _reaperNakedStopInFlight.TryRemove(ExpKey(acct.Name), out _); // [Build 969]
                                Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0}: {1} -- in-flight cleared.", acct.Name, tcEx.Message));
                            }
                        }
                    }
                }
                else
                    _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
            }

            return hasState;
        }

        private void TerminateFsmsForAccount(string accountName)
        {
            foreach (var kvp in _followerBrackets.ToArray())
            {
                FollowerBracketFSM fsm = kvp.Value;
                if (fsm == null || fsm.AccountName != accountName) continue;

                FollowerBracketFSM removedFsm;
                if (TryTerminateFollowerBracket(kvp.Key, out removedFsm))
                {
                    Print(string.Format("[FSM-C3] Terminated FSM {0} for {1} (flatten)", kvp.Key, accountName));
                }
            }
        }

        // Build 935 [REAPER-B935-004]: Audit the Master account when it isn't covered by AccountPrefix.
        // Returns true if the master account has non-zero state.
        private bool AuditMasterAccountIfNeeded(bool shouldLog)
        {
            Position masterPos = Account.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
            int masterActualQty = 0;
            if (masterPos != null && masterPos.MarketPosition != MarketPosition.Flat)
                masterActualQty = masterPos.MarketPosition == MarketPosition.Long ? masterPos.Quantity : -masterPos.Quantity;

            int masterExpectedQty = 0;
            // Build 1102U [BUG-1]: Composite key + stateLock guard.
            expectedPositions.TryGetValue(ExpKey(Account.Name), out masterExpectedQty);

            bool hasState = masterExpectedQty != 0 || masterActualQty != 0;
            if (shouldLog && hasState)
                Print($"[REAPER] {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");

            if (masterExpectedQty != masterActualQty)
            {
                if (masterActualQty == 0 && masterExpectedQty != 0)
                {
                    if (shouldLog) Print($"[REAPER] {Account.Name} (Master) is Flat (Target/Stop hit). Expected was {masterExpectedQty}.");
                }
                else
                {
                    // REAP-01: Suppress critical-desync within ReaperFillGraceTicks of a fresh reservation.
                    long stampTicks = Interlocked.Read(ref _lastExpectedPositionSetTicks);
                    bool inFillGrace = stampTicks > 0 &&
                        (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;

                    bool isCriticalDesync = !inFillGrace &&
                        ((masterActualQty != 0 && masterExpectedQty == 0) ||
                         (Math.Sign(masterActualQty) != Math.Sign(masterExpectedQty) && masterExpectedQty != 0));

                    if (inFillGrace && shouldLog)
                        Print($"[REAPER] {Account.Name} (Master): Fill grace active -- desync check suppressed.");

                    if (isCriticalDesync)
                    {
                        if (shouldLog)
                            Print($"[REAPER] CRITICAL DESYNC on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                        if (AutoFlattenDesync)
                        {
                            if (shouldLog) Print($"[REAPER] QUEUING FLATTEN for {Account.Name} (Master) - Emergency Re-sync!");
                            _reaperFlattenQueue.Enqueue(Account.Name);
                            try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                        }
                    }
                    else if (shouldLog)
                        Print($"[REAPER] Minor Desync on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                }
            }

            // Build 998: Master naked-position audit -- mirrors AuditSingleFleetAccount lines 160-200.
            // AuditMasterAccountIfNeeded previously only checked expectedPositions vs actual.
            // A naked master position (no working stop) was never detected or recovered.
            if (masterActualQty != 0)
            {
                bool masterHasWorkingStop = Account.Orders.Any(o =>
                    o.Instrument?.FullName == Instrument?.FullName &&
                    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
                    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
                    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
                if (!masterHasWorkingStop)
                {
                    DateTime masterFirstSeen;
                    int graceSeconds = (NakedPositionGraceSec > 0) ? NakedPositionGraceSec : 3;
                    if (!_nakedPositionFirstSeen.TryGetValue(Account.Name, out masterFirstSeen))
                    {
                        _nakedPositionFirstSeen[Account.Name] = DateTime.UtcNow;
                        Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct naked -- starting {2}s grace window.",
                            Account.Name, masterActualQty, graceSeconds));
                    }
                    else if ((DateTime.UtcNow - masterFirstSeen).TotalSeconds >= graceSeconds)
                    {
                        bool alreadyNakedInFlight;
                        alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(ExpKey(Account.Name));
                        if (!alreadyNakedInFlight)
                        {
                            _reaperNakedStopInFlight.TryAdd(ExpKey(Account.Name), 0);
                            Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
                            _reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
                            try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                            catch (Exception tcEx)
                            {
                                _reaperNakedStopInFlight.TryRemove(ExpKey(Account.Name), out _);
                                Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0} (Master): {1} -- in-flight cleared.",
                                    Account.Name, tcEx.Message));
                            }
                        }
                    }
                }
                else
                    _nakedPositionFirstSeen.TryRemove(Account.Name, out _);
            }

            return hasState;
        }

        /// <summary>
        /// V12.17 FIX: Processes queued flatten requests on the strategy thread.
        /// Called via TriggerCustomEvent from the Reaper background thread.
        /// This is the SAFE way to call Account.Flatten() -- same pattern as IPC.
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
                            Print($"[REAPER] ? Emergency Market Close: {qty} contracts on {accountName}");
                        }

                        // Build 1004: SetExpectedPositionLocked(0) removed -- FSM termination is the sole teardown.
                        // expectedPositions write is vestigial once FSM is the authority source.
                        TerminateFsmsForAccount(accountName);
                        Print($"[REAPER] ? MARSHAL-FLATTEN (Unmanaged) executed on strategy thread for {accountName}");
                    }
                    else
                    {
                        Print($"[REAPER] [X] Could not find account '{accountName}' for marshal-flatten");
                    }
                }
                catch (Exception ex)
                {
                    Print($"[REAPER] [X] MARSHAL-FLATTEN FAILED for {accountName}: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
