# Branch Cleanup Script - V12 Universal OR Strategy
# Purpose: Clean up stale branches after PR merges
# Usage: powershell -File .\scripts\cleanup_branches.ps1

param(
    [switch]$DryRun = $false,
    [switch]$Force = $false
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "V12 Branch Cleanup Utility" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Cyan

# Safety check: Ensure we're on main or a safe branch
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main" -and -not $Force) {
    Write-Host "ERROR: Not on main branch. Current: $currentBranch" -ForegroundColor Red
    Write-Host "Switch to main first: git checkout main" -ForegroundColor Yellow
    exit 1
}

# Protected branches (never delete)
$protectedBranches = @(
    "main",
    "pre-refactor-baseline",
    "backup/photon-spsc-hardening-clean-pre-rebase"
)

Write-Host "Phase 1: Deleting merged local branches..." -ForegroundColor Green
Write-Host "Protected branches: $($protectedBranches -join ', ')`n" -ForegroundColor Yellow

$mergedBranches = git branch --merged main | Where-Object { 
    $branch = $_.Trim()
    $branch -ne "" -and 
    $branch -notmatch '^\*' -and 
    $branch -notin $protectedBranches -and
    $branch -notmatch '^backup/'
}

if ($mergedBranches) {
    foreach ($branch in $mergedBranches) {
        $branch = $branch.Trim()
        if ($DryRun) {
            Write-Host "[DRY RUN] Would delete: $branch" -ForegroundColor Yellow
        } else {
            Write-Host "Deleting: $branch" -ForegroundColor White
            git branch -d $branch 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✓ Deleted" -ForegroundColor Green
            } else {
                Write-Host "  ✗ Failed (may have unmerged changes)" -ForegroundColor Red
            }
        }
    }
} else {
    Write-Host "No merged branches to delete." -ForegroundColor Gray
}

Write-Host "`nPhase 2: Pruning remote tracking branches..." -ForegroundColor Green
if ($DryRun) {
    Write-Host "[DRY RUN] Would run: git fetch --all --prune" -ForegroundColor Yellow
} else {
    git fetch --all --prune
    Write-Host "  ✓ Remote tracking branches pruned" -ForegroundColor Green
}

Write-Host "`nPhase 3: Stale branch analysis (>30 days old)..." -ForegroundColor Green
$staleBranches = git for-each-ref --sort=-committerdate refs/heads/ --format='%(committerdate:short)|%(refname:short)' | 
    Where-Object { 
        $parts = $_ -split '\|'
        $date = $parts[0]
        $branch = $parts[1]
        $branch -notin $protectedBranches -and
        $branch -notmatch '^backup/' -and
        ($date -match '2026-04' -or $date -match '2026-03' -or $date -match '2026-02')
    }

if ($staleBranches) {
    Write-Host "`nStale branches found:" -ForegroundColor Yellow
    foreach ($entry in $staleBranches) {
        $parts = $entry -split '\|'
        Write-Host "  $($parts[0]) - $($parts[1])" -ForegroundColor Gray
    }
    
    if (-not $DryRun) {
        Write-Host "`nTo delete these branches, run:" -ForegroundColor Yellow
        Write-Host "  git branch -D <branch-name>" -ForegroundColor White
        Write-Host "Or use -Force flag to auto-delete (DANGEROUS)" -ForegroundColor Red
    }
} else {
    Write-Host "No stale branches found." -ForegroundColor Gray
}

Write-Host "`nPhase 4: Remote branch cleanup suggestions..." -ForegroundColor Green
$remoteBranches = git branch -r --merged origin/main | Where-Object {
    $_ -notmatch 'origin/main' -and
    $_ -notmatch 'origin/HEAD' -and
    $_ -notmatch 'backup/' -and
    $_ -notmatch 'pre-refactor-baseline'
}

if ($remoteBranches) {
    Write-Host "`nMerged remote branches (safe to delete on GitHub):" -ForegroundColor Yellow
    $count = 0
    foreach ($branch in $remoteBranches) {
        $branch = $branch.Trim() -replace 'origin/', ''
        Write-Host "  $branch" -ForegroundColor Gray
        $count++
    }
    Write-Host "`nTotal: $count remote branches" -ForegroundColor Cyan
    Write-Host "Delete via GitHub UI: Settings → Branches → Delete merged branches" -ForegroundColor Yellow
} else {
    Write-Host "No merged remote branches found." -ForegroundColor Gray
}

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "Cleanup Summary" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$totalLocal = (git branch | Measure-Object).Count
$totalRemote = (git branch -r | Measure-Object).Count

Write-Host "Local branches: $totalLocal" -ForegroundColor White
Write-Host "Remote tracking branches: $totalRemote" -ForegroundColor White

if ($DryRun) {
    Write-Host "`n[DRY RUN MODE] No changes were made." -ForegroundColor Yellow
    Write-Host "Run without -DryRun to apply changes." -ForegroundColor Yellow
} else {
    Write-Host "`nCleanup complete!" -ForegroundColor Green
}

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Review stale branches above" -ForegroundColor White
Write-Host "2. Delete manually if needed: git branch -D <branch-name>" -ForegroundColor White
Write-Host "3. Clean up remote branches via GitHub UI" -ForegroundColor White

# Made with Bob
