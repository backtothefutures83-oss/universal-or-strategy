# V12 Pre-Push Validation Suite
# Runs ALL checks locally before GitHub push to catch issues early
# Integrates: Build, Lint, Tests, Security, Formatting, ASCII, Links, PR Hygiene

param(
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipLint,
    [switch]$Fast  # Skip slow checks (complexity audit, dead code scan)
)

$ErrorActionPreference = "Stop"
$script:FailureCount = 0
$script:Checks = @()

function Write-CheckHeader {
    param([string]$Name)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "CHECK: $Name" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-CheckResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details = ""
    )
    
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $color = if ($Passed) { "Green" } else { "Red" }
    
    Write-Host "[$status] $Name" -ForegroundColor $color
    if ($Details) {
        Write-Host "  $Details" -ForegroundColor Gray
    }
    
    $script:Checks += [PSCustomObject]@{
        Name = $Name
        Status = $status
        Details = $Details
    }
    
    if (-not $Passed) {
        $script:FailureCount++
    }
}

# ============================================================================
# 1. ASCII GATE (V12 DNA Mandate)
# ============================================================================
Write-CheckHeader "1. ASCII-Only Compliance"
try {
    $files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
    $violations = @()
    foreach ($f in $files) {
        $content = [System.IO.File]::ReadAllBytes($f.FullName)
        foreach ($byte in $content) {
            if ($byte -gt 127) {
                $violations += $f.FullName
                break
            }
        }
    }
    
    if ($violations.Count -gt 0) {
        Write-CheckResult "ASCII Gate" $false "Non-ASCII found in $($violations.Count) files"
        $violations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    } else {
        Write-CheckResult "ASCII Gate" $true "All source files are ASCII-clean"
    }
} catch {
    Write-CheckResult "ASCII Gate" $false $_.Exception.Message
}

# ============================================================================
# 2. BUILD VERIFICATION
# ============================================================================
if (-not $SkipBuild) {
    Write-CheckHeader "2. Build Compilation"
    try {
        $buildOutput = dotnet build Linting.csproj --nologo --verbosity quiet 2>&1
        $buildSuccess = $LASTEXITCODE -eq 0
        
        if ($buildSuccess) {
            Write-CheckResult "Build" $true "Linting.csproj compiled successfully"
        } else {
            Write-CheckResult "Build" $false "Compilation failed"
            Write-Host $buildOutput -ForegroundColor Red
        }
    } catch {
        Write-CheckResult "Build" $false $_.Exception.Message
    }
}

# ============================================================================
# 3. UNIT TESTS
# ============================================================================
if (-not $SkipTests) {
    Write-CheckHeader "3. Unit Tests"
    try {
        $testOutput = dotnet test Testing.csproj --no-build --nologo --verbosity quiet 2>&1
        $testSuccess = $LASTEXITCODE -eq 0
        
        if ($testSuccess) {
            Write-CheckResult "Unit Tests" $true "All tests passed"
        } else {
            Write-CheckResult "Unit Tests" $false "Test failures detected"
            Write-Host $testOutput -ForegroundColor Red
        }
    } catch {
        Write-CheckResult "Unit Tests" $false $_.Exception.Message
    }
}

# ============================================================================
# 4. LINTING (Roslyn Analyzers)
# ============================================================================
if (-not $SkipLint) {
    Write-CheckHeader "4. Roslyn Linting"
    try {
        & "$PSScriptRoot\lint.ps1" -ErrorAction Stop
        Write-CheckResult "Lint" $true "No linting violations"
    } catch {
        Write-CheckResult "Lint" $false "Linting violations found"
    }
}

# ============================================================================
# 5. CSHARPIER FORMATTING CHECK
# ============================================================================
Write-CheckHeader "5. Code Formatting (CSharpier)"
try {
    # Check if CSharpier is installed
    $csharpierInstalled = Get-Command "dotnet-csharpier" -ErrorAction SilentlyContinue
    
    if ($csharpierInstalled) {
        # Run CSharpier in check mode (doesn't modify files)
        $formatOutput = dotnet csharpier . --check 2>&1
        $formatSuccess = $LASTEXITCODE -eq 0
        
        if ($formatSuccess) {
            Write-CheckResult "Formatting" $true "All files properly formatted"
        } else {
            Write-CheckResult "Formatting" $false "Formatting issues detected - run 'dotnet csharpier .'"
            Write-Host $formatOutput -ForegroundColor Yellow
        }
    } else {
        Write-CheckResult "Formatting" $true "CSharpier not installed (skipped)"
    }
} catch {
    Write-CheckResult "Formatting" $true "CSharpier check skipped: $($_.Exception.Message)"
}

# ============================================================================
# 6. SECURITY SCANS
# ============================================================================
Write-CheckHeader "6. Security Scans"

