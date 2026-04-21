import { useState, useEffect } from 'react';

interface MetricPoint {
  timestamp: number;
  value: number;
}

function generateMetricData(base: number, variance: number, count: number = 50): MetricPoint[] {
  return Array.from({ length: count }, (_, i) => ({
    timestamp: i,
    value: base + (Math.random() - 0.5) * variance + Math.sin(i / 5) * variance * 0.3,
  }));
}

function MiniChart({ data, color, height = 60 }: { data: MetricPoint[]; color: string; height?: number }) {
  const max = Math.max(...data.map((d) => d.value));
  const min = Math.min(...data.map((d) => d.value));
  const range = max - min || 1;

  const points = data
    .map((d, i) => {
      const x = (i / (data.length - 1)) * 100;
      const y = height - ((d.value - min) / range) * (height - 10) - 5;
      return `${x},${y}`;
    })
    .join(' ');

  return (
    <svg viewBox={`0 0 100 ${height}`} className="w-full" style={{ height }}>
      <polyline
        points={points}
        fill="none"
        stroke={color}
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export default function PerformanceMetrics() {
  const [, setTick] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => setTick((t) => t + 1), 2000);
    return () => clearInterval(interval);
  }, []);

  const metrics = [
    {
      name: 'End-to-End Latency',
      target: '< 10µs',
      current: (8.2 + Math.random() * 2).toFixed(1) + 'µs',
      status: 'healthy',
      data: generateMetricData(9, 3),
      color: '#10b981',
    },
    {
      name: 'SLUB Jitter',
      target: '< 1µs',
      current: (0.4 + Math.random() * 0.5).toFixed(2) + 'µs',
      status: 'healthy',
      data: generateMetricData(0.5, 0.5),
      color: '#06b6d4',
    },
    {
      name: 'Worker Thread Serialization',
      target: '< 5µs',
      current: (2.1 + Math.random() * 3).toFixed(1) + 'µs',
      status: 'warning',
      data: generateMetricData(3, 4),
      color: '#f59e0b',
    },
    {
      name: 'Redis Lua Execution',
      target: '< 50µs',
      current: (18 + Math.random() * 15).toFixed(0) + 'µs',
      status: 'healthy',
      data: generateMetricData(25, 20),
      color: '#8b5cf6',
    },
    {
      name: 'Page Faults/sec',
      target: '0',
      current: '0',
      status: 'healthy',
      data: generateMetricData(0, 0.1),
      color: '#10b981',
    },
    {
      name: 'Memory Lock Status',
      target: '100%',
      current: '100%',
      status: 'healthy',
      data: generateMetricData(100, 0),
      color: '#10b981',
    },
  ];

  return (
    <div className="space-y-6">
      {/* Header Stats */}
      <div className="grid grid-cols-4 gap-4">
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
          <div className="text-xs text-slate-500 uppercase tracking-wider mb-1">P50 Latency</div>
          <div className="text-2xl font-bold text-emerald-400">8.2µs</div>
          <div className="text-xs text-slate-400">Target: &lt;10µs</div>
        </div>
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
          <div className="text-xs text-slate-500 uppercase tracking-wider mb-1">P99 Latency</div>
          <div className="text-2xl font-bold text-cyan-400">12.4µs</div>
          <div className="text-xs text-slate-400">Target: &lt;15µs</div>
        </div>
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
          <div className="text-xs text-slate-500 uppercase tracking-wider mb-1">Jitter (σ)</div>
          <div className="text-2xl font-bold text-amber-400">1.8µs</div>
          <div className="text-xs text-slate-400">Target: &lt;2µs</div>
        </div>
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
          <div className="text-xs text-slate-500 uppercase tracking-wider mb-1">Throughput</div>
          <div className="text-2xl font-bold text-emerald-400">125K/s</div>
          <div className="text-xs text-slate-400">Per engine</div>
        </div>
      </div>

      {/* Detailed Metrics */}
      <div className="bg-slate-900/50 border border-slate-700 rounded-xl p-6">
        <h3 className="font-bold text-lg mb-4">Real-Time Metrics (Simulated)</h3>
        <div className="grid grid-cols-2 gap-4">
          {metrics.map((metric, idx) => (
            <div key={idx} className="bg-slate-800/50 rounded-lg p-4 border border-slate-700">
              <div className="flex items-center justify-between mb-2">
                <span className="font-medium text-slate-200">{metric.name}</span>
                <span
                  className={`px-2 py-0.5 rounded text-xs font-bold ${
                    metric.status === 'healthy'
                      ? 'bg-emerald-500/20 text-emerald-400'
                      : 'bg-amber-500/20 text-amber-400'
                  }`}
                >
                  {metric.current}
                </span>
              </div>
              <MiniChart data={metric.data} color={metric.color} />
              <div className="flex items-center justify-between text-xs text-slate-500 mt-2">
                <span>Target: {metric.target}</span>
                <span className={metric.status === 'healthy' ? 'text-emerald-400' : 'text-amber-400'}>
                  {metric.status === 'healthy' ? '✓ Within bounds' : '⚠ Monitor closely'}
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Latency Distribution */}
      <div className="bg-slate-900/50 border border-slate-700 rounded-xl p-6">
        <h3 className="font-bold text-lg mb-4">Latency Distribution</h3>
        <div className="flex items-end justify-between h-40 gap-1">
          {[
            { label: '0-5µs', count: 12, color: 'bg-emerald-500' },
            { label: '5-10µs', count: 45, color: 'bg-emerald-500' },
            { label: '10-15µs', count: 28, color: 'bg-cyan-500' },
            { label: '15-20µs', count: 10, color: 'bg-amber-500' },
            { label: '20-25µs', count: 4, color: 'bg-orange-500' },
            { label: '25-30µs', count: 1, color: 'bg-red-500' },
          ].map((bucket, idx) => (
            <div key={idx} className="flex-1 flex flex-col items-center">
              <div
                className={`w-full ${bucket.color} rounded-t transition-all`}
                style={{ height: `${bucket.count * 2}%` }}
              />
              <div className="text-xs text-slate-400 mt-2 text-center">{bucket.label}</div>
              <div className="text-xs text-slate-500">{bucket.count}%</div>
            </div>
          ))}
        </div>
      </div>

      {/* Memory Metrics */}
      <div className="grid grid-cols-3 gap-4">
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
          <div className="text-sm text-slate-400 mb-2">Slab Pool Utilization</div>
          <div className="text-3xl font-bold text-cyan-400">67%</div>
          <div className="mt-2 h-2 bg-slate-700 rounded-full overflow-hidden">
            <div className="h-full bg-cyan-500 rounded-full" style={{ width: '67%' }} />
          </div>
          <div className="text-xs text-slate-500 mt-1">6.7GB / 10GB allocated</div>
        </div>
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
          <div className="text-sm text-slate-400 mb-2">Locked Memory</div>
          <div className="text-3xl font-bold text-emerald-400">100%</div>
          <div className="mt-2 h-2 bg-slate-700 rounded-full overflow-hidden">
            <div className="h-full bg-emerald-500 rounded-full" style={{ width: '100%' }} />
          </div>
          <div className="text-xs text-slate-500 mt-1">12.4GB mlockall'd</div>
        </div>
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
          <div className="text-sm text-slate-400 mb-2">Heap Operations</div>
          <div className="text-3xl font-bold text-emerald-400">0</div>
          <div className="mt-2 h-2 bg-slate-700 rounded-full overflow-hidden">
            <div className="h-full bg-emerald-500 rounded-full" style={{ width: '0%' }} />
          </div>
          <div className="text-xs text-slate-500 mt-1">Hot path bypasses heap</div>
        </div>
      </div>
    </div>
  );
}
