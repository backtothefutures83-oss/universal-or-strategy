# Parse Codacy Export and Extract Security + Error-Prone Issues
# V12 Universal OR Strategy - Codacy Remediation

param(
    [string]$InputFile = "docs/codacy.txt",
    [string]$OutputDir = "docs/brain"
)

Write-Host "=== Codacy Export Parser ===" -ForegroundColor Cyan
Write-Host "Input: $InputFile" -ForegroundColor Gray
Write-Host "Output: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Read all lines
$lines = Get-Content $InputFile

# Data structures
$securityIssues = @()
$errorProneIssues = @()
$currentIssue = $null
$inIssueBlock = $false

# State machine to parse the text format
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # Detect file path (starts with src/ or scripts/)
    if ($line -match '^(src/|scripts/|\./)[\w\./]+\.(cs|ps1|py)$') {
        if ($currentIssue) {
            # Save previous issue
            if ($currentIssue.Category -eq "Security") {
                $securityIssues += $currentIssue
            } elseif ($currentIssue.Category -eq "Error prone") {
                $errorProneIssues += $currentIssue
            }
        }
        
        # Start new issue
        $currentIssue = @{
            File = $line.Trim()
            Line = $null
            Severity = $null
            Category = $null
            Pattern = $null
            Message = $null
            CodeSnippet = $null
        }
        $inIssueBlock = $true
        continue
    }
    
    # Detect line number (numeric only line)
    if ($inIssueBlock -and $line -match '^\s*\d+\s*$') {
        $currentIssue.Line = $line.Trim()
        continue
    }
    
    # Detect severity (HIGH, MEDIUM, LOW)
    if ($inIssueBlock -and $line -match '^\s*(HIGH|MEDIUM|LOW)\s*$') {
        $currentIssue.Severity = $line.Trim()
        continue
    }
    
    # Detect category
    if ($inIssueBlock -and $line -match '^\s*(Security|Error prone|Code Style|Performance|Compatibility)\s*$') {
        $currentIssue.Category = $line.Trim()
        continue
    }
    
    # Detect pattern name (lines that look like pattern identifiers)
    if ($inIssueBlock -and $currentIssue.Category -and !$currentIssue.Pattern -and $line -match '\w+' -and $line.Length -lt 100) {
        $trimmed = $line.Trim()
        if ($trimmed -and $trimmed -notmatch '^(if|else|for|while|return|var|private|public|protected|internal)') {
            $currentIssue.Pattern = $trimmed
        }
    }
    
    # Capture code snippet (indented lines after pattern)
    if ($inIssueBlock -and $currentIssue.Pattern -and $line -match '^\s{2,}' -and $line.Trim().Length -gt 0) {
        if (!$currentIssue.CodeSnippet) {
            $currentIssue.CodeSnippet = $line
        }
    }
}

# Save last issue
if ($currentIssue -and $currentIssue.Category) {
    if ($currentIssue.Category -eq "Security") {
        $securityIssues += $currentIssue
    } elseif ($currentIssue.Category -eq "Error prone") {
        $errorProneIssues += $currentIssue
    }
}

Write-Host "Parsing complete!" -ForegroundColor Green
Write-Host "Security issues found: $($securityIssues.Count)" -ForegroundColor Yellow
Write-Host "Error-prone issues found: $($errorProneIssues.Count)" -ForegroundColor Yellow
Write-Host ""

# Export Security Issues
$securityOutput = "$OutputDir/codacy_security_issues.txt"
$securityIssues | ForEach-Object {
    "File: $($_.File)"
    "Line: $($_.Line)"
    "Severity: $($_.Severity)"
    "Pattern: $($_.Pattern)"
    "Code: $($_.CodeSnippet)"
    "---"
} | Out-File -FilePath $securityOutput -Encoding UTF8

Write-Host "✓ Security issues exported to: $securityOutput" -ForegroundColor Green

# Export Error-Prone Issues
$errorProneOutput = "$OutputDir/codacy_errorprone_issues.txt"
$errorProneIssues | ForEach-Object {
    "File: $($_.File)"
    "Line: $($_.Line)"
    "Severity: $($_.Severity)"
    "Pattern: $($_.Pattern)"
    "Code: $($_.CodeSnippet)"
    "---"
} | Out-File -FilePath $errorProneOutput -Encoding UTF8

