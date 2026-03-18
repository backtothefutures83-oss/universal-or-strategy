# NinjaScript V12 Project Standards (Codex Mirror)

- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8).
- **No Internal Locks**: `lock(stateLock)` is **BANNED**. All state mutations MUST use `Enqueue(ctx => ...)` by default. Exception: Build 981 direct-write for `stopOrders` during bracket submission.
- **Lifecycle**: Semaphores (\_simaToggleSem) MUST be released in finally blocks.
- **Refactoring**: Prefer explicit FirstOrDefault logic for instrument lookups (Reaper parity).
- **Style**: Use PascalCase for methods, camelCase for local variables. Avoid dense one-liners; prioritize "Metabolic Elegance."

## 🛡️ Protocol Hardening (V12.Phase7)

### 1. Scope Control

- **Surgical Edits**: AI agents MUST restrict code modifications to the specific files requested in the Mission Brief. NEVER refactor unrelated files without explicit Director authorization.
- **Zero-Trust Planning**: Always generate an `implementation_plan.md` before applying code changes to local files.

### 2. WPF/UI Guardrails

- **Escalation**: If a UI layout or positioning task enters a loop (more than 2 failed attempts), the agent MUST halt and escalate to the Director for manual layout review.
- **Headless Mode**: Prefer headless execution for batch logic updates; do not attempt complex UI re-styling without a visual brief.

### 3. Path & Deployment Management

- **Source Truth**: All primary NinjaScript logic resides in `src/`.
- **Deployment**: Local builds MUST be synced to `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\` using the `/deploy` skill.

## 🕹️ Director Commands ($)

- **$PLAN_AUDIT**: Use `read_terminal` on the active Claude/Antigravity PID to ingest Sonnet's implementation plan. Perform a forensic logic audit before recommending approval to the Director.
- **$MISSION**: Initialize a new project phase via a Mission Brief artifact.
- **$AUDIT**: Trigger the `/audit` skill to scan the `src/` directory.

## Agent Synchronization

AI Agents (Anthropic, Codex, Antigravity, Cursor, Gemini) MUST follow the **[.agent/standards_manifesto.md](file:///.agent/standards_manifesto.md)** as the primary source of truth for architectural standards and safety protocols.

**Clipboard Mandate**: All cross-agent handoff prompts, implementation plans, and commands MUST be automatically copied to the Director's clipboard (e.g., via PowerShell `Set-Clipboard`) so manual copying is never required.

## MOVE-SYNC / Follower Order Replace Pattern (Build 947+)

- **FSM Required**: Any follower order cancel+resubmit MUST use the two-phase Replace FSM (`_followerReplaceSpecs` dict).
- **Never cancel+submit directly**: Raw `Cancel()` followed immediately by `Submit()` creates ghost orders. BANNED.
- **FSM states**: `PendingCancel` -> wait for `OnAccountOrderUpdate` confirm -> `Submitting` -> `SubmitFollowerReplacement`.
- **ATR tick absorption**: While in `PendingCancel`, sizing changes update `PendingReplacementSpec` only. One cancel, one resubmit.
- **Fill-during-gap guard**: Check if master filled before submitting replacement. If yes, route to REAPER repair.
- **ChangeOrder banned for fleet accounts**: `Account.Change` silently no-ops on Apex/Tradovate.

## Live Bug Triage Protocol

- **Codex first**: For any live trading anomaly, run `/live-bug-triage` workflow before writing any mission brief.
- **Codex = diagnosis, Sonnet = implementation**: Do not ask Codex for patches.
- **Plan audit required**: Paste Sonnet's plan to Antigravity for audit before approving.
- **Workflow file**: `.agent/workflows/live-bug-triage.md`

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing in Print() or any string literal.
- Non-ASCII inside C# strings breaks the NinjaTrader compiler with 300+ cascading errors (Build 936 incident).
- Allowed substitutions: (!) not emoji, -- not em-dash, -> not arrow, straight " not curly " "
- See .agent/standards_manifesto.md Section 7 for the full rule table and emergency fix sequence.

## Role Assignment (V12 Director's Gate)

| Role | Primary | Backup |
|---|---|---|
| P4 ENGINEER (code execution) | **Codex** | Jules CLI, Gemini CLI |
| P3 ARCHITECT / P5 REVIEWER | Claude Code | -- |
| P2 FORENSICS | Codex `forensics` agent | -- |
| P1 ORCHESTRATOR | Antigravity | -- |

Codex is the **PRIMARY code executor**. Jules CLI and Gemini CLI are identical twin backups
dispatched only when Codex is unavailable or the Director explicitly assigns a task to them.

## Agentic Patterns (Google Agentic Pattern Registry)

Codex is workflow-aware and MUST follow these patterns from `.agent/workflows/`:

| Slash Command | Workflow File | Pattern |
|---|---|---|
| `/loop-critic` | `.agent/workflows/loop_critic.md` | ENGINEER generates, ARCHITECT critiques, max 3 iterations |
| `/coordinator` | `.agent/workflows/coordinator.md` | Antigravity routes to FORENSICS / ARCHITECT / ENGINEER |
| `/agent-as-tool` | `.agent/workflows/agent_as_tool.md` | Stateless single-use diagnostic or surgical edit |
| `/multi-agent-audit` | `.agent/workflows/multi_agent_audit.md` | Red-team multi-agent cross-audit |

Source of truth: `.agent/workflows/` is canonical. Codex MUST NOT deviate from workflow steps
without Director authorization. All coding tasks MUST conclude with a **/loop-critic** or **/multi-agent-audit** cycle using internal subagents before final P5 escalation.
