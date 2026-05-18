---
name: bob-epic-workflow-builder
description: Use when creating a new Bob CLI epic workflow -- porting a Traycer workflow, designing a custom pipeline from scratch, or adding new slash commands and custom modes to the .bob/ directory. Covers phase design, gate architecture, orchestrator chaining, YOLO-parity commands, and the rules file convention.
---

# Bob Epic Workflow Builder

## Overview

A Bob epic workflow is a set of coordinated slash commands + a custom mode + a rules file
that automates a multi-phase development pipeline. This skill teaches you to build them
from any source: a Traycer workflow, a verbal description, or a set of existing commands
you want to chain.

**Core principle:** Every workflow has two layers -- a PLANNING layer (docs/ only, no src/)
and an EXECUTION layer (src/ edits, verified gates). Design both separately, then chain them
with a YOLO-parity orchestrator command.

---

## Bob Mode Mechanics (Know Before You Build)

Before designing any workflow, internalize these facts from the official Bob docs:

| Mode | Tool Access | Key Constraint |
|------|------------|----------------|
| **Orchestrator** | **NONE** | Can only decide mode switches and coordinate. Cannot read files, run commands, or edit. |
| **Plan** | read, edit (markdown only), browser, mcp | Cannot edit non-markdown files. Perfect for docs/ artifacts. |
| **Code** | read, edit, command | Standard implementation mode. |
| **Advanced** | read, edit, command, mcp | Use for verification tasks needing both shell commands AND MCP tools. |
| **Ask** | read, browser, mcp | No file edits. Use for pure analysis. |
| **Custom modes** | As configured | `fileRegex: ^docs/` enforces plan-only for planner modes. |

**Orchestrator delegation = mode switching.** When the orchestrator "delegates" a phase,
Bob literally switches into that mode for the sub-task. The orchestrator cannot run ANY
tool call itself -- ALL execution must be delegated to a mode with the right tool access.

**Context mentions in sub-tasks:** When the orchestrator hands off a task, reference files
using `@docs/brain/$1/file.md` syntax. For large files, use line ranges:
`@docs/brain/$1/01-analysis.md:1-50` to pass only the relevant section.

**Checkpoints are automatic.** Bob creates a checkpoint before every file modification.
This is a free safety net -- no need to add checkpoint commands to the workflow.
If a ticket goes wrong, the Director can restore from checkpoint without losing prior work.

**Context window limit: ~100k tokens for quality, 140k auto-condenses.** Bob's 200k window
reserves 50k for responses, leaving ~150k usable. Quality noticeably degrades past ~100k.
Bob auto-condenses at 140k (lossy -- specific constraints and edge cases may be lost).
Orchestrator mode is especially vulnerable: chain stalling, infinite loops, and failure to
complete are documented symptoms of context poisoning in Orchestrator sessions.

**There is no recovery prompt for context poisoning.** Only a new session fixes it.

**Context poisoning symptoms to watch for:**
- Orchestrator stops advancing between phases
- Orchestrator re-runs a phase it already completed
- Gate questions stop making sense relative to the output
- Sub-task briefs start referencing wrong file paths or wrong ticket numbers
- If any of these occur: abandon the session, start fresh, resume from EXECUTION_GUIDE.md

**Mitigation pattern for long epics:**
- Planning session (phases 1-4): produces all artifacts, typically stays under 100k
- Execution session: fresh orchestrator session, loads EXECUTION_GUIDE.md as starting context
- Each ticket's task brief is self-contained, so fresh sessions work correctly
- Rule of thumb: if the epic has > 3 tickets, split planning and execution into two sessions
- For very large epics (>8 tickets), run execution in batches of 3-4 tickets per session

---

## When to Use

- Porting a Traycer Phases or Epic workflow into Bob
- Creating a custom multi-phase pipeline (e.g. dead-code sweep, PR audit, benchmark loop)
- Adding a new Bob mode with specialized constraints
- Designing a YOLO-parity orchestrator that automates an entire pipeline end-to-end

**Not for:** Single-command tools or minor edits to existing commands.

---

## File Map

