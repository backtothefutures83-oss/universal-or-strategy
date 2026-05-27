// V12_002.Safety.Watchdog.cs -- Build 1108.004 safety watchdog
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private const int WatchdogIntervalMs = 2000;
        private const long WatchdogTimeoutTicks = 50000000L;

        private void StartWatchdog()
        {
            StopWatchdog();
            Interlocked.Exchange(ref _watchdogStage, 0);
            TouchStrategyHeartbeat();
            _watchdogTimer = new System.Threading.Timer(OnWatchdogTimer, null, WatchdogIntervalMs, WatchdogIntervalMs);
            Print("[WATCHDOG] Started (interval=2000ms, timeout=5s)");
        }

        private void StopWatchdog()
        {
            System.Threading.Timer timer = Interlocked.Exchange(ref _watchdogTimer, null);
            if (timer == null)
                return;

            timer.Dispose();
            Interlocked.Exchange(ref _watchdogStage, 0);
            Print("[WATCHDOG] Stopped");
        }

        private void OnWatchdogTimer(object state)
        {
            if (_isTerminating || State != State.Realtime)
            {
                Interlocked.Exchange(ref _watchdogStage, 0);
                return;
            }

            long lastBeat = Interlocked.Read(ref _strategyHeartbeatTicks);
            if (lastBeat <= 0)
                return;

            long heartbeatAge = DateTime.UtcNow.Ticks - lastBeat;
            if (heartbeatAge <= WatchdogTimeoutTicks)
            {
                Interlocked.Exchange(ref _watchdogStage, 0);
                return;
            }

            if (!HasWatchdogLeadAccountWorkingOrder())
            {
                Interlocked.Exchange(ref _watchdogStage, 0);
                return;
            }

            int stage = Volatile.Read(ref _watchdogStage);
            if (stage == 0)
            {
                if (Interlocked.CompareExchange(ref _watchdogStage, 1, 0) == 0)
                {
                    try
                    {
                        Print("[!] CRITICAL: DEADLOCK DETECTED (TIMEOUT > 5S)");
                        Enqueue(ctx => ctx.ExecuteWatchdogLeadAccountFlatten());
                        Print("[WATCHDOG] Enqueued lead account emergency flatten.");
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref _watchdogStage, 0);
                        Print("[WATCHDOG] Enqueue failed: " + ex.Message);
                    }
                }
                return;
            }

            if (stage != 1)
                return;

            if (Interlocked.CompareExchange(ref _watchdogStage, 2, 1) == 1)
            {
                Print("[WATCHDOG] Escalating to direct master close fallback.");
                ExecuteWatchdogDirectFallback();
            }
        }

        private bool HasWatchdogLeadAccountPosition()
        {
            Account masterAccount = Account;
            if (masterAccount == null || Instrument == null)
                return false;

            string instrumentName = Instrument.FullName;

            foreach (Position position in masterAccount.Positions)
            {
                if (position == null || position.Instrument == null)
                    continue;
                if (position.Instrument.FullName != instrumentName)
                    continue;
                if (position.MarketPosition != MarketPosition.Flat)
                    return true;
            }

            return false;
        }

        private bool HasWatchdogLeadAccountWorkingOrder()
        {
            Account masterAccount = Account;
            if (masterAccount == null || Instrument == null)
                return false;

            string instrumentName = Instrument.FullName;

            foreach (Order order in masterAccount.Orders.ToArray())
            {
                if (order == null || order.Instrument == null)
                    continue;
                if (order.Instrument.FullName != instrumentName)
                    continue;
                if (!IsOrderTerminal(order.OrderState))
                    return true;
            }

            return false;
        }

        private bool HasWatchdogLeadAccountExposure()
        {
            return HasWatchdogLeadAccountPosition() || HasWatchdogLeadAccountWorkingOrder();
        }

        private void CancelWatchdogWorkingOrders(Account masterAccount, string instrumentName)
        {
            List<Order> ordersToCancel = new List<Order>();

            foreach (Order order in masterAccount.Orders.ToArray())
            {
                if (order == null || order.Instrument == null)
                    continue;
                if (order.Instrument.FullName != instrumentName)
                    continue;
                if (
                    order.OrderState == OrderState.Working
                    || order.OrderState == OrderState.Submitted
                    || order.OrderState == OrderState.Accepted
                    || order.OrderState == OrderState.ChangePending
                    || order.OrderState == OrderState.ChangeSubmitted
                )
                {
                    ordersToCancel.Add(order);
                }
            }

            foreach (Order orderToCancel in ordersToCancel)
                CancelOrderOnAccount(orderToCancel, masterAccount);

            if (ordersToCancel.Count > 0)
                Print("[WATCHDOG] Cancelled " + ordersToCancel.Count + " master order(s) on strategy thread.");
        }

        private void FlattenWatchdogPositions(Account masterAccount, string instrumentName)
        {
            foreach (Position position in masterAccount.Positions)
            {
                if (position == null || position.Instrument == null)
                    continue;
                if (position.Instrument.FullName != instrumentName)
                    continue;
                if (position.MarketPosition == MarketPosition.Flat)
                    continue;

                int quantity = position.Quantity;
                Order flattenOrder =
                    position.MarketPosition == MarketPosition.Long
                        ? SubmitOrderUnmanaged(
                            0,
                            OrderAction.Sell,
                            OrderType.Market,
                            quantity,
                            0,
                            0,
                            "",
                            "Watchdog_MasterLong"
                        )
                        : SubmitOrderUnmanaged(
                            0,
                            OrderAction.BuyToCover,
                            OrderType.Market,
                            quantity,
                            0,
                            0,
                            "",
                            "Watchdog_MasterShort"
                        );

                if (flattenOrder == null)
                    Print("[WATCHDOG] Strategy-thread master close returned null.");
                else
                    Print(
                        "[WATCHDOG] Strategy-thread master close submitted: " + quantity + " on " + masterAccount.Name
                    );
            }
        }

        private void ExecuteWatchdogLeadAccountFlatten()
        {
            Account masterAccount = Account;
            if (masterAccount == null || Instrument == null || _isTerminating || State != State.Realtime)
                return;
            if (!HasWatchdogLeadAccountWorkingOrder())
            {
                Interlocked.Exchange(ref _watchdogStage, 0);
                return;
            }

            if (!HasWatchdogLeadAccountExposure())
                return;

            EnterFlattenScope();
            try
            {
                string instrumentName = Instrument.FullName;
                CancelWatchdogWorkingOrders(masterAccount, instrumentName);
                FlattenWatchdogPositions(masterAccount, instrumentName);
                SetExpectedPositionLocked(ExpKey(masterAccount.Name), 0);
                PublishUiSnapshot();
            }
            catch (Exception ex)
            {
                Print("[WATCHDOG] Strategy-thread emergency close failed: " + ex.Message);
            }
            finally
            {
                ExitFlattenScope();
            }
        }

        private void ExecuteWatchdogDirectFallback()
        {
            Account masterAccount = Account;
            if (masterAccount == null || Instrument == null)
                return;
            if (!HasWatchdogLeadAccountWorkingOrder())
            {
                Interlocked.Exchange(ref _watchdogStage, 0);
                return;
            }

            try
            {
                string instrumentName = Instrument.FullName;
                CancelDirectFallbackOrders(masterAccount, instrumentName);
                FlattenDirectFallbackPositions(masterAccount, instrumentName);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _watchdogStage, 1);
                Print("[WATCHDOG] Direct fallback failed: " + ex.Message);
            }
        }

        private void CancelDirectFallbackOrders(Account masterAccount, string instrumentName)
        {
            List<Order> ordersToCancel = new List<Order>();

            foreach (Order order in masterAccount.Orders.ToArray())
            {
                if (order == null || order.Instrument == null)
                    continue;
                if (order.Instrument.FullName != instrumentName)
                    continue;
                if (
                    order.OrderState == OrderState.Working
                    || order.OrderState == OrderState.Submitted
                    || order.OrderState == OrderState.Accepted
                    || order.OrderState == OrderState.ChangePending
                    || order.OrderState == OrderState.ChangeSubmitted
                )
                {
                    ordersToCancel.Add(order);
                }
            }

            if (ordersToCancel.Count > 0)
            {
                masterAccount.Cancel(ordersToCancel.ToArray());
                Print("[WATCHDOG] Direct fallback cancelled " + ordersToCancel.Count + " master order(s).");
            }
        }

        private void FlattenDirectFallbackPositions(Account masterAccount, string instrumentName)
        {
            foreach (Position position in masterAccount.Positions)
            {
                if (position == null || position.Instrument == null)
                    continue;
                if (position.Instrument.FullName != instrumentName)
                    continue;
                if (position.MarketPosition == MarketPosition.Flat)
                    continue;

                OrderAction closeAction =
                    position.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                Order closeOrder = masterAccount.CreateOrder(
                    Instrument,
                    closeAction,
                    OrderType.Market,
                    TimeInForce.Gtc,
                    position.Quantity,
                    0,
                    0,
                    string.Empty,
                    "Watchdog_Direct_" + position.MarketPosition,
                    null
                );

                if (closeOrder == null)
                {
                    Print("[WATCHDOG] Direct fallback CreateOrder returned null.");
                    continue;
                }

                masterAccount.Submit(new[] { closeOrder });
                Print("[WATCHDOG] Direct fallback close submitted: " + position.Quantity + " on " + masterAccount.Name);
            }
        }
    }
}
