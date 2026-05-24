# load_memory.ps1 - Auto-load persistent memory at session start (PowerShell version)
#
# Purpose: Load MEMORY.md and USER.md into system prompt at session start
# Inspired by Hermes Agent's memory system
#
# This hook runs automatically when a Bob CLI session starts.
# It injects memory content into the system prompt so the agent
# remembers V12 DNA, project structure, user preferences, and past mistakes.

$ErrorActionPreference = "Stop"

$REPO_ROOT = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$MEMORY_FILE = Join-Path $REPO_ROOT "docs\brain\MEMORY.md"
$USER_FILE = Join-Path $REPO_ROOT "docs\brain\USER.md"

Write-Host "[load_memory] Loading persistent memory..."

# Check if memory files exist
if (-not (Test-Path $MEMORY_FILE)) {
    Write-Warning "[load_memory] WARNING: MEMORY.md not found at $MEMORY_FILE"
    Write-Host "[load_memory] Skipping memory load"
    exit 0
}

if (-not (Test-Path $USER_FILE)) {
    Write-Warning "[load_memory] WARNING: USER.md not found at $USER_FILE"
    Write-Host "[load_memory] Skipping user profile load"
    exit 0
}

# Memory files exist - they will be auto-loaded by Bob CLI
# The actual injection happens in the Bob CLI harness, not here
# This hook just verifies the files exist and logs the load

$memoryLines = (Get-Content $MEMORY_FILE).Count
$userLines = (Get-Content $USER_FILE).Count

Write-Host "[load_memory] [OK] MEMORY.md found ($memoryLines lines)"
Write-Host "[load_memory] [OK] USER.md found ($userLines lines)"
Write-Host "[load_memory] Memory will be injected into system prompt"

# Show memory summary
Write-Host ""
Write-Host "[load_memory] Memory Summary:"
Write-Host "  V12 DNA: Lock-free, ASCII-only, CYC ≤15, 0B allocation, <300μs"
Write-Host "  Project: universal-or-strategy (C# .NET 8, NinjaTrader 8)"
Write-Host "  User: Mohammed Khalid (Director, PowerShell, Bob CLI primary)"
Write-Host ""

exit 0

# Made with Bob
