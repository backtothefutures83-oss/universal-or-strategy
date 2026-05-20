# Implementation Plan: Agent Readiness Level 5 Sprint (4-Task Master) — REV 2

**BUILD_TAG anchor**: `1111.007-mphase-mp0` (current active) → target `1111.007-readiness-L5` (Task 4 only)
**Worktree**: `C:\WSGTA\universal-or-strategy` (current active repository)
**Source of truth**: `docs/brain/nexus_a2a.json` (`agent_readiness_target: "LEVEL_5"`)
**Scope**: Exactly the four readiness tasks enumerated in the mission brief. Stress Testing, Codecov, and a release workflow are explicitly OUT of the required scope.

---

## Context

`nexus_a2a.json` declares `agent_readiness_target: "LEVEL_5"`. While the baseline originally derived from an earlier build, our active workspace has been successfully updated with the `1111.007-mphase-mp0` core. Baseline inspection shows the repository has SonarCloud SAST, ASCII/lock() pre-commit gates, a CODEOWNERS file, and NUnit tests under `Testing.csproj` — but we must close the remaining gaps: dependency automation, CI test runner, secret scanning, CodeQL, SECURITY.md, and Sentry SDK wiring.

---

## Executive Summary

The clean baseline is missing four pillars required for Level 5 agent readiness:
1. **Dependabot**: Add `.github/dependabot.yml` and SHA-pin every existing GitHub Action.
2. **CI Test Execution with SonarCloud Coverage Ingestion**: Run NUnit tests in CI and deliver `.trx` + **OpenCover** coverage reports directly to SonarCloud.
3. **Governance & Security Hardening**: Add secret scanning (gitleaks), CodeQL, `SECURITY.md`, and remediate the committed Sentry DSN literal at `docs/brain/memory/adr019_compaction_state.md`.
4. **Sentry SDK Integration**: Wire Sentry .NET SDK dynamically via `V12_SENTRY_DSN` environment variable and instrument five target catch/reject sites in the C# strategy.

---

## Master Execution Plan

### Execution order and parallelism

| Order | Task | Parallelizable with | Hard blockers |
| :-- | :-- | :-- | :-- |
| 1 | Build & Dependency Automation | 2, 3 | none |
| 2 | Testing Infrastructure & Coverage | 1, 3 | none |
| 3 | Governance & Security Hardening | 1, 2 | none |
| 4 | Observability (Sentry Integration) | runs after 1/2/3 | `deploy-sync.ps1` + NT8 F5 compile; Task 3 must redact the DSN first |

Tasks 1–3 touch only `.github/`, `scripts/`, root-level config, and one `docs/brain/memory/` redaction; they have **zero C# source file overlap** and will merge first. Task 4 modifies `src/` and runs last.

---

## Proposed Changes

### Task 1 — Build & Dependency Automation

**Objective**: Add Dependabot for every ecosystem in the repo and SHA-pin every existing GitHub Action.

#### [NEW] [dependabot.yml](file:///C:/WSGTA/universal-or-strategy/.github/dependabot.yml)
- version: 2
- Ecosystems: `github-actions` (weekly) and `nuget` (weekly).
- Directory: `/` for both.
- Reviewers: `["mkalhitti-cloud"]`.
- Prefix: `chore(deps)`.

#### [MODIFY] [All GitHub Workflows](file:///C:/WSGTA/universal-or-strategy/.github/workflows/)
- Pinned SHA replacements for `uses:` clauses in `dotnet-build.yml`, `gemini-pr-audit.yml`, `labeler.yml`, `sonarcloud.yml`, `stylecop-enforcement.yml`, and `upstream-sync.yml`.

---

### Task 2 — Testing Infrastructure & Coverage

**Objective**: Run `Testing.csproj` in CI and deliver `.trx` + **OpenCover** coverage reports directly to SonarCloud using `sonar.cs.opencover.reportsPaths`.

#### [MODIFY] [Testing.csproj](file:///C:/WSGTA/universal-or-strategy/Testing.csproj)
- Add `<PackageReference Include="coverlet.msbuild" Version="6.0.2" />` under the PackageReference group.

#### [NEW] [dotnet-test.yml](file:///C:/WSGTA/universal-or-strategy/.github/workflows/dotnet-test.yml)
- Run `dotnet test` with `/p:CollectCoverage=true /p:CoverletOutputFormat=opencover` on pushes to `main`/`build/**` and PRs.
- Uses `windows-latest` for `.NET Framework 4.8` compatibility.

