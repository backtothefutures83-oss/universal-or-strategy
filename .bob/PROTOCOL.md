# Bob CLI Protocol

## Overview

This document defines Bob CLI-specific protocols and workflows. For universal agent protocols, see [AGENTS.md](../AGENTS.md).

## Session Initialization

Every Bob CLI session MUST follow this initialization sequence:

### 1. Load Primary Sources

```markdown
1. Read AGENTS.md (primary source of truth)
2. Read .bob/rules/ (Bob-specific rules)
3. Read .bob/PROTOCOL.md (this file)
```

### 2. Architecture Task Check

**If task involves architecture, performance, or concurrency:**

```bash
python scripts/query_kb.py "<task description>"
```

**Examples:**
```bash
python scripts/query_kb.py "lock-free queue implementation"
python scripts/query_kb.py "FSM state machine design"
python scripts/query_kb.py "microsecond latency optimization"
```

### 3. Tool Initialization

**Verify all mandatory tools are available:**

```bash
# jCodemunch MCP
jcodemunch-mcp --version

# Routa CLI
routa --version

# graphify
graphify --version

# Jane Street KB
python scripts/query_kb.py "test"
```

### 4. Repository Status

**Check graphify status:**

```bash
# If graphify-out/ exists
cat graphify-out/GRAPH_REPORT.md

# If not, create it
graphify update .
```

### 5. LangSmith Tracing

**Verify tracing is enabled:**

```bash
# Check .env
grep LANGSMITH_TRACING .env

# Expected: LANGSMITH_TRACING=true
```

## Mandatory Checks

Before starting ANY task, verify:

- [ ] KB consultation (if architecture/performance/concurrency task)
- [ ] Routa CLI available
- [ ] jCodemunch MCP connected
- [ ] graphify up-to-date
- [ ] LangSmith tracing enabled

## PR Workflow

### Critical Rules

1. **NEVER mix src/ and non-src/ files in the same PR**
2. **ALWAYS run verify_pr_separation.ps1 before pushing**
3. **Use /pr-loop for src-only PRs**
4. **Fast-track non-src/ PRs (no bot audit)**

### src/ PR Workflow

```bash
# 1. Make changes to src/ files only
# 2. Verify separation
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>

# 3. Run /pr-loop to 100/100 PHS
/pr-loop <N>

# 4. Wait for F5 verification
# 5. Merge after approval
```

### non-src/ PR Workflow

```bash
# 1. Make changes to non-src/ files only
# 2. Verify separation
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>

# 3. Fast-track merge (no /pr-loop needed)
gh pr merge <N> --squash --auto
```

### Mixed PR Remediation

**If you accidentally create a mixed PR:**

```bash
/pr-split <N>
```

This will:
1. Close the mixed PR
2. Create src-only PR
3. Create non-src-only PR
4. Link them together

## Tool Priority

Use tools in this priority order:

### 1. jCodemunch MCP (Code Navigation)

**Always use for:**
- Finding symbols
- Reading code
- Understanding relationships
- Blast radius analysis

**Never use Read/Grep/Glob for code exploration.**

### 2. graphify (Architecture Overview)

**Use for:**
- Repository structure
- God nodes identification
- Community detection
- High-level navigation

### 3. Jane Street KB (HFT Patterns)

**Query before:**
- Lock-free implementations
- Atomic operations
- Performance optimizations
- Concurrency patterns

### 4. Routa CLI (Multi-File Refactoring)

**Use for:**
- Architecture analysis
- Feature tree generation
- Multi-file coordination
- Kanban workflow

## V12 DNA Enforcement

### Lock-Free Mandate

**BANNED:**
```csharp
lock (stateLock) { ... }
Monitor.Enter/Exit
Mutex.WaitOne/ReleaseMutex
```

**APPROVED:**
```csharp
Interlocked.CompareExchange
Interlocked.Increment
Volatile.Read/Write
Thread.MemoryBarrier
FSM/Actor Enqueue pattern
```

**Verification:**
```bash
# Must return zero matches
grep -r "lock(" src/
```

### ASCII-Only Compliance

**BANNED:**
- Unicode characters
- Emoji
- Curly quotes
- Non-ASCII symbols

**Verification:**
```bash
python check_ascii.py src/
```

### Hard-Link Integrity

**MANDATORY after every src/ change:**

```bash
powershell -File .\deploy-sync.ps1
```

This synchronizes NinjaTrader hard links.

