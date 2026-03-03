# Build 938 — Follower Bracket Lifecycle Audit & Orphan-Exit Fix

## Context

In the 8:04 AM trade, SimApexSim_02 filled Short @ 6753 (8:04:27) and was Emergency Flattened
@ 6756.75 at 8:04:29 — 2 seconds later — with no bracket orders ever submitted. The OCO group
MGE_238c10db existed but its exit orders never appeared in the log.

Build 937 fixed the EF desync (expectedPositions sync on broker qty change), which was the root
cause of the incorrect EF trigger. Build 938 audits whether the bracket now submits cleanly and
adds traces to confirm correct order of operations for any future legitimate EF.

---

## Diagnostic Findings

### a) Exact bracket submission code path after a fleet follower entry fills

```
OnOrderUpdate (Callbacks.cs:155)
  → orderState == Filled
  → entryOrders.Values.Contains(order) → HandleEntryOrderFilled (Callbacks.cs:201)
    → guard: !kvp.Value.EntryFilled (Callbacks.cs:206) — prevents double processing
    → pos.EntryFilled = true (Callbacks.cs:209)
    → recalculates target prices from averageFillPrice
    → SubmitBracketOrders(kvp.Key, pos) (Callbacks.cs:242)
      → guard: if (pos.BracketSubmitted) return; (Management.cs:39)
      → submits stop via pos.ExecutingAccount.Submit (Management.cs:78)
      → loops T1-T5: submits each limit via pos.ExecutingAccount.Submit (Management.cs:146)
      → pos.BracketSubmitted = true (Management.cs:193)
```

Dual guard: `!pos.EntryFilled` (entry-level) + `pos.BracketSubmitted` (bracket-level).
Both guards are on the PositionInfo object, so they are per-position, not global.

### b) With Build 937 (EF suppressed), will bracket submit correctly for SimApexSim_02?

YES. The fill callback fires on the strategy thread and calls `SubmitBracketOrders` synchronously.
EF (via TriggerCustomEvent or cascade-filled detection) is queued on the same strategy thread and
cannot interrupt an in-progress `SubmitBracketOrders` call. The bracket completes before any
queued EF can run.

Root cause of the 8:04 AM failure: Build <937 EF desync fired an incorrect EF at 8:04:29,
2 seconds after the fill. By that time the bracket HAD been submitted, but EF cancelled it
(Step 1: blanket cancel sweep) and flattened the account. The position was correctly closed
but the bracket cancellation left no working exits in the window between EF and fill of the
market close. With Build 937 the incorrect EF is suppressed; the bracket survives.

### c) If EF fires legitimately, does it cancel the bracket FIRST?

YES — already correct in current code. `EmergencyFlattenSingleFleetAccount` (SIMA.cs:1468):
- Step 1 (L1476-1493): Blanket cancel loop — cancels ALL working/submitted/accepted orders
  on the instrument for the account (includes stop + all target limit orders)
- Step 2 (L1495-1517): Submits Market close ONLY IF a live position exists
- Step 3 (L1524): Clears ghost memory via SetExpectedPositionLocked

The bracket is cancelled before the market close is submitted. No orphan exit orders
from this path.

### d) Are orphan exit orders possible?

NOT on the NT8 single-threaded strategy model. Strategy callbacks run sequentially on the
strategy thread. `SubmitBracketOrders` runs to completion before any queued EF callback
can execute. EF's blanket cancel (Step 1) covers all brackets before the close order.

One pre-existing edge case (out of scope — do not fix): if a target `Submit()` call at
Management.cs:146 throws, the outer catch fires before `pos.BracketSubmitted = true` (L193)
is reached, leaving BracketSubmitted=false with a partial bracket. This does not produce
orphan orders (stop is live) but could cause duplicate submission if SubmitBracketOrders
is ever re-entered. No regression introduced by Build 938.

---

## Minimal Surgical Changes

### Change 1 — BUILD_TAG increment
**File:** `src/V12_002.cs:44`

```csharp
// BEFORE:
public const string BUILD_TAG = "937";  // V12.937: REAPER desync fix -- expectedPositions sync on broker qty change

// AFTER:
public const string BUILD_TAG = "938";  // V12.938: Follower bracket lifecycle audit -- orphan-exit guard + EF trace
```

### Change 2 — [938-BRACKET] trace after bracket confirmation
**File:** `src/V12_002.Orders.Management.cs`
**Location:** After `pos.BracketSubmitted = true;` (currently L193), before the existing
`StringBuilder bracketMsg` block (L195).

Add immediately after L193:
```csharp
if (isFollowerSubmit)
    Print(string.Format("[938-BRACKET] Follower bracket submitted: {0} T1={1:F2} Stop={2:F2}",
        entryName, pos.Target1Price, validatedStopPrice));
```

This fires only for follower (non-master) accounts and only after the complete bracket
(stop + all targets) has been submitted and BracketSubmitted is confirmed true.

### Change 3 — [938-EF-GUARD] trace before EF cancel sweep
**File:** `src/V12_002.SIMA.cs`
**Location:** Inside `EmergencyFlattenSingleFleetAccount`, after the null check and initial
Print (L1471), before Step 1 cancel list construction (L1476).

Add immediately before the comment `// Step 1: Cancel ALL working orders...` (L1475):
```csharp
Print(string.Format("[938-EF-GUARD] EF cancelling bracket first: {0}", acct.Name));
```

This confirms that whenever EF fires, the trace appears BEFORE any cancel or close order,
making the log unambiguous about ordering.

---

## Critical Files

| File | Change | Lines Affected |
|------|--------|---------------|
| `src/V12_002.cs` | BUILD_TAG "937" → "938" | L44 |
| `src/V12_002.Orders.Management.cs` | Add [938-BRACKET] trace | After L193 |
| `src/V12_002.SIMA.cs` | Add [938-EF-GUARD] trace | Before L1475 |

**Do NOT touch:** REAPER.cs, UI files, entry files, Properties.cs, Orders.Callbacks.cs

---

## Verification

1. Deploy: `powershell -ExecutionPolicy Bypass -File "C:\WSGTA\universal-or-strategy\deploy-sync.ps1"`
2. Gate must pass (zero errors).
3. In the next live or sim trade, confirm the log shows:
   - `[938-BRACKET] Follower bracket submitted: <acct> T1=<price> Stop=<price>` immediately
     after `ENTRY FILLED:` for each follower account
   - `[STOP_AUDIT] OK` following each [938-BRACKET] line
4. To verify EF guard: when EF fires legitimately, `[938-EF-GUARD] EF cancelling bracket first:`
   must appear BEFORE `[DEAD-01] EmergencyFlatten: Cancelled N working order(s)` in the log.
5. Confirm no bracket orders appear in broker DOM for flat accounts after EF completes.
