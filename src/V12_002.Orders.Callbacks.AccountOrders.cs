// Build 971: Orders.Callbacks.AccountOrders -- OnAccountOrderUpdate, ProcessAccountOrderQueue, TryFindOrderInPosition, HandleMatchedFollowerOrder, ExecuteFollowerCascadeCleanup, ProcessQueuedAccountOrder
// V12 Orders.Callbacks Module (Extracted)
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
    public partial class V12_002 : Strategy
    {
        #region Orders Callbacks Account Orders

        private void OnAccountOrderUpdate(object sender, OrderEventArgs e)
        {
            if (e == null || e.Order == null) return;

            Order order = e.Order;
            if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

            if (order.OrderState != OrderState.Cancelled && order.OrderState != OrderState.Rejected &&
                order.OrderState != OrderState.Unknown)
            {
                return;
            }

            // V12.1101E [TM-01]: Marshal broker-thread callback to strategy thread before mutating strategy state.
            _accountOrderQueue.Enqueue(new QueuedAccountOrderUpdate
            {
                Account = sender as Account,
                EventArgs = e
            });
            try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
        }

        // Build 935 [R-02]: Cap per-drain budget to prevent strategy-thread starvation
        // under high-velocity broker event bursts. Mirrors IpcMaxCommandsPerDrain pattern.
        private const int MaxAccountOrdersPerDrain = 8;

        private void ProcessAccountOrderQueue()
        {
            // V12.Phase7 [THREAD-01a]: Buffer-and-wait during flatten (symmetric with ProcessAccountExecutionQueue).
            if (isFlattenRunning)
            {
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
                return;
            }

            int drainedCount = 0;
            QueuedAccountOrderUpdate item;
            while (drainedCount < MaxAccountOrdersPerDrain && _accountOrderQueue.TryDequeue(out item))
            {
                if (isFlattenRunning)
                {
                    _accountOrderQueue.Enqueue(item);
                    try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
                    return;
                }
                drainedCount++;
                ProcessQueuedAccountOrder(item);
            }
            // If items remain after budget exhausted, reschedule for next strategy-thread slice.
            if (!_accountOrderQueue.IsEmpty)
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
        }

        // Build 935 [R-01]: Returns true if 'order' belongs to 'entryKey' position.
        // Encapsulates the 7-way compound OR so the outer search loop stays trivial.
        private bool TryFindOrderInPosition(Order order, string entryKey, out string matchedEntry)
        {
            matchedEntry = null;
            if ((entryOrders.TryGetValue(entryKey,   out var eOrder)  && (eOrder  == order || (eOrder  != null && eOrder.OrderId  == order.OrderId))) ||
                (stopOrders.TryGetValue(entryKey,    out var sOrder)  && (sOrder  == order || (sOrder  != null && sOrder.OrderId  == order.OrderId))) ||
                (target1Orders.TryGetValue(entryKey, out var t1Order) && (t1Order == order || (t1Order != null && t1Order.OrderId == order.OrderId))) ||
                (target2Orders.TryGetValue(entryKey, out var t2Order) && (t2Order != null && t2Order.OrderId == order.OrderId)) ||
                (target3Orders.TryGetValue(entryKey, out var t3Order) && (t3Order != null && t3Order.OrderId == order.OrderId)) ||
                (target4Orders.TryGetValue(entryKey, out var t4Order) && (t4Order != null && t4Order.OrderId == order.OrderId)) ||
                (target5Orders.TryGetValue(entryKey, out var t5Order) && (t5Order != null && t5Order.OrderId == order.OrderId)))
            {
                matchedEntry = entryKey;
                return true;
            }
            return false;
        }

        // Build 935 [R-01]: Handles a follower order positively matched to an active position.
        // Entry-not-filled -> rollback + desync label. Entry-filled or stop/target -> ghost log + cleanup.
        private void HandleMatchedFollowerOrder(string matchedEntry, PositionInfo matchedPos, Order order, string acctName, string reason)
        {
            if (entryOrders.TryGetValue(matchedEntry, out var entryOrder) &&
                (entryOrder == order || (entryOrder != null && entryOrder.OrderId == order.OrderId)) &&
                !matchedPos.EntryFilled)
            {
                entryOrders.TryRemove(matchedEntry, out _);
                int gfExp = 0;
                expectedPositions.TryGetValue(ExpKey(acctName), out gfExp);
                if (gfExp == 0)
                {
                    // Build 947: clean up any in-flight FSM spec to avoid orphaned state
                    _followerReplaceSpecs.TryRemove(matchedEntry, out _);
                    return;
                }

                // Build 947 FSM: if this cancel was our PendingCancel, submit replacement instead of DESYNC
                FollowerReplaceSpec fsm;
                if (_followerReplaceSpecs.TryGetValue(matchedEntry, out fsm)
                    && fsm.State == FollowerReplaceState.PendingCancel
                    && fsm.CancellingOrderId == order.OrderId)
                {
                    // Fill-during-gap guard: if master already has a live filled position, let REAPER handle
                    PositionInfo masterPos;
                    bool masterFilled = !string.IsNullOrEmpty(fsm.MasterSignalName)
                        && activePositions.TryGetValue(fsm.MasterSignalName, out masterPos)
                        && masterPos != null
                        && masterPos.EntryFilled
                        && masterPos.RemainingContracts > 0;

                    if (masterFilled)
                    {
                        Print("[FSM] Master filled during cancel wait -- routing "
                            + fsm.SignalName + " to repair instead of replace.");
                        _followerReplaceSpecs.TryRemove(fsm.SignalName, out _);
                        return;
                    }

                    // A1-3: Snapshot qty/price and transition state atomically under stateLock to close TOCTOU window.
                    // PropagateFollowerEntryReplace can update PendingQty/PendingPrice inside
                // while OnAccountOrderUpdate (background thread) reads them here. Without the lock,
                // the snapshot and state transition can observe torn state. (Build 960 audit fix)
                int    qty;
                double price;
                string acctNameCapture;
                string sigName;
                FollowerReplaceSpec fsmCapture;
                // V12.962 ACTOR: Direct field reads -- lock-free, serialized by _drainToken
                qty             = fsm.PendingQty;
                price           = fsm.PendingPrice;
                acctNameCapture = fsm.AccountName;
                sigName         = fsm.SignalName;
                fsmCapture      = fsm;
                fsm.State       = FollowerReplaceState.Submitting;

                try
                {
                    TriggerCustomEvent(o =>
                    {
                        // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                        // ATR tick absorption may have updated PendingPrice/PendingQty after the
                        // lambda was scheduled -- using stale captures would submit wrong values.
                        SubmitFollowerReplacement(sigName, acctNameCapture, fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }, null);
                }
                catch (Exception ex)
                {
                    Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                    _followerReplaceSpecs.TryRemove(sigName, out _);
                }
                } // END of PendingCancel block

                // B957/C1: Check for follower TARGET replace FSM spec before doing delta rollback.
                // If this cancel was part of a two-phase target replacement, submit the new order
                // and return -- no delta rollback needed (position remains open, just target moved).
                {
                    FollowerTargetReplaceSpec tSpec = null;
                    string tFsmMatchKey = null;
                    foreach (var tKvp in _followerTargetReplaceSpecs.ToArray())
                    {
                        if (tKvp.Value.CancellingOrderId == order.OrderId)
                        {
                            tSpec = tKvp.Value;
                            tFsmMatchKey = tKvp.Key;
                            break;
                        }
                    }
                    if (tSpec != null && tFsmMatchKey != null)
                    {
                        _followerTargetReplaceSpecs.TryRemove(tFsmMatchKey, out _);
                        FollowerTargetReplaceSpec captured = tSpec;
                        string capturedKey = tFsmMatchKey;
                        try
                        {
                            TriggerCustomEvent(o => SubmitFollowerTargetReplacement(capturedKey, captured), null);
                        }
                        catch (Exception tFsmEx)
                        {
                            Print("[FSM_TGT] TriggerCustomEvent failed for " + capturedKey + ": " + tFsmEx.Message);
                        }
                        return; // FSM-controlled target cancel -- skip delta rollback, not a real desync
                    }
                }

                // A2-3: Direction-aware delta rollback on CONFIRMED cancel -- deferred from SymmetryGuardCascadeFollowerCleanup
                // to prevent REAPER desync on microsecond fill race (Build 960 audit fix).
                PositionInfo cancelledFollowerPos;
                if (activePositions.TryGetValue(matchedEntry, out cancelledFollowerPos) && cancelledFollowerPos != null)
                {
                    string cancelAcctKey = cancelledFollowerPos.ExecutingAccount != null
                        ? cancelledFollowerPos.ExecutingAccount.Name : Account.Name;
                    int cancelDelta = (cancelledFollowerPos.Direction == MarketPosition.Long)
                        ? -cancelledFollowerPos.TotalContracts : cancelledFollowerPos.TotalContracts;
                    DeltaExpectedPositionLocked(ExpKey(cancelAcctKey), cancelDelta);
                    // B957/D2: Release the SIMA dispatch-sync barrier for this account. Without this, the barrier
                    // remains permanently blocked after a follower cancel, starving future dispatches.
                    _dispatchSyncPendingExpKeys.TryRemove(ExpKey(cancelAcctKey), out _); // [B967-FIX-02]
                }
                Print(string.Format("[SIMA] Follower entry cancelled: {0} on {1}. Reaper monitoring.", matchedEntry, acctName));
                Draw.TextFixed(this, "SIMA_DESYNC_" + acctName, "(!) FOLLOWER DESYNC: " + acctName, TextPosition.TopLeft, Brushes.Red, new SimpleFont("Arial", 11), Brushes.Transparent, Brushes.Transparent, 50);
            }
            else
            {
                // Build 950: Follower stop replacement -- mirrors HandleOrderCancelled master path.
                // Follower stop cancels arrive via OnAccountOrderUpdate (not OnOrderUpdate), so
                // HandleOrderCancelled never fires for them. Match pendingStopReplacements here.
                // This block is in the else branch because stop orders are not in entryOrders.
                if (order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_"))
                {
                    foreach (var _psr in pendingStopReplacements.ToArray())
                    {
                        if (_psr.Value.OldOrder == order)
                        {
                            PositionInfo _rPos;
                            // Build 955: Move guard inside lock -- check and use same atomic snapshot.
                            if (activePositions.TryGetValue(_psr.Key, out _rPos))
                            {
                                int _rQty;
                                _rQty = _rPos.RemainingContracts;
                                if (_rQty > 0)
                                {
                                    CreateNewStopOrder(_psr.Key, _rQty, _psr.Value.StopPrice, _psr.Value.Direction);
                                    if (_psr.Value.BracketRestorationNeeded && _psr.Value.CapturedTargets != null)
                                    {
                                        TargetSnapshot[] _snap = _psr.Value.CapturedTargets;
                                        string _rKey = _psr.Key;
                                        TriggerCustomEvent(o => RestoreCascadedTargets(_rKey, _snap), null);
                                    }
                                } // if (_rQty > 0)
                            } // if (activePositions.TryGetValue)
                            if (pendingStopReplacements.TryRemove(_psr.Key, out _)) Interlocked.Decrement(ref pendingReplacementCount);
                            return;
                        }
                    }
                }
                // A2-2: Deferred PendingCleanup purge -- follower stop terminal (Build 960 audit fix).
                if (order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_"))
                {
                    foreach (var _sc in stopOrders.ToArray())
                    {
                        if (_sc.Value == order)
                        {
                            PositionInfo _scPos;
                            if (activePositions.TryGetValue(_sc.Key, out _scPos) && _scPos != null
                                && _scPos.PendingCleanup && _scPos.RemainingContracts <= 0)
                            {
                                stopOrders.TryRemove(_sc.Key, out _);
                                activePositions.TryRemove(_sc.Key, out _);
                                SymmetryGuardForgetEntry(_sc.Key);
                                Print("[A2-2] Deferred PendingCleanup purge (follower stop terminal): " + _sc.Key);
                            }
                            break;
                        }
                    }
                }

                Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, order.OrderId));
                RemoveGhostOrderRef(order, reason);
            }
        }

        // Build 935 [R-01]: SIMA cascade cleanup for unmatched master-cancel events.
        // Receives pre-computed snapshot -- eliminates the second activePositions.ToArray() allocation.
        private void ExecuteFollowerCascadeCleanup(bool enableSima, Order order, string reason, KeyValuePair<string, PositionInfo>[] snapshot)
        {
            // V12.18 SIMA CASCADE: If a master-account order was cancelled,
            // check if any follower positions share the same base signal and tear them down.
            if (enableSima && order.OrderState == OrderState.Cancelled && order.Account == this.Account)
            {
                string orderSignal = order.Name;
                foreach (var kvp in snapshot)
                {
                    PositionInfo cascadePos = kvp.Value;
                    if (!cascadePos.IsFollower) continue;
                    if (kvp.Key.Contains(orderSignal) || orderSignal.Contains(kvp.Key))
                    {
                        string cascadeAcctName = cascadePos.ExecutingAccount != null ? cascadePos.ExecutingAccount.Name : "NULL";
                        if (!cascadePos.EntryFilled)
                        {
                            Print(string.Format("[GHOST_FIX] SIMA CASCADE: Master cancel of {0} triggers follower teardown for {1} on {2}",
                                orderSignal, kvp.Key, cascadeAcctName));
                            CleanupPosition(kvp.Key);

                            if (cascadePos.ExecutingAccount != null)
                            {
                                int rollbackDelta = (cascadePos.Direction == MarketPosition.Long) ? -cascadePos.TotalContracts : cascadePos.TotalContracts;
                                int currentExp = 0;
                                expectedPositions.TryGetValue(ExpKey(cascadeAcctName), out currentExp);
                                if (currentExp == 0)
                                {
                                    Print(string.Format("[GHOST_FIX] SKIP cascade delta for {0}: expectedPositions already 0 (purge-race guard). Delta suppressed.",
                                        cascadeAcctName));
                                }
                                else
                                {
                                    DeltaExpectedPositionLocked(ExpKey(cascadeAcctName), rollbackDelta);
                                }
                                ClearDispatchSyncPending(ExpKey(cascadeAcctName));
                                try { RemoveDrawObject("SIMA_DESYNC_" + cascadeAcctName); } catch { }
                            }
                        }
                        else
                        {
                            Print(string.Format("[DEAD-01] CASCADE-FILLED: Master cancel {0} -- follower {1} on {2} is FILLED. Issuing emergency flatten.",
                                orderSignal, kvp.Key, cascadeAcctName));
                            if (cascadePos.ExecutingAccount != null)
                            {
                                Account filledFollowerAcct = cascadePos.ExecutingAccount;
                                TriggerCustomEvent(o => EmergencyFlattenSingleFleetAccount(filledFollowerAcct), null);
                            }
                        }
                    }
                }
            }
            RemoveGhostOrderRef(order, reason);
        }

        private void ProcessQueuedAccountOrder(QueuedAccountOrderUpdate item)
        {
            if (item.EventArgs == null || item.EventArgs.Order == null) return;
            Order order = item.EventArgs.Order;
            if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

            string reason = order.OrderState.ToString().ToUpper();
            string acctName = item.Account != null ? item.Account.Name : "UNKNOWN";
            Print(string.Format("[GHOST-AUDIT] OnAccountOrderUpdate: {0} | State={1} | Acct={2}", order.Name, reason, acctName));

            // Build 935 [R-01]: Single snapshot -- reused by both identity search and cascade cleanup,
            // eliminating the second activePositions.ToArray() allocation in the cascade path.
            var snapshot = activePositions.ToArray();

            string matchedEntry = null;
            PositionInfo matchedPos = null;
            foreach (var kvp in snapshot)
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                PositionInfo pos = kvp.Value;
                if (!pos.IsFollower || pos.ExecutingAccount == null || pos.ExecutingAccount != item.Account) continue;
                if (TryFindOrderInPosition(order, kvp.Key, out matchedEntry))
                {
                    matchedPos = pos;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(matchedEntry) && matchedPos != null && activePositions.ContainsKey(matchedEntry))
                HandleMatchedFollowerOrder(matchedEntry, matchedPos, order, acctName, reason);
            else
                ExecuteFollowerCascadeCleanup(EnableSIMA, order, reason, snapshot);
        }


        #endregion
    }
}
