# Filter Codacy issues to ONLY .cs files in src/
# Usage: powershell -File .\scripts\filter_cs_only_issues.ps1

$securityFile = "docs/brain/codacy_security_fresh.json"
$errorProneFile = "docs/brain/codacy_errorprone_fresh.json"

Write-Host "Filtering Security issues to src/*.cs only..." -ForegroundColor Cyan
$security = Get-Content $securityFile | ConvertFrom-Json
$securityCs = $security.data | Where-Object { 
    $_.file.path -like "src/*.cs" 
}
Write-Host "  Total Security: $($security.data.Count)" -ForegroundColor Yellow
Write-Host "  src/*.cs only: $($securityCs.Count)" -ForegroundColor Green

Write-Host "`nFiltering ErrorProne issues to src/*.cs only..." -ForegroundColor Cyan
$errorProne = Get-Content $errorProneFile | ConvertFrom-Json
$errorProneCs = $errorProne.data | Where-Object { 
    $_.file.path -like "src/*.cs" 
}
Write-Host "  Total ErrorProne: $($errorProne.data.Count)" -ForegroundColor Yellow
Write-Host "  src/*.cs only: $($errorProneCs.Count)" -ForegroundColor Green

# Group by pattern
Write-Host "`n=== Security Patterns (src/*.cs) ===" -ForegroundColor Magenta
$securityCs | Group-Object { $_.patternInfo.id } | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) issues" -ForegroundColor White
}

Write-Host "`n=== ErrorProne Patterns (src/*.cs) ===" -ForegroundColor Magenta
$errorProneCs | Group-Object { $_.patternInfo.id } | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) issues" -ForegroundColor White
}

# Save filtered results
$securityCsOutput = @{
    data = $securityCs
    pagination = @{ total = $securityCs.Count }
}
$errorProneCsOutput = @{
    data = $errorProneCs
    pagination = @{ total = $errorProneCs.Count }
}

$securityCsOutput | ConvertTo-Json -Depth 10 | Out-File "docs/brain/codacy_security_cs_only.json"
$errorProneCsOutput | ConvertTo-Json -Depth 10 | Out-File "docs/brain/codacy_errorprone_cs_only.json"

Write-Host "`nFiltered results saved:" -ForegroundColor Green
Write-Host "  docs/brain/codacy_security_cs_only.json" -ForegroundColor White
Write-Host "  docs/brain/codacy_errorprone_cs_only.json" -ForegroundColor White

# Made with Bob
