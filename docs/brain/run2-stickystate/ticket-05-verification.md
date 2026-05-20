# Ticket 05: Verification & Cleanup

**Epic:** run2-stickystate  
**Phase:** E - Verification & Cleanup  
**Assignee:** v12-engineer + Advanced mode  
**Estimated Complexity:** MODERATE  
**Dependencies:** ticket-04 (Strategy Integration)

---

## OBJECTIVE

Run all 7 verification gates from [`02-approach.md`](docs/brain/run2-stickystate/02-approach.md). Verify service extraction is complete, correct, and performant. Test all 11 IPC commands. Verify restart hydration. Confirm no performance regression. Final commit with BUILD_TAG.

---

## SCOPE

### Verification Gates

1. **Build Gate**: `deploy-sync.ps1` + ASCII audit
2. **Test Gate**: `dotnet test` (service unit tests)
3. **F5 Gate**: NinjaTrader IDE compile + load
4. **IPC Gate**: Test all 11 IPC commands
5. **Restart Gate**: Verify state hydration after restart
6. **Performance Gate**: Verify no serialization latency regression
7. **Commit Gate**: Final commit with BUILD_TAG

---

## VERIFICATION STEPS

### Gate 1: Build Verification

**Executor:** v12-engineer

Run the deployment sync script:

```powershell
powershell -File .\deploy-sync.ps1
```

**Expected Output:**
```
[DEPLOY-SYNC] Starting...
[DEPLOY-SYNC] Hard-link sync complete
[ASCII GATE] PASS
[DEPLOY-SYNC] Complete
```

**Pass Criteria:**
- Exit code 0
- ASCII gate shows PASS
- No compilation errors

**Fail Action:**
- If ASCII gate fails: Fix Unicode violations
- If sync fails: Check hard-link integrity
- Re-run until PASS

---

### Gate 2: Test Verification

**Executor:** v12-engineer

Run the service unit tests:

```bash
dotnet test tests/Services/StickyStateServiceTests.cs
```

**Expected Output:**
```
Test run for tests.dll (.NET 8.0)
Total tests: 3
     Passed: 3
```

**Pass Criteria:**
- All 3 tests pass
- No NinjaTrader dependencies
- Service instantiation works

**Fail Action:**
- If tests fail: Check service implementation
- Verify DTO definitions
- Fix and re-run

---

### Gate 3: F5 Gate (NinjaTrader IDE)

**Executor:** Director (manual action)

**Instructions:**
1. Open NinjaTrader 8 IDE
2. Press F5 to compile
3. Wait for BUILD_TAG banner in Output window
4. Verify no compilation errors

**Expected Output:**
```
Compiling...
V12_002 compiled successfully
BUILD_TAG: 1108.002
```

**Pass Criteria:**
- Compilation succeeds
- BUILD_TAG appears
- Strategy loads without errors

**Fail Action:**
- If compile fails: Check syntax errors
- If BUILD_TAG missing: Check deployment
- Fix and retry F5

---

### Gate 4: IPC Command Testing

**Executor:** v12-engineer (via NinjaTrader Control Center)

Test all 11 IPC commands that trigger `MarkStickyDirty()`:

#### Config Commands (6 tests)
1. `/set-target 1 10.5 ATR` -- Verify T1 persisted
2. `/set-target 2 15.0 TICKS` -- Verify T2 persisted
3. `/set-stop-mult 2.5` -- Verify STR persisted
4. `/set-max-risk 500` -- Verify MAX persisted
5. `/set-cit ABC123` -- Verify CIT persisted
6. `/set-target-count 3` -- Verify COUNT persisted

#### Fleet Commands (3 tests)
7. `/set-leader Apex_F01_12345` -- Verify LEADER persisted
8. `/fleet-toggle Apex_F02_67890 1` -- Verify toggle persisted
9. `/fleet-toggle Apex_F02_67890 0` -- Verify toggle cleared

#### Mode Commands (2 tests)
10. `/set-mode CT` -- Verify MODE forced to OR (safety gate)
11. `/set-mode OR` -- Verify MODE persisted

**Test Procedure:**
1. Send IPC command via Control Center
2. Wait 100ms (debounce window)
3. Check sticky file: `%USERPROFILE%\Documents\NinjaTrader 8\strategies\v12_sticky\sticky_<instrument>.txt`
4. Verify value persisted correctly

**Pass Criteria:**
- All 11 commands persist correctly
- MODE safety gate works (CT → OR)
- File format matches spec
- No exceptions in Output window

