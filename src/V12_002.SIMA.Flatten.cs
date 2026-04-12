// Build 971: SIMA Flatten -- FlattenAllApexAccounts, EmergencyFlattenSingleFleetAccount, ClosePositionsOnlyApexAccounts
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
        #region V12 SIMA Flatten

        private void FlattenAllApexAccounts()
        {
            if (!EnableSIMA)
            {
                Print("[SIMA] DISABLED - Using single-account flatten");
                FlattenAll();
                return;
            }

            isFlattenRunning = true;
            Print("[SIMA] ====== GLOBAL FLATTEN START (CHUNKED) ======");

            // Phase 1: Snapshot + enqueue (zero broker calls on this cycle)
            Account[] snapshot = Account.All.ToArray();
            int enqueued = 0;
            foreach (Account acct in snapshot)
            {
                if (IsFleetAccount(acct))
                {
                    _pendingFlattenOps.Enqueue(new FlattenWorkItem
                    {
                        Account = acct, CancelOnly = false, ZombieSweepOnly = false,
                        IsMaster = false, Source = "FlattenAll"
                    });
                    enqueued++;
                }
            }

            // Master account fallback (if not covered by AccountPrefix)
            bool masterCovered = IsFleetAccount(Account);
            if (!masterCovered)
            {
                _pendingFlattenOps.Enqueue(new FlattenWorkItem
                {
                    Account = Account, CancelOnly = false, ZombieSweepOnly = false,
                    IsMaster = true, Source = "FlattenAll_Master"
                });
                enqueued++;
            }

            Print(string.Format("[SIMA] Enqueued {0} account(s) for chunked flatten", enqueued));

            // Kick the pump -- one account per strategy-thread cycle
            if (!_pendingFlattenOps.IsEmpty)
                try { TriggerCustomEvent(o => PumpFlattenOps(), null); } catch { }
            else
            {
                isFlattenRunning = false;
                Print("[SIMA] ====== GLOBAL FLATTEN COMPLETE (no accounts matched) ======");
            }
        }

        /// <summary>
        /// Build 1109 [FREEZE-PROOF]: Processes ONE fleet account flatten per TriggerCustomEvent cycle.
        /// Strategy thread yields between accounts, preventing multi-second freezes.
        /// Mirrors PumpFleetDispatch pattern from SIMA.Dispatch.cs.
        /// </summary>
        private void PumpFlattenOps()
        {
            FlattenWorkItem item;
            if (!_pendingFlattenOps.TryDequeue(out item))
            {
                isFlattenRunning = false;
                Print("[SIMA] ====== GLOBAL FLATTEN COMPLETE (CHUNKED) ======");
                return;
            }

            try
            {
                Account acct = item.Account;
                if (acct == null)
                {
                    Print("[FLATTEN_PUMP] NULL account in queue -- skipping");
                    return;
                }

                // Step 1: Cancel all working orders for this instrument
                List<Order> ordersToCancel = new List<Order>();
                foreach (Order order in acct.Orders.ToArray())
                {
                    if (order == null || order.Instrument == null) continue;
                    if (order.Instrument.FullName != Instrument.FullName) continue;

                    bool isTerminal = order.OrderState == OrderState.Cancelled
                        || order.OrderState == OrderState.CancelPending
                        || order.OrderState == OrderState.CancelSubmitted
                        || order.OrderState == OrderState.Filled
                        || order.OrderState == OrderState.Rejected;
                    if (isTerminal) continue;

                    if (item.ZombieSweepOnly)
                    {
                        // ClosePositionsOnly: Only sweep EMERGENCY_STOP_ and T1_-T5_ (zombie targets)
                        bool isZombieTarget =
                            order.Name.StartsWith("EMERGENCY_STOP_", StringComparison.OrdinalIgnoreCase) ||
                            order.Name.StartsWith("T1_", StringComparison.OrdinalIgnoreCase) ||
                            order.Name.StartsWith("T2_", StringComparison.OrdinalIgnoreCase) ||
                            order.Name.StartsWith("T3_", StringComparison.OrdinalIgnoreCase) ||
                            order.Name.StartsWith("T4_", StringComparison.OrdinalIgnoreCase) ||
                            order.Name.StartsWith("T5_", StringComparison.OrdinalIgnoreCase);
                        if (!isZombieTarget) continue;
                    }

                    ordersToCancel.Add(order);
                }

                if (ordersToCancel.Count > 0)
                {
                    acct.Cancel(ordersToCancel);
                    Print(string.Format("[FLATTEN_PUMP] {0}: Cancelled {1} order(s) [{2}]",
                        acct.Name, ordersToCancel.Count, item.Source));
                }

                // Step 2: Submit market close for each open position (skip if CancelOnly with no close intent)
                if (!item.CancelOnly)
                {
                    int closedCount = 0;
                    foreach (Position position in acct.Positions)
                    {
                        if (position.Instrument.FullName != Instrument.FullName) continue;
                        if (position.MarketPosition == MarketPosition.Flat) continue;

                        int qty = position.Quantity;
                        OrderAction closeAction = position.MarketPosition == MarketPosition.Long
                            ? OrderAction.Sell : OrderAction.BuyToCover;

                        if (item.IsMaster)
                        {
                            string sigName = position.MarketPosition == MarketPosition.Long
                                ? "Flatten_MasterLong" : "Flatten_MasterShort";
                            Order masterClose = position.MarketPosition == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qty, 0, 0, "", sigName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", sigName);
                            if (masterClose != null) closedCount++;
                            else Print(string.Format("[FLATTEN_PUMP] Master close FAILED (null): {0} {1}",
                                position.MarketPosition, qty));
                        }
                        else
                        {
                            string sigName = "Flatten_" + position.MarketPosition.ToString();
                            Order closeOrder = acct.CreateOrder(Instrument, closeAction, OrderType.Market,
                                TimeInForce.Gtc, qty, 0, 0, "", sigName, null);
                            acct.Submit(new[] { closeOrder });
                            closedCount++;
                        }
                    }

                    if (closedCount > 0)
                        Print(string.Format("[FLATTEN_PUMP] {0}: Closed {1} position(s) [{2}]",
                            acct.Name, closedCount, item.Source));
                }

                SetExpectedPositionLocked(ExpKey(acct.Name), 0);
            }
            catch (Exception ex)
            {
                Print(string.Format("[FLATTEN_PUMP] ERROR on {0}: {1} [{2}]",
                    item.Account != null ? item.Account.Name : "NULL", ex.Message, item.Source));
            }
            finally
            {
                // Chain to next account or release guard
                if (!_pendingFlattenOps.IsEmpty)
                    try { TriggerCustomEvent(o => PumpFlattenOps(), null); } catch { }
                else
                {
                    isFlattenRunning = false;
                    Print("[SIMA] ====== GLOBAL FLATTEN COMPLETE (CHUNKED) ======");
                }
            }
        }

        /// <summary>
        /// DEAD-01: Emergency single-account fleet kill. Called when a follower entry fills
        /// AFTER the master order is cancelled (CASCADE-FILLED path). Cancels all working orders
        /// on the instrument for this account, then submits a Market close if a position exists.
        /// Must be called on strategy thread (via TriggerCustomEvent).
        /// </summary>
        private void EmergencyFlattenSingleFleetAccount(Account acct)
        {
            if (acct == null) return;
            Print(string.Format("[DEAD-01] EmergencyFlatten: Initiating kill for {0}", acct.Name));

            try
            {
                // [938-EF-GUARD] Confirm bracket cancellation precedes market close.
                Print(string.Format("[938-EF-GUARD] EF cancelling bracket first: {0}", acct.Name));

                // Step 1: Cancel ALL working orders on this instrument for this account.
                var ordersToCancel = new List<Order>();
                foreach (Order o in acct.Orders)
                {
                    if (o.Instrument.FullName == Instrument.FullName &&
                        (o.OrderState == OrderState.Working    ||
                         o.OrderState == OrderState.Submitted  ||
                         o.OrderState == OrderState.Accepted   ||
                         o.OrderState == OrderState.ChangePending ||
                         o.OrderState == OrderState.ChangeSubmitted))
                    {
                        ordersToCancel.Add(o);
                    }
                }
                if (ordersToCancel.Count > 0)
                {
                    acct.Cancel(ordersToCancel);
                    Print(string.Format("[DEAD-01] EmergencyFlatten: Cancelled {0} working order(s) on {1}.", ordersToCancel.Count, acct.Name));
                }

                // Step 2: Close any live position with a Market order.
                Position pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName &&
                                                                    p.MarketPosition != MarketPosition.Flat);
                if (pos != null)
                {
                    OrderAction closeAction = pos.MarketPosition == MarketPosition.Long
                        ? OrderAction.Sell          // Close long
                        : OrderAction.BuyToCover;   // Close short

                    Order closeOrder = acct.CreateOrder(
                        Instrument,
                        closeAction,
                        OrderType.Market,
                        TimeInForce.Day,
                        pos.Quantity,
                        0, 0,
                        string.Empty,
                        "Emergency_Flatten_DEAD01",
                        null);
                    acct.Submit(new[] { closeOrder });
                    Print(string.Format("[DEAD-01] EmergencyFlatten: Market {0} {1} submitted on {2}.",
                        closeAction, pos.Quantity, acct.Name));
                }
                else
                {
                    Print(string.Format("[DEAD-01] EmergencyFlatten: {0} already flat -- no close order needed.", acct.Name));
                }

                // Phase 5.5: Direct call -- strategy thread (TriggerCustomEvent).
                SetExpectedPositionLocked(ExpKey(acct.Name), 0);
            }
            catch (Exception ex)
            {
                Print(string.Format("[DEAD-01] EmergencyFlatten ERROR on {0}: {1}", acct.Name, ex.Message));
            }
        }

        private void ClosePositionsOnlyApexAccounts()
        {
            if (!EnableSIMA) return;

            isFlattenRunning = true;
            Print("[SIMA] ====== GLOBAL POSITIONS CLOSE START (CHUNKED) ======");

            Account[] snapshot = Account.All.ToArray();
            int enqueued = 0;
            foreach (Account acct in snapshot)
            {
                if (!IsFleetAccount(acct)) continue;
                // ZombieSweepOnly=true: cancel only zombie targets, then market close
                _pendingFlattenOps.Enqueue(new FlattenWorkItem
                {
                    Account = acct, CancelOnly = false, ZombieSweepOnly = true,
                    IsMaster = false, Source = "ClosePositionsOnly"
                });
                enqueued++;
            }

            // Master fallback
            bool masterCovered = IsFleetAccount(Account);
            if (!masterCovered && Account.Positions.Count > 0)
            {
                _pendingFlattenOps.Enqueue(new FlattenWorkItem
                {
                    Account = Account, CancelOnly = false, ZombieSweepOnly = true,
                    IsMaster = true, Source = "ClosePositionsOnly_Master"
                });
                enqueued++;
            }

            Print(string.Format("[SIMA] Enqueued {0} account(s) for chunked close", enqueued));

            if (!_pendingFlattenOps.IsEmpty)
                try { TriggerCustomEvent(o => PumpFlattenOps(), null); } catch { }
            else
            {
                isFlattenRunning = false;
                Print("[SIMA] ====== GLOBAL POSITIONS CLOSE COMPLETE (no accounts) ======");
            }
        }


        #endregion
    }
}
