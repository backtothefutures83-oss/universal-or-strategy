# Ticket 01: Mock Infrastructure Setup

**Epic**: Symmetry FSM Testing  
**Phase**: Foundation  
**Priority**: P0 (Blocking)  
**Complexity**: S (Small - 2-4 hours)  
**Owner**: Bob CLI (`v12-engineer`)

---

## Objective

Create the mock infrastructure required for deterministic FSM testing. This includes MockTime, MockOrder, MockFollowerBracketFSM, and MockSymmetryFsm test harness with event builders and assertion helpers.

---

## Scope

### In Scope
- `MockTime` class for deterministic time simulation
- `MockOrder` class for broker order simulation
- `MockFollowerBracketFSM` class mirroring production FSM
- `MockSymmetryFsm` test harness with 3-tier resolution
- Event builder helper methods
- Assertion helper methods
- Test file structure and namespace setup

### Out of Scope
- Actual test scenarios (covered in Tickets 02-06)
- Integration with production code
- Performance benchmarking

---

## Implementation Steps

### Step 1: Create Test File Structure
1. Create `tests/SymmetryFsmIntegrationTests.cs`
2. Add namespace: `V12.Sima.Tests`
3. Add using statements:
   ```csharp
   using System;
   using System.Collections.Generic;
   using System.Collections.Concurrent;
   using System.Linq;
   using System.Threading;
   using Xunit;
   using NinjaTrader.Cbi;
   ```
4. Add class header with XML documentation

### Step 2: Implement MockTime
Copy pattern from `CircuitBreakerBehaviorTests.MockTime`:
- Constructor with initial ticks
- `GetTicks()` method
- `Advance(long deltaTicks)` method
- `AdvanceSeconds(double seconds)` helper

### Step 3: Implement MockOrder
Create broker order simulation:
- Properties: OrderId, SignalName, OrderAction, Quantity, State, FillPrice, FilledQuantity
- Constructor for initialization
- No NinjaTrader dependencies

### Step 4: Implement FsmPackedState Helper
Copy from `V12_002.Symmetry.BracketFSM.cs` lines 19-39:
- `Pack(byte state, bool pending, long generation)` method
- `Unpack(long value, out byte state, out bool pending, out long generation)` method
- Constants: StateShift, PendingShift, PendingMask, GenerationMask

### Step 5: Implement FollowerBracketState Enum
Copy from `V12_002.Symmetry.BracketFSM.cs` lines 46-59:
- All 11 states: None, PendingSubmit, Submitted, Accepted, Active, Replacing, Modifying, Filled, Cancelled, Rejected, Disconnected

### Step 6: Implement MockFollowerBracketFSM
Mirror production FSM structure:
- Properties: AccountName, EntryName, OcoGroupId, RemainingContracts, ReplacingCancelOrderId, LastUpdateUtc
- Atomic state field: `private long _packedState`
- State property with Interlocked access
- Generation property
- `TryTransition(FollowerBracketState newState, bool setPending)` method with complete CAS implementation
- `IsValidTransition(FollowerBracketState from, FollowerBracketState to)` helper method
- Order references: EntryOrder, StopOrder, Targets[5]

**Complete TryTransition Implementation**:
```csharp
public bool TryTransition(FollowerBracketState newState, bool setPending)
{
    long currentPacked, newPacked;
    do
    {
        currentPacked = Interlocked.Read(ref _packedState);
        FsmPackedState.Unpack(currentPacked, out byte oldState, out bool _, out long gen);
        
        // Validate transition (state machine rules)
        if (!IsValidTransition((FollowerBracketState)oldState, newState))
            return false;
            
        newPacked = FsmPackedState.Pack((byte)newState, setPending, gen + 1);
    }
    while (Interlocked.CompareExchange(ref _packedState, newPacked, currentPacked) != currentPacked);
    
    return true;
}

private bool IsValidTransition(FollowerBracketState from, FollowerBracketState to)
{
    // Valid transitions based on FSM rules
    return (from, to) switch
    {
        (FollowerBracketState.None, FollowerBracketState.PendingSubmit) => true,
        (FollowerBracketState.PendingSubmit, FollowerBracketState.Submitted) => true,
        (FollowerBracketState.Submitted, FollowerBracketState.Accepted) => true,
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
```

### Step 7: Implement AccountEvent Struct
Copy from `V12_002.Symmetry.BracketFSM.cs` lines 143-153:
- Properties: AccountAlias, OrderId, NewState, FillPrice, FilledQty, TimestampTicks, SignalName, ErrorMessage

### Step 8: Implement OrderIdToFsmMap Helper
Create simplified version for testing:
```csharp
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
```

