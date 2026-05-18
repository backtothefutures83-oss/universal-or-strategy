# V12 Lock-Free FSM Testing Pattern

## Skill Metadata
- **Skill Name**: V12 Lock-Free FSM Testing Pattern
- **Category**: Testing Infrastructure
- **V12 DNA Compliance**: ✅ Lock-free, ASCII-only, Actor pattern, Atomic primitives
- **Created**: 2026-05-17
- **Reference Implementation**: [`tests/SymmetryFsmIntegrationTests.cs`](../../tests/SymmetryFsmIntegrationTests.cs:1)

## Purpose

Reusable pattern for building deterministic, lock-free test infrastructure for V12 Finite State Machines (FSMs). This skill memorializes the patterns used in the Symmetry FSM Testing Epic (Ticket 01) to enable consistent, V12-compliant test infrastructure across all future FSM implementations.

## Core Patterns

### 1. Atomic State Packing (64-bit)

**Pattern**: Pack FSM state + flags + generation counter into a single 64-bit field for atomic updates.

**Layout**: `[State: 8 bits][Pending: 1 bit][Generation: 55 bits]`

**Implementation** (lines 98-118):

```csharp
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
```

**Key Points**:
- Single 64-bit field enables atomic read/write via `Interlocked.Read()` and `Interlocked.CompareExchange()`
- Generation counter increments on every transition (detects stale updates)
- Pending flag tracks in-flight operations without separate lock
- State enum fits in 8 bits (max 256 states)

### 2. CAS Transition Loop

**Pattern**: Atomic state transitions using Compare-And-Swap (CAS) with validation.

**Implementation** (lines 187-204):

```csharp
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
```

**Key Points**:
- **Read-Validate-CAS loop**: Read current state, validate transition, attempt atomic swap
- **Generation increment**: `gen + 1` on every successful transition
- **Early exit**: Invalid transitions return `false` immediately (no retry)
- **CAS retry**: Loop continues until successful swap (handles concurrent updates)
- **No locks**: Zero `lock()` statements, pure atomic primitives

### 3. State Validation Helper

**Pattern**: Centralized transition validation using pattern matching.

**Implementation** (lines 209-228):

```csharp
private bool IsValidTransition(FollowerBracketState from, FollowerBracketState to)
{
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

**Key Points**:
- **Explicit whitelist**: Only valid transitions return `true`
- **Pattern matching**: C# 8.0+ tuple patterns for readability
- **Fail-safe default**: Unknown transitions rejected by default (`_ => false`)
- **Testable**: Easy to unit test transition rules independently

### 4. Actor Mailbox Pattern

**Pattern**: Single-threaded consumer with CAS flag protection for event processing.

**Implementation** (lines 315-333):

```csharp
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

**Key Points**:
- **CAS flag**: `_drainingFlag` prevents concurrent draining (single consumer)
- **ConcurrentQueue**: Lock-free mailbox for multi-producer, single-consumer
- **Batch limit**: `MAX_PER_DRAIN` prevents starvation (100 events per drain)
- **Finally block**: Ensures flag reset even on exception
- **No locks**: Pure atomic primitives + concurrent collection

**Mailbox Structure** (lines 290-295):

```csharp
private readonly ConcurrentQueue<AccountEvent> _mailbox;
private int _drainingFlag = 0;
private const int MAX_PER_DRAIN = 100;

public void EnqueueEvent(AccountEvent evt) => _mailbox.Enqueue(evt);
```

### 5. 3-Tier Event Resolution

**Pattern**: Hierarchical FSM lookup with backfill optimization.

**Implementation** (lines 383-414):

```csharp
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
```

**Key Points**:
- **Tier 1 (O(1))**: Direct OrderId → FSM lookup via `ConcurrentDictionary`
- **Tier 2 (O(1))**: SignalName parsing when OrderId not cached
- **Tier 3 (O(N))**: Full scan as last resort (rare after warm-up)
- **Backfill**: Cache misses populate `_orderIdMap` for future O(1) lookups
- **Thread-safe**: All storage uses `ConcurrentDictionary`

