import { useEffect, useState } from "react";
import { scoreMatrix } from "../data/auditData";
import ScoreGauge from "./ScoreGauge";

export default function ScoreMatrix() {
  const [show, setShow] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setShow(true), 200);
    return () => clearTimeout(t);
  }, []);

  const totalBefore = scoreMatrix.reduce((s, c) => s + c.before, 0);
  const totalAfter = scoreMatrix.reduce((s, c) => s + c.after, 0);
  const totalMax = scoreMatrix.reduce((s, c) => s + c.maxPoints, 0);

  return (
    <div className="space-y-6">
      {/* Master score */}
      <div className="flex flex-col sm:flex-row items-center gap-8 bg-slate-900/60 rounded-2xl border border-slate-800 p-6">
        <div className="flex gap-8">
          <div className="flex flex-col items-center gap-2">
            <p className="text-[10px] font-mono font-bold text-slate-600 uppercase tracking-widest">Before</p>
            <ScoreGauge score={totalBefore} maxScore={totalMax} label="Pre-Remediation" size="lg" />
          </div>
          <div className="flex flex-col items-center gap-2">
            <p className="text-[10px] font-mono font-bold text-cyan-700 uppercase tracking-widest">After</p>
            <ScoreGauge score={totalAfter} maxScore={totalMax} label="Post-Remediation" size="lg" />
          </div>
        </div>
        <div className="flex-1 space-y-2">
          <h3 className="text-lg font-black text-white">
            {totalAfter}/{totalMax} — <span className="text-yellow-300">NOT 100</span>
          </h3>
          <p className="text-sm text-slate-400 leading-relaxed">
            The mlockall + Slab + Lua stack resolves <strong className="text-cyan-300">SLUB and Redis contention completely</strong>, gaining <strong className="text-white">{totalAfter - totalBefore} points</strong>. The remaining gap is held by two unresolved kernel-adjacent locks in the Node.js V8 runtime.
          </p>
          <div className="flex gap-2 flex-wrap">
            <span className="text-[10px] font-mono bg-red-950 border border-red-800 text-red-300 rounded px-2 py-1">
              −{25 - scoreMatrix[1].after}pts: IPC Serialization Lock
            </span>
            <span className="text-[10px] font-mono bg-yellow-950 border border-yellow-800 text-yellow-300 rounded px-2 py-1">
              −{25 - scoreMatrix[3].after}pts: Scheduler Isolation
            </span>
          </div>
        </div>
      </div>

      {/* Category breakdown */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {scoreMatrix.map((cat) => {
          const pctAfter = cat.after / cat.maxPoints;
          const pctBefore = cat.before / cat.maxPoints;
          const barColor =
            pctAfter >= 1.0
              ? "bg-cyan-500"
              : pctAfter >= 0.8
              ? "bg-lime-500"
              : pctAfter >= 0.65
              ? "bg-yellow-500"
              : "bg-red-500";

          return (
            <div key={cat.label} className="bg-slate-900/60 rounded-xl border border-slate-800 p-4">
              <div className="flex items-start justify-between mb-3">
                <p className="text-xs font-bold text-slate-200 leading-tight max-w-[180px]">{cat.label}</p>
                <div className="flex items-center gap-1.5 flex-shrink-0">
                  <span className="text-[10px] font-mono text-slate-600 line-through">{cat.before}</span>
                  <span className="text-slate-600">→</span>
                  <span className={`text-sm font-black font-mono ${pctAfter >= 1 ? "text-cyan-300" : pctAfter >= 0.8 ? "text-lime-300" : "text-yellow-300"}`}>
                    {cat.after}
                  </span>
                  <span className="text-[10px] text-slate-600">/{cat.maxPoints}</span>
                </div>
              </div>

              {/* Before bar */}
              <div className="space-y-1.5 mb-3">
                <div className="flex items-center gap-2">
                  <span className="text-[9px] text-slate-600 font-mono w-12">BEFORE</span>
                  <div className="flex-1 bg-slate-800 rounded-full h-1">
                    <div
                      className="bg-slate-600 h-1 rounded-full transition-all duration-1000"
                      style={{ width: show ? `${pctBefore * 100}%` : "0%" }}
                    />
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-[9px] text-slate-500 font-mono w-12">AFTER</span>
                  <div className="flex-1 bg-slate-800 rounded-full h-1.5">
                    <div
                      className={`${barColor} h-1.5 rounded-full transition-all duration-1000 delay-300`}
                      style={{ width: show ? `${pctAfter * 100}%` : "0%" }}
                    />
                  </div>
                </div>
              </div>

              <p className="text-[10px] text-slate-500 leading-relaxed">{cat.notes}</p>
            </div>
          );
        })}
      </div>
    </div>
  );
}
