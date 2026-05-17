# Epic Scope: SIMA Subgraph Hardening

## 1. Objective
Analyze and remediate the systemic concurrency, logic, and resource management traps identified in the V12 Photon Kernel during the Phase 7 forensic audit.

## 2. Forensic Evidence (Logical Proof of Failure)
You are provided with the following evidence to base your architectural design on:
1. **The Bug Registry**: `docs/brain/bug_registry.md` (Details the 80+ identified vulnerabilities, including ABA thread preemption and GC pressure).
2. **Adversarial Forensic Report**: `docs/arena_response2.txt` (Contains the Arena AI Red Team's analysis of the compound traps and their proposed lock-free primitive designs).

## 3. Scope Boundaries
**IN SCOPE**:
- Verify the forensic evidence regarding the FSM callback deadlocks, 32-bit generation overflows, and dictionary allocations.
- Propose and design structural repairs (e.g., atomic state, zero-allocation maps) that satisfy the V12 DNA based on the evidence.
- Map the blast radius for the affected SIMA components.

**OUT OF SCOPE**:
- General logic refactoring unrelated to the specific traps identified in the forensic evidence.
- UI/Frontend modifications.

## 4. V12 DNA Constraints
- **Zero-Lock**: Absolutely no `lock(stateLock)` statements added or retained.
- **Zero-Allocation**: Hot paths must not allocate memory on the heap.
- **Mathematical Safety**: Any state management repairs must preserve ABA-immunity, verifiable via FsCheck properties (`SimaFleetAbaPropertyTests.cs`).
- **ASCII Compliance**: Zero non-ASCII characters in C# strings.
