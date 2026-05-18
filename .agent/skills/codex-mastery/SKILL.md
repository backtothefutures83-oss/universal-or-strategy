---
name: codex-mastery
description: Use when delegating tickets to the Codex CLI or configuring Codex parameters. Contains official documentation, constraints, and operational quirks for the Codex logic hardener.
---

# Codex CLI Mastery

This skill file acts as the ultimate ground-truth reference for the Codex agent. It contains official documentation snippets, CLI flags, and operational quirks discovered during missions.

## 1. Official Documentation & Review Protocols

### Codex Review Mode
The review pane is the primary interface for auditing Codex changes. It requires a Git repository and focuses on uncommitted changes by default.

#### Review Scopes
- **Uncommitted**: Default view showing all local changes.
- **All branch changes**: Full diff against the base branch.
- **Last turn changes**: Only the most recent assistant modification.

#### Feedback & Guidance
- **Inline Comments**: Use the `+` button in the diff to attach line-specific feedback. Codex treats these as review guidance.
- **Intent**: After leaving comments, follow up with an explicit message: "Address the inline comments and keep the scope minimal."
- **PR Integration**: If GitHub access is enabled and `gh` CLI is authenticated, Codex can pull PR context and reviewer feedback directly into the sidebar.

#### Git Actions (Surgical Control)
- **Staging/Reverting**: Supports actions at the **Entire diff**, **Per file**, and **Per hunk** levels.
- **V12 Usage**: Use hunk-level staging to selectively accept logic repairs while discarding accidental whitespace or formatting drift.

## 2. Operational Quirks & DNA Compliance
- **Specialty**: Logic hardening, forensic repairs, lock-free kernel updates.
- **Constraints**: 
  - Must strictly adhere to the ASCII-only and Lock-Free actor patterns.
  - Codex must be invoked via the `codex-rescue` reference, never directly shell-exec the binary.

## 3. Self-Improvement Log
*(Agent: Log any unexpected behavior, rate limits, or new CLI flags discovered during execution here)*

- **[Date]**: Initial scaffolding.
