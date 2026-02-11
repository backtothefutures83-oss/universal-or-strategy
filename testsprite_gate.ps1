# TestSprite AI Safety Gate for NinjaScript
# Automates code validation before NinjaTrader compilation

# Get API Key from .env
$envContent = Get-Content .env
$keyLine = $envContent | Select-String "TESTSPRITE_API_KEY="
if ($keyLine) {
    $Env:TESTSPRITE_API_KEY = $keyLine.ToString().Split("=")[1].Trim()
}

if (-not $Env:TESTSPRITE_API_KEY) {
    Write-Error "CRITICAL: TESTSPRITE_API_KEY not found in .env"
    exit 1
}

Write-Host "STARTING TESTSPRITE AI SCAN..." -ForegroundColor Cyan

# Define targets — V12 Modular Architecture (Phase 6 Update)
$targets = @(
    "UniversalORStrategyV12_002_Dev.cs",
    "UniversalORStrategyV12_002_Dev.Orders.cs",
    "UniversalORStrategyV12_002_Dev.Entries.cs",
    "UniversalORStrategyV12_002_Dev.UI.cs",
    "UniversalORStrategyV12_002_Dev.SIMA.cs",
    "UniversalORStrategyV12_002_Dev.REAPER.cs",
    "V12StandardPanel_V12_001_Dev.cs"
)

foreach ($file in $targets) {
    if (Test-Path $file) {
        Write-Host "Scanning: $file" -ForegroundColor Yellow
        # Call TestSprite via npx
        cmd /c "npx @testsprite/testsprite-mcp@latest generateCodeAndExecute --file $file --intent 'Validate NinjaScript trading logic, check for null orders, and verify SIMA account loop safety.'"
    }
    else {
        Write-Host "Skip: $file (not found)" -ForegroundColor Gray
    }
}

Write-Host "SCAN COMPLETE." -ForegroundColor Green

# Update cost_tracking.json
try {
    $trackingPath = Join-Path $PSScriptRoot ".agent/state/cost_tracking.json"
    if (Test-Path $trackingPath) {
        $data = Get-Content $trackingPath | ConvertFrom-Json
        $data.testsprite.scans_performed += $targets.Count
        $data.testsprite.estimated_value_usd += ($targets.Count * 0.50) # Assuming $0.50 saved per manual debugging avoided
        $data.total_roi_usd += ($targets.Count * 0.50)
        $data | ConvertTo-Json -Depth 10 | Set-Content $trackingPath
        Write-Host "METRICS SYNCED: +$($targets.Count) Scans tracked." -ForegroundColor Green
    }
}
catch {
    Write-Warning "Failed to update cost_tracking.json: $($_.Exception.Message)"
}
