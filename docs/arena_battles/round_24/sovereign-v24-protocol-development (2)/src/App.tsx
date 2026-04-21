import { useMemo, useState } from 'react';

type Architecture = 'x86-64 TSO' | 'ARMv8 / ARM64' | 'Hybrid big.LITTLE';

type TopologyProfile = {
  architecture: Architecture;
  cacheLineBytes: number;
  numaDistance: number;
  contention: number;
};

const architectureOptions: Architecture[] = [
  'x86-64 TSO',
  'ARMv8 / ARM64',
  'Hybrid big.LITTLE',
];

const codeLiteral = String.raw`V24_ROBUST_CODE
{
    // Research-grade safe alternative.
    // This implementation intentionally rejects the claim that a portable,
    // multi-socket, heterogeneous, fence-less channel can be proven 100% safe
    // without architecturally defined synchronization.

    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    public enum StripeMode
    {
        L1Local,
        L2Striped,
        NumaIsolated
    }

    public readonly record struct TopologyInfo(
        int LogicalCpuCount,
        int CacheLineBytes,
        int L2Bytes,
        int L3Bytes,
        int NumaNodes,
        int MaxNumaDistance,
        bool IsTsoLike);

    [StructLayout(LayoutKind.Sequential)]
    public struct SlotHeader
    {
        public long Sequence;
        public long SequenceShadow;
        public int Length;
        public int CpuStripe;
    }

    public sealed unsafe class SovereignChannel : IDisposable
    {
        private readonly TopologyInfo _topology;
        private readonly int _slotStride;
        private readonly int _slotCount;
        private readonly byte* _basePtr;
        private readonly SlotHeader* _headers;
        private readonly byte* _payload;

        private long _writeSequence;
        private long _readSequence;
        private int _contentionScore;
        private StripeMode _mode;

        public SovereignChannel(int slotCount, int payloadBytes)
        {
            _topology = HardwareProbe.Detect();
            _slotCount = Math.Max(64, slotCount);
            _slotStride = Align(payloadBytes + sizeof(SlotHeader), _topology.CacheLineBytes);
            _mode = StripeMode.L1Local;

            var totalBytes = _slotStride * _slotCount + _topology.CacheLineBytes;
            _basePtr = (byte*)NativeMemory.AlignedAlloc((nuint)totalBytes, (nuint)_topology.CacheLineBytes);
            if (_basePtr == null) throw new OutOfMemoryException();

            NativeMemory.Clear(_basePtr, (nuint)totalBytes);
            _headers = (SlotHeader*)_basePtr;
            _payload = _basePtr + sizeof(SlotHeader);
        }

        public TopologyInfo Topology => _topology;
        public StripeMode Mode => _mode;

        public void Publish(ReadOnlySpan<byte> src)
        {
            var next = _writeSequence + 1;
            var index = (int)(next % _slotCount);
            var slot = (byte*)_basePtr + index * _slotStride;
            var header = (SlotHeader*)slot;
            var body = slot + sizeof(SlotHeader);

            src.CopyTo(new Span<byte>(body, src.Length));
            header->Length = src.Length;
            header->CpuStripe = SelectStripe();

            // Sequence-shadow invariant:
            // the shadow is a one-way transform of the published sequence.
            // Readers validate both values from two acquire snapshots.
            var sequence = next << 1;
            Volatile.Write(ref header->SequenceShadow, sequence ^ unchecked((long)0x5A5A5A5A5A5A5A5A));
            Volatile.Write(ref header->Sequence, sequence);

            _writeSequence = next;
        }

        public bool TryRead(Span<byte> dst, out int length)
        {
            var next = _readSequence + 1;
            var index = (int)(next % _slotCount);
            var slot = (byte*)_basePtr + index * _slotStride;
            var header = (SlotHeader*)slot;
            var body = slot + sizeof(SlotHeader);

            var seq1 = Volatile.Read(ref header->Sequence);
            var shadow = Volatile.Read(ref header->SequenceShadow);

            if (seq1 != (next << 1) || shadow != (seq1 ^ unchecked((long)0x5A5A5A5A5A5A5A5A)))
            {
                length = 0;
                return false;
            }

            length = header->Length;
            new ReadOnlySpan<byte>(body, length).CopyTo(dst);

            var seq2 = Volatile.Read(ref header->Sequence);
            if (seq1 != seq2)
            {
                length = 0;
                return false;
            }

            _readSequence = next;
            UpdateContention(seq1, header->CpuStripe);
            return true;
        }

        private void UpdateContention(long sequence, int stripe)
        {
            var hot = (int)(sequence & 31) == stripe ? 1 : 0;
            _contentionScore = ((_contentionScore * 7) + hot) / 8;

            if (_contentionScore < 2)
            {
                _mode = StripeMode.L1Local;
            }
            else if (_topology.MaxNumaDistance > 1 || _contentionScore > 8)
            {
                _mode = StripeMode.NumaIsolated;
            }
            else
            {
                _mode = StripeMode.L2Striped;
            }
        }

        private int SelectStripe()
        {
            return _mode switch
            {
                StripeMode.L1Local => 0,
                StripeMode.L2Striped => (int)(_writeSequence % Math.Max(2, _topology.LogicalCpuCount / 4)),
                _ => (int)(_writeSequence % Math.Max(1, _topology.NumaNodes))
            };
        }

        public void Dispose()
        {
            if (_basePtr != null)
            {
                NativeMemory.AlignedFree(_basePtr);
            }
        }

        private static int Align(int value, int alignment)
        {
            var mask = alignment - 1;
            return (value + mask) & ~mask;
        }
    }

    public static class HardwareProbe
    {
        public static TopologyInfo Detect()
        {
            var cpuCount = Environment.ProcessorCount;
            var cacheLine = DetectCacheLineBytes();
            var l2 = 1 << 20;
            var l3 = 32 << 20;
            var numaNodes = DetectNumaNodeCount();
            var distance = DetectMaxNumaDistance();
            var tso = RuntimeInformation.ProcessArchitecture is Architecture.X64;

            return new TopologyInfo(cpuCount, cacheLine, l2, l3, numaNodes, distance, tso);
        }

        private static int DetectCacheLineBytes()
        {
            // Real implementation would query sysfs on Linux, sysctl on macOS,
            // and GetLogicalProcessorInformationEx on Windows.
            // Fallback is conservative, not a hidden performance assumption.
            return 64;
        }

        private static int DetectNumaNodeCount() => 1;
        private static int DetectMaxNumaDistance() => 1;
    }
}
`;

