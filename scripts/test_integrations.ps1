# Test Integration Layer for Phoenix, Compound Intelligence, and Obsidian
# V12 Universal OR Strategy - Integration Testing Script

param(
    [switch]$SkipPhoenix,
    [switch]$SkipFirebase,
    [switch]$SkipObsidian,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$script:TestsPassed = 0
$script:TestsFailed = 0
$script:TestsSkipped = 0

function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message = ""
    )
    
    if ($Passed) {
        Write-Host "✅ PASS: $TestName" -ForegroundColor Green
        if ($Message -and $Verbose) {
            Write-Host "   $Message" -ForegroundColor Gray
        }
        $script:TestsPassed++
    } else {
        Write-Host "❌ FAIL: $TestName" -ForegroundColor Red
        if ($Message) {
            Write-Host "   $Message" -ForegroundColor Yellow
        }
        $script:TestsFailed++
    }
}

function Write-TestSkipped {
    param([string]$TestName, [string]$Reason)
    Write-Host "⏭️  SKIP: $TestName" -ForegroundColor Yellow
    Write-Host "   Reason: $Reason" -ForegroundColor Gray
    $script:TestsSkipped++
}

function Test-PythonModule {
    param([string]$ModuleName)
    
    $result = python -c "import $ModuleName; print('OK')" 2>&1
    return $result -match "OK"
}

function Test-ServiceRunning {
    param([string]$Url)
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 5 -UseBasicParsing
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

# ============================================
# Phase 1: Environment Check
# ============================================

Write-TestHeader "Phase 1: Environment Check"

# Check Python
$pythonVersion = python --version 2>&1
if ($pythonVersion -match "Python 3\.") {
    Write-TestResult "Python 3.x installed" $true $pythonVersion
} else {
    Write-TestResult "Python 3.x installed" $false "Python 3.x not found"
    exit 1
}

# Check Mise
$miseVersion = mise --version 2>&1
if ($miseVersion) {
    Write-TestResult "Mise installed" $true $miseVersion
} else {
    Write-TestResult "Mise installed" $false "Mise not found - run: irm https://mise.jdx.dev/install.ps1 | iex"
}

# Check .NET
$dotnetVersion = dotnet --version 2>&1
if ($dotnetVersion -match "8\.") {
    Write-TestResult ".NET SDK 8.0 installed" $true $dotnetVersion
} else {
    Write-TestResult ".NET SDK 8.0 installed" $false ".NET SDK 8.0 not found"
}

# ============================================
# Phase 2: Python Dependencies
# ============================================

Write-TestHeader "Phase 2: Python Dependencies"

$requiredModules = @(
    "arize.phoenix",
    "opentelemetry.sdk",
    "firebase_admin",
    "google.cloud.firestore"
)

foreach ($module in $requiredModules) {
    $installed = Test-PythonModule $module
    Write-TestResult "Python module: $module" $installed
}

# ============================================
# Phase 3: Phoenix Tracing Tests
# ============================================

if (-not $SkipPhoenix) {
    Write-TestHeader "Phase 3: Phoenix Tracing Tests"
    
    # Check if Phoenix is running
    $phoenixRunning = Test-ServiceRunning "http://localhost:6006"
    if ($phoenixRunning) {
        Write-TestResult "Phoenix server running" $true "http://localhost:6006"
    } else {
        Write-TestSkipped "Phoenix server running" "Start with: mise run phoenix"
    }
    
    # Test Phoenix tracer module
    Write-Host "`nTesting Phoenix tracer module..." -ForegroundColor Cyan
    $tracerTest = python .bob/hooks/phoenix_tracer.py 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-TestResult "Phoenix tracer module" $true
        if ($Verbose) {
            Write-Host $tracerTest -ForegroundColor Gray
        }
    } else {
        Write-TestResult "Phoenix tracer module" $false $tracerTest
    }
    
    # Check Phoenix database
    $phoenixDb = "$env:USERPROFILE\.phoenix\phoenix.db"
    if (Test-Path $phoenixDb) {
        $dbSize = (Get-Item $phoenixDb).Length
        Write-TestResult "Phoenix database exists" $true "Size: $([math]::Round($dbSize/1KB, 2)) KB"
    } else {
        Write-TestResult "Phoenix database exists" $false "Database not found at $phoenixDb"
    }
} else {
    Write-TestSkipped "Phoenix Tracing Tests" "Skipped by user"
}

# ============================================
# Phase 4: Compound Intelligence Tests
# ============================================

if (-not $SkipFirebase) {
    Write-TestHeader "Phase 4: Compound Intelligence Tests"
    
    # Check Firebase credentials
    $firebaseKey = $env:GOOGLE_APPLICATION_CREDENTIALS
    if ($firebaseKey -and (Test-Path $firebaseKey)) {
        Write-TestResult "Firebase credentials configured" $true $firebaseKey
    } else {
        Write-TestSkipped "Firebase credentials configured" "Set GOOGLE_APPLICATION_CREDENTIALS env var"
    }
    
    # Test Compound Intelligence logger module
    Write-Host "`nTesting Compound Intelligence logger module..." -ForegroundColor Cyan
    $ciTest = python .bob/hooks/compound_intelligence_logger.py 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-TestResult "Compound Intelligence logger module" $true
        if ($Verbose) {
            Write-Host $ciTest -ForegroundColor Gray
        }
    } else {
        Write-TestResult "Compound Intelligence logger module" $false $ciTest
    }
} else {
    Write-TestSkipped "Compound Intelligence Tests" "Skipped by user"
}

