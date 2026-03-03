# Consolidated Audit Log: Universal OR Strategy V12.002_Dev
**Start Time:** 2026-02-15 14:59 EST
**Phase:** Round 1 - Redundant Discovery

---

## 🛡️ ROUND_1: INDEPENDENT DISCOVERY
*Instructions: All agents scan the codebase without seeing each other's work.*

### ## ROUND_1: GEMINI
*   **Critical**: FFMA Infinity Bug (division by zero).
*   **Medium**: MOMO Slippage (StopLimit hallucination).
*   **Medium**: Mode Mutual Exclusion race conditions.

### ## ROUND_1: CLAUDE
*(Claude Code CLI Findings)*

### ## ROUND_1: CODEX_5.3
*(Codex 5.3 Model Findings)*

### ## ROUND_1: CURSOR
*(Cursor Task/Environment Findings)*

#### 1. Division-by-Zero Risk in Sizing Math

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.Entries.cs:535` | `diff / currentPrice` — no guard when `currentPrice == 0` (e.g. before first tick or bad data) | **High** |
| `UniversalORStrategyV12_002_Dev.SIMA.cs:396-397` | `Math.Round(stopPrice / tickSize)` — `tickSize` may be 0 for custom instruments or misconfiguration | **Medium** |

**Hardening Fixes:**
- **Entries.cs:535** — Guard before division: `if (currentPrice <= 0) currentPrice = lastKnownPrice; else if (Math.Abs(diff / currentPrice) < 0.05) currentPrice = lastKnownPrice;`
- **SIMA.cs:396-397** — Guard: `if (tickSize > 0) { stopPrice = Math.Round(stopPrice / tickSize) * tickSize; targetPrice = Math.Round(targetPrice / tickSize) * tickSize; } else { stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice); targetPrice = Instrument.MasterInstrument.RoundToTickSize(targetPrice); }`

#### 2. MOMO Stop-Limit Slippage Risks

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.Entries.cs:862-877` | Master submits **StopLimit** at `entryPrice`, fleet gets **Market** (default `OrderType` in `ExecuteSmartDispatchEntry`) — master waits for breakout while fleet fills immediately at current price | **Critical** |
| `UniversalORStrategyV12_002_Dev.Entries.cs:864-865` | StopLimit uses same limit and stop price (`entryPrice, entryPrice`) — no slippage buffer; fast markets may gap through without fill | **Medium** |
| `UniversalORStrategyV12_002_Dev.UI.cs:1299` | `EXEC_MOMO` passes `lastKnownPrice` as entry — may be stale; no explicit click-price validation | **Medium** |

**Hardening Fixes:**
- **Entries.cs:877** — Pass `OrderType.StopLimit` to fleet: `ExecuteSmartDispatchEntry("MOMO", ..., contracts, entryPrice, OrderType.StopLimit);`
- **SIMA.cs** — Add optional `MOMOSlippageTicks` parameter; use `entryPrice ± (slippageTicks * tickSize)` for limit when `OrderType.StopLimit`.
- **UI.cs:1299** — Validate `lastKnownPrice` age; reject EXEC_MOMO if stale (e.g. >5s since last OnBarUpdate).

#### 3. Thread-Safety in SIMA Broadcasting

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.SIMA.cs` | `GetSortedAccountFleet()` and fleet iteration run on strategy thread; `Account.All` and `expectedPositions` are read by Reaper on background thread — `ConcurrentDictionary` is used; **low risk** | Low |
| `SignalBroadcaster.cs` | Static events invoke handlers on caller thread; if IPC handler ever uses `HandleExternalSignal`, it could touch strategy state from non-strategy thread (currently `HandleExternalSignal` is **commented out**) | **Medium** |
| `UniversalORStrategyV12_002_Dev.REAPER.cs` | Reaper uses `TriggerCustomEvent` to marshal flatten — correct; `expectedPositions.TryGetValue` vs `AddOrUpdate` — `ConcurrentDictionary` is safe | OK |

**Hardening Fixes:**
- Ensure all IPC command handlers that modify strategy state (e.g. `TOGGLE_ACCOUNT`, `SET_SIMA`, mode toggles) run on the strategy thread via `TriggerCustomEvent` or equivalent.
- If re-enabling `HandleExternalSignal`, marshal its logic onto the strategy thread before updating `activeFleetAccounts`, mode flags, etc.
- Add `lock (stateLock)` or equivalent around `activeFleetAccounts` updates if IPC can modify them from a different thread.

#### 4. State-Persistence Bugs

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.cs:415-455` | `expectedPositions`, `activePositions`, etc. are recreated in `State.Configure`; `EnumerateApexAccounts` (Realtime) resets `expectedPositions[acct]=0` — correct for cold start | OK |
| `V12StandardPanel_V12_001_Dev.cs` | Panel config persistence (`fullConfig.ActiveMode`, `LastUsedCountPerMode`) — `lastSyncedCountPerMode` seeded from persisted data; possible stale/overwrite if panel reloads mid-session | **Low** |
| `isFlattenRunning` | Set in `FlattenAllApexAccounts` try, cleared in finally — robust | OK |

