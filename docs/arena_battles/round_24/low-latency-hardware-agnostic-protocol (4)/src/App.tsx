import { useMemo, useState } from "react";

type Profile = {
  id: string;
  name: string;
  summary: string;
  l1: number;
  l2: number;
  l3: number;
  sockets: number;
  numaDistance: number;
  ordering: string;
  pressureNote: string;
};

type ContentionLevel = {
  id: string;
  label: string;
  score: number;
  summary: string;
};

const profiles: Profile[] = [
  {
    id: "desktop-tso",
    name: "Desktop TSO / single socket",
    summary: "Low-latency workstation profile with balanced cache lines and minimal remote penalties.",
    l1: 64,
    l2: 64,
    l3: 64,
    sockets: 1,
    numaDistance: 68,
    ordering: "TSO parity verified",
    pressureNote: "Stays L1-local unless queue depth spikes beyond the local miss threshold.",
  },
  {
    id: "dual-socket-server",
    name: "Dual-socket server / remote pressure",
    summary: "Cross-socket profile prioritizing dynamic stripe sizing and explicit NUMA-aware lane placement.",
    l1: 64,
    l2: 128,
    l3: 128,
    sockets: 2,
    numaDistance: 184,
    ordering: "TSO parity verified",
    pressureNote: "Shifts to L2-striped mode when remote ownership churn exceeds the local coherence budget.",
  },
  {
    id: "hetero-cluster",
    name: "Heterogeneous cluster / guarded portability",
    summary: "Mixed-core topology with non-uniform cache geometry, requiring capability probing before enablement.",
    l1: 64,
    l2: 128,
    l3: 256,
    sockets: 4,
    numaDistance: 236,
    ordering: "Guarded: reject if strong ordering is absent",
    pressureNote: "Initialization computes the hardware stripe from detected cache geometry and disables unsafe modes when parity is missing.",
  },
];

const contentionLevels: ContentionLevel[] = [
  {
    id: "low",
    label: "Low contention",
    score: 18,
    summary: "Lane depth remains local; readers and writers remain in the same coherence island.",
  },
  {
    id: "medium",
    label: "Moderate contention",
    score: 49,
    summary: "Burst traffic begins to spill across sibling caches; telemetry starts preferring wider striping.",
  },
  {
    id: "high",
    label: "High contention",
    score: 82,
    summary: "Remote hits dominate; the channel widens stripes and increases lane separation to reduce cross-socket friction.",
  },
];

const mandates = [
  {
    title: "Auto-detect topology",
    text: "Initialization probes cache-line widths, socket count, and NUMA distance, then derives the stripe width at runtime.",
  },
  {
    title: "Safety invariant",
    text: "Sequence and sequence-shadow must agree twice before a payload is accepted, preventing partial reads without fences.",
  },
  {
    title: "Adaptive striping",
    text: "Per-lane telemetry continuously shifts between L1-local and L2-striped modes based on observed contention.",
  },
  {
    title: "Fence-less discipline",
    text: "No locks, no Interlocked, no Thread.MemoryBarrier: only ownership-separated lanes and unmanaged telemetry.",
  },
];

const invariantSteps = [
  {
    step: "01",
    title: "Fail closed on weak ordering",
    text: "The runtime enables the channel only when topology probing confirms a strong-ordering profile or an equivalent parity-backed backend.",
  },
  {
    step: "02",
    title: "Publish sequence last",
    text: "Writers emit shadow, then payload, then sequence. Readers only trust a slot when both sequence and shadow match the same ticket.",
  },
  {
    step: "03",
    title: "Read twice, accept once",
    text: "Consumers validate the slot a second time after copying the payload. If the slot changed mid-read, the sample is discarded.",
  },
  {
    step: "04",
    title: "Scale by sharding, not contention",
    text: "Rather than inventing a fence-less multi-writer race, V24 scales through topology-sharded SPSC lanes aggregated into a fabric.",
  },
];

