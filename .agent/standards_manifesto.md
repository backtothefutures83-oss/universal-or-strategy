# Universal Standards Manifesto: V12 Permanent DNA

This document provides the immutable technical standards for all AI agents (Anthropic, Codex, Antigravity, Cursor, Gemini, Rovo Dev) working on the **Universal OR Strategy V12**.

## 1. Zero-Trust Safety Protocol (The "Law")

- **Verify, Don't Assume:** Never assume the strategy state (for example, `activePositions`) matches the broker state. Always use `FirstOrDefault` when looking up positions in `acct.Positions` using the instrument object identity.
- **Terminal Removal:** Only remove order references from dictionaries (`stopOrders`, `target1Orders`, and so on) after a broker-confirmed terminal state (`Filled`, `Cancelled`, `Rejected`, `Unknown`).
- **Lifecycle Guards:** Any manual flatten or entry must check `isFlattenRunning` to prevent ordering loops.

## 2. Concurrency and Locking

- **StateLock Rule:** Every mutation of `activePositions`, `entryOrders`, `stopOrders`, or `expectedPositions` MUST occur inside a `lock(stateLock)`.
- **Volatile Optimization:** If a variable is marked `volatile` and is being used for a simple read-only check (for example, `if (pos.RemainingContracts > 0)`), remove unnecessary locks to minimize thread contention.
- **Semaphore Guards:** All `_simaToggleSem.Wait()` calls must be paired with a `Release()` inside a `finally` block.

## 3. Coding Style and Modularity

- **Modular Parity:** Maintain the split file structure (for example, `SIMA.cs`, `Orders.Management.cs`). Use `partial class` correctly.
- **Naming:** Use `PascalCase` for Methods/Properties and `camelCase` for fields/locals.
- **Metabolic Elegance:** Prioritize readable, surgical logic over dense one-liners.

## 4. Multi-Agent Collaboration Protocol

- **Advisor (Antigravity):** The **"General Manager."** Handles high-level brainstorming, diagnosing market issues, and engineering the core "Mission Prompt."
- **Desk Supervisor and Lab (Gemini CLI):** The **"Quant and Compliance."** Uses **Conductor/ODIN** to turn Antigravity prompts into rigid technical plans. Uses the **Sandbox** to test math/logic in Python before implementation. Runs local security/lint audits.
- **Lead Engineer (Claude/Sonnet):** The **"Execution Specialist."** Use **Sonnet** (latest) for all implementation. Sonnet is functionally autonomous: it creates its own branches, executes repairs, and **calls for automated audits** by opening PRs. **MANDATORY AUDIT GATE:** Sonnet MUST paste its `implementation_plan.md` to Antigravity for audit and receive explicit Director approval before creating any branch or PR. No branch creation without sign-off.
- **The Loop:** Repair Mission -> Sonnet Handoff -> **Plan to Antigravity for Audit** -> Director Approval -> Branch/Code/Push -> PR Re-Audit Matrix (Opus/Gemini) -> Merge.
- **The Bridge:** Handoffs are managed via `implementation_plan.md`, `session_handoff.md`, and **Mission Brief** artifacts.

## 5. Multi-Agent Parity and Sync Protocol

- **Unified Tooling (MCP):** All MCP servers configured for Claude (`.mcp.json`) must be mirrored in the Gemini CLI (`settings.json`). This ensures that if Claude has access to things like `csharp-lsp` or `tavily-search`, Gemini and Rovo Dev do too.
- **High-Performance Tooling (The Triple Threat):**
  - **Tool Search (Discovery):** Used to find specialized tools on-demand. Prevents token-leak.
  - **Fine-Grained Streaming (Data Flow):** Streams large parameters (for example, mass-refactors) without buffering. Reduces initial latency.
  - **PTC (Execution):** Runs discovered tools at high frequency inside code containers. Reduces round-trips.
- **Context Parity:** The Project DNA in `CLAUDE.md` must be identical to `GEMINI.md`. All agents must read from the same baseline instructions.
- **Environment Sync:** All agents (including Rovo Dev) must have access to the same local environment variables (API keys, project paths) to ensure execution parity.

## 6. Clean-Slate Repo Hygiene (The "Hygiene Rule")

