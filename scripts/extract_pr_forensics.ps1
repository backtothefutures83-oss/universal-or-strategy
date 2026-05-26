# PR Forensics Extraction Script
# Extracts and categorizes ALL bot findings from a GitHub PR
# Usage: .\scripts\extract_pr_forensics.ps1 -PrNumber 6

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber
)

$ErrorActionPreference = "Stop"

# Timestamp for freshness verification
$extractionTimestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
$extractionCommit = git rev-parse HEAD

# Extract repository URL dynamically from git remote
$GitRemote = git remote get-url origin
$RepoUrl = $GitRemote -replace '\.git$', '' -replace 'git@github\.com:', 'https://github.com/'

Write-Host "=== PR FORENSICS EXTRACTION ===" -ForegroundColor Cyan
Write-Host "PR #$PrNumber" -ForegroundColor Yellow
Write-Host "Repository: $RepoUrl" -ForegroundColor Gray

# Step 1: Fetch raw data
Write-Host "`n[1/5] Fetching PR data from GitHub..." -ForegroundColor Green
$rawFile = "pr_${PrNumber}_raw.json"
gh pr view $PrNumber --json comments,reviews,statusCheckRollup | Out-File -FilePath $rawFile -Encoding utf8
$prData = Get-Content $rawFile | ConvertFrom-Json

# Step 2: Extract findings
Write-Host "[2/5] Parsing bot comments and reviews..." -ForegroundColor Green

$findings = @()
$hallucinations = @()

# Process comments
foreach ($comment in $prData.comments) {
    $author = $comment.author.login
    $body = $comment.body
    
    # Skip non-bot comments
    if ($author -notmatch "bot|ai|codacy|sourcery|coderabbit|amazon-q|gemini|greptile|cubic|codeant|codeslick|insight") {
        continue
    }
    
    # Classify comment
    $classification = "UNKNOWN"
    $priority = "P2"
    $category = "INFO"
    
    # INFRA-NOISE detection
    if ($body -match "Monthly limit|Analysis Blocked|upgrade to continue|No accessibility issues") {
        $classification = "INFRA-NOISE"
        $category = "INFRA"
    }
    # HALLUCINATION detection
    elseif ($body -match "missing files|file not found|cannot find" -and $body -notmatch "test coverage") {
        $classification = "HALLUCINATION"
        $hallucinations += @{
            bot = $author
            claim = ($body -split "`n" | Select-String "missing|not found" | Select-Object -First 1).ToString()
            timestamp = $comment.createdAt
        }
    }
    # VALID findings
    elseif ($body -match "Critical|Blocking|MUST|race condition|deadlock|struct copy|atomic") {
        $classification = "VALID"
        $priority = "P0"
        $category = "CRITICAL"
    }
    elseif ($body -match "High Risk|security|vulnerability|unsafe") {
        $classification = "VALID"
        $priority = "P1"
        $category = "SECURITY"
    }
    elseif ($body -match "performance|optimization|allocation|benchmark") {
        $classification = "VALID"
        $priority = "P2"
        $category = "PERFORMANCE"
    }
    
    if ($classification -ne "UNKNOWN") {
        $findings += [PSCustomObject]@{
            Source = "comment"
            Bot = $author
            Classification = $classification
            Priority = $priority
            Category = $category
            Timestamp = $comment.createdAt
            URL = $comment.url
            Body = $body.Substring(0, [Math]::Min(500, $body.Length))
        }
    }
}

# Process reviews
foreach ($review in $prData.reviews) {
    $author = $review.author.login
    $body = $review.body
    
    # Skip empty reviews
    if ([string]::IsNullOrWhiteSpace($body)) {
        continue
    }
    
    # Classify review
    $classification = "VALID"
    $priority = "P1"
    $category = "REVIEW"
    
    if ($body -match "Critical|Blocking|MUST|P0") {
        $priority = "P0"
        $category = "CRITICAL"
    }
    elseif ($body -match "struct copy|race condition|atomic|CompareExchange|local variable") {
        $priority = "P0"
        $category = "CONCURRENCY"
    }
    
    $findings += [PSCustomObject]@{
        Source = "review"
        Bot = $author
        Classification = $classification
        Priority = $priority
        Category = $category
        Timestamp = $review.submittedAt
        URL = "$RepoUrl/pull/$PrNumber"
        Body = $body.Substring(0, [Math]::Min(500, $body.Length))
    }
}

# Step 3: Generate forensics report
Write-Host "[3/5] Generating forensics report..." -ForegroundColor Green

$reportPath = "docs/brain/pr_${PrNumber}_forensics.md"
$reportContent = @"
# PR #$PrNumber Bot Forensics Report

**Extraction Metadata**:
- **Timestamp**: $extractionTimestamp
- **Commit**: $extractionCommit
- **Branch**: $(git branch --show-current)
- **Extractor**: extract_pr_forensics.ps1 v2.0

---

## Summary

| Metric | Count |
|--------|-------|
| Total Findings | $($findings.Count) |
| VALID Issues | $(($findings | Where-Object Classification -eq "VALID").Count) |
| HALLUCINATIONS | $(($findings | Where-Object Classification -eq "HALLUCINATION").Count) |
| INFRA-NOISE | $(($findings | Where-Object Classification -eq "INFRA-NOISE").Count) |
| P0 (Critical) | $(($findings | Where-Object Priority -eq "P0").Count) |
| P1 (High) | $(($findings | Where-Object Priority -eq "P1").Count) |
| P2 (Medium) | $(($findings | Where-Object Priority -eq "P2").Count) |

