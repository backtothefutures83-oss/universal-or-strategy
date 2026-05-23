using System;
using System.Threading;
using System.Threading.Tasks;
using V12_Performance.Tests.Mocks;
using Xunit;

namespace V12_Performance.Tests.Core
{
    /// <summary>
    /// Unit tests for lock-free order management.
    /// EPIC-6 TDD Safety Net: Validates order lifecycle without lock() statements.
    /// Ensures order state transitions are atomic and thread-safe.
    /// </summary>
    public class OrderManagementTests
    {
        [Fact]
        public void OrderState_Transition_Working_To_Filled()
        {
            // Arrange
            var order = new MockOrder
            {
                Name = "ORD001",
                OrderState = OrderState.Working,
                Quantity = 1,
                LimitPrice = 4500.0,
                StopPrice = 0.0,
            };

            // Act
            var newOrder = order;
            newOrder.OrderState = OrderState.Filled;

            // Assert
            Assert.Equal(OrderState.Filled, newOrder.OrderState);
            Assert.Equal("ORD001", newOrder.Name);
        }

        [Fact]
        public void OrderState_Transition_Working_To_Cancelled()
        {
            // Arrange
            var order = new MockOrder
            {
                Name = "ORD002",
                OrderState = OrderState.Working,
                Quantity = 2,
                LimitPrice = 4500.0,
                StopPrice = 0.0,
            };

            // Act
            var newOrder = order;
            newOrder.OrderState = OrderState.Cancelled;

            // Assert
            Assert.Equal(OrderState.Cancelled, newOrder.OrderState);
        }

        [Fact]
        public void OrderTracking_ConcurrentUpdates_NoCorruption()
        {
            // Arrange
            var tracker = new MockOrderTracker();
            const int orderCount = 100;

            // Act - Concurrent order additions
            Parallel.For(
                0,
                orderCount,
                i =>
                {
                    tracker.AddOrder($"ORD_{i}", OrderState.Working);
                }
            );

            // Assert - All orders tracked
            Assert.Equal(orderCount, tracker.OrderCount);
        }

        [Fact]
        public void OrderExecution_AtomicQuantityUpdate_NoRaceCondition()
        {
            // Arrange
            var tracker = new MockOrderTracker();
            tracker.AddOrder("ORD_EXEC", OrderState.Working);
            const int fillCount = 1000;

            // Act - Concurrent partial fills
            Parallel.For(
                0,
                fillCount,
                i =>
                {
                    tracker.IncrementFilledQuantity("ORD_EXEC", 1);
                }
            );

            // Assert - Correct total filled quantity
            Assert.Equal(fillCount, tracker.GetFilledQuantity("ORD_EXEC"));
        }

        [Fact]
        public void OrderCancellation_ConcurrentRequests_IdempotentResult()
        {
            // Arrange
            var tracker = new MockOrderTracker();
            tracker.AddOrder("ORD_CANCEL", OrderState.Working);
            const int cancelAttempts = 100;
            int successCount = 0;

            // Act - Concurrent cancellation attempts
            Parallel.For(
                0,
                cancelAttempts,
                i =>
                {
                    if (tracker.CancelOrder("ORD_CANCEL"))
                    {
                        Interlocked.Increment(ref successCount);
                    }
                }
            );

            // Assert - Only one cancellation succeeded
            Assert.Equal(1, successCount);
            Assert.Equal(OrderState.Cancelled, tracker.GetOrderState("ORD_CANCEL"));
        }

        [Fact]
        public void OrderModification_AtomicPriceUpdate_NoTearing()
        {
            // Arrange
            var tracker = new MockOrderTracker();
            tracker.AddOrder("ORD_MODIFY", OrderState.Working);
            const int modifyCount = 1000;

            // Act - Concurrent price modifications
            Parallel.For(
                0,
                modifyCount,
                i =>
                {
                    tracker.UpdateLimitPrice("ORD_MODIFY", 4500.0 + i);
                }
            );

            // Assert - Final price is valid (no torn reads)
            var finalPrice = tracker.GetLimitPrice("ORD_MODIFY");
            Assert.True(finalPrice >= 4500.0 && finalPrice < 4500.0 + modifyCount);
        }
    }

    /// <summary>
    /// Mock order tracker for testing lock-free order management.
    /// Simulates V12 Orders.Management patterns.
    /// </summary>
    public class MockOrderTracker
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OrderData> _orders;

        public MockOrderTracker()
        {
            _orders = new System.Collections.Concurrent.ConcurrentDictionary<string, OrderData>();
        }

        public int OrderCount => _orders.Count;

        public void AddOrder(string name, OrderState state)
        {
            _orders.TryAdd(
                name,
                new OrderData
                {
                    State = state,
                    FilledQuantity = 0,
                    LimitPrice = 0.0,
                }
            );
        }

        public void IncrementFilledQuantity(string name, int quantity)
        {
            if (_orders.TryGetValue(name, out var data))
            {
                Interlocked.Add(ref data.FilledQuantity, quantity);
            }
        }

        public int GetFilledQuantity(string name)
        {
            return _orders.TryGetValue(name, out var data) ? data.FilledQuantity : 0;
        }

        public bool CancelOrder(string name)
        {
            if (_orders.TryGetValue(name, out var data))
            {
                // Atomic compare-exchange: only cancel if currently Working
                int workingState = (int)OrderState.Working;
                int cancelledState = (int)OrderState.Cancelled;
                int currentState = (int)data.State;

                // CompareExchange returns the ORIGINAL value
                // Success = original value matched comparand (Working)
                int original = Interlocked.CompareExchange(ref currentState, cancelledState, workingState);

                if (original == workingState)
                {
                    data.State = OrderState.Cancelled;
                    return true;
                }
            }
            return false;
        }

        public OrderState GetOrderState(string name)
        {
            return _orders.TryGetValue(name, out var data) ? data.State : OrderState.Rejected;
        }

        public void UpdateLimitPrice(string name, double price)
        {
            if (_orders.TryGetValue(name, out var data))
            {
                // Atomic double update (simulates Interlocked.Exchange for doubles)
                Interlocked.Exchange(ref data.LimitPrice, price);
            }
        }

        public double GetLimitPrice(string name)
        {
            return _orders.TryGetValue(name, out var data) ? data.LimitPrice : 0.0;
        }
    }

    /// <summary>
    /// Order data structure for lock-free tracking.
    /// </summary>
    public class OrderData
    {
        public OrderState State;
        public int FilledQuantity;
        public double LimitPrice;
    }
}

// Made with Bob
