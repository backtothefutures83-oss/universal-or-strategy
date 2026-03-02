# Copilot Code Review Instructions — Universal OR Strategy V12

You are reviewing a **high-integrity institutional futures trading strategy** for NinjaTrader 8 (C#).
Lives and capital are on the line. Apply Zero-Trust protocols to every review.

## Primary Review Focus

### 1. Zero-Trust IPC (CRITICAL)
- All TCP commands must arrive via loopback (127.0.0.1) only — reject any external binding
- Every command must pass the `AllowedIpcActions` allowlist before execution
- UTF-8 decoding must use a **stateful per-client decoder** — never `Encoding.GetString()` on a raw buffer (split-packet risk)
- Queue depth must be bounded (`IpcMaxQueueDepth`) to prevent memory exhaustion
- No command longer than `IpcMaxCommandLength` should be processed

### 2. SIMA Fleet Dispatch (CRITICAL)
- `expectedPositions` mutations MUST be serialized under `stateLock`
- `_dispatchSyncPendingExpKeys` barriers must be set before and cleared after every fleet submit
- Reserved quantities must be rolled back (`AddExpectedPositionDeltaLocked(key, -delta)`) on any Submit exception
- Fleet registration (activePositions, entryOrders, stopOrders) must happen BEFORE `expectedPositions` is incremented

### 3. REAPER Safety (CRITICAL)
- Fill-grace must be **per-account** (`_accountFillGraceTicks[expKey]`), never a single global timestamp
- Repair orders must be gated by `min(ATR bound, RepairTickFence)` distance from current price
- REAPER must never fire on the Master account (uses `SubmitOrderUnmanaged`, not follower path)
- `_repairInFlight` guard must be set before and cleared after every repair submit

### 4. Order Callbacks (HIGH)
- Callback fills must use **signed delta rollback**, never blanket zeroing of `expectedPositions`
- Ghost/zombie cleanup: only clear the specific `expKey` that was filled, not the entire account state
- Bracket orders must not be submitted until the master entry is confirmed filled

### 5. Threading (HIGH)
- `Account.Flatten()` and `acct.Submit()` must NEVER be called from a background thread — always via `TriggerCustomEvent`
- No UI Updates (Draw*, Chart*) from background threads
- All `ConcurrentDictionary` reads are safe from background threads; `HashSet` reads MUST use `lock(stateLock)`

## Naming Conventions (BMad Protocol)
- Entry signals: `Fleet_{AccountName}_{TradeType}_{index}`
- Stop signals: `Stop_{entryName}` (max 40 chars via `SymmetryTrim`)
- Target signals: `T{N}_{entryName}` (max 40 chars)
- Repair signals: must reuse the original `repairEntryName` key (NOT prefixed with "Repair_")

## Auto-Reject Conditions
Do NOT approve any PR that contains:
- `Account.Flatten()` called directly on a background/Reaper thread
- `expectedPositions[key] = 0` without `stateLock` (blanket zeroing)
- `IsReaperFillGraceActive()` without an account key argument (global grace bug)
- `Encoding.UTF8.GetString(buffer, ...)` in the IPC receive loop (stateless decoder)
- Any IPC command processed without passing through `AllowedIpcActions` check
