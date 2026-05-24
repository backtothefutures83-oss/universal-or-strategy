# Pre-PR-Loop Hook
# Verifies branch state before starting PR perfection loop
# Purpose: Prevent PR loop on dirty/outdated branches
# Exit Behavior: Halt if branch is dirty or behind main

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber
)

$ErrorActionPreference = "Stop"

Write-Host "[PRE-PR-LOOP] Verifying branch state for PR #$PrNumber..." -ForegroundColor Yellow

# 1. Verify branch is clean (no uncommitted changes)
Write-Host "  [1/2] Checking for uncommitted changes..." -ForegroundColor Cyan
$gitStatus = git status --porcelain 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[PRE-PR-LOOP] ERROR: Git status check failed" -ForegroundColor Red
    Write-Host "Details: $gitStatus" -ForegroundColor Red
    Write-Host "ACTION: Verify you are in a git repository" -ForegroundColor Yellow
    exit 1
}

if ($gitStatus) {
    Write-Host "[PRE-PR-LOOP] FAIL: Branch has uncommitted changes" -ForegroundColor Red
    Write-Host "Uncommitted files:" -ForegroundColor Yellow
    $gitStatus | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Commit or stash changes before starting PR loop" -ForegroundColor Yellow
    exit 1
}
Write-Host "  [1/2] Branch: CLEAN" -ForegroundColor Green

# 2. Verify branch is rebased on origin/main
Write-Host "  [2/2] Checking rebase status..." -ForegroundColor Cyan
try {
    git fetch origin main --quiet 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[PRE-PR-LOOP] WARN: Could not fetch origin/main" -ForegroundColor Yellow
        Write-Host "Skipping rebase check (non-blocking)..." -ForegroundColor Yellow
    } else {
        $mergeBase = git merge-base HEAD origin/main 2>&1
        $mainTip = git rev-parse origin/main 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[PRE-PR-LOOP] WARN: Could not determine merge base" -ForegroundColor Yellow
            Write-Host "Skipping rebase check (non-blocking)..." -ForegroundColor Yellow
        } elseif ($mergeBase -ne $mainTip) {
            Write-Host "[PRE-PR-LOOP] FAIL: Branch is not rebased on origin/main" -ForegroundColor Red
            Write-Host "  Merge base: $mergeBase" -ForegroundColor Yellow
            Write-Host "  Main tip:   $mainTip" -ForegroundColor Yellow
            Write-Host "ACTION: Run 'git fetch origin main && git rebase origin/main'" -ForegroundColor Yellow
            exit 1
        } else {
            Write-Host "  [2/2] Rebase: CURRENT" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "[PRE-PR-LOOP] WARN: Rebase check error: $_" -ForegroundColor Yellow
    Write-Host "Proceeding anyway (non-blocking)..." -ForegroundColor Yellow
}

Write-Host "[PRE-PR-LOOP] PASS: Branch ready for PR loop" -ForegroundColor Green
exit 0

# Made with Bob
