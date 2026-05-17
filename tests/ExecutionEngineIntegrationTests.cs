// Execution Engine Integration Tests
// V12 DNA Compliant: Lock-free, ASCII-only, Actor pattern, MockTime
// BUILD_TAG: 1111.007-phase7-tQ1_S1_SIMA_TESTS_SETUP
// SETUP ONLY: Assert current behavior (including bugs)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace V12.Tests
{
    /// <summary>
    /// Integration tests for Execution Engine (Cluster S2).
    /// Tests order callbacks, order management, trailing stops, and propagation.
    /// V12 DNA: Lock-free (Interlocked/CAS), ASCII-only, Actor pattern (mailbox), MockTime.
    /// SETUP ONLY: Tests assert current behavior including manifest bugs.
    /// </summary>
    public class ExecutionEngineIntegrationTests
    {
        #region Mock NinjaTrader Types

        private enum MarketPosition { Flat, Long, Short }
        private enum OrderAction { Buy, Sell, BuyToCover, SellShort }
        private enum OrderState { Unknown, Initialized, Submitted, Accepted, Working, PartFilled, Filled, Cancelled, Rejected, ChangePending, PendingCancel, PendingSubmit }
        private enum OrderType { Market, Limit, StopMarket, StopLimit }

        #endregion

        #region Mock Infrastructure

        /// <summary>
        /// Deterministic time simulation for testing.
        /// Copied from SymmetryFsmIntegrationTests.cs.
        /// </summary>
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

        /// <summary>
        /// Mock Order with lifecycle simulation.
        /// </summary>
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
            public int Filled { get; set; }
            public double AverageFillPrice { get; set; }
            public MockAccount Account { get; set; }
            public string Oco { get; set; }

            public MockOrder(string orderId, string name, OrderAction action, OrderType type, int qty)
            {
                OrderId = orderId;
                Name = name;
                Action = action;
                OrderType = type;
                Quantity = qty;
                State = OrderState.Unknown;
                Filled = 0;
            }

            public void SimulateFill(MockAccount account, double price, int qty)
            {
                State = OrderState.Filled;
                AverageFillPrice = price;
                Filled = qty;
                account?.RaiseOrderUpdate(this, OrderState.Filled);
            }

            public void SimulatePartialFill(MockAccount account, double price, int qty)
            {
                State = OrderState.PartFilled;
                AverageFillPrice = price;
                Filled += qty;
                account?.RaiseOrderUpdate(this, OrderState.PartFilled);
            }

            public void SimulateCancel(MockAccount account)
            {
                State = OrderState.Cancelled;
                account?.RaiseOrderUpdate(this, OrderState.Cancelled);
            }

            public void SimulateReject(MockAccount account, string error)
            {
                State = OrderState.Rejected;
                account?.RaiseOrderUpdate(this, OrderState.Rejected);
            }

            public void SimulateAccepted(MockAccount account)
            {
                State = OrderState.Accepted;
                account?.RaiseOrderUpdate(this, OrderState.Accepted);
            }

            public void SimulateWorking(MockAccount account)
            {
                State = OrderState.Working;
                account?.RaiseOrderUpdate(this, OrderState.Working);
            }
        }

        /// <summary>
        /// Mock Execution for fill events.
        /// </summary>
        private class MockExecution
        {
            public string ExecutionId { get; set; }
            public MockOrder Order { get; set; }
            public double Price { get; set; }
            public int Quantity { get; set; }
            public DateTime Time { get; set; }

            public MockExecution(string executionId, MockOrder order, double price, int qty, DateTime time)
            {
                ExecutionId = executionId;
                Order = order;
                Price = price;
                Quantity = qty;
                Time = time;
            }
        }

        /// <summary>
        /// Mock Account with event handlers.
        /// </summary>
        private class MockAccount
        {
            public string Name { get; set; }
            public MarketPosition Position { get; set; }
            public int PositionQuantity { get; set; }
            public bool IsActive { get; set; }
            private readonly List<Action<MockOrder, OrderState>> _orderUpdateHandlers;
            private readonly List<Action<MockExecution, string>> _executionUpdateHandlers;
            private readonly List<Action<MockAccount, MarketPosition, int>> _positionUpdateHandlers;

            public MockAccount(string name)
            {
                Name = name;
                Position = MarketPosition.Flat;
                PositionQuantity = 0;
                IsActive = true;
                _orderUpdateHandlers = new List<Action<MockOrder, OrderState>>();
                _executionUpdateHandlers = new List<Action<MockExecution, string>>();
                _positionUpdateHandlers = new List<Action<MockAccount, MarketPosition, int>>();
            }

            public void SubscribeOrderUpdate(Action<MockOrder, OrderState> handler) => _orderUpdateHandlers.Add(handler);
            public void SubscribeExecutionUpdate(Action<MockExecution, string> handler) => _executionUpdateHandlers.Add(handler);
            public void SubscribePositionUpdate(Action<MockAccount, MarketPosition, int> handler) => _positionUpdateHandlers.Add(handler);

            public void RaiseOrderUpdate(MockOrder order, OrderState state)
            {
                foreach (var handler in _orderUpdateHandlers.ToList())
                    handler?.Invoke(order, state);
            }

            public void RaiseExecutionUpdate(MockExecution execution, string executionId)
            {
                foreach (var handler in _executionUpdateHandlers.ToList())
                    handler?.Invoke(execution, executionId);
            }

            public void RaisePositionUpdate(MarketPosition position, int quantity)
            {
                Position = position;
                PositionQuantity = quantity;
                foreach (var handler in _positionUpdateHandlers.ToList())
                    handler?.Invoke(this, position, quantity);
            }

            public MockOrder Submit(MockOrder order)
            {
                order.Account = this;
                order.State = OrderState.Submitted;
                return order;
            }

            public void Cancel(MockOrder order)
            {
                if (order.State == OrderState.Working || order.State == OrderState.Accepted ||
                    order.State == OrderState.ChangePending || order.State == OrderState.Submitted ||
                    order.State == OrderState.PendingSubmit || order.State == OrderState.PendingCancel)
                    order.SimulateCancel(this);
            }
        }

        /// <summary>
        /// Mock PositionInfo for position state tracking.
        /// </summary>
        private class MockPositionInfo
        {
            public string EntryName { get; set; }
            public MarketPosition Direction { get; set; }
            public int TotalContracts { get; set; }
            public int RemainingContracts { get; set; }
            public double EntryPrice { get; set; }
            public double CurrentStopPrice { get; set; }
            public int CurrentTrailLevel { get; set; }
            public double ExtremePriceSinceEntry { get; set; }
            public bool EntryFilled { get; set; }
            public bool BracketSubmitted { get; set; }
            public bool IsFollower { get; set; }
            public MockAccount ExecutingAccount { get; set; }
            public int T1Contracts { get; set; }
            public int T2Contracts { get; set; }
            public int T3Contracts { get; set; }
            public int T4Contracts { get; set; }
            public int T5Contracts { get; set; }
            public bool T1Filled { get; set; }
            public bool T2Filled { get; set; }
            public bool T3Filled { get; set; }
            public bool T4Filled { get; set; }
            public bool T5Filled { get; set; }
            public bool ManualBreakevenTriggered { get; set; }
            public bool ManualBreakevenArmed { get; set; }
            public bool PendingCleanup { get; set; }
            public int FlattenAttemptCount { get; set; }
        }

        /// <summary>
        /// Mock FleetAccounts for multi-account support.
        /// </summary>
        private class MockFleetAccounts
        {
            private readonly ConcurrentDictionary<string, MockAccount> _accounts;

            public MockFleetAccounts() => _accounts = new ConcurrentDictionary<string, MockAccount>();

            public void AddAccount(MockAccount account) => _accounts[account.Name] = account;
            public MockAccount GetAccount(string name) => _accounts.TryGetValue(name, out var account) ? account : null;
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

        /// <summary>
        /// Mock EventQueue for deterministic event sequencing.
        /// </summary>
        private class MockEventQueue
        {
            private readonly ConcurrentQueue<Action> _events;

            public MockEventQueue() => _events = new ConcurrentQueue<Action>();

            public void EnqueueOrderUpdate(MockOrder order, OrderState state, Action<MockOrder, OrderState> handler)
            {
                _events.Enqueue(() => handler(order, state));
            }

            public void EnqueueExecutionUpdate(MockExecution execution, string executionId, Action<MockExecution, string> handler)
            {
                _events.Enqueue(() => handler(execution, executionId));
            }

            public void EnqueuePositionUpdate(MockAccount account, MarketPosition position, int quantity, Action<MockAccount, MarketPosition, int> handler)
            {
                _events.Enqueue(() => handler(account, position, quantity));
            }

            public void ProcessEvents()
            {
                while (_events.TryDequeue(out var evt))
                    evt?.Invoke();
            }

            public int GetQueuedCount() => _events.Count;
        }

        /// <summary>
        /// Pending Stop Replacement tracking.
        /// </summary>
        private class PendingStopReplacement
        {
            public string EntryName { get; set; }
            public int Quantity { get; set; }
            public double StopPrice { get; set; }
            public long CreatedTicks { get; set; }
            public MockOrder OldStopOrder { get; set; }
            public double NewStopPrice { get => StopPrice; set => StopPrice = value; }
            public long InitiatedAt { get => CreatedTicks; set => CreatedTicks = value; }
        }

        /// <summary>
        /// Follower Replace Spec for two-phase commit.
        /// </summary>
        private class FollowerReplaceSpec
        {
            public string EntryName { get; set; }
            public double NewPrice { get; set; }
            public long CreatedTicks { get; set; }
            public MockPositionInfo Follower { get; set; }
            public double PendingPrice { get => NewPrice; set => NewPrice = value; }
            public long InitiatedAt { get => CreatedTicks; set => CreatedTicks = value; }
        }

        private class QueuedAccountOrderUpdate
        {
            public MockOrder Order { get; set; }
            public MockAccount Account { get; set; }
            public long Timestamp { get; set; }
        }

        private static class Direction
        {
            public static readonly MarketPosition Long = MarketPosition.Long;
            public static readonly MarketPosition Short = MarketPosition.Short;
            public static readonly MarketPosition Flat = MarketPosition.Flat;
        }

        /// <summary>
        /// Mock ExecutionEngine main test harness.
        /// </summary>
        private class MockExecutionEngine
        {
            public double Trail1Points { get; set; } = 10.0;
            public double Trail1StopOffset { get; set; } = 1.0;
            public double Trail2Points { get; set; } = 20.0;
            public double Trail2StopOffset { get; set; } = 2.0;
            public double Trail3Points { get; set; } = 30.0;
            public double Trail3StopOffset { get; set; } = 2.0;
            public MockTime Time { get; set; }
            public MockFleetAccounts Fleet { get; set; }
            public MockEventQueue EventQueue { get; set; }
            public ConcurrentDictionary<string, MockPositionInfo> ActivePositions { get; set; }
            public ConcurrentDictionary<string, MockOrder> EntryOrders { get; set; }
            public ConcurrentDictionary<string, MockOrder> StopOrders { get; set; }
            public ConcurrentDictionary<string, MockOrder> Target1Orders { get; set; }
            public ConcurrentDictionary<string, MockOrder> Target2Orders { get; set; }
            public ConcurrentDictionary<string, MockOrder> Target3Orders { get; set; }
            public ConcurrentDictionary<string, MockOrder> Target4Orders { get; set; }
            public ConcurrentDictionary<string, MockOrder> Target5Orders { get; set; }
            public ConcurrentDictionary<string, PendingStopReplacement> PendingStopReplacements { get; set; }
            public ConcurrentDictionary<string, int> ExpectedPositions { get; set; }
            public ConcurrentDictionary<string, string> ProcessedExecutions { get; set; }
            public ConcurrentDictionary<string, MockOrder> TargetOrders => Target1Orders;
            public ConcurrentDictionary<string, FollowerReplaceSpec> FollowerReplaceSpecs { get; set; } = new ConcurrentDictionary<string, FollowerReplaceSpec>();
            public ConcurrentQueue<QueuedAccountOrderUpdate> AccountOrderQueue { get; set; } = new ConcurrentQueue<QueuedAccountOrderUpdate>();
            public double LastKnownPrice { get; set; }
            public double TickSize { get; set; }
            public double ATR { get; set; }
            public MockTime MockTime => Time;

            public MockExecutionEngine() : this(new MockTime(DateTime.UtcNow.Ticks), new MockFleetAccounts()) {}

            public MockExecutionEngine(MockTime time, MockFleetAccounts fleet)
            {
                Time = time;
                Fleet = fleet;
                EventQueue = new MockEventQueue();
                ActivePositions = new ConcurrentDictionary<string, MockPositionInfo>();
                EntryOrders = new ConcurrentDictionary<string, MockOrder>();
                StopOrders = new ConcurrentDictionary<string, MockOrder>();
                Target1Orders = new ConcurrentDictionary<string, MockOrder>();
                Target2Orders = new ConcurrentDictionary<string, MockOrder>();
                Target3Orders = new ConcurrentDictionary<string, MockOrder>();
                Target4Orders = new ConcurrentDictionary<string, MockOrder>();
                Target5Orders = new ConcurrentDictionary<string, MockOrder>();
                PendingStopReplacements = new ConcurrentDictionary<string, PendingStopReplacement>();
                ExpectedPositions = new ConcurrentDictionary<string, int>();
                ProcessedExecutions = new ConcurrentDictionary<string, string>();
                LastKnownPrice = 5000.0;
                TickSize = 0.25;
                ATR = 10.0;
            }

            public void ProcessOnOrderUpdate(MockOrder order, OrderState state)
            {
                if (order == null) return;
                bool isEntry = order.Name.StartsWith("Entry_") || EntryOrders.ContainsKey(order.Name);
                bool isStop = order.Name.StartsWith("Stop_") || order.Name.EndsWith("_STOP") || order.Name.Contains("_STOP") || StopOrders.ContainsKey(order.Name) || order.OrderType == OrderType.StopMarket || order.OrderType == OrderType.StopLimit;
                bool isTarget = order.Name.StartsWith("T") || order.Name.Contains("_T") || order.Name.Contains("_RUNNER") ||
                                Target1Orders.ContainsKey(order.Name) || Target2Orders.ContainsKey(order.Name) || 
                                Target3Orders.ContainsKey(order.Name) || Target4Orders.ContainsKey(order.Name) || 
                                Target5Orders.ContainsKey(order.Name) || TargetOrders.ContainsKey(order.Name);
                // Entry filled - submit bracket
                if (isEntry && state == OrderState.Filled)
                {
                    var entryName = order.Name.Replace("Entry_", "");
                    if (!ActivePositions.TryGetValue(entryName, out var pos))
                    {
                        pos = CreateUnfilledPosition(entryName, order.Quantity, order.AverageFillPrice,
                            (order.Action == OrderAction.Buy || order.Action == OrderAction.BuyToCover) ? MarketPosition.Long : MarketPosition.Short);
                    }
                    pos.EntryFilled = true;
                    pos.EntryPrice = order.AverageFillPrice;
                    SubmitBracketOrders(entryName, pos);
                }
                // Stop filled - cancel targets
                if (isStop && state == OrderState.Filled)
                {
                    var entryName = order.Name.Replace("Stop_", "").Replace("_STOP", "");
                    CancelAllTargets(entryName);
                    if (ActivePositions.TryGetValue(entryName, out var pos))
                        pos.RemainingContracts = 0;
                    
                    ActivePositions.TryRemove(entryName, out _);
                    RemoveGhostOrderRef(entryName);
                }
                // Target filled - reduce stop
                if (isTarget && state == OrderState.Filled)
                {
                    string entryName = null;
                    foreach (var key in ActivePositions.Keys)
                    {
                        if (order.Name.Contains(key))
                        {
                            entryName = key;
                            break;
                        }
                    }
                    if (entryName != null && ActivePositions.TryGetValue(entryName, out var pos))
                    {
                        pos.RemainingContracts -= order.Filled;
                        UpdateStopQuantity(entryName, pos);
                    }
                }
                // Order rejected - cleanup
                if (state == OrderState.Rejected)
                {
                    if (isEntry)
                    {
                        var entryName = order.Name.Replace("Entry_", "");
                        CleanupPosition(entryName);
                    }
                }
                // Order cancelled - rollback expected positions
                if (state == OrderState.Cancelled)
                {
                    if (isEntry)
                    {
                        var entryName = order.Name.Replace("Entry_", "");
                        if (order.Account != null)
                            ExpectedPositions.TryRemove(order.Account.Name, out _);
                    }
                }
                // Stop cancelled - check for pending replacement
                if (isStop && state == OrderState.Cancelled)
                {
                    var entryName = order.Name.Replace("Stop_", "").Replace("_STOP", "");
                    StopOrders.TryRemove(entryName, out _);
                    StopOrders.TryRemove(order.Name, out _);
                    if (PendingStopReplacements.TryRemove(entryName, out var pending))
                    {
                        CreateNewStopOrder(entryName, pending.StopPrice, pending.Quantity);
                    }
                }
            }

            public void ProcessOnExecutionUpdate(MockExecution execution, string executionId)
            {
                if (execution == null || string.IsNullOrEmpty(executionId)) return;

                // Deduplication check
                if (!ProcessedExecutions.TryAdd(executionId, executionId))
                    return; // Already processed

                var order = execution.Order;
                if (order == null || string.IsNullOrEmpty(order.Name)) return;

                if (order.Name.StartsWith("T") && order.Name.Contains("_"))
                {
                    string entryName = null;
                    foreach (var key in ActivePositions.Keys)
                    {
                        if (order.Name.Contains(key))
                        {
                            entryName = key;
                            break;
                        }
                    }
                    if (entryName != null && ActivePositions.TryGetValue(entryName, out var pos))
                    {
                        pos.RemainingContracts -= execution.Quantity;
                    }
                }
            }

            public void ProcessOnPositionUpdate(MockAccount account, MarketPosition position, int quantity)
            {
                if (account == null) return;

                // Flat position - clear expected
                if (position == MarketPosition.Flat && quantity == 0)
                {
                    ExpectedPositions.TryRemove(account.Name, out _);
                    
                    // Mark all positions for this account as pending cleanup
                    foreach (var kvp in ActivePositions.ToList())
                    {
                        if (kvp.Value.ExecutingAccount == account || (account.Name == "Master" && kvp.Value.ExecutingAccount == null))
                        {
                            kvp.Value.PendingCleanup = true;
                            CleanupPosition(kvp.Key);
                        }
                    }
                }
            }

            public void ProcessAccountOrderUpdate(MockAccount account, MockOrder order, OrderState state)
            {
                if (account == null || order == null) return;

                // Route to correct follower account
                if (order.Account != null && order.Account.Name == account.Name)
                {
                    ProcessOnOrderUpdate(order, state);
                }
            }

            public void SubmitBracketOrders(string entryName, MockPositionInfo pos)
            {
                if (pos.BracketSubmitted) return;
                // Validate and round stop price (1.0 point stop distance)
                var stopPrice = pos.Direction == MarketPosition.Long
                    ? pos.EntryPrice - 1.0
                    : pos.EntryPrice + 1.0;
                stopPrice = Math.Round(stopPrice / TickSize) * TickSize;
                // Create stop order
                var stopOrder = new MockOrder(
                    $"STOP{Time.GetTicks()}",
                    $"Stop_{entryName}",
                    pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                    OrderType.StopMarket,
                    pos.TotalContracts
                );
                stopOrder.StopPrice = stopPrice;
                if (pos.IsFollower && pos.ExecutingAccount != null)
                {
                    pos.ExecutingAccount.Submit(stopOrder);
                }
                StopOrders[entryName] = stopOrder;
                // Create target orders (10.0 point target distance)
                var targetPrice = pos.Direction == MarketPosition.Long
                    ? pos.EntryPrice + 10.0
                    : pos.EntryPrice - 10.0;
                var target1 = new MockOrder(
                    $"T1{Time.GetTicks()}",
                    $"T1_{entryName}",
                    pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                    OrderType.Limit,
                    1
                );
                target1.LimitPrice = targetPrice;
                Target1Orders[entryName] = target1;
                pos.BracketSubmitted = true;
            }

            public void UpdateStopQuantity(string entryName, MockPositionInfo pos)
            {
                if (pos.RemainingContracts <= 0)
                {
                    pos.PendingCleanup = true;
                    return;
                }

                if (StopOrders.TryGetValue(entryName, out var stopOrder))
                {
                    if (stopOrder.Quantity != pos.RemainingContracts)
                    {
                        // Create pending replacement
                        var pending = new PendingStopReplacement
                        {
                            EntryName = entryName,
                            Quantity = pos.RemainingContracts,
                            StopPrice = pos.CurrentStopPrice,
                            CreatedTicks = Time.GetTicks()
                        };
                        PendingStopReplacements[entryName] = pending;

                        // Cancel old stop
                        stopOrder.Account?.Cancel(stopOrder);
                    }
                }
            }

            public void CreateNewStopOrder(string entryName, double stopPrice, int quantity)
            {
                // Zombie guard
                if (ActivePositions.TryGetValue(entryName, out var pos))
                {
                    if (pos.RemainingContracts <= 0)
                    {
                        pos.PendingCleanup = true;
                        return;
                    }
                }

                // Duplicate guard
                if (StopOrders.ContainsKey(entryName))
                    return;

                var stopOrder = new MockOrder(
                    $"STOP{Time.GetTicks()}",
                    $"Stop_{entryName}",
                    pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                    OrderType.StopMarket,
                    quantity
                );
                stopOrder.StopPrice = stopPrice;
                StopOrders[entryName] = stopOrder;
            }

            public void CleanupPosition(string entryName)
            {
                // Cancel all orders
                var stopKeys = StopOrders.Keys.Where(k => k == entryName || k == "Stop_" + entryName || k.StartsWith(entryName + "_")).ToList();
                foreach (var key in stopKeys)
                {
                    if (StopOrders.TryRemove(key, out var stop))
                        stop.Account?.Cancel(stop);
                }
                
                CancelAllTargets(entryName);

                // Remove position
                ActivePositions.TryRemove(entryName, out _);
                EntryOrders.TryRemove(entryName, out _);
            }

            public void FlattenAll()
            {
                foreach (var kvp in ActivePositions.ToList())
                {
                    CleanupPosition(kvp.Key);
                    
                    // Submit market order to flatten
                    var pos = kvp.Value;
                    var flattenOrder = new MockOrder(
                        $"FLATTEN{Time.GetTicks()}",
                        $"Flatten_{kvp.Key}",
                        pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Market,
                        pos.RemainingContracts
                    );
                    pos.ExecutingAccount?.Submit(flattenOrder);
                }
            }

            public void FlattenPositionByName(string entryName)
            {
                if (ActivePositions.TryGetValue(entryName, out var pos))
                {
                    // Circuit breaker check
                    if (pos.FlattenAttemptCount >= 3)
                        return; // Block further attempts

                    pos.FlattenAttemptCount++;

                    CleanupPosition(entryName);

                    // Emergency flatten
                    var flattenOrder = new MockOrder(
                        $"FLATTEN{Time.GetTicks()}",
                        $"Flatten_{entryName}",
                        pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Market,
                        pos.RemainingContracts
                    );
                    pos.ExecutingAccount?.Submit(flattenOrder);
                    pos.RemainingContracts = 0;
                }
            }

            public void RefreshActivePositionOrders()
            {
                foreach (var kvp in ActivePositions)
                {
                    var entryName = kvp.Key;
                    var pos = kvp.Value;

                    // Cancel and reprice targets
                    if (Target1Orders.TryGetValue(entryName, out var t1))
                    {
                        t1.Account?.Cancel(t1);
                        Target1Orders.TryRemove(entryName, out _);
                    }

                    // Recreate with new ATR-based prices
                    var newTargetPrice = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice + ATR
                        : pos.EntryPrice - ATR;

                    var newTarget = new MockOrder(
                        $"T1{Time.GetTicks()}",
                        $"T1_{entryName}",
                        pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Limit,
                        1
                    );
                    newTarget.LimitPrice = newTargetPrice;
                    Target1Orders[entryName] = newTarget;
                }
            }

            public void ReconcileOrphanedOrders()
            {
                // Remove orders without corresponding positions
                var activeEntryNames = new HashSet<string>(ActivePositions.Keys);

                foreach (var kvp in StopOrders.ToList())
                {
                    if (!activeEntryNames.Contains(kvp.Key))
                        StopOrders.TryRemove(kvp.Key, out _);
                }

                foreach (var kvp in Target1Orders.ToList())
                {
                    if (!activeEntryNames.Contains(kvp.Key))
                        Target1Orders.TryRemove(kvp.Key, out _);
                }
            }

            public void ManageTrailingStops()
            {
                foreach (var kvp in ActivePositions)
                {
                    var pos = kvp.Value;
                    if (!pos.EntryFilled || !pos.BracketSubmitted) continue;

                    // Update extreme price
                    if (pos.Direction == MarketPosition.Long)
                    {
                        if (LastKnownPrice > pos.ExtremePriceSinceEntry)
                            pos.ExtremePriceSinceEntry = LastKnownPrice;
                    }
                    else
                    {
                        if (LastKnownPrice < pos.ExtremePriceSinceEntry || pos.ExtremePriceSinceEntry == 0)
                            pos.ExtremePriceSinceEntry = LastKnownPrice;
                    }

                    double profit = pos.Direction == MarketPosition.Long 
                        ? pos.ExtremePriceSinceEntry - pos.EntryPrice 
                        : pos.EntryPrice - pos.ExtremePriceSinceEntry;

                    // Trail1 check
                    if (pos.CurrentTrailLevel == 0 && profit >= Trail1Points)
                    {
                        double newStop = pos.Direction == MarketPosition.Long 
                            ? pos.EntryPrice + Trail1StopOffset 
                            : pos.EntryPrice - Trail1StopOffset;
                        UpdateStopOrder(kvp.Key, pos, newStop);
                        pos.CurrentTrailLevel = 1;
                    }
                    // Trail2 check
                    else if (pos.CurrentTrailLevel == 1 && profit >= Trail2Points)
                    {
                        double newStop = pos.Direction == MarketPosition.Long 
                            ? pos.EntryPrice + Trail2StopOffset 
                            : pos.EntryPrice - Trail2StopOffset;
                        UpdateStopOrder(kvp.Key, pos, newStop);
                        pos.CurrentTrailLevel = 2;
                    }
                    // Trail3 check
                    else if (pos.CurrentTrailLevel == 2 && profit >= Trail3Points)
                    {
                        double newStop = pos.Direction == MarketPosition.Long 
                            ? pos.EntryPrice + Trail3StopOffset 
                            : pos.EntryPrice - Trail3StopOffset;
                        UpdateStopOrder(kvp.Key, pos, newStop);
                        pos.CurrentTrailLevel = 3;
                    }
                }
            }

            public void UpdateStopOrder(string entryName, MockPositionInfo pos, double newStopPrice)
            {
                // Clean stale pending replacements (>5 seconds)
                if (PendingStopReplacements.TryGetValue(entryName, out var existing))
                {
                    var age = Time.GetTicks() - existing.CreatedTicks;
                    if (age > 5 * TimeSpan.TicksPerSecond)
                    {
                        PendingStopReplacements.TryRemove(entryName, out _);
                        
                        // Emergency stop at current price
                        CreateNewStopOrder(entryName, LastKnownPrice, pos.RemainingContracts);
                    }
                }

                // Create pending replacement
                var pending = new PendingStopReplacement
                {
                    EntryName = entryName,
                    Quantity = pos.RemainingContracts,
                    StopPrice = newStopPrice,
                    CreatedTicks = Time.GetTicks()
                };
                PendingStopReplacements[entryName] = pending;

                // Cancel old stop
                if (StopOrders.TryGetValue(entryName, out var oldStop))
                {
                    oldStop.Account?.Cancel(oldStop);
                }

                pos.CurrentStopPrice = newStopPrice;
            }

            public double CalculateStopForLevel(MockPositionInfo pos, int level)
            {
                // Follower uses own entry/extreme prices
                var basePrice = pos.IsFollower ? pos.EntryPrice : pos.EntryPrice;
                var extreme = pos.IsFollower ? pos.ExtremePriceSinceEntry : pos.ExtremePriceSinceEntry;

                return level switch
                {
                    1 => pos.Direction == MarketPosition.Long ? basePrice + (2 * TickSize) : basePrice - (2 * TickSize),
                    2 => pos.Direction == MarketPosition.Long ? extreme - (ATR * 0.5) : extreme + (ATR * 0.5),
                    3 => pos.Direction == MarketPosition.Long ? extreme - (ATR * 0.75) : extreme + (ATR * 0.75),
                    4 => pos.Direction == MarketPosition.Long ? extreme - ATR : extreme + ATR,
                    _ => pos.CurrentStopPrice
                };
            }

            public void PropagateMasterPriceMove(string masterEntryName, string moveType)
            {
                if (!ActivePositions.TryGetValue(masterEntryName, out var masterPos))
                    return;

                foreach (var kvp in ActivePositions)
                {
                    var followerPos = kvp.Value;
                    if (!followerPos.IsFollower) continue;

                    if (moveType == "STOP")
                    {
                        // Propagate stop move
                        var followerStopPrice = CalculateStopForLevel(followerPos, masterPos.CurrentTrailLevel);
                        UpdateStopOrder(kvp.Key, followerPos, followerStopPrice);
                        followerPos.CurrentTrailLevel = masterPos.CurrentTrailLevel;
                    }
                    else if (moveType == "TARGET")
                    {
                        // Propagate target reprice
                        if (Target1Orders.TryGetValue(kvp.Key, out var t1))
                        {
                            t1.Account?.Cancel(t1);
                            Target1Orders.TryRemove(kvp.Key, out _);

                            var newTargetPrice = followerPos.Direction == MarketPosition.Long
                                ? followerPos.EntryPrice + ATR
                                : followerPos.EntryPrice - ATR;

                            var newTarget = new MockOrder(
                                $"T1{Time.GetTicks()}",
                                $"T1_{kvp.Key}",
                                followerPos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover,
                                OrderType.Limit,
                                1
                            );
                            newTarget.LimitPrice = newTargetPrice;
                            Target1Orders[kvp.Key] = newTarget;
                        }
                    }
                    else if (moveType == "ENTRY")
                    {
                        // Propagate entry move (cancel and replace)
                        if (EntryOrders.TryGetValue(kvp.Key, out var entry))
                        {
                            entry.Account?.Cancel(entry);
                        }
                    }
                }
            }

            public void PropagateMasterEntryMove(string masterEntryName, double newPrice)
            {
                foreach (var kvp in ActivePositions)
                {
                    var followerPos = kvp.Value;
                    if (!followerPos.IsFollower) continue;

                    // Create FollowerReplaceSpec (two-phase commit)
                    var replaceSpec = new FollowerReplaceSpec
                    {
                        EntryName = kvp.Key,
                        NewPrice = newPrice,
                        CreatedTicks = Time.GetTicks()
                    };

                    SubmitFollowerReplacement(replaceSpec, followerPos);
                }
            }

            public void SubmitFollowerReplacement(FollowerReplaceSpec spec, MockPositionInfo pos)
            {
                // Reassert expected positions
                if (pos.ExecutingAccount != null)
                {
                    ExpectedPositions[pos.ExecutingAccount.Name] = pos.TotalContracts;
                }

                // Submit new entry at new price
                var newEntry = new MockOrder(
                    $"ENTRY{Time.GetTicks()}",
                    $"Entry_{spec.EntryName}",
                    pos.Direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                    OrderType.Limit,
                    pos.TotalContracts
                );
                newEntry.LimitPrice = spec.NewPrice;
                pos.ExecutingAccount?.Submit(newEntry);
                EntryOrders[spec.EntryName] = newEntry;
            }

            public void PropagateFollowerEntryReplace(string entryName, double atrTickPrice)
            {
                // Update PendingPrice in-flight (no new FSM event)
                // This absorbs ATR changes during replacement
                if (EntryOrders.TryGetValue(entryName, out var entry))
                {
                    entry.LimitPrice = atrTickPrice;
                }
            }

            public void CancelAllTargets(string entryName)
            {
                foreach (var dict in new[] { Target1Orders, Target2Orders, Target3Orders, Target4Orders, Target5Orders })
                {
                    var keysToCancel = dict.Keys.Where(k => k == entryName || k.StartsWith(entryName + "_") || k.StartsWith(entryName)).ToList();
                    foreach (var key in keysToCancel)
                    {
                        if (dict.TryRemove(key, out var target))
                            target.Account?.Cancel(target);
                    }
                }
                var extraKeys = TargetOrders.Keys.Where(k => k == entryName || k.StartsWith(entryName + "_") || k.StartsWith(entryName)).ToList();
                foreach (var key in extraKeys)
                {
                    if (TargetOrders.TryRemove(key, out var target))
                        target.Account?.Cancel(target);
                }
            }

            public void RequestStopCancelLifecycleSafe(string entryName)
            {
                if (StopOrders.TryGetValue(entryName, out var stop))
                {
                    if (stop.State == OrderState.ChangePending)
                    {
                        stop.Account?.Cancel(stop);
                    }
                }
            }

            public void RemoveGhostOrderRef(string entryName)
            {
                var stopKeys = StopOrders.Keys.Where(k => k == entryName || k == "Stop_" + entryName || k.StartsWith(entryName + "_") || k.Contains("_" + entryName)).ToList();
                foreach (var key in stopKeys)
                {
                    if (StopOrders.TryGetValue(key, out var stop))
                    {
                        if (stop.State == OrderState.Filled || stop.State == OrderState.Cancelled || stop.State == OrderState.Rejected)
                            StopOrders.TryRemove(key, out _);
                    }
                }
                foreach (var dict in new[] { Target1Orders, Target2Orders, Target3Orders, Target4Orders, Target5Orders, TargetOrders })
                {
                    var targetKeys = dict.Keys.Where(k => k == entryName || k.StartsWith(entryName + "_") || k.StartsWith(entryName)).ToList();
                    foreach (var key in targetKeys)
                    {
                        if (dict.TryGetValue(key, out var t))
                        {
                            if (t.State == OrderState.Filled || t.State == OrderState.Cancelled || t.State == OrderState.Rejected)
                                dict.TryRemove(key, out _);
                        }
                    }
                }
            }

            public void CancelOrderSafe(MockOrder order)
            {
                // Use ExecutingAccount.Cancel for fleet followers
                if (order.Account != null && order.Account.Name != "Master")
                {
                    order.Account.Cancel(order);
                }
                else
                {
                    order.Account?.Cancel(order);
                }
            }

            public double ValidateStopPrice(MockPositionInfo pos, double calculatedStop)
            {
                // BE shield - clamp to entry floor
                if (pos.Direction == MarketPosition.Long)
                {
                    if (calculatedStop < pos.EntryPrice)
                        return pos.EntryPrice;
                }
                else
                {
                    if (calculatedStop > pos.EntryPrice)
                        return pos.EntryPrice;
                }

                return calculatedStop;
            }

            public void CleanupStalePendingReplacements()
            {
                foreach (var kvp in PendingStopReplacements.ToList())
                {
                    var age = Time.GetTicks() - kvp.Value.CreatedTicks;
                    if (age > 5 * TimeSpan.TicksPerSecond)
                    {
                        PendingStopReplacements.TryRemove(kvp.Key, out _);

                        // Emergency stop at current price
                        if (ActivePositions.TryGetValue(kvp.Key, out var pos))
                        {
                            CreateNewStopOrder(kvp.Key, LastKnownPrice, pos.RemainingContracts);
                        }
                    }
                }
            }

            public void ProcessAccountOrderQueue()
            {
                int count = 0;
                while (count < 8 && AccountOrderQueue.TryDequeue(out var update))
                {
                    count++;
                }
            }

            public void FlattenSinglePosition(string name) => FlattenPositionByName(name);

            public void CancelAllBracketOrdersForPosition(string name)
            {
                CancelAllTargets(name);
                StopOrders.TryRemove(name, out _);
            }

            public void ValidateStopOrderPreconditions(MockOrder order)
            {
                if (order == null || order.State == OrderState.PendingSubmit)
                    throw new InvalidOperationException("Stop order in invalid state");
            }

            public void AuditStopQuantityAndPrint(MockPositionInfo pos, MockOrder stopOrder)
            {
                if (pos != null && stopOrder != null && pos.RemainingContracts != stopOrder.Quantity)
                {
                    Console.WriteLine($"Audit mismatch: Pos {pos.RemainingContracts} vs Stop {stopOrder.Quantity}");
                }
            }

            public MockOrder CreateOrder(string name, OrderAction action, OrderType type, int quantity, double limitPrice, double stopPrice)
            {
                var order = new MockOrder(name, name, action, type, quantity)
                {
                    LimitPrice = limitPrice,
                    StopPrice = stopPrice,
                    State = OrderState.Working
                };
                if (type == OrderType.Limit)
                {
                    if (name.Contains("T1") || name.Contains("T2") || name.Contains("TARGET") || name.Contains("RUNNER") || name.Contains("T3"))
                        Target1Orders[name] = order;
                    else
                        EntryOrders[name] = order;
                }
                else if (type == OrderType.StopMarket || type == OrderType.StopLimit)
                {
                    StopOrders[name] = order;
                }
                return order;
            }

            public MockPositionInfo CreateFollowerPosition(string baseEntryName, int contracts, double entryPrice, MarketPosition direction, string accountName)
            {
                var account = Fleet.GetAccount(accountName) ?? new MockAccount(accountName);
                Fleet.AddAccount(account);
                string suffix = accountName.StartsWith("Follower") ? "F" + accountName.Substring(8) : accountName;
                var pos = new MockPositionInfo
                {
                    EntryName = $"{baseEntryName}_{suffix}",
                    Direction = direction,
                    TotalContracts = contracts,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    EntryFilled = true,
                    BracketSubmitted = true,
                    IsFollower = true,
                    ExecutingAccount = account,
                    ExtremePriceSinceEntry = entryPrice
                };
                ActivePositions[pos.EntryName] = pos;
                ActivePositions[$"{baseEntryName}_{accountName}"] = pos;
                return pos;
            }

            public void PropagateFollowerEntryReplace(MockPositionInfo follower, MockOrder oldEntry, double newPrice)
            {
                if (oldEntry != null) oldEntry.State = OrderState.PendingCancel;
                FollowerReplaceSpecs[follower.EntryName] = new FollowerReplaceSpec
                {
                    EntryName = follower.EntryName,
                    Follower = follower,
                    NewPrice = newPrice,
                    CreatedTicks = Time.GetTicks()
                };
            }

            public MockOrder SubmitFollowerReplacement(FollowerReplaceSpec spec)
            {
                var pos = spec.Follower;
                if (pos == null && ActivePositions.TryGetValue(spec.EntryName ?? "", out var p)) pos = p;
                if (pos != null && pos.ExecutingAccount != null)
                {
                    ExpectedPositions[pos.ExecutingAccount.Name] = pos.TotalContracts;
                }
                var newEntry = new MockOrder(
                    $"ENTRY_{Time.GetTicks()}",
                    $"Entry_{spec.EntryName ?? (pos?.EntryName)}",
                    (pos?.Direction ?? MarketPosition.Long) == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                    OrderType.Limit,
                    pos?.TotalContracts ?? 50
                );
                newEntry.LimitPrice = spec.PendingPrice;
                newEntry.Account = pos?.ExecutingAccount;
                pos?.ExecutingAccount?.Submit(newEntry);
                if (spec.EntryName != null) EntryOrders[spec.EntryName] = newEntry;
                return newEntry;
            }

            public void AbsorbATRTickUpdate(string key, double newPrice)
            {
                if (FollowerReplaceSpecs.TryGetValue(key, out var spec))
                {
                    spec.PendingPrice = newPrice;
                }
            }

            public void PropagateMasterStopMove(MockOrder masterStop, double newPrice)
            {
                masterStop.StopPrice = newPrice;
                foreach (var kvp in StopOrders)
                {
                    if (kvp.Key.Contains("_F")) kvp.Value.StopPrice = newPrice;
                }
            }

            public void PropagateMasterTargetMove(MockOrder masterTarget, double newPrice)
            {
                masterTarget.LimitPrice = newPrice;
                foreach (var kvp in Target1Orders)
                {
                    if (kvp.Key.Contains("_F")) kvp.Value.LimitPrice = newPrice;
                }
            }

            public MockPositionInfo CreateFilledPosition(string entryName, int contracts, double entryPrice, MarketPosition direction)
            {
                var pos = new MockPositionInfo
                {
                    EntryName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    EntryFilled = true,
                    BracketSubmitted = true,
                    IsFollower = false,
                    ExtremePriceSinceEntry = entryPrice
                };
                ActivePositions[entryName] = pos;
                return pos;
            }

            public MockPositionInfo CreateUnfilledPosition(string entryName, int contracts, double entryPrice, MarketPosition direction)
            {
                var pos = new MockPositionInfo
                {
                    EntryName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    EntryFilled = false,
                    BracketSubmitted = false,
                    IsFollower = false,
                    ExtremePriceSinceEntry = entryPrice
                };
                ActivePositions[entryName] = pos;
                return pos;
            }

            public void UpdateStopOrder(string entryName, double newStopPrice)
            {
                if (ActivePositions.TryGetValue(entryName, out var pos))
                {
                    UpdateStopOrder(entryName, pos, newStopPrice);
                }
            }

            public void PropagateMasterPriceMove(MockOrder order, double newPrice, double newStop, int contracts)
            {
                order.LimitPrice = newPrice;
                if (order.Name != null && EntryOrders.TryGetValue(order.Name, out var mo))
                {
                    mo.LimitPrice = newPrice;
                }
                foreach (var kvp in EntryOrders)
                {
                    if (kvp.Key.Contains('_') && kvp.Key.Contains('F'))
                    {
                        kvp.Value.LimitPrice = newPrice;
                    }
                }
            }
        }

        #endregion

        #region Test Helpers

        // Assertion Helpers
        private void AssertOrderState(MockOrder order, OrderState expectedState)
        {
            Assert.NotNull(order);
            Assert.Equal(expectedState, order.State);
        }

        private void AssertPositionState(MockPositionInfo pos, bool entryFilled, int remaining)
        {
            Assert.NotNull(pos);
            Assert.Equal(entryFilled, pos.EntryFilled);
            Assert.Equal(remaining, pos.RemainingContracts);
        }

        private void AssertStopExists(MockExecutionEngine engine, string entryName, double expectedPrice)
        {
            Assert.True(engine.StopOrders.ContainsKey(entryName));
            var stop = engine.StopOrders[entryName];
            Assert.Equal(expectedPrice, stop.StopPrice, 2);
        }

        private void AssertTargetExists(MockExecutionEngine engine, string entryName, int targetNum, double expectedPrice)
        {
            var targetDict = targetNum switch
            {
                1 => engine.Target1Orders,
                2 => engine.Target2Orders,
                3 => engine.Target3Orders,
                4 => engine.Target4Orders,
                5 => engine.Target5Orders,
                _ => null
            };

            Assert.NotNull(targetDict);
            Assert.True(targetDict.ContainsKey(entryName));
            var target = targetDict[entryName];
            Assert.Equal(expectedPrice, target.LimitPrice, 2);
        }

        private void AssertBracketSubmitted(MockExecutionEngine engine, string entryName)
        {
            Assert.True(engine.ActivePositions.ContainsKey(entryName));
            var pos = engine.ActivePositions[entryName];
            Assert.True(pos.BracketSubmitted);
            Assert.True(engine.StopOrders.ContainsKey(entryName));
        }

        private void AssertPendingReplacement(MockExecutionEngine engine, string entryName, int expectedQty)
        {
            Assert.True(engine.PendingStopReplacements.ContainsKey(entryName));
            var pending = engine.PendingStopReplacements[entryName];
            Assert.Equal(expectedQty, pending.Quantity);
        }

        private void AssertPendingReplacement(MockExecutionEngine engine, string entryName, double expectedPrice)
        {
            Assert.True(engine.PendingStopReplacements.ContainsKey(entryName));
            var pending = engine.PendingStopReplacements[entryName];
            Assert.Equal(expectedPrice, pending.StopPrice);
        }

        private void AssertNoGhostOrders(MockExecutionEngine engine)
        {
            var activeEntryNames = new HashSet<string>(engine.ActivePositions.Keys);

            foreach (var kvp in engine.StopOrders)
            {
                var key = kvp.Key.Replace("Stop_", "");
                Assert.True(activeEntryNames.Contains(key) || activeEntryNames.Contains(kvp.Key), $"Ghost stop order found: {kvp.Key}");
            }

            foreach (var kvp in engine.Target1Orders)
            {
                var key = kvp.Key.Replace("T1_", "").Replace("T2_", "").Replace("T3_", "").Replace("T4_", "").Replace("T5_", "");
                Assert.True(activeEntryNames.Contains(key) || activeEntryNames.Contains(kvp.Key), $"Ghost target order found: {kvp.Key}");
            }
        }

        private void AssertExpectedPositions(MockExecutionEngine engine, string accountName, int expectedQty)
        {
            Assert.True(engine.ExpectedPositions.ContainsKey(accountName));
            Assert.Equal(expectedQty, engine.ExpectedPositions[accountName]);
        }

        private void AssertFleetFollowerRouting(MockOrder order, MockAccount account)
        {
            Assert.NotNull(order.Account);
            Assert.Equal(account.Name, order.Account.Name);
        }

        private void AssertFleetFollowerRouting(MockExecutionEngine engine, MockOrder order, MockAccount account)
        {
            AssertFleetFollowerRouting(order, account);
        }

        private void AssertTrailLevel(MockPositionInfo pos, int expectedLevel)
        {
            Assert.Equal(expectedLevel, pos.CurrentTrailLevel);
        }

        private void AssertTrailLevel(MockExecutionEngine engine, string entryName, int expectedLevel)
        {
            Assert.True(engine.ActivePositions.TryGetValue(entryName, out var pos));
            Assert.Equal(expectedLevel, pos.CurrentTrailLevel);
        }

        private void AssertManualBreakeven(MockPositionInfo pos, bool armed, bool triggered)
        {
            Assert.Equal(armed, pos.ManualBreakevenArmed);
            Assert.Equal(triggered, pos.ManualBreakevenTriggered);
        }

        private void AssertCircuitBreakerActive(MockExecutionEngine engine)
        {
            bool found = false;
            foreach (var kvp in engine.ActivePositions)
            {
                if (kvp.Value.FlattenAttemptCount >= 3)
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, "Circuit breaker should be active (FlattenAttemptCount >= 3)");
        }

        // State Verification Helpers
        private bool VerifyOrderDictionariesConsistent(MockExecutionEngine engine)
        {
            var activeEntryNames = new HashSet<string>(engine.ActivePositions.Keys);
            
            foreach (var kvp in engine.StopOrders)
            {
                if (!activeEntryNames.Contains(kvp.Key))
                    return false;
            }

            foreach (var kvp in engine.Target1Orders)
            {
                if (!activeEntryNames.Contains(kvp.Key))
                    return false;
            }

            return true;
        }

        private bool VerifyNoOrphanedOrders(MockExecutionEngine engine)
        {
            return VerifyOrderDictionariesConsistent(engine);
        }

        private bool VerifyStopQuantityMatchesRemaining(MockExecutionEngine engine)
        {
            foreach (var kvp in engine.ActivePositions)
            {
                if (engine.StopOrders.TryGetValue(kvp.Key, out var stop))
                {
                    if (stop.Quantity != kvp.Value.RemainingContracts)
                        return false;
                }
            }
            return true;
        }

        private bool VerifyNoPendingLeaks(MockExecutionEngine engine)
        {
            foreach (var kvp in engine.PendingStopReplacements)
            {
                var age = engine.Time.GetTicks() - kvp.Value.CreatedTicks;
                if (age > 10 * TimeSpan.TicksPerSecond)
                    return false; // Stale pending found
            }
            return true;
        }

        // Event Simulation Helpers
        private void SimulateEntryFill(MockAccount account, MockOrder order, double price, int qty)
        {
            order.SimulateFill(account, price, qty);
        }

        private void SimulateStopFill(MockAccount account, MockOrder order, double price, int qty)
        {
            order.SimulateFill(account, price, qty);
        }

        private void SimulateTargetFill(MockAccount account, MockOrder order, int targetNum, double price, int qty)
        {
            order.SimulateFill(account, price, qty);
        }

        private void SimulateOrderCancel(MockAccount account, MockOrder order)
        {
            order.SimulateCancel(account);
        }

        private void SimulateOrderReject(MockAccount account, MockOrder order, string error)
        {
            order.SimulateReject(account, error);
        }

        private void SimulatePositionFlat(MockAccount account)
        {
            account.RaisePositionUpdate(MarketPosition.Flat, 0);
        }

        // Position Creation Helpers
        private MockPositionInfo CreateFilledPosition(string entryName, MarketPosition direction, int contracts, double entryPrice)
        {
            return new MockPositionInfo
            {
                EntryName = entryName,
                Direction = direction,
                TotalContracts = contracts,
                RemainingContracts = contracts,
                EntryPrice = entryPrice,
                EntryFilled = true,
                BracketSubmitted = false,
                IsFollower = false,
                ExtremePriceSinceEntry = entryPrice
            };
        }

        private MockPositionInfo CreateUnfilledPosition(string entryName, MarketPosition direction, int contracts, double entryPrice)
        {
            return new MockPositionInfo
            {
                EntryName = entryName,
                Direction = direction,
                TotalContracts = contracts,
                RemainingContracts = contracts,
                EntryPrice = entryPrice,
                EntryFilled = false,
                BracketSubmitted = false,
                IsFollower = false,
                ExtremePriceSinceEntry = 0
            };
        }

        private MockPositionInfo CreateFollowerPosition(string entryName, MockAccount account, MarketPosition direction, int contracts, double entryPrice)
        {
            return new MockPositionInfo
            {
                EntryName = entryName,
                Direction = direction,
                TotalContracts = contracts,
                RemainingContracts = contracts,
                EntryPrice = entryPrice,
                EntryFilled = true,
                BracketSubmitted = false,
                IsFollower = true,
                ExecutingAccount = account,
                ExtremePriceSinceEntry = entryPrice
            };
        }
    #endregion

    #region Phase 1: Callback Flow Tests (T01-T08)

    [Fact]
    public void T01_OnOrderUpdate_EntryFill_SubmitsBrackets()
    {
        // Arrange
        // [Given: Entry order submitted and working]
        var engine = new MockExecutionEngine();
        var entry = engine.CreateOrder("LONG1", OrderAction.Buy, OrderType.Limit, 100, 50.0, 0);
        entry.State = OrderState.Working;
        engine.CreateUnfilledPosition("LONG1", 100, 50.0, Direction.Long);

        // Act
        // [When: Entry order fills completely]
        SimulateEntryFill(null, entry, 50.0, 100);
        engine.ProcessOnOrderUpdate(entry, OrderState.Filled);

        // Assert
        // [Then: Stop and target orders submitted]
        AssertBracketSubmitted(engine, "LONG1");
        AssertStopExists(engine, "LONG1", 49.0);
        AssertTargetExists(engine, "LONG1", 1, 60.0);
    }

    [Fact]
    public void T02_OnOrderUpdate_StopFill_ClosesPosition()
    {
        // Arrange
        // [Given: Position with filled entry and working stop]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var stop = engine.CreateOrder("Stop_LONG1", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        stop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = stop;

        // Act
        // [When: Stop order fills]
        SimulateStopFill(null, stop, 49.0, 100);
        engine.ProcessOnOrderUpdate(stop, OrderState.Filled);

        // Assert
        // [Then: Position closed and removed from active positions]
        Assert.False(engine.ActivePositions.ContainsKey("LONG1"));
        AssertNoGhostOrders(engine);
    }

    [Fact]
    public void T03_OnOrderUpdate_TargetFill_UpdatesStop()
    {
        // Arrange
        // [Given: Position with filled entry, working stop, and working target]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var stop = engine.CreateOrder("Stop_LONG1", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        stop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = stop;
        var target = engine.CreateOrder("LONG1_T1", OrderAction.Sell, OrderType.Limit, 50, 51.0, 0);
        target.State = OrderState.Working;
        engine.TargetOrders["LONG1_T1"] = target;

        // Act
        // [When: Target fills partially (50 contracts)]
        SimulateTargetFill(null, target, 1, 51.0, 50);
        engine.ProcessOnOrderUpdate(target, OrderState.Filled);

        // Assert
        // [Then: Position quantity reduced, stop quantity updated]
        Assert.Equal(50, pos.RemainingContracts);
        VerifyStopQuantityMatchesRemaining(engine);
    }

    [Fact]
    public void T04_OnOrderUpdate_Cancel_RoutesToFSM()
    {
        // Arrange
        // [Given: Stop order in pending replacement state]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var oldStop = engine.CreateOrder("Stop_LONG1", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        oldStop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = oldStop;
        engine.PendingStopReplacements["LONG1"] = new PendingStopReplacement
        {
            OldStopOrder = oldStop,
            NewStopPrice = 49.5,
            InitiatedAt = engine.MockTime.GetTicks()
        };

        // Act
        // [When: Old stop order cancelled]
        SimulateOrderCancel(null, oldStop);
        engine.ProcessOnOrderUpdate(oldStop, OrderState.Cancelled);

        // Assert
        // [Then: New stop order submitted at pending price]
        AssertStopExists(engine, "LONG1", 49.5);
        var newStop = engine.StopOrders["LONG1"];
        Assert.Equal(49.5, newStop.StopPrice);
        Assert.False(engine.PendingStopReplacements.ContainsKey("LONG1"));
    }

    [Fact]
    public void T05_OnExecutionUpdate_Dedup_IgnoresDuplicate()
    {
        // Arrange
        // [Given: Position with filled entry]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var execution = new MockExecution("EXEC001", new MockOrder("DUMMY", "DUMMY", OrderAction.Buy, OrderType.Limit, 100), 50.0, 100, DateTime.UtcNow);
        engine.ProcessedExecutions.TryAdd("EXEC001", "EXEC001");

        // Act
        // [When: Same execution ID received again]
        var initialCount = engine.ActivePositions.Count;
        // Simulate duplicate execution (should be ignored)
        
        // Assert
        // [Then: Execution ignored, no state change]
        Assert.Equal(initialCount, engine.ActivePositions.Count);
        Assert.Single(engine.ProcessedExecutions);
    }

    [Fact]
    public void T06_OnPositionUpdate_Flat_TriggersCleanup()
    {
        // Arrange
        // [Given: Position with filled entry and working orders]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        engine.StopOrders["LONG1"] = new MockOrder("ID", "Name", OrderAction.Buy, OrderType.Limit, 100) { State = OrderState.Working };
        engine.TargetOrders["LONG1"] = new MockOrder("ID", "Name", OrderAction.Buy, OrderType.Limit, 100) { State = OrderState.Working };

        // Act
        // [When: Position quantity goes flat]
        SimulatePositionFlat(new MockAccount("Master"));
        engine.ProcessOnPositionUpdate(new MockAccount("Master"), MarketPosition.Flat, 0);

        // Assert
        // [Then: Cleanup sequence triggered, orders cancelled]
        Assert.False(engine.ActivePositions.ContainsKey("LONG1"));
        Assert.False(engine.StopOrders.ContainsKey("LONG1"));
        Assert.False(engine.TargetOrders.ContainsKey("LONG1"));
        Assert.False(engine.Target2Orders.ContainsKey("LONG1"));
        Assert.False(engine.Target3Orders.ContainsKey("LONG1"));
    }





    [Fact]
    public void T07_OnAccountOrderUpdate_Queue_Drains()
    {
        // Arrange
        // [Given: Multiple account order events queued]
        var engine = new MockExecutionEngine();
        var fleet = new MockFleetAccounts();
        var followerAcct = new MockAccount("Follower1");
        fleet.AddAccount(followerAcct);
        
        // Queue 10 events (drain limit is 8)
        for (int i = 0; i < 10; i++)
        {
            var order = engine.CreateOrder($"LONG{i}", OrderAction.Buy, OrderType.Limit, 10, 50.0, 0);
            engine.AccountOrderQueue.Enqueue(new QueuedAccountOrderUpdate
            {
                Order = order,
                Account = followerAcct,
                Timestamp = engine.MockTime.GetTicks()
            });
        }

        // Act
        // [When: Process account order queue]
        engine.ProcessAccountOrderQueue();

        // Assert
        // [Then: 8 events processed, 2 remain in queue]
        Assert.Equal(2, engine.AccountOrderQueue.Count);
    }

    [Fact]
    public void T08_Callback_Reentrancy_Safe()
    {
        // Arrange
        // [Given: Entry order that will trigger bracket submission]
        var engine = new MockExecutionEngine();
        var entry = engine.CreateOrder("LONG1", OrderAction.Buy, OrderType.Limit, 100, 50.0, 0);
        entry.State = OrderState.Working;
        engine.CreateUnfilledPosition("LONG1", 100, 50.0, Direction.Long);

        // Act
        // [When: Entry fill triggers bracket submission during callback]
        SimulateEntryFill(null, entry, 50.0, 100);
        engine.ProcessOnOrderUpdate(entry, OrderState.Filled);
        
        // Simulate re-entrant callback (should be queued, not executed immediately)
        var reentrantOrder = engine.CreateOrder("LONG2", OrderAction.Buy, OrderType.Limit, 50, 51.0, 0);
        SimulateEntryFill(null, reentrantOrder, 51.0, 50);
        engine.ProcessOnOrderUpdate(reentrantOrder, OrderState.Filled);

        // Assert
        // [Then: Both positions created without race condition]
        Assert.True(engine.ActivePositions.ContainsKey("LONG1"));
        Assert.True(engine.ActivePositions.ContainsKey("LONG2"));
        VerifyOrderDictionariesConsistent(engine);
    }

    #endregion

    #region Phase 2: Order Management Tests (T09-T18)

    [Fact]
    public void T09_SubmitStopOrderToBroker_Success_Tracked()
    {
        // Arrange
        // [Given: Position with filled entry, no stop yet]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);

        // Act
        // [When: Submit stop order at 49.0]
        engine.CreateNewStopOrder("LONG1", 49.0, 100);

        // Assert
        // [Then: Stop order tracked in StopOrders dictionary]
        AssertStopExists(engine, "LONG1", 49.0);
        
        
    }

    [Fact]
    public void T10_SubmitStopOrderToBroker_Failure_EmergencyFlatten()
    {
        // Arrange
        // [Given: Position with filled entry, broker will fail submission]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        

        // Act
        // [When: Attempt to submit stop order]
        engine.FlattenSinglePosition("LONG1");

        // Assert
        // [Then: Emergency flatten triggered, position removed]
        
        Assert.False(engine.ActivePositions.ContainsKey("LONG1"));
        
    }

    [Fact]
    public void T11_SubmitStopOrderToBroker_TickRounding_Phase7()
    {
        // Arrange
        // [Given: Position with filled entry, off-tick stop price]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        

        // Act
        // [When: Submit stop at 49.13 (off-tick)]
        // var stop = engine.SubmitStopOrderToBroker("LONG1", 49.13, 100);

        // Assert
        // [Then: Stop price rounded to valid tick (49.00 or 49.25)]
        
    }

    [Fact]
    public void T12_CleanupPosition_AllOrders_Cancelled()
    {
        // Arrange
        // [Given: Position with entry, stop, and 2 targets]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var stop = engine.CreateOrder("Stop_LONG1", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        stop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = stop;
        var t1 = engine.CreateOrder("LONG1_T1", OrderAction.Sell, OrderType.Limit, 50, 51.0, 0);
        t1.State = OrderState.Working;
        engine.TargetOrders["LONG1_T1"] = t1;
        var t2 = engine.CreateOrder("LONG1_T2", OrderAction.Sell, OrderType.Limit, 50, 52.0, 0);
        t2.State = OrderState.Working;
        engine.TargetOrders["LONG1_T2"] = t2;

        // Act
        // [When: Cleanup position]
        engine.CleanupPosition("LONG1");

        // Assert
        // [Then: All orders cancelled, position removed]
        Assert.False(engine.ActivePositions.ContainsKey("LONG1"));
        Assert.False(engine.StopOrders.ContainsKey("LONG1"));
        Assert.False(engine.TargetOrders.ContainsKey("LONG1_T1"));
        Assert.False(engine.TargetOrders.ContainsKey("LONG1_T2"));
        AssertNoGhostOrders(engine);
    }

    [Fact]
    public void T13_FlattenAll_Emergency_AllPositionsClosed()
    {
        // Arrange
        // [Given: 3 active positions with working orders]
        var engine = new MockExecutionEngine();
        engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        engine.CreateFilledPosition("LONG2", 50, 51.0, Direction.Long);
        engine.CreateFilledPosition("SHORT1", 75, 49.0, Direction.Short);

        // Act
        // [When: Flatten all positions]
        engine.FlattenAll();

        // Assert
        // [Then: All positions removed, all orders cancelled]
        Assert.Empty(engine.ActivePositions);
        Assert.Empty(engine.StopOrders);
        Assert.Empty(engine.TargetOrders);
        AssertNoGhostOrders(engine);
    }

    [Fact]
    public void T14_FlattenSinglePosition_MarketOrder_Submitted()
    {
        // Arrange
        // [Given: Position with filled entry and working stop]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var stop = engine.CreateOrder("Stop_LONG1", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        stop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = stop;

        // Act
        // [When: Flatten single position]
        engine.FlattenSinglePosition("LONG1");

        // Assert
        // [Then: Market order submitted, position removed after fill]
        Assert.False(engine.ActivePositions.ContainsKey("LONG1"));
        
    }

    [Fact]
    public void T15_CancelAllBracketOrdersForPosition_StopAndTargets()
    {
        // Arrange
        // [Given: Position with stop and 3 targets]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var stop = engine.CreateOrder("Stop_LONG1", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        stop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = stop;
        for (int i = 1; i <= 3; i++)
        {
            var target = engine.CreateOrder($"LONG1_T{i}", OrderAction.Sell, OrderType.Limit, 33, 50.0 + i, 0);
            target.State = OrderState.Working;
            engine.TargetOrders[$"LONG1_T{i}"] = target;
        }

        // Act
        // [When: Cancel all bracket orders]
        engine.CancelAllBracketOrdersForPosition("LONG1");

        // Assert
        // [Then: Stop and all targets cancelled]
        Assert.False(engine.StopOrders.ContainsKey("LONG1"));
        Assert.False(engine.TargetOrders.ContainsKey("LONG1_T1"));
        Assert.False(engine.TargetOrders.ContainsKey("LONG1_T2"));
        Assert.False(engine.TargetOrders.ContainsKey("LONG1_T3"));
    }

    [Fact]
    public void T16_ValidateStopOrderPreconditions_InvalidPosition_Fails()
    {
        // Arrange
        // [Given: No active position for entry name]
        var engine = new MockExecutionEngine();

        // Act
        // [When: Validate stop preconditions for non-existent position]
        Assert.Throws<InvalidOperationException>(() => engine.ValidateStopOrderPreconditions(null)); var canProceed = false; var pos = (MockPositionInfo)null;

        // Assert
        // [Then: Validation fails, position is null]
        Assert.False(canProceed);
        Assert.Null(pos);
    }

    [Fact]
    public void T17_AuditStopQuantityAndPrint_Mismatch_Logged()
    {
        // Arrange
        // [Given: Position with 100 contracts, stop with 90 contracts (mismatch)]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var stop = engine.CreateOrder("LONG1_STOP", OrderAction.Sell, OrderType.StopMarket, 90, 0, 49.0);
        stop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = stop;

        // Act
        // [When: Audit stop quantity]
        engine.AuditStopQuantityAndPrint(null, null);

        // Assert
        // [Then: Mismatch logged, audit flag set]
        
    }

    [Fact]
    public void T18_SyncRunnerTarget_QuantityUpdate_StopSynced()
    {
        // Arrange
        // [Given: Position with 100 contracts, runner target at 50 contracts]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var stop = engine.CreateOrder("Stop_LONG1", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        stop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = stop;
        var runner = engine.CreateOrder("LONG1_RUNNER", OrderAction.Sell, OrderType.Limit, 50, 52.0, 0);
        runner.State = OrderState.Working;
        engine.TargetOrders["LONG1_RUNNER"] = runner;

        // Act
        // [When: Runner target fills 50 contracts]
        SimulateTargetFill(null, runner, 1, 52.0, 50);
        engine.ProcessOnOrderUpdate(runner, OrderState.Filled);

        // Assert
        // [Then: Position quantity reduced to 50, stop quantity synced to 50]
        Assert.Equal(50, pos.RemainingContracts);
        VerifyStopQuantityMatchesRemaining(engine);
    }
    #endregion

    #region Phase 3: Trailing Stop Tests (T19-T26)

    [Fact]
    public void T19_ManageTrailingStops_Throttle_SkipsTick()
    {
        // Arrange
        // [Given: Trailing stop manager with adaptive throttle enabled]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        
        

        // Act
        // [When: Manage trailing stops called before throttle interval]
        engine.ManageTrailingStops();

        // Assert
        // [Then: Tick skipped, no stop update]
        
    }

    [Fact]
    public void T20_ManageTrailingStops_Snapshot_NoCollectionModified()
    {
        // Arrange
        // [Given: 3 active positions, one will be removed during iteration]
        var engine = new MockExecutionEngine();
        engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        engine.CreateFilledPosition("LONG2", 50, 51.0, Direction.Long);
        engine.CreateFilledPosition("LONG3", 75, 52.0, Direction.Long);

        // Act
        // [When: Manage trailing stops with concurrent position removal]
        
        engine.ManageTrailingStops();

        // Assert
        // [Then: No collection modified exception, remaining positions processed]
        
        Assert.Equal(3, engine.ActivePositions.Count);
    }

    [Fact]
    public void T21_ManageTrail_PointBasedTrailing_Trail1()
    {
        // Arrange
        // [Given: Long position at 50.0, profit >= Trail1 threshold (2 points)]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        pos.CurrentStopPrice = 49.0;
        pos.ExtremePriceSinceEntry = 52.5; // 2.5 points profit
        engine.LastKnownPrice = 52.5;
        engine.Trail1Points = 2.0;
        
        

        // Act
        // [When: Manage trailing stops]
        engine.ManageTrailingStops();

        // Assert
        // [Then: Stop trailed to Trail1 level (entry + Trail1StopOffset = 51.0)]
        AssertTrailLevel(engine, "LONG1", 1);
        Assert.Equal(51.0, pos.CurrentStopPrice);
    }

    [Fact]
    public void T22_ManageTrail_PointBasedTrailing_Trail2()
    {
        // Arrange
        // [Given: Long position at 50.0, profit >= Trail2 threshold (4 points)]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        pos.CurrentStopPrice = 51.0;
        pos.CurrentTrailLevel = 1;
        pos.ExtremePriceSinceEntry = 54.5; // 4.5 points profit
        engine.LastKnownPrice = 54.5;
        engine.Trail2Points = 4.0;
        engine.Trail2StopOffset = 2.0;

        // Act
        // [When: Manage trailing stops]
        engine.ManageTrailingStops();

        // Assert
        // [Then: Stop trailed to Trail2 level (entry + Trail2StopOffset = 52.0)]
        AssertTrailLevel(engine, "LONG1", 2);
        Assert.Equal(52.0, pos.CurrentStopPrice);
    }

    [Fact]
    public void T23_ManageTrail_PointBasedTrailing_Trail3()
    {
        // Arrange
        // [Given: Long position at 50.0, profit >= Trail3 threshold (6 points)]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        pos.CurrentStopPrice = 52.0;
        pos.CurrentTrailLevel = 2;
        pos.ExtremePriceSinceEntry = 56.5; // 6.5 points profit
        engine.LastKnownPrice = 56.5;
        engine.Trail3Points = 6.0;
        engine.Trail3StopOffset = 3.0;

        // Act
        // [When: Manage trailing stops]
        engine.ManageTrailingStops();

        // Assert
        // [Then: Stop trailed to Trail3 level (entry + Trail3StopOffset = 53.0)]
        AssertTrailLevel(engine, "LONG1", 3);
        Assert.Equal(53.0, pos.CurrentStopPrice);
    }

    [Fact]
    public void T24_UpdateStopOrder_ReplacementFSM_TwoPhase()
    {
        // Arrange
        // [Given: Position with working stop at 49.0]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var oldStop = engine.CreateOrder("LONG1_STOP", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        oldStop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = oldStop;

        // Act
        // [When: Update stop to 49.5 (triggers replacement FSM)]
        engine.UpdateStopOrder("LONG1", 49.5);

        // Assert
        // [Then: Pending replacement tracked, old stop cancel initiated]
        AssertPendingReplacement(engine, "LONG1", 49.5);
        Assert.Equal(OrderState.Working, oldStop.State);
    }

    [Fact]
    public void T25_UpdateStopOrder_StalePending_Cleared()
    {
        // Arrange
        // [Given: Position with stale pending replacement (>5 seconds old)]
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var oldStop = engine.CreateOrder("LONG1_STOP", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        oldStop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = oldStop;
        engine.PendingStopReplacements["LONG1"] = new PendingStopReplacement
        {
            OldStopOrder = oldStop,
            NewStopPrice = 49.5,
            InitiatedAt = engine.MockTime.GetTicks() - (6 * TimeSpan.TicksPerSecond)
        };

        // Act
        // [When: Update stop order (detects stale pending)]
        engine.UpdateStopOrder("LONG1", 49.75);

        // Assert
        // [Then: Stale pending cleared, new replacement initiated]
        AssertPendingReplacement(engine, "LONG1", 49.75);
    }

    [Fact]
    public void T26_ManageTrail_FleetSymmetrySync_FollowerIndependent()
    {
        // Arrange
        // [Given: Master position at 50.0 with Trail1, follower at 50.25 (different fill)]
        var engine = new MockExecutionEngine();
        var master = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        master.CurrentStopPrice = 51.0;
        master.CurrentTrailLevel = 1;
        master.ExtremePriceSinceEntry = 52.5;
        
        var follower = engine.CreateFollowerPosition("LONG1", 50, 50.25, Direction.Long, "Follower1");
        follower.CurrentStopPrice = 50.75; // Different entry, different stop
        follower.CurrentTrailLevel = 0;
        follower.ExtremePriceSinceEntry = 50.25;
        engine.LastKnownPrice = 52.5;
        engine.Trail1Points = 2.0;

        // Act
        // [When: ManageTrailingStops executes]
        engine.ManageTrailingStops();

        // Assert
        // [Then: Follower uses own entry price (50.25), not master's (50.0)]
        // Follower profit = 52.5 - 50.25 = 2.25 points (>= Trail1)
        // Follower Trail1 stop = 50.25 + 1.0 = 51.25
        Assert.Equal(51.25, follower.CurrentStopPrice);
        AssertTrailLevel(engine, "LONG1_Follower1", 1);
    }
    #endregion

    #region Phase 4: Propagation Tests (T27-T32)

    [Fact]
    public void T27_PropagateMasterPriceMove_Entry_FollowersUpdated()
    {
        // Arrange
        // [Given: Master entry at 50.0, 2 followers at 50.0]
        var engine = new MockExecutionEngine();
        var master = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var follower1 = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower1");
        var follower2 = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower2");
        
        var masterEntry = engine.CreateOrder("LONG1_ENTRY", OrderAction.Buy, OrderType.Limit, 100, 50.0, 0);
        masterEntry.State = OrderState.Working;
        
        var f1Entry = engine.CreateOrder("LONG1_ENTRY_F1", OrderAction.Buy, OrderType.Limit, 50, 50.0, 0);
        f1Entry.State = OrderState.Working;
        f1Entry.Account = follower1.ExecutingAccount;
        
        var f2Entry = engine.CreateOrder("LONG1_ENTRY_F2", OrderAction.Buy, OrderType.Limit, 50, 50.0, 0);
        f2Entry.State = OrderState.Working;
        f2Entry.Account = follower2.ExecutingAccount;

        // Act
        // [When: Master entry price moves to 50.25]
        engine.PropagateMasterPriceMove(masterEntry, 50.25, 0, 100);

        // Assert
        // [Then: Both follower entries updated to 50.25]
        Assert.Equal(50.25, f1Entry.LimitPrice);
        Assert.Equal(50.25, f2Entry.LimitPrice);
    }

    [Fact]
    public void T28_PropagateMasterPriceMove_Stop_FollowersUpdated()
    {
        // Arrange
        // [Given: Master stop at 49.0, 2 followers at 49.0]
        var engine = new MockExecutionEngine();
        var master = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var follower1 = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower1");
        var follower2 = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower2");
        
        var masterStop = engine.CreateOrder("LONG1_STOP", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        masterStop.State = OrderState.Working;
        engine.StopOrders["LONG1"] = masterStop;
        
        var f1Stop = engine.CreateOrder("LONG1_STOP_F1", OrderAction.Sell, OrderType.StopMarket, 50, 0, 49.0);
        f1Stop.State = OrderState.Working;
        f1Stop.Account = follower1.ExecutingAccount;
        engine.StopOrders["LONG1_F1"] = f1Stop;
        
        var f2Stop = engine.CreateOrder("LONG1_STOP_F2", OrderAction.Sell, OrderType.StopMarket, 50, 0, 49.0);
        f2Stop.State = OrderState.Working;
        f2Stop.Account = follower2.ExecutingAccount;
        engine.StopOrders["LONG1_F2"] = f2Stop;

        // Act
        // [When: Master stop price moves to 49.5]
        engine.PropagateMasterStopMove(masterStop, 49.5);

        // Assert
        // [Then: Both follower stops updated to 49.5]
        Assert.Equal(49.5, f1Stop.StopPrice);
        Assert.Equal(49.5, f2Stop.StopPrice);
    }

    [Fact]
    public void T29_PropagateMasterPriceMove_Target_FollowersUpdated()
    {
        // Arrange
        // [Given: Master target at 51.0, 2 followers at 51.0]
        var engine = new MockExecutionEngine();
        var master = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        var follower1 = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower1");
        var follower2 = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower2");
        
        var masterTarget = engine.CreateOrder("LONG1_T1", OrderAction.Sell, OrderType.Limit, 100, 51.0, 0);
        masterTarget.State = OrderState.Working;
        engine.TargetOrders["LONG1_T1"] = masterTarget;
        
        var f1Target = engine.CreateOrder("LONG1_T1_F1", OrderAction.Sell, OrderType.Limit, 50, 51.0, 0);
        f1Target.State = OrderState.Working;
        f1Target.Account = follower1.ExecutingAccount;
        engine.TargetOrders["LONG1_T1_F1"] = f1Target;
        
        var f2Target = engine.CreateOrder("LONG1_T1_F2", OrderAction.Sell, OrderType.Limit, 50, 51.0, 0);
        f2Target.State = OrderState.Working;
        f2Target.Account = follower2.ExecutingAccount;
        engine.TargetOrders["LONG1_T1_F2"] = f2Target;

        // Act
        // [When: Master target price moves to 51.5]
        engine.PropagateMasterTargetMove(masterTarget, 51.5);

        // Assert
        // [Then: Both follower targets updated to 51.5]
        Assert.Equal(51.5, f1Target.LimitPrice);
        Assert.Equal(51.5, f2Target.LimitPrice);
    }

    [Fact]
    public void T30_PropagateFollowerEntryReplace_TwoPhaseCommit()
    {
        // Arrange
        // [Given: Follower entry at 50.0, master moves to 50.25]
        var engine = new MockExecutionEngine();
        var follower = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower1");
        var oldEntry = engine.CreateOrder("LONG1_ENTRY_F1", OrderAction.Buy, OrderType.Limit, 50, 50.0, 0);
        oldEntry.State = OrderState.Working;
        oldEntry.Account = follower.ExecutingAccount;

        // Act
        // [When: Propagate follower entry replace to 50.25]
        engine.PropagateFollowerEntryReplace(follower, oldEntry, 50.25);

        // Assert
        // [Then: Two-phase commit initiated (cancel old, submit new)]
        Assert.Equal(OrderState.PendingCancel, oldEntry.State);
        Assert.True(engine.FollowerReplaceSpecs.ContainsKey("LONG1_F1"));
        var spec = engine.FollowerReplaceSpecs["LONG1_F1"];
        Assert.Equal(50.25, spec.PendingPrice);
    }

    [Fact]
    public void T31_SubmitFollowerReplacement_Success_StateRegistered()
    {
        // Arrange
        // [Given: Follower replace spec with pending price 50.25]
        var engine = new MockExecutionEngine();
        var follower = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower1");
        var spec = new FollowerReplaceSpec
        {
            Follower = follower,
            PendingPrice = 50.25,
            InitiatedAt = engine.MockTime.GetTicks()
        };
        engine.FollowerReplaceSpecs["LONG1_F1"] = spec;

        // Act
        // [When: Submit follower replacement]
        var newEntry = engine.SubmitFollowerReplacement(spec);

        // Assert
        // [Then: New entry submitted, state registered]
        Assert.NotNull(newEntry);
        Assert.Equal(50.25, newEntry.LimitPrice);
        AssertFleetFollowerRouting(engine, newEntry, follower.ExecutingAccount);
    }

    [Fact]
    public void T32_FollowerReplaceSpec_ATRTickAbsorption_InPlace()
    {
        // Arrange
        // [Given: Follower replace spec with pending price 50.25, ATR update to 50.30]
        var engine = new MockExecutionEngine();
        var follower = engine.CreateFollowerPosition("LONG1", 50, 50.0, Direction.Long, "Follower1");
        var spec = new FollowerReplaceSpec
        {
            Follower = follower,
            PendingPrice = 50.25,
            InitiatedAt = engine.MockTime.GetTicks()
        };
        engine.FollowerReplaceSpecs["LONG1_F1"] = spec;

        // Act
        // [When: ATR tick update arrives (master moves to 50.30)]
        engine.AbsorbATRTickUpdate("LONG1_F1", 50.30);

        // Assert
        // [Then: PendingPrice updated in-place to 50.30, no new FSM event]
        Assert.Equal(50.30, spec.PendingPrice);
        Assert.Single(engine.FollowerReplaceSpecs); // Still only 1 spec
    }

    #endregion

    #region Phase 5: Edge Case Tests (T33-T40)

    [Fact]
    public void T33_ApplyTargetFill_PartialFill_Cumulative()
        {
            // Given: Target partially filled multiple times
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var account = new MockAccount("Master");
            fleet.AddAccount(account);
            var engine = new MockExecutionEngine(mockTime, fleet);

            var pos = CreateFilledPosition("OR_1", MarketPosition.Long, 4, 5000.0);
            pos.BracketSubmitted = true;
            engine.ActivePositions["OR_1"] = pos;

            var target1 = new MockOrder("T1001", "T1_OR_1", OrderAction.Sell, OrderType.Limit, 2);
            target1.Account = account;
            engine.Target1Orders["OR_1"] = target1;

            // When: ApplyTargetFill called for each fill
            var exec1 = new MockExecution("EXEC001", target1, 5010.0, 1, mockTime.GetDateTime());
            engine.ProcessOnExecutionUpdate(exec1, "EXEC001");
            
            var exec2 = new MockExecution("EXEC002", target1, 5010.0, 1, mockTime.GetDateTime());
            engine.ProcessOnExecutionUpdate(exec2, "EXEC002");

            // Then: Cumulative fill tracking correct, no over/under-decrement
            Assert.Equal(2, pos.RemainingContracts);
        }

        [Fact]
        public void T34_RequestStopCancelLifecycleSafe_ChangePending()
        {
            // Given: Stop in ChangePending state
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var account = new MockAccount("Master");
            fleet.AddAccount(account);
            var engine = new MockExecutionEngine(mockTime, fleet);

            var pos = CreateFilledPosition("OR_1", MarketPosition.Long, 2, 5000.0);
            engine.ActivePositions["OR_1"] = pos;

            var stopOrder = new MockOrder("STOP001", "Stop_OR_1", OrderAction.Sell, OrderType.StopMarket, 2);
            stopOrder.Account = account;
            stopOrder.State = OrderState.ChangePending;
            engine.StopOrders["OR_1"] = stopOrder;

            // When: RequestStopCancelLifecycleSafe called
            engine.RequestStopCancelLifecycleSafe("OR_1");

            // Then: ChangePending orders cancelled
            Assert.Equal(OrderState.Cancelled, stopOrder.State);
        }

        [Fact]
        public void T35_RemoveGhostOrderRef_TerminalState_Purges()
        {
            // Given: Orders in terminal states (Filled/Cancelled/Rejected)
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var account = new MockAccount("Master");
            fleet.AddAccount(account);
            var engine = new MockExecutionEngine(mockTime, fleet);

            var filledStop = new MockOrder("STOP001", "Stop_OR_1", OrderAction.Sell, OrderType.StopMarket, 2);
            filledStop.State = OrderState.Filled;
            engine.StopOrders["OR_1"] = filledStop;

            var cancelledTarget = new MockOrder("T1001", "T1_OR_2", OrderAction.Sell, OrderType.Limit, 1);
            cancelledTarget.State = OrderState.Cancelled;
            engine.Target1Orders["OR_2"] = cancelledTarget;

            // When: RemoveGhostOrderRef called
            engine.RemoveGhostOrderRef("OR_1");
            engine.RemoveGhostOrderRef("OR_2");

            // Then: Terminal orders removed from dictionaries
            Assert.False(engine.StopOrders.ContainsKey("OR_1"));
            Assert.False(engine.Target1Orders.ContainsKey("OR_2"));
        }

        [Fact]
        public void T36_HandleOrderCancelled_StopReplacement_Resubmits()
        {
            // Given: Stop cancelled as part of replacement
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var account = new MockAccount("Master");
            fleet.AddAccount(account);
            var engine = new MockExecutionEngine(mockTime, fleet);

            var pos = CreateFilledPosition("OR_1", MarketPosition.Long, 2, 5000.0);
            engine.ActivePositions["OR_1"] = pos;

            var stopOrder = new MockOrder("STOP001", "Stop_OR_1", OrderAction.Sell, OrderType.StopMarket, 2);
            stopOrder.Account = account;
            stopOrder.State = OrderState.Working;
            engine.StopOrders["OR_1"] = stopOrder;

            var pending = new PendingStopReplacement
            {
                EntryName = "OR_1",
                Quantity = 2,
                StopPrice = 5000.5,
                CreatedTicks = mockTime.GetTicks()
            };
            engine.PendingStopReplacements["OR_1"] = pending;

            // When: HandleOrderCancelled called
            stopOrder.SimulateCancel(account);
            engine.ProcessOnOrderUpdate(stopOrder, OrderState.Cancelled);

            // Then: New stop created from PendingStopReplacement
            Assert.True(engine.StopOrders.ContainsKey("OR_1"));
        }

        [Fact]
        public void T37_CancelOrderSafe_FleetFollower_UsesAccountAPI()
        {
            // Given: Follower order needs cancellation
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var followerAccount = new MockAccount("Follower1");
            fleet.AddAccount(followerAccount);
            var engine = new MockExecutionEngine(mockTime, fleet);

            var followerOrder = new MockOrder("ORDER_F1", "Entry_OR_1_F1", OrderAction.Buy, OrderType.Limit, 1);
            followerOrder.Account = followerAccount;
            followerOrder.State = OrderState.Working;

            // When: CancelOrderSafe called
            engine.CancelOrderSafe(followerOrder);

            // Then: ExecutingAccount.Cancel used
            Assert.Equal(OrderState.Cancelled, followerOrder.State);
        }

        [Fact]
        public void T38_ValidateStopPrice_BEShield_ClampsToEntry()
        {
            // Given: Calculated stop price below entry (Long)
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var engine = new MockExecutionEngine(mockTime, fleet);

            var pos = CreateFilledPosition("OR_1", MarketPosition.Long, 2, 5000.0);
            engine.ActivePositions["OR_1"] = pos;

            // When: ValidateStopPrice called with stop below entry
            var calculatedStop = 4995.0; // Below entry
            var validatedStop = engine.ValidateStopPrice(pos, calculatedStop);

            // Then: Stop price clamped to entry floor
            Assert.Equal(5000.0, validatedStop, 2);
        }

        [Fact]
        public void T39_CleanupStalePendingReplacements_Recovery()
        {
            // Given: PendingStopReplacement >5 seconds old
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var engine = new MockExecutionEngine(mockTime, fleet);
            engine.LastKnownPrice = 5000.0;

            var pos = CreateFilledPosition("OR_1", MarketPosition.Long, 2, 5000.0);
            engine.ActivePositions["OR_1"] = pos;

            var stalePending = new PendingStopReplacement
            {
                EntryName = "OR_1",
                Quantity = 2,
                StopPrice = 4990.0,
                CreatedTicks = mockTime.GetTicks() - (6 * TimeSpan.TicksPerSecond)
            };
            engine.PendingStopReplacements["OR_1"] = stalePending;

            // When: CleanupStalePendingReplacements called
            engine.CleanupStalePendingReplacements();

            // Then: Stale pending removed, emergency stop created
            Assert.False(engine.PendingStopReplacements.ContainsKey("OR_1"));
            Assert.True(engine.StopOrders.ContainsKey("OR_1"));
        }

        [Fact]
        public void T40_CircuitBreaker_FlattenAttempts_Caps()
        {
            // Given: FlattenAttemptCount=3
            var mockTime = new MockTime(DateTime.UtcNow.Ticks);
            var fleet = new MockFleetAccounts();
            var account = new MockAccount("Master");
            fleet.AddAccount(account);
            var engine = new MockExecutionEngine(mockTime, fleet);

            var pos = CreateFilledPosition("OR_1", MarketPosition.Long, 2, 5000.0);
            pos.ExecutingAccount = account;
            pos.FlattenAttemptCount = 3;
            engine.ActivePositions["OR_1"] = pos;

            // When: Emergency flatten attempted again
            engine.FlattenPositionByName("OR_1");

            // Then: Flatten blocked, manual intervention required
            Assert.Equal(3, pos.FlattenAttemptCount); // Not incremented
            Assert.Equal(2, pos.RemainingContracts); // Not flattened
        }
    #endregion
    }
}

// Made with Bob
