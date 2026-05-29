# Three-Tier Branch Model (V12.18)

## Overview
Separates changes by **architectural layer** to prevent PR noise and enable independent review cycles.

---

## Tier 1: Source Code Branches (`feature/src-*`, `fix/src-*`)

**Purpose**: Production code changes only

**Allowed Files:**
- ✅ `.cs` files in `src/` directory
- ✅ `.csproj` files (project structure)
- ✅ Test files in `tests/` (if test-only changes)

**Forbidden:**
- ❌ Any `docs/`, `scripts/`, `.github/` files
- ❌ Config files (`.yml`, `.json`, `.toml`)
- ❌ Markdown files
- ❌ PowerShell/Python scripts

**Review Process:**
- Full Arena AI + Codacy + CodeRabbit review
- Complexity audit required
- Build + test gates enforced
- Longest review cycle (2-3 days typical)

**Example branches:**
- `feature/src-epic-8-extract-sima`
- `fix/src-null-reference-atm`

---

## Tier 2: Infrastructure Branches (`feature/infra-*`, `fix/infra-*`)

**Purpose**: Tooling, scripts, documentation, configs

**Allowed Files:**
- ✅ `docs/` (all markdown, images, PDFs)
- ✅ `scripts/` (PowerShell, Python, Shell)
- ✅ `.github/` (workflows, actions, templates)
- ✅ Config files (`.codacy.yml`, `.editorconfig`, etc.)
- ✅ `docs/brain/` (session notes, forensics)

**Forbidden:**
- ❌ `.cs` files in `src/`

**Review Process:**
- Lightweight review (no complexity audit)
- Markdown linting only
- Fast-track merge (same day typical)
- No bot review unless security-sensitive

**Example branches:**
- `feature/infra-pr18-fixes`
- `fix/infra-broken-deploy-script`

---

## Tier 3: Protocol Branches (`feature/protocol-*`, `fix/protocol-*`)

**Purpose**: Agent rules, workflows, meta-configuration

**Allowed Files:**
- ✅ `.bob/`, `.agent/`, `.claude/`, `.gemini/` directories
- ✅ `AGENTS.md`, `CLAUDE.md`, `BOB.md`, `CODEX.md`
- ✅ `docs/protocol/` (protocol documentation)
- ✅ `.mcp.json`, `bob.config.yaml`

**Forbidden:**
- ❌ `.cs` files in `src/`
- ❌ General scripts (unless protocol-related)

**Review Process:**
- Director review only (no bots)
- Immediate merge after approval
- Critical path for agent behavior changes

**Example branches:**
- `feature/protocol-branch-guard`
- `fix/protocol-bob-mode-enforcement`

---

## Workflow

### Scenario 1: Pure Source Code Work
```
1. Create feature/src-epic-X
2. Bob IDE works here (ONLY .cs commits)
3. PR review (full bot suite)
4. Merge to main
```

### Scenario 2: Infrastructure + Source Code (Same Epic)
```
1. Create feature/infra-epic-X (infrastructure first)
2. Merge infra branch (fast-track)
3. Create feature/src-epic-X (source code)
4. Merge src branch (full review)
```

### Scenario 3: Protocol Fix During Source Work
```
1. Working on feature/src-epic-X
2. Bob makes mistake → protocol needs update
3. STOP src work
4. Create feature/protocol-fix-issue
5. Fix protocol, merge immediately
6. Resume src work on original branch
```

### Scenario 4: Emergency Hotfix (Mixed)
```
1. Create hotfix/critical-issue
2. Mixed commits allowed BUT:
   - Separate commits: [SRC] and [INFRA] prefixes
   - Document reason in commit message
3. Create follow-up tickets to separate properly
```

---

## Benefits

**Token Efficiency:**
- Infra PRs: ~500 tokens (no bot review)
- Src PRs: ~5,000 tokens (full review)
- Savings: 90% on infra changes

**Review Speed:**
- Infra: Same day merge
- Src: 2-3 day review cycle
- Protocol: Immediate (Director only)

**Git History Clarity:**
- Branch name = intent obvious
- No "mixed bag" commits
- Easy to revert by layer

**Jane Street Alignment:**
- Separation of concerns
- Predictable change scope
- Reduced cognitive load

---

## Enforcement

**Branch Guard Rule:** `.bob/rules-v12-engineer/branch-guard.md`
- Auto-blocks mixed commits
- Auto-stashes violating files
- Logs violations

**Git Hooks:** (optional, not yet implemented)
- Pre-commit validation
- Branch pattern matching

**Bob Shell/IDE Isolation:**
- Bob IDE → src branches only
- Bob Shell → infra/protocol branches only
- Separate terminal windows recommended

---

## Current Status

**Implemented:**
- ✅ Branch guard rule created (`.bob/rules-v12-engineer/branch-guard.md`)
- ✅ Three-tier model documented

**Pending:**
- ⏳ Git pre-commit hooks (optional)
- ⏳ Fine-tuning as we use it in practice

**Effective Date:** 2026-05-29 (V12.18)
