# Round 28 -- v28.0 Structural Repair: MmioDispatchMirror Hybrid

**Authoritative Build Tag:** `1111.002-v28.0`
**Architect (P3):** Claude (Opus 4.6)
**Engineer (P4) Target:** Codex / Jules
**Supersedes:** Prior `docs/brain/implementation_plan.md` (v28.0 unsafe-kernel variant, approved 2026-04-11 and **INVALIDATED 2026-04-11** by the CS0227 / CS0103 forensic from P1 (Antigravity)). Also supersedes `.claude/plans/abundant-humming-puppy.md`.
**Plan-of-Record:** This file. Source of truth: `C:\Users\Mohammed Khalid\.claude\plans\vivid-toasting-globe.md` (approved 2026-04-11 via plan-mode exit).

---

## RESET NOTICE (2026-04-11)

The previously-published v28.0 implementation plan has been **invalidated** by a P1 forensic that demonstrates NinjaTrader 8's internal NinjaScript compiler rejects `/unsafe` regardless of the `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` entry in `Linting.csproj` -- because `Linting.csproj` is an evaluation-only StyleCop/Roslyn tooling project that never participates in the NT8 runtime build. The probe file `src/V12_002.UnsafeGate.cs` currently breaks F5 compilation with `CS0227` (Unsafe code may only appear if compiling with /unsafe) and `CS0103` (The name `Unsafe` does not exist in the current context). The sandbox kernel at `sandbox/R28_MmioSpscRing/MmioSpscRing.cs` (5.28 ns/op) is structurally untransplantable into NT8 because every one of its primitives -- `unsafe sealed class`, `byte* _region`, `fixed (T* p = ...)`, `Unsafe.WriteUnaligned<T>`, `Unsafe.ReadUnaligned<T>`, `AcquirePointer(ref byte*)` -- is a distinct compile-blocker in the target runtime. This reset plan replaces the unsafe-kernel architecture with a managed-only Hybrid design (keep `SPSCRing<T>` as the hot path, refactor `FleetDispatchSlot` to blittable with a parallel `FleetDispatchSideband[]` for managed refs, replace CRC16 with XorShadow via struct-field XOR, add an optional `MmioDispatchMirror` write-through sidecar using `MemoryMappedViewAccessor.Read<T>/Write<T>`) that satisfies every R28 mission clause without a single `unsafe` keyword.

---

## Context

P1 (Antigravity) has relayed a forensic verdict that invalidates the foundational assumption of the previously-approved v28.0 plan: **NinjaTrader 8's internal compiler rejects `/unsafe` regardless of `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in `Linting.csproj`**. The compile probe file `src/V12_002.UnsafeGate.cs` triggers two stop-the-world errors:

- `CS0227`: Unsafe code may only appear if compiling with /unsafe.
- `CS0103`: The name `Unsafe` does not exist in the current context.

Cause: `Linting.csproj` is an **evaluation-only** project used by StyleCop/Roslyn tooling -- it never participates in the NT8 runtime build. NinjaTrader 8 compiles NinjaScript strategies through its own embedded compiler (`NinjaTrader.Custom.dll` generation), which hardcodes `/unsafe-` and does not resolve the `System.Runtime.CompilerServices.Unsafe` NuGet reference.

**Consequence:** The Round 28 sandbox kernel at `sandbox/R28_MmioSpscRing/MmioSpscRing.cs` (which achieves 5.28 ns/op) is structurally untransplantable into NT8. Every performance-critical primitive it relies on (`unsafe sealed class`, `byte* _region`, `fixed (T* p = ...)`, `Unsafe.WriteUnaligned<T>`, `Unsafe.ReadUnaligned<T>`, `AcquirePointer(ref byte*)`) is a distinct compile-blocker. No combination of namespace renames, reference shims, or csproj tweaks can unlock them.

The build tree is **currently broken**: `V12_002.UnsafeGate.cs` ships `CS0227` as soon as `F5` is pressed. Build integrity restoration is the most urgent item in this plan.

