# V12 Photon Kernel Bug Registry
## (Living Document: Forensic Tracking & Hardening)

This registry tracks every critical bug, race condition, and logic failure identified during the V12 Phase 7 Hardening mission. It serves as the "Proof of Failure" log required for P3 Architectural Design and P5 Engineering verification.

### 🚩 Critical Bug Tracker

| ID | Bug Name | Discovery Method | Agent / Source | Status | Summary |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BUG-001** | Race Condition: Unsubscribe Leak | Forensic Audit | Arena AI (Sonnet 4.6) | 🔴 Open | Double handler removal + O(N^2) loops in fleet unsubscribe. |
| **BUG-002** | Pump Re-Entrancy Flood | Baseline Reproducer | Antigravity / Arena | 🔴 Open | Recursive call to `PumpFleetDispatch` via `TriggerCustomEvent`. |
| **BUG-003** | Use-After-Free Window | Baseline Reproducer | Antigravity / Arena | 🔴 Open | Pool slot released before sideband clearing in `ProcessFleetSlot`. |
| **BUG-004** | XorShadow Invariant Clash | Forensic Audit | Arena AI (Sonnet 4.6) | 🔴 Open | Shadow salt zeroing contradiction in `VerifyPhotonSlotIntegrity`. |
| **BUG-005** | Atomic FSM Creation Failure | Logic Walkthrough | Arena AI (Sonnet 4.6) | 🔴 Open | Non-atomic check-then-set race in `InitializeFollowerBracketFSM`. |
| **BUG-006** | Null Ref (Hot Path) | Forensic Audit | Arena AI (Sonnet 4.6) | 🟡 Verification | Potential null access on `pos.Instrument` (Partial Hallucination). |
| **BUG-007** | O(N^2) Performance Degrade | AMAL Harness | Arena AI (Sonnet 4.6) | 🔴 Open | Performance bottleneck in fleet account iteration. |
| **BUG-008** | Sideband Poisoning | Logic Walkthrough | Arena AI (Sonnet 4.6) | 🔴 Open | Stale data retention in sideband during ring buffer wrap. |
| **BUG-009** | FSM State Leak | Logic Walkthrough | Arena AI (Sonnet 4.6) | 🔴 Open | Incomplete state reset during cancel/rollback cycles. |
| **BUG-010** | Ghost Order Window | Forensic Audit | Arena AI (Sonnet 4.6) | 🔴 Open | Use of `Enqueue` instead of Direct Write for stop orders. |
| **BUG-011** | Double-Free (Shadow) | Logic Walkthrough | Arena AI (Sonnet 4.6) | 🔴 Open | Double disposal of GCHandles in Shadow Engine. |
| **BUG-012** | Tick Noise Bypass | Baseline Reproducer | Arena AI (Sonnet 4.6) | 🔴 Open | Price drift allowed by overly aggressive half-tick noise filter. |
| **BUG-013** | Semaphore Leak | Forensic Audit | Arena AI (Sonnet 4.6) | 🔴 Open | Missing `finally` block for `_simaToggleSem` release. |
| **BUG-014** | Instrument Lookup Lag | AMAL Harness | Arena AI (Sonnet 4.6) | 🔴 Open | Inefficient dictionary lookups in high-frequency loop. |
| **BUG-015** | Async ID Mapping Failure | Logic Walkthrough | Arena AI (Sonnet 4.6) | 🔴 Open | `OrderId` registration before async broker assignment. |
| **BUG-016** | Watchdog Naked Stop Leak | User Report | Forensic / User | 🔴 Open | Manual flatten leaves orphaned stop-loss orders live. |
| **BUG-017** | Race Condition in PumpFleetDispatch (T1) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `isFlattenRunning` and `EnableSIMA` checked without state safety. |
| **BUG-018** | Counter Corruption via Multiple Decrements (T2) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `_pendingFleetDispatchCount` decremented multiple paths without validation. |
| **BUG-019** | Concurrent Ring Modification During Drain (T3) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | New dispatches enqueued while `DrainAllDispatchQueuesOnAbort` runs. |
| **BUG-020** | Unsafe ConcurrentDictionary Iteration (T4) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Iterating `_followerBrackets` while modified. |
| **BUG-021** | State Inconsistency Window in Rollback (T5) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Sync flag cleared before position delta rollback. |
| **BUG-022** | Collection Modification During Iteration (T7) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `Account.All.ToArray()` may throw if modified. |
| **BUG-023** | Unbounded `_orderIdToFsmKey` Growth (M1) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Dictionary grows indefinitely, leaking memory. |
| **BUG-024** | Incomplete Rollback Orphans Dictionary Entries (M2) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `RollbackFleetDispatchState` does not clean `_orderIdToFsmKey`. |
| **BUG-025** | Sideband Reference Retention on Exception (M4) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Account refs retained if `ProcessFleetSlot` throws before clearing. |
| **BUG-026** | Health Check Findings Ignored (L1) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `ShouldSkipFleet_RunHealthCheck` returns void, findings ignored. |
| **BUG-027** | Inconsistent Shadow Verification Logic (L2) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Shadow set to 0 before recompute despite comment. |
| **BUG-028** | Unchecked FSM State Transitions (L5) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | FSM state set without validating current state. |
| **BUG-029** | Counter Decrement Without Bounds Check (L6) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Counter decremented even if already zero. |
| **BUG-030** | Multi-Dictionary Desync Risk (L7) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `activePositions`, `_followerBrackets`, `entryOrders` drift. |
| **BUG-031** | Broad Exception Swallowing (E1) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Generic catch blocks hide critical errors. |
| **BUG-032** | Missing Null Validation (E2) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `expectedKey` not validated in rollback operations. |
| **BUG-033** | Silent Pump Failure (E3) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `TriggerCustomEvent` catch hides pump stalls. |
| **BUG-034** | Double Unsubscribe Inefficiency (E4) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | Unsubscribe runs twice. |
| **BUG-035** | Unnecessary Array Copy (P1) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `Array.Copy` executes when orderCount == orders.Length. |
| **BUG-036** | High-Frequency Allocation in Health Check (P2) | Forensic Audit | Arena AI (Sonnet 4.5) | 🔴 Open | `Positions.ToArray()` allocates on every invocation. |
| **BUG-037** | Unprotected FSM State Mutation (TS-002) | Forensic Audit | Arena AI (Qwen 3.6+) | 🔴 Open | FSM state mutated in `SubmitAndRegisterFleetOrders` without atomic safety. |
| **BUG-038** | OcoGroupId Overwritten in Loop (LB-001) | Logic Walkthrough | Arena AI (Qwen 3.6+) | 🔴 Open | OCO group ID overwritten in target order loop, causing FSM state desync. |
| **BUG-039** | No FSM Timeout / Dead State Detection (SM-002) | Logic Walkthrough | Arena AI (Qwen 3.6+) | 🔴 Open | FSMs stuck in Submitted/Accepted state never timeout or rollback. |
| **BUG-040** | Shadow Verification Rollback Incomplete (IG-001) | Logic Walkthrough | Arena AI (Qwen 3.6+) | 🔴 Open | Integrity failure clears FSM but orphans orders if already sent to broker. |
| **BUG-041** | Non-Concurrent `_orderIdToFsmKey` (Thread Safety) | Forensic Audit | Arena AI (Qwen 3.6 Max) | 🔴 Open | Broker threads read Dictionary while strategy thread writes. |
| **BUG-042** | Torn Read on `FollowerBracketFSM.EntryOrder` | Forensic Audit | Arena AI (Qwen 3.6 Max) | 🔴 Open | Reference write is not atomic with state change. |
| **BUG-043** | Torn Read on `_photonSideband` Structs | Forensic Audit | Arena AI (Qwen 3.6 Max) | 🔴 Open | Array element writes not atomic; broker thread reads partial struct. |
| **BUG-044** | `dispatchLog` StringBuilder Leak | Forensic Audit | Arena AI (Qwen 3.6 Max) | 🔴 Open | String builder grows unbounded, never flushed or cleared. |
| **BUG-045** | Abort Cycle Misses Ring Gaps | Forensic Audit | Arena AI (Qwen 3.6 Max) | 🔴 Open | `DrainAllDispatchQueuesOnAbort` misses ring gaps causing sideband leak. |
| **BUG-046** | `acct.Submit()` Lacks Exception Rollback | Forensic Audit | Arena AI (Qwen 3.6 Max) | 🔴 Open | No try-catch or rollback if broker submit call throws natively. |
| **BUG-047** | Null Ref Risk on Sideband Entities | Logic Walkthrough | GPT 5.3 Codex | 🔴 Open | Sideband account and pooled arrays consumed without explicit null guards. |
| **BUG-048** | Pump Event Storm Risk | Logic Walkthrough | GPT 5.3 Codex | 🔴 Open | Non-atomic emptiness checks + `TriggerCustomEvent` can flood the UI thread. |
| **BUG-049** | Missing Bounds Validation on `orderCount` | Logic Walkthrough | GPT 5.3 Codex | 🔴 Open | FSM loop trusts payload `orderCount` without clamping to `orders.Length`. |
| **BUG-050** | Misaligned Diagnostic Stack Traces | Logic Walkthrough | GPT 5.3 Codex | 🔴 Open | Catch blocks output confusing logs referencing unrelated methods. |
| **BUG-051** | Sync Cleared Before Broker ACK | Logic Walkthrough | GPT 5.2 Codex | 🔴 Open | `ClearDispatchSyncPending` executed immediately, falsely unlocking dispatch. |
| **BUG-052** | Integrity Drop Lacks Requeue | Logic Walkthrough | GPT 5.2 Codex | 🔴 Open | Photon integrity failure simply drops the dispatch without any retry strategy. |
| **BUG-053** | Account Snapshot Staleness | Logic Walkthrough | GPT 5.2 Codex | 🔴 Open | Stale active account snapshot can skip valid accounts during SIMA toggles. |
| **BUG-054** | Use-After-Free Window on Pool Release | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Sideband cleared AFTER pool release; concurrent allocation can corrupt references. |
| **BUG-055** | `TriggerCustomEvent` Stack Overflow Risk | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Re-entrant pump inside `finally` block creates unbounded call chain under load. |
| **BUG-056** | `PendingSubmit` Guard Never True | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | FSM initialized as Submitted; PendingSubmit check fails, bypassing timestamp update. |
| **BUG-057** | `_orderIdToFsmKey` Gated on Stale Check | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Redundant `TryGetValue` bypasses order ID registration if FSM creation skipped. |
| **BUG-058** | ConcurrentDictionary Enumeration Mutation | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Enumerating dict without snapshot can double-count or miss entries on live mutation. |
| **BUG-059** | `syncCleared` Flag Shadowing | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Catch block `!syncCleared` logic fails on partial submit, reversing valid deltas. |
| **BUG-060** | Double Unsubscribe Handler Leak | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Full sweep combined with tracked set double-removes handlers, leaking instances. |
| **BUG-061** | Shadow Field Zeroed on Copy | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Struct copy zeroing does not propagate to actual ring slot, causing blind spots. |
| **BUG-062** | `acct.Positions.ToArray()` O(N²) Leak | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Broker collection snapshotted inside per-account loop causes extreme GC pressure. |
| **BUG-063** | `StartsWith('T')` Broad Target Catch | Logic Walkthrough | Sonnet 4.6 (Run 2) | 🔴 Open | Target order detection catches "Trailing_Stop", silently dropping bracket. |
| **BUG-064** | Pool Release Exception Kills Cleanup | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Exception in `ReleaseByIndex` in `finally` block aborts counter decrement and pump prime. |
| **BUG-065** | No Maximum Queue Depth Protection | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Signal spam can enqueue infinitely, causing OutOfMemory crash. |
| **BUG-066** | Unvalidated Sideband Array Bounds | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | `PoolSlotIndex` used blindly; bounds mismatch throws `IndexOutOfRangeException` killing pump thread. |
| **BUG-067** | Timestamp Guard Silent Failure | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Unhandled exceptions in `MetadataGuardTimestamp` silently break dispatch deduplication. |
| **BUG-068** | Generic Catch Hides Fatal Errors | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Broad `catch(Exception)` hides OutOfMemory and StackOverflow exceptions. |
| **BUG-069** | Silent Pump Prime Failures | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | `TriggerCustomEvent` exceptions silently swallowed; queue locks up permanently. |
| **BUG-070** | Missing Submit Circuit Breaker | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Broker disconnect causes infinite failure loop; no exponential backoff or circuit breaker. |
| **BUG-071** | Hot Path String Allocation | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Eager `string.Format` and interpolation inside diagnostic prints cause high GC pressure. |
| **BUG-072** | Sequential TryRemove Thrashing | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Unbatched dictionary cleanup operations cause unnecessary cache locality loss and lock contention. |
| **BUG-073** | Repeated Dictionary Lookups | Logic Walkthrough | Sonnet 4.5 Thinking (Run 2) | 🔴 Open | Same key looked up multiple times in `_followerBrackets` instead of caching local variable. |
| **BUG-074** | Sideband Read Before Shadow Verification | Logic Walkthrough | Sonnet 4.5 Thinking (Run 3) | 🔴 Open | Code reads `_photonSideband[_sbIdx]` *before* verifying the XOR shadow checksum, risking execution on corrupted data. |
| **BUG-075** | Unsubscribe Race Condition | Logic Walkthrough | Sonnet 4.5 Thinking (Run 3) | 🔴 Open | `_subscribedAccountNames.Clear()` is not synchronized with concurrent subscribe operations. |
| **BUG-076** | In-Flight Submissions Escape Drain | Logic Walkthrough | Sonnet 4.5 Thinking (Run 3) | 🔴 Open | `DrainAllDispatchQueuesOnAbort` only drains the queue; it does not cancel submissions already de-queued but not yet sent to the broker. |
| **BUG-077** | Linear FSM Search O(N) Complexity | Logic Walkthrough | Sonnet 4.5 Thinking (Run 3) | 🔴 Open | Code iterates all `_followerBrackets` entries linearly, causing O(N) performance degradation as FSMs accumulate. |
| **BUG-078** | OrderId Registration Race | Logic Walkthrough | Qwen 3.6 Max (Run 2) | 🔴 Open | Mapping `OrderId` -> FSM immediately post-submit races with the broker callback; mapping should occur inside `OnAccountOrderUpdate`. |
| **BUG-079** | Null Pool Reference Risk | Logic Walkthrough | Qwen 3.6 Max (Run 2) | 🔴 Open | Missing null verification check immediately after `_photonPool.GetByIndex()`. |
| **BUG-080** | ABA / Stale Sideband Read | Logic Walkthrough | Qwen 3.6 Max (Run 2) | 🔴 Open | Sideband lacks a generation counter, allowing ABA problems and stale reads if a slot is rapidly freed and reallocated. |

### 🧬 Discovery Definitions

*   **Forensic Audit**: Deep static analysis of source code logic.
*   **Baseline Reproducer**: Surgical C# code (`SIMA_Baseline_Test.cs`) designed to trigger the bug.
*   **AMAL Harness**: Automated performance and allocation stress testing.
*   **Logic Walkthrough**: Step-by-step trace of a signal through multiple files.

### 📝 Revision History
- **2026-05-16**: Initialized Registry with 15 Arena.ai bugs + Watchdog Flattening bug. (Antigravity)
