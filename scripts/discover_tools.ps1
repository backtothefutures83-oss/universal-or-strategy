# Tool Discovery Script
# Scans system for ALL installed V12 tools
# Outputs: docs/brain/session_tools.json

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$sessionId = Get-Date -Format "yyyy-MM-dd-HH-mm"

$tools = @{
    session_id = $sessionId
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
    mcp_servers = @()
    scripts = @()
    binaries = @()
    extensions = @()
    failed = @()
}

Write-Host "[TOOL-DISCOVERY] Starting tool scan..." -ForegroundColor Cyan

# Scan MCP servers
if (Test-Path ".mcp/config.json") {
    try {
        $mcpConfig = Get-Content ".mcp/config.json" | ConvertFrom-Json
        $tools.mcp_servers = $mcpConfig.mcpServers.PSObject.Properties.Name
        Write-Host "[MCP] Found $($tools.mcp_servers.Count) MCP servers" -ForegroundColor Green
    } catch {
        Write-Host "[MCP] Failed to parse .mcp/config.json: $_" -ForegroundColor Red
        $tools.failed += "mcp_config_parse"
    }
}

# Scan scripts
$scriptPatterns = @("scripts/*.ps1", "scripts/*.py")
foreach ($pattern in $scriptPatterns) {
    $found = Get-ChildItem $pattern -ErrorAction SilentlyContinue
    if ($found) {
        $tools.scripts += $found | Select-Object -ExpandProperty Name
    }
}
Write-Host "[SCRIPTS] Found $($tools.scripts.Count) scripts" -ForegroundColor Green

# Scan binaries
$binaries = @(
    "dotnet", "git", "gh", "routa", "graphify", "bob", 
    "jcodemunch-mcp", "csharpier", "semgrep", "python"
)

foreach ($bin in $binaries) {
    try {
        $cmd = Get-Command $bin -ErrorAction SilentlyContinue
        if ($cmd) {
            $toolInfo = @{
                name = $bin
                path = $cmd.Source
                version = $null
            }
            
            # Try to get version
            try {
                $version = switch ($bin) {
                    "dotnet" { & dotnet --version 2>$null }
                    "git" { & git --version 2>$null }
                    "gh" { & gh --version 2>$null | Select-Object -First 1 }
                    "routa" { & routa --version 2>$null }
                    "python" { & python --version 2>$null }
                    "graphify" { & graphify --version 2>$null }
                    "bob" { & bob --version 2>$null }
                    "csharpier" { & csharpier --version 2>$null }
                    "semgrep" { & semgrep --version 2>$null }
                    default { $null }
                }
                if ($version) {
                    $toolInfo.version = $version.Trim()
                }
            } catch {
                # Version check failed, but binary exists
            }
            
            $tools.binaries += $toolInfo
            
            if ($Verbose) {
                Write-Host "  [OK] $bin" -ForegroundColor Green
            }
        } else {
            $tools.failed += $bin
            if ($Verbose) {
                Write-Host "  [X] $bin" -ForegroundColor Red
            }
        }
    } catch {
        $tools.failed += $bin
        if ($Verbose) {
            Write-Host "  [X] $bin - $_" -ForegroundColor Red
        }
    }
}

Write-Host "[BINARIES] Found $($tools.binaries.Count) binaries" -ForegroundColor Green

# Calculate totals
$totalTools = $tools.mcp_servers.Count + $tools.scripts.Count + $tools.binaries.Count
$tools.tools_available = $totalTools
$tools.tools_verified = $totalTools - $tools.failed.Count
$tools.tools_failed = $tools.failed.Count

# Ensure output directory exists
$outputDir = "docs/brain"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Output manifest
$outputPath = "$outputDir/session_tools.json"
$tools | ConvertTo-Json -Depth 10 | Out-File $outputPath -Encoding UTF8

Write-Host "`n[TOOL-DISCOVERY] Complete" -ForegroundColor Cyan
Write-Host "  Total: $totalTools tools" -ForegroundColor White
Write-Host "  Verified: $($tools.tools_verified) tools" -ForegroundColor Green
Write-Host "  Failed: $($tools.tools_failed) tools" -ForegroundColor $(if ($tools.tools_failed -gt 0) { "Yellow" } else { "Green" })
Write-Host "  Output: $outputPath" -ForegroundColor White

if ($tools.failed.Count -gt 0) {
    Write-Host "`n[WARNING] Missing tools:" -ForegroundColor Yellow
    $tools.failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}

exit 0

# Made with Bob
