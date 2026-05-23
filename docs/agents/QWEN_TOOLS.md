# Qwen Tools & Capabilities

## Overview

**Agent**: Qwen (Local Model)  
**Role**: Privacy-first local development agent  
**Strengths**: Offline operation, data privacy, cost-free inference  
**Primary Use Cases**: Sensitive data analysis, offline development, cost-conscious workflows

## Core Capabilities

### 1. Local Inference

Qwen runs entirely on local hardware:

- **No API Calls**: Zero external dependencies
- **Data Privacy**: All data stays on local machine
- **Cost-Free**: No per-token charges
- **Offline**: Works without internet connection

### 2. Model Variants

**Qwen2.5-Coder Series**:
- `Qwen2.5-Coder-7B`: Fast, lightweight (7B parameters)
- `Qwen2.5-Coder-14B`: Balanced (14B parameters)
- `Qwen2.5-Coder-32B`: High-quality (32B parameters)

**Qwen2.5 Series**:
- `Qwen2.5-7B`: General purpose
- `Qwen2.5-14B`: Enhanced reasoning
- `Qwen2.5-72B`: Maximum capability (requires high-end GPU)

### 3. Privacy-First Workflows

**Sensitive Data Handling**:
- API keys and secrets
- Customer data analysis
- Proprietary algorithms
- Financial data processing
- PII (Personally Identifiable Information)

**Compliance**:
- GDPR compliant (data never leaves premises)
- HIPAA compatible (for healthcare data)
- SOC 2 aligned (for enterprise security)

## Tool Access Matrix

| Tool Category | Access Level | Notes |
|---------------|--------------|-------|
| **Code Navigation** | ✅ Full | jCodemunch MCP (local) |
| **Architecture** | ✅ Full | Routa CLI, graphify |
| **Knowledge Base** | ⚠️ Limited | Local cache only |
| **Testing** | ✅ Full | All local test harnesses |
| **Build & Deploy** | ✅ Full | Local PowerShell scripts |
| **PR Workflow** | ⚠️ Limited | Via gh CLI only |
| **GitHub Apps** | ❌ None | No API access |
| **MCP Servers** | ✅ Full | All local servers |

## When to Use Qwen vs Cloud Agents

### Use Qwen When:

1. **Privacy-Sensitive Tasks**:
   - Analyzing API keys or secrets
   - Processing customer data
   - Reviewing proprietary algorithms
   - Handling financial information

2. **Offline Development**:
   - No internet connection
   - Air-gapped environments
   - Network-restricted systems

3. **Cost Optimization**:
   - High-volume tasks
   - Exploratory analysis
   - Iterative debugging
   - Long-running sessions

4. **Compliance Requirements**:
   - GDPR data residency
   - HIPAA protected health information
   - SOC 2 data handling

### Use Cloud Agents When:

1. **Complex Reasoning**:
   - Architecture design (Claude Opus)
   - Multi-step planning (Bob CLI)
   - Adversarial review (Arena AI)

2. **External Integration**:
   - GitHub API operations (Jules AI)
   - Google AI Studio (Gemini CLI)
   - MCP servers requiring API access

3. **Multimodal Tasks**:
   - Image analysis (Gemini CLI)
   - Audio transcription (Gemini CLI)
   - Video processing (Gemini CLI)

## Hardware Requirements

### Minimum (7B Models)

- **GPU**: NVIDIA RTX 3060 (12GB VRAM)
- **RAM**: 16GB system memory
- **Storage**: 10GB free space
- **Performance**: ~20 tokens/sec

### Recommended (14B Models)

- **GPU**: NVIDIA RTX 4070 Ti (16GB VRAM)
- **RAM**: 32GB system memory
- **Storage**: 20GB free space
- **Performance**: ~30 tokens/sec

### High-End (32B+ Models)

- **GPU**: NVIDIA RTX 4090 (24GB VRAM) or A100 (40GB)
- **RAM**: 64GB system memory
- **Storage**: 40GB free space
- **Performance**: ~40 tokens/sec

## Installation & Setup

### 1. Install Ollama

```bash
# Windows (PowerShell)
winget install Ollama.Ollama

# Verify installation
ollama --version
```

### 2. Pull Qwen Models

```bash
# Lightweight (7B)
ollama pull qwen2.5-coder:7b

# Balanced (14B)
ollama pull qwen2.5-coder:14b

# High-quality (32B)
ollama pull qwen2.5-coder:32b
```

### 3. Configure MCP

Add to `.mcp.json`:
```json
{
  "mcpServers": {
    "qwen-local": {
      "command": "ollama",
      "args": ["serve"],
      "env": {
        "OLLAMA_HOST": "127.0.0.1:11434",
        "OLLAMA_MODELS": "qwen2.5-coder:14b"
      }
    }
  }
}
```