**Hardening Fixes:**
- Add timestamp or version to persisted panel config; reject load if data is older than session start.
- On strategy reload, ensure `EnumerateApexAccounts` does not clobber in-flight `expectedPositions` if a dispatch is mid-flight (unlikely but document assumption).

#### 5. Logic Clashing Between Modes

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.Entries.cs:889-896` | `ActivateMOMOMode` deactivates RMA; mutual exclusion for RMA/MOMO | OK |
| `UniversalORStrategyV12_002_Dev.UI.cs:1003-1036` | `SET_MODE` uses `lock(stateLock)` and clears all modes before setting one | OK |
| `UniversalORStrategyV12_002_Dev.UI.cs:1253-1293` | `ToggleStrategyMode` uses `lock(stateLock)` | OK |
| `UniversalORStrategyV12_002_Dev.UI.cs` | `SET_MODE` sets mode absolutely; `MODE_RMA`/`MODE_MOMO` toggle — rapid IPC could: SET_MODE RMA → MODE_RMA toggle → RMA ends up false | **Low** |

**Hardening Fixes:**
- Prefer `SET_MODE|mode` as single source of truth; have panel always send `SET_MODE` on mode change rather than mixing toggle commands.
- Add a short cooldown (e.g. 100ms) after `SET_MODE` before accepting toggle commands to avoid ping-pong.

---

### ## ROUND_1: ANTIGRAVITY (Deep-Scan)

#### 1. CRITICAL: Source Code Integrity (Missing Properties)
- **File**: `UniversalORStrategyV12_002_Dev.cs` (Reference in comments)
- **Location**: Line 302 (`// V12.46: Enums, Properties, and TimeZoneConverter moved to Properties.cs`)
- **Issue**: The file `UniversalORStrategyV12_002_Dev.Properties.cs` (or similar) is **MISSING** from the directory. The main strategy file contains no `[NinjaScriptProperty]` definitions for critical variables like `MaxRiskAmount` or `Target1FixedPoints`.
- **Risk**: The provided source code **CANNOT BE COMPILED** in its current state. NinjaTrader will fail to load the strategy or will load a cached DLL, masking source-code disconnects.
- **Fix**: Locate the missing file immediately. If lost, `Properties` region must be reconstructed in `UniversalORStrategyV12_002_Dev.cs`.

