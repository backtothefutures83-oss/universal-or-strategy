# Implementation Plan — PR #75 Post-Audit Repairs
# BUILD_TAG: 1111.004-v28.0-pr75-repairs
# Branch: build-983-phase4-dispatcher-final-v2
# Status: AWAITING DIRECTOR APPROVAL

---

## ULTRAPLAN VERDICT — 1111.004-v28.0-pr75-repairs
```
Step 1  ✅ — C-01 (DrainQueuesForShutdown dead-letter) — PARTIALLY CONFIRMED: _isTerminating
              flag is NOT checked inside DrainQueuesForShutdown itself. However, the Arena AI
              finding that "the flag is set TRUE before drain runs" needs verification — see §2.1.
              The actual bug confirmed: bare catch{} silently swallows cmd.Execute(this) failures.
              The 50-item hard cap has no overflow telemetry. Both are confirmed regressions.

Step 2  ✅ — C-02 (ExpKey domain mismatch) — CONFIRMED. HandleMatchedFollowerOrder correctly
              uses cancelledFollowerPos.ExecutingAccount.Name as cancelAcctKey, then calls
              ExpKey(cancelAcctKey). This IS the follower account key — correct. However, Arena
              AI's concern is partially valid: if ExecutingAccount is null, fallback is
              Account.Name (the MASTER account). This null-path uses the wrong domain.
              Confirmed: null-guard path emits master key, clearing a barrier the master owns,
              which unblocks master dispatches incorrectly.

Step 3  ✅ — C-03 (SemaphoreSlim unguarded disposal) — CONFIRMED. In V12_002.Lifecycle.cs:
              _simaToggleSem?.Dispose() sits AFTER SignalBroadcaster.ClearAllSubscribers()
              in the Terminated handler, with no try/catch. If ClearAllSubscribers() throws,
              the Dispose() never runs and the OS handle leaks. Requires try/finally.

Step 4  ✅ — C-04 (isFlattenRunning spin) — DISPROVED AS INFINITE LOOP. isFlattenRunning
              IS guarded in FlattenAll's finally block (line 343: isFlattenRunning = false).
              Arena AI finding overstated the risk. No infinite spin possible. However, the
              flag is set redundantly BEFORE and AFTER FlattenAllApexAccounts(), which could
              leave it asserted on an exception in FlattenAllApexAccounts(). The finally guard
              on the outer try saves this. FINDING: Low-priority code smell, not a P0 bug.

Step 5  ✅ — C-05 (50-item drain cap silent overflow) — CONFIRMED. DrainQueuesForShutdown
              drains max 50 actor commands with no log on overflow. If >50 commands are
              queued at shutdown, extras are silently discarded.

Step 6  ✅ — W-01 (Culture parse) — DISPROVED AS NEW BUG. StickyState.cs already uses
              CultureInfo.InvariantCulture on all double.TryParse calls (lines 343, 347, etc.).
              PR #75 already applied this fix. FINDING: Already fixed in this PR — no action needed.

Step 7  ✅ — W-02 (Disposal order) — CONFIRMED. _simaToggleSem.Dispose() runs after
              ClearAllSubscribers() in Terminated. If ClearAllSubscribers() throws, semaphore
              leaks. This is the same root cause as C-03 — one fix resolves both.

Step 8  ✅ — W-04 (Silent catch on reconnect) — CONFIRMED. ProcessOnConnectionStatusUpdate
              has: try { Enqueue(ctx => ctx.HydrateWorkingOrdersFromBroker()); } catch { }
              Bare catch with no log. If Enqueue fails during reconnect, the re-adoption
              silently aborts. This is a mission-critical silent failure.

DEFECT AUDIT:
  D1 (Dead-letter drain) — Partially confirmed. Bare catch + silent overflow cap: ✅ CONFIRMED
  D2 (ExpKey null-path master bleed) — ✅ CONFIRMED
  D3 (Semaphore OS handle leak) — ✅ CONFIRMED
  D4 (isFlattenRunning infinite spin) — ❌ DISPROVED (overblown by Arena AI)
  D5 (Culture parse) — ❌ DISPROVED (already fixed in PR #75)
  D6 (Reconnect silent catch) — ✅ CONFIRMED

SIGN-OFF: CONDITIONAL PASS
  P0 fixes required: D1, D2, D3
  P1 fixes required: D6
  P2 backlog: Documentation Theatre (graphify CI gate), Telemetry fields, ProcessOnStateChange default case
```

---

## 1. Mission Scope

