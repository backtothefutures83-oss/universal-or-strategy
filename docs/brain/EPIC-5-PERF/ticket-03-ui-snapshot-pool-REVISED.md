# [TICKET-03] UI Snapshot Object Pool - REVISED PLAN

**EPIC**: EPIC-5-PERF  
**Priority**: P4 (Performance Optimization)  
**Status**: PLANNING (Director Revision Required)  
**Estimated Effort**: 4 hours  

---

## DIRECTOR FEEDBACK ADDRESSED

### ✅ Issue 1: LivePosition TBD - RESOLVED
**Problem**: Original plan left nested UILiveTargetSnapshot[] handling as "TBD"  
**Solution**: In-place field updates with pre-allocated array (see Section 4.2)

### ✅ Issue 2: Config Reference Copy - REJECTED & FIXED
**Problem**: Original plan suggested "reference copy" which violates UI isolation  
**Solution**: Field-by-field deep copy into pre-allocated UIConfigSnapshot (see Section 4.1)

### ✅ Issue 3: Nested Lifecycle - SPECIFIED
**Problem**: Pre-warming strategy and ReturnSnapshot behavior undefined  
**Solution**: Complete pre-warming specification with nested object preservation (see Section 5)

---

## 1. OBJECTIVE

Replace `new UIStateSnapshot()` allocations in `PublishUiSnapshot()` with a lock-free object pool to eliminate 60+ allocations per second during active trading.

**Current Allocation Pattern** (V12_002.UI.Snapshot.cs:221-247):
```csharp
UIStateSnapshot snapshot = new UIStateSnapshot  // Allocation #1
{
    Config = BuildUiConfigSnapshot(mode),        // Allocation #2
    Compliance = BuildUiComplianceSnapshot(),    // Allocation #3
    LivePosition = BuildUiLivePositionSnapshot(), // Allocation #4 + nested array
};
```

**Target**: Zero allocations during snapshot process via pooled object reuse.

---

## 2. CURRENT CLASS STRUCTURE

### 2.1 UIStateSnapshot (V12_002.cs:102-127)
```csharp
public class UIStateSnapshot
{
    // Primitive fields (20 fields)
    public double EmaValue;
    public double AtrValue;
    public string StatusMessage;
    public long LastUpdateTicks;
    public double LastPrice;
    public MarketPosition MasterMarketPosition;
    public string Mode;
    public int TargetCount;
    public bool IsRmaModeActive;
    public bool IsTrendRmaMode;
    public bool IsRetestRmaMode;
    public int ConfigRevision;
    public double OrHigh;
    public double OrLow;
    public double OrRange;
    public double Ema9Value;
    public double Ema15Value;
    public double Ema30Value;
    public double Ema65Value;
    public double Ema200Value;
    
    // Nested objects (pre-allocated in constructor)
    public UIConfigSnapshot Config = new UIConfigSnapshot();
    public UIComplianceSnapshot Compliance = new UIComplianceSnapshot();
    public UILivePositionSnapshot LivePosition = new UILivePositionSnapshot();
}
```

### 2.2 UIConfigSnapshot (V12_002.cs:85-100)
```csharp
public class UIConfigSnapshot
{
    public double Target1Value;
    public double Target2Value;
    public double Target3Value;
    public double Target4Value;
    public double Target5Value;
    public TargetMode Target1Type;
    public TargetMode Target2Type;
    public TargetMode Target3Type;
    public TargetMode Target4Type;
    public TargetMode Target5Type;
    public double StopValue;
    public double MaxRiskValue;
    public string ChaseIfTouchPoints;
}
```

### 2.3 UIComplianceSnapshot (V12_002.cs:73-83)
```csharp
public class UIComplianceSnapshot
{
    public string AccountName;
    public double DailyProfit;
    public double TotalProfit;
    public int TradeCount;
    public int UniqueDays;
    public double MaxDrawdown;
    public double PayoutMinProfit;
    public double TrailingDrawdownLimit;
}
```

### 2.4 UILivePositionSnapshot (V12_002.cs:57-71)
```csharp
public class UILivePositionSnapshot
{
    public bool HasLivePosition;
    public string EntryName;
    public MarketPosition Direction;
    public double StopPrice;
    public UILiveTargetSnapshot[] Targets = new[]  // Pre-allocated array
    {
        new UILiveTargetSnapshot(),
        new UILiveTargetSnapshot(),
        new UILiveTargetSnapshot(),
        new UILiveTargetSnapshot(),
        new UILiveTargetSnapshot(),
    };
}
```

