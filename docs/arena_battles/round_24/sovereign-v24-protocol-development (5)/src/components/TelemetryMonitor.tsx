import { useState, useEffect } from 'react';
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Area, AreaChart
} from 'recharts';

export const TelemetryMonitor: React.FC = () => {
  const [data, setData] = useState(Array.from({ length: 50 }, (_, i) => ({
    time: i,
    latency: 0.87, // Baseline V23.1
    throughput: 120,
    contention: 10
  })));

  useEffect(() => {
    const interval = setInterval(() => {
      setData(currentData => {
        const newData = [...currentData];
        newData.shift(); // Remove oldest
        
        const last = newData[newData.length - 1];
        
        // Target is < 0.50ns. Let's simulate hovering around 0.45ns with occasional spikes up to 0.49ns
        const newLatency = 0.45 + (Math.random() * 0.04 - 0.02);
        
        // Random throughput and contention
        const newThroughput = 150 + Math.random() * 30;
        const newContention = 5 + Math.random() * 8;

        newData.push({
          time: last.time + 1,
          latency: parseFloat(newLatency.toFixed(3)),
          throughput: Math.floor(newThroughput),
          contention: Math.floor(newContention)
        });

        return newData;
      });
    }, 100); // 100ms updates

    return () => clearInterval(interval);
  }, []);

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 h-full">
      <div className="bg-black/40 border border-emerald-900/50 p-4 rounded-lg relative overflow-hidden backdrop-blur">
        <div className="flex justify-between items-center mb-2">
          <h3 className="text-emerald-400 font-bold uppercase text-xs tracking-widest">Latency (ns) - Target &lt; 0.50ns</h3>
          <span className="text-xs font-mono text-cyan-400 font-bold bg-cyan-950/50 px-2 py-1 rounded">
            CURRENT: {data[data.length - 1].latency} ns
          </span>
        </div>
        <div className="h-48 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 5, right: 0, left: -20, bottom: 0 }}>
              <defs>
                <linearGradient id="colorLatency" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#10b981" stopOpacity={0.8}/>
                  <stop offset="95%" stopColor="#10b981" stopOpacity={0}/>
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#064e3b" vertical={false} />
              <XAxis dataKey="time" hide />
              <YAxis domain={[0.4, 0.6]} stroke="#064e3b" tick={{fill: '#059669', fontSize: 10}} />
              <Tooltip 
                contentStyle={{ backgroundColor: '#022c22', border: '1px solid #064e3b', borderRadius: '4px' }}
                itemStyle={{ color: '#10b981' }}
                labelStyle={{ display: 'none' }}
              />
              {/* Target Line */}
              <Line type="monotone" dataKey={() => 0.50} stroke="#ef4444" strokeWidth={1} strokeDasharray="5 5" dot={false} />
              <Area type="monotone" dataKey="latency" stroke="#10b981" fillOpacity={1} fill="url(#colorLatency)" isAnimationActive={false} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="bg-black/40 border border-emerald-900/50 p-4 rounded-lg relative overflow-hidden backdrop-blur">
        <div className="flex justify-between items-center mb-2">
          <h3 className="text-emerald-400 font-bold uppercase text-xs tracking-widest">Throughput (MT/s)</h3>
          <span className="text-xs font-mono text-purple-400 font-bold bg-purple-950/50 px-2 py-1 rounded">
            {data[data.length - 1].throughput} MT/s
          </span>
        </div>
        <div className="h-48 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={data} margin={{ top: 5, right: 0, left: -20, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#4c1d95" vertical={false} />
              <XAxis dataKey="time" hide />
              <YAxis domain={[100, 200]} stroke="#4c1d95" tick={{fill: '#8b5cf6', fontSize: 10}} />
              <Tooltip 
                contentStyle={{ backgroundColor: '#2e1065', border: '1px solid #4c1d95', borderRadius: '4px' }}
                itemStyle={{ color: '#8b5cf6' }}
                labelStyle={{ display: 'none' }}
              />
              <Line type="monotone" dataKey="throughput" stroke="#8b5cf6" strokeWidth={2} dot={false} isAnimationActive={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
};
