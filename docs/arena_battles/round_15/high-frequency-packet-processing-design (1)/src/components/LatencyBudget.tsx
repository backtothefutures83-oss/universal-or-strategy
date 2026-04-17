interface BudgetItem {
  label: string;
  ns: number;
  color: string;
  detail: string;
}

interface LatencyBudgetProps {
  items: BudgetItem[];
  totalBudget: number;
  title: string;
}

export default function LatencyBudget({ items, totalBudget, title }: LatencyBudgetProps) {
  const usedNs = items.reduce((s, i) => s + i.ns, 0);
  const remainingNs = totalBudget - usedNs;
  const overBudget = usedNs > totalBudget;

  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-800/50 p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-semibold text-slate-300 uppercase tracking-wider">{title}</h4>
        <span className={`text-xs font-mono px-2 py-1 rounded-full ${overBudget ? "bg-red-900/50 text-red-400" : "bg-emerald-900/50 text-emerald-400"}`}>
          {usedNs.toFixed(2)} ns total
        </span>
      </div>

      {/* Stacked bar */}
      <div className="relative h-8 w-full rounded-lg overflow-hidden bg-slate-900/80 border border-slate-700/40">
        <div className="absolute inset-0 flex">
          {items.map((item, i) => (
            <div
              key={i}
              className={`h-full ${item.color} flex items-center justify-center transition-all duration-700`}
              style={{ width: `${(item.ns / totalBudget) * 100}%` }}
              title={`${item.label}: ${item.ns} ns`}
            />
          ))}
          {/* Remaining budget (green) */}
          {!overBudget && (
            <div
              className="h-full bg-emerald-900/30 border-l border-emerald-700/40"
              style={{ width: `${(remainingNs / totalBudget) * 100}%` }}
            />
          )}
        </div>
        {/* 5ns marker */}
        <div className="absolute right-0 top-0 h-full w-px bg-cyan-500/60" />
        <span className="absolute right-1 top-0.5 text-[10px] text-cyan-400 font-mono">5ns</span>
      </div>

      {/* Legend */}
      <div className="space-y-2">
        {items.map((item, i) => (
          <div key={i} className="flex items-center gap-3">
            <div className={`w-3 h-3 rounded-sm flex-shrink-0 ${item.color}`} />
            <div className="flex-1 min-w-0">
              <div className="flex justify-between items-baseline gap-2">
                <span className="text-sm text-slate-300 font-mono truncate">{item.label}</span>
                <span className="text-sm font-mono font-bold text-slate-200 flex-shrink-0">{item.ns.toFixed(2)} ns</span>
              </div>
              <p className="text-xs text-slate-500 mt-0.5">{item.detail}</p>
            </div>
          </div>
        ))}
        <div className="flex items-center gap-3 pt-1 border-t border-slate-700/40">
          <div className="w-3 h-3 rounded-sm flex-shrink-0 bg-emerald-900/50 border border-emerald-700/40" />
          <div className="flex-1 flex justify-between items-baseline gap-2">
            <span className="text-sm text-slate-400 font-mono">Budget remaining</span>
            <span className={`text-sm font-mono font-bold ${remainingNs >= 0 ? "text-emerald-400" : "text-red-400"}`}>
              {remainingNs.toFixed(2)} ns
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