## Workflow Commands

### /epic-run

**Full epic orchestration:**

```bash
/epic-run <epic-slug> "<description>"
```

**Phases:**
1. Intake (scope definition)
2. Plan (analysis + approach)
3. Scan (Sentinel audit)
4. Validate (issue resolution)
5. Tickets (task breakdown)
6. Execution (ticket loop)
7. PR Submission (TWO separate PRs)

### /pr-loop

**Drive PR to 100/100 PHS:**

```bash
/pr-loop <PR_NUMBER>
```

**Steps:**
0. Pre-flight hygiene
1. Bot forensics extraction
1.5. CI log extraction
2. Local repair
3. Global push & monitor
4. Manual override gate (if <100 after 3+ iterations)
5. F5 verification

### /pr-split

**Split mixed PR:**

```bash
/pr-split <PR_NUMBER>
```

**Output:**
- src-only PR (requires /pr-loop)
- non-src-only PR (fast-track)

### /pre-push

**Pre-push checklist:**

```bash
/pre-push
```

**Verifies:**
- Branch is clean
- Diff size < 10k
- No lock() violations
- ASCII compliance
- Build passes

## Error Recovery

### Build Failures

```bash
# 1. Check build output
dotnet build .\Linting.csproj

# 2. Run build readiness
powershell -File .\scripts\build_readiness.ps1

# 3. If hard-link issue
powershell -File .\deploy-sync.ps1
```

### Lock Violations

```bash
# 1. Scan for violations
grep -r "lock(" src/

# 2. Hand off to Codex CLI for hardening
# (Bob delegates, Codex fixes)

# 3. Verify fix
grep -r "lock(" src/  # Must return zero
```

### PR Separation Violations

```bash
# 1. Verify violation
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>

# 2. Split PR
/pr-split <N>

# 3. Run /pr-loop on src-only PR
/pr-loop <SRC_PR>

# 4. Fast-track non-src-only PR
gh pr merge <NON_SRC_PR> --squash
```

## Handoff Protocols

### Bob → Codex (Logic Hardening)

**When to delegate:**
- Lock-free conversion needed
- Atomic operation implementation
- Race condition fixes
- Memory ordering issues

**Handoff format:**
```markdown
TASK: Logic Hardening
FILE: src/V12_002.Orders.Management.cs
METHOD: ProcessOrder (lines 145-178)
ISSUE: lock(stateLock) usage
GOAL: Convert to FSM/Actor pattern
```

### Bob → Gemini (Utility Tasks)

**When to delegate:**
- Non-src/ changes
- Documentation updates
- Workflow modifications
- Visual analysis

**Handoff format:**
```markdown
TASK: Update Documentation
FILES: docs/agents/BOB_TOOLS.md
GOAL: Add new tool examples
```

### Bob → Jules (GitHub Operations)

**When to delegate:**
- PR creation (non-src/)
- Issue management
- GitHub Actions triggers
- Branch operations

**Handoff format:**
```markdown
TASK: Create non-src PR
FILES: docs/, tests/
LABEL: non-src-only
```

## Quality Gates

### Pre-Commit

- [ ] All tests pass
- [ ] No lock() violations
- [ ] ASCII compliance
- [ ] Build succeeds
- [ ] Hard-links synchronized

### Pre-Push

- [ ] Branch rebased on main
- [ ] Diff size < 10k
- [ ] PR separation verified
- [ ] Local score 15/15

### Pre-Merge

- [ ] PHS 100/100 (or Director approved)
- [ ] F5 verification passed
- [ ] All bots satisfied
- [ ] CI green

## Configuration Files

### .bob/settings.json

```json
{
  "checkpointing": true,
  "auto_test": true,
  "strict_dna": true,
  "pr_separation": true
}
```

### .bob/custom_modes.yaml

```yaml
v12-engineer:
  description: "Unified Architect-Engineer for src/ work"
  rules_dir: ".bob/rules-v12-engineer/"
  allowed_files: "src/**"
```

## References

- [AGENTS.md](../AGENTS.md) - Primary source of truth
- [Universal Agent Protocol](../docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)
- [PR Loop V2](../docs/protocol/PR_LOOP_V2.md)
- [Jane Street KB](../scripts/query_kb.py)
- [Routa CLI](https://github.com/your-org/routa-cli)
- [jCodemunch MCP](https://github.com/your-org/jcodemunch-mcp)