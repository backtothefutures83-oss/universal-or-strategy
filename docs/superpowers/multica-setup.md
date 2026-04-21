# Multica -- Morpheus Integration Runbook

**Platform**: Multica v0.2.4 (self-hosted)  
**Scope**: Morpheus OS Shell (Process 3 + Agent Manager) — NOT V12/NinjaTrader  
**CLI installed**: `C:\Users\Mohammed Khalid\.multica\bin\multica.exe`

---

## Agent CLI Locations (confirmed)

| Agent                | CLI      | Path                                                      |
| -------------------- | -------- | --------------------------------------------------------- |
| Antigravity / Claude | `claude` | `C:\Users\Mohammed Khalid\AppData\Roaming\npm\claude.cmd` |
| Codex                | `codex`  | `C:\Users\Mohammed Khalid\AppData\Roaming\npm\codex.cmd`  |
| Gemini CLI           | `gemini` | `C:\Users\Mohammed Khalid\AppData\Roaming\npm\gemini.cmd` |
| Jules                | —        | On hold (ENOENT protocol)                                 |

---

## Phase 1 — Sync Fork to Upstream (One-time)

```powershell
cd "C:\WSGTA\multica"
git remote add upstream https://github.com/multica-ai/multica.git
git fetch upstream
git merge upstream/main
git push origin main
```

Keep fork current with the weekly GitHub Action (see `.github/workflows/upstream-sync.yml`).

---

## Phase 2 — Self-Host Server (requires Docker Desktop running)

> **Prerequisite**: Start Docker Desktop, wait for the whale icon to show "Running" in taskbar.

```powershell
# 1. Clone YOUR fork into tools directory (one-time)
git clone https://github.com/mkalhitti-cloud/multica "C:\WSGTA\multica"
cd "C:\WSGTA\multica"

# 2. Start all services (Go backend + Next.js + PostgreSQL via Docker Compose)
make selfhost
# -> Frontend: http://localhost:3000
# -> Backend API: http://localhost:8080
# Login: any email, verification code: 888888

# 3. Verify server is up
Start-Process "http://localhost:3000"
```

---

## Phase 3 -- Configure CLI & Start Daemon

```powershell
# One-command setup (configure localhost + auth + start daemon)
multica setup self-host

# Verify daemon detected all CLIs
multica daemon status
# Expected: shows claude, codex, gemini as detected runtimes
```

### Daemon env overrides (add to system env or daemon invocation)

```powershell
# Pin specific models
$env:MULTICA_CLAUDE_MODEL  = "claude-opus-4-5"
$env:MULTICA_CODEX_MODEL   = "o4-mini"
$env:MULTICA_GEMINI_MODEL  = "gemini-2.5-pro"

# Explicit paths (avoids any PATH lookup ambiguity)
$env:MULTICA_CLAUDE_PATH   = "$env:APPDATA\npm\claude.cmd"
$env:MULTICA_CODEX_PATH    = "$env:APPDATA\npm\codex.cmd"
$env:MULTICA_GEMINI_PATH   = "$env:APPDATA\npm\gemini.cmd"
```

---

## Phase 4 -- Create Workspace & Agent Profiles

1. Open `http://localhost:3000`
2. Create workspace: **`Morpheus-OS`**
3. Create agents (Settings -> Agents -> New Agent):

| Agent Name       | Provider    | Runtime | Role (V12 Director's Gate)   |
| ---------------- | ----------- | ------- | ---------------------------- |
| `Antigravity`    | Claude Code | Local   | P1 ORCHESTRATOR              |
| `Architect`      | Claude Code | Local   | P3 ARCHITECT                 |
| `Codex-Engineer` | Codex       | Local   | P4 ENGINEER (primary)        |
| `Gemini-Auditor` | Gemini      | Local   | P5 AUDITOR / backup engineer |

4. Set workspace root for agent isolation:

```powershell
$env:MULTICA_WORKSPACES_ROOT = "C:\WSGTA\multica_workspaces"
New-Item -ItemType Directory -Force "C:\WSGTA\multica_workspaces"
```

---

## Phase 5 -- Smoke Test (First Issue)

```powershell
# Create test issue from CLI
multica issue create --title "Smoke test: list files in C:\WSGTA\universal-or-strategy\src" --workspace Morpheus-OS

# Assign to Codex-Engineer via web board or:
multica issue list   # find issue ID
multica issue assign <issue-id> --agent Codex-Engineer

# Watch live in board: http://localhost:3000
# Or stream logs:
multica daemon logs -f
```

**Pass criteria**: Issue transitions Open -> In Progress -> Done autonomously.

---

## Day-to-Day Workflow (replaces manual copypaste handoffs)

```
Director creates issue -> assigns to Antigravity
Antigravity posts findings -> creates sub-issue for Architect
Architect posts plan -> creates implementation issue for Codex-Engineer
Codex-Engineer implements -> posts result
Gemini-Auditor reviews if P5 gate required
```

All activity is visible on the board at `http://localhost:3000`.

---

## Stopping / Restarting

```powershell
multica daemon stop

# Stop Docker server
cd "C:\WSGTA\multica"
docker compose down

# Restart
docker compose up -d
multica daemon start
```

---

## References

- SELF_HOSTING.md: https://github.com/multica-ai/multica/blob/main/SELF_HOSTING.md
- CLI_AND_DAEMON.md: https://github.com/multica-ai/multica/blob/main/CLI_AND_DAEMON.md
- Morpheus OS Shell spec: `docs/superpowers/specs/2026-04-09-morpheus-design.md`
- VS-4 embed spec: `docs/superpowers/specs/multica-morpheus-embed.md`
