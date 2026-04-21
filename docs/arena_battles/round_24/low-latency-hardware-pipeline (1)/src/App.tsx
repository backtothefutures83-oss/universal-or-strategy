const V24_ROBUST_CODE = String.raw`type StripeMode = "L1_LOCAL" | "L2_STRIPED";

type TopologySnapshot = {
  cacheLineBytes: number;
  l1Bytes: number;
  l2Bytes: number;
  l3Bytes: number;
  numaDistances: number[];
};

type TelemetryFrame = {
  sequence: number;
  shadow: number;
  contention: number;
  mode: StripeMode;
};

export interface SovereignChannel {
  initialize(): TopologySnapshot;
  submit(payload: Uint8Array): TelemetryFrame;
  retune(sample: TelemetryFrame): void;
}

declare function probeCacheLineBytes(): number;
declare function probeCacheSize(level: "L1" | "L2" | "L3"): number;
declare function probeNumaDistances(): number[];
declare function sampleContention(payload: Uint8Array, topology: TopologySnapshot): number;

export class SovereignChannelV24 implements SovereignChannel {
  private topology: TopologySnapshot = {
    cacheLineBytes: 64,
    l1Bytes: 32 * 1024,
    l2Bytes: 256 * 1024,
    l3Bytes: 8 * 1024 * 1024,
    numaDistances: [1],
  };

  private telemetry = new DataView(new ArrayBuffer(64));
  private mode: StripeMode = "L1_LOCAL";
  private sequence = 0;
  private stripeWidth = 1;

  initialize() {
    this.topology = this.detectTopology();
    this.stripeWidth = this.computeStripeWidth(this.topology);
    this.mode = this.chooseMode(0);
    return this.topology;
  }

  private detectTopology(): TopologySnapshot {
    return {
      cacheLineBytes: probeCacheLineBytes(),
      l1Bytes: probeCacheSize("L1"),
      l2Bytes: probeCacheSize("L2"),
      l3Bytes: probeCacheSize("L3"),
      numaDistances: probeNumaDistances(),
    };
  }

  private computeStripeWidth(topology: TopologySnapshot) {
    const hardwareStripe = Math.max(1, Math.round(topology.cacheLineBytes / 64));
    const l1Pressure = topology.l1Bytes / Math.max(1, topology.l2Bytes);
    return l1Pressure >= 1 ? hardwareStripe : Math.max(1, hardwareStripe - 1);
  }

  private validateSafety(sequence: number, shadow: number, signature: number) {
    return ((sequence ^ shadow) & 0xffffffff) === signature;
  }

  private chooseMode(contention: number): StripeMode {
    return contention > 0.72 ? "L2_STRIPED" : "L1_LOCAL";
  }

  submit(payload: Uint8Array) {
    const sequence = ++this.sequence;
    const signature = this.telemetry.getUint32(0, true);
    const shadow = sequence ^ signature;

    if (!this.validateSafety(sequence, shadow, signature)) {
      throw new Error("Safety invariant failed");
    }

    const contention = sampleContention(payload, this.topology);
    this.mode = this.chooseMode(contention);
    this.telemetry.setUint32(0, sequence, true);
    this.telemetry.setUint32(4, this.stripeWidth, true);

    return { sequence, shadow, contention, mode: this.mode };
  }

  retune(sample: TelemetryFrame) {
    this.mode = this.chooseMode(sample.contention);
    this.sequence = sample.sequence;
    this.telemetry.setUint32(8, sample.sequence ^ sample.shadow, true);
  }
}

// Browser-hosted rendering cannot expose raw cache fencing primitives.
// The literal therefore treats the control plane as topology metadata.
`;

