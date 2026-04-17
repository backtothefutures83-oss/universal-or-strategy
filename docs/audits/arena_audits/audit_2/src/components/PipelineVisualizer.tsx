import { useState } from "react";
import { pipeStages } from "../data/auditData";

const COLOR_MAP: Record<string, { bg: string; border: string; text: string; glow: string; dot: string }> = {
  cyan: {
    bg: "bg-cyan-950/60",
    border: "border-cyan-700",
    text: "text-cyan-300",
    glow: "shadow-cyan-900",
    dot: "bg-cyan-400",
  },
  yellow: {
    bg: "bg-yellow-950/60",
    border: "border-yellow-600",
    text: "text-yellow-300",
    glow: "shadow-yellow-900",
    dot: "bg-yellow-400",
  },
  red: {
    bg: "bg-red-950/70",
    border: "border-red-600",
    text: "text-red-300",
    glow: "shadow-red-900",
    dot: "bg-red-400",
  },
  green: {
    bg: "bg-emerald-950/60",
    border: "border-emerald-700",
    text: "text-emerald-300",
    glow: "shadow-emerald-900",
    dot: "bg-emerald-400",
  },
};

export default function PipelineVisualizer() {
  const [active, setActive] = useState<number | null>(null);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 text-xs text-slate-500 font-mono mb-4">
        <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-cyan-400 inline-block" /> CLEAN</span>
        <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-yellow-400 inline-block" /> FRICTION</span>
        <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-red-400 inline-block" /> CRITICAL</span>
        <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-emerald-400 inline-block" /> RESOLVED</span>
      </div>

      <div className="flex flex-col gap-0">
        {pipeStages.map((stage, idx) => {
          const c = COLOR_MAP[stage.color];
          const isActive = active === stage.id;
          return (
            <div key={stage.id} className="flex items-stretch gap-0">
              {/* Spine */}
              <div className="flex flex-col items-center w-8 flex-shrink-0">
                <div
                  className={`w-4 h-4 rounded-full border-2 flex-shrink-0 z-10 cursor-pointer transition-all duration-200 ${c.dot} ${isActive ? "scale-125 ring-2 ring-white/20" : ""}`}
                  style={{ marginTop: idx === 0 ? 0 : 0 }}
                  onClick={() => setActive(isActive ? null : stage.id)}
                />
                {idx < pipeStages.length - 1 && (
                  <div className="w-0.5 flex-1 bg-slate-700 min-h-[24px]" />
                )}
              </div>

              {/* Card */}
              <div className="flex-1 pb-3">
                <div
                  className={`rounded-lg border ${c.bg} ${c.border} shadow-lg ${c.glow} cursor-pointer transition-all duration-200 ${isActive ? "ring-1 ring-white/10" : "hover:opacity-90"}`}
                  onClick={() => setActive(isActive ? null : stage.id)}
                >
                  <div className="flex items-center justify-between px-4 py-2.5">
                    <div className="flex items-center gap-3">
                      <span className="text-[10px] font-mono text-slate-500">S{String(stage.id).padStart(2,"0")}</span>
                      <div>
                        <p className={`text-sm font-bold ${c.text}`}>{stage.label}</p>
                        <p className="text-[10px] text-slate-500">{stage.sublabel}</p>
                      </div>
                    </div>
                    <div className="text-right">
                      <span className={`text-xs font-mono font-bold ${c.text}`}>{stage.latency}</span>
                      {stage.warning && (
                        <p className="text-[9px] text-red-400 font-mono">⚠ LOCK DETECTED</p>
                      )}
                    </div>
                  </div>

                  {isActive && stage.warning && (
                    <div className="px-4 pb-3 border-t border-red-900/40 mt-1 pt-2">
                      <p className="text-xs text-red-300 leading-relaxed font-mono">{stage.warning}</p>
                    </div>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
