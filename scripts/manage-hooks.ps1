# Hook Management CLI
# Centralized management for V12 deterministic hooks
# Usage: .\scripts\manage-hooks.ps1 <command> [options]

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet('list', 'enable', 'disable', 'status', 'test', 'verify', 'diagnostics')]
    [string]$Command,
    
    [Parameter(Position=1)]
    [string]$HookName,
    
    [switch]$Detailed,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

# Configuration
$HooksDir = "$PSScriptRoot\hooks"
$TestsDir = "$PSScriptRoot\..\tests\hooks"
$RegistryFile = "$PSScriptRoot\..\docs\brain\hook_registry.json"

# Hook Registry
$HookRegistry = @{
    "pre-forensics" = @{
        Path = "$HooksDir\pre-forensics.ps1"
        Description = "Verify bot comment freshness before PR forensics extraction"
        Workflow = "extract_pr_forensics.ps1"
        Priority = "P0"
        Enabled = $true
        RequiresParams = $true
    }
    "pre-deploy-sync" = @{
        Path = "$HooksDir\pre-deploy-sync.ps1"
        Description = "Verify build readiness before NinjaTrader sync"
        Workflow = "deploy-sync.ps1"
        Priority = "P0"
        Enabled = $true
        RequiresParams = $false
    }
    "post-deploy-sync" = @{
        Path = "$HooksDir\post-deploy-sync.ps1"
        Description = "Verify hard link integrity after NinjaTrader sync"
        Workflow = "deploy-sync.ps1"
        Priority = "P0"
        Enabled = $true
        RequiresParams = $false
    }
    "pre-ci-log-extraction" = @{
        Path = "$HooksDir\pre-ci-log-extraction.ps1"
        Description = "Verify PR state before CI log extraction"
        Workflow = "extract_ci_logs.ps1"
        Priority = "P0"
        Enabled = $true
        RequiresParams = $true
    }
    "pre-pr-loop" = @{
        Path = "$HooksDir\pre-pr-loop.ps1"
        Description = "Verify branch state before PR perfection loop"
        Workflow = "/pr-loop"
        Priority = "P0"
        Enabled = $true
        RequiresParams = $true
    }
}

# Save registry to file
function Save-Registry {
    $HookRegistry | ConvertTo-Json -Depth 10 | Out-File $RegistryFile -Encoding utf8
}

# Load registry from file
function Load-Registry {
    if (Test-Path $RegistryFile) {
        $script:HookRegistry = Get-Content $RegistryFile -Raw | ConvertFrom-Json -AsHashtable
    }
}

# List all hooks
function Show-HookList {
    Write-Host "`n=== V12 Hook Registry ===" -ForegroundColor Cyan
    Write-Host "Total Hooks: $($HookRegistry.Count)`n" -ForegroundColor White
    
    if ($Json) {
        $HookRegistry | ConvertTo-Json -Depth 10
        return
    }
    
    foreach ($hook in $HookRegistry.GetEnumerator() | Sort-Object { $_.Value.Priority }) {
        $name = $hook.Key
        $info = $hook.Value
        $status = if ($info.Enabled) { "✅ ENABLED" } else { "❌ DISABLED" }
        $statusColor = if ($info.Enabled) { "Green" } else { "Red" }
        
        Write-Host "[$($info.Priority)] $name" -ForegroundColor Yellow
        Write-Host "  Status: $status" -ForegroundColor $statusColor
        Write-Host "  Description: $($info.Description)" -ForegroundColor Gray
        Write-Host "  Workflow: $($info.Workflow)" -ForegroundColor Gray
        Write-Host "  Path: $($info.Path)" -ForegroundColor Gray
        
        if ($Detailed) {
            $exists = Test-Path $info.Path
            Write-Host "  File Exists: $exists" -ForegroundColor $(if ($exists) { "Green" } else { "Red" })
            
            if ($exists) {
                $lines = (Get-Content $info.Path).Count
                Write-Host "  Lines of Code: $lines" -ForegroundColor Gray
            }
        }
        Write-Host ""
    }
}

