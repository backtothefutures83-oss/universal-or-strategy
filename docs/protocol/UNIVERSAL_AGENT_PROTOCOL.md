# Universal Agent Protocol - V12 DNA Enforcement

**Version:** 2.0  
**Last Updated:** 2026-05-23  
**Scope:** ALL agents (Bob, Cursor, Antigravity, Gemini CLI, Jules, Qwen, Rovo Dev, Hermes, Nemoclaw)

---

## 1. Mandatory Tool Stack

### 1.1 Core Tools (REQUIRED for ALL tasks)

| Tool | Purpose | Command | When to Use |
|------|---------|---------|-------------|
| **Routa CLI** | Code navigation, architecture analysis, multi-repo coordination | `routa -p "<query>"` | EVERY src/ task, architecture decisions |
| **jCodemunch MCP** | Symbol search, dependency analysis, blast radius | `search_symbols`, `get_blast_radius` | Code exploration, refactoring |
| **Jane Street KB** | HFT patterns, lock-free designs | `python scripts/query_kb.py "<term>"` | Performance optimization, concurrency |
| **LangSmith** | Agent tracing, handoff tracking | Auto-enabled via `.env` | Multi-agent sessions |
| **Graphify** | Knowledge graph, 71x token efficiency | `graphify update .` | After structural changes |

### 1.2 Quality Gates (REQUIRED before push)

| Tool | Purpose | Command | Failure Action |
|------|---------|---------|----------------|
| **Semgrep** | V12 DNA pattern enforcement | `powershell -File .\scripts\run_semgrep.ps1` | HALT - fix violations |
| **CodeRabbit CLI** | Local AI review | `coderabbit review` | Review findings before push |
| **Build Readiness** | Compilation + hard-link sync | `powershell -File .\scripts\build_readiness.ps1` | HALT - fix build errors |
| **PR Hygiene** | Rebase + diff size check | `powershell -File .\scripts\verify_pr_hygiene.ps1` | HALT - rebase or split PR |
| **Deploy Sync** | NinjaTrader hard-link sync | `powershell -File .\deploy-sync.ps1` | MANDATORY after src/ edits |

### 1.3 Testing Tools (REQUIRED for P4/P5 tasks)

| Tool | Purpose | Command | Coverage Target |
|------|---------|---------|-----------------|
| **xUnit** | Unit tests | `dotnet test tests/V12_Performance.Tests/` | >80% for new code |
| **BenchmarkDotNet** | Performance benchmarks | `dotnet run --project benchmarks` | All hot paths |
| **AMAL Harness** | Integration testing | `python scripts/amal_harness_v26.py` | Critical workflows |
| **Complexity Audit** | Cyclomatic complexity | `python scripts/complexity_audit.py` | <15 per function |

---

## 2. Routa CLI Integration (MANDATORY)

### 2.1 Installation Verification

```powershell
# Check if Routa is installed
routa --version

# If not installed, install via npm
npm install -g routa-cli

# Or via Cargo
cargo install routa-cli
```

### 2.2 Mandatory Routa Usage

**CRITICAL RULE:** ALL agents MUST use Routa CLI for:
- Architecture analysis
- Multi-file refactoring planning
- Cross-repository coordination
- Feature tree generation
- Kanban workflow automation

**Examples:**
```powershell
# Before any src/ refactoring
routa -p "Analyze the architecture of the SIMA subgraph"

# Before implementing a feature
routa -p "Plan the implementation of RMA proximity monitoring"

# For multi-file changes
routa -p "Identify all files affected by changing the FSM state machine"

# Kanban workflow
routa kanban card create --title "Implement feature X" --workspace-id default
routa kanban card move --card-id <id> --target-column-id in-progress
```

### 2.3 Routa + jCodemunch Synergy

```powershell
# Step 1: Use Routa for high-level planning
routa -p "Design the extraction plan for ProcessBracketEvent"

# Step 2: Use jCodemunch for detailed symbol analysis
# (via MCP tools in your agent session)
search_symbols(repo=".", query="ProcessBracketEvent")
get_blast_radius(repo=".", symbol="ProcessBracketEvent")

# Step 3: Use Routa for execution coordination
routa team run -t "Extract ProcessBracketEvent to SIMA subgraph"
```