```
.bob/
  commands/
    [phase1].md          # Individual phase commands (manual fallback)
    [phase2].md
    [phase3].md
    [pipeline-run].md    # YOLO-parity orchestrator (chains all phases)
  custom_modes.yaml      # Mode definitions (planner + engineer roles)
  rules-[mode-slug]/
    01-[protocol].md     # Behavioral rules loaded by the mode
```

**Rules directory naming:** `.bob/rules-{mode-slug}/` is the confirmed correct path.
Files are loaded alphabetically and combined with the mode's `customInstructions`.
The alternative `.bobrules-{mode-slug}` (single file) also works but directory is preferred.

---

## Step-by-Step: Building a New Workflow

### Step 1 -- Extract the Phase Map

From the Traycer workflow (or verbal description), identify:

| # | Phase name | Input | Output | Gate |
|---|-----------|-------|--------|------|
| 1 | Intake | user description | `00-scope.md` + `01-analysis.md` | Director confirms scope |
| 2 | Plan | scope + analysis | `02-approach.md` | Director reviews and approves plan |
| 3 | Breakdown | approved plan | `ticket-01.md`, etc. | Director approves tickets |
| 4 | Execution | ticket file | src/ edits (strict Red-Green-Refactor TDD loop per ticket) | Validator verifies per ticket |
| 5 | Final Review | all tickets | Epic Summary | Verify all ticket implementations after epic is done |

**Rule:** Every phase has exactly ONE input, ONE output artifact, and ONE gate message.
If a phase produces multiple artifacts, it's actually two phases -- split it.

### Step 2 -- Create Phase Commands (`.bob/commands/[phase].md`)

Each phase command is a markdown file with a YAML frontmatter block and a body.

```markdown
---
description: [What this phase does -- one line]
argument-hint: <slug> [optional-args]
---
# PHASE NAME -- [WORKFLOW NAME]

[Phase body: instructions for the sub-agent running this phase]

**GATE:** Output [GATE-NAME] and stop. Do not advance until Director confirms.
```

**Rules for phase commands:**
- One command per phase file. Never chain phases inside a single command.
- Always end with a named gate: `[INTAKE-GATE]`, `[PLAN-GATE]`, etc.
- Use `$1`, `$2` for slug and description arguments.
- Reference artifact paths explicitly: `docs/brain/$1/00-scope.md` not "the scope file."
- If the phase touches src/: embed the post-edit verification sequence inside the command.

### Step 3 -- Create the YOLO Orchestrator Command

The orchestrator command chains all phases and adds the execution loop.
It runs in **Orchestrator mode** -- it delegates, never codes directly.

**Critical:** The orchestrator has NO tool access. Every shell command, file read, and
file write must be delegated to a mode that has the right tools:
- Planning artifacts → delegate to `v12-epic-planner` (Plan-class mode, markdown edit only)
- Code execution → delegate to `v12-engineer` (Code-class mode, full edit + command)
- Verification shell commands → delegate to `Advanced` mode (command + mcp access)
- The orchestrator itself only produces text output (status reports, gate prompts)

