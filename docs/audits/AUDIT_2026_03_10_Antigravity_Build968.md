# [EXECUTIVE AUDIT] Build 968 -- REAPER Hardening & Multi-Instrument Safety
**File**: `src/V12_002.REAPER.cs`
**Commit**: c588c5a
**Auditor**: Antigravity
**Date**: 2026-03-10

---

## Audit Matrix — 10 Site Verification

### SECTION 1: Field Declarations (HashSet Elimination)

| Site | Line | Target | Finding | Status |
|------|------|--------|---------|--------|
| F-01 | 24 | `_repairInFlight` type | `private readonly ConcurrentDictionary<string, byte> _repairInFlight = new ConcurrentDictionary<string, byte>(); // [Build 968]` | ✅ PASS |
| F-02 | 30 | `_reaperNakedStopInFlight` type | `private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight = new ConcurrentDictionary<string, byte>(); // [Build 968]` | ✅ PASS |

**Finding**: Both fields are `readonly ConcurrentDictionary<string, byte>`. No `HashSet<string>` remains. Both carry `// [Build 968]` tags.

---

### SECTION 2: Repair Call-Sites (Atomicity)

| Site | Line | Target | Finding | Status |
|------|------|--------|---------|--------|
| R-01 | 252 | Loop-entry guard | `alreadyInFlight = _repairInFlight.ContainsKey(repairKey); // [Build 968]` | ✅ PASS |
| R-02 | 286 | TryAdd before dispatch | `_repairInFlight.TryAdd(repairKey, 0); // [Build 968]` | ✅ PASS |
| R-03 | 292 | TryRemove on dispatch failure | `_repairInFlight.TryRemove(repairKey, out _); // [Build 968]` | ✅ PASS |
| R-04 | 707 | TryRemove in finally block | `_repairInFlight.TryRemove(repairKey, out _); // [Build 968]` | ✅ PASS |

**Finding**: The four-stage atomicity pattern is intact:
1. Background thread reads guard via `ContainsKey` (line 252).
2. Guard is set via `TryAdd` **before** `TriggerCustomEvent` (line 286) — A3-2 ordering preserved.
3. Dispatch failure path clears guard immediately (line 292).
4. Strategy thread clears guard in `finally` block — guaranteed on ALL exit paths including `return` mid-function and exceptions (line 707).

> [!IMPORTANT]
> The `finally` block at line 703–708 wraps only the inner `try` (lines 648–703), which means the `TryRemove` fires even if `CreateOrder`, the race-guard abort, or `Submit` throw. This is the correct pattern.

---

### SECTION 3: Naked-Stop Collision Fix (Key Alignment)

| Site | Line | Target | Finding | Status |
|------|------|--------|---------|--------|
| N-01 | 354 | ContainsKey uses ExpKey | `alreadyNakedInFlight = _reaperNakedStopInFlight.ContainsKey(ExpKey(acct.Name)); // [Build 968]` | ✅ PASS |
| N-02 | 357 | TryAdd uses ExpKey | `_reaperNakedStopInFlight.TryAdd(ExpKey(acct.Name), 0); // [Build 968]` | ✅ PASS |
| N-03 | 767 | TryRemove on success uses ExpKey | `_reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _); // [Build 968]` | ✅ PASS |
| N-04 | 775 | TryRemove on failure uses ExpKey | `_reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _); // [Build 968]` | ✅ PASS |

**Finding**: All four naked-stop guard sites consistently use `ExpKey()`. Bare `acct.Name` is NOT used anywhere in the guard dictionary.

---

## Deep-Logic Analysis

### Thread-Safety Assessment

**ReaperLoop (background thread)** calls `AuditApexPositions -> AuditSingleFleetAccount`:
- Reads `_repairInFlight.ContainsKey` (line 252) — lock-free, atomic.
- Writes `_repairInFlight.TryAdd` (line 286) — lock-free, atomic.
- Reads `_reaperNakedStopInFlight.ContainsKey` (line 354) — lock-free, atomic.
- Writes `_reaperNakedStopInFlight.TryAdd` (line 357) — lock-free, atomic.

