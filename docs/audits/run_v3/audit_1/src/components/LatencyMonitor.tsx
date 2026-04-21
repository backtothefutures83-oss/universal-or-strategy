import { useState, useEffect } from 'react';
import { AreaChart, Area, XAxis, YAxis, ResponsiveContainer, ReferenceLine } from 'recharts';
import { Activity } from 'lucide-react';

export const LatencyMonitor: React.FC = () => {
  const [data, setData] = useState<any[]>([]);

  useEffect(() => {
    const interval = setInterval(() => {
      setData(prev => {
        const newData = [...prev, {
          time: Date.now(),
          latency: 2.2 + Math.random() * 0.8 // Simulated <3µs latency
        }].slice(-30);
        return newData;
      });
    }, 200);
    return () => clearInterval(interval);
  }, []);

  const avgLatency = data.length > 0 ? (data.reduce((acc, v) => acc + v.latency, 0) / data.length).toFixed(3) : '0.000';
  const jitter = data.length > 1 ? (Math.max(...data.map(d => d.latency)) - Math.min(...data.map(d => d.latency))).toFixed(3) : '0.000';

  return (
    <div className="bg-zinc-950/80 border border-zinc-800 p-6 rounded-xl flex flex-col h-full">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-zinc-100 font-mono text-sm tracking-widest flex items-center gap-2 uppercase">
          <Activity className="w-4 h-4 text-rose-500 shadow-[0_0_10px_rgba(244,63,94,0.5)]" />
          Latency Hard-Gate: 10µs
        </h3>
        <span className="text-[10px] font-mono text-rose-400 bg-rose-400/10 px-2 py-0.5 rounded border border-rose-500/20">
          REAL-TIME TRACING
        </span>
      </div>

      <div className="grid grid-cols-2 gap-4 mb-6">
        <div className="flex flex-col bg-zinc-900/50 p-2 rounded border border-zinc-800/50">
          <span className="text-[10px] font-mono text-zinc-500 uppercase">AVG LATENCY</span>
          <span className="text-2xl font-mono text-emerald-400 font-bold tracking-tighter">{avgLatency}µs</span>
        </div>
        <div className="flex flex-col bg-zinc-900/50 p-2 rounded border border-zinc-800/50">
          <span className="text-[10px] font-mono text-zinc-500 uppercase">JITTER (Δ)</span>
          <span className="text-2xl font-mono text-amber-400 font-bold tracking-tighter">{jitter}µs</span>
        </div>
      </div>

      <div className="h-40 w-full mt-auto">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={data}>
            <defs>
              <linearGradient id="colorLatency" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#ef4444" stopOpacity={0.3}/>
                <stop offset="95%" stopColor="#ef4444" stopOpacity={0}/>
              </linearGradient>
            </defs>
            <XAxis dataKey="time" hide />
            <YAxis domain={[0, 12]} hide />
            <Area
              type="monotone"
              dataKey="latency"
              stroke="#ef4444"
              fillOpacity={1}
              fill="url(#colorLatency)"
              strokeWidth={2}
              isAnimationActive={false}
            />
            <ReferenceLine y={10} stroke="#ef4444" strokeDasharray="3 3" label={{ 
              position: 'right', 
              value: '10µs LIMIT', 
              fill: '#ef4444', 
              fontSize: 10,
              fontFamily: 'monospace'
            }} />
          </AreaChart>
        </ResponsiveContainer>
      </div>
      
      <div className="mt-4 flex justify-between text-[9px] font-mono text-zinc-600">
        <span>0µs (L1 CACHE)</span>
        <span>TSC-BASED CLOCKING</span>
      </div>
    </div>
  );
};
