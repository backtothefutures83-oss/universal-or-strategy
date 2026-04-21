import { useState } from "react";
import { findings } from "../data/auditData";
import VerdictBadge from "./VerdictBadge";

export default function FindingsTable() {
  const [expanded, setExpanded] = useState<string | null>(null);

  return (
    <div className="overflow-x-auto rounded-xl border border-slate-800">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr className="bg-slate-900/80 border-b border-slate-800">
            <th className="text-left px-4 py-3 text-[10px] font-mono font-bold text-slate-500 uppercase tracking-widest w-16">ID</th>
            <th className="text-left px-4 py-3 text-[10px] font-mono font-bold text-slate-500 uppercase tracking-widest">Layer</th>
            <th className="text-left px-4 py-3 text-[10px] font-mono font-bold text-slate-500 uppercase tracking-widest">Mechanism</th>
            <th className="text-left px-4 py-3 text-[10px] font-mono font-bold text-slate-500 uppercase tracking-widest">Before</th>
            <th className="text-left px-4 py-3 text-[10px] font-mono font-bold text-slate-500 uppercase tracking-widest">After</th>
            <th className="text-left px-4 py-3 text-[10px] font-mono font-bold text-slate-500 uppercase tracking-widest">Verdict</th>
          </tr>
        </thead>
        <tbody>
          {findings.map((f) => {
            const isExp = expanded === f.id;
            return (
              <>
                <tr
                  key={f.id}
                  className={`border-b border-slate-800/60 cursor-pointer transition-colors duration-150 ${isExp ? "bg-slate-800/40" : "hover:bg-slate-800/20"}`}
                  onClick={() => setExpanded(isExp ? null : f.id)}
                >
                  <td className="px-4 py-3 font-mono text-[11px] text-slate-500">{f.id}</td>
                  <td className="px-4 py-3 text-[11px] text-slate-400">{f.layer}</td>
                  <td className="px-4 py-3 text-[11px] text-slate-200 font-medium">{f.mechanism}</td>
                  <td className="px-4 py-3 font-mono text-[11px] text-orange-400">{f.jitterBefore}</td>
                  <td className="px-4 py-3 font-mono text-[11px] text-cyan-300">{f.jitterAfter}</td>
                  <td className="px-4 py-3">
                    <VerdictBadge verdict={f.verdict} size="sm" />
                  </td>
                </tr>
                {isExp && (
                  <tr key={`${f.id}-exp`} className="bg-slate-900/60 border-b border-slate-800">
                    <td colSpan={6} className="px-6 py-3">
                      <p className="text-xs text-slate-400 leading-relaxed font-mono border-l-2 border-slate-700 pl-3">
                        {f.notes}
                      </p>
                    </td>
                  </tr>
                )}
              </>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
