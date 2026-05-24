$ErrorActionPreference = "Stop"

Write-Host "Analyzing bot findings across PRs 1, 2, 4, 6, 8..." -ForegroundColor Cyan

# Load all PR findings
$prs = @(1, 2, 4, 6, 8)
$allFindings = @()

foreach ($prNum in $prs) {
    $jsonPath = "docs/brain/pr${prNum}_findings.json"
    if (Test-Path $jsonPath) {
        $prData = Get-Content $jsonPath -Raw | ConvertFrom-Json
        $allFindings += $prData
        Write-Host "Loaded PR #$prNum findings" -ForegroundColor Green
    } else {
        Write-Host "Warning: $jsonPath not found" -ForegroundColor Yellow
    }
}

# Initialize categorized findings
$categorized = @{
    Security = @()
    Performance = @()
    Complexity = @()
    Style = @()
    ErrorProne = @()
    Maintainability = @()
}

$totalIssues = 0
$issuesByPR = @{}
$issuesByBot = @{}
$issuesByFile = @{}

# Parse cubic findings (most structured)
foreach ($pr in $allFindings) {
    $prNum = $pr.pr_number
    $issuesByPR[$prNum] = 0
    
    foreach ($cubicReview in $pr.bot_findings.cubic) {
        if ($cubicReview.body -match '(?s)<violation number="(\d+)" location="([^"]+)">([^<]+)</violation>') {
            $matches | ForEach-Object {
                if ($_ -match '<violation number="(\d+)" location="([^"]+)">([^<]+)</violation>') {
                    $location = $matches[2]
                    $description = $matches[3].Trim()
                    
                    # Categorize by priority and type
                    $severity = "UNKNOWN"
                    if ($description -match "P0:") { $severity = "CRITICAL" }
                    elseif ($description -match "P1:") { $severity = "HIGH" }
                    elseif ($description -match "P2:") { $severity = "MEDIUM" }
                    elseif ($description -match "P3:") { $severity = "LOW" }
                    
                    $category = "Maintainability"
                    if ($description -match "secret|token|API key|hardcoded") { $category = "Security" }
                    elseif ($description -match "allocation|performance|zero-allocation") { $category = "Performance" }
                    elseif ($description -match "complexity|cyclomatic") { $category = "Complexity" }
                    elseif ($description -match "style|formatting|whitespace") { $category = "Style" }
                    elseif ($description -match "rollback|race|thread|atomic|lock") { $category = "ErrorProne" }
                    
                    $finding = @{
                        PR = $prNum
                        Bot = "cubic"
                        Location = $location
                        Description = $description
                        Severity = $severity
                        Category = $category
                    }
                    
                    $categorized[$category] += $finding
                    $totalIssues++
                    $issuesByPR[$prNum]++
                    
                    # Track by file
                    $file = $location -replace ":.*", ""
                    if (-not $issuesByFile.ContainsKey($file)) {
                        $issuesByFile[$file] = 0
                    }
                    $issuesByFile[$file]++
                    
                    # Track by bot
                    if (-not $issuesByBot.ContainsKey("cubic")) {
                        $issuesByBot["cubic"] = 0
                    }
                    $issuesByBot["cubic"]++
                }
            }
        }
    }
    
    # Parse Codacy findings
    foreach ($codacyReview in $pr.bot_findings.Codacy) {
        if ($codacyReview.body -match "(?s)### Test suggestions.*?- \[ \] (.+?)(?=\n- \[ \]|\n\n|$)") {
            $testSuggestions = $matches[0]
            $finding = @{
                PR = $prNum
                Bot = "Codacy"
                Location = "General"
                Description = "Missing test coverage: $testSuggestions"
                Severity = "MEDIUM"
                Category = "Maintainability"
            }
            $categorized["Maintainability"] += $finding
            $totalIssues++
            $issuesByPR[$prNum]++
            
            if (-not $issuesByBot.ContainsKey("Codacy")) {
                $issuesByBot["Codacy"] = 0
            }
            $issuesByBot["Codacy"]++
        }
    }
    
    # Parse CodeFactor findings
    foreach ($cfReview in $pr.bot_findings.CodeFactor) {
        if ($cfReview.body -match "\[notice\] (\d+-\d+): ([^#]+)#L(\d+)\s+(.+?) \(([^)]+)\)") {
            $file = $matches[2]
            $line = $matches[3]
            $description = $matches[4]
            $rule = $matches[5]
            
            $finding = @{
                PR = $prNum
                Bot = "CodeFactor"
                Location = "${file}:${line}"
                Description = "$description (Rule: $rule)"
                Severity = "LOW"
                Category = "Style"
            }
            $categorized["Style"] += $finding
            $totalIssues++
            $issuesByPR[$prNum]++
            
            if (-not $issuesByFile.ContainsKey($file)) {
                $issuesByFile[$file] = 0
            }
            $issuesByFile[$file]++
            
            if (-not $issuesByBot.ContainsKey("CodeFactor")) {
                $issuesByBot["CodeFactor"] = 0
            }
            $issuesByBot["CodeFactor"]++
        }
    }
}

