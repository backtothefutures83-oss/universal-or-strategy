# QWEN.md — Universal OR Strategy V12

## Project Overview

**Universal OR Strategy V12** is a modular, algorithmic trading strategy built for **NinjaTrader 8** using C# (.NET 4.8 for the strategy, .NET 8.0 for tests/tooling). It evolved from a legacy "Opening Range Breakout (ORB)" monolith into a **dual-plane execution engine**:

- **V12 Photon Kernel** (Upper Plane): Modularized, high-fidelity execution inside NinjaTrader 8 using the FSM/Actor `Enqueue` model. Targets .NET 4.8 / C# 8.0.
- **Morpheus Substrate** (Lower Plane): Cross-process, lock-free architecture for autonomous scaling, telemetry, and broker integrations. Targets .NET 8.0.

The strategy supports multiple entry modes (Opening Range, Trend, RMA, FFMA, MOMO, Retest) orchestrated through a **SIMA** (Smart Intelligent Money Allocation) system with multi-account fleet management, bracket order FSM protection, trailing stops, and a REAPER defensive watchdog layer.

### Current Status
- **Build**: `1111.007` (Phase 7 Structural Hardening COMPLETE — Platinum Pass)
- **Platinum Standard**: Zero methods with cyclomatic complexity > 20 across all 817 methods
- **273-test integration suite** covering 7 architectural clusters (S1–S7)

---

## Directory Structure

| Folder | Purpose |
|---|---|
| `src/` | Core C# strategy source — V12 Photon Kernel (72 files, modularized partial classes) |
| `tests/` | xUnit + FsCheck integration tests (.NET 8.0) — 273 tests across 7 clusters |
| `docs/` | Architecture diagrams, audit reports, battle prompts, risk management docs |
| `docs/brain/` | Shared AI memory — task tracking, implementation plans, handoff state, forensic reports |
| `docs/protocol/` | Workflow DNA, handoff protocols, agent guides |
| `scripts/` | PowerShell + Python automation: build, lint, deploy, stress tests, complexity audits |
| `bin/` | Executables and binary tools (Auditors, CLI) |
| `.bob/` | Bob CLI configuration: custom modes (`v12-engineer`, `v12-epic-planner`, `v12-forensics`, `v12-phase7-lead`), rules, settings |
| `graphify-out/` | Knowledge graph output (2256 nodes, 6525 edges, 115 communities) |
| `conductor/` | Agent orchestration infrastructure |
| `plugins/`, `tools/` | Supporting tooling and plugins |
| `_agents/`, `_arena_zips/`, `.agent/`, `.agents/` | Multi-agent infrastructure and battle artifacts |

### Key Source File Naming Convention

All strategy files follow the `V12_002.<Domain>.cs` partial class pattern:

| Domain | Files |
|---|---|
| **S1 SIMA** | `V12_002.SIMA.cs`, `.Dispatch.cs`, `.Fleet.cs`, `.Lifecycle.cs`, `.Execution.cs`, `.Flatten.cs`, `.Shadow.cs` |
| **S2 Execution** | `V12_002.Orders.Callbacks*.cs`, `.Symmetry*.cs`, `.Trailing*.cs`, `.Orders.Management*.cs` |
| **S3 UI/IPC** | `V12_002.UI.*.cs` (Callbacks, Compliance, IPC, Panel, Sizing, Snapshot) |
| **S4 REAPER** | `V12_002.REAPER.*.cs`, `V12_002.Safety.Watchdog.cs` |
| **S5 Kernel** | `V12_002.StickyState.cs`, `.Lifecycle.cs`, `.Telemetry.cs`, `.Properties.cs` |
| **S6 Signals** | `V12_002.Entries.*.cs` (Trend, OR, RMA, FFMA, MOMO, Retest) |
| **S7 Infra** | `V12_002.cs`, `.BarUpdate.cs`, `.AccountUpdate.cs`, `.Atm.cs`, `.Data.cs`, `.DrawingHelpers.cs` |
| **S8 Photon IO** | `V12_002.Photon.Ring.cs`, `.Pool.cs`, `.MmioMirror.cs`, `.MetadataGuard.cs` |

