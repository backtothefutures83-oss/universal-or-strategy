# Gemini CLI Tools & Capabilities

## Overview

**Agent**: Gemini CLI (`yolo` mode)  
**Role**: Secondary non-src engineer, Orchestrator (Antigravity)  
**Strengths**: Multimodal support, context caching, local file access, visual context  
**Primary Use Cases**: Non-src tasks, utility workflows, model-agnostic orchestration

## Core Capabilities

### 1. Google AI Studio Integration

- **API**: Gemini 2.0 Flash Experimental
- **Context Window**: 1M tokens
- **Multimodal**: Vision, audio, video analysis
- **Streaming**: Real-time response generation

### 2. Context Caching (75% Cost Savings)

**Configuration** (`.env`):
```bash
GEMINI_CONTEXT_CACHING=true
GEMINI_CACHE_TTL=3600  # 1 hour
```

**Automatic Caching**:
- AGENTS.md (primary source of truth)
- .bob/rules/ (all rule files)
- docs/protocol/ (protocol documentation)
- graphify-out/GRAPH_REPORT.md (architecture overview)

**Manual Cache Control**:
```bash
# Force cache refresh
gemini --refresh-cache

# View cache stats
gemini --cache-stats
```

### 3. Multimodal Support

**Vision Analysis**:
```bash
# Analyze screenshot
gemini "Analyze this NinjaTrader screenshot" --image screenshot.jpg

# Compare before/after
gemini "Compare these two builds" --image before.png --image after.png
```

**Audio/Video**:
```bash
# Transcribe meeting
gemini "Summarize this standup" --audio standup.mp3

# Analyze demo video
gemini "Extract key features" --video demo.mp4
```

### 4. Local File Access

Unlike Jules AI (GitHub-only), Gemini CLI has full local filesystem access:

```bash
# Read local files
gemini "Analyze this log" --file logs/build.txt

# Process multiple files
gemini "Compare these configs" --file config1.json --file config2.json
```

## Tool Access Matrix

| Tool Category | Access Level | Notes |
|---------------|--------------|-------|
| **Code Navigation** | ✅ Full | jCodemunch MCP |
| **Architecture** | ✅ Full | Routa CLI, graphify |
| **Knowledge Base** | ✅ Full | Jane Street KB |
| **Testing** | ✅ Full | All test harnesses |
| **Build & Deploy** | ✅ Full | Local PowerShell scripts |
| **PR Workflow** | ⚠️ Limited | Via gh CLI only |
| **GitHub Apps** | ❌ None | No direct API access |
| **MCP Servers** | ✅ Full | All configured servers |

## When to Use Gemini vs Bob

### Use Gemini CLI When:

1. **Non-src Tasks**:
   - Documentation updates
   - Workflow script modifications
   - Test file creation
   - Benchmark analysis

2. **Multimodal Requirements**:
   - Screenshot analysis
   - Visual debugging
   - Audio transcription
   - Video processing

3. **Orchestration**:
   - Multi-agent coordination (Antigravity mode)
   - Context routing
   - Session handoffs

4. **Utility Workflows**:
   - Log analysis
   - Config file processing
   - Report generation
   - Data transformation

### Use Bob CLI When:

1. **src/ Engineering**:
   - Production code changes
   - Refactoring
   - Architecture design
   - Performance optimization

2. **Complex Refactoring**:
   - Multi-file changes
   - God-function splitting
   - Lock-free conversions

3. **Design Gates**:
   - Architecture planning
   - Implementation specs
   - Mermaid diagrams

## Workflow Integration

### 1. Epic Orchestration (Antigravity Mode)

```bash
# Initialize epic session
gemini "Start epic: SIMA Subgraph Extraction" --mode antigravity

# Route to specialists
gemini "Hand off to Bob CLI for src/ extraction"
gemini "Hand off to Jules AI for PR creation"
```

### 2. Non-src PR Workflow

```bash
# Create non-src PR
gemini "Update documentation for RMA proximity monitoring"

# Verify separation
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>

# Fast-track merge (no bot audit required)
gh pr merge <N> --squash
```

### 3. Visual Debugging

```bash
# Analyze NinjaTrader screenshot
gemini "Identify the issue in this chart" --image chart_error.png

# Compare performance graphs
gemini "Explain the latency spike" --image before.png --image after.png
```

## Configuration

### Environment Variables

```bash
# Required
GOOGLE_API_KEY=your_api_key_here

# Optional
GEMINI_MODEL=gemini-2.0-flash-exp
GEMINI_CONTEXT_CACHING=true
GEMINI_CACHE_TTL=3600
GEMINI_MAX_TOKENS=8192
GEMINI_TEMPERATURE=0.7
```

### MCP Integration

Gemini CLI uses the centralized MCP configuration:

```json
{
  "mcpServers": {
    "jcodemunch": {
      "command": "C:\\Users\\Mohammed Khalid\\.local\\bin\\jcodemunch-mcp.exe",
      "args": []
    },
    "sequential-thinking": {
      "command": "npx.cmd",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

## Best Practices

### 1. Context Efficiency

- **Always check cache stats** before long sessions
- **Refresh cache** after major protocol updates
- **Use multimodal** when visual context reduces token usage

### 2. Task Routing

- **Never use Gemini for src/ changes** (Bob CLI only)
- **Prefer Gemini for utility tasks** (conserve Bob tokens)
- **Use Gemini for orchestration** (Antigravity mode)

### 3. PR Separation

- **Always verify** non-src/ only with `verify_pr_separation.ps1`
- **Fast-track merge** non-src PRs (no bot audit)
- **Never mix** src/ and non-src/ in same PR

## Common Commands

```bash
# Check Gemini status
gemini --version
gemini --cache-stats

# Run with context caching
gemini "Your prompt here" --cache

# Multimodal analysis
gemini "Analyze this" --image file.png --audio file.mp3

# Orchestration mode
gemini "Start epic session" --mode antigravity

# Local file processing
gemini "Process this log" --file logs/build.txt
```

## Troubleshooting

### Cache Issues

```bash
# Clear cache
gemini --clear-cache

# Rebuild cache
gemini --refresh-cache
```

### API Rate Limits

```bash
# Check quota
gemini --quota

# Use exponential backoff
gemini "Your prompt" --retry-on-rate-limit
```

### Multimodal Errors

```bash
# Verify file format
gemini --validate-media file.png

# Check file size limits
# Images: 20MB max
# Audio: 100MB max
# Video: 2GB max
```

## References

- [Google AI Studio](https://aistudio.google.com/)
- [Gemini API Docs](https://ai.google.dev/docs)
- [Context Caching Guide](docs/setup/PROMPT_CACHING.md)
- [MCP Configuration](docs/setup/MCP_CONFIGURATION.md)
- [Universal Agent Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)