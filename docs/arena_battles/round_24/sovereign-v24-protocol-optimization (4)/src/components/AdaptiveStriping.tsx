import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Zap, ArrowUpRight, ArrowDownRight, Activity } from 'lucide-react';

interface CacheContention {
  timestamp: number;
  l1Pressure: number;
  l2Pressure: number;
  mode: 'l1-local' | 'l2-striped' | 'hybrid';
  latency: number;
}

function generateContentionHistory(length: number): CacheContention[] {
  const history: CacheContention[] = [];
  for (let i = 0; i < length; i++) {
    const l1Pressure = 0.3 + Math.random() * 0.6;
    const l2Pressure = 0.2 + Math.random() * 0.5;
    let mode: CacheContention['mode'];
    let latency: number;

    if (l1Pressure < 0.5) {
      mode = 'l1-local';
      latency = 0.35 + Math.random() * 0.08;
    } else if (l2Pressure > 0.55) {
      mode = 'l2-striped';
      latency = 0.44 + Math.random() * 0.06;
    } else {
      mode = 'hybrid';
      latency = 0.39 + Math.random() * 0.08;
    }

    history.push({ timestamp: Date.now() - (length - i) * 200, l1Pressure, l2Pressure, mode, latency });
  }
  return history;
}

function PressureGauge({ value, label, color }: { value: number; label: string; color: string }) {
  const angle = -90 + value * 180;
  return (
    <div className="text-center">
      <svg viewBox="0 0 100 60" className="w-full max-w-[120px] mx-auto">
        <path d="M 10 50 A 40 40 0 0 1 90 50" fill="none" stroke="rgba(255,255,255,0.05)" strokeWidth="6" strokeLinecap="round" />
        <motion.path
          d="M 10 50 A 40 40 0 0 1 90 50"
          fill="none"
          stroke={color}
          strokeWidth="6"
          strokeLinecap="round"
          initial={{ pathLength: 0 }}
          animate={{ pathLength: value }}
          transition={{ duration: 0.5 }}
        />
        <line x1="50" y1="50" x2="50" y2="15"
          stroke={color} strokeWidth="2" strokeLinecap="round"
          transform={`rotate(${angle}, 50, 50)`}
        />
      </svg>
      <div className="text-xs font-mono text-sov-text-dim mt-1">{label}</div>
      <div className="text-sm font-bold font-mono" style={{ color }}>{(value * 100).toFixed(0)}%</div>
    </div>
  );
}

