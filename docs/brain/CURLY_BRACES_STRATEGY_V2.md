# Curly Braces Fix Strategy V2 - Correct Approach

## Failure Analysis: PR #14

**Root Cause**: Python script failed to handle 3 multi-line patterns:
1. Multi-line method calls (e.g., `Print(string.Format(...))`)
2. Multi-line if conditions (e.g., `if (expr && expr)`)
3. Lambda event handlers (e.g., `Click += (s, e) => { ... }`)

**Impact**: 904 violations remain, automated approach failed twice.

## New Strategy: Incremental Manual Fix with Rigorous Testing

### Phase 1: Tool Evaluation (MANDATORY)

Test each potential tool on a SINGLE file before batch application:

**Candidates**:
1. **ReSharper CLI** (JetBrains)
   - Command: `jb cleanupcode --profile="Full Cleanup" src/V12_002.cs`
   - Test file: `src/V12_002.cs` (17 violations)
   
2. **Roslynator CLI** (open-source)
   - Command: `roslynator fix --analyzer-assemblies Roslynator.CSharp.Analyzers.dll --diagnostic-ids RCS1007`
   - Test file: `src/V12_002.cs`

3. **Manual IDE Fix** (VS Code + C# extension)
   - Use "Fix all occurrences" in VS Code
   - Test file: `src/V12_002.cs`

**Test Protocol**:
```powershell
# 1. Create test branch
git checkout -b test-curly-braces-tool

# 2. Apply tool to ONE file
[tool command] src/V12_002.cs

# 3. Verify compilation
dotnet build Linting.csproj

# 4. Verify logic unchanged
git diff src/V12_002.cs | grep -E "^\+.*[^{}]$" | wc -l  # Should be 0 (only braces added)

# 5. If PASS: proceed to Phase 2
# 6. If FAIL: try next tool
```

### Phase 2: Batch Application (After Tool Validation)

**Approach**: Process files in batches of 10, with build verification after each batch.

**Batch Protocol**:
```powershell
# Batch 1: Files 1-10
[tool command] src/V12_002.*.cs | Select-Object -First 10
dotnet build Linting.csproj
git add src/*.cs
git commit -m "fix(style): Curly braces batch 1/7 (10 files)"

# Repeat for batches 2-7
```

**Safety Gates**:
- Build MUST pass after each batch
- Diff MUST show only brace additions (no logic changes)
- If ANY batch fails: revert, analyze, fix manually

### Phase 3: Manual Fallback (If All Tools Fail)

**Strategy**: Use VS Code "Problems" panel to navigate violations one-by-one.

**Steps**:
1. Open file in VS Code
2. Navigate to each IDE0011 warning
3. Apply "Quick Fix" → "Add braces"
4. Verify change is correct
5. Move to next violation

**Estimated Time**: 2-4 hours for 904 violations

### Phase 4: Verification (MANDATORY)

**Pre-Push Checklist**:
```powershell
# 1. Full build
dotnet build Linting.csproj --no-incremental

# 2. Run tests
dotnet test

# 3. Verify diff is braces-only
git diff main --stat | grep "insertions(+), 0 deletions(-)"

# 4. Run pre-push validation
powershell -File .\scripts\pre_push_validation.ps1

# 5. Deploy sync (hard links)
powershell -File .\deploy-sync.ps1

# 6. F5 verification in NinjaTrader
# (Manual step - Director confirms)
```

## Decision Tree

```
START
  ↓
Phase 1: Test ReSharper CLI on 1 file
  ↓
  ├─ PASS → Phase 2: Batch apply (10 files at a time)
  │   ↓
  │   ├─ ALL BATCHES PASS → Phase 4: Verification → DONE
  │   └─ ANY BATCH FAILS → Revert batch → Phase 3: Manual fix
  │
  └─ FAIL → Test Roslynator CLI on 1 file
      ↓
      ├─ PASS → Phase 2: Batch apply
      └─ FAIL → Test VS Code Quick Fix on 1 file
          ↓
          ├─ PASS → Phase 2: Batch apply
          └─ FAIL → Phase 3: Manual fix (2-4 hours)
```

## Success Criteria

- ✅ Zero compilation errors
- ✅ Zero test failures
- ✅ Diff shows ONLY brace additions (no deletions, no logic changes)
- ✅ All 10 pre-push validation checks pass
- ✅ Hard links synced to NinjaTrader
- ✅ F5 verification passes in NinjaTrader IDE

## Lessons Learned

1. **Never trust automated tools without single-file validation first**
2. **Build verification is necessary but not sufficient** (PR #14 built locally but had errors)
3. **Bot reviews are critical** - Greptile caught what local build missed
4. **Batch processing with checkpoints** prevents catastrophic failures
5. **Manual fallback is always an option** - don't force automation

## Next Steps

1. Execute Phase 1 (tool evaluation) on `src/V12_002.cs`
2. Report findings to Director
3. Proceed with validated approach
4. Create PR with rigorous testing at each step