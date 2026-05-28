# Check which files have ErrorProne issues
$json = Get-Content docs/brain/codacy_errorprone_fresh.json -Raw | ConvertFrom-Json

Write-Host "Analyzing 105 ErrorProne issues..." -ForegroundColor Cyan

# Group by file path
$byFile = $json | Group-Object filePath | Sort-Object Count -Descending

Write-Host "`n=== Files with ErrorProne Issues ===" -ForegroundColor Magenta
foreach ($group in $byFile) {
    $isSrc = $group.Name -like "src/*.cs"
    $color = if ($isSrc) { "Red" } else { "Gray" }
    Write-Host "  $($group.Name): $($group.Count) issues" -ForegroundColor $color
}

# Count src/ vs non-src
$srcCount = ($json | Where-Object { $_.filePath -like "src/*.cs" }).Count
$nonSrcCount = $json.Count - $srcCount

Write-Host "`n=== Summary ===" -ForegroundColor Yellow
Write-Host "  src/*.cs: $srcCount issues" -ForegroundColor $(if ($srcCount -gt 0) { "Red" } else { "Green" })
Write-Host "  Non-src: $nonSrcCount issues" -ForegroundColor Gray

# Made with Bob