**Strategy Thread** (via `TriggerCustomEvent`):
- Writes `_repairInFlight.TryRemove` (lines 292, 707) — lock-free, atomic.
- Writes `_reaperNakedStopInFlight.TryRemove` (lines 767, 775) — lock-free, atomic.

**TOCTOU Gap Analysis** (lines 252 → 286):
- Pattern: `ContainsKey → [work block] → TryAdd`
- Only ONE background thread (`reaperThread`) ever calls `AuditSingleFleetAccount`. There is no concurrent producer. The gap is **unexploitable** — a single thread cannot race against itself. ✅
- `ConcurrentDictionary.TryAdd` is atomic even if a race were possible: only one caller wins, the other returns `false` silently. ✅

**Verdict**: ZERO data-races between background REAPER thread (read/write) and strategy thread (write/release).

---

### Multi-Instrument Safety Assessment

**Repair guard key shape**:
```
repairKey = accountName + "_" + Instrument.FullName
```
- `MES account PA_001 long` → key: `PA_001_MES 09-26`
- `MNQ account PA_001 long` → key: `PA_001_MNQ 09-26`

These are **distinct keys**. An MES in-flight guard does NOT block an MNQ repair on the same account. ✅

**Naked-stop guard key shape**:
```
ExpKey(acct.Name)  =  acct.Name + "_" + Instrument.FullName   (standard ExpKey formula)
```
- `MES` strategy instance: `ExpKey("PA_001")` → `PA_001_MES 09-26`
- `MNQ` strategy instance: `ExpKey("PA_001")` → `PA_001_MNQ 09-26`

Each strategy instance runs with its own `Instrument`, so `ExpKey()` is inherently instance-scoped. An MES naked-stop in-flight CANNOT block an MNQ naked-stop on the same account. ✅

---

### ASCII Compliance Audit (Tagged Lines)

Lines audited: 24, 30, 252, 286, 292, 354, 357, 707, 767, 775.

All `// [Build 968]` tagged lines contain only standard ASCII characters. No emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing characters found in tagged lines. ✅

> [!NOTE]
> Untagged lines 499 and 691 contain `\u2713` (checkmark) and `?` glyphs in `Print()` strings.
> These are **outside Build 968 scope** and are pre-existing. Not a Build 968 violation but flagged for future remediation.

---

## Forensic Findings Table

| ID | Severity | Location | Finding |
|----|----------|----------|---------|
| F968-00 | INFO | L24, L30 | ConcurrentDictionary fields confirmed. HashSet eliminated. |
| F968-01 | INFO | L252 | ContainsKey guard confirmed on loop entry. |
| F968-02 | INFO | L286 | TryAdd fires before TriggerCustomEvent (A3-2 preserved). |
| F968-03 | INFO | L292 | TryRemove on dispatch failure path confirmed. |
| F968-04 | INFO | L707 | TryRemove in `finally` — guaranteed cleanup on all exit paths. |
| F968-05 | INFO | L354, L357 | Naked-stop guard uses ExpKey — instrument-scoped. |
| F968-06 | INFO | L767, L775 | Naked-stop TryRemove uses ExpKey — success and failure paths both clear. |
| F968-07 | LOW | L499, L691 | Non-ASCII `\u2713`/`?` in Print() strings — NOT [Build 968] tagged. Pre-existing. Recommend remediation in Build 969. |

---

## VERDICT

```
╔══════════════════════════════════════════════════════════════╗
║  BUILD 968 AUDIT RESULT:  ██████████████████  PASS  ██████  ║
║                                                              ║
║  All 10 audit targets verified.                              ║
║  Thread-safety: CONFIRMED (zero data-races)                  ║
║  Multi-instrument safety: CONFIRMED (key-collision free)     ║
║  ASCII compliance: CONFIRMED (all [Build 968] lines clean)   ║
║                                                              ║
║  ACTION REQUIRED: Remediate pre-existing non-ASCII in        ║
║  Print() strings at lines 499, 691 (Build 969 candidate).   ║
╚══════════════════════════════════════════════════════════════╝
```

---

*Audit saved per protocol to `docs/audits/AUDIT_2026_03_10_Antigravity_Build968.md`*
