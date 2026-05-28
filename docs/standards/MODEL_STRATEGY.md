# V12 Model Strategy & Cost Optimization

**Purpose**: Living document tracking model assignments for Bob CLI modes, cost optimization strategy, and escalation protocols.

**Owner**: Director + Orchestrator (Antigravity)

**Review Cadence**: Monthly (or when new models are released)

**Last Updated**: 2026-05-28

---

## Executive Summary

V12 uses a **hybrid model strategy** based on empirical benchmark data:
- **Sonnet 4.6** for code execution (79.6% agentic coding score)
- **Opus 4.8** for strategic planning (57.9% multidisciplinary reasoning)

**Cost Impact**: 64% cheaper than all-Opus ($37.80/month vs $105/month)
**Quality Impact**: Superior (Sonnet outperforms Opus at agentic coding tasks)

---

## Current Model Assignments

### A. Built-in Modes (`.bob/settings.json`)

```json
{
  "general": {
    "defaultModels": {
      "plan": "claude-opus-4-8",       // Strategic planning, design docs
      "code": "claude-sonnet-4-6",     // DEPRECATED (V12.18) - use advanced
      "advanced": "claude-sonnet-4-6", // Primary code mode with MCP tools
      "ask": "claude-sonnet-4-6",      // Q&A, explanations, research
      "orchestrator": "claude-opus-4-8" // Multi-agent coordination
    }
  }
}
```

**Rationale**:
- **Plan mode** → Opus 4.8: Strategic thinking, architecture design
- **Code mode** → Sonnet 4.6: Simple code edits (no MCP tools, faster)
- **Advanced mode** → Sonnet 4.6: PRIMARY for complex code work (MCP tools, 79.6% agentic coding)
- **Ask mode** → Sonnet 4.6: Fast, cheap Q&A (63.3% financial analysis)
- **Orchestrator mode** → Opus 4.8: Complex multi-step coordination

**Code vs Advanced**:
- Both use Sonnet 4.6 (same cost, same quality)
- **Code**: Simpler, faster (no MCP overhead) - use for quick edits
- **Advanced**: More powerful (jcodemunch, graphify, browser) - use for exploration

### B. Custom V12 Modes (`.bob/custom_modes.yaml`)

```yaml
# v12-engineer (80% of work)
- slug: v12-engineer
  model: claude-sonnet-4-6
  rationale: |
    - BEST at agentic coding (79.6% vs Opus 69.2%)
    - 5x cheaper ($1.05 vs $5.25 per session)
    - 2x faster (30s vs 60s)
    - Handles EPIC-8 through EPIC-14 extraction tickets
    - Perfect for /ticket execution, refactoring, complexity reduction

# v12-epic-planner (15% of work)
- slug: v12-epic-planner
  model: claude-opus-4-8
  rationale: |
    - BEST at multidisciplinary reasoning (57.9% vs Sonnet 49.0%)
    - BEST at knowledge work (1890 vs Sonnet 1633)
    - Superior for breaking down complex epics
    - Handles /epic-intake, /epic-plan, /epic-validate, /epic-tickets
    - Worth the cost for strategic planning

# v12-phase7-lead (5% of work)
- slug: v12-phase7-lead
  model: claude-opus-4-8
  rationale: |
    - BEST at computer use (83.4% vs Sonnet 72.5%)
    - Lock-free algorithm design requires deep reasoning
    - Critical for Phase 7 SIMA subgraph extraction
    - Jane Street alignment requires expert-level analysis
```

---

## Benchmark Evidence

### Source: Anthropic Official Benchmarks (May 28, 2026)

