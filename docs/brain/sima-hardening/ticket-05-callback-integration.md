# Ticket 05: Callback Routing Integration

**Epic**: SIMA Subgraph Hardening  
**Phase**: Integration (Week 2)  
**Estimated Effort**: 2 hours  
**Risk Level**: HIGH (modifies callback hot path)

---

## Objective

Wire all primitives together by updating callback routing to use the new atomic primitives and generation-based ABA protection.

---

## Scope

### IN SCOPE
- Update `OnAccountOrderUpdate` to use `_orderIdToFsmMap.TryGet()`
- Add generation verification for ABA protection
- Update all `_orderIdToFsmKey` call sites to use `_orderIdToFsmMap`
- Migrate FSM state reads to use `_packedState` properties
- Remove old `_orderIdToFsmKey` dictionary (after validation)

### OUT OF SCOPE
- Business logic changes in callback handlers
- REAPER audit refactoring

---

## Context References

**Analysis**: [`docs/brain/sima-hardening/01-analysis.md`](./01-analysis.md)
- Section 2.2 (Cross-File Coupling): `_orderIdToFsmKey` has 23 references
- Section 3.1 (Critical Method Impact): Callback routing blast radius

**Approach**: [`docs/brain/sima-hardening/02-approach.md`](./02-approach.md)
- Section 1.2 (lines 156-181): Callback handling with generation check

---

## Implementation Instructions

### Step 1: Update OnAccountOrderUpdate

Locate `OnAccountOrderUpdate` in `V12_002.Orders.Callbacks.cs`.

Replace the OrderId lookup logic:

```csharp
// OLD:
// string fsmKey;
// if (_orderIdToFsmKey.TryGetValue(orderId, out fsmKey))

// NEW:
string fsmKey;
long expectedGen;
if (_orderIdToFsmMap.TryGet(orderId, out fsmKey, out expectedGen))
{
    if (_followerBrackets.TryGetValue(fsmKey, out var fsm))
    {
        // Verify generation matches (ABA protection)
        if (fsm.Generation == expectedGen)
        {
            // Process callback...
        }
        else
        {
            // Stale callback (slot was freed and reused)
            if (_diagFleet)
                Print(string.Format("[CALLBACK] Stale callback for {0} (gen mismatch)", orderId));
            return;
        }
    }
}
```

**Reference**: Approach doc section 1.2, lines 158-181

### Step 2: Update All _orderIdToFsmKey Call Sites

Search for all references to `_orderIdToFsmKey` and update:

**TryGetValue calls** → `_orderIdToFsmMap.TryGet(orderId, out fsmKey, out expectedGen)`
**TryAdd calls** → `_orderIdToFsmMap.TryAdd(orderId, fsmKey, generation)`
**Remove calls** → `_orderIdToFsmMap.Remove(orderId)`

Expected locations:
- `V12_002.Orders.Callbacks.Propagation.cs` (2-3 sites)
- `V12_002.SIMA.Fleet.cs` (already updated in Ticket 02)
- `V12_002.Orders.Management.Cleanup.cs` (1-2 sites)

### Step 3: Migrate FSM State Reads

Search for direct `fsm.State` property reads and evaluate:
- If read-only check → use `fsm.State` (existing property still works)
- If state transition → use `fsm.TryTransition()` (added in Ticket 02)
- If generation needed → use `fsm.Generation` property

**DO NOT** remove the old `State` property yet - it's still used by REAPER and other subsystems.

### Step 4: Validation Pass

After all call sites updated, verify:
1. No compilation errors
2. All `_orderIdToFsmKey` references resolved
3. Generation checks present in all callback paths

### Step 5: Remove Old Dictionary (OPTIONAL)

**ONLY after F5 compile + runtime validation**, remove:
- `_orderIdToFsmKey` field declaration
- `_orderIdToFsmKey` initialization in `OnStateChange`

**If any issues arise, REVERT this step and keep both dictionaries temporarily.**

---

## V12 DNA Guardrails

### Zero-Lock Compliance
- ✅ `_orderIdToFsmMap` uses lock-free primitives
- ✅ Generation checks use `Interlocked.Read`
- ❌ NO `lock()` statements permitted

### Zero-Allocation Compliance
- ✅ `TryGet` performs zero heap allocations
- ❌ NO `new` keyword in callback hot path

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
grep -r "_orderIdToFsmKey" src/  # Should return ZERO matches after Step 5
```

---

## Acceptance Criteria

### Functional
- [ ] All callbacks route via `_orderIdToFsmMap.TryGet()`
- [ ] Generation verification present in all callback paths
- [ ] Stale callbacks (gen mismatch) logged and ignored
- [ ] All `_orderIdToFsmKey` references migrated
- [ ] Old dictionary removed (or kept if validation fails)

### Compilation
- [ ] Code compiles without errors in NinjaTrader IDE (F5)
- [ ] BUILD_TAG banner displays correctly

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" src/` returns ZERO matches
- [ ] `grep -r "_orderIdToFsmKey" src/` returns ZERO matches (after Step 5)

---

## Dependencies

**Blocks**: Ticket 06 (testing)  
**Blocked By**: Ticket 01, Ticket 02