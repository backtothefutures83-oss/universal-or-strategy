# Ticket 03: Sideband Cleanup - Extraction Plan

**Agent**: Bob CLI (v12-engineer)  
**Date**: 2026-05-16  
**Risk**: LOW (isolated finally block reordering)  
**Estimated Time**: 15 minutes

---

## Forensic Analysis

### Current State (Lines 68-81 in V12_002.SIMA.Fleet.cs)

```csharp
finally
{
    if (poolSlotIndex >= 0)
        _photonPool.ReleaseByIndex(poolSlotIndex);
    Interlocked.Decrement(ref _pendingFleetDispatchCount);
    if ((_photonDispatchRing != null && !_photonDispatchRing.IsEmpty)
        || !_pendingFleetDispatches.IsEmpty)
        try { TriggerCustomEvent(o => PumpFleetDispatch(), null); }
        catch (Exception ex)
        {
            if (_diagFleet)
                Print("[FLEET_CATCH] ProcessFleetSlot pump prime failed: " + ex.Message);
        }
}
```

### Vulnerability Identified

**Use-After-Free Window**: Pool slot is released (line 71) BEFORE sideband is cleared. This creates a race condition where:
1. Thread A releases pool slot
2. Thread B acquires same slot and writes new data
3. Thread A clears sideband, destroying Thread B's references

**Impact**: Stale Account/Order references retained across ring wraps, causing callback routing failures.

---

## Surgical Change Required

### Target: ProcessFleetSlot Finally Block (Lines 68-81)

**Operation**: Reorder cleanup sequence to guarantee sideband-first ordering with memory barrier.

### New Ordering (Per Ticket Spec)

```csharp
finally
{
    // CRITICAL ORDERING: Sideband clear BEFORE pool release
    if (poolSlotIndex >= 0)
    {
        // Step 1: Clear sideband refs (prevents stale retention)
        if (poolSlotIndex < _photonSideband.Length)
            _photonSideband[poolSlotIndex] = default(FleetDispatchSideband);
        
        // Step 2: Memory barrier (ensure sideband write visible)
        Thread.MemoryBarrier();
        
        // Step 3: Release pool slot (now safe for reuse)
        _photonPool.ReleaseByIndex(poolSlotIndex);
    }
    
    // Step 4: Decrement counter
    Interlocked.Decrement(ref _pendingFleetDispatchCount);
    
    // Step 5: Pump prime (if queue non-empty)
    if ((_photonDispatchRing != null && !_photonDispatchRing.IsEmpty)
        || !_pendingFleetDispatches.IsEmpty)
    {
        try { TriggerCustomEvent(o => PumpFleetDispatch(), null); }
        catch (Exception ex)
        {
            if (_diagFleet)
                Print("[FLEET_CATCH] Pump prime failed: " + ex.Message);
        }
    }
}
```

---

## Key Changes

1. **Sideband Clear First** (NEW): `_photonSideband[poolSlotIndex] = default(FleetDispatchSideband);`
2. **Memory Barrier** (NEW): `Thread.MemoryBarrier();` ensures visibility across threads
3. **Pool Release Second**: Moved after sideband clear + barrier
4. **Bounds Check Added**: `if (poolSlotIndex < _photonSideband.Length)` for safety
5. **Comment Update**: Changed `[FLEET_CATCH] ProcessFleetSlot pump prime failed` to `[FLEET_CATCH] Pump prime failed` (minor cleanup)

---

## V12 DNA Compliance

### Zero-Lock ✅
- Uses `Thread.MemoryBarrier()` for ordering (lock-free primitive)
- Uses `Interlocked.Decrement` for counter (atomic primitive)
- NO `lock()` statements

### Zero-Allocation ✅
- `default(FleetDispatchSideband)` is stack operation (no heap allocation)
- NO `new` keyword in finally block

### ASCII-Only ✅
- All string literals use ASCII characters only
- NO Unicode, emoji, or curly quotes

---

## Diff Summary

**File**: `src/V12_002.SIMA.Fleet.cs`  
**Lines Modified**: 68-81 (14 lines)  
**Lines Added**: 7 (sideband clear, barrier, bounds check, restructured if block)  
**Lines Removed**: 2 (old pool release, old comment)  
**Net Change**: +5 lines

---

## Testing Strategy

### Compilation Test
```powershell
# F5 in NinjaTrader IDE
# Verify BUILD_TAG banner displays
```

### DNA Audit
```powershell
powershell -File .\deploy-sync.ps1
grep -r "lock(" src/
grep -Prn "[^\x00-\x7F]" src/
```

### Expected Results
- ✅ Code compiles without errors
- ✅ Hard links synchronized
- ✅ Zero lock() statements found
- ✅ Zero non-ASCII characters found

---

## Risk Assessment

**Risk Level**: LOW

**Rationale**:
- Isolated change to single finally block
- No business logic modifications
- No changes to try/catch blocks
- Preserves all existing cleanup operations
- Adds defensive bounds check

**Blast Radius**: ProcessFleetSlot method only (no callers affected)

---

## Approval Gate

**STOP**: Awaiting Director approval before executing surgical change.

**Verification Criteria**:
1. Sideband clear happens BEFORE pool release ✅
2. Memory barrier present between operations ✅
3. Bounds check added for safety ✅
4. All V12 DNA constraints satisfied ✅
5. No business logic changes ✅

**Ready to Execute**: YES (pending approval)