| Benchmark | Opus 4.8 | Sonnet 4.6 | Winner | Gap |
|-----------|----------|------------|--------|-----|
| **Agentic Coding** (SWE-Bench) | 69.2% | **79.6%** | Sonnet | +10.4% |
| **Terminal Coding** | **74.6%** | 59.1% | Opus | +15.5% |
| **Multidisciplinary (no tools)** | **49.8%** | 33.2% | Opus | +16.6% |
| **Multidisciplinary (with tools)** | **57.9%** | 49.0% | Opus | +8.9% |
| **Computer Use** | **83.4%** | 72.5% | Opus | +10.9% |
| **Knowledge Work** (GDPval-AA) | **1890** | 1633 | Opus | +15.7% |
| **Financial Analysis** | 53.9% | **63.3%** | Sonnet | +9.4% |
| **Agentic Tool Use** (L2-bench) | 91.7% | **97.9%** | Sonnet | +6.2% |

**Key Insight**: Sonnet 4.6 BEATS Opus 4.8 at real-world agentic coding tasks (SWE-Bench), making it the optimal choice for code execution.

---

## Workflow-Specific Assignments

### A. /epic-run (Full Orchestration)

| Phase | Task | Model | Cost | Rationale |
|-------|------|-------|------|-----------|
| **Phase 1: Intake** | Scope alignment | Opus 4.8 | $5.25 | Knowledge work (1890 score) |
| **Phase 2: Plan** | Analysis + approach | Opus 4.8 | $5.25 | Multidisciplinary reasoning (57.9%) |
| **Phase 2.3: Scan** | Sentinel audit | Sonnet 4.6 | $1.05 | Agentic tool use (97.9%) |
| **Phase 3: Validate** | DNA compliance | Opus 4.8 | $5.25 | Architecture stress-test |
| **Phase 4: Tickets** | Ticket generation | Opus 4.8 | $5.25 | Strategic decomposition |
| **Phase 5: Execution** | Code changes (10 tickets) | Sonnet 4.6 | $10.50 | Agentic coding (79.6%) ⭐ |
| **Phase 6: Verification** | Testing + audit | Sonnet 4.6 | $1.05 | Terminal coding (59.1%) |

**Total Cost per Epic**: $31.50 (vs $105 if all-Opus)

### B. /epic-tdd (Manual Execution)

| Gate | Task | Model | Cost | Rationale |
|------|------|-------|------|-----------|
| **Gate 1: Intake** | Ticket review | Sonnet 4.6 | $1.05 | Fast, cheap verification |
| **Gate 2: Plan Review** | Independent audit | Opus 4.8 | $5.25 | Deep reasoning required |
| **Gate 2.3: Sentinel** | Security scan | Sonnet 4.6 | $1.05 | Tool use (97.9%) |
| **Gate 3: DNA Validation** | Compliance check | Opus 4.8 | $5.25 | Jane Street alignment |
| **Step 1: Implementation** | Code changes | Sonnet 4.6 | $1.05 | Agentic coding (79.6%) ⭐ |
| **Step 2: Verification** | Local tests | Sonnet 4.6 | $1.05 | Terminal coding (59.1%) |
| **Step 5: Perfection** | /pr-loop | Sonnet 4.6 | $3.15 | Iterative fixes (3 iterations) |

**Total Cost per Ticket**: $13.65 (vs $36.75 if all-Opus)

### C. /pr-loop (Perfection Loop)

**All steps use Sonnet 4.6**:

| Step | Task | Cost | Rationale |
|------|------|------|-----------|
| **Step 0: Hygiene** | Rebase + diff check | $1.05 | Fast, cheap |
| **Step 1: Forensics** | Extract bot findings | $1.05 | Tool use (97.9%) |
| **Step 2: Repair** | Fix VALID issues | $1.05 | Agentic coding (79.6%) ⭐ |
| **Step 3: Push** | Monitor checks | $1.05 | Terminal coding (59.1%) |

**Total Cost per PR Loop**: $3.15 (average 3 iterations)

---

## Cost Analysis

### Monthly Projection (20 sessions)

**Scenario A: All Opus 4.8**
```
20 sessions × $5.25 = $105/month
```

**Scenario B: Hybrid (Recommended)**
```
Planning (4 sessions):    4 × $5.25 = $21.00
Execution (16 sessions): 16 × $1.05 = $16.80
Total: $37.80/month
```

