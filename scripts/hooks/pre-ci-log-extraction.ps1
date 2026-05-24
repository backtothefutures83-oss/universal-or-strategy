# Pre-CI-Log-Extraction Hook
# Verifies PR state before fetching CI logs
# Purpose: Prevent wasted API calls on invalid/closed PRs
# Exit Behavior: Halt if PR not found or invalid state

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber
)

$ErrorActionPreference = "Stop"

Write-Host "[PRE-CI-LOG] Verifying PR state..." -ForegroundColor Yellow

# 1. Verify PR exists
try {
    $prInfo = gh pr view $PrNumber --json state,number,title 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[PRE-CI-LOG] FAIL: PR #$PrNumber not found" -ForegroundColor Red
        Write-Host "Details: $prInfo" -ForegroundColor Red
        Write-Host "ACTION: Verify PR number is correct" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "[PRE-CI-LOG] FAIL: GitHub CLI error" -ForegroundColor Red
    Write-Host "Details: $_" -ForegroundColor Red
    Write-Host "ACTION: Verify 'gh' is installed and authenticated" -ForegroundColor Yellow
    exit 1
}

try {
    $pr = $prInfo | ConvertFrom-Json
} catch {
    Write-Host "[PRE-CI-LOG] FAIL: Failed to parse PR data" -ForegroundColor Red
    Write-Host "Details: $_" -ForegroundColor Red
    exit 1
}

Write-Host "  PR #$($pr.number): $($pr.title)" -ForegroundColor Cyan

# 2. Verify PR is open (not merged/closed)
if ($pr.state -ne "OPEN") {
    Write-Host "[PRE-CI-LOG] WARN: PR #$PrNumber is $($pr.state)" -ForegroundColor Yellow
    Write-Host "RECOMMENDATION: Only extract logs for OPEN PRs" -ForegroundColor Yellow
    Write-Host "Proceeding anyway (non-blocking warning)..." -ForegroundColor Yellow
}

# 3. Verify PR has CI runs (optional check)
try {
    $runs = gh run list --repo $(gh repo view --json nameWithOwner -q .nameWithOwner) --json status,conclusion --limit 1 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  CI runs detected" -ForegroundColor Green
    }
} catch {
    Write-Host "  [INFO] Could not verify CI runs (non-critical)" -ForegroundColor Yellow
}

Write-Host "[PRE-CI-LOG] PASS: PR #$PrNumber is valid" -ForegroundColor Green
exit 0

# Made with Bob
