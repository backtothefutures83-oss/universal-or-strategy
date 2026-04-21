import { useState } from "react";
import { LATENCY_HOPS, TOTAL_HOT_PATH_NS, GATE_NS, HEADROOM_NS } from "../data/meshData";

const HOP_COLORS = [
  "#06b6d4", "#06b6d4",
  "#8b5cf6", "#8b5cf6",
  "#f59e0b", "#f59e0b",
  "#10b981", "#10b981",
  "#3b82f6",
  "#6b7280",
];

export default function LatencyBudget() {
  const [activeHop, setActiveHop] = useState<number | null>(null);
  const maxBudget = Math.max(...LATENCY_HOPS.map(h => h.budget));

  return (
    <div className="bg-gray-950 border border-violet-900/40 rounded-xl overflow-hidden">
      <div className="flex items-center justify-between px-5 py-3 border-b border-violet-900/30 bg-black/40">
        <span className="text-violet-400 font-mono text-xs font-bold tracking-widest uppercase">
          ◈ STEP 3 — Jitter Guard: 10µs Latency Budget
        </span>
        <div className="flex items-center gap-4 text-xs font-mono">
          <span className="text-emerald-400 font-bold">HOT PATH: {(TOTAL_HOT_PATH_NS / 1000).toFixed(2)}µs</span>
          <span className="text-gray-500">GATE: {GATE_NS / 1000}µs</span>
          <span className="text-amber-400">HEADROOM: {(HEADROOM_NS / 1000).toFixed(2)}µs</span>
        </div>
      </div>

      <div className="p-5 grid grid-cols-1 gap-3">
        {/* Overall budget bar */}
        <div className="mb-2">
          <div className="flex items-center justify-between text-[10px] font-mono text-gray-500 mb-1.5">
            <span>CUMULATIVE HOT-PATH BUDGET</span>
            <span className="text-emerald-400 font-bold">✓ GATE CLEARED BY {((HEADROOM_NS / GATE_NS) * 100).toFixed(1)}%</span>
          </div>
          <div className="h-5 bg-gray-900 rounded-full overflow-hidden border border-gray-800 relative">
            <div
              className="h-full rounded-full transition-all duration-700 flex items-center justify-end pr-2 relative overflow-hidden"
              style={{
                width: `${(TOTAL_HOT_PATH_NS / GATE_NS) * 100}%`,
                background: "linear-gradient(90deg, #06b6d4, #8b5cf6, #10b981)",
              }}
            >
              <span className="text-[9px] font-mono font-bold text-white z-10 relative">
                {(TOTAL_HOT_PATH_NS / 1000).toFixed(3)}µs
              </span>
              {/* shimmer */}
              <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/10 to-transparent animate-shimmer" />
            </div>
            {/* Gate line */}
            <div className="absolute top-0 right-0 h-full w-px bg-red-500 opacity-60" />
            <span className="absolute right-1 top-0 h-full flex items-center text-[8px] font-mono text-red-400">10µs</span>
          </div>
        </div>

        {/* Per-hop bars */}
        {LATENCY_HOPS.map((hop, i) => {
          const isHot = i < 9; // Mirror is async
          const isActive = activeHop === i;
          const pct = (hop.budget / maxBudget) * 100;
          const color = HOP_COLORS[i] || "#6b7280";
          return (
            <div
              key={i}
              className="group cursor-pointer"
              onMouseEnter={() => setActiveHop(i)}
              onMouseLeave={() => setActiveHop(null)}
            >
              <div className="flex items-center gap-3">
                {/* Hop index */}
                <span
                  className="flex-shrink-0 w-5 h-5 rounded text-[9px] font-mono font-bold flex items-center justify-center border"
                  style={{ color, borderColor: `${color}40`, background: `${color}15` }}
                >
                  {i + 1}
                </span>
                {/* Hop name */}
                <span className="flex-shrink-0 w-44 text-[10px] font-mono text-gray-300 truncate" title={hop.hop}>
                  {hop.hop}
                </span>
                {/* Bar */}
                <div className="flex-1 h-3 bg-gray-900 rounded-full overflow-hidden border border-gray-800">
                  <div
                    className="h-full rounded-full relative overflow-hidden transition-all duration-300"
                    style={{
                      width: `${pct}%`,
                      background: color,
                      opacity: isHot ? (isActive ? 1 : 0.75) : 0.4,
                    }}
                  >
                    <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/15 to-transparent animate-shimmer" />
                  </div>
                </div>
                {/* Budget value */}
                <span className="flex-shrink-0 w-16 text-right text-[10px] font-mono" style={{ color }}>
                  {hop.budget < 1000 ? `${hop.budget}ns` : `${(hop.budget / 1000).toFixed(1)}µs`}
                </span>
                {/* Hot tag */}
                <span className={`flex-shrink-0 w-12 text-[8px] font-mono ${isHot ? "text-emerald-500" : "text-gray-600"}`}>
                  {isHot ? "●HOT" : "○ASYNC"}
                </span>
              </div>

              {/* Expanded detail */}
              {isActive && (
                <div
                  className="mt-2 ml-8 rounded-lg border p-3 text-[10px] font-mono space-y-1.5 bg-gray-900/80 backdrop-blur-sm"
                  style={{ borderColor: `${color}40` }}
                >
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <p className="opacity-40 uppercase text-[8px] tracking-wider mb-0.5">Mechanism</p>
                      <p style={{ color }}>{hop.mechanism}</p>
                    </div>
                    <div>
                      <p className="opacity-40 uppercase text-[8px] tracking-wider mb-0.5">Cache State</p>
                      <p className="text-gray-300">{hop.cacheState}</p>
                    </div>
                    <div>
                      <p className="opacity-40 uppercase text-[8px] tracking-wider mb-0.5">Guarantee</p>
                      <p className="text-emerald-400">{hop.guarantee}</p>
                    </div>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Summary proof box */}
      <div className="mx-5 mb-5 rounded-lg border border-emerald-900/50 bg-emerald-950/20 p-4 font-mono text-xs">
        <p className="text-emerald-400 font-bold mb-2 text-[11px]">∑ LATENCY PROOF — SUB-10µs ARRIVAL</p>
        <div className="grid grid-cols-2 gap-x-8 gap-y-1 text-[10px]">
          {[
            ["NIC DMA → ING", "400ns"],
            ["ING → RTR (SPSC)", "320ns"],
            ["RTR Dispatch", "600ns"],
            ["RTR → TRF (SPSC)", "520ns"],
            ["TRF AVX-512", "1200ns"],
            ["TRF → ACT (SPSC)", "640ns"],
            ["ACT FSM", "2000ns"],
            ["ACT → EGR (SPSC)", "380ns"],
            ["EGR TX Burst", "800ns"],
          ].map(([k, v], i) => (
            <div key={i} className="flex justify-between border-b border-white/5 pb-0.5">
              <span className="text-gray-400">{k}</span>
              <span className="text-emerald-300">{v}</span>
            </div>
          ))}
          <div className="col-span-2 flex justify-between pt-1 text-[11px] font-bold">
            <span className="text-white">TOTAL HOT-PATH</span>
            <span className="text-emerald-400">6,860ns &lt; 10,000ns ✓</span>
          </div>
          <div className="col-span-2 flex justify-between text-[10px]">
            <span className="text-gray-500">Remaining headroom (jitter guard buffer)</span>
            <span className="text-amber-400">3,140ns</span>
          </div>
        </div>
      </div>
    </div>
  );
}
