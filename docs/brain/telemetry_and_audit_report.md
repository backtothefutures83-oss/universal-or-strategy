# Telemetry and Audit Report: 28-Hunter Grid Sweep (S1-S7)

Date: 2026-05-17
Repository: C:\WSGTA\universal-or-strategy
Mode: P2 Forensics (read-only src)
Mission: Trace execution paths for lock-free race conditions across clusters S1-S7, enforce V12 DNA constraints, and consolidate findings.

## Mission Intake
- Read: `docs/brain/implementation_plan.md`
- Read: `docs/brain/nexus_a2a.json`
- Read: `docs/brain/task.md`
- Read: `graphify-out/GRAPH_REPORT.md`

## Sweep Topology (Current Repo State)
- S1 (SIMA Core): 6 files
- S2 (Execution Engine): 14 files
- S3 (UI and Photon IO): 16 files
- S4 (REAPER Defense): 4 files
- S5 (Kernel State): 5 files
- S6 (Signals and Entries): 6 files
- S7 (Kernel Infrastructure): 10 files
- Total scoped files: 61

Note: The workflow document references 67 files. Current glob resolution in `src/` is 61 files.

## V12 DNA Gate (Local Commands)
- `rg -n "^\s*lock\s*\(" src` -> `NO_LOCK_STATEMENTS`
- `rg -n "\bunsafe\b" src` -> `NO_UNSAFE_KEYWORD`
- Python scan of C# string literals for non-ASCII -> `ASCII_STRING_LITERALS_PASS`

## Grid Summary
- Total hunter candidates: 28
- VALIDATED: 15
- UNCERTAIN: 9
- FILTERED: 4

Severity totals across all 28 candidates:
- Critical: 8
- High: 12
- Medium: 7
- Low: 1

Severity totals across VALIDATED set only:
- Critical: 4
- High: 8
- Medium: 3
- Low: 0

## 28-Hunter Findings

### H01
- Cluster: S1
- Candidate: BUG-S1-001 Ghost order window in local RMA submit
- Status: VALIDATED (High)
- Location: `src/V12_002.SIMA.Execution.cs` lines 332, 337, 364
- Execution path: `ExecuteSmartDispatchEntry -> SubmitLocalRMAEntry -> SubmitOrderUnmanaged -> callback window -> entryOrders/activePositions registration`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H02
- Cluster: S1
- Candidate: BUG-S1-002 Sideband clear-after-release race
- Status: VALIDATED (Critical)
- Location: `src/V12_002.SIMA.Fleet.cs` lines 70, 331, 335, 341
- Execution path: `PumpFleetDispatch -> ProcessValidPhotonSlot -> ProcessFleetSlot(finally ReleaseByIndex) -> return -> sideband clear`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H03
- Cluster: S1
- Candidate: BUG-S1-003 Abort drain leaves proactively registered state
- Status: VALIDATED (High)
- Location: `src/V12_002.SIMA.Dispatch.cs` lines 533, 535, 540, 543; `src/V12_002.SIMA.Fleet.cs` lines 242, 253, 258, 270
- Execution path: `Dispatch_Publish* registers active/entry/stop/targets -> abort -> DrainAllDispatchQueuesOnAbort only rolls back delta/pending`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H04
- Cluster: S1
- Candidate: BUG-S1-005 Shutdown delta rollback uses non-locked path
- Status: UNCERTAIN (Medium)
- Location: `src/V12_002.SIMA.Lifecycle.cs` lines 123, 142
- Execution path: `ProcessShutdownSIMA ring/queue drain -> AddExpectedPositionDelta while teardown callbacks may still run`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H05
- Cluster: S2
- Candidate: BUG-S2-001 Enqueued stop registration before follower bracket submit settle
- Status: UNCERTAIN (Critical)
- Location: `src/V12_002.Symmetry.Follower.cs` lines 324, 331; `src/V12_002.cs` lines 345, 349, 351
- Execution path: `SymmetryGuardSubmitFollowerBracket -> Enqueue(stopOrders write) -> acct.Submit`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H06
- Cluster: S2
- Candidate: BUG-S2-002 Target-replace cancel path gated inside entry-order branch
- Status: VALIDATED (Critical)
- Location: `src/V12_002.Orders.Callbacks.AccountOrders.cs` lines 365, 367, 396, 493, 502
- Execution path: `OnAccountOrderUpdate -> HandleMatchedFollowerOrder(entry gate) -> HandleMatchedFollower_TargetReplaceCancel`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H07
- Cluster: S2
- Candidate: BUG-S2-004 TOCTOU ContainsKey -> indexer on ConcurrentDictionary
- Status: VALIDATED (High)
- Location: `src/V12_002.Orders.Management.StopSync.cs` lines 224, 231; `src/V12_002.Orders.Management.Flatten.cs` lines 360, 362
- Execution path: `callback/flatten path -> ContainsKey -> concurrent remove -> indexer access`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H08
- Cluster: S2
- Candidate: BUG-S2-005 Stop replacement ghost-order window via enqueued map write
- Status: UNCERTAIN (Critical)
- Location: `src/V12_002.Trailing.StopUpdate.cs` lines 264, 276; `src/V12_002.Orders.Management.StopSync.cs` line 320
- Execution path: `target fill -> UpdateStopQuantity/CreateDirectStopOrder -> submit stop -> Enqueue(stopOrders map)`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H09
- Cluster: S3
- Candidate: BUG-S3-001 Panel refresh guard is check-then-set
- Status: VALIDATED (Medium)
- Location: `src/V12_002.UI.Panel.Lifecycle.cs` lines 67, 72, 73, 76
- Execution path: `timer tick A/B -> both pass Volatile.Read -> both Exchange(1) -> dual dispatcher work`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H10
- Cluster: S3
- Candidate: BUG-S3-002 IPC listener shutdown race (Pending vs null)
- Status: VALIDATED (High)
- Location: `src/V12_002.UI.IPC.Server.cs` lines 81, 83, 365, 368
- Execution path: `ListenForRemote loop -> ipcListener.Pending while StopIpcServer sets ipcListener=null`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H11
- Cluster: S3
- Candidate: BUG-S3-003 CANCEL_ALL cleanup-before-broker-final state window
- Status: VALIDATED (Critical)
- Location: `src/V12_002.UI.IPC.Commands.Fleet.cs` lines 211, 244, 263, 271, 282
- Execution path: `CANCEL_ALL -> cancel broker orders -> immediate CleanupPosition for unfilled -> late fill/callback without mapping`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H12
- Cluster: S3
- Candidate: BUG-S3-004 Stop-close cleanup omits target dictionaries
- Status: VALIDATED (Medium)
- Location: `src/V12_002.UI.Compliance.cs` lines 445, 448, 449
- Execution path: `fleet stop fill -> FinalizeStopFilledPosition -> remove stop/active/entry only`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL (state leak), ASCII PASS, Unsafe PASS

