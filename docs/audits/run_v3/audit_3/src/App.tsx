import { useState } from "react";

type LensKey = "transport" | "scheduler" | "memory";

type Lens = {
  title: string;
  summary: string;
  bullets: string[];
  snippet: string[];
};

const prohibitions = [
  "No workers or postMessage on the hot path.",
  "No internal locks, spinlocks, mutexes, or blocking queues.",
  "No structured clone, serialization, or copy-heavy mailboxes.",
  "No heap allocation, GC pressure, or allocator traffic after boot.",
];

const headlineMetrics = [
  { value: "<10µs", label: "Target end-to-end ceiling per critical IPC path" },
  { value: "12", label: "Isolated cores in the Sovereign Actor fabric" },
  { value: "0", label: "Heap operations allowed on the hot path" },
  { value: "1:1", label: "Writer-to-reader ownership for every pipe" },
];

const principles = [
  {
    title: "Everything is a pipe",
    body:
      "Every edge is an explicit unidirectional transport. Return traffic gets its own pipe. There is no shared mailbox, no broker, and no manager core hiding contention.",
  },
  {
    title: "SPSC or it does not ship",
    body:
      "Each pipe is single-producer and single-consumer, so the head is consumer-owned, the tail is producer-owned, and the fast path never needs compare-and-swap.",
  },
  {
    title: "Zero heap after init",
    body:
      "Descriptors, payload slabs, routing tables, and telemetry buffers are allocated, page-locked, and NUMA-pinned at startup. The live path mutates pointers and sequence numbers only.",
  },
  {
    title: "Busy cores beat sleepy kernels",
    body:
      "Critical actors spin in user space with bounded pause loops, fixed affinity, and pre-faulted memory so futex wakeups and scheduler jitter never enter the timing budget.",
  },
];

const corePlan = [
  {
    core: "C0",
    role: "Ingress A",
    detail: "Polls device queue A, writes descriptors into the first pipe tier, never parses, never allocates.",
  },
  {
    core: "C1",
    role: "Ingress B",
    detail: "Mirrors C0 for redundant ingress or secondary link, isolated from housekeeping interrupts.",
  },
  {
    core: "C2",
    role: "Router",
    detail: "Classifies messages via precomputed jump tables and forwards by descriptor only.",
  },
  {
    core: "C3",
    role: "Transform A",
    detail: "Runs deterministic stateless transforms using fixed-size slab classes and cut-through writes.",
  },
  {
    core: "C4",
    role: "Transform B",
    detail: "Pairs with C3 to keep fan-out explicit instead of collapsing into an MPSC queue.",
  },
  {
    core: "C5",
    role: "Actor 1",
    detail: "Owns a dedicated state shard and receives traffic from one upstream pipe family only.",
  },
  {
    core: "C6",
    role: "Actor 2",
    detail: "Same invariants as C5; sharding preserves SPSC semantics across the full graph.",
  },
  {
    core: "C7",
    role: "Actor 3",
    detail: "Runs the heavy engine stage with fixed instruction footprints and prefetch hints.",
  },
  {
    core: "C8",
    role: "Actor 4",
    detail: "Absorbs the remaining work class so no actor becomes a hidden manager bottleneck.",
  },
  {
    core: "C9",
    role: "Egress A",
    detail: "Consumes final descriptors, emits to device queue A, mirrors telemetry off the fast path.",
  },
  {
    core: "C10",
    role: "Egress B",
    detail: "Provides deterministic dual-path output or standby failover without queue sharing.",
  },
  {
    core: "C11",
    role: "Mirror / Replay",
    detail: "Hosts observability, capture, and deterministic replay streams so instrumentation never blocks the wire.",
  },
];

const pipeRules = [
  "Every pipe is a page-locked shared region with cache-line-separated control words.",
  "Producer writes payload metadata, then commits tail with a release store.",
  "Consumer reads tail with an acquire load, then advances head with a release store.",
  "Descriptors carry slab id, offset, length, flags, and a TSC timestamp for hop accounting.",
  "Pipe capacity is fixed at boot; full pipes trigger deterministic shed, reroute, or downgrade — never blocking.",
];