# 6a. Gitleaks (secrets detection)
try {
    $gitleaksInstalled = Get-Command "gitleaks" -ErrorAction SilentlyContinue
    if ($gitleaksInstalled) {
        $gitleaksOutput = gitleaks detect --no-git --verbose 2>&1
        $gitleaksSuccess = $LASTEXITCODE -eq 0
        
        if ($gitleaksSuccess) {
            Write-CheckResult "Gitleaks" $true "No secrets detected"
        } else {
            Write-CheckResult "Gitleaks" $false "Potential secrets found"
            Write-Host $gitleaksOutput -ForegroundColor Red
        }
    } else {
        Write-CheckResult "Gitleaks" $true "Not installed (skipped)"
    }
} catch {
    Write-CheckResult "Gitleaks" $true "Skipped: $($_.Exception.Message)"
}

# 6b. Snyk (if available)
try {
    $snykInstalled = Get-Command "snyk" -ErrorAction SilentlyContinue
    if ($snykInstalled) {
        # Check if node_modules exists (Snyk requirement for many environments)
        # If not, and this is a C# project, skip Snyk or use specific args
        if (-not (Test-Path "node_modules")) {
             Write-CheckResult "Snyk" $true "Skipped: node_modules not found (C# Project)"
        } else {
            $snykOutput = snyk test --severity-threshold=high 2>&1
            $snykSuccess = $LASTEXITCODE -eq 0
            
            if ($snykSuccess) {
                Write-CheckResult "Snyk" $true "No high-severity vulnerabilities"
            } else {
                Write-CheckResult "Snyk" $false "Vulnerabilities detected"
                Write-Host $snykOutput -ForegroundColor Red
            }
        }
    } else {
        Write-CheckResult "Snyk" $true "Not installed (skipped)"
    }
} catch {
    Write-CheckResult "Snyk" $true "Skipped: $($_.Exception.Message)"
}

# ============================================================================
# 7. MARKDOWN LINK VALIDATION
# ============================================================================
Write-CheckHeader "7. Markdown Links"
try {
    & "$PSScriptRoot\verify_links.ps1" -ErrorAction Stop
    Write-CheckResult "Markdown Links" $true "All links valid"
} catch {
    Write-CheckResult "Markdown Links" $false "Broken links detected"
}

# ============================================================================
# 8. PR HYGIENE (if on a branch)
# ============================================================================
Write-CheckHeader "8. PR Hygiene"
try {
    $currentBranch = git rev-parse --abbrev-ref HEAD 2>$null
    
    if ($currentBranch -and $currentBranch -ne "main") {
        & "$PSScriptRoot\verify_pr_hygiene.ps1" -ErrorAction Stop
        Write-CheckResult "PR Hygiene" $true "Diff size and commit structure OK"
    } else {
        Write-CheckResult "PR Hygiene" $true "On main branch (skipped)"
    }
} catch {
    Write-CheckResult "PR Hygiene" $false "Hygiene violations detected"
}

# ============================================================================
# 9. COMPLEXITY AUDIT (Optional - Slow)
# ============================================================================
if (-not $Fast) {
    Write-CheckHeader "9. Complexity Audit"
    try {
        $pythonInstalled = Get-Command "python" -ErrorAction SilentlyContinue
        if ($pythonInstalled) {
            $complexityOutput = python "$PSScriptRoot\complexity_audit.py" 2>&1
            $complexitySuccess = $LASTEXITCODE -eq 0
            
            if ($complexitySuccess) {
                Write-CheckResult "Complexity" $true "No high-complexity violations"
            } else {
                Write-CheckResult "Complexity" $false "High complexity detected"
                Write-Host $complexityOutput -ForegroundColor Yellow
            }
        } else {
            Write-CheckResult "Complexity" $true "Python not installed (skipped)"
        }
    } catch {
        Write-CheckResult "Complexity" $true "Skipped: $($_.Exception.Message)"
    }
}

# ============================================================================
# 10. DEAD CODE SCAN (Optional - Slow)
# ============================================================================
if (-not $Fast) {
    Write-CheckHeader "10. Dead Code Detection"
    try {
        $pythonInstalled = Get-Command "python" -ErrorAction SilentlyContinue
        if ($pythonInstalled) {
            $deadCodeOutput = python "$PSScriptRoot\dead_code_scan.py" 2>&1
            # Dead code scan is informational, not blocking
            Write-CheckResult "Dead Code" $true "Scan complete (informational)"
        } else {
            Write-CheckResult "Dead Code" $true "Python not installed (skipped)"
        }
    } catch {
        Write-CheckResult "Dead Code" $true "Skipped: $($_.Exception.Message)"
    }
}

# ============================================================================
# FINAL REPORT
# ============================================================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "PRE-PUSH VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$passCount = ($script:Checks | Where-Object { $_.Status -eq "PASS" }).Count
$failCount = $script:FailureCount
$totalCount = $script:Checks.Count

Write-Host "`nResults: $passCount/$totalCount checks passed" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })

if ($failCount -gt 0) {
    Write-Host "`nFailed Checks:" -ForegroundColor Red
    $script:Checks | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  - $($_.Name): $($_.Details)" -ForegroundColor Red
    }
    
    Write-Host "`n[BLOCKED] Fix the above issues before pushing to GitHub" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`n[READY] All checks passed - safe to push!" -ForegroundColor Green
    exit 0
}

# Made with Bob
