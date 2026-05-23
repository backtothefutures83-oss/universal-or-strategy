# AGENTS.md - Sovereign Agent Protocol

Welcome, Agent. You are operating within the **V12 Universal OR Strategy** repository. This environment is optimized for autonomous multi-agent development under the **Sovereign Droid Protocol (SDP)**.
## ⚠️ CRITICAL: CodeFactor Protocol
**MANDATORY READING**: Before accepting ANY automated fixes from CodeFactor or similar tools, read `docs/protocol/CODEFACTOR_PROTOCOL.md`. 

**TL;DR**: NEVER use CodeFactor's "Apply fixes" button. It caused 320 compilation errors and required emergency rollback. Manual fixes only, with build verification after every batch.


## 1. Agent Hierarchy (The Director's Gate)

- **ORCHESTRATOR (P1)**: Central Switchboard (Antigravity / Gemini CLI). Controls context and cross-agent routing.
- **ARCHITECT + ENGINEER (P3/P4/P5)   src/ tasks**: **Bob CLI** (`v12-engineer`) is the unified Architect-Engineer for all `src/` work. Bob handles design (planning), extraction, refactoring, and surgical implementation in a single Orchestrator session. No separate P3 handoff to Claude is required for `src/` tickets.
  - **Bob CLI** (`v12-engineer`): Primary. Handles design-only gates, God-function splitting, and full implementation.
  - **Codex CLI** (`codex-rescue`): Secondary. Specialist for surgical logic hardening and lock-free kernel updates when Bob delegates.
- **ARCHITECT (P3)   escalation only**: **Claude Opus 4.7** is reserved for (a) non-src architectural review, (b) $battlezip compound intelligence sessions, and (c) cross-subgraph design decisions that span >3 files outside Bob's current context. Claude remains PLAN-ONLY when invoked.
- **ADJUDICATOR (Arena AI)**: **P4 Vetting Gate**. Adversarial consensus and **PR Audit** required BEFORE surgery.
- **ENGINEER (P4/P5)   non-src tasks**: Target selection follows strict routing logic:
    - **Jules AI**: Primary non-src engineer for GitHub-based workflows.
    - **Gemini CLI** (`yolo`): Secondary non-src local engineer for tasks requiring local file access or visual context.
- **FORENSICS (P2/P6)**: Diagnosis (P2) and Adversarial Audit (P6).

## 2. Architectural Mandates (THE PLATINUM STANDARD)

- **Correctness by Construction ("Make illegal states unrepresentable")**: Structure types, enums, and data models so that it is mathematically impossible for the compiler to allow an invalid state. Do not rely on runtime if/else guards for weird edge cases   design the architecture so the edge case literally cannot exist.
- **Lock-Free Actor Pattern**: Legacy `lock(stateLock)` blocks are **STRICTLY BANNED**. All state mutations must use the FSM/Actor `Enqueue` model or atomic primitives.
- **ASCII-Only Compliance**: NEVER use Unicode, emoji, or curly quotes in C# string literals.
- **Jane Street Alignment (V12.17)**: ALL agents (Bob, Codex, Qwen, Antigravity, Jules, Rovo Dev, Cursor, etc.) MUST load and apply the ingested Jane Street Intel from `docs/intel/jane-street/` for every architectural decision.
- **Hard-Link Integrity**: Every `src/` modification MUST be followed by `powershell -File .\deploy-sync.ps1` to re-synchronize NinjaTrader hard links.
- **Routa CLI Integration**: ALL agents MUST use Routa CLI for architecture analysis, multi-file refactoring, and cross-repository coordination. See [Universal Agent Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md) for details.

## 3. Mandatory Knowledge Base Consultation

**CRITICAL PROTOCOL:** ALL agents MUST query the Jane Street Knowledge Base before ANY task involving:
- Architecture decisions
- Performance optimization
- Concurrency patterns
- Lock-free design
- State management
- ANY src/ file modification

**Command:**
```powershell
python scripts/query_kb.py "<your query>"
```

**Examples:**
```powershell
# Before implementing lock-free pattern
python scripts/query_kb.py "lock-free queue implementation"

# Before refactoring state management
python scripts/query_kb.py "actor model state mutations"

# Before performance optimization
python scripts/query_kb.py "microsecond latency patterns"
```