### H13
- Cluster: S4
- Candidate: BUG-S4-001 Naked-position audit scans live Account.Orders
- Status: VALIDATED (High)
- Location: `src/V12_002.REAPER.Audit.cs` line 490
- Execution path: `REAPER audit cycle -> AuditMaster_HandleNakedPosition -> Account.Orders.Any during broker mutation`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H14
- Cluster: S4
- Candidate: BUG-S4-002 Flatten cancel loop scans live targetAcct.Orders
- Status: VALIDATED (High)
- Location: `src/V12_002.REAPER.Audit.cs` lines 666, 679
- Execution path: `ProcessReaperFlattenQueue -> ProcessReaperFlatten_CancelWorkingOrders -> foreach targetAcct.Orders`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H15
- Cluster: S4
- Candidate: BUG-S4-003 Flatten close loop scans live targetAcct.Positions
- Status: VALIDATED (High)
- Location: `src/V12_002.REAPER.Audit.cs` lines 688, 715
- Execution path: `ProcessReaperFlattenQueue -> ProcessReaperFlatten_ClosePositions -> foreach targetAcct.Positions`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H16
- Cluster: S4
- Candidate: BUG-S4-005 In-flight guards use ContainsKey then TryAdd
- Status: VALIDATED (High)
- Location: `src/V12_002.REAPER.Audit.cs` lines 305, 319, 388, 391
- Execution path: `concurrent audit ticks -> both pass ContainsKey(false) -> both TryAdd/queue`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H17
- Cluster: S5
- Candidate: BUG-S5-001 Shutdown cancel sweep precedes actor command drain
- Status: VALIDATED (Critical)
- Location: `src/V12_002.Lifecycle.cs` lines 128, 130, 72, 74
- Execution path: `OnStateChangeTerminated -> CancelAllV12GtcOrders(false) -> DrainQueuesForShutdown -> cmd.Execute(this)`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H18
- Cluster: S5
- Candidate: BUG-S5-002 StickyState background serialization races live state mutation
- Status: UNCERTAIN (High)
- Location: `src/V12_002.StickyState.cs` lines 40, 46, 68, 144
- Execution path: `MarkStickyDirty -> Task.Run -> SerializeStickyState reads/writes live strategy config profile`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H19
- Cluster: S5
- Candidate: BUG-S5-003 _cmdQueue null dereference in shutdown drain
- Status: FILTERED (Medium)
- Location: `src/V12_002.Lifecycle.cs` line 72; `src/V12_002.cs` line 308
- Execution path: `DrainQueuesForShutdown -> _cmdQueue.TryDequeue`, but `_cmdQueue` is pre-initialized readonly
- DNA impact: No Locks PASS, Actor/Atomic ordering PASS, ASCII PASS, Unsafe PASS

