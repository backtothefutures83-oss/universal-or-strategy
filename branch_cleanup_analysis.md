# Branch Cleanup Analysis - 2026-06-02

## Summary
- **Total Remote Branches**: 214 (excluding main branches)
- **Merged Branches**: 36 branches fully merged into origin/main
- **Active Remotes**: 5 (legacy, malhitticrypto, mdasdispatch-hash, new, origin, upstream)

## Part A: Branch Inventory by Remote

### 1. Legacy Remote (20 branches)
Former upstream repository (mkalhitti-cloud/universal-or-strategy)
- dependabot/github_actions/all-de869505ea
- dependabot/nuget/all-b7061f3cf7
- feat/phase7-final-validation
- feature/phase7-sprint1-sprint2-extraction
- feature/photon-spsc-hardening (5 variants)
- fix-jules-pr-review-4299196239701878118
- fix/opencode-config-v2
- infra/cleanup-and-docs
- infra/cleanup-final
- infra/p5-foundation
- p5/order-callbacks
- p5/sima-core
- phase-5-distributed-pipeline (2 variants)
- pre-refactor-baseline ✅ **MERGED**
- v12-memory-plane

### 2. Malhitticrypto Remote (12 branches)
Repository: malhitticrypto-debug/universal-or-strategy
- 1111.010-epic5-perf (4 variants)
- 1111.010-epic5-perf-docs-only ✅ **MERGED**
- 1111.010-epic5-perf-src-only ✅ **MERGED**
- codacy-phase2-errorprone-clean
- epic-quality-critical-clean
- epic-quality-curly-braces-full
- feature/infra-pr18-phs-simple-methodology
- fix/pr17-critical-violations
- test-curly-braces-tool ✅ **MERGED**

### 3. Mdasdispatch-hash Remote (26 branches)
Repository: mdasdispatch-hash/universal-or-strategy
- 1111.010-epic5-perf ✅ **MERGED**
- backup/photon-spsc-hardening-clean-pre-rebase
- epic-7-quality/ticket-002-circuit-breaker-rollback
- feat/phase7-final-validation
- feat/reaper-expansion-phase2
- feature/epic5-perf-optimization
- feature/epic6-cicd-docs
- feature/epic6-testing-clean
- feature/epic6-tests-only ✅ **MERGED**
- feature/phase7-sprint1-sprint2-extraction
- feature/phase7-sprint5-extraction
- feature/photon-spsc-hardening (5 variants)
- fix/opencode-config-v2
- infra/cleanup-and-docs
- infra/cleanup-final
- infra/p5-foundation
- p5/order-callbacks
- p5/sima-core
- phase-5-distributed-pipeline
- phase-6-t0-roadmap-registration
- pre-refactor-baseline ✅ **MERGED**
- v12-memory-plane