Repair **5 confirmed defects** in PR #75 source, validated against live `src/` index. No logic mutations beyond the stated repairs.

**Files affected:**
| File | Defects | Priority |
|------|---------|----------|
| `src/V12_002.Lifecycle.cs` | D1, D3 | P0 |
| `src/V12_002.Orders.Callbacks.AccountOrders.cs` | D2 | P0 |
| `src/V12_002.Lifecycle.cs` (reconnect) | D6 | P1 |

---

## 2. Evidence Verification (Independent — not echoed from Forensics)

### 2.1 D1 — DrainQueuesForShutdown: Bare catch + silent overflow (Lifecycle.cs:504-527)

**Confirmed code (live):**
```csharp
// Line 519-521: Bare catch silently eats all cmd.Execute failures
try { cmd.Execute(this); } catch { }
// Line 518: Hard cap 50 — no telemetry on overflow
while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
```

**Root Cause**: The bare `catch {}` means if any command throws during shutdown drain (e.g., order already cancelled), the failure is lost. The 50-item cap has no overflow counter, so if the queue had 200 items at shutdown, 150 are silently discarded with no trace.

**Note on C-01 Arena finding**: The `_isTerminating` flag check claim was **not verified** in the live code. `DrainQueuesForShutdown` does NOT check `_isTerminating` internally, but the flag is only checked by the actor loop — not this method. The drain runs unconditionally. The Arena AI conflated the actor loop guard with the drain method. **C-01 as stated is partially incorrect**, but D1 (bare catch + overflow cap) is the real confirmed defect.

### 2.2 D2 — HandleMatchedFollowerOrder: ExpKey null-path master bleed (AccountOrders.cs:~420)

**Confirmed code (live):**
```csharp
string cancelAcctKey = cancelledFollowerPos.ExecutingAccount != null
    ? cancelledFollowerPos.ExecutingAccount.Name : Account.Name;  // <- null fallback = MASTER account
int cancelDelta = ...
DeltaExpectedPositionLocked(ExpKey(cancelAcctKey), cancelDelta);
_dispatchSyncPendingExpKeys.TryRemove(ExpKey(cancelAcctKey), out _); // [B967-FIX-02]
```

**Root Cause**: `Account.Name` is the **master** strategy account. If `ExecutingAccount` is null (possible during order teardown), the dispatch-sync barrier for the **master** account is cleared. This unblocks the master account dispatcher incorrectly, potentially allowing duplicate dispatches while follower state is still dirty.

### 2.3 D3 — SemaphoreSlim unguarded disposal (Lifecycle.cs:~479)

**Confirmed code (live):**
```csharp
// V12.Phase7 [GAP-4]: Dispose SIMA toggle semaphore to release OS handle.
_simaToggleSem?.Dispose();  // No try/finally wrapping this
```
Located AFTER `SignalBroadcaster.ClearAllSubscribers()`. If `ClearAllSubscribers()` throws, the semaphore handle leaks.

### 2.4 D6 — Silent Reconnect Failure (Lifecycle.cs:ProcessOnConnectionStatusUpdate)

**Confirmed code (live):**
```csharp
try { Enqueue(ctx => ctx.HydrateWorkingOrdersFromBroker()); } catch { }
```
No log on catch. Silent re-adoption failure means orders are never re-adopted after reconnect, causing desync. Confirmed bare catch with no telemetry.

---

## 3. Repairs — Complete Code Blocks

### REPAIR-01 — DrainQueuesForShutdown: Log bare catch + add overflow telemetry
**File**: `src/V12_002.Lifecycle.cs` — Line 504

```csharp
// BEFORE (lines 504-527):
private void DrainQueuesForShutdown()
{
    try
    {
        Print("[SHUTDOWN] Draining queues...");
        int ipcDrained = 0;
        if (ipcCommandQueue != null)
        {
            while (ipcDrained < 100 && ipcCommandQueue.TryDequeue(out string _))
            {
                ipcDrained++;
            }
        }

        int actorDrained = 0;
        while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
        {
            try { cmd.Execute(this); } catch { }
            actorDrained++;
        }
        Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds and {1} Actor cmds.", ipcDrained, actorDrained));
    }
    catch { }
}

// AFTER:
private void DrainQueuesForShutdown()
{
    try
    {
        Print("[SHUTDOWN] Draining queues...");
        int ipcDrained = 0;
        if (ipcCommandQueue != null)
        {
            while (ipcDrained < 100 && ipcCommandQueue.TryDequeue(out string _))
            {
                ipcDrained++;
            }
        }

        int actorDrained = 0;
        int actorOverflow = 0;
        while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
        {
            try { cmd.Execute(this); }
            catch (Exception exCmd)
            {
                Print("[SHUTDOWN] Actor cmd failed during drain: " + exCmd.Message);
            }
            actorDrained++;
        }
        // Count overflow items without executing -- telemetry only.
        StrategyCommand overflowCmd;
        while (_cmdQueue.TryDequeue(out overflowCmd))
            actorOverflow++;

        Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds, {1} Actor cmds. Overflow discarded: {2}.",
            ipcDrained, actorDrained, actorOverflow));
    }
    catch (Exception exOuter)
    {
        Print("[SHUTDOWN] DrainQueuesForShutdown outer exception: " + exOuter.Message);
    }
}
```