- **Zero-Delta Mandate**: Every new Mission (initialized via `$MISSION`) MUST start with a 0-delta `main` branch. If "Big Numbers" (large uncommitted/unmerged diffs > 100 lines) exist, the agent MUST recommend a cleanup/merge before starting new work.
- **Autonomous Pull Request Handover (The Fresh PR Rule)**: When submitting code for bot audit or human review, agents MUST NEVER push to an existing open Pull Request (for example, updating a dirty branch). Instead, agents MUST:
  1. Checkout a completely new semantic branch (for example, `build/955-audit-remediation`).
  2. Push the new branch and open a BRAND NEW Pull Request targeting `main`.
  3. Close any superseded or legacy PRs via the GitHub CLI, explicitly leaving a comment referencing the new clean PR.
     _Why? Incrementally updating existing PRs can cause automated audit bots (Codex, Greptile, DeepSource) to miss context. A fresh PR triggers a 100% clean, full-file audit sweep._
- **Atomic Missions**: Every bug fix or feature MUST be its own branch and MUST be merged into `main` immediately upon verification (for example, F5 compile in NT8). No "stacking" unrelated fixes in long-lived branches.
- **Binary and Log Purge**: Never commit `.exe`, `.log`, `.bak`, or legacy backup folders to source control. They should be stashed, deleted, or added to `.gitignore`.
- **Dashboard Cleanup**: Before ending a session, the agent MUST ensure all work is either Committed or the user has been guided to Merge. The goal is a +0/-0 dashboard between missions.

## 7. ASCII-Only Encoding Protocol (BUILD SAFETY -- MANDATORY)

**Why this rule exists:** In Build 936, AI agents added Unicode decorators to C# log messages (emoji, em-dashes, curly quotes). A cleanup script converted curly closing-quote `"` (U+201D) to straight `"`, which TERMINATED C# string literals early. One broken quote in `SIMA.cs` caused 300+ cascading compile errors across all partial-class files and cost 2 days of trading time.

**HARD RULE: All C# string literals must use ASCII-only characters.**

| NEVER use in string literals | Use this instead |
| ---------------------------- | ---------------- |
| any emoji character | `(!)` `[OK]` `[X]` `[ERR]` |
| em dash or en dash | `--` |
| curly or smart double quotes | `"` |
| curly apostrophes | `'` |
| arrow characters | `->` `<-` `^` `v` |
| box-drawing characters | `+--+` and `|` |
| ellipsis character | `...` |

**Emergency fix if non-ASCII bytes appear in source:**

1. Run `python <repo_root>\scripts\byte_purge.py` (nuclear byte-level purge)
2. Search all `.cs` files for the pattern `?"` in non-comment lines -- each match is a broken string
3. Replace `?"` with `--` or `(!)` as appropriate
4. Run `deploy-sync.ps1` -- the ASCII gate will confirm clean before touching NT8

---

## 8. MOVE-SYNC / Follower Order Replace Pattern (Build 947+)

**Why this rule exists:** Before Build 947, `PropagateMasterEntryMove` cancelled the follower order and immediately submitted a replacement with no broker confirmation gate. If the cancel was slow or ATR oscillated at a stop-ceiling boundary, the old order and new order were both live simultaneously at the broker -- producing ghost orders, false-flat states, and fill-during-gap desyncs.

**HARD RULE: All follower order cancel+resubmit operations MUST use the two-phase FSM.**

| Rule | Detail |
| ---- | ------ |
| FSM required | Use `_followerReplaceSpecs` dict with `FollowerReplaceState` enum |
| States | `PendingCancel` -> confirm in `OnAccountOrderUpdate` -> `Submitting` -> submit |
| ATR absorption | While `PendingCancel`, update `PendingReplacementSpec` only. One cancel, one resubmit. |
| Fill-during-gap | Before submitting replacement, check if master filled. If yes, route to REAPER repair. |
| ChangeOrder banned | `Account.Change` silently no-ops on Apex/Tradovate. Cancel+resubmit via FSM only. |
| Raw cancel+submit | BANNED. `Cancel()` followed immediately by `Submit()` without FSM = ghost order risk. |

## 9. Live Bug Triage Protocol

**Standard workflow for any live trading anomaly (unexpected cancels, ghost orders, desync):**

1. **Codex first** -- paste log to Codex with 3-5 forensic questions. Code trace only, no patches.
2. **Antigravity analysis** -- evaluate architectural options, confirm root cause, write mission brief.
3. **Sonnet implements** -- plan first, Antigravity audits the plan, then implement.
4. **Role separation:** Codex = diagnosis. Sonnet = implementation. Never reverse these roles.