#### 2. HIGH: MOMO Slippage (Stop-Limit Hallucination)
- **File**: `UniversalORStrategyV12_002_Dev.Entries.cs`
- **Location**: `ExecuteMOMOEntry` (Line 863)
- **Code**: `SubmitOrderUnmanaged(..., OrderType.StopLimit, contracts, entryPrice, entryPrice, ...)`
- **Issue**: The order uses `Limit Price == Stop Price`. In a fast breakout (MOMO's specific use case), the market often gaps through the Stop price. Since the Limit price is identical to the Stop price, the order triggers but remains unfilled as the market is already "worse" than the limit.
- **Risk**: **Trade Hallucination**. The user sees the signal, hears the trigger, but gets no fill, missing the move entirely.
- **Fix**: 
    1.  **Preferred**: Change to `OrderType.StopMarket` to guarantee entry (accepting slippage).
    2.  **Alternative**: Use `StopLimit` with a 2-4 tick buffer (Limit = Stop +/- 4 ticks).

#### 3. MEDIUM: SIMA Thread-Safety (IPC Race Condition)
- **File**: `V12_002.UI.IPC.cs`
- **Issue**: The `HandleClient` background thread reads strategy state variables (`isRMAModeActive`, `Target1FixedPoints`) directly to build responses.
- **Risk**: Potential race condition or memory visibility issues on non-atomic types during rapid UI updates.
- **Fix**: Snapshot state onto the Strategy thread via `TriggerCustomEvent` before building IPC strings.

#### 4. MEDIUM: Sizing Math (NaN & Zero-Point Persistence)
- **File**: `V12_002.UI.Sizing.cs`
- **Location**: `CalculatePositionSize` (Line 85)
- **Issue**: The method checks `stopDistanceRaw <= 0` but lacks guards for `pointValue <= 0` or `double.IsNaN(stopDistanceRaw)`.
- **Risk**: Division by zero or NaN propagation into `SubmitOrder`, which can crash the strategy or trigger broker rejections.
- **Fix**: Add `if (pointValue <= 0 || double.IsNaN(stopDistanceRaw)) return minContracts;`.

#### 5. HIGH: SIMA Dispatch Desync on Submit Failure
- **File**: `V12_002.SIMA.cs`
- **Location**: `ExecuteSmartDispatchEntry` (Line 495)
- **Issue**: The code calls `acct.Submit()` outside the `stateLock`. If `Submit` throws an exception *after* `MarkDispatchSyncPending` and `AddExpectedPositionDeltaLocked` but *before* `ClearDispatchSyncPending`, the `syncPending` flag logic in the catch block (Line 540) handles it, but there is a slim window where REAPER might observe the pending state without a backup.
- **Risk**: Transient Desync alerts during broker connection drops.
- **Fix**: Move `MarkDispatchSyncPending` closer to the `Submit` call and ensure the `catch` block perfectly reverses all three states: `expectedPositions`, `_dispatchSyncPendingExpKeys`, and tracking dicts.
# Consolidated Audit Log: Universal OR Strategy V12.002_Dev
**Start Time:** 2026-02-15 14:59 EST
**Phase:** Round 1 - Redundant Discovery

---

## 🛡️ ROUND_1: INDEPENDENT DISCOVERY
*Instructions: All agents scan the codebase without seeing each other's work.*

### ## ROUND_1: GEMINI
*   **Critical**: FFMA Infinity Bug (division by zero).
*   **Medium**: MOMO Slippage (StopLimit hallucination).
*   **Medium**: Mode Mutual Exclusion race conditions.

### ## ROUND_1: CLAUDE (OPUS 4.6)
**Status: COMPLETE (2026-02-15)**
*   **CRITICAL (C-01)**: OnOrderUpdate/OnExecutionUpdate Race (Double-decrement risk).
*   **CRITICAL (C-03)**: Zombie Stop Risk (Stop placed on flat position reversing it).
*   **CRITICAL (C-04)**: Missing TickSize Rounding (Leads to broker order rejection).
*   **CRITICAL (C-10)**: Unsafe `Account.All` iteration (Throws InvalidOperationException).
*   **HIGH (H-01)**: RemainingContracts Underflow (Position tracking corruption).
*   **HIGH (H-02)**: .ToString().Contains("Pending") Logic Error (Stale orders slip through).

### ## ROUND_1: CODEX_5.3
*(Codex 5.3 Model Findings)*

### ## ROUND_1: CURSOR
*(Cursor Task/Environment Findings)*

#### 1. Division-by-Zero Risk in Sizing Math

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.Entries.cs:535` | `diff / currentPrice` — no guard when `currentPrice == 0` (e.g. before first tick or bad data) | **High** |
| `UniversalORStrategyV12_002_Dev.SIMA.cs:396-397` | `Math.Round(stopPrice / tickSize)` — `tickSize` may be 0 for custom instruments or misconfiguration | **Medium** |

**Hardening Fixes:**
- **Entries.cs:535** — Guard before division: `if (currentPrice <= 0) currentPrice = lastKnownPrice; else if (Math.Abs(diff / currentPrice) < 0.05) currentPrice = lastKnownPrice;`
- **SIMA.cs:396-397** — Guard: `if (tickSize > 0) { stopPrice = Math.Round(stopPrice / tickSize) * tickSize; targetPrice = Math.Round(targetPrice / tickSize) * tickSize; } else { stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice); targetPrice = Instrument.MasterInstrument.RoundToTickSize(targetPrice); }`

#### 2. MOMO Stop-Limit Slippage Risks

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.Entries.cs:862-877` | Master submits **StopLimit** at `entryPrice`, fleet gets **Market** (default `OrderType` in `ExecuteSmartDispatchEntry`) — master waits for breakout while fleet fills immediately at current price | **Critical** |
| `UniversalORStrategyV12_002_Dev.Entries.cs:864-865` | StopLimit uses same limit and stop price (`entryPrice, entryPrice`) — no slippage buffer; fast markets may gap through without fill | **Medium** |
| `UniversalORStrategyV12_002_Dev.UI.cs:1299` | `EXEC_MOMO` passes `lastKnownPrice` as entry — may be stale; no explicit click-price validation | **Medium** |

