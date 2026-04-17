const TIMELINE = [
  {
    step: "T+0ns",
    label: "Slab Acquire",
    detail: "Engine A calls slab.acquire() — trailing_zeros() bit-scan on AtomicU64 bitmap + CAS",
    cost: "~40ns",
    color: "#a78bfa",
    phase: "producer",
  },
  {
    step: "T+40ns",
    label: "Payload Write",
    detail: "SIMD memcpy (AVX-256) of message bytes into pre-mapped SAB slot. No malloc. No GC.",
    cost: "~80ns",
    color: "#a78bfa",
    phase: "producer",
  },
  {
    step: "T+120ns",
    label: "HEAD Bump",
    detail: "Atomics.store(ctrl, 0, next) — single 64-bit release store. Visible to all cores via cache coherency protocol (MESI).",
    cost: "~20ns",
    color: "#a78bfa",
    phase: "producer",
  },
  {
    step: "T+140ns",
    label: "Cache Coherency Propagation",
    detail: "MESI protocol propagates the cache-line write to Engine B's L2. Cross-NUMA: add ~50ns. Same CCX: ~10ns.",
    cost: "~10–60ns",
    color: "#38bdf8",
    phase: "transfer",
  },
  {
    step: "T+200ns",
    label: "Spin-Poll Hit",
    detail: "Engine B's spin-poll loop calls Atomics.load(ctrl, 0) — L2 cache hit. Detects HEAD ≠ TAIL. Enter dispatch.",
    cost: "~4–100ns",
    color: "#34d399",
    phase: "consumer",
  },
  {
    step: "T+300ns",
    label: "Zero-Copy Dispatch",
    detail: "Engine B reads SAB view directly — no structured clone, no copy. Slab slot released back via bitmap CAS.",
    cost: "~40ns",
    color: "#34d399",
    phase: "consumer",
  },
  {
    step: "T+340ns",
    label: "Total (typical)",
    detail: "End-to-end wire time on same NUMA node, L2-warm. p99 with cache pressure: 4–8µs.",
    cost: "~340ns",
    color: "#facc15",
    phase: "total",
  },
];

const PHASE_COLORS: Record<string, string> = {
  producer: "#a78bfa",
  transfer: "#38bdf8",
  consumer: "#34d399",
  total:    "#facc15",
};

export default function PhysicsBreakdown() {
  return (
    <section className="max-w-5xl mx-auto px-4 py-8">
      <div className="text-center mb-8">
        <h2 className="text-3xl font-black text-white mb-2">
          Physics of the Pipe · Send Path Breakdown
        </h2>
        <p className="text-slate-500 text-sm">
          Every nanosecond accounted for. No hidden work. No kernel mode. No surprises.
        </p>
      </div>

      <div className="rounded-2xl border border-slate-700 bg-slate-900/80 p-6 backdrop-blur overflow-x-auto">
        {/* Phase legend */}
        <div className="flex flex-wrap gap-4 mb-6">
          {Object.entries(PHASE_COLORS).map(([phase, color]) => (
            <div key={phase} className="flex items-center gap-1.5">
              <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: color }} />
              <span className="text-xs text-slate-500 capitalize font-mono">{phase}</span>
            </div>
          ))}
        </div>

        {/* Timeline */}
        <div className="space-y-3">
          {TIMELINE.map((t, i) => (
            <div key={i} className="flex items-start gap-4 group">
              {/* Time */}
              <div className="w-20 shrink-0 text-right">
                <span className="text-xs font-mono text-slate-600">{t.step}</span>
              </div>

              {/* Node */}
              <div className="flex flex-col items-center">
                <div
                  className="w-3 h-3 rounded-full shrink-0 mt-0.5"
                  style={{ backgroundColor: PHASE_COLORS[t.phase] }}
                />
                {i < TIMELINE.length - 1 && (
                  <div className="w-0.5 flex-1 bg-slate-800 mt-1" style={{ minHeight: 24 }} />
                )}
              </div>

              {/* Content */}
              <div className="flex-1 pb-3">
                <div className="flex items-center gap-2 mb-0.5">
                  <span
                    className="text-sm font-bold"
                    style={{ color: PHASE_COLORS[t.phase] }}
                  >
                    {t.label}
                  </span>
                  <span className="text-xs font-mono font-bold text-white bg-slate-800 px-2 py-0.5 rounded-full">
                    {t.cost}
                  </span>
                </div>
                <p className="text-xs text-slate-500 leading-relaxed">{t.detail}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Bar chart summary */}
        <div className="mt-8 border-t border-slate-800 pt-6">
          <div className="text-xs text-slate-600 font-semibold uppercase tracking-widest mb-4">
            Cost Distribution (typical path, 340ns total)
          </div>
          <div className="space-y-2">
            {[
              { label: "Slab Acquire", ns: 40, color: "#a78bfa", total: 340 },
              { label: "Payload Write (SIMD)", ns: 80, color: "#a78bfa", total: 340 },
              { label: "HEAD Bump (Atomic)", ns: 20, color: "#a78bfa", total: 340 },
              { label: "Cache Propagation", ns: 30, color: "#38bdf8", total: 340 },
              { label: "Spin-Poll Hit", ns: 130, color: "#34d399", total: 340 },
              { label: "Zero-Copy Dispatch", ns: 40, color: "#34d399", total: 340 },
            ].map((row) => (
              <div key={row.label} className="flex items-center gap-3">
                <div className="w-40 text-xs text-slate-500 shrink-0 text-right font-mono">
                  {row.label}
                </div>
                <div className="flex-1 h-5 bg-slate-800 rounded overflow-hidden">
                  <div
                    className="h-full rounded transition-all duration-700"
                    style={{
                      width: `${(row.ns / row.total) * 100}%`,
                      backgroundColor: row.color,
                      opacity: 0.8,
                    }}
                  />
                </div>
                <div className="w-14 text-xs font-mono text-white text-right shrink-0">
                  {row.ns}ns
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
