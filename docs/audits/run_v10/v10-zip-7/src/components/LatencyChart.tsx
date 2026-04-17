import { useState, useEffect } from 'react';
import { breakthroughs, v10Design } from '../data/breakthroughs';

export default function LatencyChart() {
  const [animated, setAnimated] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setAnimated(true), 300);
    return () => clearTimeout(t);
  }, []);

  const allPoints = [
    ...breakthroughs.map((b) => ({
      label: b.name,
      round: b.round,
      latency: b.latency,
      color: categoryColor(b.category),
    })),
    {
      label: 'Phantom Gate',
      round: 'V10',
      latency: v10Design.targetLatency,
      color: '#22d3ee',
    },
  ].sort((a, b) => b.latency - a.latency);

  const maxLatency = 520;

  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-2xl p-6">
      <h2 className="text-lg font-semibold text-gray-200 mb-1">
        Latency Evolution Timeline
      </h2>
      <p className="text-xs text-gray-500 mb-6">
        All breakthroughs V7→V10 · Lower is better
      </p>
      <div className="space-y-3">
        {allPoints.map((p, i) => {
          const pct = (p.latency / maxLatency) * 100;
          const isV10 = p.round === 'V10';
          return (
            <div key={i} className="flex items-center gap-3">
              <span
                className={`text-[10px] font-mono w-8 text-right ${
                  isV10 ? 'text-cyan-400 font-bold' : 'text-gray-500'
                }`}
              >
                {p.round}
              </span>
              <div className="flex-1 relative h-7 bg-gray-800/60 rounded-lg overflow-hidden">
                <div
                  className="absolute inset-y-0 left-0 rounded-lg transition-all duration-1000 ease-out flex items-center"
                  style={{
                    width: animated ? `${pct}%` : '0%',
                    background: isV10
                      ? 'linear-gradient(90deg, #06b6d4, #22d3ee, #67e8f9)'
                      : `linear-gradient(90deg, ${p.color}88, ${p.color}cc)`,
                    transitionDelay: `${i * 80}ms`,
                    boxShadow: isV10 ? '0 0 20px #22d3ee44' : 'none',
                  }}
                >
                  <span
                    className={`text-[10px] font-medium pl-2 whitespace-nowrap ${
                      isV10 ? 'text-gray-900 font-bold' : 'text-white/90'
                    }`}
                  >
                    {p.label}
                  </span>
                </div>
                <span
                  className={`absolute right-2 top-1/2 -translate-y-1/2 text-[11px] font-mono ${
                    isV10 ? 'text-cyan-300 font-bold' : 'text-gray-400'
                  }`}
                >
                  {p.latency}ns
                </span>
              </div>
            </div>
          );
        })}
      </div>
      {/* Target line */}
      <div className="mt-4 flex items-center gap-2 text-xs text-cyan-400/80">
        <div className="h-px flex-1 bg-gradient-to-r from-transparent via-cyan-400/40 to-transparent" />
        <span className="font-mono">TARGET: &lt;200ns</span>
        <div className="h-px flex-1 bg-gradient-to-r from-transparent via-cyan-400/40 to-transparent" />
      </div>
    </div>
  );
}

function categoryColor(cat: string) {
  switch (cat) {
    case 'dispatch':
      return '#a78bfa';
    case 'recovery':
      return '#f472b6';
    case 'memory':
      return '#34d399';
    case 'cache':
      return '#fbbf24';
    case 'queue':
      return '#60a5fa';
    default:
      return '#9ca3af';
  }
}
