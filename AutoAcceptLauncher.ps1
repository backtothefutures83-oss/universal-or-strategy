# Antigravity Auto-Accept Launcher
# This script restarts the Antigravity IDE with the correct debug port (9222)
# to enable the Auto-Accept 'Stop Asking Me' mode.
# PORT MUST MATCH what is set in the Auto Accept Control Panel (CDP Port field).

$cdpPort = 9222

$exeCandidates = @(
    "$env:LOCALAPPDATA\Programs\Antigravity\Antigravity.exe", 
    "$env:ProgramFiles\Antigravity\Antigravity.exe", 
    "$env:ProgramFiles(x86)\Antigravity\Antigravity.exe"
); 

$exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1; 

if (-not $exe) { 
    Write-Host '(!) Antigravity IDE executable not found in standard paths.'; 
    exit 1 
}; 

Write-Host ">>> Launching Antigravity with Auto-Accept bridge on port $cdpPort...";
Start-Process $exe -ArgumentList "--remote-debugging-port=$cdpPort";
Write-Host ">>> SUCCESS. Port: $cdpPort. Keep the Auto-Accept Panel open in the IDE.";
Write-Host ">>> Next: In the Control Panel, click 'Toggle Background Mode' to enable always-on accepting.";
