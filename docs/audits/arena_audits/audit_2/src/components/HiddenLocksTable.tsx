import { useState } from "react";
import { hiddenLocks } from "../data/auditData";
import VerdictBadge from "./VerdictBadge";

export default function HiddenLocksTable() {
  const [expanded, setExpanded] = useState<string | null>(null);

  return (
    <div className="space-y-2">
      {hiddenLocks.map((lock) => {
        const isExp = expanded === lock.id;
        const isCrit = lock.residual === "CRITICAL";
        const isPartial = lock.residual === "PARTIAL";

        return (
          <div
            key={lock.id}
            className={`rounded-xl border cursor-pointer transition-all duration-200 overflow-hidden ${
              isCrit
                ? "border-red-800 bg-red-950/30 hover:bg-red-950/40"
                : isPartial
                ? "border-yellow-800/60 bg-yellow-950/20 hover:bg-yellow-950/30"
                : "border-slate-800 bg-slate-900/40 hover:bg-slate-800/30"
            }`}
            onClick={() => setExpanded(isExp ? null : lock.id)}
          >
            <div className="flex items-center justify-between px-5 py-3.5">
              <div className="flex items-center gap-4">
                <span className="text-[10px] font-mono text-slate-600">{lock.id}</span>
                <div>
                  <p className={`text-sm font-bold font-mono ${isCrit ? "text-red-300" : isPartial ? "text-yellow-300" : "text-slate-300"}`}>
                    {lock.path}
                  </p>
                  <p className="text-[10px] text-slate-500 mt-0.5">{lock.lockType}</p>
                </div>
              </div>
              <div className="flex items-center gap-4">
                <div className="text-right hidden sm:block">
                  <p className={`text-xs font-mono font-bold ${isCrit ? "text-red-400" : isPartial ? "text-yellow-400" : "text-slate-400"}`}>
                    {lock.magnitude}
                  </p>
                  <p className="text-[9px] text-slate-600">per event</p>
                </div>
                <VerdictBadge verdict={lock.residual} size="sm" />
                <span className="text-slate-600 text-xs">{isExp ? "▲" : "▼"}</span>
              </div>
            </div>

            {isExp && (
              <div className="border-t border-slate-800/60 px-5 py-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <p className="text-[9px] font-mono font-bold text-slate-600 uppercase tracking-widest mb-1.5">Trigger Condition</p>
                  <p className="text-xs text-slate-400 leading-relaxed">{lock.trigger}</p>
                </div>
                <div>
                  <p className="text-[9px] font-mono font-bold text-slate-600 uppercase tracking-widest mb-1.5">Mitigation Path</p>
                  <p className="text-xs text-cyan-400 leading-relaxed">{lock.mitigation}</p>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
