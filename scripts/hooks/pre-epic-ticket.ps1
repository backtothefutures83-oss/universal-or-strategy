# Pre-Epic-Ticket Hook
# Verifies ticket dependencies before execution
# Purpose: Ensure prerequisite tickets are complete
# Exit Behavior: Halt if dependencies not met

param(
    [Parameter(Mandatory=$true)]
    [string]$EpicSlug,
    
    [Parameter(Mandatory=$true)]
    [string]$TicketNumber
)

$ErrorActionPreference = "Stop"

Write-Host "[PRE-EPIC-TICKET] Verifying dependencies for ticket $TicketNumber..." -ForegroundColor Yellow

# 1. Read EXECUTION_GUIDE.md to get dependency order
$guideFile = "docs/brain/$EpicSlug/EXECUTION_GUIDE.md"

if (-not (Test-Path $guideFile)) {
    Write-Host "[PRE-EPIC-TICKET] WARN: EXECUTION_GUIDE.md not found" -ForegroundColor Yellow
    Write-Host "  Expected: $guideFile" -ForegroundColor Gray
    Write-Host "RECOMMENDATION: Create execution guide with dependency order" -ForegroundColor Yellow
    Write-Host "[PRE-EPIC-TICKET] PASS: No dependencies to verify (non-blocking)" -ForegroundColor Green
    exit 0  # Non-blocking
}

$guide = Get-Content $guideFile -Raw

# 2. Extract ticket dependencies (format: "ticket-XX depends on: ticket-YY, ticket-ZZ")
$dependencyPattern = "ticket-$TicketNumber depends on:\s*(.+)"
$match = [regex]::Match($guide, $dependencyPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

if (-not $match.Success) {
    Write-Host "[PRE-EPIC-TICKET] No dependencies for ticket $TicketNumber" -ForegroundColor Green
    exit 0
}

$dependenciesRaw = $match.Groups[1].Value
$dependencies = $dependenciesRaw -split ",\s*"

Write-Host "  Found $($dependencies.Count) dependencies: $($dependencies -join ', ')" -ForegroundColor Cyan

# 3. Verify each dependency is marked complete
$missingDeps = @()
$incompleteDeps = @()

foreach ($dep in $dependencies) {
    $dep = $dep.Trim()
    
    # Try multiple possible locations for ticket files
    $possiblePaths = @(
        "docs/brain/$EpicSlug/$dep.md",
        "docs/brain/$EpicSlug/$dep-*.md"
    )
    
    $depTicketFile = $null
    foreach ($path in $possiblePaths) {
        $matches = Get-ChildItem -Path (Split-Path $path -Parent) -Filter (Split-Path $path -Leaf) -ErrorAction SilentlyContinue
        if ($matches) {
            $depTicketFile = $matches[0].FullName
            break
        }
    }
    
    if (-not $depTicketFile) {
        $missingDeps += $dep
        continue
    }
    
    $depContent = Get-Content $depTicketFile -Raw
    
    # Check for completion markers
    $isComplete = $depContent -match "\[x\]\s*(COMPLETE|Complete|complete)" -or
                  $depContent -match "Status:\s*Complete" -or
                  $depContent -match "✅\s*COMPLETE"
    
    if (-not $isComplete) {
        $incompleteDeps += $dep
    }
}

# 4. Report results
if ($missingDeps.Count -gt 0) {
    Write-Host "[PRE-EPIC-TICKET] FAIL: $($missingDeps.Count) dependencies not found" -ForegroundColor Red
    $missingDeps | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Verify dependency ticket numbers are correct" -ForegroundColor Yellow
    exit 1
}

if ($incompleteDeps.Count -gt 0) {
    Write-Host "[PRE-EPIC-TICKET] FAIL: $($incompleteDeps.Count) dependencies not complete" -ForegroundColor Red
    $incompleteDeps | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "ACTION: Complete prerequisite tickets before executing ticket $TicketNumber" -ForegroundColor Yellow
    exit 1
}

Write-Host "[PRE-EPIC-TICKET] PASS: All $($dependencies.Count) dependencies met" -ForegroundColor Green
exit 0

# Made with Bob
