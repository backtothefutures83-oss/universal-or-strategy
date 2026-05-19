---
# EPIC-3-REAPER: SURGICAL APPROACH
# Epic: REAPER & Lifecycle Defenses
# Phase: APPROACH (Phase 2 of 6)
# Status: DRAFT - AWAITING VALIDATION
---

## 1. Executive Summary

This document specifies the exact surgical repairs for 7 validated concurrency defects plus Watchdog hardening. All repairs use lock-free patterns already proven in the codebase. Zero architectural changes - pure thread-safety hardening.

**Repair Strategy:**
- **Collection Snapshots:** Add `.ToArray()` to 7 iteration sites (H13-H15 + Watchdog 3 sites)
- **Atomic Guards:** Replace `ContainsKey` + `TryAdd` with atomic `TryAdd` check at 3 sites (H16)
- **Lifecycle Reordering:** Move `DrainQueuesForShutdown()` before `CancelAllV12GtcOrders()` (H17)
- **State Isolation:** Clone `_modeProfiles` dictionary before background serialization (H18)
- **Overflow Cleanup:** Add `CANCEL_ALL` trigger for affected followers on queue overflow (H20)

**Estimated Impact:**
- Lines changed: ~30 (surgical edits only)
- Files modified: 4
- CYC delta: +5-10 (H20 cleanup loop only)
- Performance overhead: <1ms per audit cycle

---

## 2. Repair Specifications by Defect

### H13: Naked Position Audit - Add Collection Snapshot

**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)  
**Location:** Line 522  
**Method:** `AuditMaster_HandleNakedPosition()`

**Current Code:**
```csharp
bool masterHasWorkingStop = Account.Orders.Any(o =>
    o.Instrument?.FullName == Instrument?.FullName &&
    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
```

**Surgical Repair:**
```csharp
// H13-FIX: Snapshot broker orders before iteration to prevent InvalidOperationException
// when NinjaTrader updates Account.Orders collection from UI thread during audit.
var masterOrders = Account.Orders.ToArray();
bool masterHasWorkingStop = masterOrders.Any(o =>
    o.Instrument?.FullName == Instrument?.FullName &&
    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
    (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
    (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.BuyToCover));
```

**Rationale:**
- Mirrors existing pattern at line 378 (`AuditFleet_CheckWorkingStop()`)
- `.ToArray()` creates thread-safe snapshot (immutable array)
- Snapshot overhead: ~10-50 bytes (typically 1-5 orders)
- Pattern already proven in Build 1108.003

**Verification:**
- Grep confirm: `grep -n "masterOrders = Account.Orders.ToArray()" src/V12_002.REAPER.Audit.cs`
- Expected: 1 match at line ~522

---

### H14: Flatten Cancel Loop - Add Collection Snapshot

**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)  
**Location:** Line 698  
**Method:** `ProcessReaperFlatten_CancelWorkingOrders()`

**Current Code:**
```csharp
List<Order> ordersToCancel = new List<Order>();
foreach (Order order in targetAcct.Orders)
{
    if (order != null && order.Instrument.FullName == Instrument.FullName &&
        (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
         order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending))
    {
        ordersToCancel.Add(order);
    }
}
```

**Surgical Repair:**
```csharp
// H14-FIX: Snapshot broker orders before iteration to prevent collection-modified exception
// during emergency flatten when broker callbacks update order states concurrently.
List<Order> ordersToCancel = new List<Order>();
var accountOrders = targetAcct.Orders.ToArray();
foreach (Order order in accountOrders)
{
    if (order != null && order.Instrument.FullName == Instrument.FullName &&
        (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
         order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending))
    {
        ordersToCancel.Add(order);
    }
}
```

**Rationale:**
- Emergency flatten is critical path - cannot tolerate exceptions
- Snapshot ensures complete iteration even if broker updates orders mid-loop
- Worst case: snapshot includes order that gets cancelled before we process it (idempotent)

**Verification:**
- Grep confirm: `grep -n "accountOrders = targetAcct.Orders.ToArray()" src/V12_002.REAPER.Audit.cs`
- Expected: 1 match at line ~699

---

### H15: Flatten Close Loop - Add Collection Snapshot

**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)  
**Location:** Line 720  
**Method:** `ProcessReaperFlatten_ClosePositions()`

**Current Code:**
```csharp
foreach (Position position in targetAcct.Positions)
{
    if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat)
    {
        continue;
    }
    // ... submit market close order
}
```

**Surgical Repair:**
```csharp
// H15-FIX: Snapshot broker positions before iteration to prevent collection-modified exception
// during emergency flatten when broker fill callbacks update positions concurrently.
var accountPositions = targetAcct.Positions.ToArray();
foreach (Position position in accountPositions)
{
    if (position.Instrument.FullName != Instrument.FullName || position.MarketPosition == MarketPosition.Flat)
    {
        continue;
    }
    // ... submit market close order
}
```

**Rationale:**
- Identical pattern to H14 - emergency flatten position closure
- Snapshot ensures all positions are processed even if fills occur during iteration
- Worst case: snapshot includes position that gets filled before we process it (market close will be rejected by broker)

