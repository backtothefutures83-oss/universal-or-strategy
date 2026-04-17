import { useState, useEffect, useRef } from 'react';

export function StripingVisualizer() {
  const [isInView, setIsInView] = useState(false);
  const sectionRef = useRef<HTMLDivElement>(null);
  const [currentMode, setCurrentMode] = useState<'L1-local' | 'L2-striped'>('L1-local');
  const [contention, setContention] = useState(15);
  const [simulationTime, setSimulationTime] = useState(0);
  const [modeHistory, setModeHistory] = useState<{ time: number; mode: string; latency: number }[]>([]);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => { if (entry.isIntersecting) setIsInView(true); },
      { threshold: 0.1 }
    );
    if (sectionRef.current) observer.observe(sectionRef.current);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!isInView) return;

    const interval = setInterval(() => {
      setSimulationTime(prev => prev + 1);
      setContention(prev => {
        // Simulate workload variation
        const variation = Math.sin(prev / 10) * 15 + Math.sin(prev / 3) * 10;
        return Math.max(5, Math.min(95, 40 + variation));
      });
    }, 500);

    return () => clearInterval(interval);
  }, [isInView]);

  useEffect(() => {
    if (contention > 50 && currentMode === 'L1-local') {
      setCurrentMode('L2-striped');
    } else if (contention <= 50 && currentMode === 'L2-striped') {
      setCurrentMode('L1-local');
    }

    setModeHistory(prev => {
      const latency = currentMode === 'L1-local' ? 0.32 + contention * 0.005 : 0.45 - contention * 0.001;
      const entry = { time: simulationTime, mode: currentMode, latency: Math.max(0.25, latency) };
      return [...prev.slice(-50), entry];
    });
  }, [contention]);

  const getModeLatency = () => {
    if (currentMode === 'L1-local') {
      return Math.max(0.25, 0.32 + contention * 0.005);
    }
    return Math.max(0.35, 0.45 - contention * 0.001);
  };

  // Draw the mode history chart
  const chartWidth = 600;
  const chartHeight = 120;
  const padding = 30;

  const chartPoints = modeHistory.length > 1
    ? modeHistory.map((entry, i) => {
        const x = padding + (i / 50) * (chartWidth - padding * 2);
        const y = padding + (1 - (entry.latency - 0.25) / 0.35) * (chartHeight - padding * 2);
        return `${x},${y}`;
      }).join(' ')
    : '';

  return (
    <section ref={sectionRef} id="striping" className="py-24 px-4 hex-bg relative">
      <div className="absolute bottom-0 left-0 w-96 h-96 rounded-full bg-amber-glow/5 animate-pulse-glow pointer-events-none" />

      <div className="max-w-6xl mx-auto relative z-10">
        {/* Header */}
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-amber-glow/10 bg-amber-glow/5 mb-4">
            <span className="text-xs font-mono text-amber-glow/60 tracking-widest uppercase">MANDATE 3</span>
          </div>
          <h2 className="text-3xl md:text-5xl font-bold text-white mb-4">
            Adaptive <span className="text-amber-glow">Striping</span>
          </h2>
          <p className="text-sov-400 max-w-2xl mx-auto">
            Friction-less scaling: the core adaptively shifts between L1-local and L2-striped modes
            based on real-time cache contention diagnostics. No manual tuning required.
          </p>
        </div>

        {/* Live simulation */}
        <div className="grid lg:grid-cols-2 gap-6 mb-8">
          {/* Cache visualization */}
          <div className="glass-panel rounded-xl p-6 glow-border">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-mono text-white">Live Cache Topology</h3>
              <div className={`px-2 py-1 rounded text-xs font-mono ${
                currentMode === 'L1-local' 
                  ? 'bg-teal-glow/10 text-teal-glow border border-teal-glow/20' 
                  : 'bg-amber-glow/10 text-amber-glow border border-amber-glow/20'
              }`}>
                MODE: {currentMode.toUpperCase()}
              </div>
            </div>

            {/* Visual cache representation */}
            <svg viewBox="0 0 300 200" className="w-full">
              {/* L3 Cache */}
              <rect x={30} y={140} width={240} height={50} rx={6} fill="#ffb30010" stroke="#ffb30040" strokeWidth={1} />
              <text x={150} y={165} textAnchor="middle" fill="#ffb300" fontSize={10} fontFamily="monospace">L3 — 36 MB Shared</text>
              <text x={150} y={180} textAnchor="middle" fill="#ffb30080" fontSize={8} fontFamily="monospace">64B cache lines</text>

              {/* L2 Caches */}
              <rect x={50} y={90} width={90} height={40} rx={4} fill="#00f0ff10" stroke={currentMode === 'L2-striped' ? '#00f0ff80' : '#00f0ff30'} strokeWidth={currentMode === 'L2-striped' ? 2 : 1} />
              <text x={95} y={110} textAnchor="middle" fill="#00f0ff" fontSize={9} fontFamily="monospace">L2 Core 0-3</text>
              <text x={95} y={122} textAnchor="middle" fill="#00f0ff80" fontSize={7} fontFamily="monospace">1.25 MB</text>

              <rect x={160} y={90} width={90} height={40} rx={4} fill="#00f0ff10" stroke={currentMode === 'L2-striped' ? '#00f0ff80' : '#00f0ff30'} strokeWidth={currentMode === 'L2-striped' ? 2 : 1} />
              <text x={205} y={110} textAnchor="middle" fill="#00f0ff" fontSize={9} fontFamily="monospace">L2 Core 4-7</text>
              <text x={205} y={122} textAnchor="middle" fill="#00f0ff80" fontSize={7} fontFamily="monospace">1.25 MB</text>

              {/* L1 Caches */}
              {[70, 110, 150, 190, 230, 270].map((x, i) => {
                const isActive = currentMode === 'L1-local';
                return (
                  <g key={i}>
                    <rect x={x - 15} y={40} width={30} height={40} rx={3} 
                      fill={isActive ? '#00ffc815' : '#00ffc808'} 
                      stroke={isActive ? '#00ffc880' : '#00ffc820'} 
                      strokeWidth={isActive ? 1.5 : 1} />
                    <text x={x} y={60} textAnchor="middle" fill={isActive ? '#00ffc8' : '#00ffc860'} fontSize={7} fontFamily="monospace">
                      L1.{i}
                    </text>
                    <text x={x} y={72} textAnchor="middle" fill={isActive ? '#00ffc880' : '#00ffc830'} fontSize={6} fontFamily="monospace">
                      48K
                    </text>
                  </g>
                );
              })}

              {/* Data flow lines */}
              {currentMode === 'L1-local' ? (
                <>
                  {[70, 110, 150, 190, 230, 270].map((x, i) => (
                    <line key={i} x1={x} y1={80} x2={x < 150 ? 95 : 205} y2={90} 
                      stroke="#00ffc8" strokeWidth={1} opacity={0.4} strokeDasharray="3,3">
                      <animate attributeName="stroke-dashoffset" from="6" to="0" dur="0.5s" repeatCount="indefinite" />
                    </line>
                  ))}
                </>
              ) : (
                <>
                  <line x1={95} y1="130" x2={95} y2="140" stroke="#00f0ff" strokeWidth={1.5} opacity={0.6} strokeDasharray="4,4">
                    <animate attributeName="stroke-dashoffset" from="8" to="0" dur="0.3s" repeatCount="indefinite" />
                  </line>
                  <line x1={205} y1="130" x2={205} y2="140" stroke="#00f0ff" strokeWidth={1.5} opacity={0.6} strokeDasharray="4,4">
                    <animate attributeName="stroke-dashoffset" from="8" to="0" dur="0.3s" repeatCount="indefinite" />
                  </line>
                </>
              )}
            </svg>

            {/* Contention meter */}
            <div className="mt-4">
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-mono text-sov-400">Cache Contention</span>
                <span className={`text-xs font-mono ${
                  contention > 70 ? 'text-red-glow' : contention > 40 ? 'text-amber-glow' : 'text-teal-glow'
                }`}>{contention.toFixed(0)}%</span>
              </div>
              <div className="h-2 bg-sov-700 rounded-full overflow-hidden">
                <div 
                  className={`h-full rounded-full transition-all duration-500 ${
                    contention > 70 ? 'bg-red-glow' : contention > 40 ? 'bg-amber-glow' : 'bg-teal-glow'
                  }`}
                  style={{ width: `${contention}%` }}
                />
              </div>
            </div>
          </div>

          {/* Latency chart */}
          <div className="glass-panel rounded-xl p-6 glow-border">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-mono text-white">Latency Telemetry</h3>
              <div className="text-2xl font-mono font-bold text-cyan-glow">
                {getModeLatency().toFixed(2)}ns
              </div>
            </div>

            <svg viewBox={`0 0 ${chartWidth} ${chartHeight}`} className="w-full">
              {/* Grid */}
              {[0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6].map(v => {
                const y = padding + (1 - v / 0.6) * (chartHeight - padding * 2);
                return (
                  <g key={v}>
                    <line x1={padding} y1={y} x2={chartWidth - padding} y2={y} stroke="#1f2e44" strokeWidth={0.5} />
                    <text x={padding - 4} y={y + 3} textAnchor="end" fill="#2a3d5a" fontSize={8} fontFamily="monospace">
                      {v.toFixed(1)}
                    </text>
                  </g>
                );
              })}

              {/* Mode regions */}
              {modeHistory.map((entry, i) => {
                if (i === 0 || i >= modeHistory.length) return null;
                const x = padding + (i / 50) * (chartWidth - padding * 2);
                const color = entry.mode === 'L1-local' ? '#00ffc810' : '#ffb30010';
                return (
                  <rect key={i} x={x - 2} y={padding} width={4} height={chartHeight - padding * 2} fill={color} />
                );
              })}

              {/* 0.5ns target line */}
              <line 
                x1={padding} y1={padding + (1 - 0.5/0.6) * (chartHeight - padding * 2)} 
                x2={chartWidth - padding} y2={padding + (1 - 0.5/0.6) * (chartHeight - padding * 2)} 
                stroke="#ff306040" strokeWidth={1} strokeDasharray="4,4" 
              />
              <text x={chartWidth - padding + 2} y={padding + (1 - 0.5/0.6) * (chartHeight - padding * 2) + 3} 
                fill="#ff306060" fontSize={7} fontFamily="monospace">0.5ns</text>

              {/* Data line */}
              {chartPoints && (
                <>
                  <polyline points={chartPoints} fill="none" stroke="#00f0ff" strokeWidth={1.5} />
                  {/* Fill under line */}
                  <polygon 
                    points={`${padding},${chartHeight - padding} ${chartPoints} ${chartWidth - padding},${chartHeight - padding}`} 
                    fill="#00f0ff10" 
                  />
                </>
              )}

              {/* Current point */}
              {modeHistory.length > 0 && (() => {
                const last = modeHistory[modeHistory.length - 1];
                const x = padding + ((modeHistory.length - 1) / 50) * (chartWidth - padding * 2);
                const y = padding + (1 - (last.latency - 0.25) / 0.35) * (chartHeight - padding * 2);
                return (
                  <circle cx={x} cy={y} r={4} fill="#00f0ff">
                    <animate attributeName="r" from="3" to="6" dur="1s" repeatCount="indefinite" />
                    <animate attributeName="opacity" from="1" to="0.3" dur="1s" repeatCount="indefinite" />
                  </circle>
                );
              })()}
            </svg>

            {/* Legend */}
            <div className="flex items-center justify-center gap-4 mt-3 text-[10px] font-mono">
              <div className="flex items-center gap-1">
                <div className="w-3 h-1 bg-teal-glow/60 rounded" />
                <span className="text-sov-400">L1-local (&lt;50% contention)</span>
              </div>
              <div className="flex items-center gap-1">
                <div className="w-3 h-1 bg-amber-glow/60 rounded" />
                <span className="text-sov-400">L2-striped (&gt;50% contention)</span>
              </div>
            </div>
          </div>
        </div>

        {/* Mode details */}
        <div className="grid md:grid-cols-2 gap-4">
          <div className={`glass-panel rounded-lg p-5 border transition-all ${
            currentMode === 'L1-local' ? 'border-teal-glow/30 glow-border' : 'border-sov-600'
          }`}>
            <div className="flex items-center gap-2 mb-3">
              <div className={`w-2 h-2 rounded-full ${currentMode === 'L1-local' ? 'bg-teal-glow animate-pulse' : 'bg-sov-600'}`} />
              <h4 className="text-sm font-bold text-white font-mono">L1-Local Mode</h4>
            </div>
            <p className="text-xs text-sov-400 mb-3">
              Direct L1 cache access with zero inter-core communication. Optimal for low-contention workloads 
              with spatially localized access patterns.
            </p>
            <div className="grid grid-cols-3 gap-2 text-center">
              <div className="bg-sov-800/50 rounded p-2">
                <div className="text-teal-glow font-mono font-bold">~0.32ns</div>
                <div className="text-[9px] text-sov-400 font-mono">Latency</div>
              </div>
              <div className="bg-sov-800/50 rounded p-2">
                <div className="text-white font-mono font-bold">1×</div>
                <div className="text-[9px] text-sov-400 font-mono">Stripes</div>
              </div>
              <div className="bg-sov-800/50 rounded p-2">
                <div className="text-cyan-glow font-mono font-bold">0</div>
                <div className="text-[9px] text-sov-400 font-mono">Fences</div>
              </div>
            </div>
          </div>

          <div className={`glass-panel rounded-lg p-5 border transition-all ${
            currentMode === 'L2-striped' ? 'border-amber-glow/30 glow-border' : 'border-sov-600'
          }`}>
            <div className="flex items-center gap-2 mb-3">
              <div className={`w-2 h-2 rounded-full ${currentMode === 'L2-striped' ? 'bg-amber-glow animate-pulse' : 'bg-sov-600'}`} />
              <h4 className="text-sm font-bold text-white font-mono">L2-Striped Mode</h4>
            </div>
            <p className="text-xs text-sov-400 mb-3">
              Data striped across L2 banks with automatic rebalancing. Activates under high contention to 
              maintain sub-0.5ns latency through parallel distribution.
            </p>
            <div className="grid grid-cols-3 gap-2 text-center">
              <div className="bg-sov-800/50 rounded p-2">
                <div className="text-amber-glow font-mono font-bold">~0.42ns</div>
                <div className="text-[9px] text-sov-400 font-mono">Latency</div>
              </div>
              <div className="bg-sov-800/50 rounded p-2">
                <div className="text-white font-mono font-bold">N×</div>
                <div className="text-[9px] text-sov-400 font-mono">Stripes</div>
              </div>
              <div className="bg-sov-800/50 rounded p-2">
                <div className="text-cyan-glow font-mono font-bold">0</div>
                <div className="text-[9px] text-sov-400 font-mono">Fences</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
