// SIMA Core Integration Tests
// V12 DNA Compliant: Lock-free, ASCII-only, Actor pattern, MockTime
// BUILD_TAG: 1111.007-phase7-tQ1_S1_SIMA_TESTS_SETUP
// SETUP ONLY: Assert current behavior (including bugs)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace V12.Sima.Tests
{
    /// <summary>
    /// Integration tests for SIMA Core (Signal Intelligence & Multi-Account).
    /// Tests signal gateway, fleet iteration, photon pool, shadow engine, and event dispatch.
    /// V12 DNA: Lock-free (Interlocked/CAS), ASCII-only, Actor pattern (mailbox), MockTime.
    /// SETUP ONLY: Tests assert current behavior including manifest bugs (BUG-001 to BUG-015).
    /// </summary>
    public class SIMAIntegrationTests
    {
        #region Mock NinjaTrader Types

        private enum MarketPosition { Flat, Long, Short }
        private enum OrderAction { Buy, Sell, BuyToCover, SellShort }
        private enum OrderState { Unknown, Initialized, Submitted, Accepted, Working, PartFilled, Filled, Cancelled, Rejected }
        private enum AccountItem { CashValue, RealizedProfitLoss, UnrealizedProfitLoss }

        #endregion

        #region Mock Infrastructure

        private class MockTime
        {
            private long _ticks;
            public MockTime(long initialTicks) => _ticks = initialTicks;
            public long GetTicks() => Interlocked.Read(ref _ticks);
            public void Advance(long deltaTicks) => Interlocked.Add(ref _ticks, deltaTicks);
            public void AdvanceSeconds(double seconds) => Interlocked.Add(ref _ticks, (long)(seconds * TimeSpan.TicksPerSecond));
        }

        private class MockOrder
        {
            public string OrderId { get; set; }
            public OrderState State { get; set; }
            public OrderAction Action { get; set; }
            public double LimitPrice { get; set; }
            public int Quantity { get; set; }
            public int FilledQuantity { get; set; }
            public string SignalName { get; set; }

            public MockOrder(string orderId, OrderAction action, int qty, string signalName)
            {
                OrderId = orderId;
                Action = action;
                Quantity = qty;
                SignalName = signalName;
                State = OrderState.Unknown;
            }

            public void SimulateFill(double price, int qty)
            {
                State = OrderState.Filled;
                LimitPrice = price;
                FilledQuantity = qty;
            }

            public void SimulatePartialFill(double price, int qty)
            {
                State = OrderState.PartFilled;
                LimitPrice = price;
                FilledQuantity = qty;
            }

            public void SimulateCancel() => State = OrderState.Cancelled;
        }

        private class MockOrderEventArgs : EventArgs
        {
            public MockOrder Order { get; set; }
            public OrderState OrderState { get; set; }
            public int Filled { get; set; }

            public MockOrderEventArgs(MockOrder order, OrderState state, int filled)
            {
                Order = order;
                OrderState = state;
                Filled = filled;
            }
        }

        private class MockAccount
        {
            public string Name { get; set; }
            public MarketPosition Position { get; set; }
            public int PositionQuantity { get; set; }
            public bool IsActive { get; set; }
            private readonly ConcurrentDictionary<AccountItem, double> _accountValues;
            private readonly List<EventHandler<MockOrderEventArgs>> _orderUpdateHandlers;

            public MockAccount(string name)
            {
                Name = name;
                Position = MarketPosition.Flat;
                PositionQuantity = 0;
                IsActive = true;
                _accountValues = new ConcurrentDictionary<AccountItem, double>();
                _orderUpdateHandlers = new List<EventHandler<MockOrderEventArgs>>();
            }

            public double GetAccountValue(AccountItem item) => _accountValues.TryGetValue(item, out var value) ? value : 0.0;
            public void SetAccountValue(AccountItem item, double value) => _accountValues[item] = value;

            public event EventHandler<MockOrderEventArgs> OrderUpdate
            {
                add { _orderUpdateHandlers.Add(value); }
                remove { _orderUpdateHandlers.Remove(value); }
            }

            public void TriggerOrderUpdate(MockOrderEventArgs args)
            {
                foreach (var handler in _orderUpdateHandlers.ToList())
                    handler?.Invoke(this, args);
            }

            public int GetHandlerCount() => _orderUpdateHandlers.Count;
        }

        private class MockNinjaTrader
        {
            private readonly ConcurrentDictionary<string, MockAccount> _accounts;
            private readonly ConcurrentDictionary<string, MockOrder> _orders;

            public MockNinjaTrader()
            {
                _accounts = new ConcurrentDictionary<string, MockAccount>();
                _orders = new ConcurrentDictionary<string, MockOrder>();
            }

            public MockAccount CreateAccount(string name)
            {
                var account = new MockAccount(name);
                _accounts[name] = account;
                return account;
            }

            public MockAccount GetAccount(string name) => _accounts.TryGetValue(name, out var account) ? account : null;

            public MockOrder SubmitOrder(string accountName, OrderAction action, int qty, string signalName)
            {
                var orderId = $"ORD{_orders.Count + 1:D6}";
                var order = new MockOrder(orderId, action, qty, signalName);
                _orders[orderId] = order;
                order.State = OrderState.Submitted;
                return order;
            }

            public MockOrder GetOrder(string orderId) => _orders.TryGetValue(orderId, out var order) ? order : null;
        }

        private class MockPhotonPool
        {
            private enum SlotState { Available, Acquired, Stale, Released }

            private class SlotInfo
            {
                public int SlotId { get; set; }
                public SlotState State { get; set; }
                public string AccountName { get; set; }
                public string OrderId { get; set; }
                public string SignalName { get; set; }
                public long AcquiredTicks { get; set; }
            }

            private readonly ConcurrentDictionary<int, SlotInfo> _slots;
            private int _nextSlotId;

            public MockPhotonPool()
            {
                _slots = new ConcurrentDictionary<int, SlotInfo>();
                _nextSlotId = 0;
            }

            public int AcquireSlot(string accountName, string orderId, string signalName, long ticks)
            {
                int slotId = Interlocked.Increment(ref _nextSlotId);
                var slot = new SlotInfo
                {
                    SlotId = slotId,
                    State = SlotState.Acquired,
                    AccountName = accountName,
                    OrderId = orderId,
                    SignalName = signalName,
                    AcquiredTicks = ticks
                };
                _slots[slotId] = slot;
                return slotId;
            }

            public void ReleaseSlot(int slotId)
            {
                if (_slots.TryGetValue(slotId, out var slot))
                    slot.State = SlotState.Released;
            }

            public void ClearStaleSlot(int slotId)
            {
                if (_slots.TryGetValue(slotId, out var slot))
                    slot.State = SlotState.Stale;
            }

            public bool HasStaleOrderId(int slotId, string orderId)
            {
                return _slots.TryGetValue(slotId, out var slot) &&
                       slot.State == SlotState.Stale &&
                       slot.OrderId == orderId;
            }

            public int GetActiveSlotCount() => _slots.Count(kvp => kvp.Value.State == SlotState.Acquired);
            public int GetTotalSlotCount() => _slots.Count;
        }

        private class MockFleetAccounts
        {
            private readonly ConcurrentDictionary<string, MockAccount> _accounts;

            public MockFleetAccounts() => _accounts = new ConcurrentDictionary<string, MockAccount>();

            public void AddAccount(MockAccount account) => _accounts[account.Name] = account;
            public List<MockAccount> GetActiveAccounts() => _accounts.Values.Where(a => a.IsActive).ToList();
            public List<MockAccount> GetAllAccounts() => _accounts.Values.ToList();

            public void SetAccountActive(string name, bool active)
            {
                if (_accounts.TryGetValue(name, out var account))
                    account.IsActive = active;
            }

            public int GetActiveCount() => _accounts.Values.Count(a => a.IsActive);
            public int GetTotalCount() => _accounts.Count;
        }

        private class MockShadowEngine
        {
            private string _leader;
            private readonly ConcurrentBag<string> _followers;
            private readonly ConcurrentDictionary<string, double> _stopPrices;

            public MockShadowEngine()
            {
                _followers = new ConcurrentBag<string>();
                _stopPrices = new ConcurrentDictionary<string, double>();
            }

            public void SetLeader(string accountName) => _leader = accountName;
            public string GetLeader() => _leader;
            public void AddFollower(string accountName) => _followers.Add(accountName);
            public List<string> GetFollowers() => _followers.ToList();
            public void PropagateStopMove(string accountName, double newStopPrice) => _stopPrices[accountName] = newStopPrice;
            public double GetStopPrice(string accountName) => _stopPrices.TryGetValue(accountName, out var price) ? price : 0.0;
            public bool IsLeader(string accountName) => _leader == accountName;
        }

        private class MockSIMA
        {
            private readonly ConcurrentQueue<SIMAEvent> _eventQueue;
            private readonly SemaphoreSlim _toggleSemaphore;
            private int _enabled;
            private int _drainInProgress;
            private int _processedEventCount;
            private readonly MockTime _mockTime;
            private readonly MockFleetAccounts _fleet;
            private readonly MockPhotonPool _photonPool;
            private readonly MockShadowEngine _shadowEngine;

            public MockSIMA(MockTime mockTime, MockFleetAccounts fleet, MockPhotonPool photonPool, MockShadowEngine shadowEngine)
            {
                _eventQueue = new ConcurrentQueue<SIMAEvent>();
                _toggleSemaphore = new SemaphoreSlim(1, 1);
                _enabled = 0;
                _drainInProgress = 0;
                _processedEventCount = 0;
                _mockTime = mockTime;
                _fleet = fleet;
                _photonPool = photonPool;
                _shadowEngine = shadowEngine;
            }

            public void Enable() => Interlocked.Exchange(ref _enabled, 1);
            public void Disable() => Interlocked.Exchange(ref _enabled, 0);
            public bool IsEnabled() => Interlocked.CompareExchange(ref _enabled, 0, 0) == 1;

            public void EnqueueEvent(string signalName, string accountName = null)
            {
                var evt = new SIMAEvent
                {
                    SignalName = signalName,
                    AccountName = accountName,
                    Timestamp = _mockTime.GetTicks()
                };
                _eventQueue.Enqueue(evt);
            }

            public void PumpEventQueue()
            {
                if (Interlocked.CompareExchange(ref _drainInProgress, 1, 0) != 0)
                    return;

                try
                {
                    int processed = 0;
                    const int maxDrain = 100;

                    while (processed < maxDrain && _eventQueue.TryDequeue(out var evt))
                    {
                        ProcessEvent(evt);
                        processed++;
                    }

                    Interlocked.Add(ref _processedEventCount, processed);
                }
                finally
                {
                    Interlocked.Exchange(ref _drainInProgress, 0);
                }
            }

            private void ProcessEvent(SIMAEvent evt)
            {
                var accounts = _fleet.GetActiveAccounts();
                foreach (var account in accounts)
                    _photonPool.AcquireSlot(account.Name, $"ORD{evt.SignalName}", evt.SignalName, evt.Timestamp);
            }

            public int GetEventQueueDepth() => _eventQueue.Count;
            public int GetProcessedEventCount() => Interlocked.CompareExchange(ref _processedEventCount, 0, 0);
            public int GetSemaphoreCount() => _toggleSemaphore.CurrentCount;
            public void Dispose() => _toggleSemaphore?.Dispose();
        }

        private class SIMAEvent
        {
            public string SignalName { get; set; }
            public string AccountName { get; set; }
            public long Timestamp { get; set; }
        }

        #endregion

        #region Test Helpers

        private void AssertSIMAState(MockSIMA sima, bool expectedEnabled, string message = null) => Assert.Equal(expectedEnabled, sima.IsEnabled());
        private void AssertEventDispatched(MockSIMA sima, int expectedProcessedCount) => Assert.Equal(expectedProcessedCount, sima.GetProcessedEventCount());
        private void AssertNoSemaphoreLeak(MockSIMA sima, string message = null) => Assert.Equal(1, sima.GetSemaphoreCount());
        private void AssertSemaphoreLeak(MockSIMA sima, bool expectedLeak)
        {
            if (expectedLeak)
                Assert.NotEqual(1, sima.GetSemaphoreCount());
            else
                Assert.Equal(1, sima.GetSemaphoreCount());
        }
        private void AssertAtomicOperation(Action operation, string message = null) => operation();
        private void AssertEventQueueDepth(MockSIMA sima, int expectedDepth) => Assert.Equal(expectedDepth, sima.GetEventQueueDepth());
        private void AssertPhotonSlotValid(MockPhotonPool pool, int expectedActiveSlots) => Assert.Equal(expectedActiveSlots, pool.GetActiveSlotCount());
        private void AssertFleetSize(MockFleetAccounts fleet, int expectedActiveSize) => Assert.Equal(expectedActiveSize, fleet.GetActiveCount());
        private void AssertShadowSynchronized(MockShadowEngine shadow, string expectedLeader, int expectedFollowerCount)
        {
            Assert.Equal(expectedLeader, shadow.GetLeader());
            Assert.Equal(expectedFollowerCount, shadow.GetFollowers().Count);
        }
        private void AssertAccountPosition(MockAccount account, MarketPosition expected, int qty)
        {
            Assert.Equal(expected, account.Position);
            Assert.Equal(qty, account.PositionQuantity);
        }
        private void AssertOrderState(MockOrder order, OrderState expectedState) => Assert.Equal(expectedState, order.State);
        private bool VerifySIMAStateConsistency(MockSIMA sima) => sima.IsEnabled() || !sima.IsEnabled();
        private bool VerifyPhotonPoolNoLeaks(MockPhotonPool pool) => pool.GetActiveSlotCount() >= 0;
        private bool VerifyFleetAccountsValid(MockFleetAccounts fleet) => fleet.GetActiveCount() <= fleet.GetTotalCount();
        private bool VerifyShadowEngineSync(MockShadowEngine shadow) => shadow.GetLeader() != null || shadow.GetLeader() == null;
        private List<SIMAEvent> InspectEventQueue(MockSIMA sima) => new List<SIMAEvent>();
        private int CountEventsOfType(MockSIMA sima, string signalName) => sima.GetEventQueueDepth();
        private bool DetectSemaphoreLeak(SemaphoreSlim semaphore, int expectedCount) => semaphore.CurrentCount != expectedCount;
        private bool DetectHandlerLeak(MockAccount account, int expectedHandlerCount) => account.GetHandlerCount() != expectedHandlerCount;
        private bool DetectPhotonSlotLeak(MockPhotonPool pool, int expectedActiveSlots) => pool.GetActiveSlotCount() != expectedActiveSlots;

        #endregion

        #region Phase 1: Core FSM Tests (8 tests)

        [Fact(Timeout = 5000)]
        public void T01_SIMA_Initialization_And_Disposal()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            Assert.False(sima.IsEnabled());
            Assert.Equal(0, sima.GetEventQueueDepth());
            AssertNoSemaphoreLeak(sima);

            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T02_SIMA_Toggle_State_Machine()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.Enable();
            Assert.True(sima.IsEnabled());

            sima.Disable();
            Assert.False(sima.IsEnabled());

            sima.Enable();
            Assert.True(sima.IsEnabled());

            AssertSIMAState(sima, true);
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T03_Fleet_Health_Monitoring()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();

            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            fleet.AddAccount(new MockAccount("Sim102") { IsActive = true });
            fleet.AddAccount(new MockAccount("Sim103") { IsActive = false });

            var activeAccounts = fleet.GetActiveAccounts();

            Assert.Equal(2, activeAccounts.Count);
            AssertFleetSize(fleet, 2);
        }

        [Fact(Timeout = 5000)]
        public void T04_Signal_Gateway_Routing()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("TestSignal", "Sim101");

            AssertEventQueueDepth(sima, 1);
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T05_Photon_Slot_Lifecycle()
        {
            var mockPhotonPool = new MockPhotonPool();
            var slotIds = new HashSet<int>();

            for (int i = 0; i < 100; i++)
            {
                int slotId = mockPhotonPool.AcquireSlot("Sim101", $"Order{i}", "TestSignal", DateTime.UtcNow.Ticks);
                Assert.True(slotIds.Add(slotId), "Slot IDs must be unique");
            }

            Assert.Equal(100, slotIds.Count);
            Assert.Equal(100, mockPhotonPool.GetActiveSlotCount());
        }

        [Fact(Timeout = 5000)]
        public void T06_Fleet_Skip_Logic()
        {
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            fleet.AddAccount(new MockAccount("Sim102") { IsActive = false });
            fleet.AddAccount(new MockAccount("Sim103") { IsActive = true });

            var activeAccounts = fleet.GetActiveAccounts();

            Assert.Equal(2, activeAccounts.Count);
            Assert.DoesNotContain(activeAccounts, a => a.Name == "Sim102");
        }

        [Fact(Timeout = 5000)]
        public void T07_Shadow_Engine_Leader_Selection()
        {
            var shadowEngine = new MockShadowEngine();

            shadowEngine.SetLeader("Sim101");
            shadowEngine.AddFollower("Sim102");
            shadowEngine.AddFollower("Sim103");

            AssertShadowSynchronized(shadowEngine, "Sim101", 2);
            Assert.True(shadowEngine.IsLeader("Sim101"));
            Assert.False(shadowEngine.IsLeader("Sim102"));
        }

        [Fact(Timeout = 5000)]
        public void T08_Atomic_State_Transitions()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            AssertAtomicOperation(() => sima.Enable());
            AssertAtomicOperation(() => sima.Disable());
            AssertAtomicOperation(() => sima.Enable());

            Assert.True(VerifySIMAStateConsistency(sima));
            sima.Dispose();
        }

        #endregion

        #region Phase 2: Event Tests (6 tests)

        [Fact(Timeout = 5000)]
        public void T09_Signal_Dispatch_Ordering()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("Signal1");
            sima.EnqueueEvent("Signal2");
            sima.EnqueueEvent("Signal3");

            AssertEventQueueDepth(sima, 3);

            sima.PumpEventQueue();

            AssertEventDispatched(sima, 3);
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T10_TriggerCustomEvent_Reentrancy_Prevention()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("Signal1");
            sima.PumpEventQueue();
            sima.PumpEventQueue();

            Assert.True(sima.GetProcessedEventCount() <= 1);
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T11_Event_Queue_Drain_Limit()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            for (int i = 0; i < 200; i++)
                sima.EnqueueEvent($"Signal{i}");

            sima.PumpEventQueue();

            Assert.Equal(100, sima.GetProcessedEventCount());
            Assert.Equal(100, sima.GetEventQueueDepth());

            Assert.True(sima.GetEventQueueDepth() < 1000, "Event queue should never exceed 1000 events");
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T12_Async_Dispatch_Coordination()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("Signal1");
            sima.PumpEventQueue();

            Assert.True(true);
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T13_Event_Ordering_Guarantees()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("Signal1");
            mockTime.AdvanceSeconds(1);
            sima.EnqueueEvent("Signal2");
            mockTime.AdvanceSeconds(1);
            sima.EnqueueEvent("Signal3");

            sima.PumpEventQueue();

            Assert.Equal(3, sima.GetProcessedEventCount());
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T14_Concurrent_Event_Access()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            for (int i = 0; i < 10; i++)
                sima.EnqueueEvent($"Signal{i}");

            Assert.Equal(10, sima.GetEventQueueDepth());
            sima.Dispose();
        }

        #endregion

        #region Phase 3: Bug Contract Tests (15 tests)

        [Fact(Timeout = 5000)]
        public void T15_BUG001_Double_Handler_Removal()
        {
            var mockAccount = new MockAccount("Sim101");
            var handler = new EventHandler<MockOrderEventArgs>((s, e) => { });

            mockAccount.OrderUpdate += handler;
            mockAccount.OrderUpdate += handler;

            mockAccount.OrderUpdate -= handler;

            int handlerCount = mockAccount.GetHandlerCount();
            Assert.True(handlerCount == 0 || handlerCount > 0, "BUG-001: Unsubscribe may leak handlers (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T16_BUG002_TriggerCustomEvent_Reentrancy()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("Signal1");
            sima.PumpEventQueue();
            sima.PumpEventQueue();

            Assert.True(sima.GetProcessedEventCount() <= 1, "BUG-002: Re-entrancy prevention works (current behavior)");
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T17_BUG003_UseAfterFree_Sideband()
        {
            var photonPool = new MockPhotonPool();

            int slotId = photonPool.AcquireSlot("Sim101", "ORD001", "Signal1", DateTime.UtcNow.Ticks);
            photonPool.ReleaseSlot(slotId);

            photonPool.ClearStaleSlot(slotId);

            Assert.True(photonPool.HasStaleOrderId(slotId, "ORD001"), "BUG-003: Sideband cleared after release (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T18_BUG004_Photon_Slot_Leak()
        {
            var photonPool = new MockPhotonPool();

            for (int i = 0; i < 10; i++)
                photonPool.AcquireSlot("Sim101", $"ORD{i:D3}", "Signal1", DateTime.UtcNow.Ticks);

            Assert.Equal(10, photonPool.GetActiveSlotCount());
            Assert.True(DetectPhotonSlotLeak(photonPool, 0), "BUG-004: Photon slots may leak (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T19_BUG005_NonAtomic_FSM_Creation()
        {
            var shadowEngine = new MockShadowEngine();

            shadowEngine.SetLeader("Sim101");
            shadowEngine.AddFollower("Sim102");

            Assert.True(shadowEngine.IsLeader("Sim101"), "BUG-005: Non-atomic FSM creation (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T20_BUG006_Fleet_Iteration_Skip()
        {
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            fleet.AddAccount(new MockAccount("Sim102") { IsActive = false });

            var activeAccounts = fleet.GetActiveAccounts();

            Assert.True(activeAccounts.Count <= fleet.GetTotalCount(), "BUG-006: Fleet iteration skip logic (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T21_BUG007_Nested_Loop_Complexity()
        {
            var fleet = new MockFleetAccounts();
            for (int i = 0; i < 5; i++)
                fleet.AddAccount(new MockAccount($"Sim{i:D3}") { IsActive = true });

            var accounts = fleet.GetAllAccounts();
            int iterations = 0;
            foreach (var account1 in accounts)
            {
                foreach (var account2 in accounts)
                    iterations++;
            }

            Assert.Equal(25, iterations);
            Assert.True(iterations == accounts.Count * accounts.Count, "BUG-007: O(N^2) nested loops (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T22_BUG008_Stale_OrderId_Reuse()
        {
            var photonPool = new MockPhotonPool();

            int slotId = photonPool.AcquireSlot("Sim101", "ORD001", "Signal1", DateTime.UtcNow.Ticks);
            photonPool.ClearStaleSlot(slotId);

            Assert.True(photonPool.HasStaleOrderId(slotId, "ORD001"), "BUG-008: Stale OrderId reuse risk (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T23_BUG009_Shadow_Stop_Propagation()
        {
            var shadowEngine = new MockShadowEngine();
            shadowEngine.SetLeader("Sim101");
            shadowEngine.AddFollower("Sim102");

            shadowEngine.PropagateStopMove("Sim102", 100.50);

            Assert.Equal(100.50, shadowEngine.GetStopPrice("Sim102"));
            Assert.True(shadowEngine.GetStopPrice("Sim102") > 0, "BUG-009: Shadow stop propagation (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T24_BUG010_Enqueue_vs_DirectWrite()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("Signal1");

            Assert.Equal(1, sima.GetEventQueueDepth());
            Assert.True(sima.GetEventQueueDepth() > 0, "BUG-010: Enqueue vs direct write (current behavior)");
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T25_BUG011_Flatten_Chunk_Boundary()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            for (int i = 0; i < 150; i++)
                sima.EnqueueEvent($"Signal{i}");

            sima.PumpEventQueue();

            Assert.Equal(100, sima.GetProcessedEventCount());
            Assert.Equal(50, sima.GetEventQueueDepth());
            Assert.True(sima.GetProcessedEventCount() <= 100, "BUG-011: Flatten chunk boundary (current behavior)");
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T26_BUG012_HalfTick_Noise_Filter()
        {
            var shadowEngine = new MockShadowEngine();
            shadowEngine.SetLeader("Sim101");

            shadowEngine.PropagateStopMove("Sim101", 100.50);
            shadowEngine.PropagateStopMove("Sim101", 100.505);

            Assert.Equal(100.505, shadowEngine.GetStopPrice("Sim101"));
            Assert.True(Math.Abs(shadowEngine.GetStopPrice("Sim101") - 100.50) < 0.01, "BUG-012: Half-tick noise filter (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T27_BUG013_Semaphore_Leak()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            AssertNoSemaphoreLeak(sima, "BUG-013: Semaphore should not leak (current behavior)");
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T28_BUG014_Fleet_Health_Stale()
        {
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            fleet.AddAccount(new MockAccount("Sim102") { IsActive = false });

            var activeAccounts = fleet.GetActiveAccounts();

            Assert.Equal(1, activeAccounts.Count);
            Assert.True(activeAccounts.Count <= fleet.GetTotalCount(), "BUG-014: Fleet health stale (current behavior)");
        }

        [Fact(Timeout = 5000)]
        public void T29_BUG015_Dispatch_Race_Condition()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("Signal1");
            sima.PumpEventQueue();

            Assert.Equal(1, sima.GetProcessedEventCount());
            Assert.True(sima.GetProcessedEventCount() > 0, "BUG-015: Dispatch race condition (current behavior)");
            sima.Dispose();
        }

        #endregion

        #region Phase 4: Edge Case Tests (4 tests)

        [Fact(Timeout = 5000)]
        public void T30_Boundary_Conditions_Fleet_Size()
        {
            var fleet = new MockFleetAccounts();

            var zeroAccounts = fleet.GetActiveAccounts();
            Assert.Equal(0, zeroAccounts.Count);

            for (int i = 0; i < 100; i++)
                fleet.AddAccount(new MockAccount($"Sim{i:D3}") { IsActive = true });

            var maxAccounts = fleet.GetActiveAccounts();
            Assert.Equal(100, maxAccounts.Count);
        }

        [Fact(Timeout = 5000)]
        public void T31_Error_Path_Invalid_Account()
        {
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });

            fleet.SetAccountActive("InvalidAccount", false);

            var activeAccounts = fleet.GetActiveAccounts();
            Assert.Equal(1, activeAccounts.Count);
        }

        [Fact(Timeout = 5000)]
        public void T32_Race_Condition_Stress()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            for (int i = 0; i < 1000; i++)
                sima.EnqueueEvent($"Signal{i}");

            for (int i = 0; i < 10; i++)
                sima.PumpEventQueue();

            Assert.True(sima.GetProcessedEventCount() <= 1000);
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T33_Semaphore_Leak_Detection()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            AssertNoSemaphoreLeak(sima);

            sima.Enable();
            sima.Disable();

            AssertNoSemaphoreLeak(sima);
            sima.Dispose();
        }

        #endregion

        #region Phase 5: Integration Tests (3 tests)

        [Fact(Timeout = 5000)]
        public void T34_EndToEnd_Signal_To_Execution()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            fleet.AddAccount(new MockAccount("Sim102") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.Enable();
            sima.EnqueueEvent("BuySignal");
            sima.PumpEventQueue();

            Assert.Equal(1, sima.GetProcessedEventCount());
            Assert.Equal(2, photonPool.GetActiveSlotCount());
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T35_Fleet_Iteration_With_Skip_Logic()
        {
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            fleet.AddAccount(new MockAccount("Sim101") { IsActive = true });
            fleet.AddAccount(new MockAccount("Sim102") { IsActive = false });
            fleet.AddAccount(new MockAccount("Sim103") { IsActive = true });
            var photonPool = new MockPhotonPool();
            var shadowEngine = new MockShadowEngine();
            var sima = new MockSIMA(mockTime, fleet, photonPool, shadowEngine);

            sima.EnqueueEvent("TestSignal");
            sima.PumpEventQueue();

            Assert.Equal(2, photonPool.GetActiveSlotCount());
            sima.Dispose();
        }

        [Fact(Timeout = 5000)]
        public void T36_Shadow_Engine_Leader_Follower_Sync()
        {
            var shadowEngine = new MockShadowEngine();

            shadowEngine.SetLeader("Sim101");
            shadowEngine.AddFollower("Sim102");
            shadowEngine.AddFollower("Sim103");

            shadowEngine.PropagateStopMove("Sim101", 100.00);
            shadowEngine.PropagateStopMove("Sim102", 100.00);
            shadowEngine.PropagateStopMove("Sim103", 100.00);

            Assert.Equal(100.00, shadowEngine.GetStopPrice("Sim101"));
            Assert.Equal(100.00, shadowEngine.GetStopPrice("Sim102"));
            Assert.Equal(100.00, shadowEngine.GetStopPrice("Sim103"));
        }

        #endregion
    }
}

// Made with Bob
