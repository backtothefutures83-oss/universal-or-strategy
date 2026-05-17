# V12 Agent Performance Matrix
## (Forensic Reliability & Hallucination Tracker)

This matrix evaluates the reliability of our agent swarm (Bob, Codex, Arena AI, Jules, Gemini CLI). It is used to determine agent redundancy and optimize our distributed intelligence routing.

### 📊 Reliability Scorecard

| Agent | Total Findings | Legit Bugs | Hallucinations | Accuracy (%) | Strengths | Weaknesses |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Arena AI (Sonnet 4.6)** | 15 | 14 | 1 | 93% | Broad structural analysis, concurrency baselines. | Missed several deep memory leaks. |
| **Arena AI (Sonnet 4.5)** | 26 | 24 | 2 | 92% | Deep concurrency, FSM logic, sideband analysis, memory leaks. | Recommends banned `lock()` patterns; occasional false positive on `catch` block handling. |
| **Arena AI (Qwen 3.6+)** | 13 | 13 | 0 | 100% | Zero hallucination on logic overlap, found novel business-logic bugs (OCO group overwrite, rollback gap). | Lower total finding volume; missed some deeper edge cases found by Sonnet 4.5. |
| **Arena AI (Qwen 3.6 Max)** | 11 | 11 | 0 | 100% | Architecture, cross-thread boundary mapping, memory leak projections. | Less focus on local control flow. |
| **GPT 5.3 Codex** | 8 | 8 | 0 | 100% | Laser focus on array bounds, null safety, and event-storm scheduling loops. | Smallest overall finding footprint. |
| **GPT 5.2 Codex** | 5 | 5 | 0 | 100% | Lifecycle orchestration, broker-ACK gap detection. | Low finding volume. |
| **Antigravity** | 1 | 1 | 0 | 100% | Orchestration, Forensic Scanner integration. | None identified. |
| **Bob CLI** | 0 | 0 | 0 | - | (Awaiting execution) | (Awaiting execution) |
| **Codex CLI** | 0 | 0 | 0 | - | (Awaiting execution) | (Awaiting execution) |
| **Gemini CLI** | 0 | 0 | 0 | - | Utility, research, visual context. | Logic synthesis (Banned). |

---

### 🔍 Detailed Finding Audit (Legit vs. Hallucination)

| Finding ID | Agent | Diagnosis | Verification Method | Verdict | Root Cause of Hallucination |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BUG-006** | Arena AI (Sonnet 4.6) | Null Ref on `pos.Instrument` | Manual Scan | 🟡 Partial | `pos` is guaranteed non-null by caller guard; code is safe. | Over-aggressive null-check heuristic. |
| **M3** | Arena AI (Sonnet 4.5) | Zombie FSM Entries | Manual Scan | 🔴 False Positive | Already handled by `RollbackFleetDispatchState` in catch. | Agent failed to trace method call inside catch block. |
| **T1 (Fix)** | Arena AI (Sonnet 4.5) | Use `stateLock` | Rule Check | 🔴 DNA Violation | Recommended `stateLock` which is strictly BANNED in V12. | Base model bias towards standard C# locking instead of V12 lock-free FSM. |
| **M1** | Arena AI (Sonnet 4.5) | Unbounded `_orderIdToFsmKey` | Logic Walkthrough | 🟢 Legit | Dictionary grows indefinitely, causing memory leak. | N/A |
| **T2** | Arena AI (Sonnet 4.5) | Counter Corruption | Logic Walkthrough | 🟢 Legit | `_pendingFleetDispatchCount` double decremented. | N/A |
| **T4** | Arena AI (Sonnet 4.5) | Unsafe Dictionary Iteration | Logic Walkthrough | 🟢 Legit | Iterating `_followerBrackets` without snapshot. | N/A |

---

### 🛡️ Testing Strategy for Future Coverage

To ensure these issues are caught across the **entire `src` code** automatically, we deploy the following defensive layers:

1.  **Static Patterns**: Add the confirmed bug signature to `v12_forensic_scanner.py`.
2.  **Property Invariants**: Add an `FsCheck` property to the global regression suite.
3.  **Cross-Agent Peer Review**: Use the "Red Team" audit where Agent B must provide a "Proof of Logic" for Agent A's finding before it is accepted into the Registry.

---
**Last Updated**: 2026-05-16
**Status**: Awaiting Arena Audit Results input.
