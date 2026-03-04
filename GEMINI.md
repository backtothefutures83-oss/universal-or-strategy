# NinjaScript V12 Project Standards (Gemini Mirror)

- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8).
- **Concurrency**: All state mutations (activePositions, expectedPositions) MUST be guarded by lock(stateLock).
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
- **Deployment**: Local builds MUST be synced to `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\` using the `./deploy-sync.ps1` script (or `/deploy` skill).

### 4. Repo Hygiene

- **Zero-Delta Start**: Every new phase MUST begin with a 0-delta `main` branch to prevent context debt.
- **Atomic Merging**: Merge and delete feature branches immediately after successful F5 compilation and initial testing.
- **Binary Guard**: DO NOT commit `.exe`, `.log`, or `.bak` files. Use stashing or `.gitignore`.
- **Clean Dashboard**: All agents (Claude, Gemini, Antigravity, Rovo Dev) MUST ensure the repo is clean before starting new missions.

### 5. MOVE-SYNC / Follower Order Replace Pattern (Build 947+)

- **FSM Required**: Any follower order cancel+resubmit MUST use the two-phase Replace FSM (`_followerReplaceSpecs` dict).
- **Never cancel+submit directly**: Raw `Cancel()` followed immediately by `Submit()` is BANNED for follower orders — it creates ghost orders.
- **FSM states**: `PendingCancel` -> wait for `OnAccountOrderUpdate` confirm -> `Submitting` -> `SubmitFollowerReplacement`.
- **ATR tick absorption**: While in `PendingCancel`, additional sizing changes update `PendingReplacementSpec` only. One cancel, one resubmit.
- **Fill-during-gap guard**: Before submitting replacement, check if master already filled. If yes, route to REAPER repair instead.
- **ChangeOrder banned for fleet accounts**: `Account.Change` silently no-ops on Apex/Tradovate. Cancel+resubmit (via FSM) is the only reliable path.

### 6. Live Bug Triage Protocol

- **Codex first**: For any live trading anomaly, run `/live-bug-triage` workflow. Use Codex for forensic code trace BEFORE writing any mission brief.
- **Codex = diagnosis, Sonnet = implementation**: Do not ask Codex for patches. Do not ask Sonnet to diagnose without a brief.
- **Plan audit required**: Always paste Sonnet's plan back to Antigravity for audit before approving implementation. Sonnet may catch brief errors (direction, order type, extra locations).
- **Workflow file**: `.agent/workflows/live-bug-triage.md` contains the full step-by-step protocol.

## 🕹️ Director Commands ($)

- **$PLAN_AUDIT**: Use `read_terminal` on the active Claude/Antigravity PID to ingest Sonnet's implementation plan. Perform a forensic logic audit before recommending approval to the Director.
- **$MISSION**: Initialize a new project phase via a Mission Brief artifact.
- **$AUDIT**: Trigger the `/audit` skill to scan the `src/` directory.

## Agent Synchronization

AI Agents (Anthropic, Codex, Antigravity, Cursor, Gemini) MUST follow the **[.agent/standards_manifesto.md](file:///.agent/standards_manifesto.md)** as the primary source of truth for architectural standards and safety protocols.

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing in Print() or any string literal.
- Non-ASCII inside C# strings breaks the NinjaTrader compiler with 300+ cascading errors (Build 936 incident).
- Allowed substitutions: (!) not emoji, -- not em-dash, -> not arrow, straight " not curly " "
- See .agent/standards_manifesto.md Section 7 for the full rule table and emergency fix sequence.
