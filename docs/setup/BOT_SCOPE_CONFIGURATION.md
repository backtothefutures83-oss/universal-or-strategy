# Bot Scope Configuration Guide

**Created**: 2026-05-23  
**Purpose**: Configure all GitHub Apps to scan ONLY the `src/` directory  
**Impact**: ~70% reduction in noise, faster PR reviews, focused quality analysis

## Overview

All GitHub Apps (CodeRabbit, Codacy, Semgrep, Sourcery, DeepSource) are configured to analyze **ONLY** the `src/` directory. This focuses bot analysis on production code while excluding:

- `tests/**` - Test files (separate quality standards)
- `benchmarks/**` - Performance benchmarks (not production code)
- `docs/**` - Documentation (no code analysis needed)
- `scripts/**` - Build/utility scripts (different quality bar)
- `.github/**` - CI/CD workflows (infrastructure code)
- `conductor/**` - Project management files
- `Traycerrefactor/**` - Legacy refactoring artifacts

## Benefits

1. **Reduced Noise**: ~70% fewer irrelevant findings
2. **Focused Quality**: Bot analysis concentrated on production code
3. **Faster Reviews**: Smaller scan surface = faster PR feedback
4. **Token Efficiency**: Reduced API costs for bot operations
5. **Clear Signal**: Findings are actionable and relevant

## Configuration Files

### A. CodeRabbit (`.coderabbit.yaml`)

**Location**: `.coderabbit.yaml` (root)  
**Syntax**: YAML path filters with negation

```yaml
reviews:
  path_filters:
    - "!docs/**"
    - "!tests/**"
    - "!benchmarks/**"
    - "!scripts/**"
    - "!.github/**"
    - "!conductor/**"
    - "!Traycerrefactor/**"
    - "src/**"
```

**How it works**:
- `!` prefix = exclude pattern
- `src/**` = explicitly include src/ directory
- CodeRabbit processes filters in order

**Verification**:
1. Create a test PR with changes in `docs/` and `src/`
2. Verify CodeRabbit only comments on `src/` changes
3. Check PR comments for "Skipped files" section

### B. Codacy (`.codacy.yml`)

**Location**: `.codacy.yml` (root)  
**Syntax**: YAML exclude_paths array

```yaml
exclude_paths:
  - "docs/**"
  - "tests/**"
  - "benchmarks/**"
  - "scripts/**"
  - ".github/**"
  - "conductor/**"
  - "Traycerrefactor/**"
  - ".bob/**"
  - ".codex/**"
  - ".cursor/**"
  - ".vscode/**"
```

**How it works**:
- Codacy scans all files EXCEPT those matching exclude_paths
- Glob patterns supported (`**` = recursive)
- Applies to all analyzers (Roslyn, duplication, etc.)

**Verification**:
1. Check Codacy dashboard: https://app.codacy.com/gh/mdasdispatch-hash/universal-or-strategy/settings
2. Navigate to "Ignored Files" tab
3. Verify excluded paths are listed
4. Create test PR and confirm only `src/` files analyzed

### C. Semgrep (`.semgrep.yml`)

**Location**: `.semgrep.yml` (root)  
**Syntax**: Comment-based scope documentation

```yaml
# SCOPE RESTRICTION: This config scans src/ directory only.
# Run with: semgrep --config .semgrep.yml src/
# GitHub App is configured via Semgrep Cloud to scan src/ only.
```

**How it works**:
- Semgrep rules apply to all scanned files
- Scope restriction configured in Semgrep Cloud dashboard
- Local runs: `semgrep --config .semgrep.yml src/`

**Verification**:
1. Log in to Semgrep Cloud: https://semgrep.dev/orgs/-/projects
2. Navigate to project settings
3. Verify "Scan paths" includes only `src/`
4. Run local scan: `powershell -File .\scripts\run_semgrep.ps1`

### D. Sourcery (`.sourcery.yaml`)

**Location**: `.sourcery.yaml` (root)  
**Syntax**: YAML ignore array

```yaml
ignore:
  - "tests/**"
  - "benchmarks/**"
  - "docs/**"
  - "scripts/**"
  - ".github/**"
  - "conductor/**"
  - "Traycerrefactor/**"
  - ".bob/**"
  - ".codex/**"
  - ".cursor/**"
  - ".vscode/**"
```

**How it works**:
- Sourcery skips files matching ignore patterns
- Applies to all refactoring suggestions
- Glob patterns supported

**Verification**:
1. Install Sourcery extension in VS Code
2. Open a file in `src/` - should see Sourcery suggestions
3. Open a file in `tests/` - should see no Sourcery suggestions
4. Check Sourcery output panel for "Ignored files" count

### E. DeepSource (`.deepsource.toml`)

