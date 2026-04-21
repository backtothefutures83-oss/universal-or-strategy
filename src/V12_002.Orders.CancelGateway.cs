// V12_002.Orders.CancelGateway.cs -- Build 1104: Cancel Order Gateway
// All raw CancelOrder() usage must route through these three methods.
// Raw CancelOrder() is banned outside this file.
// DNA: ASCII-only strings, no locks, no direct CancelOrder() outside this file.
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        /// <summary>
        /// Standard cancel with fleet routing.
        /// Use for lifecycle cancels, cleanup, flatten, and general order cancels where PositionInfo is available.
        /// Fleet followers route through ExecutingAccount.Cancel().
        /// Master or null PositionInfo routes through the NinjaScript managed cancel API.
        /// </summary>
        private void CancelOrderSafe(Order order, PositionInfo pos)
        {
            if (order == null || IsOrderTerminal(order.OrderState))
                return;

            if (pos != null && pos.IsFollower && pos.ExecutingAccount != null)
                pos.ExecutingAccount.Cancel(new[] { order });
            else
                CancelOrder(order);
        }

        /// <summary>
        /// Cancel-then-resubmit workflow. Stamps the REAPER move grace before cancelling.
        /// Use for stop resizing, break-even updates, trailing updates, and other replace flows.
        /// </summary>
        private void CancelOrderForReplace(Order order, PositionInfo pos)
        {
            if (order == null || IsOrderTerminal(order.OrderState))
                return;

            StampReaperMoveGrace();
            CancelOrderSafe(order, pos);
        }

        /// <summary>
        /// Explicit account-context cancel for code paths that do not have PositionInfo but do know the account.
        /// Use for SIMA lifecycle cleanup, broker sweeps, and account-level operations.
        /// </summary>
        private void CancelOrderOnAccount(Order order, Account executingAccount)
        {
            if (order == null || IsOrderTerminal(order.OrderState))
                return;

            if (executingAccount != null && executingAccount != Account)
                executingAccount.Cancel(new[] { order });
            else
                CancelOrder(order);
        }
    }
}
