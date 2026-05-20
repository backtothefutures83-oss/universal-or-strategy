# Implementation Plan - PR #108 Remediation & CI Hardening

## Objective
Address the blocking issues in PR #108 (PII leak, hardcoded paths) and reduce CI noise by focusing audits strictly on production `.cs` files.

## Key Files & Context
- **Modify**:
  - `.gitignore`
  - `.codacy.yml` (Exclude more noise)
  - `.deepsource.toml` (Exclude `scripts/`)
  - `docs/brain/master_roadmap.md` (Correct hallucination)
  - `launch_classic.bat` (Path hardening)
  - `AGENTS.md` (Path hardening)
- **Delete/Purge**:
  - `.antigravitycli/` (Contains PII)

## Implementation Steps

### 1. Privacy & Security (PII Purge)
- **Action**: Delete the `.antigravitycli` folder and its contents.
- **Action**: Add `.antigravitycli/` to the root `.gitignore`.
- **Verify**: `git status` should not show the folder.

### 2. Path Hardening
- **Action**: Modify `launch_classic.bat` and `AGENTS.md`.
- **Change**: Replace `C:\Users\Mohammed Khalid\` with `%USERPROFILE%\` (for `.bat`) or relative/env paths (for `.md`).
- **Verify**: Manual inspection.

### 3. CI Noise Reduction (Focus on `.cs`)
- **Codacy**: Update `.codacy.yml` to ensure `scripts/**` is fully ignored. (Currently listed, but screenshot shows it is being scanned—likely need to ensure the pattern is correct or use `.codacy.yaml` consistently).
- **DeepSource**: Add `scripts/**` and `**/*.bat` to `exclude_patterns` in `.deepsource.toml`.
- **Verify**: New PR check run should show fewer issues and skip `scripts/`.

### 4. Roadmap Correction
- **Action**: Update `docs/brain/master_roadmap.md`.
- **Change**: Correct the text "ZERO CYC > 20 across 817 methods" to reflect the actual audit results (54 symbols > 20 CYC).
- **Verify**: Text matches `complexity_audit_cyc20_report.md`.

## Verification & Testing
1. `deploy-sync.ps1`: Run to ensure environment is still valid.
2. `git add .` (selective) and commit.
3. Push to `feature/photon-spsc-hardening-repair`.
4. Monitor GitHub Actions to confirm the "Secrets" fail is cleared and Codacy issue count drops.

## Migration & Rollback
- Rollback: Revert files to previous commit.
