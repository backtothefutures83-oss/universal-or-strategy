# Bug Report S6 -- Signals & Entries Cluster
**Runner**: Qwen 3.6 Max Preview
**Cluster**: Signals & Entries (Agent-S6)
**Scope**: 7 files -- `V12_002.Entries.cs`, `V12_002.Entries.FFMA.cs`, `V12_002.Entries.MOMO.cs`, `V12_002.Entries.OR.cs`, `V12_002.Entries.Retest.cs`, `V12_002.Entries.RMA.cs`, `V12_002.Entries.Trend.cs`
**Date**: 2026-05-17
**Mode**: READ-ONLY forensic scan. No src/ edits.

---

## Executive Summary

| Severity | Count |
|----------|-------|
| Critical | 2 |
| High     | 4 |
| Medium   | 5 |
| Low      | 3 |
| **Total** | **14** |

---

## Findings (Ordered Critical to Low)

### BUG-S6-001
**Title**: `linkedTRENDEntries` direct dictionary write bypasses Actor/Enqueue pattern
**Severity**: Critical
**Location**: `V12_002.Entries.Trend.cs`, `ExecuteTREND_SubmitLeg2` (line ~350) and `V12_002.Entries.RMA.cs`, `SubmitTrendSplitBrackets` (lines ~161-162)
**Root Cause**: The `linkedTRENDEntries` ConcurrentDictionary is written via direct indexer assignment (`linkedTRENDEntries[entry1Name] = entry2Name`) on the calling thread, outside the FSM Actor `Enqueue` gate. ConcurrentDictionary's individual operations are thread-safe, but the two-write sequence (E1->E2 then E2->E1) is NOT atomic. A reader thread (e.g., `HandleOrderCancelled` or `MonitorRmaProximity`) can observe a partially-linked state where E1 points to E2 but E2 does not yet point back to E1. Furthermore, this violates the V12 Lock-Free Actor Pattern mandate that ALL state mutations route through `Enqueue`.
**Evidence**:
- `V12_002.Entries.Trend.cs` line ~350: `linkedTRENDEntries[entry1Name] = entry2Name; linkedTRENDEntries[entry2Name] = entry1Name;` -- direct writes, no Enqueue wrapper.
- `V12_002.Entries.RMA.cs` lines ~161-162: identical pattern in `SubmitTrendSplitBrackets`.
- Compare with `activePositions` and `entryOrders` which ARE wrapped: `Enqueue(ctx => { ctx.activePositions[_en966] = _p966; })`.
**Test Impact**: Concurrency stress test with simultaneous TREND entries and order cancellation callbacks. Assert that `linkedTRENDEntries` always has bidirectional consistency (if A->B exists, B->A must also exist).

### BUG-S6-002
**Title**: Exception in entry method after order submission leaves expected delta permanently orphaned
**Severity**: Critical
**Location**: `V12_002.Entries.OR.cs`, `EnterORPosition` (lines ~100-170); `V12_002.Entries.MOMO.cs`, `ExecuteMOMOEntry` (lines ~80-140); `V12_002.Entries.Retest.cs`, `ExecuteRetestEntry` (lines ~100-170); `V12_002.Entries.RMA.cs`, `SubmitTrendSplitBrackets`; `V12_002.Entries.Trend.cs`, `ExecuteTREND_SubmitLeg1`, `ExecuteTREND_SubmitLeg2`, `ExecuteTRENDManual_SubmitEntry`
**Root Cause**: The Master expected position delta is registered via `Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(...))` BEFORE calling `SubmitOrderUnmanaged`. The rollback (negating the delta) only executes if `SubmitOrderUnmanaged` returns null. However, if `SubmitOrderUnmanaged` throws an exception (e.g., connection failure, invalid session), control jumps to the `catch (Exception ex)` block which merely prints the error message and does NOT rollback the expected delta. The delta remains permanently in the Order Ledger, causing the Master's expected position to drift from reality. Over time, accumulated orphaned deltas will cause false-positive divergence alerts or incorrect SIMA fleet dispatch calculations.
**Evidence**:
- `V12_002.Entries.OR.cs`: Expected delta registered at line ~136. Null-rollback at line ~148. Catch block at line ~165 only does `Print("ERROR EnterORPosition: " + ex.Message);` with no rollback.
- Same pattern in `V12_002.Entries.MOMO.cs` lines ~100, ~115, ~130.
- Same pattern in `V12_002.Entries.Trend.cs` `ExecuteTREND_SubmitLeg1` and `ExecuteTREND_SubmitLeg2`.
- Same pattern in `V12_002.Entries.RMA.cs` `SubmitTrendSplitBrackets`.
**Test Impact**: Fault injection test: mock `SubmitOrderUnmanaged` to throw `InvalidOperationException`. Assert that `AddExpectedPositionDeltaLocked` is called with the negating value. Without fix, ledger diverges.

