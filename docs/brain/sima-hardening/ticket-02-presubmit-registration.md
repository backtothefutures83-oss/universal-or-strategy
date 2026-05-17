# Ticket 02: Pre-Submit OrderId Registration

**Epic**: SIMA Subgraph Hardening  
**Phase**: Registration (Week 1)  
**Estimated Effort**: 2 hours  
**Risk Level**: MEDIUM (modifies submission lifecycle)

---

## Objective

Implement pre-submit OrderId registration with `Pending` flag to eliminate the 50-500ms callback deadlock window (Solution 2).

---

## Scope

### IN SCOPE
- Modify `SubmitAndRegisterFleetOrders` to set `Pending=true` before `acct.Submit()`
- Register OrderId → FSM mappings using `_orderIdToFsmMap.TryAdd()` before broker dispatch
- Clear `Pending=false` after successful submit
- Add generation increment on FSM state transitions

### OUT OF SCOPE
- Callback routing logic updates (Ticket 05)
- Circuit breaker integration (Ticket 04)
- Sideband cleanup ordering (Ticket 03)

---

## Context References

**Analysis**: [`docs/brain/sima-hardening/01-analysis.md`](./01-analysis.md)
- Section 4.1 (P0 Critical Hotspots): H1 - OrderId registration race
- Section 7.1 (Bug Registry Mapping): Compound Trap #2

**Approach**: [`docs/brain/sima-hardening/02-approach.md`](./02-approach.md)
- Section 1.2 (lines 106-187): Complete pre-submit registration lifecycle

---

## Implementation Instructions

### Step 1: Update SubmitAndRegisterFleetOrders

Locate `SubmitAndRegisterFleetOrders` in `V12_002.SIMA.Fleet.cs`.

Add BEFORE the `acct.Submit(orders)` call:

```csharp
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
        _orderIdToFsmMap.TryAdd(ord.OrderId, fleetEntryName, fsm.Generation);
    }
}
```

Add AFTER the `acct.Submit(orders)` call:

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

**Reference**: Approach doc section 1.2, lines 113-153

### Step 2: Add TryTransition Method to FollowerBracketFSM

Add to `FollowerBracketFSM` class in `V12_002.cs`:

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

**Reference**: Approach doc section 1.1, lines 82-93

---

## V12 DNA Guardrails

### Zero-Lock Compliance
- ✅ Uses `Interlocked.CompareExchange` for atomic state updates
- ❌ NO `lock()` statements permitted

### Zero-Allocation Compliance
- ✅ `_orderIdToFsmMap.TryAdd()` performs zero heap allocations
- ❌ NO `new` keyword in hot path

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
- [ ] `Pending=true` set before `acct.Submit()` call
- [ ] OrderId mappings registered before broker dispatch
- [ ] `Pending=false` cleared after successful submit
- [ ] Generation increments on state transitions

### Compilation
- [ ] Code compiles without errors in NinjaTrader IDE (F5)
- [ ] BUILD_TAG banner displays correctly

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" src/` returns ZERO matches

---

## Dependencies

**Blocks**: Ticket 05  
**Blocked By**: Ticket 01