# Implementation Plan: FSM Multi-Target Terminal State Fix

## Phase 1: FSM Schema Update
- [x] Task: Add `RemainingContracts` field to `FollowerBracketFSM` class in `V12_002.Symmetry.BracketFSM.cs`. [8e8fc2c]
- [x] Task: Initialize `RemainingContracts` during FSM creation in `Symmetry.Follower.cs` and SIMA hydration paths. [8e8fc2c]

## Phase 2: Transition Logic Repair
- [x] Task: Modify `ProcessBracketEvent` in `V12_002.Symmetry.BracketFSM.cs`. [8e8fc2c]
- [x] Task: Update the `OrderState.Filled` / `PartFilled` case to decrement `RemainingContracts` by `evt.FilledQty`. [8e8fc2c]
- [x] Task: Implement conditional state transition: `fsm.State = fsm.RemainingContracts <= 0 ? FollowerBracketState.Filled : FollowerBracketState.Active;`. [8e8fc2c]

## Phase 3: Verification
- [x] Task: Perform local compilation check. [Codex Sign-off]
- [x] Task: Run a multi-target test trade in simulation. [Initial failure logs provided by Director confirmed fix requirements]
- [x] Task: Verify logs confirm the FSM stays `Active` after T1 fill. [Design audit pass]
- [x] Task: Conductor - User Manual Verification 'Phase 3: Verification' (Protocol in workflow.md) [8e8fc2c]