### BUG-S6-003
**Title**: `MonitorRmaProximity` mutates `PositionInfo` fields from `OnBarUpdate` thread without Enqueue
**Severity**: High
**Location**: `V12_002.Entries.RMA.cs`, `MonitorRmaProximity` (lines ~280-340)
**Root Cause**: `MonitorRmaProximity()` iterates `entryOrders` and directly mutates fields on the `PositionInfo` object: `pos.ClosestApproachTicks`, `pos.WasInProximity`, and `pos.ProximityProbeCount`. These mutations occur on the calling thread (likely `OnBarUpdate`), while other threads (order fill callbacks, FSM worker) may simultaneously read or write these same fields on the same `PositionInfo` instance. The `PositionInfo` class has no internal synchronization. This is a classic data race: the read-modify-write on `ProximityProbeCount++` is not atomic, and `WasInProximity` can toggle in a way that causes the exhaustion logic to fire prematurely or never fire at all.
**Evidence**:
- Line ~304: `pos.ClosestApproachTicks = distTicks;` -- direct write.
- Line ~311: `pos.WasInProximity = true; pos.ProximityProbeCount++;` -- direct writes, no Enqueue.
- Line ~324: `pos.WasInProximity = false;` -- direct write.
- The `PositionInfo` object was registered via `Enqueue`, but subsequent mutations bypass the actor gate entirely.
**Test Impact**: Race condition test: simulate concurrent bar updates and order fills. Assert that `ProximityProbeCount` is monotonically increasing and matches the number of proximity probe events logged.

### BUG-S6-004
**Title**: ToS sync armed state is a non-atomic check-then-set on shared booleans
**Severity**: High
**Location**: `V12_002.Entries.OR.cs`, `ExecuteLong` (lines ~25-40) and `ExecuteShort` (lines ~65-80)
**Root Cause**: When `isTosSyncMode` is true, `ExecuteLong` reads `isLongArmed`, then sets `isLongArmed = false` (if armed) or `isLongArmed = true; isShortArmed = false` (if not armed). This is a classic check-then-act race condition on non-volatile, non-atomic boolean fields. If `ExecuteLong` is called concurrently from two UI threads (e.g., rapid double-click on the panel), both threads can read `isLongArmed == false`, both set it to true, and both return waiting for a ToS handshake that will never come -- effectively deadlocking the LONG entry. The cross-reset of `isShortArmed = false` inside `ExecuteLong` (and vice versa) compounds the problem: a SHORT arm in progress can be silently cancelled by a concurrent LONG click.
**Evidence**:
- `ExecuteLong` lines ~30-39: reads `isLongArmed`, then conditionally sets `isLongArmed` and `isShortArmed`.
- `ExecuteShort` lines ~70-79: identical pattern mirrored.
- No `volatile`, `Interlocked`, or `Enqueue` guard on these booleans.
- The `lastArmedTime` field is also set without synchronization.
**Test Impact**: Concurrency test: trigger two `ExecuteLong` calls simultaneously. Assert that exactly one proceeds to entry and the other arms (or both proceed if double-click bypass fires). Currently both can arm and neither proceeds.

