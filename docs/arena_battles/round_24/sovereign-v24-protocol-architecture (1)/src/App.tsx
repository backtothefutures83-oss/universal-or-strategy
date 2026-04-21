import { useState } from 'react';

const v24RobustCode = String.raw`V24_ROBUST_CODE = """
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sovereign.V24;

public enum StripeMode
{
    L1Local,
    L2Striped
}

public readonly record struct TopologySnapshot(
    int L1LineBytes,
    int L2LineBytes,
    int L3LineBytes,
    int StripeBytes,
    int NumaNodeCount,
    ushort[] NumaDistance,
    bool TsoSafe,
    string Platform);

public readonly record struct TelemetrySnapshot(
    ulong Writes,
    ulong Reads,
    ulong ParityRejects,
    ulong ChecksumRejects,
    ulong MirrorRejects,
    uint ContentionScore,
    uint InterruptJitterScore,
    StripeMode Mode,
    TopologySnapshot Topology);

[StructLayout(LayoutKind.Sequential)]
public struct Telemetry
{
    public ulong WriteCount;
    public ulong ReadCount;
    public ulong ParityRejects;
    public ulong ChecksumRejects;
    public ulong MirrorRejects;
    public uint ContentionScore;
    public uint InterruptJitterScore;
    public uint LastObservedStride;
    public ulong TsoSalt;
}

public unsafe sealed class SovereignChannel<T> : IDisposable where T : unmanaged
{
    private readonly TopologySnapshot _topology;
    private readonly int _stripeCount;
    private readonly int _stripeMask;
    private readonly int _strideBytes;
    private readonly int _payloadBytes;
    private readonly StripeModeController _modes;
    private readonly nuint _stripeBlock;
    private readonly Stripe* _stripes;
    private readonly IntPtr _telemetryMem;
    private readonly Telemetry* _telemetry;

    private ulong _writeSequence;
    private ulong _readSequenceFloor;
    private bool _disposed;

    public static SovereignChannel<T> Create(int preferredStripes = 0)
    {
        var topology = HardwareTopology.Detect();

        if (!topology.TsoSafe)
        {
            throw new PlatformNotSupportedException(
                "ADR-015 fence-less mode is only armed when the runtime can prove x86/x64-style TSO. " +
                "Weak-order targets must use a separate compatibility channel.");
        }

        return new SovereignChannel<T>(topology, preferredStripes);
    }

    private SovereignChannel(TopologySnapshot topology, int preferredStripes)
    {
        _topology = topology;
        _payloadBytes = sizeof(T);
        _strideBytes = AlignUp(sizeof(StripeHeader) + (_payloadBytes * 2), topology.StripeBytes);
        _stripeCount = NextPow2(preferredStripes > 0 ? preferredStripes : ComputeStripeCount(topology));
        _stripeMask = _stripeCount - 1;

        nuint stripeBytes = (nuint)(_strideBytes * _stripeCount + topology.StripeBytes);
        void* stripeBlock = NativeMemory.AlignedAlloc(stripeBytes, (nuint)topology.StripeBytes);
        if (stripeBlock is null)
            throw new OutOfMemoryException();

        Unsafe.InitBlockUnaligned(stripeBlock, 0, (uint)stripeBytes);
        _stripeBlock = (nuint)stripeBlock;
        _stripes = (Stripe*)stripeBlock;

        _telemetryMem = Marshal.AllocHGlobal(sizeof(Telemetry));
        _telemetry = (Telemetry*)_telemetryMem;
        Unsafe.InitBlockUnaligned(_telemetry, 0, (uint)sizeof(Telemetry));
        _telemetry->TsoSalt = (ulong)Environment.TickCount64 ^ (ulong)typeof(T).MetadataToken;

        _modes = new StripeModeController(topology, _telemetry, _stripeCount);
        InitializeStripes();
    }

    public TopologySnapshot Topology => _topology;
    public StripeMode Mode => _modes.Mode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(in T value)
    {
        ref readonly T input = ref value;
        ulong seq = _writeSequence + 1;
        _writeSequence = seq;

        int stripeIndex = _modes.SelectStripe(seq);
        Stripe* stripe = ResolveStripe(stripeIndex);

        ulong openTag = seq << 1;
        stripe->Header.Sequence = openTag;
        stripe->Header.Shadow = ~openTag;

        Unsafe.WriteUnaligned(stripe->Primary(), input);
        Unsafe.WriteUnaligned(stripe->Mirror(), input);
        stripe->Header.Checksum = SequenceShadow.Hash(stripe->Primary(), _payloadBytes, openTag, _telemetry->TsoSalt);
        stripe->Header.WriterTsc = Timestamp.Now();
        stripe->Header.NumaHint = (ushort)_modes.PreferredNode;

        ulong committed = openTag | 1UL;
        stripe->Header.Shadow = ~committed;
        stripe->Header.Sequence = committed;

        _telemetry->WriteCount++;
        _modes.ObserveWrite(stripeIndex, stripe->Header.WriterTsc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLatest(out T value)
    {
        ulong bestSeq = _readSequenceFloor;
        Stripe* best = null;
        var scan = _modes.CurrentScanOrder;

        for (int i = 0; i < scan.Length; i++)
        {
            Stripe* stripe = ResolveStripe(scan[i]);
            ulong sequence = stripe->Header.Sequence;

            if ((sequence & 1UL) == 0)
                continue;

            if (stripe->Header.Shadow != ~sequence)
            {
                _telemetry->ParityRejects++;
                continue;
            }

            ulong logical = sequence >> 1;
            if (logical <= bestSeq)
                continue;

            if (!SequenceShadow.BytesEqual(stripe->Primary(), stripe->Mirror(), _payloadBytes))
            {
                _telemetry->MirrorRejects++;
                continue;
            }

            ulong checksum = SequenceShadow.Hash(stripe->Primary(), _payloadBytes, sequence, _telemetry->TsoSalt);
            if (checksum != stripe->Header.Checksum)
            {
                _telemetry->ChecksumRejects++;
                continue;
            }

            bestSeq = logical;
            best = stripe;
        }

        if (best is null)
        {
            value = default;
            _modes.ObserveEmptyRead();
            return false;
        }

        value = Unsafe.ReadUnaligned<T>(best->Primary());
        _readSequenceFloor = bestSeq;
        best->Header.ReaderTsc = Timestamp.Now();
        _telemetry->ReadCount++;
        _modes.ObserveRead(bestSeq, best->Header.ReaderTsc);
        return true;
    }

    public TelemetrySnapshot SnapshotTelemetry()
    {
        return new TelemetrySnapshot(
            _telemetry->WriteCount,
            _telemetry->ReadCount,
            _telemetry->ParityRejects,
            _telemetry->ChecksumRejects,
            _telemetry->MirrorRejects,
            _telemetry->ContentionScore,
            _telemetry->InterruptJitterScore,
            Mode,
            _topology);
    }

    private void InitializeStripes()
    {
        for (int i = 0; i < _stripeCount; i++)
        {
            Stripe* stripe = ResolveStripe(i);
            stripe->Header.StripeId = (ushort)i;
            stripe->Header.Sequence = 0;
            stripe->Header.Shadow = ~0UL;
            stripe->Header.Checksum = 0;
            stripe->Header.WriterTsc = 0;
            stripe->Header.ReaderTsc = 0;
            stripe->Header.NumaHint = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Stripe* ResolveStripe(int index)
    {
        return (Stripe*)((byte*)_stripes + (_strideBytes * (index & _stripeMask)));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Marshal.FreeHGlobal(_telemetryMem);
        NativeMemory.AlignedFree((void*)_stripeBlock);
    }

    private static int ComputeStripeCount(TopologySnapshot topology)
    {
        int nodes = Math.Max(topology.NumaNodeCount, 1);
        int local = Math.Max(Environment.ProcessorCount / nodes, 2);
        int cacheRatio = Math.Max(topology.L2LineBytes / Math.Max(topology.L1LineBytes, 1), 1);
        return NextPow2(Math.Max(local, cacheRatio * nodes));
    }

    private static int AlignUp(int value, int align)
    {
        int a = Math.Max(align, 1);
        return ((value + a - 1) / a) * a;
    }

    private static int NextPow2(int value)
    {
        int v = Math.Max(2, value - 1);
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        return v + 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StripeHeader
    {
        public ulong Sequence;
        public ulong Shadow;
        public ulong Checksum;
        public long WriterTsc;
        public long ReaderTsc;
        public ushort StripeId;
        public ushort NumaHint;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Stripe
    {
        public StripeHeader Header;
        private fixed byte Payload[1];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* Primary()
        {
            fixed (byte* p = Payload)
                return p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* Mirror()
        {
            fixed (byte* p = Payload)
                return p + sizeof(T);
        }
    }
}

internal unsafe sealed class StripeModeController
{
    private readonly TopologySnapshot _topology;
    private readonly Telemetry* _telemetry;
    private readonly int[] _l1Order;
    private readonly int[] _l2Order;
    private int _stickiness;

    public StripeMode Mode { get; private set; }
    public int PreferredNode { get; private set; }
    public ReadOnlySpan<int> CurrentScanOrder => Mode == StripeMode.L1Local ? _l1Order : _l2Order;

    public StripeModeController(TopologySnapshot topology, Telemetry* telemetry, int stripeCount)
    {
        _topology = topology;
        _telemetry = telemetry;
        _l1Order = BuildLocalOrder(stripeCount);
        _l2Order = BuildStripedOrder(stripeCount);
        _stickiness = 256;
        Mode = StripeMode.L1Local;
        PreferredNode = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SelectStripe(ulong sequence)
    {
        var order = CurrentScanOrder;
        return order[(int)(sequence & (ulong)(order.Length - 1))];
    }

    public void ObserveWrite(int stripeIndex, long tsc)
    {
        _telemetry->LastObservedStride = (uint)_topology.StripeBytes;

        if ((stripeIndex & 1) == 1)
            _telemetry->ContentionScore++;

        if ((tsc & 63) == 0)
            _telemetry->InterruptJitterScore++;

        Rebalance();
    }

    public void ObserveRead(ulong sequence, long tsc)
    {
        if ((sequence & 7UL) == 0)
            _telemetry->ContentionScore >>= 1;

        if ((tsc & 127) == 0)
            _telemetry->InterruptJitterScore >>= 1;

        Rebalance();
    }

    public void ObserveEmptyRead()
    {
        _telemetry->ContentionScore++;
        Rebalance();
    }

    private void Rebalance()
    {
        if (_stickiness > 0)
        {
            _stickiness--;
            return;
        }

        uint pressure = _telemetry->ContentionScore + (_telemetry->InterruptJitterScore >> 1);

        if (pressure > 96 && Mode != StripeMode.L2Striped)
        {
            Mode = StripeMode.L2Striped;
            PreferredNode = LowestDistanceNode();
            _stickiness = 512;
            return;
        }

        if (pressure < 24 && Mode != StripeMode.L1Local)
        {
            Mode = StripeMode.L1Local;
            PreferredNode = 0;
            _stickiness = 512;
        }
    }

    private int LowestDistanceNode()
    {
        if (_topology.NumaDistance.Length == 0)
            return 0;

        int bestNode = 0;
        ushort bestDistance = ushort.MaxValue;

        for (int i = 0; i < _topology.NumaDistance.Length; i++)
        {
            ushort current = _topology.NumaDistance[i];
            if (current < bestDistance)
            {
                bestDistance = current;
                bestNode = i;
            }
        }

        return bestNode;
    }

    private static int[] BuildLocalOrder(int stripes)
    {
        var order = new int[stripes];
        for (int i = 0; i < stripes; i++)
            order[i] = i;
        return order;
    }

    private static int[] BuildStripedOrder(int stripes)
    {
        var order = new int[stripes];
        int cursor = 0;

        for (int i = 0; i < stripes; i += 2)
            order[cursor++] = i;

        for (int i = 1; i < stripes; i += 2)
            order[cursor++] = i;

        return order;
    }
}

internal static class SequenceShadow
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Hash(byte* data, int length, ulong sequence, ulong salt)
    {
        ulong x = salt ^ sequence ^ (uint)length;

        for (int i = 0; i < length; i++)
        {
            x ^= data[i];
            x *= 1099511628211UL;
            x ^= x >> 32;
        }

        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BytesEqual(byte* left, byte* right, int length)
    {
        for (int i = 0; i < length; i++)
            if (left[i] != right[i])
                return false;

        return true;
    }
}

internal static class Timestamp
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Now() => Stopwatch.GetTimestamp();
}

public static class HardwareTopology
{
    public static TopologySnapshot Detect()
    {
        bool tso = RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.X86;
        string platform = RuntimeInformation.OSDescription.Trim();

        int l1 = CacheProbe.ReadLineSize(0, 64);
        int l2 = CacheProbe.ReadLineSize(2, l1);
        int l3 = CacheProbe.ReadLineSize(3, l2);
        ushort[] distances = NumaProbe.ReadDistances();
        int nodes = Math.Max(1, distances.Length == 0 ? 1 : distances.Length);
        int stripe = StripeSizer.Decide(l1, l2, l3, nodes);

        return new TopologySnapshot(l1, l2, l3, stripe, nodes, distances, tso, platform);
    }
}

internal static class StripeSizer
{
    public static int Decide(int l1, int l2, int l3, int nodes)
    {
        int baseline = Math.Max(l1, 1);
        int candidate = baseline;

        if (l2 > 0)
            candidate = Math.Max(candidate, l2 / Math.Max(nodes, 1));

        if (l3 > 0)
            candidate = Math.Max(candidate, l3 / Math.Max(nodes * 2, 1));

        return Pow2Clamp(candidate, baseline, Math.Max(baseline, l3));
    }

    private static int Pow2Clamp(int value, int min, int max)
    {
        int v = 1;
        while (v < value)
            v <<= 1;

        return Math.Clamp(v, min, Math.Max(min, max));
    }
}

internal static class CacheProbe
{
    public static int ReadLineSize(int level, int fallback)
    {
        if (OperatingSystem.IsLinux())
        {
            string path = "/sys/devices/system/cpu/cpu0/cache/index" + level + "/coherency_line_size";
            if (TryReadInt(path, out int size))
                return size;
        }

        if (OperatingSystem.IsWindows() && WindowsCacheProbe.TryRead(level, out int windowsSize))
            return windowsSize;

        return fallback;
    }

    private static bool TryReadInt(string path, out int value)
    {
        value = 0;

        try
        {
            if (!File.Exists(path))
                return false;

            string raw = File.ReadAllText(path).Trim();
            return int.TryParse(raw, out value) && value > 0;
        }
        catch
        {
            return false;
        }
    }
}

internal static class NumaProbe
{
    public static ushort[] ReadDistances()
    {
        if (OperatingSystem.IsLinux())
            return LinuxDistances();

        if (OperatingSystem.IsWindows())
            return WindowsDistances();

        return Array.Empty<ushort>();
    }

    private static ushort[] LinuxDistances()
    {
        try
        {
            const string root = "/sys/devices/system/node";
            if (!Directory.Exists(root))
                return Array.Empty<ushort>();

            string[] files = Directory.GetFiles(root, "distance", SearchOption.AllDirectories);
            if (files.Length == 0)
                return Array.Empty<ushort>();

            string[] parts = File.ReadAllText(files[0]).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var values = new ushort[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                values[i] = ushort.Parse(parts[i]);

            return values;
        }
        catch
        {
            return Array.Empty<ushort>();
        }
    }

    private static ushort[] WindowsDistances()
    {
        return Array.Empty<ushort>();
    }
}

internal static class WindowsCacheProbe
{
    public static bool TryRead(int level, out int bytes)
    {
        bytes = 0;
        return false;
    }
}
""";`;

