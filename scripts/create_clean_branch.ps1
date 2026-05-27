# V12 Clean Branch Creation Script
# Prevents src/non-src contamination by validating main is clean before branching

param(
    [Parameter(Mandatory=$true)]
    [string]$BranchName,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("src", "non-src", "mixed")]
    [string]$BranchType = "src",
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "=== V12 Clean Branch Creation ===" -ForegroundColor Cyan
Write-Host "Branch: $BranchName" -ForegroundColor White
Write-Host "Type: $BranchType" -ForegroundColor White
Write-Host ""

# Step 1: Check if we're on main
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "[ERROR] You must be on 'main' branch to create a clean branch" -ForegroundColor Red
    Write-Host "Current branch: $currentBranch" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Run: git checkout main" -ForegroundColor Cyan
    exit 1
}

# Step 2: Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Host "[WARNING] You have uncommitted changes:" -ForegroundColor Yellow
    Write-Host ""
    git status --short
    Write-Host ""
    
    # Categorize changes
    $srcChanges = @()
    $nonSrcChanges = @()
    
    foreach ($line in $status) {
        $file = $line.Substring(3)
        if ($file -like "src/*") {
            $srcChanges += $file
        } else {
            $nonSrcChanges += $file
        }
    }
    
    if ($BranchType -eq "src" -and $nonSrcChanges.Count -gt 0) {
        Write-Host "[CONTAMINATION RISK] You're creating a src-only branch but have non-src changes:" -ForegroundColor Red
        Write-Host ""
        foreach ($file in $nonSrcChanges) {
            Write-Host "  - $file" -ForegroundColor Yellow
        }
        Write-Host ""
        Write-Host "RECOMMENDED ACTION:" -ForegroundColor Cyan
        Write-Host "1. Commit non-src files directly to main (no PR needed):" -ForegroundColor White
        Write-Host "   git add scripts/ docs/ .github/ .bob/" -ForegroundColor Gray
        Write-Host "   git commit -m 'chore: Update tooling'" -ForegroundColor Gray
        Write-Host "   git push origin main" -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Then run this script again" -ForegroundColor White
        Write-Host ""
        
        if (-not $Force) {
            Write-Host "[BLOCKED] Branch creation aborted to prevent contamination" -ForegroundColor Red
            Write-Host "Use -Force to override (not recommended)" -ForegroundColor Yellow
            exit 1
        } else {
            Write-Host "[FORCE] Proceeding despite contamination risk..." -ForegroundColor Yellow
        }
    }
    
    if ($BranchType -eq "src" -and $srcChanges.Count -gt 0) {
        Write-Host "[OK] You have src/ changes - this is expected for a src branch" -ForegroundColor Green
    }
}

# Step 3: Check if main is up to date with origin
Write-Host "[CHECK] Verifying main is up to date with origin..." -ForegroundColor Cyan
git fetch origin main --quiet

$localCommit = git rev-parse main
$remoteCommit = git rev-parse origin/main

if ($localCommit -ne $remoteCommit) {
    Write-Host "[WARNING] Your local main is out of sync with origin/main" -ForegroundColor Yellow
    
    $behind = git rev-list --count main..origin/main
    $ahead = git rev-list --count origin/main..main
    
    if ($behind -gt 0) {
        Write-Host "  Behind by $behind commit(s)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Run: git pull origin main" -ForegroundColor Cyan
        exit 1
    }
    
    if ($ahead -gt 0) {
        Write-Host "  Ahead by $ahead commit(s)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Run: git push origin main" -ForegroundColor Cyan
        exit 1
    }
}

# Step 4: Check if branch already exists
$branchExists = git rev-parse --verify $BranchName 2>$null
if ($branchExists) {
    Write-Host "[ERROR] Branch '$BranchName' already exists" -ForegroundColor Red
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Cyan
    Write-Host "1. Use a different name" -ForegroundColor White
    Write-Host "2. Delete existing branch: git branch -D $BranchName" -ForegroundColor White
    exit 1
}

# Step 5: Create the branch
Write-Host ""
Write-Host "[SUCCESS] All checks passed - creating clean branch..." -ForegroundColor Green
Write-Host ""

git checkout -b $BranchName

if ($LASTEXITCODE -eq 0) {
    Write-Host "[CREATED] Branch '$BranchName' created successfully" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    
    if ($BranchType -eq "src") {
        Write-Host "1. Make your src/ changes" -ForegroundColor White
        Write-Host "2. Stage ONLY src/ files: git add src/" -ForegroundColor White
        Write-Host "3. Commit: git commit -m 'feat: <description>'" -ForegroundColor White
        Write-Host "4. Push: git push origin $BranchName" -ForegroundColor White
        Write-Host "5. Create PR: gh pr create" -ForegroundColor White
    } else {
        Write-Host "1. Make your changes" -ForegroundColor White
        Write-Host "2. Commit: git commit -am '<description>'" -ForegroundColor White
        Write-Host "3. Push: git push origin $BranchName" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "[REMINDER] For non-src changes, commit directly to main (no PR needed)" -ForegroundColor Yellow
} else {
    Write-Host "[ERROR] Failed to create branch" -ForegroundColor Red
    exit 1
}

# Made with Bob
