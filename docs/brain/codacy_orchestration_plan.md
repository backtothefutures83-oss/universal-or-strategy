# Codacy Issue Orchestration Plan

**Date**: 2026-05-28
**Orchestrator**: Antigravity (Gemini CLI)
**Strategy**: Two-phase parallel agent dispatch

---

## Phase 1: Recent PR Regression Fixes (PRIORITY)

**Goal**: Fix new issues introduced in recent PRs to prevent quality degradation.

### Workflow
1. **User provides**: Codacy issue list for each recent PR (one at a time)
2. **Orchestrator analyzes**: Categorize by severity, file, and pattern
3. **Agent dispatch**: Spawn independent agents per PR or per file cluster
4. **Verification**: Each agent runs pre-push validation before completion

### Agent Selection Criteria
- **Bob CLI** (`v12-engineer`): For src/ files requiring architectural judgment
- **Advanced Mode** (`advanced`): For non-src/ files or simple fixes
- **Codex CLI** (`codex-rescue`): For complex logic hardening (if Bob delegates)

### Recent PRs to Audit (User will provide issues)
- PR #16: BarsRequiredToTrade + SignalBroadcaster fixes
- PR #6: EPIC-5-PERF LINQ optimization
- PR #4: EPIC-5 Performance (43M+ allocations eliminated)
- PR #2: EPIC-4 Sticky State & IPC Hardening
- Any other recent PRs with new issues

---

## Phase 2: Baseline 2k Issue Reduction

**Goal**: Chip away at the original 1,943 Codacy issues using Boy Scout Rule.

### Current Baseline (2026-05-22)
- **Total Issues**: 3,100 (includes technical debt)
- **Grade**: B
- **Breakdown**:
  - Security: 29 (HIGHEST PRIORITY)
  - Error-prone: 1,000+
  - Complexity: 288 (32% of files exceed CYC 15)
  - Style: 1,000+

### Strategy
- **Prioritization**: Security > Error-prone > Complexity > Style
- **Batch Size**: 50-100 issues per agent task
- **File Clustering**: Group issues by file to minimize context switching
- **Incremental**: Fix issues in files touched during other work (Boy Scout Rule)

### Agent Dispatch Pattern
```
For each batch:
1. Extract issue cluster (by file or category)
2. Generate mini-spec with fix guidance
3. Spawn agent with:
   - Issue list
   - File context (via jcodemunch-mcp)
   - Fix patterns (from Jane Street KB)
   - Verification criteria
4. Agent executes fixes
5. Agent runs pre-push validation
6. Agent reports completion
7. Orchestrator tracks progress
```

---

## Agent Task Template

### Task Structure
```markdown
# Codacy Fix Task: [Category] - [File/PR]

## Context
- **Source**: [PR #X / Baseline]
- **Files**: [List of files]
- **Issue Count**: [N issues]

## Issues
[Categorized list with severity, line numbers, descriptions]

## Fix Guidance
- **Pattern**: [Common fix pattern if applicable]
- **Jane Street Alignment**: [Relevant principles]
- **V12 DNA Constraints**: [Lock-free, ASCII-only, etc.]

## Verification Criteria
- [ ] All issues resolved in Codacy
- [ ] Pre-push validation passes (10/10)
- [ ] No new issues introduced
- [ ] Build successful (dotnet build)
- [ ] F5 test in NinjaTrader (if src/ changes)

## Completion
Report: [Summary of fixes, commit SHA, verification results]
```

---

## Orchestration Commands

### Spawn Agent (Bob CLI for src/)
```bash
bob /new-task --mode v12-engineer --message "$(cat task_spec.md)"
```

### Spawn Agent (Advanced Mode for non-src/)
```bash
# Via switch_mode tool in current session
<switch_mode>
<mode_slug>advanced</mode_slug>
<reason>Codacy fixes for non-src/ files</reason>
</switch_mode>
```

### Track Progress
```powershell
# Check Codacy dashboard
# https://app.codacy.com/gh/malhitticrypto-debug/universal-or-strategy/dashboard

# Local complexity audit
python scripts/complexity_audit.py

# Pre-push validation
powershell -File .\scripts\pre_push_validation.ps1
```

---

## Progress Tracking

### Phase 1: Recent PR Regressions - COMPLETE ✅

**Date Completed**: 2026-05-28
**PRs Analyzed**: 4 (PR #16, #14, #9, #8)
**Issues Fixed**: 76 (PR #16)
**Issues Deferred**: 375 (documented in `codacy_deferred_work_registry.md`)

| PR | Issues | Agent | Status | Commit |
|----|--------|-------|--------|--------|
| #16 | 76 fixed | Advanced | ✅ COMPLETE | `ac959f9b` |
| #14 | 5 non-existent | Advanced | ✅ VERIFIED | N/A |
| #9 | 17 deferred | Orchestrator | ⏸️ DEFERRED | N/A |
| #8 | 353 deferred | Orchestrator | ⏸️ DEFERRED | N/A |

**Summary**:
- **Total Issues Identified**: 400+
- **Fixed**: 76 (PR #16 - 1 new + 75 opportunistic baseline cleanup)
- **Deferred**: 375 (17 breaking changes + 29 threshold mismatch + 324 false positives + 5 non-existent)
- **Net Improvement**: -7 issues (360 resolved, 353 false positives introduced)
- **Quality Gate**: Codacy shows "Up to quality standards" ✅

**Documentation**:
- Complete analysis: `docs/brain/codacy_pr_analysis_complete.md`
- Deferred work registry: `docs/brain/codacy_deferred_work_registry.md`

**Next**: Phase 2 - Baseline 2k Issue Reduction

### Phase 2: Baseline Reduction
| Category | Total | Fixed | Remaining | Progress |
|----------|-------|-------|-----------|----------|
| Security | 29 | 0 | 29 | 0% |
| Error-prone | 1000+ | 0 | 1000+ | 0% |
| Complexity | 288 | 0 | 288 | 0% |
| Style | 1000+ | 0 | 1000+ | 0% |

---

## Next Steps

1. **User Action**: Paste Codacy issues for first PR
2. **Orchestrator**: Analyze and create task spec
3. **Orchestrator**: Spawn appropriate agent
4. **Agent**: Execute fixes and verify
5. **Orchestrator**: Update progress tracking
6. **Repeat**: For each PR, then move to Phase 2

---

## Notes

- **Parallel Execution**: Multiple agents can work on different PRs simultaneously
- **Quality Gates**: Every agent must pass pre-push validation before completion
- **Documentation**: Each fix batch documented in `docs/brain/codacy_fix_[category]_[date].md`
- **Rollback Safety**: Each agent works on a feature branch with checkpointing enabled