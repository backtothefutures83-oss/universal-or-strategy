# Project Workflow (V12 Multi-Agent Director's Protocol)

## Guiding Principles
1. **The "Director's Gate" Hierarchy**:
    - **ORCHESTRATOR**: Intake (P1) and multi-agent coordination. BANNED from manual coding.
    - **FORENSICS**: Diagnosis (P2) and "Logical Proof of Failure" audits (P5).
    - **ARCHITECT**: Design/Strategy (P3) and Peer Review Sign-off (P5).
    - **ENGINEER**: Implementation (P4). Execution of approved surgical edits.
2. **Strict Plan Approval**: Every code change requires an `implementation_plan.md`. ONLY the Director (User) can authorize execution. Orchestrators are BANNED from approving plans.
3. **Continuous Safety**: Zero internal locks (`lock(stateLock)` is BANNED). Mandatory ASCII-only C# strings.

## Task Workflow

All tasks follow a strict multi-agent lifecycle:

### Standard Task Workflow

1. **Select Task**: Choose the next task from `plan.md`.
2. **Mark In Progress**: Change the task from `[ ]` to `[~]`.
3. **Execution (P4 - ENGINEER)**: Delegate implementation to the ENGINEER.
4. **Safety Checks (P4 - Self-Audit)**:
    - Verify zero `lock(stateLock)` usage.
    - Run ASCII safety checks on all C# strings.
    - Perform internal "Dry Run" regression check against the Mission Brief.
5. **Auditing (P5 - FORENSICS/ARCHITECT)**:
    - All tracks MUST use the `/loop-critic` or `/multi-agent-audit` workflows.
    - Handoff the changes for an architectural audit and "Logical Proof of Failure" evaluation.
6. **Commit Code Changes**:
    - Stage changes after the audit passes.
    - Commit using a descriptive message.
7. **Complete Task**: Update `plan.md` task to `[x]`.

*(Note: TDD is NOT required. Safety checks and logical proofs replace test coverage requirements.)*

## Deployment & Verification

- **Code Splits**: ALL file splits must use the Python extractor script. Manual copy-paste over 50 lines is BANNED.
- Use `deploy-sync.ps1` for local NT8 environment synchronization.
- **Verification**: Run `./scripts/audit_scan.ps1` for executive logic risk discovery.

## Commit Guidelines
- Use clear, concise semantic commit messages. Example: `feat(ipc): Add sub-minute deduplication for Fleet entry commands`