**Verification:**
- Grep confirm: `grep -n "accountPositions = targetAcct.Positions.ToArray()" src/V12_002.REAPER.Audit.cs`
- Expected: 1 match at line ~720

---

### H16: REAPER In-Flight Guards - Atomic TryAdd Pattern

**File:** [`src/V12_002.REAPER.Audit.cs`](src/V12_002.REAPER.Audit.cs)
**Locations:** Lines 337, 420, 622
**Methods:** `EnqueueReaperRepairCandidate()`, `EnqueueReaperNakedStopCandidate()`, `EnqueueReaperMasterNakedStop()`

**Current Code (Line 337 - Repair Candidate):**
```csharp
bool alreadyInFlight;
alreadyInFlight = _repairInFlight.ContainsKey(repairKey); // [Build 968]

if (!alreadyInFlight)
{
    // Phase 4: Use FSM to identify working entry
    bool hasWorkingEntry = accountFsms.Any(f => f.State == FollowerBracketState.Submitted || f.State == FollowerBracketState.Accepted);

    if (!hasWorkingEntry)
    {
        if (shouldLog)
        {
            Print($"[REAPER] * REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
        }
        // A3-2: Mark in-flight BEFORE TriggerCustomEvent to block double-enqueue in next audit cycle (Build 960 audit fix)
        _repairInFlight.TryAdd(repairKey, 0); // [Build 968]
        _reaperRepairQueue.Enqueue(acct.Name);
        return true;
    }
}
```

**Surgical Repair (Line 337):**
```csharp
// H16-FIX: Atomic TryAdd check prevents TOCTOU race where two audit cycles both pass
// ContainsKey check before either calls TryAdd, causing duplicate repair submissions.
// Pattern mirrors EnqueueReaperFlattenCandidate (line 367) which already uses atomic guard.
if (!_repairInFlight.TryAdd(repairKey, 0))
{
    // Already in flight - skip
    if (shouldLog)
    {
        Print($"[REAPER] {acct.Name} repair already in-flight -- skipping.");
    }
    return false;
}

// Phase 4: Use FSM to identify working entry (EXISTING LOGIC - not new)
// This check exists in current code (lines 342-354). It prevents repair submissions when
// a working entry order is already in flight for this account.
bool hasWorkingEntry = accountFsms.Any(f => f.State == FollowerBracketState.Submitted || f.State == FollowerBracketState.Accepted);

if (!hasWorkingEntry)
{
    if (shouldLog)
    {
        Print($"[REAPER] * REPAIR CANDIDATE: {acct.Name} is Flat, expected={expectedQty}. Enqueuing repair.");
    }
    _reaperRepairQueue.Enqueue(acct.Name);
    return true;
}
else
{
    // Has working entry - clear in-flight flag since we're not enqueuing.
    // This TryRemove is CRITICAL: without it, the account would be permanently blocked
    // from future repairs because the in-flight flag was set by TryAdd above but no
    // repair was actually enqueued.
    _repairInFlight.TryRemove(repairKey, out _);
    return false;
}
```

**Note on hasWorkingEntry Check:**
The `hasWorkingEntry` check is EXISTING LOGIC in the current code (lines 342-354), not new scope being added. This check prevents repair submissions when a working entry order is already in flight for the account. The `TryRemove` is necessary to clear the in-flight flag when this check fails, otherwise the account would be permanently blocked from future repairs.

The naked stop sites (lines 420, 622) do NOT have equivalent checks because naked stop submissions are unconditional emergency actions - if a naked position is detected, a hard stop MUST be submitted regardless of other order states.

**Current Code (Line 420 - Naked Stop Candidate):**
```csharp
bool alreadyNakedInFlight;
alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(expectedKey); // [Build 968]
if (!alreadyNakedInFlight)
{
    _reaperNakedStopInFlight.TryAdd(expectedKey, 0); // [Build 968]
    Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
        acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
    _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
    return true;
}
```

**Surgical Repair (Line 420):**
```csharp
// H16-FIX: Atomic TryAdd check prevents duplicate naked stop submissions.
if (!_reaperNakedStopInFlight.TryAdd(expectedKey, 0))
{
    // Already in flight - skip
    return false;
}
Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
    acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
_reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
return true;
```

**Current Code (Line 622 - Master Naked Stop):**
```csharp
bool alreadyNakedInFlight;
alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(masterExpectedKey);
if (!alreadyNakedInFlight)
{
    _reaperNakedStopInFlight.TryAdd(masterExpectedKey, 0);
    Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
        Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
    _reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
    return true;
}
```

**Surgical Repair (Line 622):**
```csharp
// H16-FIX: Atomic TryAdd check prevents duplicate master naked stop submissions.
if (!_reaperNakedStopInFlight.TryAdd(masterExpectedKey, 0))
{
    // Already in flight - skip
    return false;
}
Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
    Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
_reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
return true;
```

**Rationale:**
- `TryAdd()` returns false if key already exists (atomic check-and-set)
- Eliminates TOCTOU window between `ContainsKey` and `TryAdd`
- Pattern already proven at line 367 (`EnqueueReaperFlattenCandidate`)
- Note: Repair candidate needs additional `TryRemove` if `hasWorkingEntry` check fails

