# Prompt Caching Configuration

## Overview

Prompt caching reduces API costs by caching frequently-used context (AGENTS.md, protocol docs, rules) across agent sessions. This document explains how prompt caching works across different LLM providers and how to configure it for maximum savings.

## Multi-Provider Support

### Supported Providers

| Provider | Caching Support | Configuration | Savings |
|----------|----------------|---------------|---------|
| **Anthropic Claude** | ✅ Native | `.anthropic/cache_config.json` | 90% on cached tokens |
| **OpenAI GPT-4** | ✅ Via Bob | Bob's internal cache | ~70% on repeated context |
| **Google Gemini** | ✅ Context Caching | Gemini API settings | ~75% on cached context |
| **Local Models** | ✅ KV Cache | Model-specific | Latency reduction only |

### How Bob Handles Multi-Provider Caching

Bob CLI intelligently routes caching based on the active provider:

```yaml
# .bob/settings.json
{
  "caching": {
    "enabled": true,
    "providers": {
      "anthropic": {
        "method": "native",
        "config": ".anthropic/cache_config.json"
      },
      "openai": {
        "method": "bob_internal",
        "ttl_seconds": 300
      },
      "gemini": {
        "method": "context_caching",
        "ttl_seconds": 300
      },
      "local": {
        "method": "kv_cache",
        "enabled": true
      }
    }
  }
}
```

## Provider-Specific Configuration

### 1. Anthropic Claude (Native Caching)

**Location:** `.anthropic/cache_config.json`

**How It Works:**
- Anthropic's native prompt caching API
- Caches system prompts for 5 minutes
- 90% cost reduction on cached tokens
- Automatic cache invalidation after TTL

**Configuration:**
```json
{
  "enabled": true,
  "cache_control": {
    "type": "ephemeral",
    "ttl_seconds": 300
  },
  "cacheable_prompts": [
    "AGENTS.md",
    ".bob/rules/**/*.md",
    "docs/protocol/**/*.md"
  ]
}
```

**Cost Savings:**
- Fresh tokens: $3.00/1M input tokens
- Cached tokens: $0.30/1M input tokens
- **Savings: 90%**

### 2. OpenAI GPT-4 (Bob Internal Cache)

**How It Works:**
- Bob maintains an internal cache of protocol context
- Hashes protocol files and reuses embeddings
- Reduces token count by deduplicating repeated context
- No OpenAI API changes required

**Configuration:**
```yaml
# .bob/settings.json
{
  "openai_cache": {
    "enabled": true,
    "cache_dir": ".bob/cache/openai",
    "ttl_seconds": 300,
    "max_size_mb": 100
  }
}
```

