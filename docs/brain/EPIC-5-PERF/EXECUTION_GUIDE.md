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
- **T01** (Baseline + Stopwatch Migration)
- **T01B** (Thread Model Analysis)

### Phase 2: Parallel Optimization & Hardening (Days 6-13)
- **T02** (String.Format Fixes)
- **T03** (UISnapshot Pool)
- **T04** (.ToArray() Elimination)
- **T05** (Order Pool)
- **T06** (MonitorRma Refactor)
- **T08** (StickyState Migration)

### Phase 3: Verification (Days 14-17)
- **T07** (Verification & Stress Testing)

---

## TICKET DETAILS

### T01: Baseline Instrumentation & Stopwatch Migration
*Details in `ticket-01-latency-probe.md`*

### T01B: Thread Model Analysis & ThreadStatic Validation
*Details in `ticket-01B-thread-model.md`*

### T02: String.Format Elimination (LogBuffer)
**Goal:** Replace all hot-path `string.Format()` with pre-allocated char[] buffers.
- **DIRECTOR FIX**: Update FormatInternal to detect format specifiers (e.g., "{0:F2}") and return -1 to trigger fallback.
- Replaced 57+ string.Format() calls.
- Included ValidateThreadAffinity telemetry.

### T03: UIStateSnapshot Object Pooling
*Details in `ticket-03-ui-snapshot-pool-REVISED.md`*

### T04: .ToArray() Elimination
*Details in `ticket-04-toarray-elimination.md`*

### T05: Order Array Pooling
*Details in `ticket-05-order-array-pooling.md`*

### T06: MonitorRmaProximity Refactoring
*Details in `ticket-06-monitor-rma-proximity.md`*

### T08: StickyState Version Migration
**Goal:** Prevent "Integrity check failed" loops on build upgrades.
- Decouple StrategyVersion from SHA256 boolean result.
- Log migration warning instead of failing load.

### T07: Verification & Stress Testing
**Goal:** Validate p99 <100μs target and zero GC pressure.
- Latency Re-Baseline (1-hour test).
- Allocation Profiling (ETW trace).
- GC Pause Validation (PerfMon).
- Stress Test (10k ticks/sec).
