# Ticket 03: Sideband-First Cleanup Ordering

**Epic**: SIMA Subgraph Hardening  
**Phase**: Safety (Week 1)  
**Estimated Effort**: 1 hour  
**Risk Level**: LOW (isolated finally block change)

---

## Objective

Fix the use-after-free window by clearing sideband refs BEFORE pool release (Solution 5).

---

## Scope

### IN SCOPE
- Reorder `ProcessFleetSlot` finally block to clear sideband before pool release
- Add `Thread.MemoryBarrier()` between sideband clear and pool release
- Ensure atomic ordering guarantees

### OUT OF SCOPE
- Business logic changes in try/catch blocks
- Pump priming logic modifications

---

## Context References

**Analysis**: [`docs/brain/sima-hardening/01-analysis.md`](./01-analysis.md)
- Section 4.1 (P0 Critical Hotspots): H3 - Pool release before sideband clear
- Section 7.1 (Bug Registry Mapping): Compound Trap #3

**Approach**: [`docs/brain/sima-hardening/02-approach.md`](./02-approach.md)
- Section 1.5 (lines 505-563): Complete sideband-first ordering

---

## Implementation Instructions

### Step 1: Locate ProcessFleetSlot Finally Block

Find `ProcessFleetSlot` method in `V12_002.SIMA.Fleet.cs`. Locate the `finally` block.

### Step 2: Reorder Cleanup Operations

Replace the existing finally block with this ordering:

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

**Reference**: Approach doc section 1.5, lines 526-556

**Key Change**: Sideband clear (Step 1) now happens BEFORE pool release (Step 3), with a memory barrier in between.

---

## V12 DNA Guardrails

### Zero-Lock Compliance
- ✅ Uses `Thread.MemoryBarrier()` for ordering
- ✅ Uses `Interlocked.Decrement` for counter
- ❌ NO `lock()` statements permitted

### Zero-Allocation Compliance
- ✅ `default(FleetDispatchSideband)` is stack operation
- ❌ NO `new` keyword in finally block

### ASCII-Only Compliance
- ✅ All string literals use ASCII characters only
- ❌ NO Unicode, emoji, or curly quotes

---

## Post-Edit Verification

```powershell
powershell -File .\deploy-sync.ps1
python scripts/complexity_audit.py
grep -r "lock(" src/
grep -Prn "[^\x00-\x7F]" src/
```

---

## Acceptance Criteria

### Functional
- [ ] Sideband cleared before pool release in all code paths
- [ ] `Thread.MemoryBarrier()` present between clear and release
- [ ] Counter decrement happens after pool release
- [ ] Pump prime logic unchanged

### Compilation
- [ ] Code compiles without errors in NinjaTrader IDE (F5)
- [ ] BUILD_TAG banner displays correctly

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" src/` returns ZERO matches

---

## Dependencies

**Blocks**: None (independent safety fix)  
**Blocked By**: None