# Universal Standards Manifesto: V12 Permanent DNA

This document provides the immutable technical standards for all AI agents (Anthropic, Codex, Antigravity, Cursor, Gemini, Rovo Dev) working on the **Universal OR Strategy V12**.

## 1. Zero-Trust Safety Protocol (The "Law")
*   **Verify, Don't Assume:** Never assume the strategy state (e.g., `activePositions`) matches the broker state. Always use `FirstOrDefault` when looking up positions in `acct.Positions` using the instrument object identity.
*   **Terminal Removal:** Only remove order references from dictionaries (`stopOrders`, `target1Orders`, etc.) after a broker-confirmed terminal state (`Filled`, `Cancelled`, `Rejected`, `Unknown`).
*   **Lifecycle Guards:** Any manual flatten or entry must check `isFlattenRunning` to prevent ordering loops.

## 2. Concurrency & Locking
*   **StateLock Rule:** Every mutation of `activePositions`, `entryOrders`, `stopOrders`, or `expectedPositions` MUST occur inside a `lock(stateLock)`.
*   **Volatile Optimization:** If a variable is marked `volatile` and is being used for a simple "read-only" check (e.g., `if (pos.RemainingContracts > 0)`), remove unnecessary locks to minimize thread contention.
*   **Semaphore Guards:** All `_simaToggleSem.Wait()` calls must be paired with a `Release()` inside a `finally` block.

## 3. Coding Style & Modularity
*   **Modular Parity:** Maintain the split file structure (e.g., `SIMA.cs`, `Orders.Management.cs`). Use `partial class` correctly.
*   **Naming:** Use `PascalCase` for Methods/Properties, `camelCase` for fields/locals.
*   **Metabolic Elegance:** Prioritize readable, surgical logic over dense one-liners.

## 4. Multi-Agent Collaboration Protocol
*   **Advisor (Antigravity):** The **"General Manager."** Handles high-level brainstorming, diagnosing market issues, and engineering the core "Mission Prompt."
*   **Desk Supervisor & Lab (Gemini CLI):** The **"Quant & Compliance."** Uses **Conductor/ODIN** to turn Antigravity prompts into rigid technical plans. Uses the **Sandbox** to test math/logic in Python before implementation. Runs local security/lint audits.
*   **Lead Engineer (Claude Code):** The **"Execution Specialist."** Performs high-speed implementing with **Simultaneous Auditing**.
*   **Forensic Auditor (Codex):** The **"Deep Logic Inspector."** Performs a 360-degree forensic scan for logic traps/leaks.
*   **Secondary Auditor (Cursor AI):** The **"Peer Reviewer."** Performed by the Cursor agent to provide a cross-verification audit from a different model perspective.
*   **Maintenance Inspector (Human):** The **"Final Sign-off."** Used by the Fund Manager in Cursor to perform visual review and manual polish.
*   **Context Layer (OneContext):** The **"Order Book."** Maintains a shared trajectory of all agent thoughts and actions.
*   **The Bridge:** Handoffs are managed via `implementation_plan.md`, `session_handoff.md`, and **OneContext snapshots**.

## 5. Multi-Agent Parity & Sync Protocol
*   **Unified Tooling (MCP):** All MCP servers configured for Claude (`.mcp.json`) must be mirrored in the Gemini CLI (`settings.json`). This ensures that if Claude has access to things like `csharp-lsp` or `tavily-search`, Gemini and Rovo Dev do too.
*   **High-Performance Tooling (The Triple Threat):** 
    - **Tool Search (Discovery):** Used to find specialized tools on-demand. Prevents token-leak.
    - **Fine-Grained Streaming (Data Flow):** Streams large parameters (e.g., mass-refactors) without buffering. Reduces initial latency.
    - **PTC (Execution):** Runs discovered tools at high frequency inside code containers. Reduces round-trips.
*   **Context Parity:** The Project DNA in `CLAUDE.md` must be identical to `GEMINI.md`. All agents must read from the same baseline instructions.
*   **Environment Sync:** All agents (including Rovo Dev) must have access to the same local environment variables (API keys, project paths) to ensure execution parity.

## 6. Clean-Slate Repo Hygiene (The "Hygiene Rule")
*   **Zero-Delta Mandate**: Every new Mission (initialized via `$MISSION`) MUST start with a 0-delta `main` branch. If "Big Numbers" (large uncommitted/unmerged diffs > 100 lines) exist, the agent MUST recommend a cleanup/merge before starting new work.
*   **Atomic Missions**: Every bug fix or feature MUST be its own branch and MUST be merged into `main` immediately upon verification (e.g. F5 compile in NT8). No "stacking" unrelated fixes in long-lived branches.
*   **Binary & Log Purge**: Never commit `.exe`, `.log`, `.bak`, or legacy backup folders to source control. They should be stashed, deleted, or added to `.gitignore`.
*   **Dashboard Cleanup**: Before ending a session, the agent MUST ensure all work is either Committed or the user has been guided to Merge. The goal is a +0/-0 dashboard between missions.

## 7. ASCII-Only Encoding Protocol (BUILD SAFETY — MANDATORY)

**Why this rule exists:** In Build 936, AI agents added Unicode decorators to C# log messages (emoji, em-dashes, curly quotes). A cleanup script converted curly closing-quote `"` (U+201D) to straight `"`, which TERMINATED C# string literals early. One broken quote in `SIMA.cs` caused 300+ cascading compile errors across all partial-class files and cost 2 days of trading time.

**HARD RULE: All C# string literals must use ASCII-only characters.**

| NEVER use in string literals | Use this instead |
|---|---|
| `⚠️` `✅` `❌` `🔴` (any emoji) | `(!)` `[OK]` `[X]` `[ERR]` |
| `—` `–` (em/en dash) | `--` |
| `"` `"` (curly/smart quotes) | `"` (straight double quote) |
| `'` `'` (curly apostrophes) | `'` (straight apostrophe) |
| `→` `←` `↑` `↓` (arrows) | `->` `<-` `^` `v` |
| `╔═╗ ║` (box-drawing chars) | `+--+ |` |
| `…` (ellipsis) | `...` |

**Emergency fix if non-ASCII bytes appear in source:**
1. Run `python C:\tmp\byte_purge.py` (nuclear byte-level purge)  
2. Search all `.cs` files for the pattern `?"` in non-comment lines — each match is a broken string
3. Replace `?"` with `--` or `(!)` as appropriate
4. Run `deploy-sync.ps1` — the ASCII gate will confirm clean before touching NT8

---

> [!NOTE]
> This document defines **Permanent Standards**. For current active refactoring goals (e.g. Phase 6.0 Simplification), refer to the specific **Implementation Plan** or **Refactoring Roadmap** files.