### 2.5 UILiveTargetSnapshot (V12_002.cs:48-55)
```csharp
public class UILiveTargetSnapshot
{
    public bool IsVisible;
    public double Price;
    public int RemainingContracts;
    public bool IsWorking;
}
```

---

## 3. POOL ARCHITECTURE

### 3.1 Pool Implementation
```csharp
// V12_002.cs (add to class-level fields)
private static readonly ConcurrentBag<UIStateSnapshot> _uiSnapshotPool = new ConcurrentBag<UIStateSnapshot>();
private const int PoolInitialSize = 4;
private const int PoolMaxSize = 8;
private static int _pooledSnapshotCount = 0;
```

### 3.2 Pool Operations

**GetSnapshot()**: Acquire from pool or create new
```csharp
private UIStateSnapshot GetPooledSnapshot()
{
    if (_uiSnapshotPool.TryTake(out UIStateSnapshot snapshot))
    {
        Interlocked.Decrement(ref _pooledSnapshotCount);
        return snapshot;
    }
    
    // Pool exhausted - create new instance with nested objects pre-allocated
    return new UIStateSnapshot();
}
```

**ReturnSnapshot()**: Return to pool (preserve nested objects)
```csharp
private void ReturnPooledSnapshot(UIStateSnapshot snapshot)
{
    if (snapshot == null)
        return;
    
    // CRITICAL: Do NOT null out nested objects - keep them allocated for reuse
    // Only clear primitive fields and string references
    
    ClearSnapshotForReuse(snapshot);
    
    int currentCount = Volatile.Read(ref _pooledSnapshotCount);
    if (currentCount < PoolMaxSize)
    {
        _uiSnapshotPool.Add(snapshot);
        Interlocked.Increment(ref _pooledSnapshotCount);
    }
    // If pool is full, let GC collect the snapshot
}
```

---

## 4. FIELD-BY-FIELD MAPPING STRATEGY

### 4.1 UIConfigSnapshot Deep Copy (DIRECTOR FIX: No Reference Copy)

**Source**: `BuildUiConfigSnapshot()` return value  
**Target**: Pre-allocated `snapshot.Config` instance  
**Method**: Field-by-field assignment (13 fields)

```csharp
private void UpdateConfigSnapshot(UIConfigSnapshot target, string mode)
{
    // CRITICAL: Deep copy into pre-allocated target, NOT reference assignment
    target.Target1Value = Target1Value;
    target.Target2Value = Target2Value;
    target.Target3Value = Target3Value;
    target.Target4Value = Target4Value;
    target.Target5Value = Target5Value;
    target.Target1Type = T1Type;
    target.Target2Type = T2Type;
    target.Target3Type = T3Type;
    target.Target4Type = T4Type;
    target.Target5Type = T5Type;
    target.StopValue = string.Equals(mode, "RMA", StringComparison.OrdinalIgnoreCase)
        ? RMAStopATRMultiplier
        : StopMultiplier;
    target.MaxRiskValue = MaxRiskAmount;
    target.ChaseIfTouchPoints = string.IsNullOrEmpty(ChaseIfTouchPoints) ? "0" : ChaseIfTouchPoints;
}
```

### 4.2 UILivePositionSnapshot In-Place Update (DIRECTOR FIX: TBD Resolved)

**Source**: `BuildUiLivePositionSnapshot()` logic  
**Target**: Pre-allocated `snapshot.LivePosition` instance  
**Method**: In-place field updates + nested array reuse

