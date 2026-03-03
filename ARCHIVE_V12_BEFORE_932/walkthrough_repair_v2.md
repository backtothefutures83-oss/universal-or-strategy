# Phase 8.2 Part 2 - Repair Hook Hardening & Sizing Parity

## Scope
- Sync `expectedPositions` during Live Sync quantity changes.
- Ensure initial dispatch sizing parity for RMA flow.
- Harden Reaper `hasWorkingEntry` blocking logic against terminal/zombie orders and improve diagnostics.

## Changes Applied

### 1) `UI.Sizing.cs` - Live Sync expected position parity
File: `src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs`

- Verified `SyncPendingOrders()` computes quantity change and stages account resolution under lock.
- Kept the post-`ChangeOrder` expected-position sync behavior (only after successful broker call).
- Normalized quantity delta naming to match directive intent:
  - `qtyDelta = newQty - entryOrder.Quantity`
  - Expected-position delta remains direction-aware for signed exposure accounting:
    - long: `+qtyDelta`
    - short: `-qtyDelta`

Relevant lines (post-change):
- `src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs:364`
- `src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs:369`
- `src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs:382`
- `src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs:385`

### 2) `SIMA.cs` - Initial dispatch quantity parity
File: `src/UniversalORStrategyV12_002_Dev.SIMA.cs`

- Verified `ExecuteRMAEntryV2()` already derives master quantity from ATR sizing (`qty = CalculatePositionSize(stopDist)`).
- Verified the same `qty` is used for:
  - master entry submission
  - follower `acct.CreateOrder(...)`
  - `PositionInfo.TotalContracts` / `RemainingContracts`
  - target distribution (`GetTargetDistribution(qty, ...)`)
- No additional code change required in this file for this mission item.

Relevant lines:
- `src/UniversalORStrategyV12_002_Dev.SIMA.cs:841`
- `src/UniversalORStrategyV12_002_Dev.SIMA.cs:853`
- `src/UniversalORStrategyV12_002_Dev.SIMA.cs:865`
- `src/UniversalORStrategyV12_002_Dev.SIMA.cs:956`
- `src/UniversalORStrategyV12_002_Dev.SIMA.cs:981`

### 3) `REAPER.cs` - Harden `hasWorkingEntry` and diagnostics
File: `src/UniversalORStrategyV12_002_Dev.REAPER.cs`

- Verified blocking check ignores terminal/zombie states (`Cancelled`, `Rejected`, `Filled`).
- Verified only `Working`, `Submitted`, `Accepted` can block repairs.
- Hardened blocking diagnostic identity capture:
  - `blockingOrderName = string.IsNullOrEmpty(ord.Name) ? kvp.Key : ord.Name;`
- Confirmed required diagnostic print exists:
  - `"[REAPER] Repair BLOCKED by {orderName} in state {orderState}"`

Relevant lines (post-change):
- `src/UniversalORStrategyV12_002_Dev.REAPER.cs:174`
- `src/UniversalORStrategyV12_002_Dev.REAPER.cs:181`
- `src/UniversalORStrategyV12_002_Dev.REAPER.cs:186`
- `src/UniversalORStrategyV12_002_Dev.REAPER.cs:201`

## Build / Safety Validation

- No `.sln`/`.csproj` exists in this repository, so CLI `dotnet build` is not available as an authoritative check here.
- This environment does not provide NinjaTrader GUI compile (`F5`), so full compile validation must be run in NinjaTrader.
- Changes were surgical and limited to:
  - `src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs`
  - `src/UniversalORStrategyV12_002_Dev.REAPER.cs`
  - `walkthrough_repair_v2.md`

## Recommended NinjaTrader Verification

1. Sync strategy source into NinjaTrader strategies folder (project standard deploy path).
2. Open NinjaTrader editor and run compile (`F5`).
3. Validate:
   - Live Sync quantity updates also shift expected positions for followers.
   - RMA initial follower entries submit at master-sized quantity (no initial `1` dispatch).
   - Reaper logs blocking order/state only for active working states; cancelled/rejected zombies do not block repair queueing.
