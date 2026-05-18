# Epic 1 Delta Plan: Build 981 Concurrency Hardening
## Target Scope: S1/S2 Verified Defects (H01-H04, H06-H07)

> **Mission**: V12 Photon Kernel Concurrency Hardening (Epic 1 Delta)
> **BUILD_TAG**: `V14.2-Sovereign-Photon-Delta`
> **Date**: 2026-05-18T04:20:00Z
> **Target Branch**: `feature/photon-spsc-hardening`
> **Status**: **CORRECTIONS APPLIED -- PENDING DIRECTOR APPROVAL** (Validated 2026-05-18 by Antigravity live source scan)

---

## 1. Executive Summary

This Delta Plan specifies the surgical repairs and Test-Driven Development (TDD) validation suites for the newly validated S1/S2 defects (**H01-H04, H06-H07**) identified in the **Consolidated Bug Bounty Report** ([cluster_bug_bounty_report.md](file:///C:/WSGTA/universal-or-strategy/docs/brain/cluster_bug_bounty_report.md)). 

Every repair adheres strictly to the **V12 Permanent DNA**:
1. **Zero legacy lock(stateLock) blocks** are permitted. All state synchronization must be lock-free via atomic primitives (`Interlocked`, `ConcurrentDictionary`) or actor enqueueing.
2. **Build 981 Protocol compliance**: Bracket submissions and replacements must perform direct synchronous atomic writes to the `stopOrders` dictionary to prevent ghost orders.
3. **ASCII-Only compiler safety**: Emojis, curly quotes, and non-ASCII character sequences are banned from C# string literals.
4. **Post-Surgery Deployment**: Mandatory hard-link restoration via `deploy-sync.ps1` and build validation via `build_readiness.ps1`.

---

## 2. Surgical Repair Specifications

### 📦 Defect 1: BUG-S1-001 (H01) - Master Local RMA Entry Race
* **Location**: [V12_002.SIMA.Execution.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.SIMA.Execution.cs#L327-L383) (`SubmitLocalRMAEntry`)
* **Forensic Diagnosis**: `SymmetryGuardBeginDispatch` registers the transaction before local order submission occurs. If `SubmitOrderUnmanaged` throws a synchronous exception (e.g., account margin block, invalid tick size), the transaction context is never rolled back, leaving an orphaned in-flight dispatch context in `symmetryDispatchById`.
* **Broken vs. Fixed Pattern**:
  ```diff
  // --- BROKEN PATTERN ---
  string symmetryDispatchId = SymmetryGuardBeginDispatch("RMA", entryAction, contracts, price);
  SubmitLocalRMAEntry(baseSignal, entryAction, contracts, price, direction, prices, symmetryDispatchId);
  
  // --- FIXED PATTERN ---
  string symmetryDispatchId = SymmetryGuardBeginDispatch("RMA", entryAction, contracts, price);
  try
  {
      bool success = SubmitLocalRMAEntry(baseSignal, entryAction, contracts, price, direction, prices, symmetryDispatchId);
      if (!success)
      {
          SymmetryGuardRollbackDispatch(symmetryDispatchId);
      }
  }
  catch (Exception ex)
  {
      SymmetryGuardRollbackDispatch(symmetryDispatchId);
      Print(string.Format("[SIMA RMA V2] LOCAL ENTRY SYNCHRONOUS ERROR: {0}", ex.Message));
      throw;
  }
  ```
* **Rollback Helper Implementation** (to be added to [V12_002.Symmetry.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.Symmetry.cs)):
  ```csharp
  private void SymmetryGuardRollbackDispatch(string dispatchId)
  {
      if (string.IsNullOrEmpty(dispatchId)) return;
      symmetryDispatchById.TryRemove(dispatchId, out _);
      Print(string.Format("[SYMMETRY] Rolled back failed dispatch context: {0}", dispatchId));
  }
  ```

---

### 📦 Defect 2: BUG-S1-002 (H02) - Sideband Clear-After-Release Race
* **Locations** (BOTH must be fixed -- validated by live source scan):
  1. [V12_002.SIMA.Fleet.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.SIMA.Fleet.cs) — `ProcessValidPhotonSlot` (line 370-383)
  2. [V12_002.SIMA.Fleet.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.SIMA.Fleet.cs) — `DrainAllDispatchQueuesOnAbort` (line 295-302)
* **Forensic Diagnosis**: In both locations, `_photonPool.ReleaseByIndex(_sbIdx)` is called BEFORE `_photonSideband[_sbIdx] = default(FleetDispatchSideband)`. A parallel thread acquiring the pool slot can immediately read stale sideband data before the releasing thread zeroes it.
* **Broken vs. Fixed Pattern** (applies to BOTH sites):
  ```diff
  // --- BROKEN PATTERN ---
  _photonPool.ReleaseByIndex(_sbIdx);                          // Pool release FIRST
  _photonSideband[_sbIdx] = default(FleetDispatchSideband);   // Clear AFTER -- race window!
  
  // --- FIXED PATTERN ---
  _photonSideband[_sbIdx] = default(FleetDispatchSideband);   // Zero sideband FIRST
  Thread.MemoryBarrier();                                      // Enforce write ordering
  _photonPool.ReleaseByIndex(_sbIdx);                         // Pool release AFTER
  ```
* **CORRECTION NOTE**: The original bug bounty report only cited `ProcessValidPhotonSlot`. Live source scan confirmed the identical pattern in `DrainAllDispatchQueuesOnAbort`. BOTH sites are in scope.

---

### 📦 Defect 3: BUG-S1-003 (H03) - Abort Drain Leaves Registered State
* **Location** (CORRECTED): [V12_002.SIMA.Fleet.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.SIMA.Fleet.cs) — `DrainAllDispatchQueuesOnAbort` (line 282)
* **CORRECTION NOTE**: The bug bounty report incorrectly cited `V12_002.SIMA.Dispatch.cs`. Live source scan confirmed `DrainAllDispatchQueuesOnAbort` is in `SIMA.Fleet.cs` (9 methods in Dispatch.cs; this method is not among them).
* **Forensic Diagnosis**: During an abort sequence (SIMA disable, NOT full shutdown), `DrainAllDispatchQueuesOnAbort` drains the Photon ring and legacy ConcurrentQueue but does NOT call `UnsubscribeFromFleetAccounts`. By contrast, `ProcessShutdownSIMA` (the full teardown) does call `UnsubscribeFromFleetAccounts`. The abort path can therefore leave Account.OrderUpdate event handlers alive, triggering stale callbacks on drained-but-not-unsubscribed accounts.
* **CRITICAL FIX**: The original plan called `ClearAllEventHandlers()` -- **this method does not exist** in the codebase and would cause a compile error. The correct call is `UnsubscribeFromFleetAccounts()`, which already handles idempotent unsubscription (V12.1101E [A-4] guard present).
* **Broken vs. Fixed Pattern**:
  ```diff
  // --- BROKEN PATTERN (in DrainAllDispatchQueuesOnAbort, Fleet.cs line 282) ---
  private void DrainAllDispatchQueuesOnAbort()
  {
      FleetDispatchSlot abortSlot;
      while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out abortSlot))
      {
          // ... delta rollback and pool release ...
          Interlocked.Decrement(ref _pendingFleetDispatchCount);
      }
      // ... legacy queue drain ...
      // Missing: no event handler cleanup!
  }
  
  // --- FIXED PATTERN ---
  private void DrainAllDispatchQueuesOnAbort()
  {
      // ... existing ring and legacy queue drain logic (unchanged) ...
      // [BUILD 981 HARDENING]: Unsubscribe fleet account handlers on abort to prevent
      // stale callbacks on drained-but-live subscribers. UnsubscribeFromFleetAccounts
      // is idempotent (V12.1101E [A-4] guard) -- safe to call even if already unsubscribed.
      UnsubscribeFromFleetAccounts();
      Print("[SIMA] Dispatch queues drained and fleet account handlers unsubscribed on abort");
  }
  ```

---

### 🚫 Defect 4: BUG-S1-005 (H04) - SUSPENDED PENDING RE-INVESTIGATION
* **Original Claim**: `ProcessShutdownSIMA` directly decrements metrics on a `_globalState.ExpectedDelta` field without atomic primitives.
* **Live Source Finding** (2026-05-18 scan): `_globalState` and `_globalState.ExpectedDelta` do **not exist** in the codebase. `ProcessShutdownSIMA` calls `AddExpectedPositionDelta()` (Lifecycle.cs line 124), which is an alias for `AddExpectedPositionDeltaLocked` (V12_002.cs line 976), which uses `ConcurrentDictionary.AddOrUpdate` -- an inherently atomic operation requiring no further hardening.
* **Verdict**: H04 is a **suspected hallucination** from the Codex/Jules agents. The specified field names do not exist and the actual implementation is already lock-free and atomic.
* **Action**: H04 is **REMOVED from the implementation scope** until a fresh forensic scan identifies a real non-atomic decrement site. Do NOT implement the `Interlocked.Add` fix -- it would reference non-existent fields and cause a compile error.
* **Status**: ⏸️ SUSPENDED -- Do not implement. Escalate to Director if investigation uncovers a real vulnerability.

---

### 📦 Defect 5: BUG-S2-002 (H06) - Target-Replace Cancel Path Gated
* **Location**: [V12_002.Orders.Callbacks.AccountOrders.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.AccountOrders.cs#L365-L502) (`HandleMatchedFollowerOrder`)
* **Forensic Diagnosis**: Follower replacement state transitions are locked inside a complex entry-order conditional branch. If the master order is cancelled while the follower is in a non-standard entry state, the cancel event is ignored, leaving the follower order active forever.
* **Broken vs. Fixed Pattern**:
  ```diff
  // --- BROKEN PATTERN ---
  if (pos.EntryOrderType == OrderType.Limit && !pos.EntryFilled)
  {
      if (order.OrderState == OrderState.Cancelled)
      {
          // Follower cancel only handled here!
      }
  }
  
  // --- FIXED PATTERN ---
  // Top-level, state-agnostic handler processes cancellations regardless of entry type
  if (order.OrderState == OrderState.Cancelled || order.OrderState == OrderState.Rejected)
  {
      ProcessFollowerCancellationSafe(entryName, order, pos);
      return;
  }
  ```

---

### 📦 Defect 6: BUG-S2-004 (H07) - ConcurrentDictionary TOCTOU Race
* **Location**: [V12_002.Orders.Management.StopSync.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.StopSync.cs#L220-L245) (`UpdateStopQuantity`) and [V12_002.Orders.Management.Flatten.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Management.Flatten.cs#L355-L368) (`CancelUnfilledMasterEntries`)
* **Forensic Diagnosis**: Check-then-act pattern on `ConcurrentDictionary` (using `ContainsKey` followed by retrieving the value) creates a TOCTOU race. Under high multi-threaded stress, another thread can remove the key between the check and the act, leading to `KeyNotFoundException` or runtime crashes.
* **Broken vs. Fixed Pattern 1 (StopSync)**:
  ```diff
  // --- BROKEN PATTERN ---
  if (!stopOrders.ContainsKey(entryName)) return;
  Order currentStop = stopOrders[entryName];
  
  // --- FIXED PATTERN ---
  if (!stopOrders.TryGetValue(entryName, out var currentStop)) return;
  ```
* **Broken vs. Fixed Pattern 2 (Flatten)**:
  ```diff
  // --- BROKEN PATTERN ---
  if (entryOrders.ContainsKey(entryName))
  {
      Order entryOrder = entryOrders[entryName];
  
  // --- FIXED PATTERN ---
  if (entryOrders.TryGetValue(entryName, out var entryOrder))
  {
  ```

---

## 3. TDD Validation Suites & Test Specifications

We will create a new dedicated unit test suite file [tests/Epic1DeltaTests.cs](file:///C:/WSGTA/universal-or-strategy/tests/Epic1DeltaTests.cs) containing **5 high-value test cases** (H04 test removed -- H04 SUSPENDED):

1. `SubmitLocalRMAEntry_ThrowsException_ClearsInFlightRegistration` **(H01)**
   * **Setup**: Mock `SubmitOrderUnmanaged` to throw an exception.
   * **Action**: Invoke `SubmitLocalRMAEntry`.
   * **Assertion**: Verify `symmetryDispatchById` is completely rolled back (zero orphaned entries).
2. `Sideband_Release_ClearsBufferPriorToPoolReturn` **(H02)**
   * **Setup**: Multi-threaded consumer-producer setup on sideband slots.
   * **Action**: Release a slot from BOTH `ProcessValidPhotonSlot` and `DrainAllDispatchQueuesOnAbort` paths.
   * **Assertion**: Verify the reclaimed buffer is 100% zeroed before pool acquisition on a parallel thread.
3. `DrainQueuesOnAbort_UnsubscribesFleetAccounts` **(H03)**
   * **Setup**: Subscribe fleet account handlers, then trigger SIMA abort drain.
   * **Action**: Call `DrainAllDispatchQueuesOnAbort`.
   * **Assertion**: Verify `UnsubscribeFromFleetAccounts` was called (handlers removed; no stale delegate invocations).
   * **NOTE**: Test validates `UnsubscribeFromFleetAccounts()` is called -- NOT the removed `ClearAllEventHandlers()` which does not exist.
4. `HandleMatchedFollowerOrder_CancelReceivedInStaleState_CancelsFollower` **(H06)**
   * **Setup**: Put follower position in non-standard entry state (`EntryFilled = true`, master cancelled).
   * **Action**: Inject master cancellation order update.
   * **Assertion**: Verify follower position is cancelled via top-level `ProcessFollowerCancellationSafe` handler immediately.
5. `UpdateStopQuantity_ConcurrentDictionary_IsAtomic` **(H07)**
   * **Setup**: Spawn concurrent readers/writers mutating `stopOrders` and `entryOrders` while calling `UpdateStopQuantity` and `CancelUnfilledMasterEntries`.
   * **Action**: Rapidly add and remove keys during execution.
   * **Assertion**: Verify zero `KeyNotFoundException` -- confirms atomic `TryGetValue` pattern.

---

## 4. Architectural Verification Path

The implementation of Epic 1 Delta will follow this exact post-edit sequence:

1. **Surgical Implementation**: Apply changes to the 7 corrected target files in a single clean-room session (H04 REMOVED from scope).
2. **Post-Edit Grep Audit** (mandatory before handoff):
   ```powershell
   # Verify no compile-time ghost methods were introduced:
   grep -r "ClearAllEventHandlers" src/   # Must return 0 matches
   grep -r "_globalState" src/            # Must return 0 matches
   grep -r "_inFlightRmaEntries" src/     # Must return 0 matches
   ```
3. **Hard-Link Synchronization**:
   ```powershell
   powershell -File .\deploy-sync.ps1
   ```
   *Verify that the ASCII Gate and diff restrictions pass 100%.*
4. **Build Integrity Check**:
   ```powershell
   powershell -File .\scripts\build_readiness.ps1
   ```
   *Ensure 0 compilation errors.*
5. **Test Suite Execution**:
   Run the 5 unit test cases in `tests/Epic1DeltaTests.cs` to verify concurrency validation.

---

## 5. Next Agent Prompt: Epic 1 Delta Handoff

Copy and paste the following block to launch **Bob CLI** (`v12-engineer`) in its designated repair terminal to execute this corrected Delta Plan:

```markdown
/nexus:sync
/read-plan docs/brain/epic-1-dna/epic_1_delta_plan.md

Execute Stage 3: Surgical Repair for Epic 1 Delta (Corrections Applied 2026-05-18).
Scope: H01, H02, H03, H06, H07. H04 is SUSPENDED -- do NOT implement.

Target Files (corrected):
1. src/V12_002.SIMA.Execution.cs            (H01: try-catch + SymmetryGuardRollbackDispatch call)
2. src/V12_002.Symmetry.cs                  (H01: add SymmetryGuardRollbackDispatch helper)
3. src/V12_002.SIMA.Fleet.cs                (H02: fix ProcessValidPhotonSlot AND DrainAllDispatchQueuesOnAbort)
4. src/V12_002.SIMA.Fleet.cs                (H03: add UnsubscribeFromFleetAccounts to abort path)
5. src/V12_002.Orders.Callbacks.AccountOrders.cs  (H06: top-level ProcessFollowerCancellationSafe handler)
6. src/V12_002.Orders.Management.StopSync.cs       (H07: ContainsKey -> TryGetValue)
7. src/V12_002.Orders.Management.Flatten.cs        (H07: ContainsKey -> TryGetValue)

CRITICAL CONSTRAINTS:
- Do NOT reference ClearAllEventHandlers -- it does not exist (compile error).
- Do NOT reference _globalState or _globalState.ExpectedDelta -- they do not exist.
- Do NOT reference _inFlightRmaEntries -- the correct dictionary is symmetryDispatchById.
- H03 file is SIMA.Fleet.cs (NOT SIMA.Dispatch.cs).
- H02 requires fixing BOTH ProcessValidPhotonSlot AND DrainAllDispatchQueuesOnAbort.

Post-surgery instructions:
1. Run grep audit: grep -r "ClearAllEventHandlers" src/ -- must return 0 matches.
2. Write tests/Epic1DeltaTests.cs with the 5 specified TDD test cases (H04 test excluded).
3. Run powershell -File .\deploy-sync.ps1 to sync hard links and verify the ASCII gate.
4. Verify compilation via scripts/build_readiness.ps1 -- 0 errors required.
```

---
**Plan Prepared by**: Antigravity Orchestrator
**Corrections Applied by**: Antigravity live source validation (2026-05-18T14:17:00Z)
**Status**: **CORRECTIONS APPLIED -- AWAITING DIRECTOR KICK-OFF**
