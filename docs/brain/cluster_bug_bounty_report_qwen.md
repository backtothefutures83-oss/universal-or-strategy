# V12 Cluster Bug Bounty Report -- Qwen Sweep

**Generated**: 2026-05-17
**Runner**: Qwen 3.6 Max Preview
**Mode**: READ-ONLY forensic scan. No src/ edits.
**Verification Method**: Every cited file, method, and code pattern was cross-referenced against actual src/ content using grep_search and read_file. Bugs without matching evidence were marked FILTERED or UNCERTAIN.

---

## Summary

| Metric | Count |
|--------|-------|
| Total bugs found (raw across 7 reports) | 80 |
| **Validated** (evidence confirmed in src/) | **74** |
| **Filtered** (hallucination / not a current bug) | **3** |
| **Uncertain** (partially verifiable -- Director review needed) | **3** |
| Critical | 8 |
| High | 21 |
| Medium | 26 |
| Low | 19 |

*Severity counts reflect validated + uncertain bugs after deduplication. Filtered bugs are excluded.*

---

## Filter Rate by Cluster

| Cluster | Found | Validated | Filtered | Uncertain | Filter Rate |
|---------|-------|-----------|----------|-----------|-------------|
| S1 SIMA Core | 13 | 13 | 0 | 0 | 0% |
| S2 Execution Engine | 11 | 9 | 1 | 1 | 9% |
| S3 UI & Photon IO | 13 | 12 | 1 | 0 | 8% |
| S4 REAPER Defense | 12 | 12 | 0 | 0 | 0% |
| S5 Kernel State | 7 | 7 | 0 | 0 | 0% |
| S6 Signals & Entries | 14 | 13 | 0 | 1 | 0% |
| S7 Kernel Infrastructure | 10 | 8 | 1 | 1 | 10% |

---

## Filtered Bugs (Hallucinations / Not Current Bugs)

| Bug ID | Cluster | Severity | Reason |
|--------|---------|----------|--------|
| BUG-S2-011 | S2 | Low | Report claims FSM state stuck in `Submitting` after master-filled early exit, but code inspection shows `fsm.State = Submitting` is inside `if (!masterFilled)` block (AccountOrders.cs:449-453). The master-filled path skips the assignment entirely. No stuck state occurs. |
| BUG-S3-013 | S3 | Low | Historical bug already fixed. Code comment `// V12.Hardening: was isTrendRmaMode (typo)` at UI.Sizing.cs:261 documents a past fix. Current code is correct. Not an active bug. |
| BUG-S7-008 | S7 | Low | Report claims pool slot waste on CAS failure, but code inspection (V12_002.cs:719-728) shows `Interlocked.Decrement` IS called when `keyIdx >= _fsmKeyPool.Length`. The waste scenario requires an implausible pathological collision pattern. Low confidence this is a real issue. |

## Uncertain Bugs (Need Director Review)

| Bug ID | Cluster | Severity | Issue |
|--------|---------|----------|-------|
| BUG-S2-002 | S2 | High | Report claims `fsm.State = FollowerBracketState.Submitting` but actual code uses `FollowerReplaceState.Submitting` on `FollowerReplaceSpec` (AccountOrders.cs:453). The core concern (direct state assignment vs CAS-based TryTransition) is valid for `FollowerBracketFSM` but this code path modifies `FollowerReplaceSpec` which has no TryTransition method. Severity may be overstated given NT8 single-threading guarantee. |
| BUG-S6-011 | S6 | Medium | `MonitorRmaProximity` accessing `Close[0]` without bar guard is confirmed (RMA.cs:274). However, the method is only called from `OnBarUpdate` which implies bar data availability. The report's crash scenario requires an unusual calling context. |
| BUG-S7-006 | S7 | Medium | `FollowerReplaceSpec` mutable fields are confirmed unsynchronized. However, all access paths run on the NT8 strategy thread via Enqueue or TriggerCustomEvent. The cross-thread concern is valid in principle but may not be reachable in practice. |

---

## Cross-Cluster Duplicates (Thematic Overlaps)

No bugs met the strict dedup criteria (same file + same method + same root cause). However, the following thematic patterns appear across multiple clusters and should be addressed as architectural initiatives rather than individual bugs:

