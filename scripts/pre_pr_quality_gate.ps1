# Pre-PR Quality Gate
# Runs 13 exhaustive tests before PR submission

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$script:FailureCount = 0
$script:TestResults = @()

function Test-Gate {
    param(
        [string]$Name,
        [scriptblock]$Test
    )
    
    Write-Host "`n[TEST] $Name..." -ForegroundColor Cyan
    
    try {
        & $Test
        Write-Host "[PASS] $Name" -ForegroundColor Green
        $script:TestResults += @{ Name = $Name; Status = "PASS" }
    }
    catch {
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        Write-Host "  Error: $_" -ForegroundColor Red
        $script:FailureCount++
        $script:TestResults += @{ Name = $Name; Status = "FAIL"; Error = $_ }
    }
}

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Pre-PR Quality Gate" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

# Test 1: Full Test Suite
Test-Gate "Full Test Suite" {
    $result = dotnet test --no-build --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}

# Test 2: Benchmarks
Test-Gate "Benchmarks (No Regressions)" {
    $result = dotnet run --project benchmarks --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Benchmarks failed" }
}

# Test 3: Deploy-Sync
Test-Gate "Deploy-Sync (Hard Links + ASCII)" {
    $result = & "$PSScriptRoot\..\deploy-sync.ps1"
    if ($LASTEXITCODE -ne 0) { throw "Deploy-sync failed" }
}

# Test 4: Lock-Free Compliance
Test-Gate "Lock-Free Compliance" {
    $locks = Select-String -Path "src\*.cs" -Pattern "lock\(" -SimpleMatch
    if ($locks.Count -gt 0) { throw "Found $($locks.Count) lock() statements" }
}

# Test 5: Unicode Compliance
Test-Gate "Unicode Compliance (ASCII-Only)" {
    $unicode = Select-String -Path "src\*.cs" -Pattern "[^\x00-\x7F]"
    if ($unicode.Count -gt 0) { throw "Found $($unicode.Count) non-ASCII characters" }
}

# Test 6: Complexity Compliance
Test-Gate "Complexity Compliance (CYC < 20)" {
    $result = python "$PSScriptRoot\complexity_audit.py"
    if ($LASTEXITCODE -ne 0) { throw "Complexity violations found" }
}

# Test 7: Semgrep (V12 DNA Patterns)
Test-Gate "Semgrep (V12 DNA Patterns)" {
    $result = & "$PSScriptRoot\run_semgrep.ps1"
    if ($LASTEXITCODE -ne 0) { throw "Semgrep violations found" }
}

# Test 8: Dead Code Scan
Test-Gate "Dead Code Scan" {
    $result = python "$PSScriptRoot\dead_code_scan.py"
    if ($LASTEXITCODE -ne 0) { throw "Dead code found" }
}

# Test 9: Build Readiness
Test-Gate "Build Readiness" {
    $result = & "$PSScriptRoot\build_readiness.ps1"
    if ($LASTEXITCODE -ne 0) { throw "Build not ready" }
}

# Test 10: Format Check
Test-Gate "Format Check (CSharpier)" {
    $result = dotnet csharpier --check .
    if ($LASTEXITCODE -ne 0) { throw "Format violations found" }
}

# Test 11: PR Hygiene
Test-Gate "PR Hygiene (Rebase + Clean)" {
    $result = & "$PSScriptRoot\verify_pr_hygiene.ps1"
    if ($LASTEXITCODE -ne 0) { throw "PR hygiene violations" }
}

# Test 12: Graphify Update
Test-Gate "Graphify Update" {
    $result = graphify update . --silent
    if ($LASTEXITCODE -ne 0) { throw "Graphify update failed" }
}

# Test 13: TDD Compliance
Test-Gate "TDD Compliance (Tests Exist)" {
    $srcFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse
    $testFiles = Get-ChildItem -Path "tests" -Filter "*Tests.cs" -Recurse
    
    if ($testFiles.Count -eq 0) { throw "No test files found" }
    
    $coverage = ($testFiles.Count / $srcFiles.Count) * 100
    if ($coverage -lt 50) { throw "Test coverage too low: $coverage%" }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "Quality Gate Summary" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

foreach ($result in $script:TestResults) {
    $color = if ($result.Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "$($result.Name): $($result.Status)" -ForegroundColor $color
}

Write-Host "`nTotal Tests: $($script:TestResults.Count)" -ForegroundColor Cyan
Write-Host "Passed: $($script:TestResults.Count - $script:FailureCount)" -ForegroundColor Green
Write-Host "Failed: $script:FailureCount" -ForegroundColor Red

if ($script:FailureCount -gt 0) {
    Write-Host "`n[BLOCKED] Quality gate FAILED. Fix issues before PR submission." -ForegroundColor Red
    exit 1
}

Write-Host "`n[APPROVED] Quality gate PASSED. Ready for PR submission." -ForegroundColor Green
exit 0

# Made with Bob
