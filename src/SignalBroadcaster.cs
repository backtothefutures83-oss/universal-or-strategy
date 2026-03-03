using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Static signal broadcasting service for Master/Slave multi-account architecture.
    /// Master strategy generates signals, Slave strategies listen and execute.
    /// </summary>
    public static class SignalBroadcaster
    {
        #region Signal Data Classes

        /// <summary>
        /// Complete trade signal with all bracket order details
        /// </summary>
        public class TradeSignal
        {
            public string SignalId { get; set; }
            public string Instrument { get; set; }        // V7.1: For instrument filtering
            public MarketPosition Direction { get; set; }
            public double EntryPrice { get; set; }
            public double StopPrice { get; set; }
            public double Target1Price { get; set; }
            public double Target2Price { get; set; }
            public double Target3Price { get; set; }      // V8: T3 price
            public int T1Contracts { get; set; }
            public int T2Contracts { get; set; }
            public int T3Contracts { get; set; }
            public int T4Contracts { get; set; }          // V8: Runner contracts
            public bool IsRMA { get; set; }
            public DateTime Timestamp { get; set; }
            public double SessionRange { get; set; }  // For reference
            public double CurrentATR { get; set; }    // For RMA trades
            
            // V8: Trail settings so slave can use master's configuration
            public double BeTrigger { get; set; }
            public double BeOffset { get; set; }
            public double Trail1Trigger { get; set; }
            public double Trail1Distance { get; set; }
            public double Trail2Trigger { get; set; }
            public double Trail2Distance { get; set; }
            public double Trail3Trigger { get; set; }
            public double Trail3Distance { get; set; }
        }

        /// <summary>
        /// Trailing stop update signal
        /// </summary>
        public class TrailUpdateSignal
        {
            public string SignalId { get; set; }
            public double NewStopPrice { get; set; }
            public int TrailLevel { get; set; }  // BE=0, 1=Trail1, 2=Trail2, 3=Trail3
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V8.1: Full stop synchronization signal
        /// Master broadcasts every stop update, slaves mirror exact price
        /// </summary>
        public class StopUpdateSignal
        {
            public string TradeId { get; set; }        // Links to original entry
            public double NewStopPrice { get; set; }   // Master's new stop price
            public string StopLevel { get; set; }      // "BE", "T1", "T2", "T3" for logging
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V8.1: Entry order price update signal
        /// Master broadcasts when pending entry order price changes
        /// </summary>
        public class EntryUpdateSignal
        {
            public string TradeId { get; set; }        // Links to original entry
            public double NewEntryPrice { get; set; }  // Master's new entry price
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V8.1: Order cancellation signal
        /// Master broadcasts when pending entry order is cancelled
        /// </summary>
        public class OrderCancelSignal
        {
            public string TradeId { get; set; }        // Links to original entry
            public string Reason { get; set; }         // Why cancelled
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Target management action signal (v5.12 feature)
        /// </summary>
        public class TargetActionSignal
        {
            public string SignalId { get; set; }
            public TargetType Target { get; set; }  // T1, T2, or Runner
            public TargetAction Action { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public enum TargetType
        {
            T1,
            T2,
            Runner
        }

        public enum TargetAction
        {
            FillAtMarket,
            MoveToBreakeven,
            MoveStopToEntry,
            CancelTarget
        }

        /// <summary>
        /// Flatten all positions signal
        /// </summary>
        public class FlattenSignal
        {
            public string Reason { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Manual breakeven signal
        /// </summary>
        public class BreakevenSignal
        {
            public string SignalId { get; set; }  // Empty = all positions
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// V10.2: External command signal (from TCP Remote)
        /// Allows the TCP owner to broadcast commands to all other strategy instances
        /// </summary>
        public class ExternalCommandSignal
        {
            public string Command { get; set; }
            public string TargetSymbol { get; set; }
            public DateTime Timestamp { get; set; }
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when Master generates a new trade signal
        /// </summary>
        public static event EventHandler<TradeSignal> OnTradeSignal;

        /// <summary>
        /// Fired when Master updates trailing stop
        /// </summary>
        public static event EventHandler<TrailUpdateSignal> OnTrailUpdate;

        /// <summary>
        /// Fired when Master updates trailing stop update request (v5.12)
        /// </summary>
        public static event EventHandler<TargetActionSignal> OnTargetAction;

        /// <summary>
        /// Fired when Master requests flatten all
        /// </summary>
        public static event EventHandler<FlattenSignal> OnFlattenAll;

        /// <summary>
        /// Fired when Master requests manual breakeven
        /// </summary>
        public static event EventHandler<BreakevenSignal> OnBreakevenRequest;

        /// <summary>
        /// V8.1: Fired when Master updates any stop (for full synchronization)
        /// </summary>
        public static event EventHandler<StopUpdateSignal> OnStopUpdate;

        /// <summary>
        /// V8.1: Fired when Master updates pending entry order price
        /// </summary>
        public static event EventHandler<EntryUpdateSignal> OnEntryUpdate;

        /// <summary>
        /// V8.1: Fired when Master cancels a pending entry order
        /// </summary>
        public static event EventHandler<OrderCancelSignal> OnOrderCancel;

        /// <summary>
        /// V10.2: Fired when an external TCP command is received
        /// </summary>
        public static event EventHandler<ExternalCommandSignal> OnExternalCommand;

        #endregion

        #region Broadcasting Methods

        /// <summary>
        /// V12.Phase6 [ISOLATION]: Safe per-handler invocation. If one subscriber throws,
        /// the exception is caught and remaining subscribers still receive the signal.
        /// Prevents a single faulty handler from breaking the entire fan-out chain.
        /// V12.Phase7.2: Added performance profiling to detect slow subscribers.
        /// </summary>
        private static void SafeInvoke<T>(EventHandler<T> handler, T args)
        {
            if (handler == null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var invocationList = handler.GetInvocationList();

            foreach (Delegate d in invocationList)
            {
                try
                {
                    ((EventHandler<T>)d).Invoke(null, args);
                }
                catch (Exception)
                {
                    // Swallow -- subscriber isolation; don't break fan-out for other listeners
                }
            }
            sw.Stop();
            // Log only if fan-out takes > 1ms to keep the output clean
            if (sw.Elapsed.TotalMilliseconds > 1.0)
            {
                NinjaTrader.Code.Output.Process(string.Format("[LATENCY_FANOUT] {0}: {1:F2}ms across {2} subscribers", 
                    typeof(T).Name, sw.Elapsed.TotalMilliseconds, invocationList.Length), PrintTo.OutputTab1);
            }
        }

        /// <summary>
        /// Broadcast a trade signal to all listening slaves
        /// </summary>
        public static void BroadcastTradeSignal(TradeSignal signal)
        {
            if (signal == null)
                throw new ArgumentNullException(nameof(signal));

            signal.Timestamp = DateTime.Now;

            // V12.Phase6: Safe per-handler invocation with subscriber isolation
            SafeInvoke(OnTradeSignal, signal);
        }

        /// <summary>
        /// Broadcast a trailing stop update to all slaves
        /// </summary>
        public static void BroadcastTrailUpdate(TrailUpdateSignal update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            update.Timestamp = DateTime.Now;
            SafeInvoke(OnTrailUpdate, update);
        }

        /// <summary>
        /// Broadcast a target management action to all slaves
        /// </summary>
        public static void BroadcastTargetAction(TargetActionSignal action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            action.Timestamp = DateTime.Now;
            SafeInvoke(OnTargetAction, action);
        }

        /// <summary>
        /// Broadcast flatten all positions command
        /// </summary>
        public static void BroadcastFlatten(string reason)
        {
            var signal = new FlattenSignal
            {
                Reason = reason ?? "Manual flatten",
                Timestamp = DateTime.Now
            };

            SafeInvoke(OnFlattenAll, signal);
        }

        /// <summary>
        /// Broadcast manual breakeven request
        /// </summary>
        public static void BroadcastBreakeven(string signalId = "")
        {
            var signal = new BreakevenSignal
            {
                SignalId = signalId,
                Timestamp = DateTime.Now
            };

            SafeInvoke(OnBreakevenRequest, signal);
        }

        /// <summary>
        /// V8.1: Broadcast stop update for full synchronization
        /// </summary>
        public static void BroadcastStopUpdate(string tradeId, double newStopPrice, string stopLevel)
        {
            var signal = new StopUpdateSignal
            {
                TradeId = tradeId,
                NewStopPrice = newStopPrice,
                StopLevel = stopLevel,
                Timestamp = DateTime.Now
            };

            SafeInvoke(OnStopUpdate, signal);
        }

        /// <summary>
        /// V8.1: Broadcast entry price update for full synchronization
        /// </summary>
        public static void BroadcastEntryUpdate(string tradeId, double newEntryPrice)
        {
            var signal = new EntryUpdateSignal
            {
                TradeId = tradeId,
                NewEntryPrice = newEntryPrice,
                Timestamp = DateTime.Now
            };

            SafeInvoke(OnEntryUpdate, signal);
        }

        /// <summary>
        /// V8.1: Broadcast order cancellation for full synchronization
        /// </summary>
        public static void BroadcastOrderCancel(string tradeId, string reason)
        {
            var signal = new OrderCancelSignal
            {
                TradeId = tradeId,
                Reason = reason ?? "Manual cancel",
                Timestamp = DateTime.Now
            };

            SafeInvoke(OnOrderCancel, signal);
        }

        /// <summary>
        /// V10.2: Broadcast an external command received via TCP
        /// </summary>
        public static void BroadcastExternalCommand(string command, string targetSymbol)
        {
            var signal = new ExternalCommandSignal
            {
                Command = command,
                TargetSymbol = targetSymbol,
                Timestamp = DateTime.Now
            };

            SafeInvoke(OnExternalCommand, signal);
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Get count of active subscribers for each event type
        /// </summary>
        public static string GetSubscriberCounts()
        {
            int tradeSignalCount = OnTradeSignal?.GetInvocationList().Length ?? 0;
            int trailUpdateCount = OnTrailUpdate?.GetInvocationList().Length ?? 0;
            int targetActionCount = OnTargetAction?.GetInvocationList().Length ?? 0;
            int flattenCount = OnFlattenAll?.GetInvocationList().Length ?? 0;
            int breakevenCount = OnBreakevenRequest?.GetInvocationList().Length ?? 0;
            int stopUpdateCount = OnStopUpdate?.GetInvocationList().Length ?? 0;
            int entryUpdateCount = OnEntryUpdate?.GetInvocationList().Length ?? 0;
            int orderCancelCount = OnOrderCancel?.GetInvocationList().Length ?? 0;

            return $"Subscribers: Trade={tradeSignalCount}, Stop={stopUpdateCount}, Entry={entryUpdateCount}, Cancel={orderCancelCount}";
        }

        /// <summary>
        /// Clear all event subscribers (for cleanup/testing)
        /// </summary>
        public static void ClearAllSubscribers()
        {
            OnTradeSignal = null;
            OnTrailUpdate = null;
            OnTargetAction = null;
            OnFlattenAll = null;
            OnBreakevenRequest = null;
            OnStopUpdate = null;
            OnEntryUpdate = null;
            OnOrderCancel = null;
            OnExternalCommand = null;
        }

        #endregion
    }
}
