# V12 Triple-Threat Adversarial Audit Protocol

This protocol defines the standardized mandate for all forensic auditors (Gemini, Jules, Kilo) operating on the Universal OR Strategy codebase. Every audit MUST address the following three pillars without exception.

## 1. Pillar I: Logical Proof of Failure (LPF)

- **Objective**: Conduct a high-integrity adversarial review to find the fundamental flaw that breaks the implementation's promise.
- **Goal**: Identify where the logic contradicts its own stated intent or the laws of NinjaScript/Actor-model execution.
- **Mandate**: "If you can break this logic, do it. Find the proof that this code WILL fail in production."

## 2. Pillar II: Institutional Compliance (GEMINI.md)

- **Objective**: Verify strict adherence to the project's "Platinum Standards."
- **Checklist**:
  - **Zero-Trust IPC**: Absolutely NO `lock(stateLock)` or any internal lock statements.
  - **FSM Integrity**: Mandatory use of the two-phase `Replace` FSM for all follower order modifications (No raw `Cancel()` + `Submit()`).
  - **ASCII Gate**: Zero non-ASCII characters, emoji, or curly quotes in C# string literals.
  - **Memory Safety**: Direct writes to `stopOrders` during bracket submission (Build 981 mandate).

## 3. Pillar III: Load & Race Condition Loopholes

- **Objective**: Identify vulnerabilities that emerge only under heavy market load or high-frequency execution.
- **Focus Areas**:
  - **Ghost Orders**: Tracking windows where an order is cancelled but a new one is submitted before the confirmation arrives.
  - **Shared State Contention**: Identifying non-actor protected state mutations (e.g., naked math on shared counters).
  - **Semaphore Lifecycle**: Ensuring `_simaToggleSem` is always released in a `finally` block to prevent deadlock.

---

## Standardized System Prompt Template

> You are a High-Integrity Forensic Auditor. You are performing a Triple-Threat Adversarial Audit on a PR Diff for the V12 Universal OR Strategy.
>
> Your report MUST cover:
>
> 1. **Logical Proof of Failure (LPF)**: Where is the logic fundamentally broken?
> 2. **Compliance (GEMINI.md)**: Does it violate the Zero-Lock or FSM-Replace rules?
> 3. **Load-Race Loopholes**: Will this crash or leak orders under 500ms market bursts?
>
> **Institutional Rules (GEMINI.md):**
> [INSERT GEMINI.MD CONTENT HERE]
>
> **PR DIFF:**
> [INSERT DIFF CONTENT HERE]
>
> Final Verdict: **PASS** or **REVISION REQUIRED**.
