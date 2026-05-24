# TDD REFACTOR Phase Command

## Purpose
Clean up implementation while keeping tests green. This command guides you through the REFACTOR phase of TDD.

## Usage
```
/tdd-refactor <feature-name>
```

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

## TDD Cycle Complete
Feature is implemented, tested, and clean. Ready for PR submission.