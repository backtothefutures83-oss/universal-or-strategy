export default function PipelineDiagram() {
  const stages = [
    {
      id: 1,
      label: "NIC / Socket",
      sublabel: "External",
      icon: "📡",
      color: "border-slate-500 bg-slate-800/80",
      labelColor: "text-slate-300",
      ns: null,
    },
    {
      id: 2,
      label: "Ingress Bridge",
      sublabel: "Ring Buffer",
      icon: "⚡",
      color: "border-cyan-500/60 bg-cyan-900/20",
      labelColor: "text-cyan-300",
      ns: "~1.2 ns",
    },
    {
      id: 3,
      label: "Tagged Ptr",
      sublabel: "CAS + Epoch",
      icon: "🔐",
      color: "border-violet-500/60 bg-violet-900/20",
      labelColor: "text-violet-300",
      ns: "~0.9 ns",
    },
    {
      id: 4,
      label: "Cache Guard",
      sublabel: "12-Thread Arena",
      icon: "🛡️",
      color: "border-emerald-500/60 bg-emerald-900/20",
      labelColor: "text-emerald-300",
      ns: "~0.4 ns",
    },
    {
      id: 5,
      label: "Processing Core",
      sublabel: "Local",
      icon: "⚙️",
      color: "border-slate-500 bg-slate-800/80",
      labelColor: "text-slate-300",
      ns: null,
    },
  ];

  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-800/30 p-6">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-sm font-semibold uppercase tracking-widest text-slate-400">
          End-to-End Pipeline
        </h3>
        <div className="flex items-center gap-2 text-xs font-mono">
          <span className="text-slate-500">Total budget:</span>
          <span className="text-emerald-400 font-bold">≤ 5.00 ns</span>
        </div>
      </div>

      {/* Pipeline flow */}
      <div className="flex items-center justify-between gap-2 overflow-x-auto pb-2">
        {stages.map((stage, i) => (
          <div key={stage.id} className="flex items-center gap-2">
            {/* Stage box */}
            <div className={`flex flex-col items-center p-3 rounded-xl border-2 ${stage.color} min-w-[96px]`}>
              <span className="text-2xl mb-1">{stage.icon}</span>
              <span className={`text-xs font-bold font-mono text-center leading-tight ${stage.labelColor}`}>
                {stage.label}
              </span>
              <span className="text-[10px] text-slate-500 font-mono text-center mt-0.5">
                {stage.sublabel}
              </span>
              {stage.ns && (
                <span className="mt-2 text-[10px] font-mono px-2 py-0.5 rounded-full bg-slate-900/60 text-yellow-400 border border-slate-700/50">
                  {stage.ns}
                </span>
              )}
            </div>

            {/* Arrow */}
            {i < stages.length - 1 && (
              <div className="flex flex-col items-center gap-0.5 flex-shrink-0">
                <div className="flex items-center gap-0">
                  <div className="w-6 h-px bg-gradient-to-r from-slate-600 to-slate-500" />
                  <svg className="w-3 h-3 text-slate-500 -ml-0.5" viewBox="0 0 12 12" fill="currentColor">
                    <path d="M2 6l6-4v8L2 6z" transform="rotate(180 6 6)" />
                  </svg>
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Total latency bar */}
      <div className="mt-6 space-y-2">
        <div className="flex justify-between text-xs font-mono text-slate-500">
          <span>Cumulative ns</span>
          <span>5 ns cap</span>
        </div>
        <div className="h-3 bg-slate-900/80 rounded-full overflow-hidden border border-slate-700/40 relative">
          {/* Ingress */}
          <div className="absolute left-0 top-0 h-full bg-cyan-600/70 rounded-l-full" style={{ width: "24%" }} />
          {/* Tagged ptr */}
          <div className="absolute top-0 h-full bg-violet-600/70" style={{ left: "24%", width: "18%" }} />
          {/* Cache guard */}
          <div className="absolute top-0 h-full bg-emerald-600/70" style={{ left: "42%", width: "8%" }} />
          {/* Remaining */}
          <div className="absolute top-0 h-full bg-slate-700/30" style={{ left: "50%", width: "50%" }} />
          {/* 5ns line */}
          <div className="absolute right-0 top-0 h-full w-0.5 bg-red-500/60" />
        </div>
        <div className="flex gap-4 text-[10px] font-mono">
          <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-sm bg-cyan-600/70 inline-block"/>Ingress 1.2 ns</span>
          <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-sm bg-violet-600/70 inline-block"/>Tagged 0.9 ns</span>
          <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-sm bg-emerald-600/70 inline-block"/>Guard 0.4 ns</span>
          <span className="flex items-center gap-1 ml-auto text-emerald-400">✓ 2.50 ns used · 2.50 ns spare</span>
        </div>
      </div>
    </div>
  );
}
