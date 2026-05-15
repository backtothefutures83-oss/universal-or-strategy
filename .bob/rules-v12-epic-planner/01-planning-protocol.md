# V12 Epic Planner -- Planning Protocol Rules
# Loaded automatically when using /v12-epic-planner mode.

## Mandatory Gate Protocol

Every phase of the Epic Workflow has a mandatory STOP gate before proceeding.
You MUST output the gate message and HALT. Do not proceed autonomously.

| Phase | Command | Gate Output |
|-------|---------|------------|
| 1 | /epic-intake | [INTAKE-GATE] Scope alignment complete. Awaiting Director confirmation. |
| 2 | /epic-plan | [PLAN-GATE] Analysis and Approach documents complete. Awaiting Director approval. |
| 3 | /epic-validate | [VALIDATE-GATE] Architecture validation complete. Awaiting Director sign-off. |
| 4 | /epic-tickets | [TICKETS-GATE] Ticket breakdown complete. Awaiting Director approval to execute. |

## Zero src/ Access Rule

You CANNOT write to any file matching `^src/`. If you find yourself about to
edit a .cs file, STOP. Report: "[PLANNER-HALT] Epic planner mode does not permit
src/ edits. Switch to /v12-engineer mode for ticket execution."

## Document Integrity Rule

- One source of truth per document. NEVER fork analysis or approach into "v2" files.
- Update docs/brain/[epic]/*.md IN-PLACE when resolving review findings.
- Never delete the gate documents (00-scope.md, 01-analysis.md, 02-approach.md).

## jCodemunch-First Rule

Before making ANY claim about code structure, complexity, or callers -- verify with
jCodemunch tools. Claims not backed by tool output are protocol violations.

Required tool calls per phase:
- /epic-intake: get_file_outline, get_blast_radius, find_references, get_dependency_graph
- /epic-plan: get_blast_radius (depth 2), find_references, get_dependency_graph (both)
- /epic-validate: get_file_outline (re-verify live state matches approach)
- /epic-tickets: no additional tool calls required (plan is grounded already)

## V12 DNA Embed Rule

Every ticket file produced by /epic-tickets MUST embed the DNA guardrails:
- Zero new lock() statements
- Zero non-ASCII in string literals
- >= 15 LOC extraction floor per sub-method
- deploy-sync.ps1 mandatory after every src/ edit
- complexity_audit.py before/after comparison

## Ticket Self-Containment Rule

Each ticket file produced by /epic-tickets must be completely self-contained.
A Director opening a new Bob session must be able to run /ticket [path] with ZERO
additional context. This means:
- Target file paths must be explicit (not "the file from before")
- Sub-method names must be fully specified (not "break it into helpers")
- Acceptance criteria must be concrete and verifiable
