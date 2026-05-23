# V12 Protocol Index

## Overview

This index provides a comprehensive map of all V12 protocols, organized by scope and agent type. Use this as your starting point for understanding the V12 protocol ecosystem.

## Universal Protocols (All Agents)

### Primary Sources

- **[AGENTS.md](../../AGENTS.md)** - Primary source of truth for all agents
  - Agent hierarchy and roles
  - Architectural mandates (Platinum Standard)
  - V12 DNA enforcement
  - Tool access matrix
  - Karpathy behavioral protocols
  - Phase 6 recursive protocol

- **[UNIVERSAL_AGENT_PROTOCOL.md](UNIVERSAL_AGENT_PROTOCOL.md)** - Cross-agent standards
  - Mandatory tool stack
  - Quality gates
  - Testing protocols
  - LangSmith tracing
  - PR separation enforcement
  - MCP configuration
  - Workflow integration

### Workflow Protocols

- **[PR_LOOP_V2.md](PR_LOOP_V2.md)** - Enhanced PR perfection workflow
  - Bot forensics extraction (Step 1)
  - CI log extraction (Step 1.5)
  - Local repair (Step 2)
  - Global push & monitor (Step 3)
  - Manual override gate (Step 4)
  - F5 verification (Step 5)

- **[CODEFACTOR_PROTOCOL.md](CODEFACTOR_PROTOCOL.md)** - CodeFactor safety protocol
  - NEVER use "Apply fixes" button
  - Manual fixes only
  - Build verification after every batch
  - Emergency rollback procedures

## Agent-Specific Protocols

### Bob CLI (Primary src/ Engineer)

- **[.bob/PROTOCOL.md](../../.bob/PROTOCOL.md)** - Bob-specific protocols
  - Session initialization
  - Mandatory checks
  - PR workflow
  - Tool priority
  - V12 DNA enforcement
  - Handoff protocols
  - Quality gates

- **[.bob/rules/](../../.bob/rules/)** - Bob rule files
  - `00-pr-hygiene.md` - PR hygiene mandate
  - Additional Bob-specific rules

- **[.bob/commands/](../../.bob/commands/)** - Bob workflow commands
  - `epic-run.md` - Full epic orchestration
  - `pr-loop.md` - PR perfection loop
  - `pr-split.md` - Split mixed PRs
  - `pre-push.md` - Pre-push checklist

### Cursor IDE (Editor-Specific)

- **[.cursorrules](../../.cursorrules)** - Cursor-only rules
  - **SCOPE:** Cursor IDE only, NOT Bob CLI
  - Standards manifesto reference
  - UltraThink/UltraPlan protocols
  - Tool parity
  - Documentation hardening

### Gemini CLI (Orchestrator + Non-src Engineer)

- **[docs/agents/GEMINI_TOOLS.md](../agents/GEMINI_TOOLS.md)** - Gemini capabilities
  - Google AI Studio integration
  - Context caching (75% savings)
  - Multimodal support
  - Tool access matrix
  - When to use Gemini vs Bob

### Jules AI (GitHub-Based Non-src Engineer)

- **[docs/agents/JULES_TOOLS.md](../agents/JULES_TOOLS.md)** - Jules capabilities
  - GitHub API integration
  - PR automation
  - Non-src engineering
  - Tool access matrix
  - When to use Jules vs Bob

### Qwen (Privacy-First Local Agent)

- **[docs/agents/QWEN_TOOLS.md](../agents/QWEN_TOOLS.md)** - Qwen capabilities
  - Local inference
  - Privacy-first workflows
  - Hardware requirements
  - Tool access matrix
  - When to use Qwen vs cloud agents

### Codex CLI (Logic Hardening Specialist)

- **[docs/agents/CODEX_TOOLS.md](../agents/CODEX_TOOLS.md)** - Codex capabilities
  - Logic hardening
  - Lock-free conversions
  - Atomic operations
  - Tool access matrix
  - When to use Codex vs Bob

## Configuration

### MCP Configuration

- **[docs/setup/MCP_CONFIGURATION.md](../setup/MCP_CONFIGURATION.md)** - Centralized MCP setup
  - jCodemunch MCP
  - Sequential thinking
  - Server configuration
  - Environment variables

### Prompt Caching

- **[docs/setup/PROMPT_CACHING.md](../setup/PROMPT_CACHING.md)** - Multi-provider caching
  - Anthropic caching
  - Google Gemini caching
  - OpenAI caching
  - Cost savings (75%+)

