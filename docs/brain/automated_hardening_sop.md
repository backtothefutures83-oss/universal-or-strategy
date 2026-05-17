# Automated Hardening SOP (V12 Photon Kernel)

**Status**: ACTIVE
**Purpose**: Synthesize Bob, Traycer, and Codex AI code review tools with static analysis gates to automatically catch V12 DNA violations (e.g., `lock()` leaks, `.ToArray()` allocations, Ghost Orders) before code is committed or merged.

---

## 1. The Hardened Gate Pipeline

To prevent logic drift and ensure zero-allocation compliance, every modification must pass a 4-tier automated review pipeline.

### Tier 1: Static Pre-Commit Gate (The Scanner)
Before any `src/` changes are committed locally, the engineer must run the static analyzer.
*   **Tool**: `scripts/v12_forensic_scanner.py`
*   **Trigger**: Manual run or Pre-Commit Git Hook.
*   **Rule**: Zero `[CRITICAL]` or `[HIGH]` findings allowed. 
*   **Action**: If the scanner finds a banned `lock()` or `Semaphore.WaitOne()` without a `finally`, the commit is BLOCKED.

### Tier 2: Agentic Local Review (Bob CLI)
Once Tier 1 passes, the local workspace changes must be reviewed by the Bob CLI.
*   **Tool**: Bob CLI (`/review`)
*   **Mode**: `Branch Comparison` (Uncommitted changes) or `Issue Coverage` (`/review --issue-coverage`).
*   **Goal**: Catch logic gaps that static analysis misses (e.g., mismatched variable state, invariant breaches).
*   **Action**: The engineer must clear all Open findings in the **Bob Findings** panel before pushing. Use "Fix with Bob" for automated repairs.

### Tier 3: Deep Implementation Analysis (Traycer AI)
For complex refactoring (e.g., SIMA Subgraph Extraction), push the branch and trigger a Traycer review.
*   **Tool**: Traycer AI (Review Mode)
*   **Mode**: Diff against `main` or specific target branch.
*   **Goal**: Comprehensive architectural review across four categories:
    1.  **Bug**: Ghost order windows, race conditions.
    2.  **Performance**: Hot-path allocations (e.g., `.ToArray()`).
    3.  **Security**: Unprotected state mutations.
    4.  **Clarity**: Karpathy-standard simplicity.
*   **Action**: Use the "Fix all in" button or iterate on the prompt to resolve identified categories.

### Tier 4: Surgical Adjudication & PR Review (Codex CLI)
When preparing for final merge, Codex acts as the forensic PR auditor.
*   **Tool**: Codex CLI (Review Pane)
*   **Integration**: GitHub CLI (`gh auth login`) to pull PR reviewer feedback.
*   **Goal**: Final logic hardening and PR comment resolution.
*   **Action**: Use **Inline Comments** in the Codex diff view to provide precise feedback. Instruct Codex to "Address the inline comments and keep the scope minimal." Use **Hunk-level staging** to selectively apply logic fixes while discarding whitespace drift.

### Tier 5: FsCheck Property Gate (State Machine Verification)
For core execution and order management components, Property-Based Testing must be used to prove concurrency invariants.
*   **Tool**: `FsCheck` (C# Property Testing Library) + xUnit/NUnit.
*   **Goal**: Blast the FSM with random sequences of market events to prove invariants.
*   **Rule**: 1,000 random permutations must execute without violating V12 DNA (e.g., zero ghost orders, exact symmetry).
*   **Action**: If FsCheck finds a falsifiable sequence, the specific event chain is added to the regression suite, and the logic is rejected.

---

## 2. Empirical Vetting (The "Proof" Gates)

Automated code reviews must be backed by empirical evidence.

*   **P5 Logic Gate**: Every modified cluster must pass a **Baseline Reproducer** test (e.g., `SIMA_Baseline_Test.cs`) using `PhotonMock` before the PR is approved.
*   **P6 Performance Gate**: The `scripts/amal_harness.py` must be executed to confirm **Zero Bytes Allocated** on all modified hot-path methods.
*   **P6 Invariant Gate**: The `FsCheck` property suite must pass.

## 3. Reviewer Checklist (For the Director)

When acting as the Director during a handoff, enforce this sequence:
1.  **[ ]** Has `v12_forensic_scanner.py` been run, yielding 0 CRITICAL/HIGH errors?
2.  **[ ]** Has Bob `/review` cleared all local findings?
3.  **[ ]** Has Traycer flagged any new Performance (GC) or Bug (Concurrency) issues?
4.  **[ ]** Has the Codex Review Pane been used to stage ONLY the necessary hunks?
5.  **[ ]** Did `amal_harness.py` return `Allocated = 0 B`?
6.  **[ ]** Did `FsCheck` complete 1,000 random property validations without failure?

If any step fails, loop back to the **P3 Architect** for a structural redesign. Do not force a flawed implementation.
