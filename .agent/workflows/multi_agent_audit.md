---
description: Multi-Agent "Red Team" Auditing Workflow
---

Use this workflow for high-stakes implementations where a single agent's blind spots must be caught.
Minimum 2 agents audit the same implementation independently. Results are cross-compared.
Mandatory for any change touching: order submission, position sizing, FSM state transitions, or fleet-wide broadcast logic.

---

## Phase 1: Define Audit Scope

1. Identify the implementation to audit (file path, diff, or code block).
2. Define audit criteria:
   - **Safety**: ghost orders, naked positions, shutdown races
   - **Correctness**: FSM state coverage, edge cases
   - **DNA compliance**: no locks, correct pattern (Enqueue vs direct-write), ASCII gate
   - **Performance**: no hot-path allocations, no blocking calls

---

## Phase 2: Independent Parallel Audits

Invoke each auditor with IDENTICAL input. They must NOT see each other's output.

| Auditor   | Tool            | Focus                                |
| --------- | --------------- | ------------------------------------ |
| FORENSICS | Codex forensics | Logic trace, state sequence proof    |
| ARCHITECT | Claude          | Structural soundness, DNA compliance |
| ENGINEER  | Codex/Jules     | Hot-path correctness, edge cases     |

Each auditor produces:

```
RED TEAM AUDIT — [Auditor Name] — [Date]

VERDICT: [PASS / FAIL / CONDITIONAL]

Findings:
1. [Finding + severity: CRITICAL / WARNING / INFO]
2. ...

Recommendation: [APPROVE / REVISE / BLOCK]
```

---

## Phase 3: Cross-Comparison

1. Collect all audit reports.
2. Cross-compare findings:
   - **Agreement on FAIL**: block implementation, escalate to Director.
   - **Divergence**: Antigravity adjudicates — present both views to Director.
   - **All PASS**: route to Phase 4.

---

## Phase 4: Consensus Sign-Off

1. All findings documented in `docs/brain/audit_[mission_id].md`.
2. ARCHITECT issues final P4 Audit Sign-Off memo.
3. Director receives consolidated report before approving implementation.

---

## Phase 5: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Did auditors receive identical inputs?** Verify isolation protocol.
2. **Was a CRITICAL finding missed by one auditor but caught by another?** Add to Phase 1 criteria.
3. **Was adjudication unclear?** Improve the Phase 3 conflict resolution rule.
4. **Was the audit scope too broad?** Narrow Phase 1 criteria for next use.

**If no gap found, state:** `workflow(multi_agent_audit): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow(multi_agent_audit): [what was fixed and why]`
