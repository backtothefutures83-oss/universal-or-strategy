# Analyze Codacy issue count from exported JSON
$ErrorActionPreference = "Stop"

$jsonPath = "docs/brain/codacy_all_issues.json"

if (-not (Test-Path $jsonPath)) {
    Write-Error "File not found: $jsonPath"
    exit 1
}

Write-Host "[Codacy Count Analysis]" -ForegroundColor Cyan
Write-Host ""

# Load JSON
$issues = Get-Content $jsonPath | ConvertFrom-Json

Write-Host "Issues in Export File: $($issues.Count)" -ForegroundColor Yellow
Write-Host ""

# Group by category
Write-Host "Breakdown by Category:" -ForegroundColor Cyan
$issues | Group-Object { $_.patternInfo.category } | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count)" -ForegroundColor White
}

Write-Host ""

# Group by severity
Write-Host "Breakdown by Severity:" -ForegroundColor Cyan
$issues | Group-Object { $_.patternInfo.level } | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count)" -ForegroundColor White
}

Write-Host ""
Write-Host "[Analysis Complete]" -ForegroundColor Green

# Made with Bob
