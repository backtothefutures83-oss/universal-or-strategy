export default function Header() {
  return (
    <header className="relative overflow-hidden border-b border-gray-800">
      {/* Scanline grid background */}
      <div
        className="absolute inset-0 opacity-10"
        style={{
          backgroundImage: `
            linear-gradient(rgba(6,182,212,0.15) 1px, transparent 1px),
            linear-gradient(90deg, rgba(6,182,212,0.15) 1px, transparent 1px)
          `,
          backgroundSize: "32px 32px",
        }}
      />
      {/* Glow orbs */}
      <div className="absolute top-0 left-1/4 w-96 h-32 bg-cyan-500/10 blur-3xl rounded-full" />
      <div className="absolute top-0 right-1/4 w-96 h-32 bg-violet-500/10 blur-3xl rounded-full" />

      <div className="relative max-w-7xl mx-auto px-4 py-10">
        {/* Top status bar */}
        <div className="flex items-center gap-3 mb-6">
          <div className="flex gap-1.5">
            <span className="w-2 h-2 rounded-full bg-red-500/80 inline-block" />
            <span className="w-2 h-2 rounded-full bg-yellow-500/80 inline-block" />
            <span className="w-2 h-2 rounded-full bg-green-500/80 inline-block" />
          </div>
          <span className="text-xs text-gray-600 tracking-widest uppercase">
            engineering-spec · v2.4.1 · x86_64 · .NET 9
          </span>
          <div className="ml-auto flex items-center gap-2">
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded bg-green-500/10 border border-green-500/30 text-green-400 text-xs tracking-widest">
              <span className="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse inline-block" />
              LIVE
            </span>
          </div>
        </div>

        {/* Main title */}
        <div className="space-y-2">
          <h1 className="text-3xl sm:text-4xl lg:text-5xl font-black tracking-tight text-white">
            High-Frequency{" "}
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 via-sky-400 to-violet-400">
              Packet Pipeline
            </span>
          </h1>
          <p className="text-gray-400 text-sm sm:text-base leading-relaxed max-w-2xl">
            Custom atomic topology design for sub-5ns cycle time. Zero-allocation ingress,
            bitwise tagged pointers, and cache-line isolation for 12-thread data planes.
          </p>
        </div>

        {/* Constraint chips */}
        <div className="flex flex-wrap gap-2 mt-6">
          {[
            { label: "Target Latency", value: "< 5 ns", color: "cyan" },
            { label: "Threads", value: "12 parallel", color: "violet" },
            { label: "Cache Line", value: "64 bytes", color: "amber" },
            { label: "Platform", value: "x86_64 / ARM64", color: "emerald" },
            { label: "Runtime", value: ".NET 9 / C# 13", color: "pink" },
          ].map((chip) => (
            <div
              key={chip.label}
              className={`
                flex items-center gap-2 px-3 py-1.5 rounded-md border text-xs
                ${chip.color === "cyan" ? "border-cyan-500/30 bg-cyan-500/10 text-cyan-300" : ""}
                ${chip.color === "violet" ? "border-violet-500/30 bg-violet-500/10 text-violet-300" : ""}
                ${chip.color === "amber" ? "border-amber-500/30 bg-amber-500/10 text-amber-300" : ""}
                ${chip.color === "emerald" ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-300" : ""}
                ${chip.color === "pink" ? "border-pink-500/30 bg-pink-500/10 text-pink-300" : ""}
              `}
            >
              <span className="text-gray-500">{chip.label}:</span>
              <span className="font-bold tracking-wide">{chip.value}</span>
            </div>
          ))}
        </div>
      </div>
    </header>
  );
}
