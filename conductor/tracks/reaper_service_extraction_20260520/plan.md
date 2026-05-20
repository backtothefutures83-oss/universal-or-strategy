# Implementation Plan - Extract REAPER Risk Service

## Objective
Extract the REAPER risk scan logic into a standalone, pure C# service (`ReaperRiskService`) to improve testability and maintain architectural separation.

## Key Files & Context
- **New Files**:
  - `src/Services/IReaperRiskService.cs`
  - `src/Services/ReaperRiskService.cs`
- **Modify**:
  - `src/V12_002.REAPER.cs` (Partial class)
  - `src/V12_002.REAPER.Audit.cs` (Partial class)
  - `src/V12_002.cs` (Service instantiation and injection)
- **Key Constraints**:
  - Pure C# (no NinjaTrader runtime dependency in the service).
  - Constructor injection of tracking collections.
  - Preserve "Phantom-Fix" ordering invariant.
  - Diff limit < 500 lines.

## Implementation Steps

### 1. Define Abstractions & DTOs
- Create `IReaperLogger` for logging from the service.
- Define `IReaperAccount`, `IReaperPosition`, `IReaperOrder` interfaces to decouple from NinjaTrader.
- Define `ReaperAction` DTOs (Repair, Flatten, NakedStop).
- Move `PositionInfo` and `FollowerBracketFSM` data structures to a more accessible (internal/public) state or create DTO versions for the service.

### 2. Create `IReaperRiskService`
- Define the interface with a method like `Audit(ReaperAuditRequest request)`.

### 3. Implement `ReaperRiskService`
- Port logic from `V12_002.REAPER.Audit.cs`.
- Ensure constructor injection of:
  - `ConcurrentDictionary<string, PositionInfo> activePositions`
  - `ConcurrentDictionary<string, Order> entryOrders` (Will use `IReaperOrder`)
  - `ConcurrentDictionary<string, int> expectedPositions`
  - `ConcurrentDictionary<string, FollowerBracketFSM> followerBrackets` (Will use `IReaperFsm`)
  - Other state tracking dicts.
- Implement the audit logic using the abstractions.

### 4. Integrate with `V12_002`
- Instantiate `ReaperRiskService` in `OnStateChange` (DataLoaded).
- Update `AuditApexPositions` to call the service.
- Update the strategy to execute the `ReaperAction`s returned by the service.

### 5. Verification
- `grep` for "Phantom-Fix" comment.
- Run `dotnet build`.
- (Optional) Create a unit test for `ReaperRiskService` proving it works without NT.

## Migration & Rollback
- The old logic in `V12_002.REAPER.Audit.cs` will be replaced.
- Rollback: Revert files to previous commit.
