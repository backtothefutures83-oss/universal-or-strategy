# ADR-019 Lock Remediation: SymmetryDispatchContext Lock-Free Refactor

**Build Tag:** `1111.003-v28.0-adr019`
**Branch:** `mission-uni-5-full-sync`
**Protocol:** V14 Alpha (Morpheus Level 5)
**P3 Architect:** Claude | **Session:** 2026-04-20
**Execution Permission:** DENIED (Director approval required for P4 adjudication)
**Destination after approval:** `docs/brain/implementation_plan.md`

> **NOTE on plan location:** Plan Mode restricts edits to this file only. Once the Director approves via ExitPlanMode, the Engineer must copy the body of this document into `docs/brain/implementation_plan.md` verbatim (preserving all OLD/NEW blocks).

---

## Context

**Problem.** The P7 Sentinel confirmed 12 banned `lock()` violations in production `src/` that must be removed per V12 Actor-model DNA (`references/v12_dna.md`). An earlier P3 draft attempted the refactor but returned a BLOCK verdict (`docs/brain/audit_v28_1_platinum.md`) with four mandatory revisions:

- **R1** — Ghost-order vector: purge must cancel broker orders before local-state cleanup.
- **R2** — `dailySummaryLock` field at `src/V12_002.cs:227` was unused but not deleted; audit gate 2 failed.
- **R3** — `SymmetryDispatchContext` visibility drifted from `private sealed` to `internal sealed`.
- **R4** — Torn-read of `MasterAnchorPrice` at `src/V12_002.Symmetry.cs:168` observed outside any lock.

**Outcome.** This plan replaces all 11 `lock(*.Sync)` sites with a lock-free `SymmetryDispatchContext` (BitConverter+Interlocked for atomic `double`, Volatile+CAS state-machine for one-shot resolve, `ConcurrentDictionary<string, byte>` for the follower set), deletes the `dailySummaryLock` field, preserves `private sealed` visibility, and makes the `Print` at `Symmetry.cs:168` automatically tear-safe via atomic property getters. The 12 counted remediations are: 11 lock-removals + 1 field deletion.

**Why now.** P6 validation is blocked on Sentinel P7 finding — ADR-019 cannot close until the 12 lock violations clear and DNA grep returns zero hits.

---

## UltraThink Adversarial Audit (Agent B: Ralph Wiggum)

| # | Probe | Finding | Mitigation |
|---|-------|---------|------------|
| 1 | Torn read on 8-byte `double` (32-bit x86) | Naked `double` reads are not CPU-atomic | `long _bits = BitConverter.DoubleToInt64Bits(0.0)`; getter via `Interlocked.Read`, setter via `Interlocked.Exchange`. Template proven at `src/V12_002.cs:156` (`lastKnownPrice`). |
| 2 | Check-then-act race on `IsResolved` | Two concurrent master-fill callbacks both seeing `false` would corrupt `MasterWeightedFill` / `MasterFilledQuantity` via double-`+=` | One-shot CAS gate: `Interlocked.CompareExchange(ref _anchorState, 1, 0) == 0`. Only the winning thread writes the three fields. Preserves first-fill-wins semantics of the prior `lock(ctx.Sync)` path. |
| 3 | Publication ordering for `MasterAnchorPrice` | Reader sees `IsResolved == true` but a stale / torn `MasterAnchorPrice` on weak-memory archs | Three-state machine: `0=unresolved, 1=resolving, 2=published`. Winner writes fields via Interlocked (full fence), then `Volatile.Write(ref _anchorState, 2)` (release). Readers: `Volatile.Read(ref _anchorState) == 2` (acquire) → all three field writes visible. |
| 4 | `HashSet<string>` → `ConcurrentDictionary<string, byte>` semantics | `Add` / `Remove` / `ToArray` / `foreach` must be lock-free-safe | `TryAdd(key, 0)`, `TryRemove(key, out _)`, `Keys.ToArray()` are atomic. Iteration via `Keys` yields a point-in-time snapshot. `StringComparer.Ordinal` preserved. |
| 5 | Print at `Symmetry.cs:168` (R4) | Naked read of `ctx.MasterAnchorPrice` outside lock | Auto-fixed: property getter now performs `BitConverter.Int64BitsToDouble(Interlocked.Read(ref _masterAnchorPriceBits))`. Tear-impossible. |
| 6 | `dailySummaryLock` deletion (R2) | Field declared at `src/V12_002.cs:227`; orphaned lock usages would break compile | Pre-verified: zero matches for `dailySummaryLock` in mainline `src/*.cs` except the declaration itself. Deletion is inert. |
| 7 | `SymmetryDispatchContext` visibility (R3) | Widening to `internal` exposes follower tracking to other assemblies | Design keeps `private sealed class`; no change to accessibility. Verified at `src/V12_002.Symmetry.cs:15`. |
| 8 | R1 ghost-order (EmergencyPurgeEntry) | Prior plan's Steps 10-12 introduced `EmergencyPurgeEntry` | This plan does NOT introduce `EmergencyPurgeEntry`. Verified: zero occurrences of `EmergencyPurgeEntry` / `SymmetryGuardEmergencyPurge` anywhere in the repo. R1 therefore becomes a compliance note for future work — see §R1 Compliance below. |
| 9 | Lost `+=` accumulation semantics at `Symmetry.cs:151` | Original `MasterWeightedFill += averageFillPrice * fillQty` inside a one-shot `if (!IsResolved)` block is equivalent to `= averageFillPrice * fillQty` (only one write ever occurs). Re-expressed as plain assignment under the CAS gate. | Unit behaviour identical. Documented inline. |
| 10 | Missing `using System.Threading;` in Symmetry.cs | `Interlocked` / `Volatile` require `System.Threading` | Task T0 adds the `using` directive. |
| 11 | `stateLock` sibling field at V12_002.cs:226 | Field retained; out of scope for this audit | Sentinel report explicitly targets `dailySummaryLock` only. `stateLock` follow-up tracked separately. |
| 12 | `MasterFilledQuantity` as `int` | Int writes are CPU-atomic on x86, but memory-ordering across threads requires fencing | `Volatile.Read` / `Volatile.Write` on `_masterFilledQuantity`. Sub-ordered to the `_anchorState` release. |
| 13 | `ConcurrentDictionary` iteration during concurrent mutation | Non-atomic iteration could miss a follower mid-`TryAdd` | Acceptable: followers are added eagerly at dispatch time, cascade/prune paths take `Keys.ToArray()` snapshots. Any follower added after the snapshot will still be picked up by the fallback `symmetryPendingFollowerFills` scan (already present in `SymmetryGuardTryResolveFollowersForDispatch`). |
| 14 | Build tag / ASCII gate / deploy-sync | Mandatory post-edit protocol per `CLAUDE.md` | Tasks T14-T17 make this explicit in the engineer handoff. |