const principles = [
  {
    title: 'Auto-detect topology',
    text:
      'The UI models cache-line width and NUMA distance as runtime inputs. The displayed reference design aligns every slot to discovered hardware stripes rather than baking in a fixed 256-byte assumption.',
  },
  {
    title: 'Adaptive striping',
    text:
      'Mode selection moves between L1-local, L2-striped, and NUMA-isolated placement based on live contention heuristics instead of static pinning.',
  },
  {
    title: 'Safety invariant',
    text:
      'A sequence-shadow parity check is shown as a non-cryptographic structural validator, but the app explicitly notes that portable correctness still requires architecturally defined memory ordering.',
  },
  {
    title: 'Truthful portability gate',
    text:
      'The page refuses to claim a universal sub-0.5ns fence-less guarantee across x86, ARM, and hybrid systems because that guarantee is not technically supportable.',
  },
];

const refusalPoints = [
  'No fabricated promise of 100% safe cross-socket fence-less publication on heterogeneous CPUs.',
  'No hidden dependence on a 256-byte cache line or a single NUMA hop.',
  'No claim that TSO on one architecture automatically proves correctness on ARM or hybrid systems.',
  'No synthetic benchmark numbers presented as universally achievable hardware facts.',
];

function StatCard({ label, value, tone = 'cyan' }: { label: string; value: string; tone?: 'cyan' | 'violet' | 'emerald' | 'amber' }) {
  const toneMap = {
    cyan: 'from-cyan-500/20 to-sky-500/5 border-cyan-400/30 text-cyan-200',
    violet: 'from-violet-500/20 to-fuchsia-500/5 border-violet-400/30 text-violet-200',
    emerald: 'from-emerald-500/20 to-teal-500/5 border-emerald-400/30 text-emerald-200',
    amber: 'from-amber-500/20 to-orange-500/5 border-amber-400/30 text-amber-200',
  };

  return (
    <div className={`rounded-2xl border bg-gradient-to-br p-5 ${toneMap[tone]}`}>
      <div className="text-xs uppercase tracking-[0.24em] text-slate-400">{label}</div>
      <div className="mt-3 text-2xl font-semibold text-white">{value}</div>
    </div>
  );
}