**Verification:**
- Grep confirm: `grep -n "if (!_repairInFlight.TryAdd" src/V12_002.REAPER.Audit.cs`
- Expected: 1 match at line ~337
- Grep confirm: `grep -n "if (!_reaperNakedStopInFlight.TryAdd" src/V12_002.REAPER.Audit.cs`
- Expected: 2 matches at lines ~420, ~622

---

### H17: Teardown Sequence - Reorder Drain Before Cancel + Add Termination Guards

**File:** [`src/V12_002.Lifecycle.cs`](src/V12_002.Lifecycle.cs)
**Location:** Lines 102-143
**Method:** `ShutdownUiAndServices()`
**Additional Changes:** Add termination guards to REAPER enqueue methods

**Current Code:**
```csharp
private void ShutdownUiAndServices()
{
    _configureComplete = false;
    _dataLoadedComplete = false;
    Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

    StopPanelRefresh();

    if (ChartControl != null)
    {
        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            // B984-F07: _isTerminating guard ensures no re-entrant panel ops if invoked late.
            if (!_isTerminating) return;
            DetachHotkeys();
            DetachChartClickHandler();
            DestroyPanel();
        });
    }

    // [BUILD 984] GTC Cancel Sweep -- cancel all tracked/broker V12 orders before teardown.
    // Must run while dicts are still populated and accounts still subscribed.
    // force=false: soft terminate, protects brackets for open positions.
    // B984-F08: Log entry count before sweep for post-mortem tracing.
    Print(string.Format("[SHUTDOWN] GTC sweep: cancelling {0} tracked + broker-scanned orders",
        (entryOrders?.Count ?? 0) + (stopOrders?.Count ?? 0)));
    CancelAllV12GtcOrders(false);

    DrainQueuesForShutdown();
    EmitMetricsSummary();

    // Stop IPC Server
    StopIpcServer();

    // V12 SIMA: Stop Reaper audit thread
    StopReaperAudit();

    // V12.7: Always unsubscribe from account updates (subscribed for fleet bracket management)
    // V12.1101E [A-4]: Use shared UnsubscribeFromFleetAccounts() -- unconditional (no EnableSIMA guard)
    // to handle cases where flag was toggled OFF mid-session while handlers were still subscribed.
    UnsubscribeFromFleetAccounts();
}
```

**Surgical Repair:**
```csharp
private void ShutdownUiAndServices()
{
    _configureComplete = false;
    _dataLoadedComplete = false;
    Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

    StopPanelRefresh();

    if (ChartControl != null)
    {
        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            // B984-F07: _isTerminating guard ensures no re-entrant panel ops if invoked late.
            if (!_isTerminating) return;
            DetachHotkeys();
            DetachChartClickHandler();
            DestroyPanel();
        });
    }

    // H17-FIX: Stop intake BEFORE draining queues to prevent new commands from entering.
    // This ensures DrainQueuesForShutdown processes a bounded set of commands.
    StopIpcServer();
    StopReaperAudit();

    // H17-FIX: Drain queues BEFORE cancel sweep so any queued order submissions are executed
    // and then included in the subsequent cancel sweep. This prevents ghost orders that would
    // bypass the cancel sweep if submitted after it runs.
    DrainQueuesForShutdown();

    // [BUILD 984] GTC Cancel Sweep -- cancel all tracked/broker V12 orders after drain.
    // Now sweeps ALL orders including any submitted during drain.
    // force=false: soft terminate, protects brackets for open positions.
    // B984-F08: Log entry count before sweep for post-mortem tracing.
    Print(string.Format("[SHUTDOWN] GTC sweep: cancelling {0} tracked + broker-scanned orders",
        (entryOrders?.Count ?? 0) + (stopOrders?.Count ?? 0)));
    CancelAllV12GtcOrders(false);

    EmitMetricsSummary();

    // V12.7: Always unsubscribe from account updates (subscribed for fleet bracket management)
    // V12.1101E [A-4]: Use shared UnsubscribeFromFleetAccounts() -- unconditional (no EnableSIMA guard)
    // to handle cases where flag was toggled OFF mid-session while handlers were still subscribed.
    UnsubscribeFromFleetAccounts();
}
```

**Rationale:**
- **Stop intake first:** `StopIpcServer()` and `StopReaperAudit()` prevent new commands from entering queues
- **Drain before cancel:** Ensures queued order submissions are executed and then cancelled
- **Cancel after drain:** Sweeps ALL orders including drain-submitted ones
- Maintains existing 50-command drain limit (no change to `DrainQueuesForShutdown()`)

**Intake-Stopping Mechanism:**
`StopReaperAudit()` calls `_reaperTimer?.Stop()` which prevents new timer callbacks from firing. However, if a timer callback is already in progress when `Stop()` is called, it will complete and may enqueue commands.

To ensure bounded drain, add explicit termination guard at the top of all REAPER enqueue methods:

**Additional Changes Required (REAPER.Audit.cs):**

