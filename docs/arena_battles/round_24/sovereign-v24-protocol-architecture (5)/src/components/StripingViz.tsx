import React, { useState, useEffect, useCallback } from 'react';
import { Settings, RefreshCw, Gauge } from 'lucide-react';

interface StripingConfig {
  mode: 'L1-local' | 'L2-striped' | 'Adaptive';
  latency: number;
  contention: number;
  stripeWidth: number;
}

const StripingViz: React.FC = () => {
  const [activeMode, setActiveMode] = useState<StripingConfig>({
    mode: 'Adaptive',
    latency: 0.347,
    contention: 12,
    stripeWidth: 64,
  });
  const [animFrame, setAnimFrame] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setAnimFrame((f) => (f + 1) % 60);
    }, 50);
    return () => clearInterval(interval);
  }, []);

  const modes: StripingConfig[] = [
    { mode: 'L1-local', latency: 0.12, contention: 3, stripeWidth: 32 },
    { mode: 'L2-striped', latency: 0.28, contention: 8, stripeWidth: 64 },
    { mode: 'Adaptive', latency: 0.347, contention: 12, stripeWidth: 128 },
  ];

  const handleModeSelect = useCallback((m: StripingConfig) => {
    setActiveMode(m);
  }, []);

  // Generate animated data flow visualization
  const renderDataFlow = () => {
    const rows = 8;
    const cols = activeMode.mode === 'L1-local' ? 4 : activeMode.mode === 'L2-striped' ? 8 : 16;
    const stripeSize = activeMode.stripeWidth;

    return (
      <div className="grid gap-0.5" style={{ gridTemplateColumns: `repeat(${cols}, minmax(0, 1fr))` }}>
        {Array.from({ length: rows * cols }).map((_, i) => {
          const row = Math.floor(i / cols);
          const col = i % cols;
          const stripeIdx = Math.floor(col / (stripeSize / 8));
          const isActive = (animFrame + row + col) % 3 === 0;
          const isHot = (animFrame * 7 + i) % 13 === 0;

          let bgClass = 'bg-slate-800/50';
          if (isHot) bgClass = 'bg-cyan-400/60';
          else if (isActive) bgClass = 'bg-cyan-500/30';
          else if (stripeIdx % 2 === 0) bgClass = 'bg-slate-700/30';

          return (
            <div
              key={i}
              className={`w-full aspect-square rounded-[1px] transition-colors duration-100 ${bgClass}`}
            />
          );
        })}
      </div>
    );
  };

  // Contention heatmap
  const renderHeatmap = () => {
    const cells = Array.from({ length: 20 }, (_, i) => {
      const val = Math.sin((animFrame + i * 17) * 0.1) * 0.5 + 0.5;
      let color = 'bg-slate-800';
      if (val > 0.8) color = 'bg-red-500/40';
      else if (val > 0.6) color = 'bg-amber-500/30';
      else if (val > 0.4) color = 'bg-yellow-500/20';
      else if (val > 0.2) color = 'bg-emerald-500/15';
      return color;
    });

    return (
      <div className="grid grid-cols-10 gap-0.5">
        {cells.map((color, i) => (
          <div key={i} className={`aspect-square rounded-sm ${color} transition-colors duration-200`} />
        ))}
      </div>
    );
  };

  return (
    <section className="py-20 px-4" id="striping">
      <div className="max-w-6xl mx-auto">
        {/* Section header */}
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/20 mb-4">
            <Settings className="w-3 h-3 text-cyan-400" />
            <span className="text-xs font-mono text-cyan-300 tracking-wider">ADAPTIVE STRIPING</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold mb-3">
            <span className="text-shimmer">Friction-Less Scaling</span>
          </h2>
          <p className="text-slate-400 max-w-xl mx-auto">
            Real-time cache contention diagnostics drive adaptive shifts between L1-local and L2-striped modes.
          </p>
        </div>

        {/* Mode selector */}
        <div className="flex flex-wrap justify-center gap-3 mb-10">
          {modes.map((m) => (
            <button
              key={m.mode}
              onClick={() => handleModeSelect(m)}
              className={`px-5 py-2.5 rounded-lg border font-mono text-sm transition-all ${
                activeMode.mode === m.mode
                  ? 'border-cyan-500/50 bg-cyan-500/10 text-cyan-300 shadow-lg shadow-cyan-500/10'
                  : 'border-slate-700 bg-slate-800/50 text-slate-400 hover:border-slate-600'
              }`}
            >
              {m.mode}
            </button>
          ))}
          <button
            onClick={() => setActiveMode(modes[2])}
            className="px-5 py-2.5 rounded-lg border border-emerald-500/30 bg-emerald-500/5 text-emerald-300 font-mono text-sm flex items-center gap-2 hover:bg-emerald-500/10 transition-all"
          >
            <RefreshCw className="w-3 h-3 animate-spin-slow" />
            Auto
          </button>
        </div>

        {/* Visualization panels */}
        <div className="grid md:grid-cols-3 gap-6">
          {/* Data flow */}
          <div className="glass-panel rounded-xl p-5">
            <div className="flex items-center gap-2 mb-4">
              <Gauge className="w-4 h-4 text-cyan-400" />
              <span className="text-sm font-semibold">Data Flow Map</span>
            </div>
            <div className="p-3 rounded-lg bg-slate-900/50 border border-slate-800">
              {renderDataFlow()}
            </div>
            <div className="mt-3 grid grid-cols-2 gap-2 text-[10px] font-mono">
              <div className="text-slate-500">Mode</div>
              <div className="text-cyan-300 text-right">{activeMode.mode}</div>
              <div className="text-slate-500">Stripe Width</div>
              <div className="text-cyan-300 text-right">{activeMode.stripeWidth}B</div>
              <div className="text-slate-500">Grid</div>
              <div className="text-cyan-300 text-right">
                8×{activeMode.mode === 'L1-local' ? '4' : activeMode.mode === 'L2-striped' ? '8' : '16'}
              </div>
            </div>
          </div>

          {/* Contention heatmap */}
          <div className="glass-panel rounded-xl p-5">
            <div className="flex items-center gap-2 mb-4">
              <Settings className="w-4 h-4 text-amber-400" />
              <span className="text-sm font-semibold">Contention Heatmap</span>
            </div>
            <div className="p-3 rounded-lg bg-slate-900/50 border border-slate-800">
              {renderHeatmap()}
            </div>
            <div className="mt-3 flex items-center justify-between text-[10px] font-mono">
              <span className="text-slate-500">Low</span>
              <div className="flex gap-0.5">
                <div className="w-8 h-2 rounded-sm bg-emerald-500/15" />
                <div className="w-8 h-2 rounded-sm bg-yellow-500/20" />
                <div className="w-8 h-2 rounded-sm bg-amber-500/30" />
                <div className="w-8 h-2 rounded-sm bg-red-500/40" />
              </div>
              <span className="text-slate-500">High</span>
            </div>
          </div>

          {/* Real-time metrics */}
          <div className="glass-panel rounded-xl p-5">
            <div className="flex items-center gap-2 mb-4">
              <RefreshCw className="w-4 h-4 text-emerald-400" />
              <span className="text-sm font-semibold">Live Metrics</span>
            </div>

            <div className="space-y-4">
              <div>
                <div className="flex justify-between text-[10px] font-mono mb-1">
                  <span className="text-slate-500">LATENCY</span>
                  <span className="text-cyan-300">{activeMode.latency}ns</span>
                </div>
                <div className="h-1.5 bg-slate-800 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-cyan-600 to-cyan-400 rounded-full transition-all duration-500"
                    style={{ width: `${(activeMode.latency / 0.5) * 100}%` }}
                  />
                </div>
              </div>

              <div>
                <div className="flex justify-between text-[10px] font-mono mb-1">
                  <span className="text-slate-500">CONTENTION</span>
                  <span className="text-amber-300">{activeMode.contention}%</span>
                </div>
                <div className="h-1.5 bg-slate-800 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-amber-600 to-amber-400 rounded-full transition-all duration-500"
                    style={{ width: `${activeMode.contention}%` }}
                  />
                </div>
              </div>

              <div>
                <div className="flex justify-between text-[10px] font-mono mb-1">
                  <span className="text-slate-500">STRIPE EFFICIENCY</span>
                  <span className="text-emerald-300">{Math.round((1 - activeMode.latency) * 100)}%</span>
                </div>
                <div className="h-1.5 bg-slate-800 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-emerald-600 to-emerald-400 rounded-full transition-all duration-500"
                    style={{ width: `${(1 - activeMode.latency) * 100}%` }}
                  />
                </div>
              </div>

              <div>
                <div className="flex justify-between text-[10px] font-mono mb-1">
                  <span className="text-slate-500">FENCE COUNT</span>
                  <span className="text-purple-300">0</span>
                </div>
                <div className="h-1.5 bg-slate-800 rounded-full overflow-hidden">
                  <div className="h-full bg-purple-500/30 rounded-full" style={{ width: '0%' }} />
                </div>
              </div>
            </div>

            <div className="mt-4 p-3 rounded-lg bg-emerald-500/5 border border-emerald-500/20">
              <div className="text-[10px] font-mono text-emerald-300 mb-1">STATUS</div>
              <div className="text-xs font-mono text-emerald-400">
                {activeMode.contention < 10 ? '✓ L1-Local optimal' : activeMode.contention < 20 ? '⚡ L2-Striping active' : '🔄 Adaptive balancing'}
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
};

export default StripingViz;
