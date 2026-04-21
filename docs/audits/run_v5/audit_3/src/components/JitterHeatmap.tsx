// ═══════════════════════════════════════════════════════════════════
//  JITTER CONTAINMENT HEATMAP — L3 Cache Miss Correlation
//  Real-time jitter samples with L3 miss markers
// ═══════════════════════════════════════════════════════════════════

import type { JitterSample } from '../types';

interface Props {
  samples: JitterSample[];
}

export default function JitterHeatmap({ samples }: Props) {
  const maxNs = Math.max(...samples.map((s) => s.latencyNs), 1000);
  const minNs = Math.min(...samples.map((s) => s.latencyNs), 0);
  const barH = 100;
  const l3Count = samples.filter((s) => s.l3Miss).length;
  const l3Rate = (l3Count / samples.length) * 100;

  const p50 = [...samples].sort((a, b) => a.latencyNs - b.latencyNs)[Math.floor(samples.length * 0.5)]?.latencyNs ?? 0;
  const p99 = [...samples].sort((a, b) => a.latencyNs - b.latencyNs)[Math.floor(samples.length * 0.99)]?.latencyNs ?? 0;
  const jitter = p99 - p50;

  return (
    <div className="p-3 h-full flex flex-col font-mono">
      <div className="flex items-center justify-between mb-2">
        <span className="text-[10px] text-slate-500 uppercase tracking-wider">Jitter Containment</span>
        <span className={`text-[10px] px-1.5 py-0.5 rounded ${
          jitter < 100 ? 'bg-emerald-950 text-emerald-400' : 'bg-amber-950 text-amber-400'
        }`}>
          Jitter: {jitter.toFixed(0)}ns
        </span>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-4 gap-1.5 mb-3">
        {[
          { label: 'P50', value: `${p50.toFixed(0)}ns`, color: 'text-cyan-400' },
          { label: 'P99', value: `${p99.toFixed(0)}ns`, color: 'text-amber-400' },
          { label: 'L3 Miss', value: `${l3Rate.toFixed(1)}%`, color: l3Rate > 5 ? 'text-red-400' : 'text-emerald-400' },
          { label: 'Samples', value: `${samples.length}`, color: 'text-slate-400' },
        ].map((s) => (
          <div key={s.label} className="bg-slate-900/60 rounded border border-slate-800 p-1.5 text-center">
            <div className="text-[8px] text-slate-600">{s.label}</div>
            <div className={`text-[11px] font-bold ${s.color}`}>{s.value}</div>
          </div>
        ))}
      </div>

      {/* Waterfall chart */}
      <div className="flex-1 relative bg-slate-900/50 rounded border border-slate-800 overflow-hidden"
        style={{ minHeight: barH }}>
        {/* 1µs threshold */}
        <div className="absolute left-0 right-0 border-t border-dashed border-red-900/40"
          style={{ bottom: `${(1000 / maxNs) * 100}%` }}>
          <span className="absolute right-1 -top-3 text-[7px] text-red-800">1µs</span>
        </div>

        {/* Bars */}
        <div className="absolute inset-0 flex items-end">
          {samples.map((s, i) => {
            const h = ((s.latencyNs - minNs) / (maxNs - minNs)) * 100;
            return (
              <div
                key={i}
                className="flex-1 mx-px rounded-t transition-all duration-150"
                style={{
                  height: `${h}%`,
                  backgroundColor: s.l3Miss 
                    ? `rgba(239, 68, 68, ${0.5 + (s.latencyNs / maxNs) * 0.5})`
                    : `rgba(56, 189, 248, ${0.3 + (s.latencyNs / maxNs) * 0.5})`,
                  minWidth: 2,
                }}
                title={`${s.latencyNs.toFixed(0)}ns${s.l3Miss ? ' [L3 MISS]' : ''}`}
              />
            );
          })}
        </div>
      </div>

      {/* Legend */}
      <div className="flex items-center gap-4 mt-2 pt-1">
        <div className="flex items-center gap-1">
          <div className="w-3 h-2 rounded-sm bg-sky-500/60" />
          <span className="text-[8px] text-slate-500">Normal</span>
        </div>
        <div className="flex items-center gap-1">
          <div className="w-3 h-2 rounded-sm bg-red-500/60" />
          <span className="text-[8px] text-slate-500">L3 Cache Miss</span>
        </div>
        <div className="flex-1 text-right text-[8px] text-slate-600">
          Zero-Heap: <span className="text-emerald-500">ENFORCED</span>
        </div>
      </div>
    </div>
  );
}