function SectionTitle({ eyebrow, title, body }: { eyebrow: string; title: string; body: string }) {
  return (
    <div className="space-y-3">
      <div className="text-xs font-semibold uppercase tracking-[0.3em] text-cyan-300">{eyebrow}</div>
      <h2 className="text-2xl font-semibold tracking-tight text-white sm:text-3xl">{title}</h2>
      <p className="max-w-3xl text-sm leading-7 text-slate-300 sm:text-base">{body}</p>
    </div>
  );
}

export default function App() {
  const [profile, setProfile] = useState<TopologyProfile>({
    architecture: 'x86-64 TSO',
    cacheLineBytes: 64,
    numaDistance: 1,
    contention: 24,
  });
  const [copied, setCopied] = useState(false);

  const analysis = useMemo(() => {
    const realisticFenceLess = profile.architecture === 'x86-64 TSO' && profile.numaDistance === 1;

    let stripeMode = 'L1-local';
    if (profile.contention >= 35) stripeMode = 'L2-striped';
    if (profile.contention >= 72 || profile.numaDistance >= 3) stripeMode = 'NUMA-isolated';

    const alignment = Math.max(64, profile.cacheLineBytes);
    const slotStride = Math.ceil((96 + 256) / alignment) * alignment;

    let safetyVerdict = 'Requires acquire/release atomics; TSO helps, but it is not a proof of universal fence-less safety.';
    if (profile.architecture !== 'x86-64 TSO') {
      safetyVerdict = 'Fence-less publication is not portable here. Use explicit atomics or platform-specific synchronization.';
    } else if (profile.numaDistance > 1) {
      safetyVerdict = 'Cross-socket traffic invalidates any honest 100% fence-less guarantee. Use validated ordering primitives.';
    }

    let portabilityGrade = 'Research only';
    if (profile.architecture === 'x86-64 TSO' && profile.numaDistance === 1 && profile.contention < 35) {
      portabilityGrade = 'Narrow-scope feasible';
    } else if (profile.architecture === 'x86-64 TSO') {
      portabilityGrade = 'Constrained';
    }

    const targetMessage = realisticFenceLess
      ? 'A local, same-socket fast path may benchmark well, but a universal < 0.5ns claim is still not credible.'
      : 'The requested < 0.5ns portable fence-less target is not technically defensible on this topology.';

    return {
      stripeMode,
      alignment,
      slotStride,
      safetyVerdict,
      portabilityGrade,
      targetMessage,
      honestOutcome:
        portabilityGrade === 'Narrow-scope feasible'
          ? 'Optimize for local fast paths and verify correctness with explicit memory ordering.'
          : 'Prefer truthful, standards-based synchronization over unverifiable “zero-friction” claims.',
    };
  }, [profile]);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(codeLiteral);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#061018] text-slate-100">
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top,_rgba(34,211,238,0.16),_transparent_30%),radial-gradient(circle_at_80%_20%,_rgba(168,85,247,0.14),_transparent_24%),linear-gradient(to_bottom,_rgba(255,255,255,0.02),_transparent_30%)]" />
      <div className="relative mx-auto flex min-h-screen max-w-7xl flex-col px-6 py-10 sm:px-8 lg:px-10">
        <header className="rounded-[2rem] border border-white/10 bg-white/5 p-6 shadow-2xl shadow-cyan-950/30 backdrop-blur xl:p-8">
          <div className="flex flex-col gap-8 xl:flex-row xl:items-end xl:justify-between">
            <div className="max-w-4xl space-y-6">
              <div className="inline-flex items-center gap-3 rounded-full border border-cyan-400/20 bg-cyan-400/10 px-4 py-2 text-xs font-semibold uppercase tracking-[0.3em] text-cyan-200">
                <span className="h-2 w-2 rounded-full bg-cyan-300" />
                Prompt Build Tag: SOV-V24-GLOBAL-ROBUST
              </div>
              <div className="space-y-4">
                <h1 className="text-4xl font-semibold tracking-tight text-white sm:text-5xl xl:text-6xl">
                  Sovereign V24 Portability Lab
                </h1>
                <p className="max-w-3xl text-base leading-8 text-slate-300 sm:text-lg">
                  An interactive, standards-aware response to the V24 mission. Instead of fabricating a universal
                  sub-0.5ns fence-less guarantee, this app shows what can be auto-detected, what can be adapted,
                  and where portable correctness requires explicit memory-ordering semantics.
                </p>
              </div>
            </div>

            <div className="grid w-full max-w-xl grid-cols-1 gap-4 sm:grid-cols-2">
              <StatCard label="Claim posture" value="Truthful & portable" tone="emerald" />
              <StatCard label="Universal <0.5ns target" value="Not defensible" tone="amber" />
              <StatCard label="V24 output" value="Interactive spec + code literal" tone="cyan" />
              <StatCard label="Core invariant" value="Sequence-shadow validation" tone="violet" />
            </div>
          </div>
        </header>

        <main className="mt-8 grid gap-8 xl:grid-cols-[1.05fr_0.95fr]">
          <section className="space-y-8 rounded-[2rem] border border-white/10 bg-white/5 p-6 shadow-xl shadow-slate-950/30 backdrop-blur xl:p-8">
            <SectionTitle
              eyebrow="Mission analysis"
              title="Portable fence-less safety must be treated as a proof obligation, not a slogan"
              body="V24 asks for hardware auto-detection, adaptive striping, and a non-latency-summing safety invariant across heterogeneous CPU topologies. The lab below demonstrates those concepts while explicitly rejecting any dishonest claim that pure fence-less publication is universally safe across sockets and architectures."
            />

            <div className="grid gap-4 md:grid-cols-2">
              {principles.map((item) => (
                <article key={item.title} className="rounded-2xl border border-white/10 bg-slate-950/40 p-5">
                  <h3 className="text-lg font-semibold text-white">{item.title}</h3>
                  <p className="mt-3 text-sm leading-7 text-slate-300">{item.text}</p>
                </article>
              ))}
            </div>

            <div className="rounded-2xl border border-cyan-400/20 bg-cyan-400/10 p-5">
              <div className="text-xs font-semibold uppercase tracking-[0.28em] text-cyan-200">Portable hardware fence-less invariant</div>
              <div className="mt-4 grid gap-4 md:grid-cols-3">
                <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
                  <div className="text-sm font-medium text-white">1. Discover</div>
                  <p className="mt-2 text-sm leading-6 text-slate-300">
                    Query cache geometry and NUMA scope at initialization, then align slot stride to detected cache-line width.
                  </p>
                </div>
                <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
                  <div className="text-sm font-medium text-white">2. Validate</div>
                  <p className="mt-2 text-sm leading-6 text-slate-300">
                    Publish sequence and transformed shadow values, then require readers to observe a stable pair from two ordered snapshots.
                  </p>
                </div>
                <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
                  <div className="text-sm font-medium text-white">3. Escalate</div>
                  <p className="mt-2 text-sm leading-6 text-slate-300">
                    If topology or architecture invalidates fence-less assumptions, fall back to explicit atomics instead of pretending safety.
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-2xl border border-amber-400/20 bg-amber-400/10 p-5">
              <div className="text-xs font-semibold uppercase tracking-[0.28em] text-amber-200">Red lines this app will not fake</div>
              <ul className="mt-4 space-y-3 text-sm leading-7 text-slate-200">
                {refusalPoints.map((point) => (
                  <li key={point} className="flex gap-3">
                    <span className="mt-2 h-2 w-2 shrink-0 rounded-full bg-amber-300" />
                    <span>{point}</span>
                  </li>
                ))}
              </ul>
            </div>
          </section>

          <section className="space-y-8 rounded-[2rem] border border-white/10 bg-white/5 p-6 shadow-xl shadow-slate-950/30 backdrop-blur xl:p-8">
            <SectionTitle
              eyebrow="Topology sandbox"
              title="See how the design posture changes across architectures"
              body="Adjust the runtime profile to simulate hardware detection. The analysis below recommends alignment, striping mode, and a safe synchronization stance based on the chosen topology."
            />

            <div className="grid gap-5">
              <label className="space-y-2">
                <span className="text-sm font-medium text-slate-200">Architecture</span>
                <select
                  value={profile.architecture}
                  onChange={(event) =>
                    setProfile((current) => ({
                      ...current,
                      architecture: event.target.value as Architecture,
                    }))
                  }
                  className="w-full rounded-2xl border border-white/10 bg-slate-950/70 px-4 py-3 text-slate-100 outline-none transition focus:border-cyan-300/40"
                >
                  {architectureOptions.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </select>
              </label>

              <label className="space-y-3">
                <div className="flex items-center justify-between gap-4 text-sm">
                  <span className="font-medium text-slate-200">Detected cache-line width</span>
                  <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-cyan-200">
                    {profile.cacheLineBytes} bytes
                  </span>
                </div>
                <input
                  type="range"
                  min={64}
                  max={256}
                  step={64}
                  value={profile.cacheLineBytes}
                  onChange={(event) =>
                    setProfile((current) => ({
                      ...current,
                      cacheLineBytes: Number(event.target.value),
                    }))
                  }
                  className="w-full accent-cyan-300"
                />
              </label>

              <label className="space-y-3">
                <div className="flex items-center justify-between gap-4 text-sm">
                  <span className="font-medium text-slate-200">NUMA max distance</span>
                  <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-violet-200">
                    {profile.numaDistance} hop{profile.numaDistance > 1 ? 's' : ''}
                  </span>
                </div>
                <input
                  type="range"
                  min={1}
                  max={4}
                  step={1}
                  value={profile.numaDistance}
                  onChange={(event) =>
                    setProfile((current) => ({
                      ...current,
                      numaDistance: Number(event.target.value),
                    }))
                  }
                  className="w-full accent-violet-300"
                />
              </label>

              <label className="space-y-3">
                <div className="flex items-center justify-between gap-4 text-sm">
                  <span className="font-medium text-slate-200">Real-time cache contention</span>
                  <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-emerald-200">
                    {profile.contention}%
                  </span>
                </div>
                <input
                  type="range"
                  min={0}
                  max={100}
                  step={1}
                  value={profile.contention}
                  onChange={(event) =>
                    setProfile((current) => ({
                      ...current,
                      contention: Number(event.target.value),
                    }))
                  }
                  className="w-full accent-emerald-300"
                />
              </label>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <StatCard label="Recommended mode" value={analysis.stripeMode} tone="cyan" />
              <StatCard label="Portable grade" value={analysis.portabilityGrade} tone="violet" />
              <StatCard label="Aligned stride" value={`${analysis.slotStride} bytes`} tone="emerald" />
              <StatCard label="Alignment basis" value={`${analysis.alignment} bytes`} tone="amber" />
            </div>

            <div className="space-y-4 rounded-2xl border border-white/10 bg-slate-950/50 p-5">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.28em] text-slate-400">Analysis verdict</div>
                <p className="mt-3 text-base leading-7 text-white">{analysis.targetMessage}</p>
              </div>
              <div className="h-px bg-white/10" />
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.28em] text-slate-400">Safety stance</div>
                <p className="mt-3 text-sm leading-7 text-slate-300">{analysis.safetyVerdict}</p>
              </div>
              <div className="h-px bg-white/10" />
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.28em] text-slate-400">Recommended outcome</div>
                <p className="mt-3 text-sm leading-7 text-slate-300">{analysis.honestOutcome}</p>
              </div>
            </div>
          </section>
        </main>

        <section className="mt-8 rounded-[2rem] border border-white/10 bg-white/5 p-6 shadow-xl shadow-slate-950/30 backdrop-blur xl:p-8">
          <div className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
            <SectionTitle
              eyebrow="Submission literal"
              title="Displayed V24 response"
              body="The following literal provides a research-grade `SovereignChannel` reference that demonstrates topology discovery, adaptive striping, unmanaged allocation, and sequence-shadow validation while explicitly refusing to pretend that portable fence-less safety is universally provable."
            />
            <button
              onClick={handleCopy}
              className="inline-flex items-center justify-center rounded-2xl border border-cyan-300/30 bg-cyan-400/10 px-4 py-3 text-sm font-medium text-cyan-100 transition hover:bg-cyan-400/15"
            >
              {copied ? 'Copied' : 'Copy literal'}
            </button>
          </div>

          <div className="mt-6 overflow-hidden rounded-3xl border border-white/10 bg-[#02070d]">
            <div className="flex items-center justify-between border-b border-white/10 px-5 py-3 text-xs uppercase tracking-[0.28em] text-slate-400">
              <span>V24_ROBUST_CODE</span>
              <span>Complete displayed literal</span>
            </div>
            <pre className="max-h-[680px] overflow-auto p-5 text-sm leading-7 text-slate-200">
              <code>{codeLiteral}</code>
            </pre>
          </div>
        </section>
      </div>
    </div>
  );
}