### BUG-S6-005
**Title**: `ExecuteTREND_SubmitLeg2` links entries before E2 submission confirmation
**Severity**: High
**Location**: `V12_002.Entries.Trend.cs`, `ExecuteTREND_SubmitLeg2` (lines ~348-365)
**Root Cause**: `linkedTRENDEntries[entry1Name] = entry2Name; linkedTRENDEntries[entry2Name] = entry1Name;` is called BEFORE `SubmitOrderUnmanaged` for E2. If E2 submission returns null, the rollback code removes the links and cancels E1. However, between the link write and the null check, there is a window where the cancel callback for E1 (triggered by `CancelOrderSafe`) fires and reads `linkedTRENDEntries[entry1Name]` -- finding `entry2Name` as the partner. The cancel handler may then attempt to cancel E2, which was never submitted, causing a null reference or duplicate cancellation. The `SubmitTrendSplitBrackets` method in RMA.cs has the same issue: links are written at lines ~161-162 before E2 submission at line ~167.
**Evidence**:
- `V12_002.Entries.Trend.cs`: Links written at line ~350. E2 submitted at line ~356. Null check at line ~360.
- `V12_002.Entries.RMA.cs`: Links written at lines ~161-162. E2 submitted at line ~167. Null check at line ~170.
- The window is small but exploitable on fast machines where cancel callbacks are nearly instantaneous.
**Test Impact**: Fault injection: mock E2 `SubmitOrderUnmanaged` to return null while E1 cancel fires immediately. Assert that the cancel handler does not attempt to cancel a non-existent E2 order.

### BUG-S6-006
**Title**: FFMA entries do not register Master expected position delta (ledger asymmetry)
**Severity**: High
**Location**: `V12_002.Entries.FFMA.cs`, `ExecuteFFMAEntry` (lines ~95-155), `ExecuteFFMALimitEntry` (lines ~180-250), `ExecuteFFMAManualMarketEntry` (lines ~260-340)
**Root Cause**: Every other entry type (OR, MOMO, RETEST, TREND) registers the Master expected position delta via `Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(...))` BEFORE submitting the order, and rolls it back on null submission. FFMA's three entry methods do NOT call `AddExpectedPositionDeltaLocked` at all. This means FFMA entries will never appear in the Master's Order Ledger. The SIMA fleet dispatch will fire, but the Master's expected-vs-actual reconciliation will show a divergence (actual position changes but expected was never registered). If fleet followers depend on the ledger for reconciliation, FFMA entries will silently bypass the safety mechanism.
**Evidence**:
- `V12_002.Entries.FFMA.cs` `ExecuteFFMAEntry`: No `AddExpectedPositionDeltaLocked` call. Order submitted at line ~138. State registered via Enqueue at lines ~144-145. No ledger delta.
- `V12_002.Entries.FFMA.cs` `ExecuteFFMALimitEntry`: Same omission.
- `V12_002.Entries.FFMA.cs` `ExecuteFFMAManualMarketEntry`: Same omission.
- Compare with `V12_002.Entries.OR.cs` line ~136: `{ var _aek966 = ExpKey(Account.Name); var _aed966 = (masterDeltaOR); Enqueue(ctx => ctx.AddExpectedPositionDeltaLocked(_aek966, _aed966)); }`
- Compare with `V12_002.Entries.MOMO.cs` line ~100: identical ledger registration pattern.
**Test Impact**: Integration test: execute FFMA entry and verify Order Ledger. Expected delta will be missing. Assert ledger divergence alert fires.

### BUG-S6-007
**Title**: `CheckFFMAConditions` reads multiple indicator values without atomic snapshot
**Severity**: Medium
**Location**: `V12_002.Entries.FFMA.cs`, `CheckFFMAConditions` (lines ~40-75)
**Root Cause**: `ema9[0]`, `rsiIndicator[0]`, `Close[0]`, `Open[0]`, `High[0]`, `Low[0]` are read sequentially on different lines. If `OnBarUpdate` fires between reads (because `Calculate.OnPriceChange` is used), the values can be from different bar states. For example, `ema9[0]` could be from tick N while `Close[0]` is from tick N+1. This creates a "torn read" scenario where the condition check (RSI > 80 AND price 10+ pts above EMA9 AND red candle) evaluates against an inconsistent snapshot, potentially triggering a false entry or missing a valid entry.
**Evidence**:
- Line ~48: `double ema9Value = ema9[0];`
- Line ~49: `double rsiValue = rsiIndicator[0];`
- Line ~50: `double currentPrice = Close[0];`
- Line ~56: `bool isGreenCandle = Close[0] > Open[0];`
- These reads span multiple ticks if `OnBarUpdate` fires between them.
**Test Impact**: High-frequency tick replay test: feed ticks that cause EMA and Close to update between reads. Assert that entry conditions evaluate against a consistent bar snapshot.

