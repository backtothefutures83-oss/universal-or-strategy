# EPIC-5-PERF: Execution Guide

**Epic ID:** EPIC-5-PERF  
**Status:** Ready for Execution  
**Created:** 2026-05-23  
**Total Tickets:** 9 (T01, T01B, T02, T03, T04, T05, T06, T07, T08)  
**Estimated Duration:** 17.5 days

---

## EXECUTION SUMMARY

This epic eliminates ALL heap allocations in V12's hot paths and hardens state/logging infrastructure through 9 surgical tickets.

**Target Outcome:** Zero allocations, p99 <100μs latency, zero GC pauses, and robust build migration support.

---

## TICKET OVERVIEW

| Ticket | Name | Duration | Dependencies | CYC Impact | Files Modified |
|--------|------|----------|--------------|------------|----------------|
| T01 | Baseline Instrumentation & Stopwatch Migration | 4 days | None | Neutral | 9 |
| T01B | Thread Model Analysis & ThreadStatic Validation | 1 day | T01 | Neutral | 0 (docs/tests) |
| T02 | String.Format Elimination (LogBuffer) | 2 days | T01B | NEUTRAL | 8 |
| T03 | UIStateSnapshot Object Pooling | 3 days | T01 | +3 | 2 |
| T04 | .ToArray() Elimination | 2 days | T01 | Neutral | 6 |
| T05 | Order Array Pooling | 1 day | T01 | +2 | 2 |
| T06 | MonitorRmaProximity Refactoring | 2 days | T01 | 32→31 | 1 |
| T08 | StickyState Version Migration | 0.5 day | None | Neutral | 1 |
| T07 | Verification & Stress Testing | 2 days | T01-T06, T08 | Neutral | 0 (testing) |

---

## EXECUTION ORDER

### Phase 1: Foundation (Days 1-5)
```
T01 (Baseline + Stopwatch Migration) [4 days]
  ↓
T01B (Thread Model Analysis) [1 day]
```

**Gate 1:** Baseline metrics established, ThreadStatic safety validated.

### Phase 2: Parallel Optimization & Hardening (Days 6-13)
```
T02 (String.Format) [2 days] ← depends on T01B
T03 (UISnapshot Pool) [3 days]
T04 (.ToArray()) [2 days]
T05 (Order Pool) [1 day]
T06 (MonitorRma) [2 days]
T08 (StickyState Migration) [0.5 day]
```

**Gate 2:** All optimizations complete, build migration enabled, individual F5 gates passed.

### Phase 3: Verification (Days 14-17)
```
T07 (Verification & Stress Testing) [2 days]
```

**Gate 3:** p99 <100μs validated, zero GC pauses confirmed.

---

## TICKET DETAILS

### T02: String.Format Elimination (LogBuffer)

**Goal:** Replace all hot-path `string.Format()` with pre-allocated char[] buffers.

**Scope:**
1. Implement `LogBuffer` class (ThreadStatic based on T01B verdict).
2. **DIRECTOR FIX**: Update FormatInternal to detect format specifiers (e.g., "{0:F2}") and return -1 to trigger a fallback to `string.Format`. This ensures correctness for prices while maintaining performance for simple indices.
3. Add overflow counter (validation mitigation).
4. Replace string.Format in 30+ instances across 7 files.
5. Include `ValidateThreadAffinity` telemetry per T01B recommendation.
6. Verify zero allocation via ETW trace for simple placeholders.

**Success Criteria:**
- LogBuffer.Format returns correct strings (no literal "{1:F2}" in logs).
- ETW trace shows zero allocations in LogBuffer.Format for simple placeholders.

---

### T08: StickyState Version Migration

**Goal:** Prevent "Integrity check failed" loops on build upgrades by separating versioning from checksums.

**Scope:**
1. Modify `ValidateSnapshotIntegrity` in `V12_002.StickyState.cs`.
2. Decouple `StrategyVersion` check from the boolean success result.
3. If checksum passes but version mismatches:
   - Log warning: `[STICKY] Version mismatch detected: {0} -> {1}. Migrating state.`
   - Return `true` (success).
4. Ensure the new build version is persisted on next save.

**Success Criteria:**
- StickyState loads successfully after build tag changes.
- Rollback only occurs on actual SHA256 checksum failures.

---

## DEPENDENCY GRAPH

```
T01 (Baseline + Stopwatch Migration) [4d]
  ↓
T01B (Thread Model Analysis) [1d]
  ↓
T02 (LogBuffer Fixes) [2d] ──────┐
                                  │
T01 ──→ T03 (UISnapshot Pool) [3d] ─┤
                                  │
T01 ──→ T04 (.ToArray() Elim) [2d] ─┤
                                    │
T01 ──→ T05 (Order Pool) [1d] ──────┤
                                    │
T01 ──→ T06 (MonitorRma Refactor) [2d] ─┤
                                        │
        T08 (StickyState Migration) [0.5d] ─┤
                                            ↓
                                    T07 (Verification) [2d]
```

**Total Duration:** 17.5 days (with parallelization)
