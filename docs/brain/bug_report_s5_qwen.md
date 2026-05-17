# Bug Bounty Report -- Agent-S5 (Kernel State Cluster)

**Cluster**: S5 -- Kernel State Cluster  
**Scope Files**:
- `src/V12_002.Lifecycle.cs` (774 lines)
- `src/V12_002.StickyState.cs` (681 lines)
- `src/V12_002.Telemetry.cs` (161 lines)
- `src/V12_002.StructuredLog.cs` (118 lines)
- `src/V12_002.Properties.cs` (362 lines)

**Date**: 2026-05-17  
**Runner**: Qwen 3.6 Max Preview  
**Mode**: READ-ONLY forensic scan

---

## Executive Summary

**Total Findings**: 7  
**Severity Breakdown**: Critical: 0 | High: 2 | Medium: 3 | Low: 2

The Kernel State Cluster is generally well-architected. No `lock()` remnants found. Thread safety is primarily achieved through `ConcurrentDictionary`, `Interlocked` primitives, and the actor `Enqueue` pattern. Two high-severity findings involve atomic file operations and cross-thread state serialization. No critical data corruption windows were identified in the core trading path.

---

## DNA Compliance

| Check | Status | Details |
|-------|--------|---------|
| `lock()` statements | PASS | Zero matches across all 5 files |
| Non-ASCII string literals | PASS | All string literals use ASCII characters |
| `Thread.Sleep()` in hot path | PASS | No `Thread.Sleep()` calls in any scoped file |
| `Dictionary<K,V>` writes without atomic guard | PASS | `_pendingStickyFleetToggles` is a plain `Dictionary<string,bool>` but is only accessed during startup on the strategy thread (DataLoaded -> EnumerateApexAccounts, single-threaded). All other dictionaries are `ConcurrentDictionary`. |

---

## Findings

### BUG-S5-001
**Title**: Atomic file write has data-loss window between Delete and Move  
**Severity**: High  
**Location**: `V12_002.StickyState.cs` -- `AtomicWriteFile()` (lines 210-216)  
**Root Cause**: The implementation deletes the target file before moving the temp file over it. Between `File.Delete(targetPath)` (line 214) and `File.Move(tmpPath, targetPath)` (line 215), there exists a window where neither the old file nor the new file exists at the target path. If the process crashes or is killed during this window, all persisted state is permanently lost. On Windows NTFS, the correct pattern is to either (a) use `File.Replace()` which provides true atomicity, or (b) skip the delete and accept that both files briefly coexist.  
**Evidence**:
```
Line 214: System.IO.File.Delete(targetPath);
Line 215: System.IO.File.Move(tmpPath, targetPath);
```
The delete-then-move sequence is the anti-pattern.  
**Test Impact**: Fault-injection test that kills the process between Delete and Move would verify data loss. Integration test should confirm `File.Replace()` or equivalent is used.

### BUG-S5-002
**Title**: Sticky state serialization reads mutable config from ThreadPool thread without memory barrier  
**Severity**: High  
**Location**: `V12_002.StickyState.cs` -- `MarkStickyDirty()` / `SerializeStickyState()` (lines 40-58, 68-76)  
**Root Cause**: `MarkStickyDirty()` schedules `SerializeStickyState()` via `Task.Run()` (line 40), which executes on a ThreadPool thread. The serialization reads 11+ mutable fields including `Target1Value` through `Target5Value`, `activeTargetCount`, `T1Type` through `T5Type`, `StopMultiplier`, `RMAStopATRMultiplier`, and `MaxRiskAmount`. These fields are NOT `volatile` and have no memory barrier. If the strategy thread is simultaneously applying a mode profile change (e.g., `HydrateFromProfile()` writes all 11 fields in sequence), the ThreadPool serialization can observe a torn snapshot -- some fields with new values, some with old. This produces an inconsistent `.v12state` file that, if loaded on restart, could apply mismatched target values and types.  
**Evidence**:  
- `V12_002.cs` line 151: `private int activeTargetCount = 1;` -- NOT volatile  
- `V12_002.Properties.cs`: `Target1Value` through `Target5Value` are auto-properties (not volatile)  
- `V12_002.StickyState.cs` lines 139-163: `SerializeSticky_WriteModeProfiles()` calls `SnapshotCurrentConfig()` which reads all config fields in a compound sequence  
**Test Impact**: Concurrency stress test that simultaneously triggers mode profile changes and sticky state serialization. Validate that persisted state is always self-consistent (all fields from same epoch or none).

