# Bob CLI Tools Reference

## Overview

This document lists all tools available to Bob CLI (`v12-engineer` mode) for the V12 Universal OR Strategy repository.

## Tool Categories

### 1. Code Navigation (jCodemunch MCP)

**Purpose:** Advanced code intelligence and architecture analysis

**Available Tools:**
- `resolve_repo` - Resolve filesystem path to indexed repo identifier
- `index_folder` - Index a local folder for code navigation
- `search_symbols` - Search for symbols (functions, classes, methods) with semantic understanding
- `search_text` - Full-text search across indexed files (supports regex)
- `search_columns` - Search column metadata across indexed models (dbt, SQLMesh)
- `get_file_outline` - Get all symbols in a file with signatures and summaries
- `get_symbol_source` - Get full source code for one or more symbols
- `get_context_bundle` - Get symbol source + imports in one call
- `get_file_content` - Get cached source for a file (with optional line range)
- `find_importers` - Find all files that import a given file
- `find_references` - Find all files that import or reference an identifier
- `check_references` - Check if an identifier is referenced anywhere
- `get_dependency_graph` - Get file-level dependency graph
- `get_blast_radius` - Find all files affected by changing a symbol
- `get_changed_symbols` - Map git diff to affected symbols
- `find_dead_code` - Find unreachable files and symbols
- `get_class_hierarchy` - Get inheritance hierarchy for a class
- `get_related_symbols` - Find symbols related to a given symbol
- `plan_turn` - Plan next turn by analyzing query against codebase
- `get_session_context` - Get current session context (files accessed, searches performed)
- `get_session_snapshot` - Get compact session snapshot for context continuity

**Usage Examples:**
```bash
# Index the repository
jcodemunch index_folder --path .

# Search for FSM-related symbols
jcodemunch search_symbols --repo universal-or-strategy --query "FSM" --kind class

# Get blast radius before refactoring
jcodemunch get_blast_radius --repo universal-or-strategy --symbol "ProcessBracketEvent"

# Plan next turn
jcodemunch plan_turn --repo universal-or-strategy --query "Implement RMA proximity monitoring" --model "claude-opus-4-7"
```

### 2. Architecture & Planning (Routa CLI)

**Purpose:** Multi-agent coordination, architecture analysis, and workflow automation

**Available Tools:**
- `routa -p "<query>"` - Architecture analysis and code navigation
- `routa kanban card create` - Create Kanban cards for task tracking
- `routa kanban card move` - Move cards between columns
- `routa team run` - Multi-specialist coordination
- `routa fitness fluency` - Assess harness fluency

**Usage Examples:**
```bash
# Analyze SIMA subgraph architecture
routa -p "Analyze the architecture of the SIMA subgraph"

# Plan RMA proximity monitoring implementation
routa -p "Plan the implementation of RMA proximity monitoring"

# Create Kanban card
routa kanban card create --title "Implement feature X" --workspace-id default

# Move card to in-progress
routa kanban card move --card-id <id> --target-column-id in-progress
```

### 3. Knowledge Graph (graphify)

**Purpose:** AST-based knowledge graph for efficient code navigation

**Available Tools:**
- `graphify update .` - Update knowledge graph after code changes
- `graphify-out/GRAPH_REPORT.md` - Read graph report for god nodes and community structure
- `graphify-out/wiki/index.md` - Navigate wiki instead of raw files

**Usage Examples:**
```bash
# Update graph after modifying code
graphify update .

# Read graph report
cat graphify-out/GRAPH_REPORT.md
```

**Benefits:**
- 71x token efficiency vs raw file reading
- Identifies god nodes and architectural hotspots
- Community detection for module boundaries

### 4. Knowledge Base (RAG)

**Purpose:** Query Jane Street HFT patterns and V12 DNA principles

**Available Tools:**
- `python scripts/query_kb.py "<query>"` - Query Firestore knowledge base

**Usage Examples:**
```bash
# Query lock-free patterns
python scripts/query_kb.py "lock-free queue implementation"

# Query actor model patterns
python scripts/query_kb.py "actor model state mutations"

# Query microsecond latency patterns
python scripts/query_kb.py "microsecond latency patterns"
```

**Knowledge Domains:**
- Lock-free concurrency patterns
- Actor model implementations
- Microsecond-latency optimizations
- HFT system design principles
- Jane Street coding standards

### 5. Testing & Quality

**Available Tools:**
- `dotnet test tests/V12_Performance.Tests/` - Run unit tests
- `dotnet run --project benchmarks` - Run performance benchmarks
- `python scripts/amal_harness_v26.py` - Run AMAL harness (adversarial testing)
- `powershell -File .\scripts\run_semgrep.ps1` - Run Semgrep V12 DNA scan
- `python scripts/complexity_audit.py` - Audit cyclomatic complexity
- `python scripts/dead_code_scan.py` - Scan for dead code
- `coderabbit review` - Local AI code review

**Usage Examples:**
```bash
# Run all unit tests
dotnet test tests/V12_Performance.Tests/

# Run specific test class
dotnet test tests/V12_Performance.Tests/ --filter "FullyQualifiedName~FSMActorTests"

# Run benchmarks
dotnet run --project benchmarks --configuration Release

# Run AMAL harness
python scripts/amal_harness_v26.py

# Run Semgrep
powershell -File .\scripts\run_semgrep.ps1

# Complexity audit
python scripts/complexity_audit.py

# CodeRabbit local review
coderabbit review
```

### 6. Build & Deployment

**Available Tools:**
- `dotnet build .\Linting.csproj` - Build the project
- `powershell -File .\deploy-sync.ps1` - Sync NinjaTrader hard links (MANDATORY after src/ edits)
- `powershell -File .\scripts\format_all_csharp.ps1` - Format all C# files
- `powershell -File .\scripts\build_readiness.ps1` - Check build readiness