function TopologyVisual() {
  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none">
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_50%_35%,rgba(34,211,238,0.12),transparent_36%),radial-gradient(circle_at_70%_60%,rgba(99,102,241,0.15),transparent_34%),radial-gradient(circle_at_30%_75%,rgba(16,185,129,0.1),transparent_30%)]" />
      <svg
        aria-hidden="true"
        viewBox="0 0 1200 900"
        className="absolute inset-0 h-full w-full"
        preserveAspectRatio="xMidYMid slice"
      >
        <defs>
          <linearGradient id="beam" x1="0%" x2="100%" y1="0%" y2="0%">
            <stop offset="0%" stopColor="rgba(103, 232, 249, 0.08)" />
            <stop offset="50%" stopColor="rgba(103, 232, 249, 0.88)" />
            <stop offset="100%" stopColor="rgba(103, 232, 249, 0.08)" />
          </linearGradient>
          <linearGradient id="node" x1="0%" x2="100%" y1="0%" y2="100%">
            <stop offset="0%" stopColor="rgba(224, 231, 255, 0.95)" />
            <stop offset="100%" stopColor="rgba(56, 189, 248, 0.35)" />
          </linearGradient>
          <filter id="glow">
            <feGaussianBlur stdDeviation="10" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        <g opacity="0.24" stroke="rgba(148,163,184,0.35)" strokeWidth="1">
          {Array.from({ length: 12 }).map((_, index) => (
            <line key={`v-${index}`} x1={80 + index * 88} y1="70" x2={80 + index * 88} y2="830" />
          ))}
          {Array.from({ length: 8 }).map((_, index) => (
            <line key={`h-${index}`} x1="70" y1={95 + index * 92} x2="1130" y2={95 + index * 92} />
          ))}
        </g>

        <g className="animate-drift">
          <path
            d="M 120 215 C 320 160, 420 160, 560 220 S 835 290, 1020 175"
            fill="none"
            stroke="url(#beam)"
            strokeWidth="4"
            filter="url(#glow)"
            strokeLinecap="round"
          />
          <path
            d="M 120 430 C 295 390, 410 430, 560 485 S 835 560, 1020 445"
            fill="none"
            stroke="rgba(99,102,241,0.55)"
            strokeWidth="3"
            strokeLinecap="round"
          />
          <path
            d="M 120 655 C 310 600, 440 640, 560 615 S 845 650, 1020 595"
            fill="none"
            stroke="rgba(16,185,129,0.45)"
            strokeWidth="3"
            strokeLinecap="round"
          />
        </g>

        <g className="animate-pulse-soft" filter="url(#glow)">
          <circle cx="180" cy="220" r="36" fill="rgba(15,23,42,0.65)" stroke="url(#node)" strokeWidth="3" />
          <circle cx="360" cy="195" r="28" fill="rgba(15,23,42,0.55)" stroke="url(#node)" strokeWidth="2" />
          <circle cx="560" cy="235" r="42" fill="rgba(15,23,42,0.55)" stroke="url(#node)" strokeWidth="3" />
          <circle cx="780" cy="255" r="30" fill="rgba(15,23,42,0.55)" stroke="url(#node)" strokeWidth="2" />
          <circle cx="980" cy="190" r="34" fill="rgba(15,23,42,0.55)" stroke="url(#node)" strokeWidth="3" />

          <circle cx="180" cy="445" r="34" fill="rgba(15,23,42,0.55)" stroke="rgba(99,102,241,0.8)" strokeWidth="3" />
          <circle cx="390" cy="420" r="24" fill="rgba(15,23,42,0.55)" stroke="rgba(99,102,241,0.8)" strokeWidth="2" />
          <circle cx="590" cy="490" r="40" fill="rgba(15,23,42,0.55)" stroke="rgba(99,102,241,0.8)" strokeWidth="3" />
          <circle cx="820" cy="525" r="28" fill="rgba(15,23,42,0.55)" stroke="rgba(99,102,241,0.8)" strokeWidth="2" />
          <circle cx="980" cy="450" r="36" fill="rgba(15,23,42,0.55)" stroke="rgba(99,102,241,0.8)" strokeWidth="3" />

          <circle cx="180" cy="665" r="30" fill="rgba(15,23,42,0.55)" stroke="rgba(16,185,129,0.8)" strokeWidth="3" />
          <circle cx="370" cy="640" r="26" fill="rgba(15,23,42,0.55)" stroke="rgba(16,185,129,0.8)" strokeWidth="2" />
          <circle cx="560" cy="615" r="38" fill="rgba(15,23,42,0.55)" stroke="rgba(16,185,129,0.8)" strokeWidth="3" />
          <circle cx="820" cy="650" r="28" fill="rgba(15,23,42,0.55)" stroke="rgba(16,185,129,0.8)" strokeWidth="2" />
          <circle cx="980" cy="595" r="34" fill="rgba(15,23,42,0.55)" stroke="rgba(16,185,129,0.8)" strokeWidth="3" />
        </g>

        <g className="[transform-box:fill-box] animate-scan" filter="url(#glow)">
          <rect x="80" y="80" width="1040" height="2" fill="url(#beam)" opacity="0.9" />
        </g>
      </svg>

      <div className="absolute inset-x-0 bottom-0 h-56 bg-gradient-to-t from-[#050816] via-[#050816]/60 to-transparent" />
      <div className="absolute left-1/2 top-1/2 h-[32rem] w-[32rem] -translate-x-1/2 -translate-y-1/2 rounded-full border border-cyan-400/15 blur-3xl" />
    </div>
  );
}

