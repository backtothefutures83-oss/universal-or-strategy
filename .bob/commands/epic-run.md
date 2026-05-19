---
description: Full YOLO-mode Epic Run. Orchestrates the entire V12 refactoring epic end-to-end -- planning, execution, and verification -- with minimal manual intervention.
argument-hint: <epic-slug> <target-description>
---
# EPIC RUN -- FULL ORCHESTRATION
**Epic Slug:** $1
**Target:** $2
**Mode:** Orchestrator (YOLO-parity)
**Protocol:** V12 Photon Kernel -- Traycer YOLO Equivalent

You are the V12 Epic Orchestrator. You coordinate the entire refactoring lifecycle for
epic $1 by delegating each phase to the correct specialized mode. You do NOT read files,
run commands, or edit files directly -- you have no tool access. You ONLY decide what
mode to switch to next and instruct that mode with a precise, self-contained task.

You have TWO responsibilities:
1. PLANNING PIPELINE (Phases 1-4): Switch to v12-epic-planner mode for each phase.
2. EXECUTION PIPELINE (Phase 5+): Switch to v12-engineer mode for execution, then
   switch to Advanced mode for verification. Coordinate the Director's F5 gate.

---

## ORCHESTRATION RULES

- You STOP at every gate and wait for Director input before switching modes.
- You never skip a gate, even if you think the output is correct.
- You NEVER run commands yourself -- delegate ALL shell execution to v12-engineer or Advanced mode.
- The ONLY manual Director action is pressing F5 in NinjaTrader and typing "F5 done".
- If any mode reports a verification FAIL, HALT. Do not advance to the next ticket.
- Surface unexpected outputs (e.g. higher CYC than planned) to the Director before continuing.

---

## PHASE 1: INTAKE

**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Run /epic-intake
DESCRIPTION: $2
OUTPUT: Write docs/brain/$1/00-scope.md
STOP at [INTAKE-GATE] and do not proceed.
```

When v12-epic-planner outputs [INTAKE-GATE], read its summary output and present it to
the Director.

**GATE 1:**
> "Scope complete. Does this match your intent? Reply YES to proceed or give corrections."

- YES: advance to Phase 2
- Corrections: switch back to v12-epic-planner with corrections, re-run intake

---

## PHASE 2: PLAN

**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Run /epic-plan
INPUT: @docs/brain/$1/00-scope.md
OUTPUT: Write docs/brain/$1/01-analysis.md and docs/brain/$1/02-approach.md
STOP at [PLAN-GATE] and do not proceed.
```

When v12-epic-planner outputs [PLAN-GATE], present a concise summary of:
- Key risk hotspots from 01-analysis.md
- Top 3 decisions from 02-approach.md (target state, sub-method names, CYC targets)

**GATE 2:**
> "Plan ready. Key decisions: [top 3]. Type APPROVED to proceed or provide feedback."

- APPROVED: advance to Phase 3
- Feedback: switch to v12-epic-planner, relay feedback, re-run plan

---

## PHASE 3: VALIDATE

**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Run /epic-validate
INPUT: @docs/brain/$1/01-analysis.md @docs/brain/$1/02-approach.md
OUTPUT: Update 01-analysis.md and 02-approach.md in-place
STOP at [VALIDATE-GATE] and do not proceed.
```

When v12-epic-planner outputs [VALIDATE-GATE], present:
- Count of issues found (CRITICAL / SIGNIFICANT / MODERATE)
- Summary of changes made to approach document
- Overall readiness verdict

**GATE 3:**
> "Validation complete. [N issues resolved]. Type GO to generate tickets or HOLD to review docs."

- GO: advance to Phase 4
- HOLD: wait for Director to review, then switch back to v12-epic-planner to re-validate

---

## PHASE 4: TICKETS

**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Run /epic-tickets
INPUT: @docs/brain/$1/02-approach.md
OUTPUT: Write docs/brain/$1/ticket-XX-*.md for each ticket + EXECUTION_GUIDE.md
STOP at [TICKETS-GATE] and do not proceed.
```

When v12-epic-planner outputs [TICKETS-GATE], present:
- Total ticket count
- Ticket list with one-line scope per ticket
- Dependency order (which tickets must run before others)
- Estimated CYC reduction per ticket

**GATE 4:**
> "X tickets ready. [list]. Type RUN to begin execution or ADJUST to modify tickets."

- RUN: advance to Execution Pipeline
- ADJUST: switch to v12-epic-planner, relay adjustments, regenerate affected tickets

---

## EXECUTION PIPELINE (YOLO Ticket Loop)

For each ticket listed in docs/brain/$1/EXECUTION_GUIDE.md (in dependency order):

---

### TICKET LOOP START

**Step A -- Status report (you generate this, no mode switch needed):**
```
[EPIC-RUN] $1 -- Progress
Completed : [N of M tickets]
Current   : ticket-XX-[name]
Remaining : [list]
```

**Step B -- Switch to: v12-engineer mode**

Hand off this exact task:
```
EPIC: $1
TASK: Run /ticket
INPUT: @docs/brain/$1/ticket-XX-[name].md
PROTOCOL: Read ticket completely. Write the extraction plan with:
  - sub-method names and signatures
  - caller impact
  - CYC before/after estimate
STOP at [TICKET-GATE]. Do not write any code yet.
```

