

export default function Header() {
  return (
    <header className="border-b border-cyan-900/60 bg-black/80 backdrop-blur-md sticky top-0 z-50">
      <div className="max-w-screen-2xl mx-auto px-6 py-3 flex items-center justify-between">
        <div className="flex items-center gap-3">
          {/* Logo glyph */}
          <div className="relative w-9 h-9">
            <svg viewBox="0 0 36 36" className="w-9 h-9">
              <defs>
                <linearGradient id="logoGrad" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0%" stopColor="#06b6d4" />
                  <stop offset="100%" stopColor="#8b5cf6" />
                </linearGradient>
              </defs>
              <polygon points="18,2 34,10 34,26 18,34 2,26 2,10" fill="none" stroke="url(#logoGrad)" strokeWidth="2" />
              <circle cx="18" cy="18" r="5" fill="url(#logoGrad)" />
              <line x1="18" y1="2"  x2="18" y2="13" stroke="#06b6d4" strokeWidth="1.2" opacity="0.7" />
              <line x1="18" y1="23" x2="18" y2="34" stroke="#8b5cf6" strokeWidth="1.2" opacity="0.7" />
              <line x1="2"  y1="10" x2="13" y2="15" stroke="#06b6d4" strokeWidth="1.2" opacity="0.7" />
              <line x1="23" y1="21" x2="34" y2="26" stroke="#8b5cf6" strokeWidth="1.2" opacity="0.7" />
              <line x1="34" y1="10" x2="23" y2="15" stroke="#06b6d4" strokeWidth="1.2" opacity="0.7" />
              <line x1="13" y1="21" x2="2"  y2="26" stroke="#8b5cf6" strokeWidth="1.2" opacity="0.7" />
            </svg>
          </div>
          <div>
            <p className="text-[10px] font-mono text-cyan-500 tracking-widest uppercase leading-none">
              Antigravity Nexus OS · IPC Layer v4
            </p>
            <h1 className="text-sm font-bold text-white tracking-tight leading-tight">
              Adaptive Sovereign Mesh
            </h1>
          </div>
        </div>

        <div className="hidden md:flex items-center gap-6">
          <Pill color="cyan"  label="SPSC" sub="Pure 1-to-1 Wires" />
          <Pill color="violet" label="ZERO-COPY" sub="Shared Slab Memory" />
          <Pill color="emerald" label="10µs GATE" sub="Jitter-Bounded" />
          <Pill color="amber"  label="NO HEAP" sub="Fixed Slab Pools" />
        </div>

        <div className="flex items-center gap-2">
          <span className="inline-flex items-center gap-1.5 text-xs font-mono text-emerald-400">
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
            LIVE SIM
          </span>
        </div>
      </div>
    </header>
  );
}

function Pill({ color, label, sub }: { color: string; label: string; sub: string }) {
  const colors: Record<string, string> = {
    cyan:    "border-cyan-700/60 text-cyan-300 bg-cyan-950/40",
    violet:  "border-violet-700/60 text-violet-300 bg-violet-950/40",
    emerald: "border-emerald-700/60 text-emerald-300 bg-emerald-950/40",
    amber:   "border-amber-700/60 text-amber-300 bg-amber-950/40",
  };
  return (
    <div className={`border rounded px-2.5 py-1 ${colors[color]}`}>
      <p className="text-[10px] font-bold font-mono tracking-widest uppercase leading-none">{label}</p>
      <p className="text-[9px] opacity-60 leading-none mt-0.5">{sub}</p>
    </div>
  );
}
