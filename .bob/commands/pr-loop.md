# pr-loop

description: Repeatable 100/100 Perfection Loop V2. Iteratively repairs and verifies code until the Project Health Score is 100/100. Includes mandatory Bot Forensics extraction before fixes.

## Usage

```
/pr-loop <PR_NUMBER>
```

**Example:**
```
/pr-loop 6
```

## Protocol

You are the V12 Perfection Orchestrator. You MUST NOT STOP until PHS is 100/100.

### ORCHESTRATION RULES

- **SCORE 100 MANDATE**: You are BANNED from merging or ending the loop if PHS < 100.
- **HYGIENE GATE**: You MUST pass Step 0 (Clean Branch & Diff Size) before every push.
- **FORENSICS FIRST**: You MUST extract bot findings (Step 1) before any fix attempts.
- **LOCAL FIRST**: You must achieve Local Score 15/15 before every push.
- **FORENSIC AUDIT**: Every failure must be categorized as [VALID], [HALLUCINATION], [INFRA-NOISE], or [ACCESS_BLOCKED].
- **F5 GATE**: The only manual action is the final NinjaTrader verification at Score 100.

---

## THE PERFECTION CYCLE

### Step -1: PR Existence Check (NEW - MANDATORY)

**Switch to: Advanced mode**

Hand off:
```
TASK: Check if PR Already Exists
PR: $1
PROTOCOL:
  1. Run: gh pr view $1 --json headRefName --jq '.headRefName'
  2. If PR exists (exit code 0):
     - Checkout the existing branch: git checkout <branch_name>
     - Emit: [PR-EXISTS] Checked out branch <branch_name>
     - Skip Step 0, proceed to Step 1 (Pre-Flight Hygiene)
  3. If PR doesn't exist (exit code 1):
     - Emit: [PR-NEW] PR does not exist yet
     - Proceed to Step 0 (create new branch)
```

**Gate:**
- If PR exists: Skip Step 0, proceed to Step 1
- If PR doesn't exist: Proceed to Step 0

**Rationale:** Prevents branch confusion when resuming work on existing PRs. Ensures fixes are pushed to the correct branch.

---

### Step 0: Pre-Flight Hygiene (MANDATORY)

**Switch to: Advanced mode**

Hand off:
```
TASK: Verify PR Hygiene
PROTOCOL:
  IF PR is new (from Step -1):
    1. Create branch: git checkout -b <branch_name>
    2. Run `git fetch origin main && git rebase origin/main`.
    3. Run `powershell -File .\scripts\verify_pr_hygiene.ps1`.
  IF PR already exists (from Step -1):
    1. Run `git fetch origin main && git rebase origin/main`.
    2. Run `powershell -File .\scripts\verify_pr_hygiene.ps1`.
  4. If FAIL: HALT and report the violation (e.g. "Diff > 10k" or "Branch is dirty").
  5. If PASS: Advance to Step 1.
```

---

### Step 1: Bot Forensics (MANDATORY - NEW IN V2)

**Switch to: Advanced mode**

Hand off:
```
TASK: Extract and Categorize Bot Findings
PR: $1
PROTOCOL:
  1. Run: powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber $1
  2. Read the generated forensics report: docs/brain/pr_$1_forensics.md
  3. Present summary to Director:
     - Total VALID issues (P0/P1/P2 breakdown)
     - Hallucinations detected
     - INFRA-NOISE filtered
  4. If P0 issues exist: Flag as CRITICAL and proceed to Step 2.
  5. If no VALID issues: Skip to Step 3 (verification only).
  6. Emit: [FORENSICS-READY] X VALID issues, Y hallucinations
```

**Outputs:**
- `docs/brain/pr_$1_forensics.md` - Full categorized findings
- `docs/brain/pr_$1_fix_queue.md` - Priority-ordered fix list
- `docs/brain/bot_hallucinations.md` - Updated hallucination log

**Gate:** Review forensics report. If P0 issues exist, they MUST be fixed before proceeding.

---

### Step 2: Local Repair

**Switch to: v12-engineer mode**

