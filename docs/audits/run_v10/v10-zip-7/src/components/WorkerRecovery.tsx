import { useState, useEffect, useCallback } from 'react';
import { v10Design } from '../data/breakthroughs';

function WorkerRing() {
  const [workers, setWorkers] = useState<
    { id: number; status: 'healthy' | 'stalled' | 'recovering' | 'recovered' }[]
  >(Array.from({ length: 12 }, (_, i) => ({ id: i, status: 'healthy' })));
  const [simRunning, setSimRunning] = useState(false);
  const [log, setLog] = useState<string[]>([]);

  const simulate = useCallback(() => {
    setSimRunning(true);
    setLog([]);

    // Pick random worker to stall
    const stallId = Math.floor(Math.random() * 12);
    const detectorId = (stallId + 1) % 12;

    setLog((l) => [...l, `t=0ns: Worker ${stallId} stops writing heartbeat`]);

    setTimeout(() => {
      setWorkers((w) =>
        w.map((wr) => (wr.id === stallId ? { ...wr, status: 'stalled' } : wr))
      );
      setLog((l) => [
        ...l,
        `t=50ns: Worker ${detectorId} detects stale timestamp via Atomics.load()`,
      ]);
    }, 800);

    setTimeout(() => {
      setLog((l) => [
        ...l,
        `t=100ns: Worker ${detectorId} executes Atomics.compareExchange() to claim slot ${stallId}`,
      ]);
      setWorkers((w) =>
        w.map((wr) =>
          wr.id === stallId ? { ...wr, status: 'recovering' } : wr
        )
      );
    }, 1600);

    setTimeout(() => {
      setLog((l) => [
        ...l,
        `t=200ns: Pending items re-enqueued to Ring-Bus SPSC`,
      ]);
    }, 2200);

    setTimeout(() => {
      setLog((l) => [
        ...l,
        `t=350ns: New worker spawned into slot ${stallId} — Arena memory intact ✓`,
      ]);
      setWorkers((w) =>
        w.map((wr) =>
          wr.id === stallId ? { ...wr, status: 'recovered' } : wr
        )
      );
    }, 2800);

    setTimeout(() => {
      setWorkers(
        Array.from({ length: 12 }, (_, i) => ({ id: i, status: 'healthy' }))
      );
      setSimRunning(false);
    }, 4500);
  }, []);

  return (
    <div>
      {/* Ring visualization */}
      <div className="flex justify-center mb-4">
        <div className="relative w-52 h-52">
          {/* Ring circle */}
          <svg
            viewBox="0 0 200 200"
            className="absolute inset-0 w-full h-full"
          >
            <circle
              cx="100"
              cy="100"
              r="75"
              fill="none"
              stroke="#374151"
              strokeWidth="1"
              strokeDasharray="4 4"
            />
            {/* Watch arrows */}
            {workers.map((_, i) => {
              const angle1 = (i * 30 - 90) * (Math.PI / 180);
              const angle2 = ((i + 1) * 30 - 90) * (Math.PI / 180);
              const midAngle = ((i * 30 + 15) - 90) * (Math.PI / 180);
              const r = 58;
              const x1 = 100 + r * Math.cos(angle1);
              const y1 = 100 + r * Math.sin(angle1);
              const x2 = 100 + r * Math.cos(angle2);
              const y2 = 100 + r * Math.sin(angle2);
              const mx = 100 + (r - 3) * Math.cos(midAngle);
              const my = 100 + (r - 3) * Math.sin(midAngle);
              return (
                <g key={i} opacity={0.2}>
                  <line
                    x1={x1}
                    y1={y1}
                    x2={mx}
                    y2={my}
                    stroke="#6b7280"
                    strokeWidth="0.5"
                  />
                  <line
                    x1={mx}
                    y1={my}
                    x2={x2}
                    y2={y2}
                    stroke="#6b7280"
                    strokeWidth="0.5"
                    markerEnd="url(#arrowhead)"
                  />
                </g>
              );
            })}
            <defs>
              <marker
                id="arrowhead"
                markerWidth="6"
                markerHeight="4"
                refX="6"
                refY="2"
                orient="auto"
              >
                <polygon points="0 0, 6 2, 0 4" fill="#6b7280" />
              </marker>
            </defs>
          </svg>
          {/* Worker nodes */}
          {workers.map((w) => {
            const angle = (w.id * 30 - 90) * (Math.PI / 180);
            const r = 75;
            const x = 50 + (r / 200) * 100 * (1 + Math.cos(angle)) - 3;
            const y = 50 + (r / 200) * 100 * (1 + Math.sin(angle)) - 3;
            const colors = {
              healthy: 'bg-emerald-400 shadow-emerald-400/40',
              stalled: 'bg-red-500 shadow-red-500/60 animate-pulse',
              recovering: 'bg-amber-400 shadow-amber-400/40 animate-pulse',
              recovered: 'bg-cyan-400 shadow-cyan-400/40',
            };
            return (
              <div
                key={w.id}
                className={`absolute w-6 h-6 rounded-full ${colors[w.status]} shadow-lg flex items-center justify-center transition-colors duration-300`}
                style={{ left: `${x}%`, top: `${y}%` }}
              >
                <span className="text-[8px] font-mono font-bold text-gray-900">
                  {w.id}
                </span>
              </div>
            );
          })}
          {/* Center label */}
          <div className="absolute inset-0 flex items-center justify-center">
            <div className="text-center">
              <div className="text-[10px] text-gray-500">Watch Ring</div>
              <div className="text-xs font-mono text-gray-400">12 Workers</div>
            </div>
          </div>
        </div>
      </div>

      {/* Simulate button */}
      <div className="flex justify-center mb-4">
        <button
          onClick={simulate}
          disabled={simRunning}
          className={`px-4 py-1.5 rounded-lg text-xs font-medium transition-all ${
            simRunning
              ? 'bg-gray-700 text-gray-500 cursor-not-allowed'
              : 'bg-pink-500/20 text-pink-300 border border-pink-500/30 hover:bg-pink-500/30 cursor-pointer'
          }`}
        >
          {simRunning ? 'Simulating...' : '▶ Simulate Worker Failure'}
        </button>
      </div>

      {/* Log */}
      {log.length > 0 && (
        <div className="bg-gray-950/60 rounded-lg p-3 font-mono text-[10px] space-y-1 max-h-32 overflow-y-auto">
          {log.map((l, i) => (
            <div
              key={i}
              className={`${
                l.includes('✓')
                  ? 'text-emerald-400'
                  : l.includes('stale') || l.includes('stops')
                  ? 'text-red-400'
                  : l.includes('claim') || l.includes('re-enqueued')
                  ? 'text-amber-400'
                  : 'text-gray-400'
              }`}
            >
              {l}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default function WorkerRecovery() {
  const { workerRecovery } = v10Design;
  const [showSteps, setShowSteps] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setShowSteps(true), 400);
    return () => clearTimeout(t);
  }, []);

  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-2xl p-6">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-pink-500/20 flex items-center justify-center text-pink-400 text-sm">
          🔄
        </div>
        <div>
          <h2 className="text-lg font-semibold text-gray-200">
            Worker Recovery Protocol
          </h2>
          <p className="text-xs text-gray-500">{workerRecovery.approach}</p>
        </div>
        <div className="ml-auto text-right">
          <div className="text-2xl font-mono font-bold text-pink-400">
            {workerRecovery.latencyEstimate}ns
          </div>
          <div className="text-[10px] text-gray-500">
            2.85× faster than V8
          </div>
        </div>
      </div>

      {/* Interactive worker ring */}
      <WorkerRing />

      {/* Steps */}
      <div className="mt-4 space-y-2">
        {workerRecovery.steps.map((step, i) => (
          <div
            key={i}
            className={`flex gap-2 transition-all duration-500 ${
              showSteps
                ? 'opacity-100 translate-x-0'
                : 'opacity-0 -translate-x-4'
            }`}
            style={{ transitionDelay: `${i * 100}ms` }}
          >
            <div className="w-4 h-4 rounded-full bg-pink-500/20 border border-pink-500/30 flex items-center justify-center text-[8px] text-pink-400 mt-0.5 shrink-0">
              {i + 1}
            </div>
            <span className="text-[11px] text-gray-400">{step}</span>
          </div>
        ))}
      </div>

      {/* Summary */}
      <div className="mt-4 bg-pink-500/5 border border-pink-500/20 rounded-xl p-3">
        <p className="text-xs text-gray-300 leading-relaxed">
          {workerRecovery.description}
        </p>
      </div>
    </div>
  );
}
