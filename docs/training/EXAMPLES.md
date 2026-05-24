# TDD Examples - V12 DNA Patterns

## Example 1: FSM State Transition

### RED: Write Failing Test
```csharp
// tests/V12_Performance.Tests/Core/FSMActorTests.cs
using Xunit;

public class FSMActorTests
{
    [Fact]
    public void Enqueue_OrderFilled_TransitionsToPositionActive()
    {
        // Arrange
        var fsm = new FSMActor();
        fsm.Enqueue(FSMEvent.OrderFilled);
        
        // Act
        var state = fsm.CurrentState;
        
        // Assert
        Assert.Equal(FSMState.PositionActive, state);
    }
}
```

**Run:** `dotnet test --filter "FSMActorTests"`
**Expected:** FAIL (FSMActor doesn't exist yet)

### GREEN: Implement Minimal Code
```csharp
// src/V12_002.FSM.cs
public class FSMActor
{
    private FSMState _currentState = FSMState.Idle;
    
    public FSMState CurrentState => _currentState;
    
    public void Enqueue(FSMEvent evt)
    {
        if (evt == FSMEvent.OrderFilled)
            _currentState = FSMState.PositionActive;
    }
}

public enum FSMState { Idle, PositionActive }
public enum FSMEvent { OrderFilled }
```

**Run:** `dotnet test --filter "FSMActorTests"`
**Expected:** PASS

### REFACTOR: Clean Up Code
```csharp
// src/V12_002.FSM.cs
public class FSMActor
{
    private FSMState _currentState = FSMState.Idle;
    private readonly Dictionary<(FSMState, FSMEvent), FSMState> _transitionTable;
    
    public FSMState CurrentState => _currentState;
    
    public FSMActor()
    {
        _transitionTable = new Dictionary<(FSMState, FSMEvent), FSMState>
        {
            { (FSMState.Idle, FSMEvent.OrderFilled), FSMState.PositionActive }
        };
    }
    
    public void Enqueue(FSMEvent evt)
    {
        if (_transitionTable.TryGetValue((_currentState, evt), out var nextState))
            _currentState = nextState;
    }
}
```

**Run:** `dotnet test`
**Expected:** All tests PASS

## Example 2: Lock-Free Queue

### RED: Write Failing Test
```csharp
// tests/V12_Performance.Tests/Core/LockFreeQueueTests.cs
using Xunit;
using System.Threading.Tasks;

public class LockFreeQueueTests
{
    [Fact]
    public void Enqueue_ConcurrentWrites_NoDataLoss()
    {
        // Arrange
        var queue = new LockFreeQueue<int>(capacity: 1024);
        
        // Act
        Parallel.For(0, 1000, i => queue.Enqueue(i));
        
        // Assert
        Assert.Equal(1000, queue.Count);
    }
}
```

**Run:** `dotnet test --filter "LockFreeQueueTests"`
**Expected:** FAIL (LockFreeQueue doesn't exist yet)

### GREEN: Implement Minimal Code
```csharp
// src/V12_002.LockFreeQueue.cs
using System.Threading;

public class LockFreeQueue<T>
{
    private readonly T[] _buffer;
    private int _writeIndex = -1;
    
    public int Count => _writeIndex + 1;
    
    public LockFreeQueue(int capacity)
    {
        _buffer = new T[capacity];
    }
    
    public void Enqueue(T item)
    {
        var slot = Interlocked.Increment(ref _writeIndex);
        _buffer[slot % _buffer.Length] = item;
    }
}
```

**Run:** `dotnet test --filter "LockFreeQueueTests"`
**Expected:** PASS

**DNA Audit:**
```powershell
grep -r "lock(" src/
# Expected: 0 matches (lock-free ✓)
```

### REFACTOR: Clean Up Code
```csharp
// src/V12_002.LockFreeQueue.cs
using System.Threading;

public class LockFreeQueue<T>
{
    private readonly T[] _buffer;
    private readonly int _mask;
    private int _writeIndex = -1;
    
    public int Count => _writeIndex + 1;
    
    public LockFreeQueue(int capacity)
    {
        _buffer = new T[capacity];
        _mask = capacity - 1; // Faster than modulo
    }
    
    public void Enqueue(T item)
    {
        var slot = Interlocked.Increment(ref _writeIndex);
        var index = slot & _mask;
        Volatile.Write(ref _buffer[index], item);
    }
}
```

**Run:** `dotnet test`
**Expected:** All tests PASS

## Example 3: Performance Test (BenchmarkDotNet)

### RED: Write Failing Benchmark
```csharp
// benchmarks/V12_Performance.Benchmarks/RMAProximityBenchmark.cs
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class RMAProximityBenchmark
{
    private RMAProximityMonitor _monitor;
    
    [GlobalSetup]
    public void Setup()
    {
        _monitor = new RMAProximityMonitor(threshold: 5);
    }
    
    [Benchmark]
    public bool CheckProximity_HotPath()
    {
        return _monitor.CheckProximity(currentPrice: 100, rmaValue: 103);
    }
}
```

**Run:** `dotnet run --project benchmarks --configuration Release`
**Expected:** Baseline established

### GREEN: Optimize for Performance
```csharp
// src/V12_002.Entries.RMA.cs
public class RMAProximityMonitor
{
    private readonly double _threshold;
    
    public RMAProximityMonitor(double threshold)
    {
        _threshold = threshold;
    }
    
    public bool CheckProximity(double currentPrice, double rmaValue)
    {
        var distance = Math.Abs(currentPrice - rmaValue);
        return distance <= _threshold;
    }
}
```

**Run:** `dotnet run --project benchmarks --configuration Release`
**Expected:** 0 B allocation, < 300μs latency

### REFACTOR: Add Performance Assertions
```csharp
// tests/V12_Performance.Tests/Core/RMAProximityTests.cs
using Xunit;
using NinjaTrader.Custom.AddOns.V12_Performance.Tests.Utilities;

public class RMAProximityTests
{
    [Fact]
    public void CheckProximity_HotPath_MeetsPerformanceTargets()
    {
        // Arrange
        var monitor = new RMAProximityMonitor(threshold: 5);
        
        // Act & Assert (Latency)
        PerformanceAssertions.AssertLatency(() =>
        {
            monitor.CheckProximity(currentPrice: 100, rmaValue: 103);
        }, maxMicroseconds: 300);
        
        // Act & Assert (Allocation)
        PerformanceAssertions.AssertZeroAllocation(() =>
        {
            monitor.CheckProximity(currentPrice: 100, rmaValue: 103);
        });
    }
}
```

**Run:** `dotnet test`
**Expected:** All tests PASS

## Example 4: Integration Test

### RED: Write Failing Integration Test
```csharp
// tests/V12_Performance.Tests/Integration/OrderLifecycleTests.cs
using Xunit;

public class OrderLifecycleTests
{
    [Fact]
    public void OrderLifecycle_CreateExecuteCleanup_CompletesSuccessfully()
    {
        // Arrange
        var strategy = new V12_002();
        var orderManager = new OrderManager();
        
        // Act
        var order = orderManager.CreateOrder(OrderAction.Buy, 1);
        orderManager.ExecuteOrder(order);
        orderManager.CleanupOrder(order);
        
        // Assert
        Assert.Equal(OrderState.Filled, order.State);
        Assert.Empty(orderManager.ActiveOrders);
    }
}
```

**Run:** `dotnet test --filter "OrderLifecycleTests"`
**Expected:** FAIL (OrderManager doesn't exist yet)

### GREEN: Implement Integration
```csharp
// src/V12_002.Orders.Management.cs
public class OrderManager
{
    private readonly List<Order> _activeOrders = new List<Order>();
    
    public IReadOnlyList<Order> ActiveOrders => _activeOrders.AsReadOnly();
    
    public Order CreateOrder(OrderAction action, int quantity)
    {
        var order = new Order { Action = action, Quantity = quantity, State = OrderState.Created };
        _activeOrders.Add(order);
        return order;
    }
    
    public void ExecuteOrder(Order order)
    {
        order.State = OrderState.Filled;
    }
    
    public void CleanupOrder(Order order)
    {
        _activeOrders.Remove(order);
    }
}

public class Order
{
    public OrderAction Action { get; set; }
    public int Quantity { get; set; }
    public OrderState State { get; set; }
}

public enum OrderAction { Buy, Sell }
public enum OrderState { Created, Filled }
```

**Run:** `dotnet test --filter "OrderLifecycleTests"`
**Expected:** PASS

### REFACTOR: Add Error Handling
```csharp
// src/V12_002.Orders.Management.cs
public class OrderManager
{
    private readonly List<Order> _activeOrders = new List<Order>();
    
    public IReadOnlyList<Order> ActiveOrders => _activeOrders.AsReadOnly();
    
    public Order CreateOrder(OrderAction action, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
            
        var order = new Order { Action = action, Quantity = quantity, State = OrderState.Created };
        _activeOrders.Add(order);
        return order;
    }
    
    public void ExecuteOrder(Order order)
    {
        if (order.State != OrderState.Created)
            throw new InvalidOperationException("Order must be in Created state");
            
        order.State = OrderState.Filled;
    }
    
    public void CleanupOrder(Order order)
    {
        if (!_activeOrders.Remove(order))
            throw new InvalidOperationException("Order not found in active orders");
    }
}
```

**Run:** `dotnet test`
**Expected:** All tests PASS

## References
- [TDD Quickstart](TDD_QUICKSTART.md)
- [Developer Guide](DEVELOPER_GUIDE.md)
- [Test Templates](../../tests/V12_Performance.Tests/Templates/)
- [Performance Assertions](../../tests/V12_Performance.Tests/Utilities/PerformanceAssertions.cs)