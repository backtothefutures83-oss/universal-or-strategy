# V12 Universal OR Strategy - Master Roadmap

**Last Updated**: 2026-05-24T03:47:00Z  
**Status**: ACTIVE  
**Owner**: Orchestrator (Bob CLI)

---

## Executive Summary

This document consolidates ALL active work streams into a single unified roadmap:
- **6 Active Conductor Tracks** (existing work)
- **71+ Deferred Bot Findings** (PRs 1-8)
- **Hook Injection System** (completed this session)
- **PR Workflow Hardening** (in progress)

**Total Estimated Effort**: 250+ hours across 12 weeks

---

## 1. Active Tracks Overview

### Track Status Matrix

| Track | Status | Priority | Effort | Dependencies |
|-------|--------|----------|--------|--------------|
| **Hook Injection System** | ✅ COMPLETE | P0 | 12h | None |
| **Bot Findings Consolidation** | 📋 PLANNING | P0 | 98h | Hook System |
| **PR Workflow Hardening** | 🔄 IN PROGRESS | P1 | 16h | Hook System |
| **SIMA Routa Pilot** | ⏸️ PAUSED | P2 | 40h | EPIC-5 complete |
| **Verification 1101e** | ⏸️ PAUSED | P2 | 24h | None |
| **Phase 8 Repair** | ⏸️ PAUSED | P3 | 32h | None |
| **FSM Multi-Target Fix** | ⏸️ PAUSED | P3 | 16h | None |
| **GTC Sweep Fix** | ⏸️ PAUSED | P3 | 12h | None |

**Total Active Effort**: 250 hours

---

## 2. Unified Work Streams

### Stream A: Infrastructure & Quality (P0 - CRITICAL PATH)

**Goal**: Establish deterministic verification and eliminate technical debt

#### A1: Hook Injection System ✅ COMPLETE
**Track**: `conductor/tracks/hook_injection_20260524/`  
**Status**: ✅ DEPLOYED  
**Effort**: 12 hours (completed)

**Deliverables**:
- 9 hooks (5 P0 + 4 P1): pre-forensics, pre/post-deploy-sync, pre-ci-log-extraction, pre-pr-loop, post-epic-ticket, pre-epic-ticket, post-pr-loop, install-git-hooks
- Pester testing framework (3 test files, 127+ tests)
- GitHub Actions workflow (.github/workflows/test-hooks.yml)
- Hook Management CLI (scripts/manage-hooks.ps1, 7 commands)
- Comprehensive documentation (docs/protocol/HOOKS.md, 363 lines)

