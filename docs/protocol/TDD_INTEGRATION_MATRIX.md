# TDD Integration Matrix - Tool Cross-Reference

## Overview
This matrix shows how all V12 tools, workflows, and protocols integrate to enforce TDD compliance.

## Tool Categories

### 1. TDD Enforcement (Pre-Implementation)
| Tool | Purpose | Trigger | Enforcement |
|------|---------|---------|-------------|
| **TDD Enforcement Hook** | Block src/ edits without tests | Pre-tool-use | HARD BLOCK |
| **ASCII Check Hook** | Block Unicode in src/ | Pre-tool-use | HARD BLOCK |
| **tdd-red command** | Guide RED phase | Manual | SOFT GUIDE |

### 2. Test Execution (Implementation)
| Tool | Purpose | Trigger | Enforcement |
|------|---------|---------|-------------|
| **xUnit** | Run unit tests | `dotnet test` | PASS/FAIL |
| **BenchmarkDotNet** | Performance tests | `dotnet run --project benchmarks` | PASS/FAIL |
| **PerformanceAssertions** | V12 DNA validation | Test execution | PASS/FAIL |

### 3. Code Quality (Post-Implementation)
| Tool | Purpose | Trigger | Enforcement |
|------|---------|---------|-------------|
| **deploy-sync.ps1** | Hard link sync + ASCII gate | Post-tool-use | HARD BLOCK |
| **format_csharp.sh** | Auto-format C# | Post-tool-use | AUTO-FIX |
| **complexity_audit.py** | CYC < 20 validation | Pre-PR | SOFT WARN |
| **Semgrep** | V12 DNA patterns | Pre-PR | HARD BLOCK |

### 4. Pre-PR Quality Gates
| Tool | Purpose | Trigger | Enforcement |
|------|---------|---------|-------------|
| **pre_pr_quality_gate.ps1** | 13 exhaustive tests | Manual | HARD BLOCK |
| **verify_pr_hygiene.ps1** | Rebase + clean | Pre-PR | HARD BLOCK |
| **CodeRabbit CLI** | Local AI review | Manual | SOFT GUIDE |

### 5. CI/CD Integration
| Tool | Purpose | Trigger | Enforcement |
|------|---------|---------|-------------|
| **GitHub Actions** | Automated CI | Push/PR | PASS/FAIL |
| **CodeRabbit AI** | PR review | PR creation | SOFT GUIDE |
| **Codacy** | Static analysis | PR creation | SOFT WARN |
| **Semgrep** | V12 DNA scan | PR creation | HARD BLOCK |

## Workflow Integration

### TDD Red-Green-Refactor Cycle

```
[RED] Write Failing Test
  ↓
  Tools: tdd-red command, UnitTestTemplate.cs
  Validation: Test FAILS (EXIT 1)
  ↓
[GREEN] Implement Minimal Code
  ↓
  Tools: tdd-green command, TDD Enforcement Hook
  Validation: Test PASSES (EXIT 0), deploy-sync PASSES
  ↓
[REFACTOR] Clean Up Code
  ↓
  Tools: tdd-refactor command, complexity_audit.py, format_csharp.sh
  Validation: Tests still PASS, CYC < 20
  ↓
[COMMIT] Save Changes
  ↓
  Tools: Git hooks, graphify update
  Validation: ASCII clean, no lock()
```

### Pre-PR Workflow

```
[LOCAL QUALITY GATE]
  ↓
  Tool: pre_pr_quality_gate.ps1
  Tests: 13 exhaustive checks
  ↓
[PR CREATION]
  ↓
  Tool: gh pr create
  Validation: PR hygiene, rebase
  ↓
[BOT REVIEW]
  ↓
  Tools: CodeRabbit, Codacy, Semgrep, CodeQL
  Validation: V12 DNA, security, quality
  ↓
[PR LOOP]
  ↓
  Tool: /pr-loop command
  Goal: Drive PHS to 100/100
  ↓
[MERGE]
```

## Tool Access Matrix

| Agent | TDD Tools | Quality Tools | CI/CD Tools |
|-------|-----------|---------------|-------------|
| **Bob CLI** | ✅ All | ✅ All | ✅ All |
| **Gemini CLI** | ✅ All | ✅ All | ✅ All |
| **Jules AI** | ❌ None | ✅ Read-only | ✅ All |
| **Codex CLI** | ✅ All | ✅ All | ❌ None |
| **Claude Opus** | ❌ None | ✅ Read-only | ❌ None |

## Enforcement Levels

### HARD BLOCK
- Operation fails immediately
- Must fix before proceeding
- Examples: TDD Enforcement Hook, deploy-sync, Semgrep

### SOFT WARN
- Operation succeeds with warning
- Should fix but not required
- Examples: Complexity audit, Codacy

### SOFT GUIDE
- Informational only
- No enforcement
- Examples: tdd-red command, CodeRabbit suggestions

### AUTO-FIX
- Automatically corrects issue
- No user action required
- Examples: format_csharp.sh, graphify update

## Gap Analysis

### Current Gaps
1. **No pre-commit hook** - TDD enforcement only at tool-use level
2. **No coverage tracking** - Can't measure test coverage %
3. **No mutation testing** - Can't verify test quality

### Planned Improvements
1. **Phase 4:** Add pre-commit hook for TDD enforcement
2. **Phase 4:** Integrate coverage tool (coverlet)
3. **Future:** Add mutation testing (Stryker.NET)

## References
- [TDD Hardening Protocol](TDD_HARDENING_PROTOCOL.md)
- [Testing Pyramid](TESTING_PYRAMID.md)
- [Universal Agent Protocol](UNIVERSAL_AGENT_PROTOCOL.md)
- [Pre-PR Quality Gate](../../scripts/pre_pr_quality_gate.ps1)