param (
    [string]$SrcPath = "C:\WSGTA\universal-or-strategy\src",
    [string]$NtPath = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies"
)

Write-Host "=== HARD LINK INTEGRITY AUDIT ===" -ForegroundColor Cyan
Write-Host "SRC : $SrcPath"
Write-Host "NT8 : $NtPath"
Write-Host ""

$desyncs = 0
$missing = 0
$ok = 0

Get-ChildItem $SrcPath -Filter "*.cs" | ForEach-Object {
    $srcFile = $_.FullName
    $ntFile  = Join-Path $NtPath $_.Name

    if (-not (Test-Path $ntFile)) {
        Write-Host "MISSING  : $($_.Name)" -ForegroundColor Red
        $missing++
        return
    }

    $srcHash = (Get-FileHash $srcFile -Algorithm MD5).Hash
    $ntHash  = (Get-FileHash $ntFile  -Algorithm MD5).Hash

    if ($srcHash -eq $ntHash) {
        Write-Host "OK       : $($_.Name)" -ForegroundColor Green
        $ok++
    } else {
        Write-Host "DESYNC   : $($_.Name)  [src=$srcHash] [nt=$ntHash]" -ForegroundColor Red
        $desyncs++
    }
}

Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "OK      : $ok"      -ForegroundColor Green
Write-Host "DESYNC  : $desyncs" -ForegroundColor $(if ($desyncs -gt 0) { "Red" } else { "Green" })
Write-Host "MISSING : $missing" -ForegroundColor $(if ($missing -gt 0) { "Red" } else { "Green" })

if (($desyncs + $missing) -eq 0) {
    Write-Host ""
    Write-Host "PASS -- All source files match NinjaTrader. No stale DLL risk." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "FAIL -- Run deploy-sync.ps1 immediately then F5 compile." -ForegroundColor Red
    exit 1
}