**Enforcement:** Agents that skip KB consultation violate V12 DNA protocol.

## 4. Routa CLI Integration (MANDATORY)

**CRITICAL RULE:** ALL agents MUST use Routa CLI for:
- Architecture analysis
- Multi-file refactoring planning
- Cross-repository coordination
- Feature tree generation
- Kanban workflow automation

**Installation:**
```powershell
# Via npm
npm install -g routa-cli

# Or via Cargo
cargo install routa-cli

# Verify installation
routa --version
```

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

## 5. LangSmith Tracing (MANDATORY for Multi-Agent Sessions)

**Configuration:** `.env` file (create from `.env.example`)

```bash
LANGSMITH_TRACING=true
LANGSMITH_API_KEY=ls__your_key_here
LANGSMITH_PROJECT=V12-Universal-OR-Strategy
```

**Agent Handoff Protocol:**
```powershell
# When handing off to another agent
python scripts/nexus_relay.py <agent_name> "<instructions>"

# This emits a LangSmith trace linking the handoff
```

**Verification:**
```powershell
python scripts/langsmith_bridge.py --test
# Expected: "[+] Trace emitted successfully."
```

## 6. PR Separation Protocol (MANDATORY)

**CRITICAL RULE:** NEVER mix src/ and non-src/ files in the same PR.

**Two-PR Model:**
1. **src/ PR:** Production code changes only
   - Full bot audit (CodeRabbit, Codacy, Semgrep)
   - CI verification required
   - `/pr-loop` to 85+ PHS
   
2. **non-src/ PR:** Documentation, tests, workflows, scripts
   - Lightweight review or direct merge
   - No bot audit required
   - Fast-track merge

**Verification Command:**
```powershell
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <PR_NUMBER>
```

**Enforcement:** Any PR mixing src/ and non-src/ will be rejected.

## 7. Standard Commands

- **Build & Sync** (Build Pillar): `powershell -File .\scripts\build_readiness.ps1`
- **Lint Audit** (Style Pillar): `powershell -File .\scripts\lint.ps1`
- **Stress Test** (Testing Pillar): `powershell -File .\scripts\test_stress.ps1`
- **Sovereign Audit**: `droid /review` (Focus on P0-P3 severity findings).
- **Readiness Check**: `droid /readiness-report` (Maintain Level 2+).
- **Forensic Scan**: `grep -r "lock(" src/` (Zero-match requirement).
- **Jane Street KB Query**: `& "%USERPROFILE%\AppData\Local\Programs\Python\Python312\python.exe" scripts/query_kb.py "<term>"` (Retrieves HFT and high-performance system guidelines from the Firestore knowledge base).

## 4. Communication & Context

- **Active Task**: Always check `docs/brain/task.md` before initiating work.
- **Handoffs**: Use the `docs/brain/nexus_a2a.json` via the **Nexus Bridge** for inter-agent state synchronization.
- **Expert Knowledge Base (RAG)**: Before starting complex design, refactoring, or performance engineering tasks, query the Jane Street Knowledge Base using `scripts/query_kb.py` to retrieve verified microsecond-latency patterns and testing standards.

## 5. Karpathy Behavioral Protocols (LLM Coding Hygiene)

Derived from Andrej Karpathy's observations on LLM coding pitfalls.
These principles apply to all agents including Gemini CLI as Orchestrator.
Bias toward caution over speed. For trivial tasks, use judgment.

### Think Before Coding

- State assumptions explicitly. If uncertain, ASK -- do not silently pick an interpretation.
- If multiple interpretations exist, surface them to the Director before proceeding.
- If a simpler approach exists, say so. Push back when warranted.

### Simplicity First

- Minimum code that solves the problem. Nothing speculative.
- No features beyond what was asked. No abstractions for single-use code.
- If 200 lines could be 50, rewrite it before submission.

### Surgical Changes

