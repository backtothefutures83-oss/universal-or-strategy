---
description: Full 5-run decoupling mission conductor. Runs all 5 epics in sequence, pausing only for plan approval (Gate 1), F5 compile (Gate 4), and PR merge confirmation between each run.
argument-hint: (no arguments required)
---
# EPIC DECOUPLE -- 5-RUN MISSION CONDUCTOR

**Mission:** V12 Universal OR Strategy -- Full Decoupling (All 5 Runs)
**Repo:** c:\WSGTA\universal-or-strategy
**Rules:** Load .bob/rules/dna.md before any action. All V12 DNA mandates apply.

---

## MISSION OVERVIEW

You are executing a 5-run sequential decoupling mission. You know all 5 runs upfront.
You auto-advance between phases within each run. Between runs, you pause only for:

1. Gate 1 -- Plan approval (once per run, at the start)
2. Gate 4 -- F5 in NinjaTrader (once per run, after self-audit passes)
3. PR merge -- Director confirms PR is merged before you start the next run

Do NOT ask for input at any other point. Report progress as you go.

**The 5 runs:**
```
Run 1: Fix CS0656 + StyleCop suppression      brief: docs/brain/runs/run1-cs0656.md
Run 2: Decouple StickyState & IPC             brief: docs/brain/runs/run2-stickystate.md
Run 3: Decouple REAPER Risk Engine            brief: docs/brain/runs/run3-reaper.md
Run 4: Decouple SIMA Fleet Coordinator        brief: docs/brain/runs/run4-sima.md
Run 5: Decouple Symmetry & Order + format     brief: docs/brain/runs/run5-symmetry.md
```

Emit this banner at the start:
```
[EPIC-DECOUPLE] 5-run mission loaded. Starting Run 1 of 5.
Runs: CS0656 Fix -> StickyState -> REAPER -> SIMA Fleet -> Symmetry + Format
Human gates per run: Plan Approval + F5 + PR Merge = 3 touches x 5 runs = 15 total
```

---

## RUN EXECUTION TEMPLATE

For each run, execute these phases using the brief file for that run.
The brief file tells you WHAT. These phases tell you HOW.

### Phase 1: Forensics & Design
- Read the run's brief file in full
- If the brief specifies a skill file, read it before designing
- Use jcodemunch-mcp `plan_turn` then `search_symbols` to locate all target symbols
- Draft the surgical plan: what changes, which files, which invariants, what the struct/interface/service looks like
- Present the plan clearly

**[GATE 1 -- STOP]**
```
[RUN N GATE 1] Design complete. Review above plan.
Type APPROVED to execute, or give feedback for revision.
```
Wait for Director input. If feedback given, revise and re-present. Do not proceed until APPROVED.

### Phase 2: Surgical Implementation
- Implement exactly what was approved in Phase 1
- Use Python extractor script for any block > 50 lines (manual copy-paste BANNED for >50 lines)
- SURGICAL ONLY: touch nothing outside the files listed in the brief
- WHITESPACE MUTATION BANNED
- DIFF LIMIT: under 500 lines total (unless brief explicitly exempts a commit)

After implementation emit:
```
[RUN N IMPL-DONE] Implementation complete. Starting self-audit.
```

### Phase 3: Self-Audit
Run ALL of the following, report every result:
```
powershell -File .\deploy-sync.ps1
grep -r "lock(" src/
grep -Prn "[^\x00-\x7F]" src/
powershell -File .\scripts\build_readiness.ps1
dotnet test Testing.csproj
```
Run any verification extras specified in the brief file.
Run Snyk code scan on new and modified files. Fix any HIGH/CRITICAL issues, rescan until clean.

Report:
```
[RUN N AUDIT]
deploy-sync   : PASS / FAIL
lock() audit  : CLEAN / FAIL [file:line]
unicode audit : CLEAN / FAIL [file:line]
compilation   : PASS / FAIL [errors]
unit tests    : PASS / FAIL / PRE-EXISTING [count]
snyk          : CLEAN / FIXED [what was fixed]
[brief extras]: PASS / FAIL
OVERALL       : PASS / FAIL
```

If OVERALL FAIL on any non-pre-existing gate -- HALT. Report to Director. Do not proceed.

### Phase 4: Compiler Gate
**[GATE 4 -- STOP]**
```
[RUN N F5-GATE] All automated gates PASSED.
deploy-sync   : PASS
lock() audit  : CLEAN
compilation   : PASS
tests         : PASS / PRE-EXISTING [N]

ACTION REQUIRED: Press F5 in NinjaTrader IDE.
When you see the BUILD_TAG banner, type: F5 done [BUILD_TAG for this run]
```
Wait for Director input.

### Phase 5: Commit
After Director types "F5 done [BUILD_TAG]":
```
git add [files changed in this run]
git commit -m "[commit message from brief file]"
git diff --stat HEAD~1
```

Emit:
```
[RUN N COMPLETE]
Commit : [hash]
Diff   : [stat]
PR desc: [one-line description for Director to use when opening the PR]
Pre-existing test failures: [list or NONE]
```

**[PR MERGE GATE -- STOP]**
```
[RUN N PR-GATE] Run N is committed.
Open PR with the description above.
When the PR is merged, type: PR merged Run N
```
Wait for Director input.

After Director types "PR merged Run N", emit:
```
[RUN N+1 STARTING] Advancing to Run N+1 of 5.
Branch: [branch name from next brief]
```
Then start Phase 1 for the next run immediately.

---

## MISSION COMPLETION

After Run 5 PR is merged, emit the final report:
```
[EPIC-DECOUPLE COMPLETE] All 5 runs finished.

Run 1  CS0656 Fix + StyleCop     : DONE
Run 2  StickyStateService        : DONE
Run 3  ReaperRiskService         : DONE
Run 4  SimaFleetService          : DONE
Run 5  SymmetryOrderService      : DONE
       dotnet format             : DONE

dotnet test WITHOUT NinjaTrader  : CONFIRMED (all 4 services)
F5 human gate                    : STILL REQUIRED (NinjaTrader compile)
Next milestone                   : /greploop via Greptile on mdasdispatch-hash fork
```
