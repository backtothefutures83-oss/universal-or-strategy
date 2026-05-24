# Analyze Mistakes
# Pattern detection + protocol hardening

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$MistakeLogPath = "docs/brain/mistake_log.jsonl"

# Ensure mistake log exists
if (-not (Test-Path $MistakeLogPath)) {
    New-Item -Path $MistakeLogPath -ItemType File -Force | Out-Null
}

# Load mistake log
$mistakes = Get-Content $MistakeLogPath | ForEach-Object { $_ | ConvertFrom-Json }

if ($mistakes.Count -eq 0) {
    Write-Host "[INFO] No mistakes logged yet." -ForegroundColor Cyan
    exit 0
}

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Mistake Analysis" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

# Pattern detection
$patterns = $mistakes | Group-Object -Property category | Sort-Object -Property Count -Descending

Write-Host "`nTop Mistake Categories:" -ForegroundColor Cyan
foreach ($pattern in $patterns) {
    Write-Host "  $($pattern.Name): $($pattern.Count) occurrences" -ForegroundColor Yellow
}

# Recent mistakes (last 10)
Write-Host "`nRecent Mistakes (Last 10):" -ForegroundColor Cyan
$mistakes | Select-Object -Last 10 | ForEach-Object {
    Write-Host "  [$($_.timestamp)] $($_.category): $($_.description)" -ForegroundColor Gray
}

# Protocol hardening suggestions
Write-Host "`nProtocol Hardening Suggestions:" -ForegroundColor Cyan

$suggestions = @()

# Check for repeated TDD violations
$tddViolations = $mistakes | Where-Object { $_.category -eq "TDD_VIOLATION" }
if ($tddViolations.Count -gt 3) {
    $suggestions += "Consider strengthening TDD enforcement hooks"
}

# Check for repeated lock() usage
$lockViolations = $mistakes | Where-Object { $_.category -eq "LOCK_VIOLATION" }
if ($lockViolations.Count -gt 2) {
    $suggestions += "Add pre-commit hook to block lock() statements"
}

# Check for repeated Unicode violations
$unicodeViolations = $mistakes | Where-Object { $_.category -eq "UNICODE_VIOLATION" }
if ($unicodeViolations.Count -gt 2) {
    $suggestions += "Add pre-tool-use hook to block Unicode in src/"
}

if ($suggestions.Count -eq 0) {
    Write-Host "  No protocol hardening needed at this time." -ForegroundColor Green
}
else {
    foreach ($suggestion in $suggestions) {
        Write-Host "  - $suggestion" -ForegroundColor Yellow
    }
}

Write-Host "`n[COMPLETE] Mistake analysis complete." -ForegroundColor Green
exit 0

# Made with Bob
