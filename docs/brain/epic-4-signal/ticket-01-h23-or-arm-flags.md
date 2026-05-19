---
# TICKET-01: H23 OR Arm Flag Atomic Operations
# Epic: epic-4-signal
# Defect: BUG-S6-003 - Race Condition on Shared Sync Flags
# Priority: HIGH
# Estimated Time: 2 hours
---

## 1. Ticket Summary

Replace check-then-act boolean pattern in OR arm flags with atomic integer operations to enforce mutual exclusion and eliminate dual-direction arming race condition.

**Defect:** H23 - BUG-S6-003  
**Severity:** High  
**Root Cause:** Boolean flags (`isLongArmed`, `isShortArmed`) accessed by multiple market data threads without atomic primitives  
**Impact:** Dual-direction entry signals can trigger conflicting orders under high market volatility

---

## 2. Technical Specification

### 2.1 Target State

**Design:** Replace two boolean flags with single atomic integer:
- `0` = disarmed
- `1` = long armed
- `2` = short armed

**Atomic Operations:**
- `Interlocked.CompareExchange(ref _armState, newValue, comparand)` - atomic check-and-set
- `Interlocked.Exchange(ref _armState, value)` - atomic write

### 2.2 Files to Modify

1. `src/V12_002.cs` - Field declarations
2. `src/V12_002.Entries.OR.cs` - ExecuteLong/ExecuteShort logic
3. `src/V12_002.UI.IPC.Commands.Fleet.cs` - ToS handshake + fleet dispatch
4. `src/V12_002.Orders.Management.Flatten.cs` - Reset on flatten

**Total Lines Changed:** ~30 across 5 files  
**CYC Delta:** 0 (pure primitive swap, no logic change)

---

## 3. Implementation Steps

### Step 1: Update Field Declarations (V12_002.cs)

**File:** `src/V12_002.cs`

**FIND (lines 256-258):**
```csharp
private volatile bool isTosSyncMode = false;
private bool isLongArmed = false;
private bool isShortArmed = false;
private DateTime lastArmedTime = DateTime.MinValue;
```

**REPLACE:**
```csharp
private volatile bool isTosSyncMode = false;
// H23: Atomic arm state (0=disarmed, 1=long, 2=short)
private int _armState = 0;
private DateTime lastArmedTime = DateTime.MinValue;
```

### Step 2: Update ExecuteLong (Entries.OR.cs)

**File:** `src/V12_002.Entries.OR.cs`

**FIND (lines 50-64):**
```csharp
if (isLongArmed)
{
    // DOUBLE-CLICK BYPASS: If already armed, fire immediately
    Print("[SYNC] Double-Click Bypass Triggered -> Executing LONG immediately (No ToS Handshake)");
    isLongArmed = false;
    // Proceed to entry logic below
}
else
{
    isLongArmed = true;
    isShortArmed = false; // Mutually exclusive for simplicity
    lastArmedTime = DateTime.Now;
    Print("[SYNC] LONG ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
```

**REPLACE:**
```csharp
// H23: Atomic arm state check (1=long armed)
int currentState = Interlocked.CompareExchange(ref _armState, 0, 1);
if (currentState == 1)
{
    // DOUBLE-CLICK BYPASS: Already armed, fire immediately and disarm
    Print("[SYNC] Double-Click Bypass Triggered -> Executing LONG immediately (No ToS Handshake)");
    // Proceed to entry logic below
}
else if (currentState == 0)
{
    // Disarmed -> Arm LONG (0 -> 1)
    Interlocked.Exchange(ref _armState, 1);
    lastArmedTime = DateTime.Now;
    Print("[SYNC] LONG ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
else
{
    // currentState == 2 (SHORT armed) -> reject LONG arm request
    Print("[SYNC] LONG ARM REJECTED: SHORT already armed (mutual exclusion)");
    return;
}
```

### Step 3: Update ExecuteShort (Entries.OR.cs)

**File:** `src/V12_002.Entries.OR.cs`

**FIND (lines 93-107):**
```csharp
if (isShortArmed)
{
    // DOUBLE-CLICK BYPASS: If already armed, fire immediately
    Print("[SYNC] Double-Click Bypass Triggered -> Executing SHORT immediately (No ToS Handshake)");
    isShortArmed = false;
    // Proceed to entry logic below
}
else
{
    isShortArmed = true;
    isLongArmed = false; // Mutually exclusive
    lastArmedTime = DateTime.Now;
    Print("[SYNC] SHORT ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
```

**REPLACE:**
```csharp
// H23: Atomic arm state check (2=short armed)
int currentState = Interlocked.CompareExchange(ref _armState, 0, 2);
if (currentState == 2)
{
    // DOUBLE-CLICK BYPASS: Already armed, fire immediately and disarm
    Print("[SYNC] Double-Click Bypass Triggered -> Executing SHORT immediately (No ToS Handshake)");
    // Proceed to entry logic below
}
else if (currentState == 0)
{
    // Disarmed -> Arm SHORT (0 -> 2)
    Interlocked.Exchange(ref _armState, 2);
    lastArmedTime = DateTime.Now;
    Print("[SYNC] SHORT ENTRY ARMED. Waiting for ToS handshake signal...");
    return;
}
else
{
    // currentState == 1 (LONG armed) -> reject SHORT arm request
    Print("[SYNC] SHORT ARM REJECTED: LONG already armed (mutual exclusion)");
    return;
}
```