### BUG-S5-003
**Title**: `_modeProfiles` dictionary write during serialization creates compound race  
**Severity**: Medium  
**Location**: `V12_002.StickyState.cs` -- `SerializeSticky_WriteModeProfiles()` (lines 139-163)  
**Root Cause**: `SerializeSticky_WriteModeProfiles()` runs on a ThreadPool thread (inside `Task.Run`). At line 144, it writes to the `ConcurrentDictionary<string, ModeConfigProfile>` via `_modeProfiles[activeMode] = SnapshotCurrentConfig()`. While `ConcurrentDictionary` provides thread-safety for individual operations, the compound action of (1) determining `activeMode` from volatile mode flags (lines 139-143), then (2) snapshotting config, then (3) writing to the dictionary, is NOT atomic. A concurrent mode change on the strategy thread could alter the mode flags between step 1 and step 2, causing the wrong profile to be overwritten. Additionally, the subsequent `foreach` iteration (line 146) over `.ToArray()` captures a point-in-time snapshot that may already be stale by the time serialization completes.  
**Evidence**:  
- Lines 139-143: Sequential reads of `isRMAModeActive`, `isTRENDModeActive`, etc. to determine `activeMode`  
- Line 144: `_modeProfiles[activeMode] = SnapshotCurrentConfig();` -- compound write  
- Line 146: `foreach (var kvp in _modeProfiles.ToArray())` -- stale snapshot  
**Test Impact**: Concurrent mode-switch + sticky-serialize stress test. Verify profile keys always match their content.

