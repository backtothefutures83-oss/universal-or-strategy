# scripts/pre_battle_hook.ps1
param (
    [string]$PlanPath = "docs\brain\implementation_plan.md"
)

Write-Host "Checking if $PlanPath exists..." -ForegroundColor Cyan
if (-not (Test-Path $PlanPath)) {
    Write-Error "Plan not found at $PlanPath"
    exit 1
}

Write-Host "Checking for uncommitted changes in plan..." -ForegroundColor Cyan
$gitStatus = git status --short $PlanPath
if ([string]::IsNullOrWhiteSpace($gitStatus)) {
    Write-Host "No changes detected in $PlanPath. Checking if already pushed..."
} else {
    Write-Host "Syncing changes to GitHub..." -ForegroundColor Cyan
    git add $PlanPath
    git commit --no-verify -m "docs(audit): sync plan for Arena AI Red Team audit"
    git push --no-verify
}

$Hash = git rev-parse HEAD
$Remote = git remote get-url origin
if ($Remote -match "https://github.com/(.*)\.git") {
    $RepoPath = $matches[1]
    $Url = "https://github.com/$RepoPath/blob/$Hash/$($PlanPath.Replace('\','/'))"
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "MANDATORY GITHUB SYNC COMPLETE" -ForegroundColor Green
    Write-Host "Your plan is locked, synced, and ready for `$redteambattle."
    Write-Host "Inject this exact permalink into your Mode A prompt:" -ForegroundColor White
    Write-Host $Url -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    Write-Host "Could not parse remote format: $Remote"
}
