# V12 User Profile

**Purpose:** Persistent user preferences, coding style, and past decisions that agents remember across sessions.

**Last Updated:** 2026-05-24T01:30:00Z

---

## User Identity

### Name
- Mohammed Khalid

### Role
- Director / Product Owner
- Primary decision maker for V12 architecture
- Final authority on DNA compliance

### Communication Style
- Direct, technical, no fluff
- Prefers concise summaries over verbose explanations
- Values efficiency and correctness over speed

---

## Development Preferences

### Shell & Environment
- **Primary Shell:** PowerShell (not Bash)
- **OS:** Windows 11
- **Home Directory:** C:/Users/Mohammed Khalid
- **Workspace:** c:/WSGTA/universal-or-strategy

### Code Style
- **Language:** C# (.NET 8)
- **Formatting:** Minimal - only fix what you touch
- **Comments:** Sparse - code should be self-documenting
- **Naming:** Descriptive, no abbreviations unless domain-standard

### Agent Preferences
- **Primary Agent:** Bob CLI (v12-engineer) for src/ work
- **Planning Agent:** Bob CLI (v12-planner) for epic planning
- **Utility Agent:** Gemini CLI for non-src tasks
- **Escalation:** Claude Opus 4.7 for architectural review (rare)

### Workflow Preferences
- **TDD:** Mandatory - RED-GREEN-REFACTOR for all features
- **F5 Verification:** Required after every ticket
- **PHS Target:** 100/100 before merge (no exceptions)
- **PR Separation:** Strict - never mix src/ and non-src/

---

## Decision History

### Architecture Decisions

#### Lock-Free Mandate (2024-Q3)
- **Decision:** Ban all `lock(stateLock)` blocks in src/
- **Rationale:** Jane Street HFT alignment, microsecond latency requirements
- **Alternative Considered:** Fine-grained locking (rejected - too error-prone)
- **Enforcement:** Pre-push hook + grep verification

#### ASCII-Only Compliance (2024-Q4)
- **Decision:** Ban Unicode/emoji in C# string literals
- **Rationale:** NinjaTrader compatibility, encoding issues in production
- **Alternative Considered:** UTF-8 everywhere (rejected - platform constraints)
- **Enforcement:** Pre-push ASCII gate

#### Complexity Threshold 15 (2025-Q1)
- **Decision:** CYC ≤15 for all methods
- **Rationale:** Jane Street cognitive simplicity, testability
- **Alternative Considered:** Threshold 20 (rejected - too permissive)
- **Enforcement:** Codacy + complexity_audit.py

#### TDD Enforcement (2026-Q2)
- **Decision:** Mandatory TDD for all src/ work
- **Rationale:** Too many regressions from untested code
- **Alternative Considered:** Optional TDD (rejected - not enforced)
- **Enforcement:** Pre-tool-use hook blocks src/ edits without tests

### Tool Adoption

#### jCodemunch MCP (2025-Q4)
- **Decision:** Primary code navigation tool
- **Rationale:** 71x token efficiency vs raw file reading
- **Alternative Considered:** Grep + Read (rejected - token waste)
- **Usage:** Mandatory for all code exploration

#### Routa CLI (2026-Q1)
- **Decision:** Architecture analysis and planning
- **Rationale:** Multi-file refactoring coordination
- **Alternative Considered:** Manual planning (rejected - error-prone)
- **Usage:** Mandatory for src/ refactoring

#### Hermes Integration (2026-Q2)
- **Decision:** Adopt persistent memory + progressive disclosure
- **Rationale:** Cross-session learning, token efficiency
- **Alternative Considered:** Status quo (rejected - too much relearning)
- **Timeline:** Phase 1 (memory) next sprint, Phase 2 (skills) next month

---

## Coding Standards

### Naming Conventions
- **Classes:** PascalCase (e.g., `OrderManager`)
- **Methods:** PascalCase (e.g., `ProcessOrder`)
- **Variables:** camelCase (e.g., `orderCount`)
- **Constants:** UPPER_SNAKE_CASE (e.g., `MAX_RETRIES`)
- **Private Fields:** _camelCase (e.g., `_orderQueue`)

### File Organization
- **One class per file** (unless nested/helper classes)
- **Partial classes:** Use for large files (e.g., V12_002.*.cs)
- **Namespace:** Match directory structure
- **Using statements:** Inside namespace (not outside)

### Error Handling
- **Exceptions:** Use for exceptional cases only
- **Return codes:** Prefer for expected failures
- **Logging:** Structured logging via StructuredLog
- **Telemetry:** Latency probes for hot paths

### Performance
- **Hot paths:** Zero allocation, <300μs latency
- **Cold paths:** Readability over micro-optimization
- **Measurement:** BenchmarkDotNet for all claims
- **Profiling:** LatencyHistogram for production monitoring

---

## Quality Standards

### Testing
- **Coverage:** >80% for new code
- **Pyramid:** 70% unit, 20% integration, 10% E2E
- **TDD:** RED-GREEN-REFACTOR mandatory
- **Benchmarks:** All hot paths must have BenchmarkDotNet tests

### Code Review
- **Bot Audit:** CodeRabbit, Codacy, Semgrep (src/ PRs)
- **Human Review:** Director reviews all src/ PRs
- **PHS Target:** 100/100 before merge
- **F5 Gate:** Director tests in NinjaTrader after every ticket

### Documentation
- **Code Comments:** Minimal - only for non-obvious logic
- **Protocol Docs:** Comprehensive - update after every pattern change
- **Training Docs:** Examples-driven - show, don't tell
- **Verification Reports:** Detailed - 100% coverage of deliverables

---

## Communication Patterns

### Feedback Style
- **Positive:** "Approved", "Go", "Proceed"
- **Negative:** "HALT", "Revert", "Fix before proceeding"
- **Clarification:** Direct questions, no hedging
- **Delegation:** Clear task boundaries, success criteria

### Response Expectations
- **Summaries:** Bullet points, not paragraphs
- **Status:** Clear PASS/FAIL, no ambiguity
- **Errors:** Root cause + fix, not just symptoms
- **Plans:** Concrete steps, not abstract goals

### Decision Making
- **Autonomy:** Agents decide implementation details
- **Escalation:** Architectural changes require approval
- **Iteration:** Prefer small PRs with fast feedback
- **Rollback:** No hesitation if something breaks

---

## Project Context

### Current Focus (2026-Q2)
- **Epic 6:** Performance lock-in (automated testing)
- **TDD Hardening:** Phases 1-4 complete
- **Hermes Integration:** Phase 1 (persistent memory) starting
- **Technical Debt:** Codacy grade B, 3,100 issues (chipping away)

### Recent Wins
- **Epic 5:** 0 B allocation, <300μs latency achieved
- **TDD Infrastructure:** 41 files, 4,545 lines, 100% verified
- **Bot Integration:** 10 GitHub apps active on PRs
- **PR Loop V2:** Bot forensics extraction, 100/100 PHS automation

### Known Pain Points
- **No persistent memory:** Agent relearns V12 DNA every session (fixing now)
- **Token waste:** All skills loaded upfront (Phase 2 will fix)
- **Manual memory updates:** mistake_log.jsonl not integrated (Phase 1 will fix)
- **Complexity debt:** 31/207 files exceed CYC 15 (Boy Scout Rule)

---

## Notes

- This file is loaded at session start alongside MEMORY.md
- Update via `update_memory` tool when preferences change
- Keep entries current - remove outdated decisions
- Cross-reference MEMORY.md for technical facts (this file is for preferences)
- Review quarterly to ensure accuracy