# Pester Tests for pre-forensics.ps1
# Tests bot comment freshness verification hook

BeforeAll {
    # Setup paths
    $script:HookPath = "$PSScriptRoot\..\..\scripts\hooks\pre-forensics.ps1"
    $script:MocksPath = "$PSScriptRoot\Mocks"
    $script:TestPrNumber = 999
    $script:TestRawFile = "pr_${script:TestPrNumber}_raw.json"
    
    # Ensure mocks directory exists
    if (-not (Test-Path $script:MocksPath)) {
        New-Item -ItemType Directory -Path $script:MocksPath -Force | Out-Null
    }
}

AfterAll {
    # Cleanup test artifacts
    if (Test-Path $script:TestRawFile) {
        Remove-Item $script:TestRawFile -Force -ErrorAction SilentlyContinue
    }
}

Describe "pre-forensics.ps1" {
    
    Context "Happy Path - 0% Staleness" {
        BeforeEach {
            # Generate mock data with all files present
            & "$script:MocksPath\Generate-MockPrData.ps1" -FileCount 10 -StalePercent 0
            Copy-Item "$script:MocksPath\MockPrData.json" $script:TestRawFile -Force
        }
        
        AfterEach {
            Remove-Item $script:TestRawFile -Force -ErrorAction SilentlyContinue
            # Cleanup created mock files
            Get-ChildItem "src\V12_002.Mock*.cs" -ErrorAction SilentlyContinue | Remove-Item -Force
        }
        
        It "Should pass with 0% staleness" {
            $result = & $script:HookPath -PrNumber $script:TestPrNumber
            $LASTEXITCODE | Should -Be 0
        }
    }
    
    Context "Warning Threshold - 30-50% Staleness" {
        BeforeEach {
            # Generate mock data with 35% stale files
            & "$script:MocksPath\Generate-MockPrData.ps1" -FileCount 10 -StalePercent 35
            Copy-Item "$script:MocksPath\MockPrData.json" $script:TestRawFile -Force
        }
        
        AfterEach {
            Remove-Item $script:TestRawFile -Force -ErrorAction SilentlyContinue
            Get-ChildItem "src\V12_002.Mock*.cs" -ErrorAction SilentlyContinue | Remove-Item -Force
        }
        
        It "Should warn but pass with 35% staleness" {
            $output = & $script:HookPath -PrNumber $script:TestPrNumber 2>&1
            $LASTEXITCODE | Should -Be 0
            $output -join "`n" | Should -Match "WARN.*30-50%"
        }
    }
    
    Context "Failure Threshold - >50% Staleness" {
        BeforeEach {
            # Generate mock data with 60% stale files
            & "$script:MocksPath\Generate-MockPrData.ps1" -FileCount 10 -StalePercent 60
            Copy-Item "$script:MocksPath\MockPrData.json" $script:TestRawFile -Force
        }
        
        AfterEach {
            Remove-Item $script:TestRawFile -Force -ErrorAction SilentlyContinue
            Get-ChildItem "src\V12_002.Mock*.cs" -ErrorAction SilentlyContinue | Remove-Item -Force
        }
        
        It "Should fail with 60% staleness" {
            $output = & $script:HookPath -PrNumber $script:TestPrNumber 2>&1
            $LASTEXITCODE | Should -Be 1
            $output -join "`n" | Should -Match "FAIL.*>50%"
        }
    }
    
    Context "Edge Cases" {
        It "Should fail gracefully when raw file is missing" {
            # Ensure no raw file exists
            if (Test-Path $script:TestRawFile) {
                Remove-Item $script:TestRawFile -Force
            }
            
            $output = & $script:HookPath -PrNumber $script:TestPrNumber 2>&1
            $LASTEXITCODE | Should -Be 1
            $output -join "`n" | Should -Match "Raw PR data not found"
        }
        
        It "Should handle empty bot comments gracefully" {
            # Create mock data with no comments
            $emptyData = @{
                comments = @()
                reviews = @()
                statusCheckRollup = @()
            }
            $emptyData | ConvertTo-Json -Depth 10 | Out-File $script:TestRawFile -Encoding utf8
            
            $output = & $script:HookPath -PrNumber $script:TestPrNumber 2>&1
            $LASTEXITCODE | Should -Be 0
            $output -join "`n" | Should -Match "No file references found"
        }
        
        It "Should handle malformed JSON gracefully" {
            # Create invalid JSON
            "{ invalid json }" | Out-File $script:TestRawFile -Encoding utf8
            
            $output = & $script:HookPath -PrNumber $script:TestPrNumber 2>&1
            $LASTEXITCODE | Should -Be 1
            $output -join "`n" | Should -Match "Failed to parse"
        }
    }
}

# Made with Bob
