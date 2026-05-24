# TDD Quickstart Guide - V12 DNA

## 5-Minute TDD Crash Course

### What is TDD?
Test-Driven Development (TDD) is a software development approach where you write tests BEFORE writing code.

### Why TDD?
- **Catch bugs early** - Tests fail immediately when code breaks
- **Better design** - Writing tests first forces you to think about API design
- **Confidence** - Refactor fearlessly knowing tests will catch regressions
- **Documentation** - Tests serve as executable documentation

### The TDD Cycle (Red-Green-Refactor)

```
RED → GREEN → REFACTOR → COMMIT
 ↑                          ↓
 └──────────────────────────┘
```

#### RED: Write Failing Test
1. Write a test that describes the desired behavior
2. Run the test - it should FAIL (no implementation yet)
3. Commit the test

#### GREEN: Implement Minimal Code
1. Write ONLY enough code to make the test pass
2. Run the test - it should PASS
3. Commit the implementation

#### REFACTOR: Clean Up Code
1. Improve code quality (naming, structure, duplication)
2. Run tests - they should still PASS
3. Commit the refactoring

## Example: RMA Proximity Check

### Step 1: RED (Write Failing Test)

```csharp
// tests/V12_Performance.Tests/Core/RMAProximityTests.cs
using Xunit;

public class RMAProximityTests
{
    [Fact]
    public void CheckProximity_WithinThreshold_ReturnsTrue()
    {
        // Arrange
        var monitor = new RMAProximityMonitor(threshold: 5);
        
        // Act
        var result = monitor.CheckProximity(currentPrice: 100, rmaValue: 103);
        
        // Assert
        Assert.True(result);
    }
}
```

**Run test:**
```powershell
dotnet test --filter "RMAProximityTests"
# Expected: FAIL (RMAProximityMonitor doesn't exist yet)
```

**Commit:**
```powershell
git add tests/
git commit -m "[TDD-RED] RMA proximity: Add failing test"
```

### Step 2: GREEN (Implement Minimal Code)

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
        return Math.Abs(currentPrice - rmaValue) <= _threshold;
    }
}
```

**Run test:**
```powershell
dotnet test --filter "RMAProximityTests"
# Expected: PASS
```

**Run DNA audits:**
```powershell
# ASCII compliance
grep -Prn "[^\x00-\x7F]" src/
# Expected: 0 matches

# Lock-free compliance
grep -r "lock(" src/
# Expected: 0 matches

# Deploy-sync
powershell -File .\deploy-sync.ps1
# Expected: ASCII gate PASS
```

**Commit:**
```powershell
git add src/ tests/
git commit -m "[TDD-GREEN] RMA proximity: Implement to pass test"
```

### Step 3: REFACTOR (Clean Up Code)

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

**Run tests:**
```powershell
dotnet test
# Expected: All tests PASS
```

**Run complexity audit:**
```powershell
python scripts/complexity_audit.py
# Expected: All methods CYC < 20
```

**Commit:**
```powershell
git add src/
git commit -m "[TDD-REFACTOR] RMA proximity: Clean up implementation"
```

## Common Mistakes

### ❌ Writing Code First
```csharp
// WRONG: Writing implementation before test
public bool CheckProximity(...) { ... }
```

### ✅ Writing Test First
```csharp
// CORRECT: Writing test before implementation
[Fact]
public void CheckProximity_WithinThreshold_ReturnsTrue() { ... }
```

### ❌ Writing Multiple Tests at Once
```csharp
// WRONG: Writing all tests upfront
[Fact] public void Test1() { ... }
[Fact] public void Test2() { ... }
[Fact] public void Test3() { ... }
```

### ✅ Writing One Test at a Time
```csharp
// CORRECT: One test, implement, refactor, repeat
[Fact] public void Test1() { ... }
// Implement Test1 → Refactor → Then write Test2
```

### ❌ Skipping Refactor Step
```csharp
// WRONG: Leaving messy code after GREEN
public bool CheckProximity(double currentPrice, double rmaValue)
{
    return Math.Abs(currentPrice - rmaValue) <= _threshold; // No intermediate variable
}
```

### ✅ Always Refactor
```csharp
// CORRECT: Clean up after GREEN
public bool CheckProximity(double currentPrice, double rmaValue)
{
    var distance = Math.Abs(currentPrice - rmaValue);
    return distance <= _threshold; // Clear intent
}
```

## V12-Specific Rules

### 1. TDD Enforcement Hook
- **Rule:** Cannot edit src/ files without corresponding test
- **Enforcement:** Pre-tool-use hook blocks edits
- **Fix:** Write test first using `/tdd-red` command

### 2. ASCII-Only Compliance
- **Rule:** No Unicode/emoji in src/ files
- **Enforcement:** Pre-tool-use hook blocks Unicode
- **Fix:** Use ASCII equivalents

### 3. Lock-Free Mandate
- **Rule:** Zero `lock()` statements in src/
- **Enforcement:** Pre-PR quality gate blocks lock()
- **Fix:** Use FSM/Enqueue pattern

### 4. Performance Targets (Epic 5)
- **Rule:** 0 B allocation, < 300μs latency for hot path
- **Enforcement:** BenchmarkDotNet tests
- **Fix:** Optimize allocations, use object pooling

## Next Steps

1. **Read:** [TDD Hardening Protocol](../protocol/TDD_HARDENING_PROTOCOL.md)
2. **Practice:** Use `/tdd-red`, `/tdd-green`, `/tdd-refactor` commands
3. **Templates:** Copy from [`tests/V12_Performance.Tests/Templates/`](../../tests/V12_Performance.Tests/Templates/)
4. **Validate:** Run `powershell -File .\scripts\pre_pr_quality_gate.ps1` before PR

## References
- [Testing Pyramid](../protocol/TESTING_PYRAMID.md)
- [TDD Integration Matrix](../protocol/TDD_INTEGRATION_MATRIX.md)
- [Test Templates](../../tests/V12_Performance.Tests/Templates/)