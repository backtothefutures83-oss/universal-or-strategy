# 🏛️ $workflow-pilot — Agent Workflow Pilot Checklist & SOP

This living document serves as the master checklist and standard operating procedure (SOP) when piloting a new workflow or executing tasks on any sovereign agent (e.g., Bob, Qwen, Codex, Claude, Jules, Gemini CLI) for the first time in the **Universal OR Strategy V12** workspace.

> [!IMPORTANT]
> **Command Access**: This checklist is referenced via the dynamic command `$workflow-pilot`. All agents must inspect this document at the start of any new epic, refactoring cycle, or multi-agent pipeline pilot.

---

## 🧭 Pre-Flight: Identity & Configuration
Before executing any prompt or tool, verify the runtime environment and agent parameters:
- [ ] **Model Identity & Tiering**: Announce or verify the exact model active in the session (e.g., `qwen-3.6-max-preview`, `claude-opus-4-7`, `gemini-2.0-flash`). Confirm that tool tiering (core/standard/full) matches the active model capability.
- [ ] **Mode & Approval Settings**: Check if the agent is operating under `Plan`, `Default`, `Auto-Edit`, or `YOLO` mode (specifically for Qwen/Bob).
- [ ] **Flag Verification**: Confirm that required execution flags are provided at startup:
  * `--checkpointing` (native shadow git tracking enabled)
  * `--json-file` / `--input-file` (if running in event-driven dual output mode)
  * `--headless` (if running background CI / automated execution)

---

## 📊 Telemetry & Observability (LangSmith & Open RAG)
Ensure that all multi-agent reasoning chains and data sweeps are fully logged for auditability:
- [ ] **Telemetry Ingestion Check**: Verify that the agent's environment variables (`LANGCHAIN_TRACING_V2=true`, `LANGCHAIN_API_KEY`, etc.) are correctly set.
- [ ] **LangSmith Connectivity**: Ensure that the agent's actions, tool invocations, and thinking tokens are captured under the correct project trace (Mission name + `BUILD_TAG`).
- [ ] **Open RAG Sweep Verification**: Check that any vector query or semantic search references are logged, preventing silent knowledge gaps or outdated document references.

---

## ⚡ Token Conservation & Loop Control (MANDATORY LAW)
Enforce strict zero-waste execution limits to preserve context space and prevent quadratic token cost:
- [ ] **Zero Active Polling**: Confirm the agent is not configured to run polling checks or busy-waiting loops.
- [ ] **Event-Driven IPC (Dual Output)**: Utilize Qwen Code's **Dual Output engine** (`--json-file` + `--input-file`) to communicate with sidecars or external watchers (Node.js sidecar or FS sentinel). The Orchestrator MUST sleep/yield until the sidecar triggers the input channel.
- [ ] **Decoupled Script Execution**: Ensure sequential agent tasks (like the 7-cluster sweep or PR audits) are dispatched to a single local execution script (`.ps1` or `.sh`) rather than calling distinct LLM loops for each step. The Orchestrator yields the turn immediately and wakes up exactly ONCE at the end of the entire script run.
- [ ] **Background Agents for Review Only**: Ensure background sub-agents are restricted to read-only tasks (P2/P5 audits). All file writes and surgical code edits must occur in the foreground session to prevent race conditions.

---

## 🔒 Checkpointing & Recovery Guard
Protect against unexpected session crashes, token rate-limiting, or local terminal timeouts:
- [ ] **Shadow Git Verification**: Verify that the checkpointing directory (e.g., `~/.qwen/history/`) is writable and initialized. Run `/history` or equivalent to confirm the shadow log works.
- [ ] **Workspace Milestone Persistence**: Mandate that all intermediate progress (draft codes, forensic audits, verified logs, plans) is persistently written to physical files under `docs/brain/` (e.g., `docs/brain/memory/[mission_name]_compaction_state.md`) at the end of every workflow stage.
- [ ] **Resumption / Restore Logic**: Verify that `/restore` (or equivalent command) correctly lists and can recover the latest state without duplicating prior computational token expenses.

---

## 🛡️ Pre-Surgery Environment Verification
Before applying any file write or replacement edits:
- [ ] **0-Delta State**: Verify that the git workspace is clean and has zero uncommitted modifications (`git status`).
- [ ] **Read-Before-Write Rule**: Ensure the agent performs a `Read`/`view_file` on the target path first before invoking `write_file` or `replace_file_content` (to satisfy local harness cache checks).
- [ ] **Whitespace & Format Preservation**: Enforce that the agent preserves existing indents, newlines, and line endings. Formatting overrides and arbitrary whitespace refactoring are strictly banned.
- [ ] **Strict Diff Guard**: Pull Request diffs MUST remain under 150,000 characters. If your formatting or logic pushes the diff over this limit, you must revert and isolate the logic changes.

---

## 🧪 Post-Surgery Verification & Handoff
Once changes have been applied to the workspace:
- [ ] **Hard-Link Synchronization**: Execute `powershell -File .\deploy-sync.ps1` immediately to restore the hard links to the NinjaTrader Custom directory.
- [ ] **ASCII Compiler Gate**: Verify that no non-ASCII, Unicode arrows, or box-drawing characters are introduced into C# string literals.
- [ ] **Compilation Gate**: Instruct the Director to compile (F5 in NT8) and verify that the `BUILD_TAG` banner matches the target.
- [ ] **Post-Use Skill Audit**: Perform a post-use audit on any skills utilized during the turn, updating the corresponding `SKILL.md` if any gaps or quirks were identified.
- [ ] **Physical Handoff Registration**: Update [docs/brain/nexus_a2a.json](file:///C:/WSGTA/universal-or-strategy/docs/brain/nexus_a2a.json) via the Nexus Bridge, registering the milestone data for the next agent before concluding.