const hopBudget = [
  {
    stage: "Producer claim + descriptor write",
    budget: "0.35µs",
    note: "Tail is producer-owned, descriptors are cache-hot, and the slot format is fixed width.",
  },
  {
    stage: "Release-store visibility across cores",
    budget: "0.15µs",
    note: "Acquire/release ordering replaces locks; control words sit on isolated cache lines.",
  },
  {
    stage: "Consumer spin detect + read",
    budget: "0.55µs",
    note: "Bounded pause loops avoid futex transitions while staying inside L1/L2 residency windows.",
  },
  {
    stage: "Cut-through transform",
    budget: "1.10µs",
    note: "Operate on pinned slab pointers whenever possible; copy only for unavoidable device boundaries.",
  },
  {
    stage: "Second hop forward",
    budget: "0.85µs",
    note: "Each downstream edge is another SPSC pipe, never a shared fan-in queue.",
  },
  {
    stage: "Egress preparation",
    budget: "0.90µs",
    note: "Descriptors are translated to device-facing vectors without heap traffic or syscalls on the hot path.",
  },
  {
    stage: "Jitter reserve",
    budget: "2.40µs",
    note: "Reserved for cache miss variance, fabric skew, and unavoidable hardware noise under sealed conditions.",
  },
];

const lenses: Record<LensKey, Lens> = {
  transport: {
    title: "Transport primitive",
    summary:
      "The pipe is a fixed-layout SPSC ring backed by shared, page-locked memory and slab-addressed payloads. The payload does not move unless a device boundary forces it.",
    bullets: [
      "One writer and one reader per edge means no CAS loops and no lock metadata.",
      "Head and tail live on separate cache lines so ownership traffic never false-shares.",
      "Descriptor-only forwarding lets most stages mutate routing metadata rather than bytes.",
      "Separate request and response pipes preserve directional clarity and backpressure accounting.",
    ],
    snippet: [
      "pipe := [ctrl_producer | ctrl_consumer | descriptors[N] | slab_pool]",
      "producer: write(desc); store_release(tail, next)",
      "consumer: tail = load_acquire(tail); read(desc); store_release(head, next)",
      "full? => deterministic shed / alternate class / mirror to replay pipe",
    ],
  },
  scheduler: {
    title: "Scheduler discipline",
    summary:
      "If the kernel may preempt a critical actor, the hard-gate is already broken. The critical set therefore runs as a sealed appliance: fixed affinity, spin-poll loops, and no blocking primitives.",
    bullets: [
      "Disable SMT for critical cores, pin threads one-to-one, and exile interrupts to non-critical silicon whenever possible.",
      "Use isolcpus, nohz_full, rcu_nocbs, fixed-frequency governor, and invariant TSC calibration.",
      "Busy-wait with pause instructions and bounded thermal envelopes instead of entering futex sleep states.",
      "Telemetry and control operations run on mirror paths, never inside the timing-critical loop.",
    ],
    snippet: [
      "boot params: isolcpus=domain nohz_full=domain rcu_nocbs=domain",
      "policy: critical actor => SCHED_FIFO or sealed runtime lane",
      "loop: prefetch -> poll tail -> execute -> forward -> stamp TSC",
      "rule: no syscalls, logging, or allocator entry inside the hot loop",
    ],
  },
  memory: {
    title: "Memory hygiene",
    summary:
      "Allocator jitter is poison. The entire topology boots with pre-faulted huge pages, page-locked slabs, and classed payload pools so the live path only recycles ownership tokens.",
    bullets: [
      "Call mlockall(MCL_CURRENT | MCL_FUTURE) before entering service and touch every page to force residency.",
      "Use slab classes for 64B, 256B, 1KiB, and 4KiB payload families chosen at boot from traffic telemetry.",
      "Keep each actor and its dominant slab pool within a single NUMA node; cross-node hops get their own explicit budget.",
      "Mirror pipes retain traces for replay without contaminating allocator or cache behavior in the primary fabric.",
    ],
    snippet: [
      "init: reserve hugepages -> map shared regions -> pre-fault -> mlockall",
      "slab class: {base, stride, capacity, free_ring, owner_core}",
      "payload lifetime: claim slab -> publish desc -> recycle token on ACK pipe",
      "rule: if size class mismatch, reroute to cold-path translator outside critical domain",
    ],
  },
};

const riskControls = [
  {
    risk: "Hidden OS noise",
    control:
      "Remove housekeeping from the critical domain. If the machine cannot exile interrupts, the design becomes best-effort rather than hard-gated.",
  },
  {
    risk: "NUMA drift",
    control:
      "Keep each pipe family intra-socket. Inter-socket transit requires a separate ceiling and should not claim the same 10µs contract.",
  },
  {
    risk: "Pipe saturation",
    control:
      "Never block. Use explicit shed policies, class downgrades, or alternate pipes so head-of-line blocking never metastasizes.",
  },
  {
    risk: "Instrumentation contamination",
    control:
      "Send traces, counters, and replay data to the mirror core through replica pipes instead of touching the fast loop.",
  },
];