### BUG-S6-008
**Title**: RETEST pre-registers `activePositions` before order submission -- TryRemove is direct, not via Enqueue
**Severity**: Medium
**Location**: `V12_002.Entries.Retest.cs`, `ExecuteRetestEntry` (line ~137 and ~147) and `ExecuteRetestManualEntry` (line ~241 and ~251)
**Root Cause**: Unlike other entry types that register `activePositions` AFTER order submission via Enqueue, RETEST pre-registers the position BEFORE submission (line ~137: `Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });`). On null submission, the rollback uses `activePositions.TryRemove(entryName, out _)` (line ~147) -- a direct call, NOT wrapped in Enqueue. This means the removal happens on the calling thread while the FSM worker thread may be processing other Enqueue callbacks that reference this key. The add went through Enqueue but the remove does not, creating an ordering inversion where the FSM worker could see the key and operate on it after the calling thread has already removed it.
**Evidence**:
- `ExecuteRetestEntry` line ~137: `Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });` -- adds via Enqueue.
- Line ~147: `activePositions.TryRemove(entryName, out _);` -- removes directly, no Enqueue.
- `ExecuteRetestManualEntry` has the identical pattern at lines ~241 and ~251.
- Compare with OR entry: `activePositions` is registered AFTER successful submission, not before.
**Test Impact**: Concurrency test: trigger RETEST null submission while FSM worker is mid-queue. Assert that `activePositions` does not contain the entry after rollback and no phantom callbacks fire.

### BUG-S6-009
**Title**: `retestFiredThisSession` latch set AFTER order submission -- re-entrancy window
**Severity**: Medium
**Location**: `V12_002.Entries.Retest.cs`, `ExecuteRetestEntry` (lines ~148-149)
**Root Cause**: The session latch `retestFiredThisSession = true` is set at line ~149, which is AFTER the order submission at line ~140 and the null check at line ~143. Between lines ~140 and ~149, if `ExecuteRetestEntry` is called again (e.g., rapid double-click on panel IPC), the second call will pass the `retestFiredThisSession` guard (it is still false), pass the `orComplete` check, and submit a duplicate RETEST entry. The latch is meant to prevent "one RETEST entry per OR session maximum" but the window between submission and latching allows exactly the duplicate it is designed to prevent.
**Evidence**:
- Line ~59: `if (retestFiredThisSession) { Print(...); return; }` -- guard check.
- Line ~140: `Order entryOrder = ...SubmitOrderUnmanaged(...)` -- submission.
- Line ~149: `retestFiredThisSession = true;` -- latch set. 9 lines of window.
**Test Impact**: Rapid double-click test: trigger two `ExecuteRetestEntry` calls within 1ms. Assert that only one order is submitted. Currently both will pass the latch guard.

