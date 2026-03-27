# deploy-vm-safe.ps1
# WSGTA Infrastructure: One Source of Truth Automation (VM-SPECIFIC)
# Establishes Hard Links between Git Repo and NinjaTrader 8 on the VM

$ErrorActionPreference = "Stop"

# --- CONFIGURATION (DYNAMIC FOR VM) ---
$RepoRoot = "C:\WSGTA\universal-or-strategy" # Repo location on VM
if (!(Test-Path $RepoRoot)) { $RepoRoot = "C:\Users\admin\repos\universal-or-strategy" } # Fallback for common VM repo path
$NtCustomDir = "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom"
$NtStrategyDir = Join-Path $NtCustomDir "Strategies"
$NtIndicatorDir = Join-Path $NtCustomDir "Indicators"

Write-Host "--- VM DEPLOY SYNC: Hardening Environment ---" -ForegroundColor Cyan
Write-Host "Target: $NtStrategyDir" -ForegroundColor Gray

# =============================================================================
# 1. NUCLEAR SUBFOLDER PURGE (防止命名冲突 CS0102)
# Searches for and destroys ANY subdirectories starting with V12_002.
# NinjaTrader will try to compile BOTH the files in root and files in subfolders
# if we don't kill these, leading to "Member already defined" errors.
# =============================================================================
Write-Host "`n--- STEP 1: Nuclear Subfolder Purge ---" -ForegroundColor Yellow
$subfolders = Get-ChildItem -Path $NtStrategyDir -Directory -Filter "V12_002*" -ErrorAction SilentlyContinue
foreach ($f in $subfolders) {
    Write-Host "DELETE: Found legacy subfolder -> $($f.Name). Removing..." -ForegroundColor Red
    Remove-Item -Path $f.FullName -Recurse -Force
}

# =============================================================================
# 2. ASCII PRE-DEPLOY GATE
# Scans ALL .cs source files for non-ASCII bytes BEFORE touching NT8.
# =============================================================================
Write-Host "`n--- STEP 2: ASCII GATE: Scanning source files ---" -ForegroundColor Yellow
$srcDir = Join-Path $RepoRoot "src"
$gatePass = $true
foreach ($csFile in (Get-ChildItem $srcDir -Filter "*.cs" -Recurse)) {
    $bytes = [System.IO.File]::ReadAllBytes($csFile.FullName)
    $badBytes = $bytes | Where-Object { $_ -gt 127 }
    if ($badBytes.Count -gt 0) {
        Write-Host "ASCII GATE FAIL: $($csFile.Name) has $($badBytes.Count) non-ASCII bytes" -ForegroundColor Red
        $gatePass = $false
    }
}
if (-not $gatePass) {
    Write-Host "`nDEPLOY ABORTED - Fix encoding errors first (non-ASCII found)" -ForegroundColor Red
    exit 1
}
Write-Host "ASCII GATE PASS - all source files are clean" -ForegroundColor Green

# =============================================================================
# 3. DYNAMIC FILE DISCOVERY & SYNC (Hardlinks)
# Automatically finds all V12_002 modular files + shared components.
# =============================================================================
Write-Host "`n--- STEP 3: Establishing Dynamic Hardlinks ---" -ForegroundColor Yellow

# Static Mappings (Exceptions to the modular pattern)
$StaticMappings = @(
    # Indicators
    @{ src = "V12_001.cs"; dst = Join-Path $NtIndicatorDir "V12_001.cs" },
    # Shared Components
    @{ src = "SignalBroadcaster.cs"; dst = Join-Path $NtStrategyDir "SignalBroadcaster.cs" }
)

# 1. Process Static Mappings
foreach ($map in $StaticMappings) {
    $srcPath = Join-Path $srcDir $map.src
    $dstPath = $map.dst
    
    if (!(Test-Path $srcPath)) {
        Write-Host "SKIP: Missing Static -> src/$($map.src)" -ForegroundColor Gray
        continue
    }

    if (Test-Path $dstPath) { Remove-Item $dstPath -Force }
    Write-Host "LINKING (Static): $($map.src) -> NT8" -ForegroundColor Green
    New-Item -ItemType HardLink -Path $dstPath -Value $srcPath | Out-Null
}

# 2. Process Dynamic Modular Files (V12_002 Series)
$modularFiles = Get-ChildItem -Path $srcDir -Filter "V12_002*.cs"
foreach ($f in $modularFiles) {
    $srcPath = $f.FullName
    $dstPath = Join-Path $NtStrategyDir $f.Name
    
    if (Test-Path $dstPath) { Remove-Item $dstPath -Force }
    Write-Host "LINKING (Dynamic): $($f.Name) -> Strategies" -ForegroundColor Green
    New-Item -ItemType HardLink -Path $dstPath -Value $srcPath | Out-Null
}

Write-Host "`n--- VM SYNC COMPLETE: Environment Hardened (Dynamic Discovery Active) ---" -ForegroundColor Cyan