---

## 3. LangSmith Tracing (MANDATORY for Multi-Agent Sessions)

### 3.1 Configuration

**File:** `.env` (create from `.env.example`)

```bash
# LangSmith Tracing (MANDATORY for multi-agent workflows)
LANGSMITH_TRACING=true
LANGSMITH_API_KEY=ls__your_key_here
LANGSMITH_PROJECT=V12-Universal-OR-Strategy
```

### 3.2 Tracing Continuity Protocol

**CRITICAL ISSUE:** Tracing works during testing but doesn't continue across agent handoffs.

**Root Cause:** Each agent spawns a new process without inheriting the parent's LangSmith context.

**Solution:**

1. **Session ID Propagation:**
   ```python
   # In nexus_relay.py (already implemented)
   @traceable(run_type="chain", name="A2A Relay")
   def relay_to_agent(to_agent, instructions):
       # Trace is automatically linked via LangSmith context
       pass
   ```

2. **Agent Handoff Protocol:**
   ```markdown
   When handing off to another agent:
   1. Call `python scripts/nexus_relay.py <agent_name> "<instructions>"`
   2. This emits a LangSmith trace linking the handoff
   3. The receiving agent inherits the trace context via environment
   ```

3. **Verification:**
   ```powershell
   # Test tracing connectivity
   python scripts/langsmith_bridge.py --test
   
   # Expected output: "[+] Trace emitted successfully."
   ```

### 3.3 Tracing Best Practices

- **Always trace handoffs:** Use `nexus_relay.py` for agent-to-agent communication
- **Trace forensic runs:** AMAL harness automatically traces via `langsmith_bridge.py`
- **Check traces:** Visit https://smith.langchain.com/o/<your-org>/projects/V12-Universal-OR-Strategy
- **Debug missing traces:** Check `.env` has `LANGSMITH_TRACING=true`

---

## 4. PR Separation Protocol (MANDATORY)

**CRITICAL RULE:** NEVER mix src/ and non-src/ files in the same PR.

### 4.1 Two-PR Model

| PR Type | Contents | Bot Audit | Merge Speed |
|---------|----------|-----------|-------------|
| **src/ PR** | Production code only | Full (CodeRabbit, Codacy, Semgrep) | Slow (85+ PHS required) |
| **non-src/ PR** | Docs, tests, workflows, scripts | Lightweight or none | Fast-track |

### 4.2 Verification Command

```powershell
# Before pushing ANY PR
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <PR_NUMBER>

# Expected output: "PR VALID: Separation protocol enforced"
# If mixed: "PR VIOLATION: Mixed src/ and non-src/ files detected"
```

### 4.3 Enforcement

**Script:** `scripts/verify_pr_separation.ps1`

```powershell
param([int]$PrNumber)

$files = gh pr view $PrNumber --json files --jq '.files[].path'
$srcFiles = $files | Where-Object { $_ -match '^src/' }
$nonSrcFiles = $files | Where-Object { $_ -notmatch '^src/' }

if ($srcFiles -and $nonSrcFiles) {
    Write-Error "PR VIOLATION: Mixed src/ and non-src/ files detected"
    Write-Host "src/ files: $($srcFiles -join ', ')"
    Write-Host "non-src/ files: $($nonSrcFiles -join ', ')"
    exit 1
}

Write-Host "PR VALID: Separation protocol enforced"
```

---

## 5. Jane Street KB Consultation (MANDATORY)

**CRITICAL PROTOCOL:** ALL agents MUST query the Jane Street Knowledge Base before ANY task involving:
- Architecture decisions
- Performance optimization
- Concurrency patterns
- Lock-free design
- State management
- ANY src/ file modification

### 5.1 Query Command

```powershell
python scripts/query_kb.py "<your query>"
```

### 5.2 Examples

```powershell
# Before implementing lock-free pattern
python scripts/query_kb.py "lock-free queue implementation"

# Before refactoring state management
python scripts/query_kb.py "actor model state mutations"

# Before performance optimization
python scripts/query_kb.py "microsecond latency patterns"

# Before concurrency work
python scripts/query_kb.py "atomic operations best practices"
```