**Fail Action:**
- If command fails: Check IPC handler
- If persist fails: Check service serialization
- If format wrong: Check WriteStickyConfig logic
- Fix and re-test

---

### Gate 5: Restart Hydration Test

**Executor:** v12-engineer

**Test Procedure:**
1. Set T1=12.5, T2=18.0, STR=3.0 via IPC
2. Wait 100ms for persist
3. Disable strategy in Control Center
4. Re-enable strategy
5. Verify values restored from sticky file

**Verification:**
```bash
# Check Output window for load message
grep "Loaded settings from sticky_" <output_log>
```

**Expected Output:**
```
[STICKY] Loaded settings from sticky_ES_03-25.txt
```

**Pass Criteria:**
- Load message appears
- T1, T2, STR values restored
- No "using defaults" message
- Strategy state matches persisted state

**Fail Action:**
- If load fails: Check deserialization
- If values wrong: Check ApplyStickyStateData
- If file missing: Check file path logic
- Fix and re-test

---

### Gate 6: Performance Verification

**Executor:** v12-engineer

**Test Procedure:**
1. Enable strategy with live data
2. Send 10 rapid IPC commands (trigger MarkStickyDirty)
3. Monitor Output window for timing
4. Verify no UI lag

**Baseline (from 01-analysis.md):**
- Serialization: <5ms (background thread)
- Debounce window: 50ms
- No strategy thread blocking

**Verification:**
```bash
# Check for timing messages in Output
grep "STICKY.*ms" <output_log>
```

**Pass Criteria:**
- No "Write took >10ms" warnings
- No strategy thread blocking
- UI remains responsive
- Debouncing works (coalesces rapid calls)

**Fail Action:**
- If slow: Check file I/O
- If blocking: Check thread safety
- If no debounce: Check recursive call logic
- Fix and re-test

---

### Gate 7: Final Commit

**Executor:** Advanced mode (auto-commit after F5 done)

**Commit Message Format:**
```
[run2-stickystate] ticket-05: Verification complete -- Service extraction PASS [BUILD_TAG]

- All 7 gates passed
- 11 IPC commands tested
- Restart hydration verified
- Performance baseline maintained
- Service testable via dotnet test

Files modified:
- src/Services/IStickyStateService.cs (new)
- src/Services/StickyStateService.cs (new)
- tests/Services/StickyStateServiceTests.cs (new)
- src/V12_002.StickyState.cs (modified)

Lines removed: ~400 (dead serialization/deserialization)
Lines added: ~150 (service integration)
Net diff: -250 lines
```

**Commit Procedure:**
1. Wait for Director to type "F5 done [BUILD_TAG]"
2. Run: `git add -A`
3. Run: `git commit -m "<message>"`
4. Report commit hash

---

## ACCEPTANCE CRITERIA

1. ✅ Gate 1: Build verification PASS
2. ✅ Gate 2: Test verification PASS (3/3 tests)
3. ✅ Gate 3: F5 gate PASS (BUILD_TAG confirmed)
4. ✅ Gate 4: IPC testing PASS (11/11 commands)
5. ✅ Gate 5: Restart hydration PASS
6. ✅ Gate 6: Performance verification PASS
7. ✅ Gate 7: Final commit complete

---

## VERIFICATION SUMMARY

After all gates pass, generate this summary:

```
[VERIFICATION-COMPLETE] run2-stickystate

Build Gate       : PASS (deploy-sync + ASCII)
Test Gate        : PASS (3/3 tests)
F5 Gate          : PASS (BUILD_TAG: 1108.002)
IPC Gate         : PASS (11/11 commands)
Restart Gate     : PASS (hydration verified)
Performance Gate : PASS (no regression)
Commit Gate      : PASS (hash: <commit_hash>)

Service Extraction: COMPLETE
- IStickyStateService.cs (new)
- StickyStateService.cs (new)
- StickyStateServiceTests.cs (new)
- V12_002.StickyState.cs (modified)

Net diff: -250 lines (400 removed, 150 added)
Testability: dotnet test works (no NinjaTrader runtime)
Thread safety: H18-FIX snapshot pattern preserved
Call sites: All 18 unchanged

Epic ready for PR.
```

---

## NOTES

- This is the final ticket in the epic
- All verification must pass before commit
- Director must confirm F5 gate manually
- Performance baseline from 01-analysis.md
- Commit message includes BUILD_TAG

---

## ESTIMATED TIME

**1-2 hours** (7 gates + manual F5 + commit)