**Workflow file:** `.agent/workflows/live-bug-triage.md`

---

## 10. Autonomous Evidence Discovery (ALL Agents -- Mandatory)

**Every agent on every platform MUST locate logs and data autonomously. Quote only minimal relevant excerpts from discovered logs when escalating to human review or Codex forensic audit.**

### NT8 Application Logs

```
%USERPROFILE%\Documents\NinjaTrader 8\log\
  log.YYYYMMDD.00000.txt   <- premarket session
  log.YYYYMMDD.00001.txt   <- main session
  log.YYYYMMDD.00002.txt   <- post-restart session
```

### NT8 Trace Logs (Rithmic Adapter -- Low Level)

```
%USERPROFILE%\Documents\NinjaTrader 8\trace\
  trace.YYYYMMDD.00001.txt  <- main session (most detail)
  trace.YYYYMMDD.00002.txt  <- post-restart (order replay here)
```

Contains: `OnLineInfo` (replayed orders), `ReplayDataSeen` (count of replayed orders per account), `OnExecutionUpdate`, connection events.

### SIMA Performance Logs

```
%USERPROFILE%\Documents\NinjaTrader 8\SIMA_Logs\
  ApexPerformance_MES.json  <- ActualQty, ExpectedQty, Balance, Connection per account
  DailySummaries.csv        <- daily P&L
```

### Strategy Source Files

```
<repo_root>\src\
  V12_002.SIMA.cs           <- fleet dispatch, ExecuteSmartDispatchEntry, PumpFleetDispatch
  V12_002.REAPER.cs         <- audit loop, Thread.Sleep(ReaperIntervalMs)
  V12_001.cs                <- reconnectTimer, glowTimer, OnConnectionStatusChange
  V12_002.UI.IPC.cs         <- ReceiveLoop, Thread.Sleep(50/100)
  SignalBroadcaster.cs      <- SafeInvoke, 1ms latency probe
  Orders.Management.cs      <- order cancel/submit/replace lifecycle
```

### Key Search Markers

```
[GHOST-AUDIT]              <- order state mismatch
[REAPER] Repair BLOCKED    <- repair suppressed by Working order
ReplayDataSeen: N accounts <- N orders replayed at reconnect
ExpectedQty != ActualQty   <- SIMA desync
OnLineInfo ... status=open <- live untracked GTC order at broker
```

**Full discovery steps:** See `.agent/workflows/live-bug-triage.md` Section 0.

## 12. Claude Agent Operation Protocol (Usage Insights)

**Based on historical friction data, all agents MUST adhere to these execution constraints:**

- **The "Do Not Interrupt" Protocol:** Agents operating in standard execution mode should complete their logical batches and commit _autonomously_. Do not pause mid-task to ask for user check-ins unless explicitly blocked by a missing file or a hard compilation failure.
- **.NET 4.8 Hardening Hook:** Target framework is .NET 4.8. Do NOT use C# features unavailable in .NET 4.8 (for example, range operators `[..]`, `Index`/`Range` types, default interface implementations). Always use `CultureInfo.InvariantCulture` for numeric parsing. This must be checked before every commit.
- **The "Missing Brief" Failsafe:** Before any phase starts, the Agent MUST verify that the referenced `implementation_plan.md` or `$MISSION` artifact exists on disk. If it does not, the Agent MUST halt and ask the user for the brief, rather than attempting to guess or reverse-engineer the plan via codebase searches.
- **Autonomy Rule (Default to Action):** Agents are empowered and EXPECTED to execute the full end-to-end lifecycle of a task autonomously. This includes:
  1. **Branch Creation**: Create a semantic branch (for example, `fix/sima-dispatch-gate`).
  2. **Surgical Implementation**: Apply changes per the Mission Brief.
  3. **Verification**: Run local tests/ASCII checks (`deploy-sync.ps1`).
  4. **PR Trigger**: Push and open a PR to trigger the **3-Agent Multi-Model Audit (Opus, Sonnet, Gemini)**.
  5. **Merge**: Merge once bot-approvals and human sign-off are received.

---

> [!NOTE]
> This document defines **Permanent Standards**. For current active refactoring goals (for example, Phase 6.0 Simplification), refer to the specific **Implementation Plan** or **Refactoring Roadmap** files.
