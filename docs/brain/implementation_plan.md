# Epic 6 & Global Quality Adjudication Master Execution Plan

## Goal Description

Epic 5 (Performance Optimization) has been successfully completed, achieving zero allocations in the hot path and bounded latency < 300μs. We are now transitioning to **Epic 6**, which focuses on building automated test harnesses to lock in these performance gains, while simultaneously adjudicating deferred quality debt from PR #1 (REAPER-EXPANSION) and PR #2 (EPIC-4-STICKY-STATE).

This plan synthesizes the automated testing goals of Epic 6 with the 5-phase quality debt remediation strategy, ensuring that refactoring does not compromise the performance baselines established in Epic 5.

## Proposed Changes

### Phase 1: Epic 6 Performance Lock-In (Automated Testing)
Before mutating any source code for quality debt, we must protect the Epic 5 gains.
- **AMAL / Benchmark Harness**: Create/update BenchmarkDotNet tests to assert `Allocated = 0 B` and `Mean Latency < 300μs`.
- **TDD Safety Net**: Implement unit tests covering the FSM/Actor `Enqueue` model and lock-free execution paths.

### Phase 2: Critical Complexity Reduction (Quality Debt P0 & EPIC-4 P1)
Targeting Jane Street alignment (≤15 cyclomatic complexity).
- **Split God Functions**: 
  - `V12_002.Orders.Callbacks.AccountOrders.cs` (CC 221)
  - `V12_002.SIMA.Lifecycle.cs` (CC 217)
  - `V12_002.IPC.Hardening.cs` (CC 18)
  - `V12_002.StickyState.cs` (CC 12 - extract restoration logic)
- **Method**: Use Bob CLI and Python extractor script for all file splits.

### Phase 3: Duplication Elimination & Error-Prone Fixes (Quality Debt P1 & EPIC-4 P2)
- **Entry Method Consolidation**: Extract unified entry logic across the 6 high-clone files (e.g., `V12_002.Entries.FFMA.cs`, `V12_002.Entries.Retest.cs`).
- **NRT & Null Guards**: Resolve the 46 ErrorProne issues from EPIC-4 (Nullable reference warnings, explicit `ArgumentNullException.ThrowIfNull()`).

### Phase 4: High Issue Resolution & CodeStyle Cleanup
- **Codacy Hotspots**: Triage and fix the 10 files with 80+ issues (`V12_002.Orders.Callbacks.Propagation.cs`, etc.).
- **Style & Documentation**: Add XML docs to public methods, fix PascalCase/camelCase violations, and normalize whitespace (EPIC-4 P3).

### Phase 5: Final Polish & Validation
- **Quality Gates**: Ensure Codacy Grade A, 100% PHS (25/25), <20 CodeFactor issues.
- **Build Sync**: Run `deploy-sync.ps1`, verify ASCII-only compliance, and ensure zero locks across `src/`.

## Verification Plan

### Automated Tests
- Run `powershell -File .\scripts\test_stress.ps1`.
- Execute AMAL harness (`scripts/amal_harness.py`) to verify zero allocations and bounded latency.
- Run `powershell -File .\scripts\lint.ps1` and `droid /review` to assert quality improvements.

### Manual Verification
- Deploy to NinjaTrader (F5 compilation) and verify BUILD_TAG.
- Director confirmation of Codacy dashboard metrics.