# Generate markdown report
$report = @"
# Deferred Work Audit - Bot Findings Consolidation

**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC")  
**PRs Analyzed:** #1, #2, #4, #6, #8  
**Total Findings:** $totalIssues

## Executive Summary

### Findings by PR
$(foreach ($pr in $prs) {
    $count = if ($issuesByPR[$pr]) { $issuesByPR[$pr] } else { 0 }
    "- **PR #$pr**: $count findings"
}) -join "`n"

### Findings by Bot
$(foreach ($bot in $issuesByBot.Keys | Sort-Object) {
    "- **$bot**: $($issuesByBot[$bot]) findings"
}) -join "`n"

### Findings by Category
$(foreach ($cat in $categorized.Keys | Sort-Object) {
    $count = $categorized[$cat].Count
    "- **$cat**: $count findings"
}) -join "`n"

### Top 10 Files with Most Issues
$(($issuesByFile.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 10 | ForEach-Object {
    "- **$($_.Key)**: $($_.Value) findings"
}) -join "`n")

---

## Detailed Findings by Category

### Security Issues (Priority: CRITICAL)
**Count:** $($categorized.Security.Count)

$(if ($categorized.Security.Count -gt 0) {
    $categorized.Security | ForEach-Object {
        @"
#### PR #$($_.PR) - $($_.Location)
- **Bot:** $($_.Bot)
- **Severity:** $($_.Severity)
- **Description:** $($_.Description)

"@
    }
} else {
    "No security issues found."
})

---

### Error-Prone Issues (Priority: HIGH)
**Count:** $($categorized.ErrorProne.Count)

$(if ($categorized.ErrorProne.Count -gt 0) {
    $categorized.ErrorProne | ForEach-Object {
        @"
#### PR #$($_.PR) - $($_.Location)
- **Bot:** $($_.Bot)
- **Severity:** $($_.Severity)
- **Description:** $($_.Description)

"@
    }
} else {
    "No error-prone issues found."
})

---

### Complexity Issues (Priority: MEDIUM)
**Count:** $($categorized.Complexity.Count)

$(if ($categorized.Complexity.Count -gt 0) {
    $categorized.Complexity | ForEach-Object {
        @"
#### PR #$($_.PR) - $($_.Location)
- **Bot:** $($_.Bot)
- **Severity:** $($_.Severity)
- **Description:** $($_.Description)

"@
    }
} else {
    "No complexity issues found."
})

---

### Performance Issues (Priority: MEDIUM)
**Count:** $($categorized.Performance.Count)

$(if ($categorized.Performance.Count -gt 0) {
    $categorized.Performance | ForEach-Object {
        @"
#### PR #$($_.PR) - $($_.Location)
- **Bot:** $($_.Bot)
- **Severity:** $($_.Severity)
- **Description:** $($_.Description)

"@
    }
} else {
    "No performance issues found."
})

---

### Maintainability Issues (Priority: LOW)
**Count:** $($categorized.Maintainability.Count)

$(if ($categorized.Maintainability.Count -gt 0) {
    $categorized.Maintainability | ForEach-Object {
        @"
#### PR #$($_.PR) - $($_.Location)
- **Bot:** $($_.Bot)
- **Severity:** $($_.Severity)
- **Description:** $($_.Description)

"@
    }
} else {
    "No maintainability issues found."
})

---

### Style Issues (Priority: LOW)
**Count:** $($categorized.Style.Count)

$(if ($categorized.Style.Count -gt 0) {
    $categorized.Style | ForEach-Object {
        @"
#### PR #$($_.PR) - $($_.Location)
- **Bot:** $($_.Bot)
- **Severity:** $($_.Severity)
- **Description:** $($_.Description)

"@
    }
} else {
    "No style issues found."
})

---

## Pattern Analysis

