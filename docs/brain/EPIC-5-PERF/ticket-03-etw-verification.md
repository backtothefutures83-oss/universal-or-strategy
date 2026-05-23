# ETW Trace Verification Guide - Ticket 03 UI Snapshot Pool

**EPIC**: EPIC-5-PERF  
**Ticket**: T03 - UI Snapshot Object Pool  
**Objective**: Verify zero allocations in PublishUiSnapshot after pooling implementation

---

## CRITICAL REQUIREMENT

**Director Mandate**: "Verify the final result with an ETW trace to confirm that PublishUiSnapshot no longer appears in the allocation profile."

---

## ETW Trace Collection Steps

### 1. Prerequisites

- **PerfView**: Download from https://github.com/microsoft/perfview/releases
- **Admin Rights**: ETW tracing requires elevated privileges
- **Active Trading Session**: Run during live market hours for realistic allocation patterns

### 2. Start ETW Collection

```powershell
# Launch PerfView as Administrator
# Navigate to: Collect > Collect

# Settings:
# - Data File: V12_UISnapshot_Baseline.etl
# - Zip: Checked
# - Merge: Checked
# - Circular MB: 1000
# - Providers: .NET
```

**Command Line Alternative**:
```powershell
PerfView.exe /DataFile:V12_UISnapshot_Baseline.etl /Zip:true /Merge:true /CircularMB:1000 collect
```

### 3. Trigger Allocation Activity

1. **Start NinjaTrader** with V12_002 strategy enabled
2. **Wait for State.DataLoaded** (pool pre-warming occurs here)
3. **Run for 60 seconds** during active trading (60+ PublishUiSnapshot calls)
4. **Stop Collection** in PerfView

### 4. Analyze Allocation Profile

#### Open GC Heap Allocations View
```
PerfView > Memory > GC Heap Alloc Stacks
```

#### Filter to V12_002 Strategy
```
IncPats: V12_002
ExcPats: System.*;Microsoft.*
```

#### Search for PublishUiSnapshot
```
Find: PublishUiSnapshot
```

### 5. Success Criteria

**BEFORE Pooling** (Baseline):
```
V12_002.PublishUiSnapshot
  ├─ UIStateSnapshot..ctor          [60+ allocations]
  ├─ UIConfigSnapshot..ctor          [60+ allocations]
  ├─ UIComplianceSnapshot..ctor      [60+ allocations]
  └─ UILivePositionSnapshot..ctor    [60+ allocations]
```

**AFTER Pooling** (Target):
```
V12_002.PublishUiSnapshot
  └─ [NO ALLOCATIONS] or [<4 allocations during pool warm-up only]
```

**Verification Gate**:
- ✅ **PASS**: PublishUiSnapshot does NOT appear in allocation stacks during steady-state (after first 5 seconds)
- ❌ **FAIL**: PublishUiSnapshot shows >4 allocations after pool warm-up

---

## Alternative Verification: GC Collection Counts

If ETW is unavailable, use GC metrics:

```csharp
// Add to V12_002.UI.Snapshot.cs (temporary diagnostic)
private void VerifyPoolEffectiveness()
{
    long gen0Before = GC.CollectionCount(0);
    long gen1Before = GC.CollectionCount(1);
    
    for (int i = 0; i < 1000; i++)
        PublishUiSnapshot();
    
    long gen0After = GC.CollectionCount(0);
    long gen1After = GC.CollectionCount(1);
    
    Print($"[POOL-VERIFY] Gen0: {gen0After - gen0Before}, Gen1: {gen1After - gen1Before}");
    Print($"[POOL-VERIFY] Pool Health: {GetPoolHealthMetrics()}");
}
```

**Expected Results**:
- **Gen0 Collections**: 0-1 (vs 5-10 without pooling)
- **Gen1 Collections**: 0 (vs 1-2 without pooling)
- **Pool Fallbacks**: 0 (all snapshots served from pool)

---

## Rollback Trigger

If ETW trace shows **>10 allocations** in PublishUiSnapshot during steady-state:

1. Revert `src/V12_002.UI.Snapshot.cs` to original `new UIStateSnapshot { ... }` pattern
2. Remove `src/V12_002.UI.SnapshotPool.cs`
3. Remove `PreWarmSnapshotPool()` call from `V12_002.Lifecycle.cs`
4. File incident report with ETW trace attached

---

## Post-Verification Actions

1. **Archive ETW Trace**: Store `.etl.zip` file in `docs/brain/EPIC-5-PERF/traces/`
2. **Update Ticket Status**: Mark T03 as VERIFIED in `ticket-03-ui-snapshot-pool-REVISED.md`
3. **Log Pool Metrics**: Add `GetPoolHealthMetrics()` to telemetry dashboard
4. **Proceed to T04**: Begin next performance optimization ticket

---

**END OF ETW VERIFICATION GUIDE**