# verify_pr_separation.ps1
# Verifies that a PR contains ONLY src/ OR ONLY non-src/ files (never both)
# Part of V12 PR Separation Protocol

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber
)

$ErrorActionPreference = "Stop"

Write-Host "[PR-SEPARATION] Verifying PR #$PrNumber..." -ForegroundColor Cyan

# Get PR files
try {
    $filesJson = gh pr view $PrNumber --json files --jq '.files[].path' 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to fetch PR files. Is gh CLI authenticated?"
        exit 1
    }
    $files = $filesJson -split "`n" | Where-Object { $_ -ne "" }
} catch {
    Write-Error "Failed to fetch PR #$PrNumber : $_"
    exit 1
}

if ($files.Count -eq 0) {
    Write-Host "[PR-SEPARATION] ✅ PR is empty (no files changed)" -ForegroundColor Green
    exit 0
}

# Categorize files
$srcFiles = @($files | Where-Object { $_ -match '^src/' })
$nonSrcFiles = @($files | Where-Object { $_ -notmatch '^src/' })

Write-Host "`n[PR-SEPARATION] File Analysis:" -ForegroundColor Yellow
Write-Host "  Total files: $($files.Count)"
Write-Host "  src/ files: $($srcFiles.Count)"
Write-Host "  non-src/ files: $($nonSrcFiles.Count)"

# Check for violation
if ($srcFiles.Count -gt 0 -and $nonSrcFiles.Count -gt 0) {
    Write-Host "`n[PR-SEPARATION] ❌ VIOLATION: Mixed src/ and non-src/ files detected" -ForegroundColor Red
    Write-Host "`nV12 PR Separation Protocol requires:" -ForegroundColor Yellow
    Write-Host "  1. src/ PR: Production code changes only (full bot audit)"
    Write-Host "  2. non-src/ PR: Docs, tests, workflows, scripts (fast-track)"
    Write-Host "`nsrc/ files in this PR:" -ForegroundColor Red
    $srcFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nnon-src/ files in this PR:" -ForegroundColor Red
    $nonSrcFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nAction Required: Split this PR into two separate PRs" -ForegroundColor Yellow
    exit 1
}

# Success
if ($srcFiles.Count -gt 0) {
    Write-Host "`n[PR-SEPARATION] ✅ VALID: src/ PR (production code only)" -ForegroundColor Green
    Write-Host "  - Full bot audit will run (CodeRabbit, Codacy, Semgrep)"
    Write-Host "  - PHS target: 85+"
} else {
    Write-Host "`n[PR-SEPARATION] ✅ VALID: non-src/ PR (documentation/tooling)" -ForegroundColor Green
    Write-Host "  - Lightweight review or fast-track merge"
}

exit 0

# Made with Bob
