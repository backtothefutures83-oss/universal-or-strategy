// ═══════════════════════════════════════════════════════════════════
//  DATA FLOW TRACER — Live packet trace through the mesh
//  Shows the critical path with per-hop timing
// ═══════════════════════════════════════════════════════════════════

import { useEffect, useState } from 'react';

interface Hop {
  stage: string;
  role: string;
  latencyNs: number;
  color: string;
  icon: string;
}

const PIPELINE: Hop[] = [
  { stage: 'RECV',      role: 'Ingress α',    latencyNs: 0,   color: '#10b981', icon: '▶' },
  { stage: 'ROUTE',     role: 'Router ∅',     latencyNs: 0,   color: '#818cf8', icon: '◆' },
  { stage: 'TRANSFORM', role: 'SIMD Xform α', latencyNs: 0,   color: '#f59e0b', icon: '⚡' },
  { stage: 'EXECUTE',   role: 'Actor ⊕ A',    latencyNs: 0,   color: '#a855f7', icon: '●' },
  { stage: 'EMIT',      role: 'Egress α',     latencyNs: 0,   color: '#3b82f6', icon: '▷' },
  { stage: 'MIRROR',    role: 'Mirror ◇ A',   latencyNs: 0,   color: '#ec4899', icon: '◇' },
];

export default function DataFlowTracer() {
  const [activeHop, setActiveHop] = useState(0);
  const [hops, setHops] = useState<Hop[]>(PIPELINE);
  const [traceId, setTraceId] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setActiveHop((prev) => {
        if (prev >= PIPELINE.length - 1) {
          setTraceId((t) => t + 1);
          // Regenerate latencies
          setHops(PIPELINE.map((h) => ({
            ...h,
            latencyNs: Math.floor(40 + Math.random() * 120),
          })));
          return 0;
        }
        return prev + 1;
      });
    }, 400);
    return () => clearInterval(interval);
  }, []);

  const totalNs = hops.reduce((sum, h) => sum + h.latencyNs, 0);

  return (
    <div className="p-3 h-full flex flex-col font-mono">
      <div className="flex items-center justify-between mb-2">
        <span className="text-[10px] text-slate-500 uppercase tracking-wider">
          Packet Trace #{String(traceId).padStart(6, '0')}
        </span>
        <span className="text-[10px] text-cyan-500">
          Σ {totalNs}ns
        </span>
      </div>

      <div className="flex-1 flex flex-col justify-center space-y-1">
        {hops.map((hop, i) => {
          const isActive = i === activeHop;
          const isPast = i < activeHop;
          return (
            <div key={i} className="flex items-center gap-2">
              {/* Stage indicator */}
              <div className={`w-5 h-5 rounded flex items-center justify-center text-[10px] transition-all duration-200 ${
                isActive ? 'ring-1 ring-offset-1 ring-offset-slate-950 scale-110' : ''
              }`}
                style={{
                  backgroundColor: isActive || isPast ? hop.color + '30' : '#0f172a',
                  borderColor: hop.color,
                  borderWidth: 1,
                  color: isActive || isPast ? hop.color : '#334155',
                  boxShadow: isActive ? `0 0 0 2px ${hop.color}40` : undefined,
                }}
              >
                {hop.icon}
              </div>

              {/* Stage name */}
              <span className={`text-[9px] w-20 ${
                isActive ? 'text-white' : isPast ? 'text-slate-400' : 'text-slate-600'
              }`}>
                {hop.stage}
              </span>

              {/* Timing bar */}
              <div className="flex-1 h-3 bg-slate-900 rounded-full overflow-hidden border border-slate-800">
                <div
                  className="h-full rounded-full transition-all duration-300"
                  style={{
                    width: isActive || isPast ? `${(hop.latencyNs / 200) * 100}%` : '0%',
                    backgroundColor: hop.color,
                    opacity: isActive ? 1 : 0.5,
                  }}
                />
              </div>

              {/* Latency */}
              <span className={`text-[9px] w-12 text-right ${
                isActive ? 'text-white' : isPast ? 'text-slate-500' : 'text-slate-700'
              }`}>
                {isPast || isActive ? `${hop.latencyNs}ns` : '—'}
              </span>
            </div>
          );
        })}
      </div>

      {/* Connection line */}
      <div className="flex items-center gap-1 mt-2 pt-2 border-t border-slate-800">
        {hops.map((hop, i) => (
          <div key={i} className="flex items-center flex-1">
            <div className="w-1.5 h-1.5 rounded-full" style={{
              backgroundColor: i <= activeHop ? hop.color : '#1e293b',
            }} />
            {i < hops.length - 1 && (
              <div className="flex-1 h-px" style={{
                backgroundColor: i < activeHop ? hops[i + 1].color : '#1e293b',
              }} />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
