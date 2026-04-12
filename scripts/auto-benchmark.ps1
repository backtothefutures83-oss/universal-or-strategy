param (
    [string]$TargetBenchmark = "V14.8: RoundTrip",
    [double]$TargetNs = 7.667
)

$ErrorActionPreference = "Stop"

Write-Host "==============================================="
Write-Host " [AMAL] Auto-Benchmark Sequence Initiated      "
Write-Host "==============================================="

$BenchmarksDir = "c:\WSGTA\universal-or-strategy\benchmarks"
Set-Location $BenchmarksDir

Write-Host "-> Running BenchmarkDotNet (Release Mode)..."
# Run dotnet run and capture output just to ensure it proceeds cleanly, but allow it to print.
dotnet run -c Release 

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Benchmark failed to compile or run." -ForegroundColor Red
    exit 1
}

# The CSV is created inside benchmarks\BenchmarkDotNet.Artifacts\results\
# Find the latest CSV report.
$reportPath = Get-ChildItem -Path ".\BenchmarkDotNet.Artifacts\results\*report.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $reportPath) {
    Write-Host "[FAIL] Benchmark CSV report not found." -ForegroundColor Red
    exit 1
}

Write-Host "-> Parsing results from $($reportPath.Name)..."
$csv = Import-Csv $reportPath.FullName

# Keep in mind BenchmarkDotNet CSV fields are often named "Method", "Mean", "Allocated"
# We'll filter for the target benchmark:
$row = $csv | Where-Object { $_.Method -match $TargetBenchmark }

if (-not $row) {
    Write-Host "[FAIL] Benchmark target '$TargetBenchmark' not found in CSV." -ForegroundColor Red
    exit 1
}

$meanNs = $row.Mean
$allocated = $row.Allocated

Write-Host "-----------------------------------------------"
Write-Host " Target : $TargetBenchmark"
Write-Host " Mean   : $meanNs"
Write-Host " Alloc  : $allocated"
Write-Host "-----------------------------------------------"

# BenchmarkDotNet CSV outputs in plain numbers (e.g., 7.667, NA). "Allocated" might be numeric.
# Sometimes it includes text or commas.
$cleanMeanString = $meanNs -replace '[^\d\.]', ''
if ([string]::IsNullOrWhiteSpace($cleanMeanString)) {
    Write-Host "[FAIL] Could not parse Mean latency." -ForegroundColor Red
    exit 1
}
$cleanMean = [double]$cleanMeanString

$passed = $true

if ($allocated -ne "0" -and $allocated -ne "0 B" -and $allocated -ne "NA" -and $allocated -ne "-") {
    Write-Host "[FAIL] Integrity Violation! Memory Allocation detected: $allocated" -ForegroundColor Red
    $passed = $false
}
else {
    Write-Host "[PASS] Memory Integrity: 0 B Allocated." -ForegroundColor Green
}

if ($cleanMean -ge $TargetNs) {
    Write-Host "[FAIL] Performance Deficit! Mean $cleanMean ns >= Baseline $TargetNs ns" -ForegroundColor Red
    $passed = $false
}
else {
    Write-Host "[PASS] Performance Target Met! Mean $cleanMean ns < $TargetNs ns" -ForegroundColor Green
}

Write-Host "==============================================="
if ($passed) {
    Write-Host " [GO] EMPIRICAL VALIDATION PASSED          " -ForegroundColor DarkGreen -BackgroundColor White
    exit 0
}
else {
    Write-Host " [NOGO] EMPIRICAL VALIDATION FAILED       " -ForegroundColor White -BackgroundColor DarkRed
    exit 1
}
