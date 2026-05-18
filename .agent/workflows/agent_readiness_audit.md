# Workflow: Agent Readiness Audit (Level 6)

This workflow implements the **Agent Readiness Auditor & Hardener** protocol to graduate a repository to a **Sovereign Substrate (Level 6)**.

## 1. Initialization (P1/P2)
- [ ] **Discover Denominators**: 
    - Identify Repo Root.
    - Identify IDUs (Independently Deployable Units).
    - Calculate $N$ (Number of Apps).
- [ ] **Scan Configuration**: Run `ls -R` to find all `package.json`, `pyproject.toml`, `.csproj`, and `.github/workflows/`.

## 2. Signal Evaluation (P2)
- [ ] **Execute 82-Gate Audit**: 
    - Category A: Style & Validation (Linter, Formatter, Pre-commit).
    - Category B: Build System (AGENTS.md, Caching, CD).
    - Category C: Testing (Unit, Integration, Isolation).
    - Category D: Documentation (Mermaid, Docs-in-CI).
    - Category E: Dev Environment (.env.example, DevContainer).
    - Category F: Observability (Logging, Tracing, Metrics).
    - Category G: Security (Branch Protection, Gitleaks, Renovate).

## 3. Hardening Mission (P5)
- [ ] **Apply "Python Loophole"**: Instrument `app/` (Orchestration) with Sentry and PostHog.
- [ ] **Inject Security Gates**: 
    - `powershell -File scripts/install_hooks.ps1` (ASCII/Size checks).
    - Add `.github/workflows/gitleaks.yml`.
- [ ] **Configure Governance**:
    - Update `.github/labeler.yml` with P0-P3 labels.
    - Add `release-please.yml`.

## 4. Validation (P6)
- [ ] **Run Readiness Script**: `powershell -File scripts/build_readiness.ps1`.
- [ ] **Verify Telemetry**: Check heartbeat signals in Sentry/PostHog.

## 5. Certification (P7)
- [ ] **Generate Report**: Use `agent-readiness` skill to emit the final Level 6 report.
- [ ] **Archive State**: Save snapshot to `docs/brain/archive/`.

---

**Trigger**: `/readiness-audit` or when the user asks "How ready is this agent?".