- Touch only what you must. Clean up only your own mess.
- Do NOT "improve" adjacent code, comments, or formatting.
- **WHITESPACE MUTATION BANNED**: Never mutate whitespace, line endings, or indentation across files. This creates bloated diffs that obscure logic and break CI limits.
- **STRICT DIFF LIMIT**: Pull Request diffs MUST target less than 10,000 characters of source code changes (in `src/`). Split larger epics into smaller, focused PRs.
- **DIFF PRE-CHECK**: Before pushing, run `powershell -File .\deploy-sync.ps1`. If the **DIFF GUARD** fails, you must isolate the logic changes and revert whitespace/artifact bloat.
- If unrelated dead code is noticed, REPORT it -- do not act on it.
- Every changed line must trace directly to the Mission Brief.

### Goal-Driven Execution

- State verify criteria before each implementation stage:
  1. [Step] -> verify: [check]
  2. [Step] -> verify: [check]
- Strong success criteria let you loop independently. "Make it work" is not a criterion.

## 6. Autonomous Skill Creation & Self-Improvement (MANDATORY PILLAR)

**All agents MUST perform a post-use audit after every skill or tool use:**
1. Check if any instruction was ambiguous or produced an unexpected result.
2. Update the corresponding `SKILL.md` or persistent rule file if a gap or quirk is found.
3. State `skill(name): no gaps identified` if no gap is found.
4. Skipping the post-use audit is a protocol violation.

## Graphify Protocols (Universal Knowledge Layer)

- **Check First**: Before deep architectural exploration, always check for `graphify-out/graph.json` or `graphify-out/GRAPH_REPORT.md`.
- **Update**: Use `graphify update .` to refresh the repo knowledge graph after major structural changes.
- **Efficiency**: Use the graph to navigate codebase relationships with 71x fewer tokens than raw file reading.

## Code Exploration Policy

Always use jCodemunch-MCP tools for code navigation. Never fall back to Read, Grep, Glob, or Bash for code exploration.
**Exception:** Use `Read` when you need to edit a file     the agent harness requires a `Read` before `Edit`/`Write` will succeed. Use jCodemunch tools to *find and understand* code, then `Read` only the specific file you're about to modify.

**Start any session:**
1. `resolve_repo { "path": "." }`     confirm the project is indexed. If not: `index_folder { "path": "." }`
2. `suggest_queries`     when the repo is unfamiliar

**Finding code:**
- symbol by name     `search_symbols` (add `kind=`, `language=`, `file_pattern=`, `decorator=` to narrow)
- decorator-aware queries     `search_symbols(decorator="X")` to find symbols with a specific decorator (e.g. `@property`, `@route`); combine with set-difference to find symbols *lacking* a decorator (e.g. "which endpoints lack CSRF protection?")
- string, comment, config value     `search_text` (supports regex, `context_lines`)
- database columns (dbt/SQLMesh)     `search_columns`

**Reading code:**
- before opening any file     `get_file_outline` first
- one or more symbols     `get_symbol_source` (single ID     flat object; array     batch)
- symbol + its imports     `get_context_bundle`
- specific line range only     `get_file_content` (last resort)

**Repo structure:**
- `get_repo_outline`     dirs, languages, symbol counts
- `get_file_tree`     file layout, filter with `path_prefix`

**Relationships & impact:**
- what imports this file     `find_importers`
- where is this name used     `find_references`
- is this identifier used anywhere     `check_references`
- file dependency graph     `get_dependency_graph`
- what breaks if I change X     `get_blast_radius`
- what symbols actually changed since last commit     `get_changed_symbols`
- find unreachable/dead code     `find_dead_code`
- class hierarchy     `get_class_hierarchy`

## Session-Aware Routing

**Opening move for any task:**
1. `plan_turn { "repo": "...", "query": "your task description", "model": "<your-model-id>" }`     get confidence + recommended files; the `model` parameter narrows the exposed tool list to match your capabilities at zero extra requests.
2. Obey the confidence level:
   - `high`     go directly to recommended symbols, max 2 supplementary reads
   - `medium`     explore recommended files, max 5 supplementary reads
   - `low`     the feature likely doesn't exist. Report the gap to the user. Do NOT search further hoping to find it.

**Interpreting search results:**
- If `search_symbols` returns `negative_evidence` with `verdict: "no_implementation_found"`:
  - Do NOT re-search with different terms hoping to find it
  - Do NOT assume a related file (e.g. auth middleware) implements the missing feature (e.g. CSRF)
  - DO report: "No existing implementation found for X. This would need to be created."
  - DO check `related_existing` files     they show what's nearby, not what exists