### BUG-S5-004
**Title**: `_currentTraceId` non-volatile field read across threads  
**Severity**: Medium  
**Location**: `V12_002.Telemetry.cs` (line 24) and `V12_002.StructuredLog.cs` (lines 53, 59, 65, 107)  
**Root Cause**: `_currentTraceId` is declared as a plain `string` (line 24: `private string _currentTraceId = "00000";`) without `volatile`. It is written by `NewTraceId()` on the strategy thread (line 47) and read by `LogInfo()`, `LogWarn()`, `LogError()`, and `LogException()` via the convenience wrappers (lines 53, 59, 65, 107). When logging occurs from a non-strategy thread (e.g., the sticky state ThreadPool thread in `MarkStickyDirty`'s exception handler, or from IPC callbacks), the read may observe a stale trace ID or, worse, a reference to a string object that was just replaced. While `string` references are atomic on x64, the C# memory model does not guarantee visibility without `volatile` or a memory barrier. This produces misleading trace IDs in cross-thread diagnostic logs, complicating post-mortem analysis.  
**Evidence**:  
- `V12_002.Telemetry.cs` line 24: `private string _currentTraceId = "00000";` -- no volatile  
- `V12_002.Telemetry.cs` line 47: `_currentTraceId = string.Format(...)` -- strategy thread write  
- `V12_002.StructuredLog.cs` line 53: `StructuredPrint(_currentTraceId, ...)` -- cross-thread read possible  
**Test Impact**: Diagnostic correctness. Cross-thread logging would show incorrect trace correlation IDs, hindering incident investigation.

### BUG-S5-005
**Title**: Shutdown GTC sweep operates on dictionaries not yet guarded from concurrent callbacks  
**Severity**: Medium  
**Location**: `V12_002.Lifecycle.cs` -- `ShutdownUiAndServices()` (lines 102-143)  
**Root Cause**: The shutdown sequence calls `CancelAllV12GtcOrders(false)` (line 128) which iterates over order tracking dictionaries (`entryOrders`, `stopOrders`, etc.), then calls `DrainQueuesForShutdown()` (line 130), and finally `CleanupDictionaries()` is called separately in `OnStateChangeTerminated()` (line 697) AFTER `ShutdownUiAndServices()` returns. While the `_isTerminating` flag is set before `ShutdownUiAndServices()` runs (via `SetTerminatingAndStopWatchdog()`), any pending `OnOrderUpdate` or `OnExecutionUpdate` callbacks that were already scheduled on the strategy thread but not yet executed could still fire during the GTC sweep and mutate the dictionaries being iterated. The dictionaries are `ConcurrentDictionary` so this won't crash, but the iteration could observe partially-modified state, leading to missed cancellations or double-cancellations.  
**Evidence**:  
- Line 694-697: `OnStateChangeTerminated()` calls `SetTerminatingAndStopWatchdog()` then `ShutdownUiAndServices()` then `CleanupDictionaries()`  
- Line 128: `CancelAllV12GtcOrders(false)` iterates dictionaries while callbacks may still be pending  
- Line 130: `DrainQueuesForShutdown()` executes remaining queued commands that could mutate the same dictionaries  
**Test Impact**: Shutdown integration test with in-flight orders. Verify all orders are cancelled exactly once and no dictionary mutation during iteration.

### BUG-S5-006
**Title**: `_stickyWritePending` gate allows recursive re-entry after release  
**Severity**: Low  
**Location**: `V12_002.StickyState.cs` -- `MarkStickyDirty()` (lines 33-59)  
**Root Cause**: In the `finally` block (lines 55-58), after `Interlocked.Exchange(ref _stickyWritePending, 0)` releases the gate, the code checks `if (_stickyStateDirty)` and recursively calls `MarkStickyDirty()`. This is by design for coalescing, but creates a subtle ordering dependency: the gate is released BEFORE the dirty check. If thread A releases the gate and is preempted before the dirty check, thread B can set `_stickyWritePending` to 1 and start a new write task. Thread A then checks `_stickyStateDirty` (which is true) and calls `MarkStickyDirty()`, which fails the `CompareExchange` (already 1). This is correct behavior, but the window between gate release and dirty check is unprotected and relies on the `CompareExchange` in the recursive call as the safety net. A more robust design would check the dirty flag BEFORE releasing the gate (inside the try block) and only release if clean.  
**Evidence**:  
- Lines 55-58: `finally { Interlocked.Exchange(ref _stickyWritePending, 0); if (_stickyStateDirty) MarkStickyDirty(); }`  
- Gate release (line 55) precedes dirty re-check (line 57)  
**Test Impact**: Edge-case correctness under heavy concurrent dirty calls. No data loss, but unnecessary task allocations and scheduling overhead under race conditions.

### BUG-S5-007
**Title**: `EnrichTrailStateFromSticky()` directly mutates `PositionInfo` fields without atomic guard  
**Severity**: Low  
**Location**: `V12_002.StickyState.cs` -- `EnrichTrailStateFromSticky()` (lines 591-639)  
**Root Cause**: This method reads the persisted state file and directly writes to `PositionInfo` fields (lines 621-626): `pi.ExtremePriceSinceEntry`, `pi.CurrentTrailLevel`, `pi.ManualBreakevenArmed`, `pi.ManualBreakevenTriggered`, `pi.InitialTargetCount`. These fields are on a `PositionInfo` object that lives in the `activePositions` ConcurrentDictionary. If the strategy thread's trailing stop logic (`OnTrailingStopTick` or similar) reads these same fields concurrently during SIMA startup hydration, it could observe torn values (e.g., `ExtremePriceSinceEntry` updated but `CurrentTrailLevel` still stale). While `EnrichTrailStateFromSticky()` is called during SIMA startup (before realtime trading begins), the SIMA hydration path runs via `Enqueue` on the strategy thread, so this is technically single-threaded. The risk is low but the code lacks explicit documentation of the threading contract.  
**Evidence**:  
- Lines 621-626: Direct field writes to `pi` obtained from `activePositions.TryGetValue()`  
- No `volatile` or `Interlocked` guards on `PositionInfo` fields  
**Test Impact**: Low -- only relevant if SIMA startup coincides with trailing stop logic on a different thread, which the current architecture prevents via `Enqueue`.

---

## Cross-File Dependency Map

```
V12_002.Lifecycle.cs
  ├── reads: V12_002.Properties.cs (all properties in SetDefaults)
  ├── calls: LoadStickyState() -> V12_002.StickyState.cs
  ├── calls: StartIpcServer(), StopIpcServer()
  ├── calls: ResetTelemetry() -> V12_002.Telemetry.cs
  ├── calls: EmitMetricsSummary() -> V12_002.Telemetry.cs
  └── calls: CancelAllV12GtcOrders() -> V12_002.SIMA.Lifecycle.cs (out of scope)

V12_002.StickyState.cs
  ├── reads: V12_002.Properties.cs (Target1Value, etc.)
  ├── reads: V12_002.cs fields (activeTargetCount, mode flags, _modeProfiles)
  ├── calls: SetRmaAnchorFromIpc() -> V12_002.SIMA.cs (out of scope)
  └── writes: _pendingStickyFleetToggles (plain Dictionary, startup-only)

V12_002.Telemetry.cs
  ├── writes: _currentTraceId (non-volatile, cross-thread read)
  ├── reads: Interlocked metrics (correctly guarded)
  └── called by: StructuredLog wrappers, Lifecycle, all subsystems

V12_002.StructuredLog.cs
  ├── reads: _currentTraceId from Telemetry.cs (cross-thread concern)
  └── calls: NinjaTrader Print() (thread-safe per NT8 docs)

V12_002.Properties.cs
  ├── pure data declarations (auto-properties)
  └── no cross-thread guards (relies on callers for safety)
```

---

## Summary Assessment

The S5 cluster demonstrates solid lock-free architecture with correct use of `ConcurrentDictionary`, `Interlocked` primitives, and the actor `Enqueue` pattern. The two high-severity findings involve the sticky state persistence layer (not the core trading path), where ThreadPool serialization reads mutable state without memory barriers. The medium findings relate to compound operations on otherwise thread-safe primitives. No critical bugs that could cause order corruption or financial loss were identified in this cluster.
