# Bug Report: S3 Cluster (UI + Photon IO)
**Cluster**: Agent-S3 -- UI & Photon IO
**Scope**: 19 files (16 UI sub-files + 3 Photon sub-files)
**Date**: 2026-05-17
**Runner**: Qwen 3.6 Max Preview
**Mode**: READ-ONLY forensic scan. No src/ edits.

---

## Executive Summary

| Severity | Count |
|----------|-------|
| Critical | 2     |
| High     | 4     |
| Medium   | 3     |
| Low      | 4     |
| **Total**| **13**|

### DNA Compliance

| Check              | Result  | Detail                                      |
|--------------------|---------|---------------------------------------------|
| `lock()` remnants  | **PASS**| Zero actual `lock()` statements found. All grep hits are in comments only. |
| Non-ASCII strings  | **PASS**| No non-ASCII characters in C# string literals. |
| `Thread.Sleep()`   | **FAIL**| 2 instances in IPC server listener thread (lines 85, 214 of `V12_002.UI.IPC.Server.cs`). |
| `Dictionary<K,V>` writes without atomic guard | **FAIL**| `_modeProfiles` (regular `Dictionary`) written from strategy thread without concurrent guard. See BUG-S3-003. |

---

## Findings (Ordered Critical -> Low)

---

### BUG-S3-001
**Title**: IPC `GET_LAYOUT` reads torn config snapshot across threads
**Severity**: Critical
**Location**: `V12_002.UI.IPC.Server.cs.HandleIncomingIpcLine_RespondLayout` (lines ~228-260)
**Root Cause**: The IPC listener thread (background TCP thread) reads 17 individual strategy state fields (`activeTargetCount`, `Target1Value` through `Target5Value`, `T1Type` through `T5Type`, `RMAStopATRMultiplier`, `StopMultiplier`, `MaxRiskAmount`, `ChaseIfTouchPoints`, `_stickyLeaderAccount`, `isTrendRmaMode`, `isRetestRmaMode`) one-by-one without any atomic snapshot mechanism. These fields are written by the strategy thread. Between reading `Target1Value` and `T1Type`, the strategy thread may process a `CONFIG` command that changes both, resulting in a torn read where the panel receives a T1 value from the old config paired with a T1 type from the new config.
**Evidence**: Each field is read as a separate C# statement (e.g., `snapT1 = Target1Value;` then `snapT1Type = T1Type;`). No `Interlocked`, no lock, no snapshot struct. The `GetCurrentConfigMode()` call on line ~232 reads mode flags (`isRMAModeActive`, etc.) that are independently written by the strategy thread. A config sync arriving mid-read produces a frankenstein response mixing old and new config.
**Test Impact**: IPC integration test that sends `CONFIG|...` while simultaneously requesting `GET_LAYOUT` would observe torn responses. A unit test reading all 17 fields under concurrent writes would demonstrate non-atomic reads.

---

### BUG-S3-002
**Title**: `_glowTimer` null-race between UI thread and lifecycle thread
**Severity**: Critical
**Location**: `V12_002.UI.Panel.Lifecycle.cs` -- `TriggerGlow` (line 104) vs `StopGlowTimer` (line 118)
**Root Cause**: `_glowTimer` is a `DispatcherTimer` that is read and written from two threads without synchronization. `TriggerGlow` (called from 21 WPF button click handlers on the **UI thread**) reads `_glowTimer` and calls `.Stop()/.Start()`. `StopGlowTimer` (called from `StopPanelRefresh`, invoked on the **NinjaScript lifecycle thread** at `V12_002.Lifecycle.cs:108`) calls `_glowTimer.Stop()` then sets `_glowTimer = null`. The null assignment on the lifecycle thread can interleave with the null-check in `TriggerGlow` on the UI thread, causing a `NullReferenceException` when `_glowTimer.Stop()` executes after the field has been nulled.

