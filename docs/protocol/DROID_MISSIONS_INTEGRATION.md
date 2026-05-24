# Droid Missions Integration

## Overview
This document describes how V12 integrates Droid Factory Missions patterns for collaborative planning, iterative refinement, and milestone validation.

## Core Droid Missions Principles

### 1. Collaborative Planning
**From Droid Factory:**
> "The planning phase matters most. Getting the upfront plan right determines success."

**V12 Adoption:**
- Interactive conversation to build plan
- Ask clarifying questions
- Probe for constraints
- Iterate until plan is solid

### 2. Validation Frequency
**From Droid Factory:**
> "Milestones define validation frequency. Simple projects: 1 milestone. Complex projects: More frequent validation."

**V12 Adoption:**
- Run validation after EVERY ticket (not just milestones)
- Block next ticket if validation fails
- Comprehensive DNA audit included

### 3. Cost Estimation
**From Droid Factory:**
```
total runs ≈ #features + 2 * #milestones
```

**V12 Adaptation:**
```
total tickets ≈ #sub-methods + 2 * #validation_runs
total validation runs = #tickets
```

## Epic Planning Workflow

### Phase 1: Intake (Scope Alignment)
**Goal:** Build shared understanding

**Questions to Ask:**
- What code area is being refactored?
- What is the motivation?
- What outcome do you want?
- What are the constraints?
- What is out of scope?

**Output:** `docs/brain/<epic>/00-scope.md`

**Example:**
```markdown
# Epic Scope: SIMA Subgraph Extraction

## IN SCOPE
- Extract SIMA dispatch logic to separate file
- Reduce V12_002.cs from 3500 LOC to < 1200 LOC
- Maintain all FSM state transitions

## OUT OF SCOPE
- Changing SIMA algorithm logic
- Modifying signal names or order IDs
- Refactoring non-SIMA code
```

### Phase 2: Plan (Analysis + Approach)
**Goal:** Thorough analysis before decisions

**Analysis:**
- Dependency map (who calls this?)
- Risk hotspots (what could break?)
- Test coverage (what's tested?)
- Change surface area (what's affected?)

**Approach:**
- Key technical decisions (3-5 major choices)
- Target state (what "done" looks like)
- Component architecture (new files needed?)
- Invariants (what MUST NOT change?)

**Output:** `docs/brain/<epic>/01-analysis.md`, `docs/brain/<epic>/02-approach.md`

**Example Analysis:**
```markdown
# Dependency Map
- ProcessBracketEvent() calls SIMA dispatch
- OnBarUpdate() calls ProcessBracketEvent()
- 47 call sites total

# Risk Hotspots
- FSM state transitions (HIGH)
- Signal name mutations (CRITICAL)
- Order ID generation (CRITICAL)

# Test Coverage
- 12 existing tests cover SIMA logic
- 0 tests for extraction boundaries
```

### Phase 3: Validate (Stress-Test)
**Goal:** Verify approach is safe and minimal

**Stress-Test Questions:**
- What breaks if this decision is wrong?
- Could the same outcome be achieved more simply?
- What happens in partial extraction states?
- Is the verification strategy strong enough?

**V12 DNA Checklist:**
- [ ] Each sub-method >= 15 LOC?
- [ ] Residual method < 20 CYC?
- [ ] FSM state transitions preserved?
- [ ] Zero new lock() statements?
- [ ] deploy-sync after EVERY src/ edit?
- [ ] ASCII-only method names?
- [ ] No signal/order ID mutation?

**Output:** Updated `01-analysis.md`, `02-approach.md`

## Milestone Validation

### When to Run
- After EVERY ticket execution (not just at end of epic)
- Before advancing to next ticket
- After any src/ file modification

### Validation Steps

#### Step 1: Full Test Suite
```powershell
dotnet test
# Expected: All tests PASS
```

#### Step 2: Benchmarks
```powershell
dotnet run --project benchmarks --configuration Release
# Expected: No performance regressions
```

#### Step 3: DNA Audits
```powershell
# Deploy-sync (hard links + ASCII)
powershell -File .\deploy-sync.ps1
# Expected: ASCII gate PASS

# Lock-free compliance
grep -r "lock(" src/
# Expected: 0 matches

# Unicode compliance
grep -Prn "[^\x00-\x7F]" src/
# Expected: 0 matches

# Complexity compliance
python scripts/complexity_audit.py
# Expected: All methods CYC < 20
```

#### Step 4: Semgrep (V12 DNA Patterns)
```powershell
powershell -File .\scripts\run_semgrep.ps1
# Expected: 0 V12 DNA violations
```

#### Step 5: Dead Code Scan
```powershell
python scripts/dead_code_scan.py
# Expected: No new dead code
```

#### Step 6: Graphify Update
```powershell
graphify update . --silent
# Expected: Knowledge graph updated
```

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

### If ANY Validation Fails
1. HALT immediately
2. Report failure to Director
3. DO NOT advance to next ticket
4. Fix issue before continuing

### Common Failures

**Test Failures:**
- Fix broken tests or implementation
- Re-run test suite
- Verify PASS before continuing

**Lock() Violations:**
- Refactor to FSM/Enqueue pattern
- Remove all `lock()` statements
- Re-run lock() audit

**Unicode Violations:**
- Replace Unicode with ASCII
- Re-run Unicode audit

**CYC > 20:**
- Extract methods (>= 15 LOC each)
- Re-run complexity audit

**Semgrep Violations:**
- Fix V12 DNA issues
- Re-run Semgrep

## Benefits of Droid Missions Integration

### 1. Prevents Drift
- Validation after EVERY ticket catches regressions early
- No accumulation of technical debt
- Continuous alignment with V12 DNA

### 2. Reduces Rework
- Thorough planning upfront
- Stress-testing approach before execution
- Clear success criteria per ticket

### 3. Improves Quality
- Comprehensive DNA audits
- Automated enforcement via hooks
- Objective validation reports

### 4. Enables Autonomy
- Clear validation criteria
- Automated pass/fail gates
- Self-service progression

## Comparison: Before vs After

### Before Droid Missions
- Validation only at end of epic
- Drift accumulates across tickets
- Rework required after epic completion
- Manual DNA checks (error-prone)

### After Droid Missions
- Validation after EVERY ticket
- Drift caught immediately
- Zero rework (validation blocks progression)
- Automated DNA checks (reliable)

## Cost Analysis

### Validation Overhead
```
Validation time per ticket: ~2 minutes
Tickets per epic: ~10
Total validation time: ~20 minutes
```

### Rework Savings
```
Rework time without validation: ~2 hours
Rework time with validation: ~0 hours
Net savings: ~2 hours per epic
```

**ROI:** 6x time savings (2 hours saved / 20 minutes invested)

## References

- [Droid Factory Missions](https://droid.build/missions)
- [TDD Hardening Protocol](TDD_HARDENING_PROTOCOL.md)
- [V12 DNA Protocol](../AGENTS.md)
- [Universal Agent Protocol](UNIVERSAL_AGENT_PROTOCOL.md)