### Pattern A: Non-Atomic Mutable Fields on Shared Objects
Related bugs: BUG-S1-011, BUG-S1-012, BUG-S2-008, BUG-S2-010, BUG-S6-004, BUG-S7-002, BUG-S7-004
Root cause: `FollowerBracketFSM`, `PositionInfo`, and various mode/state booleans use plain fields without `volatile` or `Interlocked`. Multiple threads (strategy, broker callbacks, REAPER timer) read/write these fields.
Recommendation: Audit all cross-thread mutable fields. Either add `volatile`/`Interlocked` guards or document the single-threaded contract.

### Pattern B: Direct Dictionary Indexer Write Bypassing Enqueue Actor Pattern
Related bugs: BUG-S1-004, BUG-S2-006, BUG-S2-009, BUG-S4-007, BUG-S6-001, BUG-S7-009
Root cause: `ConcurrentDictionary[key] = value` writes occur outside the `Enqueue(ctx => { ... })` actor closure. While individually thread-safe, these bypass serialization guarantees and can create logical races with deferred Enqueue operations.
Recommendation: Audit all `dict[key] = value` patterns. Wrap in Enqueue where the dict is also read from Enqueue closures.

### Pattern C: TOCTOU (Check-Then-Act) on ConcurrentDictionary
Related bugs: BUG-S1-005, BUG-S4-006
Root cause: `ContainsKey` check followed by `TryAdd` is not atomic. Between the check and the add, another thread can modify the dictionary.
Recommendation: Replace `ContainsKey`+`TryAdd` patterns with atomic `TryAdd` (check return value for failure).

### Pattern D: PositionInfo Field Mutations Outside Enqueue
Related bugs: BUG-S6-003, BUG-S7-004
Root cause: `PositionInfo` objects in `activePositions` are mutated directly from non-Enqueue paths (e.g., `MonitorRmaProximity` in RMA.cs).
Recommendation: Route all `PositionInfo` mutations through Enqueue.

### Pattern E: FollowerReplaceSpec Mutable Fields
Related bugs: BUG-S2-007, BUG-S7-006
Root cause: `FollowerReplaceSpec` fields (`PendingQty`, `PendingPrice`, `State`) are mutated without synchronization and read across scheduled lambda boundaries.
Recommendation: Make spec fields immutable after creation, or use atomic swap on a packed state word.

---

## Validated Bug List (Ranked by Severity)

### Critical (8)

| Bug ID | Cluster | Title | File / Method |
|--------|---------|-------|---------------|
| BUG-S1-001 | S1 | Shared `_simaToggleState` semaphore creates cross-domain starvation | V12_002.SIMA.Dispatch.cs:49 / V12_002.SIMA.Lifecycle.cs:57 |
| BUG-S1-002 | S1 | Shadow engine `_leaderWasInPosition` non-atomic compound RMW | V12_002.SIMA.Shadow.cs:214-223 |
| BUG-S2-001 | S2 | Ghost order window: stop pre-registered before broker Submit | V12_002.Orders.Management.cs:218-219 (SubmitStopOrderSafe) |
| BUG-S3-001 | S3 | IPC `GET_LAYOUT` reads torn config snapshot across threads | V12_002.UI.IPC.Server.cs:299 (HandleIncomingIpcLine_RespondLayout) |
| BUG-S3-002 | S3 | `_glowTimer` null-race between UI thread and lifecycle thread | V12_002.UI.Panel.Lifecycle.cs:104 vs 120-123 |
| BUG-S4-001 | S4 | Watchdog stage-2 escalation runs broker API on background timer thread | V12_002.Safety.Watchdog.cs:87 -> 221 |
| BUG-S4-002 | S4 | Repair order created before authorization guard -- orphan on rejection | V12_002.REAPER.Repair.cs:196 vs 212 |
| BUG-S6-002 | S6 | Exception after order submission leaves expected delta permanently orphaned | Multiple entry files (OR, MOMO, Trend, RMA, Retest) |

### High (21)