Write-Host "✓ Error-prone issues exported to: $errorProneOutput" -ForegroundColor Green
Write-Host ""

# Create File Clustering Analysis
Write-Host "Creating file clustering analysis..." -ForegroundColor Cyan

# Group by file
$securityByFile = $securityIssues | Group-Object -Property File | Sort-Object Count -Descending
$errorProneByFile = $errorProneIssues | Group-Object -Property File | Sort-Object Count -Descending

# Combine for top files
$allIssues = $securityIssues + $errorProneIssues
$combinedByFile = $allIssues | Group-Object -Property File | Sort-Object Count -Descending

$clusterOutput = "$OutputDir/codacy_sec_errorprone_clusters.md"

@"
# Security + Error-Prone File Clusters

**Generated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Total Security Issues**: $($securityIssues.Count)
**Total Error-Prone Issues**: $($errorProneIssues.Count)
**Total Combined**: $($allIssues.Count)

---

## Security Issues ($($securityIssues.Count) total)

| File | Count | Patterns |
|------|-------|----------|
"@ | Out-File -FilePath $clusterOutput -Encoding UTF8

foreach ($group in $securityByFile) {
    $patterns = ($group.Group | Select-Object -ExpandProperty Pattern -Unique) -join ", "
    "| $($group.Name) | $($group.Count) | $patterns |" | Out-File -FilePath $clusterOutput -Append -Encoding UTF8
}

@"

---

## Error-Prone Issues ($($errorProneIssues.Count) total)

| File | Count | Patterns |
|------|-------|----------|
"@ | Out-File -FilePath $clusterOutput -Append -Encoding UTF8

foreach ($group in $errorProneByFile | Select-Object -First 20) {
    $patterns = ($group.Group | Select-Object -ExpandProperty Pattern -Unique) -join ", "
    "| $($group.Name) | $($group.Count) | $patterns |" | Out-File -FilePath $clusterOutput -Append -Encoding UTF8
}

@"

---

## Top 10 Files (Combined Security + Error-Prone)

| Rank | File | Security | Error-Prone | Total | Key Patterns |
|------|------|----------|-------------|-------|--------------|
"@ | Out-File -FilePath $clusterOutput -Append -Encoding UTF8

$rank = 1
foreach ($group in $combinedByFile | Select-Object -First 10) {
    $secCount = ($securityIssues | Where-Object { $_.File -eq $group.Name }).Count
    $errCount = ($errorProneIssues | Where-Object { $_.File -eq $group.Name }).Count
    $patterns = ($group.Group | Select-Object -ExpandProperty Pattern -Unique | Select-Object -First 3) -join ", "
    "| $rank | $($group.Name) | $secCount | $errCount | $($group.Count) | $patterns |" | Out-File -FilePath $clusterOutput -Append -Encoding UTF8
    $rank++
}

Write-Host "✓ File clustering analysis exported to: $clusterOutput" -ForegroundColor Green
Write-Host ""

# Summary Report
Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "Security Issues: $($securityIssues.Count)" -ForegroundColor $(if ($securityIssues.Count -eq 12) { "Green" } else { "Yellow" })
Write-Host "Error-Prone Issues: $($errorProneIssues.Count)" -ForegroundColor $(if ($errorProneIssues.Count -eq 105) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "Top 5 Files with Most Issues:" -ForegroundColor Cyan
$combinedByFile | Select-Object -First 5 | ForEach-Object {
    $secCount = ($securityIssues | Where-Object { $_.File -eq $_.Name }).Count
    $errCount = ($errorProneIssues | Where-Object { $_.File -eq $_.Name }).Count
    $message = "  $($_.Name): $secCount Security + $errCount Error-Prone = $($_.Count) total"
    Write-Host $message -ForegroundColor Gray
}
Write-Host ""

# Return data for further processing
return @{
    SecurityIssues = $securityIssues
    ErrorProneIssues = $errorProneIssues
    SecurityByFile = $securityByFile
    ErrorProneByFile = $errorProneByFile
    CombinedByFile = $combinedByFile
}

# Made with Bob