- If `verdict: "low_confidence_matches"`: examine the matches critically before assuming they implement the feature

**After editing files:**
- If PostToolUse hooks are installed (Claude Code only), edited files are auto-reindexed
- Otherwise, call `register_edit` with edited file paths to invalidate caches and keep the index fresh
- For bulk edits (5+ files), always use `register_edit` with all paths to batch-invalidate

**Token efficiency:**
- If `_meta` contains `budget_warning`: stop exploring and work with what you have
- If `auto_compacted: true` appears: results were automatically compressed due to turn budget
- Use `get_session_context` to check what you've already read     avoid re-reading the same files

## Model-Driven Tool Tiering

Your jcodemunch-mcp server narrows the exposed tool list based on the model you are running as. To avoid wasting requests on primitives when a composite would do, always include `model="<your-model-id>"` in your opening `plan_turn` call.

Replace `<your-model-id>` with your active model:
- Claude Opus variants     `claude-opus-4-7` (or any `claude-opus-*`)
- Claude Sonnet variants     `claude-sonnet-4-6`
- Claude Haiku variants     `claude-haiku-4-5`
- GPT-4o / GPT-5 / o1 / Llama     use the model id as printed by your runner

The `model=` parameter rides on the existing `plan_turn` call     it does **not** add a separate tool invocation. If `plan_turn` is not appropriate for a non-code task, call `announce_model(model="...")` once instead.

## 7. Phase 6 Recursive Protocol (V15.4)

This protocol governs the **SIMA Subgraph Extraction** and all complex refactoring missions.

### Stage 0: Forensic Intake (Orchestrator)
- **Tool**: `jcodemunch-mcp` + `graphify`
- **Goal**: Generate "Platinum Standard" prompts for the ARCHITECT.
- **Output**: Forensic report in `docs/brain/forensics_report.md`.

### Stage 1: Vision/Spec (Architect)
- **Agent**: Bob CLI (`v12-engineer`)
- **Goal**: Dialogue with Director to generate `mini-spec.md`.
- **Constraint**: Must verify logic against V12 DNA.

### Stage 2: Arch Planning (Architect)
- **Agent**: Bob CLI (`v12-engineer`)
- **Goal**: Generate `implementation_plan.md` + Mermaid diagrams.
- **Audit**: Triple-Agent UltraThink audit required.

### Stage 3: DNA & PR Audit (Adjudicator)
- **Agent**: Arena AI (Red Team)
- **Goal**: Verify plan and PR health against V12 constraints (No locks, Atomic, ASCII-only).
- **Gate**: PASS/FAIL. Fail triggers Stage 2 rework.

### Stage 4: Recursive Execution (Engineer Selection)
- **Action**: Hand off to the selected Engineer via the Bob CLI Orchestrator session.
- **Targets**: 
  - **Bob CLI** for extraction/splitting (P5 Surgical).
  - **Codex CLI** for logic hardening (P5 Logic).
  - **Gemini CLI** for **Utility/Non-src** tasks (P5 Utility). Always use Gemini for model-agnostic tasks to conserve specialized tokens.
- **Safety**: Mandatory checkpointing enabled.

### Stage 5: Verification/Review (Forensics)
- **Agent**: Bob CLI (verify cycle) + Orchestrator
- **Goal**: Compare implementation against `implementation_plan.md`.
- **Loop**: Automated "Fix-all" loop if logic drifts.

### Stage 6: Sign-off (Director)
- **Action**: `powershell -File .\deploy-sync.ps1`
- **Final Test**: F5 in NinjaTrader + BUILD_TAG verification.

## 8. IBM Bob Shell Integration

- **Binary**: `bob` (via alias or path)
- **Mode**: `v12-engineer` (custom mode defined in `.bob/custom_modes.yaml`)
- **Rules**: Enforced via `.bob/rules-v12-engineer/`

## 9. Codacy Quality Integration

**Purpose**: Automated code quality tracking and technical debt management aligned with V12 DNA principles.

### Configuration Overview

