# Check what categories exist in Codacy issues

$issues = Get-Content 'docs/brain/codacy_all_issues.json' | ConvertFrom-Json

Write-Host "Total issues: $($issues.Count)"
Write-Host ""
Write-Host "Categories found:"
$issues | Group-Object { $_.patternInfo.category } | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) issues"
}

Write-Host ""
Write-Host "Sample issue:"
$sample = $issues[0]
Write-Host "  File: $($sample.filePath)"
Write-Host "  Category: $($sample.patternInfo.category)"
Write-Host "  Pattern: $($sample.patternInfo.id)"
Write-Host "  Message: $($sample.message)"

# Made with Bob
