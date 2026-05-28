---
title: "[GitHub #28] feat: TypeScript CLI with ACP orchestration (ROUTA→CRAFTER→GATE pipeline)"
date: "2026-02-27"
status: resolved
severity: medium
area: "github"
tags: ["github", "github-sync", "gh-28"]
reported_by: "phodal"
related_issues: ["https://github.com/phodal/routa/issues/28"]
github_issue: 28
github_state: "closed"
github_url: "https://github.com/phodal/routa/issues/28"
---

# [GitHub #28] feat: TypeScript CLI with ACP orchestration (ROUTA→CRAFTER→GATE pipeline)

## Sync Metadata

- Source: GitHub issue sync
- GitHub Issue: #28
- URL: https://github.com/phodal/routa/issues/28
- State: closed
- Author: phodal
- Created At: 2026-02-27T13:47:58Z
- Updated At: 2026-02-27T13:47:58Z

## Labels

- (none)

## Original GitHub Body

## Overview

Build a TypeScript-based CLI for Routa that acts as an ACP orchestrator — managing multiple ACP agents (ROUTA, CRAFTER, GATE) to fulfill user requests from the terminal. The primary interface is:

```bash
routa acp -p "add a login page with email/password validation"
```

This triggers the full ROUTA→CRAFTER→GATE pipeline: ROUTA plans and delegates, CRAFTER implements, GATE verifies.

## Motivation

The web UI already manages multiple ACP services visually. The CLI should expose the same orchestration power for terminal-first workflows, CI/CD pipelines, and scripted automation — without requiring a browser or running server.

## Tech Stack Reference