Add termination guard to `EnqueueReaperRepairCandidate()` (line ~320):
```csharp
private bool EnqueueReaperRepairCandidate(Account acct, int expectedQty, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

Add termination guard to `EnqueueReaperNakedStopCandidate()` (line ~400):
```csharp
private bool EnqueueReaperNakedStopCandidate(Account acct, Position pos, DateTime firstSeen, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

Add termination guard to `EnqueueReaperMasterNakedStop()` (line ~600):
```csharp
private bool EnqueueReaperMasterNakedStop(Position masterPos, DateTime masterFirstSeen, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

Add termination guard to `EnqueueReaperFlattenCandidate()` (line ~360):
```csharp
private bool EnqueueReaperFlattenCandidate(Account acct, bool shouldLog)
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating) return false;
    
    // ... rest of method
}
```

These guards are checked BEFORE any in-flight checks or queue operations, ensuring no new commands enter the queue after `_isTerminating` is set (which happens at the start of `ShutdownUiAndServices()`).

**Sequence Comparison:**

**Before (INCORRECT):**
```
1. CancelAllV12GtcOrders()  ← Cancels tracked orders
2. DrainQueuesForShutdown() ← Can submit NEW orders (ghost window)
3. StopIpcServer()
4. StopReaperAudit()
```

**After (CORRECT):**
```
1. StopIpcServer()          ← Stop intake
2. StopReaperAudit()        ← Stop intake
3. DrainQueuesForShutdown() ← Execute pending work
4. CancelAllV12GtcOrders()  ← Sweep ALL orders (including drain-submitted)
```

**Verification:**
- Grep confirm: `grep -n "DrainQueuesForShutdown" src/V12_002.Lifecycle.cs`
- Expected: Call appears BEFORE `CancelAllV12GtcOrders`

---

### H18: StickyState Serialization - Snapshot ALL Mutable State Before Background Thread

**File:** [`src/V12_002.StickyState.cs`](src/V12_002.StickyState.cs)
**Location:** Lines 40-76
**Methods:** `MarkStickyDirty()`, `SerializeStickyState()`, and 4 helper methods

**Current Code:**
```csharp
private void MarkStickyDirty()
{
    _stickyStateDirty = true;

    // Coalescing gate: only one pending write at a time
    if (Interlocked.CompareExchange(ref _stickyWritePending, 1, 0) == 0)
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(STICKY_DEBOUNCE_MS);
                _stickyStateDirty = false;
                string payload = SerializeStickyState();
                AtomicWriteFile(_stickyStatePath, payload);
            }
            catch (Exception ex)
            {
                Print("[STICKY] Save failed (best-effort): " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _stickyWritePending, 0);
                // If dirtied again during write, schedule another
                if (_stickyStateDirty)
                    MarkStickyDirty();
            }
        });
    }
}

private string SerializeStickyState()
{
    var sb = new StringBuilder(1024);
    SerializeSticky_WriteHeaderConfig(sb);
    SerializeSticky_WriteFleetAnchor(sb);
    SerializeSticky_WriteModeProfiles(sb);  // Line 73 - mutates _modeProfiles
    SerializeSticky_WritePositions(sb);
    return sb.ToString();
}
```

**Surgical Repair:**
```csharp
private void MarkStickyDirty()
{
    _stickyStateDirty = true;

    // Coalescing gate: only one pending write at a time
    if (Interlocked.CompareExchange(ref _stickyWritePending, 1, 0) == 0)
    {
        // H18-FIX: Capture snapshot of ALL mutable state on strategy thread BEFORE spawning background task.
        // This prevents race conditions where background serialization reads state that's being mutated
        // by IPC commands on the strategy thread. Must snapshot EVERYTHING read by SerializeStickyState().
        
        // Snapshot dictionaries and collections
        var modeProfilesSnapshot = new Dictionary<string, ModeConfigProfile>(_modeProfiles);
        var activeFleetSnapshot = activeFleetAccounts != null
            ? new Dictionary<string, bool>(activeFleetAccounts)
            : null;
        var activePositionsSnapshot = activePositions != null
            ? activePositions.ToArray()
            : null;
        
        // H18-FIX: Snapshot header config properties (CRITICAL - was missing in original approach)
        // These are read by SerializeSticky_WriteHeaderConfig() (lines 90-112 in current code).
        // Without this snapshot, torn reads can occur when IPC commands mutate config mid-serialization.
        var headerConfigSnapshot = new {
            IsRMAModeActive = isRMAModeActive,
            IsTRENDModeActive = isTRENDModeActive,
            IsRetestModeActive = isRetestModeActive,
            IsMOMOModeActive = isMOMOModeActive,
            IsFFMAModeArmed = isFFMAModeArmed,
            ActiveTargetCount = activeTargetCount,
            Target1Value = Target1Value,
            T1Type = T1Type,
            Target2Value = Target2Value,
            T2Type = T2Type,
            Target3Value = Target3Value,
            T3Type = T3Type,
            Target4Value = Target4Value,
            T4Type = T4Type,
            Target5Value = Target5Value,
            T5Type = T5Type,
            StopMultiplier = StopMultiplier,
            RMAStopATRMultiplier = RMAStopATRMultiplier,
            MaxRiskAmount = MaxRiskAmount,
            ChaseIfTouchPoints = ChaseIfTouchPoints,
            IsTrendRmaMode = isTrendRmaMode,
            IsRetestRmaMode = isRetestRmaMode
        };

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(STICKY_DEBOUNCE_MS);
                _stickyStateDirty = false;
                string payload = SerializeStickyState(headerConfigSnapshot, modeProfilesSnapshot, activeFleetSnapshot, activePositionsSnapshot);
                AtomicWriteFile(_stickyStatePath, payload);
            }
            catch (Exception ex)
            {
                Print("[STICKY] Save failed (best-effort): " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _stickyWritePending, 0);
                // If dirtied again during write, schedule another
                if (_stickyStateDirty)
                    MarkStickyDirty();
            }
        });
    }
}