The repository uses `.codacy.yml` to enforce V12 architectural standards:

**Key Settings**:
- **Complexity Threshold**: 15 (Jane Street alignment - keep functions simple)
- **Roslyn Analyzer**: Enabled for C# code quality checks
- **Duplication Detection**: Enabled (excludes tests/benchmarks)
- **Excluded Paths**: docs/, scripts/, .github/, conductor/, Traycerrefactor/, and tool directories

### Complexity Threshold Rationale

**Why 15?** (Jane Street Alignment)
- Jane Street's HFT systems prioritize **cognitive simplicity** over clever abstractions
- Functions with cyclomatic complexity >15 are harder to:
  - Reason about under microsecond latency constraints
  - Test exhaustively (exponential path growth)
  - Audit for race conditions in lock-free code
- V12 DNA mandates: "Make illegal states unrepresentable" - this requires simple, verifiable logic

**Enforcement**:
- Codacy flags functions exceeding threshold 15
- Refactor into smaller, single-purpose functions
- Use the Actor/FSM pattern to decompose complex state machines

### Validating Configuration

**After pushing `.codacy.yml` to GitHub**:

1. **Check Codacy Dashboard**:
   - Navigate to: https://app.codacy.com/gh/mdasdispatch-hash/universal-or-strategy/settings
   - Verify "Configuration file" shows `.codacy.yml` detected
   - Confirm complexity threshold displays as 15

2. **Verify Exclusions**:
   - Go to "Ignored Files" tab
   - Confirm docs/, scripts/, .github/ are excluded
   - Verify tests/ and benchmarks/ excluded from duplication checks

3. **Test on PR**:
   - Create a test PR with a function exceeding complexity 15
   - Verify Codacy flags it as "Code complexity" issue
   - Confirm PR shows "Up to quality standards" if no new issues

### Current Baseline (2026-05-22)

- **Total Issues**: 3,100 (technical debt)
- **Grade**: B
- **Coverage**: 0% (coverage integration pending)
- **Complexity**: 32% of files (31/207 exceed threshold)

**Strategy**: Boy Scout Rule - fix issues in files you touch, chip away at debt incrementally.

### Integration with V12 Workflows

**Before Surgery** (P4/P5 tasks):
- Check Codacy dashboard for file-specific issues
- Prioritize: Security (29) > Error-prone (1k) > Complexity (288) > Style (1k)

**After Surgery**:
- Verify PR shows "Up to quality standards" (no new issues)
- If new issues appear: fix before merge (quality gate enforcement)

**Debt Reduction**:
- Dedicate 20% of sprint capacity to debt reduction
- Target high-complexity files first (V12_002.DrawingHelpers.cs, V12_002.Atm.cs)
- Use `scripts/complexity_audit.py` for local pre-checks

### Commands

- **Local Complexity Audit**: `python scripts/complexity_audit.py`
- **View Codacy Dashboard**: https://app.codacy.com/gh/mdasdispatch-hash/universal-or-strategy/dashboard
- **Check PR Quality**: Codacy bot comments on every PR with issue delta

- **Checkpointing**: Always enabled via `.bob/settings.json`. Restore via `/restore`.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files

## 18. Available Tools

### Code Navigation (jCodemunch MCP)
- `resolve_repo`, `index_folder`
- `search_symbols`, `search_text`, `search_columns`
- `get_file_outline`, `get_symbol_source`, `get_context_bundle`
- `find_importers`, `find_references`, `check_references`
- `get_dependency_graph`, `get_blast_radius`, `get_changed_symbols`
- `find_dead_code`, `get_class_hierarchy`
- `plan_turn`, `get_session_context`, `announce_model`

### Architecture & Planning (Routa CLI)
- `routa -p "<query>"` - Architecture analysis
- `routa kanban card create` - Kanban workflow
- `routa team run` - Multi-specialist coordination
- `routa fitness fluency` - Harness fluency assessment

### Knowledge Graph (graphify)
- `graphify update .`
- `graphify-out/GRAPH_REPORT.md`
- 71x token efficiency vs raw file reading

### Knowledge Base (RAG)
- `python scripts/query_kb.py "<query>"`
- Firestore Jane Street intel
- HFT patterns, lock-free designs, microsecond-latency optimizations

