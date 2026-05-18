# scripts/jules_bug_bounty_sweep.ps1
# V12 Photon Kernel -- Jules Parallel Bug Bounty Dispatcher
# Automatically dispatches 7 parallel Cloud VMs, one per cluster scope.
$ErrorActionPreference = "Stop"

$repo = "mkalhitti-cloud/universal-or-strategy"
$specPath = "docs/brain/bug_bounty_workflow.md"

# Define the 7 clusters and their specific files
$clusters = @(
    @{
        Id = "S1"
        Name = "SIMA Core"
        Files = "src/V12_002.LogicAudit.cs, src/V12_002.Orders.Callbacks.cs"
    },
    @{
        Id = "S2"
        Name = "Execution Engine"
        Files = "src/V12_002.Orders.Management.StopSync.cs, src/V12_002.Trailing.StopUpdate.cs"
    },
    @{
        Id = "S3"
        Name = "UI & Photon IO"
        Files = "src/V12_002.UI.Panel.Handlers.cs, src/V12_002.UI.Callbacks.cs"
    },
    @{
        Id = "S4"
        Name = "REAPER Defense"
        Files = "src/V12_002.Watchdog.cs, src/V12_002.Diagnostics.cs"
    },
    @{
        Id = "S5"
        Name = "Kernel State"
        Files = "src/V12_002.State.Fsm.cs, src/V12_002.State.Variables.cs"
    },
    @{
        Id = "S6"
        Name = "Signals & Entries"
        Files = "src/V12_002.Entry.RetestLong.cs, src/V12_002.Entry.RetestShort.cs"
    },
    @{
        Id = "S7"
        Name = "Kernel Infrastructure"
        Files = "src/V12_002.Services.cs, src/V12_002.Config.cs"
    }
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "🚀 V12 PHOTON KERNEL -- DISPATCHING JULES BUG BOUNTY SWEEP" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

foreach ($c in $clusters) {
    $id = $c.Id
    $name = $c.Name
    $files = $c.Files
    
    $prompt = "SPEC REF: $specPath. Run a focused bug hunt on Cluster ${id}: $name. Scope: $files. Output: docs/brain/bug_report_$( $id.ToLower() ).md"
    
    Write-Host "Spawning Agent-$id ($name)..." -ForegroundColor Yellow
    # Trigger the cloud VM session
    jules remote new --repo $repo --session "$prompt"
    
    # Brief pause to prevent rate limit spikes
    Start-Sleep -Seconds 2
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "✅ ALL 7 JULES AGENTS SUCCESSFULLY DISPATCHED IN PARALLEL!" -ForegroundColor Green
Write-Host "Use 'jules remote list --session' to track their progress." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