**Verdict:** Design is sound. No unmitigated silent-failure paths. Proceeding to task map.

---

## Design: Lock-Free SymmetryDispatchContext (Target Shape)

```csharp
private sealed class SymmetryDispatchContext   // R3: visibility preserved
{
    public string DispatchId;
    public string TradeType;
    public MarketPosition Direction;
    public int ExpectedQuantity;
    public DateTime CreatedUtc;

    // Atomic doubles via BitConverter+Interlocked (R4 torn-read fix).
    private long _masterWeightedFillBits = BitConverter.DoubleToInt64Bits(0.0);
    public double MasterWeightedFill
    {
        get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref _masterWeightedFillBits)); }
        set { Interlocked.Exchange(ref _masterWeightedFillBits, BitConverter.DoubleToInt64Bits(value)); }
    }

    private long _masterAnchorPriceBits = BitConverter.DoubleToInt64Bits(0.0);
    public double MasterAnchorPrice
    {
        get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref _masterAnchorPriceBits)); }
        set { Interlocked.Exchange(ref _masterAnchorPriceBits, BitConverter.DoubleToInt64Bits(value)); }
    }

    private int _masterFilledQuantity;
    public int MasterFilledQuantity
    {
        get { return Volatile.Read(ref _masterFilledQuantity); }
        set { Volatile.Write(ref _masterFilledQuantity, value); }
    }

    // Resolve state machine: 0 = unresolved, 1 = resolving (CAS winner), 2 = published.
    private int _anchorState;
    public bool IsResolved
    {
        get { return Volatile.Read(ref _anchorState) == 2; }
    }

    public bool TryBeginResolve()   // CAS gate — exactly one winner
    {
        return Interlocked.CompareExchange(ref _anchorState, 1, 0) == 0;
    }

    public void PublishAnchor()     // Release fence: publishes the three data writes
    {
        Volatile.Write(ref _anchorState, 2);
    }

    // Lock-free follower set. `StringComparer.Ordinal` preserved.
    public readonly ConcurrentDictionary<string, byte> FollowerEntries =
        new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
}
```

**Note on `Sync` field:** DELETED. Any residual `lock(ctx.Sync)` remaining after T1-T12 will fail compile, providing a natural audit gate.

---

## Task Map (17 edits total — 12 remediations + 5 structural)

