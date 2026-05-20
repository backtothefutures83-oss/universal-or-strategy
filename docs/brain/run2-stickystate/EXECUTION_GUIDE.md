# EXECUTION GUIDE: run2-stickystate

**Epic:** Decouple StickyState & IPC  
**Status:** Ready for Execution  
**Total Tickets:** 5  
**Estimated Total Time:** 8-10 hours

---

## EPIC OVERVIEW

Extract serialization/IPC logic from [`V12_002.StickyState.cs`](src/V12_002.StickyState.cs) into pure C# service to enable `dotnet test` without NinjaTrader runtime. This is a **surgical port with zero logic drift** - preserve all parsing logic, thread safety patterns, and call site integrity.

---

## TICKET EXECUTION ORDER

Tickets MUST be executed in this order due to dependencies:

### 1. ticket-01-service-foundation.md
**Phase:** A - Service Foundation  
**Dependencies:** None  
**Estimated Time:** 30 minutes  
**Complexity:** LOW

**Deliverables:**
- `src/Services/IStickyStateService.cs` (interface + logger interface)
- `src/Services/StickyStateData.cs` (DTO definitions)
- `tests/Services/StickyStateServiceTests.cs` (unit test stub)

**Success Criteria:**
- `dotnet build` succeeds
- `dotnet test` passes (1 instantiation test)
- No NinjaTrader dependencies

---

### 2. ticket-02-serialization.md
**Phase:** B - Serialization Extraction  
**Dependencies:** ticket-01  
**Estimated Time:** 2 hours  
**Complexity:** MODERATE

**Deliverables:**
- `StickyStateService.Serialize()` method
- All serialization helpers (WriteConfig, WriteFleet, etc.)
- Atomic file write pattern (.tmp + rename)

**Success Criteria:**
- Service can serialize snapshot to file
- File format matches V12 spec
- Atomic write pattern works
- `dotnet test` passes

---

### 3. ticket-03-deserialization.md
**Phase:** C - Deserialization Extraction  
**Dependencies:** ticket-02  
**Estimated Time:** 3 hours  
**Complexity:** HIGH

**Deliverables:**
- `StickyStateService.Deserialize()` method
- All parsing methods (ParseConfig, ParseFleet, etc.)
- DTO population logic

**Success Criteria:**
- Service can deserialize file to DTO
- All parsing logic ported 1:1
- Safety gates preserved (MODE forced to OR)
- `dotnet test` passes

---

### 4. ticket-04-strategy-integration.md
**Phase:** D - Strategy Integration  
**Dependencies:** ticket-03  
**Estimated Time:** 2 hours  
**Complexity:** HIGH

**Deliverables:**
- Service wired into Strategy lifecycle
- `MarkStickyDirty()` modified to call service
- `CreateStickyStateSnapshot()` method
- `ApplyStickyStateData()` method
- Dead code removed (lines 134-354, 407-769)

**Success Criteria:**
- Service initialized in State.DataLoaded
- H18-FIX snapshot pattern preserved
- All 18 call sites unchanged
- `deploy-sync.ps1` passes
- ASCII gate passes

---

### 5. ticket-05-verification.md
**Phase:** E - Verification & Cleanup  
**Dependencies:** ticket-04  
**Estimated Time:** 1-2 hours  
**Complexity:** MODERATE

**Deliverables:**
- All 7 verification gates passed
- Final commit with BUILD_TAG

**Success Criteria:**
- Build gate: PASS
- Test gate: PASS (3/3 tests)
- F5 gate: PASS (BUILD_TAG confirmed)
- IPC gate: PASS (11/11 commands)
- Restart gate: PASS (hydration verified)
- Performance gate: PASS (no regression)
- Commit gate: PASS (commit hash recorded)

---

## DEPENDENCY GRAPH

```
ticket-01 (Foundation)
    ↓
ticket-02 (Serialization)
    ↓
ticket-03 (Deserialization)
    ↓
ticket-04 (Integration)
    ↓
ticket-05 (Verification)
```

**CRITICAL:** Do NOT skip tickets or execute out of order. Each ticket builds on the previous one.

---

## VERIFICATION GATES

Each ticket has its own verification checklist. Additionally, these gates apply to ALL tickets:

### Per-Ticket Gates
1. **Build Gate**: `dotnet build` succeeds
2. **Test Gate**: `dotnet test` passes
3. **ASCII Gate**: No Unicode violations
4. **Sync Gate**: `deploy-sync.ps1` passes (tickets 04-05 only)

### Epic-Level Gates (ticket-05)
1. **F5 Gate**: NinjaTrader IDE compile + BUILD_TAG
2. **IPC Gate**: All 11 IPC commands tested
3. **Restart Gate**: State hydration verified
4. **Performance Gate**: No latency regression

