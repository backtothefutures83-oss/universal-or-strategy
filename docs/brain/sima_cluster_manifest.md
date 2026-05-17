# Cluster Manifest: S1 SIMA Core (Upper Plane)

**Mission**: V12 Phase 7 Hardening | SIMA Cluster Baseline
**Status**: ACTIVE
**Architectural Goal**: Zero-allocation, lock-free fleet management with < 150 CYC total latency.

## 📦 Cluster Components (Source Files)

This cluster represents the **Sovereign Independent Multi-Account (SIMA)** core. All files must be analyzed together to maintain logical integrity.

| File Path | Purpose | CYC (Baseline) |
| :--- | :--- | :---: |
| `src/V12_002.SIMA.cs` | Main SIMA Entry & Signal Gateway | < 15 |
| `src/V12_002.SIMA.Lifecycle.cs` | Strategy start/stop & state hydration | < 20 |
| `src/V12_002.SIMA.Dispatch.cs` | Atomic order routing to fleet lanes | 20 |
| `src/V12_002.SIMA.Fleet.cs` | Multi-account iteration & health checks | 28 |
| `src/V12_002.SIMA.Execution.cs` | Logic for Entry/Exit command synthesis | < 15 |
| `src/V12_002.SIMA.Flatten.cs` | Global emergency shutdown & fleet flattening | < 20 |
| `src/V12_002.SIMA.Shadow.cs` | Leader-Follower state synchronization | 20 |
| `src/V12_002.Constants.cs` | Shared kernel constants | 0 |

## 🛡️ Critical Integration Points
- **Master Entry**: `V12_002.SIMA.cs` calls `ExecuteSmartDispatchEntry` in `V12_002.SIMA.Dispatch.cs`.
- **Fleet Sync**: `V12_002.SIMA.Fleet.cs` relies on `ShadowModeEnabled` state from `V12_002.SIMA.Shadow.cs`.
- **Direct Write**: Bracket submission in `Dispatch.cs` must write directly to `stopOrders` (Build 981 mandate).

## 🧪 Testing Protocol
1.  **Forensic Audit**: Check for `lock()` leakage and non-ASCII characters.
2.  **Logic Walkthrough**: Trace a signal from `V12_002.SIMA.cs` through `Dispatch.cs` to a follower account in `Fleet.cs`.
3.  **Benchmark (SIMA Mock)**: (Requires Mocked NinjaTrader harness to isolate allocations).

## 🐛 Arena Bug Tracker (Forensic Hardening Scope)

The following 15 bugs were identified by the Arena.ai audit and are the primary targets for this hardening mission.

| ID | Title | Severity | Location | Root Cause |
| :--- | :--- | :--- | :--- | :--- |
| **BUG-001** | Race Condition: Unsubscribe Leak | Critical | `UnsubscribeFromFleetAccounts()` | Double Handler Removal + Untracked Subscribe Leak |
| **BUG-002** | Re-Entrancy Flood | Critical | `PumpFleetDispatch()` | `TriggerCustomEvent` inside finally block |
| **BUG-003** | Use-After-Free Window | Critical | `ProcessFleetSlot()` | Sideband cleared AFTER pool slot release |
| **BUG-004** | XorShadow Zeroing | High | `VerifyPhotonSlotIntegrity()` | Zeroing invariant contradiction in shadow salt |
| **BUG-005** | Atomic FSM Creation | High | `EnsureFollowerBracket()` | Non-atomic check-then-set for follower FSMs |
| **BUG-006** | Null Ref (Hot Path) | High | `ShouldSkipFleetAccount()` | Accessing `pos.Instrument` before null check |
| **BUG-007** | O(N^2) Performance | High | `Unsubscribe...()` | Nested loops on fleet account lists |
| **BUG-008** | Sideband Poisoning | High | `ProcessValidPhotonSlot()` | Stale `OrderId` from previous slot reuse |
| **BUG-009** | FSM State Leak | Med | `ResetFollowerBracket()` | Incomplete state reset during cancel |
| **BUG-010** | Ghost Order Window | High | `SubmitFollowerReplacement()` | Using `Enqueue` instead of direct write (Build 981) |
| **BUG-011** | Double-Free (Shadow) | High | `ShadowEngineCheck()` | Double disposal of shadow salt handles |
| **BUG-012** | Tick Noise Bypass | Med | `ShadowPropagateStopMoves()` | Half-tick noise filter allows price drift |
| **BUG-013** | Semaphore Leak | High | `_simaToggleSem` | Missing `finally` block on toggle release |
| **BUG-014** | Instrument Lookup | Med | `GetFleetInstrument()` | Inefficient dictionary lookup in hot path |
| **BUG-015** | Async ID Failure | High | `ExecuteSmartDispatchEntry()` | Premature `OrderId` registration before submission |

---
*Generated: 2026-05-16 | Universal OR Strategy V12*
