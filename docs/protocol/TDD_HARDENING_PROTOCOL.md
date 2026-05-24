# TDD Hardening Protocol

## Overview
This document defines the mandatory TDD (Test-Driven Development) protocol for all V12 src/ modifications, integrated with Droid Factory Missions patterns for collaborative planning and milestone validation.

## Core Principles

### 1. Red-Green-Refactor Cycle
Every feature MUST follow the TDD cycle:
1. **RED**: Write failing test first
2. **GREEN**: Implement minimal code to pass test
3. **REFACTOR**: Clean up while keeping tests green

### 2. Test-First Mandate
- NO src/ file may be modified without a corresponding test
- Tests MUST fail before implementation (verified)
- Tests MUST pass after implementation (verified)

### 3. Droid Missions Integration
- Collaborative planning before execution
- Milestone validation after EVERY ticket
- Iterative refinement of approach

## Skills System

### Available Skills
Located in `.bob/skills/`:

1. **tdd-red** - Write failing test (TDD RED phase)
2. **tdd-green** - Implement to pass test (TDD GREEN phase)
3. **tdd-refactor** - Clean up code (TDD REFACTOR phase)
4. **milestone-validation** - Comprehensive validation after each ticket
5. **epic-planning** - Collaborative planning (intake/plan/validate)

### Skill Activation
Skills are automatically loaded by Bob CLI. Use them via:
- Direct invocation: `/skill tdd-red`
- Workflow integration: Skills auto-activate during `/ticket` execution

## Hooks System

### Pre-Tool-Use Hooks
Located in `.bob/hooks/pre_tool_use/`:

1. **tdd_enforcement.sh** - Blocks src/ edits without tests
2. **ascii_check.sh** - Blocks Unicode/emoji in src/

### Post-Tool-Use Hooks
Located in `.bob/hooks/post_tool_use/`:

1. **format_csharp.sh** - Auto-format C# files
2. **deploy_sync.sh** - Auto-run deploy-sync after src/ edits

### Session Hooks
- **session_start/tool_discovery.sh** - Auto-discover tools at session start
- **session_end/mistake_analysis.sh** - Analyze mistakes at session end

## Custom Droids

### v12-engineer
- **Model**: Claude Sonnet 4.6
- **Role**: src/ implementation (TDD execution)
- **Tools**: Read, Edit, Create, ApplyPatch, Execute, LS, Grep, Glob
- **Constraints**: Lock-free, ASCII-only, CYC < 20

### v12-planner
- **Model**: Claude Opus 4.7
- **Role**: Epic planning and architectural analysis
- **Tools**: Read, LS, Grep, Glob, WebSearch (read-only)
- **Constraints**: No edits, no execution, high reasoning

### v12-validator
- **Model**: Claude Sonnet 4.6
- **Role**: Validation and DNA auditing
- **Tools**: Read, Execute, LS, Grep, Glob (read-only)
- **Constraints**: No edits, verification only

## Workflow Integration

### /ticket Command
Enhanced with TDD enforcement:

```bash
/ticket <ticket-number>
```

**Automatic TDD Flow:**
1. Load ticket from `docs/brain/<epic>/ticket-<N>.md`
2. Activate `tdd-red` skill → Write failing test
3. Verify test FAILS (EXIT 1)
4. Activate `tdd-green` skill → Implement feature
5. Verify test PASSES (EXIT 0)
6. Activate `tdd-refactor` skill → Clean up code
7. Activate `milestone-validation` skill → Full audit
8. Block next ticket if validation fails

### /epic-run Command
Enhanced with Droid Missions patterns:

```bash
/epic-run <epic-slug>
```

**Automatic Epic Flow:**
1. Load epic from `docs/brain/<epic>/`
2. Activate `epic-planning` skill → Collaborative planning
3. Execute tickets sequentially with TDD enforcement
4. Run milestone validation after EACH ticket
5. Generate completion report

## DNA Compliance

### Mandatory Checks (Every Ticket)
- [ ] Tests pass (EXIT 0)
- [ ] Zero `lock()` statements
- [ ] Zero non-ASCII characters
- [ ] CYC < 20 for all methods
- [ ] deploy-sync passes
- [ ] Semgrep clean (0 V12 DNA violations)

### Validation Report Format
```
[MILESTONE-VALIDATION] Ticket XX
========================================
Test Suite      : PASS (X tests)
Benchmarks      : PASS (no regressions)
Deploy-Sync     : PASS
Lock() Audit    : CLEAN (0 matches)
Unicode Audit   : CLEAN (0 matches)
Complexity      : PASS (all < 20)
Semgrep         : PASS (0 violations)
Dead Code       : PASS (no new dead code)
Graphify        : UPDATED
========================================
Verdict: PASS / FAIL
```

## Failure Handling

### TDD Enforcement Failures
**Symptom**: Hook blocks src/ edit
**Resolution**:
1. Create test file in `tests/V12_Performance.Tests/`
2. Write failing test
3. Verify test FAILS (EXIT 1)
4. Retry src/ edit

### Milestone Validation Failures
**Symptom**: Validation report shows FAIL
**Resolution**:
1. HALT immediately
2. Fix failing validation
3. Re-run validation
4. Only advance to next ticket after PASS

### DNA Audit Failures
**Symptom**: Lock(), Unicode, or CYC violations
**Resolution**:
1. Refactor to FSM/Enqueue pattern (lock-free)
2. Replace Unicode with ASCII
3. Extract methods if CYC > 20
4. Re-run DNA audits

## Cost Estimation

### Droid Missions Formula
```
total runs ≈ #tickets + 2 * #validation_runs
```

### V12 Adaptation
```
total validation runs = #tickets
```

**Rationale**: Validation runs after EVERY ticket (not just milestones) to prevent drift.

## Success Metrics

### Per-Ticket Metrics
- Test coverage: 100% of new code
- DNA compliance: 100% (zero violations)
- Validation pass rate: 100% (no failures)

### Per-Epic Metrics
- Tickets completed: 100% (no blockers)
- Rework rate: < 10% (minimal drift)
- Build health: 100% (F5 compiles)

## References

- [Droid Factory Missions](https://droid.build/missions)
- [V12 DNA Protocol](AGENTS.md)
- [Universal Agent Protocol](UNIVERSAL_AGENT_PROTOCOL.md)
- [Testing Strategy](../TESTING_AND_TOOLS.md)