#### [MODIFY] [sonarcloud.yml](file:///C:/WSGTA/universal-or-strategy/.github/workflows/sonarcloud.yml)
- Add test run step generating OpenCover results.
- Add `/d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"` to scanner initialization.

---

### Task 3 — Governance & Security Hardening

**Objective**: Configure secret-scanning (Gitleaks), CodeQL analysis, write `SECURITY.md`, and redact the leaked Sentry DSN literal.

#### [MODIFY] [adr019_compaction_state.md](file:///C:/WSGTA/universal-or-strategy/docs/brain/memory/adr019_compaction_state.md)
- Replace Sentry DSN literal at line 19 with: `REDACTED_SENTRY_DSN -- see V12_SENTRY_DSN env var`.

#### [NEW] [.gitleaks.toml](file:///C:/WSGTA/universal-or-strategy/.gitleaks.toml)
- Default rules configuration with a narrow allowlist for `docs/telemetry/droid_mission_01/README.md` and `check_ascii.py` canary strings.

#### [NEW] [gitleaks.yml](file:///C:/WSGTA/universal-or-strategy/.github/workflows/gitleaks.yml)
- Standard Gitleaks detection pipeline on push and PR.

#### [NEW] [codeql.yml](file:///C:/WSGTA/universal-or-strategy/.github/workflows/codeql.yml)
- Set up C# CodeQL analysis using manual build step `dotnet build Linting.csproj --nologo`.

#### [NEW] [SECURITY.md](file:///C:/WSGTA/universal-or-strategy/SECURITY.md)
- Governance policies, vulnerability reporting SLO, required branch protection checks, and a "Known historical exposures" subsection documenting the rotated DSN.

#### [MODIFY] [install_hooks.ps1](file:///C:/WSGTA/universal-or-strategy/scripts/install_hooks.ps1)
- Append Gitleaks staged protect command as a third hook gate.

---

### Task 4 — Observability (Sentry Integration)

**Objective**: Wire the Sentry .NET SDK dynamically via `V12_SENTRY_DSN` environment variable and instrument the five target catch/reject sites.

#### [NEW] [V12_002.Sentry.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.Sentry.cs)
- Partial class file encapsulating:
  - `InitializeSentryIfConfigured()`
  - `CaptureForensic(string tag, Exception ex)`
  - `CaptureForensicMessage(string tag, string message)`
  - `ShutdownSentry()`
  - Uses `Interlocked.CompareExchange` for thread safety (no legacy locks).

#### [MODIFY] [Linting.csproj](file:///C:/WSGTA/universal-or-strategy/Linting.csproj)
- Add `<PackageReference Include="Sentry" Version="4.13.0" />` for compilation safety.

#### [MODIFY] [V12_002.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.cs)
- Advance `BUILD_TAG` to `"1111.007-readiness-L5"`.

#### [MODIFY] [V12_002.Lifecycle.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.Lifecycle.cs)
- Init call in `State.Configure` and shutdown call in `State.Terminated`.
- Capture site #1: `catch (Exception _mmioEx)` around MMIO mirror init.

#### [MODIFY] [V12_002.REAPER.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.REAPER.cs)
- Capture site #2: `catch (Exception ex)` in REAPER main audit loop.

#### [MODIFY] [V12_002.Orders.Callbacks.Propagation.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.Propagation.cs)
- Capture site #3: `catch (Exception submitEx)` around follower order submissions.

#### [MODIFY] [V12_002.SIMA.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.SIMA.cs)
- Capture site #4: `catch (Exception ex)` on the SIMA mutation path.

#### [MODIFY] [V12_002.UI.IPC.cs](file:///C:/WSGTA/universal-or-strategy/src/V12_002.UI.IPC.cs)
- Capture site #5: Zero-trust IPC command rejection (uses `CaptureForensicMessage` to prevent PII leakage).

#### [NEW] [sentry_runtime_setup.md](file:///C:/WSGTA/universal-or-strategy/docs/telemetry/sentry_runtime_setup.md)
- User instructions for dropping `Sentry.dll` into the local NinjaTrader directory and configuring the environment variable.

---

## Verification Plan

### Automated Tests
- Run `powershell -File .\scripts\build_readiness.ps1`
- Run local unit tests: `dotnet test Testing.csproj` (ensure NUnit tests pass)
- Run ASCII gate check: `python check_ascii.py`

### Manual Verification
- Deploy hard links: `powershell -File .\deploy-sync.ps1`
- Compile inside NinjaTrader (F5) and check for dynamic banner logs in output window.
- Verify environment variable `V12_SENTRY_DSN` is read and initialized correctly.