Additionally, `InitGlowTimer` (line 84) uses a simple null guard (`if (_glowTimer != null) return;`) without `Interlocked.CompareExchange`, unlike `StartPanelRefresh` which correctly uses `Interlocked.CompareExchange` for `_panelRefreshTimer`. This means if `InitGlowTimer` were ever called from two paths, it would create duplicate timers.
**Evidence**: `TriggerGlow` accesses `_glowTimer` on UI thread (21 call sites in `V12_002.UI.Panel.Handlers.cs`). `StopGlowTimer` sets `_glowTimer = null` on the lifecycle thread (`V12_002.Lifecycle.cs:108` -> `StopPanelRefresh()` -> `StopGlowTimer()`). No `volatile` keyword, no `Interlocked` on `_glowTimer` field.
**Test Impact**: Rapidly disabling the strategy while clicking panel buttons would trigger `NullReferenceException`. Stress test with concurrent enable/disable + UI interaction.

---

### BUG-S3-003
**Title**: `_modeProfiles` Dictionary written from strategy thread without concurrent guard
**Severity**: High
**Location**: `V12_002.UI.IPC.Commands.Config.cs` (line 136), `V12_002.UI.IPC.Commands.Mode.cs` (lines 120, 138)
**Root Cause**: `_modeProfiles` is a regular `Dictionary<string, ModeConfigProfile>`, not a `ConcurrentDictionary`. It is written from the strategy thread via `Enqueue` (lines 136, 120) and read from the strategy thread (line 138). While current architecture serializes through `Enqueue`, the comment on line 383 of `V12_002.UI.IPC.Commands.Config.cs` ("Lock IPC writes to activeFleetAccounts") demonstrates awareness of cross-thread dict writes in this module, yet `_modeProfiles` receives no such protection. If any future code path reads `_modeProfiles` from a non-strategy thread (e.g., `GET_LAYOUT` on the IPC listener thread), it will produce `InvalidOperationException` due to concurrent dictionary modification.

The V12 Platinum Standard mandates "make illegal states unrepresentable" -- using a non-concurrent dictionary for shared state violates this principle.
**Evidence**: `_modeProfiles[currentMode] = SnapshotCurrentConfig();` at `V12_002.UI.IPC.Commands.Config.cs:136`. `_modeProfiles[outgoingMode] = SnapshotCurrentConfig();` at `V12_002.UI.IPC.Commands.Mode.cs:120`. `_modeProfiles.TryGetValue(newMode, out incomingProfile)` at `V12_002.UI.IPC.Commands.Mode.cs:138`. All use regular `Dictionary<K,V>` indexer.
**Test Impact**: Any cross-thread read during a mode switch would crash. Convert to `ConcurrentDictionary<string, ModeConfigProfile>` to eliminate the hazard.

---

### BUG-S3-004
**Title**: `activeFleetAccounts` indexer write races with concurrent reads
**Severity**: High
**Location**: `V12_002.UI.IPC.Commands.Config.cs.HandleToggleAccountCommand` (line 384)
**Root Cause**: `activeFleetAccounts` is a `ConcurrentDictionary`, but line 384 uses the direct indexer `activeFleetAccounts[resolvedName] = active;` instead of `AddOrUpdate` or `TryAdd`. While `ConcurrentDictionary`'s indexer IS thread-safe for individual writes, the comment on line 382-383 states "Lock IPC writes to activeFleetAccounts -- this dict is also read by the strategy thread (ExecuteMultiAccountMarket) without a lock." This comment is misleading: there is no actual lock, and the indexer write is atomic per-key but does NOT provide a consistent multi-key snapshot. If `HandleFleet_DiagFleet` (line 133 of `V12_002.UI.IPC.Commands.Misc.cs`) iterates with `TryGetValue` while `HandleToggleAccountCommand` writes, the diagnostic output can show an inconsistent fleet state.

The real danger: `ExecuteMultiAccountMarket` reads this dict to determine which accounts receive orders. A torn read during a toggle could include or exclude an account mid-iteration.
**Evidence**: `activeFleetAccounts[resolvedName] = active;` at line 384. Read at `V12_002.UI.IPC.Commands.Misc.cs:133`: `activeFleetAccounts.TryGetValue(acct.Name, out isActive);` inside a `foreach` loop over `Account.All`.
**Test Impact**: Toggle an account via IPC while fleet dispatch is iterating `activeFleetAccounts`. The account could be included in one iteration and excluded in the next within the same dispatch cycle.

