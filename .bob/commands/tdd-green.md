# TDD GREEN Phase Command

## Purpose
Implement MINIMAL code to pass the failing test. This command guides you through the GREEN phase of TDD.

## Usage
```
/tdd-green <feature-name>
```

## Protocol

### Step 1: Verify Test is Failing
```powershell
dotnet test --filter "FullyQualifiedName~YourFeature"
# Must show: EXIT 1 (FAIL)
```

### Step 2: Implement Minimal Code
Write ONLY enough code to pass the test. No:
- Extra features
- Premature optimization
- Speculative abstractions

### Step 3: Verify Test Passes
```powershell
dotnet test --filter "FullyQualifiedName~YourFeature"
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

## Next Step
After GREEN phase complete, run `/tdd-refactor` to clean up the code.