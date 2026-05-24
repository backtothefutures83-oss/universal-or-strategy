# TDD RED Phase Command

## Purpose
Write a failing test BEFORE implementation. This command guides you through the RED phase of TDD.

## Usage
```
/tdd-red <feature-name>
```

## Protocol

### Step 1: Identify Feature
What feature are you implementing?
- Feature name: _______________
- File to modify: src/_______________
- Expected behavior: _______________

### Step 2: Create Test File
```powershell
# Create test file
$featureName = "YourFeature"
$testFile = "tests/V12_Performance.Tests/${featureName}Tests.cs"
New-Item -Path $testFile -ItemType File -Force
```

### Step 3: Write Failing Test
Use the unit test template:
```csharp
[Fact]
public void FeatureName_Scenario_ExpectedResult()
{
    // Arrange
    var sut = new SystemUnderTest();
    var input = /* test input */;
    var expected = /* expected output */;
    
    // Act
    var actual = sut.MethodToTest(input);
    
    // Assert
    Assert.Equal(expected, actual);
}
```

### Step 4: Verify Test FAILS
```powershell
dotnet test --filter "FullyQualifiedName~YourFeature"
# Expected: EXIT 1 (FAIL)
```

**CRITICAL:** If test PASSES, it's not validating the feature. Fix the test.

### Step 5: Commit Test Only
```powershell
git add tests/
git commit -m "[TDD-RED] <feature>: Add failing test"
```

## Next Step
After RED phase complete, run `/tdd-green` to implement the feature.