# install_hooks.ps1 -- Activates V12 pre-commit safety hooks in the local git repo.
# Run once after cloning or whenever the hook policy changes.
# Protocol: Path Hardening -- all paths quoted (BMad V12 Permanent DNA).

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$hooksDir        = (& git -C "$repoRoot" rev-parse --git-path hooks).Trim()
$preCommitHook   = Join-Path $hooksDir "pre-commit"
$prePushHook     = Join-Path $hooksDir "pre-push"

Write-Host "--- V12 Hook Installer ---"
Write-Host "Repo root : $repoRoot"
Write-Host "Hooks dir : $hooksDir"

if (-not $hooksDir -or -not (Test-Path -LiteralPath $hooksDir)) {
    Write-Error "ERROR: Git hooks path not found. Is this a git repository?"
    exit 1
}

# Build hook lines as an array to avoid PowerShell heredoc conflicts with sh $() syntax.
# Each element is one line of the resulting sh script.
$lines = @(
    "#!/bin/sh",
    "# V12 Pre-Commit Safety Hook -- installed by scripts/install_hooks.ps1",
    "# Gates: (1) No lock() in src/, (2) ASCII-only in staged .cs files",
    "",
    'echo "--- V12 Pre-Commit Gate ---"',
    "",
    "# Gate 1: Lock-free audit",
    'REPO_ROOT=$(git rev-parse --show-toplevel)',
    'LOCK_HITS=$(grep -rnE "(^|[[:space:]])lock[[:space:]]*\(" "$REPO_ROOT/src/" 2>/dev/null | grep -vE "(//|stateLock|\*)" | wc -l)',
    'if [ "$LOCK_HITS" -gt "0" ]; then',
    '    echo "PRE-COMMIT FAIL: lock() found in src/ -- BANNED by Platinum Standard."',
    '    grep -rnE "(^|[[:space:]])lock[[:space:]]*\(" "$REPO_ROOT/src/" 2>/dev/null | grep -vE "(//|stateLock|\*)"',
    '    exit 1',
    "fi",
    "",
    "# Gate 2: ASCII purity on staged .cs files",
    'STAGED_CS=$(git diff --cached --name-only --diff-filter=ACM | grep "\.cs$")',
    'if [ -n "$STAGED_CS" ]; then',
    '    python "$REPO_ROOT/check_ascii.py" $STAGED_CS',
    '    if [ $? -ne 0 ]; then',
    '        echo "PRE-COMMIT FAIL: Non-ASCII detected in staged C# files."',
    '        exit 1',
    '    fi',
    "fi",
    "",
    "# Gate 3: Gitleaks (staged-files scan; best-effort if binary missing)",
    'if command -v gitleaks >/dev/null 2>&1; then',
    '    gitleaks protect --staged --config "$REPO_ROOT/.gitleaks.toml" --redact',
    '    if [ $? -ne 0 ]; then',
    '        echo "PRE-COMMIT FAIL: Gitleaks detected a potential secret in staged files."',
    '        exit 1',
    '    fi',
    'else',
    '    echo "[WARN] gitleaks not on PATH -- skipping secret scan. CI will catch it."',
    'fi',
    "",
    'echo "--- V12 Pre-Commit Gate: PASS ---"',
    "exit 0"
)

$hookContent = $lines -join "`n"
[System.IO.File]::WriteAllText($preCommitHook, $hookContent + "`n", (New-Object System.Text.UTF8Encoding $false))

Write-Host ""
Write-Host "PRE-COMMIT HOOK INSTALLED : $preCommitHook"
Write-Host "Active gates              : [1] lock() ban  [2] ASCII purity  [3] gitleaks (if installed)"

# ============================================================================
# PRE-PUSH HOOK - Comprehensive Validation Suite
# ============================================================================
Write-Host ""
Write-Host "Installing pre-push hook..."

$prePushLines = @(
    "#!/bin/sh",
    "# V12 Pre-Push Validation Hook -- installed by scripts/install_hooks.ps1",
    "# Runs comprehensive validation suite before push",
    "",
    'echo "--- V12 Pre-Push Validation ---"',
    "",
    'REPO_ROOT=$(git rev-parse --show-toplevel)',
    "",
    "# Run the PowerShell validation script",
    'if command -v powershell >/dev/null 2>&1; then',
    '    powershell -File "$REPO_ROOT/scripts/pre_push_validation.ps1" -Fast',
    '    if [ $? -ne 0 ]; then',
    '        echo "PRE-PUSH FAIL: Validation suite detected issues."',
    '        echo "Fix the issues above or use --no-verify to bypass (not recommended)."',
    '        exit 1',
    '    fi',
    'else',
    '    echo "[WARN] PowerShell not found -- skipping pre-push validation."',
    '    echo "       Install PowerShell or run manually: powershell -File ./scripts/pre_push_validation.ps1"',
    'fi',
    "",
    'echo "--- V12 Pre-Push Validation: PASS ---"',
    "exit 0"
)

$prePushContent = $prePushLines -join "`n"
[System.IO.File]::WriteAllText($prePushHook, $prePushContent + "`n", (New-Object System.Text.UTF8Encoding $false))

Write-Host "PRE-PUSH HOOK INSTALLED   : $prePushHook"
Write-Host "Validation suite          : scripts/pre_push_validation.ps1 -Fast"
Write-Host ""
Write-Host "To bypass hooks (rare)    : git commit --no-verify  OR  git push --no-verify"
Write-Host ""
Write-Host "[SUCCESS] V12 Git hooks installed successfully!"
