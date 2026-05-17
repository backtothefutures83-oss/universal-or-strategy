# Ticket 02: Pre-Submit Registration - Extraction Plan

**Generated**: 2026-05-16T19:07 UTC  
**Agent**: Bob CLI (v12-engineer)  
**Status**: AWAITING DIRECTOR APPROVAL

---

## Executive Summary

This plan implements pre-submit OrderId registration with `Pending` flag to eliminate the 50-500ms callback deadlock window. The implementation adds atomic state management before broker submission and clears the pending flag after successful dispatch.

---

## Forensic Analysis Results

### Ticket 01 Dependencies Verified

✅ **FsmPackedState struct** exists (V12_002.Symmetry.BracketFSM.cs:19-39)
- Pack/Unpack methods for atomic 64-bit state management
- Layout: [State: 8 bits][Pending: 1 bit][Generation: 55 bits]

✅ **ZeroAllocOrderIdMap class** exists (V12_002.cs:685-833)
- Lock-free hash table with FNV-1a hashing
- TryAdd, TryGet, Remove methods available
- Zero heap allocations after initialization

✅ **FollowerBracketFSM._packedState** field exists (V12_002.Symmetry.BracketFSM.cs:70)
- Private long field for atomic state storage
- Generation property accessor exists (lines 93-101)

### Missing Components (To Be Added)

❌ **_orderIdToFsmMap instance field** - Not found in V12_002.cs
- Need to add: `private ZeroAllocOrderIdMap _orderIdToFsmMap;`
- Need to initialize in OnStateChange (State.Configure block)

❌ **TryTransition method** - Not found in FollowerBracketFSM class
- Need to add to V12_002.Symmetry.BracketFSM.cs
- Atomic state transition with generation increment

### Current SubmitAndRegisterFleetOrders Analysis

**File**: V12_002.SIMA.Fleet.cs  
**Method**: SubmitAndRegisterFleetOrders (lines 148-184)

**Current Flow**:
1. Line 158: `acct.Submit(submitOrders)` - Broker dispatch
2. Lines 162-169: FSM state update to `Submitted` (AFTER submit)
3. Lines 171-180: OrderId registration (AFTER submit)

**Problem**: 50-500ms window between submit and registration where callbacks can arrive before OrderId mapping exists.

**Solution**: Reverse the order - register BEFORE submit, set Pending flag.

---

## Implementation Plan

### Phase 1: Add Missing Infrastructure

#### 1.1 Add _orderIdToFsmMap Instance Field
**File**: `src/V12_002.cs`  
**Location**: After line 833 (after ZeroAllocOrderIdMap class)  
**Action**: INSERT

```csharp

        // Phase 8: Zero-allocation OrderId map instance
        private ZeroAllocOrderIdMap _orderIdToFsmMap;
```

#### 1.2 Initialize _orderIdToFsmMap
**File**: `src/V12_002.cs`  
**Location**: Search for `State.Configure` block in OnStateChange  
**Action**: ADD initialization line

```csharp
                _orderIdToFsmMap = new ZeroAllocOrderIdMap(8192);
```

**Note**: Need to locate exact line number via search. Typical location is in OnStateChange method around State.Configure initialization.

#### 1.3 Add TryTransition Method to FollowerBracketFSM
**File**: `src/V12_002.Symmetry.BracketFSM.cs`  
**Location**: After line 114 (after ExpectedTargetPrices field, before closing brace)  
**Action**: INSERT

```csharp

            public bool TryTransition(FollowerBracketState expectedState, FollowerBracketState newState)
            {
                long current = Interlocked.Read(ref _packedState);
                FsmPackedState.Unpack(current, out byte state, out bool pending, out long gen);
                
                if ((FollowerBracketState)state != expectedState)
                    return false;
                
                long next = FsmPackedState.Pack((byte)newState, pending, gen + 1);
                return Interlocked.CompareExchange(ref _packedState, next, current) == current;
            }
```

### Phase 2: Modify SubmitAndRegisterFleetOrders

#### 2.1 Add Pre-Submit Registration Logic
**File**: `src/V12_002.SIMA.Fleet.cs`  
**Location**: BEFORE line 158 (`acct.Submit(submitOrders)`)  
**Action**: INSERT

```csharp

            // TICKET-02: Pre-submit registration to eliminate callback deadlock window
            // STEP 1: Set Pending flag atomically
            FollowerBracketFSM fsm;
            if (_followerBrackets.TryGetValue(fleetEntryName, out fsm))
            {
                long current = Interlocked.Read(ref fsm._packedState);
                FsmPackedState.Unpack(current, out byte state, out _, out long gen);
                long next = FsmPackedState.Pack(state, pending: true, gen);
                Interlocked.CompareExchange(ref fsm._packedState, next, current);
            }

            // STEP 2: Register OrderId mappings BEFORE submit
            for (int i = 0; i < orderCount; i++)
            {
                var ord = orders[i];
                if (ord != null && !string.IsNullOrEmpty(ord.OrderId))
                {
                    _orderIdToFsmMap.TryAdd(ord.OrderId, fleetEntryName, fsm != null ? fsm.Generation : 0);
                }
            }

```

