# Mission Brief: Audit Remediation Loop (Build 957) Fixes

## Objectives

Automated remediation loop failed with two C# compilation errors. Additionally, a forensic audit of the RMA module found a missing state synchronization mechanism. You (Sonnet) must implement these fixes to get back to a completely clean 0-delta compilation and address the ledger drift.

## Target Files

1. `src/V12_002.Entries.Trend.cs`
2. `src/V12_002.Orders.Callbacks.cs`
3. `src/V12_002.Entries.RMA.cs`

## Task 1: Compilation Error - Trend.cs

- **Error:** `'Order' does not contain a definition for 'IsTerminal'`
- **Location:** `src/V12_002.Entries.Trend.cs` around Line 260
- **Context:** In `ExecuteTRENDEntry`, the abort logic says `if (entryOrder1 != null && !entryOrder1.IsTerminal)`.
- **Fix:** `Order` does not have an `IsTerminal` property. You must use the existing strategy method `IsOrderTerminal()`.
- **Change to:** `if (entryOrder1 != null && !IsOrderTerminal(entryOrder1.OrderState))`

## Task 2: Compilation Error - Callbacks.cs

- **Error:** `'HashSet<string>' does not contain a definition for 'TryRemove'`
- **Location:** `src/V12_002.Orders.Callbacks.cs` around Line 675
- **Context:** In `HandleMatchedFollowerOrder`, there is `_dispatchSyncPendingExpKeys.TryRemove(cancelAcctKey, out removedSyncKey);`
- **Fix:** `_dispatchSyncPendingExpKeys` is a `HashSet<string>`, not a `ConcurrentDictionary`. It does not have `TryRemove(key, out val)`.
- **Change to:** `_dispatchSyncPendingExpKeys.Remove(cancelAcctKey);` (You don't need the `removedSyncKey` logic).

## Task 3: Audit Finding - Missed Ledger Sync in RMA.cs

- **Vulnerability:** Ghost State / Ledger Sync Failure (Critical)
- **Location:** `src/V12_002.Entries.RMA.cs` -> `ExecuteTrendSplitEntry()`
- **Context:** In the standard TREND module (`ExecuteTRENDEntry`), the `AddExpectedPositionDeltaLocked` is strictly enforced to adjust the master ledger _before_ order submission. However, in `ExecuteTrendSplitEntry()` in the RMA module, this lock/ledger tracking is completely missing.
- **Fix Requirement:**
  1. For `entryOrder1`: Add `AddExpectedPositionDeltaLocked(ExpKey(Account.Name), masterDeltaE1);` before submission. If `entryOrder1 == null`, roll it back.
  2. For `entryOrder2`: Add `AddExpectedPositionDeltaLocked(ExpKey(Account.Name), masterDeltaE2);` before submission. If `entryOrder2 == null`, roll it back.
  3. Ensure you calculate `masterDeltaE1` and `masterDeltaE2` using `qty9` and `qty15` with the proper sign (+ for Long, - for Short).

## Protocol Compliance

- Ensure all dictionary mutations inside these changes reside inside `lock(stateLock)` if required (note that `AddExpectedPositionDeltaLocked` already acquires the lock).
- Maintain completely clean F5 compilation. DO NOT proceed or finish if there are remaining compilation errors.