### 4. Test Connection

```bash
# Test Ollama
ollama run qwen2.5-coder:7b "Hello, test"

# Test MCP integration
python scripts/test_mcp.py --server qwen-local
```

## Workflow Integration

### 1. Privacy-First Code Review

```bash
# Analyze sensitive code locally
qwen "Review this API key handling" --file src/secrets.cs

# No data leaves local machine
# No API calls logged
# No cloud provider access
```

### 2. Offline Development

```bash
# Work without internet
qwen "Refactor this function" --file src/utils.cs

# All inference local
# No network dependency
# Full functionality offline
```

### 3. Cost-Optimized Iteration

```bash
# High-volume analysis (free)
for file in src/*.cs; do
  qwen "Analyze complexity" --file $file
done

# Zero API costs
# Unlimited iterations
# No rate limits
```

## Performance Optimization

### 1. Model Selection

**Task Complexity vs Model Size**:
- Simple tasks (formatting, linting): 7B model
- Medium tasks (refactoring, debugging): 14B model
- Complex tasks (architecture, design): 32B model

### 2. Context Management

```bash
# Limit context for speed
qwen "Quick fix" --max-tokens 2048

# Full context for quality
qwen "Deep analysis" --max-tokens 8192
```

### 3. Batch Processing

```bash
# Process multiple files efficiently
qwen "Analyze all" --batch --files src/*.cs
```

## Limitations

### 1. Reasoning Capability

Qwen models are **less capable** than cloud models for:

- ❌ Complex architecture design (use Claude Opus)
- ❌ Multi-step planning (use Bob CLI)
- ❌ Adversarial review (use Arena AI)
- ❌ Multimodal tasks (use Gemini CLI)

**Best for**:
- ✅ Code analysis
- ✅ Refactoring suggestions
- ✅ Bug detection
- ✅ Documentation generation

### 2. Knowledge Cutoff

Local models have **fixed knowledge**:

- ❌ No real-time updates
- ❌ No internet search
- ❌ No API documentation lookup

**Workaround**: Use Jane Street KB for HFT patterns.

### 3. Hardware Constraints

Performance depends on **local hardware**:

- Slower than cloud APIs
- Limited by GPU memory
- May require model quantization

## Best Practices

### 1. Privacy Workflow

**For sensitive data**:
1. Use Qwen for initial analysis
2. Redact sensitive information
3. Use cloud agents for complex reasoning (if needed)

### 2. Hybrid Approach

**Combine local and cloud**:
- Qwen: Privacy-sensitive analysis
- Bob CLI: Architecture design
- Gemini CLI: Multimodal tasks

### 3. Model Caching

**Optimize startup time**:
```bash
# Keep model loaded
ollama run qwen2.5-coder:14b --keep-alive 24h

# Preload on system startup
# Add to startup scripts
```

## Common Commands

### Model Management

```bash
# List installed models
ollama list

# Pull new model
ollama pull qwen2.5-coder:14b

# Remove model
ollama rm qwen2.5-coder:7b

# Show model info
ollama show qwen2.5-coder:14b
```

### Inference

```bash
# Interactive mode
ollama run qwen2.5-coder:14b

# Single prompt
ollama run qwen2.5-coder:14b "Your prompt here"

# With file input
ollama run qwen2.5-coder:14b "Analyze this" < file.cs
```

### Performance Tuning

```bash
# Adjust context size
ollama run qwen2.5-coder:14b --ctx-size 4096

# Control temperature
ollama run qwen2.5-coder:14b --temperature 0.7

# Set max tokens
ollama run qwen2.5-coder:14b --max-tokens 2048
```

## Troubleshooting

### GPU Memory Issues

```bash
# Use smaller model
ollama pull qwen2.5-coder:7b

# Enable quantization
ollama run qwen2.5-coder:14b --quantize q4_0

# Reduce context size
ollama run qwen2.5-coder:14b --ctx-size 2048
```

### Slow Inference

```bash
# Check GPU usage
nvidia-smi

# Verify model loaded
ollama ps

# Restart Ollama service
ollama stop && ollama serve
```

### Connection Errors

```bash
# Check Ollama status
ollama ps

# Restart service
ollama serve

# Verify port
netstat -an | findstr 11434
```

## References

- [Qwen Documentation](https://qwen.readthedocs.io/)
- [Ollama Documentation](https://ollama.ai/docs)
- [MCP Configuration](docs/setup/MCP_CONFIGURATION.md)
- [Privacy Best Practices](docs/protocol/PRIVACY_GUIDELINES.md)
- [Universal Agent Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)