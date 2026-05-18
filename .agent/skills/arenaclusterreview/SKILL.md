---
name: arenaclusterreview
description: Execute the $arenaclusterreview workflow to review subgraphs via Arena AI, compound intelligence, and non-prescriptively hand off to Bob for epic planning.
---

# $arenaclusterreview Skill

This skill enforces the `$arenaclusterreview` workflow.

## Trigger
Use this skill whenever the user invokes `$arenaclusterreview` or requests a multi-agent subgraph review that culminates in a Bob `/epic-plan` handoff.

## Execution Steps

1. **Subgraph Review**: Generate a prompt for Arena AI to audit the target subgraph.
2. **Intelligence Compounding**: Analyze the Arena AI responses, extract breakthroughs, and generate a follow-up prompt for repair design that builds on these breakthroughs.
3. **Bob Handoff**: Synthesize the chosen design into an approach document and generate a strictly non-prescriptive prompt directing Bob to create an `/epic-plan`.

## CRITICAL REQUIREMENT: The Copy Button Rule
All prompts intended for the user to copy and paste into Arena AI or the Bob CLI **MUST** adhere to the following:

1. **Internal Enforcement**: The prompt must be output directly in the main chat response inside a standard Markdown code block (e.g., ` ```markdown `). **NEVER** place these prompts inside an artifact.
2. **External Requirement**: The generated prompt itself MUST include an explicit requirement for the models (Arena AI) to include a "Copy" button on the main page/panel of any UI, dashboard, or template they propose.

This dual-layer rule ensures the user can copy the prompt from the main chat, and any resulting design remains high-utility with one-click retrieval.
