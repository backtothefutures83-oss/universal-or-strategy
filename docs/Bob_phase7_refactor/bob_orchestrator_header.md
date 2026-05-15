# Bob Orchestrator Header — Phase 7 Complexity Extraction

Use this header at the top of every Bob Orchestrator session when executing a
Phase 7 ticket. Update BUILD_TAG_BASELINE to the previous ticket's output
tag before pasting.

---

## Header Template

```
MISSION: Phase 7 Complexity Extraction Epic -- V12 Photon Kernel
BUILD_TAG_BASELINE: [PREVIOUS_TAG]
REPO: c:\WSGTA\universal-or-strategy
BRANCH: feature/phase7-sprint5-extraction
SPEC REF: docs/brain/phase7_complexity_epic_brief.md

Execute PLAN-THEN-EXECUTE PROTOCOL. Produce a written plan with helper
names, signatures, and caller impact. STOP and confirm before coding.
Post-edit: 
1. Run deploy-sync.ps1 + complexity_audit.py + bump BUILD_TAG.
2. If any new logic constraints or workflow quirks were discovered, append them to .agent/skills/bob-cli-mastery/SKILL.md before returning success.

--- TICKET BELOW ---
[paste full ticket content here]
```

---

## Tag Sequence (update as tickets complete)

| Ticket | BUILD_TAG_BASELINE (input) | BUILD_TAG_TARGET (output) |
|:-------|:--------------------------|:--------------------------|
| T-Q1   | 1111.007-phase7-t16       | 1111.007-phase7-tQ1       |
| T-W1   | 1111.007-phase7-tQ1       | 1111.007-phase7-tW1       |
| T-H    | 1111.007-phase7-tW1       | 1111.007-phase7-tH        |
| T-W2   | 1111.007-phase7-tH        | 1111.007-phase7-tW2       |
| T4     | 1111.007-phase7-tW2       | 1111.007-phase7-final     |

---

## Director Manual Gate (run AFTER each Bob session)

Bob's P6 verifier runs in Ask mode and cannot execute shell commands.
After Bob reports PASS, the Director must manually run:

```powershell
# 1. Confirm zero empty catches remain
grep -E "catch\s*\{\s*\}" src/V12_002.Orders.Callbacks.AccountOrders.cs src/V12_002.SIMA.Lifecycle.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Dispatch.cs

# 2. Confirm no new lock() introduced
grep -c "lock\s*(" src/*.cs

# 3. ASCII gate
python scripts/check_ascii.py

# 4. deploy-sync.ps1 (if not already run by Code mode)
powershell -File .\deploy-sync.ps1
```

Then press F5 in NinjaTrader and verify the BUILD_TAG banner.

---

## Known Workflow Behaviour

- Bob Orchestrator coordinates: Plan mode (forensics + plan) -> Code/Advanced mode
  (implementation + deploy-sync.ps1 + F5 test) -> Ask mode (P6 verification).
- Ask mode cannot run grep/powershell, so P6 directly inspects only some files
  and infers the rest from Code mode success. The Director manual gate above
  covers the gap.
- Adjudicator clarifications: paste to Antigravity for resolution, then select
  the matching pre-built Bob response. Do not send back to Architect.