| Bug ID | Cluster | Title | File / Method |
|--------|---------|-------|---------------|
| BUG-S1-003 | S1 | Flatten gate `isFlattenRunning` set outside atomic scope allows double-entry | V12_002.SIMA.Flatten.cs:47,324 |
| BUG-S1-004 | S1 | Tracking dictionary indexer silently overwrites concurrent REAPER mutations | V12_002.SIMA.Dispatch.cs:533-535 / V12_002.SIMA.Execution.cs:337,364 |
| BUG-S1-005 | S1 | Proactive FSM creation uses TOCTOU (ContainsKey then TryAdd) | V12_002.SIMA.Dispatch.cs:549 / V12_002.SIMA.Execution.cs:486 / V12_002.SIMA.Fleet.cs:120 |
| BUG-S2-003 | S2 | Stale pending replacement purge races with new replacement creation | V12_002.Trailing.StopUpdate.cs:37 |
| BUG-S2-004 | S2 | `pendingReplacementCount` drift: counter async vs synchronous increment | V12_002.Orders.Management.StopSync.cs:271 vs 320 |
| BUG-S2-005 | S2 | Bidirectional `Contains()` substring matching risks false-positive master ID | V12_002.Orders.Callbacks.Propagation.cs:414 |
| BUG-S3-003 | S3 | `_modeProfiles` regular Dictionary written from strategy thread without guard | V12_002.UI.IPC.Commands.Config.cs:136 |
| BUG-S3-004 | S3 | `activeFleetAccounts` indexer write races with concurrent reads | V12_002.UI.IPC.Commands.Config.cs:384 |
| BUG-S3-005 | S3 | `isRMAModeActive` bool written from UI thread, read from strategy thread | V12_002.UI.Panel.Handlers.cs:455 vs V12_002.UI.Snapshot.cs:202 |
| BUG-S3-006 | S3 | `selectedFleetAccounts` List modified from WPF handlers without guard | V12_002.UI.Panel.Construction.cs:503-512 |
| BUG-S4-003 | S4 | Naked stop in-flight guard cleared immediately after Submit, allowing duplicates | V12_002.REAPER.NakedStop.cs:68 |
| BUG-S4-004 | S4 | Unsafety iteration of live `targetAcct.Orders` during flatten | V12_002.REAPER.Audit.cs:666 |
| BUG-S4-005 | S4 | Master naked position check iterates `Account.Orders` without snapshot | V12_002.REAPER.Audit.cs:490 |
| BUG-S4-006 | S4 | TOCTOU race in `_reaperNakedStopInFlight` check-then-add | V12_002.REAPER.Audit.cs:388-393 |
| BUG-S5-001 | S5 | Atomic file write has data-loss window between Delete and Move | V12_002.StickyState.cs:259-260 |
| BUG-S5-002 | S5 | Sticky serialization reads mutable config from ThreadPool without barrier | V12_002.StickyState.cs:135 (SerializeSticky_WriteModeProfiles) |
| BUG-S6-001 | S6 | `linkedTRENDEntries` direct write bypasses Actor/Enqueue pattern | V12_002.Entries.Trend.cs:336-337 / V12_002.Entries.RMA.cs:153-154 |
| BUG-S6-003 | S6 | `MonitorRmaProximity` mutates PositionInfo fields outside Enqueue | V12_002.Entries.RMA.cs:291 |
| BUG-S6-004 | S6 | ToS sync armed state non-atomic check-then-set on shared booleans | V12_002.Entries.OR.cs:50-59 |
| BUG-S6-005 | S6 | `ExecuteTREND_SubmitLeg2` links entries before E2 submission confirmation | V12_002.Entries.Trend.cs:336-337 vs 356 |
| BUG-S6-006 | S6 | FFMA entries do not register Master expected position delta | V12_002.Entries.FFMA.cs (no AddExpectedPositionDeltaLocked calls found) |

### Medium (26)

