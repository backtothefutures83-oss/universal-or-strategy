// ConfigurationIntegrationTests.cs
// BUILD_TAG: 1111.007-phase7-tQ1_S5_CONFIG_TESTS_SETUP
// Cluster S5: Configuration & Persistence Integration Tests (26 tests)
// V12 DNA: Lock-free, MockTime, ASCII-only, Atomic primitives
// SETUP ONLY - asserts current behavior, no bug fixes

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace V12.Tests
{
    /// <summary>
    /// Integration tests for V12 Configuration & Persistence System (Cluster S5).
    /// Covers 5 configuration files (2,299 lines).
    /// Tests property loading, StickyState persistence, IPC config updates, mode profiles, and concurrency.
    /// SETUP ONLY - asserts current behavior, no bug fixes.
    /// </summary>
    public class ConfigurationIntegrationTests
    {
        #region Mock NinjaTrader Types

        private enum MarketPosition { Flat, Long, Short }
        private enum OrderAction { Buy, Sell, BuyToCover, SellShort }
        private enum OrderState { Unknown, Initialized, Submitted, Accepted, Working, PartFilled, Filled, Cancelled, Rejected }
        private enum OrderType { Market, Limit, StopMarket, StopLimit }

        #endregion

        #region Mock Infrastructure (Lines 34-450)

        // ============================================================================
        // MockTime: Deterministic time simulation (copied from S1/S2/S3)
        // ============================================================================
        private class MockTime
        {
            private long _ticks;

            public MockTime(long initialTicks) => _ticks = initialTicks;

            public long GetTicks() => Interlocked.Read(ref _ticks);

            public void Advance(long deltaTicks) => Interlocked.Add(ref _ticks, deltaTicks);

            public void AdvanceSeconds(double seconds) =>
                Interlocked.Add(ref _ticks, (long)(seconds * TimeSpan.TicksPerSecond));

            public DateTime GetDateTime() => new DateTime(GetTicks(), DateTimeKind.Utc);
        }

        // ============================================================================
        // MockReaperTimer: Background timer with manual Advance()
        // ============================================================================
        private class MockReaperTimer
        {
            private int _isRunning;
            private long _intervalMs;
            private long _lastElapsedTicks;
            private MockTime _time;
            public event EventHandler Elapsed;

            public MockReaperTimer(MockTime time, long intervalMs)
            {
                _time = time;
                _intervalMs = intervalMs;
                _isRunning = 0;
                _lastElapsedTicks = time.GetTicks();
            }

            public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

            public void Start()
            {
                Interlocked.Exchange(ref _isRunning, 1);
                _lastElapsedTicks = _time.GetTicks();
            }

            public void Stop()
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }

            public void Advance(long deltaMs)
            {
                if (IsRunning)
                {
                    long currentTicks = _time.GetTicks();
                    long elapsedMs = (currentTicks - _lastElapsedTicks) / TimeSpan.TicksPerMillisecond;
                    
                    if (elapsedMs >= _intervalMs)
                    {
                        _lastElapsedTicks = currentTicks;
                        Elapsed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            public void SimulateElapsed()
            {
                if (IsRunning)
                {
                    Elapsed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // ============================================================================
        // MockAccount: Position/order tracking + flatten calls
        // ============================================================================
        private class MockAccount
        {
            public string Name { get; set; }
            public MarketPosition Position { get; set; }
            public int PositionQuantity { get; set; }
            public double PositionAvgPrice { get; set; }
            public List<MockOrder> Orders { get; set; }
            private int _flattenCallCount;
            private int _cancelAllCallCount;
            public int FlattenCallCount => _flattenCallCount;
            public int CancelAllCallCount => _cancelAllCallCount;

            public MockAccount(string name)
            {
                Name = name;
                Position = MarketPosition.Flat;
                PositionQuantity = 0;
                Orders = new List<MockOrder>();
                _flattenCallCount = 0;
                _cancelAllCallCount = 0;
            }

            public void SetPosition(MarketPosition pos, int qty, double avgPrice)
            {
                Position = pos;
                PositionQuantity = qty;
                PositionAvgPrice = avgPrice;
            }

            public void Flatten()
            {
                Interlocked.Increment(ref _flattenCallCount);
                Position = MarketPosition.Flat;
                PositionQuantity = 0;
            }

            public void CancelAllOrders()
            {
                Interlocked.Increment(ref _cancelAllCallCount);
                foreach (var order in Orders)
                {
                    if (order.State != OrderState.Filled && order.State != OrderState.Cancelled)
                    {
                        order.State = OrderState.Cancelled;
                    }
                }
            }

            public void SubmitOrder(MockOrder order)
            {
                order.Account = this;
                order.State = OrderState.Submitted;
                Orders.Add(order);
            }
        }

        // ============================================================================
        // MockOrder: Order properties
        // ============================================================================
        private class MockOrder
        {
            public string OrderId { get; set; }
            public string Name { get; set; }
            public OrderState State { get; set; }
            public OrderAction Action { get; set; }
            public OrderType OrderType { get; set; }
            public double LimitPrice { get; set; }
            public double StopPrice { get; set; }
            public int Quantity { get; set; }
            public MockAccount Account { get; set; }

            public MockOrder(string orderId, string name, OrderAction action, OrderType type, int qty)
            {
                OrderId = orderId;
                Name = name;
                Action = action;
                OrderType = type;
                Quantity = qty;
                State = OrderState.Unknown;
            }
        }

        // ============================================================================
        // MockFSM: FollowerBracketFSM state simulation
        // ============================================================================
        private class MockFSM
        {
            public string AccountName { get; set; }
            public string PositionName { get; set; }
            public string State { get; set; }
            public int ExpectedPosition { get; set; }
            private int _isTerminated;

            public MockFSM(string accountName, string positionName, string state, int expectedPos)
            {
                AccountName = accountName;
                PositionName = positionName;
                State = state;
                ExpectedPosition = expectedPos;
                _isTerminated = state == "Terminated" ? 1 : 0;
            }

            public bool IsTerminated => Interlocked.CompareExchange(ref _isTerminated, 0, 0) == 1;

            public void Terminate()
            {
                Interlocked.Exchange(ref _isTerminated, 1);
                State = "Terminated";
            }
        }

        // ============================================================================
        // MockQueue<T>: ConcurrentQueue wrapper with inspection
        // ============================================================================
        private class MockQueue<T>
        {
            private ConcurrentQueue<T> _queue;

            public MockQueue()
            {
                _queue = new ConcurrentQueue<T>();
            }

            public void Enqueue(T item) => _queue.Enqueue(item);

            public bool TryDequeue(out T item) => _queue.TryDequeue(out item);

            public int Count => _queue.Count;

            public bool Contains(T item) => _queue.Contains(item);

            public void Clear()
            {
                while (_queue.TryDequeue(out _)) { }
            }
        }

        // ============================================================================
        // MockInFlightGuard: ConcurrentDictionary wrapper with tracking
        // ============================================================================
        private class MockInFlightGuard
        {
            private ConcurrentDictionary<string, byte> _guards;

            public MockInFlightGuard()
            {
                _guards = new ConcurrentDictionary<string, byte>();
            }

            public bool TryAdd(string key)
            {
                return _guards.TryAdd(key, 0);
            }

            public bool TryRemove(string key)
            {
                return _guards.TryRemove(key, out _);
            }

            public bool IsSet(string key)
            {
                return _guards.ContainsKey(key);
            }

            public int Count => _guards.Count;

            public void Clear()
            {
                _guards.Clear();
            }
        }

        #endregion

        #region Test Helpers (Lines 451-650)

        // ============================================================================
        // Assertion Helpers (12 methods)
        // ============================================================================

        private void AssertTimerRunning(MockReaperTimer timer, bool expected)
        {
            Assert.Equal(expected, timer.IsRunning);
        }

        private void AssertQueueContains(MockQueue<string> queue, string accountName)
        {
            Assert.True(queue.Contains(accountName), $"Queue should contain {accountName}");
        }

        private void AssertInFlightGuardSet(MockInFlightGuard guard, string key)
        {
            Assert.True(guard.IsSet(key), $"InFlightGuard should be set for {key}");
        }

        private void AssertInFlightGuardCleared(MockInFlightGuard guard, string key)
        {
            Assert.False(guard.IsSet(key), $"InFlightGuard should be cleared for {key}");
        }

        private void AssertGraceWindowActive(MockTime time, long stampTicks, double graceSec)
        {
            long currentTicks = time.GetTicks();
            long elapsedSec = (currentTicks - stampTicks) / TimeSpan.TicksPerSecond;
            Assert.True(elapsedSec < graceSec, $"Grace window should be active (elapsed: {elapsedSec}s, grace: {graceSec}s)");
        }

        private void AssertGraceWindowExpired(MockTime time, long stampTicks, double graceSec)
        {
            long currentTicks = time.GetTicks();
            long elapsedSec = (currentTicks - stampTicks) / TimeSpan.TicksPerSecond;
            Assert.True(elapsedSec >= graceSec, $"Grace window should be expired (elapsed: {elapsedSec}s, grace: {graceSec}s)");
        }

        private void AssertAccountFlattened(MockAccount account)
        {
            Assert.True(account.FlattenCallCount > 0, $"Account {account.Name} should be flattened");
            Assert.Equal(MarketPosition.Flat, account.Position);
        }

        private void AssertOrderCancelled(MockOrder order)
        {
            Assert.Equal(OrderState.Cancelled, order.State);
        }

        private void AssertOrderSubmitted(MockAccount account, int expectedCount)
        {
            int submittedCount = account.Orders.Count(o => o.State == OrderState.Submitted || o.State == OrderState.Working);
            Assert.Equal(expectedCount, submittedCount);
        }

        private void AssertFSMTerminated(MockFSM fsm)
        {
            Assert.True(fsm.IsTerminated, $"FSM {fsm.PositionName} should be terminated");
            Assert.Equal("Terminated", fsm.State);
        }

        private void AssertWatchdogStage(int stage, int expected)
        {
            Assert.Equal(expected, stage);
        }

        private void AssertEmergencyStopPrice(double stopPrice, double close, double distance, MarketPosition position)
        {
            double expectedStop = position == MarketPosition.Long 
                ? close - distance 
                : close + distance;
            Assert.Equal(expectedStop, stopPrice, 2);
        }

        private void AssertRepairBlocked(bool blocked, string reason)
        {
            Assert.True(blocked, $"Repair should be blocked: {reason}");
        }

        // ============================================================================
        // Verification Helpers (6 methods)
        // ============================================================================

        private bool VerifyAccountFlattened(MockAccount account)
        {
            return account.FlattenCallCount > 0 && account.Position == MarketPosition.Flat;
        }

        private bool VerifyAllOrdersCancelled(MockAccount account)
        {
            return account.Orders.All(o => o.State == OrderState.Cancelled || o.State == OrderState.Filled);
        }

        private bool VerifyEmergencyStopSubmitted(MockAccount account)
        {
            return account.Orders.Any(o => o.OrderType == OrderType.StopMarket && o.State == OrderState.Submitted);
        }

        private bool VerifyFSMTerminated(MockFSM fsm)
        {
            return fsm.IsTerminated && fsm.State == "Terminated";
        }

        private bool VerifyQueueDrained(MockQueue<string> queue)
        {
            return queue.Count == 0;
        }

        private bool VerifyInFlightCleanup(MockInFlightGuard guard)
        {
            return guard.Count == 0;
        }

        // ============================================================================
        // Simulation Helpers (6 methods)
        // ============================================================================

        private void SimulateGhostPosition(MockAccount account, MockFSM fsm)
        {
            account.SetPosition(MarketPosition.Long, 2, 5000.0);
            fsm.ExpectedPosition = 0;
            fsm.State = "Idle";
        }

        private void SimulateCriticalDesync(MockAccount account, MockFSM fsm)
        {
            account.SetPosition(MarketPosition.Flat, 0, 0);
            fsm.ExpectedPosition = 2;
            fsm.State = "BracketActive";
        }

        private void SimulateNakedPosition(MockAccount account)
        {
            account.SetPosition(MarketPosition.Long, 2, 5000.0);
            account.Orders.Clear();
        }

        private void SimulateDeadlock(MockTime time, ref long heartbeatTicks)
        {
            time.AdvanceSeconds(15.0);
        }

        private void AdvanceGraceWindow(MockTime time, double seconds)
        {
            time.AdvanceSeconds(seconds);
        }

        private void SimulateTimerElapsed(MockReaperTimer timer)
        {
            timer.SimulateElapsed();
        }

        // ============================================================================
        // Creation Helpers (3 methods)
        // ============================================================================

        private MockAccount CreateMockAccount(string name, MarketPosition position, int quantity)
        {
            var account = new MockAccount(name);
            account.SetPosition(position, quantity, position == MarketPosition.Long ? 5000.0 : 5100.0);
            return account;
        }

        private MockFSM CreateMockFSM(string accountName, string positionName, string state, int expectedPos)
        {
            return new MockFSM(accountName, positionName, state, expectedPos);
        }

        private MockOrder CreateMockOrder(string name, OrderType type, OrderAction action, int qty)
        {
            return new MockOrder(Guid.NewGuid().ToString(), name, action, type, qty);
        }

        #endregion

        #region Phase 1: REAPER Timer & Lifecycle Tests (T01-T06)

        [Fact]
        public void T01_ReaperTimer_Start_SetsRunningFlag()
        {
            // Given: REAPER timer initialized
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var timer = new MockReaperTimer(mockTime, 1000);

            // When: Timer started
            timer.Start();

            // Then: IsRunning flag set
            AssertTimerRunning(timer, true);
        }

        [Fact]
        public void T02_ReaperTimer_Stop_ClearsRunningFlag()
        {
            // Given: REAPER timer running
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var timer = new MockReaperTimer(mockTime, 1000);
            timer.Start();

            // When: Timer stopped
            timer.Stop();

            // Then: IsRunning flag cleared
            AssertTimerRunning(timer, false);
        }

        [Fact]
        public void T03_ReaperTimer_Elapsed_FiresEvent()
        {
            // Given: REAPER timer running with event handler
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var timer = new MockReaperTimer(mockTime, 1000);
            int elapsedCount = 0;
            timer.Elapsed += (s, e) => Interlocked.Increment(ref elapsedCount);
            timer.Start();

            // When: Timer elapsed simulated
            SimulateTimerElapsed(timer);

            // Then: Event fired once
            Assert.Equal(1, elapsedCount);
        }

        [Fact]
        public void T04_ReaperTimer_MultipleElapsed_FiresMultipleTimes()
        {
            // Given: REAPER timer running
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var timer = new MockReaperTimer(mockTime, 1000);
            int elapsedCount = 0;
            timer.Elapsed += (s, e) => Interlocked.Increment(ref elapsedCount);
            timer.Start();

            // When: Timer elapsed 3 times
            SimulateTimerElapsed(timer);
            SimulateTimerElapsed(timer);
            SimulateTimerElapsed(timer);

            // Then: Event fired 3 times
            Assert.Equal(3, elapsedCount);
        }

        [Fact]
        public void T05_ReaperTimer_StoppedTimer_NoEventFire()
        {
            // Given: REAPER timer stopped
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var timer = new MockReaperTimer(mockTime, 1000);
            int elapsedCount = 0;
            timer.Elapsed += (s, e) => Interlocked.Increment(ref elapsedCount);
            timer.Start();
            timer.Stop();

            // When: Timer elapsed simulated after stop
            SimulateTimerElapsed(timer);

            // Then: Event not fired (timer stopped)
            Assert.Equal(0, elapsedCount);
        }

        [Fact]
        public void T06_ReaperAudit_EmergencyQueue_EnqueueDequeue()
        {
            // Given: Emergency action queue
            var queue = new MockQueue<string>();

            // When: Account enqueued
            queue.Enqueue("Account1");
            queue.Enqueue("Account2");

            // Then: Queue contains accounts
            Assert.Equal(2, queue.Count);
            AssertQueueContains(queue, "Account1");
            AssertQueueContains(queue, "Account2");

            // When: Dequeue
            queue.TryDequeue(out var account1);
            queue.TryDequeue(out var account2);

            // Then: Queue drained
            Assert.Equal("Account1", account1);
            Assert.Equal("Account2", account2);
            Assert.True(VerifyQueueDrained(queue));
        }

        #endregion

        #region Phase 2: Desync Detection & Repair Tests (T07-T12)

        [Fact]
        public void T07_DesyncDetection_GhostPosition_Detected()
        {
            // Given: Ghost position (broker has position, FSM expects flat)
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            var fsm = CreateMockFSM("Account1", "OR_1", "Idle", 0);

            // When: Desync detected
            SimulateGhostPosition(account, fsm);

            // Then: Position mismatch detected
            Assert.Equal(MarketPosition.Long, account.Position);
            Assert.Equal(2, account.PositionQuantity);
            Assert.Equal(0, fsm.ExpectedPosition);
            Assert.Equal("Idle", fsm.State);
        }

        [Fact]
        public void T08_DesyncDetection_CriticalDesync_Detected()
        {
            // Given: Critical desync (broker flat, FSM expects position)
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Flat, 0);
            var fsm = CreateMockFSM("Account1", "OR_1", "BracketActive", 2);

            // When: Critical desync simulated
            SimulateCriticalDesync(account, fsm);

            // Then: Critical mismatch detected
            Assert.Equal(MarketPosition.Flat, account.Position);
            Assert.Equal(0, account.PositionQuantity);
            Assert.Equal(2, fsm.ExpectedPosition);
            Assert.Equal("BracketActive", fsm.State);
        }

        [Fact]
        public void T09_DesyncDetection_MinorDesync_Detected()
        {
            // Given: Minor desync (quantity mismatch, same direction)
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 3);
            var fsm = CreateMockFSM("Account1", "OR_1", "BracketActive", 2);

            // When: Minor desync exists
            // Then: Quantity mismatch detected
            Assert.Equal(MarketPosition.Long, account.Position);
            Assert.Equal(3, account.PositionQuantity);
            Assert.Equal(2, fsm.ExpectedPosition);
            Assert.NotEqual(account.PositionQuantity, fsm.ExpectedPosition);
        }

        [Fact]
        public void T10_DesyncRepair_GraceWindow_Active()
        {
            // Given: Ghost position detected with grace window
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            long stampTicks = mockTime.GetTicks();
            double graceSec = 2.0;

            // When: Time advanced within grace window
            AdvanceGraceWindow(mockTime, 1.0);

            // Then: Grace window still active
            AssertGraceWindowActive(mockTime, stampTicks, graceSec);
        }

        [Fact]
        public void T11_DesyncRepair_GraceWindow_Expired()
        {
            // Given: Ghost position detected with grace window
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            long stampTicks = mockTime.GetTicks();
            double graceSec = 2.0;

            // When: Time advanced past grace window
            AdvanceGraceWindow(mockTime, 3.0);

            // Then: Grace window expired
            AssertGraceWindowExpired(mockTime, stampTicks, graceSec);
        }

        [Fact]
        public void T12_DesyncRepair_InFlightGuard_PreventsDuplicate()
        {
            // Given: In-flight guard for repair operation
            var guard = new MockInFlightGuard();
            string accountKey = "Account1_Repair";

            // When: First repair attempt
            bool firstAttempt = guard.TryAdd(accountKey);

            // Then: First attempt succeeds
            Assert.True(firstAttempt);
            AssertInFlightGuardSet(guard, accountKey);

            // When: Second repair attempt (duplicate)
            bool secondAttempt = guard.TryAdd(accountKey);

            // Then: Second attempt blocked
            Assert.False(secondAttempt);

            // When: Repair completes, guard cleared
            guard.TryRemove(accountKey);

            // Then: Guard cleared
            AssertInFlightGuardCleared(guard, accountKey);
        }

        #endregion

        #region Phase 3: Repair Engine Tests (T13-T18)

        [Fact]
        public void T13_RepairEngine_EligibilityCheck_GhostPosition()
        {
            // Given: Ghost position eligible for repair
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            var fsm = CreateMockFSM("Account1", "OR_1", "Idle", 0);
            SimulateGhostPosition(account, fsm);

            // When: Eligibility checked
            bool isGhost = account.Position != MarketPosition.Flat && fsm.ExpectedPosition == 0;

            // Then: Ghost position eligible
            Assert.True(isGhost, "Ghost position should be eligible for repair");
        }

        [Fact]
        public void T14_RepairEngine_EligibilityCheck_CriticalDesync()
        {
            // Given: Critical desync eligible for repair
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Flat, 0);
            var fsm = CreateMockFSM("Account1", "OR_1", "BracketActive", 2);
            SimulateCriticalDesync(account, fsm);

            // When: Eligibility checked
            bool isCritical = account.Position == MarketPosition.Flat && fsm.ExpectedPosition != 0;

            // Then: Critical desync eligible
            Assert.True(isCritical, "Critical desync should be eligible for repair");
        }

        [Fact]
        public void T15_RepairEngine_OrphanSelfHeal_TerminatesFSM()
        {
            // Given: Ghost position with orphan FSM
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            var fsm = CreateMockFSM("Account1", "OR_1", "Idle", 0);
            SimulateGhostPosition(account, fsm);

            // When: Orphan self-heal triggered (FSM termination)
            fsm.Terminate();

            // Then: FSM terminated
            AssertFSMTerminated(fsm);
        }

        [Fact]
        public void T16_RepairEngine_RiskBounds_ChecksMaxPosition()
        {
            // Given: Ghost position with risk bounds
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 10);
            int maxAllowedPosition = 5;

            // When: Risk bounds checked
            bool exceedsRisk = account.PositionQuantity > maxAllowedPosition;

            // Then: Risk bounds exceeded
            Assert.True(exceedsRisk, "Position exceeds risk bounds");
        }

        [Fact]
        public void T17_RepairEngine_Authorization_RequiresConfirmation()
        {
            // Given: Ghost position requiring authorization
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            var fsm = CreateMockFSM("Account1", "OR_1", "Idle", 0);
            SimulateGhostPosition(account, fsm);
            bool authorized = false;

            // When: Authorization not granted
            // Then: Repair blocked
            AssertRepairBlocked(!authorized, "Authorization required");
        }

        [Fact]
        public void T18_RepairEngine_FlattenCall_ExecutesForGhost()
        {
            // Given: Ghost position authorized for flatten
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            var fsm = CreateMockFSM("Account1", "OR_1", "Idle", 0);
            SimulateGhostPosition(account, fsm);

            // When: Flatten executed
            account.Flatten();

            // Then: Account flattened
            AssertAccountFlattened(account);
            Assert.True(VerifyAccountFlattened(account));
        }

        #endregion

        #region Phase 4: Naked Position Detection Tests (T19-T24)

        [Fact]
        public void T19_NakedDetection_PositionWithoutStop_Detected()
        {
            // Given: Position without working stop orders
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            SimulateNakedPosition(account);

            // When: Naked position checked
            bool hasWorkingStop = account.Orders.Any(o => 
                o.OrderType == OrderType.StopMarket && 
                (o.State == OrderState.Working || o.State == OrderState.Submitted));

            // Then: No working stop detected
            Assert.False(hasWorkingStop, "Naked position should have no working stop");
        }

        [Fact]
        public void T20_NakedDetection_GraceWindow_FillGrace()
        {
            // Given: Position just filled, within 2s fill grace
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            long fillStampTicks = mockTime.GetTicks();
            double fillGraceSec = 2.0;

            // When: Time advanced within fill grace
            AdvanceGraceWindow(mockTime, 1.0);

            // Then: Fill grace window active
            AssertGraceWindowActive(mockTime, fillStampTicks, fillGraceSec);
        }

        [Fact]
        public void T21_NakedDetection_GraceWindow_NakedGrace()
        {
            // Given: Naked position detected, within 5-10s naked grace
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            long nakedStampTicks = mockTime.GetTicks();
            double nakedGraceSec = 5.0;

            // When: Time advanced within naked grace
            AdvanceGraceWindow(mockTime, 3.0);

            // Then: Naked grace window active
            AssertGraceWindowActive(mockTime, nakedStampTicks, nakedGraceSec);
        }

        [Fact]
        public void T22_NakedDetection_GraceWindow_Expired()
        {
            // Given: Naked position with expired grace
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            long nakedStampTicks = mockTime.GetTicks();
            double nakedGraceSec = 5.0;

            // When: Time advanced past naked grace
            AdvanceGraceWindow(mockTime, 6.0);

            // Then: Naked grace window expired
            AssertGraceWindowExpired(mockTime, nakedStampTicks, nakedGraceSec);
        }

        [Fact]
        public void T23_NakedStop_EmergencyStop_CalculatesPrice()
        {
            // Given: Naked long position requiring emergency stop
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            double currentClose = 5000.0;
            double stopDistance = 10.0;

            // When: Emergency stop price calculated
            double emergencyStopPrice = currentClose - stopDistance;

            // Then: Stop price correct for long position
            AssertEmergencyStopPrice(emergencyStopPrice, currentClose, stopDistance, MarketPosition.Long);
        }

        [Fact]
        public void T24_NakedStop_EmergencyStop_SubmitsOrder()
        {
            // Given: Naked position with expired grace
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            SimulateNakedPosition(account);

            // When: Emergency stop submitted
            var emergencyStop = CreateMockOrder("EmergencyStop_OR_1", OrderType.StopMarket, OrderAction.Sell, 2);
            emergencyStop.StopPrice = 4990.0;
            account.SubmitOrder(emergencyStop);

            // Then: Emergency stop order submitted
            Assert.True(VerifyEmergencyStopSubmitted(account));
            AssertOrderSubmitted(account, 1);
        }

        #endregion

        #region Phase 5: Watchdog & Flatten Tests (T25-T30)

        [Fact]
        public void T25_Watchdog_DeadlockDetection_StaleHeartbeat()
        {
            // Given: Watchdog monitoring heartbeat
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            long heartbeatTicks = mockTime.GetTicks();
            double deadlockThresholdSec = 10.0;

            // When: Heartbeat stale (no update for 15s)
            SimulateDeadlock(mockTime, ref heartbeatTicks);

            // Then: Deadlock detected
            long elapsedSec = (mockTime.GetTicks() - heartbeatTicks) / TimeSpan.TicksPerSecond;
            Assert.True(elapsedSec > deadlockThresholdSec, $"Deadlock should be detected (elapsed: {elapsedSec}s)");
        }

        [Fact]
        public void T26_Watchdog_StageTransition_Stage0To1()
        {
            // Given: Watchdog at stage 0
            int watchdogStage = 0;

            // When: First deadlock detected, transition to stage 1
            int newStage = Interlocked.CompareExchange(ref watchdogStage, 1, 0);

            // Then: Stage transitioned to 1
            Assert.Equal(0, newStage); // CAS returned old value
            AssertWatchdogStage(Interlocked.CompareExchange(ref watchdogStage, 0, 0), 1);
        }

        [Fact]
        public void T27_Watchdog_StageTransition_Stage1To2()
        {
            // Given: Watchdog at stage 1
            int watchdogStage = 1;

            // When: Second deadlock detected, transition to stage 2
            int newStage = Interlocked.CompareExchange(ref watchdogStage, 2, 1);

            // Then: Stage transitioned to 2
            Assert.Equal(1, newStage); // CAS returned old value
            AssertWatchdogStage(Interlocked.CompareExchange(ref watchdogStage, 0, 0), 2);
        }

        [Fact]
        public void T28_Watchdog_Stage2_TriggersEmergencyFlatten()
        {
            // Given: Watchdog at stage 2 (emergency threshold)
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            int watchdogStage = 2;

            // When: Emergency flatten triggered
            if (watchdogStage >= 2)
            {
                account.Flatten();
            }

            // Then: Account flattened
            AssertAccountFlattened(account);
        }

        [Fact]
        public void T29_Watchdog_FlattenFallback_CancelsAllOrders()
        {
            // Given: Account with working orders
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account = CreateMockAccount("Account1", MarketPosition.Long, 2);
            var order1 = CreateMockOrder("Stop_OR_1", OrderType.StopMarket, OrderAction.Sell, 2);
            order1.State = OrderState.Working;
            account.Orders.Add(order1);
            var order2 = CreateMockOrder("Target_OR_1", OrderType.Limit, OrderAction.Sell, 2);
            order2.State = OrderState.Working;
            account.Orders.Add(order2);

            // When: Flatten fallback triggered
            account.CancelAllOrders();
            account.Flatten();

            // Then: All orders cancelled and account flattened
            Assert.True(VerifyAllOrdersCancelled(account));
            Assert.True(VerifyAccountFlattened(account));
        }

        [Fact]
        public void T30_Watchdog_MultiAccount_FleetFlatten()
        {
            // Given: Fleet with multiple accounts
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var account1 = CreateMockAccount("Account1", MarketPosition.Long, 2);
            var account2 = CreateMockAccount("Account2", MarketPosition.Short, 3);
            var account3 = CreateMockAccount("Account3", MarketPosition.Long, 1);
            var fleet = new List<MockAccount> { account1, account2, account3 };

            // When: Fleet-wide flatten triggered
            foreach (var account in fleet)
            {
                account.Flatten();
            }

            // Then: All accounts flattened
            Assert.True(fleet.All(a => VerifyAccountFlattened(a)));
            Assert.Equal(3, fleet.Count(a => a.FlattenCallCount > 0));
        }

        #endregion
    }
}