### GitHub Apps

- **[docs/setup/GITHUB_APPS_INSTALLATION.md](../setup/GITHUB_APPS_INSTALLATION.md)** - Bot installation
  - CodeRabbit AI
  - Sourcery AI
  - Codacy
  - cubic-dev-ai
  - Amazon Q Developer
  - Semgrep
  - CodeQL
  - Snyk
  - Gitleaks
  - SonarCloud

### Semgrep

- **[docs/setup/SEMGREP_SETUP.md](../setup/SEMGREP_SETUP.md)** - Semgrep configuration
  - V12 DNA rules
  - Lock detection
  - ASCII compliance
  - Custom rule creation

## Workflows

### Epic Orchestration

- **[.bob/commands/epic-run.md](../../.bob/commands/epic-run.md)** - Full epic workflow
  - Phase 1: Intake
  - Phase 2: Plan
  - Phase 2.3: Scan (Sentinel audit)
  - Phase 3: Validate
  - Phase 4: Tickets
  - Phase 5: Execution (ticket loop)
  - Phase 6: PR submission (TWO separate PRs)

### PR Workflows

- **[.bob/commands/pr-loop.md](../../.bob/commands/pr-loop.md)** - PR perfection loop
  - Step 0: Pre-flight hygiene
  - Step 1: Bot forensics
  - Step 1.5: CI log extraction
  - Step 2: Local repair
  - Step 3: Global push & monitor
  - Step 4: Manual override gate
  - Step 5: F5 verification

- **[.bob/commands/pr-split.md](../../.bob/commands/pr-split.md)** - Split mixed PRs
  - Verify violation
  - Fetch file list
  - Close original PR
  - Create src-only PR
  - Create non-src-only PR
  - Link PRs

- **[.bob/commands/pre-push.md](../../.bob/commands/pre-push.md)** - Pre-push checklist
  - Branch hygiene
  - Diff size check
  - Lock detection
  - ASCII compliance
  - Build verification

## Enforcement

### Verification Scripts

- **[scripts/verify_pr_separation.ps1](../../scripts/verify_pr_separation.ps1)** - PR separation check
  - Detects mixed src/ and non-src/ files
  - Returns PASS/VIOLATION
  - Used in /pr-loop Step 0

- **[scripts/verify_pr_hygiene.ps1](../../scripts/verify_pr_hygiene.ps1)** - PR hygiene check
  - Branch rebase status
  - Diff size limit (10k)
  - Clean working directory
  - Used in /pre-push

- **[scripts/extract_pr_forensics.ps1](../../scripts/extract_pr_forensics.ps1)** - Bot forensics
  - Extracts bot findings
  - Categorizes as VALID/HALLUCINATION/INFRA-NOISE
  - Generates fix queue
  - Used in /pr-loop Step 1

- **[scripts/extract_ci_logs.ps1](../../scripts/extract_ci_logs.ps1)** - CI log extraction
  - Fetches actual CI failure logs
  - Ground truth verification
  - Cross-references with bot comments
  - Used in /pr-loop Step 1.5

- **[scripts/calculate_fleet_score.ps1](../../scripts/calculate_fleet_score.ps1)** - Fleet score calculation
  - Local score (15 points)
  - Global score (85 points)
  - PHS (Project Health Score)
  - Used in /pr-loop Step 3

### Quality Tools

- **[scripts/run_semgrep.ps1](../../scripts/run_semgrep.ps1)** - Semgrep runner
  - V12 DNA pattern matching
  - Lock detection
  - ASCII compliance
  - Custom rules

- **[scripts/complexity_audit.py](../../scripts/complexity_audit.py)** - Complexity audit
  - Cyclomatic complexity
  - Function length
  - Nesting depth
  - Jane Street alignment (threshold 15)

- **[scripts/dead_code_scan.py](../../scripts/dead_code_scan.py)** - Dead code detection
  - Unreachable functions
  - Unused imports
  - Orphaned files

- **[scripts/format_all_csharp.ps1](../../scripts/format_all_csharp.ps1)** - Code formatting
  - CSharpier formatting
  - Consistent style
  - Pre-commit hook

### Build & Deploy

- **[scripts/build_readiness.ps1](../../scripts/build_readiness.ps1)** - Build verification
  - Compilation check
  - Test execution
  - Linting
  - Readiness report

