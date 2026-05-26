<#
.SYNOPSIS
Verifies forensics files are fresh before reading.

.PARAMETER PrNumber
The PR number to verify forensics for.

.PARAMETER MaxAgeMinutes
Maximum age in minutes for forensics to be considered fresh (default: 5).

.EXAMPLE
powershell -File .\scripts\verify_forensics_freshness.ps1 -PrNumber 5
#>

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber,
    
    [Parameter(Mandatory=$false)]
    [int]$MaxAgeMinutes = 5
)

$forensicsFile = "docs/brain/pr_$PrNumber`_forensics.md"
$fixQueueFile = "docs/brain/pr_$PrNumber`_fix_queue.md"

Write-Host "🔍 Verifying forensics freshness for PR #$PrNumber..." -ForegroundColor Cyan

# Check forensics file exists
if (-not (Test-Path $forensicsFile)) {
    Write-Error "❌ Forensics file not found: $forensicsFile"
    exit 1
}

# Check fix queue exists
if (-not (Test-Path $fixQueueFile)) {
    Write-Error "❌ Fix queue file not found: $fixQueueFile"
    exit 1
}

# Check forensics age
$forensicsAge = (Get-Date) - (Get-Item $forensicsFile).LastWriteTime
if ($forensicsAge.TotalMinutes -gt $MaxAgeMinutes) {
    Write-Error "❌ Forensics file is STALE! Age: $($forensicsAge.TotalMinutes) minutes (max: $MaxAgeMinutes)"
    Write-Host "   File: $forensicsFile" -ForegroundColor Red
    Write-Host "   Last Modified: $((Get-Item $forensicsFile).LastWriteTime)" -ForegroundColor Red
    exit 1
}

# Check fix queue age
$queueAge = (Get-Date) - (Get-Item $fixQueueFile).LastWriteTime
if ($queueAge.TotalMinutes -gt $MaxAgeMinutes) {
    Write-Error "❌ Fix queue file is STALE! Age: $($queueAge.TotalMinutes) minutes (max: $MaxAgeMinutes)"
    Write-Host "   File: $fixQueueFile" -ForegroundColor Red
    Write-Host "   Last Modified: $((Get-Item $fixQueueFile).LastWriteTime)" -ForegroundColor Red
    exit 1
}

# Success
Write-Host "✅ Forensics files are FRESH" -ForegroundColor Green
Write-Host "   Forensics age: $($forensicsAge.TotalSeconds) seconds" -ForegroundColor Gray
Write-Host "   Fix queue age: $($queueAge.TotalSeconds) seconds" -ForegroundColor Gray
exit 0

# Made with Bob