**Savings**: $67.20/month (64% cheaper)

### Quality Comparison

| Metric | All Opus | Hybrid | Winner |
|--------|----------|--------|--------|
| **Code Quality** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | **Hybrid** (Sonnet better at coding) |
| **Architecture** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | **Tie** (both use Opus) |
| **Speed** | ⏱️ 60s | ⏱️ 35s | **Hybrid** (Sonnet 2x faster) |
| **Cost** | 💰💰💰💰💰 | 💰 | **Hybrid** (64% cheaper) |

---

## Escalation Protocols

### When to Escalate from Sonnet to Opus

**Automatic Escalation Triggers**:
1. ❌ Sonnet fails to solve problem after 3 attempts
2. ❌ Task requires deep architectural reasoning
3. ❌ Lock-free concurrency design (Phase 7 work)
4. ❌ Jane Street pattern selection (not implementation)

**Manual Escalation Methods**:

#### Method A: Switch Mode (Recommended)
```bash
# In Bob CLI, type:
/switch v12-epic-planner

# Then give instruction:
"I need deeper architectural analysis for [specific problem]"
```

#### Method B: New Task with Different Model
```bash
# Start fresh session with Opus:
bob --mode v12-epic-planner

# Or use Antigravity orchestrator:
antigravity "Use Opus 4.8 to analyze [problem]"
```

#### Method C: Manual Model Override (Advanced)
```bash
# In .bob/settings.json, temporarily override:
{
  "model_override": "claude-opus-4-8"
}
# Then restart Bob CLI
```

**Best Practice**: Use Method A (switch mode) - fastest and preserves context.

---

## Jane Street Alignment

### V12 DNA Principles (from `JANE_STREET_DEVIATIONS.md`)

1. **Correctness by Construction**: Make illegal states unrepresentable
2. **Zero-Allocation Hot Paths**: Stack allocation over heap allocation
3. **Lock-Free Concurrency**: FSM/Actor pattern, atomic primitives only
4. **Microsecond Latency**: Every allocation, lock, or virtual call is scrutinized

### Model Requirements for Jane Street Patterns

**Design Decisions** (Opus 4.8):
- ✅ Struct vs class trade-offs
- ✅ When to use `Volatile.Read/Write`
- ✅ Cache-line alignment strategies
- ✅ Lock-free algorithm selection

**Implementation** (Sonnet 4.6):
- ✅ Applying the chosen pattern
- ✅ Avoiding `lock()` statements
- ✅ Writing zero-allocation code
- ✅ Refactoring for complexity reduction

**Conclusion**: Use Opus for **design decisions**, Sonnet for **implementation**.

---

## Model Availability Status

### Confirmed Available (as of 2026-05-28)

| Provider | Model | Status | Use Case |
|----------|-------|--------|----------|
| **Anthropic** | Claude Opus 4.8 | ✅ Available | Strategic planning |
| **Anthropic** | Claude Sonnet 4.6 | ✅ Available | Code execution |
| **Anthropic** | Claude Haiku 4.5 | ✅ Available | Not used (too weak) |
| **OpenAI** | GPT-4o | ✅ Available | Not used (inferior to Claude) |
| **OpenAI** | o1-preview | ✅ Available | Not used (reasoning only) |
| **Google** | Gemini 3.1 Pro | ✅ Available | Not used (inferior to Claude) |

### NOT Available (Confirmed)

| Model | Status | Notes |
|-------|--------|-------|
| GPT-5.5 | ❌ Does not exist | Benchmark mislabel |
| Gemini 3.5 | ❌ Does not exist | Latest is 3.1 Pro |

---

## Decision Log

### Decision #1: Hybrid Model Strategy

**Date**: 2026-05-28  
**Approved By**: Director  
**Benchmark Source**: Anthropic Official (May 28, 2026)

**Key Finding**: Sonnet 4.6 outperforms Opus 4.8 at agentic coding (79.6% vs 69.2%)