### Testing & Quality
- **Unit Tests**: `dotnet test tests/V12_Performance.Tests/`
- **Benchmarks**: `dotnet run --project benchmarks`
- **AMAL Harness**: `python scripts/amal_harness_v26.py`
- **Semgrep**: `powershell -File .\scripts\run_semgrep.ps1`
- **Complexity Audit**: `python scripts/complexity_audit.py`
- **Dead Code Scan**: `python scripts/dead_code_scan.py`

### Build & Deployment
- **Build**: `dotnet build .\Linting.csproj`
- **Deploy Sync**: `powershell -File .\deploy-sync.ps1` (MANDATORY after src/ edits)
- **Format**: `powershell -File .\scripts\format_all_csharp.ps1`
- **Build Readiness**: `powershell -File .\scripts\build_readiness.ps1`

### PR Workflow
- **PR Hygiene**: `powershell -File .\scripts\verify_pr_hygiene.ps1`
- **PR Separation**: `powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>`
- **PR Forensics**: `powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <N>`
- **CI Logs**: `powershell -File .\scripts\extract_ci_logs.ps1 -PrNumber <N>`
- **Fleet Score**: `powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber <N>`
- **CodeRabbit CLI**: `coderabbit review` (local AI review)

### GitHub Apps (Active on PRs)
- **CodeRabbit AI**: V12 DNA alignment, CAS bug detection
- **Sourcery AI**: Architectural feedback, JIT analysis
- **Codacy**: Static analysis, complexity metrics
- **cubic-dev-ai**: Jane Street alignment, atomic operation audits
- **Amazon Q Developer**: Struct semantics, atomic operations
- **Semgrep**: V12 DNA pattern matching
- **CodeQL**: Security scanning
- **Snyk**: Dependency vulnerabilities
- **Gitleaks**: Secret detection
- **SonarCloud**: Code quality platform

## 19. Tool Discovery Protocol (MANDATORY)

**CRITICAL:** ALL agents MUST discover and verify tools at session start.

### Session Initialization

```powershell
# Step 1: Discover all installed tools
powershell -File .\scripts\discover_tools.ps1

# Step 2: Load tool manifest
$tools = Get-Content "docs/brain/session_tools.json" | ConvertFrom-Json

# Step 3: Verify critical tools
# - jcodemunch-mcp, graphify, query_kb.py, Routa CLI, deploy-sync.ps1
```

### Tool Categories

- **MCP Servers:** jcodemunch, lsp-mcp, greptile (via Quarkus bridge), sequential-thinking
- **Knowledge:** graphify, query_kb.py, Routa CLI
- **Build:** dotnet, MSBuild, deploy-sync.ps1
- **Testing:** xUnit, BenchmarkDotNet, AMAL Harness
- **Quality:** Semgrep, CodeRabbit CLI, Codacy
- **Git:** gh, git, PR scripts
- **Specialized:** Routa, complexity_audit.py, dead_code_scan.py

**See:** [Tool Discovery Protocol](docs/protocol/TOOL_DISCOVERY_PROTOCOL.md)

### Greptile HTTP Bridge

Greptile API uses HTTP/REST, but Bob CLI requires SSE (Server-Sent Events) for MCP. Use the Quarkus MCP Server HTTP bridge to convert Greptile HTTP → Bob SSE.

**See:** [Greptile HTTP Bridge Setup](docs/setup/GREPTILE_HTTP_BRIDGE.md)

## 20. Universal Agent Protocol

**For complete tool integration, workflow protocols, and enforcement rules, see:**

📘 **[Universal Agent Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)**

This comprehensive document covers:
- Mandatory tool stack (Routa, jCodemunch, Jane Street KB, LangSmith)
- Quality gates (Semgrep, CodeRabbit CLI, build readiness)
- Testing tools (xUnit, BenchmarkDotNet, AMAL Harness)
- LangSmith tracing configuration and continuity
- PR separation protocol enforcement
- MCP configuration (centralized)
- Prompt caching configuration
- Workflow integration (pre-push checklist, PR loop, epic run)
- Tool access matrix by agent and task type
- Enforcement & verification procedures
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)