---

## Building and Running

### Deployment to NinjaTrader 8

The repo uses **hard links** to sync source files to NinjaTrader 8's `bin/Custom` directory. After any `src/` modification:

```powershell
powershell -File .\deploy-sync.ps1
```

This script runs pre-deploy gates:
1. **ASCII Gate** — scans all `.cs` files for non-ASCII bytes; aborts if found
2. **DIFF Guard** — checks diff size against `main` (limit: 150,000 characters)
3. **Test Gate** — runs xUnit/FsCheck test suite; aborts on failure
4. **Sovereign Audit** — runs Droid CLI P5 review (if available)

Then compile in NinjaTrader 8 with **F5**.

### Standard Commands

| Command | Purpose |
|---|---|
| `powershell -File .\scripts\build_readiness.ps1` | Build & sync readiness check |
| `powershell -File .\scripts\lint.ps1` | Lint audit (style pillar) |
| `powershell -File .\scripts\test_stress.ps1` | Stress test (testing pillar) |
| `powershell -File .\scripts\audit_scan.ps1` | Executive audit scan for logic risks |
| `python scripts/complexity_audit.py` | Cyclomatic complexity analysis |
| `droid /review` | Sovereign AI audit (P0–P3 findings) |
| `droid /readiness-report` | Readiness level check (maintain Level 2+) |
| `graphify update .` | Refresh knowledge graph after structural changes |

### Test Execution

Tests use **xUnit** + **FsCheck** targeting .NET 8.0:

```powershell
dotnet test tests\V12.Sima.Tests.csproj -c Release
```

Test clusters: `SIMAIntegrationTests`, `ExecutionEngineIntegrationTests`, `UIPhotonIOIntegrationTests`, `REAPERDefenseIntegrationTests`, `ConfigurationIntegrationTests`, `MetricsIntegrationTests`, `OrchestrationIntegrationTests`.

---

## Development Conventions

### Architectural Mandates (Platinum Standard)

1. **Correctness by Construction**: Structure types/enums so illegal states are unrepresentable at compile time. Do not rely on runtime if/else guards.
2. **Lock-Free Actor Pattern**: `lock(stateLock)` blocks are **STRICTLY BANNED**. All state mutations must use the FSM/Actor `Enqueue` model or atomic primitives (`Interlocked.*`, `Volatile.*`, `Channel<T>`).
3. **ASCII-Only Compliance**: NEVER use Unicode, emoji, curly quotes, em-dashes, or box-drawing characters in C# string literals. Use ASCII equivalents only.
4. **Hard-Link Integrity**: Every `src/` change MUST be followed by `deploy-sync.ps1`.

### Code Exploration Policy

Use **jCodemunch-MCP** tools for all code navigation:
- Start sessions with `resolve_repo { "path": "." }` then `suggest_queries`
- Find symbols: `search_symbols` (with `kind=`, `language=`, `file_pattern=`, `decorator=` filters)
- Search text: `search_text` (regex, context_lines)
- Read outlines: `get_file_outline` before any file read
- Impact analysis: `get_blast_radius`, `get_dependency_graph`, `find_references`
- After edits: `register_edit` with modified file paths

Only use `Read` when you need to edit a file (the harness requires it before `Edit`/`Write`).

### Surgical Change Protocol

- Touch only what you must. Clean up only your own mess.
- **WHITESPACE MUTATION BANNED**: Never mutate whitespace, line endings, or indentation across files.
- **DIFF LIMIT**: PR diffs must stay under 150,000 characters.
- Report unrelated dead code — do not act on it.
- Every changed line must trace directly to the task at hand.

### Qwen CLI Native Checkpointing & Recovery

- **Automatic Snapshots**: When running Qwen Code with `--checkpointing` (or when `"general.checkpointing.enabled": true` is set in global `settings.json`), Qwen CLI automatically takes shadow Git snapshots under `~/.qwen/history/` and saves your conversation states before any tool-based file edits are executed.
- **Mid-Task Recovery**: If Qwen CLI crashes or hits a usage limit during a complex step (like `/epic-tdd` Stage 2), the session can be perfectly restored to the last tool execution by listing and running:
  ```bash
  /restore
  /restore <checkpoint_file>
  ```