---

### REPAIR-02 — HandleMatchedFollowerOrder: Guard null ExecutingAccount before ExpKey
**File**: `src/V12_002.Orders.Callbacks.AccountOrders.cs` — Entry cancel path (~line 420)

```csharp
// BEFORE:
PositionInfo cancelledFollowerPos;
if (activePositions.TryGetValue(matchedEntry, out cancelledFollowerPos) && cancelledFollowerPos != null)
{
    string cancelAcctKey = cancelledFollowerPos.ExecutingAccount != null
        ? cancelledFollowerPos.ExecutingAccount.Name : Account.Name;
    int cancelDelta = (cancelledFollowerPos.Direction == MarketPosition.Long)
        ? -cancelledFollowerPos.TotalContracts : cancelledFollowerPos.TotalContracts;
    DeltaExpectedPositionLocked(ExpKey(cancelAcctKey), cancelDelta);
    // B957/D2: Release the SIMA dispatch-sync barrier for this account.
    _dispatchSyncPendingExpKeys.TryRemove(ExpKey(cancelAcctKey), out _); // [B967-FIX-02]
}

// AFTER:
PositionInfo cancelledFollowerPos;
if (activePositions.TryGetValue(matchedEntry, out cancelledFollowerPos) && cancelledFollowerPos != null)
{
    // [B983-FIX-D2]: Guard null ExecutingAccount. Fallback to Account.Name (master) is
    // a domain mismatch -- would clear the master dispatch barrier instead of the follower's.
    // Skip ExpKey operations entirely if the follower account cannot be determined.
    if (cancelledFollowerPos.ExecutingAccount == null)
    {
        Print("[B983-D2] HandleMatchedFollowerOrder: ExecutingAccount null for " + matchedEntry
            + " -- skipping ExpKey delta and sync barrier ops to avoid master domain bleed.");
    }
    else
    {
        string cancelAcctKey = cancelledFollowerPos.ExecutingAccount.Name;
        int cancelDelta = (cancelledFollowerPos.Direction == MarketPosition.Long)
            ? -cancelledFollowerPos.TotalContracts : cancelledFollowerPos.TotalContracts;
        DeltaExpectedPositionLocked(ExpKey(cancelAcctKey), cancelDelta);
        // B957/D2: Release the SIMA dispatch-sync barrier for this follower account only.
        _dispatchSyncPendingExpKeys.TryRemove(ExpKey(cancelAcctKey), out _); // [B967-FIX-02]
    }
}
```

---

### REPAIR-03 — Lifecycle Terminated: Wrap SemaphoreSlim disposal in try/finally
**File**: `src/V12_002.Lifecycle.cs` — Terminated handler (~line 475)

```csharp
// BEFORE:
// V12.Phase7 [C-08]: Clear ALL static SignalBroadcaster event handlers on termination.
SignalBroadcaster.ClearAllSubscribers();

// V12.Phase7 [GAP-4]: Dispose SIMA toggle semaphore to release OS handle.
_simaToggleSem?.Dispose();

// Clear references
activePositions?.Clear();

// AFTER:
// V12.Phase7 [C-08]: Clear ALL static SignalBroadcaster event handlers on termination.
// Static events survive instance disposal. Wrapped in try/finally to guarantee semaphore
// disposal even if ClearAllSubscribers throws. [B983-FIX-D3]
try
{
    SignalBroadcaster.ClearAllSubscribers();
}
finally
{
    // V12.Phase7 [GAP-4]: Dispose SIMA toggle semaphore to release OS handle.
    // In finally block: guaranteed to run even if ClearAllSubscribers throws.
    try { _simaToggleSem?.Dispose(); }
    catch (Exception exSem) { Print("[SHUTDOWN] SemaphoreSlim dispose failed: " + exSem.Message); }
}

// Clear references
activePositions?.Clear();
```