#### 2.2 Add Post-Submit Pending Clear
**File**: `src/V12_002.SIMA.Fleet.cs`  
**Location**: AFTER line 160 (`syncCleared = true;`)  
**Action**: INSERT

```csharp

            // STEP 3: Clear Pending flag on success
            if (fsm != null)
            {
                long current = Interlocked.Read(ref fsm._packedState);
                FsmPackedState.Unpack(current, out byte state, out _, out long gen);
                long next = FsmPackedState.Pack(state, pending: false, gen);
                Interlocked.CompareExchange(ref fsm._packedState, next, current);
            }

```

#### 2.3 Update Legacy OrderId Registration (Keep for Backward Compat)
**File**: `src/V12_002.SIMA.Fleet.cs`  
**Location**: Lines 171-180 (existing OrderId registration)  
**Action**: KEEP AS-IS (dual registration for safety during migration)

**Rationale**: Keep both _orderIdToFsmKey (legacy) and _orderIdToFsmMap (new) registrations during Ticket 02. Ticket 05 will migrate callbacks to use _orderIdToFsmMap exclusively.

---

## DNA Compliance Verification

### Zero-Lock Checklist
- ✅ Uses `Interlocked.CompareExchange` for atomic state updates
- ✅ Uses `Interlocked.Read` for atomic reads
- ✅ TryTransition uses CAS pattern (compare-and-swap)
- ❌ NO `lock()` statements added

### Zero-Allocation Checklist
- ✅ `_orderIdToFsmMap.TryAdd()` performs zero heap allocations
- ✅ FsmPackedState operations are stack-only
- ✅ No `new` keyword in hot path (pre-submit/post-submit)
- ❌ NO heap allocations in critical path

### ASCII-Only Checklist
- ✅ All comments use ASCII characters
- ✅ All string literals use ASCII characters
- ❌ NO Unicode, emoji, or curly quotes

---

## Risk Assessment

### HIGH RISK
- None (all changes are additive or surgical)

### MEDIUM RISK
- **Pending flag logic**: Must ensure flag is cleared even on exception paths
  - Mitigation: Existing try/catch in ProcessFleetSlot handles rollback

### LOW RISK
- **Dual registration**: Both old and new maps populated during migration
  - Mitigation: Ticket 05 will remove legacy map after callback migration

---

## Testing Strategy

### Manual Verification (F5 in NinjaTrader)
1. Load strategy, verify BUILD_TAG displays
2. Enable SIMA, place fleet order
3. Check Output window for "[PUMP] Submitted" messages
4. Verify no exceptions during order submission
5. Verify FSM state transitions correctly (Submitted -> Accepted -> Active)

### Forensic Verification
```powershell
# 1. Verify zero locks
grep -r "lock(" src/

# 2. Verify ASCII-only
grep -Prn "[^\x00-\x7F]" src/

# 3. Sync hard links
powershell -File .\deploy-sync.ps1
```

---

## Rollback Plan

If issues arise:
1. Revert changes to V12_002.SIMA.Fleet.cs (remove pre-submit/post-submit blocks)
2. Remove _orderIdToFsmMap field and initialization
3. Remove TryTransition method
4. Run `powershell -File .\deploy-sync.ps1`

All changes are surgical and isolated. Legacy _orderIdToFsmKey registration remains functional.

---

## File Modification Summary

| File | Lines Added | Lines Modified | Risk | Type |
|------|-------------|----------------|------|------|
| V12_002.cs | +2 | 0 | LOW | Additive (field + init) |
| V12_002.Symmetry.BracketFSM.cs | +11 | 0 | LOW | Additive (method) |
| V12_002.SIMA.Fleet.cs | +30 | 0 | MEDIUM | Surgical (pre/post submit) |

**Total**: ~43 lines added, 0 lines modified

---

## Dependencies

**Blocks**: Ticket 05 (Callback Integration)  
**Blocked By**: Ticket 01 (COMPLETE - verified above)

---

## Acceptance Criteria

### Functional
- [ ] `Pending=true` set before `acct.Submit()` call
- [ ] OrderId mappings registered in _orderIdToFsmMap before broker dispatch
- [ ] `Pending=false` cleared after successful submit
- [ ] TryTransition method increments generation on state change

### Compilation
- [ ] Code compiles without errors in NinjaTrader IDE (F5)
- [ ] BUILD_TAG banner displays correctly

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" src/` returns ZERO matches

---

## Director Approval Required

**STOP**: This plan requires Director approval before execution.

**Approval Checklist**:
- [ ] Forensic analysis is complete and accurate
- [ ] Ticket 01 dependencies verified
- [ ] Implementation approach is sound
- [ ] DNA compliance is verified
- [ ] Rollback plan is clear
- [ ] Risk assessment is acceptable

**Awaiting**: Director sign-off to proceed to Phase 3 (Execution)