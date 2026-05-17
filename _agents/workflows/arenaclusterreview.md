---
description: $arenaclusterreview -- Compound Intelligence Subgraph Review & Non-Prescriptive Bob Handoff
---

Use this workflow to conduct a deep, multi-agent review of a subgraph, compound the resulting intelligence to formulate a repair design, and subsequently hand off the execution to Bob (`/epic-plan`) using a strictly non-prescriptive approach.

## Phase 1: Subgraph Forensic Review (Arena AI)
1. **Identify the Subgraph**: Determine the exact boundaries, files, and architectural responsibilities of the subgraph under review.
2. **Generate the Review Prompt**: Create a prompt for the user to paste into Arena AI. The prompt must task the models with:
   - **Pattern-First Synthesis (MANDATORY)**: Identifying repeating structural anti-patterns and V12 DNA violations (e.g., TOCTOU, Bypassing Enqueue, Non-Atomic mutations) *before* listing individual bugs.
   - Grouping every localized logic flaw or bug under its root-cause architectural constraint failure.
   - Forcing the output format to include a "Systemic Anti-Patterns" section at the top.
3. **Copy Button Requirement (MANDATORY)**: 
   - **Internal**: The generated prompt MUST be provided directly in the main chat interface within a Markdown code block (Do NOT use artifacts).
   - **External**: The prompt itself MUST include a requirement for the models to include a "Copy" button on the main page of any UI or template they design.

## Phase 2: Compounding the Intelligence
1. **Analyze Arena Outputs**: Once the user pastes the Arena AI responses back, extract the breakthroughs and forensic findings.
2. **Generate the Repair Design Prompt**: Create a follow-up prompt for Arena AI to design the repair.
   - **Compound the Knowledge**: The prompt must explicitly list the breakthroughs and findings from Phase 1.
   - **Non-Prescriptive**: Ask the models to propose architectural solutions that adhere to V12 DNA constraints without dictating the exact implementation paths.
   - **Copy Button Mandate**: The prompt MUST explicitly state: "Any UI, dashboard, or template designed as part of this solution MUST include a 'Copy' button placed strictly on the main page/panel."
   - **Internal Copy Rule**: Provide this prompt directly in the main chat interface within a Markdown code block.

## Phase 3: Bob Handoff (`/epic-plan`)
1. **Synthesize Final Architecture**: Once a consensus or winning design emerges from Phase 2, synthesize the approach into an approach document (e.g., `02-approach.md`).
2. **Non-Prescriptive Bob Prompt**: Generate the final prompt for Bob.
   - Do NOT provide Bob with exact line-by-line code edits.
   - DO provide Bob with the forensic evidence, the chosen architectural strategy, and the V12 DNA constraints.
   - Instruct Bob to generate the `/epic-plan` (ticket breakdown) based on the approach.
   - **Internal Copy Rule**: Provide this prompt in the main chat interface within a Markdown code block.

## Phase 4: Mandatory Self-Improvement Audit
After EVERY use of this workflow, perform a post-use audit:
1. Did any step produce an unexpected result?
2. Was the "Copy Button" rule violated?
3. Update this workflow file if gaps are identified.
If no gaps: `workflow(arenaclusterreview): no gaps identified -- workflow correct as written.`
