# Run 3 Mission Brief -- Decouple REAPER Risk Engine (Epic 2)
**BUILD_TAG:** Run3-ReaperService
**Branch:** refactor/reaper-service (0-delta from main, after Run 2 PR merged)
**Skill:** Read .agent/skills/code-structure/SKILL.md before designing.

## Goal
Extract REAPER risk scan logic (background-thread phantom order detection and
position mismatch correction) into a standalone pure C# service.

## Surgical Targets (NEW files)
- src/Services/IReaperRiskService.cs  (interface)
- src/Services/ReaperRiskService.cs   (extracted scan logic, pure C#)

## Surgical Targets (MODIFY)
- REAPER partial class file(s) -- identify via jcodemunch search_symbols

## Invariants (HARD -- Must Preserve)
- PHANTOM-FIX ordering: dict registration BEFORE expectedPositions update.
  The comment "dict BEFORE expectedPositions update (Phantom-Fix)" must survive
  at the call site after extraction. grep for it to confirm.
- REAPER reads entryOrders, activePositions, expectedPositions, _followerBrackets --
  these become constructor-injected dependencies. REAPER must NOT hold its own copy.
- DIFF LIMIT: under 500 lines total
- WHITESPACE MUTATION BANNED

## Verification Extras
- grep -r "dict BEFORE expectedPositions" src/ -> must be PRESENT (not 0)
- dotnet test must instantiate ReaperRiskService WITHOUT NinjaTrader running

## Commit Message
refactor: extract REAPER risk engine into ReaperRiskService (Epic 2) [Run3-ReaperService]