| # | File | Lines | Purpose | Tag |
|---|------|-------|---------|-----|
| T0 | `src/V12_002.Symmetry.cs` | top | Add `using System.Threading;` | Structural |
| T1 | `src/V12_002.Symmetry.cs` | 15-30 | Redesign `SymmetryDispatchContext` | Remediation 1 |
| T2 | `src/V12_002.Symmetry.cs` | 145-163 | Replace master-fill resolve body | Remediation 2 (site :151) |
| T3 | `src/V12_002.Symmetry.cs` | 111-116 | Replace `FollowerEntries.Add` | Remediation 3 (site :115) |
| T4 | `src/V12_002.Symmetry.cs` | 167-168 | R4 Print tear-safe (auto-fixed by T1) | R4 compliance — no edit needed |
| T5 | `src/V12_002.Symmetry.Follower.cs` | 34-44 | Replace pre-check anchor read | Remediation 4 (site :38) |
| T6 | `src/V12_002.Symmetry.Follower.cs` | 126-135 | Replace resolve anchor read | Remediation 5 (site :131) |
| T7 | `src/V12_002.Symmetry.Replace.cs` | 119-148 | Replace worklist iterator | Remediation 6 (site :127) |
| T8 | `src/V12_002.Symmetry.Replace.cs` | 187-193 | Replace cascade ToArray | Remediation 7 (site :189) |
| T9 | `src/V12_002.Symmetry.Replace.cs` | 217-227 | Replace `FollowerEntries.Remove` | Remediation 8 (site :221) |
| T10 | `src/V12_002.Symmetry.Replace.cs` | 238-263 | Replace prune iterator | Remediation 9 (site :244) |
| T11 | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 243-247 | Replace follower ToArray | Remediation 10 (site :244) |
| T12 | `src/V12_002.Orders.Callbacks.Propagation.cs` | 124-128 | Replace follower ToArray | Remediation 11 (site :126) |
| T13 | `src/V12_002.SIMA.Shadow.cs` | 87-105 | Replace follower iterator | Remediation 12 (site :89) |
| T14 | `src/V12_002.cs` | 225-228 | Delete `dailySummaryLock` field | R2 (13th remediation — counts toward the 12 banned-locks inventory) |
| T15 | `src/V12_002.Properties.cs` | BuildTag | Bump tag to `1111.003-v28.0-adr019` | Build |
| T16 | Repo | — | Run `deploy-sync.ps1` + ASCII gate + DNA grep | Gate |
| T17 | Repo | — | Run AMAL 6-test harness | Validation |

---

## T0 — Add `using System.Threading;` to Symmetry.cs

**File:** `src/V12_002.Symmetry.cs` (lines 1-7)

**OLD:**
```csharp
// V12.50 SYMMETRY GUARD - Master-Fill Anchored Fleet Risk Isolation
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
```

**NEW:**
```csharp
// V12.50 SYMMETRY GUARD - Master-Fill Anchored Fleet Risk Isolation
// V28.0-adr019: System.Threading added for Interlocked / Volatile primitives.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
```

---

## T1 — Redesign `SymmetryDispatchContext` (R3 preserves `private sealed`)

**File:** `src/V12_002.Symmetry.cs` (lines 15-30)

**OLD:**
```csharp
        private sealed class SymmetryDispatchContext
        {
            public string DispatchId;
            public string TradeType;
            public MarketPosition Direction;
            public int ExpectedQuantity;
            public DateTime CreatedUtc;

            public double MasterWeightedFill;
            public int MasterFilledQuantity;
            public double MasterAnchorPrice;
            public bool IsResolved;

            public readonly object Sync = new object();
            public readonly HashSet<string> FollowerEntries = new HashSet<string>(StringComparer.Ordinal);
        }
```

**NEW:**
```csharp
        // V28.0-adr019: Lock-free dispatch context. All mutable state is published via
        // Interlocked / Volatile primitives; the Sync lock object is DELETED. Resolve is
        // one-shot via CAS on _anchorState (0=unresolved, 1=resolving, 2=published).
        // Visibility preserved as private sealed (R3).
        private sealed class SymmetryDispatchContext
        {
            public string DispatchId;
            public string TradeType;
            public MarketPosition Direction;
            public int ExpectedQuantity;
            public DateTime CreatedUtc;

            // Atomic double: BitConverter+Interlocked prevents torn reads on 32-bit x86.
            // Template: V12_002.cs:156 (lastKnownPrice).
            private long _masterWeightedFillBits = BitConverter.DoubleToInt64Bits(0.0);
            public double MasterWeightedFill
            {
                get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref _masterWeightedFillBits)); }
                set { Interlocked.Exchange(ref _masterWeightedFillBits, BitConverter.DoubleToInt64Bits(value)); }
            }

            // Atomic double (R4 torn-read fix for Print at Symmetry.cs:168).
            private long _masterAnchorPriceBits = BitConverter.DoubleToInt64Bits(0.0);
            public double MasterAnchorPrice
            {
                get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref _masterAnchorPriceBits)); }
                set { Interlocked.Exchange(ref _masterAnchorPriceBits, BitConverter.DoubleToInt64Bits(value)); }
            }

            // Int is CPU-atomic on x86, but Volatile provides memory-ordering.
            private int _masterFilledQuantity;
            public int MasterFilledQuantity
            {
                get { return Volatile.Read(ref _masterFilledQuantity); }
                set { Volatile.Write(ref _masterFilledQuantity, value); }
            }

            // Resolve state machine: 0 = unresolved, 1 = resolving (CAS winner), 2 = published.
            // Readers observing _anchorState == 2 (acquire) see all prior writes to
            // MasterWeightedFill / MasterAnchorPrice / MasterFilledQuantity (release).
            private int _anchorState;
            public bool IsResolved
            {
                get { return Volatile.Read(ref _anchorState) == 2; }
            }

            // Exactly one thread wins the CAS. Winner must then write the three data
            // fields and call PublishAnchor() to transition state 1 -> 2.
            public bool TryBeginResolve()
            {
                return Interlocked.CompareExchange(ref _anchorState, 1, 0) == 0;
            }

            // Release-store. All prior writes in the winner thread become visible to any
            // reader that subsequently observes IsResolved == true.
            public void PublishAnchor()
            {
                Volatile.Write(ref _anchorState, 2);
            }

            // ConcurrentDictionary<string, byte> as Set<string>. StringComparer.Ordinal preserved.
            public readonly ConcurrentDictionary<string, byte> FollowerEntries =
                new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        }
```