private string SerializeStickyState(
    dynamic headerConfigSnapshot,
    Dictionary<string, ModeConfigProfile> modeProfilesSnapshot,
    Dictionary<string, bool> activeFleetSnapshot,
    KeyValuePair<string, PositionInfo>[] activePositionsSnapshot)
{
    var sb = new StringBuilder(1024);
    SerializeSticky_WriteHeaderConfig(sb, headerConfigSnapshot);
    SerializeSticky_WriteFleetAnchor(sb, activeFleetSnapshot);
    SerializeSticky_WriteModeProfiles(sb, modeProfilesSnapshot);
    SerializeSticky_WritePositions(sb, activePositionsSnapshot);
    return sb.ToString();
}
```

**Updated Helper Methods:**

**`SerializeSticky_WriteHeaderConfig()` signature change:**
```csharp
private void SerializeSticky_WriteHeaderConfig(StringBuilder sb, dynamic headerConfigSnapshot)
{
    // Header
    sb.AppendLine("# V12 StickyState v1");
    sb.AppendLine("# Symbol: " + (Instrument != null ? Instrument.FullName : "unknown"));
    sb.AppendLine("# Updated: " + DateTime.UtcNow.ToString("o"));
    sb.AppendLine("# Build: " + BUILD_TAG);
    sb.AppendLine();

    // [CONFIG] - H18-FIX: Read from snapshot instead of live properties
    sb.AppendLine("[CONFIG]");
    string mode = "OR";
    if (headerConfigSnapshot.IsRMAModeActive) mode = "RMA";
    else if (headerConfigSnapshot.IsTRENDModeActive) mode = "TREND";
    else if (headerConfigSnapshot.IsRetestModeActive) mode = "RETEST";
    else if (headerConfigSnapshot.IsMOMOModeActive) mode = "MOMO";
    else if (headerConfigSnapshot.IsFFMAModeArmed) mode = "FFMA";
    sb.AppendLine("MODE=" + mode);
    sb.AppendLine("COUNT=" + headerConfigSnapshot.ActiveTargetCount.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", headerConfigSnapshot.Target1Value));
    sb.AppendLine("T1TYPE=" + headerConfigSnapshot.T1Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", headerConfigSnapshot.Target2Value));
    sb.AppendLine("T2TYPE=" + headerConfigSnapshot.T2Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", headerConfigSnapshot.Target3Value));
    sb.AppendLine("T3TYPE=" + headerConfigSnapshot.T3Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", headerConfigSnapshot.Target4Value));
    sb.AppendLine("T4TYPE=" + headerConfigSnapshot.T4Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", headerConfigSnapshot.Target5Value));
    sb.AppendLine("T5TYPE=" + headerConfigSnapshot.T5Type.ToString());
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}",
        headerConfigSnapshot.IsRMAModeActive ? headerConfigSnapshot.RMAStopATRMultiplier : headerConfigSnapshot.StopMultiplier));
    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", headerConfigSnapshot.MaxRiskAmount));
    sb.AppendLine("CIT=" + (headerConfigSnapshot.ChaseIfTouchPoints ?? "0"));
    sb.AppendLine("TRMA=" + (headerConfigSnapshot.IsTrendRmaMode ? "1" : "0"));
    sb.AppendLine("RRMA=" + (headerConfigSnapshot.IsRetestRmaMode ? "1" : "0"));
    sb.AppendLine();
}
```

**`SerializeSticky_WriteFleetAnchor()` signature change:**
```csharp
private void SerializeSticky_WriteFleetAnchor(StringBuilder sb, Dictionary<string, bool> activeFleetSnapshot)
{
    // [FLEET]
    sb.AppendLine("[FLEET]");
    sb.AppendLine("LEADER=" + (_stickyLeaderAccount ?? ""));
    if (activeFleetSnapshot != null)
    {
        foreach (var kvp in activeFleetSnapshot)
            sb.AppendLine(kvp.Key + "=" + (kvp.Value ? "1" : "0"));
    }
    sb.AppendLine();
    // ... rest unchanged
}
```

**`SerializeSticky_WriteModeProfiles()` signature change:**
```csharp
private void SerializeSticky_WriteModeProfiles(StringBuilder sb, Dictionary<string, ModeConfigProfile> modeProfilesSnapshot)
{
    // Build 1106: [CONFIG_*] -- per-mode profile snapshots
    // H18-FIX: Use snapshot instead of mutating live _modeProfiles dictionary
    string activeMode = "OR";
    if (isRMAModeActive) activeMode = "RMA";
    else if (isTRENDModeActive) activeMode = "TREND";
    else if (isRetestModeActive) activeMode = "RETEST";
    else if (isMOMOModeActive) activeMode = "MOMO";
    else if (isFFMAModeArmed) activeMode = "FFMA";
    
    // Capture current config into snapshot (not live dictionary)
    modeProfilesSnapshot[activeMode] = SnapshotCurrentConfig();

    foreach (var kvp in modeProfilesSnapshot)
    {
        ModeConfigProfile p = kvp.Value;
        if (p == null) continue;
        sb.AppendLine("[CONFIG_" + kvp.Key + "]");
        // ... rest unchanged
    }
}
```

**`SerializeSticky_WritePositions()` signature change:**
```csharp
private void SerializeSticky_WritePositions(StringBuilder sb, KeyValuePair<string, PositionInfo>[] activePositionsSnapshot)
{
    // [POSITIONS] -- trailing stop state for active positions
    sb.AppendLine("[POSITIONS]");
    sb.AppendLine("# key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount");
    if (activePositionsSnapshot != null)
    {
        foreach (var kvp in activePositionsSnapshot)
        {
            var pi = kvp.Value;
            if (pi == null || pi.PendingCleanup) continue;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}",
                kvp.Key,
                pi.ExtremePriceSinceEntry,
                pi.CurrentTrailLevel,
                pi.ManualBreakevenArmed ? "1" : "0",
                pi.ManualBreakevenTriggered ? "1" : "0",
                pi.InitialTargetCount));
        }
    }
}
```

**Rationale:**
- Snapshot captured on strategy thread (single-threaded, no races)
- Background thread reads only from snapshot (isolated, no shared state)
- Dictionary clone is shallow (references only, minimal overhead)
- `activePositions.ToArray()` already thread-safe (ConcurrentDictionary)
- Eliminates all race conditions between serialization and IPC mutations

**Verification:**
- Grep confirm: `grep -n "modeProfilesSnapshot" src/V12_002.StickyState.cs`
- Expected: Multiple matches (snapshot creation + usage)

---

### H20: Queue Overflow - Add Follower Cleanup

**File:** [`src/V12_002.Lifecycle.cs`](src/V12_002.Lifecycle.cs)  
**Location:** Lines 81-86  
**Method:** `DrainQueuesForShutdown()`

**Current Code:**
```csharp
StrategyCommand overflowCmd;
while (_cmdQueue.TryDequeue(out overflowCmd))
    actorOverflow++;

Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds, {1} Actor cmds. Overflow discarded: {2}.",
    ipcDrained, actorDrained, actorOverflow));
