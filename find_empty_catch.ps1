$files = @(
    'src/V12_002.SIMA.Fleet.cs',
    'src/V12_002.SIMA.Dispatch.cs',
    'src/V12_002.UI.Compliance.cs',
    'src/V12_002.Orders.Callbacks.AccountOrders.cs',
    'src/V12_002.Orders.Management.StopSync.cs'
)

foreach ($file in $files) {
    Write-Host "`n=== $file ==="
    $lines = Get-Content $file
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'catch') {
            $nextLine = if ($i+1 -lt $lines.Count) { $lines[$i+1] } else { "" }
            $nextNextLine = if ($i+2 -lt $lines.Count) { $lines[$i+2] } else { "" }
            
            if ($nextLine -match '^\s*\{\s*$' -and $nextNextLine -match '^\s*\}\s*$') {
                Write-Host "Empty catch at line $($i+1):"
                Write-Host "  $($i+1): $($lines[$i])"
                Write-Host "  $($i+2): $($lines[$i+1])"
                Write-Host "  $($i+3): $($lines[$i+2])"
            }
        }
    }
}

# Made with Bob
