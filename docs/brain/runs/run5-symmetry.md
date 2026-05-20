# Run 5 Mission Brief -- Decouple Symmetry & Order Management + dotnet format (Epic 4)
**BUILD_TAG:** Run5-SymmetryService
**Branch:** refactor/symmetry-order-service (0-delta from main, after Run 4 PR merged)
**Skill:** Read .agent/skills/code-structure/SKILL.md before designing.

## Goal
Extract Symmetry dispatch and Order Management tracking into a standalone pure C# service.
Then run the deferred `dotnet format` across the full codebase (suppressed since Run 1).

## Surgical Targets (NEW files)
- src/Services/ISymmetryOrderService.cs  (interface)
- src/Services/SymmetryOrderService.cs   (extracted logic, pure C#)

## Surgical Targets (MODIFY)
- Symmetry partial class file(s) -- identify via jcodemunch search_symbols

## Invariants (HARD -- Must Preserve)
- MoveSync / Follower Order Replace FSM: PendingCancel -> confirm -> Submitting -> Submit.
  grep for "PendingCancel" in SymmetryOrderService.cs after extraction -- must be PRESENT.
  Raw Cancel() + Submit() in sequence is BANNED.
- WHITESPACE MUTATION BANNED (for the extraction commit)

## TWO COMMITS REQUIRED (keep them separate)
1. Extraction commit (under 500 lines):
   git commit -m "refactor: extract Symmetry & Order Management into SymmetryOrderService (Epic 4) [Run5-SymmetryService]"

2. Format commit (line limit exempt -- pure whitespace, no logic):
   Run: dotnet format
   git commit -m "style: apply dotnet format across full codebase (deferred from Run 1) [Run5-Format]"

## Verification Extras
- grep -rn "PendingCancel" src/Services/SymmetryOrderService.cs -> must be PRESENT
- dotnet test must instantiate SymmetryOrderService WITHOUT NinjaTrader running
- python scripts/amal_harness.py -> Allocated=0B, Gen0=0 required

## Final Milestone Confirmation (in status report)
After both commits confirm:
- StickyStateService   : DONE (Run 2)
- ReaperRiskService    : DONE (Run 3)
- SimaFleetService     : DONE (Run 4)
- SymmetryOrderService : DONE (Run 5)
- dotnet format        : DONE (Run 5)
- dotnet test runs WITHOUT NinjaTrader for all 4 services: CONFIRMED
- Next: /greploop via Greptile on mdasdispatch-hash fork