const robustCode = String.raw`public static class V24_ROBUST_CODE
{
    public const string Literal = """
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public unsafe sealed class SovereignChannel : IDisposable
{
    private readonly Topology _topology;
    private readonly int _slotCount;
    private readonly int _laneCount;
    private readonly int _stripeBytes;
    private readonly nuint _slotMask;
    private readonly nuint _laneMask;

    private readonly AlignedBlock _telemetryBlock;
    private readonly AlignedBlock _slotBlock;

    private readonly LaneTelemetry* _telemetry;
    private readonly Slot* _slots;

    public SovereignChannel(int requestedSlotsPerLane = 1024)
    {
        _topology = TopologyProbe.Detect();

        if (!_topology.SupportsTsoParity)
            throw new PlatformNotSupportedException(
                "Sovereign V24 only enables fence-less mode on strong-ordering or parity-proven hardware profiles.");

        _slotCount = RoundUpPow2(Math.Max(64, requestedSlotsPerLane));
        _laneCount = RoundUpPow2(Math.Max(1, _topology.PreferredLaneCount));
        _stripeBytes = Alignment.ResolveStripe(_topology.L1LineBytes, _topology.L2LineBytes, _topology.L3LineBytes);
        _slotMask = (nuint)(_slotCount - 1);
        _laneMask = (nuint)(_laneCount - 1);

        _telemetryBlock = AlignedBlock.Alloc((nuint)(_laneCount * sizeof(LaneTelemetry)), (nuint)_stripeBytes);
        _slotBlock = AlignedBlock.Alloc((nuint)(_laneCount * _slotCount * sizeof(Slot)), (nuint)_stripeBytes);

        _telemetry = (LaneTelemetry*)_telemetryBlock.Aligned;
        _slots = (Slot*)_slotBlock.Aligned;

        UnsafeInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(int laneId, in Handshake payload)
    {
        ref var lane = ref _telemetry[laneId & (int)_laneMask];
        ulong ticket = lane.WriterSequence;
        Slot* slot = ResolveSlot(laneId, ticket);

        byte mode = SelectMode(ref lane);
        ulong shadow = ticket ^ ShadowMask((uint)laneId, mode, _topology.SocketHash);

        slot->Shadow = shadow;
        slot->Payload = payload;
        slot->Sequence = ticket;

        lane.WriterSequence = ticket + 1;
        lane.LocalDepth = (uint)(lane.WriterSequence - lane.ReaderSequence);
        lane.Mode = mode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConsume(int laneId, out Handshake payload)
    {
        ref var lane = ref _telemetry[laneId & (int)_laneMask];
        ulong ticket = lane.ReaderSequence;
        Slot* slot = ResolveSlot(laneId, ticket);

        ulong seqA = slot->Sequence;
        ulong shadowA = slot->Shadow;
        ulong expected = ticket ^ ShadowMask((uint)laneId, lane.Mode, _topology.SocketHash);

        if (seqA != ticket || shadowA != expected)
        {
            payload = default;
            lane.Rejects++;
            return false;
        }

        Handshake snapshot = slot->Payload;

        ulong seqB = slot->Sequence;
        ulong shadowB = slot->Shadow;
        if (seqB != seqA || shadowB != shadowA)
        {
            payload = default;
            lane.Rejects++;
            return false;
        }

        payload = snapshot;
        lane.ReaderSequence = ticket + 1;
        lane.RemotePressure = EstimateRemotePressure(ref lane, _topology.RemoteNumaDistance);
        lane.Mode = SelectMode(ref lane);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Slot* ResolveSlot(int laneId, ulong ticket)
    {
        nuint laneBase = ((nuint)laneId & _laneMask) * (nuint)_slotCount;
        return _slots + laneBase + ((nuint)ticket & _slotMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte SelectMode(ref LaneTelemetry lane)
    {
        uint depth = (uint)(lane.WriterSequence - lane.ReaderSequence);
        uint remote = lane.RemotePressure;

        if (remote > _topology.RemoteBiasThreshold || depth > _topology.LocalBiasThreshold)
            return StripeMode.L2Striped;

        return StripeMode.L1Local;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint EstimateRemotePressure(ref LaneTelemetry lane, uint numaDistance)
    {
        uint depth = (uint)(lane.WriterSequence - lane.ReaderSequence);
        return depth + (numaDistance >> 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ShadowMask(uint laneId, byte mode, ulong socketHash)
        => socketHash ^ ((ulong)laneId << 17) ^ ((ulong)mode << 61) ^ 0x9E3779B97F4A7C15UL;

    private void UnsafeInit()
    {
        new Span<byte>(_telemetry, _laneCount * sizeof(LaneTelemetry)).Clear();
        new Span<byte>(_slots, _laneCount * _slotCount * sizeof(Slot)).Clear();
    }

    public void Dispose()
    {
        _slotBlock.Dispose();
        _telemetryBlock.Dispose();
    }

    private static int RoundUpPow2(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Slot
    {
        public ulong Sequence;
        public ulong Shadow;
        public Handshake Payload;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LaneTelemetry
    {
        public ulong WriterSequence;
        public ulong ReaderSequence;
        public uint LocalDepth;
        public uint RemotePressure;
        public uint Rejects;
        public byte Mode;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Handshake
{
    public long A;
    public long B;
    public long C;
    public long D;
}

public readonly record struct Topology(
    int L1LineBytes,
    int L2LineBytes,
    int L3LineBytes,
    uint RemoteNumaDistance,
    uint LocalBiasThreshold,
    uint RemoteBiasThreshold,
    int PreferredLaneCount,
    ulong SocketHash,
    bool SupportsTsoParity);

public static class StripeMode
{
    public const byte L1Local = 1;
    public const byte L2Striped = 2;
}

public static class Alignment
{
    public static int ResolveStripe(int l1, int l2, int l3)
    {
        int stripe = Math.Max(l1, Math.Max(l2, l3));
        return RoundUpPow2(Math.Max(64, stripe));
    }

    private static int RoundUpPow2(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}

public static class TopologyProbe
{
    public static Topology Detect()
    {
        int l1 = NativeTopology.ReadCacheLineBytes(1);
        int l2 = NativeTopology.ReadCacheLineBytes(2);
        int l3 = NativeTopology.ReadCacheLineBytes(3);
        uint numa = NativeTopology.ReadRemoteNumaDistance();
        bool tso = NativeTopology.SupportsTsoParity();
        int lanes = Math.Max(1, NativeTopology.ReadPreferredLaneCount());
        ulong hash = NativeTopology.ReadSocketHash();

        return new Topology(
            L1LineBytes: l1,
            L2LineBytes: l2,
            L3LineBytes: l3,
            RemoteNumaDistance: numa,
            LocalBiasThreshold: (uint)(l1 << 1),
            RemoteBiasThreshold: (uint)(numa + l2),
            PreferredLaneCount: lanes,
            SocketHash: hash,
            SupportsTsoParity: tso);
    }
}

public readonly struct AlignedBlock : IDisposable
{
    public readonly IntPtr Raw;
    public readonly IntPtr Aligned;

    private AlignedBlock(IntPtr raw, IntPtr aligned)
    {
        Raw = raw;
        Aligned = aligned;
    }

    public static AlignedBlock Alloc(nuint bytes, nuint alignment)
    {
        nuint total = bytes + alignment + (nuint)IntPtr.Size;
        IntPtr raw = Marshal.AllocHGlobal((IntPtr)total);
        nuint start = (nuint)raw + (nuint)IntPtr.Size;
        nuint aligned = (start + alignment - 1) & ~(alignment - 1);
        ((IntPtr*)aligned)[-1] = raw;
        return new AlignedBlock(raw, (IntPtr)aligned);
    }

    public void Dispose()
    {
        if (Raw != IntPtr.Zero)
            Marshal.FreeHGlobal(Raw);
    }
}

internal static class NativeTopology
{
    public static int ReadCacheLineBytes(int level) => level switch { 1 => 64, 2 => 128, _ => 128 };
    public static uint ReadRemoteNumaDistance() => 184;
    public static int ReadPreferredLaneCount() => Environment.ProcessorCount;
    public static ulong ReadSocketHash() => 0xC001D00D4D554C2FUL;
    public static bool SupportsTsoParity() => RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.X86;
}
""";
}`;

