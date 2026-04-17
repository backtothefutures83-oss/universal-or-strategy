import { useEffect, useState } from 'react';
import type { SimulationState } from '../hooks/useSimulation';

interface Props {
  state: SimulationState;
}

export default function MetricsDashboard({ state }: Props) {
  const [history, setHistory] = useState<number[]>([]);

  useEffect(() => {
    setHistory(prev => {
      const next = [...prev, state.pipeMetrics.latencyNs];
      return next.slice(-120);
    });
  }, [state.tick, state.pipeMetrics.latencyNs]);

  return (
    <section className="relative py-20 px-4">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-12">
          <span className="font-mono text-xs text-[#C0A040]/60 tracking-[0.3em] uppercase">Real-Time Telemetry</span>
          <h2 className="text-4xl md:text-5xl font-black text-[#E5E4E2] mt-2">
            System Metrics
          </h2>
        </div>

        {/* Main latency chart */}
        <div className="rounded-xl border border-[#C0A040]/10 bg-[#0D0D14] p-6 mb-8">
          <div className="flex items-center justify-between mb-4">
            <div>
              <span className="font-mono text-xs text-[#B0AFA8]/60 tracking-wider">GATE LATENCY</span>
              <div className="flex items-baseline gap-2 mt-1">
                <span className={`font-mono text-3xl font-bold ${state.gatePassed ? 'text-[#10B981]' : 'text-[#DC2626]'}`}>
                  {state.gateTimeNs.toFixed(0)}
                </span>
                <span className="font-mono text-sm text-[#B0AFA8]/40">ns</span>
                <span className={`font-mono text-xs px-2 py-0.5 rounded ${state.gatePassed ? 'bg-[#10B981]/10 text-[#10B981]' : 'bg-[#DC2626]/10 text-[#DC2626]'}`}>
                  {state.gatePassed ? '< 1µs ✓' : '> 1µs ✗'}
                </span>
              </div>
            </div>
            <div className="text-right">
              <span className="font-mono text-xs text-[#B0AFA8]/60 tracking-wider">THROUGHPUT</span>
              <div className="font-mono text-xl text-[#C0A040] font-bold mt-1">
                {state.pipeMetrics.throughputGbps.toFixed(1)} <span className="text-xs text-[#B0AFA8]/40">Gb/s</span>
              </div>
            </div>
          </div>

          {/* Sparkline */}
          <svg viewBox={`0 0 ${history.length} 100`} className="w-full h-32" preserveAspectRatio="none">
            {/* 1µs threshold line */}
            <line x1={0} y1={50} x2={history.length} y2={50} stroke="#DC2626" strokeWidth={0.5} strokeDasharray="4 4" opacity={0.3} />
            <text x={history.length - 1} y={48} fill="#DC2626" fontSize={4} textAnchor="end" opacity={0.5}>1µs</text>

            {/* Latency line */}
            {history.length > 1 && (
              <polyline
                fill="none"
                stroke="#C0A040"
                strokeWidth={1}
                points={history.map((v, i) => {
                  const y = 100 - ((v - 600) / 500) * 100;
                  return `${i},${Math.max(5, Math.min(95, y))}`;
                }).join(' ')}
              />
            )}

            {/* Fill under line */}
            {history.length > 1 && (
              <polygon
                fill="url(#latencyGrad)"
                opacity={0.15}
                points={`0,100 ${history.map((v, i) => {
                  const y = 100 - ((v - 600) / 500) * 100;
                  return `${i},${Math.max(5, Math.min(95, y))}`;
                }).join(' ')} ${history.length - 1},100`}
              />
            )}

            <defs>
              <linearGradient id="latencyGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#C0A040" />
                <stop offset="100%" stopColor="transparent" />
              </linearGradient>
            </defs>
          </svg>
        </div>

        {/* Metric cards */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <MetricCard
            label="Jitter"
            value={state.pipeMetrics.jitterNs.toFixed(1)}
            unit="ns"
            status={state.pipeMetrics.jitterNs < 20 ? 'good' : 'warn'}
          />
          <MetricCard
            label="Cache Miss Rate"
            value={(state.pipeMetrics.cacheMissRate * 100).toFixed(2)}
            unit="%"
            status={state.pipeMetrics.cacheMissRate < 0.03 ? 'good' : 'warn'}
          />
          <MetricCard
            label="Slab Utilization"
            value={(state.pipeMetrics.slabUtilization * 100).toFixed(1)}
            unit="%"
            status="good"
          />
          <MetricCard
            label="Mirror Status"
            value={state.mirrorActive ? 'ACTIVE' : state.nmiDetected ? 'INJECTING' : 'STANDBY'}
            unit=""
            status={state.nmiDetected ? 'error' : state.mirrorActive ? 'warn' : 'good'}
          />
        </div>
      </div>
    </section>
  );
}

function MetricCard({ label, value, unit, status }: { label: string; value: string; unit: string; status: 'good' | 'warn' | 'error' }) {
  const statusColors = {
    good: { text: 'text-[#10B981]', border: 'border-[#10B981]/15', dot: 'bg-[#10B981]' },
    warn: { text: 'text-[#C0A040]', border: 'border-[#C0A040]/15', dot: 'bg-[#C0A040]' },
    error: { text: 'text-[#DC2626]', border: 'border-[#DC2626]/15', dot: 'bg-[#DC2626]' },
  };
  const c = statusColors[status];

  return (
    <div className={`p-4 rounded-xl border ${c.border} bg-[#12121A]/80`}>
      <div className="flex items-center gap-2 mb-2">
        <div className={`w-2 h-2 rounded-full ${c.dot} animate-pulse`} />
        <span className="font-mono text-[10px] text-[#B0AFA8]/60 tracking-wider uppercase">{label}</span>
      </div>
      <div className="flex items-baseline gap-1">
        <span className={`font-mono text-2xl font-bold ${c.text}`}>{value}</span>
        <span className="font-mono text-xs text-[#B0AFA8]/40">{unit}</span>
      </div>
    </div>
  );
}
