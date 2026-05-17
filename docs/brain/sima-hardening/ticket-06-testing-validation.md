# Ticket 06: Testing & Validation

**Epic**: SIMA Subgraph Hardening  
**Phase**: Validation (Week 3)  
**Estimated Effort**: 3 hours  
**Risk Level**: LOW (test-only, no production code changes)

---

## Objective

Create comprehensive test coverage for the SIMA hardening changes, including FsCheck property tests for ABA immunity and stress tests for concurrency.

---

## Scope

### IN SCOPE
- Create `SimaFleetAbaPropertyTests.cs` with FsCheck properties
- Create `PhotonIntegrityStressTest.cs` for concurrent slot allocation
- Create `CircuitBreakerBehaviorTests.cs` for state machine transitions
- Add stress test scenarios to `scripts/test_stress.ps1`
- Document test execution in EXECUTION_GUIDE.md

### OUT OF SCOPE
- Performance benchmarking (defer to Phase 4)
- UI test automation

---

## Context References

**Analysis**: [`docs/brain/sima-hardening/01-analysis.md`](./01-analysis.md)
- Section 6 (Test Coverage Gaps): Lists missing concurrency and stress tests

**Approach**: [`docs/brain/sima-hardening/02-approach.md`](./02-approach.md)
- Section 6 (Testing Strategy): Complete test specifications

---

## Implementation Instructions

### Step 1: Create FsCheck Property Tests

Create `tests/SimaFleetAbaPropertyTests.cs`:

```csharp
// V12 Phase 8: SIMA ABA Immunity Property Tests
using FsCheck;
using FsCheck.Xunit;

public class SimaFleetAbaPropertyTests
{
    [Property]
    public Property PackedState_RoundTrip_Preserves_All_Fields()
    {
        return Prop.ForAll<byte, bool, long>((state, pending, gen) =>
        {
            // Constrain generation to 55 bits
            long constrainedGen = gen & ((1L << 55) - 1);
            
            long packed = FsmPackedState.Pack(state, pending, constrainedGen);
            FsmPackedState.Unpack(packed, out byte s2, out bool p2, out long g2);
            
            return s2 == state && p2 == pending && g2 == constrainedGen;
        });
    }
    
    [Property]
    public Property Generation_Never_Wraps_In_347_Years()
    {
        return Prop.ForAll<long>(gen =>
        {
            long constrainedGen = gen & ((1L << 55) - 1);
            long opsPerSec = 1_000_000;
            long secondsIn347Years = 347L * 365 * 24 * 3600;
            
            return constrainedGen < (opsPerSec * secondsIn347Years);
        });
    }
    
    [Property]
    public Property OrderIdMap_TryAdd_TryGet_Consistent()
    {
        return Prop.ForAll<string, string, long>((orderId, fsmKey, gen) =>
        {
            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(fsmKey))
                return true;
            
            var map = new ZeroAllocOrderIdMap(1024);
            bool added = map.TryAdd(orderId, fsmKey, gen);
            
            if (!added) return true;  // Table full, skip
            
            bool found = map.TryGet(orderId, out string retrievedKey, out long retrievedGen);
            
            return found && retrievedKey == fsmKey && retrievedGen == gen;
        });
    }
}
```

**Reference**: Approach doc section 6.1, lines 802-850

### Step 2: Create Stress Tests

Create `tests/PhotonIntegrityStressTest.cs`:

```csharp
// V12 Phase 8: Photon Ring Stress Test
using System.Threading;
using System.Threading.Tasks;

public class PhotonIntegrityStressTest
{
    [Fact]
    public async Task Concurrent_Slot_Allocation_No_Corruption()
    {
        var pool = new PhotonOrderPool(64);
        var ring = new SPSCRing<FleetDispatchSlot>(64);
        var sideband = new FleetDispatchSideband[64];
        
        int iterations = 1_000_000;
        int corruptionCount = 0;
        
        var producer = Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                int slotIdx = pool.Claim();
                if (slotIdx >= 0)
                {
                    var slot = new FleetDispatchSlot { /* ... */ };
                    sideband[slotIdx] = new FleetDispatchSideband { /* ... */ };
                    ring.TryEnqueue(slot);
                }
            }
        });
        
        var consumer = Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                if (ring.TryDequeue(out var slot))
                {
                    // Verify integrity
                    if (sideband[slot.PoolIndex].Account == null)
                        Interlocked.Increment(ref corruptionCount);
                    
                    // Clear sideband BEFORE release
                    sideband[slot.PoolIndex] = default;
                    Thread.MemoryBarrier();
                    pool.ReleaseByIndex(slot.PoolIndex);
                }
            }
        });
        
        await Task.WhenAll(producer, consumer);
        
        Assert.Equal(0, corruptionCount);
    }
}
```