---

### BUG-S3-005
**Title**: `isRMAModeActive` bool written from UI thread, read from strategy thread without memory barrier
**Severity**: High
**Location**: `V12_002.UI.Panel.Handlers.cs` (lines 243, 262, 455) vs `V12_002.UI.Snapshot.cs` (line 202) and `V12_002.UI.Callbacks.cs` (line 332)
**Root Cause**: `isRMAModeActive` is a plain `bool` field written from the **UI thread** (panel button click handlers in `V12_002.UI.Panel.Handlers.cs` lines 243, 262, 455) and read from the **strategy thread** (`PublishUiSnapshot` in `V12_002.UI.Snapshot.cs` line 202, `HandleChartClick_DeactivateRma` in `V12_002.UI.Callbacks.cs` line 332 via `HandleChartClick_ValidateMode`). Without `volatile` or `Interlocked`, the C# memory model does not guarantee that a write on the UI thread is visible to a read on the strategy thread. On ARM or weakly-ordered architectures, the strategy thread could cache a stale value indefinitely.

The practical impact: `IsClickTraderArmed()` reads `isRMAModeActive` on the UI thread for mouse hover detection (minor), but `PublishUiSnapshot` reads it on the strategy thread to build the UI state snapshot (moderate -- panel may show stale RMA mode). The `HandleChartClick_DeactivateRma` method writes `false` on the UI thread (line 332), while `TryHandleMode_SetRmaMode` writes on the strategy thread (line 56 of `V12_002.UI.IPC.Commands.Mode.cs`) -- a multi-writer scenario.
**Evidence**: Write on UI thread: `isRMAModeActive = false;` at `V12_002.UI.Panel.Handlers.cs:455` (in `ResetExecutionMode`). Write on strategy thread: `isRMAModeActive = enable;` at `V12_002.UI.IPC.Commands.Mode.cs:56`. Read on strategy thread: `IsRmaModeActive = isRMAModeActive` at `V12_002.UI.Snapshot.cs:202`.
**Test Impact**: Panel may display stale RMA mode indicator. Strategy may suppress chart-click trades because `IsClickTraderArmed()` returns stale `false`.

---

### BUG-S3-006
**Title**: `selectedFleetAccounts` List modified from WPF event handlers without guard
**Severity**: High
**Location**: `V12_002.UI.Panel.Construction.cs` -- CheckBox handlers (lines 503-512), fleet popup construction (lines 479-487)
**Root Cause**: `selectedFleetAccounts` is a plain `List<string>` (line 35) modified from WPF CheckBox `Checked`/`Unchecked` event handlers (lines 503-504, 512). While WPF events are serialized on the UI thread, this list is also iterated elsewhere (e.g., line 486: `if (isActive && !selectedFleetAccounts.Contains(acct.Name))`). The `.Contains()` + `.Add()` pattern in the `Checked` handler (lines 503-504) is a check-then-act TOCTOU window. If two CheckBox events were somehow queued before the first handler completes (e.g., from programmatic `IsChecked` changes in `selectAllCheck.Checked` handler at lines 495-500), the list could receive duplicates.

More critically: if any code path reads `selectedFleetAccounts` from a non-UI thread (e.g., snapshot, compliance, or IPC), it would encounter undefined behavior from `List<T>` during concurrent modification.
**Evidence**: `private List<string> selectedFleetAccounts = new List<string>();` at line 35. `selectedFleetAccounts.Add(accountName);` at line 504 inside `cb.Checked +=`. `selectedFleetAccounts.Remove(accountName);` at line 512 inside `cb.Unchecked +=`.
**Test Impact**: Rapidly toggling fleet checkboxes or using "Select All" during fleet popup open could produce duplicate entries. Convert to `HashSet<string>` for O(1) dedup.

---