### Systemic Issues
$(
# Identify patterns
$patterns = @{}
foreach ($cat in $categorized.Keys) {
    foreach ($finding in $categorized[$cat]) {
        $desc = $finding.Description
        # Extract pattern keywords
        if ($desc -match "(hardcoded|secret|token|API key)") {
            $pattern = "Hardcoded Secrets"
        } elseif ($desc -match "(complexity|cyclomatic)") {
            $pattern = "High Complexity"
        } elseif ($desc -match "(allocation|zero-allocation)") {
            $pattern = "Allocation Violations"
        } elseif ($desc -match "(rollback|incomplete)") {
            $pattern = "Incomplete Rollback Logic"
        } elseif ($desc -match "(test|coverage)") {
            $pattern = "Missing Test Coverage"
        } elseif ($desc -match "(style|formatting|parenthesis)") {
            $pattern = "Style Violations"
        } else {
            $pattern = "Other"
        }
        
        if (-not $patterns.ContainsKey($pattern)) {
            $patterns[$pattern] = @()
        }
        $patterns[$pattern] += $finding
    }
}

foreach ($pattern in $patterns.Keys | Sort-Object) {
    $count = $patterns[$pattern].Count
    $prs = ($patterns[$pattern] | Select-Object -ExpandProperty PR -Unique | Sort-Object) -join ", "
    "- **$pattern**: $count occurrences across PRs $prs"
}
)

### High-Impact Clusters
$(
# Group by file
$fileGroups = @{}
foreach ($cat in $categorized.Keys) {
    foreach ($finding in $categorized[$cat]) {
        $file = $finding.Location -replace ":.*", ""
        if (-not $fileGroups.ContainsKey($file)) {
            $fileGroups[$file] = @()
        }
        $fileGroups[$file] += $finding
    }
}

$topFiles = $fileGroups.GetEnumerator() | Sort-Object -Property { $_.Value.Count } -Descending | Select-Object -First 5
foreach ($fileGroup in $topFiles) {
    $file = $fileGroup.Key
    $count = $fileGroup.Value.Count
    $categories = ($fileGroup.Value | Select-Object -ExpandProperty Category -Unique) -join ", "
    "- **$file**: $count findings ($categories)"
}
)

---

## EPIC-7-QUALITY Ticket Specifications

### Ticket 1: Security - Remove Hardcoded Secrets
- **Scope:** Multiple files across PRs #1, #2, #6
- **Findings:** $($patterns["Hardcoded Secrets"].Count) hardcoded API keys/tokens
- **Effort:** Medium (M)
- **Priority:** P0 (CRITICAL)
- **Action:** Rotate all exposed tokens, move to environment variables, add to .gitignore

### Ticket 2: Error-Prone - Complete Circuit Breaker Rollback
- **Scope:** src/V12_002.SIMA.Dispatch.cs
- **Findings:** Incomplete rollback logic in circuit breaker
- **Effort:** Small (S)
- **Priority:** P1 (HIGH)
- **Action:** Add dictionary cleanup and registeredForCleanup reset

### Ticket 3: Maintainability - Add Missing Test Coverage
- **Scope:** Circuit breaker, counter sync, dispatch logic
- **Findings:** 5+ test gaps identified by Codacy
- **Effort:** Large (L)
- **Priority:** P2 (MEDIUM)
- **Action:** Implement unit tests for trip/reset thresholds, state rollback

### Ticket 4: Style - Fix StyleCop Violations
- **Scope:** src/V12_002.SIMA.Dispatch.cs, src/V12_002.SIMA.Fleet.cs
- **Findings:** $($categorized.Style.Count) style violations (SA1111, SA1009)
- **Effort:** Small (S)
- **Priority:** P3 (LOW)
- **Action:** Auto-fix with dotnet format, verify build

### Ticket 5: Maintainability - Clean Up Build Artifacts
- **Scope:** Root directory (query_kb.extracted.py, sync_to_firestore.extracted.py)
- **Findings:** 2 accidentally committed build artifacts
- **Effort:** Extra Small (XS)
- **Priority:** P2 (MEDIUM)
- **Action:** Remove files, add patterns to .gitignore

---

## Next Steps

1. **Immediate (P0):** Rotate all exposed API tokens (Greptile, etc.)
2. **High Priority (P1):** Fix circuit breaker rollback logic
3. **Medium Priority (P2):** Add test coverage for critical paths
4. **Low Priority (P3):** Clean up style violations

**Estimated Total Effort:** 2-3 sprints (assuming 2-week sprints)

---

**End of Report**
"@

$report | Out-File "docs/brain/DEFERRED_WORK_AUDIT.md" -Encoding UTF8

Write-Host ""
Write-Host "Report generated: docs/brain/DEFERRED_WORK_AUDIT.md" -ForegroundColor Green
Write-Host "Total findings: $totalIssues" -ForegroundColor Cyan

# Made with Bob
