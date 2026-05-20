# scripts/reset_workspace_storage.ps1
# Resets the bloated SQLite database in IBM Bob's workspaceStorage to prevent event-loop freezes.

$storagePath = "%USERPROFILE%\AppData\Roaming\IBM Bob\User\workspaceStorage\05e6c2b33f73f128ebb95431ccb812da"
$dbFile = "$storagePath\state.vscdb"
$backupFile = "$storagePath\state.vscdb.backup"

Write-Host "--- Workspace Storage Reset Utility ---" -ForegroundColor Cyan

# Check if files exist
if (!(Test-Path $dbFile)) {
    Write-Host "No storage file found at $dbFile. Workspace is already clean." -ForegroundColor Green
    exit 0
}

Write-Host "Attempting to reset bloated databases..." -ForegroundColor Yellow

# Loop until successful (waiting for user to close IDE)
$attempts = 0
$maxAttempts = 15
while ($attempts -lt $maxAttempts) {
    try {
        if (Test-Path $dbFile) {
            Remove-Item -Path $dbFile -Force -ErrorAction Stop
            Write-Host "[SUCCESS] Deleted state.vscdb" -ForegroundColor Green
        }
        if (Test-Path $backupFile) {
            Remove-Item -Path $backupFile -Force -ErrorAction Stop
            Write-Host "[SUCCESS] Deleted state.vscdb.backup" -ForegroundColor Green
        }
        break
    }
    catch {
        $attempts++
        Write-Host "[ATTEMPT $attempts/$maxAttempts] Files are locked. PLEASE CLOSE THE IBM BOB IDE NOW to release the lock." -ForegroundColor Red
        if ($attempts -lt $maxAttempts) {
            Start-Sleep -Seconds 10
        }
    }
}

if ($attempts -eq $maxAttempts) {
    Write-Host "FAILED to reset storage. Please manually close the IBM Bob IDE and run this script again." -ForegroundColor Red
    exit 1
}

Write-Host "Workspace storage successfully reset! You can now reopen IBM Bob IDE safely." -ForegroundColor Green