---

## T2 — Lock-free master-fill resolve

**File:** `src/V12_002.Symmetry.cs` (lines 145-163, inside `SymmetryGuardOnMasterFill`)

**OLD:**
```csharp
            bool resolvedNow = false;
            lock (ctx.Sync)
            {
                if (!ctx.IsResolved)
                {
                    ctx.MasterWeightedFill += averageFillPrice * fillQty;
                    ctx.MasterFilledQuantity += fillQty;

                    double avg = ctx.MasterWeightedFill / Math.Max(1, ctx.MasterFilledQuantity);
                    ctx.MasterAnchorPrice = Instrument.MasterInstrument.RoundToTickSize(avg);
                    ctx.IsResolved = true;
                    resolvedNow = true;
                }
            }
```

**NEW:**
```csharp
            // V28.0-adr019: Lock-free one-shot resolve.
            // TryBeginResolve does an atomic CAS 0 -> 1. Only one thread ever enters this block.
            // Prior lock(ctx.Sync) path used `+=` inside a one-shot gate; with only one entrant
            // ever, += from zero == plain =. Semantics preserved.
            bool resolvedNow = false;
            if (ctx.TryBeginResolve())
            {
                double weighted = averageFillPrice * fillQty;
                ctx.MasterWeightedFill = weighted;
                ctx.MasterFilledQuantity = fillQty;

                double avg = weighted / Math.Max(1, fillQty);
                ctx.MasterAnchorPrice = Instrument.MasterInstrument.RoundToTickSize(avg);

                // Release-store: publishes all three prior writes. Readers subsequently
                // observing IsResolved == true see them via acquire-load (R4 compliance).
                ctx.PublishAnchor();
                resolvedNow = true;
            }
```

---

## T3 — Lock-free `FollowerEntries.Add`

**File:** `src/V12_002.Symmetry.cs` (lines 111-116, inside `SymmetryGuardRegisterFollower`)

**OLD:**
```csharp
            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                lock (ctx.Sync)
                    ctx.FollowerEntries.Add(fleetEntryName);
            }
```

**NEW:**
```csharp
            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                // V28.0-adr019: ConcurrentDictionary<string, byte> used as Set<string>.
                // TryAdd is atomic; idempotent re-registration is a no-op.
                ctx.FollowerEntries.TryAdd(fleetEntryName, 0);
            }
```

---

## T4 — Print at Symmetry.cs:168 (R4 auto-fix)

**File:** `src/V12_002.Symmetry.cs` (lines 167-168)

**No edit required.** The R4 torn-read concern is eliminated by T1: `ctx.MasterAnchorPrice` is now an atomic property whose getter performs `Interlocked.Read` via `BitConverter`. The existing Print:

```csharp
if (resolvedNow)
{
    Print(string.Format("[SYMMETRY_GUARD] MASTER ANCHOR LOCKED | Trade={0} | Anchor={1:F2} | FillQty={2}",
        ctx.TradeType, ctx.MasterAnchorPrice, ctx.MasterFilledQuantity));
    ...
}
```

— reads through the atomic getter, so no tear is possible on any architecture. R4 compliance is structural, not per-site.

---

## T5 — Lock-free pre-check anchor read

**File:** `src/V12_002.Symmetry.Follower.cs` (lines 34-44, inside `SymmetryGuardOnFollowerFill` pre-check block)

**OLD:**
```csharp
                if (symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var preCheckId) &&
                    symmetryDispatchById.TryGetValue(preCheckId, out var preCheckCtx))
                {
                    bool anchorReady;
                    double preCheckAnchor;
                    lock (preCheckCtx.Sync)
                    {
                        anchorReady   = preCheckCtx.IsResolved;
                        preCheckAnchor = preCheckCtx.MasterAnchorPrice;
                    }
                    if (anchorReady && preCheckAnchor > 0)
```

**NEW:**
```csharp
                if (symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var preCheckId) &&
                    symmetryDispatchById.TryGetValue(preCheckId, out var preCheckCtx))
                {
                    // V28.0-adr019: Lock-free acquire. IsResolved is a Volatile.Read on _anchorState;
                    // when it observes true (state == 2), the subsequent MasterAnchorPrice atomic
                    // read is guaranteed to see the published value (acquire-release pairing).
                    bool anchorReady = preCheckCtx.IsResolved;
                    double preCheckAnchor = anchorReady ? preCheckCtx.MasterAnchorPrice : 0.0;
                    if (anchorReady && preCheckAnchor > 0)
```

---

## T6 — Lock-free resolve anchor read