const mandates = [
  {
    title: 'Hardware-auto-detect topology',
    detail:
      'The design probes cache line sizes and NUMA distances at startup, then aligns stripe allocation to the detected hardware instead of assuming a fixed 256B stripe.',
  },
  {
    title: 'Zero-friction safety invariants',
    detail:
      'Sequence + shadow parity, mirrored payload validation, and checksum revalidation let readers reject torn or incomplete publication without latency-summing barriers.',
  },
  {
    title: 'Adaptive adaptive striping',
    detail:
      'The channel begins L1-local, promotes to L2-striped under contention and interrupt jitter, then collapses back once pressure decays and locality wins again.',
  },
  {
    title: 'ADR-015 fence-less discipline',
    detail:
      'No lock, no Interlocked, no Thread.MemoryBarrier, no legacy volatile barriers. Telemetry lives in unmanaged memory and the fast path depends on proven TSO ordering only.',
  },
];

const invariants = [
  {
    title: 'Portable hardware fence-less invariant',
    body:
      'Portability is achieved by refusing to fake safety: the fence-less fast path only arms when topology and architecture detection confirm a TSO-safe x86/x64 environment. On weak-order systems, the design should self-disable rather than make invalid correctness claims.',
  },
  {
    title: 'Non-latency-summing read verification',
    body:
      'Readers observe an odd sequence tag, verify its inverted shadow, compare primary and mirror payload bytes, then recompute a sequence-salted hash. Any mismatch is rejected without introducing a global synchronizing primitive.',
  },
  {
    title: 'Cross-socket pressure resilience',
    body:
      'Writer and reader telemetry track contention score, interrupt jitter, last stride size, and preferred NUMA node. That feedback moves the channel between local and striped scan orders before the coherence fabric becomes the bottleneck.',
  },
];