```csharp
private void UpdateLivePositionSnapshot(UILivePositionSnapshot target)
{
    // Reset state
    target.HasLivePosition = false;
    target.EntryName = null;
    target.Direction = MarketPosition.Flat;
    target.StopPrice = 0;
    
    // Clear all target slots (reuse existing array instances)
    for (int i = 0; i < 5; i++)
    {
        target.Targets[i].IsVisible = false;
        target.Targets[i].Price = 0;
        target.Targets[i].RemainingContracts = 0;
        target.Targets[i].IsWorking = false;
    }
    
    // Find master position
    PositionInfo masterPos;
    string entryName;
    if (!FindMasterPosition(out masterPos, out entryName))
        return;
    
    // Update live position fields
    target.HasLivePosition = true;
    target.EntryName = entryName;
    target.Direction = masterPos.Direction;
    
    // Update target snapshots (in-place, reusing array elements)
    for (int targetNum = 1; targetNum <= 5; targetNum++)
    {
        UILiveTargetSnapshot targetSlot = target.Targets[targetNum - 1];
        bool isVisible = targetNum <= masterPos.InitialTargetCount && !IsTargetFilled(masterPos, targetNum);
        targetSlot.IsVisible = isVisible;
        
        if (!isVisible)
            continue;
        
        var targetDict = GetTargetOrdersDictionary(targetNum);
        Order targetOrder = null;
        if (targetDict != null)
            targetDict.TryGetValue(entryName, out targetOrder);
        
        double price = GetTargetPrice(masterPos, targetNum);
        if (targetOrder != null && targetOrder.LimitPrice > 0)
            price = targetOrder.LimitPrice;
        
        int contracts = GetTargetContracts(masterPos, targetNum);
        int filled = GetTargetFilledQuantity(masterPos, targetNum);
        
        targetSlot.Price = price;
        targetSlot.RemainingContracts = Math.Max(0, contracts - filled);
        targetSlot.IsWorking = targetOrder != null &&
            (targetOrder.OrderState == OrderState.Working || targetOrder.OrderState == OrderState.Accepted);
    }
    
    // Update stop snapshot
    Order stopOrder = null;
    if (stopOrders != null)
        stopOrders.TryGetValue(entryName, out stopOrder);
    
    target.StopPrice = masterPos.CurrentStopPrice;
    if (stopOrder != null && stopOrder.StopPrice > 0)
        target.StopPrice = stopOrder.StopPrice;
}
```

### 4.3 UIComplianceSnapshot Deep Copy

**Source**: `BuildUiComplianceSnapshot()` return value  
**Target**: Pre-allocated `snapshot.Compliance` instance  
**Method**: Field-by-field assignment (8 fields)

```csharp
private void UpdateComplianceSnapshot(UIComplianceSnapshot target)
{
    string accountName = Account != null ? Account.Name : "--";
    target.AccountName = accountName;
    target.DailyProfit = accountDailyProfit.TryGetValue(accountName, out double daily) ? daily : 0;
    target.TotalProfit = accountTotalProfit.TryGetValue(accountName, out double total) ? total : 0;
    target.TradeCount = accountTradeCount.TryGetValue(accountName, out int trades) ? trades : 0;
    target.UniqueDays = GetUniqueTradingDays(accountName);
    target.MaxDrawdown = accountMaxDrawdown.TryGetValue(accountName, out double maxDd) ? maxDd : 0;
    target.PayoutMinProfit = PayoutMinProfit;
    target.TrailingDrawdownLimit = TrailingDrawdownLimit;
}
```

---

## 5. PRE-WARMING & LIFECYCLE (DIRECTOR FIX: Complete Specification)

### 5.1 Pre-Warming Strategy

**When**: During `OnStateChange(State.DataLoaded)`  
**Count**: 4 instances (PoolInitialSize)  
**Structure**: Each instance has nested objects fully allocated

```csharp
private void PreWarmSnapshotPool()
{
    for (int i = 0; i < PoolInitialSize; i++)
    {
        UIStateSnapshot warmInstance = new UIStateSnapshot();
        // Nested objects already allocated by constructor:
        // - warmInstance.Config (new UIConfigSnapshot())
        // - warmInstance.Compliance (new UIComplianceSnapshot())
        // - warmInstance.LivePosition (new UILivePositionSnapshot())
        //   - warmInstance.LivePosition.Targets[0-4] (5 pre-allocated UILiveTargetSnapshot)
        
        _uiSnapshotPool.Add(warmInstance);
        Interlocked.Increment(ref _pooledSnapshotCount);
    }
}
```

### 5.2 ReturnSnapshot Nested Object Preservation

**CRITICAL RULE**: `ReturnPooledSnapshot()` MUST NOT null out nested objects.