# Show hook status
function Show-HookStatus {
    param([string]$Name)
    
    if (-not $HookRegistry.ContainsKey($Name)) {
        Write-Host "Hook '$Name' not found in registry" -ForegroundColor Red
        return
    }
    
    $info = $HookRegistry[$Name]
    
    Write-Host "`n=== Hook Status: $Name ===" -ForegroundColor Cyan
    Write-Host "Priority: $($info.Priority)" -ForegroundColor White
    Write-Host "Enabled: $($info.Enabled)" -ForegroundColor $(if ($info.Enabled) { "Green" } else { "Red" })
    Write-Host "Description: $($info.Description)" -ForegroundColor Gray
    Write-Host "Workflow: $($info.Workflow)" -ForegroundColor Gray
    Write-Host "Path: $($info.Path)" -ForegroundColor Gray
    Write-Host "Requires Parameters: $($info.RequiresParams)" -ForegroundColor Gray
    
    # File checks
    $exists = Test-Path $info.Path
    Write-Host "`nFile Exists: $exists" -ForegroundColor $(if ($exists) { "Green" } else { "Red" })
    
    if ($exists) {
        $content = Get-Content $info.Path -Raw
        $lines = ($content -split "`n").Count
        $hasErrorHandling = $content -match '\$ErrorActionPreference'
        $hasExitCodes = $content -match 'exit [01]'
        $isAscii = -not ($content -match '[^\x00-\x7F]')
        
        Write-Host "Lines of Code: $lines" -ForegroundColor Gray
        Write-Host "Error Handling: $hasErrorHandling" -ForegroundColor $(if ($hasErrorHandling) { "Green" } else { "Red" })
        Write-Host "Exit Codes: $hasExitCodes" -ForegroundColor $(if ($hasExitCodes) { "Green" } else { "Red" })
        Write-Host "ASCII-Only: $isAscii" -ForegroundColor $(if ($isAscii) { "Green" } else { "Red" })
    }
}

# Enable hook
function Enable-Hook {
    param([string]$Name)
    
    if (-not $HookRegistry.ContainsKey($Name)) {
        Write-Host "Hook '$Name' not found in registry" -ForegroundColor Red
        return
    }
    
    $HookRegistry[$Name].Enabled = $true
    Save-Registry
    Write-Host "Hook '$Name' enabled" -ForegroundColor Green
}

# Disable hook
function Disable-Hook {
    param([string]$Name)
    
    if (-not $HookRegistry.ContainsKey($Name)) {
        Write-Host "Hook '$Name' not found in registry" -ForegroundColor Red
        return
    }
    
    $HookRegistry[$Name].Enabled = $false
    Save-Registry
    Write-Host "Hook '$Name' disabled" -ForegroundColor Yellow
    Write-Host "WARNING: Disabling hooks may lead to non-deterministic failures" -ForegroundColor Yellow
}

# Test hook
function Test-Hook {
    param([string]$Name)
    
    if ($Name) {
        Write-Host "Testing hook: $Name" -ForegroundColor Cyan
        $testFile = "$TestsDir\$Name.Tests.ps1"
        
        if (-not (Test-Path $testFile)) {
            Write-Host "Test file not found: $testFile" -ForegroundColor Red
            return
        }
        
        & powershell -File "$TestsDir\Run-HookTests.ps1" -TestName $Name
    } else {
        Write-Host "Testing all hooks..." -ForegroundColor Cyan
        & powershell -File "$TestsDir\Run-HookTests.ps1"
    }
}

# Verify all hooks
function Invoke-HookVerification {
    Write-Host "Verifying all hooks..." -ForegroundColor Cyan
    & powershell -File "$PSScriptRoot\verify_hooks.ps1"
}