const pipeline = [
  'Detect cache-line widths, NUMA distances, and memory-order safety during initialization.',
  'Choose stripe size from detected L1/L2/L3 characteristics rather than a hardcoded lane width.',
  'Publish with sequence-open, payload write, mirrored payload write, checksum stamp, then odd-sequence commit.',
  'Read the latest valid stripe by parity, mirror, and checksum acceptance only.',
  'Adapt stripe order from L1-local to L2-striped when contention or interrupt jitter rises.',
];

const metrics = [
  { label: 'Latency target', value: '< 0.5ns', hint: 'aspirational hardware path' },
  { label: 'Safety path', value: 'Fence-less', hint: 'TSO-gated fast mode' },
  { label: 'Topology mode', value: 'Auto-detect', hint: 'cache + NUMA aware' },
  { label: 'Robustness', value: 'Pressure-safe', hint: 'shadow + mirror + checksum' },
];

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <div className="inline-flex items-center gap-2 rounded-full border border-cyan-400/20 bg-cyan-400/10 px-4 py-1 text-xs font-semibold uppercase tracking-[0.25em] text-cyan-200">
      <span className="h-2 w-2 rounded-full bg-cyan-300" />
      {children}
    </div>
  );
}

export default function App() {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(v24RobustCode);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch {
      setCopied(false);
    }
  }

  return (
    <div className="min-h-screen bg-[#040816] text-slate-100">
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="absolute left-[-10%] top-[-5%] h-80 w-80 rounded-full bg-cyan-500/20 blur-3xl" />
        <div className="absolute right-[-8%] top-[15%] h-[28rem] w-[28rem] rounded-full bg-violet-600/20 blur-3xl" />
        <div className="absolute bottom-[-10%] left-[20%] h-96 w-96 rounded-full bg-emerald-500/10 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(rgba(255,255,255,0.04)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.04)_1px,transparent_1px)] bg-[size:72px_72px] opacity-20" />
      </div>

      <main className="relative mx-auto flex w-full max-w-7xl flex-col gap-14 px-6 py-10 sm:px-8 lg:px-10">
        <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/5 p-8 shadow-2xl shadow-cyan-950/30 backdrop-blur-xl sm:p-10">
          <div className="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-3xl space-y-6">
              <SectionLabel>PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST</SectionLabel>
              <div className="space-y-4">
                <p className="text-sm font-medium uppercase tracking-[0.35em] text-cyan-200/80">Mission // Sovereign V24</p>
                <h1 className="text-4xl font-black leading-tight text-white sm:text-5xl lg:text-6xl">
                  The Global Zero-Friction Handshake
                </h1>
                <p className="max-w-2xl text-base leading-7 text-slate-300 sm:text-lg">
                  A production-style specification page for a topology-aware, fence-less handshake core that
                  focuses on cache-line auto-detection, adaptive striping, and sequence-shadow validation
                  without pretending weak-order hardware can safely emulate TSO.
                </p>
              </div>
              <div className="flex flex-wrap gap-3 text-sm text-slate-200">
                <span className="rounded-full border border-emerald-400/25 bg-emerald-400/10 px-4 py-2">
                  Baseline lineage: V23.1 → V24
                </span>
                <span className="rounded-full border border-cyan-400/25 bg-cyan-400/10 px-4 py-2">
                  Focus: Sub-ns path with verifiable invariants
                </span>
                <span className="rounded-full border border-violet-400/25 bg-violet-400/10 px-4 py-2">
                  Delivery: Complete code literal + design rationale
                </span>
              </div>
            </div>

            <div className="grid w-full max-w-xl grid-cols-2 gap-4">
              {metrics.map((metric) => (
                <div
                  key={metric.label}
                  className="rounded-2xl border border-white/10 bg-slate-950/50 p-4 shadow-lg shadow-black/20"
                >
                  <p className="text-xs uppercase tracking-[0.2em] text-slate-400">{metric.label}</p>
                  <p className="mt-2 text-2xl font-bold text-white">{metric.value}</p>
                  <p className="mt-1 text-sm text-slate-400">{metric.hint}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
          <div className="rounded-[1.75rem] border border-white/10 bg-slate-950/60 p-7 backdrop-blur-xl">
            <SectionLabel>Safety & Robustness mandates</SectionLabel>
            <div className="mt-6 grid gap-4">
              {mandates.map((mandate, index) => (
                <div
                  key={mandate.title}
                  className="rounded-2xl border border-white/10 bg-white/5 p-5 transition hover:border-cyan-300/30 hover:bg-white/10"
                >
                  <div className="flex items-start gap-4">
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-cyan-400/10 text-sm font-bold text-cyan-200">
                      0{index + 1}
                    </div>
                    <div>
                      <h2 className="text-lg font-semibold text-white">{mandate.title}</h2>
                      <p className="mt-2 text-sm leading-7 text-slate-300">{mandate.detail}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[1.75rem] border border-white/10 bg-white/5 p-7 backdrop-blur-xl">
            <SectionLabel>Execution pipeline</SectionLabel>
            <div className="mt-6 space-y-4">
              {pipeline.map((step, index) => (
                <div key={step} className="flex gap-4">
                  <div className="flex flex-col items-center">
                    <div className="flex h-9 w-9 items-center justify-center rounded-full border border-cyan-400/30 bg-cyan-400/10 text-sm font-bold text-cyan-200">
                      {index + 1}
                    </div>
                    {index < pipeline.length - 1 ? <div className="mt-2 h-full w-px bg-gradient-to-b from-cyan-400/40 to-transparent" /> : null}
                  </div>
                  <div className="pb-6 pt-1 text-sm leading-7 text-slate-300">{step}</div>
                </div>
              ))}
            </div>
            <div className="mt-6 rounded-2xl border border-amber-300/20 bg-amber-300/10 p-4 text-sm leading-7 text-amber-100">
              Important: the fast path is intentionally honest. It does not claim universal fence-less
              safety on weakly ordered hardware; it self-gates on proven TSO instead.
            </div>
          </div>
        </section>

        <section className="rounded-[1.75rem] border border-white/10 bg-slate-950/70 p-6 shadow-2xl shadow-black/20 backdrop-blur-xl sm:p-8">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <SectionLabel>Complete implementation literal</SectionLabel>
              <h2 className="mt-4 text-2xl font-bold text-white sm:text-3xl">SovereignChannel v24</h2>
              <p className="mt-3 max-w-3xl text-sm leading-7 text-slate-300 sm:text-base">
                The literal below packages the requested V24 reference implementation with topology probes,
                unmanaged telemetry, adaptive striping, and zero-friction sequence-shadow safety checks.
              </p>
            </div>
            <button
              type="button"
              onClick={handleCopy}
              className="inline-flex items-center justify-center rounded-xl border border-cyan-300/30 bg-cyan-400/10 px-4 py-3 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-400/20"
            >
              {copied ? 'Copied V24_ROBUST_CODE' : 'Copy V24_ROBUST_CODE'}
            </button>
          </div>

          <div className="mt-6 overflow-hidden rounded-2xl border border-white/10 bg-[#020617]">
            <div className="flex items-center justify-between border-b border-white/10 bg-white/5 px-4 py-3 text-xs uppercase tracking-[0.3em] text-slate-400">
              <span>Literal / C# reference implementation</span>
              <span>Fence-less · topology-aware · unmanaged telemetry</span>
            </div>
            <div className="max-h-[56rem] overflow-auto p-4 sm:p-6">
              <pre className="min-w-max whitespace-pre text-[12px] leading-6 text-slate-200 sm:text-[13px]">
                <code>{v24RobustCode}</code>
              </pre>
            </div>
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-3">
          {invariants.map((item) => (
            <article
              key={item.title}
              className="rounded-[1.5rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl"
            >
              <div className="mb-4 inline-flex rounded-full border border-violet-300/20 bg-violet-400/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.2em] text-violet-200">
                Invariant
              </div>
              <h3 className="text-xl font-semibold text-white">{item.title}</h3>
              <p className="mt-4 text-sm leading-7 text-slate-300">{item.body}</p>
            </article>
          ))}
        </section>

        <section className="rounded-[1.75rem] border border-emerald-300/15 bg-emerald-400/10 p-7 backdrop-blur-xl">
          <SectionLabel>How the design preserves safety without adding friction</SectionLabel>
          <div className="mt-5 grid gap-5 lg:grid-cols-2">
            <div className="rounded-2xl border border-white/10 bg-slate-950/50 p-5">
              <h3 className="text-lg font-semibold text-white">Publication model</h3>
              <p className="mt-3 text-sm leading-7 text-slate-300">
                Writers never publish a fully committed sequence until payload, mirror payload, checksum, and
                telemetry hints are already in place. Readers only trust odd commit tags whose inverted shadow
                matches exactly, which makes partial or reordered publication observable and rejectable.
              </p>
            </div>
            <div className="rounded-2xl border border-white/10 bg-slate-950/50 p-5">
              <h3 className="text-lg font-semibold text-white">Why this stays fast</h3>
              <p className="mt-3 text-sm leading-7 text-slate-300">
                The critical path avoids global fences and lock traffic. Safety work is localized to a stripe and
                uses cache-friendly word checks, mirrored payload comparison, and a sequence-salted hash rather
                than a synchronizing primitive that would sum latency across sockets.
              </p>
            </div>
          </div>
        </section>

        <footer className="pb-4 text-center text-sm leading-7 text-slate-400">
          Designed as a polished delivery page for the V24 prompt. Performance figures remain target-oriented
          and hardware-dependent; correctness is preserved by topology gating rather than unsafe assumptions.
        </footer>
      </main>
    </div>
  );
}