function SparkLine({ data, width = 200, height = 40, color = '#00e5ff' }: { data: number[]; width?: number; height?: number; color?: string }) {
  if (data.length < 2) return null;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  const points = data.map((v, i) => {
    const x = (i / (data.length - 1)) * width;
    const y = height - ((v - min) / range) * height;
    return `${x},${y}`;
  }).join(' ');

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full">
      <polyline points={points} fill="none" stroke={color} strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function AdaptiveStriping() {
  const [history, setHistory] = useState<CacheContention[]>(generateContentionHistory(50));
  const [isRunning, setIsRunning] = useState(true);

  useEffect(() => {
    if (!isRunning) return;
    const interval = setInterval(() => {
      setHistory((prev) => {
        const last = prev[prev.length - 1];
        const l1Pressure = Math.min(1, Math.max(0.1, last.l1Pressure + (Math.random() - 0.5) * 0.15));
        const l2Pressure = Math.min(1, Math.max(0.1, last.l2Pressure + (Math.random() - 0.5) * 0.12));
        let mode: CacheContention['mode'];
        let latency: number;

        if (l1Pressure < 0.5) {
          mode = 'l1-local';
          latency = 0.35 + Math.random() * 0.08;
        } else if (l2Pressure > 0.55) {
          mode = 'l2-striped';
          latency = 0.44 + Math.random() * 0.06;
        } else {
          mode = 'hybrid';
          latency = 0.39 + Math.random() * 0.08;
        }

        const newHistory = [...prev.slice(-49), { timestamp: Date.now(), l1Pressure, l2Pressure, mode, latency }];
        return newHistory;
      });
    }, 200);
    return () => clearInterval(interval);
  }, [isRunning]);

  const current = history[history.length - 1];
  const latencies = history.map(h => h.latency);
  const l1Pressures = history.map(h => h.l1Pressure);
  const l2Pressures = history.map(h => h.l2Pressure);

  const modeColors: Record<string, string> = {
    'l1-local': '#00ff87',
    'l2-striped': '#00e5ff',
    'hybrid': '#b388ff',
  };

  return (
    <section id="striping" className="py-20 px-4 max-w-6xl mx-auto">
      <div className="text-center mb-12">
        <div className="inline-flex items-center gap-2 text-sov-purple font-mono text-sm mb-4">
          <Activity className="w-4 h-4" />
          <span>MANDATE #3</span>
        </div>
        <h2 className="text-3xl sm:text-4xl font-bold mb-3">
          Adaptive Friction-Less Striping
        </h2>
        <p className="text-sov-text-dim max-w-2xl mx-auto">
          Core adaptively shifts between L1-local and L2-striped modes based on real-time
          cache contention diagnostics — zero overhead, zero friction.
        </p>
      </div>

      {/* Mode indicator */}
      <div className="flex justify-center mb-8">
        <motion.button
          onClick={() => setIsRunning(!isRunning)}
          className={`px-6 py-2.5 rounded-lg font-mono text-sm transition-colors flex items-center gap-2 ${
            isRunning
              ? 'bg-sov-green/10 border border-sov-green/30 text-sov-green'
              : 'bg-sov-red/10 border border-sov-red/30 text-sov-red'
          }`}
          whileTap={{ scale: 0.95 }}
        >
          <Zap className="w-4 h-4" />
          {isRunning ? 'Simulation Running' : 'Simulation Paused'}
        </motion.button>
      </div>

      <div className="grid md:grid-cols-2 gap-6">
        {/* Current mode */}
        <AnimatePresence mode="wait">
          <motion.div
            key={current.mode}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            className="glass-panel rounded-xl p-6"
          >
            <div className="flex items-center justify-between mb-4">
              <span className="text-xs font-mono text-sov-text-dim">ACTIVE STRIPING MODE</span>
              {current.mode === 'l1-local' ? (
                <ArrowDownRight className="w-4 h-4 text-sov-green" />
              ) : current.mode === 'l2-striped' ? (
                <ArrowUpRight className="w-4 h-4 text-sov-cyan" />
              ) : (
                <Activity className="w-4 h-4 text-sov-purple" />
              )}
            </div>
            <div className="text-3xl font-black font-mono mb-2" style={{ color: modeColors[current.mode] }}>
              {current.mode.toUpperCase()}
            </div>
            <div className="text-sm text-sov-text-dim mb-4">
              {current.mode === 'l1-local' && 'L1-local: Data stays in private cache — lowest latency'}
              {current.mode === 'l2-striped' && 'L2-striped: Data spread across shared cache — high throughput'}
              {current.mode === 'hybrid' && 'Hybrid: Balanced approach — moderate pressure on both tiers'}
            </div>

            <div className="grid grid-cols-2 gap-4">
              <PressureGauge value={current.l1Pressure} label="L1 Pressure" color="#00ff87" />
              <PressureGauge value={current.l2Pressure} label="L2 Pressure" color="#00e5ff" />
            </div>
          </motion.div>
        </AnimatePresence>

        {/* Latency sparkline */}
        <div className="glass-panel rounded-xl p-6">
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs font-mono text-sov-text-dim">LATENCY HISTORY (50 samples)</span>
            <span className="text-sm font-mono font-bold text-sov-green">{current.latency.toFixed(3)}ns</span>
          </div>
          <SparkLine data={latencies} height={50} color={modeColors[current.mode]} />
          <div className="mt-4 grid grid-cols-2 gap-4">
            <div>
              <div className="text-xs text-sov-text-dim font-mono">L1 Pressure</div>
              <SparkLine data={l1Pressures} height={30} color="#00ff87" />
            </div>
            <div>
              <div className="text-xs text-sov-text-dim font-mono">L2 Pressure</div>
              <SparkLine data={l2Pressures} height={30} color="#00e5ff" />
            </div>
          </div>
        </div>
      </div>

      {/* Mode transitions log */}
      <div className="glass-panel rounded-xl p-4 mt-6 max-h-40 overflow-y-auto">
        <div className="text-xs font-mono text-sov-text-dim mb-2">TRANSITION LOG</div>
        <div className="space-y-1">
          {history.slice(-8).reverse().map((h, i) => (
            <div key={i} className="flex items-center gap-3 text-xs font-mono">
              <span className="text-sov-text-dim">{new Date(h.timestamp).toLocaleTimeString()}</span>
              <span className="w-2 h-2 rounded-full" style={{ backgroundColor: modeColors[h.mode] }} />
              <span style={{ color: modeColors[h.mode] }}>{h.mode.toUpperCase()}</span>
              <span className="text-sov-text-dim">L1:{(h.l1Pressure * 100).toFixed(0)}% L2:{(h.l2Pressure * 100).toFixed(0)}%</span>
              <span className="text-sov-green">{h.latency.toFixed(3)}ns</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
