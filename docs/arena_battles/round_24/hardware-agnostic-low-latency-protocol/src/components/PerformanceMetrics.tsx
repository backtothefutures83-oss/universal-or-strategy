import { motion } from 'framer-motion';
import { Activity, Zap, BarChart3 } from 'lucide-react';
import type { MetricPoint } from '../types';

interface PerformanceMetricsProps {
  latencyData: MetricPoint[];
  throughputData: MetricPoint[];
  contentionData: MetricPoint[];
}

// Mini sparkline component
function Sparkline({ data, color, height = 60 }: { data: MetricPoint[]; color: string; height?: number }) {
  if (data.length < 2) return null;

  const values = data.map((d) => d.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;
  const width = 240;

  const points = values
    .map((v, i) => {
      const x = (i / (values.length - 1)) * width;
      const y = height - ((v - min) / range) * (height - 10) - 5;
      return `${x},${y}`;
    })
    .join(' ');

  const areaPoints = `0,${height} ${points} ${width},${height}`;

  const current = values[values.length - 1];

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full" style={{ height }}>
      <defs>
        <linearGradient id={`grad-${color}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.2" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <polygon points={areaPoints} fill={`url(#grad-${color})`} />
      <polyline
        points={points}
        fill="none"
        stroke={color}
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      {/* Current value dot */}
      <circle
        cx={width}
        cy={height - ((current - min) / range) * (height - 10) - 5}
        r="3"
        fill={color}
        className="animate-pulse"
      />
    </svg>
  );
}

export function PerformanceMetrics({ latencyData, throughputData, contentionData }: PerformanceMetricsProps) {
  const currentLatency = latencyData.length > 0 ? latencyData[latencyData.length - 1].value : 0;
  const currentThroughput = throughputData.length > 0 ? throughputData[throughputData.length - 1].value : 0;
  const currentContention = contentionData.length > 0 ? contentionData[contentionData.length - 1].value : 0;

  return (
    <div className="p-6 md:p-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-lg bg-blue-500/10 border border-blue-500/20">
          <Activity className="w-5 h-5 text-blue-400" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-white">Real-Time Performance Telemetry</h3>
          <p className="text-xs text-gray-500 font-mono">Marshal-allocated unmanaged metrics</p>
        </div>
      </div>

      {/* Latency Card */}
      <div className="p-4 rounded-xl bg-white/[0.02] border border-white/[0.04] mb-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <Zap className="w-4 h-4 text-amber-400" />
            <span className="text-sm text-gray-400 font-mono">LATENCY</span>
          </div>
          <span className="text-2xl font-bold font-mono text-white">
            {currentLatency.toFixed(3)}<span className="text-sm text-gray-500">ns</span>
          </span>
        </div>
        <Sparkline data={latencyData} color="#10b981" />
        <div className="flex justify-between mt-2 text-[10px] font-mono text-gray-600">
          <span>Target: &lt;0.5ns</span>
          <span>{latencyData.length > 1 ? `${(latencyData[latencyData.length - 1].value - latencyData[latencyData.length - 2].value).toFixed(3)}ns Δ` : '—'}</span>
        </div>
      </div>

      {/* Throughput Card */}
      <div className="p-4 rounded-xl bg-white/[0.02] border border-white/[0.04] mb-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <BarChart3 className="w-4 h-4 text-cyan-400" />
            <span className="text-sm text-gray-400 font-mono">THROUGHPUT</span>
          </div>
          <span className="text-2xl font-bold font-mono text-white">
            {currentThroughput.toFixed(1)}<span className="text-sm text-gray-500">%</span>
          </span>
        </div>
        <Sparkline data={throughputData} color="#06b6d4" />
      </div>

      {/* Contention Card */}
      <div className="p-4 rounded-xl bg-white/[0.02] border border-white/[0.04]">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <Activity className="w-4 h-4 text-purple-400" />
            <span className="text-sm text-gray-400 font-mono">CACHE CONTENTION</span>
          </div>
          <span className={`text-2xl font-bold font-mono ${currentContention > 3 ? 'text-amber-400' : 'text-white'}`}>
            {currentContention.toFixed(1)}
          </span>
        </div>
        <Sparkline data={contentionData} color="#8b5cf6" />
        <div className="mt-2 h-1 rounded-full bg-white/5 overflow-hidden">
          <motion.div
            className={`h-full rounded-full ${currentContention > 3 ? 'bg-amber-400' : currentContention > 2 ? 'bg-cyan-400' : 'bg-emerald-400'}`}
            animate={{ width: `${Math.min(100, currentContention * 25)}%` }}
            transition={{ duration: 0.3 }}
          />
        </div>
      </div>
    </div>
  );
}