**Usage Examples:**
```bash
# Build project
dotnet build .\Linting.csproj

# Sync hard links (MANDATORY after src/ changes)
powershell -File .\deploy-sync.ps1

# Format all C# files
powershell -File .\scripts\format_all_csharp.ps1

# Check build readiness
powershell -File .\scripts\build_readiness.ps1
```

**Critical Rule:** ALWAYS run `deploy-sync.ps1` after modifying any file in `src/`. This syncs hard links to NinjaTrader.

### 7. PR Workflow

**Available Tools:**
- `powershell -File .\scripts\verify_pr_hygiene.ps1` - Verify PR hygiene (rebase, diff size)
- `powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>` - Verify src/ and non-src/ separation
- `powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <N>` - Extract bot findings
- `powershell -File .\scripts\extract_ci_logs.ps1 -PrNumber <N>` - Extract CI failure logs
- `powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber <N>` - Calculate Project Health Score
- `coderabbit review` - Local AI code review

**Usage Examples:**
```bash
# Verify PR hygiene before push
powershell -File .\scripts\verify_pr_hygiene.ps1

# Verify PR separation
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber 8

# Extract bot forensics
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber 8

# Extract CI logs
powershell -File .\scripts\extract_ci_logs.ps1 -PrNumber 8

# Calculate PHS
powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber 8

# Local review before push
coderabbit review
```

### 8. LangSmith Tracing

**Purpose:** Agent execution tracing and context propagation

**Available Tools:**
- `python scripts/nexus_relay.py <agent_name> "<instructions>"` - Hand off to another agent with trace context

**Usage Examples:**
```bash
# Hand off to Codex CLI with trace context
python scripts/nexus_relay.py codex "Harden FSM state transitions"

# Hand off to Gemini CLI
python scripts/nexus_relay.py gemini "Update documentation"
```

**Configuration:**
```bash
# .env file
LANGSMITH_TRACING=true
LANGSMITH_API_KEY=ls__your_key_here
LANGSMITH_PROJECT=V12-Universal-OR-Strategy
```

## Tool Access Matrix

| Tool Category | Bob CLI | Cursor IDE | Gemini CLI | Jules AI |
|--------------|---------|------------|------------|----------|
| jCodemunch MCP | ✅ Full | ✅ Full | ✅ Full | ❌ No |
| Routa CLI | ✅ Full | ❌ No | ✅ Full | ❌ No |
| graphify | ✅ Full | ✅ Read | ✅ Full | ❌ No |
| Jane Street KB | ✅ Full | ❌ No | ✅ Full | ❌ No |
| Testing Tools | ✅ Full | ✅ Full | ✅ Full | ✅ Via GH Actions |
| Build Tools | ✅ Full | ✅ Full | ✅ Full | ❌ No |
| PR Workflow | ✅ Full | ✅ Full | ✅ Full | ✅ Via GH Actions |
| LangSmith | ✅ Full | ❌ No | ✅ Full | ❌ No |

## Mandatory Tool Usage

### Before ANY src/ Task

1. **Query Jane Street KB:**
   ```bash
   python scripts/query_kb.py "<task description>"
   ```

2. **Plan Turn:**
   ```bash
   jcodemunch plan_turn --repo universal-or-strategy --query "<task>" --model "claude-opus-4-7"
   ```

3. **Check graphify:**
   ```bash
   cat graphify-out/GRAPH_REPORT.md
   ```

### After ANY src/ Edit

1. **Sync Hard Links:**
   ```bash
   powershell -File .\deploy-sync.ps1
   ```

2. **Update graphify:**
   ```bash
   graphify update .
   ```

3. **Register Edit (if using jCodemunch):**
   ```bash
   jcodemunch register_edit --repo universal-or-strategy --file-paths "src/V12_002.cs"
   ```

### Before ANY Push

1. **Verify PR Hygiene:**
   ```bash
   powershell -File .\scripts\verify_pr_hygiene.ps1
   ```

2. **Verify PR Separation:**
   ```bash
   powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>
   ```

3. **Run Semgrep:**
   ```bash
   powershell -File .\scripts\run_semgrep.ps1
   ```

4. **Local CodeRabbit Review:**
   ```bash
   coderabbit review
   ```

## Tool Efficiency Guidelines

### Token Optimization

1. **Use jCodemunch over Read/Grep:**
   - `search_symbols` is 71x more efficient than reading files
   - `get_file_outline` shows structure without full content
   - `get_context_bundle` gets symbol + imports in one call

2. **Use graphify for Architecture:**
   - Read `GRAPH_REPORT.md` instead of exploring files
   - Navigate `graphify-out/wiki/` for structured knowledge

3. **Use Routa for Planning:**
   - `routa -p` generates architecture analysis
   - Avoids manual file exploration

### Workflow Optimization

1. **Batch Operations:**
   - Use `get_symbol_source` with multiple symbol IDs
   - Use `find_importers` with multiple file paths
   - Use `register_edit` with all edited files at once

2. **Session Context:**
   - Use `get_session_context` to avoid re-reading files
   - Use `get_session_snapshot` for context continuity

3. **Prompt Caching:**
   - Protocol docs are automatically cached
   - 90% cost reduction on repeated context

## Related Documentation

- [Universal Agent Protocol](../docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)
- [MCP Configuration](../docs/setup/MCP_CONFIGURATION.md)
- [Prompt Caching](../docs/setup/PROMPT_CACHING.md)
- [Bob CLI Documentation](BOB.md)

## Version History

- **v1.0.0** (2026-05-23): Initial Bob CLI tools reference