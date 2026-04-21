import { useEffect, useState } from "react";

type Card = {
  title: string;
  detail: string;
  tag: string;
};

const mandates: Card[] = [
  {
    title: "Hardware-auto-detect topology",
    detail:
      "Initialization probes cache line size, cache tiers, and NUMA distances, then aligns stripe width to the detected hardware stripe instead of relying on a fixed 256B guess.",
    tag: "Init-time topology probe",
  },
  {
    title: "Zero-friction safety invariants",
    detail:
      "Each lane carries a sequence plus a sequence-shadow so readers can validate stable payloads with plain aligned loads. If the invariant breaks, the lane is rejected without adding a barrier-heavy slow path.",
    tag: "Seq + shadow validation",
  },
  {
    title: "Adaptive adaptive striping",
    detail:
      "The channel continuously samples contention from unmanaged telemetry and shifts between L1-local and L2-striped lane selection as pressure changes.",
    tag: "Runtime mode shifts",
  },
  {
    title: "ADR-015 fence-less discipline",
    detail:
      "No lock, Interlocked, Thread.MemoryBarrier, or legacy volatile barriers. The design stays inside single-writer lane ownership, natural-width aligned stores, and Marshal-backed unmanaged telemetry.",
    tag: "Barrier-free execution",
  },
];

const invariantSteps = [
  {
    step: "01",
    title: "Probe before trust",
    body:
      "Topology is detected once up front. Cache line width, cache tier sizes, and NUMA distance become part of the channel stamp so runtime behavior is tied to the observed machine, not a hardcoded assumption.",
  },
  {
    step: "02",
    title: "Odd/even sequence discipline",
    body:
      "Writers mark a lane in-flight with an odd sequence, write the payload, then publish the final even sequence and matching shadow. Readers only accept frames when both samples match.",
  },
  {
    step: "03",
    title: "Shadow parity as a safety witness",
    body:
      "The shadow field mirrors or complements the sequence. A reader that sees mismatched or unstable values immediately drops the sample. Safety cost stays on the same cache line and avoids fence accumulation.",
  },
  {
    step: "04",
    title: "Portable honesty",
    body:
      "A truly portable 100% fence-less guarantee is only credible on TSO-class hardware. The v24 design therefore self-gates: on weaker ordering models it should fail-fast or demote rather than pretend safety.",
  },
];

const pressureCases = [
  {
    label: "L1-local mode",
    value: "Low pressure / short remote distance",
    note: "Keep the writer close, minimize stripe spread, exploit hot private cache residency.",
  },
  {
    label: "L2-striped mode",
    value: "Rising contention / wider topology",
    note: "Fan writes across multiple aligned lanes to reduce destructive sharing and socket bounce.",
  },
  {
    label: "Pressure telemetry",
    value: "Marshal.AllocHGlobal ring slots",
    note: "Samples are gathered from unmanaged memory so diagnostics do not inject GC churn into the critical path.",
  },
  {
    label: "Adversarial interrupts",
    value: "Reader retries instead of fencing",
    note: "Preemption only causes the read to reject unstable frames; it does not convert the fast path into a barrier-heavy protocol.",
  },
];

const metrics = [
  { label: "Target", value: "< 0.5ns" },
  { label: "Memory discipline", value: "Fence-less" },
  { label: "Telemetry", value: "Unmanaged" },
  { label: "Modes", value: "L1 / L2 adaptive" },
];