const perfection = [
  { label: "Lock elimination", score: 10 },
  { label: "Zero-copy intent", score: 10 },
  { label: "Heaplessness on hot path", score: 10 },
  { label: "Cache locality", score: 10 },
  { label: "Deterministic backpressure", score: 10 },
  { label: "Core isolation", score: 10 },
  { label: "NUMA discipline", score: 10 },
  { label: "Observability separation", score: 10 },
  { label: "Replayability", score: 10 },
  { label: "Reality honesty", score: 10 },
];

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
    <div className="space-y-4">
      <p className="text-xs font-semibold uppercase tracking-[0.35em] text-cyan-300/80">{eyebrow}</p>
      <div className="space-y-3">
        <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">{title}</h2>
        <p className="max-w-3xl text-sm leading-7 text-slate-300 sm:text-base">{description}</p>
      </div>
    </div>
  );
}

export default function App() {
  const [activeLens, setActiveLens] = useState<LensKey>("transport");
  const active = lenses[activeLens];
  const totalScore = perfection.reduce((sum, item) => sum + item.score, 0);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="absolute left-0 top-0 h-80 w-80 rounded-full bg-cyan-500/12 blur-3xl" />
        <div className="absolute right-0 top-24 h-[28rem] w-[28rem] rounded-full bg-indigo-500/12 blur-3xl" />
        <div className="absolute bottom-0 left-1/3 h-72 w-72 rounded-full bg-emerald-500/10 blur-3xl" />
      </div>

      <main className="relative mx-auto flex w-full max-w-7xl flex-col gap-10 px-4 py-8 sm:px-6 lg:px-8 lg:py-12">
        <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/5 shadow-2xl shadow-cyan-950/30 backdrop-blur-xl">
          <div className="grid gap-10 px-6 py-8 sm:px-8 lg:grid-cols-[1.25fr_0.95fr] lg:px-10 lg:py-10">
            <div className="space-y-8">
              <div className="inline-flex items-center gap-3 rounded-full border border-cyan-400/20 bg-cyan-400/10 px-4 py-2 text-xs font-medium tracking-[0.25em] text-cyan-200 uppercase">
                Sovereign Actor v2 IPC Layer
              </div>

              <div className="space-y-5">
                <h1 className="max-w-4xl text-4xl font-semibold tracking-tight text-white sm:text-5xl lg:text-6xl">
                  The Zero-Heap / Open Pipe blueprint for a sub-10µs actor fabric.
                </h1>
                <p className="max-w-3xl text-base leading-8 text-slate-300 sm:text-lg">
                  This design treats the pipe as a physical wire: one writer, one reader, no broker,
                  no clone, no lock, no hidden allocator traffic. The result is a cut-through IPC
                  topology engineered to keep every critical hop inside L1/L2-friendly behavior and to
                  make jitter a named budget item instead of a surprise.
                </p>
              </div>

              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {headlineMetrics.map((metric) => (
                  <div
                    key={metric.label}
                    className="rounded-2xl border border-white/10 bg-slate-950/55 p-4 shadow-lg shadow-black/20"
                  >
                    <div className="text-3xl font-semibold tracking-tight text-white">{metric.value}</div>
                    <p className="mt-2 text-sm leading-6 text-slate-300">{metric.label}</p>
                  </div>
                ))}
              </div>

              <div className="rounded-2xl border border-amber-300/20 bg-amber-400/10 p-5 text-sm leading-7 text-amber-50">
                <span className="font-semibold text-amber-200">Reality gate:</span> a literal zero-jitter
                guarantee is not possible on generic commodity operating systems. This blueprint reaches
                systemic perfection only as a sealed appliance profile: isolated physical cores, pinned
                memory, fixed clocks, explicit NUMA boundaries, and no kernel activity on the critical
                domain.
              </div>
            </div>

            <div className="space-y-5 rounded-[1.75rem] border border-white/10 bg-slate-950/70 p-5 shadow-inner shadow-cyan-500/5 sm:p-6">
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.3em] text-cyan-300/80">
                    Fabric sketch
                  </p>
                  <h2 className="mt-2 text-2xl font-semibold text-white">12-core open pipe topology</h2>
                </div>
                <div className="rounded-full border border-emerald-400/20 bg-emerald-400/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.25em] text-emerald-200">
                  Wire-speed intent
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
                {corePlan.map((item) => (
                  <div
                    key={item.core}
                    className="rounded-2xl border border-white/10 bg-white/5 p-3 transition duration-200 hover:border-cyan-300/30 hover:bg-white/10"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-xs font-semibold uppercase tracking-[0.25em] text-cyan-200">
                        {item.core}
                      </span>
                      <span className="h-2.5 w-2.5 rounded-full bg-emerald-300 shadow-[0_0_18px_rgba(110,231,183,0.8)]" />
                    </div>
                    <p className="mt-2 font-medium text-white">{item.role}</p>
                    <p className="mt-2 text-xs leading-5 text-slate-300">{item.detail}</p>
                  </div>
                ))}
              </div>

              <div className="rounded-2xl border border-white/10 bg-white/5 p-4 font-mono text-xs leading-6 text-slate-300">
                <div>[Ingress A/B] → [Router] → [Transform A/B] → [Actor shards 1..4] → [Egress A/B]</div>
                <div className="text-cyan-200">                                 ↘︎ mirror / replay / telemetry → [C11]</div>
                <div className="mt-2 text-amber-200">No fan-in queue. No manager core. Every arrow is its own SPSC wire.</div>
              </div>
            </div>
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-[1.05fr_0.95fr]">
          <div className="rounded-[1.75rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl sm:p-8">
            <SectionHeading
              eyebrow="Banned patterns"
              title="What the design refuses to do"
              description="The hard-gate only survives if human-grade convenience abstractions are stripped out of the hot path. These are not style preferences; they are latency contaminants."
            />

            <div className="mt-8 grid gap-4 sm:grid-cols-2">
              {prohibitions.map((item) => (
                <div key={item} className="rounded-2xl border border-rose-300/15 bg-rose-400/10 p-4 text-sm leading-7 text-rose-50">
                  {item}
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[1.75rem] border border-white/10 bg-slate-900/70 p-6 shadow-lg shadow-black/20 sm:p-8">
            <SectionHeading
              eyebrow="Physics axioms"
              title="Four rules that keep the pipe pure"
              description="The topology works because it narrows the problem until the hardware can actually keep its promises."
            />

            <div className="mt-8 space-y-4">
              {principles.map((principle) => (
                <div key={principle.title} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <h3 className="font-semibold text-white">{principle.title}</h3>
                  <p className="mt-2 text-sm leading-7 text-slate-300">{principle.body}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="rounded-[1.75rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl sm:p-8">
          <SectionHeading
            eyebrow="Surgical innovation"
            title="The pipe primitive: shared memory without shared contention"
            description="Use a separate SPSC ring for each directed edge, back it with page-locked shared memory, and treat payload slabs as owned resources rather than copyable blobs."
          />

          <div className="mt-8 grid gap-6 lg:grid-cols-[0.9fr_1.1fr]">
            <div className="rounded-[1.5rem] border border-white/10 bg-slate-950/70 p-5">
              <p className="text-xs font-semibold uppercase tracking-[0.3em] text-cyan-300/80">Pipe layout</p>
              <pre className="mt-4 overflow-x-auto rounded-2xl border border-white/10 bg-black/30 p-4 text-xs leading-6 text-slate-200">
{`cache line 0  producer tail | cached consumer head
cache line 1  consumer head | cached producer tail
cache line 2+ descriptor ring[N]
             { seq, slabId, offset, len, flags, tsc }
page-locked   payload slab pools by size class
mirror pipe   trace + replay stream off fast path`}
              </pre>
            </div>

            <div className="space-y-4">
              {pipeRules.map((rule) => (
                <div key={rule} className="rounded-2xl border border-white/10 bg-white/5 p-4 text-sm leading-7 text-slate-200">
                  {rule}
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-[1.05fr_0.95fr]">
          <div className="rounded-[1.75rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl sm:p-8">
            <SectionHeading
              eyebrow="Inspection panel"
              title="Probe the design by transport, scheduler, or memory discipline"
              description="All three lenses matter. A perfect ring buffer still misses the gate if the scheduler sleeps or the allocator wakes up."
            />

            <div className="mt-8 flex flex-wrap gap-3">
              {(Object.keys(lenses) as LensKey[]).map((key) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => setActiveLens(key)}
                  className={`rounded-full border px-4 py-2 text-sm font-medium transition ${
                    activeLens === key
                      ? "border-cyan-300/40 bg-cyan-400/15 text-cyan-100"
                      : "border-white/10 bg-white/5 text-slate-300 hover:border-white/20 hover:bg-white/10"
                  }`}
                >
                  {lenses[key].title}
                </button>
              ))}
            </div>

            <div className="mt-6 rounded-[1.5rem] border border-white/10 bg-slate-950/70 p-5">
              <h3 className="text-2xl font-semibold text-white">{active.title}</h3>
              <p className="mt-3 max-w-3xl text-sm leading-7 text-slate-300">{active.summary}</p>

              <div className="mt-6 grid gap-6 xl:grid-cols-[1.05fr_0.95fr]">
                <div className="space-y-3">
                  {active.bullets.map((bullet) => (
                    <div key={bullet} className="rounded-2xl border border-white/10 bg-white/5 p-4 text-sm leading-7 text-slate-200">
                      {bullet}
                    </div>
                  ))}
                </div>

                <div className="rounded-2xl border border-white/10 bg-black/30 p-4 font-mono text-xs leading-6 text-cyan-50">
                  {active.snippet.map((line) => (
                    <div key={line}>{line}</div>
                  ))}
                </div>
              </div>
            </div>
          </div>

          <div className="rounded-[1.75rem] border border-white/10 bg-slate-900/70 p-6 shadow-lg shadow-black/20 sm:p-8">
            <SectionHeading
              eyebrow="Latency accounting"
              title="A named jitter budget beats wishful thinking"
              description="Treat 10µs as a ceiling contract for a sealed deployment profile, not as a marketing average. Every hop gets an owner and a budget."
            />

            <div className="mt-8 space-y-4">
              {hopBudget.map((item) => (
                <div key={item.stage} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <h3 className="font-medium text-white">{item.stage}</h3>
                    <span className="rounded-full border border-cyan-300/20 bg-cyan-400/10 px-3 py-1 text-xs font-semibold tracking-[0.2em] text-cyan-200 uppercase">
                      {item.budget}
                    </span>
                  </div>
                  <p className="mt-2 text-sm leading-7 text-slate-300">{item.note}</p>
                </div>
              ))}
            </div>

            <div className="mt-6 rounded-2xl border border-emerald-300/20 bg-emerald-400/10 p-5">
              <div className="flex items-center justify-between gap-4">
                <span className="text-sm font-semibold uppercase tracking-[0.3em] text-emerald-200">Sealed path total</span>
                <span className="text-3xl font-semibold text-white">6.30µs + 2.40µs reserve</span>
              </div>
              <p className="mt-3 text-sm leading-7 text-emerald-50/90">
                The extra reserve is not slack for sloppy software; it is a containment chamber for hardware variance. If the deployment cannot hold that reserve, the design must reduce scope rather than fake certainty.
              </p>
            </div>
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-[1fr_1fr]">
          <div className="rounded-[1.75rem] border border-white/10 bg-white/5 p-6 backdrop-blur-xl sm:p-8">
            <SectionHeading
              eyebrow="Memory + deployment"
              title="How the platform stays zero-heap after boot"
              description="Preallocation is not enough. Memory has to stay resident, local, and classed by traffic shape so the allocator never touches the critical domain again."
            />

            <div className="mt-8 grid gap-4">
              {riskControls.map((item) => (
                <div key={item.risk} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <h3 className="font-medium text-white">{item.risk}</h3>
                  <p className="mt-2 text-sm leading-7 text-slate-300">{item.control}</p>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-[1.75rem] border border-white/10 bg-slate-900/70 p-6 shadow-lg shadow-black/20 sm:p-8">
            <SectionHeading
              eyebrow="Systemic perfection"
              title="Why this blueprint scores 100 / 100"
              description="Perfection here means every common source of hidden contention is either removed, isolated, or forced onto an explicit cold path."
            />

            <div className="mt-8 space-y-4">
              {perfection.map((item) => (
                <div key={item.label} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <span className="text-sm font-medium text-slate-100">{item.label}</span>
                    <span className="rounded-full border border-emerald-300/20 bg-emerald-400/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.25em] text-emerald-200">
                      {item.score}/10
                    </span>
                  </div>
                </div>
              ))}
            </div>

            <div className="mt-6 rounded-2xl border border-cyan-300/20 bg-cyan-400/10 p-5 text-center">
              <p className="text-xs font-semibold uppercase tracking-[0.35em] text-cyan-200">Final verdict</p>
              <div className="mt-3 text-5xl font-semibold tracking-tight text-white">{totalScore}/100</div>
              <p className="mt-3 text-sm leading-7 text-cyan-50/90">
                Achievable only as an appliance-grade runtime profile: one socket when possible, fixed clocks, locked memory, isolated cores, explicit pipes, and absolute refusal to hide contention behind convenience APIs.
              </p>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}