### 5.3 Enforcement

**Agents that skip KB consultation violate V12 DNA protocol** and risk introducing anti-patterns that contradict Jane Street HFT principles.

---

## 6. MCP Configuration (Centralized)

### 6.1 Configuration File

**File:** `.mcp/config.json`

```json
{
  "mcpServers": {
    "jcodemunch": {
      "command": "jcodemunch-mcp",
      "args": [],
      "env": {}
    },
    "lsp-mcp": {
      "command": "cjl-lsp-mcp",
      "args": ["--workspace", "."],
      "env": {}
    }
  },
  "agents": [
    "bob",
    "cursor",
    "antigravity",
    "gemini-cli",
    "jules",
    "qwen",
    "rovo-dev",
    "hermes",
    "nemoclaw"
  ]
}
```

### 6.2 Agent-Specific Access

| Agent | jCodemunch | LSP MCP | Routa CLI | Browser |
|-------|------------|---------|-----------|---------|
| Bob CLI (v12-engineer) | ✅ | ✅ | ✅ | ❌ |
| Bob CLI (v12-epic-planner) | ✅ | ✅ | ✅ | ✅ |
| Claude (Architect) | ✅ | ✅ | ✅ | ✅ |
| Codex (Engineer) | ✅ | ✅ | ✅ | ❌ |
| Cursor | ✅ | ✅ | ❌ | ❌ |
| Gemini CLI | ✅ | ✅ | ✅ | ✅ |
| Jules AI | ✅ | ❌ | ✅ | ✅ |

### 6.3 Verification

```powershell
# Verify MCP servers are accessible
jcodemunch-mcp --version
cjl-lsp-mcp --help

# Test jCodemunch connection
# (via your agent's MCP interface)
resolve_repo(path=".")
```

---

## 7. Prompt Caching Configuration

### 7.1 Configuration File

**File:** `.anthropic/cache_config.json`

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
    "docs/intel/jane-street/**/*.md"
  ],
  "estimated_savings": {
    "tokens_per_session": 50000,
    "sessions_per_day": 10,
    "cost_per_1M_tokens": 3.00,
    "annual_savings_usd": 438
  }
}
```

### 7.2 Benefits

- **50,000 tokens saved per session** (AGENTS.md + rules + protocols)
- **$438/year cost savings** (10 sessions/day × 365 days)
- **Faster response times** (cached prompts load instantly)

### 7.3 Verification

Check your Anthropic dashboard for cache hit rates after enabling.

---

## 8. Workflow Integration

### 8.1 Pre-Push Checklist (MANDATORY)

```powershell
# 1. Rebase onto main
git fetch origin main && git rebase origin/main

# 2. Run PR hygiene check
powershell -File .\scripts\verify_pr_hygiene.ps1

# 3. Run Semgrep V12 DNA scan
powershell -File .\scripts\run_semgrep.ps1

# 4. Run CodeRabbit local review
coderabbit review

# 5. If src/ files changed: sync hard links
powershell -File .\deploy-sync.ps1

# 6. Run build readiness
powershell -File .\scripts\build_readiness.ps1

