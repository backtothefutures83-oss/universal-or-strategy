---
name: epic-planning
description: Collaborative planning with iterative refinement (Droid Missions pattern). Use for /epic-intake, /epic-plan, /epic-validate phases.
user-invocable: false
disable-model-invocation: false
---

# Epic Planning (Droid Missions Pattern)

## Purpose
Collaborative planning with iterative refinement before execution. Inspired by Droid Factory Missions pattern.

## When to Use
- `/epic-intake` phase (scope alignment)
- `/epic-plan` phase (analysis + approach)
- `/epic-validate` phase (stress-test approach)

## Droid Missions Principles

### 1. Collaborative Planning
- Interactive conversation to build plan
- Ask clarifying questions
- Probe for constraints
- Iterate until plan is solid

### 2. Planning Phase Matters Most
- Getting upfront plan right determines success
- Well-scoped plan with clear milestones
- Dramatically better results than vague goals

### 3. Validation Frequency
- Milestones define validation frequency
- Simple projects: 1 milestone
- Complex projects: More frequent validation

## Protocol

### Phase 1: Intake (Scope Alignment)
**Goal:** Build shared understanding

**Questions to Ask:**
- What code area is being refactored?
- What is the motivation?
- What outcome do you want?
- What are the constraints?
- What is out of scope?

**Output:** `docs/brain/<epic>/00-scope.md`

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

## Droid Missions Cost Estimation

```
total runs ≈ #features + 2 * #milestones
```

V12 equivalent:
```
total tickets ≈ #sub-methods + 2 * #validation_runs
```

## Success Criteria
- [ ] Scope clearly defined (IN/OUT explicit)
- [ ] Dependencies mapped (blast radius known)
- [ ] Risks identified (hotspots documented)
- [ ] Approach validated (stress-tested)
- [ ] Tickets ready (self-contained, sequenced)

## Next Step
After planning complete: Advance to execution pipeline with validated plan