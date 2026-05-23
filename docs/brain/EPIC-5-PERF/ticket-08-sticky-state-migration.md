# EPIC-5-PERF: Ticket T08 - StickyState Version Migration

**Ticket ID:** T08  
**Epic:** EPIC-5-PERF  
**Status:** Ready for Execution  
**Created:** 2026-05-23  
**Dependencies:** None (independent hardening task)  
**Estimated Duration:** 0.5 day  
**Risk Level:** LOW (defensive fix, no new features)

---

## OBJECTIVE

Fix the "Integrity check failed" infinite loop that occurs when BUILD_TAG changes between strategy restarts. The current implementation couples checksum validation with version checking, causing valid snapshots from previous builds to be rejected and triggering unnecessary rollback attempts.

**Target Outcome:** Smooth migration across BUILD_TAG changes, zero data loss, clear migration logging.

---

## PROBLEM STATEMENT

### Current Behavior (Build Logs Evidence)

When BUILD_TAG changes (e.g., from `Build 935 [R-01]` to `Build 936 [R-02]`):

1. User restarts NinjaTrader with new build
2. `LoadStateSnapshot()` reads persisted state from previous build
3. `ValidateSnapshotIntegrity()` computes checksum over snapshot with **old BUILD_TAG**
4. Checksum validation **FAILS** (line 158-168) because:
   - Stored checksum was computed with `StrategyVersion = "Build 935 [R-01]"`
   - Current checksum computed with `StrategyVersion = "Build 936 [R-02]"`
5. Method returns `false`, triggering rollback at line 128
6. Rollback reads `.bak` file (also from Build 935)
7. Same checksum mismatch occurs → rollback fails
8. Strategy starts with empty state, losing all persisted positions

### Root Cause

**File:** `src/V12_002.StickyState.cs`  
**Method:** `ValidateSnapshotIntegrity` (lines 148-184)

The method performs two validations in sequence:
1. **Checksum validation** (lines 158-168) - Hard fail if mismatch
2. **Version check** (lines 170-181) - Soft migration if mismatch

**The Bug:** Checksum is computed over the **entire snapshot including StrategyVersion field**. When BUILD_TAG changes, the checksum will ALWAYS fail, even though the data is valid.

**Current Logic:**
```csharp
// Line 151-155: Compute checksum over snapshot WITH current StrategyVersion
string storedChecksum = snapshot.ChecksumSHA256;
snapshot.ChecksumSHA256 = string.Empty;
string canonicalJson = SerializeSnapshot(snapshot); // Uses snapshot.StrategyVersion (old build)
string computedChecksum = ComputeSHA256(canonicalJson);
snapshot.ChecksumSHA256 = storedChecksum;

// Line 158-168: Checksum validation (FAILS on version change)
if (storedChecksum != computedChecksum)
{
    Print("[STICKY] Checksum mismatch! ..."); // This fires on every BUILD_TAG change
    return false; // Triggers rollback loop
}

// Line 170-181: Version check (NEVER REACHED due to early return above)
if (snapshot.StrategyVersion != BUILD_TAG)
{
    Print("[STICKY] Version mismatch detected: {0} -> {1}. Migrating state.", ...);
    return true; // Would allow migration, but never executed
}
```

---

## SOLUTION DESIGN

### Strategy: Decouple Version Check from Checksum Validation

**Key Insight:** The checksum should validate **data integrity** (corruption detection), not **version compatibility** (migration policy).

**New Logic Flow:**
1. **Version check FIRST** (soft migration)
   - If `snapshot.StrategyVersion != BUILD_TAG`, log migration warning and proceed
   - Do NOT recompute checksum with new BUILD_TAG (preserve original)
2. **Checksum validation SECOND** (hard fail)
   - Compute checksum over snapshot with **original StrategyVersion** (as stored)
   - Compare against stored checksum
   - Fail only if data is corrupted (not if version changed)

### Implementation Pattern

