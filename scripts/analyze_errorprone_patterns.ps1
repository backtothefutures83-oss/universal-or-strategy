# Analyze ErrorProne patterns in src/*.cs files
$json = Get-Content docs/brain/codacy_errorprone_fresh.json -Raw | ConvertFrom-Json

Write-Host "Analyzing ErrorProne patterns in src/*.cs..." -ForegroundColor Cyan

# Filter to src/*.cs only
$srcIssues = $json | Where-Object { $_.filePath -like "src/*.cs" }

Write-Host "`nTotal src/*.cs ErrorProne issues: $($srcIssues.Count)" -ForegroundColor Yellow

# Group by pattern
$byPattern = $srcIssues | Group-Object { $_.patternInfo.id } | Sort-Object Count -Descending

Write-Host "`n=== ErrorProne Patterns (src/*.cs) ===" -ForegroundColor Magenta
foreach ($group in $byPattern) {
    $sample = $group.Group[0]
    Write-Host "`n$($group.Name): $($group.Count) issues" -ForegroundColor White
    Write-Host "  Level: $($sample.patternInfo.level)" -ForegroundColor $(if ($sample.patternInfo.level -eq "High") { "Red" } else { "Yellow" })
    Write-Host "  Message: $($sample.message)" -ForegroundColor Gray
    Write-Host "  Sample: $($sample.filePath):$($sample.lineNumber)" -ForegroundColor Cyan
}

# Save detailed breakdown
$output = @()
foreach ($group in $byPattern) {
    $output += [PSCustomObject]@{
        Pattern = $group.Name
        Count = $group.Count
        Level = $group.Group[0].patternInfo.level
        Message = $group.Group[0].message
        Files = ($group.Group | Select-Object -ExpandProperty filePath -Unique | Sort-Object) -join ", "
    }
}

$output | ConvertTo-Json -Depth 5 | Out-File "docs/brain/errorprone_src_patterns.json"
Write-Host "`nDetailed breakdown saved to: docs/brain/errorprone_src_patterns.json" -ForegroundColor Green

# Made with Bob
