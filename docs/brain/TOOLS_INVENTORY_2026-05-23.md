# Tools & Extensions Inventory
**Date:** 2026-05-23  
**Project:** V12 Universal OR Strategy  
**Scope:** Bob IDE, VSCode, CLI Tools, MCP Servers, Build Tools

---

## Executive Summary

This inventory documents all installed tools, extensions, programs, and binaries in the V12 project environment. Organized by category for quick reference.

---

## 1. Core Development Tools

### 1.1 IDEs & Editors

| Tool | Version | Location | Purpose |
|------|---------|----------|---------|
| **Bob CLI** | Unknown | System PATH | Primary AI-assisted development IDE |
| **VSCode** | Latest | System | Secondary editor, formatting |
| **Cursor** | Latest | System | AI-assisted coding (backup) |

### 1.2 Language Runtimes

| Runtime | Version | Location | Purpose |
|---------|---------|----------|---------|
| **.NET SDK** | 6.0+ | System | C# compilation, NinjaTrader 8 |
| **.NET Framework** | 4.8 | System | NinjaTrader 8 runtime |
| **Python** | 3.12 | `%USERPROFILE%\AppData\Local\Programs\Python\Python312\` | Scripts, KB queries |
| **Node.js** | Latest | System | Routa-tools, npm packages |
| **Rust** | Latest | System | Routa CLI compilation |

---

## 2. MCP Servers (Model Context Protocol)

### 2.1 Configured Servers

**Configuration File:** `.mcp.json`

| Server | Type | Command | Purpose |
|--------|------|---------|---------|
| **jCodemunch** | stdio | `jcodemunch-mcp.exe` | Code navigation, symbol search, dependency analysis |

### 2.2 jCodemunch Tools (30+ Available)

**Categories:**
- **Finding:** `search_symbols`, `search_text`, `search_columns`
- **Reading:** `get_file_outline`, `get_symbol_source`, `get_context_bundle`, `get_file_content`
- **Structure:** `get_repo_outline`, `get_file_tree`
- **Relationships:** `find_importers`, `find_references`, `check_references`, `get_dependency_graph`, `get_blast_radius`, `get_changed_symbols`, `find_dead_code`, `get_class_hierarchy`
- **Session:** `plan_turn`, `get_session_context`, `get_session_snapshot`, `announce_model`

### 2.3 Identified But Not Configured

| Extension | ID | Status | Purpose |
|-----------|-----|--------|---------|
| **LSP MCP** | `cjl-lsp-mcp` | Not configured | Language Server Protocol via MCP |

---

## 3. Knowledge & Analysis Tools

### 3.1 RAG/Knowledge Base

| Tool | Type | Location | Purpose |
|------|------|----------|---------|
| **Firebase/Firestore** | Cloud DB | `v12-morpheus` project | Jane Street knowledge base |
| **query_kb.py** | Python script | `scripts/query_kb.py` | KB query interface |
| **firebase-credentials.json** | Config | Root directory | Firestore authentication |

**Collection:** `jane_street_knowledge_base`  
**Query Command:** `python scripts/query_kb.py "<term>"`

### 3.2 Code Analysis

| Tool | Location | Purpose |
|------|----------|---------|
| **graphify** | System PATH | Knowledge graph generation (71x token efficiency) |
| **Semgrep** | `scripts/run_semgrep.ps1` | V12 DNA pattern matching |
| **complexity_audit.py** | `scripts/` | Cyclomatic complexity analysis |

**Graphify Output:** `graphify-out/`
- `graph.json` - Full knowledge graph
- `GRAPH_REPORT.md` - God nodes, community structure
- `wiki/index.md` - Navigable wiki

---

## 4. Build & Deployment Tools

### 4.1 Build System

| Tool | Purpose | Command |
|------|---------|---------|
| **MSBuild** | C# compilation | `dotnet build` |
| **Linting.csproj** | Lint project | `dotnet build .\Linting.csproj` |
| **Testing.csproj** | Test project | `dotnet test .\Testing.csproj` |
| **Benchmarks** | Performance testing | `dotnet run --project benchmarks` |

### 4.2 Deployment Scripts

| Script | Purpose |
|--------|---------|
| `deploy-sync.ps1` | Sync src/ to NinjaTrader hard links (MANDATORY after src/ edits) |
| `build_readiness.ps1` | Build pillar verification |
| `verify_pr_hygiene.ps1` | Pre-push hygiene check |
| `format_all_csharp.ps1` | CSharpier formatting |

### 4.3 Compiled Binaries

**Benchmarks:**
- `benchmarks/bin/Debug/net6.0/V12_Performance.Benchmarks.exe`
- `benchmarks/bin/Debug/net6.0/testhost.exe`

**Tests:**
- `tests/bin/T04_SnapshotPattern_ConcurrentModification_Test.exe`
- `tests/V12_Performance.Tests/bin/Debug/net6.0/testhost.exe`

**Routa Tools:**
- `routa-tools/target/release/routa.exe` (Rust CLI)
- 60+ Rust build-script executables in `routa-tools/target/release/build/`

---

## 5. Version Control & CI/CD

### 5.1 Git Tools

| Tool | Purpose |
|------|---------|
| **git** | Version control |
| **gh** | GitHub CLI |

### 5.2 GitHub Apps (Bots)

| Bot | Purpose | Configuration |
|-----|---------|---------------|
| **CodeRabbit** | AI code review, V12 DNA checks | `.coderabbit.yaml` |
| **Semgrep** | Pattern matching (local only, GitHub App pending) | `.semgrep.yml` |
| **Codacy** | Quality metrics, complexity tracking | `.codacy.yml` |
| **SonarCloud** | Static analysis | (config not found) |
| **Qodo** | Code review | (paused for user) |

---

## 6. Python Packages

### 6.1 Installed Packages (Inferred)

| Package | Purpose |
|---------|---------|
| `firebase-admin` | Firestore SDK |
| `google-cloud-firestore` | Firestore client |

**Requirements File:** Not found (packages installed globally)

---

## 7. Node.js Packages

### 7.1 Routa-Tools Dependencies

**Location:** `routa-tools/tools/office-skills/package-lock.json`

**Key Packages:**
- `@types/node` (18.19.130)
- `@xmldom/xmldom` (0.9.8)
- `commander` (13.1.0)
- `jszip` (3.10.1)
- `mathjax-full` (3.2.1)
- `pptxgenjs` (3.12.0)
- `prismjs` (1.30.0)

**Infrastructure:**
- `@esbuild/win32-x64` - esbuild.exe at `infrastructure/paperclip/node_modules/`

---

## 8. Rust Crates (Routa CLI)

### 8.1 Core Dependencies

**Build Artifacts:** `routa-tools/target/release/build/`

**Key Crates:**
- `tree-sitter` - AST parsing
- `tree-sitter-java`, `tree-sitter-rust`, `tree-sitter-typescript` - Language parsers
- `serde`, `serde_json` - Serialization
- `tokio` - Async runtime
- `axum` - Web framework
- `sqlx` - Database
- `libsqlite3-sys` - SQLite bindings
- `native-tls` - TLS support
- `zip`, `bzip2-sys`, `lzma-sys`, `zstd-sys` - Compression

---

## 9. VSCode Extensions

### 9.1 Configured Extensions

**Configuration:** `.vscode/settings.json`

| Extension | Purpose |
|-----------|---------|
| **CSharpier** | C# auto-formatting on save |
| **OmniSharp** | C# language server |
| **Snyk** | Security scanning |

**Settings:**
- Auto-format on save: Enabled for C#
- EditorConfig support: Enabled
- Roslyn analyzers: Enabled

---

## 10. Bob CLI Configuration

### 10.1 Custom Modes

**Configuration:** `.bob/custom_modes.yaml`

| Mode | Slug | Tools | Purpose |
|------|------|-------|---------|
| **V12 Epic Planner** | `v12-epic-planner` | read, edit (docs/ only), mcp | Epic planning (PLAN-ONLY) |
| **V12 Engineer** | `v12-engineer` | read, edit, command | Surgical src/ edits |
| **V12 Phase7 Lead** | `v12-phase7-lead` | read, edit, command | Concurrency engineering |

### 10.2 Custom Commands

**Location:** `.bob/commands/`

| Command | Purpose |
|---------|---------|
| `/epic-intake` | Phase 1: Scope definition |
| `/epic-plan` | Phase 2: Analysis + approach |
| `/epic-validate` | Phase 3: DNA compliance audit |
| `/epic-tickets` | Phase 4: Ticket generation |
| `/ticket` | Single ticket execution |
| `/epic-run` | YOLO-parity full orchestration |
| `/pr-loop` | PR perfection loop |

### 10.3 Custom Rules

**Location:** `.bob/rules-v12-engineer/`

| File | Purpose |
|------|---------|
| `dna.md` | V12 DNA constraints (no locks, ASCII-only, FSM patterns) |

**Location:** `.bob/rules/`

| File | Purpose |
|------|---------|
| `00-pr-hygiene.md` | PR hygiene mandate |

### 10.4 Bob Settings

**Configuration:** `.bob/settings.json`

```json
{
  "general": {
    "checkpointing": { "enabled": true }
  },
  "shell": {
    "preferredEditor": "code",
    "autoApprove": ["read_file", "list_dir", "grep_search", "apply_diff", "write_to_file", "insert_content"],
    "approvalMode": "yolo"
  },
  "tools": {
    "context7": "python scripts/context7_cli.py",
    "graphify": "graphify",
    "deploy_sync": "powershell -File .\\deploy-sync.ps1",
    "nexus_bridge": "python scripts/nexus_relay.py"
  }
}
```

---

## 11. Formatting & Linting

### 11.1 C# Formatting

| Tool | Configuration | Purpose |
|------|---------------|---------|
| **CSharpier** | `.csharpierrc.json` | C# code formatting |
| **EditorConfig** | `.editorconfig` | Cross-editor style rules |

### 11.2 Markdown Linting

| Tool | Configuration | Purpose |
|------|---------------|---------|
| **markdownlint** | `.markdownlint.json` | Markdown style enforcement |

### 11.3 Ignore Files

| File | Purpose |
|------|---------|
| `.gitignore` | Git exclusions |
| `.claudeignore` | Claude exclusions |
| `.cursorignore` | Cursor exclusions |
| `.codacyignore` | Codacy exclusions |
| `.pr-review-ignore` | PR review exclusions |

---

## 12. Security & Secrets

### 12.1 Secret Scanning

| Tool | Configuration | Purpose |
|------|---------------|---------|
| **Gitleaks** | `.gitleaks.toml` | Secret detection |
| **DeepSource** | `.deepsource.toml` | Code quality & security |

### 12.2 Credentials

| File | Purpose | Status |
|------|---------|--------|
| `firebase-credentials.json` | Firestore auth | ✅ Present |
| `.env.example` | Environment template | ✅ Present |

---

## 13. Project-Specific Tools

### 13.1 V12 Scripts

**Location:** `scripts/`

| Script | Purpose |
|--------|---------|
| `query_kb.py` | Jane Street KB queries |
| `extract_pr_forensics.ps1` | Bot findings extraction |
| `extract_ci_logs.ps1` | CI log extraction |
| `verify_pr_hygiene.ps1` | Pre-push validation |
| `run_semgrep.ps1` | Semgrep scan |
| `build_readiness.ps1` | Build verification |
| `lint.ps1` | Lint audit |
| `test_stress.ps1` | Stress testing |
| `format_all_csharp.ps1` | Bulk C# formatting |
| `calculate_fleet_score.ps1` | PHS calculation |
| `complexity_audit.py` | Complexity analysis |
| `check_ascii.py` | ASCII compliance check |

### 13.2 Extracted Tools

| Script | Source | Purpose |
|--------|--------|---------|
| `sync_to_firestore.extracted.py` | Extracted | Firestore sync (historical) |
| `query_kb.extracted.py` | Extracted | KB query (historical) |

---

## 14. Routa-Tools Ecosystem

### 14.1 Routa CLI

**Binary:** `routa-tools/target/release/routa.exe`  
**Language:** Rust  
**Purpose:** Multi-tool CLI for code analysis, office document processing, MCP server

### 14.2 Office Tools

**Location:** `routa-tools/tools/`

| Tool | Purpose |
|------|---------|
| `office-wasm-reader` | WASM-based office document reader |
| `office-skills` | Office document manipulation |
| `hook-runtime` | Runtime hooks |

### 14.3 Routa Packages

**Location:** `routa-tools/packages/`

| Package | Purpose |
|---------|---------|
| `office-render` | Office document rendering |
| `office` | Office document core |

---

## 15. Missing/Recommended Tools

### 15.1 Not Installed But Referenced

| Tool | Status | Recommendation |
|------|--------|----------------|
| **LSP MCP** (`cjl-lsp-mcp`) | Identified but not configured | Test in isolated session before adding |
| **Context7 CLI** | Referenced in `.bob/settings.json` | Verify `scripts/context7_cli.py` exists |
| **Nexus Bridge** | Referenced in `.bob/settings.json` | Verify `scripts/nexus_relay.py` exists |

### 15.2 Recommended Additions

| Tool | Purpose | Priority |
|------|---------|----------|
| **Prompt caching** | Cost savings ($438/year) | HIGH |
| **KB query caching** | Performance improvement | MEDIUM |
| **Semgrep GitHub App** | PR comment integration | MEDIUM |

---

## 16. Tool Access Matrix

### 16.1 By Agent

| Agent | Tools Available |
|-------|----------------|
| **Bob CLI (v12-engineer)** | read, edit, command, jCodemunch, graphify, deploy-sync |
| **Bob CLI (v12-epic-planner)** | read, edit (docs/ only), jCodemunch, graphify |
| **Claude (Architect)** | read, jCodemunch, graphify, browser |
| **Codex (Engineer)** | read, edit, command, jCodemunch, graphify |
| **Cursor** | read, edit, CSharpier, OmniSharp |

### 16.2 By Mode (Bob CLI)

| Mode | read | edit | command | mcp | browser |
|------|------|------|---------|-----|---------|
| **Code** | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Ask** | ✅ | ❌ | ❌ | ✅ | ✅ |
| **Plan** | ✅ | ✅ (markdown only) | ❌ | ✅ | ✅ |
| **Advanced** | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Orchestrator** | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## 17. Installation Verification Commands

### 17.1 Check Installed Tools

```powershell
# Core tools
bob --version
jcodemunch-mcp --version
graphify --version
python --version
dotnet --version
git --version
gh --version
node --version
npm --version