### Step 9: Implement MockSymmetryFsm Test Harness
Core test harness with 3-tier resolution:
- Constructor with MockTime dependency
- `ConcurrentDictionary<string, MockFollowerBracketFSM> _brackets`
- `ConcurrentQueue<AccountEvent> _mailbox`
- `OrderIdToFsmMap _orderIdMap`
- `EnqueueEvent(AccountEvent evt)` method
- `DrainMailbox()` method with single-threaded consumer enforcement
- `ResolveFsm_ByOrderId(string orderId)` method (Tier 1)
- `ResolveFsm_BySignalName(string signalName, string orderId)` method (Tier 2)
- `ResolveFsm_ByScan(string accountAlias, string orderId)` method (Tier 3)
- `ResolveFsmFromEvent(AccountEvent evt)` method (3-tier router)
- `ProcessBracketEvent(AccountEvent evt)` method (state machine logic)
- `GetFsmExpectedPosition(string accountName)` method

**Complete DrainMailbox Implementation**:
```csharp
// Single-threaded consumer enforcement
private int _drainingFlag = 0;
private const int MAX_PER_DRAIN = 100;

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
```

**Complete ProcessBracketEvent Implementation**:
```csharp
// State machine logic
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
```

**Complete ResolveFsmFromEvent Implementation (3-Tier)**:
```csharp
// 3-tier resolution with backfill
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

// Helper: Parse entry name from signal name
private string ParseEntryNameFromSignal(string signalName)
{
    // Example: "Entry_Fleet_Apex_1" -> "Fleet_Apex_1"
    if (signalName.StartsWith("Entry_"))
        return signalName.Substring(6);
    if (signalName.StartsWith("Stop_"))
        return signalName.Substring(5);
    if (signalName.StartsWith("Target"))
        return signalName.Substring(signalName.IndexOf('_') + 1);
    return signalName;
}

// Helper: Check if FSM matches order
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
```

### Step 10: Implement Event Builder Helpers
```csharp
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
```

### Step 11: Implement Assertion Helpers
```csharp
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
```

### Step 12: Add Smoke Test
Create one simple test to verify infrastructure compiles:
```csharp
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
```

---

## Verification Criteria

### Compilation
- [ ] `tests/SymmetryFsmIntegrationTests.cs` compiles without errors
- [ ] No warnings related to unused variables or methods
- [ ] All using statements resolve correctly

### Infrastructure Completeness
- [ ] MockTime class implemented with all methods
- [ ] MockOrder class implemented with all properties
- [ ] MockFollowerBracketFSM class implemented with atomic state
- [ ] MockSymmetryFsm class implemented with 3-tier resolution
- [ ] All event builder helpers implemented (5 methods)
- [ ] All assertion helpers implemented (5 methods)

### Smoke Test
- [ ] Infrastructure_Smoke_Test passes
- [ ] Test execution time <100ms
- [ ] No exceptions thrown during test

### V12 DNA Compliance
- [ ] Zero `lock()` statements in mock code
- [ ] All state updates use `Interlocked` or `Volatile`
- [ ] ASCII-only string literals (no Unicode)
- [ ] ConcurrentQueue used for mailbox
- [ ] ConcurrentDictionary used for FSM storage

---

## Dependencies

### Prerequisites
- `tests/` directory exists
- Xunit test framework installed
- NinjaTrader.Cbi assembly referenced

### Blocks
- Ticket 02 (Phase 1 Tests)
- Ticket 03 (Phase 2 Tests)
- Ticket 04 (Phase 3 Tests)
- Ticket 05 (Phase 4 Tests)
- Ticket 06 (Phase 5 Tests)

---

## Estimated Complexity

**Size**: S (Small)  
**Time**: 2-4 hours  
**Risk**: Low (foundational work, no complex logic)

**Breakdown**:
- File structure setup: 15 minutes
- MockTime + MockOrder: 30 minutes
- FsmPackedState + Enum: 15 minutes
- MockFollowerBracketFSM: 45 minutes
- OrderIdToFsmMap: 20 minutes
- MockSymmetryFsm: 60 minutes
- Event builders: 30 minutes
- Assertion helpers: 20 minutes
- Smoke test: 15 minutes
- Testing and debugging: 30 minutes

---

## Notes

- This ticket is **BLOCKING** - all other tickets depend on it
- Focus on exact replication of production FSM logic
- Use same atomic primitives as production code
- Follow existing test patterns from CircuitBreaker and ReaperWatchdog tests
- No shortcuts - this is the foundation for all subsequent tests

---

**END OF TICKET 01**