# Universal OR Strategy V12 - Tech Stack

## Core Technologies
- **Programming Language**: C# 8.0
- **Framework / Platform**: .NET Framework 4.8
- **Trading Platform**: NinjaTrader 8 API

## Code Quality & CI/CD
- **Linting**: StyleCop.Analyzers
- **Build System**: MSBuild / dotnet build (for evaluation only without proprietary assemblies)
- **CI**: GitHub Actions (dotnet-build, gemini-pr-audit, sonarcloud, stylecop-enforcement)

## Deployment
- PowerShell scripts (`deploy-sync.ps1`) for local NT8 environment synchronization.