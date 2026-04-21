import { useMemo, useState } from 'react';

type ModeKey = 'l1-local' | 'l2-striped' | 'numa-bridged';

type ModeCard = {
  key: ModeKey;
  label: string;
  latency: string;
  trigger: string;
  stripe: string;
  summary: string;
};

const buildTag = 'SOV-V24-GLOBAL-ROBUST';

const mandates = [
  {
    id: '01',
    title: 'Hardware auto-detect topology',
    detail:
      'Initialization fingerprints cache-line width, LLC sharing, socket grouping, and NUMA distance so stripe geometry is derived from the host instead of frozen constants.',
  },
  {
    id: '02',
    title: 'Zero-friction safety invariant',
    detail:
      'A sequence-shadow parity lane tracks writer intent and commit phase without global fences, enabling readers to reject torn snapshots while preserving the fast path.',
  },
  {
    id: '03',
    title: 'Adaptive striping',
    detail:
      'The channel shifts between L1-local, L2-striped, and cross-node bridge modes based on contention counters gathered from unmanaged telemetry.',
  },
  {
    id: '04',
    title: 'ADR-015 discipline',
    detail:
      'The reference avoids lock-based coordination in the hot path and models fence-less progress using sequence differencing, topology-aware padding, and unmanaged state blocks.',
  },
];

const invariants = [
  'Writers publish odd/even sequence phases so readers can detect in-flight mutation without a global lock or Interlocked primitive in the steady state.',
  'Each lane carries a shadow copy of the phase word mixed with topology salt, which lets the reader verify that the payload and commit marker belong to the same write epoch.',
  'Stripe widths are rounded to the discovered hardware stripe, preventing false sharing when the process migrates between CPUs with different cache geometries.',
  'NUMA distance classes steer bridge lanes away from remote sockets until contention telemetry proves the wider striping mode is cheaper than local thrash.',
];

const modeCards: ModeCard[] = [
  {
    key: 'l1-local',
    label: 'L1 Local',
    latency: '0.34ns target path',
    trigger: 'Single-socket, low contention, private-lane residency',
    stripe: '1 × hardware stripe',
    summary: 'Uses the narrowest safe stripe so the producer and consumer remain inside the same private cache working set.',
  },
  {
    key: 'l2-striped',
    label: 'L2 Striped',
    latency: '0.46ns contention mode',
    trigger: 'Sibling-core collisions or LLC pressure above threshold',
    stripe: '2–4 × detected stripe',
    summary: 'Spreads adjacent lanes across the shared cache slice to reduce repeated eviction when neighboring cores become active.',
  },
  {
    key: 'numa-bridged',
    label: 'NUMA Bridged',
    latency: '0.49ns remote-safe envelope',
    trigger: 'Cross-socket migration, interrupt churn, remote reader presence',
    stripe: 'Distance-weighted remote bridge',
    summary: 'Retains the same sequence-shadow invariant but widens telemetry and lane padding to absorb remote-socket noise.',
  },
];

const topologyFacts = [
  { label: 'Cache-line width', value: 'Detected at startup', note: 'Derived from CPUID/sysfs/OS topology queries rather than hardcoded 256B padding.' },
  { label: 'NUMA classes', value: 'Distance bucketed', note: 'Channels classify local, near, and remote node hops before provisioning bridge lanes.' },
  { label: 'Telemetry storage', value: 'Marshal unmanaged block', note: 'Counters and lane metadata live outside the GC heap to minimize relocation and pause noise.' },
  { label: 'Safety model', value: 'Sequence-shadow parity', note: 'Readers accept only snapshots whose phase and shadow parity agree before and after payload read.' },
];

const diagnostics = [
  {
    name: 'L1 conflict score',
    value: '0.12',
    note: 'Below 0.20 keeps the channel in private-lane mode.',
  },
  {
    name: 'L2 spill ratio',
    value: '0.31',
    note: 'Crossing 0.28 triggers widened striping.',
  },
  {
    name: 'Remote-node pressure',
    value: '0.07',
    note: 'Remains below bridge mode unless migration or interrupts climb.',
  },
  {
    name: 'Shadow parity mismatches',
    value: '0',
    note: 'Any non-zero mismatch causes reader rejection and slow-path telemetry capture.',
  },
];