**Implementation**:
- v12-engineer → Sonnet 4.6 (code execution)
- v12-epic-planner → Opus 4.8 (strategic planning)
- v12-phase7-lead → Opus 4.8 (concurrency design)

**Impact**:
- ✅ 64% cost reduction ($37.80 vs $105/month)
- ✅ Superior code quality (Sonnet better at coding)
- ✅ 2x faster execution (Sonnet 30s vs Opus 60s)
- ✅ Maintains architectural excellence (Opus for planning)

**Trade-offs**:
- ❌ Requires mode switching for escalation
- ❌ More complex workflow (2 models vs 1)
- ✅ But: Benchmark data proves this is optimal

---

## Monthly Review Checklist

- [ ] Verify cost savings are realized (target: $37.80/month)
- [ ] Check if new models are released (Opus 4.9, Sonnet 4.7, etc.)
- [ ] Confirm escalation protocols are being followed
- [ ] Review any cases where Sonnet failed and required Opus
- [ ] Update benchmark data if new tests are published
- [ ] Adjust model assignments if performance changes

**Last Review**: 2026-05-28  
**Next Review**: 2026-06-28  
**Reviewer**: Director

---

## References

- Benchmark Images: Provided by Director (2026-05-28)
- Mode Configuration: `.bob/custom_modes.yaml`
- Jane Street Principles: `docs/standards/JANE_STREET_DEVIATIONS.md`
- Workflow Definitions: `AGENTS.md` (epic-run, epic-tdd, pr-loop commands)
- Cost Calculations: Based on Anthropic pricing (May 2026)

---

## Appendix: Benchmark Methodology

### SWE-Bench Pro (Agentic Coding)
- **What it tests**: Real-world GitHub issue resolution
- **Why it matters**: Directly measures code generation quality
- **V12 relevance**: EPIC-8 through EPIC-14 extraction tickets

### Multidisciplinary Reasoning (Humanity's Last Exam)
- **What it tests**: Cross-domain problem solving
- **Why it matters**: Epic planning requires broad knowledge
- **V12 relevance**: /epic-plan phase (analysis + approach)

### Computer Use (OSWorld-Verified)
- **What it tests**: Tool use and system interaction
- **Why it matters**: Lock-free design requires deep system understanding
- **V12 relevance**: Phase 7 concurrency work

### Knowledge Work (GDPval-AA)
- **What it tests**: Information synthesis and reasoning
- **Why it matters**: Scope alignment and ticket generation

---

## All Mode Assignments Summary

| Mode | Type | Model | Cost | Primary Use |
|------|------|-------|------|-------------|
| **Plan** | Built-in | Opus 4.8 | $5.25 | Strategic planning, design docs |
| **Code** | Built-in | Sonnet 4.6 | $1.05 | DEPRECATED (use Advanced) |
| **Advanced** | Built-in | Sonnet 4.6 | $1.05 | PRIMARY code mode (MCP tools) |
| **Ask** | Built-in | Sonnet 4.6 | $1.05 | Q&A, explanations, research |
| **Orchestrator** | Built-in | Opus 4.8 | $5.25 | Multi-agent coordination |
| **v12-engineer** | Custom | Sonnet 4.6 | $1.05 | EPIC tickets, refactoring |
| **v12-epic-planner** | Custom | Opus 4.8 | $5.25 | Epic planning (4 phases) |
| **v12-phase7-lead** | Custom | Opus 4.8 | $5.25 | Lock-free concurrency |

---

## References

- Benchmark Images: Provided by Director (2026-05-28)
- Built-in Mode Configuration: `.bob/settings.json`
- Custom Mode Configuration: `.bob/custom_modes.yaml`
- Jane Street Principles: `docs/standards/JANE_STREET_DEVIATIONS.md`
- Workflow Definitions: `AGENTS.md` (epic-run, epic-tdd, pr-loop commands)
- Cost Calculations: Based on Anthropic pricing (May 2026)
- **V12 relevance**: /epic-intake and /epic-tickets phases