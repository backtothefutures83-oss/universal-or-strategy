# Fetch ALL Codacy issues (Security + Error-Prone focus)
$ErrorActionPreference = "Stop"

# Load .env file
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+?)\s*=\s*(.+?)\s*$') {
            $name = $matches[1]
            $value = $matches[2]
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
            Write-Host "[ENV] Loaded $name" -ForegroundColor Green
        }
    }
}

if (-not $env:CODACY_API_TOKEN) {
    Write-Error "CODACY_API_TOKEN not found in .env"
    exit 1
}

$org = "gh"
$owner = "malhitticrypto-debug"
$repo = "universal-or-strategy"

$headers = @{
    "api-token" = $env:CODACY_API_TOKEN
    "Content-Type" = "application/json"
}

# Fetch ALL issues (no level filter)
$body = @{} | ConvertTo-Json

$uri = "https://api.codacy.com/api/v3/analysis/organizations/$org/$owner/repositories/$repo/issues/search?limit=2000"

Write-Host "[Codacy] Fetching all issues (limit 2000)..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body
    
    Write-Host "[Codacy] Total issues: $($response.data.Count)" -ForegroundColor Green
    
    # Group by category (nested in patternInfo)
    $grouped = $response.data | Group-Object -Property { $_.patternInfo.category } | Sort-Object Count -Descending
    
    Write-Host "`n[Codacy] Issues by Category:" -ForegroundColor Cyan
    $grouped | ForEach-Object {
        Write-Host "  $($_.Name): $($_.Count) issues" -ForegroundColor Yellow
    }
    
    # Filter Security and ErrorProne
    $security = $response.data | Where-Object { $_.patternInfo.category -eq "Security" }
    $errorProne = $response.data | Where-Object { $_.patternInfo.category -eq "ErrorProne" }
    
    Write-Host "`n[Codacy] Security: $($security.Count) issues" -ForegroundColor Red
    Write-Host "[Codacy] ErrorProne: $($errorProne.Count) issues" -ForegroundColor Red
    
    # Save to files
    $security | ConvertTo-Json -Depth 10 | Out-File "docs/brain/codacy_security_fresh.json" -Encoding utf8
    $errorProne | ConvertTo-Json -Depth 10 | Out-File "docs/brain/codacy_errorprone_fresh.json" -Encoding utf8
    
    Write-Host "`n[Codacy] Saved to:" -ForegroundColor Green
    Write-Host "  - docs/brain/codacy_security_fresh.json" -ForegroundColor Green
    Write-Host "  - docs/brain/codacy_errorprone_fresh.json" -ForegroundColor Green
    
    # Show sample Security issues
    if ($security.Count -gt 0) {
        Write-Host "`n[Codacy] Sample Security Issues:" -ForegroundColor Cyan
        $security | Select-Object -First 5 | ForEach-Object {
            Write-Host "  - $($_.filePath):$($_.lineNumber)" -ForegroundColor Yellow
            Write-Host "    Pattern: $($_.patternId)" -ForegroundColor Gray
            Write-Host "    Message: $($_.message)" -ForegroundColor Gray
        }
    }
    
    # Show sample ErrorProne issues
    if ($errorProne.Count -gt 0) {
        Write-Host "`n[Codacy] Sample ErrorProne Issues:" -ForegroundColor Cyan
        $errorProne | Select-Object -First 5 | ForEach-Object {
            Write-Host "  - $($_.filePath):$($_.lineNumber)" -ForegroundColor Yellow
            Write-Host "    Pattern: $($_.patternId)" -ForegroundColor Gray
            Write-Host "    Message: $($_.message)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "[Codacy] Error: $_" -ForegroundColor Red
    throw
}

# Made with Bob
