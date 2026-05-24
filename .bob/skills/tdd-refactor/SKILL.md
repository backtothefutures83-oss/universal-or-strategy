---
name: tdd-refactor
description: Clean up implementation while keeping tests green (TDD REFACTOR phase). Use after TDD GREEN phase completes.
user-invocable: true
disable-model-invocation: false
---

# TDD REFACTOR Phase - Clean Up Implementation

## Purpose
Improve code quality while keeping all tests passing. Focus on readability, maintainability, and V12 DNA compliance.

## When to Use
- After TDD GREEN phase (test is passing)
- Code works but needs cleanup
- Ready to improve without changing behavior

## Protocol

### Step 1: Verify Tests Pass
```powershell
dotnet test
# Expected: All tests PASS
```

### Step 2: Run Complexity Audit
```powershell
python scripts/complexity_audit.py
# Target: All methods CYC < 20
```

### Step 3: Refactor Code
Focus areas:
- Extract methods if CYC > 15
- Improve naming (PascalCase, descriptive)
- Remove duplication
- Simplify logic

### Step 4: Run Formatters
```powershell
powershell -File .\scripts\format_all_csharp.ps1
```

### Step 5: Verify Tests Still Pass
```powershell
dotnet test
# Expected: All tests PASS (no regressions)
```

### Step 6: Run Full DNA Audit
```powershell
# Complexity
python scripts/complexity_audit.py

# Semgrep (V12 DNA patterns)
powershell -File .\scripts\run_semgrep.ps1

# Deploy-sync
powershell -File .\deploy-sync.ps1
```

### Step 7: Commit Refactoring (if changes made)
```powershell
git add src/
git commit -m "[TDD-REFACTOR] <feature>: Clean up implementation"
```

## V12 DNA Targets

- **CYC:** < 20 per method (< 15 preferred)
- **LOC:** >= 15 per extracted method
- **Naming:** PascalCase verb-noun (Handle..., Process..., Validate...)
- **Lock-free:** Zero `lock()` statements
- **ASCII:** Zero non-ASCII characters

## V12-Specific Examples

### Example 1: FSM State Transition (Refactored)
```csharp
// Before (GREEN phase)
public void Enqueue(FSMEvent evt)
{
    if (evt == FSMEvent.OrderFilled)
        _currentState = FSMState.PositionActive;
}

// After (REFACTOR phase)
public void Enqueue(FSMEvent evt)
{
    var nextState = _transitionTable[(_currentState, evt)];
    if (nextState != FSMState.Invalid)
        _currentState = nextState;
}
```

### Example 2: Lock-Free Queue (Refactored)
```csharp
// Before (GREEN phase)
public void Enqueue(T item)
{
    var slot = Interlocked.Increment(ref _writeIndex);
    _buffer[slot % _capacity] = item;
}

// After (REFACTOR phase)
public void Enqueue(T item)
{
    var slot = Interlocked.Increment(ref _writeIndex);
    var index = slot & _mask; // Faster than modulo
    Volatile.Write(ref _buffer[index], item);
}
```

### Example 3: RMA Proximity (Refactored)
```csharp
// Before (GREEN phase)
public bool CheckProximity(double currentPrice, double rmaValue)
{
    return Math.Abs(currentPrice - rmaValue) <= _threshold;
}

// After (REFACTOR phase)
public bool CheckProximity(double currentPrice, double rmaValue)
{
    var distance = Math.Abs(currentPrice - rmaValue);
    return distance <= _threshold;
}
```

## Success Criteria
- [ ] All tests still pass
- [ ] CYC < 20 for all methods
- [ ] Code is more readable/maintainable
- [ ] DNA audits pass
- [ ] Refactoring committed (if changes made)

## Next Step
TDD cycle complete. Feature is implemented, tested, and clean.