---

## CRITICAL PRESERVATION POINTS

### Thread Safety (H18-FIX Pattern)
- ✅ Snapshot created on strategy thread
- ✅ Snapshot passed to Task.Run (immutable)
- ✅ No direct property access in background thread

### Debouncing Pattern
- ✅ `_stickyWriteInProgress` flag preserved
- ✅ `_stickyDirtyDuringWrite` flag preserved
- ✅ 50ms sleep window preserved
- ✅ Recursive call logic preserved

### Safety Gates
- ✅ MODE always forced to OR (Build 1108.002)
- ✅ Target count clamped 1-5
- ✅ Profile target count clamped 1-5

### Call Site Integrity
- ✅ All 18 call sites to `MarkStickyDirty()` unchanged
- ✅ No new parameters added
- ✅ Public API preserved

### Code Quality
- ✅ ASCII-only compliance (no Unicode)
- ✅ CultureInfo.InvariantCulture for all number parsing
- ✅ All Build tags preserved
- ✅ Zero logic drift (1:1 port)

---

## DIFF BUDGET

**Target:** <500 lines total diff  
**Estimated:** ~327 lines

### Breakdown by Ticket
- ticket-01: +120 lines (new files)
- ticket-02: +180 lines (serialization)
- ticket-03: +380 lines (deserialization)
- ticket-04: -400 lines (dead code), +150 lines (integration) = -250 net
- ticket-05: 0 lines (verification only)

**Net Total:** +430 lines added, -400 lines removed = +30 net change

**Status:** ✅ Within 500-line limit

---

## ROLLBACK PLAN

If any ticket fails verification:

1. **Ticket 01-03 Failure:**
   - Delete new service files
   - Revert to baseline
   - No Strategy changes yet, safe rollback

2. **Ticket 04 Failure:**
   - Revert V12_002.StickyState.cs
   - Keep service files (for retry)
   - Re-run ticket-04 after fix

3. **Ticket 05 Failure:**
   - Fix specific gate failure
   - Re-run verification
   - Do NOT revert code (integration is complete)

---

## SUCCESS METRICS

### Functional Metrics
- ✅ All 11 IPC commands work
- ✅ Restart hydration works
- ✅ MODE safety gate works (CT → OR)
- ✅ All 18 call sites unchanged

### Quality Metrics
- ✅ `dotnet test` works (no NinjaTrader runtime)
- ✅ Zero compilation errors
- ✅ ASCII-only compliance
- ✅ <500 line diff limit

### Performance Metrics
- ✅ Serialization <5ms (background thread)
- ✅ No strategy thread blocking
- ✅ Debouncing works (50ms window)

---

## NOTES FOR ORCHESTRATOR

1. **Mode Selection:**
   - Tickets 01-04: Use `v12-engineer` mode
   - Ticket 05: Use `v12-engineer` for gates 1-6, `Advanced` mode for gate 7 (commit)

2. **F5 Gate (ticket-05):**
   - This is the ONLY manual Director action
   - Wait for Director to press F5 in NinjaTrader
   - Wait for Director to type "F5 done [BUILD_TAG]"
   - Then proceed to auto-commit

3. **Verification Strategy:**
   - Each ticket has self-audit (engineer runs own checks)
   - Ticket 05 has independent verification (Advanced mode)
   - Two-pass verification catches different failure modes

4. **Failure Handling:**
   - If any gate fails: HALT immediately
   - Report failure to Director
   - Do NOT advance to next ticket
   - Wait for Director decision (fix or rollback)

---

## EXECUTION CHECKLIST

Use this checklist to track progress:

- [ ] ticket-01: Service Foundation (30 min)
- [ ] ticket-02: Serialization (2 hours)
- [ ] ticket-03: Deserialization (3 hours)
- [ ] ticket-04: Strategy Integration (2 hours)
- [ ] ticket-05: Verification (1-2 hours)
- [ ] Final commit with BUILD_TAG
- [ ] Epic complete summary generated

---

## FINAL DELIVERABLES

After ticket-05 completes, the epic delivers:

### New Files
1. `src/Services/IStickyStateService.cs` (~50 lines)
2. `src/Services/StickyStateService.cs` (~600 lines)
3. `tests/Services/StickyStateServiceTests.cs` (~100 lines)

### Modified Files
1. `src/V12_002.StickyState.cs` (-400 lines, +150 lines = -250 net)

### Capabilities Unlocked
- ✅ Service testable via `dotnet test` (no NinjaTrader)
- ✅ Serialization logic decoupled from Strategy
- ✅ IPC integration preserved (all 18 call sites unchanged)
- ✅ Thread safety preserved (H18-FIX pattern)
- ✅ Performance maintained (no regression)

---

**Ready for execution. Proceed to ticket-01.**