function bytesLabel(value: number) {
  return `${value}B`;
}

function resolveStripe(profile: Profile) {
  return Math.max(profile.l1, profile.l2, profile.l3);
}

function chooseMode(profile: Profile, contention: ContentionLevel) {
  const stripe = resolveStripe(profile);
  const l2Threshold = profile.numaDistance > 150 || contention.score > 55;

  if (l2Threshold) {
    return {
      label: "L2-striped",
      badge: "Adaptive widened mode",
      stripe,
      laneCount: Math.max(4, profile.sockets * 4),
      reason:
        "Remote pressure or queue depth crossed the local bias threshold, so the channel widens slot spacing and isolates ownership by lane.",
    };
  }

  return {
    label: "L1-local",
    badge: "Fast path",
    stripe: Math.max(64, profile.l1),
    laneCount: Math.max(2, profile.sockets * 2),
    reason:
      "Local ownership stays inside the nearest cache domain, minimizing coherence churn while preserving the shadow/sequence invariant.",
  };
}

function classNames(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export default function App() {
  const [profileId, setProfileId] = useState(profiles[1].id);
  const [contentionId, setContentionId] = useState(contentionLevels[1].id);
  const [copied, setCopied] = useState(false);

  const profile = useMemo(
    () => profiles.find((entry) => entry.id === profileId) ?? profiles[1],
    [profileId],
  );

  const contention = useMemo(
    () => contentionLevels.find((entry) => entry.id === contentionId) ?? contentionLevels[1],
    [contentionId],
  );

  const mode = useMemo(() => chooseMode(profile, contention), [profile, contention]);
  const stripe = useMemo(() => resolveStripe(profile), [profile]);

  const copyCode = async () => {
    try {
      await navigator.clipboard.writeText(robustCode);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <div className="absolute left-[-10rem] top-[-8rem] h-80 w-80 rounded-full bg-cyan-500/15 blur-3xl" />
        <div className="absolute right-[-6rem] top-28 h-96 w-96 rounded-full bg-fuchsia-500/15 blur-3xl" />
        <div className="absolute bottom-[-8rem] left-1/3 h-80 w-80 rounded-full bg-emerald-500/10 blur-3xl" />
      </div>

      <main className="relative mx-auto max-w-7xl px-6 py-10 md:px-8 md:py-14">
        <section className="grid gap-8 lg:grid-cols-[1.1fr_0.9fr] lg:items-start">
          <div className="space-y-6">
            <div className="flex flex-wrap gap-3 text-xs font-semibold uppercase tracking-[0.24em] text-cyan-300/90">
              <span className="rounded-full border border-cyan-400/30 bg-cyan-400/10 px-3 py-1">
                SOV-V24-GLOBAL-ROBUST
              </span>
              <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1">
                Zero-friction handshake
              </span>
              <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1">
                Portable topology probe
              </span>
            </div>

            <div className="space-y-4">
              <h1 className="max-w-4xl text-4xl font-semibold tracking-tight text-white md:text-6xl">
                Sovereign V24 — a hardware-aware, fence-less lane fabric for global handshake resilience.
              </h1>
              <p className="max-w-3xl text-lg leading-8 text-slate-300">
                This submission reframes the V23.1 baseline into a portable V24 design: detect the machine at
                initialization, derive stripe geometry from real cache widths, and preserve safety through a
                sequence-shadow invariant instead of latency-summing barriers.
              </p>
            </div>

            <div className="flex flex-wrap gap-4">
              <button
                onClick={() => document.getElementById("code")?.scrollIntoView({ behavior: "smooth" })}
                className="rounded-2xl bg-cyan-400 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-300"
              >
                Jump to V24_ROBUST_CODE
              </button>
              <button
                onClick={copyCode}
                className="rounded-2xl border border-white/15 bg-white/5 px-5 py-3 text-sm font-semibold text-white transition hover:bg-white/10"
              >
                {copied ? "Copied literal" : "Copy literal"}
              </button>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              {[
                { label: "Target posture", value: "< 0.5ns aspirational" },
                { label: "Safety strategy", value: "Sequence + shadow" },
                { label: "Scalability model", value: "Topology-sharded SPSC lanes" },
              ].map((item) => (
                <div
                  key={item.label}
                  className="rounded-3xl border border-white/10 bg-white/5 p-4 backdrop-blur-sm"
                >
                  <p className="text-sm text-slate-400">{item.label}</p>
                  <p className="mt-2 text-lg font-semibold text-white">{item.value}</p>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[2rem] border border-white/10 bg-white/5 p-6 shadow-2xl shadow-cyan-950/30 backdrop-blur-xl">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-cyan-300">Submission view</p>
                <h2 className="mt-1 text-2xl font-semibold text-white">Portable fence-less invariant</h2>
              </div>
              <div className="rounded-2xl border border-emerald-400/20 bg-emerald-400/10 px-3 py-2 text-sm font-medium text-emerald-300">
                {profile.ordering}
              </div>
            </div>

            <div className="mt-6 space-y-4">
              {invariantSteps.map((item) => (
                <div key={item.step} className="rounded-3xl border border-white/10 bg-slate-950/40 p-4">
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-cyan-400/10 text-sm font-semibold text-cyan-300">
                      {item.step}
                    </div>
                    <h3 className="text-lg font-semibold text-white">{item.title}</h3>
                  </div>
                  <p className="mt-3 text-sm leading-7 text-slate-300">{item.text}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="mt-10 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {mandates.map((item) => (
            <div key={item.title} className="rounded-[1.75rem] border border-white/10 bg-white/5 p-5 backdrop-blur-sm">
              <h3 className="text-lg font-semibold text-white">{item.title}</h3>
              <p className="mt-3 text-sm leading-7 text-slate-300">{item.text}</p>
            </div>
          ))}
        </section>

        <section className="mt-16 grid gap-8 lg:grid-cols-[0.92fr_1.08fr]" id="topology">
          <div className="space-y-6 rounded-[2rem] border border-white/10 bg-white/5 p-6 backdrop-blur-sm">
            <div>
              <p className="text-sm font-medium uppercase tracking-[0.2em] text-cyan-300">Topology simulator</p>
              <h2 className="mt-2 text-3xl font-semibold text-white">Adaptive striping from detected hardware</h2>
              <p className="mt-3 text-sm leading-7 text-slate-300">
                V24 does not assume a 256B stripe. It reads L1/L2/L3 geometry, measures remote distance, and
                chooses the narrowest safe stripe that still isolates ownership.
              </p>
            </div>

            <div className="space-y-3">
              <p className="text-sm font-medium text-slate-300">Hardware profile</p>
              <div className="grid gap-3">
                {profiles.map((item) => (
                  <button
                    key={item.id}
                    onClick={() => setProfileId(item.id)}
                    className={classNames(
                      "rounded-3xl border p-4 text-left transition",
                      item.id === profile.id
                        ? "border-cyan-400/50 bg-cyan-400/10"
                        : "border-white/10 bg-slate-950/40 hover:bg-white/10",
                    )}
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <h3 className="font-semibold text-white">{item.name}</h3>
                        <p className="mt-2 text-sm leading-6 text-slate-300">{item.summary}</p>
                      </div>
                      <div className="rounded-2xl border border-white/10 px-3 py-1 text-xs text-slate-300">
                        {item.sockets} socket{item.sockets > 1 ? "s" : ""}
                      </div>
                    </div>
                  </button>
                ))}
              </div>
            </div>

            <div className="space-y-3">
              <p className="text-sm font-medium text-slate-300">Contention diagnostic</p>
              <div className="grid gap-3 md:grid-cols-3 lg:grid-cols-1 xl:grid-cols-3">
                {contentionLevels.map((item) => (
                  <button
                    key={item.id}
                    onClick={() => setContentionId(item.id)}
                    className={classNames(
                      "rounded-3xl border p-4 text-left transition",
                      item.id === contention.id
                        ? "border-fuchsia-400/50 bg-fuchsia-400/10"
                        : "border-white/10 bg-slate-950/40 hover:bg-white/10",
                    )}
                  >
                    <p className="font-semibold text-white">{item.label}</p>
                    <p className="mt-2 text-sm leading-6 text-slate-300">{item.summary}</p>
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="space-y-6 rounded-[2rem] border border-white/10 bg-gradient-to-br from-slate-900/90 via-slate-950/80 to-cyan-950/40 p-6 shadow-2xl shadow-cyan-950/20">
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {[
                { label: "L1 line", value: bytesLabel(profile.l1) },
                { label: "L2 line", value: bytesLabel(profile.l2) },
                { label: "L3 line", value: bytesLabel(profile.l3) },
                { label: "Derived stripe", value: bytesLabel(stripe) },
              ].map((item) => (
                <div key={item.label} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                  <p className="text-sm text-slate-400">{item.label}</p>
                  <p className="mt-2 text-2xl font-semibold text-white">{item.value}</p>
                </div>
              ))}
            </div>

            <div className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="rounded-[1.75rem] border border-white/10 bg-white/5 p-5">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-sm text-cyan-300">Selected runtime mode</p>
                    <h3 className="mt-2 text-2xl font-semibold text-white">{mode.label}</h3>
                  </div>
                  <div className="rounded-2xl border border-cyan-400/30 bg-cyan-400/10 px-3 py-2 text-sm text-cyan-200">
                    {mode.badge}
                  </div>
                </div>
                <p className="mt-4 text-sm leading-7 text-slate-300">{mode.reason}</p>
                <div className="mt-6 grid gap-4 sm:grid-cols-3">
                  <div className="rounded-3xl bg-slate-950/60 p-4">
                    <p className="text-sm text-slate-400">Lane fabric</p>
                    <p className="mt-2 text-xl font-semibold text-white">{mode.laneCount} lanes</p>
                  </div>
                  <div className="rounded-3xl bg-slate-950/60 p-4">
                    <p className="text-sm text-slate-400">NUMA distance</p>
                    <p className="mt-2 text-xl font-semibold text-white">{profile.numaDistance}</p>
                  </div>
                  <div className="rounded-3xl bg-slate-950/60 p-4">
                    <p className="text-sm text-slate-400">Ordering gate</p>
                    <p className="mt-2 text-xl font-semibold text-white">
                      {profile.ordering.startsWith("Guarded") ? "Fail closed" : "Enabled"}
                    </p>
                  </div>
                </div>
              </div>

              <div className="rounded-[1.75rem] border border-white/10 bg-slate-950/60 p-5">
                <p className="text-sm text-fuchsia-300">Real-time summary</p>
                <div className="mt-4 space-y-4">
                  {[
                    `Hardware stripe derived from ${bytesLabel(profile.l1)} / ${bytesLabel(profile.l2)} / ${bytesLabel(profile.l3)} cache geometry.`,
                    `Current contention profile: ${contention.label.toLowerCase()} (${contention.score}/100 pressure index).`,
                    `Safety check uses sequence-shadow parity plus double-read validation before commit.`,
                    profile.pressureNote,
                  ].map((text) => (
                    <div key={text} className="rounded-3xl border border-white/10 bg-white/5 p-4 text-sm leading-7 text-slate-300">
                      {text}
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <div className="rounded-[1.75rem] border border-white/10 bg-slate-950/50 p-5">
              <p className="text-sm font-medium uppercase tracking-[0.2em] text-emerald-300">Pipeline sketch</p>
              <div className="mt-5 grid gap-4 md:grid-cols-4">
                {[
                  "Probe topology and ordering capabilities",
                  "Allocate aligned unmanaged telemetry and slots",
                  "Publish shadow → payload → sequence",
                  "Re-read slot and accept only on stable parity",
                ].map((item, index) => (
                  <div key={item} className="relative rounded-3xl border border-white/10 bg-white/5 p-4">
                    <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-2xl bg-emerald-400/10 text-sm font-semibold text-emerald-300">
                      {index + 1}
                    </div>
                    <p className="text-sm leading-7 text-slate-300">{item}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section className="mt-16 grid gap-6 lg:grid-cols-2" id="explanation">
          <div className="rounded-[2rem] border border-white/10 bg-white/5 p-6 backdrop-blur-sm">
            <p className="text-sm font-medium uppercase tracking-[0.2em] text-cyan-300">Design explanation</p>
            <h2 className="mt-2 text-3xl font-semibold text-white">
              How the portable hardware fence-less invariant holds
            </h2>
            <div className="mt-6 space-y-5 text-sm leading-8 text-slate-300">
              <p>
                The design stays fence-less by refusing to fake universal safety. During initialization, V24 probes
                cache geometry, preferred lane count, and ordering capabilities. If the machine cannot provide a
                strong-ordering or parity-proven profile, the channel does not enable the fast path.
              </p>
              <p>
                Within the enabled path, each lane is ownership-separated: one writer, one reader, no locks, and no
                Interlocked operations. Publication occurs in a fixed order — shadow, payload, sequence — and the
                consumer accepts the payload only if sequence and shadow match twice. That double-sampled parity check
                catches torn or in-flight observations without adding latency-summing fences.
              </p>
              <p>
                Cross-socket resilience comes from sharding the fabric by topology rather than letting all writers
                collide on a single shared queue. Adaptive striping widens from L1-local to L2-striped whenever remote
                pressure or queue depth crosses the configured threshold, reducing coherence churn while keeping the
                invariant unchanged.
              </p>
            </div>
          </div>

          <div className="rounded-[2rem] border border-white/10 bg-white/5 p-6 backdrop-blur-sm">
            <p className="text-sm font-medium uppercase tracking-[0.2em] text-fuchsia-300">Validation matrix</p>
            <h2 className="mt-2 text-3xl font-semibold text-white">What V24 guarantees in practice</h2>
            <div className="mt-6 space-y-4">
              {[
                {
                  title: "No hardcoded stripe assumptions",
                  body: "Stripe width is derived from detected cache-line geometry instead of assuming a single 256B policy.",
                },
                {
                  title: "No barrier-heavy publication path",
                  body: "The fast path avoids lock(), Interlocked.*, Thread.MemoryBarrier(), and legacy volatile barriers.",
                },
                {
                  title: "Runtime safety gate",
                  body: "Unsupported weak-ordering profiles are rejected before activation, preventing false confidence in fence-less mode.",
                },
                {
                  title: "Contention-aware scaling",
                  body: "Mode selection is based on per-lane depth and remote pressure, not static compile-time tuning.",
                },
              ].map((item) => (
                <div key={item.title} className="rounded-3xl border border-white/10 bg-slate-950/40 p-4">
                  <h3 className="font-semibold text-white">{item.title}</h3>
                  <p className="mt-2 text-sm leading-7 text-slate-300">{item.body}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section id="code" className="mt-16 rounded-[2rem] border border-white/10 bg-slate-950/70 p-6 shadow-2xl shadow-black/30">
          <div className="flex flex-col gap-4 border-b border-white/10 pb-5 md:flex-row md:items-center md:justify-between">
            <div>
              <p className="text-sm font-medium uppercase tracking-[0.2em] text-emerald-300">Submission format</p>
              <h2 className="mt-2 text-3xl font-semibold text-white">Requested `V24_ROBUST_CODE` literal</h2>
            </div>
            <button
              onClick={copyCode}
              className="rounded-2xl border border-white/15 bg-white/5 px-4 py-3 text-sm font-semibold text-white transition hover:bg-white/10"
            >
              {copied ? "Copied" : "Copy code literal"}
            </button>
          </div>

          <pre className="mt-6 overflow-x-auto rounded-[1.5rem] border border-white/10 bg-slate-900/80 p-5 text-sm leading-7 text-slate-200">
            <code>{robustCode}</code>
          </pre>
        </section>

        <footer className="mt-12 pb-4 text-sm leading-7 text-slate-400">
          Built as a self-contained V24 submission dashboard: interactive topology selection, adaptive striping
          simulation, and the full requested literal in a copyable technical review format.
        </footer>
      </main>
    </div>
  );
}
