# EPIC-CCN-10: Cohesion Improvement (Post-Extraction)

**Status**: BACKLOG  
**Priority**: P2 (Technical Debt)  
**Created**: 2026-06-02  
**Blocked By**: EPIC-CCN-11 (must complete extraction first)

---

## Goal

Improve cohesion score in `src/V12_002.Orders.Management.Flatten.cs` without regressing complexity gains from EPIC-CCN-11.

---

## Scope

**File**: `src/V12_002.Orders.Management.Flatten.cs`  
**Current State**: CYC 9, Low Cohesion  
**Target State**: CYC ≤ 15, Improved Cohesion

---

## Approach

1. **Group Related Helpers**: Cluster validation, calculation, and execution functions
2. **Introduce Value Objects**: Replace parameter clusters with typed structs
   - Example: `NudgeContext { Key, Order, NewLimitPrice, CitOffset }`
3. **Extract Shared Logic**: Identify common patterns across local/follower nudges
4. **Maintain Complexity**: Ensure no function exceeds CYC 15

---

## Constraints

- **No Complexity Regression**: CYC must remain ≤ 15 per function
- **Preserve Behavior**: Zero logic changes (refactoring only)
- **Jane Street Aligned**: "Make illegal states unrepresentable" via value objects

---

## Success Criteria

- [ ] CodeScene cohesion score improves (target: 5.5+)
- [ ] Cyclomatic complexity remains ≤ 15 per function
- [ ] All tests pass (no behavior change)
- [ ] Pre-push validation: 13/13 checks pass

---

## Estimated Effort

- **Analysis**: 2 hours (identify cohesion patterns)
- **Implementation**: 4 hours (value objects + grouping)
- **Testing**: 2 hours (verify behavior preservation)
- **Total**: 8 hours (1 day)

---

## Dependencies

- EPIC-CCN-11 must be merged and stable
- No active work on `V12_002.Orders.Management.Flatten.cs`

---

## Reference

- **Deviation Doc**: `docs/standards/JANE_STREET_DEVIATIONS.md`
- **CodeScene Report**: PR #21 delta analysis
- **Jane Street Principles**: Cognitive simplicity + correctness by construction