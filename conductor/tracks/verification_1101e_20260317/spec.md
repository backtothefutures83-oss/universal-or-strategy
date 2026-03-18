# Specification: Build 1101E Stability Verification (Hard Restart)

## Objective
Verify the robustness of the V12.1101E order hydration logic. The system must successfully re-adopt orphaned broker orders and re-hydrate the `FollowerBracketFSM` after a sudden process termination (Hard Restart).

## Success Criteria
1. **Detection**: `[SIMA HYDRATE]` logs confirm the adoption of entry, stop, and target orders from the broker.
2. **State Recovery**: `[FSM-SHADOW]` logs confirm the FSM transitions to the `Active` state for existing positions.
3. **Integrity**: `REAPER` audit cycle runs without triggering phantom repairs or double-submissions.
4. **Resumption**: Trailing stops and target modifications function correctly on the re-hydrated fleet.

## Test Environment
- NinjaTrader 8
- Simulation Connection
- 1 Master + 3 Follower accounts (minimum)
- Target: MES or MNQ (Liquid instruments)
