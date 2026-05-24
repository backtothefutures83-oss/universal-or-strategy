---
name: tdd-green
description: Implement MINIMAL code to pass the failing test (TDD GREEN phase). Use after TDD RED phase completes.
user-invocable: true
disable-model-invocation: false
---

# TDD GREEN Phase - Implement to Pass Test

## Purpose
Write the MINIMAL code needed to make the failing test pass. No more, no less.

## When to Use
- After TDD RED phase (failing test exists)
- Test is currently FAILING (verified)
- Ready to implement the feature

## Protocol

### Step 1: Verify Test is Failing
```powershell
dotnet test --filter <TestName>
# Must show: EXIT 1 (FAIL)
```

### Step 2: Implement Minimal Code
Write ONLY enough code to pass the test. No:
- Extra features
- Premature optimization
- Speculative abstractions

### Step 3: Verify Test Passes
```powershell
dotnet test --filter <TestName>
# Expected: EXIT 0 (PASS)
```

### Step 4: Run DNA Audits
```powershell
# ASCII compliance
grep -Prn "[^\x00-\x7F]" src/
# Expected: 0 matches

# Lock-free compliance
grep -r "lock(" src/
# Expected: 0 matches

# Deploy-sync (if src/ changed)
powershell -File .\deploy-sync.ps1
# Expected: ASCII gate PASS
```

### Step 5: Commit Implementation
```powershell
git add src/ tests/
git commit -m "[TDD-GREEN] <feature>: Implement to pass test"
```

## V12 DNA Checklist

- [ ] Zero new `lock()` statements
- [ ] Zero non-ASCII characters
- [ ] All state mutations use FSM/Enqueue pattern
- [ ] No logic drift (pure structural movement)
- [ ] deploy-sync.ps1 passes

## V12-Specific Examples

### Example 1: FSM State Transition (Minimal)
```csharp
public void Enqueue(FSMEvent evt)
{
    if (evt == FSMEvent.OrderFilled)
        _currentState = FSMState.PositionActive;
}
```

### Example 2: Lock-Free Queue (Minimal)
```csharp
public void Enqueue(T item)
{
    var slot = Interlocked.Increment(ref _writeIndex);
    _buffer[slot % _capacity] = item;
}
```

### Example 3: RMA Proximity (Minimal)
```csharp
public bool CheckProximity(double currentPrice, double rmaValue)
{
    return Math.Abs(currentPrice - rmaValue) <= _threshold;
}
```

## Success Criteria
- [ ] Test now PASSES (EXIT 0)
- [ ] Minimal code written (no extras)
- [ ] DNA audits pass (lock-free, ASCII, deploy-sync)
- [ ] Implementation committed

## Next Step
After GREEN phase complete, proceed to TDD REFACTOR phase (clean up code).