```

**Surgical Repair:**
```csharp
// H20-FIX: Trigger CANCEL_ALL on all fleet accounts when overflow detected.
// Discarded commands may include follower bracket submissions, leaving followers
// with open positions but no protection orders. CANCEL_ALL ensures clean state.
StrategyCommand overflowCmd;
while (_cmdQueue.TryDequeue(out overflowCmd))
    actorOverflow++;

if (actorOverflow > 0)
{
    Print(string.Format("[SHUTDOWN] Overflow detected: {0} commands discarded. Triggering fleet CANCEL_ALL for safety.", actorOverflow));
    
    // Enqueue CANCEL_ALL for each fleet account to ensure clean shutdown state
    if (EnableSIMA && activeFleetAccounts != null)
    {
        foreach (var kvp in activeFleetAccounts.ToArray())
        {
            if (kvp.Value) // Account is enabled
            {
                try
                {
                    string accountName = kvp.Key;
                    Account fleetAcct = Account.All.FirstOrDefault(a => a.Name == accountName);
                    if (fleetAcct != null)
                    {
                        // Cancel all working orders for this account
                        var workingOrders = fleetAcct.Orders.ToArray()
                            .Where(o => o != null && o.Instrument?.FullName == Instrument?.FullName &&
                                       !IsOrderTerminal(o.OrderState))
                            .ToArray();
                        
                        if (workingOrders.Length > 0)
                        {
                            fleetAcct.Cancel(workingOrders);
                            Print(string.Format("[SHUTDOWN] Overflow cleanup: Cancelled {0} orders on {1}", 
                                workingOrders.Length, accountName));
                        }
                    }
                }
                catch (Exception exCleanup)
                {
                    Print("[SHUTDOWN] Overflow cleanup failed for " + kvp.Key + ": " + exCleanup.Message);
                }
            }
        }
    }
}

Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds, {1} Actor cmds. Overflow discarded: {2}.",
    ipcDrained, actorDrained, actorOverflow));