### BUG-S3-007
**Title**: `Thread.Sleep()` on IPC listener and client stream threads
**Severity**: Medium
**Location**: `V12_002.UI.IPC.Server.cs.ListenForRemote` (line 85), `ProcessClientStream_ReadChunk` (line 214)
**Root Cause**: Two `Thread.Sleep()` calls in the IPC server:
1. Line 85: `Thread.Sleep(100)` in the listener accept loop when no pending connections. This blocks the IPC listener thread for 100ms per iteration, adding up to 100ms latency for new client connections.
2. Line 214: `Thread.Sleep(50)` in `ProcessClientStream_ReadChunk` when `!stream.DataAvailable`. This blocks each client handler thread for 50ms when no data is ready, adding latency to command processing.

These are on dedicated background threads (not the strategy or UI thread), so they do not cause freezes. However, they violate the V12 performance standard and waste thread pool resources. Under load with many clients, each blocked thread consumes ~1MB of stack space.
**Evidence**: `Thread.Sleep(100);` at `V12_002.UI.IPC.Server.cs:85` inside `while (isIpcRunning)`. `Thread.Sleep(50);` at `V12_002.UI.IPC.Server.cs:214` inside `ProcessClientStream_ReadChunk`.
**Test Impact**: IPC command latency increases by up to 50ms per command when commands arrive in rapid succession. Replace with `async/await` + `Stream.ReadAsync` with `CancellationToken`.

---

### BUG-S3-008
**Title**: Compliance daily reset writes are non-atomic across three dictionaries
**Severity**: Medium
**Location**: `V12_002.UI.Compliance.cs.MaybeFinalizeDailySummaries` (lines 198-200)
**Root Cause**: When a new trading day is detected, three separate dictionary writes reset daily counters:
```csharp
accountDailyProfit[acct.Name] = 0;          // line 198
accountDailyTradeCount[acct.Name] = 0;      // line 199
accountLastSummaryDate[acct.Name] = nowInZone.Date; // line 200
```
Between lines 198 and 199, a concurrent read from `BuildUiComplianceSnapshot` (in `V12_002.UI.Snapshot.cs`) could read `accountDailyProfit` as 0 but `accountDailyTradeCount` as the previous day's value, producing a compliance display showing "$0 daily PL with 47 trades" -- a nonsensical state that violates correctness-by-construction.

Similarly, line 102 (`accountDailyProfit[acct.Name] = dailyPL;` in `UpdateAccountMetricsFromAccount`) writes a single dict entry without coordinating with related metrics.
**Evidence**: Three separate indexer writes at lines 198-200 of `V12_002.UI.Compliance.cs`. `BuildUiComplianceSnapshot` in `V12_002.UI.Snapshot.cs` reads each independently with `TryGetValue`.
**Test Impact**: Compliance display on the panel could show torn daily summary during midnight rollover. Bundle daily metrics into a struct and swap atomically via `Interlocked.Exchange`.

---

### BUG-S3-009
**Title**: `PopulateDirectionCombo` clears and rebuilds WPF ItemsCollection on every mode change
**Severity**: Medium
**Location**: `V12_002.UI.Panel.Handlers.cs.PopulateDirectionCombo` (lines 591-605)
**Root Cause**: `PopulateDirectionCombo` calls `directionCombo.Items.Clear()` then `.Add()` for each item on every mode change. This is called from `UpdateContextualUI`, which is called from `UpdatePanelState` (every 250ms via the refresh timer) whenever `_panelLastSyncedMode` changes. While `UpdatePanelState` runs on the WPF dispatcher (so the collection modification is thread-safe), clearing and rebuilding the items collection on every mode change causes WPF to destroy and recreate visual elements, creating GC pressure and potential visual flicker.

The more concerning issue: if the user has the ComboBox dropdown open when `UpdatePanelState` fires a mode change, the `Items.Clear()` will close the dropdown and may cause an `InvalidOperationException` if WPF is mid-layout.
**Evidence**: `directionCombo.Items.Clear();` at line 594. Called from `UpdateContextualUI(mode)` at `V12_002.UI.Panel.StateSync.cs:37`, which is called from `UpdatePanelState()` every 250ms when mode changes.
**Test Impact**: User opens direction combo dropdown, mode changes in background -> dropdown closes unexpectedly. In worst case, `InvalidOperationException` during layout pass.