**File:** `src/V12_002.Symmetry.Follower.cs` (lines 126-135, inside `SymmetryGuardTryResolveFollower`)

**OLD:**
```csharp
            bool isResolved;
            double masterAnchor;
            lock (ctx.Sync)
            {
                // V1101E HOT-PATCH: Snapshot dispatch state under ctx.Sync, then release before any stateLock path.
                isResolved = ctx.IsResolved;
                masterAnchor = ctx.MasterAnchorPrice;
            }
```

**NEW:**
```csharp
            // V28.0-adr019: Lock-free acquire. IsResolved gates the atomic MasterAnchorPrice
            // read via acquire-release memory ordering; no stateLock path exists anymore.
            bool isResolved = ctx.IsResolved;
            double masterAnchor = isResolved ? ctx.MasterAnchorPrice : 0.0;
```

---

## T7 — Lock-free worklist iterator (Replace:127)

**File:** `src/V12_002.Symmetry.Replace.cs` (lines 119-148, inside `SymmetryGuardTryResolveFollowersForDispatch`)

**OLD:**
```csharp
            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx) && ctx != null)
            {
                lock (ctx.Sync)
                {
                    // V1101E HOT-PATCH: Build follower worklist under ctx.Sync only; never call stateLock paths while holding ctx.Sync.
                    foreach (string fleetEntryName in ctx.FollowerEntries)
                    {
                        if (string.IsNullOrEmpty(fleetEntryName))
                            continue;

                        if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var linkedDispatch))
                            continue;
                        if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                            continue;
                        if (!symmetryPendingFollowerFills.ContainsKey(fleetEntryName))
                            continue;

                        followersToResolve.Add(fleetEntryName);
                    }
                }
            }
```

**NEW:**
```csharp
            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx) && ctx != null)
            {
                // V28.0-adr019: ConcurrentDictionary.Keys enumeration is lock-free. Followers
                // added after this snapshot are picked up by the legacy dispatch-map scan
                // immediately below, preserving prior completeness.
                foreach (string fleetEntryName in ctx.FollowerEntries.Keys)
                {
                    if (string.IsNullOrEmpty(fleetEntryName))
                        continue;

                    if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var linkedDispatch))
                        continue;
                    if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                        continue;
                    if (!symmetryPendingFollowerFills.ContainsKey(fleetEntryName))
                        continue;

                    followersToResolve.Add(fleetEntryName);
                }
            }
```

---

## T8 — Lock-free cascade snapshot (Replace:189)

**File:** `src/V12_002.Symmetry.Replace.cs` (lines 187-193, inside `SymmetryGuardCascadeFollowerCleanup`)

**OLD:**
```csharp
            string[] followers;
            lock (ctx.Sync) { followers = ctx.FollowerEntries.ToArray(); }
```

**NEW:**
```csharp
            // V28.0-adr019: Lock-free snapshot of follower keys. Point-in-time atomic.
            string[] followers = ctx.FollowerEntries.Keys.ToArray();
```

---

## T9 — Lock-free `FollowerEntries.Remove` (Replace:221)

**File:** `src/V12_002.Symmetry.Replace.cs` (lines 217-227, inside `SymmetryGuardForgetEntry`)

**OLD:**
```csharp
            if (symmetryFleetEntryToDispatch.TryRemove(entryName, out var dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                lock (ctx.Sync)
                    ctx.FollowerEntries.Remove(entryName);
            }
```

**NEW:**
```csharp
            if (symmetryFleetEntryToDispatch.TryRemove(entryName, out var dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                // V28.0-adr019: TryRemove on ConcurrentDictionary is atomic.
                byte _discard;
                ctx.FollowerEntries.TryRemove(entryName, out _discard);
            }
```

---

## T10 — Lock-free prune iterator (Replace:244)

**File:** `src/V12_002.Symmetry.Replace.cs` (lines 238-263, inside `SymmetryGuardPruneDispatches`)

**OLD:**
```csharp
                    bool hasActiveFollowers = false;
                    lock (ctx.Sync)
                    {
                        foreach (string follower in ctx.FollowerEntries)
                        {
                            // V12.Phase8 [F-04]: activePositions is a ConcurrentDictionary but
                            // ContainsKey here is used alongside ctx.FollowerEntries iteration under
                            // ctx.Sync -- acquire stateLock for the read to prevent torn observations
                            // when ExecuteSmartDispatchEntry commits or removes entries concurrently.
                            bool exists;
                            exists = activePositions.ContainsKey(follower);
                            if (exists)
                            {
                                hasActiveFollowers = true;
                                break;
                            }
                        }
                    }
```

**NEW:**
```csharp
                    // V28.0-adr019: Lock-free scan. activePositions is already ConcurrentDictionary
                    // so its ContainsKey is atomic; ctx.FollowerEntries.Keys is also atomic. Both
                    // reads are tear-safe without any enclosing lock. Prune is best-effort pruning —
                    // any follower added after this scan will survive until the next TTL sweep.
                    bool hasActiveFollowers = false;
                    foreach (string follower in ctx.FollowerEntries.Keys)
                    {
                        if (activePositions.ContainsKey(follower))
                        {
                            hasActiveFollowers = true;
                            break;
                        }
                    }
```

