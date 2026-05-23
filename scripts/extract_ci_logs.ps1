# V12 CI Log Extraction Script
# Extracts GitHub Actions workflow run logs for PR diagnostics
# Made with Bob (v12-engineer)

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "docs/brain/pr_${PrNumber}_ci_logs.md"
)

Write-Host "[CI-LOG-EXTRACTOR] Starting extraction for PR #$PrNumber" -ForegroundColor Cyan

# Get all workflow runs for the PR
Write-Host "[CI-LOG-EXTRACTOR] Fetching workflow runs..." -ForegroundColor Yellow
$runs = gh run list --json databaseId,name,conclusion,createdAt,headBranch --limit 50 | ConvertFrom-Json

if (-not $runs) {
    Write-Host "[CI-LOG-EXTRACTOR] ERROR: No workflow runs found" -ForegroundColor Red
    exit 1
}

# Filter runs related to this PR (by timing or branch)
$prRuns = $runs | Where-Object { $_.conclusion -in @("failure", "cancelled", "timed_out") }

if (-not $prRuns) {
    Write-Host "[CI-LOG-EXTRACTOR] No failed runs found for PR #$PrNumber" -ForegroundColor Green
    exit 0
}

Write-Host "[CI-LOG-EXTRACTOR] Found $($prRuns.Count) failed runs" -ForegroundColor Yellow

# Extract logs for each failed run
$logReport = @"
# CI Log Forensics Report - PR #$PrNumber
**Generated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC")
**Source**: GitHub Actions workflow runs

---

"@

foreach ($run in $prRuns) {
    Write-Host "[CI-LOG-EXTRACTOR] Extracting logs for run $($run.databaseId)..." -ForegroundColor Yellow
    
    $logReport += @"
## Run: $($run.name) (ID: $($run.databaseId))
**Status**: $($run.conclusion)
**Created**: $($run.createdAt)
**Branch**: $($run.headBranch)

### Raw Logs
``````
$(gh run view $($run.databaseId) --log 2>&1)
``````

---

"@
}

# Write report
$logReport | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "[CI-LOGS-EXTRACTED] Report written to: $OutputPath" -ForegroundColor Green
Write-Host "[CI-LOG-EXTRACTOR] Extraction complete" -ForegroundColor Cyan