**Cost Savings:**
- Typical reduction: 60-70% on repeated context
- Works by deduplicating protocol text
- No API-level caching (OpenAI doesn't support it yet)

### 3. Google Gemini (Context Caching)

**How It Works:**
- Gemini's native context caching API
- Caches up to 1M tokens for 1 hour
- Automatic cache management
- 75% cost reduction on cached tokens

**Configuration:**
```python
# Bob automatically configures Gemini caching
import google.generativeai as genai

cached_content = genai.caching.CachedContent.create(
    model='gemini-1.5-pro',
    system_instruction=agents_md_content,
    ttl=datetime.timedelta(minutes=5)
)
```

**Cost Savings:**
- Fresh tokens: $0.35/1M input tokens
- Cached tokens: $0.0875/1M input tokens
- **Savings: 75%**

### 4. Local Models (KV Cache)

**How It Works:**
- Key-Value cache at model level
- Reduces inference latency (not cost)
- Automatic in most local model servers
- No configuration needed

**Benefits:**
- Faster response times
- Lower GPU memory usage
- No cost impact (local models are free)

## Unified Configuration

### .anthropic/cache_config.json

This file configures caching behavior across all providers:

```json
{
  "enabled": true,
  "cache_control": {
    "type": "ephemeral",
    "ttl_seconds": 300
  },
  "cacheable_prompts": [
    "AGENTS.md",
    ".bob/rules/**/*.md",
    "docs/protocol/**/*.md",
    "docs/setup/**/*.md",
    ".cursorrules",
    "CLAUDE.md",
    "BOB.md",
    "CODEX.md",
    "GEMINI.md",
    "JULES.md"
  ],
  "providers": {
    "anthropic": {
      "enabled": true,
      "method": "native"
    },
    "openai": {
      "enabled": true,
      "method": "bob_internal"
    },
    "gemini": {
      "enabled": true,
      "method": "context_caching"
    },
    "local": {
      "enabled": true,
      "method": "kv_cache"
    }
  },
  "estimated_savings": {
    "anthropic": {
      "tokens_per_session": 50000,
      "sessions_per_day": 10,
      "cost_per_1M_tokens": 3.00,
      "cache_discount": 0.90,
      "annual_savings_usd": 443.48
    },
    "openai": {
      "tokens_per_session": 50000,
      "sessions_per_day": 5,
      "cost_per_1M_tokens": 5.00,
      "dedup_rate": 0.70,
      "annual_savings_usd": 318.50
    },
    "gemini": {
      "tokens_per_session": 50000,
      "sessions_per_day": 3,
      "cost_per_1M_tokens": 0.35,
      "cache_discount": 0.75,
      "annual_savings_usd": 9.59
    }
  },
  "total_annual_savings_usd": 771.57,
  "version": "1.0.0",
  "last_updated": "2026-05-23"
}
```

## Cost Savings Analysis

### Combined Savings (All Providers)

| Provider | Daily Sessions | Daily Cost (No Cache) | Daily Cost (With Cache) | Daily Savings |
|----------|---------------|----------------------|------------------------|---------------|
| Anthropic Claude | 10 | $1.50 | $0.29 | $1.21 (81%) |
| OpenAI GPT-4 | 5 | $1.25 | $0.38 | $0.87 (70%) |
| Google Gemini | 3 | $0.05 | $0.01 | $0.03 (75%) |
| **Total** | **18** | **$2.80** | **$0.68** | **$2.12 (76%)** |

**Annual Savings: $771.57**

## Verification

### Check Cache Status (Anthropic)

```python
import anthropic

client = anthropic.Anthropic()
response = client.messages.create(
    model="claude-opus-4-7",
    messages=[{"role": "user", "content": "test"}],
    system=[
        {
            "type": "text",
            "text": open("AGENTS.md").read(),
            "cache_control": {"type": "ephemeral"}
        }
    ]
)

print(f"Cache creation: {response.usage.cache_creation_input_tokens}")
print(f"Cache read: {response.usage.cache_read_input_tokens}")
```

### Check Cache Status (Bob Internal)

```bash
bob /cache-stats

# Expected output:
# [✓] Anthropic: 9/10 hits (90%)
# [✓] OpenAI: 4/5 hits (80%)
# [✓] Gemini: 2/3 hits (67%)
# [✓] Total savings today: $2.12
```

### Monitor Savings

```powershell
# Generate multi-provider caching report
python scripts/cache_savings_report.py --all-providers

# Expected output:
# Provider      | Cache Hits | Tokens Saved | Cost Saved
# --------------|------------|--------------|------------
# Anthropic     | 9/10 (90%) | 450,000      | $1.22
# OpenAI        | 4/5 (80%)  | 140,000      | $0.70
# Gemini        | 2/3 (67%)  | 66,667       | $0.02
# --------------|------------|--------------|------------
# Total         | 15/18 (83%)| 656,667      | $1.94
```

## Best Practices

### 1. Provider Selection Strategy

**For Cost Optimization:**
- Use Anthropic Claude for complex reasoning (best caching)
- Use OpenAI GPT-4 for code generation (good caching)
- Use Gemini for high-volume tasks (cheapest with caching)
- Use local models for privacy-sensitive tasks (no cost)

**Bob's Auto-Selection:**
```yaml
# .bob/settings.json
{
  "provider_strategy": "cost_optimized",
  "fallback_order": ["anthropic", "gemini", "openai", "local"]
}
```

### 2. Cache Stable Content Only

**DO Cache:**
- Agent protocols (AGENTS.md, CLAUDE.md)
- V12 DNA rules
- Protocol documentation
- Setup guides

**DON'T Cache:**
- Task-specific context
- File contents that change frequently
- User messages
- Dynamic data

### 3. Update Cache After Protocol Changes

```powershell
# Clear all provider caches
python scripts/clear_prompt_cache.py --all-providers

# Or clear specific provider
python scripts/clear_prompt_cache.py --provider anthropic
```

## Integration with V12 Workflows

### Pre-Push Checklist

```powershell
# Verify caching is enabled
bob /cache-stats

# If disabled, enable it
bob /enable-cache --all-providers
```

### PR Loop Integration

Prompt caching automatically reduces costs during PR loops:
- Anthropic: $0.15 → $0.015 per iteration (90% savings)
- OpenAI: $0.25 → $0.075 per iteration (70% savings)
- Gemini: $0.02 → $0.005 per iteration (75% savings)

### Epic Run Integration

Epic runs benefit most from caching:
- Long sessions (30-60 minutes)
- Multiple agent handoffs
- Consistent protocol context
- **Estimated savings per epic: $10-20**

## Troubleshooting

### Issue: Low Cache Hit Rate

**Symptoms:**
- Cache hit rate < 50%
- High API costs despite caching enabled

**Solutions:**
1. Increase TTL to 600 seconds
2. Verify cacheable content is stable
3. Check for protocol updates mid-session
4. Review provider-specific logs

### Issue: Provider-Specific Cache Failure

**Anthropic:**
```powershell
# Check API key has caching enabled
echo $env:ANTHROPIC_API_KEY | python scripts/verify_cache_support.py
```

**OpenAI:**
```powershell
# Clear Bob's internal cache
Remove-Item -Recurse .bob/cache/openai
```

**Gemini:**
```powershell
# Verify context caching API access
python scripts/test_gemini_cache.py
```

## Related Documentation

- [Universal Agent Protocol](../protocol/UNIVERSAL_AGENT_PROTOCOL.md)
- [MCP Configuration](MCP_CONFIGURATION.md)
- [Anthropic Prompt Caching](https://docs.anthropic.com/claude/docs/prompt-caching)
- [Gemini Context Caching](https://ai.google.dev/gemini-api/docs/caching)

## Version History

- **v1.0.0** (2026-05-23): Initial multi-provider caching configuration
  - Anthropic native caching (90% savings)
  - OpenAI Bob internal cache (70% savings)
  - Gemini context caching (75% savings)
  - Local model KV cache support
  - Estimated $771.57/year total savings