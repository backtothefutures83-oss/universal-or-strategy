---
description: Split a mixed PR into separate src/ and non-src/ PRs
argument-hint: <PR_NUMBER>
---
# /pr-split - Split Mixed PR into Compliant PRs

**PR Number:** $1  
**Purpose:** Remediate PR Separation Protocol violations

## Overview

This command splits a PR that violates the separation protocol (contains both src/ and non-src/ files) into two compliant PRs:
1. **src-only PR**: Production code changes
2. **non-src-only PR**: Documentation, tests, workflows

## Usage

```
/pr-split <PR_NUMBER>
```

**Example:**
```
/pr-split 8
```

## Protocol

### Step 1: Verify Violation

**Switch to: Advanced mode**

Hand off:
```
TASK: Verify PR Separation Violation
PR: $1
PROTOCOL:
  1. Run: powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber $1
  2. If [PASS]: HALT - PR is already compliant, no split needed
  3. If [VIOLATION]: Continue to Step 2
  4. Emit: [VIOLATION-CONFIRMED] src=[N] non-src=[M]
```

### Step 2: Fetch PR File List

**Switch to: Advanced mode**

Hand off:
```
TASK: Fetch PR Files
PR: $1
PROTOCOL:
  1. Run: gh pr view $1 --json files --jq '.files[].path'
  2. Categorize files:
     - src_files: Files matching src/**
     - non_src_files: Files matching docs/, tests/, benchmarks/, scripts/, .github/, *.md
  3. Save lists to:
     - docs/brain/pr_$1_src_files.txt
     - docs/brain/pr_$1_non_src_files.txt
  4. Emit: [FILES-CATEGORIZED] src=[N] non-src=[M]
```

### Step 3: Close Original PR

**Switch to: Advanced mode**

Hand off:
```
TASK: Close Original PR
PR: $1
PROTOCOL:
  1. Add comment explaining split:
     "This PR violated the PR Separation Protocol (mixed src/ and non-src/ files).
      Closing and splitting into two compliant PRs:
      - src-only PR: [will be created]
      - non-src-only PR: [will be created]"
  2. Run: gh pr close $1
  3. Emit: [PR-CLOSED] PR #$1
```

### Step 4: Create src-only PR

**If src_files > 0, switch to: Advanced mode**

Hand off:
```
TASK: Create src-only PR
ORIGINAL_PR: $1
PROTOCOL:
  1. Create new branch: git checkout -b pr-$1-src-only
  2. Cherry-pick src/ changes from original PR
  3. Run: git add src/
  4. Run: git commit -m "fix: src/ changes from PR #$1 (split)"
  5. Run: gh pr create --title "[SPLIT] src/ changes from PR #$1" \
                       --body "Production code changes split from PR #$1.\n\nOriginal PR: #$1\nRelated: #<NON_SRC_PR>" \
                       --label "src-only"
  6. Extract <SRC_PR_NUMBER>
  7. Emit: [SRC-PR-CREATED] PR #<SRC_PR_NUMBER>
```

**Verification:**
```bash
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <SRC_PR_NUMBER>
```

Expected: `[PASS] PR contains ONLY src/ files`

### Step 5: Create non-src-only PR

**If non_src_files > 0, switch to: Advanced mode**

Hand off:
```
TASK: Create non-src-only PR
ORIGINAL_PR: $1
PROTOCOL:
  1. Create new branch: git checkout -b pr-$1-non-src-only
  2. Cherry-pick non-src/ changes from original PR
  3. Run: git add docs/ tests/ benchmarks/ scripts/ .github/ *.md
  4. Run: git commit -m "docs: non-src/ changes from PR #$1 (split)"
  5. Run: gh pr create --title "[SPLIT] non-src/ changes from PR #$1" \
                       --body "Documentation, tests, and workflow changes split from PR #$1.\n\nOriginal PR: #$1\nRelated: #<SRC_PR>" \
                       --label "non-src-only"
  6. Extract <NON_SRC_PR_NUMBER>
  7. Emit: [NON-SRC-PR-CREATED] PR #<NON_SRC_PR_NUMBER>
```

