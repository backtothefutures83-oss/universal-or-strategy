# Cursor IDE Tools Reference

## Overview

This document lists all tools available to Cursor IDE for the V12 Universal OR Strategy repository.

**IMPORTANT:** This file is for Cursor IDE only. Bob CLI has its own tool reference at `.bob/TOOLS.md`.

## Tool Categories

### 1. Code Navigation (jCodemunch MCP)

**Purpose:** Advanced code intelligence and architecture analysis

**Available Tools:**
- `resolve_repo` - Resolve filesystem path to indexed repo identifier
- `index_folder` - Index a local folder for code navigation
- `search_symbols` - Search for symbols with semantic understanding
- `search_text` - Full-text search across indexed files
- `get_file_outline` - Get all symbols in a file
- `get_symbol_source` - Get full source code for symbols
- `get_context_bundle` - Get symbol source + imports
- `find_importers` - Find files that import a given file
- `find_references` - Find references to an identifier
- `get_dependency_graph` - Get file-level dependency graph
- `get_blast_radius` - Find files affected by changing a symbol
- `find_dead_code` - Find unreachable code

**Usage in Cursor:**
- Use Cursor's Command Palette (Ctrl+Shift+P)
- Type "MCP: jCodemunch" to access tools
- Or use Cursor's AI chat with "@jcodemunch" prefix

**Examples:**
```
@jcodemunch search symbols for "FSM"
@jcodemunch get blast radius for "ProcessBracketEvent"
@jcodemunch find dead code
```

### 2. Knowledge Graph (graphify)

**Purpose:** AST-based knowledge graph for efficient code navigation

**Available Tools:**
- Read `graphify-out/GRAPH_REPORT.md` - God nodes and community structure
- Read `graphify-out/wiki/index.md` - Structured wiki navigation

**Usage in Cursor:**
- Open files directly in Cursor
- Use Cursor's file search (Ctrl+P) to navigate wiki
- Reference in AI chat: "@graphify-out/GRAPH_REPORT.md"

**Benefits:**
- 71x token efficiency vs raw file reading
- Identifies architectural hotspots
- Community detection for module boundaries

### 3. Testing & Quality

**Available Tools:**
- `dotnet test` - Run unit tests
- `dotnet run --project benchmarks` - Run benchmarks
- Cursor's built-in test runner
- Cursor's built-in debugger

**Usage in Cursor:**
- Use Cursor's Test Explorer (Ctrl+Shift+T)
- Run tests from code lens
- Debug tests with breakpoints

**Examples:**
```bash
# Run all tests
dotnet test tests/V12_Performance.Tests/

# Run specific test
dotnet test --filter "FullyQualifiedName~FSMActorTests"
```

### 4. Build & Deployment

**Available Tools:**
- `dotnet build` - Build the project
- `powershell -File .\deploy-sync.ps1` - Sync NinjaTrader hard links
- `powershell -File .\scripts\format_all_csharp.ps1` - Format C# files

**Usage in Cursor:**
- Use Cursor's integrated terminal (Ctrl+`)
- Run commands directly
- Use Cursor's build tasks (Ctrl+Shift+B)

**Critical Rule:** ALWAYS run `deploy-sync.ps1` after modifying any file in `src/`.

### 5. Git Integration

**Available Tools:**
- Cursor's built-in Git UI
- Source Control panel (Ctrl+Shift+G)
- Git commands in terminal

**Usage in Cursor:**
- Stage changes in Source Control panel
- Commit with Ctrl+Enter
- Push/pull with sync button
- View diffs inline

### 6. AI Features (Cursor-Specific)

**Available Tools:**
- Cursor Chat (Ctrl+L) - AI-powered code assistance
- Cursor Composer (Ctrl+K) - Multi-file editing
- Cursor Tab - AI code completion
- Cursor Cmd+K - Inline code generation

**Usage Examples:**
```
# In Cursor Chat
"Explain this FSM implementation"
"Refactor this function to be lock-free"
"Add unit tests for this class"

# In Cursor Composer
"Update all files to use the new FSM pattern"
"Refactor SIMA subgraph into separate files"

# Inline with Cmd+K
Select code → Cmd+K → "Make this lock-free"
```

## Tool Access Matrix

| Tool Category | Cursor IDE | Bob CLI | Notes |
|--------------|------------|---------|-------|
| jCodemunch MCP | ✅ Full | ✅ Full | Via MCP integration |
| Routa CLI | ❌ No | ✅ Full | Use Bob for Routa tasks |
| graphify | ✅ Read | ✅ Full | Cursor can read, Bob can update |
| Jane Street KB | ❌ No | ✅ Full | Use Bob for KB queries |
| Testing Tools | ✅ Full | ✅ Full | Native test runner |
| Build Tools | ✅ Full | ✅ Full | Via terminal |
| Git Integration | ✅ Native | ✅ Via CLI | Cursor has UI |
| AI Features | ✅ Native | ❌ No | Cursor-specific |
| LangSmith | ❌ No | ✅ Full | Bob only |

## Cursor-Specific Workflows

### 1. Code Exploration

