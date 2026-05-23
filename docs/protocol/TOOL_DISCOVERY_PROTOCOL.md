# Tool Discovery Protocol

## Purpose

ALL agents MUST discover and verify installed tools at session start (inspired by Droid's 50+ tool initialization).

## Mandatory Session Initialization

### Step 1: Tool Discovery Scan

```powershell
# Run at session start
powershell -File .\scripts\discover_tools.ps1
```

**Output**: `docs/brain/session_tools.json`

### Step 2: Tool Verification

Verify each discovered tool:
- [ ] Executable exists
- [ ] Version check passes
- [ ] Configuration valid
- [ ] Dependencies met

### Step 3: Tool Manifest

Generate session-specific tool manifest:

```json
{
  "session_id": "2026-05-23-23-07",
  "tools_available": 47,
  "tools_verified": 45,
  "tools_failed": 2,
  "categories": {
    "code_navigation": ["jcodemunch-mcp", "lsp-mcp"],
    "knowledge_graph": ["graphify"],
    "knowledge_base": ["query_kb.py"],
    "build_tools": ["dotnet", "msbuild"],
    "testing": ["xunit", "benchmarkdotnet"],
    "quality": ["semgrep", "coderabbit-cli"],
    "deployment": ["deploy-sync.ps1"],
    "git": ["gh", "git"],
    "routa": ["routa.exe"]
  }
}
```

## Tool Categories

### 1. Code Navigation (MCP Servers)

- **jcodemunch-mcp**: Primary code navigation, symbol search, blast radius
- **lsp-mcp** (cjl-lsp-mcp): Language Server Protocol integration
- **greptile-mcp**: Semantic code search (via Quarkus bridge)
- **sequential-thinking-mcp**: Chain-of-thought reasoning

### 2. Knowledge Systems

- **graphify**: Knowledge graph generation and navigation
- **query_kb.py**: Jane Street Knowledge Base (Firestore RAG)
- **Routa CLI**: Architecture analysis, multi-file refactoring

### 3. Build & Deploy

- **dotnet CLI**: .NET build system
- **MSBuild**: Microsoft Build Engine
- **deploy-sync.ps1**: NinjaTrader hard link synchronization
- **build_readiness.ps1**: Pre-deployment verification

### 4. Testing & Quality

- **xUnit**: Unit testing framework
- **BenchmarkDotNet**: Performance benchmarking
- **AMAL Harness**: Automated multi-agent loop testing
- **Semgrep**: Static analysis (V12 DNA patterns)
- **CodeRabbit CLI**: Local AI code review
- **Codacy CLI**: Code quality metrics
- **complexity_audit.py**: Cyclomatic complexity analysis

### 5. Git & GitHub

- **gh** (GitHub CLI): PR management, issue tracking
- **git**: Version control
- **extract_pr_forensics.ps1**: PR analysis and metrics
- **verify_pr_separation.ps1**: src/non-src separation enforcement
- **verify_pr_hygiene.ps1**: Rebase and cleanliness checks
- **calculate_fleet_score.ps1**: Project Health Score calculation

### 6. Formatting & Linting

- **CSharpier**: C# code formatter
- **Roslyn analyzers**: C# static analysis
- **StyleCop**: C# style enforcement
- **check_ascii.py**: ASCII-only compliance verification

### 7. Monitoring & Observability

- **LangSmith**: Agent tracing and observability
- **nexus_relay.py**: Inter-agent handoff coordination
- **langsmith_bridge.py**: LangSmith integration

### 8. Specialized Tools

- **Routa CLI**: Multi-file refactoring, architecture analysis
- **complexity_audit.py**: Complexity threshold enforcement
- **dead_code_scan.py**: Unreachable code detection
- **run_semgrep.ps1**: Semgrep execution wrapper

## Implementation

### discover_tools.ps1

```powershell
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
            $tools.binaries += @{
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
                    default { $null }
                }
                if ($version) {
                    $tools.binaries[-1].version = $version.Trim()
                }
            } catch {
                # Version check failed, but binary exists
            }
            
            if ($Verbose) {
                Write-Host "  [✓] $bin" -ForegroundColor Green
            }
        } else {
            $tools.failed += $bin
            if ($Verbose) {
                Write-Host "  [✗] $bin" -ForegroundColor Red
            }
        }
    } catch {
        $tools.failed += $bin
        if ($Verbose) {
            Write-Host "  [✗] $bin - $_" -ForegroundColor Red
        }
    }
}

Write-Host "[BINARIES] Found $($tools.binaries.Count) binaries" -ForegroundColor Green

# Calculate totals
$totalTools = $tools.mcp_servers.Count + $tools.scripts.Count + $tools.binaries.Count
$tools.tools_available = $totalTools
$tools.tools_verified = $totalTools - $tools.failed.Count
$tools.tools_failed = $tools.failed.Count

# Output manifest
$outputPath = "docs/brain/session_tools.json"
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
```

## Agent Integration

### Mandatory Session Initialization

ALL agents MUST run tool discovery at session start:

```powershell
# Step 1: Discover tools
powershell -File .\scripts\discover_tools.ps1

# Step 2: Load tool manifest
$tools = Get-Content "docs/brain/session_tools.json" | ConvertFrom-Json

# Step 3: Verify critical tools
$criticalTools = @(
    "jcodemunch-mcp",  # Code navigation
    "graphify",        # Knowledge graph
    "query_kb.py",     # Jane Street KB
    "routa",           # Architecture
    "deploy-sync.ps1"  # Deployment
)

$missing = $criticalTools | Where-Object { $_ -notin $tools.binaries.name -and $_ -notin $tools.scripts }

if ($missing) {
    Write-Host "[CRITICAL] Missing tools: $($missing -join ', ')" -ForegroundColor Red
    Write-Host "[HALT] Cannot proceed without critical tools" -ForegroundColor Red
    exit 1
}
```

### Tool Availability Check

Before using any tool, verify it's available:

```powershell
function Test-ToolAvailable {
    param([string]$ToolName)
    
    $tools = Get-Content "docs/brain/session_tools.json" | ConvertFrom-Json
    
    $available = (
        $ToolName -in $tools.mcp_servers -or
        $ToolName -in $tools.scripts -or
        $ToolName -in $tools.binaries.name
    )
    
    if (-not $available) {
        Write-Host "[ERROR] Tool not available: $ToolName" -ForegroundColor Red
        Write-Host "[HINT] Run: powershell -File .\scripts\discover_tools.ps1" -ForegroundColor Yellow
        return $false
    }
    
    return $true
}

# Usage
if (Test-ToolAvailable "jcodemunch-mcp") {
    # Use jcodemunch-mcp
}
```

## Droid-Style Initialization

Inspired by Droid's 50+ tool initialization:

1. **Parallel Discovery**: Run tool discovery BEFORE accepting any task
2. **Async Verification**: Verify ALL tools in parallel (async)
3. **Session Cache**: Cache results for session duration
4. **Auto Re-verify**: Re-verify on tool failure
5. **User Reporting**: Report tool availability to user at session start

### Example Session Start

```
[TOOL-DISCOVERY] Starting tool scan...
[MCP] Found 4 MCP servers
[SCRIPTS] Found 23 scripts
[BINARIES] Found 12 binaries

[TOOL-DISCOVERY] Complete
  Total: 39 tools
  Verified: 37 tools
  Failed: 2 tools
  Output: docs/brain/session_tools.json

[WARNING] Missing tools:
  - semgrep
  - coderabbit-cli

[SESSION] Ready for task execution
```

## Benefits

- ✅ Agents know ALL available tools
- ✅ No "tool not found" errors mid-task
- ✅ Faster task execution (pre-verified)
- ✅ Better error messages (missing tool X)
- ✅ Droid-level tool awareness
- ✅ Session-specific tool manifest
- ✅ Parallel verification for speed
- ✅ Graceful degradation (missing non-critical tools)

## Integration with V12 Workflows

### Pre-Task Checklist

```powershell
# 1. Discover tools
powershell -File .\scripts\discover_tools.ps1

# 2. Verify critical tools
$tools = Get-Content "docs/brain/session_tools.json" | ConvertFrom-Json
if ($tools.tools_failed -gt 0) {
    Write-Host "[WARNING] Some tools unavailable" -ForegroundColor Yellow
}

# 3. Proceed with task
```

### Mid-Task Tool Check

```powershell
# Before using a specialized tool
if (-not (Test-ToolAvailable "routa")) {
    Write-Host "[FALLBACK] Using jcodemunch-mcp instead of Routa" -ForegroundColor Yellow
}
```

### Post-Task Cleanup

```powershell
# Optional: Clean up session manifest
Remove-Item "docs/brain/session_tools.json" -ErrorAction SilentlyContinue
```

## See Also

- [Universal Agent Protocol](UNIVERSAL_AGENT_PROTOCOL.md)
- [MCP Configuration](../setup/MCP_CONFIGURATION.md)
- [Greptile HTTP Bridge](../setup/GREPTILE_HTTP_BRIDGE.md)
- [AGENTS.md](../../AGENTS.md)