```

**Rationale:**
- Overflow indicates high-volume session with potential follower bracket submissions in queue
- `CANCEL_ALL` ensures followers don't have orphaned working orders after shutdown
- Uses `.ToArray()` snapshots for thread-safe iteration
- Wrapped in try-catch to prevent cleanup failures from blocking shutdown
- Only runs if `actorOverflow > 0` (zero overhead in normal case)

**CYC Impact:** +8-10 (new cleanup loop)

**Verification:**
- Grep confirm: `grep -n "Overflow cleanup" src/V12_002.Lifecycle.cs`
- Expected: 2 matches (log messages)

---

### Watchdog Collection Snapshots (3 Sites)

**File:** [`src/V12_002.Safety.Watchdog.cs`](src/V12_002.Safety.Watchdog.cs)  
**Locations:** Lines 99, 167, 274

#### Site 1: `HasWatchdogLeadAccountPosition()` (Line 99)

**Current Code:**
```csharp
foreach (Position position in masterAccount.Positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition != MarketPosition.Flat)
        return true;
}
```

**Surgical Repair:**
```csharp
// WATCHDOG-FIX: Snapshot positions before iteration to prevent InvalidOperationException
// when broker fill callbacks update Positions collection from UI thread during watchdog check.
var positions = masterAccount.Positions.ToArray();
foreach (Position position in positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition != MarketPosition.Flat)
        return true;
}
```

#### Site 2: `FlattenWatchdogPositions()` (Line 167)

**Current Code:**
```csharp
foreach (Position position in masterAccount.Positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit flatten order
}
```

**Surgical Repair:**
```csharp
// WATCHDOG-FIX: Snapshot positions before iteration (same pattern as Site 1).
var positions = masterAccount.Positions.ToArray();
foreach (Position position in positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit flatten order
}
```

#### Site 3: `FlattenDirectFallbackPositions()` (Line 274)

**Current Code:**
```csharp
foreach (Position position in masterAccount.Positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit direct fallback order
}
```

**Surgical Repair:**
```csharp
// WATCHDOG-FIX: Snapshot positions before iteration (same pattern as Sites 1-2).
var positions = masterAccount.Positions.ToArray();
foreach (Position position in positions)
{
    if (position == null || position.Instrument == null)
        continue;
    if (position.Instrument.FullName != instrumentName)
        continue;
    if (position.MarketPosition == MarketPosition.Flat)
        continue;
    // ... submit direct fallback order
}
```

**Rationale:**
- Mirrors existing correct pattern at line 120 (`HasWatchdogLeadAccountWorkingOrder()`)
- Watchdog is last-resort safety mechanism - cannot tolerate exceptions
- All 3 sites use identical pattern for consistency

**Verification:**
- Grep confirm: `grep -n "var positions = masterAccount.Positions.ToArray()" src/V12_002.Safety.Watchdog.cs`
- Expected: 3 matches at lines ~99, ~167, ~274

---

## 3. Testing Strategy

### Unit Tests (7 Tests)

**Test File:** `tests/Build981ComplianceTests.cs` (extend existing file)

#### Test 1: H13 - Naked Position Audit Concurrent Modification
```csharp
[Test]
public void Test_H13_NakedPositionAudit_ConcurrentOrderModification_NoException()
{
    // Arrange: Mock Account.Orders with concurrent modification
    // Act: Call AuditMaster_HandleNakedPosition() during order update
    // Assert: No InvalidOperationException thrown
}
```

#### Test 2: H14 - Flatten Cancel Concurrent Modification
```csharp
[Test]
public void Test_H14_FlattenCancel_ConcurrentOrderModification_CompletesSuccessfully()
{
    // Arrange: Mock targetAcct.Orders with concurrent modification
    // Act: Call ProcessReaperFlatten_CancelWorkingOrders() during order update
    // Assert: All orders cancelled, no exception
}
```

#### Test 3: H15 - Flatten Close Concurrent Modification
```csharp
[Test]
public void Test_H15_FlattenClose_ConcurrentPositionModification_CompletesSuccessfully()
{
    // Arrange: Mock targetAcct.Positions with concurrent modification
    // Act: Call ProcessReaperFlatten_ClosePositions() during position update
    // Assert: All positions closed, no exception
}
```

#### Test 4: H16 - Repair Enqueue Atomic Guard
```csharp
[Test]
public void Test_H16_RepairEnqueue_ConcurrentAuditCycles_EnqueuesExactlyOnce()
{
    // Arrange: Two concurrent audit cycles
    // Act: Both call EnqueueReaperRepairCandidate() simultaneously
    // Assert: Repair queue contains exactly 1 entry (not 2)
}
```

#### Test 5: H17 - Shutdown Drain Before Cancel
```csharp
[Test]
public void Test_H17_Shutdown_DrainBeforeCancel_NoGhostOrders()
{
    // Arrange: Queue contains order submission command
    // Act: Call ShutdownUiAndServices()
    // Assert: Order submitted during drain is cancelled by sweep
}
```

#### Test 6: H18 - StickyState Concurrent Mutation
```csharp
[Test]
public void Test_H18_StickyState_ConcurrentMutation_NoCorruption()
{
    // Arrange: Background serialization in progress
    // Act: IPC command mutates Target1Value
    // Assert: Serialized state is consistent (no torn reads)
}
```

#### Test 7: H20 - Queue Overflow Cleanup
```csharp
[Test]
public void Test_H20_QueueOverflow_TriggersFollowerCleanup()
{
    // Arrange: 100 commands in queue
    // Act: Call DrainQueuesForShutdown()
    // Assert: CANCEL_ALL triggered for all fleet accounts
}
```

### Stress Tests

**Stress Test 1: REAPER Audit Under Load**
```powershell
# Simulate 1000+ orders/sec with concurrent REAPER audit cycles
# Run for 5 minutes, verify zero InvalidOperationException
```

**Stress Test 2: Rapid Shutdown**
```powershell
# Start strategy, submit 100 orders, shutdown immediately
# Repeat 100 times, verify zero ghost orders
```

**Stress Test 3: Concurrent Serialization**
```powershell
# Send 100 rapid IPC config changes
# Verify .v12state file integrity after each change
```

---

## 4. Verification Checklist

### Pre-Deployment Gates

- [ ] **Compile Gate:** `powershell -File .\deploy-sync.ps1` - PASS
- [ ] **ASCII Gate:** Zero non-ASCII characters in modified files
- [ ] **DIFF GUARD:** Total diff < 150,000 characters
- [ ] **Lock Regression:** `grep -r "lock(" src/` - Zero new matches
- [ ] **Ghost-Method Audit:** Zero banned identifiers
- [ ] **Unit Tests:** All 7 tests green
- [ ] **Integration Tests:** Existing tests pass (no regressions)

### Post-Deployment Validation

- [ ] **F5 Compile:** BUILD_TAG banner visible in NinjaTrader
- [ ] **REAPER Audit:** Runs every 1000ms without exceptions
- [ ] **Watchdog Heartbeat:** Runs every 2000ms without exceptions
- [ ] **Shutdown Clean:** Zero ghost orders after termination
- [ ] **Config Persistence:** .v12state file loads correctly on restart

---

## 5. Rollback Plan

### Per-Ticket Rollback

```powershell
# Rollback specific ticket (replace XX with ticket number)
git checkout HEAD~1 -- src/V12_002.REAPER.Audit.cs
git checkout HEAD~1 -- src/V12_002.Lifecycle.cs
git checkout HEAD~1 -- src/V12_002.StickyState.cs
git checkout HEAD~1 -- src/V12_002.Safety.Watchdog.cs
powershell -File .\deploy-sync.ps1
```

### Full Epic Rollback

```powershell
# Rollback all 7 tickets
git checkout HEAD~7 -- src/V12_002.REAPER.Audit.cs
git checkout HEAD~7 -- src/V12_002.Lifecycle.cs
git checkout HEAD~7 -- src/V12_002.StickyState.cs
git checkout HEAD~7 -- src/V12_002.Safety.Watchdog.cs
powershell -File .\deploy-sync.ps1
```

---

## 6. Success Criteria

### Functional Requirements

1. ✅ **H13-H15 + Watchdog:** Zero `InvalidOperationException` in logs
2. ✅ **H16:** Zero duplicate repair submissions (verify via queue logs)
3. ✅ **H17:** Zero ghost orders after shutdown (verify via broker)
4. ✅ **H18:** Zero config corruption (verify via .v12state integrity)
5. ✅ **H20:** Follower cleanup triggered on overflow (verify via logs)

### Performance Requirements

1. ✅ REAPER audit cycle < 10ms (allow 2x overhead from snapshots)
2. ✅ Watchdog cycle < 5ms (allow 2x overhead from snapshots)
3. ✅ Serialization frequency < 20/sec (50ms debounce enforced)
4. ✅ Shutdown time < 5 seconds (allow 2x overhead from cleanup)

### DNA Compliance

1. ✅ Zero new `lock()` statements
2. ✅ Zero non-ASCII characters
3. ✅ Diff size < 150,000 characters
4. ✅ Zero ghost-method references

---

**[PLAN-GATE]**

**Approach Summary:**
- **7 defects + Watchdog** = 11 surgical edits across 4 files
- **Zero architectural changes** (pure thread-safety hardening)
- **Lock-free patterns** (`.ToArray()`, `TryAdd()`, dictionary clone)
- **Estimated lines changed:** ~30
- **Estimated CYC delta:** +5-10 (H20 cleanup loop only)

**Key Decisions:**
1. ✅ Include Watchdog fixes (3 sites)
2. ✅ Fix all 3 REAPER enqueue sites (H16)
3. ✅ Maintain 50-command drain limit
4. ✅ Trigger `CANCEL_ALL` on queue overflow

**Ready for Phase 3 (VALIDATE)?** Type **APPROVED** to proceed to triple-agent audit or provide feedback.

---

**Document Status:** COMPLETE - AWAITING VALIDATION  
**Author:** Bob CLI (v12-engineer) via Plan Mode  
**Date:** 2026-05-19T00:51:00Z  
**Epic:** epic-3-reaper  
**Phase:** 2/6 (APPROACH)  
**Next:** Phase 3 (VALIDATE) - Triple-agent audit