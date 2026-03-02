# Build 935 Deep Logic & Security Audit

Date: 2026-03-01  
Branch: `build-935-deep-audit`  
Scope: `src/V12_001.cs`, `src/V12_002*.cs` (logic, callbacks, REAPER, IPC, ATR/RMA sizing paths)

## Executive Summary

This audit focused on asynchronous SIMA fleet consistency, REAPER ghost/memory behavior, IPC trust boundaries, and ATR/RMA integer sizing integrity.  
Critical and high-risk race/validation issues were remediated directly in strategy code. IPC parsing and network exposure were hardened under a zero-trust model.

## Findings And Remediations

### B935-001 (Critical): Follower dispatch race could trigger false REAPER repairs / premature flat-clears
- Area: SIMA dispatch lifecycle (`ExecuteSmartDispatchEntry`, `ExecuteRMAEntryV2`), callback flat-sync guards.
- Risk: During reserve/submit windows, follower expected state could be observed before full sync, allowing repair or reset logic to run against in-flight entries.
- Remediation:
  - Added dispatch-sync pending set (`_dispatchSyncPendingExpKeys`) and helpers:
    - `MarkDispatchSyncPending`, `ClearDispatchSyncPending`, `IsDispatchSyncPending`.
  - Marked pending before reserve+submit and cleared after submit completion.
  - Added pending checks in `OnPositionUpdate`, compliance flat-sync path, and REAPER repair gating.
  - Ensured expected timestamps are stamped on non-zero delta updates.
- Files:
  - `src/V12_002.cs`
  - `src/V12_002.SIMA.cs`
  - `src/V12_002.Orders.Callbacks.cs`
  - `src/V12_002.UI.Compliance.cs`
  - `src/V12_002.REAPER.cs`

### B935-002 (High): Expected-position rollback paths could over/under-correct on submit failure
- Area: SIMA market/bracket/RMA dispatch catches.
- Risk: Legacy rollback used clamped/account-wide logic in some paths or unconditional rollback in catch paths where reserve may not have happened.
- Remediation:
  - Converted rollback flow to signed `reservedDelta` pattern.
  - Rollback now only executes when reserve actually occurred.
  - Removed zero-floor rollback clamps that can corrupt signed short state.
- Files:
  - `src/V12_002.SIMA.cs`

### B935-003 (High): Ghost/ZOMBIE cleanup used account-wide zeroing instead of signed deltas
- Area: order callbacks on rejection/cancel/cascade.
- Risk: Zeroing expected state can erase unrelated in-flight positions and trigger desync loops.
- Remediation:
  - Replaced zeroing with signed delta rollback per position quantity/direction.
  - Cleared dispatch-sync pending flags as part of teardown.
  - Improved flat-update skip conditions to include dispatch pending.
- File:
  - `src/V12_002.Orders.Callbacks.cs`

### B935-004 (High): REAPER auto-repair distance safety was too permissive for non-market repairs
- Area: REAPER repair queue and emergency naked-stop logic.
- Risk: Repair entries could be submitted outside intended ATR/risk envelopes.
- Remediation:
  - Added hard bound for all repair order types:
    - `abs(currentPrice - repairEntryPrice) <= min(ATR-bound, tick-fence bound)`.
  - Deferred repair when dispatch sync is pending or in fill grace window.
  - ATR-capped emergency naked stop distance.
- File:
  - `src/V12_002.REAPER.cs`

### B935-005 (High): IPC parser/network robustness and trust boundary gaps
- Area: IPC server/client handling and command processing.
- Risk:
  - Listener exposed on all interfaces.
  - Malformed/oversized payloads could cause unbounded buffering.
  - Unrestricted command actions and unbounded queueing increased crash/abuse surface.
- Remediation:
  - Bound listener to loopback (`127.0.0.1`) instead of `IPAddress.Any`.
  - Added strict UTF-8 decoding with rejection/disconnect on malformed bytes.
  - Added max buffered chars, max command length, max queue depth, and bounded command drains.
  - Added IPC allowlist/prefix validation for actions.
  - Added robust per-client tracking/cleanup with `ConcurrentDictionary<int, TcpClient>`.
- Files:
  - `src/V12_002.cs`
  - `src/V12_002.UI.IPC.cs`

### B935-006 (Medium): IPC state leakage of fleet identity
- Area: `GET_FLEET` and `REQUEST_FLEET_STATE` responses.
- Risk: Real account names exposed unnecessarily to local IPC clients.
- Remediation:
  - Added `IpcExposeSensitiveFleetIdentity` property (default `false`).
  - Introduced aliasing (`F01`, `F02`, ...) for IPC responses when property is disabled.
  - Updated fleet response builders to use alias mapping.
- Files:
  - `src/V12_002.Properties.cs`
  - `src/V12_002.cs`
  - `src/V12_002.UI.IPC.cs`

### B935-007 (Medium): Symmetry follower bracket submission timing race
- Area: follower fill handling.
- Risk: Brackets could be submitted before master-anchor resolution, causing transient wrong-price submissions.
- Remediation:
  - Gated immediate bracket submission on pre-resolved anchor.
  - If anchor not ready, delay bracket submit until resolver path completes.
- File:
  - `src/V12_002.Symmetry.cs`

### B935-008 (Audit Result): ATR/RMA sizing integer consistency
- Area: ATR sizing and RMA entry quantity paths.
- Result:
  - `CalculatePositionSize` returns `int` and floors risk-based quantity after ceiling stop conversion.
  - Entry order quantity paths (RMA and related dispatch consumers) use integer contracts.
  - No fractional share/contract submission path found in audited ATR/RMA execution routes.
- Primary file:
  - `src/V12_002.UI.Sizing.cs`

## Files Modified In Build 935

- `src/V12_002.cs`
- `src/V12_002.Properties.cs`
- `src/V12_002.SIMA.cs`
- `src/V12_002.Orders.Callbacks.cs`
- `src/V12_002.REAPER.cs`
- `src/V12_002.Symmetry.cs`
- `src/V12_002.UI.Compliance.cs`
- `src/V12_002.UI.IPC.cs`

## Verification Notes

- Static code audit and diff validation completed across all modified modules.
- No local `.sln` / `.csproj` present in repository, so full compile/test execution was not runnable in this workspace.

## Residual Risks / Follow-Ups

1. Consider adding command authentication/nonce for IPC if hostile local processes are in threat model (current hardening is transport and parser level).
2. Add targeted integration tests around dispatch-sync pending lifecycle and REAPER defer/repair transitions.
3. Add explicit telemetry counters for IPC rejects (queue depth, invalid UTF-8, allowlist failures) for operational monitoring.
