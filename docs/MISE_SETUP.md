# Mise Setup Guide

## Overview

V12 Universal OR Strategy uses [Mise](https://mise.jdx.dev/) for reproducible development environments. Mise ensures all team members use identical tool versions with zero configuration overhead.

## Why Mise?

- ✅ **Fast**: 10x faster than asdf, written in Rust
- ✅ **Simple**: One config file (`.mise.toml`)
- ✅ **Cross-platform**: Native Windows, macOS, Linux support
- ✅ **Task Runner**: Built-in task automation (no need for make/npm scripts)
- ✅ **Version Management**: Automatic tool version switching per project
- ✅ **No Docker**: Lightweight, native performance

## Installation

### Windows (PowerShell)

```powershell
irm https://mise.jdx.dev/install.ps1 | iex
```

### macOS/Linux

```bash
curl https://mise.run | sh
```

### Verify Installation

```bash
mise --version
```

## Quick Start

### 1. Activate Mise

```bash
# Activate mise in current shell
mise activate

# Or add to your shell profile (one-time)
# PowerShell: Add to $PROFILE
mise activate | Out-String | Invoke-Expression

# Bash/Zsh: Add to ~/.bashrc or ~/.zshrc
echo 'eval "$(mise activate bash)"' >> ~/.bashrc
```

### 2. Install Tools

```bash
# Install all tools defined in .mise.toml
mise install

# Or just enter the directory - mise auto-installs
cd c:/WSGTA/universal-or-strategy
```

### 3. Install Dependencies

```bash
# Install Python and Node packages
mise run install
```

### 4. Start Development

```bash
# Start all services (Phoenix, Greptile, Obsidian, Compound Intelligence)
mise run start-dev

# Or use individual commands
mise run build      # Build C# project
mise run test       # Run tests
mise run phoenix    # Start Phoenix tracing server
```

## Available Commands

### Development

```bash
mise run start-dev       # Start all dev services
mise run build           # Build C# project
mise run test            # Run C# tests
mise run lint            # Run linting
mise run format          # Format C# code
mise run format-check    # Check formatting
```

### Quality Gates

```bash
mise run pre-push        # Run all 13 quality gates
mise run pre-push-fast   # Run fast quality gates (skip slow checks)
mise run audit           # Run security audit
mise run complexity      # Run complexity analysis
mise run dead-code       # Scan for dead code
```

### Deployment

```bash
mise run deploy          # Deploy to NinjaTrader
mise run clean           # Clean build artifacts
```

### Utilities

```bash
mise run phoenix         # Start Phoenix tracing server
mise run kb-query <term> # Query Jane Street knowledge base
mise run bootstrap <agent> <task>  # Run agent bootstrap
mise run info            # Show environment info
```

### Setup

```bash
mise run setup           # First-time setup (installs everything)
```

## Included Tools

### Managed by Mise

- **Python 3.12**: Phoenix, Firebase, agent scripts
- **Node.js 20**: Routa tools, MCP servers

### System Requirements

- **.NET SDK 8.0**: Install separately (mise doesn't manage .NET yet)
  ```powershell
  winget install Microsoft.DotNet.SDK.8
  ```

### Python Packages (from requirements.txt)

- **Phoenix**: OpenTelemetry tracing UI
- **Firebase Admin**: Firestore for Compound Intelligence
- **LangSmith**: Agent tracing
- **OpenTelemetry**: Distributed tracing
- **Lizard/Radon**: Code complexity analysis
- **pytest**: Testing framework
- **black/ruff**: Code formatting and linting

## Environment Variables

Mise automatically sets:

```bash
MISE_SHELL_ENABLED=1
PYTHONPATH=$PWD/scripts:$PYTHONPATH
DOTNET_CLI_TELEMETRY_OPTOUT=1
DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
```

Additional variables (set in `.env`):

```bash
# Phoenix Tracing
PHOENIX_COLLECTOR_ENDPOINT=http://localhost:6006/v1/traces
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:6006/v1/traces

# Firebase (Compound Intelligence)
GOOGLE_APPLICATION_CREDENTIALS=path/to/firebase-key.json

# Greptile MCP
GREPTILE_API_KEY=your_key_here
GITHUB_TOKEN=your_token_here

# LangSmith
LANGCHAIN_API_KEY=your_key_here
LANGCHAIN_PROJECT=v12-universal-or-strategy
```

## CI/CD Integration

Create `.github/workflows/mise-ci.yml`:

```yaml
name: Mise CI

on: [push, pull_request]

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]
    
    steps:
      - uses: actions/checkout@v4
      
      - uses: jdx/mise-action@v2
        with:
          install: true
      
      - name: Install dependencies
        run: mise run install
      
      - name: Build
        run: mise run build
      
      - name: Test
        run: mise run test
```

## Troubleshooting

### Issue: "mise: command not found"

**Solution**: Activate mise in your shell:

```bash
# PowerShell
mise activate | Out-String | Invoke-Expression

# Bash/Zsh
eval "$(mise activate bash)"
```

### Issue: Python packages not found

**Solution**: Reinstall requirements:

```bash
mise run install
```

### Issue: Wrong Python/Node version

**Solution**: Reinstall tools:

```bash
mise install --force
```

### Issue: .NET build fails

**Solution**: Ensure .NET SDK 8.0 is installed:

```powershell
dotnet --version  # Should show 8.0.x
winget install Microsoft.DotNet.SDK.8
```

### Issue: Phoenix won't start

**Solution**: Check if port 6006 is in use:

```bash
# Windows
netstat -ano | findstr :6006

# macOS/Linux
lsof -i :6006
```

## Updating Mise

### Update Mise itself

```bash
mise self-update
```

### Update tools

```bash
# Update all tools to latest versions
mise upgrade

# Update specific tool
mise upgrade python
```

### Update Python packages

```bash
pip install -r requirements.txt --upgrade
```

## Best Practices

### 1. Always Use Mise Tasks

```bash
# ✅ Good
mise run build

# ❌ Bad (bypasses mise environment)
dotnet build
```

### 2. Pin Package Versions

In `requirements.txt`:

```txt
# ✅ Good (reproducible)
arize-phoenix==4.0.0

# ❌ Bad (version drift)
arize-phoenix
```

### 3. Check Tool Versions

```bash
# See what versions are active
mise current

# See all available versions
mise ls-remote python
```

### 4. Use Task Dependencies

Tasks can depend on other tasks:

```toml
[tasks.test]
depends = ["build"]
run = "dotnet test"
```

## Integration with Bob CLI

Bob CLI automatically uses Mise when available:

1. Bob detects `MISE_SHELL_ENABLED=1`
2. Phoenix tracer initializes with correct Python packages
3. Compound Intelligence logger connects to Firebase
4. All scripts run in isolated environment

## Migration from Devbox

If you were using Devbox:

### Before (Devbox)

```bash
devbox shell
devbox run build
```

### After (Mise)

```bash
# No shell needed - mise activates automatically
mise run build
```

### Cleanup

```bash
# Remove Devbox
rm -rf .devbox devbox.json devbox.lock

# Uninstall Devbox (optional)
# Windows: Remove from Programs & Features
# macOS/Linux: rm ~/.local/bin/devbox
```

## Advanced Features

### Custom Tasks

Add to `.mise.toml`:

```toml
[tasks.my-task]
description = "My custom task"
run = "echo 'Hello from Mise!'"
```

### Environment-Specific Config

```toml
[env]
_.file = ".env"  # Load from .env file
MY_VAR = "value"
```

### Tool Aliases

```toml
[tools]
python = "3.12"
py = "python@3.12"  # Alias
```

### Watch Mode

```bash
# Re-run task on file changes
mise watch -t build
```

## Resources

- [Mise Documentation](https://mise.jdx.dev/)
- [Mise GitHub](https://github.com/jdx/mise)
- [Mise Tasks Guide](https://mise.jdx.dev/tasks/)
- [V12 Project Documentation](./README.md)

## Support

For Mise issues:
- GitHub Issues: https://github.com/jdx/mise/issues
- Discord: https://discord.gg/mise

For V12 project issues:
- See `docs/TROUBLESHOOTING.md`
- Check `docs/brain/` for agent logs