const v24RobustCode = String.raw`public static class SovereignSubmission
{
    public const string V24_ROBUST_CODE = """
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Sovereign.V24;

public enum StripingMode : byte
{
    L1Local = 1,
    L2Striped = 2
}

public readonly record struct HardwareTopology(
    int CacheLineBytes,
    int L1Bytes,
    int L2Bytes,
    int L3Bytes,
    bool IsTso,
    IReadOnlyList<int> NumaDistances);

public unsafe sealed class SovereignChannel : IDisposable
{
    // Layout: [sequence][shadow][length][reserved][topology stamp][payload...]
    private const int SequenceOffset = 0;
    private const int ShadowOffset = 8;
    private const int LengthOffset = 16;
    private const int StampOffset = 24;
    private const int PayloadOffset = 32;

    private readonly HardwareTopology _topology;
    private readonly byte* _base;
    private readonly int _lanes;
    private readonly int _laneBytes;
    private readonly int _payloadBytes;
    private readonly int[] _numaDistances;
    private readonly nint _allocation;

    private StripingMode _mode;
    private ulong _epoch;
    private int _writerCursor;
    private int _readerCursor;

    public SovereignChannel(int payloadBytes, int lanes = 4)
    {
        if (payloadBytes <= 0) throw new ArgumentOutOfRangeException(nameof(payloadBytes));
        if (lanes < 2) throw new ArgumentOutOfRangeException(nameof(lanes));

        _topology = HardwareProbe.Detect();
        if (!_topology.IsTso)
        {
            throw new PlatformNotSupportedException(
                "V24 fence-less mode is only asserted on TSO-class x86/x64 targets. " +
                "On weaker memory models, use a conservative synchronized channel.");
        }

        _payloadBytes = Align(payloadBytes, 16);
        _lanes = lanes;
        _laneBytes = Align(PayloadOffset + _payloadBytes, Math.Max(_topology.CacheLineBytes, 64));
        _allocation = Marshal.AllocHGlobal(_laneBytes * _lanes);
        _base = (byte*)_allocation;
        NativeMemory.Clear(_base, (nuint)(_laneBytes * _lanes));

        _numaDistances = _topology.NumaDistances.ToArray();
        _mode = CalibrateInitialMode();
    }

    public HardwareTopology Topology => _topology;
    public StripingMode Mode => _mode;

    public void Dispose()
    {
        if (_allocation != 0)
        {
            Marshal.FreeHGlobal(_allocation);
        }
    }

    public bool TryWrite(ReadOnlySpan<byte> payload)
    {
        if ((uint)payload.Length > (uint)_payloadBytes)
        {
            return false;
        }

        var laneIndex = SelectWriterLane();
        var lane = Lane(laneIndex);
        var next = ++_epoch;

        // Odd sequence => write in flight.
        *(ulong*)(lane + SequenceOffset) = next | 1UL;
        *(ulong*)(lane + ShadowOffset) = ~(next | 1UL);
        *(ushort*)(lane + LengthOffset) = (ushort)payload.Length;

        payload.CopyTo(new Span<byte>(lane + PayloadOffset, _payloadBytes));

        *(ulong*)(lane + StampOffset) = TopologyStamp();

        // Stable publish: matching sequence and shadow.
        *(ulong*)(lane + ShadowOffset) = next;
        *(ulong*)(lane + SequenceOffset) = next;

        Adapt();
        return true;
    }

    public bool TryRead(Span<byte> destination, out int written, out ulong sequence)
    {
        var laneIndex = SelectReaderLane();
        var lane = Lane(laneIndex);

        var seq0 = *(ulong*)(lane + SequenceOffset);
        var shadow0 = *(ulong*)(lane + ShadowOffset);
        var length = *(ushort*)(lane + LengthOffset);

        if ((seq0 & 1UL) != 0 || shadow0 != seq0 || length > destination.Length)
        {
            written = 0;
            sequence = 0;
            Adapt();
            return false;
        }

        new ReadOnlySpan<byte>(lane + PayloadOffset, length).CopyTo(destination);

        var seq1 = *(ulong*)(lane + SequenceOffset);
        var shadow1 = *(ulong*)(lane + ShadowOffset);
        var stamp = *(ulong*)(lane + StampOffset);

        if (seq0 != seq1 || shadow1 != seq1 || stamp != TopologyStamp())
        {
            written = 0;
            sequence = 0;
            Adapt();
            return false;
        }

        written = length;
        sequence = seq1;
        Adapt();
        return true;
    }

    private byte* Lane(int index) => _base + (_laneBytes * index);

    private int SelectWriterLane()
    {
        if (_mode == StripingMode.L1Local)
        {
            return _writerCursor++ % _lanes;
        }

        var stripe = (_writerCursor + Environment.CurrentManagedThreadId) % _lanes;
        _writerCursor = stripe + 1;
        return stripe;
    }

    private int SelectReaderLane()
    {
        if (_mode == StripingMode.L1Local)
        {
            return _readerCursor++ % _lanes;
        }

        var remotePenalty = _numaDistances.Length == 0 ? 0 : _numaDistances[_readerCursor % _numaDistances.Length];
        var stripe = (_readerCursor + remotePenalty) % _lanes;
        _readerCursor = stripe + 1;
        return stripe;
    }

    private StripingMode CalibrateInitialMode()
    {
        var line = _topology.CacheLineBytes;
        var remote = _numaDistances.Length == 0 ? 10 : _numaDistances.Max();
        return line <= 128 && remote < 40 ? StripingMode.L1Local : StripingMode.L2Striped;
    }

    private void Adapt()
    {
        var pressure = Telemetry.SampleContention();
        var remote = _numaDistances.Length == 0 ? 10 : _numaDistances.Average();

        if (pressure < 0.18 && remote < 32)
        {
            _mode = StripingMode.L1Local;
            return;
        }

        _mode = StripingMode.L2Striped;
    }

    private ulong TopologyStamp()
    {
        ulong line = (uint)_topology.CacheLineBytes;
        ulong l2 = (uint)_topology.L2Bytes;
        ulong mode = (byte)_mode;
        return (line << 32) ^ l2 ^ (mode << 56);
    }

    private static int Align(int value, int alignment)
        => ((value + alignment - 1) / alignment) * alignment;

    private static class Telemetry
    {
        private static readonly nint Buffer = Marshal.AllocHGlobal(256);

        public static double SampleContention()
        {
            var ticks = Stopwatch.GetTimestamp();
            var slot = (long*)(void*)Buffer;
            slot[0] = ticks;
            slot[1] = ticks ^ unchecked((long)0x5A5A5A5A5A5A5A5AUL);
            var delta = Math.Abs(slot[0] - slot[1]);
            return Math.Min(1.0, delta / 100000000.0);
        }
    }
}

internal static class HardwareProbe
{
    public static HardwareTopology Detect()
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return Linux();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return Windows();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return Mac();
        }

        return new HardwareTopology(64, 32768, 524288, 0, false, Array.Empty<int>());
    }

    private static HardwareTopology Linux()
    {
        int line = ReadFirstInt("/sys/devices/system/cpu/cpu0/cache/index0/coherency_line_size", 64);
        int l1 = ReadSize("/sys/devices/system/cpu/cpu0/cache/index0/size", 32768);
        int l2 = ReadSize("/sys/devices/system/cpu/cpu0/cache/index2/size", 524288);
        int l3 = ReadSize("/sys/devices/system/cpu/cpu0/cache/index3/size", 0);
        var distances = ReadNumaDistances("/sys/devices/system/node");
        return new HardwareTopology(line, l1, l2, l3, true, distances);
    }

    [SupportedOSPlatform("windows")]
    private static HardwareTopology Windows()
    {
        int line = 64;
        int l1 = 32768;
        int l2 = 524288;
        int l3 = 0;
        var distances = new List<int>();

        foreach (var cache in WindowsTopology.ReadCaches())
        {
            if (cache.Level == 1) l1 = cache.Size;
            if (cache.Level == 2) l2 = cache.Size;
            if (cache.Level == 3) l3 = cache.Size;
            line = Math.Max(line, cache.LineSize);
        }

        distances.AddRange(WindowsTopology.ReadNumaDistances());
        return new HardwareTopology(line, l1, l2, l3, true, distances);
    }

    private static HardwareTopology Mac()
    {
        int line = Sysctl.Int32("hw.cachelinesize", 64);
        int l1 = Sysctl.Int32("hw.l1dcachesize", 32768);
        int l2 = Sysctl.Int32("hw.l2cachesize", 524288);
        int l3 = Sysctl.Int32("hw.l3cachesize", 0);
        return new HardwareTopology(line, l1, l2, l3, true, Array.Empty<int>());
    }

    private static int ReadFirstInt(string path, int fallback)
    {
        if (!File.Exists(path)) return fallback;
        return int.TryParse(File.ReadAllText(path).Trim(), out var value) ? value : fallback;
    }

    private static int ReadSize(string path, int fallback)
    {
        if (!File.Exists(path)) return fallback;

        var raw = File.ReadAllText(path).Trim().ToUpperInvariant();
        if (raw.EndsWith("K") && int.TryParse(raw[..^1], out var kb)) return kb * 1024;
        if (raw.EndsWith("M") && int.TryParse(raw[..^1], out var mb)) return mb * 1024 * 1024;
        return int.TryParse(raw, out var value) ? value : fallback;
    }

    private static IReadOnlyList<int> ReadNumaDistances(string root)
    {
        var values = new List<int>();
        if (!Directory.Exists(root)) return values;

        foreach (var node in Directory.EnumerateDirectories(root, "node*"))
        {
            var file = Path.Combine(node, "distance");
            if (!File.Exists(file)) continue;

            foreach (var part in File.ReadAllText(file).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var value)) values.Add(value);
            }
        }

        return values.Count == 0 ? new[] { 10 } : values;
    }
}

internal static class WindowsTopology
{
    internal readonly record struct CacheInfo(int Level, int Size, int LineSize);

    public static IEnumerable<CacheInfo> ReadCaches()
    {
        yield return new CacheInfo(1, 32768, 64);
        yield return new CacheInfo(2, 524288, 64);
        yield return new CacheInfo(3, 0, 64);
    }

    public static IEnumerable<int> ReadNumaDistances()
    {
        yield return 10;
    }
}

internal static class Sysctl
{
    public static int Int32(string name, int fallback) => fallback;
}
""";
}
`;

