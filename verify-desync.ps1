# verify-desync.ps1
# WSGTA Infrastructure: Desync Diagnostic + Auto-Fix (Phase 6 Update)
# Compares Repo files with NinjaTrader files to ensure One Source of Truth.
# Now integrates deploy-sync.ps1 for automatic remediation.
#
# Backup: verify-desync.ps1.bak_phase6

$RepoRoot = "C:\WSGTA\universal-or-strategy"
$NtCustomDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"

$Files = @(
    "Indicators\V12StandardPanel_V12_001_Dev.cs",
    "Strategies\UniversalORStrategyV12_002_Dev.cs",
    "Strategies\UniversalORStrategyV12_002_Dev.Entries.cs",
    "Strategies\UniversalORStrategyV12_002_Dev.Orders.cs",
    "Strategies\UniversalORStrategyV12_002_Dev.SIMA.cs",
    "Strategies\UniversalORStrategyV12_002_Dev.REAPER.cs",
    "Strategies\UniversalORStrategyV12_002_Dev.UI.cs",
    "Strategies\SignalBroadcaster.cs"
)

Write-Host "`n--- WSGTA DESYNC AUDIT (Phase 6) ---" -ForegroundColor Cyan
$DesyncedCount = 0
$DesyncedFiles = @()

foreach ($relPath in $Files) {
    $repoPath = Join-Path $RepoRoot (Split-Path $relPath -Leaf)
    $ntPath = Join-Path $NtCustomDir $relPath
    
    if (!(Test-Path $repoPath)) { continue }
    if (!(Test-Path $ntPath)) {
        Write-Host "FAIL: Missing in NT8 -> $relPath" -ForegroundColor Red
        $DesyncedCount++
        $DesyncedFiles += $relPath
        continue
    }

    $ntItem = Get-Item $ntPath
    if ($ntItem.Attributes -match "ReparsePoint") {
        Write-Host "PASS: [LINKED] $relPath" -ForegroundColor Green
    }
    else {
        # Compare content if not linked
        $repoHash = (Get-FileHash $repoPath).Hash
        $ntHash = (Get-FileHash $ntPath).Hash
        
        if ($repoHash -eq $ntHash) {
            Write-Host "PASS: [SYNCED] $relPath" -ForegroundColor Green
        }
        else {
            Write-Host "FAIL: [DRIFTED] $relPath (Contents Different!)" -ForegroundColor Red
            $DesyncedCount++
            $DesyncedFiles += $relPath
        }
    }
}

if ($DesyncedCount -eq 0) {
    Write-Host "`nSUCCESS: All V12 Files are Secure." -ForegroundColor Cyan
}
else {
    Write-Host "`nWARNING: $DesyncedCount file(s) found in Desync state!" -ForegroundColor Yellow
    foreach ($f in $DesyncedFiles) {
        Write-Host "  -> $f" -ForegroundColor DarkYellow
    }

    # Phase 6: Integrated Auto-Fix via deploy-sync.ps1
    $syncScript = Join-Path $RepoRoot "deploy-sync.ps1"
    if (Test-Path $syncScript) {
        Write-Host ""
        Write-Host "AUTO-FIX AVAILABLE: deploy-sync.ps1 can re-link these files." -ForegroundColor Cyan
        Write-Host "This will:" -ForegroundColor Gray
        Write-Host "  1. Backup existing NT8 files (.bak_YYYYMMDD_HHmm)" -ForegroundColor Gray
        Write-Host "  2. Create hard links from Repo -> NT8 bin" -ForegroundColor Gray
        Write-Host ""

        $choice = Read-Host "Apply Auto-Fix? [Y/N]"
        if ($choice -eq "Y" -or $choice -eq "y") {
            Write-Host "`nExecuting deploy-sync.ps1..." -ForegroundColor Cyan
            & $syncScript
            Write-Host "`nAuto-Fix Complete. Re-run verify-desync.ps1 to confirm." -ForegroundColor Green
        }
        else {
            Write-Host "`nSkipped. Run .\deploy-sync.ps1 manually when ready." -ForegroundColor Gray
        }
    }
    else {
        Write-Host "Manual fix required: Run .\deploy-sync.ps1 to fix." -ForegroundColor Gray
    }
}
