import { useState, useEffect, useRef } from 'react';

interface Metric {
  label: string;
  value: number;
  unit: string;
  min: number;
  max: number;
  color: string;
}

const baseMetrics: Metric[] = [
  { label: 'Dispatch Latency', value: 180, unit: 'ns', min: 170, max: 195, color: '#22d3ee' },
  { label: 'Worker Heartbeat', value: 20, unit: 'ns', min: 15, max: 28, color: '#34d399' },
  { label: 'CAS Recovery', value: 350, unit: 'ns', min: 310, max: 390, color: '#f472b6' },
  { label: 'Cache Hit Rate', value: 99.7, unit: '%', min: 99.2, max: 99.9, color: '#fbbf24' },
  { label: 'Ring Queue Depth', value: 3, unit: 'items', min: 0, max: 12, color: '#a78bfa' },
  { label: 'Arena Utilization', value: 67, unit: '%', min: 55, max: 82, color: '#60a5fa' },
];

export default function LiveMetrics() {
  const [metrics, setMetrics] = useState(baseMetrics);
  const intervalRef = useRef<ReturnType<typeof setInterval>>(null);

  useEffect(() => {
    intervalRef.current = setInterval(() => {
      setMetrics((prev) =>
        prev.map((m) => ({
          ...m,
          value:
            Math.round(
              (m.min + Math.random() * (m.max - m.min)) *
                (m.unit === '%' ? 10 : 1)
            ) / (m.unit === '%' ? 10 : 1),
        }))
      );
    }, 16); // ~60fps — demonstrates the zero-reflow concept

    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  }, []);

  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-2xl p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h2 className="text-lg font-semibold text-gray-200">
            Live Telemetry
          </h2>
          <p className="text-xs text-gray-500">
            60fps updates · zero-reflow via tabular-nums
          </p>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
          <span className="text-[10px] text-emerald-400 font-mono">LIVE</span>
        </div>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {metrics.map((m, i) => (
          <div
            key={i}
            className="bg-gray-800/50 rounded-xl p-3 border border-gray-700/20"
          >
            <div className="text-[10px] text-gray-500 mb-1">{m.label}</div>
            <div className="flex items-baseline gap-1">
              <span
                className="text-xl font-mono font-bold tabular-nums"
                style={{ color: m.color, fontVariantNumeric: 'tabular-nums' }}
              >
                {m.value}
              </span>
              <span className="text-[10px] text-gray-500">{m.unit}</span>
            </div>
            {/* Mini sparkline bar */}
            <div className="mt-2 h-1 bg-gray-700/50 rounded-full overflow-hidden">
              <div
                className="h-full rounded-full transition-all duration-100"
                style={{
                  width: `${((m.value - m.min) / (m.max - m.min)) * 100}%`,
                  backgroundColor: m.color,
                  opacity: 0.6,
                }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
