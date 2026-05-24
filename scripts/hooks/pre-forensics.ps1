# Pre-Forensics Hook
# Verifies bot comment freshness before extraction
# Purpose: Prevent PR #8 scenario (380-line drift from stale bot comments)
# Exit Behavior: Halt if staleness >50%

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber
)

$ErrorActionPreference = "Stop"

Write-Host "[PRE-FORENSICS] Verifying bot comment freshness..." -ForegroundColor Yellow

# 1. Get current HEAD commit
$currentHead = git rev-parse HEAD
Write-Host "  Current HEAD: $currentHead" -ForegroundColor Cyan

# 2. Extract all file references from bot comments
$rawFile = "pr_${PrNumber}_raw.json"

if (-not (Test-Path $rawFile)) {
    Write-Host "[PRE-FORENSICS] ERROR: Raw PR data not found: $rawFile" -ForegroundColor Red
    Write-Host "ACTION: Run 'gh pr view $PrNumber --json comments,reviews,statusCheckRollup > $rawFile' first" -ForegroundColor Yellow
    exit 1
}

try {
    $prData = Get-Content $rawFile -Raw | ConvertFrom-Json
} catch {
    Write-Host "[PRE-FORENSICS] ERROR: Failed to parse $rawFile" -ForegroundColor Red
    Write-Host "Details: $_" -ForegroundColor Red
    exit 1
}

$targetedFiles = @()

# Process comments
if ($prData.comments) {
    foreach ($comment in $prData.comments) {
        # Extract file paths from comment body (regex: src/.*\.cs)
        $matches = [regex]::Matches($comment.body, 'src/[^\s\)]+\.cs')
        foreach ($match in $matches) {
            $filePath = $match.Value
            if ($targetedFiles -notcontains $filePath) {
                $targetedFiles += $filePath
            }
        }
    }
}

# Process reviews
if ($prData.reviews) {
    foreach ($review in $prData.reviews) {
        if ($review.body) {
            $matches = [regex]::Matches($review.body, 'src/[^\s\)]+\.cs')
            foreach ($match in $matches) {
                $filePath = $match.Value
                if ($targetedFiles -notcontains $filePath) {
                    $targetedFiles += $filePath
                }
            }
        }
    }
}

Write-Host "  Found $($targetedFiles.Count) unique file references in bot comments" -ForegroundColor Cyan

# 3. Verify each file exists in current HEAD
$staleCount = 0
$totalCount = $targetedFiles.Count

if ($totalCount -eq 0) {
    Write-Host "[PRE-FORENSICS] INFO: No file references found in bot comments" -ForegroundColor Yellow
    Write-Host "[PRE-FORENSICS] PASS: Nothing to verify" -ForegroundColor Green
    exit 0
}

foreach ($file in $targetedFiles) {
    if (-not (Test-Path $file)) {
        Write-Host "  [STALE] $file (referenced by bot, not in HEAD)" -ForegroundColor Red
        $staleCount++
    }
}

# 4. Calculate staleness percentage
$stalenessPercent = if ($totalCount -gt 0) { 
    [math]::Round(($staleCount / $totalCount) * 100, 2) 
} else { 
    0 
}

Write-Host "`n[PRE-FORENSICS] Staleness: $stalenessPercent% ($staleCount/$totalCount files)" -ForegroundColor $(
    if ($stalenessPercent -gt 50) { "Red" } 
    elseif ($stalenessPercent -gt 30) { "Yellow" } 
    else { "Green" }
)

# 5. Exit decision
if ($stalenessPercent -gt 50) {
    Write-Host "[PRE-FORENSICS] FAIL: >50% staleness detected" -ForegroundColor Red
    Write-Host "ACTION: Wait for bots to re-analyze current HEAD, then retry" -ForegroundColor Yellow
    Write-Host "TIP: Push a trivial commit to trigger bot re-analysis" -ForegroundColor Yellow
    exit 1
}

if ($stalenessPercent -gt 30) {
    Write-Host "[PRE-FORENSICS] WARN: 30-50% staleness detected" -ForegroundColor Yellow
    Write-Host "RECOMMENDATION: Consider waiting for bot re-analysis" -ForegroundColor Yellow
    Write-Host "Proceeding with caution..." -ForegroundColor Yellow
}

Write-Host "[PRE-FORENSICS] PASS: Bot comments are fresh" -ForegroundColor Green
exit 0

# Made with Bob
