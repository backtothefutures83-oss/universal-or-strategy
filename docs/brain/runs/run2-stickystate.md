# Run 2 Mission Brief -- Decouple StickyState & IPC (Epic 1)
**BUILD_TAG:** Run2-StickyService
**Branch:** refactor/stickystate-service (0-delta from main, after Run 1 PR merged)
**Skill:** Read .agent/skills/code-structure/SKILL.md before designing.

## Goal
Extract serialization and IPC send/receive logic out of src/V12_002.StickyState.cs
into a standalone pure C# service so `dotnet test` runs without NinjaTrader.

## Surgical Targets (NEW files)
- src/Services/IStickyStateService.cs  (interface)
- src/Services/StickyStateService.cs   (extracted logic, pure C#, no NinjaTrader base class)

## Surgical Targets (MODIFY)
- src/V12_002.StickyState.cs
  - Replace inlined serialization/IPC blocks with calls to _stickyStateService
  - Instantiate StickyStateService in the appropriate lifecycle method
  - Remove now-dead inline code

## Invariants (Must Preserve)
- StickyStateService must accept all state via constructor injection (no global statics)
- DIFF LIMIT: under 500 lines total
- WHITESPACE MUTATION BANNED

## Verification Extras
- dotnet test must instantiate StickyStateService WITHOUT NinjaTrader running
  (prove NinjaTrader-free testability -- add a unit test stub if none exists)

## Commit Message
refactor: extract StickyState & IPC into StickyStateService (Epic 1) [Run2-StickyService]