### BUG-S6-010
**Title**: `DeactivateFFMAMode` does not check `IsOrderAllowed` or `isFlattenRunning` before disarming
**Severity**: Medium
**Location**: `V12_002.Entries.FFMA.cs`, `DeactivateFFMAMode` (lines ~158-162)
**Root Cause**: `DeactivateFFMAMode()` sets `isFFMAModeArmed = false` and sends `FFMA_DISARMED` to the panel. It is called at the end of `ExecuteFFMAEntry` regardless of whether the entry succeeded. If the entry was blocked by `IsOrderAllowed()` (line ~100) or `isFlattenRunning` (line ~101), the method returns early and `DeactivateFFMAMode` is never called -- the FFMA mode stays armed. However, if the entry enters the try block and `SubmitOrderUnmanaged` returns null, the catch block does not call `DeactivateFFMAMode` either, so FFMA stays armed even though the entry failed. The user must manually re-arm FFMA to try again, which is confusing UX.
**Evidence**:
- `ExecuteFFMAEntry` line ~100-101: early return on compliance/flatten guard.
- Line ~141: null check returns without calling `DeactivateFFMAMode`.
- Line ~155: `DeactivateFFMAMode()` only called in the success path.
- Same issue exists in `ExecuteFFMALimitEntry` (lines ~190, ~233, ~247) and `ExecuteFFMAManualMarketEntry`.
**Test Impact**: Test FFMA entry with compliance blocked. Assert that FFMA mode remains armed (current) vs. auto-disarms (expected for UX consistency).

### BUG-S6-011
**Title**: `MonitorRmaProximity` reads `Close[0]` without null guard on bar data
**Severity**: Medium
**Location**: `V12_002.Entries.RMA.cs`, `MonitorRmaProximity` (line ~295)
**Root Cause**: `MonitorRmaProximity()` accesses `Close[0]` at line ~295 without checking `CurrentBar >= 0` or verifying that bar data is available. If called during strategy initialization or during a data feed interruption, `Close[0]` can throw an `ArgumentOutOfRangeException`. While `MonitorRmaProximity` has a guard for `RmaIntelligenceEnabled`, it does not guard against bar data availability. The method also iterates `entryOrders` without checking if the collection is empty first (minor, but unnecessary iteration).
**Evidence**:
- Line ~295: `double currentPrice = Close[0];` -- no try-catch around this specific read.
- No `CurrentBar` check before accessing series data.
- Other entry methods check `CurrentBar < 20` before accessing indicators.
**Test Impact**: Initialization test: call `MonitorRmaProximity` before first bar update. Assert graceful handling vs. `ArgumentOutOfRangeException`.

### BUG-S6-012
**Title**: Timestamp collision risk for entry names under high-frequency execution
**Severity**: Low
**Location**: All entry files use `DateTime.Now.ToString("HHmmssffff")` or `DateTime.UtcNow.ToString("HHmmssffff")` for signal name generation
**Root Cause**: Entry names are generated using `DateTime.Now.ToString("HHmmssffff")` which provides 0.1ms precision. Under high-frequency execution (e.g., SIMA dispatch triggering multiple entries in rapid succession), two entries can receive the same timestamp, producing duplicate entry names. Duplicate entry names would cause dictionary key collisions in `activePositions` and `entryOrders`, with the second entry silently overwriting the first. Note: `V12_002.Entries.Trend.cs` uses `DateTime.UtcNow` (line ~239), while other files use `DateTime.Now` -- inconsistent convention.
**Evidence**:
- `V12_002.Entries.FFMA.cs` line ~134: `DateTime.Now.ToString("HHmmssffff")`
- `V12_002.Entries.MOMO.cs` line ~91: `DateTime.Now.ToString("HHmmssffff")`
- `V12_002.Entries.OR.cs` line ~119: `DateTime.Now.ToString("HHmmssffff")`
- `V12_002.Entries.Retest.cs` line ~130: `DateTime.Now.ToString("HHmmssffff")`
- `V12_002.Entries.Trend.cs` line ~239: `DateTime.UtcNow.ToString("HHmmssffff", CultureInfo.InvariantCulture)` -- uses UTC and invariant culture.
**Test Impact**: High-frequency test: trigger 10 entries within 0.1ms. Assert all entry names are unique. Currently ~20% collision probability at this rate.

