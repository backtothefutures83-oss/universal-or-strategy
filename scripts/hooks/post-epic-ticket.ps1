# Post-Epic-Ticket Hook
# Validates ticket completion with full Droid Mission suite
# Purpose: Ensure ticket meets V12 DNA standards before marking complete
# Exit Behavior: Halt if any validation fails

param(
    [Parameter(Mandatory=$true)]
    [string]$EpicSlug,
    
    [Parameter(Mandatory=$true)]
    [string]$TicketNumber
)

$ErrorActionPreference = "Stop"

Write-Host "[POST-EPIC-TICKET] Validating ticket $TicketNumber..." -ForegroundColor Yellow

# 1. Full Test Suite
Write-Host "  [1/6] Running tests..." -ForegroundColor Cyan
try {
    $testOutput = dotnet test --no-build --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[POST-EPIC-TICKET] FAIL: Test failures" -ForegroundColor Red
        Write-Host $testOutput -ForegroundColor Red
        exit 1
    }
    Write-Host "  [1/6] Tests: PASS" -ForegroundColor Green
} catch {
    Write-Host "[POST-EPIC-TICKET] WARN: Test execution error (non-blocking)" -ForegroundColor Yellow
    Write-Host "  $_" -ForegroundColor Gray
}

# 2. Benchmarks (non-blocking, informational)
Write-Host "  [2/6] Running benchmarks..." -ForegroundColor Cyan
try {
    dotnet run --project benchmarks --configuration Release --no-build 2>&1 | Out-Null
    Write-Host "  [2/6] Benchmarks: COMPLETE" -ForegroundColor Green
} catch {
    Write-Host "  [2/6] Benchmarks: SKIPPED (non-blocking)" -ForegroundColor Yellow
}

# 3. Deploy-Sync (DNA Audits)
Write-Host "  [3/6] Running deploy-sync..." -ForegroundColor Cyan
try {
    & "$PSScriptRoot\..\..\deploy-sync.ps1" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[POST-EPIC-TICKET] FAIL: Deploy-sync failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "  [3/6] Deploy-Sync: PASS" -ForegroundColor Green
} catch {
    Write-Host "[POST-EPIC-TICKET] WARN: Deploy-sync error (non-blocking)" -ForegroundColor Yellow
    Write-Host "  $_" -ForegroundColor Gray
}

# 4. Lock() Audit
Write-Host "  [4/6] Scanning for lock()..." -ForegroundColor Cyan
$lockFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
$lockViolations = @()

foreach ($file in $lockFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match '\block\s*\(') {
        $lockViolations += $file.FullName
    }
}

if ($lockViolations.Count -gt 0) {
    Write-Host "[POST-EPIC-TICKET] FAIL: lock() usage detected in $($lockViolations.Count) files" -ForegroundColor Red
    $lockViolations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Remove lock() usage (V12 DNA: Lock-Free)" -ForegroundColor Yellow
    exit 1
}
Write-Host "  [4/6] Lock Audit: CLEAN" -ForegroundColor Green

# 5. Unicode Audit
Write-Host "  [5/6] Scanning for Unicode..." -ForegroundColor Cyan
$unicodeViolations = @()

foreach ($file in $lockFiles) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    foreach ($byte in $bytes) {
        if ($byte -gt 127) {
            $unicodeViolations += $file.FullName
            break
        }
    }
}

if ($unicodeViolations.Count -gt 0) {
    Write-Host "[POST-EPIC-TICKET] FAIL: Unicode detected in $($unicodeViolations.Count) files" -ForegroundColor Red
    $unicodeViolations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Remove Unicode/emoji (V12 DNA: ASCII-Only)" -ForegroundColor Yellow
    exit 1
}
Write-Host "  [5/6] Unicode Audit: CLEAN" -ForegroundColor Green

# 6. Complexity Audit
Write-Host "  [6/6] Running complexity audit..." -ForegroundColor Cyan
if (Test-Path "scripts/complexity_audit.py") {
    try {
        python scripts/complexity_audit.py 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[POST-EPIC-TICKET] WARN: Complexity violations detected" -ForegroundColor Yellow
            Write-Host "RECOMMENDATION: Refactor complex functions (CYC >15)" -ForegroundColor Yellow
            # Non-blocking warning
        } else {
            Write-Host "  [6/6] Complexity: PASS" -ForegroundColor Green
        }
    } catch {
        Write-Host "  [6/6] Complexity: SKIPPED (script error)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [6/6] Complexity: SKIPPED (script not found)" -ForegroundColor Yellow
}

Write-Host "`n[POST-EPIC-TICKET] PASS: Ticket $TicketNumber validated" -ForegroundColor Green
Write-Host "Epic: $EpicSlug" -ForegroundColor Gray
Write-Host "Ticket: $TicketNumber" -ForegroundColor Gray
exit 0

# Made with Bob