**Location**: `.deepsource.toml` (root)  
**Syntax**: TOML exclude_patterns array

```toml
[analyzers.meta]
exclude_patterns = [
    "docs/**",
    "tests/**",
    "benchmarks/**",
    "scripts/**",
    ".github/**",
    "conductor/**",
    "Traycerrefactor/**"
]
```

**How it works**:
- DeepSource C# analyzer skips excluded patterns
- Applies to all analysis types (security, performance, style)
- Glob patterns supported

**Verification**:
1. Check DeepSource dashboard: https://deepsource.io/gh/mdasdispatch-hash/universal-or-strategy
2. Navigate to "Settings" → "Analysis Configuration"
3. Verify excluded paths are listed
4. Create test PR and confirm only `src/` files analyzed

## Verification Checklist

After configuring all bots, verify scope restriction is working:

- [ ] **CodeRabbit**: Create test PR with `docs/` and `src/` changes, verify only `src/` reviewed
- [ ] **Codacy**: Check dashboard "Ignored Files" tab, verify exclusions listed
- [ ] **Semgrep**: Run local scan with `.\scripts\run_semgrep.ps1`, verify only `src/` scanned
- [ ] **Sourcery**: Open files in `tests/` and `src/`, verify suggestions only in `src/`
- [ ] **DeepSource**: Check dashboard settings, verify excluded paths configured

## Troubleshooting

### Bot still scanning excluded directories

**Symptom**: Bot comments on files in `tests/`, `docs/`, etc.

**Fix**:
1. Verify configuration file syntax (YAML/TOML indentation)
2. Check bot dashboard for configuration errors
3. Re-push configuration file to trigger bot re-scan
4. Contact bot support if issue persists

### Configuration file not recognized

**Symptom**: Bot ignores configuration file

**Fix**:
1. Verify file is in repository root (not subdirectory)
2. Check file name matches exactly (`.coderabbit.yaml`, not `.coderabbit.yml`)
3. Validate YAML/TOML syntax with online validator
4. Commit and push file (bots read from GitHub, not local)

### Glob patterns not working

**Symptom**: Specific files not excluded despite matching pattern

**Fix**:
1. Use `**` for recursive matching (not `*`)
2. Ensure no leading `/` in patterns (use `docs/**`, not `/docs/**`)
3. Test pattern with `git ls-files | grep <pattern>`
4. Check bot documentation for pattern syntax differences

## Integration with V12 Workflows

### PR Loop (`/pr-loop`)

Bot scope restriction is automatically applied during PR review:

1. Bots analyze only `src/` changes
2. Findings are focused on production code
3. Test/doc changes don't trigger bot noise
4. PHS calculation reflects production code quality only

### Epic Workflows (`/epic-run`, `/epic-tdd`)

Bot scope restriction applies to all PR-based workflows:

- Epic PRs only get bot feedback on `src/` changes
- Test failures in `tests/` don't trigger bot analysis
- Documentation updates don't affect bot scores

### Pre-Push Checks (`/pre-push`)

Local Semgrep runs respect scope restriction:

```powershell
# Runs Semgrep on src/ only
powershell -File .\scripts\run_semgrep.ps1
```

## Maintenance

### Adding New Exclusions

If a new directory should be excluded (e.g., `sandbox/`):

1. Update all 5 configuration files:
   - `.coderabbit.yaml` - Add `- "!sandbox/**"`
   - `.codacy.yml` - Add `- "sandbox/**"`
   - `.semgrep.yml` - Update comment
   - `.sourcery.yaml` - Add `- "sandbox/**"`
   - `.deepsource.toml` - Add `"sandbox/**"`

2. Commit and push changes
3. Verify in bot dashboards
4. Update this documentation

### Removing Exclusions

If a directory should be analyzed (e.g., moving `conductor/` to `src/`):

1. Remove from all 5 configuration files
2. Commit and push changes
3. Verify bots start analyzing the directory
4. Update this documentation

## Related Documentation

- **PR Loop Protocol**: `docs/protocol/PR_LOOP_V2.md`
- **Workflow Enhancement Plan**: `docs/brain/WORKFLOW_ENHANCEMENT_CI_LOGS.md`
- **GitHub Apps Installation**: `docs/setup/GITHUB_APPS_INSTALLATION.md`
- **Semgrep Setup**: `docs/setup/SEMGREP_SETUP.md`

## Support

For bot configuration issues:

- **CodeRabbit**: https://docs.coderabbit.ai/guides/configure-coderabbit
- **Codacy**: https://docs.codacy.com/repositories-configure/configuring-your-repository/
- **Semgrep**: https://semgrep.dev/docs/
- **Sourcery**: https://docs.sourcery.ai/
- **DeepSource**: https://docs.deepsource.com/docs/configuration

---

**[BOT-SCOPE-CONFIGURED]**