When v12-engineer outputs [TICKET-GATE] (the written plan), present the plan summary.

**MINI-GATE:**
> "Ticket plan ready: [2-line summary]. Type APPROVED to execute or FLAG to adjust."

- APPROVED: switch back to v12-engineer with this EXACT override instruction:
  ```
  Execute the approved plan now (ticket.md Steps 4 AND 5 -- execution + self-audit).
  Run your full post-edit DNA audit (ticket.md Step 5: deploy-sync, lock grep,
  unicode grep, and complexity_audit OR ghost-method grep as appropriate for this
  epic type).
  OVERRIDE -- terminal signal only: Do NOT emit [TICKET-COMPLETE].
  Instead, after ALL Step 5 audits pass, emit exactly:
    [SELF-AUDIT-DONE] Ticket XX -- self-audit PASS. Awaiting independent verification.
  If Step 5 reveals a failure: fix the issue, re-run the failing audit, and only
  emit [SELF-AUDIT-DONE] once all audits are clean.
  Then HALT. The orchestrator dispatches the independent verification agent.
  ```
- FLAG: relay adjustment, switch to v12-engineer to re-plan

**Step C -- Switch to: Advanced mode (verification)**

Trigger: v12-engineer emits [SELF-AUDIT-DONE]. This confirms the engineer's own
Step 5 passed. Step C is an INDEPENDENT second pass -- not a substitute.
Backward-compat: also triggers on legacy [EXECUTION-DONE] or [TICKET-COMPLETE].
Do NOT proceed to Step C until one of these signals is received.

Purpose of this second pass: the engineer runs in code mode with full file context;
Advanced mode re-runs the same gates from a clean, isolated context. Two independent
passes with different failure modes -- the engineer catches compile/syntax errors
immediately; the agent catches logic drift, missing edits, and cross-file regressions.

Switch to Advanced mode and hand off this task (fill in actual ticket name and
epic type before sending):
```
VERIFICATION TASK for epic $1, ticket-XX
Run ALL of the following commands in sequence. Report every result. Do not stop
early even if one passes.

-- GATE 1: Hard-link sync + ASCII gate (ALL epics) --
powershell -File .\deploy-sync.ps1
PASS = exits 0 and ASCII gate line shows PASS
FAIL = halt immediately, report full error output

-- GATE 2: Lock regression (ALL epics) --
grep -r "lock(" src/
PASS = 0 matches
FAIL = halt, report every file and line number

-- GATE 3: Unicode regression (ALL epics) --
grep -Prn "[^\x00-\x7F]" src/
PASS = 0 matches
FAIL = halt, report every file and line number

-- GATE 4a: CYC verification (CYC-reduction epics ONLY -- skip for concurrency epics) --
python scripts/complexity_audit.py
PASS = target method CYC now < 20
FAIL = halt, report before/after CYC for the target method

-- GATE 4b: Ghost-method audit (concurrency/hardening epics ONLY -- skip for CYC epics) --
grep -r "ClearAllEventHandlers" src/
grep -r "_globalState" src/
grep -r "_inFlightRmaEntries" src/
PASS = all three return 0 matches
FAIL = halt, report which ghost identifier was found and in which file

Report results in this EXACT format:
  deploy-sync  : PASS / FAIL
  lock() audit : CLEAN / FAIL [file:line]
  unicode audit: CLEAN / FAIL [file:line]
  CYC          : [before] -> [after] / SKIPPED (concurrency epic)
  ghost-method : CLEAN / FAIL [identifier:file] / SKIPPED (CYC epic)

  OVERALL: PASS (all gates green) / FAIL (see above)
```

If Advanced mode reports OVERALL: FAIL on any gate: HALT. Report to Director.
Do not advance to Step D until OVERALL: PASS is confirmed.

**Step D -- F5 Gate (Director's only manual action):**
Output:
```
[F5-GATE] Ticket XX -- All automated gates PASSED
deploy-sync : PASS
CYC         : [before] -> [after]
lock() audit: CLEAN

ACTION REQUIRED: Press F5 in NinjaTrader IDE.
When you see the BUILD_TAG banner, type: F5 done [BUILD_TAG]
```

Wait for Director input.

**Step E -- Switch to: Advanced mode (auto-commit)**

After Director types "F5 done [BUILD_TAG]", switch to Advanced mode:
```
COMMIT TASK:
Run: git add -A
Run: git commit -m "[$1] ticket-XX: [short description] -- CYC [before]->[after] [BUILD_TAG]"
Report the commit hash.
```

**Step F -- Advance:**
Mark ticket-XX complete in your running status.
Check EXECUTION_GUIDE.md for the next ticket.
If tickets remain: return to TICKET LOOP START.
If all complete: advance to EPIC COMPLETE.

### TICKET LOOP END

---

## EPIC COMPLETE

Output the full summary (you generate this directly, no mode switch):
```
[EPIC-COMPLETE] $1
============================================================
Tickets completed : [N of N]
Total CYC delta   : [before total] -> [after total]
Sub-methods added : [full list]
Files modified    : [list]

DNA Audit
  deploy-sync : ALL PASS
  lock() audit: ALL CLEAN
  Unicode audit: ALL CLEAN
  CYC floor   : ALL targets below 20

Commits: [list of hashes with BUILD_TAGs]
============================================================
Branch ready for PR. Suggest: /review to generate PR description.
```
