# Implementation Plan: GTC Sweep "Friendly Fire" Logic Repair

**Date**: 2026-03-18 | **Architect**: Claude (P3) | **Engineer**: Codex (P4)
**Trigger**: Hard restart test revealed "Friendly Fire" cancellation of bracket orders during strategy termination.
**Scope**: 1 file, 1 surgical change
**Build Tag**: 1102Z

---

## Context
During strategy termination, the strategy calls `CancelAllV12GtcOrders(false)` to clean up orphaned entries. However, the broad prefix list (RMA, Trend, etc.) catches bracket orders (Stop/Target) if they inherit those prefixes, leading to naked positions during a restart.

## Change Inventory

### 1. src/V12_002.SIMA.Lifecycle.cs
- [x] **Surgical Change**: In `SweepBrokerOrders`, add an explicit guard when `force=false`. [1c0f0bd]
- [x] **Logic**: If `!force`, explicitly exclude any order with prefixes: `Stop_`, `S_`, `T1_`, `T2_`, `T3_`, `T4_`, `T5_`, `Target_`. [1c0f0bd]
- [x] **Diagnostic**: Add `[FIX-FF]` logging to confirm when an order is protected by this exclusion. [1c0f0bd]

## P4 Self-Audit Checklist
- [x] No `lock(stateLock)` introduced.
- [x] Bracket orders are explicitly protected when `force=false`.
- [x] `check_ascii.py` passes on `src/V12_002.SIMA.Lifecycle.cs`.
- [x] Conductor - User Manual Verification 'Phase 3: Verification' (Protocol in workflow.md)
