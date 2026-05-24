# V12 Agent Memory

**Purpose:** Persistent cross-session knowledge that agents load at session start. This file contains facts the agent has learned about V12 DNA, project structure, environment, and past mistakes.

**Last Updated:** 2026-05-24T01:33:58Z

---

## V12 DNA Constraints (Never Forget)

### Lock-Free Mandate
- **STRICTLY BANNED:** `lock(stateLock)` blocks
- **REQUIRED:** FSM/Actor `Enqueue` model or atomic primitives
- **Verification:** `grep -r "lock(" src/` must return 0 matches
- **Enforcement:** Pre-push hook blocks any lock() in src/

### ASCII-Only Compliance
- **STRICTLY BANNED:** Unicode, emoji, curly quotes in C# string literals
- **REQUIRED:** ASCII-only characters (0x00-0x7F)
- **Verification:** Pre-push ASCII gate scans all src/ files
- **Enforcement:** Hard block - push rejected if Unicode detected

### Complexity Threshold
- **REQUIRED:** Cyclomatic complexity ≤15 for all methods
- **RATIONALE:** Jane Street HFT alignment - cognitive simplicity over clever abstractions
- **VERIFICATION:** `python scripts/complexity_audit.py`
- **ENFORCEMENT:** Codacy flags functions exceeding threshold

### Performance Targets
- **REQUIRED:** 0 B allocation in hot paths
- **REQUIRED:** < 300μs latency for critical operations
- **VERIFICATION:** BenchmarkDotNet tests in benchmarks/
- **ENFORCEMENT:** Performance regression = ticket blocker

### Correctness by Construction
- **PRINCIPLE:** "Make illegal states unrepresentable"
- **APPROACH:** Structure types/enums so compiler prevents invalid states
- **AVOID:** Runtime if/else guards for edge cases
- **DESIGN:** Architecture eliminates edge cases at compile time

---

## Project Structure

### Repository
- **Name:** universal-or-strategy
- **Location:** c:/WSGTA/universal-or-strategy
- **Primary Language:** C# (.NET 8)
- **Target Platform:** NinjaTrader 8

### Build System
- **Primary:** MSBuild + dotnet CLI
- **Linting:** Roslyn analyzers + StyleCop
- **Formatting:** CSharpier (optional)
- **Compilation:** `dotnet build .\Linting.csproj`

### Testing Infrastructure
- **Unit Tests:** xUnit in tests/V12_Performance.Tests/
- **Benchmarks:** BenchmarkDotNet in benchmarks/
- **Integration:** AMAL Harness (scripts/amal_harness_v26.py)
- **Coverage Target:** >80% for new code

### Deployment
- **CRITICAL:** `powershell -File .\deploy-sync.ps1` MANDATORY after src/ edits
- **Purpose:** Synchronize hard links between src/ and NinjaTrader 8/bin/Custom/Strategies
- **Verification:** deploy-sync.ps1 checks hash integrity
- **Failure:** Desync = stale DLL = BUILD_TAG mismatch = F5 failure

### Hard Links
- **Source:** c:/WSGTA/universal-or-strategy/src/
- **Target:** C:/Users/Mohammed Khalid/Documents/NinjaTrader 8/bin/Custom/Strategies/
- **Count:** 79 files
- **Integrity:** Verified by deploy-sync.ps1 on every push

---

## Architecture Patterns

### FSM/Actor Model
- **State Mutations:** All via `Enqueue` to FSM queue
- **Concurrency:** Lock-free message passing
- **Example:** SIMA subgraph uses FSM for order lifecycle

### Atomic Operations
- **Primitives:** Interlocked.CompareExchange, Interlocked.Exchange
- **Volatility:** `volatile` keyword for shared state
- **Memory Barriers:** Explicit when needed

### Zero-Allocation Patterns
- **ArrayPool:** Rent/return for temporary buffers
- **Struct Semantics:** Value types for hot path data
- **Span<T>:** Zero-copy slicing
- **StringBuilder:** Reuse for string building

### Microsecond Latency
- **Measurement:** LatencyProbe + LatencyHistogram
- **Target:** <300μs for critical paths
- **Optimization:** Jane Street KB patterns

---

## Tools & Workflows

### Code Navigation
- **Primary:** jCodemunch MCP (search_symbols, get_blast_radius)
- **Secondary:** Routa CLI (architecture analysis)
- **Knowledge Graph:** graphify (71x token efficiency)
- **Knowledge Base:** Jane Street KB (query_kb.py)

### Quality Gates
- **Semgrep:** V12 DNA pattern enforcement
- **CodeRabbit CLI:** Local AI review
- **Build Readiness:** Compilation + hard-link sync
- **PR Hygiene:** Rebase + diff size check

### PR Workflow
- **Separation:** NEVER mix src/ and non-src/ in same PR
- **src/ PR:** Full bot audit, 85+ PHS required
- **non-src/ PR:** Lightweight review, fast-track merge
- **Verification:** `verify_pr_separation.ps1 -PrNumber <N>`

### Testing
- **TDD Enforcement:** Pre-tool-use hook blocks src/ edits without tests
- **Testing Pyramid:** 70% unit, 20% integration, 10% E2E
- **Red-Green-Refactor:** Mandatory for all feature work

