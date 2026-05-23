# MCP Configuration Guide

## Overview

The V12 Universal OR Strategy repository uses a centralized MCP (Model Context Protocol) configuration to ensure consistent tool access across all agents. This document explains how to configure and use MCP servers.

## Centralized Configuration

**Location:** `.mcp/config.json`

This file defines all MCP servers available to agents in the repository. All agents should reference this configuration for consistent tool access.

## Available MCP Servers

### 1. jCodemunch MCP

**Purpose:** Advanced code navigation, architecture analysis, and codebase intelligence.

**Command:** `C:\Users\Mohammed Khalid\.local\bin\jcodemunch-mcp.exe`

**Environment Variables:**
- `JCODEMUNCH_USE_AI_SUMMARIES=true` - Enable AI-powered symbol summaries
- `JCODEMUNCH_CROSS_REPO_DEFAULT=false` - Disable cross-repo search by default

**Key Capabilities:**
- Symbol search with semantic understanding
- Dependency graph analysis
- Blast radius calculation
- Dead code detection
- Architecture pattern recognition
- 71x token efficiency vs raw file reading

**Usage Example:**
```bash
# Index the repository
jcodemunch index_folder --path .

# Search for symbols
jcodemunch search_symbols --repo universal-or-strategy --query "FSM"

# Get blast radius
jcodemunch get_blast_radius --repo universal-or-strategy --symbol "ProcessBracketEvent"
```

### 2. LSP MCP (Language Server Protocol)

**Purpose:** Real-time code intelligence, diagnostics, and IDE-like features.

**Command:** `cjl-lsp-mcp --workspace .`

**Key Capabilities:**
- Go-to-definition
- Find references
- Hover documentation
- Code completion suggestions
- Real-time diagnostics
- Rename refactoring

**Usage Example:**
```bash
# Start LSP server for current workspace
cjl-lsp-mcp --workspace .
```

### 3. Sequential Thinking MCP

**Purpose:** Multi-step reasoning and problem decomposition.

**Command:** `npx.cmd -y @modelcontextprotocol/server-sequential-thinking`

**Key Capabilities:**
- Break down complex problems into steps
- Maintain context across multiple reasoning steps
- Revise and refine thinking iteratively
- Generate solution hypotheses
- Verify hypotheses against constraints

**Usage Example:**
```bash
# Use via MCP protocol (agent-specific)
# Typically invoked automatically by agents for complex tasks
```

## Agent-Specific Configuration

### Bob CLI

Bob CLI uses MCP servers through its `.bob/settings.json` configuration:

```json
{
  "mcp_config": ".mcp/config.json",
  "enabled_servers": ["jcodemunch", "lsp-mcp", "sequential-thinking"]
}
```

### Cursor IDE

Cursor IDE references MCP servers through its workspace settings:

```json
{
  "mcp.servers": {
    "jcodemunch": {
      "command": "C:\\Users\\Mohammed Khalid\\.local\\bin\\jcodemunch-mcp.exe"
    }
  }
}
```

### Gemini CLI

Gemini CLI uses environment variables to access MCP servers:

```bash
export MCP_CONFIG_PATH=".mcp/config.json"
gemini-cli --use-mcp
```

### Jules AI

Jules AI (GitHub-based) accesses MCP servers through GitHub Actions workflows:

```yaml
- name: Setup MCP
  run: |
    echo "MCP_CONFIG_PATH=.mcp/config.json" >> $GITHUB_ENV
```

## Verification Steps

### 1. Verify jCodemunch Installation

```powershell
# Check if jcodemunch-mcp is installed
& "C:\Users\Mohammed Khalid\.local\bin\jcodemunch-mcp.exe" --version

# Expected output: jcodemunch-mcp v1.x.x
```

### 2. Verify LSP MCP Installation

```powershell
# Check if cjl-lsp-mcp is available
cjl-lsp-mcp --help

# Expected output: Usage information
```

### 3. Test MCP Configuration

```powershell
# Validate configuration file
python scripts/validate_mcp_config.py

# Expected output: [✓] All MCP servers configured correctly
```

### 4. Test Agent Access

**Bob CLI:**
```bash
bob /test-mcp
# Expected: [✓] jCodemunch MCP: Connected
#           [✓] LSP MCP: Connected
```

**Cursor IDE:**
- Open Command Palette (Ctrl+Shift+P)
- Type "MCP: Show Status"
- Verify all servers show "Connected"

## Troubleshooting

### Issue: jCodemunch MCP Not Found

**Solution:**
```powershell
# Install jCodemunch MCP
npm install -g jcodemunch-mcp

# Or via Cargo
cargo install jcodemunch-mcp
```

### Issue: LSP MCP Connection Failed

**Solution:**
```powershell
# Ensure workspace path is correct
cjl-lsp-mcp --workspace "C:\WSGTA\universal-or-strategy"

# Check for port conflicts
netstat -ano | findstr :6000
```

### Issue: Sequential Thinking MCP Timeout

**Solution:**
```powershell
# Clear npm cache
npm cache clean --force

# Reinstall
npx -y @modelcontextprotocol/server-sequential-thinking
```

## Best Practices

1. **Always Use Centralized Config**: Reference `.mcp/config.json` instead of hardcoding server paths
2. **Verify Before Use**: Run verification steps before starting work
3. **Update Regularly**: Keep MCP servers updated to latest versions
4. **Monitor Performance**: Check MCP server logs for performance issues
5. **Fallback Strategy**: If MCP fails, fall back to native tools (grep, read_file)

## Integration with V12 Workflows

### Pre-Push Checklist

```powershell
# Verify MCP servers are running
bob /test-mcp

# If failed, restart MCP servers
powershell -File .\scripts\restart_mcp_servers.ps1
```

### PR Loop Integration

MCP servers are automatically used during the PR loop for:
- Code navigation (jCodemunch)
- Real-time diagnostics (LSP MCP)
- Complex reasoning (Sequential Thinking)

### Epic Run Integration

Epic runs leverage MCP servers for:
- Architecture analysis (jCodemunch)
- Multi-file refactoring planning (LSP MCP)
- Task decomposition (Sequential Thinking)

## Related Documentation

- [Universal Agent Protocol](../protocol/UNIVERSAL_AGENT_PROTOCOL.md)
- [Prompt Caching Configuration](PROMPT_CACHING.md)
- [jCodemunch Documentation](https://github.com/jcodemunch/jcodemunch-mcp)
- [LSP MCP Documentation](https://github.com/cjl/lsp-mcp)

## Version History

- **v1.0.0** (2026-05-23): Initial centralized MCP configuration