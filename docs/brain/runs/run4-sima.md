# Run 4 Mission Brief -- Decouple SIMA Fleet Coordinator (Epic 3)
**BUILD_TAG:** Run4-SimaFleetService
**Branch:** refactor/sima-fleet-service (0-delta from main, after Run 3 PR merged)
**Skill:** Read .agent/skills/code-structure/SKILL.md before designing.

## Goal
Extract SIMA fleet dispatch coordinator -- Photon ring buffer dispatch path and
FollowerBracketFSM management -- into a standalone pure C# service.

## Surgical Targets (NEW files)
- src/Services/ISimaFleetService.cs  (interface)
- src/Services/SimaFleetService.cs   (extracted dispatch logic, pure C#)

## Surgical Targets (MODIFY)
- SIMA dispatch partial class file(s) -- identify via jcodemunch search_symbols
  (ExecuteSmartDispatchEntry, Dispatch_PublishMarketBracketToPhoton,
   Dispatch_PublishLimitEntryToPhoton, FollowerBracketFSM transitions)

## Invariants (HARD -- Must Preserve)
- Build 981 Protocol: Direct writes to stopOrders during bracket submission are MANDATORY.
  DO NOT wrap in Enqueue. grep for stopOrders after extraction -- all writes must be direct.
- Phantom-Fix ordering: dict registration BEFORE expectedPositions update.
- FollowerBracketFSM Replace FSM: PendingCancel -> confirm -> Submitting -> Submit.
  Raw Cancel() + Submit() sequence is BANNED.
- Photon ring dependencies (_photonPool, _photonDispatchRing, _photonSideband,
  _photonMmioMirror, _photonShadowSalt) become constructor-injected.
- DIFF LIMIT: under 500 lines total
- WHITESPACE MUTATION BANNED

## Verification Extras
- grep -rn "stopOrders" src/ -> confirm zero Enqueue wrappers on stopOrders writes
- python scripts/amal_harness.py -> Allocated=0B, Gen0=0 required (HARD gate)
- dotnet test must instantiate SimaFleetService WITHOUT NinjaTrader running

## Commit Message
refactor: extract SIMA fleet coordinator into SimaFleetService (Epic 3) [Run4-SimaFleetService]