### H20
- Cluster: S5
- Candidate: BUG-S5-004 Shutdown overflow discard drops queued actor work
- Status: VALIDATED (Medium)
- Location: `src/V12_002.Lifecycle.cs` lines 72, 79, 82, 83, 85
- Execution path: `DrainQueuesForShutdown executes <=50 actor cmds -> dequeues overflow without Execute`
- DNA impact: No Locks PASS, Actor/Atomic ordering FAIL, ASCII PASS, Unsafe PASS

### H21
- Cluster: S6
- Candidate: BUG-S6-001 Retest auto entry enqueue-add vs sync-remove rollback
- Status: UNCERTAIN (Critical)
- Location: `src/V12_002.Entries.Retest.cs` lines 173, 181, 187
- Execution path: `ExecuteRetestEntry -> Enqueue(activePositions add) -> SubmitOrderUnmanaged null -> activePositions.TryRemove`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H22
- Cluster: S6
- Candidate: BUG-S6-001b Retest manual entry enqueue-add vs sync-remove rollback
- Status: UNCERTAIN (Critical)
- Location: `src/V12_002.Entries.Retest.cs` lines 310, 318, 324
- Execution path: `ExecuteRetestManualEntry -> Enqueue(activePositions add) -> SubmitOrderUnmanaged null -> activePositions.TryRemove`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H23
- Cluster: S6
- Candidate: BUG-S6-003 OR arm-flag mutation without atomic primitive
- Status: UNCERTAIN (High)
- Location: `src/V12_002.Entries.OR.cs` lines 50, 59, 60, 93, 102, 103
- Execution path: `command callback -> ExecuteLong/ExecuteShort -> direct isLongArmed/isShortArmed writes`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H24
- Cluster: S6
- Candidate: BUG-S6-004 RMA proximity mutates PositionInfo sideband fields directly
- Status: UNCERTAIN (High)
- Location: `src/V12_002.Entries.RMA.cs` lines 266, 272, 279, 290, 291
- Execution path: `OnBarUpdate proximity loop -> entryOrders iteration -> mutate PositionInfo fields`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H25
- Cluster: S7
- Candidate: BUG-S7-001 Target-fill double-decrement between callbacks
- Status: FILTERED (High)
- Location: `src/V12_002.Orders.Callbacks.Execution.cs` lines 240, 242, 387; `src/V12_002.Orders.Callbacks.cs` lines 55, 76, 80
- Execution path: `OnExecutionUpdate + OnOrderUpdate both route fills, but ApplyTargetFill marks and exits on repeat`
- DNA impact: No Locks PASS, Actor/Atomic ordering PASS, ASCII PASS, Unsafe PASS

### H26
- Cluster: S7
- Candidate: BUG-S7-002 Possible late OrderId mapping residue after FSM termination
- Status: UNCERTAIN (Medium)
- Location: `src/V12_002.Symmetry.BracketFSM.cs` lines 102, 107, 120, 124, 130, 187, 219
- Execution path: `termination removes known IDs -> later broker event backfills _orderIdToFsmKey via fallback`
- DNA impact: No Locks PASS, Actor/Atomic ordering UNCERTAIN, ASCII PASS, Unsafe PASS

### H27
- Cluster: S7
- Candidate: BUG-S7-003 Photon pool release leak on fallback
- Status: FILTERED (Medium)
- Location: `src/V12_002.SIMA.Dispatch.cs` lines 651, 652, 777, 778; `src/V12_002.SIMA.Fleet.cs` line 70
- Execution path: `fallback paths explicitly ReleaseByIndex + clear sideband; consumer path releases in finally`
- DNA impact: No Locks PASS, Actor/Atomic ordering PASS, ASCII PASS, Unsafe PASS

### H28
- Cluster: S7
- Candidate: BUG-S7-004 Legacy lock(stateLock) remnant deadlock risk
- Status: FILTERED (Low)
- Location: `src/V12_002.cs` lines 227, 230
- Execution path: `stateLock exists as marker field/comment; no executable lock(...) found in src`
- DNA impact: No Locks PASS, Actor/Atomic ordering PASS, ASCII PASS, Unsafe PASS

## Recommended Forensic Repair Order
1. S1: H02, H03, H01
2. S5: H17, H20
3. S3: H11, H10, H09, H12
4. S2: H06, H07
5. S4: H13, H14, H15, H16
6. UNCERTAIN replay set (targeted fuzz/replay): H05, H08, H21, H22, H23, H24, H26, H18, H04

## Handoff
- Consolidated report written to `docs/brain/telemetry_and_audit_report.md`
- Source files were not modified (forensics-only sweep)