**Verification:**
```bash
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <NON_SRC_PR_NUMBER>
```

Expected: `[PASS] PR contains ONLY non-src/ files`

### Step 6: Link PRs

**Switch to: Advanced mode**

Hand off:
```
TASK: Link Split PRs
PROTOCOL:
  1. Update src-only PR description with non-src PR link
  2. Update non-src-only PR description with src PR link
  3. Add comment to original PR #$1:
     "Split complete:
      - src-only PR: #<SRC_PR>
      - non-src-only PR: #<NON_SRC_PR>"
  4. Emit: [SPLIT-COMPLETE]
```

### Step 7: Workflow Routing

**Output to Director:**

```
[PR-SPLIT-COMPLETE] Original PR #$1
============================================================
src-only PR     : #<SRC_PR_NUMBER> (requires /pr-loop)
non-src-only PR : #<NON_SRC_PR_NUMBER> (fast-track eligible)

NEXT STEPS:
1. Run /pr-loop <SRC_PR_NUMBER> to drive src/ PR to 100/100 PHS
2. Fast-track merge non-src/ PR after basic CI passes

Original PR #$1 has been closed.
============================================================
```

## Error Handling

### No Violation Detected

If `verify_pr_separation.ps1` returns `[PASS]`:

```
[ERROR] PR #$1 is already compliant
No split needed. PR contains only src/ OR only non-src/ files.
```

**Action:** HALT. No split required.

### Cherry-Pick Conflicts

If cherry-picking fails:

```
[ERROR] Cherry-pick conflict detected
Manual resolution required.

STEPS:
1. Resolve conflicts in affected files
2. Run: git add <resolved_files>
3. Run: git cherry-pick --continue
4. Resume split process
```

### PR Already Closed

If original PR is already closed:

```
[ERROR] PR #$1 is already closed
Cannot split a closed PR.

WORKAROUND:
1. Reopen PR: gh pr reopen $1
2. Run /pr-split $1 again
```

## Best Practices

### 1. Split Early

**Ideal timing:**
- Before any bot reviews
- Before CI runs complete
- Before code review starts

**Why:** Avoids wasted bot cycles on mixed PRs.

### 2. Preserve Commit History

**Strategy:**
- Use `git cherry-pick` to preserve original commits
- Maintain commit messages and authorship
- Link split PRs to original PR

### 3. Coordinate Merges

**Order:**
1. Merge src-only PR first (after /pr-loop to 100/100)
2. Merge non-src-only PR second (fast-track)

**Why:** Ensures production code is verified before documentation.

## Common Scenarios

### Scenario 1: Accidental Mix

**Situation:** Developer added docs in same commit as src/ changes.

**Solution:**
```bash
/pr-split <PR_NUMBER>
# Creates two clean PRs automatically
```

### Scenario 2: Epic Completion

**Situation:** Epic run created single PR with both src/ and non-src/.

**Solution:**
```bash
/pr-split <PR_NUMBER>
# Then run /pr-loop on src-only PR
# Fast-track merge non-src-only PR
```

### Scenario 3: Hotfix with Docs

**Situation:** Urgent src/ fix includes updated documentation.

**Solution:**
```bash
/pr-split <PR_NUMBER>
# Merge src-only PR immediately (after verification)
# Merge non-src-only PR later (no urgency)
```

## Verification Checklist

After split completion:

- [ ] Original PR closed with explanation comment
- [ ] src-only PR created and verified (PASS)
- [ ] non-src-only PR created and verified (PASS)
- [ ] PRs linked in descriptions
- [ ] /pr-loop started on src-only PR
- [ ] non-src-only PR queued for fast-track

## References

- [PR Separation Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md#pr-separation)
- [verify_pr_separation.ps1](scripts/verify_pr_separation.ps1)
- [/pr-loop](pr-loop.md)
- [/epic-run](epic-run.md)