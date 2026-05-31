# PR #6 Fix Queue (Jane Street Audited)
Generated: 2026-05-31 14:48:00

## VALID-FIX Issues (Priority Order)

### Fix #1 - [P0] CRITICAL - Metric Tracking Before Abort Guard
[x] **Bot:** cubic-dev-ai  
[x] **File:** src/V12_002.SIMA.Fleet.cs:235  
[x] **Issue:** TrackSimaDispatch() called before abort guard - metrics track aborted work

**Jane Street Analysis:**
- **Category**: VALID-FIX
- **Rationale**: Metrics should only track actual work, not aborted paths
- **V12 DNA Alignment**: Correctness by construction - metrics must reflect reality

**Fix Required:**
Move `TrackSimaDispatch();` from line 235 to line 243 (after abort guard passes)

```csharp
// BEFORE (line 233-242):
private void PumpFleetDispatch()
{
    TrackSimaDispatch();  // ❌ Tracks even when aborting
    if (isFlattenRunning || !EnableSIMA)
    {
        DrainAllDispatchQueuesOnAbort();
        Print("[PUMP] Abort: SIMA inactive or flatten running...");
        return;
    }

// AFTER:
private void PumpFleetDispatch()
{
    // A3-1: Abort and drain if SIMA disabled or flatten running
    if (isFlattenRunning || !EnableSIMA)
    {
        DrainAllDispatchQueuesOnAbort();
        Print("[PUMP] Abort: SIMA inactive or flatten running...");
        return;
    }
    TrackSimaDispatch();  // ✅ Only tracks actual dispatches
```

---

### Fix #3/#5 - [P1] PERFORMANCE - ToArray() Allocations in Hot Path
[x] **Bot:** sourcery-ai + gitar-bot (duplicate)  
[x] **File:** src/V12_002.SIMA.Fleet.cs:536, 562  
[x] **Issue:** HasActiveFsmForAccount and HasActivePositionForAccount call ToArray() on ConcurrentDictionary

**Jane Street Analysis:**
- **Category**: VALID-FIX (CRITICAL)
- **Rationale**: Violates V12 DNA zero-allocation mandate
- **V12 DNA Alignment**: Zero-allocation hot paths are non-negotiable
- **Performance Impact**: Each ToArray() = heap allocation in dispatch hot path

**Fix Required:**
Replace ToArray() with direct enumeration on ConcurrentDictionary (lock-free, zero-allocation)

```csharp
// BEFORE (line 534-555):
private bool HasActiveFsmForAccount(string accountName)
{
    var followerBracketsSnapshot = _followerBrackets.ToArray();  // ❌ Heap allocation
    for (int fi = 0; fi < followerBracketsSnapshot.Length; fi++)
    {
        var f = followerBracketsSnapshot[fi].Value;
        // ... check logic
    }
    return false;
}

// AFTER:
private bool HasActiveFsmForAccount(string accountName)
{
    foreach (var kvp in _followerBrackets)  // ✅ Zero-allocation enumeration
    {
        var f = kvp.Value;
        if (
            f != null
            && f.AccountName == accountName
            && (
                f.State == FollowerBracketState.Active
                || f.State == FollowerBracketState.Accepted
                || f.State == FollowerBracketState.Submitted
                || f.State == FollowerBracketState.Replacing
            )
        )
        {
            return true;
        }
    }
    return false;
}
```

Same fix for HasActivePositionForAccount (line 560-572):
```csharp
// BEFORE:
private bool HasActivePositionForAccount(string accountName)
{
    var activePositionsSnapshot = activePositions.ToArray();  // ❌ Heap allocation
    for (int api = 0; api < activePositionsSnapshot.Length; api++)
    {
        var p = activePositionsSnapshot[api].Value;
        // ... check logic
    }
    return false;
}

// AFTER:
private bool HasActivePositionForAccount(string accountName)
{
    foreach (var kvp in activePositions)  // ✅ Zero-allocation enumeration
    {
        var p = kvp.Value;
        if (p != null && p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == accountName)
        {
            return true;
        }
    }
    return false;
}
```

---

## Completion Checklist
- [ ] Fix #1: Move TrackSimaDispatch() after abort guard
- [ ] Fix #3/#5: Replace ToArray() with foreach in HasActiveFsmForAccount
- [ ] Fix #3/#5: Replace ToArray() with foreach in HasActivePositionForAccount
- [ ] Run: `powershell -File .\scripts\format_all_csharp.ps1`
- [ ] Run: `powershell -File .\scripts\pre_push_validation.ps1`
- [ ] Verify: All 13/13 checks pass
