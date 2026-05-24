# Developer Guide - V12 TDD Workflow

## Daily Workflow

### Morning Routine
1. **Pull latest changes**
   ```powershell
   git fetch origin main
   git rebase origin/main
   ```

2. **Run tests**
   ```powershell
   dotnet test
   # Expected: All tests PASS
   ```

3. **Check tool availability**
   ```powershell
   powershell -File .\scripts\discover_tools.ps1
   ```

### Feature Development

#### Step 1: Plan Feature
1. **Identify feature scope**
   - What are you building?
   - What files will change?
   - What tests are needed?

2. **Check existing tests**
   ```powershell
   # Find related tests
   Get-ChildItem -Path "tests" -Filter "*Tests.cs" -Recurse | Select-String -Pattern "YourFeature"
   ```

#### Step 2: TDD RED Phase
1. **Create test file**
   ```powershell
   $featureName = "YourFeature"
   $testFile = "tests/V12_Performance.Tests/${featureName}Tests.cs"
   New-Item -Path $testFile -ItemType File -Force
   ```

2. **Write failing test**
   - Copy from [`UnitTestTemplate.cs`](../../tests/V12_Performance.Tests/Templates/UnitTestTemplate.cs)
   - Replace placeholders with actual logic
   - Follow naming: `MethodName_Scenario_ExpectedResult`

3. **Verify test FAILS**
   ```powershell
   dotnet test --filter "YourFeature"
   # Expected: EXIT 1 (FAIL)
   ```

4. **Commit test**
   ```powershell
   git add tests/
   git commit -m "[TDD-RED] YourFeature: Add failing test"
   ```

#### Step 3: TDD GREEN Phase
1. **Implement minimal code**
   - Write ONLY enough to pass the test
   - No extra features
   - No premature optimization

2. **Verify test PASSES**
   ```powershell
   dotnet test --filter "YourFeature"
   # Expected: EXIT 0 (PASS)
   ```

3. **Run DNA audits**
   ```powershell
   # ASCII compliance
   grep -Prn "[^\x00-\x7F]" src/
   
   # Lock-free compliance
   grep -r "lock(" src/
   
   # Deploy-sync
   powershell -File .\deploy-sync.ps1
   ```

4. **Commit implementation**
   ```powershell
   git add src/ tests/
   git commit -m "[TDD-GREEN] YourFeature: Implement to pass test"
   ```

#### Step 4: TDD REFACTOR Phase
1. **Clean up code**
   - Extract methods if CYC > 15
   - Improve naming
   - Remove duplication

2. **Verify tests still PASS**
   ```powershell
   dotnet test
   # Expected: All tests PASS
   ```

3. **Run complexity audit**
   ```powershell
   python scripts/complexity_audit.py
   # Expected: All methods CYC < 20
   ```

4. **Commit refactoring**
   ```powershell
   git add src/
   git commit -m "[TDD-REFACTOR] YourFeature: Clean up implementation"
   ```

### Pre-PR Checklist

#### 1. Run Quality Gate
```powershell
powershell -File .\scripts\pre_pr_quality_gate.ps1
# Expected: [APPROVED] Quality gate PASSED
```

#### 2. Verify PR Hygiene
```powershell
powershell -File .\scripts\verify_pr_hygiene.ps1
# Expected: Branch up-to-date, no conflicts
```

#### 3. Create PR
```powershell
gh pr create --title "[FEATURE] YourFeature" --body "Description here" --label "enhancement"
```

#### 4. Run PR Loop
```powershell
# After PR created, get PR number
$prNumber = 123

# Run PR loop to drive PHS to 100/100
/pr-loop $prNumber
```

### Troubleshooting

#### Test Fails After Refactoring
**Problem:** Tests passed in GREEN phase but fail after REFACTOR
**Fix:**
1. Revert refactoring: `git reset --hard HEAD~1`
2. Re-run tests: `dotnet test`
3. Refactor more carefully, run tests after each change

#### TDD Enforcement Hook Blocks Edit
**Problem:** Cannot edit src/ file without test
**Fix:**
1. Write test first: `/tdd-red YourFeature`
2. Verify test FAILS
3. Then edit src/ file

#### Deploy-Sync Fails
**Problem:** ASCII gate fails or hard links not synced
**Fix:**
1. Check for Unicode: `grep -Prn "[^\x00-\x7F]" src/`
2. Replace Unicode with ASCII
3. Re-run: `powershell -File .\deploy-sync.ps1`

#### Complexity Audit Fails
**Problem:** Method CYC > 20
**Fix:**
1. Identify complex method: `python scripts/complexity_audit.py`
2. Extract sub-methods (>= 15 LOC each)
3. Re-run audit

## Best Practices

### Test Naming
```csharp
// GOOD: Descriptive, clear intent
[Fact]
public void CheckProximity_WithinThreshold_ReturnsTrue()

// BAD: Vague, unclear intent
[Fact]
public void Test1()
```

### Test Structure (Arrange-Act-Assert)
```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange: Set up test data
    var sut = new SystemUnderTest();
    var input = ...;
    var expected = ...;
    
    // Act: Execute method under test
    var actual = sut.MethodToTest(input);
    
    // Assert: Verify result
    Assert.Equal(expected, actual);
}
```

### One Assertion Per Test
```csharp
// GOOD: One assertion
[Fact]
public void Add_TwoNumbers_ReturnsSum()
{
    Assert.Equal(5, Add(2, 3));
}

// BAD: Multiple assertions
[Fact]
public void Add_MultipleScenarios()
{
    Assert.Equal(5, Add(2, 3));
    Assert.Equal(10, Add(5, 5));
    Assert.Equal(0, Add(-1, 1));
}
```

### Use Theory for Multiple Scenarios
```csharp
// GOOD: Theory with InlineData
[Theory]
[InlineData(2, 3, 5)]
[InlineData(5, 5, 10)]
[InlineData(-1, 1, 0)]
public void Add_MultipleScenarios_ReturnsSum(int a, int b, int expected)
{
    Assert.Equal(expected, Add(a, b));
}
```

## References
- [TDD Quickstart](TDD_QUICKSTART.md)
- [Testing Pyramid](../protocol/TESTING_PYRAMID.md)
- [TDD Integration Matrix](../protocol/TDD_INTEGRATION_MATRIX.md)
- [Pre-PR Quality Gate](../protocol/PRE_PR_QUALITY_GATE.md)