# Run diagnostics
function Show-Diagnostics {
    Write-Host "`n=== V12 Hook Diagnostics ===" -ForegroundColor Cyan
    
    # Check hooks directory
    Write-Host "`n[1/5] Checking hooks directory..." -ForegroundColor Yellow
    if (Test-Path $HooksDir) {
        $hookFiles = Get-ChildItem $HooksDir -Filter "*.ps1"
        Write-Host "  ✅ Hooks directory exists" -ForegroundColor Green
        Write-Host "  Found $($hookFiles.Count) hook files" -ForegroundColor Gray
    } else {
        Write-Host "  ❌ Hooks directory not found: $HooksDir" -ForegroundColor Red
    }
    
    # Check tests directory
    Write-Host "`n[2/5] Checking tests directory..." -ForegroundColor Yellow
    if (Test-Path $TestsDir) {
        $testFiles = Get-ChildItem $TestsDir -Filter "*.Tests.ps1"
        Write-Host "  ✅ Tests directory exists" -ForegroundColor Green
        Write-Host "  Found $($testFiles.Count) test files" -ForegroundColor Gray
    } else {
        Write-Host "  ❌ Tests directory not found: $TestsDir" -ForegroundColor Red
    }
    
    # Check Pester installation
    Write-Host "`n[3/5] Checking Pester installation..." -ForegroundColor Yellow
    $pester = Get-Module -ListAvailable -Name Pester
    if ($pester) {
        Write-Host "  ✅ Pester installed (version $($pester.Version))" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Pester not installed" -ForegroundColor Red
        Write-Host "  Run: Install-Module -Name Pester -Force -SkipPublisherCheck" -ForegroundColor Yellow
    }
    
    # Check registry file
    Write-Host "`n[4/5] Checking registry file..." -ForegroundColor Yellow
    if (Test-Path $RegistryFile) {
        Write-Host "  ✅ Registry file exists" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Registry file not found (will be created)" -ForegroundColor Yellow
    }
    
    # Check hook integrity
    Write-Host "`n[5/5] Checking hook integrity..." -ForegroundColor Yellow
    $missingHooks = @()
    $brokenHooks = @()
    
    foreach ($hook in $HookRegistry.GetEnumerator()) {
        $name = $hook.Key
        $path = $hook.Value.Path
        
        if (-not (Test-Path $path)) {
            $missingHooks += $name
        } else {
            $content = Get-Content $path -Raw
            if (-not ($content -match 'exit [01]')) {
                $brokenHooks += $name
            }
        }
    }
    
    if ($missingHooks.Count -eq 0 -and $brokenHooks.Count -eq 0) {
        Write-Host "  ✅ All hooks are intact" -ForegroundColor Green
    } else {
        if ($missingHooks.Count -gt 0) {
            Write-Host "  ❌ Missing hooks: $($missingHooks -join ', ')" -ForegroundColor Red
        }
        if ($brokenHooks.Count -gt 0) {
            Write-Host "  ⚠️  Hooks without exit codes: $($brokenHooks -join ', ')" -ForegroundColor Yellow
        }
    }
    
    Write-Host "`n=== Diagnostics Complete ===" -ForegroundColor Cyan
}

# Main execution
Load-Registry

switch ($Command) {
    'list' {
        Show-HookList
    }
    'status' {
        if (-not $HookName) {
            Write-Host "Error: Hook name required for 'status' command" -ForegroundColor Red
            Write-Host "Usage: .\manage-hooks.ps1 status <hook-name>" -ForegroundColor Yellow
            exit 1
        }
        Show-HookStatus -Name $HookName
    }
    'enable' {
        if (-not $HookName) {
            Write-Host "Error: Hook name required for 'enable' command" -ForegroundColor Red
            Write-Host "Usage: .\manage-hooks.ps1 enable <hook-name>" -ForegroundColor Yellow
            exit 1
        }
        Enable-Hook -Name $HookName
    }
    'disable' {
        if (-not $HookName) {
            Write-Host "Error: Hook name required for 'disable' command" -ForegroundColor Red
            Write-Host "Usage: .\manage-hooks.ps1 disable <hook-name>" -ForegroundColor Yellow
            exit 1
        }
        Disable-Hook -Name $HookName
    }
    'test' {
        Test-Hook -Name $HookName
    }
    'verify' {
        Invoke-HookVerification
    }
    'diagnostics' {
        Show-Diagnostics
    }
}

# Made with Bob
