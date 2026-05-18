---
name: traycer-mastery
description: Use when utilizing Traycer AI for agentic code reviews and deep implementation analysis.
---

# Traycer Mastery

## Overview
Traycer is an agentic code review platform that provides deep exploration, implementation analysis, and multi-category feedback (Bug, Performance, Security, Clarity).

## Review Mode
Agentic code review with thorough exploration and analysis. Perfect for comprehensive code quality checks.

### 1. Initiation (User Query)
State your goal, expected outcome, and constraints.
- **Context**: You can provide files, folders, images (UI mockups), and Git diffs (uncommitted, main, branch, or commit).

### 2. Analysis & Categories
Traycer organizes findings into four categories:
- **Bug**: Functional issues or logic errors.
- **Performance**: Inefficiencies or optimization opportunities (GC spikes, hot-path allocations).
- **Security**: Vulnerabilities or unsafe practices (unprotected state).
- **Clarity**: Readability and maintainability (Karpathy simplicity).

### 3. Fixing Findings
- **Individual**: Fix specific comments one-by-one.
- **Selected**: Enable selection mode for batch fixes.
- **Fix All**: Use the "Fix all in" button for full automation.

## V12 Traycer Protocol
- **Deep Exploration**: Use Traycer to analyze cross-file impact when splitting God-methods.
- **DNA Validation**: Ensure the "Security" and "Bug" categories are used to catch `lock()` leaks and Ghost Order windows.
- **Iteration**: If findings are insufficient, iterate on the query until the logic proof of failure is identified.