## VALID Issues (Priority Order)

"@

# Group by priority
$validFindings = $findings | Where-Object Classification -eq "VALID" | Sort-Object @{Expression={
    switch ($_.Priority) {
        "P0" { 0 }
        "P1" { 1 }
        "P2" { 2 }
        default { 3 }
    }
}}

foreach ($finding in $validFindings) {
    $reportContent += @"

### [$($finding.Priority)] $($finding.Category) - $($finding.Bot)
**Source:** $($finding.Source)  
**Timestamp:** $($finding.Timestamp)  
**URL:** $($finding.URL)

**Excerpt:**
``````
$($finding.Body)
``````

"@
}

# Add hallucinations section
if ($hallucinations.Count -gt 0) {
    $reportContent += @"

## Hallucinations Log

"@
    foreach ($h in $hallucinations) {
        $reportContent += "- **$($h.bot)** @ $($h.timestamp): $($h.claim)`n"
    }
}

# Add INFRA-NOISE section
$infraNoise = $findings | Where-Object Classification -eq "INFRA-NOISE"
if ($infraNoise.Count -gt 0) {
    $reportContent += @"

## INFRA-NOISE (Ignored)

"@
    foreach ($noise in $infraNoise) {
        $reportContent += "- **$($noise.Bot)**: $($noise.Category)`n"
    }
}

$reportContent | Out-File -FilePath $reportPath -Encoding utf8

# Step 4: Generate fix queue
Write-Host "[4/5] Generating fix queue..." -ForegroundColor Green

$fixQueuePath = "docs/brain/pr_${PrNumber}_fix_queue.md"
$fixQueue = @"
# PR #$PrNumber Fix Queue
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Instructions for v12-engineer

Process these issues in priority order. Mark each as FIXED after applying the fix.

"@

$fixNumber = 1
foreach ($finding in $validFindings) {
    $status = "[ ]"
    $fixQueue += @"

### Fix #$fixNumber - [$($finding.Priority)] $($finding.Category)
$status **Bot:** $($finding.Bot)  
$status **File:** (extract from body)  
$status **Issue:** $($finding.Body.Substring(0, [Math]::Min(200, $finding.Body.Length)))...

**Action Required:**
1. Read the full finding at: $($finding.URL)
2. Apply the fix
3. Verify locally
4. Mark as [x] FIXED

---

"@
    $fixNumber++
}

$fixQueue | Out-File -FilePath $fixQueuePath -Encoding utf8

# Step 5: Update hallucination log
Write-Host "[5/5] Updating persistent hallucination log..." -ForegroundColor Green

$hallucinationLogPath = "docs/brain/bot_hallucinations.md"
if (-not (Test-Path $hallucinationLogPath)) {
    "# Bot Hallucination Log`n`nTracks false positives for pattern learning.`n" | Out-File -FilePath $hallucinationLogPath -Encoding utf8
}

if ($hallucinations.Count -gt 0) {
    $hallucinationEntry = "`n## PR #$PrNumber - $(Get-Date -Format 'yyyy-MM-dd')`n"
    foreach ($h in $hallucinations) {
        $hallucinationEntry += "- **$($h.bot)**: $($h.claim)`n"
    }
    Add-Content -Path $hallucinationLogPath -Value $hallucinationEntry
}

# Summary
Write-Host "`n=== EXTRACTION COMPLETE ===" -ForegroundColor Cyan
Write-Host "Forensics Report: $reportPath" -ForegroundColor Yellow
Write-Host "Fix Queue: $fixQueuePath" -ForegroundColor Yellow
Write-Host "Hallucination Log: $hallucinationLogPath" -ForegroundColor Yellow
Write-Host "`nVALID Issues: $(($validFindings).Count)" -ForegroundColor Green
Write-Host "  P0 (Critical): $(($validFindings | Where-Object Priority -eq 'P0').Count)" -ForegroundColor Red
Write-Host "  P1 (High): $(($validFindings | Where-Object Priority -eq 'P1').Count)" -ForegroundColor Yellow
Write-Host "  P2 (Medium): $(($validFindings | Where-Object Priority -eq 'P2').Count)" -ForegroundColor Cyan
Write-Host "`nHallucinations: $($hallucinations.Count)" -ForegroundColor Magenta
Write-Host "INFRA-NOISE: $(($infraNoise).Count)" -ForegroundColor DarkGray

# Freshness helper function
function Test-ForensicsFreshness {
    param(
        [string]$FilePath,
        [int]$MaxAgeMinutes = 5
    )
    
    if (-not (Test-Path $FilePath)) {
        Write-Error "Forensics file not found: $FilePath"
        return $false
    }
    
    $file = Get-Item $FilePath
    $age = (Get-Date) - $file.LastWriteTime
    
    if ($age.TotalMinutes -gt $MaxAgeMinutes) {
        Write-Warning "Forensics file is stale! Age: $($age.TotalMinutes) minutes"
        return $false
    }
    
    Write-Host "✅ Forensics file is fresh (age: $($age.TotalSeconds) seconds)"
    return $true
}

# Export function for use in other scripts
Export-ModuleMember -Function Test-ForensicsFreshness

exit 0

# Made with Bob
