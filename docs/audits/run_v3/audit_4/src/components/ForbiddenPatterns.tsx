const BANNED = [
  {
    pattern: "Workers / postMessage",
    reason: "Standard serialization overhead ~40µs — 4× the hard gate",
    cost: "~40µs",
    icon: "🚫",
    color: "text-red-400",
    border: "border-red-500/20",
    bg: "bg-red-950/20",
  },
  {
    pattern: "Internal Locks (mutex / spinlock)",
    reason: "Kernel futex(2) contention on CAS failure → unbounded jitter",
    cost: "~2–200µs",
    icon: "🔒",
    color: "text-red-400",
    border: "border-red-500/20",
    bg: "bg-red-950/20",
  },
  {
    pattern: "Structured Cloning",
    reason: "Deep object graph copy — O(n) time + GC pressure per message",
    cost: "~15–60µs",
    icon: "📠",
    color: "text-red-400",
    border: "border-red-500/20",
    bg: "bg-red-950/20",
  },
  {
    pattern: "SLUB Heap Alloc per Send",
    reason: "Each kmalloc touches the SLUB free-list under spinlock",
    cost: "~1–4µs",
    icon: "🗑️",
    color: "text-red-400",
    border: "border-red-500/20",
    bg: "bg-red-950/20",
  },
  {
    pattern: "futex(2) / nanosleep wait",
    reason: "Minimum kernel wakeup granularity ~50µs (tickless: ~4µs worst)",
    cost: "~50µs",
    icon: "💤",
    color: "text-red-400",
    border: "border-red-500/20",
    bg: "bg-red-950/20",
  },
  {
    pattern: "GC Pauses (JS runtime)",
    reason: "V8 incremental GC can pause 1–20ms — catastrophic for real-time",
    cost: "~1–20ms",
    icon: "♻️",
    color: "text-red-400",
    border: "border-red-500/20",
    bg: "bg-red-950/20",
  },
];

const ALLOWED = [
  {
    pattern: "Atomics.load / store",
    reason: "Acquire-release fence — no syscall, no kernel mode flip",
    cost: "~5ns",
    icon: "⚡",
    color: "text-emerald-400",
    border: "border-emerald-500/20",
    bg: "bg-emerald-950/20",
  },
  {
    pattern: "SharedArrayBuffer SAB slice",
    reason: "Zero-copy view into pre-committed slab — no allocation",
    cost: "~2ns",
    icon: "🔬",
    color: "text-emerald-400",
    border: "border-emerald-500/20",
    bg: "bg-emerald-950/20",
  },
  {
    pattern: "PAUSE / cpu_relax() hint",
    reason: "Spin-poll back-off — keeps branch predictor warm, ~5ns cost",
    cost: "~5ns",
    icon: "⏸️",
    color: "text-emerald-400",
    border: "border-emerald-500/20",
    bg: "bg-emerald-950/20",
  },
  {
    pattern: "Slab alloc (bitmap CAS)",
    reason: "Pre-committed pages, O(1) bit-scan — bypasses kernel heap",
    cost: "~40ns",
    icon: "🧱",
    color: "text-emerald-400",
    border: "border-emerald-500/20",
    bg: "bg-emerald-950/20",
  },
];

export default function ForbiddenPatterns() {
  return (
    <section className="max-w-5xl mx-auto px-4 py-8">
      <div className="text-center mb-8">
        <h2 className="text-3xl font-black text-white mb-2">
          Banned vs. Sovereign Patterns
        </h2>
        <p className="text-slate-500 text-sm">
          Every banned pattern is a latency cliff. Every sovereign pattern is a physics fact.
        </p>
      </div>

      <div className="grid md:grid-cols-2 gap-8">
        {/* Banned */}
        <div>
          <div className="flex items-center gap-2 mb-4">
            <div className="h-1 w-6 rounded bg-red-500" />
            <span className="text-sm font-bold text-red-400 uppercase tracking-widest">
              BANNED — Human-Grade Patterns
            </span>
          </div>
          <div className="space-y-3">
            {BANNED.map((b) => (
              <div
                key={b.pattern}
                className={`rounded-xl border ${b.border} ${b.bg} p-4 flex items-start gap-3`}
              >
                <span className="text-xl">{b.icon}</span>
                <div className="flex-1 min-w-0">
                  <div className={`font-bold text-sm ${b.color} line-through decoration-red-700`}>
                    {b.pattern}
                  </div>
                  <div className="text-xs text-slate-500 mt-0.5">{b.reason}</div>
                </div>
                <div className="text-xs font-mono font-bold text-red-500 whitespace-nowrap">
                  {b.cost}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Allowed */}
        <div>
          <div className="flex items-center gap-2 mb-4">
            <div className="h-1 w-6 rounded bg-emerald-500" />
            <span className="text-sm font-bold text-emerald-400 uppercase tracking-widest">
              SOVEREIGN — Physics-Level Primitives
            </span>
          </div>
          <div className="space-y-3">
            {ALLOWED.map((a) => (
              <div
                key={a.pattern}
                className={`rounded-xl border ${a.border} ${a.bg} p-4 flex items-start gap-3`}
              >
                <span className="text-xl">{a.icon}</span>
                <div className="flex-1 min-w-0">
                  <div className={`font-bold text-sm ${a.color}`}>{a.pattern}</div>
                  <div className="text-xs text-slate-500 mt-0.5">{a.reason}</div>
                </div>
                <div className="text-xs font-mono font-bold text-emerald-400 whitespace-nowrap">
                  {a.cost}
                </div>
              </div>
            ))}
          </div>

          {/* Physics explanation */}
          <div className="mt-4 rounded-xl border border-slate-700 bg-slate-900/60 p-4">
            <div className="text-xs font-bold text-slate-400 uppercase tracking-widest mb-2">
              Physics of the Cache
            </div>
            <div className="space-y-1.5 text-xs text-slate-500 font-mono">
              {[
                ["L1 hit (4 cycles @ 3GHz)", "~1.3ns"],
                ["L2 hit (12 cycles)", "~4ns"],
                ["L3 hit (40 cycles)", "~13ns"],
                ["DRAM (200 cycles)", "~67ns"],
                ["Kernel round-trip (futex)", "~2000ns"],
                ["Context switch (full)", "~10 000ns"],
              ].map(([desc, cost]) => (
                <div key={desc} className="flex justify-between">
                  <span className="text-slate-600">{desc}</span>
                  <span className="text-sky-400">{cost}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
