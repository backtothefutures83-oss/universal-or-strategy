---
# TICKET epic-1-dna-01: Stop Order Synchronous Registration
# Epic: epic-1-dna
# Sequence: 1 of 3
# Depends on: NONE
---

## Objective
Convert stop order registration from asynchronous `Enqueue` to synchronous atomic writes to eliminate ghost-order tracking windows during bracket submission and flatten operations (Build 981 compliance).

## Scope
IN scope:
- `src/V12_002.Orders.Management.StopSync.cs` - Line 320 in `CreateNewStopOrder()`
- `src/V12_002.Trailing.StopUpdate.cs` - Lines 264, 276 in `CreateDirectStopOrder()`

OUT of scope:
- Other `Enqueue` usages for non-stop-order state
- REAPER audit logic modifications
- Test file creation (handled in Ticket 03)

## Context References
- Analysis: docs/brain/epic-1-dna/01-analysis.md -- Section 1 (H05, H08 dependency maps)
- Approach: docs/brain/epic-1-dna/02-approach.md -- Section 1 (Pattern A), Section 2 (H05, H08 implementation)

## Implementation Instructions

### H05: CreateNewStopOrder() (Line 320)

**File:** `src/V12_002.Orders.Management.StopSync.cs`

**Current Code (line 320):**
```csharp
Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; });
```

**Replace with:**
```csharp
// [BUILD 981 EXEMPTION]: Synchronous write to stopOrders during bracket submission.
// Prevents ghost-order tracking window if flatten occurs before actor drain.
// ConcurrentDictionary single-write is thread-safe (no lock required).
stopOrders[_en966] = _ns966;
```

### H08: CreateDirectStopOrder() (Lines 264, 276)

**File:** `src/V12_002.Trailing.StopUpdate.cs`

**Current Code (line 264):**
```csharp
Enqueue(ctx => { ctx._followerTargetReplaceSpecs[...] = ...; });
```

**Replace with:**
```csharp
// [BUILD 981 EXEMPTION]: Synchronous write to _followerTargetReplaceSpecs during stop replacement.
// Prevents ghost-order tracking window if flatten occurs before actor drain.
// ConcurrentDictionary single-write is thread-safe (no lock required).
_followerTargetReplaceSpecs[...] = ...;
```

**Current Code (line 276):**
```csharp
Enqueue(ctx => { ctx.stopOrders[...] = ...; });
```

**Replace with:**
```csharp
// [BUILD 981 EXEMPTION]: Synchronous write to stopOrders during stop replacement.
// Prevents ghost-order tracking window if flatten occurs before actor drain.
// ConcurrentDictionary single-write is thread-safe (no lock required).
stopOrders[...] = ...;
```

## V12 DNA Guardrails
- [x] Zero new lock() statements
- [x] Zero non-ASCII characters in string literals
- [x] All changes are single-line replacements (no extraction required)
- [x] No logic drift -- pure structural movement from async to sync
- [x] Inline comments document Build 981 exemption at all sync write sites

## Post-Edit Verification (Mandatory)
```powershell
# 1. Re-establish hard links (MANDATORY after every src/ edit)
powershell -File .\deploy-sync.ps1

# 2. Lock regression (must return ZERO)
grep -r "lock(" src/V12_002.Orders.Management.StopSync.cs
grep -r "lock(" src/V12_002.Trailing.StopUpdate.cs

# 3. ASCII gate (must return ZERO)
grep -Prn "[^\x00-\x7F]" src/V12_002.Orders.Management.StopSync.cs
grep -Prn "[^\x00-\x7F]" src/V12_002.Trailing.StopUpdate.cs

# 4. Verify Enqueue removal
grep -n "Enqueue.*stopOrders" src/V12_002.Orders.Management.StopSync.cs
grep -n "Enqueue.*stopOrders" src/V12_002.Trailing.StopUpdate.cs
# Expected: ZERO matches

# 5. Verify Build 981 exemption comments
grep -n "BUILD 981 EXEMPTION" src/V12_002.Orders.Management.StopSync.cs
grep -n "BUILD 981 EXEMPTION" src/V12_002.Trailing.StopUpdate.cs
# Expected: 3 matches total (1 in StopSync, 2 in Trailing.StopUpdate)
```

## Acceptance Criteria
- [x] Line 320 in `CreateNewStopOrder()` uses synchronous write (no `Enqueue`)
- [x] Lines 264, 276 in `CreateDirectStopOrder()` use synchronous writes (no `Enqueue`)
- [x] All 3 sync write sites have Build 981 exemption comment block
- [x] deploy-sync.ps1 ASCII gate: PASS
- [x] lock() audit: ZERO matches in modified files
- [x] Enqueue audit: ZERO matches for stopOrders in modified files
- [x] Director presses F5 in NinjaTrader -- BUILD_TAG banner visible