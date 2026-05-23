# Workflow Enhancement: CI Log Extraction Integration

**Status**: Planning Phase  
**Created**: 2026-05-23  
**Priority**: P2 (Quality Enhancement)

## Problem Statement

Current workflows rely on bot interpretations (CodeRabbit, Codacy) which can miss details or hallucinate. PR #8 iteration 3 demonstrated that **CI logs provide ground truth** that bots miss.

## Scope: Commands to Update

1. **`/pr-loop`** - Add CI log extraction after bot forensics
2. **`/epic-run`** - Add CI log extraction in verification phase
3. **`/epic-tdd`** - Add CI log extraction for test failures
4. **`/repair-pr`** - Add CI log extraction as first diagnostic step

## Implementation Plan

### 1. Create Reusable Script
**File**: `scripts/extract_ci_logs.ps1`

**Functionality**:
- Fetch PR status checks via `gh pr view <PR> --json statusCheckRollup`
- Extract failed/action_required checks
- Fetch log content from GitHub Actions API
- Format as structured markdown with error excerpts and line numbers
- Save to `docs/brain/pr_<NUMBER>_ci_logs.md`

### 2. Update `/pr-loop` Command
**File**: `.bob/commands/pr-loop.md`

Add Step 1.5 after bot forensics:
```markdown
## Step 1.5: CI Log Extraction
powershell -File .\scripts\extract_ci_logs.ps1 -PrNumber <PR_NUMBER>
Review CI logs alongside bot forensics. Prioritize CI findings when conflicts arise.
```

### 3. Update `/epic-run` Command
**File**: `.bob/commands/epic-run.md`

Add to Stage 5 (Verification):
```markdown
If verification fails, extract CI logs before fix loop:
powershell -File .\scripts\extract_ci_logs.ps1 -PrNumber <PR_NUMBER>
```

### 4. Update `/epic-tdd` Command
**File**: `.bob/commands/epic-tdd.md`

Add to test failure analysis:
```markdown
When tests fail:
1. Extract CI logs: powershell -File .\scripts\extract_ci_logs.ps1 -PrNumber <PR_NUMBER>
2. Analyze stack traces in logs
3. Apply surgical fixes
```

### 5. Update `/repair-pr` Command
**File**: `.bob/commands/repair-pr.md`

Make CI log extraction the FIRST step:
```markdown
## Step 1: CI Log Extraction (FIRST STEP)
powershell -File .\scripts\extract_ci_logs.ps1 -PrNumber <PR_NUMBER>
Ground truth before bot analysis prevents fixing wrong issues.
```

### 6. Update Protocol Documentation
**File**: `docs/protocol/PR_LOOP_V2.md`

- Document CI log extraction as mandatory step
- Add guidance on prioritizing CI logs over bot interpretations
- Include PR #8 iteration 3 as case study

## Benefits

1. **Faster Root Cause**: 1-2 iterations vs 3-4 (ground truth vs bot interpretations)
2. **Higher Quality Fixes**: Fix actual CI failures, not assumed issues
3. **Reduced Waste**: Fewer assumption-based fix attempts
4. **Better Autonomy**: Agents have CI context, can self-correct
5. **Consistent Process**: Systematized vs ad-hoc log inspection

## Success Metrics

- **Iteration Reduction**: Target 30% fewer PR loop iterations
- **PHS Improvement**: Target 85+ PHS within 2 iterations
- **Time Savings**: Target 20% reduction in PR fix time

## Case Study: PR #8 Iteration 3

**Problem**: Iteration 2 fixes based on bot forensics failed CI  
**Solution**: Manual CI log inspection revealed:
- StyleCop SA1028 (spacing in generics)
- Benchmark project missing `<IsPackable>false</IsPackable>`

**Outcome**: Iteration 3 fixes based on CI logs → PHS 75/100 (up from 58/100)

**Lesson**: CI logs provided ground truth that bots missed.

## Implementation Timeline

- **Week 1**: Create `scripts/extract_ci_logs.ps1`, test on PR #8
- **Week 2**: Update all workflow commands
- **Week 3**: Update documentation, add to `AGENTS.md`
- **Week 4**: Validate on next PR, measure metrics

## Bot Scope Restriction

**Rationale**: All GitHub Apps (CodeRabbit, Codacy, Semgrep, Sourcery, DeepSource) should scan **ONLY** the `src/` directory to:
1. **Reduce Noise**: ~70% reduction in irrelevant findings from docs/scripts/tests
2. **Focus Quality**: Concentrate bot analysis on production code
3. **Faster Reviews**: Smaller scan surface = faster PR feedback
4. **Token Efficiency**: Reduce API costs for bot operations

**Configuration Files**:
- `.coderabbit.yaml` - Path filters to exclude non-src paths
- `.codacy.yml` - Exclude patterns for non-src directories
- `.semgrep.yml` - Path includes for src/ only
- `.sourcery.yaml` - Path filters for src/ only
- `.deepsource.toml` - Exclude patterns for non-src directories

**Documentation**: See `docs/setup/BOT_SCOPE_CONFIGURATION.md` for detailed configuration guide.

---

**[WORKFLOW-ENHANCEMENT-PLANNED]**