---

## T11 — Lock-free AccountOrders ToArray (:244)

**File:** `src/V12_002.Orders.Callbacks.AccountOrders.cs` (lines 243-247, inside `TryGetDispatchFollowerEntries`)

**OLD:**
```csharp
            lock (ctx.Sync)
                followerEntries = ctx.FollowerEntries.ToArray();

            return followerEntries != null && followerEntries.Length > 0;
```

**NEW:**
```csharp
            // V28.0-adr019: Lock-free snapshot.
            followerEntries = ctx.FollowerEntries.Keys.ToArray();

            return followerEntries != null && followerEntries.Length > 0;
```

---

## T12 — Lock-free Propagation ToArray (:126)

**File:** `src/V12_002.Orders.Callbacks.Propagation.cs` (lines 124-128)

**OLD:**
```csharp
            if (symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out string dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                string[] snapshot;
                lock (ctx.Sync) { snapshot = ctx.FollowerEntries.ToArray(); }
                followerEntryNames = snapshot;
            }
```

**NEW:**
```csharp
            if (symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out string dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                // V28.0-adr019: Lock-free snapshot of follower keys.
                followerEntryNames = ctx.FollowerEntries.Keys.ToArray();
            }
```

---

## T13 — Lock-free SIMA Shadow iterator (:89)

**File:** `src/V12_002.SIMA.Shadow.cs` (lines 87-105, inside `ShadowMoveFollowerStops`)

**OLD:**
```csharp
            var followerEntryNames = new System.Collections.Generic.List<string>();
            lock (ctx.Sync)
            {
                foreach (string followerEntryName in ctx.FollowerEntries)
                {
                    if (string.IsNullOrEmpty(followerEntryName))
                        continue;
                    if (!symmetryFleetEntryToDispatch.TryGetValue(followerEntryName, out var linkedDispatch))
                        continue;
                    if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                        continue;
                    followerEntryNames.Add(followerEntryName);
                }
            }
```

**NEW:**
```csharp
            // V28.0-adr019: Lock-free scan over ConcurrentDictionary keys.
            var followerEntryNames = new System.Collections.Generic.List<string>();
            foreach (string followerEntryName in ctx.FollowerEntries.Keys)
            {
                if (string.IsNullOrEmpty(followerEntryName))
                    continue;
                if (!symmetryFleetEntryToDispatch.TryGetValue(followerEntryName, out var linkedDispatch))
                    continue;
                if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                    continue;
                followerEntryNames.Add(followerEntryName);
            }
```

---

## T14 — Delete `dailySummaryLock` field (R2)

**File:** `src/V12_002.cs` (lines 225-228)

**Pre-flight verification (engineer must run before edit):**
```bash
grep -nE "lock\s*\(\s*dailySummaryLock\s*\)" src/*.cs   # Expect: 0 hits (verified at plan time).
grep -nE "\bdailySummaryLock\b" src/*.cs                 # Expect: 1 hit (the declaration at :227).
```
If either expectation fails, STOP and escalate to P1 — scope has changed since plan was written.

**OLD:**
```csharp
        // V12 PERFORMANCE: Locks are BANNED in favor of the Actor model (Enqueue).
        // Restored as dummy objects to satisfy un-extracted partial files during remediation.
        private readonly object stateLock = new object();
        private readonly object dailySummaryLock = new object();
```

**NEW:**
```csharp
        // V12 PERFORMANCE: Locks are BANNED in favor of the Actor model (Enqueue).
        // V28.0-adr019 (R2): dailySummaryLock deleted -- field had zero lock() usages in src/.
        // Any future daily-summary CSV serialisation must use ConcurrentQueue-backed async
        // writes or the Enqueue actor path. stateLock retained as a dummy object to satisfy
        // un-extracted partial files; tracked for removal in a follow-up pass.
        private readonly object stateLock = new object();
```

**Audit gate 2 (now passing):** `grep -cn "dailySummaryLock" src/*.cs` must return `0`.

---

## T15 — Build tag increment

**File:** `src/V12_002.Properties.cs` (the `BUILD_TAG` constant — exact line provided by engineer during apply)

**Action:** Bump the build tag string constant from its current value to `1111.003-v28.0-adr019`. Engineer must preserve surrounding formatting and ASCII-only rules (no curly quotes, no emoji, no en/em-dash).

---

## T16 — Post-edit deployment gate (MANDATORY)

After T0-T15 are applied:

```powershell
powershell -File .\deploy-sync.ps1
```

**Expected:** ASCII Gate PASS. All hard links re-established.

Then the Director presses F5 in NinjaTrader to compile. Verify banner shows `BUILD_TAG = 1111.003-v28.0-adr019`.

