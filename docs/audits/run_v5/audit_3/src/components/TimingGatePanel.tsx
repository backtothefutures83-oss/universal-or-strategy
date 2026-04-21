// ═══════════════════════════════════════════════════════════════════
//  1µs HARD GATE — Timing breakdown & verdict display
//  Shows per-stage latency, total pass time, CS/interrupt counts
// ═══════════════════════════════════════════════════════════════════

import type { TimingGate } from '../types';

interface Props {
  gate: TimingGate;
  history: number[];
}

const STAGE_COLORS: Record<string, string> = {
  ingressNs:   '#10b981',
  routerNs:    '#818cf8',
  transformNs: '#f59e0b',
  actorNs:     '#a855f7',
  egressNs:    '#3b82f6',
};

const STAGE_LABELS: Record<string, string> = {
  ingressNs:   'Ingress',
  routerNs:    'Router',
  transformNs: 'Transform',
  actorNs:     'Actor',
  egressNs:    'Egress',
};

export default function TimingGatePanel({ gate, history }: Props) {
  const total = gate.totalPassNs;
  const pct = (ns: number) => (ns / 1000) * 100;
  const verdictColor =
    gate.verdict === 'PASS' ? 'text-emerald-400' :
    gate.verdict === 'WARN' ? 'text-amber-400' : 'text-red-400';
  const verdictBg =
    gate.verdict === 'PASS' ? 'bg-emerald-950 border-emerald-800' :
    gate.verdict === 'WARN' ? 'bg-amber-950 border-amber-800' : 'bg-red-950 border-red-800';

  // Mini sparkline
  const maxHist = Math.max(...history, 1000);
  const sparkH = 36;
  const sparkW = history.length;

  return (
    <div className="p-3 h-full flex flex-col font-mono">
      {/* Verdict */}
      <div className={`flex items-center justify-between mb-3 p-2 rounded-lg border ${verdictBg}`}>
        <div className="flex items-center gap-2">
          <div className={`w-2 h-2 rounded-full ${
            gate.verdict === 'PASS' ? 'bg-emerald-400 animate-pulse' :
            gate.verdict === 'WARN' ? 'bg-amber-400 animate-pulse' : 'bg-red-400 animate-pulse'
          }`} />
          <span className={`text-sm font-bold ${verdictColor}`}>{gate.verdict}</span>
        </div>
        <span className={`text-lg font-bold ${verdictColor}`}>
          {total.toFixed(0)}<span className="text-xs ml-0.5">ns</span>
        </span>
      </div>

      {/* Budget bar (stacked) */}
      <div className="mb-1 text-[10px] text-slate-500 flex justify-between">
        <span>0ns</span>
        <span className="text-slate-600">1µs HARD GATE</span>
        <span>1000ns</span>
      </div>
      <div className="h-5 w-full bg-slate-900 rounded-full overflow-hidden flex mb-3 border border-slate-800">
        {Object.entries(gate.breakdown).map(([key, val]) => (
          <div
            key={key}
            className="h-full transition-all duration-500"
            style={{
              width: `${pct(val)}%`,
              backgroundColor: STAGE_COLORS[key],
              opacity: 0.8,
            }}
            title={`${STAGE_LABELS[key]}: ${val.toFixed(0)}ns`}
          />
        ))}
        {/* Remaining budget */}
        <div className="h-full flex-1 bg-slate-800/30" />
      </div>

      {/* Stage breakdown */}
      <div className="space-y-1.5 mb-3">
        {Object.entries(gate.breakdown).map(([key, val]) => (
          <div key={key} className="flex items-center gap-2 text-xs">
            <div className="w-2 h-2 rounded-sm" style={{ backgroundColor: STAGE_COLORS[key] }} />
            <span className="text-slate-500 w-20">{STAGE_LABELS[key]}</span>
            <div className="flex-1 h-1.5 bg-slate-800 rounded-full overflow-hidden">
              <div
                className="h-full rounded-full transition-all duration-500"
                style={{ width: `${pct(val)}%`, backgroundColor: STAGE_COLORS[key] }}
              />
            </div>
            <span className="text-slate-400 w-14 text-right">{val.toFixed(0)}ns</span>
          </div>
        ))}
      </div>

      {/* Invariants */}
      <div className="grid grid-cols-2 gap-2 mb-3">
        <div className={`p-2 rounded border text-center ${
          gate.csCount === 0 ? 'border-emerald-800 bg-emerald-950/50' : 'border-red-800 bg-red-950/50'
        }`}>
          <div className="text-[10px] text-slate-500">Context Switches</div>
          <div className={`text-sm font-bold ${gate.csCount === 0 ? 'text-emerald-400' : 'text-red-400'}`}>
            {gate.csCount}
          </div>
        </div>
        <div className={`p-2 rounded border text-center ${
          gate.interruptCount === 0 ? 'border-emerald-800 bg-emerald-950/50' : 'border-red-800 bg-red-950/50'
        }`}>
          <div className="text-[10px] text-slate-500">OS Interrupts</div>
          <div className={`text-sm font-bold ${gate.interruptCount === 0 ? 'text-emerald-400' : 'text-red-400'}`}>
            {gate.interruptCount}
          </div>
        </div>
      </div>

      {/* Sparkline */}
      <div className="flex-1 flex flex-col">
        <div className="text-[10px] text-slate-600 mb-1">Pass Latency History</div>
        <div className="flex-1 relative bg-slate-900/50 rounded border border-slate-800 overflow-hidden"
          style={{ minHeight: sparkH + 8 }}>
          {/* 1µs threshold line */}
          <div className="absolute left-0 right-0 border-t border-dashed border-red-900/60"
            style={{ bottom: `${(1000 / maxHist) * 100}%` }}>
            <span className="absolute right-1 -top-3 text-[8px] text-red-700">1µs</span>
          </div>
          <svg
            viewBox={`0 0 ${sparkW} ${sparkH}`}
            preserveAspectRatio="none"
            className="absolute inset-0 w-full h-full"
          >
            <polyline
              fill="none"
              stroke="#38bdf8"
              strokeWidth={1}
              points={history
                .map((v, i) => `${i},${sparkH - (v / maxHist) * sparkH}`)
                .join(' ')}
            />
          </svg>
        </div>
      </div>
    </div>
  );
}
