# Generate Mock PR Data for Hook Testing
# Creates realistic PR comment data with configurable staleness

param(
    [int]$FileCount = 10,
    [int]$StalePercent = 0
)

$ErrorActionPreference = "Stop"

# Ensure src directory exists
if (-not (Test-Path "src")) {
    New-Item -ItemType Directory -Path "src" -Force | Out-Null
}

$mockComments = @()
$staleCount = [math]::Floor($FileCount * ($StalePercent / 100))

Write-Host "Generating mock PR data: $FileCount files, $staleCount stale ($StalePercent%)" -ForegroundColor Cyan

for ($i = 1; $i -le $FileCount; $i++) {
    $fileName = "src/V12_002.Mock$i.cs"
    
    # Create file if not stale (files 1 to FileCount-staleCount exist)
    if ($i -gt $staleCount) {
        $content = @"
// Mock file $i for testing
namespace V12 {
    public class Mock$i {
        public void TestMethod() {
            // Test implementation
        }
    }
}
"@
        $content | Out-File -FilePath $fileName -Encoding utf8 -Force
        Write-Host "  Created: $fileName" -ForegroundColor Green
    } else {
        Write-Host "  Skipped (stale): $fileName" -ForegroundColor Yellow
    }
    
    # Create bot comment referencing this file
    $mockComments += @{
        author = @{ login = "coderabbit-ai[bot]" }
        body = @"
**Issue found in $fileName**

Line 42: Potential null reference exception
Severity: Medium
Category: Code Quality

Recommendation: Add null check before accessing property.
"@
        createdAt = (Get-Date).AddHours(-$i).ToString("o")
        url = "https://github.com/mock/repo/pull/999#issuecomment-$i"
    }
}

# Add some reviews with file references
$mockReviews = @(
    @{
        author = @{ login = "sourcery-ai[bot]" }
        body = "Found architectural issues in src/V12_002.Mock1.cs and src/V12_002.Mock2.cs"
        state = "COMMENTED"
        submittedAt = (Get-Date).AddHours(-2).ToString("o")
    }
)

$mockPrData = @{
    comments = $mockComments
    reviews = $mockReviews
    statusCheckRollup = @(
        @{
            context = "CodeRabbit"
            state = "SUCCESS"
            conclusion = "SUCCESS"
        }
    )
}

$outputPath = "$PSScriptRoot\MockPrData.json"
$mockPrData | ConvertTo-Json -Depth 10 | Out-File $outputPath -Encoding utf8

Write-Host "`nMock data generated: $outputPath" -ForegroundColor Green
Write-Host "  Total comments: $($mockComments.Count)" -ForegroundColor Cyan
Write-Host "  Files created: $($FileCount - $staleCount)" -ForegroundColor Cyan
Write-Host "  Stale references: $staleCount" -ForegroundColor Cyan

# Made with Bob
