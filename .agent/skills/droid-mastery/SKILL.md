---
name: droid-mastery
description: Use when interacting with the Droid CLI for Sovereign Audits and Readiness Checks.
---

# Droid Mastery

This skill file acts as the ultimate ground-truth reference for the Droid utility.

## 1. Official Documentation & Commands
*(Agent: Inject official Droid CLI commands here)*
- `droid /review`: Sovereign Audit (Focus on P0-P3 severity findings).
- `droid /readiness-report`: Readiness Check (Maintain Level 2+).

## 2. Operational Quirks & DNA Compliance
- **Specialty**: V12 DNA enforcement, readiness reporting, and security gating.
- **Constraints**: 
  - Do not proceed with P5/P6 execution if `droid /review` fails on P0/P1 issues.

## 3. Self-Improvement Log
*(Agent: Log any false positives, audit bypasses, or new flags here)*

- **[Date]**: Initial scaffolding.
