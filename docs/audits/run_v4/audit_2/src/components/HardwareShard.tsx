import { useState } from "react";
import { CORE_MAPPINGS } from "../data/meshData";

type Mode = "12" | "6" | "4";

const MODE_META: Record<Mode, { label: string; sub: string; color: string; bg: string; border: string }> = {
  "12": { label: "Elite Mode", sub: "12-Core · 1 Core = 1 Job", color: "#10b981", bg: "#022c22", border: "#065f46" },
  "6":  { label: "Core-Compressed", sub: "6-Core · Hot-Path Pinned", color: "#f59e0b", bg: "#451a03", border: "#92400e" },
  "4":  { label: "Survival Mode", sub: "4-Core · Minimum Footprint", color: "#ef4444", bg: "#3b0000", border: "#7f1d1d" },
};

const PRIORITY_COLORS: Record<string, string> = {
  hot:    "#10b981",
  warm:   "#f59e0b",
  cold:   "#6b7280",
};

const ROLE_COLORS: Record<string, string> = {
  "ING-0": "#06b6d4", "ING-1": "#06b6d4",
  "RTR-0": "#8b5cf6", "RTR-1": "#8b5cf6",
  "TRF-0": "#f59e0b", "TRF-1": "#f59e0b",
  "ACT-0": "#10b981", "ACT-1": "#10b981",
  "EGR-0": "#3b82f6", "EGR-1": "#3b82f6",
  "MIR-0": "#6b7280", "MIR-1": "#6b7280",
};

