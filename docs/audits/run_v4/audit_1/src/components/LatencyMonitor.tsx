import React, { useState, useEffect, useMemo } from 'react';
import { motion } from 'framer-motion';
import { Activity } from 'lucide-react';
import { MeshMode } from '../types/mesh';

interface LatencyMonitorProps {
  mode: MeshMode;
}

export const LatencyMonitor: React.FC<LatencyMonitorProps> = ({ mode }) => {
  const [data, setData] = useState<number[]>([]);

  useEffect(() => {
    const interval = setInterval(() => {
      // Simulate real-time latency with slight jitter
      // Base latency is lower for 12-CORE mode
      const baseLatency = mode === '12-CORE' ? 1.2 : mode === '6-CORE' ? 3.5 : 5.8;
      const jitter = (Math.random() - 0.5) * 0.4;
      const val = baseLatency + jitter;
      
      setData(prev => {
        const newData = [...prev, val];
        if (newData.length > 20) return newData.slice(1);
        return newData;
      });
    }, 200);
    return () => clearInterval(interval);
  }, [mode]);

  const maxLatency = useMemo(() => Math.max(...data, 0), [data]);
  const avgLatency = useMemo(() => data.length > 0 ? (data.reduce((a, b) => a + b, 0) / data.length) : 0, [data]);

  return (
    <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-5 backdrop-blur-sm">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-sm font-bold text-slate-400 uppercase tracking-widest flex items-center gap-2">
          <Activity size={16} className="text-emerald-500" />
          10µs Jitter Guard
        </h2>
        <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded border ${
          avgLatency < 5 ? 'text-emerald-400 bg-emerald-500/10 border-emerald-500/20' : 'text-orange-400 bg-orange-500/10 border-orange-500/20'
        }`}>
          {avgLatency < 10 ? 'NOMINAL' : 'DEGRADED'}
        </span>
      </div>

      <div className="h-24 flex items-end gap-1 mb-4 border-b border-slate-800 pb-1">
        {data.map((val, i) => (
          <motion.div
            key={i}
            initial={{ height: 0 }}
            animate={{ height: `${(val / 10) * 100}%` }}
            className={`flex-1 rounded-t-sm ${
              val < 5 ? 'bg-emerald-500/60' : val < 8 ? 'bg-cyan-500/60' : 'bg-orange-500/60'
            }`}
          />
        ))}
        {Array.from({ length: 20 - data.length }).map((_, i) => (
          <div key={`empty-${i}`} className="flex-1 bg-slate-800/10 h-0.5 rounded-t-sm" />
        ))}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="bg-slate-950/50 p-2 rounded border border-slate-800/50">
          <p className="text-[10px] text-slate-500 uppercase font-bold mb-1">Avg Latency</p>
          <p className="text-lg font-bold text-emerald-400 tracking-tighter">
            {avgLatency.toFixed(2)} <span className="text-[10px] text-slate-400">µs</span>
          </p>
        </div>
        <div className="bg-slate-950/50 p-2 rounded border border-slate-800/50">
          <p className="text-[10px] text-slate-500 uppercase font-bold mb-1">P99 Jitter</p>
          <p className="text-lg font-bold text-cyan-400 tracking-tighter">
            {maxLatency.toFixed(2)} <span className="text-[10px] text-slate-400">µs</span>
          </p>
        </div>
      </div>

      <div className="mt-4 p-2 bg-emerald-500/5 border border-emerald-500/10 rounded">
        <div className="flex justify-between items-center text-[9px] font-bold text-emerald-500/80 uppercase">
          <span>Target: &lt;10.00 µs</span>
          <span>STATUS: PASSING</span>
        </div>
      </div>
    </div>
  );
};