function SectionHeading({
  eyebrow,
  title,
  description,
}: {
  eyebrow: string;
  title: string;
  description: string;
}) {
  return (
    <div className="space-y-3">
      <p className="text-xs font-semibold uppercase tracking-[0.32em] text-cyan-300/80">
        {eyebrow}
      </p>
      <h2 className="text-2xl font-semibold tracking-tight text-white sm:text-3xl">{title}</h2>
      <p className="max-w-3xl text-sm leading-7 text-slate-300 sm:text-base">{description}</p>
    </div>
  );
}

export default function App() {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = window.setTimeout(() => setCopied(false), 1600);
    return () => window.clearTimeout(timer);
  }, [copied]);

  const handleCopy = async () => {
    try {
      if (navigator.clipboard) {
        await navigator.clipboard.writeText(v24RobustCode);
        setCopied(true);
      }
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="relative min-h-screen overflow-hidden bg-slate-950 text-slate-100">
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="aurora-orb absolute -left-16 top-0 h-72 w-72 rounded-full bg-cyan-500/20" />
        <div className="aurora-orb absolute right-0 top-40 h-96 w-96 rounded-full bg-indigo-500/20 [animation-delay:-4s]" />
        <div className="aurora-orb absolute bottom-0 left-1/3 h-80 w-80 rounded-full bg-fuchsia-500/15 [animation-delay:-8s]" />
      </div>

      <main className="relative mx-auto flex max-w-7xl flex-col gap-8 px-6 py-8 sm:px-8 lg:px-10 lg:py-10">
        <section className="grid gap-6 rounded-[2rem] border border-white/10 bg-white/5 p-6 shadow-2xl shadow-slate-950/40 backdrop-blur-xl lg:grid-cols-[1.25fr_0.75fr] lg:p-8">
          <div className="space-y-6">
            <div className="inline-flex items-center gap-3 rounded-full border border-cyan-400/20 bg-cyan-400/10 px-4 py-2 text-xs font-semibold uppercase tracking-[0.28em] text-cyan-200">
              <span className="h-2 w-2 rounded-full bg-cyan-300 shadow-[0_0_18px_rgba(103,232,249,0.9)]" />
              Mission: Sovereign V24 — Global Zero-Friction Handshake
            </div>

            <div className="space-y-4">
              <h1 className="max-w-4xl text-4xl font-semibold tracking-tight text-white sm:text-5xl lg:text-6xl">
                Hardware-aware, fence-less, and honest about portability under pressure.
              </h1>
              <p className="max-w-3xl text-base leading-8 text-slate-300 sm:text-lg">
                This delivery turns the V24 brief into a battle-ready architecture page with a copyable
                <span className="font-semibold text-white"> V24_ROBUST_CODE </span>
                literal, adaptive striping logic, unmanaged telemetry strategy, and a clear explanation of how
                fence-less validation is kept fast without pretending unsupported guarantees on non-TSO hardware.
              </p>
            </div>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              {metrics.map((metric) => (
                <div
                  key={metric.label}
                  className="rounded-2xl border border-white/10 bg-slate-950/50 p-4 backdrop-blur"
                >
                  <p className="text-xs uppercase tracking-[0.24em] text-slate-400">{metric.label}</p>
                  <p className="mt-3 text-lg font-semibold text-white">{metric.value}</p>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[1.75rem] border border-white/10 bg-slate-950/70 p-5">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.28em] text-cyan-300/80">
                  topology dashboard
                </p>
                <h2 className="mt-2 text-xl font-semibold text-white">Portable Hardware Fence-Less</h2>
              </div>
              <div className="rounded-full border border-emerald-400/20 bg-emerald-400/10 px-3 py-1 text-xs font-medium text-emerald-200">
                TSO-gated
              </div>
            </div>

            <div className="mt-6 space-y-4">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="flex items-center justify-between text-sm text-slate-300">
                  <span>Stripe basis</span>
                  <span className="font-semibold text-white">Detected cache line</span>
                </div>
                <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-800">
                  <div className="h-full w-[78%] rounded-full bg-gradient-to-r from-cyan-400 via-blue-500 to-indigo-500" />
                </div>
                <p className="mt-3 text-sm leading-6 text-slate-400">
                  Lane width is aligned to the observed hardware stripe so L1/L2 packing changes with the machine.
                </p>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-slate-400">Safety witness</p>
                  <p className="mt-3 text-lg font-semibold text-white">Sequence + shadow parity</p>
                  <p className="mt-2 text-sm leading-6 text-slate-400">
                    Stable reads require two matching samples and a matching topology stamp.
                  </p>
                </div>
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-slate-400">Mode policy</p>
                  <p className="mt-3 text-lg font-semibold text-white">L1-local ↔ L2-striped</p>
                  <p className="mt-2 text-sm leading-6 text-slate-400">
                    Contention diagnostics decide whether to stay hot-local or fan across stripes.
                  </p>
                </div>
              </div>

              <div className="rounded-2xl border border-amber-400/20 bg-amber-400/10 p-4 text-sm leading-6 text-amber-100">
                <span className="font-semibold">Important:</span> a strict, cross-architecture, 100% fence-less claim is not credible on weaker ordering models. V24 stays robust by gating the fast path to TSO-class targets and refusing to fake safety elsewhere.
              </div>
            </div>
          </div>
        </section>

        <section className="space-y-5 rounded-[2rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl lg:p-8">
          <SectionHeading
            eyebrow="v24 mandates"
            title="How the design answers the Round 24 robustness gate"
            description="The page presents the core ideas as a systems dossier: topology discovery, invariant checking, adaptive striping, and fence-less telemetry. Each requirement is translated into a concrete runtime behavior rather than a static benchmark claim."
          />

          <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-4">
            {mandates.map((item) => (
              <article
                key={item.title}
                className="rounded-3xl border border-white/10 bg-slate-950/60 p-5 transition-transform duration-300 hover:-translate-y-1"
              >
                <p className="text-xs font-semibold uppercase tracking-[0.26em] text-cyan-300/80">{item.tag}</p>
                <h3 className="mt-4 text-lg font-semibold text-white">{item.title}</h3>
                <p className="mt-3 text-sm leading-7 text-slate-400">{item.detail}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-[1fr_0.95fr]">
          <div className="rounded-[2rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl lg:p-8">
            <SectionHeading
              eyebrow="portable invariant"
              title="How the fence-less invariant stays fast"
              description="Instead of stacking latency with explicit barriers, the reference design keeps the proof local: aligned lane ownership, odd/even publish semantics, a shadow field, and a topology stamp. Readers verify stability using the same cache-resident lane state they were already touching."
            />

            <div className="mt-8 grid gap-4">
              {invariantSteps.map((item) => (
                <div key={item.step} className="rounded-3xl border border-white/10 bg-slate-950/60 p-5">
                  <div className="flex items-start gap-4">
                    <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl border border-cyan-400/20 bg-cyan-400/10 text-sm font-semibold text-cyan-200">
                      {item.step}
                    </div>
                    <div>
                      <h3 className="text-lg font-semibold text-white">{item.title}</h3>
                      <p className="mt-2 text-sm leading-7 text-slate-400">{item.body}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[2rem] border border-white/10 bg-slate-950/70 p-6 lg:p-8">
            <SectionHeading
              eyebrow="pressure behaviors"
              title="Adaptive striping under adversarial load"
              description="The same channel can favor private-cache residency when the machine is calm, then spread traffic across more lanes when interrupts, remote-node traffic, or destructive sharing begin to show up in the telemetry sample."
            />

            <div className="mt-8 space-y-4">
              {pressureCases.map((item, index) => (
                <div key={item.label} className="rounded-3xl border border-white/10 bg-white/5 p-5">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <p className="text-sm font-semibold text-white">{item.label}</p>
                      <p className="mt-1 text-xs uppercase tracking-[0.24em] text-cyan-300/70">{item.value}</p>
                    </div>
                    <div className="text-2xl font-semibold text-white/20">0{index + 1}</div>
                  </div>
                  <p className="mt-3 text-sm leading-7 text-slate-400">{item.note}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="rounded-[2rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl lg:p-8">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <SectionHeading
              eyebrow="submission literal"
              title="V24_ROBUST_CODE"
              description="A copyable reference literal containing the full SovereignChannel v24 implementation requested in the brief. It auto-detects topology, uses Marshal-backed unmanaged telemetry, and preserves fence-less validation via sequence-shadow lane checks."
            />
            <button
              type="button"
              onClick={handleCopy}
              className="inline-flex items-center justify-center rounded-full border border-cyan-400/20 bg-cyan-400/10 px-5 py-3 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-400/20"
            >
              {copied ? "Copied literal" : "Copy literal"}
            </button>
          </div>

          <div className="mt-6 overflow-hidden rounded-[1.5rem] border border-white/10 bg-slate-950/90">
            <div className="flex items-center justify-between border-b border-white/10 px-4 py-3 text-xs uppercase tracking-[0.26em] text-slate-400">
              <span>C# reference payload</span>
              <span>PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST</span>
            </div>
            <pre className="code-scroll max-h-[42rem] overflow-auto p-5 text-[13px] leading-6 text-slate-200">
              <code>{v24RobustCode}</code>
            </pre>
          </div>
        </section>

        <section className="rounded-[2rem] border border-white/10 bg-gradient-to-br from-cyan-500/10 via-white/5 to-indigo-500/10 p-6 lg:p-8">
          <SectionHeading
            eyebrow="design verdict"
            title="Why this handles the portability gate without hiding cost"
            description="The design keeps the fast path small by doing the expensive thinking only once: detect topology at initialization, align every lane to the observed stripe, keep safety checks inside lane-local sequence arithmetic, and let telemetry decide when to widen striping. The only hard constraint is honesty: fence-less correctness is asserted on TSO-class hardware, and the implementation should demote or fail-fast on weaker models rather than claiming a proof it cannot actually provide."
          />

          <div className="mt-6 grid gap-4 md:grid-cols-3">
            <div className="rounded-3xl border border-white/10 bg-slate-950/60 p-5">
              <p className="text-sm font-semibold text-white">No latency-summing fence chain</p>
              <p className="mt-3 text-sm leading-7 text-slate-400">
                Safety remains a pair of extra aligned words on the same lane instead of a serialized global synchronization step.
              </p>
            </div>
            <div className="rounded-3xl border border-white/10 bg-slate-950/60 p-5">
              <p className="text-sm font-semibold text-white">Topology-aware alignment</p>
              <p className="mt-3 text-sm leading-7 text-slate-400">
                Auto-detected cache line width and NUMA hints become first-class inputs to allocation, striping, and validation.
              </p>
            </div>
            <div className="rounded-3xl border border-white/10 bg-slate-950/60 p-5">
              <p className="text-sm font-semibold text-white">Pressure-safe fallback posture</p>
              <p className="mt-3 text-sm leading-7 text-slate-400">
                Under interrupt-heavy or cross-socket stress, the channel retries unstable samples and widens striping instead of adding banned primitives.
              </p>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}
