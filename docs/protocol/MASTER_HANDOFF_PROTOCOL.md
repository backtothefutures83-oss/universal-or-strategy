# Master Handoff Protocol (MHP) v1.0

This protocol ensures that when switching between AI agents (e.g., from Web Agent to CLI Agent), the "Project DNA" and "Tactical Intent" are preserved with zero slippage.

## Protocol Structure

Every handoff prompt MUST contain the following four pillars:

### 1. Project Intelligence (The Memory)

- **Session Log**: `docs/audits/DIRECTOR_SESSION_LOG.md` (What happened in this session)
- **Command Center**: `README.md` (Where things are)
- **Active Tasks**: `task.md` (What is being done)
- **Plans**: `implementation_plan.md` (How it will be done)

### 2. Environment & Paths (The Map)

- **Root Path**: Absolute path to the repository.
- **Source Code**: Exact location of relevant modules (e.g., `src/`).
- **Artifacts**: Path to the active session's "Brain" folder.

### 3. Tactical Skills (The Tools)

- **Trading DNA**: Knowledge of Apex MAE rules, slippage protection, and NinjaTrader constraints.
- **Workflow References**: Pointers to `.agent/workflows/` or specific `scripts/`.

### 4. Forensic Findings (The Directives)

- Explicit "Call-outs" of specific lines or logical gaps discovered in the current session.
- No-go zones (e.g., "Do not refactor the IPC loop").
- **Virtual Branching**: Use GitButler for all multi-agent lanes to prevent physical file churn that breaks NinjaTrader hard-links (`deploy-sync.ps1`).

## Standard Handoff Template

```markdown
# MISSION: [Task Name]

# ROLE: Project Director / Senior Trading Technologist

### 1. PROJECT INTELLIGENCE

- Root: [Absolute Path]
- Command Center: README.md
- Logic Baseline: docs/audits/DIRECTOR_SESSION_LOG.md

### 2. ARCHITECTURE & PATHS

- Core Logic: [Path to src]
- Critical Files: [List of files]

### 3. TACTICAL DIRECTIVES (FORENSICS)

[Bullet points of specific code gaps/findings]

### 4. GOVERNANCE

- Always use `render_diffs`.
- Save backups as `.bak` for NinjaTrader safety.
- Verify against Phase 4 Implementation Plan.
```
