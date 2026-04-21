import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import type { LatencyPoint } from '../hooks/useSovereignSimulation';

interface Props {
  data: LatencyPoint[];
  currentLatency: LatencyPoint;
}

export function LatencyMonitor({ data, currentLatency }: Props) {
  const targetLine = 0.5;
  
  const CustomTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ value: number; color: string; payload?: { time?: string; mode?: string } }> }) => {
    if (active && payload && payload.length) {
      const val = payload[0].value;
      const pt = payload[0].payload;
      return (
        <div className="bg-sov-panel border border-sov-border rounded-lg p-2.5 shadow-xl">
          <div className="text-xs text-sov-text-dim">{pt?.time}</div>
          <div className={`text-sm font-bold font-mono ${val < targetLine ? 'text-sov-green' : 'text-sov-red'}`}>
            {val.toFixed(3)} ns
          </div>
          <div className="text-xs text-sov-text-muted">{pt?.mode}</div>
        </div>
      );
    }
    return null;
  };

  const modeColor = currentLatency.latency < 0.4 
    ? 'text-sov-green' 
    : currentLatency.latency < 0.5 
    ? 'text-sov-amber' 
    : 'text-sov-red';

  const modeBg = currentLatency.latency < 0.4 
    ? 'bg-sov-green/10 border-sov-green/30' 
    : currentLatency.latency < 0.5 
    ? 'bg-sov-amber/10 border-sov-amber/30' 
    : 'bg-sov-red/10 border-sov-red/30';

  const modeDot = currentLatency.latency < 0.4 
    ? 'bg-sov-green' 
    : currentLatency.latency < 0.5 
    ? 'bg-sov-amber' 
    : 'bg-sov-red';

  return (
    <div className="panel-glow rounded-xl border border-sov-border bg-sov-panel p-5 relative overflow-hidden">
      <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-10">
        <div className="w-full h-px bg-gradient-to-r from-transparent via-sov-green to-transparent animate-scan-line" style={{ animationDuration: '6s' }} />
      </div>

      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-sov-green/20 to-sov-green/5 border border-sov-green/30 flex items-center justify-center text-sm">
            ⚡
          </div>
          <div>
            <h3 className="text-sm font-semibold text-sov-text">Latency Monitor</h3>
            <p className="text-xs text-sov-text-muted font-mono">Sub-0.5ns Target — ADR-015</p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className={`flex items-center gap-2 px-3 py-1.5 rounded-full border ${modeBg}`}>
            <span className={`w-2 h-2 rounded-full ${modeDot} animate-pulse-glow`} />
            <span className={`text-sm font-bold font-mono ${modeColor}`}>
              {currentLatency.latency.toFixed(3)} ns
            </span>
          </div>
          <div className="text-xs text-sov-text-muted font-mono px-2 py-1 bg-sov-dark/40 rounded border border-sov-border">
            Target: &lt;0.5ns
          </div>
        </div>
      </div>

      {/* Chart */}
      <div className="h-48 w-full -ml-2">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={data}>
            <defs>
              <linearGradient id="latencyGreen" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#00ff88" stopOpacity={0.3} />
                <stop offset="100%" stopColor="#00ff88" stopOpacity={0.02} />
              </linearGradient>
              <linearGradient id="latencyAmber" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#ffab00" stopOpacity={0.3} />
                <stop offset="100%" stopColor="#ffab00" stopOpacity={0.02} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e2136" vertical={false} />
            <XAxis 
              dataKey="time" 
              tick={{ fill: '#4a5068', fontSize: 10 }} 
              tickLine={false}
              axisLine={{ stroke: '#1e2136' }}
              tickFormatter={(v: string) => {
                const parts = v.split(':');
                return parts.length >= 3 ? `${parts[1]}:${parts[2]}` : v;
              }}
              interval={Math.max(0, Math.floor(data.length / 8))}
            />
            <YAxis 
              domain={[0.25, 0.55]} 
              tick={{ fill: '#4a5068', fontSize: 10 }} 
              tickLine={false}
              axisLine={{ stroke: '#1e2136' }}
              tickFormatter={(v: number) => v.toFixed(2)}
            />
            <Tooltip content={<CustomTooltip />} />
            {/* Target line */}
            <line
              x1="0"
              y1="80%"
              x2="100%"
              y2="80%"
              stroke="#ff3366"
              strokeWidth={1}
              strokeDasharray="6 3"
              opacity={0.5}
            />
            <Area
              type="monotone"
              dataKey="latency"
              stroke="#00ff88"
              strokeWidth={1.5}
              fill="url(#latencyGreen)"
              dot={false}
              animationDuration={200}
              isAnimationActive={false}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      {/* Legend */}
      <div className="flex items-center justify-between mt-2 text-xs text-sov-text-muted">
        <div className="flex gap-4">
          <span className="flex items-center gap-1.5">
            <span className="w-3 h-0.5 bg-sov-green rounded" />
            Measured
          </span>
          <span className="flex items-center gap-1.5">
            <span className="w-3 h-0.5 bg-sov-red rounded opacity-50" style={{ borderTop: '1px dashed #ff3366' }} />
            0.5ns Target
          </span>
        </div>
        <span className="font-mono">
          {data.length} samples
        </span>
      </div>
    </div>
  );
}