**OrderId Map Helper** (lines 251-282):

```csharp
private class OrderIdToFsmMap
{
    private ConcurrentDictionary<string, (string EntryName, long Generation)> _map;

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
}
```

### 6. MockTime Pattern

**Pattern**: Deterministic time simulation without `Thread.Sleep()`.

**Implementation** (lines 58-67):

```csharp
private class MockTime
{
    private long _ticks;

    public MockTime(long initialTicks) => _ticks = initialTicks;
    public long GetTicks() => _ticks;
    public void Advance(long deltaTicks) => _ticks += deltaTicks;
    public void AdvanceSeconds(double seconds) =>
        _ticks += (long)(seconds * TimeSpan.TicksPerSecond);
}
```

**Key Points**:
- **Deterministic**: No wall-clock dependency, fully controllable
- **Tick-based**: Uses `TimeSpan.TicksPerSecond` for precision
- **Manual advance**: Test controls time progression explicitly
- **No sleep**: Zero `Thread.Sleep()` calls in tests

**Usage Pattern**:

```csharp
var time = new MockTime(1000000L);
// ... perform operations ...
time.AdvanceSeconds(5.0); // Simulate 5 seconds passing
// ... verify time-dependent behavior ...
```

## V12 DNA Compliance Checklist

### Lock-Free Requirements
- [ ] **Zero `lock()` statements** in FSM or test infrastructure
- [ ] All state updates via `Interlocked.CompareExchange()` or `Interlocked.Exchange()`
- [ ] Read operations via `Interlocked.Read()` or `Volatile.Read()`
- [ ] CAS loops for atomic transitions with validation

### Concurrent Collections
- [ ] `ConcurrentQueue<T>` for mailbox (multi-producer, single-consumer)
- [ ] `ConcurrentDictionary<K,V>` for FSM storage and OrderId mapping
- [ ] No `Dictionary<K,V>` or `List<T>` without external synchronization

### ASCII-Only Compliance
- [ ] All string literals use ASCII characters only
- [ ] No Unicode, emoji, or curly quotes in test data
- [ ] Signal names, OrderIds, and error messages are ASCII

### Actor Pattern
- [ ] Single-threaded consumer with CAS flag protection
- [ ] Batch processing limit (`MAX_PER_DRAIN`)
- [ ] `finally` block ensures flag reset

### Deterministic Testing
- [ ] MockTime pattern for time simulation
- [ ] No `Thread.Sleep()` or wall-clock dependencies
- [ ] Explicit time advancement in tests

## Usage Guidelines

### When to Use This Pattern

**Ideal for**:
- FSM testing (state machines with complex transitions)
- State machine mocking (simulating production FSM behavior)
- Event-driven systems (order callbacks, market data, etc.)
- Multi-threaded scenarios requiring deterministic testing

**Not suitable for**:
- Simple stateless functions (overkill)
- Single-threaded code with no concurrency (unnecessary complexity)
- UI testing (different patterns apply)

### How to Adapt for Different FSM Types

1. **Define State Enum**: Create enum matching your FSM states (max 256 states)
2. **Implement Validation**: Write `IsValidTransition()` with your state machine rules
3. **Pack State**: Use `FsmPackedState` pattern with your state enum
4. **Add Mailbox**: Use `ConcurrentQueue<YourEvent>` for event processing
5. **Implement Resolution**: Adapt 3-tier resolution for your event types

### Common Pitfalls to Avoid

#### Pitfall 1: Forgetting Generation Increment
```csharp
// WRONG: Reusing same generation
newPacked = FsmPackedState.Pack((byte)newState, setPending, gen);

// CORRECT: Increment generation on every transition
newPacked = FsmPackedState.Pack((byte)newState, setPending, gen + 1);
```

#### Pitfall 2: Missing CAS Loop
```csharp
// WRONG: Single CAS attempt (race condition)
var current = Interlocked.Read(ref _packedState);
var newPacked = FsmPackedState.Pack(...);
Interlocked.CompareExchange(ref _packedState, newPacked, current);

// CORRECT: Loop until successful
do {
    current = Interlocked.Read(ref _packedState);
    // ... validation ...
    newPacked = FsmPackedState.Pack(...);
} while (Interlocked.CompareExchange(ref _packedState, newPacked, current) != current);
```

