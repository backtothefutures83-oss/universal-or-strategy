# Universal OR Strategy V12 - Product Guidelines

## Institutional Workflow DNA
- **The "Director's Gate" Hierarchy**:
  - **ORCHESTRATOR** (Antigravity/Gemini CLI): Intake and coordination. BANNED from manual coding or plan approval.
  - **FORENSICS** (Codex): Strategic Diagnosis and Logic Audits.
  - **ARCHITECT** (Claude Code): Design and Peer Review Sign-off.
  - **ENGINEER** (Codex/Jules): Implementation of surgical edits.
- **Plan Approval**: Every code change requires an `implementation_plan.md`. ONLY the Director (User) can authorize execution.

## Architectural Mandates
- **No Internal Locks**: Legacy `lock(stateLock)` is **BANNED**. State mutations must be thread-safe via the `Enqueue(ctx => ...)` model.
- **MOVE-SYNC / Follower Order Replace**: FSM required. Raw `Cancel()` followed by `Submit()` is BANNED for follower orders.
- **ASCII-Only Enforcement**: NEVER use emoji, curly quotes, or Unicode in `Print()` or C# string literals.
- **File Split Protocol**: All file splits must use the Python extractor script. Manual copy-paste over 50 lines is BANNED.

## Engineer Audit Standards (P4)
- Run `grep` audits to confirm no accidental deletions of guards or `lock` blocks.
- Verify new logic uses the Actor `Enqueue` model.
- Check for non-ASCII characters in C# strings.
- Perform internal Dry Run regression against the Mission Brief.