# V12 Hook System

Deterministic verification hooks for CI/PR workflows.

## Quick Start

```powershell
# Run a specific hook
& scripts\hooks\pre-forensics.ps1 -PrNumber 8

# Run all tests
powershell -File tests\hooks\Run-HookTests.ps1
```

## Available Hooks

| Hook | Purpose | Blocking |
|------|---------|----------|
| `pre-forensics.ps1` | Verify bot comment freshness | Yes |
| `pre-deploy-sync.ps1` | Verify build readiness | Yes |
| `post-deploy-sync.ps1` | Verify hard link integrity | Yes |
| `pre-ci-log-extraction.ps1` | Verify PR state | Yes |
| `pre-pr-loop.ps1` | Verify branch state | Yes |

## Design Principles

1. **Fail-Safe**: Non-critical failures don't block workflows
2. **Idempotent**: Multiple executions produce same result
3. **Fast**: <2s execution time per hook
4. **Informative**: Clear error messages with remediation steps
5. **ASCII-Only**: V12 DNA compliance

## Exit Codes

- `0`: Success (continue workflow)
- `1`: Failure (abort workflow)

## Documentation

See [docs/protocol/HOOKS.md](../../docs/protocol/HOOKS.md) for complete documentation.

## Testing

```powershell
# Run all hook tests
powershell -File tests\hooks\Run-HookTests.ps1

# Run with detailed output
powershell -File tests\hooks\Run-HookTests.ps1 -Detailed

# Run specific test
powershell -File tests\hooks\Run-HookTests.ps1 -TestName "pre-forensics"
```

## Integration

Hooks are automatically called by their parent workflows:

- `extract_pr_forensics.ps1` → `pre-forensics.ps1`
- `deploy-sync.ps1` → `pre-deploy-sync.ps1` + `post-deploy-sync.ps1`
- `extract_ci_logs.ps1` → `pre-ci-log-extraction.ps1`
- `/pr-loop` → `pre-pr-loop.ps1`

## Troubleshooting

### Hook fails with "command not found"

Ensure you're running from the repository root:
```powershell
cd C:\WSGTA\universal-or-strategy
```

### Hook fails with "access denied"

Run PowerShell as Administrator or adjust execution policy:
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

### Hook fails with "module not found"

Install required modules:
```powershell
Install-Module -Name Pester -Force -SkipPublisherCheck
```

## Contributing

When adding new hooks:

1. Create hook script in `scripts/hooks/`
2. Create Pester test in `tests/hooks/`
3. Update `docs/protocol/HOOKS.md`
4. Add integration point to parent workflow
5. Run tests: `powershell -File tests\hooks\Run-HookTests.ps1`

---

**Last Updated**: 2026-05-24  
**Status**: Phase 1-2 Complete (5 P0 hooks)