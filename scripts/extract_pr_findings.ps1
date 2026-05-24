param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber
)

$ErrorActionPreference = "Stop"

Write-Host "Extracting findings from PR #$PrNumber..." -ForegroundColor Cyan

# Fetch PR data
$prData = gh pr view $PrNumber --json title,body,comments,reviews --repo mdasdispatch-hash/universal-or-strategy | ConvertFrom-Json

$findings = @{
    pr_number = $PrNumber
    title = $prData.title
    bot_findings = @{
        CodeRabbit = @()
        Codacy = @()
        Semgrep = @()
        Sourcery = @()
        cubic = @()
        AmazonQ = @()
        Greptile = @()
        CodeFactor = @()
    }
}

# Parse reviews for bot findings
foreach ($review in $prData.reviews) {
    $author = $review.author.login
    $body = $review.body
    
    if ($author -eq "coderabbitai") {
        # Extract CodeRabbit findings
        if ($body -match "(?s)## Summary by CodeRabbit.*?(?=##|$)") {
            $findings.bot_findings.CodeRabbit += @{
                author = $author
                body = $matches[0]
                submittedAt = $review.submittedAt
            }
        }
    }
    elseif ($author -eq "cubic-dev-ai") {
        # Extract cubic findings - look for violation blocks
        if ($body -match "(?s)<violation.*?</violation>") {
            $findings.bot_findings.cubic += @{
                author = $author
                body = $body
                submittedAt = $review.submittedAt
            }
        }
    }
    elseif ($author -eq "codacy-production") {
        $findings.bot_findings.Codacy += @{
            author = $author
            body = $body
            submittedAt = $review.submittedAt
        }
    }
    elseif ($author -eq "greptile-apps") {
        $findings.bot_findings.Greptile += @{
            author = $author
            body = $body
            submittedAt = $review.submittedAt
        }
    }
    elseif ($author -eq "codefactor-io") {
        $findings.bot_findings.CodeFactor += @{
            author = $author
            body = $body
            submittedAt = $review.submittedAt
        }
    }
}

# Output as JSON
$findings | ConvertTo-Json -Depth 10 | Out-File "docs/brain/pr${PrNumber}_findings.json" -Encoding UTF8

Write-Host "Findings extracted to docs/brain/pr${PrNumber}_findings.json" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  CodeRabbit: $($findings.bot_findings.CodeRabbit.Count) reviews" -ForegroundColor White
Write-Host "  cubic: $($findings.bot_findings.cubic.Count) reviews" -ForegroundColor White
Write-Host "  Codacy: $($findings.bot_findings.Codacy.Count) reviews" -ForegroundColor White
Write-Host "  Greptile: $($findings.bot_findings.Greptile.Count) reviews" -ForegroundColor White
Write-Host "  CodeFactor: $($findings.bot_findings.CodeFactor.Count) reviews" -ForegroundColor White

# Made with Bob
