# Ticket 05 Extraction Plan: Callback Routing Integration

**Epic**: SIMA Subgraph Hardening  
**Ticket**: 05 - Callback Routing Integration  
**Estimated Effort**: 2 hours  
**Risk Level**: HIGH (modifies callback hot path)

---

## Scope Analysis

Found **19 references** to `_orderIdToFsmKey` across **6 files**:

1. **V12_002.cs** (1): Field declaration
2. **V12_002.Symmetry.BracketFSM.cs** (7): TryRemove, TryGetValue, indexer writes
3. **V12_002.SIMA.Lifecycle.cs** (6): Indexer writes during hydration
4. **V12_002.SIMA.Fleet.cs** (2): Indexer writes (already has dual registration)
5. **V12_002.SIMA.Execution.cs** (1): Indexer write
6. **V12_002.Orders.Callbacks.Propagation.cs** (2): TryRemove, indexer write

---

## Migration Strategy

### Phase 1: Update Write Operations (Registration)
All `_orderIdToFsmKey[orderId] = fsmKey` → `_orderIdToFsmMap.TryAdd(orderId, fsmKey, generation)`

**Files to modify:**
- `V12_002.Symmetry.BracketFSM.cs` (3 sites)
- `V12_002.SIMA.Lifecycle.cs` (6 sites)
- `V12_002.SIMA.Fleet.cs` (1 site - already has dual write, remove legacy)
- `V12_002.SIMA.Execution.cs` (1 site)
- `V12_002.Orders.Callbacks.Propagation.cs` (1 site)

### Phase 2: Update Read Operations (Lookup)
All `_orderIdToFsmKey.TryGetValue(orderId, out fsmKey)` → `_orderIdToFsmMap.TryGet(orderId, out fsmKey, out generation)`

**Files to modify:**
- `V12_002.Symmetry.BracketFSM.cs` (1 site at line 236)

### Phase 3: Update Delete Operations (Cleanup)
All `_orderIdToFsmKey.TryRemove(orderId, out _)` → `_orderIdToFsmMap.Remove(orderId)`

**Files to modify:**
- `V12_002.Symmetry.BracketFSM.cs` (4 sites)
- `V12_002.Orders.Callbacks.Propagation.cs` (1 site)

### Phase 4: Remove Legacy Dictionary
After validation, remove:
- Field declaration in `V12_002.cs` (line 681-682)

---

## Surgical Edit Plan

### Edit 1: V12_002.Symmetry.BracketFSM.cs
**Lines to modify**: 182, 185, 188, 195, 236, 262, 285, 294, 303

**Operations:**
- Lines 182, 185, 188, 195: `TryRemove` → `Remove`
- Line 236: `TryGetValue` → `TryGet` with generation check
- Lines 262, 285, 294, 303: Indexer write → `TryAdd` with generation

### Edit 2: V12_002.SIMA.Lifecycle.cs
**Lines to modify**: 707, 725, 824, 841, 946

**Operations:**
- All indexer writes → `TryAdd` with generation from FSM

### Edit 3: V12_002.SIMA.Fleet.cs
**Lines to modify**: 220-228

**Operations:**
- Remove legacy comment and `_orderIdToFsmKey` writes (already has `_orderIdToFsmMap`)

### Edit 4: V12_002.SIMA.Execution.cs
**Lines to modify**: 508

**Operations:**
- Indexer write → `TryAdd` with generation

### Edit 5: V12_002.Orders.Callbacks.Propagation.cs
**Lines to modify**: 612, 619

**Operations:**
- Line 612: `TryRemove` → `Remove`
- Line 619: Indexer write → `TryAdd` with generation

### Edit 6: V12_002.cs (FINAL - After Validation)
**Lines to modify**: 681-682

**Operations:**
- Remove field declaration (ONLY after F5 compile + runtime validation)

---

## Generation Source Strategy

For `TryAdd(orderId, fsmKey, generation)` calls, generation comes from:
- **FSM context**: `fsm.Generation` (when FSM is in scope)
- **New registrations**: Use `0` for initial registration (will be updated on first transition)

---

## V12 DNA Compliance

### Zero-Lock ✅
- All operations use lock-free `_orderIdToFsmMap` primitives
- No `lock()` statements added

### Zero-Allocation ✅
- `TryGet` performs zero heap allocations
- No `new` keyword in hot paths

### ASCII-Only ✅
- All string literals use ASCII characters only

---

## Verification Checklist

### Pre-Deploy
- [ ] All 19 references migrated
- [ ] Generation checks added where needed
- [ ] No compilation errors

### Post-Deploy
- [ ] `deploy-sync.ps1` passes
- [ ] F5 compile successful in NinjaTrader
- [ ] `grep -r "_orderIdToFsmKey" src/` returns ZERO matches (after Edit 6)
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] Runtime validation: callbacks route correctly

---

## Rollback Plan

If issues arise:
1. Keep both dictionaries temporarily
2. Revert Edit 6 (field removal)
3. Add dual-write safety net in critical paths

---

## Acceptance Criteria

- [ ] All 19 `_orderIdToFsmKey` references migrated to `_orderIdToFsmMap`
- [ ] Generation verification present in lookup paths
- [ ] Stale callbacks logged and ignored
- [ ] Legacy dictionary removed (or kept if validation fails)
- [ ] Zero compilation errors
- [ ] DNA compliance verified