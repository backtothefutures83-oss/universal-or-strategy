# Ticket 04: Global Circuit Breaker

**Epic**: SIMA Subgraph Hardening  
**Phase**: Protection (Week 2)  
**Estimated Effort**: 2 hours  
**Risk Level**: MEDIUM (adds failure handling logic)

---

## Objective

Implement global submit circuit breaker to prevent infinite retry loops during broker disconnects (Solution 4).

---

## Scope

### IN SCOPE
- Create `SubmitCircuitBreaker` class with lock-free FSM
- Add circuit breaker state transitions (Closed → HalfOpen → Open)
- Integrate circuit breaker checks in `SubmitAndRegisterFleetOrders`
- Add success/failure recording after submit attempts
- Initialize circuit breaker in `OnStateChange`

### OUT OF SCOPE
- Telemetry/monitoring integration (defer to Phase 3)
- UI indicators for circuit breaker state

---

## Context References

**Analysis**: [`docs/brain/sima-hardening/01-analysis.md`](./01-analysis.md)
- Section 4.1 (P0 Critical Hotspots): H5 - Missing circuit breaker
- Section 7.1 (Bug Registry Mapping): Compound Trap #5

**Approach**: [`docs/brain/sima-hardening/02-approach.md`](./02-approach.md)
- Section 1.4 (lines 340-504): Complete circuit breaker implementation

---

## Implementation Instructions

### Step 1: Create SubmitCircuitBreaker Class

Add to `V12_002.cs` (after `ZeroAllocOrderIdMap`):

```csharp
// V12 Phase 8: Global Submit Circuit Breaker
private sealed class SubmitCircuitBreaker
{
    private long _state;  // Packed: [State: 2 bits][FailureCount: 62 bits]
    private const int StateShift = 62;
    private const long FailureMask = (1L << 62) - 1;
    
    private const int STATE_CLOSED = 0;
    private const int STATE_HALF_OPEN = 1;
    private const int STATE_OPEN = 2;
    
    private long _openUntilTicks;
    private const int FailureThreshold = 5;
    private const long CooldownTicks = 30L * TimeSpan.TicksPerSecond;  // 30 seconds
    
    public bool AllowSubmit()
    {
        long snapshot = Interlocked.Read(ref _state);
        int state = (int)(snapshot >> StateShift);
        long failures = snapshot & FailureMask;
        long nowTicks = DateTime.UtcNow.Ticks;
        
        if (state == STATE_OPEN)
        {
            long openUntil = Volatile.Read(ref _openUntilTicks);
            if (nowTicks < openUntil)
                return false;
            
            return TryHalfOpen(snapshot);
        }
        
        if (state == STATE_HALF_OPEN && failures > 0)
            return false;
        
        return true;
    }
    
    public void RecordSuccess()
    {
        long snapshot;
        do
        {
            snapshot = Interlocked.Read(ref _state);
            int state = (int)(snapshot >> StateShift);
            
            if (state == STATE_HALF_OPEN)
            {
                long next = ((long)STATE_CLOSED << StateShift) | 0L;
                if (Interlocked.CompareExchange(ref _state, next, snapshot) == snapshot)
                    return;
            }
            else if (state == STATE_CLOSED)
            {
                long next = ((long)STATE_CLOSED << StateShift) | 0L;
                if (Interlocked.CompareExchange(ref _state, next, snapshot) == snapshot)
                    return;
            }
            else
            {
                return;
            }
        }
        while (true);
    }
    
    public void RecordFailure()
    {
        long snapshot;
        do
        {
            snapshot = Interlocked.Read(ref _state);
            int state = (int)(snapshot >> StateShift);
            long failures = (snapshot & FailureMask) + 1;
            
            int nextState = state;
            if (failures >= FailureThreshold)
            {
                nextState = STATE_OPEN;
                Volatile.Write(ref _openUntilTicks, 
                    DateTime.UtcNow.Ticks + CooldownTicks);
            }
            else if (state == STATE_HALF_OPEN)
            {
                nextState = STATE_OPEN;
                Volatile.Write(ref _openUntilTicks, 
                    DateTime.UtcNow.Ticks + CooldownTicks);
            }
            
            long next = ((long)nextState << StateShift) | failures;
            if (Interlocked.CompareExchange(ref _state, next, snapshot) == snapshot)
                return;
        }
        while (true);
    }
    
    private bool TryHalfOpen(long snapshot)
    {
        long next = ((long)STATE_HALF_OPEN << StateShift) | 0L;
        return Interlocked.CompareExchange(ref _state, next, snapshot) == snapshot;
    }
    
    public string GetDiagnostics()
    {
        long snapshot = Interlocked.Read(ref _state);
        int state = (int)(snapshot >> StateShift);
        long failures = snapshot & FailureMask;
        
        string stateName = state == STATE_CLOSED ? "Closed" :
                          state == STATE_HALF_OPEN ? "HalfOpen" : "Open";
        
        return string.Format("CircuitBreaker: {0} (failures={1})", stateName, failures);
    }
}
```

**Reference**: Approach doc section 1.4, lines 347-463

### Step 2: Add Circuit Breaker Field

Add to `V12_002.cs` (near other infrastructure fields):

```csharp
private SubmitCircuitBreaker _submitCircuitBreaker;
```

Initialize in `OnStateChange` (State.SetDefaults section):

```csharp
_submitCircuitBreaker = new SubmitCircuitBreaker();
```

### Step 3: Integrate in SubmitAndRegisterFleetOrders

Locate `SubmitAndRegisterFleetOrders` in `V12_002.SIMA.Fleet.cs`.

Add at the START of the method:

```csharp
// Check circuit breaker BEFORE submit
if (!_submitCircuitBreaker.AllowSubmit())
{
    Print("[CIRCUIT_BREAKER] Submit blocked (circuit open)");
    throw new InvalidOperationException("Circuit breaker open");
}
```

Wrap the submit call in try/catch:

```csharp
try
{
    // ... pre-submit registration (Ticket 02) ...
    
    acct.Submit(orders);
    
    // Record success
    _submitCircuitBreaker.RecordSuccess();
    
    // ... post-submit cleanup (Ticket 02) ...
}
catch (Exception ex)
{
    // Record failure
    _submitCircuitBreaker.RecordFailure();
    throw;
}
```

**Reference**: Approach doc section 1.4, lines 467-495

---

## V12 DNA Guardrails

### Zero-Lock Compliance
- ✅ Uses `Interlocked.CompareExchange` for state transitions
- ✅ Uses `Volatile.Read/Write` for timestamp
- ❌ NO `lock()` statements permitted

### Zero-Allocation Compliance
- ✅ Circuit breaker state is a single `long` field
- ✅ No heap allocations in hot path methods
- ❌ NO `new` keyword in `AllowSubmit`/`RecordSuccess`/`RecordFailure`

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
- [ ] Circuit breaker blocks submits after 5 consecutive failures
- [ ] Circuit breaker transitions to HalfOpen after 30-second cooldown
- [ ] Single successful probe in HalfOpen resets to Closed
- [ ] Single failed probe in HalfOpen returns to Open
- [ ] `GetDiagnostics()` returns current state and failure count

### Compilation
- [ ] Code compiles without errors in NinjaTrader IDE (F5)
- [ ] BUILD_TAG banner displays correctly

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" src/` returns ZERO matches

---

## Dependencies

**Blocks**: None (independent protection layer)  
**Blocked By**: None