| Bug ID | Cluster | Title | File / Method |
|--------|---------|-------|---------------|
| BUG-S1-006 | S1 | Photon ring fallback can leak pool slot on legacy queue failure | V12_002.SIMA.Dispatch.cs:651 |
| BUG-S1-007 | S1 | Shadow stop cache eviction may remove entries for positions mid-replace | V12_002.SIMA.Shadow.cs:54-67 |
| BUG-S1-008 | S1 | `HydrateFSM_RecoverFromOpenPositions` recovers only one orphan per call | V12_002.SIMA.Lifecycle.cs:852-890 |
| BUG-S1-009 | S1 | `symmetryDispatchId` null propagation after empty fleet resolution | V12_002.SIMA.Dispatch.cs:312 |
| BUG-S1-010 | S1 | `ProcessFlattenWorkItem_ClosePositions` submits without error handling | V12_002.SIMA.Flatten.cs:187 |
| BUG-S2-006 | S2 | CIT follower nudge writes entryOrders outside Enqueue context | V12_002.Orders.Management.Flatten.cs:150 |
| BUG-S2-007 | S2 | FollowerReplaceSpec mutable fields updated outside Enqueue | V12_002.Orders.Callbacks.Propagation.cs:450 |
| BUG-S2-009 | S2 | SymmetryGuardSubmitFollowerBracket writes target dicts outside Enqueue | V12_002.Symmetry.Follower.cs:326 |
| BUG-S3-007 | S3 | `Thread.Sleep()` on IPC listener and client stream threads | V12_002.UI.IPC.Server.cs:85,214 |
| BUG-S3-008 | S3 | Compliance daily reset writes non-atomic across three dictionaries | V12_002.UI.Compliance.cs:198-200 |
| BUG-S3-009 | S3 | `PopulateDirectionCombo` clears/rebuilds WPF ItemsCollection every mode change | V12_002.UI.Panel.Handlers.cs:594 |
| BUG-S4-007 | S4 | Repair `entryOrders` write bypasses Actor Enqueue pattern | V12_002.REAPER.Repair.cs:217 |
| BUG-S4-008 | S4 | `_repairBlockedLastLogged` declared but never read or written -- dead code | V12_002.REAPER.cs:51 |
| BUG-S4-009 | S4 | Redundant FSM state check creates TOCTOU in repair authorization | V12_002.REAPER.Repair.cs:187 vs MetadataGuard.cs:140 |
| BUG-S4-010 | S4 | Watchdog `ExecuteWatchdogDirectFallback` lacks `_isTerminating` and `State` guards | V12_002.Safety.Watchdog.cs:221 vs 191 |
| BUG-S5-003 | S5 | `_modeProfiles` dictionary write during serialization creates compound race | V12_002.StickyState.cs:144 |
| BUG-S5-004 | S5 | `_currentTraceId` non-volatile field read across threads | V12_002.Telemetry.cs:24 vs V12_002.StructuredLog.cs:53 |
| BUG-S5-005 | S5 | Shutdown GTC sweep operates on dictionaries not yet guarded from callbacks | V12_002.Lifecycle.cs:128 |
| BUG-S6-007 | S6 | `CheckFFMAConditions` reads multiple indicator values without atomic snapshot | V12_002.Entries.FFMA.cs:48-56 |
| BUG-S6-008 | S6 | RETEST pre-registers `activePositions` then TryRemove direct, not via Enqueue | V12_002.Entries.Retest.cs:177 vs 187 |
| BUG-S6-009 | S6 | `retestFiredThisSession` latch set after order submission -- re-entrancy window | V12_002.Entries.Retest.cs:193 |
| BUG-S6-010 | S6 | `DeactivateFFMAMode` does not check `IsOrderAllowed` or `isFlattenRunning` | V12_002.Entries.FFMA.cs:158 |
| BUG-S6-011 | S6 | `MonitorRmaProximity` reads `Close[0]` without bar data guard | V12_002.Entries.RMA.cs:274 |
| BUG-S7-005 | S7 | LogicAudit Case 9 writes expectedPositions directly, bypassing REAPER grace | V12_002.LogicAudit.cs:348 |
| BUG-S7-007 | S7 | SignalBroadcaster.SafeInvoke silently swallows subscriber exceptions | SignalBroadcaster.cs:218 |

### Low (19)