---

### BUG-S3-010
**Title**: IPC `SendResponseToRemote` does not synchronize stream writes across concurrent callers
**Severity**: Low
**Location**: `V12_002.UI.IPC.Commands.Misc.cs.SendResponseToRemote` (lines 188-227)
**Root Cause**: `SendResponseToRemote` iterates `connectedClients.ToArray()` and writes `responseBytes` to each client's `NetworkStream`. Multiple callers can invoke this concurrently (e.g., a compliance snapshot publish + a fleet state response arriving simultaneously). Each `session.Stream.Write()` call is NOT thread-safe per the .NET documentation for `NetworkStream`. Concurrent writes to the same stream can interleave bytes, corrupting the message framing on the receiving end. The panel would receive garbled IPC responses.

While `TryRemove` on disconnected clients is safe, the actual stream write has no mutual exclusion.
**Evidence**: `session.Stream.Write(responseBytes, 0, responseBytes.Length);` at line ~210. No lock or semaphore around the stream. Multiple callers: `HandleFleet_GetFleet`, `HandleFleet_RequestFleetState`, `HandleIncomingIpcLine_RespondLayout`, `TryHandleMode_SyncMode`.
**Test Impact**: Under concurrent IPC responses, panel receives interleaved/garbled messages. Add a per-client write lock or queue outbound messages.

---

### BUG-S3-011
**Title**: Photon Pool `_freeTop` is volatile but documented as single-threaded
**Severity**: Low
**Location**: `V12_002.Photon.Pool.cs` -- `PhotonOrderPool` class (lines 74-150)
**Root Cause**: The `PhotonOrderPool` class is documented as "MUST be called from strategy thread only. Not safe for concurrent access." However, `_freeTop` is marked `volatile` and all operations use `Interlocked`. This creates a misleading contract: the implementation suggests thread-safety but the documentation denies it. If a future developer reads from another thread based on the `Interlocked` signals, they would encounter unsafe access to `_orderArrays` and `_freeStack` which are NOT protected.

This is not a current bug but violates "correctness by construction" -- the implementation should match the documented contract. Either remove `volatile`/`Interlocked` (if truly single-threaded) or make the class fully concurrent.
**Evidence**: `private volatile int _freeTop;` at line 80. `Interlocked.Decrement(ref _freeTop)` at line 117. Comment at line 74: "THREADING: MUST be called from strategy thread only."
**Test Impact**: Future refactoring risk. No current runtime impact.

---

### BUG-S3-012
**Title**: IPC listener `isIpcRunning` is plain bool without volatile
**Severity**: Low
**Location**: `V12_002.UI.IPC.Server.cs.ListenForRemote` (line 75), `StopIpcServer` (line 275)
**Root Cause**: `isIpcRunning` is a plain `bool` field written by `StopIpcServer` (line 275: `isIpcRunning = false;`) on the calling thread and read by `ListenForRemote` (line 75: `while (isIpcRunning)`) on the IPC listener thread. Without `volatile` or `Volatile.Read`, the listener thread may cache the value and never observe the write, causing the listener loop to continue indefinitely after `StopIpcServer` is called. The `ipcListener.Stop()` call would cause `AcceptTcpClient()` to throw, which exits the loop via the catch block, but the `isIpcRunning` flag itself is not reliable.

This is mitigated in practice because `ipcListener.Stop()` throws and the catch block sets `isIpcRunning = false`, but the initial read in the `while` condition is still technically a data race.
**Evidence**: `while (isIpcRunning)` at line 75. `isIpcRunning = false;` at line 275. Field declaration not visible in scanned files (likely in main `V12_002.cs`), but no `volatile` keyword usage found in IPC server file.
**Test Impact**: In rare cases on multi-core systems, the listener thread may not see `isIpcRunning = false` for several iterations. Mark field as `volatile`.

---