```csharp
private bool ValidateSnapshotIntegrity(StateSnapshot snapshot, string json)
{
    // 1. VERSION CHECK FIRST (soft migration)
    bool isVersionMismatch = (snapshot.StrategyVersion != BUILD_TAG);
    if (isVersionMismatch)
    {
        Print(
            string.Format(
                "[STICKY] Version mismatch detected: {0} -> {1}. Migrating state.",
                snapshot.StrategyVersion,
                BUILD_TAG
            )
        );
        // Continue to checksum validation (do NOT early return)
    }

    // 2. CHECKSUM VALIDATION (hard fail on corruption)
    // Compute checksum over ORIGINAL snapshot (with old StrategyVersion)
    string storedChecksum = snapshot.ChecksumSHA256;
    snapshot.ChecksumSHA256 = string.Empty;
    string canonicalJson = SerializeSnapshot(snapshot); // Uses snapshot.StrategyVersion (original)
    string computedChecksum = ComputeSHA256(canonicalJson);
    snapshot.ChecksumSHA256 = storedChecksum;

    if (storedChecksum != computedChecksum)
    {
        Print(
            string.Format(
                "[STICKY] Checksum mismatch! Expected: {0}, Got: {1}",
                storedChecksum,
                computedChecksum
            )
        );
        return false; // Data corruption detected
    }

    // 3. SUCCESS (data valid, migration allowed)
    if (isVersionMismatch)
    {
        Print("[STICKY] Migration successful. State loaded from previous build.");
    }
    return true;
}
```

**Key Changes:**
- Version check moved BEFORE checksum validation
- Checksum computed over **original snapshot** (preserves StrategyVersion as stored)
- Migration warning logged, but validation continues
- Only return `false` if checksum fails (data corruption)

---

## MIGRATION STRATEGY

### Phase 1: Code Review & Verification (Morning)

**Goal:** Confirm the bug exists and understand current behavior.

**Actions:**
1. Read `V12_002.StickyState.cs` lines 148-184 (ValidateSnapshotIntegrity)
2. Trace execution flow for BUILD_TAG change scenario
3. Verify checksum computation includes StrategyVersion field
4. Document current rollback behavior (lines 186-232)

**Deliverable:** Confirmation that checksum validation blocks version migration.

### Phase 2: Implement Fix (Afternoon)

**Goal:** Reorder validation logic to allow version migration.

**Protocol:**
1. Modify `ValidateSnapshotIntegrity` method (lines 148-184)
2. Move version check (lines 170-181) BEFORE checksum validation (lines 158-168)
3. Remove early return from version check (allow checksum validation to proceed)
4. Add migration success log after checksum passes
5. Run `deploy-sync.ps1` + F5 compile gate
6. Commit: `[T08] Decouple version check from checksum validation`

**Verification:**
- Checksum validation still protects against corruption
- Version mismatch no longer triggers rollback
- Migration warning logged clearly

### Phase 3: Manual Testing (End of Day)

**Goal:** Verify smooth migration across BUILD_TAG changes.

**Test Scenario:**
1. Start strategy with `BUILD_TAG = "Build 935 [R-01]"`
2. Create sticky state (fill entry order, persist positions)
3. Stop strategy
4. Change `BUILD_TAG` to `"Build 936 [R-02]"` (simulate new build)
5. Restart strategy
6. **Expected:** Migration warning logged, state restored successfully
7. **Verify:** No "Integrity check failed" errors, no rollback attempts

---

## CALLER IMPACT ANALYSIS

### Methods Modified

**Primary:**
- `ValidateSnapshotIntegrity` (refactored, signature unchanged)

**Callers:**
- `LoadStateSnapshot` (line 126) - No changes needed
- `RollbackToLastGoodState` (line 207) - No changes needed

**Public API Impact:** ZERO (all changes are private/internal)

---

## CYC IMPACT ESTIMATE

### Before

**ValidateSnapshotIntegrity:** CYC ~4 (2 if-statements, 1 early return)

### After

**ValidateSnapshotIntegrity:** CYC ~4 (2 if-statements, 1 early return)

**Net CYC Impact:** ZERO (logic reordered, no new branches)

---

## RISK MITIGATION

### High-Risk Scenarios

1. **Checksum Bypass on Corruption**
   - **Risk:** Version check allows corrupted data to load
   - **Mitigation:** Checksum validation still runs AFTER version check (hard fail preserved)
   - **Verification:** Manually corrupt `.json` file, verify rollback triggers

2. **Migration Loop on Backup**
   - **Risk:** Backup file also has old BUILD_TAG, triggers same issue
   - **Mitigation:** Fix applies to ALL snapshot loads (primary + backup)
   - **Verification:** Test rollback scenario with version mismatch

### Low-Risk Scenarios

1. **Log Spam on Every Restart**
   - **Risk:** Migration warning logged on every restart (even if no data change)
   - **Mitigation:** Acceptable - warns user that state is from previous build
   - **Future:** Could suppress after first successful migration

---

## ACCEPTANCE CRITERIA

### Functional Requirements

1. ✅ Version mismatch no longer triggers "Integrity check failed" error
2. ✅ Checksum validation still protects against data corruption
3. ✅ Migration warning logged clearly when BUILD_TAG changes
4. ✅ State restored successfully across BUILD_TAG changes