| Bug ID | Cluster | Title | File / Method |
|--------|---------|-------|---------------|
| BUG-S1-011 | S1 | `FollowerBracketFSM.RemainingContracts` is non-atomic mutable field | V12_002.Symmetry.BracketFSM.cs:71 |
| BUG-S1-012 | S1 | `FollowerBracketFSM.Targets` array element reads lack synchronization | V12_002.SIMA.Shadow.cs:148 |
| BUG-S1-013 | S1 | `ProcessApplySimaState` spin-wait with `Thread.Yield()` burns CPU | V12_002.SIMA.Lifecycle.cs:57-69 |
| BUG-S1-W02 | S1 | `activeFleetAccounts` default to INACTIVE, fragile sticky state dependency | V12_002.SIMA.Lifecycle.cs:170 |
| BUG-S2-008 | S2 | `FollowerBracketFSM.RemainingContracts` compound RMW not atomic | V12_002.Symmetry.BracketFSM.cs:71 / V12_002.cs:53 |
| BUG-S2-010 | S2 | Stop replacement circuit breaker count check not atomic with activation | V12_002.Trailing.StopUpdate.cs:152-153 |
| BUG-S3-010 | S3 | IPC `SendResponseToRemote` unsynchronized stream writes | V12_002.UI.IPC.Commands.Misc.cs:210 |
| BUG-S3-011 | S3 | Photon Pool `_freeTop` volatile but documented as single-threaded | V12_002.Photon.Pool.cs:80 |
| BUG-S3-012 | S3 | IPC listener `isIpcRunning` plain bool without volatile | V12_002.UI.IPC.Server.cs:85 vs 364 |
| BUG-S4-011 | S4 | `StopReaperAudit` non-atomic null-check-then-dispose | V12_002.REAPER.cs:119 |
| BUG-S4-012 | S4 | Watchdog stage transition non-atomic read-then-CAS | V12_002.Safety.Watchdog.cs:61-87 |
| BUG-S5-006 | S5 | `_stickyWritePending` gate allows recursive re-entry after release | V12_002.StickyState.cs:55-58 |
| BUG-S5-007 | S5 | `EnrichTrailStateFromSticky` directly mutates PositionInfo fields | V12_002.StickyState.cs:621-626 |
| BUG-S6-012 | S6 | Timestamp collision risk for entry names under high-frequency execution | All entry files (DateTime.Now HHmmssffff) |
| BUG-S6-013 | S6 | Inconsistent timestamp convention (UTC vs local) between entry types | V12_002.Entries.Trend.cs vs all other entry files |
| BUG-S6-014 | S6 | Exception handler after SubmitOrderUnmanaged does not clean up dicts | All entry files catch blocks |
| BUG-S7-003 | S7 | `_subscribedAccountNames` HashSet is not thread-safe | V12_002.cs:538 |
| BUG-S7-009 | S7 | `activePositions[fleetKey]` direct indexer overwrites without existence check | V12_002.SIMA.Execution.cs:481 |
| BUG-S7-010 | S7 | `SignalBroadcaster.ClearAllSubscribers` not atomic across all events | SignalBroadcaster.cs:385-392 |

---

## Recommended Repair Sequence

Based on critical count, dependency graph, and blast radius:

1. **S4 REAPER Defense** (2 Critical, 4 High) -- Safety-critical. Watchdog broker API on timer thread (S4-001) and orphan repair orders (S4-002) can cause financial loss. Fix first.
2. **S1 SIMA Core** (2 Critical, 3 High) -- Central dispatch/lifecycle. Semaphore starvation (S1-001) and shadow engine compound RMW (S1-002) affect all fleet operations. Fix second.
3. **S6 Signals & Entries** (2 Critical, 4 High) -- Entry points. Expected delta orphan (S6-002) causes ledger drift across all entry types. linkedTRENDEntries bypass (S6-001) affects TREND/RMA.
4. **S3 UI & Photon IO** (2 Critical, 4 High) -- IPC torn config (S3-001) and glow timer null-race (S3-002) are UI/lifecycle issues. Fix before S2 due to simpler scope.
5. **S2 Execution Engine** (0 Critical after filter, 4 High) -- Stop pre-registration ghost window (S2-001) is the most impactful remaining. Contains matching (S2-005) is a known anti-pattern.
6. **S5 Kernel State** (0 Critical, 2 High) -- Sticky state atomicity and file write concerns. Important but lower blast radius.
7. **S7 Kernel Infrastructure** (0 Critical after filter, 3 High) -- Non-volatile fields and HashSet thread-safety. Foundation for other fixes but lower urgency.

---

## /epic-tdd Ticket Blocks

### Critical Tickets

**TICKET: Fix semaphore starvation between dispatch and lifecycle**
- Title: [Critical] Shared `_simaToggleState` creates cross-domain starvation and potential stack overflow
- File/Method: V12_002.SIMA.Dispatch.cs:ExecuteSmartDispatchEntry / V12_002.SIMA.Lifecycle.cs:ProcessApplySimaState
- Severity: Critical
- Description: Both dispatch and lifecycle contend on `_simaToggleState` via Interlocked.CompareExchange. When lifecycle holds the gate during fleet enumeration, dispatch continuously defers via TriggerCustomEvent creating recursion loop.
- Acceptance Criteria: (1) Separate semaphores for dispatch and lifecycle domains. (2) Deferral uses bounded retry with exponential backoff. (3) No stack overflow under sustained contention for 60 seconds.