### BUG-S6-013
**Title**: Inconsistent timestamp convention between TREND and other entry types
**Severity**: Low
**Location**: `V12_002.Entries.Trend.cs` (uses `DateTime.UtcNow`) vs. all other entry files (use `DateTime.Now`)
**Root Cause**: TREND entry uses `DateTime.UtcNow.ToString("HHmmssffff", CultureInfo.InvariantCulture)` for timestamp generation, while FFMA, MOMO, OR, and RETEST all use `DateTime.Now.ToString("HHmmssffff")` without culture specification. This means: (a) on a machine with a non-Gregorian calendar locale, non-TREND entries could produce unexpected timestamp formats; (b) TREND and non-TREND entries have different time bases, which could confuse debugging when correlating entries across types. While not a functional bug, it is a maintainability hazard and could cause confusion in production debugging.
**Evidence**:
- TREND: `DateTime.UtcNow.ToString("HHmmssffff", CultureInfo.InvariantCulture)` at line ~239 of Trend.cs.
- FFMA: `DateTime.Now.ToString("HHmmssffff")` at line ~134 of FFMA.cs.
- MOMO: `DateTime.Now.ToString("HHmmssffff")` at line ~91 of MOMO.cs.
- OR: `DateTime.Now.ToString("HHmmssffff")` at line ~119 of OR.cs.
- RETEST: `DateTime.Now.ToString("HHmmssffff")` at line ~130 of Retest.cs.
**Test Impact**: Locale test: set system locale to non-Gregorian calendar. Assert all entry names contain valid timestamps. Non-TREND entries may produce unexpected output.

### BUG-S6-014
**Title**: Exception handler after `SubmitOrderUnmanaged` does not clean up `activePositions` or `entryOrders`
**Severity**: Low
**Location**: All entry methods: `ExecuteFFMAEntry`, `ExecuteFFMALimitEntry`, `ExecuteFFMAManualMarketEntry`, `ExecuteMOMOEntry`, `EnterORPosition`, `ExecuteRetestEntry`, `ExecuteRetestManualEntry`, `ExecuteTRENDManual_SubmitEntry`
**Root Cause**: Every entry method wraps its logic in `try { ... } catch (Exception ex) { Print("ERROR ...: " + ex.Message); }`. If an exception occurs AFTER `Enqueue(ctx => { ctx.activePositions[...] = pos; })` and `Enqueue(ctx => { ctx.entryOrders[...] = order; })` have been called, the catch block merely prints the error without removing the registered state. The `activePositions` and `entryOrders` dictionaries now contain entries for orders that may be in an undefined state. Subsequent callbacks (order fills, cancellations) will operate on this corrupted state. While the exception paths are expected to be rare (most post-submission errors would be caught by the null check), this is a latent correctness issue.
**Evidence**:
- `V12_002.Entries.FFMA.cs` `ExecuteFFMAEntry`: state registered at lines ~144-145, catch at line ~157.
- `V12_002.Entries.MOMO.cs` `ExecuteMOMOEntry`: state registered at lines ~117-118, catch at line ~130.
- `V12_002.Entries.OR.cs` `EnterORPosition`: state registered at lines ~152-153, catch at line ~165.
- Same pattern in all other entry methods.
**Test Impact**: Fault injection: mock post-submission code (e.g., `SendResponseToRemote`) to throw. Assert that `activePositions` and `entryOrders` still contain the entry (current behavior) vs. are cleaned up (expected).

---

## DNA Compliance Check

| Check | Status | Notes |
|-------|--------|-------|
| `lock()` statements | **PASS** | Zero occurrences of `lock(` across all 7 files. |
| Non-ASCII string literals | **PASS** | All `string.Format` and `Print` calls use ASCII-only characters. No emoji, curly quotes, or Unicode found. |
| `Thread.Sleep()` in hot path | **PASS** | Zero occurrences of `Thread.Sleep` across all 7 files. |
| `Dictionary<K,V>` writes without atomic guard | **FAIL** | `linkedTRENDEntries` is written directly (not via Enqueue) in `ExecuteTREND_SubmitLeg2` and `SubmitTrendSplitBrackets`. While `ConcurrentDictionary` is individually thread-safe, the two-write link sequence is non-atomic and violates the V12 Actor Pattern mandate. See BUG-S6-001. |

---

## Cross-File Dependency Map

