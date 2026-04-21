// ═══════════════════════════════════════════════════════════════════
//  CORE PINNING GRID — 12-core sovereign assignment display
//  Shows per-core metrics, cache hit rates, spin cycles
// ═══════════════════════════════════════════════════════════════════

import type { CoreMetrics } from '../types';
import { MESH_ROLES } from '../simulation';

const CATEGORY_BORDERS: Record<string, string> = {
  ingress:   'border-emerald-700',
  router:    'border-indigo-700',
  transform: 'border-amber-700',
  actor:     'border-purple-700',
  egress:    'border-blue-700',
  mirror:    'border-pink-700',
};

const CATEGORY_ACCENTS: Record<string, string> = {
  ingress:   'text-emerald-400',
  router:    'text-indigo-400',
  transform: 'text-amber-400',
  actor:     'text-purple-400',
  egress:    'text-blue-400',
  mirror:    'text-pink-400',
};

interface Props {
  metrics: CoreMetrics[];
}

export default function CorePinningGrid({ metrics }: Props) {
  const roleMap = new Map(MESH_ROLES.map((r) => [r.id, r]));

  return (
    <div className="p-3 h-full flex flex-col font-mono">
      <div className="flex items-center justify-between mb-2">
        <span className="text-[10px] text-slate-500 uppercase tracking-wider">Sovereign Core Assignment</span>
        <span className="text-[10px] text-slate-600">12C / 2 NUMA</span>
      </div>

      <div className="grid grid-cols-3 gap-1.5 flex-1">
        {metrics.map((m) => {
          const role = roleMap.get(m.roleId);
          if (!role) return null;
          const catBorder = CATEGORY_BORDERS[role.category];
          const catAccent = CATEGORY_ACCENTS[role.category];
          const hasL3Miss = m.l3Misses > 0;

          return (
            <div
              key={m.roleId}
              className={`p-1.5 rounded border ${catBorder} bg-slate-900/80 flex flex-col ${
                hasL3Miss ? 'ring-1 ring-red-900/50' : ''
              }`}
            >
              <div className="flex items-center justify-between mb-0.5">
                <span className={`text-[9px] font-bold ${catAccent} truncate`}>
                  {role.label}
                </span>
                <span className="text-[8px] text-slate-600">C{m.coreId}</span>
              </div>

              {/* Mini metrics */}
              <div className="space-y-0.5 text-[8px]">
                <div className="flex justify-between">
                  <span className="text-slate-600">IPC</span>
                  <span className="text-slate-400">{m.ipc.toFixed(1)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-600">Cache</span>
                  <span className={m.cacheHitRate > 0.99 ? 'text-emerald-500' : 'text-amber-500'}>
                    {(m.cacheHitRate * 100).toFixed(1)}%
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-600">L3Miss</span>
                  <span className={hasL3Miss ? 'text-red-400' : 'text-emerald-600'}>
                    {m.l3Misses}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-600">Poll</span>
                  <span className={m.pollLatencyNs > 200 ? 'text-red-400' : 'text-cyan-500'}>
                    {m.pollLatencyNs.toFixed(0)}ns
                  </span>
                </div>
              </div>

              {/* Heap invariant */}
              <div className={`mt-auto pt-0.5 text-center text-[7px] rounded ${
                m.heapAllocBytes === 0 
                  ? 'text-emerald-600 bg-emerald-950/30' 
                  : 'text-red-400 bg-red-950/30'
              }`}>
                HEAP: {m.heapAllocBytes}B
              </div>
            </div>
          );
        })}
      </div>

      {/* Scaling modes */}
      <div className="mt-2 pt-2 border-t border-slate-800">
        <div className="text-[9px] text-slate-600 mb-1">Scaling Profiles</div>
        <div className="flex gap-2">
          {[
            { cores: 12, label: '12C Full', active: true },
            { cores: 6,  label: '6C Compact', active: false },
            { cores: 4,  label: '4C Minimal', active: false },
          ].map((p) => (
            <div key={p.cores}
              className={`flex-1 text-center text-[9px] p-1 rounded border ${
                p.active 
                  ? 'border-cyan-800 bg-cyan-950/30 text-cyan-400' 
                  : 'border-slate-800 text-slate-600'
              }`}>
              {p.label}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