```csharp
private void ClearSnapshotForReuse(UIStateSnapshot snapshot)
{
    // Clear primitive fields
    snapshot.EmaValue = 0;
    snapshot.AtrValue = 0;
    snapshot.StatusMessage = null;  // String reference cleared
    snapshot.LastUpdateTicks = 0;
    snapshot.LastPrice = 0;
    snapshot.MasterMarketPosition = MarketPosition.Flat;
    snapshot.Mode = null;  // String reference cleared
    snapshot.TargetCount = 0;
    snapshot.IsRmaModeActive = false;
    snapshot.IsTrendRmaMode = false;
    snapshot.IsRetestRmaMode = false;
    snapshot.ConfigRevision = 0;
    snapshot.OrHigh = 0;
    snapshot.OrLow = 0;
    snapshot.OrRange = 0;
    snapshot.Ema9Value = 0;
    snapshot.Ema15Value = 0;
    snapshot.Ema30Value = 0;
    snapshot.Ema65Value = 0;
    snapshot.Ema200Value = 0;
    
    // CRITICAL: Do NOT null out nested objects - they remain allocated
    // snapshot.Config = null;        // BANNED
    // snapshot.Compliance = null;    // BANNED
    // snapshot.LivePosition = null;  // BANNED
    
    // Nested objects will be overwritten in-place during next use
}
```

### 5.3 Lifecycle Flow

```
1. OnStateChange(State.DataLoaded)
   └─> PreWarmSnapshotPool() creates 4 instances with nested objects

2. PublishUiSnapshot() (60x/sec during trading)
   ├─> oldSnapshot = _uiSnapshot (capture previous)
   ├─> snapshot = GetPooledSnapshot() (acquire from pool or create)
   ├─> UpdateConfigSnapshot(snapshot.Config, mode) (in-place)
   ├─> UpdateComplianceSnapshot(snapshot.Compliance) (in-place)
   ├─> UpdateLivePositionSnapshot(snapshot.LivePosition) (in-place)
   ├─> Update primitive fields (20 fields)
   ├─> _uiSnapshot = snapshot (publish)
   └─> ReturnPooledSnapshot(oldSnapshot) (return previous to pool)

3. ReturnPooledSnapshot()
   ├─> ClearSnapshotForReuse() (clear primitives, preserve nested objects)
   └─> Add to pool if count < PoolMaxSize
```

---

## 6. REFACTORED PublishUiSnapshot()

```csharp
private void PublishUiSnapshot()
{
    var probe = LatencyProbe.Start();
    
    try
    {
        // Capture old snapshot for return to pool
        UIStateSnapshot oldSnapshot = _uiSnapshot;
        
        // Acquire snapshot from pool (zero allocation if pool has instances)
        UIStateSnapshot snapshot = GetPooledSnapshot();
        
        // Update nested objects IN-PLACE (zero allocation)
        string mode = GetCurrentPanelMode();
        UpdateConfigSnapshot(snapshot.Config, mode);
        UpdateComplianceSnapshot(snapshot.Compliance);
        UpdateLivePositionSnapshot(snapshot.LivePosition);
        
        // Update primitive fields
        snapshot.EmaValue = SafeEmaValue(ema9);
        snapshot.AtrValue = currentATR > 0 ? currentATR : 0;
        snapshot.LastUpdateTicks = DateTime.UtcNow.Ticks;
        snapshot.LastPrice = lastKnownPrice;
        snapshot.Mode = mode;
        snapshot.TargetCount = Math.Max(1, Math.Min(5, activeTargetCount));
        snapshot.IsRmaModeActive = isRMAModeActive;
        snapshot.IsTrendRmaMode = isTrendRmaMode;
        snapshot.IsRetestRmaMode = isRetestRmaMode;
        snapshot.ConfigRevision = Volatile.Read(ref _uiConfigRevision);
        snapshot.OrHigh = sessionHigh != double.MinValue ? sessionHigh : 0;
        snapshot.OrLow = sessionLow != double.MaxValue ? sessionLow : 0;
        snapshot.OrRange = (sessionHigh != double.MinValue && sessionLow != double.MaxValue)
            ? (sessionHigh - sessionLow) : 0;
        snapshot.Ema9Value = snapshot.EmaValue;
        snapshot.Ema15Value = SafeEmaValue(ema15);
        snapshot.Ema30Value = SafeEmaValue(ema30);
        snapshot.Ema65Value = SafeEmaValue(ema65);
        snapshot.Ema200Value = SafeEmaValue(ema200);
        
        snapshot.MasterMarketPosition = snapshot.LivePosition != null && snapshot.LivePosition.HasLivePosition
            ? snapshot.LivePosition.Direction
            : (Position != null ? Position.MarketPosition : MarketPosition.Flat);
        snapshot.StatusMessage = BuildUiStatusMessage(snapshot);
        
        // Publish new snapshot
        _uiSnapshot = snapshot;
        
        // Return old snapshot to pool
        if (oldSnapshot != null)
            ReturnPooledSnapshot(oldSnapshot);
    }
    finally
    {
        probe = probe.Stop();
        _histPublishUiSnapshot.Record(probe);
    }
}
```