**Hardening Fixes:**
- **Entries.cs:877** — Pass `OrderType.StopLimit` to fleet: `ExecuteSmartDispatchEntry("MOMO", ..., contracts, entryPrice, OrderType.StopLimit);`
- **SIMA.cs** — Add optional `MOMOSlippageTicks` parameter; use `entryPrice ± (slippageTicks * tickSize)` for limit when `OrderType.StopLimit`.
- **UI.cs:1299** — Validate `lastKnownPrice` age; reject EXEC_MOMO if stale (e.g. >5s since last OnBarUpdate).

#### 3. Thread-Safety in SIMA Broadcasting

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.SIMA.cs` | `GetSortedAccountFleet()` and fleet iteration run on strategy thread; `Account.All` and `expectedPositions` are read by Reaper on background thread — `ConcurrentDictionary` is used; **low risk** | Low |
| `SignalBroadcaster.cs` | Static events invoke handlers on caller thread; if IPC handler ever uses `HandleExternalSignal`, it could touch strategy state from non-strategy thread (currently `HandleExternalSignal` is **commented out**) | **Medium** |
| `UniversalORStrategyV12_002_Dev.REAPER.cs` | Reaper uses `TriggerCustomEvent` to marshal flatten — correct; `expectedPositions.TryGetValue` vs `AddOrUpdate` — `ConcurrentDictionary` is safe | OK |

**Hardening Fixes:**
- Ensure all IPC command handlers that modify strategy state (e.g. `TOGGLE_ACCOUNT`, `SET_SIMA`, mode toggles) run on the strategy thread via `TriggerCustomEvent` or equivalent.
- If re-enabling `HandleExternalSignal`, marshal its logic onto the strategy thread before updating `activeFleetAccounts`, mode flags, etc.
- Add `lock (stateLock)` or equivalent around `activeFleetAccounts` updates if IPC can modify them from a different thread.

#### 4. State-Persistence Bugs

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.cs:415-455` | `expectedPositions`, `activePositions`, etc. are recreated in `State.Configure`; `EnumerateApexAccounts` (Realtime) resets `expectedPositions[acct]=0` — correct for cold start | OK |
| `V12StandardPanel_V12_001_Dev.cs` | Panel config persistence (`fullConfig.ActiveMode`, `LastUsedCountPerMode`) — `lastSyncedCountPerMode` seeded from persisted data; possible stale/overwrite if panel reloads mid-session | **Low** |
| `isFlattenRunning` | Set in `FlattenAllApexAccounts` try, cleared in finally — robust | OK |

**Hardening Fixes:**
- Add timestamp or version to persisted panel config; reject load if data is older than session start.
- On strategy reload, ensure `EnumerateApexAccounts` does not clobber in-flight `expectedPositions` if a dispatch is mid-flight (unlikely but document assumption).

#### 5. Logic Clashing Between Modes

| Location | Risk | Severity |
|----------|------|----------|
| `UniversalORStrategyV12_002_Dev.Entries.cs:889-896` | `ActivateMOMOMode` deactivates RMA; mutual exclusion for RMA/MOMO | OK |
| `UniversalORStrategyV12_002_Dev.UI.cs:1003-1036` | `SET_MODE` uses `lock(stateLock)` and clears all modes before setting one | OK |
| `UniversalORStrategyV12_002_Dev.UI.cs:1253-1293` | `ToggleStrategyMode` uses `lock(stateLock)` | OK |
| `UniversalORStrategyV12_002_Dev.UI.cs` | `SET_MODE` sets mode absolutely; `MODE_RMA`/`MODE_MOMO` toggle — rapid IPC could: SET_MODE RMA → MODE_RMA toggle → RMA ends up false | **Low** |

