# Git Hooks Installer
# Installs V12 hooks into .git/hooks directory
# Purpose: Automate pre-push validation

$ErrorActionPreference = "Stop"

Write-Host "=== V12 Git Hooks Installer ===" -ForegroundColor Cyan

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Host "ERROR: Not a git repository" -ForegroundColor Red
    Write-Host "Run this script from the repository root" -ForegroundColor Yellow
    exit 1
}

$hooksDir = ".git/hooks"

# 1. Install pre-push hook
Write-Host "`n[1/1] Installing pre-push hook..." -ForegroundColor Yellow

$prePushHook = @"
#!/usr/bin/env pwsh
# V12 Pre-Push Hook
# Runs full validation suite before push

Write-Host "[PRE-PUSH] Running V12 validation suite..." -ForegroundColor Yellow

# Check if pre_push_validation.ps1 exists
if (Test-Path "scripts/pre_push_validation.ps1") {
    & powershell -File .\scripts\pre_push_validation.ps1
    exit `$LASTEXITCODE
} else {
    Write-Host "[PRE-PUSH] WARN: pre_push_validation.ps1 not found" -ForegroundColor Yellow
    Write-Host "Skipping pre-push validation (non-blocking)" -ForegroundColor Yellow
    exit 0
}
"@

$prePushPath = Join-Path $hooksDir "pre-push"

try {
    $prePushHook | Out-File -FilePath $prePushPath -Encoding UTF8 -Force
    
    # Make executable (Windows doesn't need chmod, but set for cross-platform)
    if ($IsLinux -or $IsMacOS) {
        chmod +x $prePushPath
    }
    
    Write-Host "  ✅ pre-push hook installed" -ForegroundColor Green
    Write-Host "  Location: $prePushPath" -ForegroundColor Gray
} catch {
    Write-Host "  ❌ Failed to install pre-push hook" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}

# 2. Verify installation
Write-Host "`n=== Verification ===" -ForegroundColor Cyan

if (Test-Path $prePushPath) {
    $content = Get-Content $prePushPath -Raw
    if ($content -match "V12 Pre-Push Hook") {
        Write-Host "✅ pre-push hook verified" -ForegroundColor Green
    } else {
        Write-Host "⚠️  pre-push hook exists but content unexpected" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ pre-push hook not found after installation" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Installation Complete ===" -ForegroundColor Cyan
Write-Host "Git hooks installed successfully" -ForegroundColor Green
Write-Host "`nNext push will trigger V12 validation suite" -ForegroundColor Yellow
Write-Host "To bypass: git push --no-verify" -ForegroundColor Gray

exit 0

# Made with Bob
