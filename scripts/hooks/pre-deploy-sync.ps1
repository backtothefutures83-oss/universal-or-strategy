# Pre-Deploy-Sync Hook
# Verifies build readiness before NinjaTrader hard link sync
# Purpose: Prevent deploying broken or non-ASCII code to NT8
# Exit Behavior: Halt if build fails or non-ASCII detected

$ErrorActionPreference = "Stop"

Write-Host "[PRE-DEPLOY-SYNC] Verifying build readiness..." -ForegroundColor Yellow

# 1. Verify build succeeds
Write-Host "  [1/3] Compiling Linting.csproj..." -ForegroundColor Cyan
$buildOutput = dotnet build Linting.csproj --nologo --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[PRE-DEPLOY-SYNC] FAIL: Build compilation failed" -ForegroundColor Red
    Write-Host $buildOutput -ForegroundColor Red
    Write-Host "ACTION: Fix build errors before deploying to NT8" -ForegroundColor Yellow
    exit 1
}
Write-Host "  [1/3] Build: PASS" -ForegroundColor Green

# 2. Verify ASCII compliance (redundant with deploy-sync.ps1, but critical)
Write-Host "  [2/3] Scanning for non-ASCII..." -ForegroundColor Cyan
$srcDir = "src"
$violations = @()

if (Test-Path $srcDir) {
    foreach ($csFile in (Get-ChildItem $srcDir -Filter "*.cs" -Recurse)) {
        try {
            $content = [System.IO.File]::ReadAllBytes($csFile.FullName)
            foreach ($byte in $content) {
                if ($byte -gt 127) {
                    $violations += $csFile.FullName
                    break
                }
            }
        } catch {
            Write-Host "  [WARN] Could not scan $($csFile.FullName): $_" -ForegroundColor Yellow
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "[PRE-DEPLOY-SYNC] FAIL: Non-ASCII detected in $($violations.Count) files" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Remove Unicode/emoji characters (V12 DNA: ASCII-only)" -ForegroundColor Yellow
    exit 1
}
Write-Host "  [2/3] ASCII: PASS" -ForegroundColor Green

# 3. Verify no uncommitted changes (prevents partial sync)
Write-Host "  [3/3] Checking git status..." -ForegroundColor Cyan
$gitStatus = git status --porcelain 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  [WARN] Git status check failed (not a git repo?)" -ForegroundColor Yellow
} elseif ($gitStatus) {
    Write-Host "[PRE-DEPLOY-SYNC] WARN: Uncommitted changes detected" -ForegroundColor Yellow
    Write-Host "RECOMMENDATION: Commit changes before deploy-sync" -ForegroundColor Yellow
    # Non-blocking warning - allow deployment of uncommitted changes for testing
} else {
    Write-Host "  [3/3] Git: CLEAN" -ForegroundColor Green
}

Write-Host "[PRE-DEPLOY-SYNC] PASS: Ready for NT8 sync" -ForegroundColor Green
exit 0

# Made with Bob
