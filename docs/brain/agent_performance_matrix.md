# V12 Agent Performance Matrix
## (Forensic Reliability & Hallucination Tracker)

This matrix evaluates the reliability of our agent swarm (Bob, Codex, Arena AI, Jules, Gemini CLI). It is used to track agent diagnostics, audit performance, cross-agent consensus, and optimize our distributed intelligence routing.

---

### 📊 Swarm Reliability Scorecard

| Agent | Total Findings | Legit Bugs | Hallucinations | Accuracy (%) | Key Strengths | Diagnostic Weaknesses / Blindspots |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Arena AI (Sonnet 4.6)** | 15 | 14 | 1 | 93.3% | Broad structural analysis, concurrency baselines, performance. | Over-aggressive null-check heuristic (e.g. BUG-006). |
| **Arena AI (Sonnet 4.5)** | 26 | 24 | 2 | 92.3% | Deep concurrency, FSM state tracking, sideband leaks, memory leaks. | Bias toward standard C# `lock()` patterns instead of V12 lock-free. |
| **Arena AI (Qwen 3.6+)** | 13 | 13 | 0 | 100.0% | Zero hallucination on logic overlap, found novel business-logic bugs. | Lower total finding volume; missed some deeper memory leaks. |
| **Arena AI (Qwen 3.6 Max)** | 11 | 11 | 0 | 100.0% | Architecture, cross-thread boundary mapping, leak projections. | Less focus on local control flow. |
| **GPT 5.3 Codex** | 8 | 8 | 0 | 100.0% | Laser focus on array bounds, null safety, and scheduling loops. | Smallest overall finding footprint. |
| **GPT 5.2 Codex** | 5 | 5 | 0 | 100.0% | Lifecycle orchestration, broker-ACK gap detection. | Low finding volume. |
| **Antigravity** | 1 | 1 | 0 | 100.0% | Orchestration, Forensic Scanner integration. | None identified. |
| **Jules CLI** | 4 | 4 | 0 | 100.0% | .NET 4.8 framework safety, ABA concurrency hazard identification. | Missed several logic compiler errors in fallback dedup. |
| **Codex CLI** | 5 | 5 | 0 | 100.0% | Compiler safety, thread-safety mechanics, structural integration. | None identified. |
| **Gemini CLI** | 3 | 3 | 0 | 100.0% | Utility, research, visual context, power-of-2 verification. | Logic synthesis (Banned from P4 strategy edits). |

---

### 🔍 Detailed Finding Audit (Legit vs. Hallucination)

| Finding ID | Agent | Diagnosis | Verification Method | Verdict | Root Cause of Hallucination / Resolution |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BUG-006** | Sonnet 4.6 | Null Ref on `pos.Instrument` | Manual Scan | 🟡 Partial | `pos` is guaranteed non-null by caller guard; code is safe. Over-aggressive heuristic. |
| **M3** | Sonnet 4.5 | Zombie FSM Entries | Manual Scan | 🔴 False Positive | Already handled by `RollbackFleetDispatchState` in catch. Model failed to trace method call. |
| **T1 (Fix)** | Sonnet 4.5 | Recommended using C# `lock(stateLock)` | Rule Check | 🔴 DNA Violation | Recommended legacy `lock` which is strictly BANNED in V12. Base model bias. |
| **M1** | Sonnet 4.5 | Unbounded `_orderIdToFsmKey` leak | Logic Walkthrough | 🟢 Legit | Dictionary grows indefinitely, causing memory leak. N/A. |
| **T2** | Sonnet 4.5 | Counter Corruption | Logic Walkthrough | 🟢 Legit | `_pendingFleetDispatchCount` double decremented. N/A. |
| **T4** | Sonnet 4.5 | Unsafe Dictionary Iteration | Logic Walkthrough | 🟢 Legit | Iterating `_followerBrackets` without snapshot. N/A. |
| **B1 / P2** | Codex / Gemini / Jules | `O(N)` linear scan on `Release()` | Logic Walkthrough | 🟢 Legit | With capacity 32, linear scan is O(N). Fix: embed `PoolSlotIndex` into `FleetDispatchSlot`. |
| **B2 / P0** | Codex / Gemini / Jules | Missing Payload Linkage (`Order[]`) | Logic Walkthrough | 🟢 Legit | `FleetDispatchSlot` has no array reference; consumer cannot submit orders to broker. |
| **B3 / P1** | Codex / Jules / Gemini | Pool slot leak on Fallback path | Logic Walkthrough | 🟢 Legit | Fallback ConcurrentQueue does not release the claimed pool slot. Leads to GC pressure. |
| **O4 / P1** | Codex | Fallback Key uses undefined variables | Compiler Gate | 🟢 Legit | Plan uses undefined `cumulativeFilledQuantity` and `order` variables -- will not compile. |

---

### 🛡️ Swarm Redundancy & Mitigation Strategy

To ensure these issues are caught across the **entire `src` code** automatically, we deploy the following defensive layers:

1. **Consensus Aggregation**: High-complexity refactorings are audited by multiple models. Any P0/P1 consensus findings are immediately promoted to the `implementation_plan.md` repair checklist.
2. **Static Patterns**: Add the confirmed bug signature to `v12_forensic_scanner.py`.
3. **Property Invariants**: Add an `FsCheck` property to the global regression suite.
4. **Cross-Agent Peer Review**: Use the "Red Team" audit where Agent B must provide a "Proof of Logic" for Agent A's finding before it is accepted into the Registry.

---
**Last Updated**: 2026-05-17
**Status**: ACTIVE (V14.2 SPSC Kernel audit findings consolidated).