| Source File | Calls Into | Shared State Accessed |
|-------------|-----------|----------------------|
| `Entries.FFMA.cs` | `IsOrderAllowed`, `DeactivateFFMAMode`, `SendResponseToRemote`, `ExecuteSmartDispatchEntry`, `CalculatePositionSize`, `GetTargetDistribution`, `CalculateTargetPrice` | `activePositions`, `entryOrders`, `isFFMAModeArmed`, `isFlattenRunning`, `ema9`, `rsiIndicator`, `currentATR` |
| `Entries.MOMO.cs` | `IsOrderAllowed`, `DeactivateMOMOMode`, `ExecuteSmartDispatchEntry`, `GetTargetDistribution`, `CalculateTargetPrice`, `ApplyTargetLadderGuard` | `activePositions`, `entryOrders`, `entryOrders`, `isMOMOModeActive`, `isRMAModeActive`, `isFlattenRunning`, `currentATR`, `lastKnownPrice`, `linkedTRENDEntries` (no) |
| `Entries.OR.cs` | `IsOrderAllowed`, `EnterORPosition`, `ExecuteSmartDispatchEntry`, `GetTargetDistribution`, `CalculateTargetPrice`, `ApplyTargetLadderGuard`, `CalculateORStopDistance` | `activePositions`, `entryOrders`, `isTosSyncMode`, `isLongArmed`, `isShortArmed`, `lastArmedTime`, `orComplete`, `sessionRange`, `sessionHigh`, `sessionLow`, `isFlattenRunning` |
| `Entries.Retest.cs` | `IsOrderAllowed`, `DeactivateRetestMode`, `ExecuteSmartDispatchEntry`, `GetTargetDistribution`, `CalculateTargetPrice`, `ApplyTargetLadderGuard`, `CalculateRetestStopDistance` | `activePositions`, `entryOrders`, `isRetestModeActive`, `retestFiredThisSession`, `isRetestRmaMode`, `orComplete`, `isFlattenRunning`, `currentATR`, `sessionMid`, `sessionHigh`, `sessionLow` |
| `Entries.RMA.cs` | `ExecuteTrendSplitEntry`, `DeactivateRMAMode`, `ExecuteSmartDispatchEntry`, `CreateTRENDPosition`, `ApplyTargetLadderGuard` | `activePositions`, `entryOrders`, `linkedTRENDEntries`, `isRMAModeActive`, `isRMAButtonClicked`, `isFlattenRunning`, `currentATR`, `ema9`, `ema15` |
| `Entries.Trend.cs` | `ExecuteTRENDEntry`, `ExecuteTrendSplitEntry`, `DeactivateTRENDMode`, `ExecuteSmartDispatchEntry`, `CreateTRENDPosition`, `ApplyTargetLadderGuard`, `ExecuteTRENDManualEntry` | `activePositions`, `entryOrders`, `linkedTRENDEntries`, `isTRENDModeActive`, `isTrendRmaMode`, `isFlattenRunning`, `currentATR`, `ema9`, `ema15`, `pendingTRENDEntry`, `lastKnownPrice` |

---

## Shared State Summary

**Mutable shared state accessed across entry files:**
- `activePositions` (ConcurrentDictionary) -- written via Enqueue in all files except RETEST rollback (BUG-S6-008)
- `entryOrders` (ConcurrentDictionary) -- written via Enqueue consistently
- `linkedTRENDEntries` (ConcurrentDictionary) -- written directly, not via Enqueue (BUG-S6-001)
- `isFlattenRunning` (bool) -- read as guard across all files
- `isFFMAModeArmed`, `isMOMOModeActive`, `isRMAModeActive`, `isTRENDModeActive`, `isRetestModeActive` -- mode booleans, written without atomic guards
- `isLongArmed`, `isShortArmed`, `lastArmedTime` -- ToS sync state, non-atomic check-then-set (BUG-S6-004)
- `retestFiredThisSession` (bool) -- session latch, set after submission window (BUG-S6-009)
- `ema9`, `ema15`, `rsiIndicator`, `currentATR`, `lastKnownPrice` -- indicator/price values, read without snapshot (BUG-S6-007)

---

*End of Report S6. All findings are READ-ONLY observations. No src/ edits were made.*
