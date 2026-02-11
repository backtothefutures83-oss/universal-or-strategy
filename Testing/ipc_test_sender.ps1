# ═══════════════════════════════════════════════════════════════════
#  V12 IPC TEST SENDER — Phase 6 Testing Rig
#  Purpose: Send raw IPC commands to the V12 strategy via TCP socket
#           to verify command processing without using the Panel UI.
#
#  Usage:
#    Interactive:   .\ipc_test_sender.ps1
#    Single-shot:   .\ipc_test_sender.ps1 -Command "FLATTEN_ONLY"
#    Custom port:   .\ipc_test_sender.ps1 -Port 5002 -Command "BE"
#
#  What to look for: In NinjaTrader Output Window, you should see:
#    "V12 IPC: Received 'FLATTEN_ONLY' for 'Global'. For Me? True"
# ═══════════════════════════════════════════════════════════════════

param(
    [int]$Port = 5001,
    [string]$Command = ""
)

$ErrorActionPreference = "Stop"

function Send-IpcCommand {
    param(
        [string]$Cmd,
        [int]$TargetPort
    )

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.Connect("127.0.0.1", $TargetPort)
        $stream = $client.GetStream()
        $writer = New-Object System.IO.StreamWriter($stream)
        $writer.AutoFlush = $true

        # V12 IPC protocol: commands are newline-terminated strings
        $writer.WriteLine($Cmd)

        Write-Host "  SENT: " -NoNewline -ForegroundColor Green
        Write-Host "$Cmd" -ForegroundColor White
        Write-Host "  PORT: $TargetPort | TIME: $(Get-Date -Format 'HH:mm:ss.fff')" -ForegroundColor DarkGray

        # Brief pause to allow strategy to process before disconnecting
        Start-Sleep -Milliseconds 300

        # Try to read any response (some commands like GET_LAYOUT send data back)
        if ($stream.DataAvailable) {
            $reader = New-Object System.IO.StreamReader($stream)
            $buffer = New-Object char[] 4096
            $bytesRead = $reader.Read($buffer, 0, $buffer.Length)
            if ($bytesRead -gt 0) {
                $response = New-Object string($buffer, 0, $bytesRead)
                Write-Host "  RECV: " -NoNewline -ForegroundColor Cyan
                Write-Host "$response" -ForegroundColor White
            }
        }

        $writer.Close()
        $stream.Close()
        $client.Close()
        return $true
    }
    catch {
        Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Is NinjaTrader running with V12 strategy loaded on a chart?" -ForegroundColor Yellow
        return $false
    }
}

# ─── Banner ───
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  V12 IPC TEST SENDER — Phase 6 Testing Rig         ║" -ForegroundColor Cyan
Write-Host "║  Target: localhost:$Port                              ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ─── Single-Shot Mode ───
if ($Command -ne "") {
    Write-Host "Single-Shot Mode: Sending command..." -ForegroundColor Yellow
    $result = Send-IpcCommand -Cmd $Command -TargetPort $Port
    if ($result) {
        Write-Host "`n  Check NinjaTrader Output Window for confirmation.`n" -ForegroundColor Green
    }
    exit
}

# ─── Interactive Mode ───
Write-Host "Interactive Mode — Select a command or type custom:`n" -ForegroundColor Yellow

$presets = @{
    "1" = @{ cmd = "FLATTEN_ONLY"; desc = "Close positions, keep pending orders" }
    "2" = @{ cmd = "BE"; desc = "Move stops to breakeven + offset" }
    "3" = @{ cmd = "SET_MANUAL_PRICE|6961.25"; desc = "Set manual anchor price" }
    "4" = @{ cmd = "CANCEL_ALL"; desc = "Cancel all pending entry orders" }
    "5" = @{ cmd = "FLATTEN"; desc = "Full flatten (positions + orders)" }
    "6" = @{ cmd = "SET_MODE|RMA"; desc = "Switch to RMA execution mode" }
    "7" = @{ cmd = "SET_MODE|ORB"; desc = "Switch to OR Breakout mode" }
    "8" = @{ cmd = "SET_SIMA|ON"; desc = "Enable SIMA multi-account mode" }
    "9" = @{ cmd = "DIAG_FLEET"; desc = "Diagnostic: show fleet account status" }
    "A" = @{ cmd = "GET_LAYOUT"; desc = "Request current config layout" }
    "B" = @{ cmd = "TRIM_25"; desc = "Trim 25% of active positions" }
    "C" = @{ cmd = "TRIM_50"; desc = "Trim 50% of active positions" }
}

while ($true) {
    Write-Host "─── PRESET COMMANDS ───" -ForegroundColor DarkCyan
    foreach ($key in ($presets.Keys | Sort-Object)) {
        $p = $presets[$key]
        Write-Host "  [$key] " -NoNewline -ForegroundColor Cyan
        Write-Host "$($p.cmd)" -NoNewline -ForegroundColor White
        Write-Host " — $($p.desc)" -ForegroundColor DarkGray
    }
    Write-Host ""
    Write-Host "  [X] " -NoNewline -ForegroundColor Red
    Write-Host "Exit" -ForegroundColor White
    Write-Host ""
    Write-Host "  Or type any raw command string (e.g. 'LONG|MES'):" -ForegroundColor DarkGray
    Write-Host ""

    $input_cmd = Read-Host "  >"

    if ($input_cmd -eq "X" -or $input_cmd -eq "x") {
        Write-Host "`n  Session ended.`n" -ForegroundColor Yellow
        break
    }

    # Resolve preset or use raw input
    if ($presets.ContainsKey($input_cmd.ToUpper())) {
        $finalCmd = $presets[$input_cmd.ToUpper()].cmd
    }
    else {
        $finalCmd = $input_cmd
    }

    if ([string]::IsNullOrWhiteSpace($finalCmd)) {
        Write-Host "  (empty — skipped)" -ForegroundColor DarkGray
        continue
    }

    Write-Host ""
    Send-IpcCommand -Cmd $finalCmd -TargetPort $Port | Out-Null
    Write-Host ""
}