**Reference**: Approach doc section 6.2, lines 851-917

### Step 3: Create Circuit Breaker Tests

Create `tests/CircuitBreakerBehaviorTests.cs`:

```csharp
// V12 Phase 8: Circuit Breaker State Machine Tests
public class CircuitBreakerBehaviorTests
{
    [Fact]
    public void CircuitBreaker_Opens_After_Threshold_Failures()
    {
        var cb = new SubmitCircuitBreaker();
        
        // Record 5 failures
        for (int i = 0; i < 5; i++)
            cb.RecordFailure();
        
        // Circuit should be open
        Assert.False(cb.AllowSubmit());
    }
    
    [Fact]
    public void CircuitBreaker_Transitions_To_HalfOpen_After_Cooldown()
    {
        var cb = new SubmitCircuitBreaker();
        
        // Open the circuit
        for (int i = 0; i < 5; i++)
            cb.RecordFailure();
        
        // Wait for cooldown (30 seconds)
        Thread.Sleep(31000);
        
        // Should allow one probe
        Assert.True(cb.AllowSubmit());
    }
    
    [Fact]
    public void CircuitBreaker_Resets_On_Successful_Probe()
    {
        var cb = new SubmitCircuitBreaker();
        
        // Open the circuit
        for (int i = 0; i < 5; i++)
            cb.RecordFailure();
        
        Thread.Sleep(31000);
        
        // Successful probe
        cb.AllowSubmit();
        cb.RecordSuccess();
        
        // Should be closed now
        Assert.True(cb.AllowSubmit());
    }
}
```

**Reference**: Approach doc section 6.3, lines 918-958

### Step 4: Update Stress Test Script

Add to `scripts/test_stress.ps1`:

```powershell
# SIMA Hardening Stress Tests
Write-Host "[STRESS] Running SIMA ABA property tests..."
dotnet test tests/SimaFleetAbaPropertyTests.cs --filter "Category=Property"

Write-Host "[STRESS] Running Photon integrity stress test..."
dotnet test tests/PhotonIntegrityStressTest.cs --filter "Category=Stress"

Write-Host "[STRESS] Running circuit breaker behavior tests..."
dotnet test tests/CircuitBreakerBehaviorTests.cs --filter "Category=Unit"
```

---

## V12 DNA Guardrails

### Zero-Lock Compliance
- ✅ Tests verify lock-free behavior
- ❌ NO `lock()` statements in test code

### Zero-Allocation Compliance
- ✅ Stress tests verify zero allocations in hot paths
- ❌ Test setup can allocate (not production code)

### ASCII-Only Compliance
- ✅ All string literals use ASCII characters only
- ❌ NO Unicode, emoji, or curly quotes

---

## Post-Edit Verification

```powershell
powershell -File .\deploy-sync.ps1
python scripts/complexity_audit.py
powershell -File .\scripts\test_stress.ps1
```

---

## Acceptance Criteria

### Functional
- [ ] FsCheck property tests pass 100 iterations
- [ ] Photon stress test completes 1M ops with zero corruption
- [ ] Circuit breaker tests verify all state transitions
- [ ] All tests pass in CI pipeline

### Compilation
- [ ] Test projects compile without errors
- [ ] Test runner executes all tests successfully

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" tests/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" tests/` returns ZERO matches

---

## Dependencies

**Blocks**: None (final validation ticket)  
**Blocked By**: Ticket 01, Ticket 02, Ticket 03, Ticket 04, Ticket 05