**Validation greps (must all return 0):**
```bash
grep -nE "lock\s*\(\s*ctx\.Sync\s*\)" src/*.cs                 # 0 hits
grep -nE "lock\s*\(\s*preCheckCtx\.Sync\s*\)" src/*.cs         # 0 hits
grep -nE "\bdailySummaryLock\b" src/*.cs                       # 0 hits
grep -nE "public\s+readonly\s+object\s+Sync" src/*.cs          # 0 hits
grep -nE "new\s+HashSet<string>\s*\(\s*StringComparer\.Ordinal\s*\)\s*;\s*\}" src/V12_002.Symmetry.cs   # 0 hits (old FollowerEntries decl gone)
grep -nE "[^\x00-\x7F]" src/*.cs                               # 0 hits (ASCII gate)
```

---

## T17 — AMAL 6-test validation (Engineer → P6)

Run the AMAL harness:
```bash
python scripts/amal_harness.py --gate adr019
```

**Expected gates (all PASS required):**
1. Allocation baseline parity vs. pre-edit snapshot (no regression > 2%).
2. Logic Unit Tests (`tests/LogicTests.cs`) — zero failures.
3. Symmetry anchor resolution — first-fill-wins invariant holds under 16-thread concurrent dispatch.
4. Follower-set cardinality invariant — TryAdd / TryRemove produce expected transitions under concurrent stress.
5. DNA grep — zero banned-pattern hits (see T16 grep suite).
6. Stress test — 1000-dispatch soak with Interlocked metrics monotonic increase.

---

## R1 Compliance (Ghost-Order Rollback)

**Scope:** R1 required that any defensive purge of local dispatch state (`EmergencyPurgeEntry` proposed in a prior plan) must first cancel live broker orders via `CancelOrderSafe` and flatten positions via `FlattenPositionByName`.

**This plan:** Does NOT introduce `EmergencyPurgeEntry`. Verification:
```bash
grep -rE "EmergencyPurgeEntry|SymmetryGuardEmergencyPurge" src/ docs/brain/implementation_plan.md   # 0 hits
```

**Future engineers:** If a subsequent plan adds defensive purging of `SymmetryDispatchContext`, `activePositions`, `entryOrders`, or equivalent local state, the R1 rule is MANDATORY — call `CancelOrderSafe(order, pos)` for every live broker order (Working / Submitted / Accepted states) and `FlattenPositionByName(entryName)` for any non-flat position BEFORE any local-state removal.

The existing `SymmetryGuardCascadeFollowerCleanup` at `src/V12_002.Symmetry.Replace.cs:176-208` already follows this pattern and is the reference implementation.

---

## Verification Matrix

| Gate | How to run | Pass criterion |
|------|------------|----------------|
| ASCII gate | `powershell -File .\deploy-sync.ps1` | Script exits 0; reports "ASCII Gate PASS" |
| Lock grep | See T16 | All four lock-related greps return 0 hits |
| DNA grep | `python scripts/audit_scan.ps1 --strict` | No `lock(`, `stateLock` in business logic, no `unsafe`/`fixed`/`stackalloc` |
| Compile | F5 in NinjaTrader | BUILD_TAG banner shows `1111.003-v28.0-adr019`; zero CS errors |
| Unit tests | `dotnet test tests/LogicTests.cs` | Zero failures |
| AMAL | `python scripts/amal_harness.py --gate adr019` | All 6 gates PASS |
| Stress | 16-thread concurrent dispatch soak | First-fill-wins invariant holds; no exceptions |

---

## Director's Handoff Block (for Codex P5 Engineer)

```
You are the P5 Engineer. Read docs/brain/implementation_plan.md end-to-end before any edit.
Target build tag: 1111.003-v28.0-adr019. Branch: mission-uni-5-full-sync.

Apply tasks T0 through T15 in order. Each task has exact OLD / NEW blocks -- use
replace_file_content with anchor context. After T15, run T16 (deploy-sync.ps1 + grep suite)
and T17 (AMAL harness) and report verbatim output.

DO NOT:
- Skip the pre-flight grep for T14 (dailySummaryLock).
- Widen SymmetryDispatchContext visibility beyond `private sealed`.
- Introduce EmergencyPurgeEntry or any new defensive purge path.
- Use --no-verify, --no-gpg-sign, or amend published commits.
- Touch any file outside the T-list.

REPORT back to P1 with:
- Verbatim T16 grep output (all six greps must return 0 hits).
- Verbatim T17 AMAL output (all six gates must PASS).
- Build banner screenshot / text showing the new BUILD_TAG.
- Git status + diff summary.

Any deviation requires P1 re-adjudication -- do NOT attempt rework unilaterally.
```

---

## P4 Red Team "Trojan Horse" Adjudication Prompt (Arena)

**Delivery:** Paste the block below into the Arena session as the P4 prompt. Per GITHUB-LINK PROTOCOL, raw code is referenced via GitHub URLs, not inlined.