**Impact**: Prevents non-deterministic bot failures (PR #8 lesson: 380-line drift)

#### A2: Bot Findings Consolidation 📋 PLANNING → 🔄 EXECUTION
**Track**: `conductor/tracks/bot_findings_consolidation_20260524/`
**Status**: Phase 3 COMPLETE (Tickets Created)
**Effort**: 98 hours (phased over 10 weeks)
**Dependencies**: Hook System (complete), PR #8 merge

**Scope**:
- **Phase 0**: Forensic audit of PRs 1-8 ✅ COMPLETE
- **Phase 1**: Tracking infrastructure ✅ COMPLETE
- **Phase 2**: Pattern analysis ✅ COMPLETE (236 findings analyzed)
- **Phase 3**: Architectural tickets ✅ COMPLETE (5 tickets created)
- **Phase 4**: Workflow integration (pending)
- **Phase 5**: Retroactive cleanup (pending)

**Known Deferred Work**:
- PR #1: 0 findings (no bot comments)
- PR #2: 145 findings (highest volume)
- PR #4: 0 findings (cleanest)
- PR #6: 38 findings
- PR #8: 53 findings
- **Total**: 236 findings across 5 PRs

**Systemic Patterns Identified**:
1. Hardcoded secrets (36 occurrences) - P0 CRITICAL
2. Incomplete rollback logic (12 occurrences) - P1
3. Missing test coverage (24 occurrences) - P2
4. Allocation violations (29 occurrences) - P2
5. Build artifacts (5 occurrences) - P2

**Timeline**:
- **Sprint 1 (Weeks 1-2)**: Infrastructure + Audit ✅ COMPLETE
- **Sprint 2 (Weeks 3-4)**: Integration + P0 Fixes (READY TO START)
- **Sprint 3+ (Weeks 5-10)**: P1/P2 Cleanup (phased)

## EPIC-7: Quality & Technical Debt Reduction

**Status:** 🟢 ACTIVE - Tickets Created (Phase 3 Complete)
**Priority:** P0-P3 (Mixed)
**Effort:** 29.5-45 hours total
**Created:** 2026-05-24

### Overview
Consolidation of deferred bot findings from PRs #1, #2, #4, #6, #8 into actionable quality improvement tickets.

### Tickets (5 Total)

1. **TICKET-001** (P0 CRITICAL) - Remove Hardcoded Secrets
   - 36 hardcoded secrets requiring immediate rotation
   - Effort: 8-12 hours
   - Status: Ready for execution

2. **TICKET-002** (P1 HIGH) - Complete Circuit Breaker Rollback
   - 12 incomplete rollback instances causing memory leaks
   - Effort: 4-6 hours
   - Status: Ready for execution

3. **TICKET-003** (P2 MEDIUM) - Add Missing Test Coverage
   - 24 missing test cases for critical paths
   - Effort: 16-24 hours
   - Status: Ready for execution

4. **TICKET-004** (P3 LOW) - Fix StyleCop Violations
   - 2 StyleCop violations requiring auto-fix
   - Effort: 1-2 hours
   - Status: Ready for execution

5. **TICKET-005** (P2 MEDIUM) - Clean Up Build Artifacts
   - 5 `.extracted.py` files causing repository bloat
   - Effort: 1 hour
   - Status: Ready for execution

### Execution Order (Recommended)
1. TICKET-001 (P0) - Security first
2. TICKET-002 (P1) - Fix memory leaks
3. TICKET-005 (P2) - Quick cleanup win
4. TICKET-003 (P2) - Comprehensive testing
5. TICKET-004 (P3) - Style polish

### References
- Audit: [`docs/brain/DEFERRED_WORK_AUDIT.md`](docs/brain/DEFERRED_WORK_AUDIT.md)
- Tickets: [`docs/brain/EPIC-7-QUALITY/`](docs/brain/EPIC-7-QUALITY/)
- Tracking: [`conductor/tracks/bot_findings_consolidation_20260524/`](conductor/tracks/bot_findings_consolidation_20260524/)


#### A3: PR Workflow Hardening 🔄 IN PROGRESS
**Track**: `conductor/tracks/pr_workflow_hardening_20260522/`  
**Status**: PLANNING → IMPLEMENTATION  
**Effort**: 16 hours  
**Dependencies**: Hook System (complete)

**Scope**:
- Fix "Dirty Branch" violations (auto-rebase)
- Automate `/pr-loop` at Epic completion
- Mandatory Phase 6 (PR Perfection) in epic-run.md
- PHS 100/100 gate before Epic completion

**Deliverables**:
- Updated `scripts/verify_pr_hygiene.ps1` (actionable error messages)
- Updated `.bob/commands/pr-loop.md` (auto-rebase in Step 0)
- Updated `.bob/commands/epic-run.md` (mandatory Phase 6)
- New `.bob/rules/00-pr-hygiene.md` (project-wide mandate)

**Timeline**: Week 1-2 (parallel with Bot Findings Phase 0-1)

---

### Stream B: Performance & Architecture (P2 - DEFERRED)

**Goal**: Complete EPIC-5 performance work and SIMA extraction

#### B1: SIMA Routa Pilot ⏸️ PAUSED
**Track**: `conductor/tracks/sima_routa_pilot/`  
**Status**: PAUSED (waiting for EPIC-5 completion)  
**Effort**: 40 hours  
**Dependencies**: EPIC-5 ticket-06 (RMA proximity monitoring)

**Scope**: Extract SIMA subgraph using Routa CLI for multi-file refactoring

**Resume Trigger**: After EPIC-5 ticket-06 completion

#### B2: Verification 1101e ⏸️ PAUSED
**Track**: `conductor/tracks/verification_1101e_20260317/`  
**Status**: PAUSED  
**Effort**: 24 hours

**Scope**: Verification protocols for Build 1101e

**Resume Trigger**: After Stream A completion (quality baseline established)

---

### Stream C: Bug Fixes & Repairs (P3 - BACKLOG)

**Goal**: Address legacy issues and technical debt

#### C1: Phase 8 Repair ⏸️ PAUSED
**Track**: `conductor/tracks/phase8_repair_20260317/`  
**Status**: PAUSED  
**Effort**: 32 hours

#### C2: FSM Multi-Target Fix ⏸️ PAUSED
**Track**: `conductor/tracks/fsm_multitarget_fix_20260318/`  
**Status**: PAUSED  
**Effort**: 16 hours

#### C3: GTC Sweep Fix ⏸️ PAUSED
**Track**: `conductor/tracks/gtc_sweep_fix_20260318/`  
**Status**: PAUSED  
**Effort**: 12 hours

**Resume Trigger**: After Stream A + B completion

---

## 3. Execution Priority

### Phase 1: Critical Path (Weeks 1-4) - P0
**Focus**: Infrastructure + Quality Foundation

1. **Week 1**: Bot Findings Forensic Audit (Phase 0)
   - Extract PRs #1, #4 findings
   - Verify PRs #2, #6, #8 status
   - Create master registry (71+ findings)

2. **Week 2**: Tracking Infrastructure (Phase 1)
   - Deploy BOT_FINDINGS_PROTOCOL.md
   - Create extraction scripts
   - Integrate into PR loop + Epic run

3. **Week 3**: PR Workflow Hardening
   - Auto-rebase implementation
   - Mandatory Phase 6 in epic-run.md
   - PHS 100/100 gate

4. **Week 4**: P0 Bot Findings Fixes
   - Concurrency bugs (PR #6)
   - IPC hot path allocations (PR #2)
   - Non-atomic file operations (PR #2)

### Phase 2: Quality Improvement (Weeks 5-10) - P1
**Focus**: Systemic Pattern Resolution

5. **Weeks 5-6**: P1 Bot Findings (Part 1)
   - DateTime UTC kind audit
   - Unused variable cleanup
   - Error handling standardization (phase 1)

6. **Weeks 7-10**: Complexity Reduction Campaign
   - Target: <15% files exceeding CYC 15 (from 32%)
   - Refactor 2 files per week
   - Codacy Grade A target

### Phase 3: Performance & Architecture (Weeks 11-16) - P2
**Focus**: EPIC-5 Completion + SIMA Extraction

7. **Weeks 11-12**: EPIC-5 Ticket-06 (RMA Proximity)
8. **Weeks 13-16**: SIMA Routa Pilot

### Phase 4: Backlog Cleanup (Weeks 17+) - P3
**Focus**: Legacy Issues

9. **Weeks 17+**: Stream C (Phase 8, FSM, GTC fixes)

---

## 4. Integration Points

### Hook System ↔ Bot Findings
- **pre-pr-loop.ps1**: Blocks push if bot findings not extracted
- **post-pr-loop.ps1**: Auto-generates PR summary after PHS 100/100
- **post-epic-ticket.ps1**: Validates Droid Mission completion

### Bot Findings ↔ PR Workflow
- **Step 0.5 in pr-loop.md**: Mandatory bot findings extraction
- **Phase 6.5 in epic-run.md**: Mandatory findings review before Epic completion
- **PHS 100/100 gate**: No merge until all P0/P1 findings resolved

### PR Workflow ↔ Hook System
- **pre-push hook**: Enforces PR hygiene (rebase, clean branch)
- **pre-forensics.ps1**: Captures PR metadata before bot analysis
- **pre-deploy-sync.ps1**: Verifies build readiness before NT8 sync

---

## 5. Success Metrics

### Infrastructure Metrics (Stream A)
- **Hook Coverage**: 100% of critical workflows have hooks
- **Bot Tracking**: 100% of PRs have findings extracted
- **Pattern Detection**: ≥80% of recurring issues identified
- **PHS Achievement**: ≥90% of PRs reach 100/100 PHS

### Quality Metrics (Bot Findings)
- **Deferred Work**: 0 P0 findings deferred past PR merge
- **Complexity**: <15% files exceeding CYC 15 (from 32%)
- **Codacy Score**: Grade A (from B)
- **Resolution Rate**: ≥70% of P0/P1 tickets resolved within 2 sprints

### Performance Metrics (Stream B)
- **EPIC-5 Completion**: All tickets closed
- **SIMA Extraction**: Subgraph isolated with <5% coupling
- **Benchmark Lock-In**: All performance gains verified

---

## 6. Risk Management

### Risk 1: Overwhelming Backlog (71+ findings)
**Mitigation**: Strict P0 → P1 → P2 prioritization, phased execution
**Contingency**: Defer P2 to background tasks, focus on P0/P1

### Risk 2: Retroactive Fixes Break Production
**Mitigation**: Feature branches + full CI + F5 verification
**Contingency**: Git revert + emergency hotfix process

### Risk 3: Workflow Overhead
**Mitigation**: Automate extraction (≤30s), integrate into existing hooks
**Contingency**: Make extraction async (post-merge background job)

### Risk 4: Resource Constraints
**Mitigation**: Parallel execution where possible (A1 + A3)
**Contingency**: Extend timeline, prioritize P0 only

---

## 7. Decision Gates

### Gate 1: After PR #8 Merge
**Decision**: Start Bot Findings Consolidation (Phase 0)  
**Criteria**: PR #8 achieves PHS 100/100

### Gate 2: After Phase 0 Audit
**Decision**: Approve EPIC-7-QUALITY tickets  
**Criteria**: Master registry complete, systemic patterns identified

### Gate 3: After P0 Fixes
**Decision**: Proceed to P1 or pause for EPIC-5  
**Criteria**: All P0 findings resolved, Codacy score improved

### Gate 4: After Stream A Complete
**Decision**: Resume Stream B (SIMA) or continue Stream A (P2)  
**Criteria**: Quality baseline established, no P0/P1 backlog

---

## 8. Communication Protocol

### Weekly Status Updates
**Format**: `docs/brain/weekly_status_YYYY-MM-DD.md`  
**Content**:
- Completed work (by stream)
- In-progress work (by stream)
- Blocked work (with blockers)
- Next week priorities

### Monthly Retrospectives
**Format**: `docs/brain/retrospective_YYYY-MM.md`  
**Content**:
- Metrics review (success criteria)
- Process improvements
- Lessons learned
- Roadmap adjustments

### Ad-Hoc Updates
**Trigger**: Major milestone completion, critical blocker, priority shift  
**Channel**: Update this document + notify Director

---

## 9. Appendix: Track Details

### A. Hook Injection System (COMPLETE)
**Location**: `conductor/tracks/hook_injection_20260524/`  
**Documentation**: `docs/protocol/HOOKS.md`  
**Status**: ✅ DEPLOYED

### B. Bot Findings Consolidation (PLANNING)
**Location**: `conductor/tracks/bot_findings_consolidation_20260524/`  
**Documentation**: `docs/protocol/BOT_FINDINGS_PROTOCOL.md` (pending)  
**Status**: 📋 PLANNING

### C. PR Workflow Hardening (IN PROGRESS)
**Location**: `conductor/tracks/pr_workflow_hardening_20260522/`  
**Documentation**: `.bob/rules/00-pr-hygiene.md`  
**Status**: 🔄 IN PROGRESS

### D. SIMA Routa Pilot (PAUSED)
**Location**: `conductor/tracks/sima_routa_pilot/`  
**Status**: ⏸️ PAUSED

### E. Verification 1101e (PAUSED)
**Location**: `conductor/tracks/verification_1101e_20260317/`  
**Status**: ⏸️ PAUSED

### F. Phase 8 Repair (PAUSED)
**Location**: `conductor/tracks/phase8_repair_20260317/`  
**Status**: ⏸️ PAUSED

### G. FSM Multi-Target Fix (PAUSED)
**Location**: `conductor/tracks/fsm_multitarget_fix_20260318/`  
**Status**: ⏸️ PAUSED

### H. GTC Sweep Fix (PAUSED)
**Location**: `conductor/tracks/gtc_sweep_fix_20260318/`  
**Status**: ⏸️ PAUSED

---

## 10. Quick Reference

### Current Sprint Focus (Week 1)
1. ✅ Hook System Deployment (COMPLETE)
2. 📋 Bot Findings Forensic Audit (START AFTER PR #8 MERGE)
3. 🔄 PR Workflow Hardening (IN PROGRESS)

### Next Sprint Focus (Week 2)
1. Bot Findings Tracking Infrastructure
2. PR Workflow Hardening Completion
3. P0 Bot Findings Fixes (Start)

### Commands
```powershell
# Check roadmap status
Get-Content conductor/MASTER_ROADMAP.md | Select-String -Pattern "Status:"

# View active tracks
Get-ChildItem conductor/tracks -Directory | Where-Object { $_.Name -notlike "*2026031*" }

# Check bot findings
Get-Content docs/brain/pr_*_fix_queue.md | Select-String -Pattern "^\[.\]" | Measure-Object

# Run hook diagnostics
powershell -File scripts/manage-hooks.ps1 diagnostics
```

---

**Last Review**: 2026-05-24  
**Next Review**: After PR #8 merge  
**Owner**: Orchestrator (Bob CLI)  
**Approver**: Director