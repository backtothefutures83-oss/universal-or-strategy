# Codacy Phase 2: P0 ErrorProne Fix Progress

**Date Started**: 2026-05-28
**Agent**: Advanced Mode
**Target**: 73 P0 issues (70 generic catch + 3 missing defaults)

## Progress Summary

| Category | Total | Fixed | Remaining | Status |
|----------|-------|-------|-----------|--------|
| Generic Exception Catch (S2221) | 70 | 0 | 70 | 🔴 NOT STARTED |
| Missing Default Clause (S131) | 3 | 0 | 3 | 🔴 NOT STARTED |
| **TOTAL P0** | **73** | **0** | **73** | **0%** |

## Fix Strategy

### Generic Exception Catch (70 issues)
**Pattern**: Replace `catch (Exception ex)` with specific exception types + fail-fast

**Fix Template**:
```csharp
// BEFORE
try
{
    // risky operation
}
catch (Exception ex)
{
    Log.Error($"Error: {ex.Message}");
}

// AFTER
try
{
    // risky operation
}
catch (InvalidOperationException ex) when (ex.Message.Contains("known quirk"))
{
    // Known NT8 quirk - safe to handle
    Log.Warning($"Known issue: {ex.Message}");
}
catch (ArgumentException ex)
{
    // Invalid parameter - log and fail fast
    Log.Error($"Invalid argument: {ex.Message}");
    throw;
}
catch (Exception ex)
{
    // Unexpected - fail fast
    Log.Error($"CRITICAL: Unexpected exception: {ex}");
    throw;
}
```

### Missing Default Clause (3 issues)
**Pattern**: Add `default:` case to switch statements

**Fix Template**:
```csharp
// BEFORE
switch (state)
{
    case State.Active:
        // handle
        break;
    case State.Inactive:
        // handle
        break;
}

// AFTER
switch (state)
{
    case State.Active:
        // handle
        break;
    case State.Inactive:
        // handle
        break;
    default:
        throw new InvalidOperationException($"Unexpected state: {state}");
}
```

## Files to Fix (Top Priority)

| File | Generic Catch | Missing Default | Total | Priority |
|------|---------------|-----------------|-------|----------|
| V12_002.REAPER.Audit.cs | 5 | 0 | 5 | P0 |
| V12_002.Lifecycle.cs | 5 | 0 | 5 | P0 |
| V12_002.PositionInfo.cs | ? | 1 | ? | P0 |
| V12_002.Orders.Management.StopSync.cs | ? | 0 | ? | P0 |
| (others TBD) | ? | 2 | ? | P0 |

## Batch Plan

### Batch 1: REAPER.Audit.cs (5 issues) - IN PROGRESS
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch

### Batch 2: Lifecycle.cs (5 issues)
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch
- [ ] Line TBD: Generic catch

### Batch 3: Missing Defaults (3 issues)
- [ ] PositionInfo.cs:384 - Add default clause
- [ ] File TBD - Add default clause
- [ ] File TBD - Add default clause

## Verification Checklist

After each batch:
- [ ] Build successful (`dotnet build`)
- [ ] No new Codacy issues introduced
- [ ] ASCII-only compliance maintained
- [ ] No lock statements added
- [ ] Pre-push validation passes

## Notes

- **Inline comments deferred**: NOT fixing inline comment style issues (documented in `codacy_inline_comment_issue.md`)
- **Focus**: P0 ErrorProne issues ONLY
- **Jane Street alignment**: Fail-fast principle, specific exception handling
- **V12 DNA**: No silent failures, explicit error handling

---

**Last Updated**: 2026-05-28 15:29 PST