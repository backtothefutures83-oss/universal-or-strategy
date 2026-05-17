# Ticket 04: Circuit Breaker - Extraction Plan

**Status**: READY FOR DIRECTOR APPROVAL  
**Agent**: Bob CLI (v12-engineer)  
**Date**: 2026-05-16

---

## Forensic Analysis Summary

### Current State
1. **V12_002.cs (line 695)**: `ZeroAllocOrderIdMap` class exists - insertion point identified
2. **V12_002.SIMA.Fleet.cs (line 152)**: `SubmitAndRegisterFleetOrders` method exists with Ticket-02 pre/post-submit registration
3. **V12_002.Lifecycle.cs (line 203)**: `OnStateChangeSetDefaults` method exists - initialization point identified
4. **Circuit Breaker**: NOT YET IMPLEMENTED (search returned 0 results)

### Key Findings
- **Insertion Point A**: After `ZeroAllocOrderIdMap` class (line 836 in V12_002.cs)
- **Insertion Point B**: In `SubmitAndRegisterFleetOrders` - wrap existing `acct.Submit(submitOrders)` at line 177
- **Insertion Point C**: In `OnStateChangeSetDefaults` - add field initialization after line 327 (after `CpuAffinityMask = 0;`)
- **Field Declaration**: Add near other infrastructure fields in V12_002.cs

---

## Surgical Changes Required

### Change 1: Add SubmitCircuitBreaker Class
**File**: `src/V12_002.cs`  
**Location**: After line 836 (after `ZeroAllocOrderIdMap` closing brace)  
**Action**: Insert new sealed class with lock-free FSM

**Verification**:
- Zero `lock()` statements
- Uses `Interlocked.CompareExchange` for state transitions
- Uses `Volatile.Read/Write` for timestamp
- ASCII-only string literals in `GetDiagnostics()`

### Change 2: Add Circuit Breaker Field
**File**: `src/V12_002.cs`  
**Location**: Search for field declaration section (near other private fields)  
**Action**: Add `private SubmitCircuitBreaker _submitCircuitBreaker;`

### Change 3: Initialize Circuit Breaker
**File**: `src/V12_002.Lifecycle.cs`  
**Location**: In `OnStateChangeSetDefaults()` after line 327  
**Action**: Add `_submitCircuitBreaker = new SubmitCircuitBreaker();`

### Change 4: Integrate Circuit Breaker in Submit Path
**File**: `src/V12_002.SIMA.Fleet.cs`  
**Location**: `SubmitAndRegisterFleetOrders` method (lines 152-200)  
**Action**: 
1. Add circuit breaker check at method start (before line 155)
2. Wrap `acct.Submit(submitOrders)` (line 177) in try/catch
3. Add `RecordSuccess()` after submit
4. Add `RecordFailure()` in catch block

**Critical Constraint**: Must preserve existing Ticket-02 pre/post-submit registration logic (lines 162-185)

---

## V12 DNA Compliance Checklist

### Zero-Lock Compliance
- [ ] No `lock()` statements in `SubmitCircuitBreaker`
- [ ] Uses `Interlocked.CompareExchange` for all state transitions
- [ ] Uses `Volatile.Read/Write` for `_openUntilTicks`

### Zero-Allocation Compliance
- [ ] Circuit breaker state is single `long` field (packed state + failure count)
- [ ] No heap allocations in `AllowSubmit()`/`RecordSuccess()`/`RecordFailure()`
- [ ] No `new` keyword in hot path methods

### ASCII-Only Compliance
- [ ] All string literals use ASCII characters only
- [ ] No Unicode, emoji, or curly quotes in `GetDiagnostics()`

---

## Implementation Sequence

1. **Add SubmitCircuitBreaker class** to V12_002.cs (after ZeroAllocOrderIdMap)
2. **Add field declaration** to V12_002.cs (near other infrastructure fields)
3. **Initialize in OnStateChangeSetDefaults** in V12_002.Lifecycle.cs
4. **Integrate in SubmitAndRegisterFleetOrders** in V12_002.SIMA.Fleet.cs

---

## Risk Assessment

**Risk Level**: MEDIUM

**Mitigations**:
- Circuit breaker is fail-open (allows submits on error)
- Preserves existing Ticket-02 registration logic
- No changes to FSM state machine
- Isolated protection layer (no dependencies)

---

## Post-Edit Verification Commands

```powershell
# 1. Sync hard links
powershell -File .\deploy-sync.ps1

# 2. Verify zero locks
grep -r "lock(" src/

# 3. Verify ASCII-only
grep -Prn "[^\x00-\x7F]" src/

# 4. Complexity audit
python scripts/complexity_audit.py
```

---

## Acceptance Criteria

### Functional
- [ ] Circuit breaker blocks submits after 5 consecutive failures
- [ ] Circuit breaker transitions to HalfOpen after 30-second cooldown
- [ ] Single successful probe in HalfOpen resets to Closed
- [ ] Single failed probe in HalfOpen returns to Open
- [ ] `GetDiagnostics()` returns current state and failure count

### Compilation
- [ ] Code compiles without errors in NinjaTrader IDE (F5)
- [ ] BUILD_TAG banner displays correctly

### DNA Compliance
- [ ] `deploy-sync.ps1` passes
- [ ] `grep -r "lock(" src/` returns ZERO matches
- [ ] `grep -Prn "[^\x00-\x7F]" src/` returns ZERO matches

---

## DIRECTOR APPROVAL REQUIRED

This extraction plan is READY for review. Awaiting Director approval to proceed with surgical changes.