**Workflow:**
1. Open `graphify-out/GRAPH_REPORT.md` to understand architecture
2. Use Cursor's "Go to Definition" (F12) for navigation
3. Use jCodemunch MCP for deeper analysis
4. Use Cursor Chat for explanations

**Example:**
```
1. Read graphify-out/GRAPH_REPORT.md
2. F12 on "ProcessBracketEvent"
3. @jcodemunch get blast radius for "ProcessBracketEvent"
4. Cursor Chat: "Explain this function's role in the FSM"
```

### 2. Refactoring

**Workflow:**
1. Use jCodemunch to analyze impact
2. Use Cursor Composer for multi-file edits
3. Run tests in Test Explorer
4. Sync hard links with deploy-sync.ps1

**Example:**
```
1. @jcodemunch get blast radius for "OldFunction"
2. Cursor Composer: "Rename OldFunction to NewFunction across all files"
3. Ctrl+Shift+T → Run affected tests
4. Terminal: powershell -File .\deploy-sync.ps1
```

### 3. Bug Fixing

**Workflow:**
1. Use Cursor's debugger to identify issue
2. Use jCodemunch to find related code
3. Use Cursor Chat for fix suggestions
4. Run tests to verify

**Example:**
```
1. Set breakpoint → F5 to debug
2. @jcodemunch find references to "BuggyFunction"
3. Cursor Chat: "How should I fix this race condition?"
4. Ctrl+Shift+T → Run tests
```

### 4. Documentation

**Workflow:**
1. Use Cursor Chat to generate docs
2. Use Cursor Composer for multi-file updates
3. Preview markdown in Cursor

**Example:**
```
1. Cursor Chat: "Generate XML docs for this class"
2. Cursor Composer: "Add XML docs to all public methods in src/"
3. Ctrl+Shift+V → Preview markdown
```

## Mandatory Tool Usage

### Before ANY src/ Edit

1. **Check graphify:**
   - Open `graphify-out/GRAPH_REPORT.md`
   - Understand module structure

2. **Use jCodemunch:**
   ```
   @jcodemunch plan turn for "<task description>"
   ```

### After ANY src/ Edit

1. **Sync Hard Links:**
   ```bash
   powershell -File .\deploy-sync.ps1
   ```

2. **Run Tests:**
   - Use Test Explorer (Ctrl+Shift+T)
   - Or terminal: `dotnet test`

3. **Format Code:**
   ```bash
   powershell -File .\scripts\format_all_csharp.ps1
   ```

### Before ANY Commit

1. **Review Changes:**
   - Use Source Control panel (Ctrl+Shift+G)
   - Review diffs inline

2. **Run Local Checks:**
   ```bash
   # In terminal
   powershell -File .\scripts\verify_pr_hygiene.ps1
   ```

3. **Stage & Commit:**
   - Stage in Source Control panel
   - Write descriptive commit message
   - Commit with Ctrl+Enter

## Cursor Settings for V12

### Recommended Settings

**`.vscode/settings.json`:**
```json
{
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll": true
  },
  "files.exclude": {
    "**/bin": true,
    "**/obj": true,
    "**/.vs": true
  },
  "csharp.format.enable": true,
  "omnisharp.enableRoslynAnalyzers": true,
  "mcp.servers": {
    "jcodemunch": {
      "command": "C:\\Users\\Mohammed Khalid\\.local\\bin\\jcodemunch-mcp.exe"
    }
  }
}
```

### Cursor AI Settings

**For V12 DNA Compliance:**
- Enable "Strict Mode" for code generation
- Set "Max Complexity" to 15 (Jane Street alignment)
- Enable "Lock-Free Pattern" suggestions
- Disable "Unicode Characters" in code

## Limitations

### What Cursor CANNOT Do

1. **Routa CLI Tasks:**
   - Architecture analysis
   - Multi-agent coordination
   - Kanban workflow
   - **Solution:** Hand off to Bob CLI

2. **Jane Street KB Queries:**
   - HFT pattern lookup
   - Lock-free design patterns
   - **Solution:** Hand off to Bob CLI

3. **LangSmith Tracing:**
   - Agent execution tracing
   - Context propagation
   - **Solution:** Use Bob CLI for multi-agent tasks

4. **graphify Updates:**
   - Can read, but cannot update
   - **Solution:** Use Bob CLI or terminal

### When to Use Bob Instead

**Use Bob CLI for:**
- Complex refactoring (multi-file, cross-module)
- Architecture analysis (Routa CLI)
- Jane Street KB queries
- Multi-agent coordination
- LangSmith-traced workflows

**Use Cursor for:**
- Quick edits and fixes
- Debugging
- Test writing
- Documentation
- Code exploration

## Related Documentation

- [Universal Agent Protocol](../docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)
- [Bob CLI Tools](.bob/TOOLS.md)
- [MCP Configuration](../docs/setup/MCP_CONFIGURATION.md)
- [Cursor Rules](.cursorrules)

## Version History

- **v1.0.0** (2026-05-23): Initial Cursor IDE tools reference
  - Clarified Cursor-only scope
  - Distinguished from Bob CLI tools
  - Added Cursor-specific workflows