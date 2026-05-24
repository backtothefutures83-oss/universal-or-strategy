# Fetch complete PR audit data from GitHub
param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber
)

$ErrorActionPreference = "Continue"
$outputDir = "docs/brain"

Write-Host "[+] Fetching complete audit data for PR #$PrNumber..." -ForegroundColor Cyan

# 1. Fetch PR comments
Write-Host "[+] Fetching PR comments..." -ForegroundColor Yellow
gh api "repos/mdasdispatch-hash/universal-or-strategy/issues/$PrNumber/comments" | Out-File "$outputDir/pr_${PrNumber}_comments_raw.json" -Encoding UTF8

# 2. Fetch PR reviews
Write-Host "[+] Fetching PR reviews..." -ForegroundColor Yellow
gh api "repos/mdasdispatch-hash/universal-or-strategy/pulls/$PrNumber/reviews" | Out-File "$outputDir/pr_${PrNumber}_reviews_raw.json" -Encoding UTF8

# 3. Fetch review comments (inline code comments)
Write-Host "[+] Fetching review comments..." -ForegroundColor Yellow
gh api "repos/mdasdispatch-hash/universal-or-strategy/pulls/$PrNumber/comments" | Out-File "$outputDir/pr_${PrNumber}_review_comments_raw.json" -Encoding UTF8

# 4. Fetch check runs
Write-Host "[+] Fetching check runs..." -ForegroundColor Yellow
$prData = gh pr view $PrNumber --json headRefOid | ConvertFrom-Json
$sha = $prData.headRefOid
gh api "repos/mdasdispatch-hash/universal-or-strategy/commits/$sha/check-runs" | Out-File "$outputDir/pr_${PrNumber}_check_runs_raw.json" -Encoding UTF8

# 5. Fetch status checks
Write-Host "[+] Fetching status checks..." -ForegroundColor Yellow
gh api "repos/mdasdispatch-hash/universal-or-strategy/commits/$sha/status" | Out-File "$outputDir/pr_${PrNumber}_status_checks_raw.json" -Encoding UTF8

# 6. Parse and format comments
Write-Host "[+] Parsing comments..." -ForegroundColor Yellow
$comments = Get-Content "$outputDir/pr_${PrNumber}_comments_raw.json" | ConvertFrom-Json
$formatted = ""
foreach ($comment in $comments) {
    $formatted += "## Comment by $($comment.user.login) at $($comment.created_at)`n`n"
    $formatted += "$($comment.body)`n`n"
    $formatted += "---`n`n"
}
$formatted | Out-File "$outputDir/pr_${PrNumber}_comments.md" -Encoding UTF8

# 7. Parse and format reviews
Write-Host "[+] Parsing reviews..." -ForegroundColor Yellow
$reviews = Get-Content "$outputDir/pr_${PrNumber}_reviews_raw.json" | ConvertFrom-Json
$formatted = ""
foreach ($review in $reviews) {
    $formatted += "## Review by $($review.user.login) - $($review.state)`n`n"
    if ($review.body) {
        $formatted += "$($review.body)`n`n"
    }
    $formatted += "---`n`n"
}
$formatted | Out-File "$outputDir/pr_${PrNumber}_reviews.md" -Encoding UTF8

# 8. Parse and format review comments
Write-Host "[+] Parsing review comments..." -ForegroundColor Yellow
$reviewComments = Get-Content "$outputDir/pr_${PrNumber}_review_comments_raw.json" | ConvertFrom-Json
$formatted = ""
foreach ($rc in $reviewComments) {
    $formatted += "## $($rc.user.login) on $($rc.path):$($rc.position)`n`n"
    $formatted += "$($rc.body)`n`n"
    $formatted += "---`n`n"
}
$formatted | Out-File "$outputDir/pr_${PrNumber}_review_comments.md" -Encoding UTF8

# 9. Parse and format check runs
Write-Host "[+] Parsing check runs..." -ForegroundColor Yellow
$checkRuns = Get-Content "$outputDir/pr_${PrNumber}_check_runs_raw.json" | ConvertFrom-Json
$formatted = ""
foreach ($run in $checkRuns.check_runs) {
    $formatted += "## $($run.name) - $($run.conclusion)`n`n"
    $formatted += "Started: $($run.started_at)`n"
    $formatted += "Completed: $($run.completed_at)`n`n"
    if ($run.output.summary) {
        $formatted += "$($run.output.summary)`n`n"
    }
    $formatted += "---`n`n"
}
$formatted | Out-File "$outputDir/pr_${PrNumber}_check_runs.md" -Encoding UTF8

# 10. Parse and format status checks
Write-Host "[+] Parsing status checks..." -ForegroundColor Yellow
$statusChecks = Get-Content "$outputDir/pr_${PrNumber}_status_checks_raw.json" | ConvertFrom-Json
$formatted = ""
foreach ($status in $statusChecks.statuses) {
    $formatted += "## $($status.context) - $($status.state)`n`n"
    $formatted += "$($status.description)`n`n"
    $formatted += "Target: $($status.target_url)`n`n"
    $formatted += "---`n`n"
}
$formatted | Out-File "$outputDir/pr_${PrNumber}_status_checks.md" -Encoding UTF8

Write-Host "[+] Complete audit data saved to $outputDir/" -ForegroundColor Green
Write-Host "    - pr_${PrNumber}_comments.md" -ForegroundColor White
Write-Host "    - pr_${PrNumber}_reviews.md" -ForegroundColor White
Write-Host "    - pr_${PrNumber}_review_comments.md" -ForegroundColor White
Write-Host "    - pr_${PrNumber}_check_runs.md" -ForegroundColor White
Write-Host "    - pr_${PrNumber}_status_checks.md" -ForegroundColor White

# Made with Bob