### BUG-S3-013
**Title**: `GetATRMultiplierForPosition` had a typo bug -- `isTrendRmaMode` used instead of `isRetestRmaMode`
**Severity**: Low
**Location**: `V12_002.UI.Sizing.cs.GetATRMultiplierForPosition` (line ~185)
**Root Cause**: The comment on line ~185 states: `// V12.Hardening: was isTrendRmaMode (typo)`. This indicates a historical bug where `isTrendRmaMode` was used in the `IsRetestTrade` branch instead of `isRetestRmaMode`. The current code appears to be fixed (`return isRetestRmaMode ? RMAStopATRMultiplier : RetestATRMultiplier;`), but the comment itself is evidence of a recent fix.

The concern is whether there are similar mode-flag typos elsewhere in the codebase. A search for cross-mode flag usage patterns would be prudent.
**Evidence**: `V12_002.UI.Sizing.cs` line ~185: `return isRetestRmaMode ? RMAStopATRMultiplier : RetestATRMultiplier; // V12.Hardening: was isTrendRmaMode (typo)`
**Test Impact**: Previously, retest trades would use the wrong ATR multiplier when trend RMA mode was active but retest RMA mode was not (or vice versa). If the fix has not been validated in production, this is a latent risk.

---

## Appendix: Files Scanned

### UI Cluster (16 files)
1. `src/V12_002.UI.Callbacks.cs` (994 lines)
2. `src/V12_002.UI.Compliance.cs` (666 lines)
3. `src/V12_002.UI.IPC.Commands.Config.cs` (387 lines)
4. `src/V12_002.UI.IPC.Commands.Fleet.cs` (580 lines)
5. `src/V12_002.UI.IPC.Commands.Misc.cs` (350 lines)
6. `src/V12_002.UI.IPC.Commands.Mode.cs` (335 lines)
7. `src/V12_002.UI.IPC.cs` (422 lines)
8. `src/V12_002.UI.IPC.Server.cs` (298 lines)
9. `src/V12_002.UI.Panel.Brushes.cs` (76 lines)
10. `src/V12_002.UI.Panel.Construction.cs` (1191 lines)
11. `src/V12_002.UI.Panel.Handlers.cs` (720 lines)
12. `src/V12_002.UI.Panel.Helpers.cs` (716 lines)
13. `src/V12_002.UI.Panel.Lifecycle.cs` (128 lines)
14. `src/V12_002.UI.Panel.StateSync.cs` (390 lines)
15. `src/V12_002.UI.Sizing.cs` (200 lines)
16. `src/V12_002.UI.Snapshot.cs` (220 lines)

### Photon IO Cluster (3 files)
17. `src/V12_002.Photon.MmioMirror.cs` (128 lines)
18. `src/V12_002.Photon.Pool.cs` (280 lines)
19. `src/V12_002.Photon.Ring.cs` (80 lines)

---

## Observations: Photon Cluster Quality

The Photon cluster (`MmioMirror`, `Pool`, `Ring`) is the highest-quality code in this scan:
- **SPSCRing**: Correct lock-free SPSC implementation with `Volatile.Read`/`Volatile.Write` barriers, cache-line padding to prevent false sharing, and power-of-2 capacity enforcement.
- **MmioDispatchMirror**: Proper single-writer MMIO pattern with `Thread.MemoryBarrier()` before cursor publish, disposal guard via `Interlocked`, and clean separation of header/slot offsets.
- **PhotonOrderPool**: Clean O(1) slot claim/release via free-stack with `Interlocked` guards. Index-based retrieval eliminates O(N) reference scan.
- **FleetDispatchSlot**: Correctly blittable with explicit layout, cache-line sizing (64 bytes), and XorShadow integrity via `ComputeFleetDispatchShadow`.
- **ExecutionIdRing**: Proper open-addressing hash table with Robin Hood deletion and ring-based eviction.

No critical or high-severity bugs found in the Photon cluster. Only BUG-S3-011 (documentation/implementation mismatch on threading contract) was identified.

---

## Post-Use Skill Audit
skill(audit): No gaps identified. The forensic scan covered all 19 files with line-specific evidence.
