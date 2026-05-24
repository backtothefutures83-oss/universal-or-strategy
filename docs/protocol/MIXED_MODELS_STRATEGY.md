# Mixed Models Strategy

## Overview
This document defines the mixed models strategy for V12, enabling optimal model selection per task type while maintaining cost efficiency and quality.

## Model Inventory

### Claude Opus 4.7
- **Strengths**: Deep reasoning, architectural analysis, complex planning
- **Cost**: High ($15/1M input, $75/1M output)
- **Use Cases**: Epic planning, architectural review, complex design decisions

### Claude Sonnet 4.6
- **Strengths**: Fast coding, balanced reasoning, good accuracy
- **Cost**: Medium ($3/1M input, $15/1M output)
- **Use Cases**: src/ implementation, validation, refactoring

### Claude Haiku 4.5
- **Strengths**: Ultra-fast, low cost, simple tasks
- **Cost**: Low ($0.25/1M input, $1.25/1M output)
- **Use Cases**: Formatting, simple edits, documentation

## Custom Droids Configuration

### v12-engineer (Sonnet 4.6)
```yaml
name: v12-engineer
model: claude-sonnet-4-6-20250929
reasoningEffort: medium
tools: ["Read", "Edit", "Create", "ApplyPatch", "Execute", "LS", "Grep", "Glob"]
```

**Rationale:**
- Sonnet 4.6 provides optimal balance for coding tasks
- Medium reasoning sufficient for TDD implementation
- Edit tools enable fast, accurate code changes

### v12-planner (Opus 4.7)
```yaml
name: v12-planner
model: claude-opus-4-7-20251101
reasoningEffort: high
tools: ["Read", "LS", "Grep", "Glob", "WebSearch"]
```

**Rationale:**
- Opus 4.7 excels at deep architectural analysis
- High reasoning enables thorough planning
- Read-only tools prevent accidental edits

### v12-validator (Sonnet 4.6)
```yaml
name: v12-validator
model: claude-sonnet-4-6-20250929
reasoningEffort: medium
tools: ["Read", "Execute", "LS", "Grep", "Glob"]
```

**Rationale:**
- Sonnet 4.6 sufficient for validation tasks
- Medium reasoning balances speed and accuracy
- Execute tools enable automated audits

## Task-to-Model Mapping

### Epic Planning (Opus 4.7)
**Tasks:**
- `/epic-intake` - Scope alignment
- `/epic-plan` - Analysis + approach
- `/epic-validate` - Stress-test approach

**Why Opus:**
- Requires deep reasoning
- Complex architectural decisions
- High cost justified by planning quality

### Ticket Execution (Sonnet 4.6)
**Tasks:**
- `/ticket` - TDD implementation
- TDD RED - Write failing test
- TDD GREEN - Implement feature
- TDD REFACTOR - Clean up code

**Why Sonnet:**
- Fast coding with good accuracy
- Medium reasoning sufficient
- Cost-effective for iterative work

### Validation (Sonnet 4.6)
**Tasks:**
- Milestone validation
- DNA audits
- Test execution
- Report generation

**Why Sonnet:**
- Reliable verification
- Fast execution
- Cost-effective for frequent runs

### Documentation (Haiku 4.5)
**Tasks:**
- Formatting
- Simple edits
- README updates
- Comment generation

**Why Haiku:**
- Ultra-fast
- Very low cost
- Sufficient for simple tasks

## Cost Optimization Strategies

### 1. Prompt Caching
**Configuration:**
```json
{
  "anthropic_beta": "prompt-caching-2024-07-31",
  "system": [
    {
      "type": "text",
      "text": "...",
      "cache_control": {"type": "ephemeral"}
    }
  ]
}
```

**Savings:**
- 90% cost reduction on cached content
- Effective for repeated system prompts
- Amortizes over multiple requests

### 2. Batch Processing
**Strategy:**
- Group similar tasks together
- Use single model session
- Leverage context continuity

**Example:**
```bash
# Bad: 3 separate sessions
/ticket 01
/ticket 02
/ticket 03

# Good: 1 batch session
/epic-run <epic-slug>
```

### 3. Model Downgrading
**Strategy:**
- Start with Sonnet for most tasks
- Escalate to Opus only when needed
- Downgrade to Haiku for simple tasks

**Decision Tree:**
```
Task complexity?
├─ High (architectural) → Opus 4.7
├─ Medium (coding) → Sonnet 4.6
└─ Low (formatting) → Haiku 4.5
```

## Reasoning Effort Configuration

### High Reasoning (Opus 4.7)
**Use Cases:**
- Epic planning
- Architectural review
- Complex design decisions

**Configuration:**
```yaml
reasoningEffort: high
```

**Cost Impact:**
- 2-3x more tokens
- Justified by planning quality

### Medium Reasoning (Sonnet 4.6)
**Use Cases:**
- TDD implementation
- Validation
- Refactoring

**Configuration:**
```yaml
reasoningEffort: medium
```

**Cost Impact:**
- Balanced token usage
- Optimal for most tasks

### Low Reasoning (Haiku 4.5)
**Use Cases:**
- Formatting
- Simple edits
- Documentation

**Configuration:**
```yaml
reasoningEffort: low
```

**Cost Impact:**
- Minimal token usage
- Fast execution

## Mixed Models in Practice

### Example: Epic Execution
```bash
# Phase 1: Planning (Opus 4.7)
/epic-intake <epic-slug>  # Scope alignment
/epic-plan <epic-slug>    # Analysis + approach
/epic-validate <epic-slug> # Stress-test

# Phase 2: Execution (Sonnet 4.6)
/epic-run <epic-slug>     # TDD implementation

# Phase 3: Validation (Sonnet 4.6)
# Automatic after each ticket
```

**Cost Breakdown:**
- Planning: $5 (Opus, 1 session)
- Execution: $15 (Sonnet, 10 tickets)
- Validation: $5 (Sonnet, 10 runs)
- **Total: $25 per epic**

### Example: Single Ticket
```bash
# Ticket execution (Sonnet 4.6)
/ticket 01

# Automatic TDD flow:
# 1. TDD RED (Sonnet)
# 2. TDD GREEN (Sonnet)
# 3. TDD REFACTOR (Sonnet)
# 4. Milestone Validation (Sonnet)
```

**Cost Breakdown:**
- TDD RED: $0.50
- TDD GREEN: $1.00
- TDD REFACTOR: $0.50
- Validation: $0.50
- **Total: $2.50 per ticket**

## Quality Metrics

### Model Performance by Task Type

**Epic Planning (Opus 4.7):**
- Accuracy: 95%
- Completeness: 98%
- Rework rate: < 5%

**Ticket Execution (Sonnet 4.6):**
- Test pass rate: 98%
- DNA compliance: 100%
- Rework rate: < 10%

**Validation (Sonnet 4.6):**
- False positive rate: < 2%
- False negative rate: < 1%
- Audit completeness: 100%

## Migration Path

### Phase 1: Current State
- All tasks use Sonnet 4.6
- No model specialization
- Suboptimal cost/quality

### Phase 2: Mixed Models (This Phase)
- Opus for planning
- Sonnet for execution
- Haiku for documentation

### Phase 3: Future Optimization
- Dynamic model selection
- Cost-based routing
- Quality-based escalation

## References

- [TDD Hardening Protocol](TDD_HARDENING_PROTOCOL.md)
- [Droid Missions Integration](DROID_MISSIONS_INTEGRATION.md)
- [Prompt Caching Configuration](../setup/PROMPT_CACHING.md)
- [Bob CLI Documentation](../../.bob/PROTOCOL.md)