---

### REPAIR-04 — ProcessOnConnectionStatusUpdate: Log reconnect Enqueue failure
**File**: `src/V12_002.Lifecycle.cs` — ProcessOnConnectionStatusUpdate (~line 556)

```csharp
// BEFORE:
else if (status == ConnectionStatus.Connected)
{
    Print("[BUILD 948] Reconnected -- scheduling working order re-adoption.");
    try { Enqueue(ctx => ctx.HydrateWorkingOrdersFromBroker()); } catch { }
}

// AFTER:
else if (status == ConnectionStatus.Connected)
{
    Print("[BUILD 948] Reconnected -- scheduling working order re-adoption.");
    try { Enqueue(ctx => ctx.HydrateWorkingOrdersFromBroker()); }
    catch (Exception exReconnect)
    {
        // [B983-FIX-D6]: Silent bare catch promoted to logged failure.
        // Re-adoption failure means orders will not be re-adopted after reconnect.
        // Director must be alerted -- this is a mission-critical path.
        Print("[B983-D6] CRITICAL: Reconnect re-adoption Enqueue failed: " + exReconnect.Message
            + " -- orders may not be re-adopted. Manual intervention required.");
    }
}
```

---

## 4. P2 Backlog (No code changes this PR — track in docs only)

| ID | Item | Action |
|----|------|--------|
| Q-01 | Doc coverage 9% vs 80% required | Track as M3 backlog item |
| Q-04 | ProcessOnStateChange missing default state handler | Add `else { Print("[STATE-WARN] Unhandled state: " + state); }` in M3 cleanup PR |
| Q-06 | graphify CI enforcement | Add CI step to check `graphify-out/` staleness in M3 infra sprint |

---

## 5. DNA Compliance Checklist

- [x] No `lock(stateLock)` introduced
- [x] No Unicode/emoji in C# string literals (Print statements use ASCII only)
- [x] No new allocations on hot path (drain telemetry runs at shutdown only)
- [x] Semaphore lifecycle: Dispose in `finally` — compliant with V12 semaphore protocol
- [x] All new catch blocks log — no silent swallows
- [x] No Enqueue used for stopOrders path — direct write rule maintained (Build 981 protocol)

---

## 6. Audit Gates (ENGINEER must run before handoff)

```powershell
# 1. Lock audit -- must return zero hits
grep -rn "lock\s*(\s*stateLock\s*)" src/

# 2. ASCII gate
python scripts/check_ascii.py

# 3. Deploy sync (after edits)
powershell -File .\deploy-sync.ps1
```

---

## 7. Director's Handoff Block

```
P3 ARCHITECT SIGN-OFF — BUILD_TAG: 1111.004-v28.0-pr75-repairs
================================================================
Status: PLAN COMPLETE — AWAITING DIRECTOR APPROVAL

Confirmed defects: 4 (D1, D2, D3, D6)
Disproved defects: 2 (D4 isFlattenRunning spin, D5 culture parse — already fixed)

Files to edit:
  - src/V12_002.Lifecycle.cs        (REPAIR-01, REPAIR-03, REPAIR-04)
  - src/V12_002.Orders.Callbacks.AccountOrders.cs  (REPAIR-02)

ENGINEER (Codex) Instructions:
  1. Apply REPAIR-01 to DrainQueuesForShutdown (Lifecycle.cs:504-527)
  2. Apply REPAIR-02 to HandleMatchedFollowerOrder cancel path (AccountOrders.cs ~line 420)
  3. Apply REPAIR-03 — wrap ClearAllSubscribers in try/finally for semaphore (Lifecycle.cs ~line 475)
  4. Apply REPAIR-04 — log reconnect Enqueue failure (Lifecycle.cs ~line 556)
  5. Run audit gates (Section 6)
  6. Run: powershell -File .\deploy-sync.ps1
  7. Press F5 in NinjaTrader. Verify BUILD_TAG banner shows 1111.004-v28.0-pr75-repairs.

BANNED:
  - Do NOT modify any other logic outside the 4 targeted blocks
  - Do NOT use lock(stateLock)
  - Do NOT use Unicode in string literals
  - Do NOT reorder the Terminated handler cleanup sequence beyond the try/finally wrapper
```

Plan saved to `docs/brain/implementation_plan.md`. Awaiting Director approval.
