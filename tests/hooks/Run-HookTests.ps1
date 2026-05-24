# Hook Test Runner
# Executes all Pester tests for V12 hooks

param(
    [switch]$Detailed,
    [string]$TestName = "*"
)

$ErrorActionPreference = "Stop"

Write-Host "=== V12 Hook Test Runner ===" -ForegroundColor Cyan

# Check if Pester is installed
$pesterModule = Get-Module -ListAvailable -Name Pester
if (-not $pesterModule) {
    Write-Host "Pester not found. Installing..." -ForegroundColor Yellow
    Install-Module -Name Pester -Force -SkipPublisherCheck -Scope CurrentUser
    Write-Host "Pester installed successfully" -ForegroundColor Green
}

# Import Pester
Import-Module Pester -MinimumVersion 5.0 -ErrorAction Stop

# Configure Pester
$config = New-PesterConfiguration
$config.Run.Path = "$PSScriptRoot"
$config.Run.PassThru = $true
$config.Output.Verbosity = if ($Detailed) { 'Detailed' } else { 'Normal' }
$config.TestResult.Enabled = $true
$config.TestResult.OutputPath = "$PSScriptRoot\TestResults.xml"
$config.TestResult.OutputFormat = 'NUnitXml'

if ($TestName -ne "*") {
    $config.Filter.FullName = "*$TestName*"
}

# Run tests
Write-Host "`nRunning hook tests..." -ForegroundColor Yellow
$result = Invoke-Pester -Configuration $config

# Report results
Write-Host "`n=== Test Results ===" -ForegroundColor Cyan
Write-Host "Total:  $($result.TotalCount)" -ForegroundColor White
Write-Host "Passed: $($result.PassedCount)" -ForegroundColor Green
Write-Host "Failed: $($result.FailedCount)" -ForegroundColor $(if ($result.FailedCount -gt 0) { "Red" } else { "Green" })
Write-Host "Skipped: $($result.SkippedCount)" -ForegroundColor Yellow

if ($result.FailedCount -gt 0) {
    Write-Host "`nFailed tests:" -ForegroundColor Red
    foreach ($test in $result.Failed) {
        Write-Host "  - $($test.ExpandedName)" -ForegroundColor Yellow
        if ($test.ErrorRecord) {
            Write-Host "    $($test.ErrorRecord.Exception.Message)" -ForegroundColor Gray
        }
    }
    exit 1
}

Write-Host "`nAll tests passed!" -ForegroundColor Green
exit 0

# Made with Bob