Hand off:
```
TASK: Fix VALID Issues from Forensics Report
INPUT: @docs/brain/pr_$1_fix_queue.md
PROTOCOL:
  1. Read fix queue completely.
  2. For each VALID issue (P0 first, then P1, then P2):
     - Apply fix
     - Verify locally (compile, test)
     - Mark as [x] FIXED in fix queue
  3. Run formatters: powershell -File .\scripts\format_all_csharp.ps1
  4. Run local verification: powershell -File .\scripts\calculate_fleet_score.ps1
  5. If Score < 15: identify remaining issues, repeat Step 2.
  6. If Score = 15: emit [LOCAL-READY] with fix summary.
```

**Gate:** Local Score = 15/15. If < 15, repeat Step 2.

---

### Step 3: Global Push & Monitor

**Switch to: Advanced mode**

Hand off:
```
TASK: Global Audit & Monitor
PR: $1
PROTOCOL:
  0. DELETE old forensics files (prevent stale reads):
     Remove-Item docs/brain/pr_$1_forensics.md -ErrorAction SilentlyContinue
     Remove-Item docs/brain/pr_$1_fix_queue.md -ErrorAction SilentlyContinue
  1. powershell -File .\deploy-sync.ps1 (MANDATORY - syncs NT8 hard links)
  2. git add . && git commit -m "fix: PHS Perfection Loop - PR #$1" && git push
  3. monitor_pr_checks $1 (Wait for all bots)
     - **MANDATORY SLEEP**: Start-Sleep -Seconds 300 (5 min) for first check
     - **SUBSEQUENT SLEEP**: Start-Sleep -Seconds 180 (3 min) if checks pending
  4. Run: powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber $1
  5. VERIFY extraction:
     - Test-Path docs/brain/pr_$1_forensics.md (must exist)
     - File age < 2 minutes (must be fresh)
  6. Run: powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber $1
  7. If Score < 100: emit [PHS-RETRY] Current: X/100
  8. If Score = 100: emit [PHS-PERFECT] 100/100
```

**Gate:**
- If [PHS-RETRY]: **RESTART at Step 1** (re-extract forensics for new findings).
- If [PHS-PERFECT]: **Advance to Step 4**.

---

### Step 4: Manual Override Gate (NEW IN V2)

**Mode:** Orchestrator  
**Trigger:** PHS < 100 after 3+ iterations

**Protocol:**
1. Present current PHS and remaining issues to Director.
2. Classify remaining issues:
   - VALID but low-priority (P2 style issues)
   - Hallucinations not yet logged
   - INFRA-NOISE
3. Ask Director: "PHS is X/100. Remaining issues: [list]. Approve merge? (YES/NO)"

**Director Options:**
- **YES**: Proceed to Step 5 (F5 Gate)
- **NO**: Provide guidance, restart at Step 1
- **DEFER**: Create follow-up ticket, proceed to Step 5

---

### Step 5: Final F5 Verification

**Mode:** Orchestrator  
**Action:** Director presses F5 in NinjaTrader

Output:
```
[F5-GATE] PR #$1 - PHS <SCORE>/100
All automated gates: PASSED/APPROVED
Remaining issues: [list if <100]

ACTION REQUIRED: Press F5 in NinjaTrader IDE.
When you see the BUILD_TAG banner, type: F5 done [BUILD_TAG]
```

**Gate:** Wait for Director confirmation.

---

## FINAL HANDSHAKE

Once 100/100 is achieved (or Director approves <100), output:

```
[PHS-PERFECT] PR #$1 - Ready for Merge
============================================================
PHS Score       : <SCORE>/100
VALID Issues    : <COUNT> (all fixed or approved)
Hallucinations  : <COUNT> (logged)
INFRA-NOISE     : <COUNT> (ignored)

Commits: [list of hashes]
============================================================
Branch ready for merge. Awaiting F5 verification.
```

---

## V2 Improvements Over V1

| Aspect | V1 (Old) | V2 (New) |
|--------|----------|----------|
| Bot Comment Reading | ❌ Never read | ✅ Mandatory extraction |
| Issue Categorization | ❌ None | ✅ VALID/HALLUCINATION/INFRA-NOISE |
| Hallucination Tracking | ❌ None | ✅ Persistent log |
| Fix Priority | ❌ Undefined | ✅ P0 → P1 → P2 |
| Manual Override | ❌ None | ✅ Director gate at <100 |
| Loop Efficiency | ❌ Blind retries | ✅ Forensics-guided |

---

## Reference Documentation

Full V2 workflow documentation: `docs/protocol/PR_LOOP_V2.md`
