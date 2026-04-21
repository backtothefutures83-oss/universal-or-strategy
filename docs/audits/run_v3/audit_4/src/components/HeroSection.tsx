export default function HeroSection() {
  return (
    <section className="relative flex flex-col items-center justify-center text-center pt-20 pb-10 px-4 overflow-hidden">
      {/* Background grid */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          backgroundImage:
            "linear-gradient(rgba(0,255,180,0.04) 1px, transparent 1px), linear-gradient(90deg, rgba(0,255,180,0.04) 1px, transparent 1px)",
          backgroundSize: "40px 40px",
        }}
      />

      {/* Glow orb */}
      <div className="pointer-events-none absolute top-10 left-1/2 -translate-x-1/2 w-[600px] h-[300px] rounded-full bg-emerald-500 opacity-10 blur-[120px]" />

      {/* Badge */}
      <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-4 py-1.5 text-xs font-semibold tracking-widest text-emerald-400 uppercase">
        <span className="relative flex h-2 w-2">
          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
          <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500" />
        </span>
        Arena AI Design Challenge · v2
      </div>

      <h1 className="text-5xl md:text-7xl font-black tracking-tight text-white leading-none mb-4">
        Sovereign Actor{" "}
        <span className="bg-gradient-to-r from-emerald-400 via-cyan-400 to-sky-400 bg-clip-text text-transparent">
          v2
        </span>{" "}
        IPC Layer
      </h1>

      <p className="text-lg md:text-xl text-slate-400 max-w-2xl mb-2 font-light">
        <span className="text-emerald-400 font-semibold">Antigravity Nexus OS</span> ·{" "}
        Zero-Heap / Open Pipe Topology
      </p>

      <p className="text-sm text-slate-500 max-w-xl">
        Everything is a Pipe. No managers. No buffers. No blockages.
        Data flows from Engine to Engine at the purity of a physical wire.
      </p>

      {/* Sub-10µs guarantee pill */}
      <div className="mt-8 flex flex-wrap justify-center gap-3">
        {[
          { label: "Hard Latency Gate", value: "< 10µs" },
          { label: "Core Mesh", value: "12 Isolated" },
          { label: "Lock Contention", value: "ZERO" },
          { label: "Systemic Score", value: "100 / 100" },
        ].map((s) => (
          <div
            key={s.label}
            className="rounded-xl border border-slate-700 bg-slate-800/60 px-5 py-3 backdrop-blur"
          >
            <div className="text-2xl font-black text-white">{s.value}</div>
            <div className="text-xs text-slate-500 mt-0.5">{s.label}</div>
          </div>
        ))}
      </div>
    </section>
  );
}
