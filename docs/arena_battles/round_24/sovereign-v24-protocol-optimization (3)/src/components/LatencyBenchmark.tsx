import { useLatencySimulator } from '../hooks/useSimulation';
import { cn } from '../utils/cn';

export default function LatencyBenchmark() {
  const {
    samples,
    currentLatency,
    avgLatency,
    minLatency,
    maxLatency,
    mode,
    contention,
    isRunning,
    setIsRunning,
  } = useLatencySimulator();

  const maxSampleValue = 0.5;
  const chartWidth = 600;
  const chartHeight = 200;

  const chartPoints = samples.map((s, i) => {
    const x = (i / Math.max(samples.length - 1, 1)) * chartWidth;
    const y = chartHeight - (s.value / maxSampleValue) * chartHeight;
    return `${x},${y}`;
  }).join(' ');

  const areaPoints = `0,${chartHeight} ${chartPoints} ${chartWidth},${chartHeight}`;

  const modeColors: Record<string, string> = {
    'L1-Local': 'text-sov-green',
    'L2-Striped': 'text-sov-amber',
    'L3-Shared': 'text-sov-purple',
    'Hybrid': 'text-sov-cyan',
  };

  return (
    <section id="benchmark" className="py-20 relative">
      <div className="max-w-6xl mx-auto px-4">
        <div className="text-center mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-sov-cyan/20 bg-sov-cyan/5 mb-4">
            <span className="text-xs font-mono text-sov-cyan">LIVE TELEMETRY</span>
          </div>
          <h2 className="text-3xl sm:text-4xl font-bold text-sov-text-bright mb-3">
            Real-Time <span className="text-sov-cyan">Latency Benchmark</span>
          </h2>
          <p className="text-sov-text-dim max-w-xl mx-auto">
            Sub-0.5ns performance under simulated heterogeneous CPU topology and high-interrupt context switching.
          </p>
        </div>

        {/* Main metrics */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-8">
          <div className="p-5 rounded-xl border border-sov-cyan/20 bg-sov-surface/50 text-center box-glow-cyan">
            <div className="text-[10px] font-mono text-sov-text-dim mb-1">CURRENT LATENCY</div>
            <div className="text-3xl font-black font-mono text-sov-cyan glow-cyan">
              {currentLatency.toFixed(3)}
            </div>
            <div className="text-[10px] font-mono text-sov-text-dim">nanoseconds</div>
          </div>

          <div className="p-5 rounded-xl border border-sov-green/20 bg-sov-surface/50 text-center">
            <div className="text-[10px] font-mono text-sov-text-dim mb-1">AVERAGE</div>
            <div className="text-3xl font-black font-mono text-sov-green">
              {avgLatency.toFixed(3)}
            </div>
            <div className="text-[10px] font-mono text-sov-text-dim">nanoseconds</div>
          </div>

          <div className="p-5 rounded-xl border border-sov-border bg-sov-surface/50 text-center">
            <div className="text-[10px] font-mono text-sov-text-dim mb-1">MIN</div>
            <div className="text-3xl font-black font-mono text-sov-text-bright">
              {minLatency.toFixed(3)}
            </div>
            <div className="text-[10px] font-mono text-sov-text-dim">nanoseconds</div>
          </div>

          <div className="p-5 rounded-xl border border-sov-border bg-sov-surface/50 text-center">
            <div className="text-[10px] font-mono text-sov-text-dim mb-1">MAX</div>
            <div className="text-3xl font-black font-mono text-sov-text-bright">
              {maxLatency.toFixed(3)}
            </div>
            <div className="text-[10px] font-mono text-sov-text-dim">nanoseconds</div>
          </div>
        </div>

        {/* Chart */}
        <div className="rounded-xl border border-sov-border bg-sov-surface/30 p-4 mb-6">
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs font-mono text-sov-text-dim">LATENCY OVER TIME (last 60 samples)</span>
            <div className="flex items-center gap-3">
              <span className={cn('text-xs font-mono font-bold', modeColors[mode] || 'text-sov-text-bright')}>
                {mode}
              </span>
              <span className="text-xs font-mono text-sov-text-dim">|</span>
              <span className="text-xs font-mono text-sov-text-dim">Contention: {contention}</span>
            </div>
          </div>

          <div className="relative w-full overflow-hidden" style={{ aspectRatio: '3/1' }}>
            <svg
              viewBox={`0 0 ${chartWidth} ${chartHeight}`}
              className="w-full h-full"
              preserveAspectRatio="none"
            >
              {/* Grid lines */}
              {[0, 0.1, 0.2, 0.3, 0.4, 0.5].map(v => (
                <g key={v}>
                  <line
                    x1="0"
                    y1={chartHeight - (v / maxSampleValue) * chartHeight}
                    x2={chartWidth}
                    y2={chartHeight - (v / maxSampleValue) * chartHeight}
                    stroke="#1a1a3a"
                    strokeWidth="0.5"
                  />
                  <text
                    x="4"
                    y={chartHeight - (v / maxSampleValue) * chartHeight - 4}
                    fill="#64748b"
                    fontSize="8"
                    fontFamily="JetBrains Mono"
                  >
                    {v.toFixed(1)}ns
                  </text>
                </g>
              ))}

              {/* Target line at 0.5ns */}
              <line
                x1="0"
                y1="0"
                x2={chartWidth}
                y2="0"
                stroke="#ef4444"
                strokeWidth="1"
                strokeDasharray="4 4"
                opacity="0.5"
              />

              {/* Area fill */}
              {samples.length > 1 && (
                <polygon
                  points={areaPoints}
                  fill="url(#areaGradient)"
                  opacity="0.3"
                />
              )}

              {/* Line */}
              {samples.length > 1 && (
                <polyline
                  points={chartPoints}
                  fill="none"
                  stroke="#00f0ff"
                  strokeWidth="1.5"
                  strokeLinejoin="round"
                />
              )}

              {/* Current point */}
              {samples.length > 0 && (
                <circle
                  cx={chartWidth}
                  cy={chartHeight - (samples[samples.length - 1].value / maxSampleValue) * chartHeight}
                  r="4"
                  fill="#00f0ff"
                  className="animate-glow-pulse"
                />
              )}

              <defs>
                <linearGradient id="areaGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#00f0ff" stopOpacity="0.4" />
                  <stop offset="100%" stopColor="#00f0ff" stopOpacity="0" />
                </linearGradient>
              </defs>
            </svg>
          </div>
        </div>

        {/* Controls */}
        <div className="flex items-center justify-center gap-4">
          <button
            onClick={() => setIsRunning(!isRunning)}
            className={cn(
              'px-6 py-2.5 rounded-lg border font-mono text-sm transition-all active:scale-95',
              isRunning
                ? 'bg-sov-red/10 border-sov-red/30 text-sov-red hover:bg-sov-red/20'
                : 'bg-sov-green/10 border-sov-green/30 text-sov-green hover:bg-sov-green/20'
            )}
          >
            {isRunning ? '⏸ Pause Simulation' : '▶ Resume Simulation'}
          </button>
        </div>

        {/* Performance verdict */}
        <div className={cn(
          'mt-8 p-6 rounded-xl border text-center',
          avgLatency < 0.5
            ? 'border-sov-green/30 bg-sov-green/5'
            : 'border-sov-red/30 bg-sov-red/5'
        )}>
          <div className="text-xs font-mono text-sov-text-dim mb-2">PERFORMANCE VERDICT</div>
          <div className={cn(
            'text-2xl font-black font-mono',
            avgLatency < 0.5 ? 'text-sov-green glow-green' : 'text-sov-red'
          )}>
            {avgLatency < 0.5 ? '✓ TARGET ACHIEVED' : '✕ TARGET MISSED'}
          </div>
          <div className="text-xs font-mono text-sov-text-dim mt-1">
            Average {avgLatency.toFixed(3)}ns {'<'} 0.500ns threshold
          </div>
        </div>
      </div>
    </section>
  );
}