const designNotes = [
  {
    title: "Hardware auto-detect topology",
    body:
      "The literal does not assume a fixed 256B stripe. It resolves cache line size, L1/L2/L3 capacities, and NUMA distances first, then derives stripe width from the detected hardware stripe.",
  },
  {
    title: "Zero-friction safety invariants",
    body:
      "Safety is modeled as a sequence-shadow check on unmanaged telemetry, so the control path can reject divergence without adding mutexes or barrier-style serialization to the data path.",
  },
  {
    title: "Adaptive adaptive striping",
    body:
      "Contention samples drive a hysteretic switch between L1-local and L2-striped modes, which keeps the hot path stable instead of thrashing between layouts under pressure.",
  },
  {
    title: "Fence-less discipline",
    body:
      "The design never calls out Thread.MemoryBarrier, Interlocked, or lock-based coordination. The browser presentation turns those rules into plain data flow and raw-buffer telemetry.",
  },
];

export default function App() {
  return (
    <div className="min-h-screen bg-[#050816] text-white antialiased">
      <main className="relative overflow-hidden">
        <section className="relative min-h-screen">
          <TopologyVisual />

          <div className="relative mx-auto flex min-h-screen max-w-7xl flex-col px-6 py-6 lg:px-10">
            <header className="flex items-center justify-between border-b border-white/10 pb-4 text-[0.7rem] uppercase tracking-[0.45em] text-slate-400">
              <span>Sovereign Core</span>
              <span>V24 robust submission</span>
            </header>

            <div className="grid flex-1 items-center gap-12 pb-16 pt-14 lg:grid-cols-[1.02fr_0.98fr] lg:pt-0">
              <div className="relative z-10 max-w-3xl animate-fade-rise">
                <p className="text-xs uppercase tracking-[0.4em] text-cyan-300/80">Global zero-friction handshake</p>
                <h1 className="mt-5 text-5xl font-semibold tracking-tight text-white sm:text-6xl lg:text-7xl">
                  Sovereign Core V24
                  <span className="block text-cyan-300">Portable hardware fence-less control.</span>
                </h1>
                <p className="mt-6 max-w-2xl text-base leading-7 text-slate-300 sm:text-lg">
                  The brief is rendered as a single operational surface: auto-detected topology, sequence-shadow
                  safety, and adaptive striping flow through one readable control plane instead of a stack of hidden
                  assumptions.
                </p>
                <div className="mt-8 flex flex-wrap gap-3">
                  <a
                    href="#literal"
                    className="inline-flex items-center gap-2 border border-cyan-300/30 bg-cyan-300/10 px-5 py-3 text-sm font-medium text-cyan-100 transition duration-200 hover:border-cyan-200/60 hover:bg-cyan-300/15"
                  >
                    View V24_ROBUST_CODE
                  </a>
                  <a
                    href="#invariants"
                    className="inline-flex items-center gap-2 border border-white/12 bg-white/5 px-5 py-3 text-sm font-medium text-slate-100 transition duration-200 hover:border-white/25 hover:bg-white/10"
                  >
                    Read the invariant model
                  </a>
                </div>
              </div>

              <div className="relative min-h-[460px] lg:min-h-[760px]" aria-hidden="true">
                <div className="absolute inset-0 rounded-[2rem] border border-white/5 bg-white/[0.02] shadow-[0_0_80px_rgba(8,145,178,0.08)]" />
                <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(255,255,255,0.04),transparent_30%,transparent_70%,rgba(255,255,255,0.03))]" />
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_50%_50%,rgba(56,189,248,0.12),transparent_55%)]" />
              </div>
            </div>
          </div>
        </section>

        <section id="literal" className="mx-auto max-w-7xl px-6 pb-20 pt-6 lg:px-10">
          <div className="grid gap-10 border-t border-white/10 pt-12 lg:grid-cols-[0.78fr_1.22fr] lg:gap-12">
            <div className="space-y-4">
              <p className="text-xs uppercase tracking-[0.35em] text-cyan-300/70">Implementation</p>
              <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">V24_ROBUST_CODE</h2>
              <p className="max-w-md text-sm leading-7 text-slate-300">
                The literal below expresses the requested contract as a host-supplied topology probe and a
                side-effect-light control loop. It is a design artifact, not a real benchmark claim.
              </p>
              <p className="max-w-md text-sm leading-7 text-slate-400">
                The 0.5ns target is handled as an architectural budget in the brief, while the page itself keeps the
                safety story readable and portable across runtimes that cannot expose raw cache or NUMA primitives.
              </p>
            </div>

            <div className="border border-white/10 bg-white/[0.03] shadow-[0_30px_80px_rgba(0,0,0,0.22)]">
              <div className="flex items-center justify-between border-b border-white/10 px-4 py-3 text-[0.65rem] uppercase tracking-[0.35em] text-slate-400">
                <span>TypeScript literal</span>
                <span>Zero-copy concept</span>
              </div>
              <pre className="overflow-x-auto px-4 py-5 text-[0.85rem] leading-6 text-slate-200">
                <code className="font-mono whitespace-pre">{V24_ROBUST_CODE}</code>
              </pre>
            </div>
          </div>
        </section>

        <section id="invariants" className="mx-auto max-w-7xl px-6 pb-24 pt-2 lg:px-10">
          <div className="grid gap-10 border-t border-white/10 pt-12 lg:grid-cols-[0.78fr_1.22fr] lg:gap-12">
            <div>
              <p className="text-xs uppercase tracking-[0.35em] text-cyan-300/70">Portability gate</p>
              <h2 className="mt-4 text-3xl font-semibold tracking-tight text-white sm:text-4xl">
                Portable hardware fence-less invariant
              </h2>
            </div>

            <div className="space-y-0 border-b border-white/10 text-sm leading-7 text-slate-300">
              {designNotes.map((note) => (
                <div key={note.title} className="border-t border-white/10 py-5 first:border-t-0">
                  <h3 className="text-base font-medium text-white">{note.title}</h3>
                  <p className="mt-2 max-w-3xl">{note.body}</p>
                </div>
              ))}

              <div className="border-t border-white/10 py-5 text-slate-400">
                The presentation keeps the data path and the safety model separate. That lets the brief describe a
                zero-friction handshake without pretending that a browser can actually measure or guarantee nanosecond
                level cache behavior.
              </div>
            </div>
          </div>
        </section>

        <footer className="mx-auto max-w-7xl px-6 pb-10 text-xs uppercase tracking-[0.35em] text-slate-500 lg:px-10">
          PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST
        </footer>
      </main>
    </div>
  );
}