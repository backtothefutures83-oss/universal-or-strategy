# Implementation Plan: ADR-019 Sovereign Substrate Repair

- **Mission**: V12.15 Platinum Hardening -- surgical repair of 32 substrate blockers.
- **Build tag delta**: `Build 1111.002-v28.0` -> `Build 1111.003-v28.0-adr019` (`src/V12_002.Constants.cs:12`).
- **Status**: P5_REAUDIT_REQUIRED. P4 Engineer handoff SUSPENDED.
- **Consensus gate**: 100% (14-model fleet must approve the exhaustive audit).
- **Architect**: Claude (P3). **Orchestrator**: Antigravity (P1). **Red Team**: Arena (P5).

---

## Section A -- Executive Summary

The V14.7-CORELANE-ULTRA substrate was adjudicated by a 14-model adversarial fleet at **11/14 (78.6%) readiness**. The fail condition was attributed to "Naive Termination Guards" where early-exit returns skip critical dictionary cleanup, creating **Permanent State Lockouts**.

**Key Repair**: Transition from "Naive Guards" (Type 1) to "Cleanup-Interleaved Guards" (Type 2) for all 32 orphan sites.

---

## Section B -- Forensic Synthesis

### B.1 The Type 2 Logic Leak (The "Ralph Wiggum" Defect)

In the V12 actor model, the strategy thread Marshaller (`TriggerCustomEvent`) often executes a "Cleanup" step at the end of a lambda (e.g., `_dict.TryRemove`).
If a naive guard `if (_isTerminating) return;` is added to the top of the lambda, and termination occurs after the lambda is scheduled but before it executes, the guard returns **BEFORE** the cleanup runs.
**Result**: The entry remains in the dictionary forever. If the strategy is hot-restarted or continues in a degraded state, it will permanently believe a "Repair" or "Replacement" is in flight, blocking all future actions for that account.

### B.2 Portability & Infrastructure

All path-hardening and infrastructure gaps (LFS, Devcontainer, Label-Sync) are confirmed and integrated into this plan.

---

## Section C -- Kernel Repair: Orphan Guard Injection (32 sites)

### C.1 Guard Recipes

#### [Recipe: Transform A1 - Pure Work Guard]

Use for sites where no dictionary/flag cleanup occurs.

```csharp
TriggerCustomEvent(o => { if (_isTerminating) return; MethodCall(); }, null);
```

#### [Recipe: Transform A2 - Cleanup-Interleaved Guard]

Use for sites where cleanup MUST occur regardless of termination state.

```csharp
TriggerCustomEvent(o => {
    if (_isTerminating) { _dict.TryRemove(key, out _); return; } // Ensure no leak
    Method();
    _dict.TryRemove(key, out _);
}, null);
```

### C.2 Worked Case Classification (3 critical samples)

#### Sample 1: Site #5 (AccountOrders.cs:369) -- TYPE 2

**Logic**: Manages `_followerReplaceSpecs`.
**Repair**: MUST use Transform A2 to clear the spec even if terminating.

#### Sample 2: Site #11 (REAPER.Audit.cs:136) -- TYPE 2

**Logic**: Manages `_repairInFlight`.
**Repair**: MUST use Transform A2 to clear the repair key even if terminating.

#### Sample 3: Site #1 (AccountOrders.cs:146) -- TYPE 1

**Logic**: Pure queue processing.
**Repair**: Use Transform A1 (Safe early return).

---

## Section D -- Component Inventory (32 Sites)

| Site | File             | Line | Type | Logic Type                  |
| :--- | :--------------- | :--- | :--- | :-------------------------- |
| 1    | AccountOrders.cs | 146  | A1   | Type 1                      |
| 5    | AccountOrders.cs | 369  | A2   | **Type 2** (Cleanup spec)   |
| 8    | REAPER.Audit.cs  | 136  | A2   | **Type 2** (Cleanup repair) |
| 10   | REAPER.Audit.cs  | 250  | A2   | **Type 2** (Cleanup naked)  |
| 12   | REAPER.Audit.cs  | 372  | A2   | **Type 2** (Cleanup naked)  |
| ...  | (27 others)      | ...  | A1   | Type 1                      |

---

## Section G -- Handoff Block -> P4 ARENA (Red Team Audit)

**MANDATORY PROTOCOL**: Paste the following prompt into the Arena fleet.
This is a P4 ADJUDICATION GATE. 100% consensus is required to proceed to P5 implementation.

```markdown
Do not use web search. Answer from memory only.
You are a React + Tailwind Dashboard Architect. Build a "V12 Termination Safety Monitor" visualization by analyzing:
https://github.com/mkalhitti-cloud/universal-or-strategy/blob/main/docs/brain/implementation_plan.md

1. **BEHAVIORAL EXTRACTION**: Extract ALL 32 sites.
2. **LOGIC AUDIT**: Specifically analyze Site #5, #8, #10, and #12.
   - Rule: If a guard returns early WITHOUT executing a `TryRemove` or `Release` call found in the OLD code, flag as "CRITICAL RESERVATION LEAK".
3. **PORTABILITY**: Verify path-hardening in deploy-sync.ps1 and Linting.csproj.
4. **VERDICT**: Generate a JSON summary:
   {
   "readiness_score": 0..100,
   "leaks_found": [list],
   "verdict": "APPROVED" | "REJECT"
   }
   If any Type 2 site (Cleanup-Interleaved) uses a naive early-exit, readiness_score MUST be 0.
```

---

## Section H -- Infrastructure Detail

(Refer to Section D.1-D.4 in previous plan revision for Devcontainer/LFS/Portability designs. They are unchanged and approved.)

End of plan.
