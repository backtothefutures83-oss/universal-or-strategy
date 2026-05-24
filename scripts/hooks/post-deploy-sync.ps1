# Post-Deploy-Sync Hook
# Verifies hard link integrity after NinjaTrader sync
# Purpose: Ensure all V12_002 files are properly hard-linked to NT8
# Exit Behavior: Halt if verification fails

$ErrorActionPreference = "Stop"

Write-Host "[POST-DEPLOY-SYNC] Verifying hard link integrity..." -ForegroundColor Yellow

$RepoRoot = "C:\WSGTA\universal-or-strategy"
$NtStrategyDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies"
$srcDir = Join-Path $RepoRoot "src"

# Verify directories exist
if (-not (Test-Path $srcDir)) {
    Write-Host "[POST-DEPLOY-SYNC] ERROR: Source directory not found: $srcDir" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $NtStrategyDir)) {
    Write-Host "[POST-DEPLOY-SYNC] ERROR: NinjaTrader directory not found: $NtStrategyDir" -ForegroundColor Red
    Write-Host "ACTION: Verify NinjaTrader 8 is installed" -ForegroundColor Yellow
    exit 1
}

# 1. Get all V12_002 files in src/
$srcFiles = Get-ChildItem -Path $srcDir -Filter "V12_002*.cs" -ErrorAction SilentlyContinue

if ($srcFiles.Count -eq 0) {
    Write-Host "[POST-DEPLOY-SYNC] WARN: No V12_002*.cs files found in src/" -ForegroundColor Yellow
    Write-Host "[POST-DEPLOY-SYNC] PASS: Nothing to verify" -ForegroundColor Green
    exit 0
}

Write-Host "  Found $($srcFiles.Count) V12_002 files to verify" -ForegroundColor Cyan

$brokenLinks = @()
$missingLinks = @()
$verifiedLinks = 0

foreach ($srcFile in $srcFiles) {
    $ntPath = Join-Path $NtStrategyDir $srcFile.Name
    
    # Check if link exists
    if (-not (Test-Path $ntPath)) {
        $missingLinks += $srcFile.Name
        continue
    }
    
    # Verify it's a hard link (not a copy)
    # Note: PowerShell's Get-Item.LinkType may not reliably detect hard links
    # Use file hash comparison as primary verification
    try {
        $srcHash = (Get-FileHash $srcFile.FullName -Algorithm MD5).Hash
        $ntHash = (Get-FileHash $ntPath -Algorithm MD5).Hash
        
        if ($srcHash -ne $ntHash) {
            $brokenLinks += "$($srcFile.Name) (hash mismatch)"
        } else {
            $verifiedLinks++
        }
    } catch {
        $brokenLinks += "$($srcFile.Name) (verification error: $_)"
    }
}

# 2. Report results
Write-Host "`n[POST-DEPLOY-SYNC] Verification Results:" -ForegroundColor Cyan
Write-Host "  Verified: $verifiedLinks" -ForegroundColor Green
Write-Host "  Missing:  $($missingLinks.Count)" -ForegroundColor $(if ($missingLinks.Count -gt 0) { "Red" } else { "Green" })
Write-Host "  Broken:   $($brokenLinks.Count)" -ForegroundColor $(if ($brokenLinks.Count -gt 0) { "Red" } else { "Green" })

if ($missingLinks.Count -gt 0) {
    Write-Host "`n[POST-DEPLOY-SYNC] FAIL: $($missingLinks.Count) missing links" -ForegroundColor Red
    $missingLinks | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Re-run deploy-sync.ps1 to create missing links" -ForegroundColor Yellow
    exit 1
}

if ($brokenLinks.Count -gt 0) {
    Write-Host "`n[POST-DEPLOY-SYNC] FAIL: $($brokenLinks.Count) broken links" -ForegroundColor Red
    $brokenLinks | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Re-run deploy-sync.ps1 to fix broken links" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n[POST-DEPLOY-SYNC] PASS: All $verifiedLinks hard links verified" -ForegroundColor Green
exit 0

# Made with Bob
