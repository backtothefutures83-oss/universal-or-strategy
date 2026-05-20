# Mission Snapshot: Fix CS0656 + V12 Decoupling Roadmap
**Mission Name:** Fix CS0656 Compiler Error (PR 106) + 5-Run Decoupling Roadmap
**Plan Path:** docs/brain/implementation_plan.md (full 5-run roadmap)
**Context Files:**
- docs/brain/pr_106_bot_results.md (PR 106 compiler error forensics)
- .bob/rules/dna.md (global V12 DNA rules -- all Bob modes)
- .bob/commands/repair-pr.md (repeatable /repair-pr command)
- .agent/skills/code-structure/SKILL.md (V12-adapted service layer skill)

## Completed Steps
1. Bob IDE + Shell docs ingested into docs/brain/bob_docs_synthesis.md
2. Global DNA rules created at .bob/rules/dna.md (all Bob modes now load them)
3. /repair-pr custom command created at .bob/commands/repair-pr.md (non-prescriptive, repeatable)
4. code-structure skill created at .agent/skills/code-structure/SKILL.md (adapted from Micky @rasmic)
5. Full 5-run roadmap written to docs/brain/implementation_plan.md
6. Collaborator mdasdispatch-hash invited (Greptile trial account -- pending acceptance)
7. Workflow adjustment approved: agents NO LONGER stop between routine P4->P5->P6 phases
8. graphify update . run -- 58,830 nodes synced

## The 5-Run Roadmap (Approved by Director)
- Run 1 (NEXT): /repair-pr for CS0656 fix + .editorconfig StyleCop suppression
- Run 2: Epic 1 -- Decouple StickyState & IPC into src/Services/
- Run 3: Epic 2 -- Decouple REAPER Risk Engine
- Run 4: Epic 3 -- Decouple SIMA Fleet Coordinator
- Run 5: Epic 4 -- Decouple Symmetry & Order Management

## Workflow Rules (Director-Approved)
- Agents auto-advance between phases WITHOUT stopping for approval
- Only 3 genuine human gates remain:
  1. Plan approval (before any src/ edit)
  2. F5 in NinjaTrader (until decoupling removes this requirement)
  3. PR merge approval
- PRs must stay small -- 1 logical change per PR, max ~500 lines changed
- StyleCop: suppress via .editorconfig in Run 1, full dotnet format after Run 5

## Key Decisions Validated
- HeaderConfigSnapshot struct replacement: CONFIRMED VALID (minimum change, typed)
- Decoupled Service Layers: CONFIRMED VALID (matches Micky code-structure pattern)
- Micky's "repeated runtimes" = same operation copy-pasted across partial class files
  (bug fix in one path does not propagate to others -- service layer fixes this)

## Fork-and-Scan (Greptile)
- mdasdispatch-hash invited as collaborator -- awaiting acceptance
- Plan: fork repo to mdasdispatch-hash, enable Greptile on fork, run /repair-pr loop
  until Greptile gives perfect score, then push clean commits back to main

## Open Blockers
- None. Ready to start Run 1 in new session.