Modeled after [gemini-cli](https://github.com/google-gemini/gemini-cli):

| Concern | Technology |
|---|---|
| Language | TypeScript (ESM, Node ≥ 20) |
| Monorepo | npm workspaces |
| TUI rendering | [Ink](https://github.com/vadimdemedes/ink) (React for terminal) |
| Arg parsing | `yargs` |
| Streaming | `AsyncGenerator` over ACP SSE/stdio |
| Bundling | `esbuild` → `bundle/routa.js` |
| Testing | `vitest` |

## Package Structure

```
packages/
  cli/          # TUI, arg parsing, interactive REPL, non-interactive runner
  core/         # ACP client, orchestrator, session store, config, tools
  sdk/          # Public programmatic API
```

## Core Commands

### `routa acp -p "<prompt>"` — Single-shot orchestration (primary use case)

```bash
routa acp -p "refactor the auth module to use JWT"
routa acp -p "add unit tests for UserService" --provider opencode
routa acp -p "review PR #42 changes" --role GATE
routa acp --prompt "implement dark mode" --cwd ./frontend --model claude-sonnet
```

Flags:
- `-p / --prompt` — the task description (required)
- `--provider` — ACP agent binary: `opencode`, `gemini`, `claude`, `codex`, `kiro`, etc. (default: from config)
- `--role` — entry role: `ROUTA` (default), `DEVELOPER`, `CRAFTER`, `GATE`
- `--cwd` — working directory for agents (default: `process.cwd()`)
- `--model` — override model for the session
- `--no-gate` — skip GATE verification step
- `--output-format` — `text` (default) | `json` | `stream-json`
- `--session` — resume an existing session by ID

### `routa acp` — Interactive REPL mode (no `-p` flag)

Launches a full-screen Ink TUI:
- Left panel: session list + agent tree (ROUTA → CRAFTER children → GATE)
- Center: streaming message feed with role badges
- Bottom: input composer
- Slash commands: `/agents`, `/tasks`, `/status`, `/sessions`, `/clear`, `/quit`

### Other commands (parity with Rust CLI)

```bash
routa session list
routa session get <id>
routa agent list --workspace-id <id>
routa task list --workspace-id <id>
routa workspace list
routa workspace create --name <name>
routa skill list
routa config set <key> <value>
routa config get <key>
```

## Architecture

### `packages/core`

**`AcpClient`** — wraps ACP JSON-RPC over stdio or HTTP/SSE:
- `initialize()` → handshake
- `newSession(cwd, provider, role)` → `sessionId`
- `prompt(sessionId, text)` → `AsyncGenerator<AcpEvent>`
- `cancel(sessionId)`

**`RoutaOrchestrator`** — mirrors `src/core/orchestration/orchestrator.ts`:
- Spawns ROUTA session, streams its output
- Detects `delegate_task` MCP tool calls → spawns CRAFTER child sessions
- Detects CRAFTER completion → spawns GATE session for verification
- Emits structured `OrchestratorEvent` stream consumed by the TUI

**`SessionStore`** — in-memory + optional SQLite persistence:
- Tracks active sessions, message history, agent tree
- Supports `--resume` flag

**`ConfigManager`** — three-tier config:
1. `~/.routa/config.json` (user global)
2. `.routa/config.json` (project-level)
3. CLI flags (highest priority)

Config keys: `defaultProvider`, `defaultModel`, `defaultRole`, `acpTimeout`, `maxDelegationDepth`.

**`AcpPresets`** — ported from `src/core/acp/acp-presets.ts`, resolves agent binary + args per provider name.

### `packages/cli`

**`main.ts`** — entry point:
1. Parse args via `yargs`
2. Load config
3. If `-p` flag → `runNonInteractive(prompt, options)`
4. Else → `startInteractiveUI()`

**`runNonInteractive()`**:
- Creates `RoutaOrchestrator`
- Consumes `AsyncGenerator<OrchestratorEvent>` 
- Renders progress to stdout (role badges, streaming text, task status)
- Exits with code 0 (success) or 1 (GATE rejected / error)

**`startInteractiveUI()`**:
- Renders Ink component tree
- `<OrchestratorView>` — main layout
- `<AgentTree>` — live agent hierarchy with status indicators
- `<MessageFeed>` — streaming messages with role-colored prefixes
- `<TaskPanel>` — task list with status badges
- `<Composer>` — input box with slash command support

## TUI Layout (Interactive Mode)

```
┌─ Routa ──────────────────────────────────────────────────────────┐
│ Agents                    │ Session: implement-auth-2024-01-15   │
│ ● ROUTA (planning)        │                                       │
│   ├─ ● CRAFTER-1 (coding) │ [ROUTA] Analyzing requirements...    │
│   └─ ○ GATE (waiting)     │ [ROUTA] Creating task plan:          │
│                           │   @@@task Implement JWT middleware    │
│ Tasks                     │   @@@task Add /auth/login endpoint    │
│ ○ JWT middleware           │                                       │
│ ○ Login endpoint          │ [CRAFTER] Writing src/middleware/...  │
│ ✓ Schema migration        │ [CRAFTER] ▊                          │
│                           │                                       │
│ Workspace: my-app         │                                       │
├───────────────────────────┴───────────────────────────────────────┤
│ > /tasks                                                          │
└───────────────────────────────────────────────────────────────────┘
```

## Non-Interactive Output Format

```
$ routa acp -p "add input validation to the signup form"

[ROUTA] Planning task...
[ROUTA] Spec written to .routa/tasks/task-001.md
[ROUTA] Delegating to CRAFTER (opencode)...

[CRAFTER] Reading src/components/SignupForm.tsx...
[CRAFTER] Editing src/components/SignupForm.tsx
[CRAFTER] Running: npm test -- SignupForm
[CRAFTER] Tests passed. Reporting to ROUTA.

[GATE] Reviewing changes against acceptance criteria...
[GATE] ✓ Email format validation present
[GATE] ✓ Password min-length enforced
[GATE] ✓ Error messages displayed inline
[GATE] APPROVED

✓ Task completed in 3 steps (ROUTA → CRAFTER → GATE)
  Session: ses_abc123  |  Tasks: 1 completed
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Task completed, GATE approved (or no GATE) |
| 1 | GATE rejected / agent error |
| 2 | Config / auth error |
| 130 | Interrupted (Ctrl+C) |

## Configuration File (`~/.routa/config.json`)

```json
{
  "defaultProvider": "opencode",
  "defaultModel": "claude-sonnet",
  "defaultRole": "ROUTA",
  "acpTimeout": 300000,
  "maxDelegationDepth": 2,
  "gate": {
    "enabled": true,
    "provider": "opencode"
  },
  "specialists": {
    "ROUTA": "~/.routa/specialists/routa.md",
    "CRAFTER": "~/.routa/specialists/crafter.md",
    "GATE": "~/.routa/specialists/gate.md"
  }
}
```

## Implementation Phases

### Phase 1 — Core + Non-interactive
- [ ] `packages/core`: `AcpClient`, `AcpPresets`, `ConfigManager`
- [ ] `packages/core`: `RoutaOrchestrator` with event streaming
- [ ] `packages/cli`: `main.ts`, yargs setup, `runNonInteractive()`
- [ ] Plain-text progress output with role badges
- [ ] `routa acp -p "..."` works end-to-end

### Phase 2 — Interactive TUI
- [ ] Ink component tree: `OrchestratorView`, `AgentTree`, `MessageFeed`, `Composer`
- [ ] Slash commands: `/agents`, `/tasks`, `/status`, `/quit`
- [ ] Session persistence + `--resume`

### Phase 3 — Full parity
- [ ] `routa session`, `routa agent`, `routa task`, `routa workspace` commands
- [ ] `routa config` management
- [ ] `--output-format json` for scripting
- [ ] `packages/sdk` public API

## References

- Web orchestrator: `src/core/orchestration/orchestrator.ts`
- ACP presets: `src/core/acp/acp-presets.ts`
- ACP process: `src/core/acp/acp-process.ts`
- Specialist prompts: `src/core/orchestration/specialist-prompts.ts`
- Rust CLI (reference): `crates/routa-cli/src/`
- gemini-cli architecture: https://github.com/google-gemini/gemini-cli