# Rust
cargo --version
rustc --version

# Bob CLI
bob /help

# jCodemunch
jcodemunch-mcp list-tools

# Graphify
graphify --help
```

### 17.2 Verify Configurations

```powershell
# MCP servers
cat .mcp.json

# Bob settings
cat .bob/settings.json

# VSCode extensions
code --list-extensions

# Python packages
pip list | findstr firebase

# Node packages
npm list -g --depth=0
```

---

## 18. Maintenance Notes

### 18.1 Update Procedures

**Bob CLI:**
- Check for updates: `bob /update` (if available)
- Manual: Download from official source

**jCodemunch:**
- Update: `npm update -g jcodemunch-mcp` (if npm-based)
- Or download latest `.exe`

**Graphify:**
- Update: `npm update -g graphify` (if npm-based)

**Python Packages:**
```bash
pip install --upgrade firebase-admin google-cloud-firestore
```

**Node Packages:**
```bash
cd routa-tools/tools/office-skills
npm update
```

**Rust Crates:**
```bash
cd routa-tools
cargo update
cargo build --release
```

### 18.2 Cleanup Commands

```powershell
# Clean build artifacts
dotnet clean
Remove-Item -Recurse -Force benchmarks/bin, benchmarks/obj, tests/bin, tests/obj

