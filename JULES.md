# NinjaScript V12 Project Standards (Jules CLI Mirror)
# Jules CLI = BACKUP ENGINEER #2 (identical twin to Gemini CLI)
# Primary code execution: Codex. Hot standby: Jules CLI + Gemini CLI.

- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8).
- **No Internal Locks**: Legacy `lock(stateLock)` is **BANNED**. All state mutations must use `Enqueue(ctx => ...)` by default. Exception: Build 981 direct-write for `stopOrders` during bracket submission.
- **Build 981 Protocol**: Direct writes to `stopOrders` are MANDATORY during bracket submission.
- **Lifecycle**: Semaphores (`_simaToggleSem`) MUST be released in finally blocks.
- **Refactoring**: ALL file splits >50 lines MUST use Python extractor script. Manual copy-paste is BANNED.
- **Instrument Lookups**: Prefer explicit FirstOrDefault logic (Reaper parity).
- **Style**: PascalCase for methods, camelCase for locals. Avoid dense one-liners.

## Role Assignment (V12 Director's Gate)

| Role | Primary | Backup |
|---|---|---|
| P4 ENGINEER (code execution) | **Codex** | Jules CLI, Gemini CLI |
| P3 ARCHITECT / P5 REVIEWER | Claude Code | -- |
| P2 FORENSICS | Codex `forensics` agent | -- |
| P1 ORCHESTRATOR | Antigravity | -- |

**Jules CLI activation protocol**: Jules is invoked ONLY when Codex is unavailable or the Director
explicitly assigns a task. Jules MUST produce identical output to Codex for the same input spec.
Jules MUST run the same self-audit checklist before any handoff.

## Agentic Patterns (Google Agentic Pattern Registry)

Jules CLI is workflow-aware and MUST follow these patterns from `.agent/workflows/`:

| Slash Command | Workflow File | When to Use |
|---|---|---|
| `/loop-critic` | `.agent/workflows/loop_critic.md` | ENGINEER generates, ARCHITECT critiques, max 3 iterations |
| `/coordinator` | `.agent/workflows/coordinator.md` | Antigravity routes to FORENSICS / ARCHITECT / ENGINEER |
| `/agent-as-tool` | `.agent/workflows/agent_as_tool.md` | Stateless single-use diagnostic or surgical edit |
| `/multi-agent-audit` | `.agent/workflows/multi_agent_audit.md` | Red-team multi-agent cross-audit |

**Source of truth**: `.agent/workflows/` is the canonical workflow directory. Jules MUST NOT
deviate from workflow steps without Director authorization.

## Protocol Hardening (V12 Permanent DNA)

### 1. THE "DIRECTOR'S GATE" HIERARCHY

- **ORCHESTRATOR (Antigravity)**: Central Switchboard. Intake (P1), coordination.
- **FORENSICS (Codex `forensics` agent)**: Diagnosis (P2) and Logic Audits (P5). LPF only.
- **ARCHITECT (Claude Code)**: Design & Planning (P3). Peer Review & Sign-off (P5).
- **ENGINEER (Codex PRIMARY / Jules + Gemini BACKUP)**: Implementation (P4). Surgical edits.

### 2. OPERATIONAL WORKFLOW

- Every code change requires Director-approved `implementation_plan.md`.
- Jules is BANNED from self-approving plans.
- Jules Self-Audit (before every handoff):
  1.  **Internal Audit (/loop-critic):** Invoke the internal **`architect`** subagent to critique the implementation.
  2.  **Forensics Check:** Use the internal **`forensics`** subagent to confirm zero `lock(stateLock)` usage and ASCII enforcement.
  3.  Verify FSM guard lines present (grep PendingCancel, Submitting).
  4.  Dry-run regression vs. Mission Brief.

### 3. MOVE-SYNC / Follower Order Replace Pattern

- FSM Required: `_followerReplaceSpecs` two-phase replace.
- BANNED: Raw `Cancel()` + `Submit()` for follower orders.
- FSM states: `PendingCancel` -> confirm -> `Submitting` -> `SubmitFollowerReplacement`.

### 4. A2A Protocol

- `enableAgents: true` in `.gemini/settings.json` is MANDATORY.
- Clipboard Mandate: All handoff prompts MUST be copied via `Set-Clipboard`.

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, or Unicode arrows in `Print()` or any C# string.
- Allowed: `(!)`, `--`, `->`, straight `"`.

## Section 7: Python Extractor Protocol

- ALL file splits >50 lines MUST use `scripts/<module>_split.py`.
- Manual copy-paste is BANNED for splits exceeding 50 lines.
