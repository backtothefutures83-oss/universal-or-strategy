# Epic TDD Workflow
## V12 Complexity Extraction -- TDD Red-Green-Refactor Pipeline

> **Agent-agnostic** -- usable by any CLI (Bob, Codex, Gemini) via SPEC REF header.
> **Bob native command**: `/epic-tdd` (`.bob/commands/epic-tdd.md`)
> **Full spec**: `docs/brain/epic_tdd_workflow.md`

---

## Trigger

Use this workflow for **any** V12 complexity extraction ticket that requires:
- Surgical method extraction with CYC reduction target
- TDD contract gate (RED-GREEN-REFACTOR)
- Full P2-P6 forensic + verification report chain

---

## How to Invoke

### In Bob CLI (native)
```
/epic-tdd
[paste Traycer ticket content]
```

### In any other CLI (Codex, Gemini, Cursor, Codex-rescue)
Paste this header before the ticket:

```
SPEC REF: docs/brain/epic_tdd_workflow.md
TDD PROTOCOL: Red-Green-Refactor (3-attempt auto-retry). Worker-Validator loop active.

Execute PLAN-THEN-EXECUTE PROTOCOL with TDD Contract Gate:
  P2 Forensics   -> docs/brain/forensics_report_t[ID].md
  P3 Architect   -> docs/brain/implementation_plan_t[ID].md -- STOP for Director confirm
  P4 Adjudicator -> docs/brain/adjudicator_audit_t[ID].md  -- STOP for Director confirm
  P5 Engineer    -> RED test first, GREEN on extraction, deploy-sync.ps1, bump BUILD_TAG
  P6 Verifier    -> dotnet test + complexity_audit.py -> docs/brain/verification_report_t[ID].md

[paste Traycer ticket content here]
```

---

## Pipeline Summary (all CLIs)

| Phase | Output Artifact | Gate |
|:------|:----------------|:-----|
| P2 Forensics | `docs/brain/forensics_report_t[ID].md` | Auto |
| P3 Architect | `docs/brain/implementation_plan_t[ID].md` | STOP -- Director confirms |
| P4 Adjudicator | `docs/brain/adjudicator_audit_t[ID].md` | CONDITIONAL PASS -> Antigravity resolves |
| P5 Engineer | `src/` edits + `deploy-sync.ps1` | RED test must fail; GREEN test must pass |
| P6 Verifier | `docs/brain/verification_report_t[ID].md` | Full test suite + CYC audit must pass |

---

## Full Spec Reference

See `docs/brain/epic_tdd_workflow.md` for:
- Complete Stage 1 (Traycer Epic Creation) instructions
- Full header prompt template with all TDD constraints
- Post-ticket Director checklist
- Upstream Cluster Audit Workflow integration (planned)