### Performance Requirements

1. ✅ Zero latency impact (validation logic reordered, not expanded)
2. ✅ Zero allocation impact (no new string operations)

### V12 DNA Compliance

1. ✅ Zero `lock()` statements introduced (verified via grep)
2. ✅ ASCII-only strings (no Unicode in any changes)
3. ✅ CYC unchanged (verified via complexity_audit.py)
4. ✅ Hard-link integrity maintained (deploy-sync.ps1 passes)

### Regression Tests

1. ✅ F5 compile gate passes (NinjaTrader loads without errors)
2. ✅ Manual test: BUILD_TAG change, verify migration succeeds
3. ✅ Manual test: Corrupt `.json` file, verify rollback triggers
4. ✅ Manual test: Backup file with old BUILD_TAG, verify rollback succeeds

---

## DELIVERABLES

1. **Refactored Source File**
   - `src/V12_002.StickyState.cs` (ValidateSnapshotIntegrity method)

2. **Verification Report** (Markdown)
   - Before/after execution flow diagram
   - Manual test results (BUILD_TAG change scenario)
   - Corruption test results (checksum validation still works)

---

## EXECUTION CHECKLIST

### Pre-Flight

- [ ] Read this ticket completely
- [ ] Read `V12_002.StickyState.cs` lines 148-184 (ValidateSnapshotIntegrity)
- [ ] Trace execution flow for BUILD_TAG change scenario
- [ ] Confirm bug exists (checksum blocks version migration)

### Phase 1: Code Review (Morning)

- [ ] Document current validation order (checksum → version)
- [ ] Identify early return at line 167 (blocks version check)
- [ ] Verify checksum includes StrategyVersion field
- [ ] **[GATE]** Director approval of fix strategy

### Phase 2: Implementation (Afternoon)

- [ ] Modify `ValidateSnapshotIntegrity` method
- [ ] Move version check BEFORE checksum validation
- [ ] Remove early return from version check
- [ ] Add migration success log
- [ ] Run `deploy-sync.ps1` (hard-link sync)
- [ ] F5 compile test
- [ ] Run `python scripts/complexity_audit.py` (verify CYC unchanged)
- [ ] Commit: `[T08] Decouple version check from checksum validation`

### Phase 3: Manual Testing (End of Day)

- [ ] Test Scenario 1: BUILD_TAG change (verify migration succeeds)
- [ ] Test Scenario 2: Corrupt `.json` file (verify rollback triggers)
- [ ] Test Scenario 3: Backup with old BUILD_TAG (verify rollback succeeds)
- [ ] Generate verification report
- [ ] **[GATE]** Director sign-off

---

## ROLLBACK STRATEGY

**Revert Command:** `git revert <commit-hash>`

**Impact:** Reverts to original validation order (checksum → version)

**Validation:** Run F5 compile gate after revert to confirm clean rollback

---

## NOTES

### Why This Is Low-Risk

1. **Pure Logic Reordering:** No new branches, no new allocations
2. **Checksum Protection Preserved:** Hard fail on corruption still enforced
3. **Single Method Change:** Isolated to ValidateSnapshotIntegrity
4. **No Caller Impact:** Method signature unchanged

### Future Enhancements (Out of Scope)

1. **Migration Metadata:** Track last migrated BUILD_TAG to suppress duplicate warnings
2. **Schema Versioning:** Add `SchemaVersion` field separate from `StrategyVersion`
3. **Backward Compatibility:** Support loading snapshots from older schema versions

---

## SUCCESS METRICS

| Metric | Before | Target | Measurement |
|--------|--------|--------|-------------|
| BUILD_TAG change success rate | 0% (rollback loop) | 100% | Manual test |
| Checksum validation preserved | Yes | Yes | Corruption test |
| CYC (ValidateSnapshotIntegrity) | ~4 | ~4 | complexity_audit.py |
| Migration warnings logged | No | Yes | Log inspection |

---

## DEPENDENCIES

**Upstream:** None (independent hardening task)

**Downstream:**
- T07 (Verification & Stress Testing) - Will validate migration behavior

**Parallel:**
- T02 (String.Format Elimination) - Independent
- T03 (UISnapshot Pooling) - Independent
- T04 (.ToArray() Elimination) - Independent
- T05 (Order Array Pooling) - Independent
- T06 (MonitorRma Refactoring) - Independent

---

**[TICKET-GATE]** T08 ticket ready for execution. This is a LOW-RISK defensive fix to prevent data loss on BUILD_TAG changes. Awaiting Director approval to proceed with Phase 1 code review.