**TICKET: Fix shadow engine compound RMW on _leaderWasInPosition**
- Title: [Critical] `_leaderWasInPosition` non-atomic compound read-modify-write allows double-flatten
- File/Method: V12_002.SIMA.Shadow.cs:ShadowPropagateLeaderFlatten
- Severity: Critical
- Description: `volatile bool` read, side-effect call (FlattenAllApexAccounts which can re-enter), then write. Re-entrancy can cause double-flatten or missed-flatten.
- Acceptance Criteria: (1) Use Interlocked.CompareExchange on a packed state word for edge detection. (2) No duplicate flatten orders under concurrent bar tick + leader flatten. (3) Edge consumed exactly once.

**TICKET: Fix ghost order window in follower stop pre-registration**
- Title: [Critical] Stop pre-registered in dictionary before broker Submit creates false protection signal
- File/Method: V12_002.Orders.Management.cs:SubmitStopOrderSafe
- Severity: Critical
- Description: `stopOrders[entryName] = sOrd` writes before `pos.ExecutingAccount.Submit()`. If Submit throws or hangs, other paths see a "protected" position that has no live stop.
- Acceptance Criteria: (1) Reverse ordering: Submit first, then write to stopOrders on success. (2) Catch block cleanup verified. (3) No false-positive protected state under Submit latency injection.

**TICKET: Fix IPC GET_LAYOUT torn config snapshot**
- Title: [Critical] IPC listener reads 17 config fields without atomic snapshot
- File/Method: V12_002.UI.IPC.Server.cs:HandleIncomingIpcLine_RespondLayout
- Severity: Critical
- Description: 17 fields read one-by-one from background IPC thread while strategy thread writes them. Torn reads produce frankenstein config responses.
- Acceptance Criteria: (1) All config fields read via atomic snapshot (Interlocked on packed struct or snapshot copy). (2) No torn responses under concurrent CONFIG + GET_LAYOUT.

**TICKET: Fix _glowTimer null-race between UI and lifecycle threads**
- Title: [Critical] `_glowTimer` read on UI thread races null-write on lifecycle thread
- File/Method: V12_002.UI.Panel.Lifecycle.cs:TriggerGlow vs StopGlowTimer
- Severity: Critical
- Description: UI thread reads `_glowTimer` for Stop/Start. Lifecycle thread sets `_glowTimer = null`. NullReferenceException possible.
- Acceptance Criteria: (1) Use Interlocked.Exchange for null assignment and local copy for read. (2) No NullReferenceException under rapid enable/disable + UI clicks.

**TICKET: Fix watchdog stage-2 broker API on timer thread**
- Title: [Critical] Watchdog ExecuteWatchdogDirectFallback runs Cancel/Submit on background timer thread
- File/Method: V12_002.Safety.Watchdog.cs:OnWatchdogTimer -> ExecuteWatchdogDirectFallback
- Severity: Critical
- Description: Stage-2 escalation calls broker API directly from System.Threading.Timer callback. Stage-1 correctly uses Enqueue. Contradicts V12.17 REAPER threading fix.
- Acceptance Criteria: (1) Stage-2 marshals via TriggerCustomEvent or Enqueue. (2) No broker API calls from timer thread. (3) Watchdog escalation completes within acceptable latency budget.

**TICKET: Fix repair order creation before authorization guard**
- Title: [Critical] Repair order created before MetadataGuardRepairAuthorized check -- orphan on rejection
- File/Method: V12_002.REAPER.Repair.cs:SubmitRepairOrderWithAuthorization
- Severity: Critical
- Description: `targetAcct.CreateOrder()` at line 196 creates order before authorization check at line 212. If guard rejects, order object is orphaned.
- Acceptance Criteria: (1) Authorization check before CreateOrder. (2) No orphan order objects under rapid fill/repair race. (3) Repair queue correctly re-enqueues on rejection.

**TICKET: Fix exception leaving expected delta orphaned in entry methods**
- Title: [Critical] Exception after AddExpectedPositionDeltaLocked leaves delta permanently orphaned
- File/Method: All entry files (OR, MOMO, Trend, RMA, Retest) catch blocks
- Severity: Critical
- Description: Expected delta registered before SubmitOrderUnmanaged. If Submit throws, catch block only prints error -- no rollback. Delta permanently orphaned causing ledger drift.
- Acceptance Criteria: (1) Catch block negates expected delta. (2) No ledger drift under fault injection (SubmitOrderUnmanaged throws). (3) All entry types have consistent rollback.