```
ROLE: P4 Adjudicator (Red Team) for ADR-019 Sovereign Substrate Repair.
TARGET: Branch `mission-uni-5-full-sync` of `universal-or-strategy`.
PLAN UNDER AUDIT: docs/brain/implementation_plan.md (build 1111.003-v28.0-adr019)

GitHub sources (read these BEFORE adjudicating):
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/src/V12_002.Symmetry.cs
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/src/V12_002.Symmetry.Follower.cs
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/src/V12_002.Symmetry.Replace.cs
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/src/V12_002.Orders.Callbacks.AccountOrders.cs
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/src/V12_002.Orders.Callbacks.Propagation.cs
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/src/V12_002.SIMA.Shadow.cs
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/src/V12_002.cs (line 227)
- https://github.com/<owner>/universal-or-strategy/blob/mission-uni-5-full-sync/docs/brain/audit_v28_1_platinum.md

MANDATE (P5 Redundancy Protocol -- NO TASK SPLITTING):
Every red-team agent (Codex, Gemini CLI, Jules) MUST independently audit ALL 17 tasks
and ALL of R1-R4. Consensus is valid ONLY if every agent confirms every item individually.

AUDIT MATRIX -- answer PASS / FAIL / EVIDENCE for each row:

| # | Claim to verify | Evidence required |
|---|-----------------|-------------------|
| 1 | T1 preserves `private sealed` (R3) | Cite line in NEW block that begins `private sealed class` |
| 2 | T1 provides atomic MasterWeightedFill via BitConverter+Interlocked (Probe 1) | Cite getter/setter signatures |
| 3 | T1 provides atomic MasterAnchorPrice (R4) | Cite getter/setter signatures |
| 4 | T1 resolves via CAS state machine (Probe 2, Probe 3) | Cite TryBeginResolve + PublishAnchor bodies |
| 5 | T1 replaces HashSet with ConcurrentDictionary<string, byte> preserving StringComparer.Ordinal (Probe 4) | Cite FollowerEntries declaration |
| 6 | T2 preserves first-fill-wins semantics (Probe 9) | Explain why `= weighted` is equivalent to the original `+= averageFillPrice * fillQty` |
| 7 | T3-T13 each replace exactly one `lock(*.Sync)` site | Count: 11 removals expected; cite line numbers per file |
| 8 | T14 deletes dailySummaryLock field; pre-flight grep documented (R2) | Cite pre-flight grep expectations |
| 9 | R1 compliance: plan introduces NO EmergencyPurgeEntry | Run `grep -rE "EmergencyPurgeEntry" src/ docs/` and report hits |
| 10 | ASCII gate: zero non-ASCII in any NEW block | Scan each NEW block for codepoints > 0x7F |
| 11 | DNA compliance: no `unsafe` / `fixed` / `stackalloc` / `volatile` field modifiers added | Review NEW blocks |
| 12 | `using System.Threading;` added to Symmetry.cs (T0) | Verify line appears between `using System.Linq;` and `using NinjaTrader.Cbi;` |
| 13 | Sync field DELETED from context class (not just unused) | Verify T1 NEW block does NOT declare `public readonly object Sync` |
| 14 | Build tag bump planned (T15) | Verify T15 specifies `1111.003-v28.0-adr019` |
| 15 | Deploy-sync mandated post-edit (T16) | Verify T16 names `deploy-sync.ps1` |
| 16 | AMAL harness planned (T17) | Verify T17 names `scripts/amal_harness.py --gate adr019` |
| 17 | No lock widening: plan MUST NOT introduce any new `lock(...)` anywhere | Scan NEW blocks for `lock(` -- expect 0 |

TROJAN-HORSE CHECK (independent scan, not in the matrix above):
The plan may contain a deliberately-subtle flaw. Candidate hiding spots include:
- Incorrect memory-ordering argument (e.g. claiming release semantics where a full fence is needed).
- Off-by-one line number on an OLD anchor that would cause replace_file_content to fail.
- A `using` directive in the wrong file.
- Missed strengthening of MasterFilledQuantity when it participates in `+=` semantics elsewhere.

If you find a flaw, name it, cite the line, and mark plan as REVISE. If you find none,
state explicitly "Trojan-Horse scan: no defects found" -- a blanket "looks good" is a FAIL
and will be rejected as rubber-stamping.

DELIVER verdict: APPROVED / REVISE / BLOCK. Majority (2/3) required for APPROVED.
Attach per-row evidence. No summarisation of the plan -- only per-claim verification.
```

---

## Post-Use Workflow Audit

Per CLAUDE.md "Mandatory Workflow Self-Improvement":

- **Did any step produce an unexpected result?** `mcp__jcodemunch__plan_turn` returned "Repository not found: ." — had to call `resolve_repo` with the absolute path first. Documented as guidance for the architect workflow: `plan_turn` requires the resolved repo id, not the literal `"."` string unless the repo was indexed under that name.
- **Was any rule ambiguous?** R1 scope was ambiguous (applied to prior-plan `EmergencyPurgeEntry` which does not exist in current `src/`). Resolved by adding "R1 Compliance" section that converts R1 from an edit-site rule to a compliance gate for future plans.
- **Was a step missing?** Yes — the pre-flight grep for T14 (dailySummaryLock) and the T0 `using` insertion were not present in the prior draft that was BLOCKed. Added both.

**Commit tag (workflow self-improvement):** `workflow(architect_intake): add R1-as-compliance-gate, pre-flight grep for field deletion, and explicit `using System.Threading;` insertion step`