```markdown
---
description: Full YOLO-mode run. Orchestrates [workflow] end-to-end.
argument-hint: <slug> <description>
---
# [WORKFLOW] RUN -- FULL ORCHESTRATION

## ORCHESTRATION RULES
[Brief rules: strict sequential roles (Orchestrator -> Worker -> Validator), no src/ direct edits by orchestrator, stop at every gate]

## PHASE 1: INTAKE & PLAN
Delegate to [planner-mode]:
  [draft scope, analysis, approach]

### Subagent Review (Mandatory)
Delegate to Validator/Adjudicator subagent:
  Review the implementation plan for V12 DNA compliance:
  - Lock-free patterns (no `lock()` blocks)
  - ASCII-only compliance (no Unicode, emoji, curly quotes)
  - Architectural soundness (FSM/Actor model adherence)
  - Test coverage adequacy (xUnit/FsCheck requirements)
  Output: Critique document highlighting any violations or risks

**GATE 1:** Present BOTH the plan AND the subagent's critique to Director. Ask: "Approve plan?"

## PHASE 2: TICKET BREAKDOWN
Delegate to [planner-mode]:
  [break down approved plan into explicit tickets]

### Subagent Review (Mandatory)
Delegate to Validator/Adjudicator subagent:
  Review the ticket breakdown for completeness and sequencing:
  - Ticket independence (can each be executed standalone?)
  - Clear scope (unambiguous acceptance criteria)
  - Verification criteria (testable success conditions)
  - Dependency correctness (proper sequencing)
  Output: Critique document highlighting any gaps or ordering issues

**GATE 2:** Present BOTH the tickets AND the subagent's critique to Director. Ask: "Approve tickets?"

## EXECUTION PIPELINE: [LOOP]
For each [ticket/item], run sequentially (only one worker or validator active at a time):
  Step A: Status Outline - Review ticket requirements and current state.
  Step B: Write Failing Test (RED) - Delegate to [worker-mode] to write the failing xUnit/FsCheck test first AND run `dotnet test` to confirm it fails for the expected reason.
  Step C: Implement Logic (GREEN) - Delegate to [worker-mode] to implement the target logic.
  Step D: Verify & Refactor - Delegate to [validator-mode] (e.g. Advanced mode) to run `powershell -File .\deploy-sync.ps1`.
  Step E: TDD HOOK - If Validator tests fail ($LASTEXITCODE is not 0), Validator captures the error output and Orchestrator routes it back to [worker-mode] to fix immediately. Loop until tests pass without human intervention unless stuck > 3 times.
  Step F: Skill Synthesis (Self-Improvement) - After successful ticket execution, analyze the completed work to identify: novel patterns, testing strategies, architectural solutions, or fixes discovered during execution. If novel learnings exist, MUST either update an existing relevant `SKILL.md` in `.agent/skills/` or create a new specialized skill file in `.agent/skills/` using the standardized skill format. This fulfills Section 13 (Autonomous Skill Creation & Self-Improvement) of AGENTS.md. Only update skills if genuinely novel patterns emerge (avoid redundant updates).
  Step G: Auto-commit & Advance - Delegate to [validator-mode] to commit and move to next ticket.

## PHASE 3: FINAL REVIEW
Delegate to [validator-mode]:
  Review all tickets implementation after all phases of epic are done.
**GATE 3:** Ask Director: "Epic Complete. Confirm merge?"

## COMPLETE
[Summary output format]
```

**Orchestrator rules:**
- Every gate asks ONE short question. Director answers in one word/phrase.
- All shell commands (deploy-sync, grep, complexity_audit) are listed explicitly.
- The human gate is the MINIMUM required human action -- trim it to one line.
- The COMPLETE section outputs a machine-readable summary (counts, CYC, commits).

### Step 4 -- Add the Custom Mode (`.bob/custom_modes.yaml`)