# ============================================
# Phase 5: Obsidian Sync Tests
# ============================================

if (-not $SkipObsidian) {
    Write-TestHeader "Phase 5: Obsidian Sync Tests"
    
    # Check Obsidian vault
    $obsidianVault = "$env:USERPROFILE\Obsidian\V12-Knowledge"
    if (Test-Path $obsidianVault) {
        Write-TestResult "Obsidian vault exists" $true $obsidianVault
        
        # Check vault structure
        $requiredDirs = @("Sessions", "Learnings", "Agents", "Files", "Tags")
        foreach ($dir in $requiredDirs) {
            $dirPath = Join-Path $obsidianVault $dir
            if (Test-Path $dirPath) {
                Write-TestResult "Obsidian/$dir directory" $true
            } else {
                Write-TestResult "Obsidian/$dir directory" $false "Directory not found"
            }
        }
    } else {
        Write-TestResult "Obsidian vault exists" $false "Vault not found at $obsidianVault"
    }
    
    # Test Obsidian sync module
    Write-Host "`nTesting Obsidian sync module..." -ForegroundColor Cyan
    $obsTest = python .bob/hooks/obsidian_sync.py 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-TestResult "Obsidian sync module" $true
        if ($Verbose) {
            Write-Host $obsTest -ForegroundColor Gray
        }
    } else {
        Write-TestResult "Obsidian sync module" $false $obsTest
    }
} else {
    Write-TestSkipped "Obsidian Sync Tests" "Skipped by user"
}

# ============================================
# Phase 6: Bob CLI Hooks Integration
# ============================================

Write-TestHeader "Phase 6: Bob CLI Hooks Integration"

# Check pre_session.py
if (Test-Path ".bob/hooks/pre_session.py") {
    Write-TestResult "pre_session.py exists" $true
    
    # Check if it imports all integrations
    $preSessionContent = Get-Content ".bob/hooks/pre_session.py" -Raw
    $hasPhoenix = $preSessionContent -match "phoenix_tracer"
    $hasCI = $preSessionContent -match "compound_intelligence_logger"
    $hasObsidian = $preSessionContent -match "obsidian_sync"
    
    Write-TestResult "pre_session.py imports Phoenix" $hasPhoenix
    Write-TestResult "pre_session.py imports Compound Intelligence" $hasCI
    Write-TestResult "pre_session.py imports Obsidian" $hasObsidian
} else {
    Write-TestResult "pre_session.py exists" $false
}

# Check post_session.py
if (Test-Path ".bob/hooks/post_session.py") {
    Write-TestResult "post_session.py exists" $true
    
    # Check if it finalizes all integrations
    $postSessionContent = Get-Content ".bob/hooks/post_session.py" -Raw
    $hasPhoenix = $postSessionContent -match "finalize_tracing"
    $hasCI = $postSessionContent -match "log_session_completion"
    $hasObsidian = $postSessionContent -match "obs_finalize_session"
    
    Write-TestResult "post_session.py finalizes Phoenix" $hasPhoenix
    Write-TestResult "post_session.py finalizes Compound Intelligence" $hasCI
    Write-TestResult "post_session.py finalizes Obsidian" $hasObsidian
} else {
    Write-TestResult "post_session.py exists" $false
}

# ============================================
# Phase 7: Integration Test (End-to-End)
# ============================================

Write-TestHeader "Phase 7: End-to-End Integration Test"

Write-Host "Running simulated Bob CLI session..." -ForegroundColor Cyan

# Set environment variables for test session
$env:BOB_AGENT_NAME = "test-agent"
$env:BOB_TASK_DESCRIPTION = "Integration test session"
$env:BOB_SESSION_ID = "test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$env:BOB_MODE = "test"

# Run pre-session hook
Write-Host "`n1. Running pre_session.py..." -ForegroundColor Yellow
$preResult = python .bob/hooks/pre_session.py 2>&1
Write-Host $preResult -ForegroundColor Gray

# Simulate some work
Write-Host "`n2. Simulating session work..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

# Set session completion status
$env:BOB_SESSION_SUCCESS = "true"
$env:BOB_SESSION_NOTES = "Integration test completed successfully"

# Run post-session hook
Write-Host "`n3. Running post_session.py..." -ForegroundColor Yellow
$postResult = python .bob/hooks/post_session.py 2>&1
Write-Host $postResult -ForegroundColor Gray

# Check if session was logged
$sessionLogged = ($preResult -match "initialized") -and ($postResult -match "finalized")
Write-TestResult "End-to-end session simulation" $sessionLogged

# ============================================
# Summary
# ============================================

Write-TestHeader "Test Summary"

$total = $script:TestsPassed + $script:TestsFailed + $script:TestsSkipped
$passRate = if ($total -gt 0) { [math]::Round(($script:TestsPassed / $total) * 100, 1) } else { 0 }

Write-Host "Total Tests: $total" -ForegroundColor Cyan
Write-Host "✅ Passed: $script:TestsPassed" -ForegroundColor Green
Write-Host "❌ Failed: $script:TestsFailed" -ForegroundColor Red
Write-Host "⏭️  Skipped: $script:TestsSkipped" -ForegroundColor Yellow
Write-Host "Pass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 80) { "Green" } elseif ($passRate -ge 60) { "Yellow" } else { "Red" })

if ($script:TestsFailed -eq 0) {
    Write-Host "`n🎉 All tests passed! Integration layer is ready." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️  Some tests failed. Review the output above." -ForegroundColor Yellow
    exit 1
}

# Made with Bob
