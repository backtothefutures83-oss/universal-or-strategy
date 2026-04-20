# Implementation Plan: V12.15 Sovereign Dashboard Visual Hardening

## Overview

This plan addresses the "missing visuals" and "broken layout" issues in the **Stack Registry**, **Sovereign Controls**, and **Mission Backlog** tabs. We will apply a high-fidelity "Anthropic Tactical" design system—reconciling Anthropic's brand guidelines with a "sharp but bubbly" aesthetic—and inject dynamic Mermaid visuals into each tab.

## Mission Objectives

1.  **Tab Restoration:** Fix the layout and visual integrity of the Stack, Controls, and Backlog tabs.
2.  **Visual Enrichment:** Add Mermaid-driven diagrams for the Tech Stack, Mission Timeline, and Security Logic.
3.  **Brand Alignment:** Strict enforcement of the Anthropic Palette (Orange, Blue, Green) and Typography (Poppins/Lora).
4.  **UI/UX Polish:** Apply "sharp but bubbly" styling (24px rounding, glassmorphism, precise 1px borders).

## Design System (Tactical Anthropic)

- **Colors:**
  - `--brand-orange`: #d97757 (Critical/Active/Hot)
  - `--brand-blue`: #6a9bcc (System/Info/Ingress)
  - `--brand-green`: #788c5d (Complete/Success/Physical)
  - `--brand-bg`: #1e1e1d (Charcoal Dark)
- **Shapes:** `border-radius: 24px` for main cards; `border-radius: 12px` for buttons/chips.
- **Typography:** `Poppins` (Bold) for IDs and Titles; `Lora` for rationale and body.

## Surgical Changes

### 1. Stack Registry (`#stack-tab`)

- **New Visual:** Add a `mermaid` Sovereign Dependency Graph showing the relationship between Morpheus Core, MCP, and Agent Harnesses.
- **UI Upgrade:** Replace static divs with "Sovereign Chip" cards using consistent glassmorphism.

### 2. Sovereign Controls (`#settings-tab`)

- **New Visual:** Add a "Security Pulse" CSS animation using SVG paths to represent the "Director's Gate" isolation.
- **UI Upgrade:** Refine the Scaffold Selection cards to be tactile, with clear "ACTIVE" states and hover-glow effects.

### 3. Mission Backlog (`#mission-backlog-tab`)

- **New Visual:** Add a `mermaid` Gantt chart showing the V12.15 Platinum Hardening phases.
- **UI Upgrade:** Replace the table with a "Unified Task Matrix" featuring custom status badges and "Breakthrough" indicators.

## Verification Steps

1.  **Link Audit:** Ensure `switchTab` correctly triggers Mermaid rendering for the new content.
2.  **Visual Proof:** Verify all three tabs now display the intended diagrams and follow the Anthropic color system.
3.  **Responsive Check:** Ensure the "sharp but bubbly" cards wrap correctly on smaller layouts.

---

_Status: Awaiting Director Approval (@Mohammed Khalid)_
