// Build 982: BracketFSM (Shadow Mode) - Phase 2 Definitions
// V12 Symmetry Module - Follower Bracket Finite State Machine
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region BracketFSM Definitions

        /// <summary>
        /// Phase 2: Follower Bracket States for Shadow Mode.
        /// Tracks the lifecycle of a follower bracket from strategic intent to terminal state.
        /// </summary>
        private enum FollowerBracketState
        {
            None,            // Initial state
            PendingSubmit,   // Strategic intent to submit, pre-submission validation/anchoring
            Submitted,       // acct.Submit() called, awaiting broker ack
            Accepted,        // Broker acknowledged (OrderState.Accepted/Working)
            Active,          // Entry filled, protective bracket (Stop + Targets) live
            Replacing,       // In-flight two-phase cancel+resubmit (MOVE-SYNC FSM active)
            Modifying,       // Price change (trailing) in flight, awaiting confirm
            Filled,          // Final: Position closed via Stop or Target fill
            Cancelled,       // Final: All orders cancelled
            Rejected,        // Final: Broker rejected (requires audit)
            Disconnected     // Temporary: Account connection lost, FSM frozen
        }

        /// <summary>
        /// Consolidated FSM Container for a single Follower Bracket (Entry + Stop + Targets).
        /// Replaces the dictionary-scatter pattern with a single source of truth.
        /// </summary>
        private class FollowerBracketFSM
        {
            public string AccountName;
            public string EntryName;         // Links to Master Position key (fleetEntryName)
            public string OcoGroupId;        // Shared ID for broker OCO
            public FollowerBracketState State = FollowerBracketState.None;
            public int RemainingContracts;
            public string ReplacingCancelOrderId;
            public DateTime LastUpdateUtc = DateTime.UtcNow;

            public Order EntryOrder;
            public Order StopOrder;
            public Order[] Targets = new Order[5]; // Index 0-4 for T1-T5

            // Shadow Mode Diagnostics
            public bool IsInSync = true;
            public string LastBrokerError;
            
            // Metadata for reconciliation
            public double ExpectedEntryPrice;
            public double ExpectedStopPrice;
            public double[] ExpectedTargetPrices = new double[5];
        }

        /// <summary>
        /// Actor Mailbox Message for lock-free account event processing.
        /// Enqueued by account threads, consumed by strategy thread.
        /// </summary>
        public struct AccountEvent
        {
            public string AccountAlias;
            public string OrderId;
            public OrderState NewState;
            public double FillPrice;
            public int FilledQty;
            public long TimestampTicks;
            public string SignalName; // Optional: helps with un-tracked order matching
            public string ErrorMessage;
        }

        #endregion

        #region BracketFSM Logic (Actor Consumer)

        /// <summary>
        /// Consumes queued account events from the strategy thread.
        /// Called from OnBarUpdate or OnOrderUpdate via TriggerCustomEvent.
        /// </summary>
        private void DrainAccountMailbox()
        {
            if (!EnsureStartupReady("DrainAccountMailbox")) return;

            int processed = 0;
            const int MAX_PER_DRAIN = 100;

            while (processed < MAX_PER_DRAIN && _accountMailbox.TryDequeue(out var evt))
            {
                ProcessBracketEvent(evt);
                processed++;
            }
        }

        private void RemoveFsmOrderIdMappings(FollowerBracketFSM fsm)
        {
            if (fsm == null) return;

            if (fsm.EntryOrder != null && !string.IsNullOrEmpty(fsm.EntryOrder.OrderId))
                _orderIdToFsmKey.TryRemove(fsm.EntryOrder.OrderId, out _);

            if (!string.IsNullOrEmpty(fsm.ReplacingCancelOrderId))
                _orderIdToFsmKey.TryRemove(fsm.ReplacingCancelOrderId, out _);

            if (fsm.StopOrder != null && !string.IsNullOrEmpty(fsm.StopOrder.OrderId))
                _orderIdToFsmKey.TryRemove(fsm.StopOrder.OrderId, out _);

            if (fsm.Targets == null) return;

            foreach (Order target in fsm.Targets)
            {
                if (target != null && !string.IsNullOrEmpty(target.OrderId))
                    _orderIdToFsmKey.TryRemove(target.OrderId, out _);
            }
        }

        private bool TryTerminateFollowerBracket(string entryName, out FollowerBracketFSM removedFsm)
        {
            removedFsm = null;
            if (string.IsNullOrEmpty(entryName)) return false;
            if (!_followerBrackets.TryRemove(entryName, out removedFsm)) return false;

            RemoveFsmOrderIdMappings(removedFsm);
            return true;
        }

        private void SetFsmReplacing(string fleetEntryName, string cancelOrderId)
        {
            if (string.IsNullOrEmpty(fleetEntryName) || string.IsNullOrEmpty(cancelOrderId)) return;

            FollowerBracketFSM fsm;
            if (!_followerBrackets.TryGetValue(fleetEntryName, out fsm) || fsm == null) return;

            fsm.State = FollowerBracketState.Replacing;
            fsm.ReplacingCancelOrderId = cancelOrderId;
            fsm.LastUpdateUtc = DateTime.UtcNow;
            Print(string.Format("[FSM-C2] {0} -> Replacing (cancelId={1})", fleetEntryName, cancelOrderId));
        }

        /// <summary>
        /// Core FSM transition logic. Driven exclusively by broker confirmations.
        /// Shadow Mode: Observes reality and logs divergences.
        /// </summary>
        private void ProcessBracketEvent(AccountEvent evt)
        {
            // V12.Phase2: Implement FSM transition logic based on docs/copy_trader_design.md Section 1.3
            
            // 1. Find the FSM by OrderId, SignalName, or fallback scan
            FollowerBracketFSM fsm = null;
            
            // Phase 3 [Step 4]: O(1) Lookup via OrderId map (primary)
            if (!string.IsNullOrEmpty(evt.OrderId))
            {
                if (_orderIdToFsmKey.TryGetValue(evt.OrderId, out var entryName))
                {
                    _followerBrackets.TryGetValue(entryName, out fsm);
                }
            }

            // Fallback: Try matching by SignalName (secondary)
            if (fsm == null && !string.IsNullOrEmpty(evt.SignalName))
            {
                // Signal names are like "Stop_Fleet_Apex_1" or "T1_Fleet_Apex_1"
                // The fleetEntryName is the part after the first underscore.
                int firstUnder = evt.SignalName.IndexOf('_');
                if (firstUnder >= 0 && firstUnder < evt.SignalName.Length - 1)
                {
                    string fleetEntryName = evt.SignalName.Substring(firstUnder + 1);
                    if (_followerBrackets.TryGetValue(fleetEntryName, out fsm))
                    {
                        // Back-fill the OrderId map if we found it via signal
                        if (!string.IsNullOrEmpty(evt.OrderId))
                            _orderIdToFsmKey[evt.OrderId] = fleetEntryName;
                    }
                }
            }
            
            // Last resort: search all FSMs (slow O(N) scan)
            if (fsm == null)
            {
                foreach (var f in _followerBrackets.Values)
                {
                    if (f.AccountName != evt.AccountAlias) continue;
                    
                    if (f.StopOrder != null && f.StopOrder.OrderId == evt.OrderId) { fsm = f; break; }
                    bool foundT = false;
                    for (int i = 0; i < 5; i++)
                    {
                        if (f.Targets[i] != null && f.Targets[i].OrderId == evt.OrderId) { fsm = f; foundT = true; break; }
                    }
                    if (foundT) break;
                    if (f.EntryOrder != null && f.EntryOrder.OrderId == evt.OrderId) { fsm = f; break; }
                }
                
                // Back-fill if found
                if (fsm != null && !string.IsNullOrEmpty(evt.OrderId))
                    _orderIdToFsmKey[evt.OrderId] = fsm.EntryName;
            }

            if (fsm == null) return; // Not tracked by FSM system yet
            if (!MetadataGuardFsmEvent(evt, fsm)) return;

            // 2. Process State Transition
            FollowerBracketState oldState = fsm.State;
            
            switch (evt.NewState)
            {
                case OrderState.Accepted:
                case OrderState.Working:
                    if (fsm.State == FollowerBracketState.Submitted || fsm.State == FollowerBracketState.PendingSubmit)
                        fsm.State = FollowerBracketState.Accepted;
                    break;

                case OrderState.Filled:
                case OrderState.PartFilled:
                    // Phase 2 [D2/D3]: Precise target matching with null guards
                    bool isStop = !string.IsNullOrEmpty(evt.SignalName) && (evt.SignalName.StartsWith("Stop_") || evt.SignalName.StartsWith("S_"));
                    bool isTarget = !string.IsNullOrEmpty(evt.SignalName) && (evt.SignalName.StartsWith("T1_") || evt.SignalName.StartsWith("T2_") || 
                                     evt.SignalName.StartsWith("T3_") || evt.SignalName.StartsWith("T4_") || evt.SignalName.StartsWith("T5_"));

                    if (isStop || isTarget)
                    {
                        fsm.RemainingContracts = Math.Max(0, fsm.RemainingContracts - Math.Max(0, evt.FilledQty));
                        fsm.State = fsm.RemainingContracts <= 0 ? FollowerBracketState.Filled : FollowerBracketState.Active;
                    }
                    else if (fsm.State == FollowerBracketState.Accepted || fsm.State == FollowerBracketState.Submitted)
                    {
                        // Entry filled -> Bracket is now ACTIVE
                        fsm.State = FollowerBracketState.Active;
                    }
                    break;

                case OrderState.Cancelled:
                    if (fsm.State == FollowerBracketState.Replacing
                        && string.Equals(fsm.ReplacingCancelOrderId, evt.OrderId, StringComparison.Ordinal))
                    {
                        Print("[FSM-C2] Replace-cycle cancel absorbed -- FSM stays Replacing");
                    }
                    else
                    {
                        fsm.State = FollowerBracketState.Cancelled;
                    }
                    break;

                case OrderState.Rejected:
                    fsm.State = FollowerBracketState.Rejected;
                    fsm.LastBrokerError = evt.ErrorMessage;
                    break;
            }

            if (fsm.State != oldState)
            {
                fsm.LastUpdateUtc = DateTime.UtcNow;
                Print(string.Format("[FSM-SHADOW] {0} Transition: {1} -> {2} | Event={3} | Order={4}",
                    fsm.EntryName, oldState, fsm.State, evt.NewState, evt.SignalName));
            }
        }

        #endregion
    }
}
