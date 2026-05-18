---
# TICKET epic-1-dna-02: Retest Entry Rollback Race Fix
# Epic: epic-1-dna
# Sequence: 2 of 3
# Depends on: NONE (can run in parallel with Ticket 01)
---

## Objective
Eliminate TOCTOU race condition in Retest entry rollback logic by converting async `Enqueue` add to synchronous atomic write, preventing permanent FSM state leaks when broker submission fails.

## Scope
IN scope:
- `src/V12_002.Entries.Retest.cs` - Line 173 in `ExecuteRetestEntry()`
- `src/V12_002.Entries.Retest.cs` - Line 310 in `ExecuteRetestManualEntry()`

OUT of scope:
- Complexity reduction for ExecuteRetestEntry (CYC=26) and ExecuteRetestManualEntry (CYC=17)
- Other `Enqueue` usages in Retest entry methods
- REAPER audit logic modifications
- Test file creation (handled in Ticket 03)

## Context References
- Analysis: docs/brain/epic-1-dna/01-analysis.md -- Section 1 (H21, H22 dependency maps)
- Approach: docs/brain/epic-1-dna/02-approach.md -- Section 1 (Pattern B), Section 2 (H21, H22 implementation)

## Implementation Instructions

### H21: ExecuteRetestEntry() (Line 173)

**File:** `src/V12_002.Entries.Retest.cs`

**Current Code (line 173):**
```csharp
Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });
```

**Replace with:**
```csharp
// [BUILD 981 EXEMPTION]: Synchronous write to activePositions before broker submission.
// Prevents TOCTOU race where rollback (line 187) executes before queued addition.
// ConcurrentDictionary single-write is thread-safe (no lock required).
activePositions[_en966] = _p966;
```

**Note:** Line 187 (sync remove in catch block) remains unchanged:
```csharp
activePositions.TryRemove(entryName, out _);
```

### H22: ExecuteRetestManualEntry() (Line 310)

**File:** `src/V12_002.Entries.Retest.cs`

**Current Code (line 310):**
```csharp
Enqueue(ctx => { ctx.activePositions[_en966] = _p966; });
```

**Replace with:**
```csharp
// [BUILD 981 EXEMPTION]: Synchronous write to activePositions before broker submission.
// Prevents TOCTOU race where rollback (line 324) executes before queued addition.
// ConcurrentDictionary single-write is thread-safe (no lock required).
activePositions[_en966] = _p966;
```

**Note:** Line 324 (sync remove in catch block) remains unchanged:
```csharp
activePositions.TryRemove(entryName, out _);
```

## V12 DNA Guardrails
- [x] Zero new lock() statements
- [x] Zero non-ASCII characters in string literals
- [x] All changes are single-line replacements (no extraction required)
- [x] No logic drift -- pure structural movement from async to sync
- [x] Inline comments document Build 981 exemption at all sync write sites
- [x] Sync remove statements (lines 187, 324) remain unchanged

## Post-Edit Verification (Mandatory)
```powershell
# 1. Re-establish hard links (MANDATORY after every src/ edit)
powershell -File .\deploy-sync.ps1

# 2. Lock regression (must return ZERO)
grep -r "lock(" src/V12_002.Entries.Retest.cs

# 3. ASCII gate (must return ZERO)
grep -Prn "[^\x00-\x7F]" src/V12_002.Entries.Retest.cs

# 4. Verify Enqueue removal for activePositions adds
grep -n "Enqueue.*activePositions\[_en966\]" src/V12_002.Entries.Retest.cs
# Expected: ZERO matches

# 5. Verify synchronous writes exist
grep -n "activePositions\[_en966\] = _p966;" src/V12_002.Entries.Retest.cs
# Expected: 2 matches (lines 173, 310)

# 6. Verify Build 981 exemption comments
grep -n "BUILD 981 EXEMPTION" src/V12_002.Entries.Retest.cs
# Expected: 2 matches (one per method)

# 7. Verify rollback statements unchanged
grep -n "activePositions.TryRemove(entryName, out _);" src/V12_002.Entries.Retest.cs
# Expected: 2 matches (lines 187, 324)
```

## Acceptance Criteria
- [x] Line 173 in `ExecuteRetestEntry()` uses synchronous write (no `Enqueue`)
- [x] Line 310 in `ExecuteRetestManualEntry()` uses synchronous write (no `Enqueue`)
- [x] Both sync write sites have Build 981 exemption comment block
- [x] Lines 187, 324 (sync remove statements) remain unchanged
- [x] deploy-sync.ps1 ASCII gate: PASS
- [x] lock() audit: ZERO matches in modified file
- [x] Enqueue audit: ZERO matches for activePositions adds in modified file
- [x] Director presses F5 in NinjaTrader -- BUILD_TAG banner visible