**Intended outcome:**
1. Restore NT8 build integrity (zero errors on F5) by removing the unsafe probe file.
2. Deliver the R28 blittable-slot + sideband refactor using managed-only primitives (no `unsafe`, no `Unsafe.*`, no `byte*`, no `fixed` blocks, no pointer arithmetic).
3. Deliver zero-allocation MMIO communication via `MemoryMappedViewAccessor.Read<T>/Write<T>` (the managed escape hatch built into .NET 4.8's BCL -- the underlying pointer access is encapsulated inside `System.IO.MemoryMappedFiles.dll`, caller stays managed).
4. Preserve hot-path performance by keeping the proven managed `SPSCRing<T>` as the primary dispatch lane and layering the MMIO mirror as a write-through sidecar.
5. Replace CRC16 integrity with the XorShadow 64-bit contract mandated by ADR-016, computed via struct-field XOR (no pointer access required).
6. Synchronize `BUILD_TAG` across the working tree to `"1111.002-v28.0"`.

---

## 1. Forensic Summary: Why v28.0 Must Be Rewritten

| Defect | Evidence | Impact |
|---|---|---|
| NT8 compiler rejects `/unsafe` | `src/V12_002.UnsafeGate.cs:21` `private unsafe void` triggers CS0227 on F5 | Every sandbox primitive blocked |
| `System.Runtime.CompilerServices.Unsafe` unresolved | `src/V12_002.UnsafeGate.cs:27` `Unsafe.SizeOf<long>()` triggers CS0103 | `Unsafe.ReadUnaligned<T>` / `WriteUnaligned<T>` unusable |
| `AcquirePointer(ref byte*)` requires unsafe caller | `MemoryMappedViewAccessor.SafeMemoryMappedViewHandle.AcquirePointer` signature is `ref byte*` | Raw pointer lifetime pattern from FIX-D6 impossible |
| `FleetDispatchSlot` still non-blittable | `src/V12_002.Photon.Pool.cs:19-38` has `Account`, `string FleetEntryName`, `string ExpectedKey` | `where T : unmanaged` constraint unsatisfiable even if unsafe were allowed |
| `Linting.csproj` does not gate NT8 build | File is `OutputType=Library`, `EnableDefaultCompileItems=false`, no `AllowUnsafeBlocks` set -- wraps `src/*.cs` for tooling only | Prior plan's PREFLIGHT-G1 "RESOLVED" was a false positive |

**Forensic truth:** the previously-approved v28.0 plan's core architectural move (transplant of a raw-pointer kernel into `V12_002`) is **structurally impossible** in the target runtime. This plan replaces it with a managed-only design that still satisfies the R28 mission.

---

## 2. Architectural Decision: Hybrid Path (Managed Ring + MMIO Mirror)

### Three paths considered

**Path A -- Full MMIO Replacement:** Replace `SPSCRing<T>` entirely with `MmioSpscRing<T>` backed by `MemoryMappedViewAccessor.Read<T>/Write<T>`. Pure MMIO, single ring. Latency: ~100-150 ns/op (per-call `AcquirePointer` inside BCL, plus `Thread.MemoryBarrier()` fences for ordering). **Rejected:** ~3x regression against the current ~50 ns/op managed ring penalizes every production trade to subsidize a sidecar reader that does not yet exist.

**Path B -- Build Fix Only:** Delete `V12_002.UnsafeGate.cs`, leave everything else alone. Restores compilation. **Rejected:** Fails the mission's "zero-allocation MMIO communication" requirement and the R28 blittable-slot mandate from ADR-016. Leaves the slot refactor permanently blocked by the D1 defect.

**Path C -- Hybrid (selected):** Keep `SPSCRing<FleetDispatchSlot>` as the hot path (unchanged semantics, ~50 ns/op). Refactor `FleetDispatchSlot` to blittable (moves 3 managed refs to `FleetDispatchSideband[]`). Replace CRC16 with XorShadow (ADR-016) via struct-field XOR. Add a write-through `MmioDispatchMirror` that publishes each enqueued slot to a `MemoryMappedFile` for the Antigravity Nexus OS sidecar to observe read-only. Delete `V12_002.UnsafeGate.cs`. Bump `BUILD_TAG` to `1111.002-v28.0`.

### Why Hybrid wins all three mission criteria

| Mission clause | Hybrid answer |
|---|---|
| "Restores build-integrity" | Step 1 deletes the UnsafeGate probe; F5 recompiles clean |
| "Zero-allocation MMIO communication" | `MmioDispatchMirror` mirrors every hot-path slot into a named `MemoryMappedFile` via `Write<T>` (zero allocation -- `Write<T>` takes `ref T` and writes bytes through an internal pointer encapsulated by the BCL) |
| "Maximizing order-dispatch performance" | Primary consumer reads from the heap-backed `SPSCRing<T>` (unchanged ~50 ns/op); the MMF mirror is producer-side-only and does not slow the consumer. Producer pays ~30 ns extra only when `_photonMmioMirror != null`. |
| "Lock-free SPSC (Platinum Standard)" | `SPSCRing<T>` already uses `Volatile.Read/Write` with 7-long cursor padding -- no change |
| "XorShadow ADR-016 integrity" | Replaces CRC16; salt is per-ring random from `Guid.NewGuid().GetHashCode() * 0x9E3779B97F4A7C15UL`; computed via struct-field XOR, no unsafe |
| "ASCII-only strings (CLAUDE.md Section 7)" | All new Print/log strings audited |
| "No `lock(stateLock)`" | Zero new locks introduced; actor/volatile pattern preserved |

### Why this does not recreate the R28 defect matrix

| Legacy R28 Defect | v28.0 Resolution |
|---|---|
| D1 -- non-blittable slot blocks `where T : unmanaged` | Not needed: `SPSCRing<T>` uses `where T : struct` and the mirror uses `Write<T> where T : struct` + a runtime blittable check. Slot is blittable either way via the refactor. |
| D2 -- shadow overwrites tail of payload | Impossible: `Shadow` is reserved as the LAST field of `FleetDispatchSlot` via `[StructLayout(LayoutKind.Explicit, Size = 64)] [FieldOffset(56)] public ulong Shadow;`. Compute covers fields 0..48 only. |
| D3 -- C# 11 / .NET 8+ APIs | No `nint`, no `Unsafe.*`, no `NativeMemory`, no `Span<T>`, no `stackalloc`, no `Environment.ProcessId`. All managed primitives are .NET Framework 4.8 / C# 7.3 compatible. |
| D4 -- `/unsafe` unverified | RESOLVED by forensic: unsafe is banned. Plan avoids it entirely. |
| D5 -- namespace placement | `MmioDispatchMirror` is a `private sealed class` nested in `public partial class V12_002` in `NinjaTrader.NinjaScript.Strategies` -- matches existing ring placement exactly. |
| D6 -- `byte*` lifetime undefined | No `byte*` anywhere. `MemoryMappedViewAccessor` is owned by `MmioDispatchMirror`, disposed in `ProcessShutdownSIMA` and `OnStateChange(State.Terminated)`. |
| D7 -- torn cursor reads | `_producerCursor` is managed `long` accessed via `Volatile.Read/Write` (existing ring pattern). MMF cursor uses `ReadInt64`/`Write(long, long)` which is atomic on aligned longs on x64 + `Thread.MemoryBarrier()` fence for happens-before ordering. |

---

## 3. Working-Tree Ground Truth (verified 2026-04-11)

| Surface | Location | Current State |
|---|---|---|
| Canonical BUILD_TAG | `src/V12_002.cs:44` | `public const string BUILD_TAG = "1109.003-v14.2";` |
| Stale build tag orphan | `src/V12_002.Constants.cs:12` | `public const string Version = "Build 972";` (137 builds behind, unused in Print statements -- BUILD_TAG is the real source) |
| Ring class | `src/V12_002.Photon.Ring.cs:69` | `private sealed class SPSCRing<T> where T : struct` -- heap `T[]`, `ushort[] _checksums`, `Volatile.Read/Write` on `_producerCursor`/`_consumerCursor` with 7-long cursor padding |
| Ring integrity helpers | `src/V12_002.Photon.Ring.cs:13-59` | `Crc16Byte`, `Crc16Int`, `Crc16Long`, `Crc16Double`, `ComputeProxyCrc` -- all to be deleted and replaced with `ComputeFleetDispatchShadow` |
| Slot struct | `src/V12_002.Photon.Pool.cs:19-38` | 3 managed refs (`Account Account`, `string FleetEntryName`, `string ExpectedKey`), 9 value-type fields |
| Pool field decl | `src/V12_002.cs:322` | `private PhotonOrderPool _photonPool;` |
| Ring field decl | `src/V12_002.cs:323` | `private SPSCRing<FleetDispatchSlot> _photonDispatchRing;` |
| Pool + ring construction | `src/V12_002.Lifecycle.cs:203-204` | Lives in `OnStateChange` / `State.Configure` branch |
| Producer site #1 (market) | `src/V12_002.SIMA.Dispatch.cs:400-444` | Builds `FleetDispatchSlot`, `ComputeProxyCrc`, `TryEnqueue(ref _slot, _slotCrc)`, fallback to `_pendingFleetDispatches` |
| Producer site #2 (limit) | `src/V12_002.SIMA.Dispatch.cs:505-544` | Identical pattern with `_slotLmt` / `_slotCrcLmt` |
| Consumer site #1 (main drain) | `src/V12_002.SIMA.Fleet.cs:210-253` in `PumpFleetDispatch` | `TryDequeue(out _ringSlot, out _storedCrc, out _crcValid)`, re-computes CRC, rollback on mismatch, calls `ProcessFleetSlot` with managed refs from slot |
| Consumer site #2 (abort drain) | `src/V12_002.SIMA.Fleet.cs:178-207` in `PumpFleetDispatch` abort branch | `TryDequeue(out abortSlot, out _ac, out _av)`, reads `abortSlot.ExpectedKey` for delta rollback |
| Consumer site #3 (shutdown drain) | `src/V12_002.SIMA.Lifecycle.cs:95-109` in `ProcessShutdownSIMA` | `TryDequeue(out ringSlot, out _crc, out _cv)`, reads `ringSlot.ExpectedKey` for delta rollback |
| Blocking probe file | `src/V12_002.UnsafeGate.cs` (46 lines) | Compiles to CS0227 + CS0103 -- MUST be deleted |
| Sandbox (reference only) | `sandbox/R28_MmioSpscRing/MmioSpscRing.cs` | 162 lines, `unsafe sealed class`, `where T : unmanaged`, `byte* _region`, `AcquirePointer(ref byte*)`, `Unsafe.WriteUnaligned<T>`, `Unsafe.ReadUnaligned<T>`. Achieves 5.28 ns/op in isolation. Unshippable to NT8. |
| `unsafe` usage in `src/` | grep `\bunsafe\b` | ONLY in `V12_002.UnsafeGate.cs` -- deleting that file eliminates all unsafe usage |
| `System.IO.MemoryMappedFiles` in `src/` | grep | ZERO hits. New dependency. Must survive a preflight gate. |
| `Marshal.*` usage in `src/` | grep `Marshal\.(StructureToPtr|PtrToStructure|SizeOf|OffsetOf)` | ZERO hits. We introduce `Marshal.SizeOf` + `Marshal.OffsetOf` for the first time in the static assert. |
| `lock(stateLock)` | grep | ZERO executable occurrences. Ban already enforced. |
| `Linting.csproj` config | `Linting.csproj:4-10` | `<TargetFramework>net48</TargetFramework>`, `<LangVersion>8.0</LangVersion>`. No `AllowUnsafeBlocks`. Evaluation-only project. |

---

## 4. Mandatory Preflight Gate: MmfGate

**Rationale:** zero `System.IO.MemoryMappedFiles` usage exists in `src/`. NT8's NinjaScript compile environment may not automatically reference the `System.IO.MemoryMappedFiles.dll` assembly. A one-file probe verifies the reference chain before any production edits.

**Step 0a -- create `src/V12_002.MmfGate.cs`:**

```csharp
// V12.15 PREFLIGHT-G2: MemoryMappedFile Reference Resolution Gate
// Probe purpose: verify System.IO.MemoryMappedFiles is resolvable by the
// NT8 internal compiler. Supersedes the failed V12_002.UnsafeGate.cs probe.
// This file is deleted after the gate passes (Step 0c below).

using System;
using System.IO.MemoryMappedFiles;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private void V28_Mmf_Preflight_Gate()
        {
            try
            {
                using (var mmf = MemoryMappedFile.CreateNew(null, 256))
                using (var acc = mmf.CreateViewAccessor(0, 256))
                {
                    acc.Write(0L, unchecked((long)0xAA55AA55AA55AA55UL));
                    long val = acc.ReadInt64(0L);
                    Print(string.Format("V12 R28 PREFLIGHT-G2: OK (readback=0x{0:X})", val));
                }
            }
            catch (Exception ex)
            {
                Print("V12 R28 PREFLIGHT-G2: RUNTIME ERROR -- " + ex.Message);
            }
        }
    }
}
```

**Step 0b -- deploy + compile probe:**
1. `powershell -File .\deploy-sync.ps1` (ASCII gate must pass)
2. Director presses F5 in NT8.
3. **Expected compile result:** clean (zero errors). The probe method is never invoked, but the type resolution + namespace import must succeed.

**Step 0c -- on success:**
1. Delete `src/V12_002.MmfGate.cs`.
2. `deploy-sync.ps1`.
3. Director F5 once more to confirm clean tree.
4. Proceed to Step 1.

**Step 0d -- on failure (compile error on `MemoryMappedFile` or `CreateViewAccessor`):**
1. Engineer reports the exact error code (likely `CS0234` or `CS0246`) to the Director.
2. Engineer adds `<Reference Include="System.IO.MemoryMappedFiles" />` to the NT8 NinjaScript references via the NinjaScript Editor -> Reference Explorer dialog (Director may need to click through the UI).
3. Re-run Step 0b.
4. If a second failure follows, **halt the plan** and escalate -- the MMIO mirror becomes infeasible and a Path B (build-fix + blittable refactor only, no MMIO) fallback plan is the replacement deliverable.

**Preflight authority:** The gate is the single point where Codex is permitted to pause and escalate to the Director. Everywhere else in Steps 1-14, Codex proceeds linearly without user interaction.

---

## 5. Implementation Plan (linear, no reordering)

### Step 1 -- Overwrite `docs/brain/implementation_plan.md`

Already in progress at the time this plan-of-record is being written by the P3 Architect. Engineer should verify the working tree's `docs/brain/implementation_plan.md` begins with the Reset Notice block above; if not, overwrite with this file's contents.

### Step 2 -- Delete `src/V12_002.UnsafeGate.cs`

**OLD (entire file, 46 lines) -- delete:**

```csharp
// V12.15 PREFLIGHT-G1: Unsafe & Assembly Resolution Gate
// This file is a "canary" to verify that the NinjaTrader 8 compiler supports:
// 1. /unsafe blocks
// 2. System.Runtime.CompilerServices.Unsafe (NuGet/GAC)
// 3. Pointer arithmetic
// [... 42 more lines, full contents deleted ...]
```

**NEW:** file removed from working tree via `git rm src/V12_002.UnsafeGate.cs` followed by a `deploy-sync.ps1` rerun so the hard link in the NT8 Strategies folder is also cleared.

**Why deletion, not refactor:** The file's stated purpose ("verify unsafe compiles") is disproven by the forensic; keeping it as a dormant empty partial is dead code. Also, the probe file's `using System.Runtime.CompilerServices;` pulls in the `Unsafe` type unconditionally, which is the CS0103 trigger -- any retention strategy has to strip that line, at which point the file has no purpose.

### Step 3 -- Refactor `FleetDispatchSlot` to blittable + add `FleetDispatchSideband`

**File:** `src/V12_002.Photon.Pool.cs`

**OLD (lines 1-38):**

```csharp
using System;
using System.Threading;
using NinjaTrader.Cbi;

// V14.2 Sovereign Photon: Pre-allocated Order Proxy Pool + Execution ID Dedup Ring
// ADR-012 + ADR-011: Zero-allocation fleet dispatch + lock-free dedup

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private const int PhotonPoolCapacity = 64; // V14.2 FIX-D4: 5 signals x 12 accounts = 60 < 64
        private const int MaxOrdersPerSlot = 7;    // 1 entry + 1 stop + 5 targets

        // === FleetDispatchSlot ===
        // V14.2 FIX-D1: PoolSlotIndex links this struct to PhotonOrderPool._orderArrays[index].
        // The consumer retrieves Order[] via _photonPool.GetByIndex(slot.PoolSlotIndex).

        private struct FleetDispatchSlot
        {
            // NT8 API refs (unavoidable managed references)
            public Account Account;
            public int OrderCount;               // Actual orders (1-7)
            public int PoolSlotIndex;            // FIX-D1: Index into PhotonOrderPool (-1 if heap fallback)

            // Value-type dispatch data (CRC16-verified)
            public int Quantity;
            public double EntryPrice;
            public double StopPrice;
            public int TargetCount;
            public OrderAction Action;
            public int ReservedDelta;
            public long SignalTicks;

            // String keys (kept as refs; interned by NT8 runtime)
            public string FleetEntryName;
            public string ExpectedKey;
        }
```

**NEW (lines 1-55, replacing the entire header + struct):**

```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading;
using NinjaTrader.Cbi;

// v28.0 Sovereign Photon Kernel: blittable slot + parallel sideband + XorShadow integrity
// ADR-012 + ADR-016: zero-allocation fleet dispatch, lock-free SPSC, MMIO-ready payload

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private const int PhotonPoolCapacity = 64; // 5 signals x 12 accounts = 60 < 64
        private const int MaxOrdersPerSlot   = 7;  // 1 entry + 1 stop + 5 targets

        // FleetDispatchSlot (v28.0, blittable, 64 bytes, cache-line sized)
        //
        // Layout contract (ADR-016):
        //   - Explicit layout so Marshal.OffsetOf is deterministic across framework versions.
        //   - Size = 64 B = exactly one cache line.
        //   - Shadow is the LAST 8 bytes (FieldOffset 56). XorShadow computes over [0..48);
        //     bytes [48..56) are implicit padding (Size attribute auto-zeros them).
        //   - All managed reference fields (Account, FleetEntryName, ExpectedKey) moved to
        //     FleetDispatchSideband below, indexed by PoolSlotIndex (same index the pool uses).
        //
        // Blittable verification: the struct contains only int, long, double, ulong primitives.
        // MemoryMappedViewAccessor.Write<FleetDispatchSlot> will accept it.
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FleetDispatchSlot
        {
            [FieldOffset(0)]  public double EntryPrice;
            [FieldOffset(8)]  public double StopPrice;
            [FieldOffset(16)] public long   SignalTicks;
            [FieldOffset(24)] public int    PoolSlotIndex;   // also the SidebandIndex (same index)
            [FieldOffset(28)] public int    OrderCount;
            [FieldOffset(32)] public int    Quantity;
            [FieldOffset(36)] public int    TargetCount;
            [FieldOffset(40)] public int    Action;          // (int)OrderAction, cast at boundary
            [FieldOffset(44)] public int    ReservedDelta;
            // bytes 48..56 reserved padding (Size=64 auto-zeros)
            [FieldOffset(56)] public ulong  Shadow;          // XorShadow integrity (last 8 bytes)
        }

        // Parallel sideband: managed refs indexed by PoolSlotIndex.
        // Producer writes sideband[i] BEFORE publishing slot to the ring; consumer reads
        // sideband[i] AFTER dequeue and clears it when slot processing completes. No GC
        // pressure: the array is allocated once at State.Configure and reused for the
        // lifetime of the strategy.
        private struct FleetDispatchSideband
        {
            public Account Account;
            public string  FleetEntryName;
            public string  ExpectedKey;
        }

        private FleetDispatchSideband[] _photonSideband;
        private ulong                   _photonShadowSalt;
```

Then leave `PoolClaimResult`, `PhotonOrderPool`, `FnvHash64`, and `ExecutionIdRing` at lines 44-286 UNCHANGED (verified lines 40-286 of current file do not touch slot refs).

### Step 4 -- Add `ComputeFleetDispatchShadow` helper to `Photon.Pool.cs`

**NEW (append to `V12_002.Photon.Pool.cs`, just before the closing brace of the partial class):**

```csharp
        // ComputeFleetDispatchShadow (ADR-016)
        //
        // 64-bit XorShadow over FleetDispatchSlot value fields, salted per-ring with a
        // Guid-derived random. Covers every byte of the struct EXCLUDING the trailing
        // 8-byte Shadow slot itself. The exclusion is by construction: we XOR field-by-field
        // and deliberately omit `slot.Shadow` from the accumulator.
        //
        // Collision resistance: 2^-64 false-pass probability (vs. 2^-16 for the old CRC16).
        // Determinism: the salt is captured once per strategy instance at State.Configure;
        // producer and consumer use the same salt field. Cross-process readers (Antigravity
        // sidecar) read the salt from a published header byte in the MMF mirror (see Step 6).
        //
        // Zero allocation: BitConverter.DoubleToInt64Bits is a struct-to-long reinterpret,
        // not a boxing conversion. No heap allocation on any path.
        private static ulong ComputeFleetDispatchShadow(ref FleetDispatchSlot slot, ulong salt)
        {
            ulong acc = salt;
            acc ^= unchecked((ulong)BitConverter.DoubleToInt64Bits(slot.EntryPrice));
            acc = (acc << 13) | (acc >> 51); // rotate-left 13 to diffuse field positions
            acc ^= unchecked((ulong)BitConverter.DoubleToInt64Bits(slot.StopPrice));
            acc = (acc << 7)  | (acc >> 57);
            acc ^= unchecked((ulong)slot.SignalTicks);
            acc = (acc << 11) | (acc >> 53);
            acc ^= ((ulong)(uint)slot.PoolSlotIndex)
                 | (((ulong)(uint)slot.OrderCount) << 32);
            acc = (acc << 17) | (acc >> 47);
            acc ^= ((ulong)(uint)slot.Quantity)
                 | (((ulong)(uint)slot.TargetCount) << 32);
            acc = (acc << 19) | (acc >> 45);
            acc ^= ((ulong)(uint)slot.Action)
                 | (((ulong)(uint)slot.ReservedDelta) << 32);
            return acc;
        }
```

### Step 5 -- Strip `SPSCRing<T>` integrity plumbing + drop CRC16 helpers

**File:** `src/V12_002.Photon.Ring.cs` (OVERWRITE entire file)

**OLD (133 lines, current):** the full contents shown in the working-tree inspection, including `Crc16Byte`, `Crc16Int`, `Crc16Long`, `Crc16Double`, `ComputeProxyCrc`, and the `ushort[] _checksums` array + `checksum`-carrying overloads.

**NEW (compact ring with XorShadow-aware API):**

```csharp
using System;
using System.Threading;

// v28.0 Sovereign Photon: lock-free SPSC ring, XorShadow integrity in-slot
// ADR-012 + ADR-016: zero-allocation, zero-GC, single-producer/single-consumer
//
// Integrity contract: T must reserve its LAST 8 bytes as a `ulong Shadow` field
// populated by the caller BEFORE enqueue (via ComputeFleetDispatchShadow or equivalent).
// The ring does not inspect, compute, or verify Shadow -- the caller owns integrity.
// Rationale: keeping the ring agnostic of T's shape lets us reuse the class for any
// blittable slot type without re-plumbing the checksum pipeline.

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private sealed class SPSCRing<T> where T : struct
        {
            private readonly T[] _buffer;
            private readonly int _mask;

            // Cache-line isolation: 7 long pads between cursors. False-sharing hurts
            // only throughput, not correctness. Both cursors are Volatile-fenced.
            private long _producerCursor;
#pragma warning disable 0169
            private long _pad1, _pad2, _pad3, _pad4, _pad5, _pad6, _pad7;
#pragma warning restore 0169
            private long _consumerCursor;

            public int Capacity { get { return _buffer.Length; } }

            public int Count
            {
                get { return (int)(Volatile.Read(ref _producerCursor) - Volatile.Read(ref _consumerCursor)); }
            }

            public bool IsEmpty
            {
                get { return Volatile.Read(ref _producerCursor) == Volatile.Read(ref _consumerCursor); }
            }

            public SPSCRing(int capacityPowerOf2)
            {
                if (capacityPowerOf2 < 2 || (capacityPowerOf2 & (capacityPowerOf2 - 1)) != 0)
                    throw new ArgumentException("Capacity must be power of 2", "capacityPowerOf2");
                _buffer = new T[capacityPowerOf2];
                _mask = capacityPowerOf2 - 1;
                _producerCursor = 0;
                _consumerCursor = 0;
            }

            public bool TryEnqueue(ref T item)
            {
                long prod = Volatile.Read(ref _producerCursor);
                long cons = Volatile.Read(ref _consumerCursor);
                if (prod - cons >= _buffer.Length)
                    return false; // ring full
                int idx = (int)(prod & _mask);
                _buffer[idx] = item;
                Volatile.Write(ref _producerCursor, prod + 1); // publish barrier
                return true;
            }

            public bool TryDequeue(out T item)
            {
                long cons = Volatile.Read(ref _consumerCursor);
                long prod = Volatile.Read(ref _producerCursor);
                if (cons >= prod)
                {
                    item = default(T);
                    return false; // ring empty
                }
                int idx = (int)(cons & _mask);
                item = _buffer[idx];
                Volatile.Write(ref _consumerCursor, cons + 1); // consume barrier
                return true;
            }
        }
    }
}
```

Note: `ComputeProxyCrc`, `Crc16Byte`, `Crc16Int`, `Crc16Long`, `Crc16Double` DELETED. The only integrity helper now is `ComputeFleetDispatchShadow` (Step 4, lives in Photon.Pool.cs).

### Step 6 -- New file `src/V12_002.Photon.MmioMirror.cs`

**NEW (entire file):**

```csharp
using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;

// v28.0 Sovereign Photon MMIO Mirror: write-through sidecar for cross-process observation
// ADR-016 + R28 Arena directive
//
// Contract:
//   - Strategy (V12_002 producer) is the SOLE WRITER. MPSC is not supported.
//   - Antigravity Nexus OS sidecar (separate process) is a READ-ONLY observer that
//     attaches to the named MMF at runtime. It never writes cursor or slot bytes.
//   - Mirror is OPTIONAL: _photonMmioMirror may be null. Producers check before calling
//     TryPublish. If MMIO reference resolution fails at construction, the field stays
//     null and the strategy runs hot-path-only. Hot-path dispatch never blocks on
//     mirror state.
//
// Layout inside the MMF (total = 128 B header + capacity * 64 B payload):
//   [0..8)    producer cursor  (long; strategy writes, sidecar reads)
//   [8..64)   pad              (cache-line isolation)
//   [64..72)  shadow salt      (ulong; copied from _photonShadowSalt so the sidecar
//                                can verify slot integrity independently)
//   [72..80)  reserved
//   [80..128) pad              (header rounded to 128 B)
//   [128..)   slot array       (capacity * 64 B, each a FleetDispatchSlot)
//
// No AcquirePointer. No raw byte*. All writes go through
// MemoryMappedViewAccessor.Write<T>/Write(long,long), which encapsulate pointer
// access inside System.IO.MemoryMappedFiles.dll. The caller (this class) stays
// fully managed and NT8-compilable.

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private sealed class MmioDispatchMirror : IDisposable
        {
            private const int HeaderBytes         = 128;
            private const long ProducerCursorOffset = 0;
            private const long ShadowSaltOffset     = 64;
            private const long SlotsBaseOffset      = 128;

            private readonly MemoryMappedFile         _mmf;
            private readonly MemoryMappedViewAccessor _accessor;
            private readonly int                      _capacity;
            private readonly int                      _mask;
            private readonly int                      _slotSize;
            private long                              _producerCursor;
            private int                               _disposed;

            public string Name { get; private set; }

            public MmioDispatchMirror(string name, int capacity, int slotSize, ulong salt)
            {
                if (capacity < 2 || (capacity & (capacity - 1)) != 0)
                    throw new ArgumentException("Capacity must be power of 2", "capacity");
                if (slotSize <= 0 || (slotSize & 7) != 0)
                    throw new ArgumentException("Slot size must be a positive multiple of 8", "slotSize");

                _capacity = capacity;
                _mask     = capacity - 1;
                _slotSize = slotSize;
                Name      = name;

                long totalBytes = HeaderBytes + (long)slotSize * (long)capacity;

                _mmf      = MemoryMappedFile.CreateOrOpen(name, totalBytes, MemoryMappedFileAccess.ReadWrite);
                _accessor = _mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

                // Zero header and publish salt
                for (long i = 0; i < HeaderBytes; i++)
                    _accessor.Write(i, (byte)0);

                unchecked { _accessor.Write(ShadowSaltOffset, (long)salt); }

                _producerCursor = 0L;
                _accessor.Write(ProducerCursorOffset, _producerCursor);
            }

            // Fire-and-forget write-through. Returns false if the MMF ring is full
            // relative to the producer cursor; in that case the slot is dropped from
            // the mirror but still succeeds on the primary heap ring.
            public bool TryPublish(ref FleetDispatchSlot slot)
            {
                if (Volatile.Read(ref _disposed) != 0) return false;

                long prod = _producerCursor;
                // Sidecar cursor is not read back in single-writer/observer mode; wrap
                // is allowed (the observer is expected to keep up; stale slots in the
                // MMF are simply overwritten on the next wrap).
                int idx = (int)(prod & _mask);
                long slotOffset = SlotsBaseOffset + (long)idx * (long)_slotSize;

                // Write the full 64-byte slot. Write<T> takes ref T and performs a
                // single marshaled copy into the mapped region. No boxing. No allocation.
                _accessor.Write(slotOffset, ref slot);

                // Publish barrier: slot bytes must be visible before cursor update.
                // Thread.MemoryBarrier is a full StoreStore/LoadLoad fence (~15 ns on
                // modern CPUs). Required because MemoryMappedViewAccessor.Write does
                // not itself emit a fence.
                Thread.MemoryBarrier();

                _producerCursor = prod + 1;
                _accessor.Write(ProducerCursorOffset, _producerCursor);
                return true;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                try { _accessor.Dispose(); } catch { }
                try { _mmf.Dispose();      } catch { }
            }

            // Diagnostic helper; not on the hot path.
            public string GetDiagnostics()
            {
                return string.Format(
                    "MmioDispatchMirror: name={0} capacity={1} slotSize={2} prod={3}",
                    Name, _capacity, _slotSize, Volatile.Read(ref _producerCursor));
            }
        }
    }
}
```

### Step 7 -- Add fields in `src/V12_002.cs`

**OLD (lines 44 and 322-323):**

```csharp
        public const string BUILD_TAG = "1109.003-v14.2";  // Freeze-proof structural repair: non-blocking semaphores, chunked flatten, queue monitoring
```

```csharp
        private PhotonOrderPool _photonPool;
        private SPSCRing<FleetDispatchSlot> _photonDispatchRing;
```

**NEW (line 44):**

```csharp
        public const string BUILD_TAG = "1111.002-v28.0";  // R28 v28.0 -- blittable slot + XorShadow + optional MMIO mirror
```

**NEW (lines 322-324, replacing 322-323):**

```csharp
        private PhotonOrderPool _photonPool;
        private SPSCRing<FleetDispatchSlot> _photonDispatchRing;
        private MmioDispatchMirror _photonMmioMirror; // v28.0 -- optional MMIO write-through; may be null if CreateOrOpen throws
```

Note: `_photonSideband` and `_photonShadowSalt` are declared in Photon.Pool.cs (Step 3 NEW block lines 51-52). Do not redeclare here.

### Step 8 -- Initialize mirror + sideband in `Lifecycle.cs`

**File:** `src/V12_002.Lifecycle.cs`

**OLD (lines 201-204):**

```csharp
                // V14.2 Sovereign Photon [ADR-012]: Pre-allocate dispatch pool + ring
                // Capacity 64: supports 5 concurrent signals x 12 accounts = 60 < 64
                _photonPool = new PhotonOrderPool(PhotonPoolCapacity);
                _photonDispatchRing = new SPSCRing<FleetDispatchSlot>(PhotonPoolCapacity);
```

**NEW (lines 201-233, replacing 201-204):**

```csharp
                // v28.0 Sovereign Photon [ADR-012 + ADR-016]: pool + ring + sideband + salt + MMIO mirror
                // Capacity 64: 5 concurrent signals x 12 accounts = 60 < 64
                _photonPool = new PhotonOrderPool(PhotonPoolCapacity);
                _photonDispatchRing = new SPSCRing<FleetDispatchSlot>(PhotonPoolCapacity);
                _photonSideband = new FleetDispatchSideband[PhotonPoolCapacity];
                _photonShadowSalt = unchecked((ulong)Guid.NewGuid().GetHashCode() * 0x9E3779B97F4A7C15UL);

                // Static assert: Shadow must be the last 8 bytes of FleetDispatchSlot (ADR-016)
                {
                    int _slotSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FleetDispatchSlot));
                    int _shadowOffset = System.Runtime.InteropServices.Marshal.OffsetOf(typeof(FleetDispatchSlot), "Shadow").ToInt32();
                    if (_slotSize != 64 || _shadowOffset != 56)
                    {
                        throw new InvalidOperationException(string.Format(
                            "FleetDispatchSlot layout invariant violated: size={0}, shadowOffset={1}; expected size=64, offset=56",
                            _slotSize, _shadowOffset));
                    }
                }

                // Optional MMIO mirror. Named per-process so multiple NT instances do not collide.
                // Failure is non-fatal: hot path runs against the heap ring even if the mirror fails.
                try
                {
                    string _mmfName = "V12_FleetDispatch_" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
                    _photonMmioMirror = new MmioDispatchMirror(_mmfName, PhotonPoolCapacity, 64, _photonShadowSalt);
                    Print(string.Format("[PHOTON MMIO] mirror online: {0}", _mmfName));
                }
                catch (Exception _mmioEx)
                {
                    _photonMmioMirror = null;
                    Print("[PHOTON MMIO] mirror unavailable (hot path unaffected): " + _mmioEx.Message);
                }
```

### Step 9 -- Dispose mirror + clear sideband on shutdown (two sites)

**9a.** `src/V12_002.SIMA.Lifecycle.cs` -- `ProcessShutdownSIMA`:

**OLD (lines 94-109):**

```csharp
            // V14.2 FIX-F6: Drain Photon ring on shutdown with full delta rollback + pool release
            {
                FleetDispatchSlot ringSlot;
                ushort _crc;
                bool _cv;
                while (_photonDispatchRing != null
                    && _photonDispatchRing.TryDequeue(out ringSlot, out _crc, out _cv))
                {
                    if (ringSlot.ReservedDelta != 0)
                        AddExpectedPositionDelta(ringSlot.ExpectedKey, -ringSlot.ReservedDelta);
                    ClearDispatchSyncPending(ringSlot.ExpectedKey);
                    if (ringSlot.PoolSlotIndex >= 0)
                        _photonPool.ReleaseByIndex(ringSlot.PoolSlotIndex);
                }
                Print("[SIMA] Photon ring cleared on shutdown with delta rollback.");
            }
```

**NEW (lines 94-117, sideband-aware drain):**

```csharp
            // v28.0 shutdown drain: sideband-aware, XorShadow-free (we do not verify on shutdown;
            // we just need to release pool + roll back delta). Sideband entries are zeroed after.
            {
                FleetDispatchSlot ringSlot;
                while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out ringSlot))
                {
                    int _sbIdx = ringSlot.PoolSlotIndex;
                    string _expectedKey = (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                        ? _photonSideband[_sbIdx].ExpectedKey
                        : null;
                    if (ringSlot.ReservedDelta != 0 && _expectedKey != null)
                        AddExpectedPositionDelta(_expectedKey, -ringSlot.ReservedDelta);
                    if (_expectedKey != null)
                        ClearDispatchSyncPending(_expectedKey);
                    if (_sbIdx >= 0)
                    {
                        _photonPool.ReleaseByIndex(_sbIdx);
                        if (_sbIdx < _photonSideband.Length)
                            _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                    }
                }
                Print("[SIMA] Photon ring cleared on shutdown with delta rollback.");
            }
```

**9b.** `src/V12_002.Lifecycle.cs` -- `OnStateChange(State.Terminated)` branch. Locate the existing Terminated handler (grep `State.Terminated` in Lifecycle.cs to confirm the exact line) and append:

```csharp
            // v28.0 MMIO mirror teardown
            if (_photonMmioMirror != null)
            {
                try { _photonMmioMirror.Dispose(); } catch { }
                _photonMmioMirror = null;
            }
```

If no `State.Terminated` handler exists in Lifecycle.cs yet, add one to the `OnStateChange` switch. Engineer must verify by reading the file first.

### Step 10 -- Producer rewrite: market order path (`V12_002.SIMA.Dispatch.cs:400-444`)

**OLD (lines 400-444):**

```csharp
                            FleetDispatchSlot _slot = new FleetDispatchSlot
                            {
                                Account = acct,
                                OrderCount = _orderIdx,
                                PoolSlotIndex = _poolSlotIndex,
                                Quantity = followerQty,
                                EntryPrice = entryPrice,
                                StopPrice = stopPrice,
                                TargetCount = dispatchTargetCount,
                                Action = action,
                                ReservedDelta = reservedDelta,
                                SignalTicks = DateTime.UtcNow.Ticks,
                                FleetEntryName = fleetEntryName,
                                ExpectedKey = expectedKey
                            };
                            ushort _slotCrc = ComputeProxyCrc(ref _slot);

                            Interlocked.Increment(ref _pendingFleetDispatchCount);

                            if (_poolSlotIndex >= 0 && _photonDispatchRing.TryEnqueue(ref _slot, _slotCrc))
                            {
                                // Success: slot in ring, pool array linked via PoolSlotIndex
                            }
                            else
                            {
                                // Ring full or pool exhausted -- fallback to ConcurrentQueue
                                if (_poolSlotIndex >= 0)
                                {
                                    // V14.2 FIX-D3b: Pool succeeded but ring full -- release pool, heap-copy
                                    Print("[PHOTON] Ring full -- fallback to ConcurrentQueue");
                                    Order[] legacyOrders = new Order[_orderIdx];
                                    Array.Copy(_proxyOrders, legacyOrders, _orderIdx);
                                    _photonPool.ReleaseByIndex(_poolSlotIndex);
                                    _proxyOrders = legacyOrders;
                                }
                                _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                                {
                                    Account = acct,
                                    Orders = _proxyOrders,
                                    FleetEntryName = fleetEntryName,
                                    ExpectedKey = expectedKey,
                                    ReservedDelta = reservedDelta,
                                    SignalTicks = DateTime.UtcNow.Ticks
                                });
                            }
```

**NEW (lines 400-454, sideband-first publish pattern):**

```csharp
                            // v28.0 blittable slot + sideband-first publish
                            if (_poolSlotIndex >= 0)
                            {
                                _photonSideband[_poolSlotIndex].Account        = acct;
                                _photonSideband[_poolSlotIndex].FleetEntryName = fleetEntryName;
                                _photonSideband[_poolSlotIndex].ExpectedKey    = expectedKey;
                                Thread.MemoryBarrier(); // sideband writes visible before ring publish
                            }

                            FleetDispatchSlot _slot = new FleetDispatchSlot
                            {
                                EntryPrice    = entryPrice,
                                StopPrice     = stopPrice,
                                SignalTicks   = DateTime.UtcNow.Ticks,
                                PoolSlotIndex = _poolSlotIndex,
                                OrderCount    = _orderIdx,
                                Quantity      = followerQty,
                                TargetCount   = dispatchTargetCount,
                                Action        = (int)action,
                                ReservedDelta = reservedDelta
                            };
                            _slot.Shadow = ComputeFleetDispatchShadow(ref _slot, _photonShadowSalt);

                            Interlocked.Increment(ref _pendingFleetDispatchCount);

                            if (_poolSlotIndex >= 0 && _photonDispatchRing.TryEnqueue(ref _slot))
                            {
                                // Success: slot in ring, pool + sideband linked by PoolSlotIndex.
                                // MMIO mirror is a best-effort write-through -- never blocks or fails hot path.
                                if (_photonMmioMirror != null)
                                {
                                    try { _photonMmioMirror.TryPublish(ref _slot); } catch { }
                                }
                            }
                            else
                            {
                                // Ring full or pool exhausted -- fallback to ConcurrentQueue
                                if (_poolSlotIndex >= 0)
                                {
                                    // Pool succeeded but ring full -- release pool, clear sideband, heap-copy
                                    Print("[PHOTON] Ring full -- fallback to ConcurrentQueue");
                                    Order[] legacyOrders = new Order[_orderIdx];
                                    Array.Copy(_proxyOrders, legacyOrders, _orderIdx);
                                    _photonPool.ReleaseByIndex(_poolSlotIndex);
                                    _photonSideband[_poolSlotIndex] = default(FleetDispatchSideband);
                                    _proxyOrders = legacyOrders;
                                }
                                _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                                {
                                    Account = acct,
                                    Orders = _proxyOrders,
                                    FleetEntryName = fleetEntryName,
                                    ExpectedKey = expectedKey,
                                    ReservedDelta = reservedDelta,
                                    SignalTicks = DateTime.UtcNow.Ticks
                                });
                            }
```

### Step 11 -- Producer rewrite: limit order path (`V12_002.SIMA.Dispatch.cs:505-544`)

**OLD (lines 505-544):**

```csharp
                            FleetDispatchSlot _slotLmt = new FleetDispatchSlot
                            {
                                Account = acct,
                                OrderCount = 1,
                                PoolSlotIndex = _poolSlotIndexLmt,
                                Quantity = followerQty,
                                EntryPrice = entry.LimitPrice > 0 ? entry.LimitPrice : 0,
                                StopPrice = 0,
                                TargetCount = 0,
                                Action = action,
                                ReservedDelta = reservedDelta,
                                SignalTicks = DateTime.UtcNow.Ticks,
                                FleetEntryName = fleetEntryName,
                                ExpectedKey = expectedKey
                            };
                            ushort _slotCrcLmt = ComputeProxyCrc(ref _slotLmt);

                            Interlocked.Increment(ref _pendingFleetDispatchCount);

                            if (_poolSlotIndexLmt >= 0 && _photonDispatchRing.TryEnqueue(ref _slotLmt, _slotCrcLmt))
                            {
                                // Success
                            }
                            else
                            {
                                if (_poolSlotIndexLmt >= 0)
                                {
                                    Order[] legacyOrdersLmt = new Order[] { entry };
                                    _photonPool.ReleaseByIndex(_poolSlotIndexLmt);
                                    _proxyOrdersLmt = legacyOrdersLmt;
                                }
                                // [the plan does not modify the ConcurrentQueue fallback line below,
                                //  which Engineer will re-verify by reading the current tree]
                            }
```

**NEW (lines 505-554, same sideband-first pattern):**

```csharp
                            if (_poolSlotIndexLmt >= 0)
                            {
                                _photonSideband[_poolSlotIndexLmt].Account        = acct;
                                _photonSideband[_poolSlotIndexLmt].FleetEntryName = fleetEntryName;
                                _photonSideband[_poolSlotIndexLmt].ExpectedKey    = expectedKey;
                                Thread.MemoryBarrier();
                            }

                            FleetDispatchSlot _slotLmt = new FleetDispatchSlot
                            {
                                EntryPrice    = entry.LimitPrice > 0 ? entry.LimitPrice : 0,
                                StopPrice     = 0,
                                SignalTicks   = DateTime.UtcNow.Ticks,
                                PoolSlotIndex = _poolSlotIndexLmt,
                                OrderCount    = 1,
                                Quantity      = followerQty,
                                TargetCount   = 0,
                                Action        = (int)action,
                                ReservedDelta = reservedDelta
                            };
                            _slotLmt.Shadow = ComputeFleetDispatchShadow(ref _slotLmt, _photonShadowSalt);

                            Interlocked.Increment(ref _pendingFleetDispatchCount);

                            if (_poolSlotIndexLmt >= 0 && _photonDispatchRing.TryEnqueue(ref _slotLmt))
                            {
                                if (_photonMmioMirror != null)
                                {
                                    try { _photonMmioMirror.TryPublish(ref _slotLmt); } catch { }
                                }
                            }
                            else
                            {
                                if (_poolSlotIndexLmt >= 0)
                                {
                                    Order[] legacyOrdersLmt = new Order[] { entry };
                                    _photonPool.ReleaseByIndex(_poolSlotIndexLmt);
                                    _photonSideband[_poolSlotIndexLmt] = default(FleetDispatchSideband);
                                    _proxyOrdersLmt = legacyOrdersLmt;
                                }
                                // [preserve existing ConcurrentQueue fallback path from lines 534+
                                //  -- no changes needed; _pendingFleetDispatches.Enqueue new FleetDispatchRequest {...}]
                            }
```

### Step 12 -- Consumer rewrite: main drain (`V12_002.SIMA.Fleet.cs:210-253`)

**OLD (lines 210-253, PumpFleetDispatch main path):**

```csharp
            // V14.2 [ADR-012]: Try Photon ring first (zero-alloc primary path)
            FleetDispatchSlot _ringSlot;
            ushort _storedCrc;
            bool _crcValid;
            if (_photonDispatchRing != null
                && _photonDispatchRing.TryDequeue(out _ringSlot, out _storedCrc, out _crcValid))
            {
                // CRC16 integrity verification (defense-in-depth)
                ushort _computedCrc = ComputeProxyCrc(ref _ringSlot);
                if (_computedCrc != _storedCrc)
                {
                    // V14.2 FIX-D3a: CRC failure -- full rollback + dict cleanup + pool release
                    Interlocked.Increment(ref _photonCrcFailures);
                    Print(string.Format(
                        "[PHOTON_CRC] INTEGRITY FAILURE: expected=0x{0:X4} got=0x{1:X4} entry={2} -- SKIPPING",
                        _storedCrc, _computedCrc, _ringSlot.FleetEntryName));
                    if (_ringSlot.ReservedDelta != 0)
                        AddExpectedPositionDeltaLocked(_ringSlot.ExpectedKey, -_ringSlot.ReservedDelta);
                    ClearDispatchSyncPending(_ringSlot.ExpectedKey);
                    activePositions.TryRemove(_ringSlot.FleetEntryName, out _);
                    entryOrders.TryRemove(_ringSlot.FleetEntryName, out _);
                    stopOrders.TryRemove(_ringSlot.FleetEntryName, out _);
                    for (int tNum = 1; tNum <= 5; tNum++)
                    {
                        var td = GetTargetOrdersDictionary(tNum);
                        if (td != null) td.TryRemove(_ringSlot.FleetEntryName, out _);
                    }
                    _followerBrackets.TryRemove(_ringSlot.FleetEntryName, out _);
                    if (_ringSlot.PoolSlotIndex >= 0)
                        _photonPool.ReleaseByIndex(_ringSlot.PoolSlotIndex);
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                    // Self-reschedule
                    if (!_photonDispatchRing.IsEmpty || !_pendingFleetDispatches.IsEmpty)
                        try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
                    return;
                }

                // Valid slot -- retrieve Order[] from pool via PoolSlotIndex
                Order[] ringOrders = _photonPool.GetByIndex(_ringSlot.PoolSlotIndex);
                ProcessFleetSlot(_ringSlot.Account, ringOrders, _ringSlot.OrderCount,
                    _ringSlot.FleetEntryName, _ringSlot.ExpectedKey, _ringSlot.ReservedDelta,
                    _ringSlot.SignalTicks, _ringSlot.PoolSlotIndex);
                return;
            }
```

**NEW (lines 210-268, XorShadow + sideband):**

```csharp
            // v28.0 [ADR-012 + ADR-016]: Photon ring, XorShadow integrity, sideband refs
            FleetDispatchSlot _ringSlot;
            if (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out _ringSlot))
            {
                int _sbIdx = _ringSlot.PoolSlotIndex;

                // Sideband read (BEFORE shadow verify -- sideband is required for rollback logs)
                FleetDispatchSideband _sb = (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                    ? _photonSideband[_sbIdx]
                    : default(FleetDispatchSideband);

                // XorShadow integrity verification (defense-in-depth, structurally stronger than CRC16)
                ulong _stored   = _ringSlot.Shadow;
                _ringSlot.Shadow = 0UL;                             // zero before recompute (compute excludes Shadow by construction, but this is belt-and-braces)
                ulong _recomputed = ComputeFleetDispatchShadow(ref _ringSlot, _photonShadowSalt);
                _ringSlot.Shadow = _stored;                         // restore for downstream logging
                if (_recomputed != _stored)
                {
                    Interlocked.Increment(ref _photonCrcFailures);
                    Print(string.Format(
                        "[PHOTON_SHADOW] INTEGRITY FAILURE: expected=0x{0:X16} got=0x{1:X16} entry={2} -- SKIPPING",
                        _stored, _recomputed, _sb.FleetEntryName));
                    if (_ringSlot.ReservedDelta != 0 && _sb.ExpectedKey != null)
                        AddExpectedPositionDeltaLocked(_sb.ExpectedKey, -_ringSlot.ReservedDelta);
                    if (_sb.ExpectedKey != null)
                        ClearDispatchSyncPending(_sb.ExpectedKey);
                    if (_sb.FleetEntryName != null)
                    {
                        activePositions.TryRemove(_sb.FleetEntryName, out _);
                        entryOrders.TryRemove(_sb.FleetEntryName, out _);
                        stopOrders.TryRemove(_sb.FleetEntryName, out _);
                        for (int tNum = 1; tNum <= 5; tNum++)
                        {
                            var td = GetTargetOrdersDictionary(tNum);
                            if (td != null) td.TryRemove(_sb.FleetEntryName, out _);
                        }
                        _followerBrackets.TryRemove(_sb.FleetEntryName, out _);
                    }
                    if (_sbIdx >= 0)
                    {
                        _photonPool.ReleaseByIndex(_sbIdx);
                        if (_sbIdx < _photonSideband.Length)
                            _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                    }
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                    if (!_photonDispatchRing.IsEmpty || !_pendingFleetDispatches.IsEmpty)
                        try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
                    return;
                }

                // Valid slot -- retrieve Order[] from pool via PoolSlotIndex
                Order[] ringOrders = _photonPool.GetByIndex(_sbIdx);
                ProcessFleetSlot(_sb.Account, ringOrders, _ringSlot.OrderCount,
                    _sb.FleetEntryName, _sb.ExpectedKey, _ringSlot.ReservedDelta,
                    _ringSlot.SignalTicks, _sbIdx);

                // Clear sideband to release refs (avoid stale retention across ring wraps)
                if (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                    _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                return;
            }
```

### Step 13 -- Consumer rewrite: abort drain (`V12_002.SIMA.Fleet.cs:178-207`)

**OLD (lines 178-207):**

```csharp
            if (isFlattenRunning || !EnableSIMA)
            {
                // V14.2 FIX-D2: Drain Photon ring FIRST with full delta rollback + pool release
                FleetDispatchSlot abortSlot;
                ushort _ac;
                bool _av;
                while (_photonDispatchRing != null
                    && _photonDispatchRing.TryDequeue(out abortSlot, out _ac, out _av))
                {
                    if (abortSlot.ReservedDelta != 0)
                        AddExpectedPositionDeltaLocked(abortSlot.ExpectedKey, -abortSlot.ReservedDelta);
                    ClearDispatchSyncPending(abortSlot.ExpectedKey);
                    if (abortSlot.PoolSlotIndex >= 0)
                        _photonPool.ReleaseByIndex(abortSlot.PoolSlotIndex);
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                }
                // ... then drain legacy ConcurrentQueue (unchanged) ...
```

**NEW (lines 178-208):**

```csharp
            if (isFlattenRunning || !EnableSIMA)
            {
                // v28.0: drain Photon ring FIRST with sideband-aware delta rollback + pool release
                FleetDispatchSlot abortSlot;
                while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out abortSlot))
                {
                    int _sbIdx = abortSlot.PoolSlotIndex;
                    string _expectedKey = (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                        ? _photonSideband[_sbIdx].ExpectedKey
                        : null;
                    if (abortSlot.ReservedDelta != 0 && _expectedKey != null)
                        AddExpectedPositionDeltaLocked(_expectedKey, -abortSlot.ReservedDelta);
                    if (_expectedKey != null)
                        ClearDispatchSyncPending(_expectedKey);
                    if (_sbIdx >= 0)
                    {
                        _photonPool.ReleaseByIndex(_sbIdx);
                        if (_sbIdx < _photonSideband.Length)
                            _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                    }
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                }
                // ... then drain legacy ConcurrentQueue (unchanged) ...
```

### Step 14 -- BUILD_TAG sync across working tree

1. `src/V12_002.cs:44` -- already updated in Step 7 to `"1111.002-v28.0"`.
2. `src/V12_002.Constants.cs:12` -- **OLD:** `public const string Version = "Build 972";` **NEW:** `public const string Version = "Build 1111.002-v28.0";`.
3. `docs/brain/nexus_a2a.json` -- update `"mission"` to `"Round 28 v28.0 -- MmioDispatchMirror Hybrid (managed MMIO)"`, `"build_tag"` to `"1111.002-v28.0"`, `"phase"` to `"Kernel Integration (R28 v28.0)"`, `"last_updated"` to the execution date.
4. Memory hook `~/.claude/projects/C--WSGTA-universal-or-strategy/memory/project_state_build1004.md` -- update the BUILD_TAG reference (Architect will do this during post-Codex audit, not Engineer scope).

### Step 15 -- Deploy sync + F5 (MANDATORY per CLAUDE.md)

After ALL the above `src/` edits:

1. `powershell -File .\deploy-sync.ps1`
   - ASCII Gate MUST PASS on every modified file (check_ascii.py or in-script gate)
   - Hard links to `%USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\Strategies\` rehydrated
2. Director presses F5 in NinjaTrader.
3. Zero compile errors expected.
4. Enable V12_002 strategy on a chart -- banner MUST print `Build 1111.002-v28.0`.
5. Observe `[PHOTON MMIO] mirror online: V12_FleetDispatch_<pid>` in the output window, OR the graceful fallback message `[PHOTON MMIO] mirror unavailable (hot path unaffected): <reason>` if MMF creation fails at runtime for any reason.

Skipping this step is a protocol violation (file-edit tools break hard links; stale DLL compiles from the OLD src/).

---

## 6. Verification Gates (Engineer P4 self-audit before handoff back to P3)

1. **Compile:** `F5` in NT8 produces **zero errors**. CS0227, CS0103, CS0246 must all be clean. If any appear, escalate immediately -- do not attempt shim patches.
2. **ASCII gate:** Every modified `.cs` file contains only ASCII in string literals. Run `python scripts/check_ascii.py src/V12_002.Photon.Ring.cs src/V12_002.Photon.Pool.cs src/V12_002.Photon.MmioMirror.cs src/V12_002.Lifecycle.cs src/V12_002.cs src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Lifecycle.cs src/V12_002.Constants.cs` (or equivalent).
3. **Static assert (runtime):** `State.Configure` throws `InvalidOperationException` immediately on startup if `Marshal.SizeOf(typeof(FleetDispatchSlot)) != 64` or `Marshal.OffsetOf(typeof(FleetDispatchSlot), "Shadow").ToInt32() != 56`. Verify the exception message mentions the offending values. (Engineer writes a deliberately-broken variant in a scratch branch, confirms the assert fires, then reverts.)
4. **Roundtrip selftest:** Append to `OnStartUp` (after existing init) a one-shot test that constructs a synthetic `FleetDispatchSlot` with known values, populates `_photonSideband[0]`, calls `ComputeFleetDispatchShadow`, enqueues, dequeues, re-verifies the shadow, and asserts byte-level equality on every value field. Log `[PHOTON SELFTEST] v28.0 ring + shadow + sideband OK` on success. Remove after first successful run -- this is a development scaffold, not a production check.
5. **Corruption injection (development-only):** In a scratch branch, flip a single bit in a slot between enqueue and dequeue (e.g., via a debug-only back-door method). Assert the dequeue path logs `[PHOTON_SHADOW] INTEGRITY FAILURE` and takes the rollback path without throwing. Do NOT merge the back-door method.
6. **Lock scan:** `grep -rn "lock\s*(\s*stateLock\s*)" src/` -- MUST return zero executable hits. Enforced by existing CI; reconfirm after edits.
7. **Leak check:** 10-minute paper-trading run after enabling the strategy; record `Process.GetCurrentProcess().PrivateMemorySize64` delta. Expected delta `< 5 MB`. MMF allocation shows up as ~4 KB resident. Sideband array: 64 * 24 bytes = 1536 bytes (negligible).
8. **Handle hygiene:** Disable the strategy -> re-enable -> repeat 3 times. `MmioDispatchMirror.Dispose` must run each time. Process Explorer should show the `Section`/`FileMapping` handle count returning to baseline after each disable. Zero leaked handles.
9. **MMF name collision:** Launch a second NT instance with the same strategy active. Each instance's `V12_FleetDispatch_<pid>` is unique by process id. Both should report `[PHOTON MMIO] mirror online` with distinct names. No `IOException`.
10. **Forensics + architect self-audit** (per CLAUDE.md "Engineer Self-Audit P4"):
    - `/architect` subagent: confirm the managed ring + mirror design matches this plan section-by-section.
    - `/forensics` subagent: grep for `\bunsafe\b`, `Unsafe\.`, `byte\*`, `fixed\s*\(`, `stackalloc`, `nint` -- MUST be zero hits across `src/`. Also re-run the `lock(stateLock)` scan.

All 10 gates must report PASS before Codex hands back to Architect.

---

## 7. Director's Handoff Block (Engineer P4 -- Codex or Jules)

```
=== ROUND 28 v28.0 HANDOFF: MmioDispatchMirror Hybrid ===
FROM: Claude (P3 Architect, Opus 4.6)
TO:   Codex (P4 primary) / Jules (P4 standby)
TAG:  1111.002-v28.0
PLAN: docs/brain/implementation_plan.md (this file, as overwritten in Step 1)
SUPERSEDES: prior docs/brain/implementation_plan.md unsafe-kernel variant
            (invalidated by CS0227 forensic 2026-04-11)

TARGET RUNTIME:  NinjaTrader 8 / .NET Framework 4.8 / C# 7.3 (NT8 internal compiler)
FORBIDDEN:       unsafe, byte*, fixed blocks, sizeof(T) in unsafe ctx,
                 System.Runtime.CompilerServices.Unsafe.*, nint, Span<T>,
                 stackalloc expressions, NativeMemory, Environment.ProcessId,
                 AcquirePointer(ref byte*), C# 8+ features

REQUIRED:        managed-only primitives, MemoryMappedViewAccessor.Read<T>/Write<T>,
                 Marshal.SizeOf, Marshal.OffsetOf, GCHandle-free allocation,
                 [StructLayout(LayoutKind.Explicit, Size=64)] on FleetDispatchSlot,
                 ASCII-only string literals, no lock(stateLock), no agent impersonation

EXECUTION ORDER: Steps 0 (preflight) -> 1 (plan overwrite, already done by P3) ->
                 2-14 -> 15 (deploy+F5)
                 NO reordering. NO skipping. Preflight gate is the SOLE escalation
                 point; if MmfGate fails compile, halt and report the exact error
                 code to Director.

TOUCH LIST:
  src/V12_002.UnsafeGate.cs          DELETE
  src/V12_002.MmfGate.cs             NEW (preflight only; deleted after gate passes)
  src/V12_002.Photon.MmioMirror.cs   NEW
  src/V12_002.Photon.Pool.cs         MODIFY (slot refactor + sideband + salt + shadow helper)
  src/V12_002.Photon.Ring.cs         OVERWRITE (strip CRC16, new TryEnqueue/TryDequeue signatures)
  src/V12_002.cs                     MODIFY (BUILD_TAG line 44, fields line 322-324)
  src/V12_002.Lifecycle.cs           MODIFY (construction lines 201-204, Terminated teardown)
  src/V12_002.SIMA.Dispatch.cs       MODIFY (producer sites lines 400-444 and 505-544)
  src/V12_002.SIMA.Fleet.cs          MODIFY (consumer sites lines 178-207 and 210-253)
  src/V12_002.SIMA.Lifecycle.cs      MODIFY (shutdown drain lines 94-109)
  src/V12_002.Constants.cs           MODIFY (line 12 Version bump)
  docs/brain/implementation_plan.md  ALREADY OVERWRITTEN by P3 (this file)
  docs/brain/nexus_a2a.json          MODIFY (mission/build_tag/phase/last_updated)

AUDIT GATES: Section 6 items 1-10. All 10 must PASS before handoff back to P3.

POST-EDIT:   deploy-sync.ps1 MUST RUN. Director F5 in NT8. Verify banner shows
             "Build 1111.002-v28.0". Verify [PHOTON MMIO] mirror message prints.

RULES:       ASCII-only in ALL C# string literals (no curly quotes, no em-dashes,
             no Unicode arrows -- CLAUDE.md Section 7).
             No lock(stateLock) anywhere.
             No silent fallbacks on shadow mismatch -- log [PHOTON_SHADOW] INTEGRITY FAILURE.
             MMIO mirror failure is NON-fatal: log and proceed.
             No agent impersonation (Identity Integrity).

DIRECTOR INVOCATION (non-blocking, push/wake-on-event flow):
  1. Claude spawns Codex via codex:codex-rescue subagent:
       codex exec --prompt-file docs/brain/implementation_plan.md --model gpt-5-codex
     run in background. Tool result carries the task output file path.
  2. On task completion the harness emits <task-notification>. Claude Reads the
     output file exactly once and ingests the transcript.
  3. Claude then verifies Section 6 gates against the working tree independently
     (no reliance on Codex self-reports for pass/fail).
  4. Report PASS/FAIL back to Director.
```

---

## 8. Remarks, Deferrals, and Open Items

1. **Cross-process cursor publication for the sidecar:** The `MmioDispatchMirror` publishes a producer cursor at MMF offset 0 and the XorShadow salt at offset 64. The Antigravity Nexus OS sidecar (when it exists) reads the salt once and uses it to verify each slot's Shadow independently. The sidecar is expected to track its own internal cursor and poll the MMF producer cursor; there is no sidecar-to-strategy writeback channel in v28.0. A future v28.1 can add a sidecar heartbeat at MMF offset 72 (reserved in Step 6 layout) if two-way coordination becomes necessary.

2. **Performance expectations (managed path, vs. sandbox unsafe path):**
   - Hot path producer (heap ring only): ~50 ns/op (unchanged from current)
   - Hot path producer (heap ring + MMIO mirror): ~80 ns/op (+30 ns for Write<T> + barrier + cursor update)
   - Hot path consumer: ~50 ns/op (heap ring read; no MMF touch)
   - Sandbox unsafe reference: 5.28 ns/op (unshippable)
   - The hybrid path is ~10x slower than the sandbox but does not regress against the current production ring on the consumer side, and adds only ~60% overhead on the producer side for the cross-process feature.

3. **Alternative Path C (full MMIO replacement) deferred.** If the Director later requires the entire ring to live in the MMF (e.g., to allow a second process to DEQUEUE, not just observe), replace `SPSCRing<FleetDispatchSlot>` with a `MmioSpscRing<T>` class that uses `Read<T>(long)` / `Write<T>(long, ref T)` for all slot I/O and `ReadInt64`/`Write(long, long)` + `Thread.MemoryBarrier()` for cursors. Expect ~100-150 ns/op per side. Preserves the same blittable-slot + sideband refactor from this plan, so v28.0 is a stepping stone, not a dead end.

4. **`V12_002.Constants.cs` hygiene:** The orphaned `public const string Version = "Build 972";` at `Constants.cs:12` is not referenced by any Print statement (all live Print sites use `BUILD_TAG` from `V12_002.cs:44`). Step 14 bumps it for consistency, but the Director may prefer to delete the file entirely at a later time.

5. **Linting.csproj gap (non-blocking):** The hosted `Linting.csproj` at `<LangVersion>8.0</LangVersion>` allows C# 8 features in static analysis, but the actual NT8 runtime compiler is C# 7.3. This plan uses only C# 7.3-safe syntax to avoid a second category of false-positive pass in the lint environment. No Linting.csproj change is required for this plan to land.

6. **Round 28 benchmark provenance (D8 from legacy plan):** The `14.25 ns/op`, `8/8 AMAL`, and `5.28 ns/op sandbox` claims remain unverifiable from this repo (`scripts/amal_harness.py` is a dynamic iterator, not an 8-test battery). They are retained in this plan only as motivation context, not as acceptance criteria. This plan's acceptance criteria are the 10 Section 6 gates, all measured against the working tree.

7. **Question for the Director (non-blocking, resolvable in review):**
   Should the MMIO mirror be enabled **by default** (wired in Lifecycle.cs Step 8 as shown) or **opt-in** via a strategy property (`EnablePhotonMmioMirror = false` default)? The plan currently defaults to **enabled**, with graceful failure if MMF creation throws. Opt-in adds zero overhead when disabled but requires user action to turn on. Flagging for discussion; the plan can be amended in Step 8 without cascading changes elsewhere.

---

**End of Plan -- v28.0 MmioDispatchMirror Hybrid**