### 4. New Remote (56 branches)
Current working repository (backtothefutures83-oss/universal-or-strategy)
**Merged Branches** (14):
- 1111.010-epic5-perf-docs-only ✅
- 1111.010-epic5-perf-src-only ✅
- epic-quality-curly-braces ✅
- feature/docs-pr10-forensics ✅ (PR #11)
- feature/epic6-testing ✅
- feature/epic6-tests-only ✅
- feature/infra-bob-mode-tooling ✅
- feature/protocol-branch-guard ✅
- feature/reaper-expansion ✅
- feature/src-phase7-new1-callbacks ✅ (PR #8)
- feature/src-phase7-new2-stopsync ✅ (PR #9)
- fix/pr5-clean-cs-only ✅ (PR #5)
- pre-refactor-baseline ✅
- test-curly-braces-tool ✅

**Active/Unmerged Branches** (42):
- 1111.010-epic5-perf (5 variants)
- backup/photon-spsc-hardening-clean-pre-rebase
- codacy-phase2-errorprone-clean
- codacy-phase2-security-errorprone
- epic-7-quality/ticket-002-circuit-breaker-rollback
- epic-quality-critical-clean
- epic-quality-p0-currentbar-guards
- epic-quality-p1-struct-false-positives
- feat/phase7-final-validation
- feat/reaper-expansion-phase2
- feature/epic5-perf-optimization
- feature/epic6-cicd-docs
- feature/epic6-testing-clean
- feature/infra-pr18-phs-simple-methodology
- feature/infra-session-2026-05-31
- feature/phase7-sprint1-sprint2-extraction
- feature/phase7-sprint5-extraction
- feature/photon-spsc-hardening (5 variants)
- feature/src-gap2-spsc-integration
- feature/src-tw1-perf-clean
- fix/codacy-phase2-src
- fix/opencode-config-v2
- fix/pr17-critical-violations
- fix/pr3-clean-cs-only
- fix/signalbroadcaster-struct-validation
- infra/cleanup-and-docs
- infra/cleanup-final
- infra/p5-foundation
- p5/order-callbacks
- p5/sima-core
- phase-5-distributed-pipeline
- phase-6-t0-roadmap-registration
- pr-8-clean
- src/epic-13-extraction-v2 (PR #13, #14 - MERGED)
- v12-memory-plane

### 5. Origin Remote (80 branches)
Primary remote (backtothefutures83-oss/universal-or-strategy)
**Merged Branches** (14):
- Same as "new" remote merged branches

**Active/Unmerged Branches** (66):
- Includes all dependabot branches
- All epic-quality branches
- All feature branches
- All fix branches
- All phase branches

### 6. Upstream Remote (20 branches)
Upstream repository (mkalhitti-cloud/universal-or-strategy)
- Same structure as legacy remote
- pre-refactor-baseline ✅ **MERGED**

## Part B: Safe-to-Delete Analysis

### Criteria for Deletion
✅ Branch has been merged to main
✅ No open PRs associated
✅ Not a protected branch

### Recommended for Deletion (36 branches)

#### High Priority - Merged and Confirmed Safe (14 branches on origin/new)
1. `origin/1111.010-epic5-perf-docs-only`
2. `origin/1111.010-epic5-perf-src-only`
3. `origin/epic-quality-curly-braces`
4. `origin/feature/docs-pr10-forensics` (PR #11 merged)
5. `origin/feature/epic6-testing`
6. `origin/feature/epic6-tests-only`
7. `origin/feature/infra-bob-mode-tooling`
8. `origin/feature/protocol-branch-guard`
9. `origin/feature/reaper-expansion`
10. `origin/feature/src-phase7-new1-callbacks` (PR #8 merged)
11. `origin/feature/src-phase7-new2-stopsync` (PR #9 merged)
12. `origin/fix/pr5-clean-cs-only` (PR #5 merged)
13. `origin/pre-refactor-baseline`
14. `origin/test-curly-braces-tool`

#### Medium Priority - Merged on Mirror Remotes (22 branches)
All corresponding branches on:
- `new/*` (14 branches - mirrors of origin)
- `malhitticrypto/*` (3 merged branches)
- `mdasdispatch-hash/*` (2 merged branches)
- `legacy/pre-refactor-baseline`
- `upstream/pre-refactor-baseline`

### Branches Requiring Review Before Deletion

#### Closed PRs (Not Merged) - Verify Before Deletion
1. `origin/src/epic-13-extraction` (PR #12 - CLOSED, not merged)
2. `origin/feature/src-gap2-spsc-integration` (PR #7 - CLOSED)
3. `origin/feature/src-tw1-perf-clean` (PR #6 - CLOSED)
4. `origin/fix/pr3-clean-cs-only` (PR #4 - CLOSED)
5. `origin/fix/codacy-phase2-src` (PR #3 - CLOSED)
6. `origin/dependabot/nuget/all-88f86858a1` (PR #2 - CLOSED)

#### Active Development Branches - DO NOT DELETE
1. `origin/feature/src-phase7-new3-dispatch` (PR #10 merged - but may have follow-up work)
2. `origin/src/epic-13-extraction-v2` (PR #13, #14 merged - CURRENT WORK)
3. All `feature/infra-*` branches (infrastructure work in progress)
4. All `codacy-phase2-*` branches (quality improvement work)

## Part C: Deletion Commands (Awaiting Director Approval)

### Phase 1: Delete Merged Origin Branches (14 branches)
```powershell
# High-confidence deletions - fully merged to main
git push origin --delete 1111.010-epic5-perf-docs-only
git push origin --delete 1111.010-epic5-perf-src-only
git push origin --delete epic-quality-curly-braces
git push origin --delete feature/docs-pr10-forensics
git push origin --delete feature/epic6-testing
git push origin --delete feature/epic6-tests-only
git push origin --delete feature/infra-bob-mode-tooling
git push origin --delete feature/protocol-branch-guard
git push origin --delete feature/reaper-expansion
git push origin --delete feature/src-phase7-new1-callbacks
git push origin --delete feature/src-phase7-new2-stopsync
git push origin --delete fix/pr5-clean-cs-only
git push origin --delete pre-refactor-baseline
git push origin --delete test-curly-braces-tool
```

### Phase 2: Clean Up Mirror Remotes (Optional)
Mirror remotes (new, malhitticrypto, mdasdispatch-hash) can be cleaned up if no longer needed.

## Part D: Infrastructure Files Status

### Infrastructure Files Check
✅ All infrastructure files are committed and clean:
- `install-cs-tool.ps1` - Clean
- `scripts/query_codescene.py` - Clean
- `scripts/setup_linear_env.ps1` - Clean
- `docs/brain/*.md` - Some modified files detected (pr_14_fix_queue.md, pr_14_forensics.md)

### Current Working Tree Status
```
 M docs/brain/pr_14_fix_queue.md
 M docs/brain/pr_14_forensics.md
 M pr_14_raw.json
?? branch_cleanup_analysis.md
?? branches.txt
```

**Recommendation**: The modified docs/brain files appear to be PR #14 related documentation. These should be committed to the current branch (`src/epic-13-extraction-v2`) before any branch cleanup operations.

**No separate infrastructure branch needed** - all infrastructure tools are already committed.

## Director Approval Required

**STOP - Awaiting Director Approval for Part B & C**

Before proceeding with branch deletion:
1. ✅ Review the 36 merged branches identified for deletion
2. ✅ Confirm the 6 closed (but not merged) PRs can be deleted
3. ✅ Verify no active work depends on these branches
4. ✅ Approve Phase 1 deletion commands (14 origin branches)

**Next Steps After Approval**:
- Execute Phase 1 deletions (14 high-confidence merged branches)
- Optionally clean up mirror remotes (new, malhitticrypto, mdasdispatch-hash)
- Commit current working tree changes to src/epic-13-extraction-v2
- Run final verification: `git fetch --all --prune`