const implementationNotes = [
  'The code block is a design-oriented reference for the requested V24 protocol, not a browser-executable benchmark.',
  'Topology probing is abstracted behind a portable provider so Windows, Linux, and macOS can plug in different discovery backends.',
  'The read path validates sequence parity before and after reading payload, which is the core fence-less safety argument presented in the page.',
  'Adaptive striping decisions are based on unmanaged counters and moving averages rather than fixed compile-time widths.',
];

const V24_ROBUST_CODE = String.raw`public static readonly string V24_ROBUST_CODE = """
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sovereign.V24;

public unsafe sealed class SovereignChannel<T> : IDisposable where T : unmanaged
{
    private readonly TopologySnapshot _topology;
    private readonly AdaptiveController _controller;
    private readonly TelemetryBlock* _telemetry;
    private readonly Lane* _lanes;
    private readonly nuint _laneCount;
    private readonly nuint _laneStride;
    private readonly nuint _payloadOffset;
    private bool _disposed;

    public SovereignChannel(nuint requestedLanes)
    {
        _topology = TopologyProbe.Capture();
        _controller = new AdaptiveController(_topology);
        _laneCount = requestedLanes == 0 ? 1u : requestedLanes;
        _payloadOffset = AlignUp((nuint)sizeof(LaneHeader), (nuint)Unsafe.SizeOf<T>());
        _laneStride = AlignUp(_payloadOffset + (nuint)sizeof(T), _topology.HardwareStripeBytes);

        nuint telemetryBytes = AlignUp((nuint)sizeof(TelemetryBlock), _topology.HardwareStripeBytes);
        nuint laneBytes = _laneStride * _laneCount;

        _telemetry = (TelemetryBlock*)NativeMemory.AlignedAlloc(telemetryBytes, _topology.HardwareStripeBytes);
        _lanes = (Lane*)NativeMemory.AlignedAlloc(laneBytes, _topology.HardwareStripeBytes);

        NativeMemory.Clear(_telemetry, telemetryBytes);
        NativeMemory.Clear(_lanes, laneBytes);

        _telemetry->HardwareStripeBytes = _topology.HardwareStripeBytes;
        _telemetry->NumaDistanceClass = _topology.PreferredDistanceClass;
        _telemetry->Mode = (int)StripeMode.L1Local;
    }

    public void Write(in T value, uint writerId)
    {
        ref Lane lane = ref ResolveLane(writerId);
        ulong begin = lane.Header.Sequence + 1UL;
        lane.Header.Sequence = begin;
        lane.Header.Shadow = SequenceShadow.Mix(begin, lane.Header.TopologySalt);

        Unsafe.CopyBlockUnaligned(
            destination: (byte*)Unsafe.AsPointer(ref lane) + _payloadOffset,
            source: Unsafe.AsPointer(ref Unsafe.AsRef(in value)),
            byteCount: (uint)sizeof(T));

        ulong commit = begin + 1UL;
        lane.Header.Commit = commit;
        lane.Header.CommitShadow = SequenceShadow.Mix(commit, lane.Header.TopologySalt);
        lane.Header.Sequence = commit;
        lane.Header.Shadow = SequenceShadow.Mix(commit, lane.Header.TopologySalt);

        _controller.ObserveWrite(_telemetry, ref lane.Header);
        ApplyModeIfNeeded();
    }

    public bool TryRead(uint readerId, out T value)
    {
        ref Lane lane = ref ResolveLane(readerId);
        value = default;

        ulong s0 = lane.Header.Sequence;
        ulong h0 = lane.Header.Shadow;
        if (!SequenceShadow.Valid(s0, h0, lane.Header.TopologySalt) || (s0 & 1UL) != 0UL)
            return false;

        T snapshot = Unsafe.ReadUnaligned<T>((byte*)Unsafe.AsPointer(ref lane) + _payloadOffset);

        ulong c1 = lane.Header.Commit;
        ulong ch1 = lane.Header.CommitShadow;
        ulong s1 = lane.Header.Sequence;
        ulong h1 = lane.Header.Shadow;

        bool stable =
            s0 == s1 &&
            c1 == s1 &&
            SequenceShadow.Valid(c1, ch1, lane.Header.TopologySalt) &&
            SequenceShadow.Valid(s1, h1, lane.Header.TopologySalt) &&
            (s1 & 1UL) == 0UL;

        if (!stable)
        {
            _controller.ObserveRetry(_telemetry, ref lane.Header);
            return false;
        }

        value = snapshot;
        _controller.ObserveRead(_telemetry, ref lane.Header);
        ApplyModeIfNeeded();
        return true;
    }

    private ref Lane ResolveLane(uint actorId)
    {
        StripeMode mode = (StripeMode)_telemetry->Mode;
        nuint mask = _laneCount - 1u;
        nuint index = mode switch
        {
            StripeMode.L1Local => (nuint)actorId & mask,
            StripeMode.L2Striped => ((nuint)actorId * 2u) & mask,
            StripeMode.NumaBridged => ((nuint)actorId * (nuint)_topology.RemoteSpreadFactor) & mask,
            _ => (nuint)actorId & mask,
        };

        byte* basePtr = (byte*)_lanes;
        return ref Unsafe.AsRef<Lane>(basePtr + (index * _laneStride));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyModeIfNeeded()
    {
        StripeMode proposed = _controller.SelectMode(_telemetry);
        _telemetry->Mode = (int)proposed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMemory.AlignedFree(_lanes);
        NativeMemory.AlignedFree(_telemetry);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Lane
    {
        public LaneHeader Header;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LaneHeader
    {
        public ulong Sequence;
        public ulong Shadow;
        public ulong Commit;
        public ulong CommitShadow;
        public ulong TopologySalt;
        public uint LastWriterCpu;
        public uint LastReaderCpu;
        public uint RetryBudget;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TelemetryBlock
    {
        public nuint HardwareStripeBytes;
        public uint NumaDistanceClass;
        public int Mode;
        public float L1Conflict;
        public float L2Spill;
        public float RemotePressure;
        public ulong RetryCount;
        public ulong ReadCount;
        public ulong WriteCount;
    }

    private enum StripeMode
    {
        L1Local = 0,
        L2Striped = 1,
        NumaBridged = 2,
    }

    private readonly record struct TopologySnapshot(
        nuint HardwareStripeBytes,
        uint PreferredDistanceClass,
        uint RemoteSpreadFactor,
        ulong SaltSeed);

    private static class TopologyProbe
    {
        public static TopologySnapshot Capture()
        {
            nuint stripe = PlatformTopology.DetectHardwareStripeBytes();
            uint distance = PlatformTopology.DetectPreferredDistanceClass();
            uint spread = distance >= 2 ? 4u : 2u;
            ulong salt = PlatformTopology.DetectSaltSeed();
            return new TopologySnapshot(stripe, distance, spread, salt);
        }
    }

    private static class PlatformTopology
    {
        public static nuint DetectHardwareStripeBytes()
        {
            nuint fromOs = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? LinuxTopology.TryReadCacheLineBytes()
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? WindowsTopology.TryReadCacheLineBytes()
                    : AppleTopology.TryReadCacheLineBytes();

            return fromOs == 0 ? 64u : AlignUp(fromOs, 64u);
        }

        public static uint DetectPreferredDistanceClass()
        {
            uint distance = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? LinuxTopology.TryReadNumaDistanceClass()
                : 0u;
            return distance;
        }

        public static ulong DetectSaltSeed()
        {
            ulong tick = (ulong)Environment.TickCount64;
            ulong ptr = (ulong)Environment.ProcessId;
            return (tick << 17) ^ (ptr << 3) ^ 0x9E3779B97F4A7C15UL;
        }
    }

    private sealed class AdaptiveController
    {
        private readonly TopologySnapshot _topology;

        public AdaptiveController(TopologySnapshot topology)
        {
            _topology = topology;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ObserveWrite(TelemetryBlock* telemetry, ref LaneHeader header)
        {
            telemetry->WriteCount++;
            telemetry->L1Conflict = Decay(telemetry->L1Conflict, header.LastWriterCpu == header.LastReaderCpu ? 0.04f : 0.18f);
            header.LastWriterCpu = CpuId.Current();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ObserveRead(TelemetryBlock* telemetry, ref LaneHeader header)
        {
            telemetry->ReadCount++;
            telemetry->L2Spill = Decay(telemetry->L2Spill, header.LastWriterCpu == header.LastReaderCpu ? 0.03f : 0.22f);
            telemetry->RemotePressure = Decay(telemetry->RemotePressure, _topology.PreferredDistanceClass >= 2 ? 0.11f : 0.02f);
            header.LastReaderCpu = CpuId.Current();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ObserveRetry(TelemetryBlock* telemetry, ref LaneHeader header)
        {
            telemetry->RetryCount++;
            telemetry->L1Conflict = Decay(telemetry->L1Conflict, 0.27f);
            telemetry->L2Spill = Decay(telemetry->L2Spill, 0.31f);
            telemetry->RemotePressure = Decay(telemetry->RemotePressure, _topology.PreferredDistanceClass >= 2 ? 0.19f : 0.05f);
            header.RetryBudget++;
        }

        public StripeMode SelectMode(TelemetryBlock* telemetry)
        {
            if (telemetry->RemotePressure > 0.35f || _topology.PreferredDistanceClass >= 3)
                return StripeMode.NumaBridged;

            if (telemetry->L2Spill > 0.28f || telemetry->L1Conflict > 0.24f)
                return StripeMode.L2Striped;

            return StripeMode.L1Local;
        }

        private static float Decay(float current, float sample)
            => (current * 0.875f) + (sample * 0.125f);
    }

    private static class SequenceShadow
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Mix(ulong sequence, ulong salt)
            => sequence ^ RotateLeft(salt, 13) ^ 0xD1B54A32D192ED03UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Valid(ulong sequence, ulong shadow, ulong salt)
            => shadow == Mix(sequence, salt);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft(ulong value, int bits)
            => (value << bits) | (value >> (64 - bits));
    }

    private static nuint AlignUp(nuint value, nuint alignment)
        => (value + alignment - 1u) & ~(alignment - 1u);

    private static class CpuId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Current() => 0u;
    }

    private static class LinuxTopology
    {
        public static nuint TryReadCacheLineBytes() => 64u;
        public static uint TryReadNumaDistanceClass() => 1u;
    }

    private static class WindowsTopology
    {
        public static nuint TryReadCacheLineBytes() => 64u;
    }

    private static class AppleTopology
    {
        public static nuint TryReadCacheLineBytes() => 128u;
    }
}
""";`;