**Hardening Fixes:**
- Prefer `SET_MODE|mode` as single source of truth; have panel always send `SET_MODE` on mode change rather than mixing toggle commands.
- Add a short cooldown (e.g. 100ms) after `SET_MODE` before accepting toggle commands to avoid ping-pong.

---

### ## ROUND_1: ANTIGRAVITY (Deep-Scan)

#### 1. CRITICAL: Source Code Integrity (Missing Properties)
- **File**: `UniversalORStrategyV12_002_Dev.cs` (Reference in comments)
- **Location**: Line 302 (`// V12.46: Enums, Properties, and TimeZoneConverter moved to Properties.cs`)
- **Issue**: The file `UniversalORStrategyV12_002_Dev.Properties.cs` (or similar) is **MISSING** from the directory. The main strategy file contains no `[NinjaScriptProperty]` definitions for critical variables like `MaxRiskAmount` or `Target1FixedPoints`.
- **Risk**: The provided source code **CANNOT BE COMPILED** in its current state. NinjaTrader will fail to load the strategy or will load a cached DLL, masking source-code disconnects.
- **Fix**: Locate the missing file immediately. If lost, `Properties` region must be reconstructed in `UniversalORStrategyV12_002_Dev.cs`.

#### 2. HIGH: MOMO Slippage (Stop-Limit Hallucination)
- **File**: `UniversalORStrategyV12_002_Dev.Entries.cs`
- **Location**: `ExecuteMOMOEntry` (Line 863)
- **Code**: `SubmitOrderUnmanaged(..., OrderType.StopLimit, contracts, entryPrice, entryPrice, ...)`
- **Issue**: The order uses `Limit Price == Stop Price`. In a fast breakout (MOMO's specific use case), the market often gaps through the Stop price. Since the Limit price is identical to the Stop price, the order triggers but remains unfilled as the market is already "worse" than the limit.
- **Risk**: **Trade Hallucination**. The user sees the signal, hears the trigger, but gets no fill, missing the move entirely.
- **Fix**: 
    1.  **Preferred**: Change to `OrderType.StopMarket` to guarantee entry (accepting slippage).
    2.  **Alternative**: Use `StopLimit` with a 2-4 tick buffer (Limit = Stop +/- 4 ticks).

#### 3. MEDIUM: SIMA Thread-Safety (IPC Race Condition)
- **File**: `UniversalORStrategyV12_002_Dev.UI.cs`
- **Location**: `ListenForRemote` -> `HandleClient` -> `GET_LAYOUT` (Line 328)
- **Issue**: The `ipcThread` (Background Thread) reads strategy state variables (`minContracts`, `Target1FixedPoints`, `isTrendRmaMode`) directly to construct the configuration string.
- **Risk**: While unlikely to crash, reading state variables from a background thread while the main logic thread potentially modifies them (e.g., via `ToggleStrategyMode`) is a Race Condition. On 32-bit runtimes, 64-bit double reads are not atomic.
- **Fix**: Marshal the `GET_LAYOUT` response generation to the generic Dispatcher or Strategy thread using `TriggerCustomEvent` or `Dispatcher.Invoke`.

#### 4. MEDIUM: Logic Clashing (Mode Toggles)
- **File**: `UniversalORStrategyV12_002_Dev.UI.cs`
- **Location**: `ToggleStrategyMode` (Line 1253)
- **Issue**: IPC commands toggle modes independently (`isRMAModeActive = !isRMAModeActive`). The logic does not enforce strict mutual exclusivity at the toggle level (except for `ActivateMOMOMode` in `Entries.cs`).
- **Risk**: A user could accidentally activate `RMA` via IPC while `MOMO` is active, leading to "Ghost Mode" signals where clicks trigger unintended logic.
- **Fix**: Implement a centralized `SetMode(string mode)` helper that sets the target mode to `true` and **explicitly sets all others to false**.

#### 5. LOW: Sizing Math (NaN weakness)
- **File**: `UniversalORStrategyV12_002_Dev.UI.cs`
- **Location**: `CalculatePositionSize` (Line 1790)
- **Issue**: The method guards against `stopDollars <= 0` but does not explicitly handle `NaN` or `pointValue == 0` (though `stopDollars` check covers 0). `Math.Floor(risk / NaN)` behavior in C# needs explicit handling to avoid passing invalid quantities to `SubmitOrder`.
- **Risk**: Defaulting to `minContracts` on error might trigger a trade with unintended size during data feed glitches.

---

## ⚖️ ROUND_2: THE CONSENSUS DEBATE
*(Log synthesized by Gemini Antigravity for the next Director Instance)*

### ## CROSS-AGENT REBUTTALS & SYNTHESIS

**Consensus Items (Confirmed by Gemini, Codex 5.3, & Cursor):**
1.  **MOMO Order Execution**: `StopLimit` with 0 offset is a confirmed fail-point. **Action**: Transition to `StopMarket` or `Offset Limit`.
2.  **FFMA Sizing Math**: Division-by-zero risk confirmed (`High/Low` edge case). **Action**: Enforce `Math.Max(TickSize, stopDistance)`.
3.  **RETEST Mode Bug (Crucial Catch by Codex)**: ATR sync is using the wrong flag (`isTrendRmaMode` instead of `isRetestRmaMode`). **Action**: Correct the flag mapping in `ATRMultiplier` logic.
4.  **SIMA Thread Safety**: Background IPC thread reading state variables (`Target1FixedPoints`) during main thread updates. **Action**: Marshal `GET_LAYOUT` to UI thread.
5.  **Reaper Audit Lifecycle**: Desync risk if strategy restarts mid-trade. **Action**: Hydrate `expectedPositions` correctly on cold boot.

**Infrastructure Status:**
*   **Properties.cs**: [RESTORED & VERIFIED & POLISHED](file:///C:/WSGTA/universal-or-strategy/UniversalORStrategyV12_002_Dev.Properties.cs). Multi-agent audit confirms 100% property alignment. Codex applied deterministic UI ordering for all trailing properties.

---

## ✅ FINAL_HARDENING_CONSENSUS
**Approved Strategy for Sunday Open (V12.002_Dev):**

1.  **Entries.cs**: Fix MOMO StopLimit -> StopMarket.
2.  **Entries.cs**: Guard FFMA sizing math with `Math.Max(TickSize, ...)`.
3.  **UI.cs**: Correct the RETEST ATR Multiplier flag typo.
4.  **UI.cs**: Marshal `GET_LAYOUT` IPC response to the Strategy/UI thread.
5.  **SIMA.cs**: Apply tickSize guards for Path-B bracket rounding.

---

6.  **Contract Distribution (Sonnet Catch)**: Integer rounding/negative floor bug can double position size. **Action**: Re-cap total sum after bucket allocation.
7.  **Broker Thread Race (Sonnet Catch)**: `OnAccountExecutionUpdate` mutates state from the wrong thread context. **Action**: Marshal all broker callbacks to the strategy thread.
8.  **Timestamp Collision (Sonnet Catch)**: `HHmmss` keying is insufficient for same-second entries. **Action**: Upgrade to `HHmmss.fff`.

## ⚖️ ROUND_2: AGENT DEBATE
9.  **Dual-Callback Race (Opus Catch)**: `OnOrderUpdate` and `OnExecutionUpdate` both decrement contracts. **Action**: Implement a processed-execution-ID hashset to deduplicate.
10. **Zombie Stop Prevention (Opus Catch)**: Stop orders can fire on flat positions. **Action**: Add final `RemainingContracts > 0` guard inside the stop-creation lock.
11. **TickSize Rounding (Opus Catch)**: Missing rounding for ATR-calculated brackets. **Action**: Standardize all prices via `Instrument.MasterInstrument.RoundToTickSize()`.
*(This log is now ready for the next Project Director to lead the debate and implementation.)*


---

## ✅ FINAL_HARDENING_CONSENSUS
*Final approved plan for implementation.*
## ROUND_1: CLAUDE / CODEX_5.3 / CURSOR
### CODEX_5.3

#### (1) Division-by-zero and sizing math risks
1. High: `tickSize` divide-by-zero exposure in Path-B bracket rounding (`UniversalORStrategyV12_002_Dev.SIMA.cs:396`, `UniversalORStrategyV12_002_Dev.SIMA.cs:397`).
Fix: gate the math with `if (tickSize <= 0 || !double.IsFinite(tickSize)) { log + abort bracket submit; }` and use `RoundToTickSize()` instead of manual division.

2. Medium: potential zero-denominator in stale-price guard (`diff / currentPrice`) in RMA entry (`UniversalORStrategyV12_002_Dev.Entries.cs:535`).
Fix: replace with guarded form `if (currentPrice > tickSize && diff / currentPrice < 0.05)`.

3. Medium: potential zero-height divide in click-price conversion (`yInPanel / effectivePriceHeight`) (`UniversalORStrategyV12_002_Dev.UI.cs:106`, `UniversalORStrategyV12_002_Dev.UI.cs:114`).
Fix: `if (effectivePriceHeight <= 0) return;` before computing ratio.

4. Medium: sizing guard is incomplete versus its own spec (`clamp to [min,max]`) (`UniversalORStrategyV12_002_Dev.UI.cs:1787`, `UniversalORStrategyV12_002_Dev.UI.cs:1803`). There is no max-cap clamp and external commands can set invalid counts (`UniversalORStrategyV12_002_Dev.UI.cs:531`-`UniversalORStrategyV12_002_Dev.UI.cs:534`, `UniversalORStrategyV12_002_Dev.UI.cs:987`-`UniversalORStrategyV12_002_Dev.UI.cs:990`).
Fix: enforce `minContracts >= 1`, apply instrument max cap in `CalculatePositionSize`, and reject non-positive qty before order submission.

#### (2) MOMO stop-limit slippage and fill risks
1. High: master MOMO uses stop-limit with zero limit offset (`stopPrice == limitPrice`) (`UniversalORStrategyV12_002_Dev.Entries.cs:864`, `UniversalORStrategyV12_002_Dev.Entries.cs:865`). In fast moves this can trigger without fill and strand intent.
Fix: add `MomoStopLimitOffsetTicks` (default 2-4 ticks) and compute asymmetric limit offsets by side.

2. High: execution model mismatch between master and fleet for MOMO. Master submits StopLimit, but SIMA dispatch call omits order type and defaults to Market (`UniversalORStrategyV12_002_Dev.Entries.cs:877`, `UniversalORStrategyV12_002_Dev.SIMA.cs:93`).
Fix: pass explicit MOMO order model through dispatch (StopLimit+offset, or StopMarket if selected) so master/followers share identical entry semantics.

3. Medium: entry slippage re-anchor updates stop for non-RMA trades but does not re-anchor MOMO targets from actual fill (`UniversalORStrategyV12_002_Dev.Orders.cs:121`-`UniversalORStrategyV12_002_Dev.Orders.cs:125`, `UniversalORStrategyV12_002_Dev.Orders.cs:137`-`UniversalORStrategyV12_002_Dev.Orders.cs:153`).
Fix: for MOMO fills, recompute target prices from `averageFillPrice` before `SubmitBracketOrders`.

4. Medium: rejected/cancelled MOMO entries can leave stale position state (entry ref removed, position object retained) (`UniversalORStrategyV12_002_Dev.Orders.cs:275`-`UniversalORStrategyV12_002_Dev.Orders.cs:311`, `UniversalORStrategyV12_002_Dev.Orders.cs:954`-`UniversalORStrategyV12_002_Dev.Orders.cs:971`).
Fix: add explicit entry-terminal handler that calls `CleanupPosition(entryName)` when entry never filled.

#### (3) Thread-safety in SIMA broadcasting/sync
1. High: follower fill detection in `OnAccountExecutionUpdate` relies on object reference equality only (`UniversalORStrategyV12_002_Dev.UI.cs:1683`-`UniversalORStrategyV12_002_Dev.UI.cs:1686`), while account-order path already needed OrderId dual matching (`UniversalORStrategyV12_002_Dev.Orders.cs:523`-`UniversalORStrategyV12_002_Dev.Orders.cs:528`). Missed match can skip bracket submission.
Fix: use dual-match (`reference || OrderId`) in execution path too.

2. Medium: mutable `PositionInfo` objects are updated from multiple callback paths (strategy thread + account callbacks + reaper marshalling boundary) without per-position synchronization.
Fix: marshal account callbacks into a strategy-thread queue (`ConcurrentQueue` + `TriggerCustomEvent`) and mutate `PositionInfo` in one place.

3. Medium: `SignalBroadcaster` dispatches static events synchronously and without subscriber isolation (`SignalBroadcaster.cs:210`, `SignalBroadcaster.cs:223`, `SignalBroadcaster.cs:324`). A single subscriber exception can break fan-out.
Fix: invoke captured delegate list per-handler in try/catch, log handler failures, and optionally decouple with queued dispatch.

#### (4) State-persistence/lifecycle bugs
1. High: `expectedPositions` is reinitialized to zero on account enumeration (`UniversalORStrategyV12_002_Dev.SIMA.cs:284`) and reaper can classify live positions as critical desync (`UniversalORStrategyV12_002_Dev.REAPER.cs:137`-`UniversalORStrategyV12_002_Dev.REAPER.cs:151`).
Fix: on startup/enable-SIMA, hydrate expected state from broker positions before enabling reaper.

2. High: runtime `SET_SIMA` toggles only flip a flag (`UniversalORStrategyV12_002_Dev.UI.cs:916`-`UniversalORStrategyV12_002_Dev.UI.cs:929`); it does not symmetrically start/stop reaper and subscribe/unsubscribe account events.
Fix: implement a single `ApplySimaState(bool enabled)` that performs full lifecycle actions.

3. Medium: termination cleanup unsubscribes account handlers only when `EnableSIMA` is true at termination (`UniversalORStrategyV12_002_Dev.cs:545`-`UniversalORStrategyV12_002_Dev.cs:554`). If SIMA was enabled then toggled off, handlers may remain attached.
Fix: track subscribed accounts in a dedicated set and always unsubscribe from that set during termination.

4. Medium: no boot-time reconciliation of existing live orders/positions into strategy dictionaries after restart; internal state can restart empty while broker state is non-empty.
Fix: add `RebuildStateFromBroker()` in `State.Realtime` before order management loops.

#### (5) Logic clashes between modes
1. High: RETEST ATR sync uses Trend flag by mistake (`return isTrendRmaMode ? ... : RetestATRMultiplier`) (`UniversalORStrategyV12_002_Dev.UI.cs:1966`).
Fix: use `isRetestRmaMode` for RETEST branch.

2. High: mode command paths are inconsistent. `SET_MODE` is exclusive (`UniversalORStrategyV12_002_Dev.UI.cs:1003`-`UniversalORStrategyV12_002_Dev.UI.cs:1033`) but `ToggleStrategyMode` toggles flags independently (`UniversalORStrategyV12_002_Dev.UI.cs:1258`-`UniversalORStrategyV12_002_Dev.UI.cs:1293`). This can create multi-active mode states.
Fix: route all mode changes through one exclusive `SetActiveMode()` method.

3. Medium: click entry prioritizes MOMO when both RMA and MOMO are active (`UniversalORStrategyV12_002_Dev.UI.cs:82`-`UniversalORStrategyV12_002_Dev.UI.cs:85`, `UniversalORStrategyV12_002_Dev.UI.cs:132`-`UniversalORStrategyV12_002_Dev.UI.cs:139`).
Fix: enforce mutual exclusion at flag-write time; add guard `if (rmaActive && momoActive) reject + log`.

4. Medium: docs/comments still state MOMO is stop-market while implementation is stop-limit (`UniversalORStrategyV12_002_Dev.Entries.cs:761`, `UniversalORStrategyV12_002_Dev.Entries.cs:864`).
Fix: align docs with code and expose an explicit mode option (`MOMOEntryType = StopMarket | StopLimit`).

#### Hardening implementation order (recommended)
1. Safety-critical first: MOMO offset/fallback + expected-state hydration + RETEST multiplier flag fix.
2. Concurrency next: execution dual-match + strategy-thread event queue for SIMA callbacks.
3. Lifecycle consistency: centralize SIMA enable/disable and deterministic subscribe/unsubscribe.
4. Sizing integrity: tick-size guards, zero-denominator guards, qty bounds validation.