---

## 7. THREAD SAFETY ANALYSIS

### 7.1 Pool Access
- **ConcurrentBag<T>**: Lock-free for TryTake/Add operations
- **Interlocked**: Atomic counter updates for _pooledSnapshotCount

### 7.2 Snapshot Publishing
- **Single Writer**: Only strategy thread calls `PublishUiSnapshot()`
- **Multiple Readers**: UI thread reads `_uiSnapshot` via `GetUiSnapshot()`
- **Volatile Read**: `_uiSnapshot` field must be volatile for visibility

### 7.3 Race Condition Prevention
```csharp
// V12_002.cs (update field declaration)
private volatile UIStateSnapshot _uiSnapshot;  // Ensure visibility across threads
```

---

## 8. VERIFICATION STRATEGY

### 8.1 Allocation Verification
```csharp
// Before: Measure baseline allocations
long gen0Before = GC.CollectionCount(0);
for (int i = 0; i < 1000; i++)
    PublishUiSnapshot();
long gen0After = GC.CollectionCount(0);
Print($"Gen0 collections: {gen0After - gen0Before}");
```

### 8.2 Pool Health Metrics
```csharp
private void LogPoolHealth()
{
    int pooled = Volatile.Read(ref _pooledSnapshotCount);
    Print($"[POOL] Snapshots in pool: {pooled}/{PoolMaxSize}");
}
```

### 8.3 Functional Verification
- UI panel displays correct values after pooling
- No stale data from previous snapshots
- Nested objects update correctly

---

## 9. ROLLBACK PLAN

If pooling causes issues:
1. Remove pool operations from `PublishUiSnapshot()`
2. Restore original `new UIStateSnapshot { ... }` pattern
3. Keep helper methods (UpdateConfigSnapshot, etc.) for future use

---

## 10. SUCCESS CRITERIA

✅ **Zero new allocations** during `PublishUiSnapshot()` when pool has instances  
✅ **All nested objects pre-allocated** and reused across snapshots  
✅ **Field-by-field deep copy** for Config, Compliance (no reference copies)  
✅ **In-place array updates** for LivePosition.Targets (no new arrays)  
✅ **Thread-safe** for UI consumption (volatile _uiSnapshot)  
✅ **Pool pre-warmed** with 4 instances during DataLoaded  
✅ **ReturnSnapshot preserves** nested object allocations  
✅ **Functional correctness** verified via UI panel display  

---

## 11. IMPLEMENTATION CHECKLIST

- [ ] Add pool fields to V12_002.cs
- [ ] Implement GetPooledSnapshot()
- [ ] Implement ReturnPooledSnapshot()
- [ ] Implement ClearSnapshotForReuse()
- [ ] Implement UpdateConfigSnapshot()
- [ ] Implement UpdateComplianceSnapshot()
- [ ] Implement UpdateLivePositionSnapshot()
- [ ] Implement PreWarmSnapshotPool()
- [ ] Call PreWarmSnapshotPool() in OnStateChange(State.DataLoaded)
- [ ] Refactor PublishUiSnapshot() to use pool
- [ ] Mark _uiSnapshot as volatile
- [ ] Remove BuildUiConfigSnapshot() (replaced by UpdateConfigSnapshot)
- [ ] Remove BuildUiComplianceSnapshot() (replaced by UpdateComplianceSnapshot)
- [ ] Remove BuildUiLivePositionSnapshot() (replaced by UpdateLivePositionSnapshot)
- [ ] Verify zero allocations via GC metrics
- [ ] Verify UI panel correctness
- [ ] Run stress test (1000 iterations)
- [ ] Document pool health in telemetry

---

**END OF REVISED PLAN**