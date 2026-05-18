# Knowledge Sync Pipeline (V12.16)

# This script captures the live command surface of project agents 
# and synchronizes them with the Graphify Knowledge Graph.

Write-Host "🚀 Starting Multi-Agent Knowledge Sync..." -ForegroundColor Cyan

# 1. Capture CLI Help Surfaces
$Agents = @("jules", "codex", "bob", "gemini")
$Registry = @{}

foreach ($Agent in $Agents) {
    if (Get-Command $Agent -ErrorAction SilentlyContinue) {
        Write-Host "Indexing $Agent..." -ForegroundColor Green
        $HelpText = Invoke-Expression "$Agent --help" | Out-String
        $Registry[$Agent] = $HelpText
        # Save individual snapshots for RAG indexing
        $HelpText | Out-File -FilePath "docs/brain/live_$( $Agent )_help.txt" -Encoding utf8
    } else {
        Write-Host "Warning: $Agent CLI not found in PATH." -ForegroundColor Yellow
    }
}

# 2. Update Knowledge Graph
Write-Host "Updating Graphify Knowledge Graph..." -ForegroundColor Blue
graphify update .

# 3. Finalize
Write-Host "✅ Knowledge Pipeline Sync Complete." -ForegroundColor Green