#### Pitfall 3: Forgetting Finally Block
```csharp
// WRONG: Flag not reset on exception
if (Interlocked.CompareExchange(ref _drainingFlag, 1, 0) != 0) return;
ProcessEvents(); // May throw
Interlocked.Exchange(ref _drainingFlag, 0);

// CORRECT: Finally ensures reset
if (Interlocked.CompareExchange(ref _drainingFlag, 1, 0) != 0) return;
try {
    ProcessEvents();
} finally {
    Interlocked.Exchange(ref _drainingFlag, 0);
}
```

#### Pitfall 4: Using Regular Collections
```csharp
// WRONG: Dictionary requires external lock
private Dictionary<string, FSM> _brackets = new Dictionary<string, FSM>();

// CORRECT: ConcurrentDictionary is lock-free
private ConcurrentDictionary<string, FSM> _brackets = new ConcurrentDictionary<string, FSM>();
```

#### Pitfall 5: Backfill Without Generation
```csharp
// WRONG: Stale mapping may persist
_orderIdMap.TryAdd(evt.OrderId, entryName); // No generation tracking

// CORRECT: Store generation for staleness detection
_orderIdMap.TryAdd(evt.OrderId, entryName, fsm.Generation);
```

## Reference Implementation

**File**: [`tests/SymmetryFsmIntegrationTests.cs`](../../tests/SymmetryFsmIntegrationTests.cs:1)

**Key Line Numbers**:
- **Atomic State Packing**: Lines 98-118 (`FsmPackedState` struct)
- **CAS Transition Loop**: Lines 187-204 (`TryTransition()` method)
- **State Validation**: Lines 209-228 (`IsValidTransition()` method)
- **Actor Mailbox**: Lines 315-333 (`DrainMailbox()` method)
- **3-Tier Resolution**: Lines 383-414 (`ResolveFsmFromEvent()` method)
- **MockTime Pattern**: Lines 58-67 (`MockTime` class)
- **OrderId Mapping**: Lines 251-282 (`OrderIdToFsmMap` class)
- **Event Processing**: Lines 338-363 (`ProcessBracketEvent()` method)

## Code Examples

### Example 1: Basic FSM Setup

```csharp
// Create MockTime
var time = new MockTime(DateTime.UtcNow.Ticks);

// Create FSM harness
var mockFsm = new MockSymmetryFsm(time);

// Create FSM instance
var fsm = new MockFollowerBracketFSM
{
    AccountName = "Sim101",
    EntryName = "Fleet_Apex_1",
    State = FollowerBracketState.None,
    RemainingContracts = 2
};

// Register FSM
mockFsm.AddBracket("Fleet_Apex_1", fsm);
```

### Example 2: Event Processing

```csharp
// Enqueue event (thread-safe)
var evt = new AccountEvent
{
    AccountAlias = "Sim101",
    OrderId = "ORD123",
    NewState = OrderState.Accepted,
    SignalName = "Entry_Fleet_Apex_1",
    TimestampTicks = time.GetTicks()
};
mockFsm.EnqueueEvent(evt);

// Drain mailbox (single-threaded consumer)
mockFsm.DrainMailbox();

// Verify state transition
Assert.Equal(FollowerBracketState.Accepted, fsm.State);
```

### Example 3: Time Advancement

```csharp
// Initial state
var initialTicks = time.GetTicks();

// Simulate 5 seconds passing
time.AdvanceSeconds(5.0);

// Verify time advanced
Assert.Equal(initialTicks + (5 * TimeSpan.TicksPerSecond), time.GetTicks());
```

## Post-Use Audit

**Skill Status**: No gaps identified

This skill document accurately captures the lock-free FSM testing patterns from the Symmetry FSM Testing Epic (Ticket 01). All patterns are V12 DNA compliant and ready for reuse in future FSM test infrastructure.

---

**Made with Bob**