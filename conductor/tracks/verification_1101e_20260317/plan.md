# Implementation Plan: Build 1101E Stability Verification (Hard Restart)

## Phase 1: Test Initialization
- [ ] Task: Configure fleet with 1 Master and at least 3 active Follower accounts.
- [ ] Task: Execute an entry (Master) and verify fleet fan-out.
- [ ] Task: Wait for fill and confirm `Active` state for all FSMs.

## Phase 2: Hard Crash Simulation
- [ ] Task: Force-kill NinjaTrader (Task Manager) while the trade is live.
- [ ] Task: Restart NinjaTrader 8.
- [ ] Task: Enable the V12 strategy on the same instrument.

## Phase 3: Forensic Hydration Audit
- [ ] Task: Verify `[SIMA HYDRATE]` logs confirm adoption of all orders.
- [ ] Task: Verify `[FSM-SHADOW]` logs show transition to `Active`.
- [ ] Task: Verify `REAPER` heartbeat is clean (no phantom repairs).
- [ ] Task: Conductor - User Manual Verification 'Phase 3: Forensic Hydration Audit' (Protocol in workflow.md)

## Phase 4: Final Certification
- [ ] Task: Director Sign-off on Build 1101E Stability.