---

## Environment

### Operating System
- **OS:** Windows 11
- **Shell:** PowerShell (default)
- **Home:** C:/Users/Mohammed Khalid

### Development Tools
- **IDE:** VS Code (primary), NinjaTrader IDE (F5 testing)
- **Git:** GitHub CLI (gh) + git
- **Python:** 3.12 (scripts, AMAL harness)
- **Node:** npm (Routa CLI)

### Agent Ecosystem
- **Bob CLI:** v12-engineer (src/ work), v12-planner (epic planning)
- **Claude Opus 4.7:** Architectural review (escalation only)
- **Codex CLI:** Surgical logic hardening
- **Gemini CLI:** Non-src utility tasks
- **Jules AI:** GitHub workflows

---

## Past Mistakes (Learned)


### Test entry - Persistent memory system implementation
- **Added:** 2026-05-24T01:33:58Z
- **Source:** Agent memory update

### Mistake: Forgot deploy-sync.ps1 after src/ edit
- **Impact:** Hard link desync → stale DLL → BUILD_TAG mismatch → F5 failure
- **Root Cause:** Manual step, easy to forget
- **Solution:** Post-tool-use hook auto-runs deploy-sync.ps1
- **Prevention:** Hook enforcement + pre-push verification

### Mistake: Used Unicode in C# string literal
- **Impact:** ASCII gate FAIL → push rejected → rollback required
- **Root Cause:** Copy-paste from external source with curly quotes
- **Solution:** Pre-tool-use hook blocks Unicode
- **Prevention:** ASCII check in pre-push validation

### Mistake: Skipped TDD RED phase
- **Impact:** Implemented without test → milestone validation FAIL → rework
- **Root Cause:** Rushed implementation, skipped protocol
- **Solution:** Pre-tool-use hook blocks src/ edits without tests
- **Prevention:** TDD enforcement in all workflows

### Mistake: Mixed src/ and non-src/ in same PR
- **Impact:** Bot audit confusion → PHS calculation error → merge delay
- **Root Cause:** Convenience over protocol
- **Solution:** PR separation verification script
- **Prevention:** verify_pr_separation.ps1 in pre-push checklist

### Mistake: Exceeded CYC threshold (>15)
- **Impact:** Codacy flagged → complexity debt → refactor required
- **Root Cause:** God function, too much logic in one method
- **Solution:** Extract sub-methods, use FSM pattern
- **Prevention:** complexity_audit.py in pre-push checklist

---

## Active Patterns

### Epic Workflow
1. **Intake:** Scope definition (docs/brain/<epic>/00-scope.md)
2. **Plan:** Analysis + approach (01-analysis.md, 02-approach.md)
3. **Scan:** Sentinel audit (02-greptile-report.md)
4. **Validate:** DNA compliance check
5. **Tickets:** Execution plan (ticket-XX-*.md)
6. **Execute:** Ticket loop with F5 verification
7. **PR:** Submission + /pr-loop to 100/100 PHS

### Ticket Workflow
1. **Read ticket:** Load ticket-XX-*.md
2. **Plan:** Write extraction plan (sub-methods, CYC estimates)
3. **Gate:** Director approval
4. **Execute:** Implement plan
5. **Verify:** deploy-sync + complexity audit + lock() scan
6. **F5 Gate:** Director tests in NinjaTrader
7. **Commit:** Auto-commit with BUILD_TAG
8. **PR Loop:** Drive to 100/100 PHS

### TDD Workflow
1. **RED:** Write failing test (tdd-red skill)
2. **GREEN:** Implement minimal code to pass (tdd-green skill)
3. **REFACTOR:** Clean up while keeping tests green (tdd-refactor skill)
4. **Milestone Validation:** After every ticket

---

## Knowledge Sources

### Jane Street KB
- **Purpose:** HFT patterns, lock-free designs, microsecond-latency optimizations
- **Access:** `python scripts/query_kb.py "<query>"`
- **Mandatory:** Query before ANY src/ modification
- **Examples:** "lock-free queue", "actor model state mutations", "microsecond latency patterns"

### Graphify Knowledge Graph
- **Purpose:** Codebase structure, god nodes, community detection
- **Access:** graphify-out/GRAPH_REPORT.md
- **Update:** `graphify update .` after structural changes
- **Efficiency:** 71x fewer tokens than raw file reading

### jCodemunch MCP
- **Purpose:** Symbol search, dependency analysis, blast radius
- **Tools:** search_symbols, get_blast_radius, find_references
- **Session Start:** resolve_repo, suggest_queries
- **Efficiency:** Targeted code exploration vs grep

### Routa CLI
- **Purpose:** Architecture analysis, multi-file refactoring
- **Usage:** `routa -p "<query>"`
- **Examples:** "Analyze SIMA subgraph", "Plan RMA proximity monitoring"
- **Integration:** Kanban workflow automation

---

## Notes

- This file is loaded at session start and injected into system prompt
- Update via `update_memory` tool when learning new facts
- Keep entries concise - this is bounded memory, not a knowledge dump
- Cross-reference detailed docs (don't duplicate content)
- Review and prune quarterly to prevent bloat