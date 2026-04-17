# AGENTS.md - Sovereign Agent Protocol

Welcome, Agent. You are operating within the **V12 Universal OR Strategy** repository. This environment is optimized for autonomous multi-agent development under the **Sovereign Droid Protocol (SDP)**.

## 1. Agent Hierarchy (The Director's Gate)

- **ORCHESTRATOR (P1)**: Central Switchboard (Antigravity). Controls context and cross-agent routing.
- **ARCHITECT (P3)**: Strategic Design (**Claude Opus 4.7**). **PLAN-ONLY**. Authored plans reside in `docs/brain/implementation_plan.md`.
- **ENGINEER (P4)**: Implementation (Codex/Jules). Executes surgical edits to `src/`.
- **FORENSICS (P2/P5)**: Diagnosis (P2) and Adversarial Audit (P5).

## 2. Architectural Mandates (THE PLATINUM STANDARD)

- **Lock-Free Actor Pattern**: Legacy `lock(stateLock)` blocks are **STRICTLY BANNED**. All state mutations must use the FSM/Actor `Enqueue` model or atomic primitives.
- **ASCII-Only Compliance**: NEVER use Unicode, emoji, or curly quotes in C# string literals.
- **Hard-Link Integrity**: Every `src/` modification MUST be followed by `powershell -File .\deploy-sync.ps1` to re-synchronize NinjaTrader hard links.

## 3. Standard Commands

- **Build & Sync** (Build Pillar): `powershell -File .\scripts\build_readiness.ps1`
- **Lint Audit** (Style Pillar): `powershell -File .\scripts\lint.ps1`
- **Stress Test** (Testing Pillar): `powershell -File .\scripts\test_stress.ps1`
- **Sovereign Audit**: `droid /review` (Focus on P0-P3 severity findings).
- **Readiness Check**: `droid /readiness-report` (Maintain Level 2+).
- **Forensic Scan**: `grep -r "lock(" src/` (Zero-match requirement).

## 4. Communication & Context

- **Active Task**: Always check `docs/brain/task.md` before initiating work.
- **Handoffs**: Use the `docs/brain/nexus_a2a.json` via the **Nexus Bridge** for inter-agent state synchronization.

## 5. Karpathy Behavioral Protocols (LLM Coding Hygiene)

> Derived from Andrej Karpathy's observations on LLM coding pitfalls.
> Every agent operating in this repo MUST apply these principles.

### Think Before Coding

- State assumptions explicitly. If uncertain, ASK -- do not silently pick an interpretation.
- If multiple interpretations exist, surface them to the Director before proceeding.
- If a simpler approach exists, say so. Push back when warranted.

### Simplicity First

- Minimum code that solves the problem. Nothing speculative.
- No features beyond what was asked. No abstractions for single-use code.
- If 200 lines could be 50, rewrite it before submission.

### Surgical Changes

- Touch only what you must. Clean up only your own mess.
- Do NOT "improve" adjacent code, comments, or formatting.
- If unrelated dead code is noticed, REPORT it -- do not act on it.
- Every changed line must trace directly to the Mission Brief.

### Goal-Driven Execution

- State verify criteria before each implementation stage:
  1. [Step] -> verify: [check]
  2. [Step] -> verify: [check]
- Strong success criteria let you loop independently. "Make it work" is not a criterion.

## Graphify Protocols (Universal Knowledge Layer)

- **Check First**: Before deep architectural exploration, always check for `graphify-out/graph.json` or `graphify-out/GRAPH_REPORT.md`.
- **Update**: Use `graphify update .` to refresh the repo knowledge graph after major structural changes.
- **Efficiency**: Use the graph to navigate codebase relationships with 71x fewer tokens than raw file reading.