- **Milestone Persistence**: Always write intermediate progress (draft code, verified logs, forensic reports) to physical files in the project workspace (under `docs/brain/`) at the end of each stage. This ensures complete continuity across multi-agent handoffs, even if an active session is cut off or the model becomes completely unreachable.
- **Token Conservation Protocol**:
  1. **Banish Active Polling Loops**: Do not repeatedly prompt, sleep-loop, or poll file paths to wait for a concurrent sub-agent.
  2. **Event-Driven IPC**: Utilize `--json-file` / `--json-fd` and `--input-file` flags to let external Node.js / Bun sidecars or local FS watchers orchestrate the state changes. The Orchestrator MUST sleep/yield until the sidecar triggers the input channel.
  3. **Decoupled Sequential Sweeps**: For sequential sweeps (like `/bug-bounty`), dispatch the entire chain to a single OS shell script (PowerShell / Bash). Yield the turn immediately, waking up exactly ONCE at the end of the script execution to consolidate reports.

### Karpathy Behavioral Protocols

- State assumptions explicitly. If uncertain, ASK.
- Minimum code that solves the problem — nothing speculative.
- If 200 lines could be 50, rewrite it.
- Define verify criteria before each implementation stage.

### Agent Workflow (Sovereign Droid Protocol)

This repo is optimized for autonomous multi-agent development. Key hierarchy:

| Role | Agent | Scope |
|---|---|---|
| **Orchestrator (P1)** | Antigravity / Gemini CLI | Central routing |
| **Architect + Engineer (P3–P5)** | Bob CLI (`v12-engineer`) | All `src/` work |
| **Engineer (non-src)** | Jules AI / Gemini CLI | GitHub workflows, local utility |
| **Forensics (P2/P6)** | Bob (`v12-forensics`) | Adversarial audit |
| **Adjudicator** | Arena AI | PR audit, adversarial consensus |

Before starting work, check `docs/brain/task.md` for the active mission state.

### File Header Convention

C# files use standard copyright headers:

```csharp
// <copyright file="V12_002.Constants.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
```

Namespace: `NinjaTrader.NinjaScript.Strategies`

---

## Key Reference Files

| File | Purpose |
|---|---|
| `AGENTS.md` | Sovereign Agent Protocol — hierarchy, mandates, commands, Phase 6 recursive protocol |
| `README.md` | Project command center — architecture overview, shared AI memory links |
| `docs/architecture.md` | Full dual-plane architecture map with Mermaid diagrams and complexity heatmap |
| `docs/brain/task.md` | Active mission dashboard — current BUILD_TAG, sprint status, next steps |
| `docs/brain/implementation_plan.md` | Active surgical implementation steps |
| `docs/brain/nexus_a2a.json` | Inter-agent state synchronization bridge |
| `deploy-sync.ps1` | Hard-link deployment script with ASCII/DIFF/Test gates |
| `IDE_GUIDE.md` | IDE alignment guide for Cursor, Claude, Codex, NinjaTrader |
| `bob.config.yaml` | Bob CLI defaults (advanced mode, auto-apply, checkpointing) |
| `.bob/custom_modes.yaml` | Bob persona definitions (v12-engineer, v12-epic-planner, v12-forensics, v12-phase7-lead) |
| `graphify-out/GRAPH_REPORT.md` | Knowledge graph — 2256 nodes, 115 communities for codebase navigation |
| `docs/protocol/INSTITUTIONAL_WORKFLOW_DNA.md` | Zero-trust workflow psychology |
| `docs/protocol/MASTER_HANDOFF_PROTOCOL.md` | Agent transition protocol |

---

## Environment Details

- **OS**: Windows
- **Repo Path**: `C:\WSGTA\universal-or-strategy`
- **NT8 Custom Dir**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom`
- **Source of Truth**: `C:\WSGTA\universal-or-strategy\src\` (never OneDrive paths)
- **Branch**: `main`
- **Remote**: `mkalhitti-cloud/universal-or-strategy`
