import { useEffect, useState } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, ResponsiveContainer, ReferenceLine } from 'recharts';

interface DataPoint {
  time: number;
  latency: number;
}

export default function TelemetryChart({ targetLatency, currentLatency }: { targetLatency: number, currentLatency: number }) {
  const [data, setData] = useState<DataPoint[]>([]);

  useEffect(() => {
    // Generate initial data points to simulate past latency (0.87 -> target)
    const initialData = Array.from({ length: 20 }, (_, i) => ({
      time: i,
      latency: 0.87 - Math.random() * 0.1, // around 0.87
    }));
    
    setData(initialData);
  }, []);

  useEffect(() => {
    // Append new data every 500ms
    const interval = setInterval(() => {
      setData(prev => {
        const newData = [...prev.slice(1)];
        // Add random jitter to current latency
        const jitter = (Math.random() - 0.5) * 0.05;
        newData.push({
          time: prev[prev.length - 1].time + 1,
          latency: Math.max(0.3, currentLatency + jitter),
        });
        return newData;
      });
    }, 500);

    return () => clearInterval(interval);
  }, [currentLatency]);

  return (
    <ResponsiveContainer width="100%" height="100%">
      <LineChart data={data} margin={{ top: 5, right: 0, left: -20, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#334155" vertical={false} />
        <XAxis dataKey="time" hide />
        <YAxis 
          domain={[0, 1]} 
          tick={{ fontSize: 10, fill: '#64748b' }} 
          axisLine={false} 
          tickLine={false} 
          tickFormatter={(val) => `${val.toFixed(1)}ns`}
        />
        <ReferenceLine y={targetLatency} stroke="#10b981" strokeDasharray="3 3" label={{ position: 'top', value: '< 0.5ns Target', fill: '#10b981', fontSize: 10 }} />
        <Line 
          type="monotone" 
          dataKey="latency" 
          stroke={currentLatency < 0.5 ? '#10b981' : '#3b82f6'} 
          strokeWidth={2} 
          dot={false}
          isAnimationActive={false}
        />
      </LineChart>
    </ResponsiveContainer>
  );
}