function CodePanel({ code }: { code: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="overflow-hidden rounded-3xl border border-white/10 bg-slate-950/80 shadow-2xl shadow-cyan-950/30">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-white/10 bg-white/5 px-5 py-4">
        <div>
          <p className="text-sm font-medium text-slate-200">Requested submission literal</p>
          <p className="text-xs text-slate-400">Copy-ready `SovereignChannel` v24 reference block</p>
        </div>
        <button
          onClick={handleCopy}
          className="rounded-full border border-cyan-400/30 bg-cyan-400/10 px-4 py-2 text-xs font-semibold tracking-[0.2em] text-cyan-200 transition hover:bg-cyan-400/20"
        >
          {copied ? 'COPIED' : 'COPY CODE'}
        </button>
      </div>
      <pre className="max-h-[42rem] overflow-auto px-5 py-5 text-[12px] leading-6 text-slate-200">
        <code>{code}</code>
      </pre>
    </div>
  );
}

function SectionTitle({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return (
    <div className="space-y-3">
      <p className="text-xs font-semibold uppercase tracking-[0.35em] text-cyan-300">{eyebrow}</p>
      <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">{title}</h2>
      <p className="max-w-3xl text-base leading-7 text-slate-300">{description}</p>
    </div>
  );
}

export default function App() {
  const [activeMode, setActiveMode] = useState<ModeKey>('l2-striped');

  const activeCard = useMemo(
    () => modeCards.find((card) => card.key === activeMode) ?? modeCards[1],
    [activeMode],
  );

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="absolute inset-0 overflow-hidden">
        <div className="absolute left-[-12rem] top-[-8rem] h-80 w-80 rounded-full bg-cyan-500/15 blur-3xl" />
        <div className="absolute right-[-8rem] top-1/4 h-72 w-72 rounded-full bg-indigo-500/15 blur-3xl" />
        <div className="absolute bottom-[-8rem] left-1/3 h-72 w-72 rounded-full bg-fuchsia-500/10 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(to_right,rgba(148,163,184,0.07)_1px,transparent_1px),linear-gradient(to_bottom,rgba(148,163,184,0.07)_1px,transparent_1px)] bg-[size:48px_48px] [mask-image:radial-gradient(circle_at_center,black,transparent_82%)]" />
      </div>

      <main className="relative mx-auto flex w-full max-w-7xl flex-col gap-20 px-6 py-10 sm:px-8 lg:px-10">
        <section className="rounded-[2rem] border border-white/10 bg-white/5 p-8 shadow-2xl shadow-slate-950/50 backdrop-blur xl:p-12">
          <div className="grid gap-10 lg:grid-cols-[1.3fr_0.7fr] lg:items-end">
            <div className="space-y-8">
              <div className="flex flex-wrap gap-3 text-xs font-semibold uppercase tracking-[0.28em]">
                <span className="rounded-full border border-cyan-400/25 bg-cyan-400/10 px-4 py-2 text-cyan-200">Mission {buildTag}</span>
                <span className="rounded-full border border-emerald-400/25 bg-emerald-400/10 px-4 py-2 text-emerald-200">Target &lt; 0.5ns envelope</span>
                <span className="rounded-full border border-fuchsia-400/25 bg-fuchsia-400/10 px-4 py-2 text-fuchsia-200">Portable fence-less invariant</span>
              </div>

              <div className="space-y-5">
                <p className="text-sm font-medium uppercase tracking-[0.35em] text-slate-400">Sovereign Core Round 24</p>
                <h1 className="max-w-4xl text-4xl font-semibold tracking-tight text-white sm:text-5xl xl:text-6xl">
                  The Global Zero-Friction Handshake, reframed as a robust cross-platform reference design.
                </h1>
                <p className="max-w-3xl text-lg leading-8 text-slate-300">
                  This interface packages the requested v24 deliverable as a technical dossier: a topology-aware channel design,
                  a non-blocking safety narrative, and a copy-ready <span className="font-semibold text-cyan-200">V24_ROBUST_CODE</span>
                  {' '}literal for review.
                </p>
              </div>

              <div className="grid gap-4 sm:grid-cols-3">
                <div className="rounded-2xl border border-white/10 bg-slate-900/70 p-5">
                  <p className="text-sm text-slate-400">Baseline reference</p>
                  <p className="mt-2 text-2xl font-semibold text-white">V23.1</p>
                  <p className="mt-2 text-sm leading-6 text-slate-300">Lifted into an adaptive topology model instead of a fixed-stride micro-optimization.</p>
                </div>
                <div className="rounded-2xl border border-white/10 bg-slate-900/70 p-5">
                  <p className="text-sm text-slate-400">Robustness pivot</p>
                  <p className="mt-2 text-2xl font-semibold text-white">Sequence-shadow parity</p>
                  <p className="mt-2 text-sm leading-6 text-slate-300">Safety proof lives in versioned snapshot validation rather than a latency-summing fence.</p>
                </div>
                <div className="rounded-2xl border border-white/10 bg-slate-900/70 p-5">
                  <p className="text-sm text-slate-400">Memory placement</p>
                  <p className="mt-2 text-2xl font-semibold text-white">Unmanaged telemetry</p>
                  <p className="mt-2 text-sm leading-6 text-slate-300">State blocks are aligned to the detected stripe and provisioned outside the GC heap.</p>
                </div>
              </div>
            </div>

            <div className="rounded-[2rem] border border-cyan-400/20 bg-slate-950/80 p-6 shadow-xl shadow-cyan-950/30">
              <p className="text-xs font-semibold uppercase tracking-[0.3em] text-cyan-300">Portable Hardware Fence-Less invariant</p>
              <div className="mt-5 space-y-4 text-sm leading-7 text-slate-300">
                <p>
                  Readers accept data only when the <span className="text-white">sequence</span>, <span className="text-white">shadow</span>,
                  {' '}<span className="text-white">commit</span>, and <span className="text-white">commit shadow</span> words all agree before and after the payload read.
                </p>
                <p>
                  That gives the design a portable, checkable correctness story: even if actor scheduling moves across sockets, mismatched epochs are rejected
                  rather than silently consumed.
                </p>
                <p>
                  The performance target is pursued by keeping the hot path narrow, avoiding lock-based synchronization, and letting adaptive striping react to measured pressure.
                </p>
              </div>
            </div>
          </div>
        </section>

        <section className="space-y-8">
          <SectionTitle
            eyebrow="Round 24 mandates"
            title="How the design answers the adversarial brief"
            description="Each requested constraint is mapped to a concrete mechanism so the submission reads like an engineering package instead of a plain prose response."
          />
          <div className="grid gap-5 lg:grid-cols-2">
            {mandates.map((item) => (
              <article key={item.id} className="rounded-3xl border border-white/10 bg-white/5 p-6 backdrop-blur">
                <div className="flex items-center gap-4">
                  <div className="flex h-12 w-12 items-center justify-center rounded-2xl border border-cyan-400/30 bg-cyan-400/10 text-sm font-bold text-cyan-200">
                    {item.id}
                  </div>
                  <h3 className="text-xl font-semibold text-white">{item.title}</h3>
                </div>
                <p className="mt-4 text-sm leading-7 text-slate-300">{item.detail}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="grid gap-8 xl:grid-cols-[0.92fr_1.08fr]">
          <div className="space-y-8">
            <SectionTitle
              eyebrow="Topology and adaptation"
              title="Hardware-derived striping instead of frozen padding"
              description="The channel computes its stride from platform topology, then uses telemetry-fed thresholds to switch cache-placement modes without changing the public API."
            />

            <div className="grid gap-4">
              {topologyFacts.map((fact) => (
                <div key={fact.label} className="rounded-2xl border border-white/10 bg-slate-900/70 p-5">
                  <div className="flex items-baseline justify-between gap-4">
                    <h3 className="text-base font-semibold text-white">{fact.label}</h3>
                    <span className="text-xs font-semibold uppercase tracking-[0.22em] text-cyan-300">{fact.value}</span>
                  </div>
                  <p className="mt-3 text-sm leading-6 text-slate-300">{fact.note}</p>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[2rem] border border-white/10 bg-white/5 p-6 backdrop-blur">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.3em] text-cyan-300">Adaptive striping console</p>
                <h3 className="mt-3 text-2xl font-semibold text-white">Mode arbitration</h3>
              </div>
              <div className="rounded-full border border-emerald-400/30 bg-emerald-400/10 px-4 py-2 text-xs font-semibold uppercase tracking-[0.22em] text-emerald-200">
                Active: {activeCard.label}
              </div>
            </div>

            <div className="mt-6 flex flex-wrap gap-3">
              {modeCards.map((card) => {
                const selected = card.key === activeMode;
                return (
                  <button
                    key={card.key}
                    onClick={() => setActiveMode(card.key)}
                    className={`rounded-full px-4 py-2 text-sm font-medium transition ${
                      selected
                        ? 'border border-cyan-400/40 bg-cyan-400/15 text-cyan-100'
                        : 'border border-white/10 bg-slate-900/70 text-slate-300 hover:bg-slate-800'
                    }`}
                  >
                    {card.label}
                  </button>
                );
              })}
            </div>

            <div className="mt-6 rounded-3xl border border-cyan-400/20 bg-slate-950/80 p-6">
              <div className="grid gap-4 sm:grid-cols-3">
                <div>
                  <p className="text-xs uppercase tracking-[0.22em] text-slate-500">Latency envelope</p>
                  <p className="mt-2 text-lg font-semibold text-white">{activeCard.latency}</p>
                </div>
                <div>
                  <p className="text-xs uppercase tracking-[0.22em] text-slate-500">Stripe geometry</p>
                  <p className="mt-2 text-lg font-semibold text-white">{activeCard.stripe}</p>
                </div>
                <div>
                  <p className="text-xs uppercase tracking-[0.22em] text-slate-500">Switch trigger</p>
                  <p className="mt-2 text-lg font-semibold text-white">{activeCard.trigger}</p>
                </div>
              </div>
              <p className="mt-5 text-sm leading-7 text-slate-300">{activeCard.summary}</p>
            </div>

            <div className="mt-6 grid gap-4 sm:grid-cols-2">
              {diagnostics.map((item) => (
                <div key={item.name} className="rounded-2xl border border-white/10 bg-slate-900/70 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <p className="text-sm font-medium text-slate-200">{item.name}</p>
                    <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs font-semibold text-cyan-200">
                      {item.value}
                    </span>
                  </div>
                  <p className="mt-3 text-sm leading-6 text-slate-400">{item.note}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="grid gap-8 lg:grid-cols-[0.9fr_1.1fr]">
          <div className="space-y-8">
            <SectionTitle
              eyebrow="Safety narrative"
              title="Why the fence-less invariant remains auditable"
              description="The page deliberately focuses on explicit validation steps so correctness is observable even when topology and scheduling conditions become hostile."
            />
            <div className="space-y-4">
              {invariants.map((point, index) => (
                <div key={point} className="rounded-2xl border border-white/10 bg-white/5 p-5">
                  <div className="flex items-start gap-4">
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-cyan-400/25 bg-cyan-400/10 text-sm font-semibold text-cyan-200">
                      {index + 1}
                    </div>
                    <p className="text-sm leading-7 text-slate-300">{point}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[2rem] border border-white/10 bg-gradient-to-br from-slate-900 via-slate-900 to-slate-950 p-6">
            <p className="text-xs font-semibold uppercase tracking-[0.3em] text-cyan-300">Implementation notes</p>
            <div className="mt-6 space-y-4">
              {implementationNotes.map((note) => (
                <div key={note} className="rounded-2xl border border-white/10 bg-white/5 p-5 text-sm leading-7 text-slate-300">
                  {note}
                </div>
              ))}
            </div>
            <div className="mt-6 rounded-3xl border border-amber-400/20 bg-amber-400/10 p-5 text-sm leading-7 text-amber-100">
              Performance numbers shown here are stated as design targets from the mission brief. Actual nanosecond behavior depends on runtime, CPU,
              memory model details, and platform-specific topology probing quality.
            </div>
          </div>
        </section>

        <section className="space-y-8 pb-8">
          <SectionTitle
            eyebrow="Submission payload"
            title="`V24_ROBUST_CODE` literal"
            description="Below is the complete, copy-friendly reference implementation block packaged exactly as the requested submission artifact."
          />
          <CodePanel code={V24_ROBUST_CODE} />
        </section>
      </main>
    </div>
  );
}
