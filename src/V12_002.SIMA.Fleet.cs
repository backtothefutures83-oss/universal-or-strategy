// Build 971: SIMA Fleet -- PumpFleetDispatch, ShouldSkipFleetAccount, UnsubscribeFromFleetAccounts
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
        #region V12 SIMA Fleet

        /// <summary>
        /// V14.2 [ADR-012]: Shared fleet dispatch processing logic.
        /// Called by both Photon ring consumer and legacy ConcurrentQueue consumer.
        /// Guarantees identical invariants on both paths.
        /// </summary>
        /// <param name="poolSlotIndex">Index into PhotonOrderPool. -1 for legacy path (no pool release).</param>
        private void ProcessFleetSlot(Account acct, Order[] orders, int orderCount,
            string fleetEntryName, string expectedKey, int reservedDelta, long signalTicks,
            int poolSlotIndex)
        {
            bool syncCleared = false;
            try
            {
                // Phase 6 [MG-T1]: Reject stale queued dispatch (enqueued > 5s ago)
                if (signalTicks > 0 && !MetadataGuardTimestamp(signalTicks, "Pump:" + fleetEntryName))
                {
                    ClearDispatchSyncPending(expectedKey);
                    syncCleared = true;
                    if (reservedDelta != 0)
                        AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
                    activePositions.TryRemove(fleetEntryName, out _);
                    entryOrders.TryRemove(fleetEntryName, out _);
                    stopOrders.TryRemove(fleetEntryName, out _);
                    for (int tNum = 1; tNum <= 5; tNum++)
                    {
                        var td = GetTargetOrdersDictionary(tNum);
                        if (td != null) td.TryRemove(fleetEntryName, out _);
                    }
                    _followerBrackets.TryRemove(fleetEntryName, out _);
                    Print(string.Format("[PUMP] STALE dispatch rejected for {0} -- rolled back", fleetEntryName));
                    return;
                }

                // Phase 2 [D1]: Initialize FollowerBracketFSM for Shadow Mode
                if (!_followerBrackets.ContainsKey(fleetEntryName))
                {
                    var newFsm = new FollowerBracketFSM
                    {
                        AccountName = acct.Name,
                        EntryName = fleetEntryName,
                        State = FollowerBracketState.Submitted,
                        RemainingContracts = Math.Abs(reservedDelta),
                        LastUpdateUtc = DateTime.UtcNow
                    };

                    // FIX-D2: Use bounded for-loop (pool arrays are MaxOrdersPerSlot=7, may have fewer)
                    for (int i = 0; i < orderCount; i++)
                    {
                        var ord = orders[i];
                        if (ord == null || string.IsNullOrEmpty(ord.Name)) continue;

                        if (ord.Name == fleetEntryName)
                        {
                            newFsm.EntryOrder = ord;
                            newFsm.ExpectedEntryPrice = ord.LimitPrice > 0 ? ord.LimitPrice : 0;
                        }
                        else if (ord.Name.StartsWith("Stop_") || ord.Name.StartsWith("S_"))
                        {
                            newFsm.StopOrder = ord;
                            newFsm.ExpectedStopPrice = ord.StopPrice;
                            newFsm.OcoGroupId = ord.Oco;
                        }
                        else if (ord.Name.StartsWith("T"))
                        {
                            for (int tIdx = 1; tIdx <= 5; tIdx++)
                            {
                                if (ord.Name.StartsWith("T" + tIdx + "_"))
                                {
                                    newFsm.Targets[tIdx - 1] = ord;
                                    newFsm.ExpectedTargetPrices[tIdx - 1] = ord.LimitPrice;
                                    newFsm.OcoGroupId = ord.Oco;
                                    break;
                                }
                            }
                        }
                    }
                    _followerBrackets.TryAdd(fleetEntryName, newFsm);
                }

                Order[] submitOrders = orders;
                if (orders != null && orderCount > 0 && orderCount < orders.Length)
                {
                    submitOrders = new Order[orderCount];
                    Array.Copy(orders, submitOrders, orderCount);
                }

                acct.Submit(submitOrders);
                ClearDispatchSyncPending(expectedKey);
                syncCleared = true;

                // Phase 6 [FSM-P2]: Promote from PendingSubmit to Submitted
                FollowerBracketFSM pFsm;
                if (_followerBrackets.TryGetValue(fleetEntryName, out pFsm)
                    && pFsm != null
                    && pFsm.State == FollowerBracketState.PendingSubmit)
                {
                    pFsm.State = FollowerBracketState.Submitted;
                    pFsm.LastUpdateUtc = DateTime.UtcNow;
                }

                // Phase 3 [Step 3]: Register all order IDs for O(1) FSM lookup
                FollowerBracketFSM fsm;
                if (_followerBrackets.TryGetValue(fleetEntryName, out fsm))
                {
                    for (int i = 0; i < orderCount; i++)
                    {
                        var ord = orders[i];
                        if (ord != null && !string.IsNullOrEmpty(ord.OrderId))
                            _orderIdToFsmKey[ord.OrderId] = fleetEntryName;
                    }
                }

                Print(string.Format("[PUMP] Submitted {0} orders for {1} | {2}",
                    orderCount, fleetEntryName, acct.Name));
            }
            catch (Exception ex)
            {
                Print(string.Format("[PUMP] Submit FAILED for {0} ({1}): {2}",
                    fleetEntryName, acct.Name, ex.Message));
                if (!syncCleared)
                    ClearDispatchSyncPending(expectedKey);
                if (reservedDelta != 0)
                    AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
                activePositions.TryRemove(fleetEntryName, out _);
                entryOrders.TryRemove(fleetEntryName, out _);
                stopOrders.TryRemove(fleetEntryName, out _);
                for (int tNum = 1; tNum <= 5; tNum++)
                {
                    var targetDict = GetTargetOrdersDictionary(tNum);
                    if (targetDict != null)
                        targetDict.TryRemove(fleetEntryName, out _);
                }
                _followerBrackets.TryRemove(fleetEntryName, out _);
            }
            finally
            {
                // V14.2 FIX-D1: Release pool slot if from Photon pool
                if (poolSlotIndex >= 0)
                    _photonPool.ReleaseByIndex(poolSlotIndex);
                Interlocked.Decrement(ref _pendingFleetDispatchCount);
                // Chain next pump -- check BOTH ring and queue (FIX-F7)
                if ((_photonDispatchRing != null && !_photonDispatchRing.IsEmpty)
                    || !_pendingFleetDispatches.IsEmpty)
                    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
            }
        }

        private void PumpFleetDispatch()
        {
            // A3-1: Abort and drain if SIMA disabled or flatten running
            if (isFlattenRunning || !EnableSIMA)
            {
                // v28.0: drain Photon ring FIRST with sideband-aware delta rollback + pool release
                FleetDispatchSlot abortSlot;
                while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out abortSlot))
                {
                    int _sbIdx = abortSlot.PoolSlotIndex;
                    string _expectedKey = (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                        ? _photonSideband[_sbIdx].ExpectedKey
                        : null;
                    if (abortSlot.ReservedDelta != 0 && _expectedKey != null)
                        AddExpectedPositionDeltaLocked(_expectedKey, -abortSlot.ReservedDelta);
                    if (_expectedKey != null)
                        ClearDispatchSyncPending(_expectedKey);
                    if (_sbIdx >= 0)
                    {
                        _photonPool.ReleaseByIndex(_sbIdx);
                        if (_sbIdx < _photonSideband.Length)
                            _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                    }
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                }
                // Then drain legacy ConcurrentQueue
                FleetDispatchRequest stale;
                while (_pendingFleetDispatches.TryDequeue(out stale))
                {
                    if (stale.ReservedDelta != 0)
                        AddExpectedPositionDeltaLocked(stale.ExpectedKey, -stale.ReservedDelta);
                    ClearDispatchSyncPending(stale.ExpectedKey);
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                }
                Print("[PUMP] Abort: SIMA inactive or flatten running. Ring+Queue drained with delta rollback.");
                return;
            }

            // v28.0 [ADR-012 + ADR-016]: Photon ring, XorShadow integrity, sideband refs
            FleetDispatchSlot _ringSlot;
            if (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out _ringSlot))
            {
                int _sbIdx = _ringSlot.PoolSlotIndex;

                // Sideband read (BEFORE shadow verify -- sideband is required for rollback logs)
                FleetDispatchSideband _sb = (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                    ? _photonSideband[_sbIdx]
                    : default(FleetDispatchSideband);

                // XorShadow integrity verification (defense-in-depth, structurally stronger than CRC16)
                ulong _stored   = _ringSlot.Shadow;
                _ringSlot.Shadow = 0UL;                             // zero before recompute (compute excludes Shadow by construction, but this is belt-and-braces)
                ulong _recomputed = ComputeFleetDispatchShadow(ref _ringSlot, _photonShadowSalt);
                _ringSlot.Shadow = _stored;                         // restore for downstream logging
                if (_recomputed != _stored)
                {
                    Interlocked.Increment(ref _photonCrcFailures);
                    Print(string.Format(
                        "[PHOTON_SHADOW] INTEGRITY FAILURE: expected=0x{0:X16} got=0x{1:X16} entry={2} -- SKIPPING",
                        _stored, _recomputed, _sb.FleetEntryName));
                    if (_ringSlot.ReservedDelta != 0 && _sb.ExpectedKey != null)
                        AddExpectedPositionDeltaLocked(_sb.ExpectedKey, -_ringSlot.ReservedDelta);
                    if (_sb.ExpectedKey != null)
                        ClearDispatchSyncPending(_sb.ExpectedKey);
                    if (_sb.FleetEntryName != null)
                    {
                        activePositions.TryRemove(_sb.FleetEntryName, out _);
                        entryOrders.TryRemove(_sb.FleetEntryName, out _);
                        stopOrders.TryRemove(_sb.FleetEntryName, out _);
                        for (int tNum = 1; tNum <= 5; tNum++)
                        {
                            var td = GetTargetOrdersDictionary(tNum);
                            if (td != null) td.TryRemove(_sb.FleetEntryName, out _);
                        }
                        _followerBrackets.TryRemove(_sb.FleetEntryName, out _);
                    }
                    if (_sbIdx >= 0)
                    {
                        _photonPool.ReleaseByIndex(_sbIdx);
                        if (_sbIdx < _photonSideband.Length)
                            _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                    }
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                    if (!_photonDispatchRing.IsEmpty || !_pendingFleetDispatches.IsEmpty)
                        try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
                    return;
                }

                // Valid slot -- retrieve Order[] from pool via PoolSlotIndex
                Order[] ringOrders = _photonPool.GetByIndex(_sbIdx);
                ProcessFleetSlot(_sb.Account, ringOrders, _ringSlot.OrderCount,
                    _sb.FleetEntryName, _sb.ExpectedKey, _ringSlot.ReservedDelta,
                    _ringSlot.SignalTicks, _sbIdx);

                // Clear sideband to release refs (avoid stale retention across ring wraps)
                if (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                    _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                return;
            }

            // Fallback: drain legacy ConcurrentQueue
            FleetDispatchRequest req;
            if (!_pendingFleetDispatches.TryDequeue(out req))
                return;
            ProcessFleetSlot(req.Account, req.Orders, req.Orders.Length,
                req.FleetEntryName, req.ExpectedKey, req.ReservedDelta,
                req.SignalTicks, -1);  // -1 = no pool release
        }

        // Build 935 [SIMA-B935-001]: Skip-logic extracted from ExecuteSmartDispatchEntry fleet loop.
        // Returns true if the account should be skipped for this dispatch cycle.
        // Threading: strategy thread only. stateLock usage identical to original inline code.
        private bool ShouldSkipFleetAccount(Account acct, AccountRankInfo rankInfo,
            System.Collections.Generic.HashSet<string> activeAccountSnapshot, System.Text.StringBuilder dispatchLog)
        {
            // Step 1: Inactive check -- prevents UI toggle race.
            if (!activeAccountSnapshot.Contains(acct.Name))
            {
                dispatchLog.AppendLine(string.Format("[SIMA] {0} SKIPPED (Inactive)", acct.Name));
                return true;
            }

            // Step 2: H-13 stale state reconciliation (Build 1004: FSM-primary, no expectedPositions read).
            try
            {
                // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
                var brokerPos = acct.Positions.ToArray().FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);

                bool hasActiveFsmForAcct = _followerBrackets.Values.Any(f =>
                    f != null
                    && f.AccountName == acct.Name
                    && (f.State == FollowerBracketState.Active
                        || f.State == FollowerBracketState.Accepted
                        || f.State == FollowerBracketState.Submitted
                        || f.State == FollowerBracketState.Replacing));
                bool hasActivePositionForAcct = activePositions.Values.Any(p =>
                    p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name);
                bool hasDispatchPending = _dispatchSyncPendingExpKeys.ContainsKey(ExpKey(acct.Name));

                if (brokerFlat && !hasActiveFsmForAcct && !hasActivePositionForAcct && !hasDispatchPending)
                {
                    // Truly stale: broker flat, no FSM, no position, no dispatch in flight. No-op (nothing to reset).
                    dispatchLog.AppendLine(string.Format("[DISPATCH] H-13: {0} broker flat, no FSM/position/dispatch -- no action", acct.Name));
                }
                else if (brokerFlat && (hasActiveFsmForAcct || hasActivePositionForAcct || hasDispatchPending))
                {
                    dispatchLog.AppendLine(string.Format("[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting",
                        acct.Name, hasActiveFsmForAcct ? "FSM active" : (hasDispatchPending ? "dispatch pending" : "activePos present")));
                }
            }
            catch { }

            // Step 3: Consistency Lock -- skip if daily P&L cap hit.
            if (EnableConsistencyLock && rankInfo.DailyPL >= MaxDailyProfitCap)
            {
                dispatchLog.AppendLine(string.Format("[DISPATCH] {0} SKIPPED - Consistency Lock ({1:C})", acct.Name, rankInfo.DailyPL));
                return true;
            }

            return false;
        }


        /// <summary>
        /// V12.1101E [A-4]: Idempotent unsubscribe -- removes all SIMA event handlers before
        /// re-subscribing. Prevents handler accumulation on repeated SIMA toggle cycles.
        /// V12.Phase6 [UNSUB-TRACK]: Deterministic unsubscribe -- uses tracked set of subscribed accounts
        /// instead of re-scanning Account.All, which may have changed since subscribe time.
        /// </summary>
        private void UnsubscribeFromFleetAccounts()
        {
            // Build 1109 [FREEZE-PROOF]: Snapshot Account.All once to prevent InvalidOperationException
            // if broker reconnects or modifies the collection during iteration.
            Account[] _acctSnapshot = Account.All.ToArray();

            // First: unsubscribe from tracked set (deterministic -- guaranteed to match subscribe)
            foreach (string acctName in _subscribedAccountNames)
            {
                foreach (Account acct in _acctSnapshot)
                {
                    if (acct.Name == acctName)
                    {
                        acct.ExecutionUpdate -= OnAccountExecutionUpdate;
                        acct.OrderUpdate     -= OnAccountOrderUpdate;
                        break;
                    }
                }
            }
            // Fallback: also sweep snapshot for any handlers from untracked subscribe paths
            foreach (Account acct in _acctSnapshot)
            {
                if (IsFleetAccount(acct))
                {
                    acct.ExecutionUpdate -= OnAccountExecutionUpdate;
                    acct.OrderUpdate     -= OnAccountOrderUpdate;
                }
            }
            _subscribedAccountNames.Clear();
        }


        #endregion
    }
}
