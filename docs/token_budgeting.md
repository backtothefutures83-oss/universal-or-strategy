# Droid Token Budgeting: V12.15 Multiplier Map

This document tracks the price-per-token multipliers for all supported Droid variants within the Morpheus substrate. These values inform the "Performance-per-Token" (PPT) optimization logic used in automated task routing.

## Model Pricing Tiers

| Model Variant                 | Multiplier | Tier            | Notes                                |
| :---------------------------- | :--------- | :-------------- | :----------------------------------- |
| **Claude Opus 4.7**           | **0.5x**   | Budget Frontier | Current 50% promo. SOTA leader.      |
| **Claude Sonnet 4.6**         | **1.2x**   | Mid-Range       | Standard agentic coding variant.     |
| **Claude Opus 4.6**           | **2.0x**   | Premium         | High deep-reasoning reliability.     |
| **GPT-5.4**                   | **1.0x**   | Standard        | Baseline frontier model.             |
| **GPT-5.3-Codex**             | **0.7x**   | Optimized       | Fast terminal-based agentic variant. |
| **Gemini 3.1 Pro**            | **0.8x**   | Competitor      | Large context, low cost.             |
| **GLM-5.1**                   | **0.55x**  | Budget          | Specialized droid core.              |
| **Droid Core (Kimi K2.5)**    | **0.25x**  | Ultra-Budget    | Lightweight sub-agent execution.     |
| **Droid Core (Minimax M2.7)** | **0.12x**  | Nano-Tier       | Low-complexity automated pings.      |
| **Claude Haiku 4.5**          | **0.4x**   | Efficiency      | High-speed, high-recall droid.       |

## Strategy Allocation

- **P1 Orchestration**: Claude Opus 4.7 (0.5x) is the primary choice due to the current discount scaling advantage.
- **P4 Engineering**: GPT-5.3-Codex (0.7x) provides the best terminal-native latency/cost ratio.
- **P5 Auditing**: Parallel use of Sonnet 4.6 (1.2x) and Haiku 4.5 (0.4x) for diverse critique coverage.