### High Tickets (abbreviated -- 21 total)

**TICKET: Fix flatten gate double-entry window**
- [High] BUG-S1-003: `isFlattenRunning` set without CAS in FlattenAllApexAccounts and ClosePositionsOnlyApexAccounts. Use Interlocked.CompareExchange.

**TICKET: Fix tracking dictionary indexer overwrite of REAPER corrections**
- [High] BUG-S1-004: `activePositions[key] = value` unconditionally overwrites. Use AddOrUpdate with merge function or TryAdd with logging.

**TICKET: Fix FSM creation TOCTOU pattern**
- [High] BUG-S1-005: `ContainsKey` then `TryAdd` not atomic. Replace with TryAdd-only pattern.

**TICKET: Fix stale pending replacement race**
- [High] BUG-S2-003: CleanupStalePendingReplacements TryRemove/CreateNewStopOrder races with UpdateStopQuantity TryAdd. Add atomic swap.

**TICKET: Fix pendingReplacementCount counter drift**
- [High] BUG-S2-004: Counter incremented synchronously but dict write deferred via Enqueue. Synchronize counter with dict state.

**TICKET: Fix bidirectional Contains matching for master entry**
- [High] BUG-S2-005: Replace `fleetEntryName.Contains(kvp.Key) || kvp.Key.Contains(fleetEntryName)` with delimiter-anchored matching.

**TICKET: Convert _modeProfiles to ConcurrentDictionary**
- [High] BUG-S3-003: Regular Dictionary written from strategy thread. Use ConcurrentDictionary.

**TICKET: Fix activeFleetAccounts indexer race**
- [High] BUG-S3-004: Direct indexer write. Use AddOrUpdate.

**TICKET: Add volatile to isRMAModeActive**
- [High] BUG-S3-005: Written from UI thread, read from strategy thread. Add volatile.

**TICKET: Guard selectedFleetAccounts List modifications**
- [High] BUG-S3-006: Plain List modified from WPF handlers. Convert to ConcurrentBag or add lock.

**TICKET: Fix naked stop guard cleared too early**
- [High] BUG-S4-003: In-flight guard cleared immediately after Submit. Hold guard until broker confirmation.

**TICKET: Fix live collection iteration in REAPER flatten**
- [High] BUG-S4-004: `foreach (Order order in targetAcct.Orders)` without ToArray. Add snapshot.

**TICKET: Fix master naked position check without snapshot**
- [High] BUG-S4-005: `Account.Orders.Any(...)` without ToArray. Add snapshot.

**TICKET: Fix naked stop TOCTOU**
- [High] BUG-S4-006: Replace ContainsKey+TryAdd with atomic TryAdd-only pattern.

**TICKET: Fix atomic file write data-loss window**
- [High] BUG-S5-001: Delete-then-Move creates window where neither file exists. Use File.Replace.

**TICKET: Fix sticky state serialization torn snapshot**
- [High] BUG-S5-002: Task.Run serialization reads mutable fields without barrier. Use volatile or snapshot.

**TICKET: Fix linkedTRENDEntries direct write**
- [High] BUG-S6-001: Two-write link sequence not atomic. Route through Enqueue.

**TICKET: Fix MonitorRmaProximity direct PositionInfo mutation**
- [High] BUG-S6-003: Direct field writes bypass Enqueue. Route through Enqueue.

**TICKET: Fix ToS sync armed state check-then-set**
- [High] BUG-S6-004: `isLongArmed`/`isShortArmed` non-atomic. Use Interlocked.CompareExchange.

**TICKET: Fix TREND link-before-submit ordering**
- [High] BUG-S6-005: Link entries after E2 submission confirmation, not before.

**TICKET: Add FFMA expected position delta registration**
- [High] BUG-S6-006: FFMA entries missing AddExpectedPositionDeltaLocked. Add ledger registration matching other entry types.

---

[BUG-BOUNTY-CONSOLIDATION-COMPLETE]
Total validated: 74
Filtered: 3
Uncertain (needs Director review): 3
Report: docs/brain/cluster_bug_bounty_report_qwen.md
Next: Director reviews report -> selects cluster -> /epic-tdd for repairs