export default function HardwareShard() {
  const [mode, setMode] = useState<Mode>("12");
  const meta = MODE_META[mode];



  return (
    <div className="bg-gray-950 border border-amber-900/40 rounded-xl overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-5 py-3 border-b border-amber-900/30 bg-black/40">
        <span className="text-amber-400 font-mono text-xs font-bold tracking-widest uppercase">
          ◈ STEP 2 — Hardware-Symmetric Sharding · Auto-Scale Logic
        </span>
        <div className="flex gap-2">
          {(["12", "6", "4"] as Mode[]).map((m) => {
            const mt = MODE_META[m];
            const isActive = mode === m;
            return (
              <button
                key={m}
                onClick={() => setMode(m)}
                className="px-3 py-1 rounded text-[10px] font-mono font-bold border transition-all duration-200"
                style={{
                  borderColor: isActive ? mt.color : "#374151",
                  color: isActive ? mt.color : "#6b7280",
                  background: isActive ? `${mt.bg}` : "transparent",
                }}
              >
                {m}-CORE
              </button>
            );
          })}
        </div>
      </div>

      {/* Mode badge */}
      <div className="px-5 py-3 border-b border-white/5 flex items-center gap-3">
        <div className="rounded border px-3 py-1.5" style={{ borderColor: meta.border, background: meta.bg }}>
          <p className="text-[10px] font-mono opacity-60 uppercase tracking-widest">Mode</p>
          <p className="font-bold font-mono text-sm" style={{ color: meta.color }}>{meta.label}</p>
          <p className="text-[9px] font-mono opacity-70 mt-0.5">{meta.sub}</p>
        </div>
        <div className="flex-1 text-[10px] font-mono text-gray-500 leading-relaxed">
          {mode === "12" && "Full sovereign mesh. Every node owns exactly one physical core. CPU affinity locked via pthread_setaffinity_np. Zero contention. All SPSC wires operate at theoretical peak."}
          {mode === "6"  && "Hot-path (ING, ACT) remains pinned on dedicated cores. Router shards collapse onto Core 2. Transform shards collapse onto Core 3. Cold roles (Mirror, Egress) timeshare Core 5."}
          {mode === "4"  && "Survival topology. Ingress pinned to C0/C1. Router+Transform collapse to C2. Actor pinned to C3/C4. Egress+Mirror folded onto C5. SPSC logic preserved — no shared-producer violation."}
        </div>
        <div className="flex gap-3 text-[9px] font-mono">
          {Object.entries(PRIORITY_COLORS).map(([p, c]) => (
            <span key={p} className="flex items-center gap-1" style={{ color: c }}>
              <span className="w-2 h-2 rounded-full" style={{ background: c }} />
              {p.toUpperCase()} PATH
            </span>
          ))}
        </div>
      </div>

      {/* Core Grid */}
      <div className="p-5">
        <div className="grid grid-cols-6 gap-3">
          {CORE_MAPPINGS.map((cm) => {
            const roles = mode === "12" ? cm.mode12 : mode === "6" ? cm.mode6 : cm.mode4;
            const isActive = roles.length > 0;
            const isIdle = !isActive;
            return (
              <div
                key={cm.core}
                className="rounded-lg border overflow-hidden transition-all duration-300"
                style={{
                  borderColor: isIdle ? "#1f2937" : PRIORITY_COLORS[cm.priority] + "40",
                  background: isIdle ? "#0a0a0a" : `${PRIORITY_COLORS[cm.priority]}08`,
                  opacity: isIdle ? 0.35 : 1,
                }}
              >
                {/* Core header */}
                <div
                  className="px-2 py-1.5 flex items-center justify-between border-b"
                  style={{ borderColor: isIdle ? "#1f2937" : PRIORITY_COLORS[cm.priority] + "30" }}
                >
                  <span className="text-[9px] font-mono font-bold" style={{ color: isIdle ? "#374151" : PRIORITY_COLORS[cm.priority] }}>
                    CORE {cm.core}
                  </span>
                  {isActive && (
                    <span
                      className="text-[7px] font-mono rounded px-1"
                      style={{ color: PRIORITY_COLORS[cm.priority], background: `${PRIORITY_COLORS[cm.priority]}20` }}
                    >
                      {cm.priority.toUpperCase()}
                    </span>
                  )}
                </div>

                {/* Roles */}
                <div className="p-1.5 space-y-1 min-h-[60px]">
                  {isIdle ? (
                    <div className="flex items-center justify-center h-12 text-[8px] font-mono text-gray-700">IDLE</div>
                  ) : (
                    roles.map((role) => (
                      <div
                        key={role}
                        className="rounded px-1.5 py-0.5 text-[8px] font-mono font-bold flex items-center gap-1"
                        style={{
                          color: ROLE_COLORS[role] || "#9ca3af",
                          background: `${ROLE_COLORS[role] || "#9ca3af"}15`,
                          border: `0.5px solid ${ROLE_COLORS[role] || "#9ca3af"}30`,
                        }}
                      >
                        <span
                          className="w-1.5 h-1.5 rounded-full flex-shrink-0"
                          style={{ background: ROLE_COLORS[role] || "#9ca3af" }}
                        />
                        {role}
                      </div>
                    ))
                  )}
                </div>

                {/* Affinity badge */}
                {isActive && (
                  <div className="px-2 pb-1.5">
                    <span className="text-[7px] font-mono text-gray-600">
                      {cm.affinity === "pinned" ? "⚲ PINNED" : "⇌ TIMESHARE"}
                    </span>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Collapse rules */}
      {mode !== "12" && (
        <div className="mx-5 mb-5 rounded-lg border border-white/5 bg-gray-900/40 p-4 font-mono text-[10px] space-y-2">
          <p className="text-amber-400 font-bold text-[11px] mb-2">COLLAPSE RULES — SPSC INVARIANT PRESERVED</p>
          {mode === "6" && [
            ["RTR-0 + RTR-1", "Core 2", "Round-robin produce into separate SPSC rings. Each has its own producer slot — no MPSC violation."],
            ["TRF-0 + TRF-1", "Core 3", "Time-multiplexed on core via cooperative yield. Each TRF node owns its own ring. No shared-producer."],
            ["ACT-0",         "Core 4", "HOT — dedicated, pinned. Never collapses."],
            ["ACT-1 + EGR-0 + MIR-0 + MIR-1", "Core 5", "Warm/cold roles. ACT-1 runs first in epoch, EGR/MIR in drain phase."],
          ].map(([nodes, core, note], i) => (
            <div key={i} className="flex gap-3 border-b border-white/5 pb-1.5">
              <span className="w-36 text-amber-300 font-bold flex-shrink-0">{nodes}</span>
              <span className="w-12 text-gray-400 flex-shrink-0">→ {core}</span>
              <span className="text-gray-500">{note}</span>
            </div>
          ))}
          {mode === "4" && [
            ["RTR-0 + RTR-1 + TRF-0 + TRF-1", "Core 2", "Epoch-sliced: RTR runs for 1µs window, TRF drains in next window. SPSC heads unshared."],
            ["ACT-0",         "Core 3", "HOT — dedicated, never collapses."],
            ["ACT-1",         "Core 4", "HOT — dedicated, never collapses."],
            ["EGR-0 + EGR-1 + MIR-0 + MIR-1", "Core 5", "Drain-phase sharing. All are pure consumers on their own SPSC — no write contention."],
          ].map(([nodes, core, note], i) => (
            <div key={i} className="flex gap-3 border-b border-white/5 pb-1.5">
              <span className="w-48 text-red-300 font-bold flex-shrink-0">{nodes}</span>
              <span className="w-12 text-gray-400 flex-shrink-0">→ {core}</span>
              <span className="text-gray-500">{note}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
