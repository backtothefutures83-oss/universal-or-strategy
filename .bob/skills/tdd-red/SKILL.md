---
name: tdd-red
description: Write failing test BEFORE implementation (TDD RED phase). Use when starting any new feature or bug fix that requires code changes.
user-invocable: true
disable-model-invocation: false
---

# TDD RED Phase - Write Failing Test

## Purpose
Write a failing test BEFORE writing any implementation code. This ensures the test actually validates the feature.

## When to Use
- Starting a new feature
- Fixing a bug
- Refactoring existing code
- ANY src/ file modification

## Protocol

### Step 1: Create Test File
Location: `tests/V12_Performance.Tests/`
Naming: `<Feature>Tests.cs`

### Step 2: Write Test Method
```csharp
[Fact]
public void <Feature>_<Scenario>_<ExpectedResult>()
{
    // Arrange
    var sut = new SystemUnderTest();
    
    // Act
    var result = sut.MethodToTest();
    
    // Assert
    Assert.Equal(expected, result);
}
```

### Step 3: Verify Test FAILS
```powershell
dotnet test --filter <TestName>
# Expected: EXIT 1 (FAIL)
```

**CRITICAL:** If test PASSES before implementation, it's not validating the feature. Fix the test.

### Step 4: Commit Test Only
```powershell
git add tests/
git commit -m "[TDD-RED] <feature>: Add failing test"
```

## V12-Specific Examples

### Example 1: FSM State Transition
```csharp
[Fact]
public void Enqueue_OrderFilled_TransitionsToPositionActive()
{
    var fsm = new FSMActor();
    fsm.Enqueue(FSMEvent.OrderFilled);
    Assert.Equal(FSMState.PositionActive, fsm.CurrentState);
}
```

### Example 2: Lock-Free Queue
```csharp
[Fact]
public void Enqueue_ConcurrentWrites_NoDataLoss()
{
    var queue = new LockFreeQueue<int>();
    Parallel.For(0, 1000, i => queue.Enqueue(i));
    Assert.Equal(1000, queue.Count);
}
```

### Example 3: RMA Proximity Check
```csharp
[Fact]
public void CheckRMAProximity_WithinThreshold_ReturnsTrue()
{
    var rma = new RMAProximityMonitor(threshold: 5);
    var result = rma.CheckProximity(currentPrice: 100, rmaValue: 103);
    Assert.True(result);
}
```

## Success Criteria
- [ ] Test file created in tests/
- [ ] Test method follows naming convention
- [ ] Test FAILS when run (EXIT 1)
- [ ] Test committed separately (no src/ changes)

## Next Step
After RED phase complete, proceed to TDD GREEN phase (implement to pass test).