### Step 4: Update ToS Handshake (UI.IPC.Commands.Fleet.cs)

**File:** `src/V12_002.UI.IPC.Commands.Fleet.cs`

**FIND (line 374):**
```csharp
bool armed = (action == "LONG") ? isLongArmed : isShortArmed;
```

**REPLACE:**
```csharp
// H23: Atomic arm state check
int currentState = Interlocked.CompareExchange(ref _armState, 0, 0);
bool armed = (action == "LONG" && currentState == 1) || (action == "SHORT" && currentState == 2);
```

**FIND (line 383):**
```csharp
if (action == "LONG") isLongArmed = false; else isShortArmed = false;
```

**REPLACE:**
```csharp
// H23: Atomic disarm
Interlocked.Exchange(ref _armState, 0);
```

### Step 5: Update Fleet Dispatch (UI.IPC.Commands.Fleet.cs)

**File:** `src/V12_002.UI.IPC.Commands.Fleet.cs`

**FIND (lines 445-450):**
```csharp
if (isLongArmed)
{
    Print("[SYNC] LONG ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteLong(orContracts));
    isLongArmed = false;
}
```

**REPLACE:**
```csharp
// H23: Atomic check and disarm
if (Interlocked.CompareExchange(ref _armState, 0, 1) == 1)
{
    Print("[SYNC] LONG ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteLong(orContracts));
}
```

**FIND (lines 472-477):**
```csharp
if (isShortArmed)
{
    Print("[SYNC] SHORT ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteShort(orContracts));
    isShortArmed = false;
}
```

**REPLACE:**
```csharp
// H23: Atomic check and disarm
if (Interlocked.CompareExchange(ref _armState, 0, 2) == 2)
{
    Print("[SYNC] SHORT ARMED -> Executing Fleet Entry");
    Enqueue(ctx => ctx.ExecuteShort(orContracts));
}
```

### Step 6: Update Flatten Reset (Orders.Management.Flatten.cs)

**File:** `src/V12_002.Orders.Management.Flatten.cs`

**FIND (lines 247-248):**
```csharp
isLongArmed = false;
isShortArmed = false;
```

**REPLACE:**
```csharp
// H23: Atomic disarm on flatten
Interlocked.Exchange(ref _armState, 0);
```

---

## 4. Self-Audit Checklist (Step 5)

After completing Steps 1-4, run these audits BEFORE emitting [SELF-AUDIT-DONE]:

### 4.1 DNA Compliance

```powershell
# Hard-link sync + ASCII gate
powershell -File .\deploy-sync.ps1
# Expected: EXIT 0, ASCII gate PASS
```

### 4.2 Lock Regression

```bash
grep -r "lock(" src/
# Expected: ZERO matches
```

### 4.3 Unicode Regression

```bash
grep -Prn "[^\x00-\x7F]" src/
# Expected: ZERO matches
```

### 4.4 Ghost Method Audit (Concurrency Epic)

```bash
grep -r "ClearAllEventHandlers" src/
grep -r "_globalState" src/
grep -r "_inFlightRmaEntries" src/
# Expected: ALL return ZERO matches
```

### 4.5 Compilation Check

- Verify no compiler errors
- Verify no new warnings introduced

**If ALL audits PASS:** Emit `[SELF-AUDIT-DONE] Ticket 01 -- self-audit PASS. Awaiting independent verification.`

**If ANY audit FAILS:** Fix the issue, re-run the failing audit, and only emit [SELF-AUDIT-DONE] once all audits are clean.

---

## 5. Verification Criteria (Independent - Step C)

**Unit Test:** `ORFlags_ConcurrentArming_AllowsOnlyOneDirection`
- Spawn 100 threads: 50 calling `ExecuteLong()`, 50 calling `ExecuteShort()`
- Assert: `_armState` never equals both 1 and 2 simultaneously
- Assert: All arm requests either succeed or are rejected (no silent failures)

**Stress Test:** `ORFlags_HighFrequencyToggle_MaintainsMutualExclusion`
- 1000 rapid arm/disarm cycles
- Verify mutual exclusion invariant holds throughout

**Manual Test:** Replay historical OR sessions, verify zero dual-arming events

---

## 6. Success Criteria

- [x] All 5 files modified with atomic operations
- [x] Zero new `lock()` statements
- [x] ASCII-only compliance maintained
- [x] `deploy-sync.ps1` passes
- [x] All ghost-method audits clean
- [x] Unit tests pass
- [x] F5 compile gate passes

---

## 7. Rollback Plan

**If atomic operations fail:**
1. Revert all changes via git
2. Restore boolean flags
3. Add explicit mutex (DNA waiver required)
4. Escalate to P3 Architect

---

**Ticket Status:** READY FOR EXECUTION  
**Dependencies:** None (independent ticket)  
**Estimated CYC Impact:** 0 (pure primitive swap)  
**Risk Level:** LOW