# Clean Rust artifacts
cd routa-tools
cargo clean

# Clean Node modules
cd routa-tools/tools/office-skills
Remove-Item -Recurse -Force node_modules
npm install
```

---

## 19. Tool Dependencies Graph

```
Bob CLI
├── jCodemunch MCP (code navigation)
├── graphify (knowledge graph)
├── Python 3.12
│   ├── firebase-admin (KB queries)
│   └── google-cloud-firestore
├── .NET SDK 6.0+
│   └── .NET Framework 4.8 (NinjaTrader)
└── Git + GitHub CLI

Routa CLI
├── Rust toolchain
├── Node.js (office tools)
└── SQLite (local DB)

VSCode
├── CSharpier extension
├── OmniSharp extension
└── Snyk extension
```

---

## 20. Quick Reference

### 20.1 Essential Commands

```powershell
# Build & Deploy
dotnet build .\Linting.csproj
powershell -File .\deploy-sync.ps1

# Knowledge Base
python scripts/query_kb.py "lock-free"

# Code Analysis
graphify update .
powershell -File .\scripts\run_semgrep.ps1

# PR Workflow
powershell -File .\scripts\verify_pr_hygiene.ps1
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber 8

# Formatting
powershell -File .\scripts\format_all_csharp.ps1
```

### 20.2 Emergency Contacts

- **Bob CLI Issues:** Check `.bob/settings.json`, restart IDE
- **jCodemunch Issues:** Verify `.mcp.json`, check PATH
- **Build Issues:** Run `deploy-sync.ps1`, verify hard links
- **KB Issues:** Check `firebase-credentials.json`, test connection

---

**Last Updated:** 2026-05-23T22:24:00Z  
**Auditor:** Advanced Mode (Claude Sonnet 4.6)  
**Next Review:** After major tool updates or new installations