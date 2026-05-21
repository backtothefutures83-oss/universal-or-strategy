# /pre-push - Comprehensive Local Validation

**Purpose**: Run ALL quality checks locally BEFORE pushing to GitHub to avoid waiting for CI/bot failures.

## Usage

```bash
# Full validation (recommended before any push)
powershell -File .\scripts\pre_push_validation.ps1

# Fast mode (skip slow checks like complexity audit)
powershell -File .\scripts\pre_push_validation.ps1 -Fast

# Skip specific checks
powershell -File .\scripts\pre_push_validation.ps1 -SkipBuild -SkipTests
```

## What It Checks

### 1. **ASCII Gate** (V12 DNA Mandate)
- Scans all `src/**/*.cs` files for non-ASCII characters
- **Blocks**: Unicode, emoji, curly quotes
- **Why**: NinjaTrader compiler crashes on non-ASCII

### 2. **Build Compilation**
- Compiles `Linting.csproj` with Roslyn analyzers
- **Catches**: Syntax errors, type errors, analyzer warnings
- **Skip**: Use `-SkipBuild` if you just ran a build

### 3. **Unit Tests**
- Runs all tests in `Testing.csproj`
- **Catches**: Logic regressions, broken contracts
- **Skip**: Use `-SkipTests` for non-code changes

### 4. **Roslyn Linting**
- Runs `scripts/lint.ps1` (StyleCop, Roslynator, etc.)
- **Catches**: Style violations, code smells
- **Skip**: Use `-SkipLint` for docs-only changes

### 5. **CSharpier Formatting**
- Checks code formatting without modifying files
- **Catches**: Inconsistent indentation, line length
- **Auto-fix**: Run `dotnet csharpier .` to format

### 6. **Security Scans**
- **Gitleaks**: Detects hardcoded secrets, API keys
- **Snyk**: Scans for dependency vulnerabilities
- **Catches**: Credential leaks, CVEs

### 7. **Markdown Links**
- Validates all markdown links via `scripts/verify_links.ps1`
- **Catches**: Broken internal/external links
- **Why**: Prevents documentation rot

### 8. **PR Hygiene**
- Runs `scripts/verify_pr_hygiene.ps1` if on a branch
- **Catches**: Oversized diffs (>10k chars), missing origin/main
- **Why**: Keeps PRs reviewable

### 9. **Complexity Audit** (Optional - Slow)
- Runs `scripts/complexity_audit.py`
- **Catches**: High cyclomatic complexity (>15)
- **Skip**: Use `-Fast` to skip

### 10. **Dead Code Scan** (Optional - Slow)
- Runs `scripts/dead_code_scan.py`
- **Informational**: Doesn't block push
- **Skip**: Use `-Fast` to skip

## Integration Points

### Git Pre-Push Hook (Recommended)
```bash
# Install hook
powershell -File .\scripts\install_hooks.ps1

# Hook runs automatically on `git push`
# Blocks push if validation fails
```

### Bob CLI Integration
```bash
# Add to your workflow
bob /pre-push
bob /commit "fix: your changes"
bob /push
```

### VSCode Task
Add to `.vscode/tasks.json`:
```json
{
  "label": "Pre-Push Validation",
  "type": "shell",
  "command": "powershell -File .\\scripts\\pre_push_validation.ps1",
  "problemMatcher": [],
  "group": {
    "kind": "test",
    "isDefault": true
  }
}
```

## Expected Output

```
========================================
CHECK: 1. ASCII-Only Compliance
========================================
[PASS] ASCII Gate: All source files are ASCII-clean

========================================
CHECK: 2. Build Compilation
========================================
[PASS] Build: Linting.csproj compiled successfully

========================================
CHECK: 3. Unit Tests
========================================
[PASS] Unit Tests: All tests passed

... (8 more checks)

========================================
PRE-PUSH VALIDATION SUMMARY
========================================

Results: 10/10 checks passed

[READY] All checks passed - safe to push!
```

## Failure Handling

If any check fails:
```
Results: 8/10 checks passed

Failed Checks:
  - ASCII Gate: Non-ASCII found in 2 files
  - Unit Tests: Test failures detected

[BLOCKED] Fix the above issues before pushing to GitHub
```

**Exit Code**: 1 (blocks git hook)

## Performance

- **Fast Mode**: ~30 seconds (skips complexity/dead code)
- **Full Mode**: ~2 minutes (includes all checks)
- **Parallel**: Checks run sequentially for clear output

## Comparison to CI

| Check | Local (Pre-Push) | GitHub CI | Time Saved |
|-------|------------------|-----------|------------|
| Build | ✅ Instant | ⏱️ 1-2 min | 1-2 min |
| Tests | ✅ Instant | ⏱️ 1-2 min | 1-2 min |
| Lint | ✅ Instant | ⏱️ 30-60s | 30-60s |
| Security | ✅ Instant | ⏱️ 1-2 min | 1-2 min |
| **Total** | **~2 min** | **~5-10 min** | **3-8 min** |

**Plus**: No waiting for bot checks (CodeRabbit, Kilo, Cubic, etc.)

## Bob Findings Integration

The script checks for:
- **Bob CLI findings**: Via Roslyn analyzers in build step
- **CodeRabbit**: Markdown formatting, link validation
- **Greptile**: Dead code scan, complexity audit
- **Snyk**: Dependency vulnerabilities
- **Gitleaks**: Secret detection

## Troubleshooting

### "CSharpier not installed"
```bash
dotnet tool install -g csharpier
```

### "Gitleaks not installed"
```bash
# Windows (via Chocolatey)
choco install gitleaks

# Or download from: https://github.com/gitleaks/gitleaks/releases
```

### "Snyk not installed"
```bash
npm install -g snyk
snyk auth
```

### "Python not found"
Install Python 3.12+ from python.org

## Related Commands

- `/pr-loop` - Full PR workflow (includes pre-push)
- `/epi-run` - Epic workflow (includes pre-push)
- `/repair-pr` - Fix PR issues (runs pre-push after fixes)

## V12 DNA Compliance

This script enforces:
- ✅ ASCII-only (Section 7)
- ✅ Lock-free patterns (via tests)
- ✅ Surgical changes (via PR hygiene)
- ✅ Zero-trust validation (all checks mandatory)