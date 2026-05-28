# Analyze Codacy Security and Error-Prone issues

$issues = Get-Content 'docs/brain/codacy_all_issues.json' | ConvertFrom-Json

$security = $issues | Where-Object { $_.patternInfo.category -eq 'Security' }
$errorprone = $issues | Where-Object { $_.patternInfo.category -eq 'Error Prone' }

Write-Host "="*80
Write-Host "SECURITY ISSUES ANALYSIS"
Write-Host "="*80
Write-Host "Total Security Issues: $($security.Count)"
Write-Host ""

Write-Host "Top 10 Files with Security Issues:"
$security | Group-Object filePath | Sort-Object Count -Descending | Select-Object -First 10 | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) issues"
}

Write-Host ""
Write-Host "Security Patterns:"
$security | Group-Object { $_.patternInfo.id } | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) occurrences"
    $example = $_.Group[0]
    Write-Host "    Example: $($example.message)"
    Write-Host "    File: $($example.filePath):$($example.lineNumber)"
    Write-Host ""
}

Write-Host ""
Write-Host "="*80
Write-Host "ERROR-PRONE ISSUES ANALYSIS"
Write-Host "="*80
Write-Host "Total Error-Prone Issues: $($errorprone.Count)"
Write-Host ""

Write-Host "Top 10 Files with Error-Prone Issues:"
$errorprone | Group-Object filePath | Sort-Object Count -Descending | Select-Object -First 10 | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) issues"
}

Write-Host ""
Write-Host "Error-Prone Patterns:"
$errorprone | Group-Object { $_.patternInfo.id } | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) occurrences"
    $example = $_.Group[0]
    Write-Host "    Example: $($example.message)"
    Write-Host "    File: $($example.filePath):$($example.lineNumber)"
    Write-Host ""
}

# Made with Bob
