export default function HeroSection() {
  return (
    <header className="relative overflow-hidden bg-gray-950 border-b border-gray-800">
      {/* Grid background */}
      <div
        className="absolute inset-0 opacity-10"
        style={{
          backgroundImage:
            "linear-gradient(rgba(239,68,68,0.4) 1px, transparent 1px), linear-gradient(90deg, rgba(239,68,68,0.4) 1px, transparent 1px)",
          backgroundSize: "40px 40px",
        }}
      />

      <div className="relative max-w-7xl mx-auto px-4 py-14">
        {/* Badge */}
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-red-950 border border-red-700 text-red-400 text-xs font-bold mb-6 tracking-widest uppercase">
          <span className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
          Engineering Integrity Report
        </div>

        <h1 className="text-4xl md:text-5xl font-black text-white leading-tight mb-4">
          Low-Latency IPC
          <span className="text-red-500"> — </span>
          <span className="text-transparent bg-clip-text bg-gradient-to-r from-red-400 to-orange-400">
            What's Real
          </span>
        </h1>

        <p className="text-gray-400 text-lg max-w-3xl mb-8 leading-relaxed">
          A rigorous audit of "sub-nanosecond fence-free" claims, alongside accurate
          latency reference data, real CPU cache topology, correct memory-model
          semantics, and proven lock-free patterns that actually work in production.
        </p>

        {/* Key findings strip */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {[
            { label: "Claimed target",    value: "0.5 ns",   sub: "per channel op",        color: "red"    },
            { label: "CLR floor (no-op)", value: "~2 ns",    sub: "irreducible managed",    color: "orange" },
            { label: "Real SPSC floor",   value: "~6 ns",    sub: "pinned, unmanaged C++",  color: "yellow" },
            { label: "Fence-free safety", value: "x86 only", sub: "single socket, 1 writer", color: "green" },
          ].map((stat) => {
            const colors: Record<string, string> = {
              red:    "border-red-700    bg-red-950/50   text-red-400",
              orange: "border-orange-700 bg-orange-950/50 text-orange-400",
              yellow: "border-yellow-700 bg-yellow-950/50 text-yellow-400",
              green:  "border-green-700  bg-green-950/50  text-green-400",
            };
            return (
              <div key={stat.label} className={`rounded-xl border p-4 ${colors[stat.color]}`}>
                <div className="text-2xl font-black">{stat.value}</div>
                <div className="text-xs font-bold uppercase tracking-wider mt-1">{stat.label}</div>
                <div className="text-xs opacity-70 mt-1">{stat.sub}</div>
              </div>
            );
          })}
        </div>
      </div>
    </header>
  );
}