- **[deploy-sync.ps1](../../deploy-sync.ps1)** - Hard-link synchronization
  - **MANDATORY after every src/ edit**
  - Syncs NinjaTrader hard links
  - ASCII gate verification
  - Diff guard (10k limit)

## Testing

### Test Harnesses

- **[docs/TESTING_AND_TOOLS.md](../TESTING_AND_TOOLS.md)** - Testing overview
  - xUnit tests
  - BenchmarkDotNet
  - AMAL Harness
  - Stress tests

### Test Scripts

- **[scripts/test_stress.ps1](../../scripts/test_stress.ps1)** - Stress testing
  - Concurrency tests
  - Load tests
  - Memory leak detection

- **[scripts/amal_harness_v26.py](../../scripts/amal_harness_v26.py)** - AMAL Harness
  - Automated testing
  - Performance benchmarks
  - Regression detection

## Knowledge Base

### Jane Street Intel

- **[scripts/query_kb.py](../../scripts/query_kb.py)** - KB query tool
  - HFT patterns
  - Lock-free designs
  - Microsecond-latency optimizations
  - **MANDATORY before architecture/performance/concurrency tasks**

- **[docs/intel/jane-street/](../intel/jane-street/)** - Jane Street knowledge
  - Lock-free patterns
  - Atomic operations
  - Performance optimization
  - Testing standards

### Architecture Tools

- **[graphify](../../graphify-out/)** - Knowledge graph
  - Repository structure
  - God nodes
  - Community detection
  - 71x token efficiency

- **[Routa CLI](https://github.com/your-org/routa-cli)** - Architecture analysis
  - Multi-file refactoring
  - Feature tree generation
  - Kanban workflow
  - Team coordination

- **[jCodemunch MCP](https://github.com/your-org/jcodemunch-mcp)** - Code navigation
  - Symbol search
  - Dependency analysis
  - Blast radius
  - Call hierarchy

## Quick Reference

### Agent Selection Matrix

| Task Type | Primary Agent | Secondary Agent | Tertiary Agent |
|-----------|---------------|-----------------|----------------|
| src/ Engineering | Bob CLI | Codex CLI | - |
| Architecture Design | Bob CLI | Claude Opus | - |
| Logic Hardening | Codex CLI | Bob CLI | - |
| Non-src/ Engineering | Jules AI | Gemini CLI | - |
| Orchestration | Gemini CLI | Bob CLI | - |
| Privacy-Sensitive | Qwen | - | - |
| GitHub Operations | Jules AI | Gemini CLI | - |
| Multimodal Tasks | Gemini CLI | - | - |

### PR Type Matrix

| PR Type | Files | Bot Audit | Workflow | Merge Strategy |
|---------|-------|-----------|----------|----------------|
| src-only | src/** | ✅ Full | /pr-loop to 100/100 | After F5 verification |
| non-src-only | docs/, tests/, etc. | ❌ None | Fast-track | Squash and merge |
| Mixed | BOTH | ❌ VIOLATION | /pr-split | Split into two PRs |

### Tool Priority Matrix

| Task | Tool 1 | Tool 2 | Tool 3 | Tool 4 |
|------|--------|--------|--------|--------|
| Code Navigation | jCodemunch MCP | graphify | - | - |
| Architecture | graphify | Routa CLI | Jane Street KB | - |
| Performance | Jane Street KB | BenchmarkDotNet | AMAL Harness | - |
| Testing | xUnit | BenchmarkDotNet | AMAL Harness | Stress tests |
| PR Workflow | verify_pr_separation | extract_pr_forensics | calculate_fleet_score | - |

## Protocol Versioning

- **Current Version:** V12.20
- **Last Updated:** 2026-05-23
- **Breaking Changes:** PR separation enforcement (Phase 2)

## Contributing

When adding new protocols:

1. Update this INDEX.md
2. Add to appropriate agent-specific docs
3. Update AGENTS.md if universal
4. Create verification script if enforceable
5. Add to relevant workflow commands

## Support

For protocol questions:

1. Check this INDEX.md first
2. Read agent-specific docs
3. Query Jane Street KB for HFT patterns
4. Consult AGENTS.md for universal rules
5. Ask Director for clarification

---

**Last Updated:** 2026-05-23  
**Maintained By:** V12 Protocol Team  
**Version:** V12.20 (Protocol Hardening Phase 2)