# 7. Push
git push
```

### 8.2 PR Loop Integration

**Command:** `/pr-loop <PR_NUMBER>`

**Phases:**
1. **Step 0:** PR Hygiene (rebase + diff size)
2. **Step 1:** Bot Forensics (extract findings)
3. **Step 1.5:** CI Log Extraction (ground truth)
4. **Step 2:** Local Repair (fix VALID issues)
5. **Step 3:** Global Push & Monitor (wait for bots)
6. **Step 4:** Manual Override Gate (if PHS < 100 after 3+ iterations)
7. **Step 5:** F5 Verification (NinjaTrader test)

### 8.3 Epic Run Integration

**Command:** `/epic-run <epic-slug> <target-description>`

**Phases:**
1. **Phase 1:** Intake (scope definition)
2. **Phase 2:** Plan (analysis + approach)
3. **Phase 2.3:** Scan (Sentinel audit)
4. **Phase 3:** Validate (DNA compliance)
5. **Phase 4:** Tickets (execution plan)
6. **Phase 5:** Execution (ticket loop with `/pr-loop` per ticket)
7. **Phase 6:** PR Submission & Perfection

---

## 9. Tool Access Matrix

### 9.1 By Agent

| Agent | Tools Available |
|-------|----------------|
| **Bob CLI (v12-engineer)** | read, edit, command, jCodemunch, Routa, graphify, deploy-sync |
| **Bob CLI (v12-epic-planner)** | read, edit (docs/ only), jCodemunch, Routa, graphify |
| **Claude (Architect)** | read, jCodemunch, Routa, graphify, browser |
| **Codex (Engineer)** | read, edit, command, jCodemunch, Routa, graphify |
| **Cursor** | read, edit, CSharpier, OmniSharp, jCodemunch |
| **Gemini CLI** | read, edit, command, jCodemunch, Routa, graphify, browser |
| **Jules AI** | read, edit (GitHub), jCodemunch, Routa, browser |

### 9.2 By Task Type

| Task Type | Primary Agent | Required Tools |
|-----------|---------------|----------------|
| **src/ Refactoring** | Bob CLI | Routa, jCodemunch, Jane Street KB, Semgrep |
| **Architecture Design** | Claude Opus | Routa, jCodemunch, Jane Street KB, graphify |
| **GitHub Workflows** | Jules AI | Routa, GitHub CLI |
| **Documentation** | Gemini CLI | Routa, graphify |
| **Testing** | Bob CLI | xUnit, BenchmarkDotNet, AMAL Harness |

---

## 10. Enforcement & Verification

### 10.1 Protocol Violations

| Violation | Severity | Action |
|-----------|----------|--------|
| Skipped KB consultation | P0 | HALT - query KB before proceeding |
| Mixed src/ + non-src/ PR | P0 | HALT - split into 2 PRs |
| No Routa usage for src/ task | P1 | WARNING - use Routa for planning |
| No LangSmith trace for handoff | P2 | WARNING - use nexus_relay.py |
| Skipped Semgrep scan | P0 | HALT - run Semgrep before push |

### 10.2 Verification Commands

```powershell
# Verify all tools are installed
bob --version
routa --version
jcodemunch-mcp --version
python --version
dotnet --version
gh --version

# Verify LangSmith tracing
python scripts/langsmith_bridge.py --test

# Verify MCP servers
cat .mcp/config.json

# Verify prompt caching
cat .anthropic/cache_config.json
```

---

## 11. Quick Reference

### 11.1 Essential Commands

```powershell
# Architecture & Planning
routa -p "Analyze the architecture of this repository"
python scripts/query_kb.py "lock-free patterns"
graphify update .

# Code Quality
powershell -File .\scripts\run_semgrep.ps1
coderabbit review
python scripts/complexity_audit.py

# Build & Deploy
dotnet build .\Linting.csproj
powershell -File .\deploy-sync.ps1
powershell -File .\scripts\build_readiness.ps1

# PR Workflow
powershell -File .\scripts\verify_pr_hygiene.ps1
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <N>

# Testing
dotnet test tests/V12_Performance.Tests/
dotnet run --project benchmarks
python scripts/amal_harness_v26.py
```

### 11.2 Emergency Contacts

- **Routa Issues:** Check `.mcp/config.json`, verify PATH
- **LangSmith Issues:** Check `.env`, test with `langsmith_bridge.py --test`
- **jCodemunch Issues:** Verify `.mcp.json`, check PATH
- **Build Issues:** Run `deploy-sync.ps1`, verify hard links
- **KB Issues:** Check `firebase-credentials.json`, test connection

---

## 12. References

- **Main Protocol:** `AGENTS.md`
- **Tools Inventory:** `docs/brain/TOOLS_INVENTORY_2026-05-23.md`
- **Testing Guide:** `docs/TESTING_AND_TOOLS.md`
- **Routa Documentation:** `routa-tools/README.md`
- **LangSmith Setup:** `scripts/langsmith_bridge.py`
- **MCP Configuration:** `.mcp/config.json`

---

**Last Updated:** 2026-05-23T22:41:00Z  
**Auditor:** Advanced Mode (Claude Sonnet 4.6)  
**Next Review:** After major tool updates or protocol changes