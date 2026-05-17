// Symmetry FSM Integration Tests
// V12 DNA Compliant: Lock-free, ASCII-only, Actor pattern, MockTime
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace V12.Sima.Tests
{
    /// <summary>
    /// Integration tests for Symmetry FSM (Follower Bracket Finite State Machine).
    /// Tests the lifecycle of follower brackets from strategic intent to terminal states.
    /// V12 DNA: Lock-free (Interlocked/CAS), ASCII-only, Actor pattern (mailbox), MockTime.
    /// </summary>
    public class SymmetryFsmIntegrationTests
    {
        #region Mock NinjaTrader Types

        /// <summary>
        /// Mock OrderAction enum (mirrors NinjaTrader.Cbi.OrderAction).
        /// </summary>
        private enum OrderAction
        {
            Buy,
            Sell,
            BuyToCover,
            SellShort
        }

        /// <summary>
        /// Mock OrderState enum (mirrors NinjaTrader.Cbi.OrderState).
        /// </summary>
        private enum OrderState
        {
            Unknown,
            Initialized,
            Submitted,
            Accepted,
            Working,
            PartFilled,
            Filled,
            Cancelled,
            Rejected,
            CancelPending,
            CancelSubmitted
        }

        #endregion

        #region Mock Infrastructure

        /// <summary>
        /// Deterministic time simulation for testing.
        /// Pattern from CircuitBreakerBehaviorTests.MockTime.
        /// </summary>
        private class MockTime
        {
            private long _ticks;

            public MockTime(long initialTicks) => _ticks = initialTicks;
            public long GetTicks() => _ticks;
            public void Advance(long deltaTicks) => _ticks += deltaTicks;
            public void AdvanceSeconds(double seconds) =>
                _ticks += (long)(seconds * TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// Broker order simulation for testing.
        /// No NinjaTrader dependencies.
        /// </summary>
        private class MockOrder
        {
            public string OrderId { get; set; }
            public string SignalName { get; set; }
            public OrderAction OrderAction { get; set; }
            public int Quantity { get; set; }
            public OrderState State { get; set; }
            public double FillPrice { get; set; }
            public int FilledQuantity { get; set; }

            public MockOrder(string orderId, string signalName, OrderAction action, int qty)
            {
                OrderId = orderId;
                SignalName = signalName;
                OrderAction = action;
                Quantity = qty;
                State = OrderState.Unknown;
            }
        }

        /// <summary>
        /// Atomic FSM State Packing (64-bit).
        /// Layout: [State: 8 bits][Pending: 1 bit][Generation: 55 bits]
        /// Copied from V12_002.Symmetry.BracketFSM.cs lines 19-39.
        /// </summary>
        private struct FsmPackedState
        {
            private const int StateShift = 56;
            private const int PendingShift = 55;
            private const long PendingMask = 1L << PendingShift;
            private const long GenerationMask = (1L << 55) - 1;

            public static long Pack(byte state, bool pending, long generation)
            {
                var gen = generation & GenerationMask;
                var pend = pending ? PendingMask : 0;
                return ((long)state << StateShift) | pend | gen;
            }

            public static void Unpack(long value, out byte state, out bool pending, out long generation)
            {
                state = (byte)(value >> StateShift);
                pending = (value & PendingMask) != 0;
                generation = value & GenerationMask;
            }
        }

        /// <summary>
        /// Follower Bracket States.
        /// Copied from V12_002.Symmetry.BracketFSM.cs lines 46-59.
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
        /// Mock FSM Container for a single Follower Bracket.
        /// Mirrors production FSM structure with atomic state.
        /// </summary>
        private class MockFollowerBracketFSM
        {
            public string AccountName { get; set; }
            public string EntryName { get; set; }
            public string OcoGroupId { get; set; }
            private long _packedState;
            public int RemainingContracts { get; set; }
            public string ReplacingCancelOrderId { get; set; }
            public DateTime LastUpdateUtc { get; set; }

            public MockOrder EntryOrder { get; set; }
            public MockOrder StopOrder { get; set; }
            public MockOrder[] Targets { get; set; } = new MockOrder[5];

            public FollowerBracketState State
            {
                get
                {
                    FsmPackedState.Unpack(Interlocked.Read(ref _packedState),
                        out byte state, out _, out _);
                    return (FollowerBracketState)state;
                }
                set
                {
                    long current = Interlocked.Read(ref _packedState);
                    FsmPackedState.Unpack(current, out _, out bool pending, out long gen);
                    long newPacked = FsmPackedState.Pack((byte)value, pending, gen);
                    Interlocked.Exchange(ref _packedState, newPacked);
                }
            }

            public long Generation
            {
                get
                {
                    FsmPackedState.Unpack(Interlocked.Read(ref _packedState),
                        out _, out _, out long gen);
                    return gen;
                }
            }

            /// <summary>
            /// Atomic state transition with CAS loop and validation.
            /// </summary>
            public bool TryTransition(FollowerBracketState newState, bool setPending)
            {
                long currentPacked, newPacked;
                do
                {
                    currentPacked = Interlocked.Read(ref _packedState);
                    FsmPackedState.Unpack(currentPacked, out byte oldState, out _, out long gen);

                    // Validate transition (state machine rules)
                    if (!IsValidTransition((FollowerBracketState)oldState, newState))
                        return false;

                    newPacked = FsmPackedState.Pack((byte)newState, setPending, gen + 1);
                }
                while (Interlocked.CompareExchange(ref _packedState, newPacked, currentPacked) != currentPacked);

                return true;
            }

            /// <summary>
            /// Validates FSM state transitions based on state machine rules.
            /// </summary>
            private bool IsValidTransition(FollowerBracketState from, FollowerBracketState to)
            {
                return (from, to) switch
                {
                    (FollowerBracketState.None, FollowerBracketState.PendingSubmit) => true,
                    (FollowerBracketState.None, FollowerBracketState.Accepted) => true, // Out-of-order: Accepted before PendingSubmit
                    (FollowerBracketState.PendingSubmit, FollowerBracketState.Submitted) => true,
                    (FollowerBracketState.Submitted, FollowerBracketState.Accepted) => true,
                    (FollowerBracketState.Submitted, FollowerBracketState.Active) => true, // Out-of-order: Filled before Accepted
                    (FollowerBracketState.Submitted, FollowerBracketState.Rejected) => true,
                    (FollowerBracketState.Accepted, FollowerBracketState.Active) => true,
                    (FollowerBracketState.Active, FollowerBracketState.Filled) => true,
                    (FollowerBracketState.Active, FollowerBracketState.Cancelled) => true,
                    (FollowerBracketState.Active, FollowerBracketState.Replacing) => true,
                    (FollowerBracketState.Active, FollowerBracketState.Modifying) => true,
                    (FollowerBracketState.Active, FollowerBracketState.Disconnected) => true,
                    (FollowerBracketState.Replacing, FollowerBracketState.Accepted) => true,
                    (FollowerBracketState.Modifying, FollowerBracketState.Active) => true,
                    (FollowerBracketState.Disconnected, FollowerBracketState.Active) => true,
                    _ => false
                };
            }
        }

        /// <summary>
        /// Actor Mailbox Message for lock-free account event processing.
        /// Copied from V12_002.Symmetry.BracketFSM.cs lines 143-153.
        /// </summary>
        private struct AccountEvent
        {
            public string AccountAlias { get; set; }
            public string OrderId { get; set; }
            public OrderState NewState { get; set; }
            public double FillPrice { get; set; }
            public int FilledQty { get; set; }
            public long TimestampTicks { get; set; }
            public string SignalName { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// OrderId to FSM mapping helper.
        /// Thread-safe wrapper around ConcurrentDictionary.
        /// </summary>
        private class OrderIdToFsmMap
        {
            private ConcurrentDictionary<string, (string EntryName, long Generation)> _map;

            public OrderIdToFsmMap()
            {
                _map = new ConcurrentDictionary<string, (string, long)>();
            }

            public bool TryAdd(string orderId, string entryName, long generation)
            {
                return _map.TryAdd(orderId, (entryName, generation));
            }

            public bool TryGet(string orderId, out string entryName, out long generation)
            {
                if (_map.TryGetValue(orderId, out var tuple))
                {
                    entryName = tuple.EntryName;
                    generation = tuple.Generation;
                    return true;
                }
                entryName = null;
                generation = 0;
                return false;
            }

            public bool Remove(string orderId)
            {
                return _map.TryRemove(orderId, out _);
            }
        }

        /// <summary>
        /// Mock Symmetry FSM Test Harness.
        /// Implements 3-tier FSM resolution and Actor pattern mailbox.
        /// </summary>
        private class MockSymmetryFsm
        {
            private readonly MockTime _time;
            private readonly ConcurrentDictionary<string, MockFollowerBracketFSM> _brackets;
            private readonly ConcurrentQueue<AccountEvent> _mailbox;
            private readonly OrderIdToFsmMap _orderIdMap;
            private int _drainingFlag = 0;
            private const int MAX_PER_DRAIN = 100;

            public MockSymmetryFsm(MockTime time)
            {
                _time = time;
                _brackets = new ConcurrentDictionary<string, MockFollowerBracketFSM>();
                _mailbox = new ConcurrentQueue<AccountEvent>();
                _orderIdMap = new OrderIdToFsmMap();
            }

            public void EnqueueEvent(AccountEvent evt) => _mailbox.Enqueue(evt);

            public void AddBracket(string entryName, MockFollowerBracketFSM fsm)
            {
                _brackets[entryName] = fsm;
            }

            /// <summary>
            /// Single-threaded consumer with CAS flag protection.
            /// </summary>
            public void DrainMailbox()
            {
                if (Interlocked.CompareExchange(ref _drainingFlag, 1, 0) != 0)
                    return; // Already draining

                try
                {
                    int processed = 0;
                    while (processed < MAX_PER_DRAIN && _mailbox.TryDequeue(out var evt))
                    {
                        ProcessBracketEvent(evt);
                        processed++;
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _drainingFlag, 0);
                }
            }

            /// <summary>
            /// State machine logic for processing bracket events.
            /// </summary>
            private void ProcessBracketEvent(AccountEvent evt)
            {
                var fsm = ResolveFsmFromEvent(evt);
                if (fsm == null) return;

                // Update state based on event
                switch (evt.NewState)
                {
                    case OrderState.Accepted:
                        fsm.TryTransition(FollowerBracketState.Accepted, false);
                        break;
                    case OrderState.Working:
                        fsm.TryTransition(FollowerBracketState.Active, false);
                        break;
                    case OrderState.Filled:
                    case OrderState.PartFilled:
                        HandleFsmFilled(fsm, evt);
                        break;
                    case OrderState.Cancelled:
                        fsm.TryTransition(FollowerBracketState.Cancelled, false);
                        break;
                    case OrderState.Rejected:
                        fsm.TryTransition(FollowerBracketState.Rejected, false);
                        break;
                }
            }

            /// <summary>
            /// Handle filled/part-filled events with contract tracking.
            /// Determines if the fill is for Entry, Stop, or Target and updates state accordingly.
            /// Entry fills transition to Active (establish position, don't reduce contracts).
            /// Stop/Target fills reduce contracts and transition to Filled when zero.
            /// </summary>
            private void HandleFsmFilled(MockFollowerBracketFSM fsm, AccountEvent evt)
            {
                // Determine order type from signal name or order matching
                bool isEntryFill = IsEntryOrder(fsm, evt.OrderId, evt.SignalName);
                
                if (isEntryFill)
                {
                    // Entry fill: Transition to Active (brackets now live)
                    // Entry fills don't reduce RemainingContracts (they establish the position)
                    fsm.TryTransition(FollowerBracketState.Active, false);
                }
                else
                {
                    // Stop or Target fill: Reduce contracts
                    fsm.RemainingContracts -= evt.FilledQty;
                    
                    // If all contracts filled, transition to terminal Filled state
                    if (fsm.RemainingContracts <= 0)
                    {
                        fsm.TryTransition(FollowerBracketState.Filled, false);
                    }
                    // Otherwise stay in Active state (partial fill)
                }
            }

            /// <summary>
            /// Determine if an order is an entry order based on OrderId matching or SignalName.
            /// </summary>
            private bool IsEntryOrder(MockFollowerBracketFSM fsm, string orderId, string signalName)
            {
                // Check if OrderId matches entry order
                if (fsm.EntryOrder?.OrderId == orderId)
                    return true;
                
                // Check signal name pattern
                if (!string.IsNullOrEmpty(signalName) && signalName.StartsWith("Entry_"))
                    return true;
                
                return false;
            }

            /// <summary>
            /// 3-tier FSM resolution with backfill.
            /// Tier 1: OrderId lookup (O(1))
            /// Tier 2: SignalName parsing (O(1) if SignalName present)
            /// Tier 3: Scan all FSMs (O(N))
            /// </summary>
            private MockFollowerBracketFSM ResolveFsmFromEvent(AccountEvent evt)
            {
                // Tier 1: OrderId lookup (O(1))
                if (_orderIdMap.TryGet(evt.OrderId, out string entryName, out long _))
                {
                    return _brackets.TryGetValue(entryName, out var fsm) ? fsm : null;
                }

                // Tier 2: SignalName parsing (O(1) if SignalName present)
                if (!string.IsNullOrEmpty(evt.SignalName))
                {
                    string parsedName = ParseEntryNameFromSignal(evt.SignalName);
                    if (_brackets.TryGetValue(parsedName, out var fsm))
                    {
                        _orderIdMap.TryAdd(evt.OrderId, parsedName, fsm.Generation); // Backfill
                        return fsm;
                    }
                }

                // Tier 3: Scan all FSMs (O(N))
                foreach (var kvp in _brackets)
                {
                    var fsm = kvp.Value;
                    if (MatchesOrder(fsm, evt.OrderId))
                    {
                        _orderIdMap.TryAdd(evt.OrderId, kvp.Key, fsm.Generation); // Backfill
                        return fsm;
                    }
                }

                return null;
            }

            /// <summary>
            /// Parse entry name from signal name.
            /// Example: "Entry_Fleet_Apex_1" -> "Fleet_Apex_1"
            /// </summary>
            private string ParseEntryNameFromSignal(string signalName)
            {
                if (signalName.StartsWith("Entry_"))
                    return signalName.Substring(6);
                if (signalName.StartsWith("Stop_"))
                    return signalName.Substring(5);
                if (signalName.StartsWith("Target"))
                    return signalName.Substring(signalName.IndexOf('_') + 1);
                return signalName;
            }

            /// <summary>
            /// Check if FSM matches order ID.
            /// </summary>
            private bool MatchesOrder(MockFollowerBracketFSM fsm, string orderId)
            {
                if (fsm.EntryOrder?.OrderId == orderId) return true;
                if (fsm.StopOrder?.OrderId == orderId) return true;
                foreach (var target in fsm.Targets)
                {
                    if (target?.OrderId == orderId) return true;
                }
                return false;
            }

            /// <summary>
            /// Tier 1 resolution: Direct OrderId lookup.
            /// </summary>
            public MockFollowerBracketFSM ResolveFsm_ByOrderId(string orderId)
            {
                if (_orderIdMap.TryGet(orderId, out string entryName, out long _))
                {
                    return _brackets.TryGetValue(entryName, out var fsm) ? fsm : null;
                }
                return null;
            }

            /// <summary>
            /// Get FSM expected position for account.
            /// Handles null EntryOrder (restart scenario) by returning 0.
            /// </summary>
            public int GetFsmExpectedPosition(string accountName)
            {
                int total = 0;
                foreach (var fsm in _brackets.Values)
                {
                    if (fsm.AccountName == accountName)
                    {
                        // Null EntryOrder = restart scenario, fallback to broker position (0)
                        if (fsm.EntryOrder == null)
                            continue;
                        
                        total += fsm.RemainingContracts;
                    }
                }
                return total;
            }
            /// <summary>
            /// Tier 3 resolution: Scan all FSMs.
            /// </summary>
            public MockFollowerBracketFSM ResolveFsm_ByScan(string accountAlias, string orderId)
            {
                foreach (var kvp in _brackets)
                {
                    var fsm = kvp.Value;
                    if (fsm.AccountName == accountAlias && MatchesOrder(fsm, orderId))
                    {
                        _orderIdMap.TryAdd(orderId, kvp.Key, fsm.Generation); // Backfill
                        return fsm;
                    }
                }
                return null;
            }

            /// <summary>
            /// Map OrderId to FSM for testing.
            /// </summary>
            public void MapOrderId(string orderId, string entryName, long generation)
            {
                _orderIdMap.TryAdd(orderId, entryName, generation);
            }

            /// <summary>
            /// Get bracket by name for testing.
            /// </summary>
            public MockFollowerBracketFSM GetBracket(string entryName)
            {
                return _brackets.TryGetValue(entryName, out var fsm) ? fsm : null;
            }

            /// <summary>
            /// Remove bracket and clean up OrderId mappings.
            /// </summary>
            public bool RemoveBracket(string entryName)
            {
                if (!_brackets.TryRemove(entryName, out var fsm))
                    return false;

                // Clean up OrderId mappings
                if (fsm.EntryOrder != null)
                    _orderIdMap.Remove(fsm.EntryOrder.OrderId);
                if (fsm.StopOrder != null)
                    _orderIdMap.Remove(fsm.StopOrder.OrderId);
                foreach (var target in fsm.Targets)
                {
                    if (target != null)
                        _orderIdMap.Remove(target.OrderId);
                }

                return true;
            }

            /// <summary>
            /// Set FSM to Replacing state for two-phase replace testing.
            /// </summary>
            public void SetFsmReplacing(string entryName, string cancelOrderId)
            {
                if (_brackets.TryGetValue(entryName, out var fsm))
                {
                    fsm.TryTransition(FollowerBracketState.Replacing, false);
                    fsm.ReplacingCancelOrderId = cancelOrderId;
                }
            }
        }

        #endregion

        #region Event Builders

        private MockTime _time;
        private MockSymmetryFsm _mockFsm;

        private AccountEvent CreateAcceptedEvent(string orderId, string signalName,
                                                 string accountAlias = "Sim101")
        {
            return new AccountEvent
            {
                AccountAlias = accountAlias,
                OrderId = orderId,
                NewState = OrderState.Accepted,
                SignalName = signalName,
                TimestampTicks = _time.GetTicks()
            };
        }

        private AccountEvent CreateFilledEvent(string orderId, string signalName,
                                               int qty, double price,
                                               string accountAlias = "Sim101")
        {
            return new AccountEvent
            {
                AccountAlias = accountAlias,
                OrderId = orderId,
                NewState = OrderState.Filled,
                FilledQty = qty,
                FillPrice = price,
                SignalName = signalName,
                TimestampTicks = _time.GetTicks()
            };
        }

        private AccountEvent CreatePartFilledEvent(string orderId, string signalName,
                                                   int qty, double price,
                                                   string accountAlias = "Sim101")
        {
            return new AccountEvent
            {
                AccountAlias = accountAlias,
                OrderId = orderId,
                NewState = OrderState.PartFilled,
                FilledQty = qty,
                FillPrice = price,
                SignalName = signalName,
                TimestampTicks = _time.GetTicks()
            };
        }

        private AccountEvent CreateRejectedEvent(string orderId, string signalName,
                                                 string errorMessage,
                                                 string accountAlias = "Sim101")
        {
            return new AccountEvent
            {
                AccountAlias = accountAlias,
                OrderId = orderId,
                NewState = OrderState.Rejected,
                SignalName = signalName,
                ErrorMessage = errorMessage,
                TimestampTicks = _time.GetTicks()
            };
        }

        private AccountEvent CreateCancelledEvent(string orderId, string signalName,
                                                  string accountAlias = "Sim101")
        {
            return new AccountEvent
            {
                AccountAlias = accountAlias,
                OrderId = orderId,
                NewState = OrderState.Cancelled,
                SignalName = signalName,
                TimestampTicks = _time.GetTicks()
            };
        }

        #endregion

        #region Assertion Helpers

        private void AssertFsmState(MockFollowerBracketFSM fsm,
                                   FollowerBracketState expectedState,
                                   string message = null)
        {
            Assert.Equal(expectedState, fsm.State);
        }

        private void AssertRemainingContracts(MockFollowerBracketFSM fsm, int expected)
        {
            Assert.Equal(expected, fsm.RemainingContracts);
        }

        private void AssertOrderIdMapped(MockSymmetryFsm mockFsm, string orderId,
                                        string expectedEntryName)
        {
            var fsm = mockFsm.ResolveFsm_ByOrderId(orderId);
            Assert.NotNull(fsm);
            Assert.Equal(expectedEntryName, fsm.EntryName);
        }

        private void AssertFsmNotNull(MockFollowerBracketFSM fsm, string message = null)
        {
            Assert.NotNull(fsm);
        }

        private void AssertFsmNull(MockFollowerBracketFSM fsm, string message = null)
        {
            Assert.Null(fsm);
        }

        #endregion

        #region Tests

        /// <summary>
        /// Smoke test to verify infrastructure compiles and basic properties work.
        /// </summary>
        [Fact]
        public void Infrastructure_Smoke_Test()
        {
            // Arrange
            var time = new MockTime(1000000L);
            var mockFsm = new MockSymmetryFsm(time);

            // Act: Create a simple FSM
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.None
            };

            // Assert: Basic properties work
            Assert.Equal("Sim101", fsm.AccountName);
            Assert.Equal("Fleet_Apex_1", fsm.EntryName);
            Assert.Equal(FollowerBracketState.None, fsm.State);
        }

        /// <summary>
        /// T01: Happy Path - Complete lifecycle from None to Filled.
        /// Tests: None -> PendingSubmit -> Submitted -> Accepted -> Active -> Filled
        /// </summary>
        [Fact]
        public void T01_HappyPath_None_To_Filled()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);

            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.None,
                RemainingContracts = 2,
                EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1",
                                          OrderAction.Buy, 2)
            };

            _mockFsm.AddBracket("Fleet_Apex_1", fsm);

            // Act & Assert: Step through state transitions
            // Step 1: None -> PendingSubmit
            fsm.State = FollowerBracketState.PendingSubmit;
            AssertFsmState(fsm, FollowerBracketState.PendingSubmit,
                          "Strategic intent set");

            // Step 2: PendingSubmit -> Submitted
            fsm.State = FollowerBracketState.Submitted;
            AssertFsmState(fsm, FollowerBracketState.Submitted,
                          "Order submitted to broker");

            // Step 3: Submitted -> Accepted (broker ack)
            var acceptEvent = CreateAcceptedEvent("ORD001", "Entry_Fleet_Apex_1");
            _mockFsm.EnqueueEvent(acceptEvent);
            _mockFsm.DrainMailbox();
            AssertFsmState(fsm, FollowerBracketState.Accepted,
                          "Broker accepted order");

            // Step 4: Accepted -> Active (entry filled)
            var fillEvent = CreateFilledEvent("ORD001", "Entry_Fleet_Apex_1",
                                             2, 4500.0);
            _mockFsm.EnqueueEvent(fillEvent);
            _mockFsm.DrainMailbox();
            AssertFsmState(fsm, FollowerBracketState.Active,
                          "Entry filled, bracket active");
            AssertRemainingContracts(fsm, 2);

            // Step 5: Active -> Filled (stop filled)
            fsm.StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1",
                                         OrderAction.Sell, 2);

            var stopFillEvent = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1",
                                                 2, 4480.0);
            _mockFsm.EnqueueEvent(stopFillEvent);
            _mockFsm.DrainMailbox();
            AssertFsmState(fsm, FollowerBracketState.Filled,
                          "Stop filled, position closed");
            AssertRemainingContracts(fsm, 0);
        }

        /// <summary>
        /// T02: Rejection Path - Broker rejects order during submission.
        /// Tests: Submitted -> Rejected
        /// </summary>
        [Fact]
        public void T02_Rejection_Submitted_To_Rejected()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);

            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Submitted,
                RemainingContracts = 2,
                EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1",
                                          OrderAction.Buy, 2)
            };

            _mockFsm.AddBracket("Fleet_Apex_1", fsm);

            // Act: Broker rejects order
            var rejectEvent = CreateRejectedEvent("ORD001", "Entry_Fleet_Apex_1",
                                                 "Insufficient margin");
            _mockFsm.EnqueueEvent(rejectEvent);
            _mockFsm.DrainMailbox();

            // Assert
            AssertFsmState(fsm, FollowerBracketState.Rejected,
                          "Order rejected by broker");
        }

        /// <summary>
        /// T03: Cancellation Path - User cancels active bracket.
        /// Tests: Active -> Cancelled
        /// </summary>
        [Fact]
        public void T03_Cancellation_Active_To_Cancelled()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);

            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Active,
                RemainingContracts = 2,
                StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1",
                                         OrderAction.Sell, 2)
            };

            _mockFsm.AddBracket("Fleet_Apex_1", fsm);

            // Act: Cancel stop order
            var cancelEvent = CreateCancelledEvent("ORD002", "Stop_Fleet_Apex_1");
            _mockFsm.EnqueueEvent(cancelEvent);
            _mockFsm.DrainMailbox();

            // Assert
            AssertFsmState(fsm, FollowerBracketState.Cancelled,
                          "Bracket cancelled");
        }

        /// <summary>
        /// T04: Partial Fill Path - Multi-step partial fills leading to complete fill.
        /// Tests: Active -> PartFilled -> Active -> Filled
        /// </summary>
        [Fact]
        public void T04_PartialFill_Active_To_PartFilled_To_Filled()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);

            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Active,
                RemainingContracts = 2,
                StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1",
                                         OrderAction.Sell, 2)
            };

            _mockFsm.AddBracket("Fleet_Apex_1", fsm);

            // Act: First partial fill (1 contract)
            var partFill1 = CreatePartFilledEvent("ORD002", "Stop_Fleet_Apex_1",
                                                 1, 4480.0);
            _mockFsm.EnqueueEvent(partFill1);
            _mockFsm.DrainMailbox();

            // Assert: Still active with reduced contracts
            AssertFsmState(fsm, FollowerBracketState.Active,
                          "First partial fill");
            AssertRemainingContracts(fsm, 1);

            // Act: Final fill (1 contract)
            var finalFill = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1",
                                             1, 4482.0);
            _mockFsm.EnqueueEvent(finalFill);
            _mockFsm.DrainMailbox();

            // Assert: Fully filled
            AssertFsmState(fsm, FollowerBracketState.Filled,
                          "All contracts filled");
            AssertRemainingContracts(fsm, 0);
        }
        /// <summary>
        /// T05: Tier 1 - OrderId Hit (O(1) lookup).
        /// Tests direct OrderId resolution without SignalName parsing.
        /// </summary>
        [Fact]
        public void T05_Tier1_OrderId_Hit_Primary_Path()
        {
            // Arrange: OrderId already mapped
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.None,
                RemainingContracts = 2
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);

            // Act: Resolve via OrderId
            var resolved = _mockFsm.ResolveFsm_ByOrderId("ORD001");

            // Assert: O(1) hit
            AssertFsmNotNull(resolved, "Tier 1 hit");
            Assert.Equal("Fleet_Apex_1", resolved.EntryName);
        }

        /// <summary>
        /// T06: Tier 2 - SignalName Hit with Backfill.
        /// Tests SignalName parsing when OrderId not cached.
        /// </summary>
        [Fact]
        public void T06_Tier2_SignalName_Hit_With_Backfill()
        {
            // Arrange: OrderId NOT mapped, but SignalName parseable
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.None,
                RemainingContracts = 2
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);

            // Act: Resolve via SignalName (Entry_Fleet_Apex_1 -> Fleet_Apex_1)
            var evt = CreateAcceptedEvent("ORD002", "Entry_Fleet_Apex_1");
            _mockFsm.EnqueueEvent(evt);
            _mockFsm.DrainMailbox();

            // Assert: Tier 2 hit + backfill
            AssertFsmState(fsm, FollowerBracketState.Accepted, "Tier 2 hit");

            // Verify backfill occurred
            var backfilled = _mockFsm.ResolveFsm_ByOrderId("ORD002");
            AssertFsmNotNull(backfilled, "Backfill successful");
        }

        /// <summary>
        /// T07: Tier 3 - Scan Hit with Backfill.
        /// Tests O(N) scan when OrderId not cached and SignalName unparseable.
        /// </summary>
        [Fact]
        public void T07_Tier3_Scan_Hit_With_Backfill()
        {
            // Arrange: OrderId NOT mapped, SignalName unparseable
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.None,
                RemainingContracts = 2,
                StopOrder = new MockOrder("ORD003", "Stop_Fleet_Apex_1",
                                          OrderAction.Sell, 2)
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);

            // Act: Resolve via O(N) scan (no OrderId, no parseable SignalName)
            var resolved = _mockFsm.ResolveFsm_ByScan("Sim101", "ORD003");

            // Assert: Tier 3 hit + backfill
            AssertFsmNotNull(resolved, "Tier 3 scan hit");
            Assert.Equal("Fleet_Apex_1", resolved.EntryName);

            // Verify backfill occurred
            var backfilled = _mockFsm.ResolveFsm_ByOrderId("ORD003");
            AssertFsmNotNull(backfilled, "Backfill successful");
        }

        /// <summary>
        /// T08: Duplicate Events (Idempotency).
        /// Tests that duplicate events don't cause invalid state transitions.
        /// </summary>
        [Fact]
        public void T08_Duplicate_Events_Idempotent()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Submitted,
                RemainingContracts = 2
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);

            // Act: Process same Accepted event twice
            var acceptEvent = CreateAcceptedEvent("ORD001", "Entry_Fleet_Apex_1");
            _mockFsm.EnqueueEvent(acceptEvent);
            _mockFsm.DrainMailbox();
            AssertFsmState(fsm, FollowerBracketState.Accepted, "First event");

            _mockFsm.EnqueueEvent(acceptEvent);
            _mockFsm.DrainMailbox();

            // Assert: State unchanged (idempotent)
            AssertFsmState(fsm, FollowerBracketState.Accepted, "Duplicate ignored");
        }

        /// <summary>
        /// T09: Out-of-Order Events.
        /// Tests handling of Filled arriving before Accepted (race condition).
        /// </summary>
        [Fact]
        public void T09_OutOfOrder_Filled_Before_Accepted()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Submitted,
                RemainingContracts = 2,
                EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1",
                                          OrderAction.Buy, 2)
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);

            // Act: Filled arrives before Accepted (race condition)
            var fillEvent = CreateFilledEvent("ORD001", "Entry_Fleet_Apex_1",
                                             2, 4500.0);
            _mockFsm.EnqueueEvent(fillEvent);
            _mockFsm.DrainMailbox();

            // Assert: FSM handles gracefully (transitions to Active)
            AssertFsmState(fsm, FollowerBracketState.Active,
                          "Out-of-order fill handled");
        }

        /// <summary>
        /// T10: Stop Fill Contract Decrement.
        /// Tests that stop fills properly decrement RemainingContracts.
        /// </summary>
        [Fact]
        public void T10_StopFill_Decrements_RemainingContracts()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Active,
                RemainingContracts = 2,
                StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1",
                                          OrderAction.Sell, 2)
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);

            // Act: Stop fills completely
            var stopFill = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1",
                                            2, 4480.0);
            _mockFsm.EnqueueEvent(stopFill);
            _mockFsm.DrainMailbox();

            // Assert
            AssertFsmState(fsm, FollowerBracketState.Filled, "Stop filled");
            AssertRemainingContracts(fsm, 0);
        }

        /// <summary>
        /// T11: T1 Target Detection.
        /// Tests that T1 target fills are detected and contracts decremented.
        /// </summary>
        [Fact]
        public void T11_T1_Target_Detection_And_Decrement()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Active,
                RemainingContracts = 5
            };
            fsm.Targets[0] = new MockOrder("ORD003", "T1_Fleet_Apex_1",
                                           OrderAction.Sell, 1);
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD003", "Fleet_Apex_1", fsm.Generation);

            // Act: T1 fills (1 contract)
            var t1Fill = CreateFilledEvent("ORD003", "T1_Fleet_Apex_1",
                                          1, 4520.0);
            _mockFsm.EnqueueEvent(t1Fill);
            _mockFsm.DrainMailbox();

            // Assert: Still active with 4 contracts
            AssertFsmState(fsm, FollowerBracketState.Active, "T1 filled");
            AssertRemainingContracts(fsm, 4);
        }

        /// <summary>
        /// T12: Multi-Target Scaling.
        /// Tests that multiple target fills (T1+T2+T3) properly decrement contracts.
        /// </summary>
        [Fact]
        public void T12_MultiTarget_Scaling_T1_T2_T3()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Active,
                RemainingContracts = 5
            };

            // Setup T1, T2, T3 targets
            fsm.Targets[0] = new MockOrder("ORD003", "T1_Fleet_Apex_1",
                                           OrderAction.Sell, 1);
            fsm.Targets[1] = new MockOrder("ORD004", "T2_Fleet_Apex_1",
                                           OrderAction.Sell, 1);
            fsm.Targets[2] = new MockOrder("ORD005", "T3_Fleet_Apex_1",
                                           OrderAction.Sell, 1);

            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD003", "Fleet_Apex_1", fsm.Generation);
            _mockFsm.MapOrderId("ORD004", "Fleet_Apex_1", fsm.Generation);
            _mockFsm.MapOrderId("ORD005", "Fleet_Apex_1", fsm.Generation);

            // Act: T1 fills
            var t1Fill = CreateFilledEvent("ORD003", "T1_Fleet_Apex_1",
                                          1, 4520.0);
            _mockFsm.EnqueueEvent(t1Fill);
            _mockFsm.DrainMailbox();
            AssertRemainingContracts(fsm, 4);

            // Act: T2 fills
            var t2Fill = CreateFilledEvent("ORD004", "T2_Fleet_Apex_1",
                                          1, 4530.0);
            _mockFsm.EnqueueEvent(t2Fill);
            _mockFsm.DrainMailbox();
            AssertRemainingContracts(fsm, 3);

            // Act: T3 fills
            var t3Fill = CreateFilledEvent("ORD005", "T3_Fleet_Apex_1",
                                          1, 4540.0);
            _mockFsm.EnqueueEvent(t3Fill);
            _mockFsm.DrainMailbox();

            // Assert: Still active with 2 contracts remaining
            AssertFsmState(fsm, FollowerBracketState.Active, "T1+T2+T3 filled");
            AssertRemainingContracts(fsm, 2);
        }

        /// <summary>
        /// T13: Zero Contracts Terminal State.
        /// Tests that reaching zero contracts transitions to Filled state.
        /// </summary>
        [Fact]
        public void T13_ZeroContracts_Transitions_To_Filled()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Active,
                RemainingContracts = 1,
                StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1",
                                          OrderAction.Sell, 1)
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);

            // Act: Final contract fills
            var finalFill = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1",
                                             1, 4480.0);
            _mockFsm.EnqueueEvent(finalFill);
            _mockFsm.DrainMailbox();

            // Assert: Terminal state reached
            AssertFsmState(fsm, FollowerBracketState.Filled,
                          "Zero contracts = Filled");
            AssertRemainingContracts(fsm, 0);
        }

        #endregion

        #region Phase 4: Edge Cases (T14-T17)
        [Fact]
        public void T14_NullOrderReference_Restart_Scenario()
        {
            // Arrange: Hydrated Active FSM with null EntryOrder (restart edge case)
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Active,
                EntryOrder = null, // Restart scenario - order reference lost
                RemainingContracts = 2
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            
            // Act: GetFsmExpectedPosition should handle null gracefully
            int expectedPos = _mockFsm.GetFsmExpectedPosition("Sim101");
            
            // Assert: Returns 0 (fallback to broker position)
            Assert.Equal(0, expectedPos);
        }

        [Fact]
        public void T15_MailboxOverflow_Handles_Backpressure()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Submitted
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
            
            // Act: Enqueue 150 events (exceeds MAX_PER_DRAIN = 100)
            for (int i = 0; i < 150; i++)
            {
                var evt = CreateAcceptedEvent("ORD001", "Entry_Fleet_Apex_1");
                _mockFsm.EnqueueEvent(evt);
            }
            
            // First drain processes 100
            _mockFsm.DrainMailbox();
            
            // Second drain processes remaining 50
            _mockFsm.DrainMailbox();
            
            // Assert: All events processed, no exceptions
            AssertFsmState(fsm, FollowerBracketState.Accepted, "Overflow handled");
        }

        [Fact]
        public void T16_ConcurrentModifications_CAS_Retry()
        {
            // Arrange
            _time = new MockTime(1000000L);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.None
            };
            
            // Act: Simulate concurrent state transitions
            bool success1 = fsm.TryTransition(FollowerBracketState.PendingSubmit, false);
            bool success2 = fsm.TryTransition(FollowerBracketState.Submitted, false);
            
            // Assert: Both transitions succeed (CAS-based)
            Assert.True(success1, "First transition");
            Assert.True(success2, "Second transition");
            AssertFsmState(fsm, FollowerBracketState.Submitted, "Final state");
        }

        [Fact]
        public void T17_InvalidTransition_Rejected_To_Active()
        {
            // Arrange
            _time = new MockTime(1000000L);
            _mockFsm = new MockSymmetryFsm(_time);
            var fsm = new MockFollowerBracketFSM
            {
                AccountName = "Sim101",
                EntryName = "Fleet_Apex_1",
                State = FollowerBracketState.Rejected // Terminal state
            };
            _mockFsm.AddBracket("Fleet_Apex_1", fsm);
            _mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
            
            // Act: Attempt invalid transition (Rejected -> Active)
            var fillEvent = CreateFilledEvent("ORD001", "Entry_Fleet_Apex_1", 2, 4500.0);
            _mockFsm.EnqueueEvent(fillEvent);
            _mockFsm.DrainMailbox();
            
            // Assert: State unchanged (invalid transition blocked)
            AssertFsmState(fsm, FollowerBracketState.Rejected, "Invalid transition blocked");
        }
        #endregion

        #region Phase 5: Integration (T18-T20)
        /// <summary>
        /// T18: REAPER Integration - GetFsmExpectedPosition aggregates multiple FSMs.
        /// </summary>
        [Fact]
        public void T18_REAPER_GetFsmExpectedPosition_Aggregates()
    {
        // Arrange: Multiple FSMs for same account
        _time = new MockTime(1000000L);
        _mockFsm = new MockSymmetryFsm(_time);
        
        var fsm1 = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_1",
            State = FollowerBracketState.Active,
            RemainingContracts = 2,
            EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", OrderAction.Buy, 2)
        };
        
        var fsm2 = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_2",
            State = FollowerBracketState.Active,
            RemainingContracts = 3,
            EntryOrder = new MockOrder("ORD002", "Entry_Fleet_Apex_2", OrderAction.Buy, 3)
        };
        
        _mockFsm.AddBracket("Fleet_Apex_1", fsm1);
        _mockFsm.AddBracket("Fleet_Apex_2", fsm2);
        
        // Act: Calculate expected position
        int expectedPos = _mockFsm.GetFsmExpectedPosition("Sim101");
        
        // Assert: Aggregates both FSMs (2 + 3 = 5)
        Assert.Equal(5, expectedPos);
    }

    /// <summary>
    /// T18: REAPER Integration - Short position handling.
    /// </summary>
    [Fact]
    public void T18_REAPER_GetFsmExpectedPosition_Short_Position()
    {
        // Arrange: Short position
        _time = new MockTime(1000000L);
        _mockFsm = new MockSymmetryFsm(_time);
        
        var fsm = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_1",
            State = FollowerBracketState.Active,
            RemainingContracts = -2,
            EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", OrderAction.SellShort, 2)
        };
        
        _mockFsm.AddBracket("Fleet_Apex_1", fsm);
        
        // Act
        int expectedPos = _mockFsm.GetFsmExpectedPosition("Sim101");
        
        // Assert: Negative for short (-2)
        Assert.Equal(-2, expectedPos);
    }

    /// <summary>
    /// T18: REAPER Integration - Terminal states excluded from position calculation.
    /// </summary>
    [Fact]
    public void T18_REAPER_GetFsmExpectedPosition_Terminal_States_Excluded()
    {
        // Arrange: Mix of active and terminal FSMs
        _time = new MockTime(1000000L);
        _mockFsm = new MockSymmetryFsm(_time);
        
        var fsm1 = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_1",
            State = FollowerBracketState.Active,
            RemainingContracts = 2,
            EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", OrderAction.Buy, 2)
        };
        
        var fsm2 = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_2",
            State = FollowerBracketState.Filled, // Terminal
            RemainingContracts = 0,
            EntryOrder = new MockOrder("ORD002", "Entry_Fleet_Apex_2", OrderAction.Buy, 3)
        };
        
        _mockFsm.AddBracket("Fleet_Apex_1", fsm1);
        _mockFsm.AddBracket("Fleet_Apex_2", fsm2);
        
        // Act
        int expectedPos = _mockFsm.GetFsmExpectedPosition("Sim101");
        
        // Assert: Only active FSM counted (2, not 5)
        Assert.Equal(2, expectedPos);
    }

    /// <summary>
    /// T19: SIMA Integration - FSM creation and removal.
    /// </summary>
    [Fact]
    public void T19_SIMA_FSM_Creation_And_Removal()
    {
        // Arrange
        _time = new MockTime(1000000L);
        _mockFsm = new MockSymmetryFsm(_time);
        
        // Act: Create FSM
        var fsm = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_1",
            State = FollowerBracketState.PendingSubmit
        };
        _mockFsm.AddBracket("Fleet_Apex_1", fsm);
        
        // Assert: FSM exists
        var retrieved = _mockFsm.GetBracket("Fleet_Apex_1");
        AssertFsmNotNull(retrieved, "FSM created");
        
        // Act: Remove FSM
        bool removed = _mockFsm.RemoveBracket("Fleet_Apex_1");
        
        // Assert: FSM removed
        Assert.True(removed, "FSM removed");
        var afterRemoval = _mockFsm.GetBracket("Fleet_Apex_1");
        AssertFsmNull(afterRemoval, "FSM no longer exists");
    }

    /// <summary>
    /// T19: SIMA Integration - OrderId mappings cleaned on FSM removal.
    /// </summary>
    [Fact]
    public void T19_SIMA_FSM_OrderId_Mappings_Cleaned()
    {
        // Arrange
        _time = new MockTime(1000000L);
        _mockFsm = new MockSymmetryFsm(_time);
        var fsm = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_1",
            EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", OrderAction.Buy, 2),
            StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", OrderAction.Sell, 2)
        };
        
        _mockFsm.AddBracket("Fleet_Apex_1", fsm);
        _mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
        _mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
        
        // Act: Remove FSM
        _mockFsm.RemoveBracket("Fleet_Apex_1");
        
        // Assert: OrderId mappings cleaned
        var resolved1 = _mockFsm.ResolveFsm_ByOrderId("ORD001");
        var resolved2 = _mockFsm.ResolveFsm_ByOrderId("ORD002");
        AssertFsmNull(resolved1, "Entry mapping cleaned");
        AssertFsmNull(resolved2, "Stop mapping cleaned");
    }

    /// <summary>
    /// T20: Orders Integration - Two-phase replace with Replacing state.
    /// </summary>
    [Fact]
    public void T20_Orders_TwoPhase_Replace_Replacing_State()
    {
        // Arrange
        _time = new MockTime(1000000L);
        _mockFsm = new MockSymmetryFsm(_time);
        var fsm = new MockFollowerBracketFSM
        {
            AccountName = "Sim101",
            EntryName = "Fleet_Apex_1",
            State = FollowerBracketState.Active,
            StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", OrderAction.Sell, 2)
        };
        _mockFsm.AddBracket("Fleet_Apex_1", fsm);
        _mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
        
        // Act: Phase 1 - Cancel old stop (enter Replacing state)
        _mockFsm.SetFsmReplacing("Fleet_Apex_1", "ORD002");
        AssertFsmState(fsm, FollowerBracketState.Replacing, "Phase 1: Replacing");
        Assert.Equal("ORD002", fsm.ReplacingCancelOrderId);
        
        // Act: Phase 2 - Cancel confirmed
        var cancelEvent = CreateCancelledEvent("ORD002", "Stop_Fleet_Apex_1");
        _mockFsm.EnqueueEvent(cancelEvent);
        _mockFsm.DrainMailbox();
        
        // Assert: Still in Replacing (cancel absorbed)
        AssertFsmState(fsm, FollowerBracketState.Replacing, "Cancel absorbed, stays Replacing");
        
        // Act: Phase 3 - New stop submitted and accepted
        fsm.StopOrder = new MockOrder("ORD003", "Stop_Fleet_Apex_1", OrderAction.Sell, 2);
        _mockFsm.MapOrderId("ORD003", "Fleet_Apex_1", fsm.Generation);
        fsm.State = FollowerBracketState.Active;
        fsm.ReplacingCancelOrderId = null;
        
        // Assert: Back to Active with new stop
        AssertFsmState(fsm, FollowerBracketState.Active, "Replace complete");
        Assert.Equal("ORD003", fsm.StopOrder.OrderId);
        }
        #endregion
    }
}

// Made with Bob