Add a YAML block for each new mode (planner + engineer if they don't exist):

```yaml
- slug: [mode-slug]
  name: [Display Name]
  roleDefinition: >
    [Who is this agent? What is its primary constraint? What are its outputs?
    What tools does it use? Keep to 5-8 sentences.]
  whenToUse: >
    [One sentence: when to switch to this mode. No workflow summary.]
  groups:
    - read
    - - edit
      - fileRegex: ^docs/        # restrict to docs/ for planners
        description: Documentation files only
    - mcp
  customRules:
    - dna: rules-v12-engineer/dna.md
    - protocol: rules-[mode-slug]/01-[name].md
```

**Critical:** `fileRegex: ^docs/` is the PLAN-ONLY enforcement mechanism for planner modes.
Engineer modes omit this restriction. Never skip it on a planner.

### Step 5 -- Create the Rules File (`.bob/rules-[mode-slug]/01-[protocol].md`)

The rules file defines behavioral constraints loaded automatically by the mode.

Required sections:
1. **Gate Protocol table** -- maps each phase to its gate message and STOP behavior
2. **File access rule** -- what can and cannot be written
3. **Tool-first rule** -- which tools must be called before making any claim
4. **Self-containment rule** -- every produced artifact must be usable in a new session

```markdown
# [Mode] -- [Protocol Name] Rules

## Mandatory Gate Protocol
| Phase | Command | Gate Output |
|-------|---------|------------|
| 1 | /[phase1] | [GATE-1] message |

## Zero [restricted area] Access Rule
[What the agent cannot touch and what to output if it tries]

## [Tool]-First Rule
[What must be verified with tools before claims are made]

## Self-Containment Rule
[How artifacts must be structured so a new session can use them]
```

---

## Porting a Traycer Workflow to Bob

| Traycer Concept | Bob Equivalent |
|----------------|----------------|
| Phases (Kanban columns) | Individual phase commands (`/[phase].md`) |
| YOLO for Phases (fixed config) | YOLO orchestrator command (`/[pipeline-run].md`) |
| Smart YOLO for Epic | Orchestrator mode + `/epic-run` style command |
| Plan handoff | Orchestrator switches to Plan/planner mode with explicit task brief |
| Verification step | Orchestrator switches to Advanced mode, runs shell gates there |
| Next Phase advance | Orchestrator receives gate output → switches to next mode |
| Intent Clarification | Phase 1 (intake) interview questions embedded in command |
| Agent selection | Mode switch (`v12-epic-planner`, `v12-engineer`, `Advanced`) |
| Auto-commit | Delegated to Advanced/Code mode (orchestrator has no command access) |
| Checkpoint/rollback | Automatic in Bob -- no command needed; Director restores from UI |

**The one thing Traycer has that Bob doesn't natively:**
Traycer's Smart YOLO can evolve specs mid-execution (update ticket files based on discoveries).
In Bob, the orchestrator CAN update ticket files -- embed this instruction:
```
If execution reveals scope changes, update docs/brain/$1/ticket-XX.md in-place
and report the change to Director before continuing.
```

---

## Testing Automation (Red-Green-Refactor TDD)

Inspired by Droid Missions, Bob Epic Workflows must integrate a strict, sequential Autonomous TDD loop for every ticket in the execution phase.

### Strict Role Sequencing

**Only one active role at a time.** The Orchestrator coordinates, but never executes directly:

1. **Orchestrator**: Routes tasks between Worker and Validator. Monitors test results and decides when to loop back or advance.
2. **Worker**: Writes code (tests first, then implementation). Receives error feedback from Validator via Orchestrator.
3. **Validator**: Runs tests and verification commands. Reports results back to Orchestrator.

**Critical:** The Orchestrator has NO tool access. It cannot run `deploy-sync.ps1` or read test output directly. All execution must be delegated to Worker (Code mode) or Validator (Advanced mode).

### The Red-Green-Refactor Pipeline

For every ticket in the execution phase, follow this exact sequence:

#### RED Phase (Write Failing Test)
1. **Worker** writes the failing `xUnit` or `FsCheck` test first, covering the target behavior, and runs it to ensure it fails.
2. **Gate**: If the test passes unexpectedly, Worker must revise the test. If it fails correctly, advance to GREEN.

#### GREEN Phase (Implement Logic)
1. **Worker** implements the minimum code required to make the test pass.
2. **Worker** does NOT refactor or optimize yet -- just make it work.

#### REFACTOR/VERIFY Phase (Run Full Verification)
1. **Validator** runs `powershell -File .\deploy-sync.ps1` (builds, syncs hard links, runs all tests).
2. **Validator** reports the exit code and any test failures to Orchestrator.

#### TDD HOOK (Autonomous Fix Loop)
- **If tests pass** ($LASTEXITCODE == 0): Advance to Human Gate (Step F).
- **If tests fail** ($LASTEXITCODE != 0):
  1. **Validator** captures the full error output (test names, stack traces, failure messages).
  2. **Orchestrator** routes the error output back to **Worker** with instruction: "Fix the failing tests."
  3. **Worker** analyzes the errors and makes corrections.
  4. Loop back to REFACTOR/VERIFY phase.
  5. **Repeat until tests pass** OR the loop fails 3 consecutive times.
  6. **If stuck after 3 loops**: Orchestrator halts and asks Director for guidance.

**Do NOT ask the Director for help on the first or second test failure.** The autonomous loop is designed to self-correct. Only escalate after 3 failed attempts.

### Integration with Execution Pipeline

The TDD loop is embedded in the EXECUTION PIPELINE (Step 3) of every Bob epic workflow. The orchestrator command must explicitly delegate each phase:

```markdown
## EXECUTION PIPELINE: [LOOP]
For each ticket:
  Step A: Plan Ticket (Planner outlines steps)
  Step B: RED - Write Failing Test (Worker writes test, Validator confirms it fails)
  Step C: GREEN - Implement Logic (Worker writes code)
  Step D: REFACTOR/VERIFY (Validator runs deploy-sync.ps1)
  Step E: TDD HOOK (If fail: loop back to Worker; if pass: advance)
  Step F: Human Gate ("F5 done")
  Step G: Skill Synthesis (Planner analyzes work, updates/creates SKILL.md files)
  Step H: Auto-commit & Advance (Validator commits)
```

### Testing Tools

- **xUnit**: For unit tests and integration tests in C#.
- **FsCheck**: For property-based testing (randomized input generation).
- **deploy-sync.ps1**: Runs full build + test suite + hard-link sync. This is the canonical verification gate.

### Self-Improvement Protocol

After every epic execution, audit the TDD loop:
1. Did any test fail more than 3 times before passing? → Analyze why and update Worker instructions.
2. Did any ticket skip the RED phase? → Protocol violation. Update orchestrator command to enforce RED first.
3. Did the Validator fail to capture error output? → Add explicit "capture stderr" instruction to Validator delegation.

---

## Quick Checklist

- [ ] Phase map drawn (input / output / gate per phase)
- [ ] One `.md` file per phase in `.bob/commands/`
- [ ] Each phase ends with a named `[GATE]` message
- [ ] YOLO orchestrator command chains all phases
- [ ] Orchestrator lists ALL auto-run verification commands explicitly
- [ ] Human gate trimmed to minimum (one phrase max)
- [ ] Custom mode added to `custom_modes.yaml` with correct `fileRegex` for planners
- [ ] Rules file created in `.bob/rules-[mode-slug]/`
- [ ] Artifact paths are explicit (`docs/brain/$1/00-scope.md` not "the scope doc")
- [ ] Each artifact is self-contained (usable in a fresh Bob session with no prior context)
- [ ] `bob-cli-mastery` skill updated with new entry point

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Two phases in one command file | Split into separate files. Each command = one phase = one gate. |
| Gate with no stop | Add `[GATE-NAME]` token + explicit "HALT until Director confirms." |
| Orchestrator running commands directly | Orchestrator has NO tool access. Delegate shell commands to Advanced mode. |
| Orchestrator reading files directly | Orchestrator has NO read access. Sub-agents read files; orchestrator gets their output. |
| Missing `fileRegex` on planner mode | Planner gains src/ access. Add `fileRegex: ^docs/`. |
| Ticket not self-contained | A ticket referencing "the approach from before" fails in a new session. Embed all paths. |
| Long epic in one session (>3 tickets) | Context hits ~100k quality wall. Split: planning session + separate execution session. |
| Trying to fix a stalled orchestrator | Context poisoning has no recovery prompt. Abandon session. Resume from EXECUTION_GUIDE.md. |
| Using broad `@dir` mentions | Wastes tokens. Use `@file` or `@file:line-range` for targeted context. |
| Trusting Bob's Ask/Plan mode for shell gates | Neither has `command` access. Shell verification must go to Code or Advanced mode. |
| Skipping the rules file | Mode has no behavioral constraints. It will drift. Always create the rules file. |

---

## Self-Improvement Protocol

After building any new workflow, answer:
1. Did any phase produce an unexpected output format? → Update the phase command's output spec.
2. Did any gate question confuse the Director? → Simplify to one word trigger.
3. Did the orchestrator need to run a shell command not in its list? → Add it to the orchestrator.
4. Did a ticket fail to self-contain? → Add the missing context to the ticket template in `/epic-tickets`.

Commit improvements to the skill and the commands in the same PR.
