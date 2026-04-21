import { motion } from 'framer-motion';
import { TrendingDown, Zap, Gauge, BarChart3, AlertTriangle } from 'lucide-react';
import type { BenchmarkResult } from '../data/protocol';

interface BenchmarkPanelProps {
  benchmarks: BenchmarkResult[];
  stats: { avgLatency: number; minLatency: number; maxLatency: number; p99Latency: number; totalOps: number; modeSwitches: number };
}

export default function BenchmarkPanel({ benchmarks, stats }: BenchmarkPanelProps) {
  const maxLatency = Math.max(stats.maxLatency * 1.2, 0.6);
  const chartData = benchmarks.slice(-100);

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      className="bg-surface-800/50 rounded-2xl border border-sov-800/50 overflow-hidden"
    >
      {/* Header */}
      <div className="px-5 py-4 border-b border-sov-800/50 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-accent-cyan/20 flex items-center justify-center">
            <Gauge className="w-4 h-4 text-accent-cyan" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-white/90">Real-Time Benchmark</h3>
            <p className="text-xs text-white/40 font-mono">Sub-0.5ns Target • Live Telemetry</p>
          </div>
        </div>
        <div className="flex items-center gap-3 text-xs font-mono">
          <span className="text-white/30">{benchmarks.length} samples</span>
        </div>
      </div>

      <div className="p-5 space-y-5">
        {/* Latency Chart */}
        <div className="bg-surface-900/50 rounded-xl p-4 border border-sov-700/20">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs uppercase tracking-wider text-white/40">Latency Distribution (ns)</span>
            <div className="flex items-center gap-1">
              <div className="w-2 h-2 rounded-full bg-accent-cyan" />
              <span className="text-[10px] text-white/40">L1-local</span>
              <div className="w-2 h-2 rounded-full bg-sov-400 ml-2" />
              <span className="text-[10px] text-white/40">L2-striped</span>
              <div className="w-2 h-2 rounded-full bg-accent-purple ml-2" />
              <span className="text-[10px] text-white/40">L3-fallback</span>
            </div>
          </div>
          
          <div className="relative h-48 flex items-end gap-[2px]">
            {/* Target line at 0.5ns */}
            <div
              className="absolute left-0 right-0 border-t border-dashed border-accent-red/40"
              style={{ bottom: `${(0.5 / maxLatency) * 100}%` }}
            >
              <span className="absolute right-0 -top-4 text-[10px] text-accent-red/60 font-mono">0.5ns TARGET</span>
            </div>
            
            {chartData.map((b, i) => {
              const height = (b.latencyNs / maxLatency) * 100;
              const color = b.mode === 'L1-local' ? 'bg-accent-cyan/70' : b.mode === 'L2-striped' ? 'bg-sov-400/70' : 'bg-accent-purple/70';
              return (
                <motion.div
                  key={i}
                  className={`flex-1 rounded-t ${color} min-w-[2px]`}
                  style={{ height: `${height}%` }}
                  initial={{ height: 0 }}
                  animate={{ height: `${height}%` }}
                  transition={{ duration: 0.2 }}
                />
              );
            })}
          </div>
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          <StatBox
            icon={TrendingDown}
            label="AVG"
            value={`${stats.avgLatency.toFixed(3)}ns`}
            color="text-accent-cyan"
          />
          <StatBox
            icon={Zap}
            label="MIN"
            value={`${stats.minLatency.toFixed(3)}ns`}
            color="text-accent-green"
          />
          <StatBox
            icon={AlertTriangle}
            label="MAX"
            value={`${stats.maxLatency.toFixed(3)}ns`}
            color="text-accent-amber"
          />
          <StatBox
            icon={BarChart3}
            label="P99"
            value={`${stats.p99Latency.toFixed(3)}ns`}
            color="text-accent-purple"
          />
          <StatBox
            icon={Gauge}
            label="Throughput"
            value={stats.avgLatency > 0 ? `${(1000 / stats.avgLatency).toFixed(0)}M/s` : '—'}
            color="text-white/70"
          />
          <StatBox
            icon={Zap}
            label="Mode Switches"
            value={stats.modeSwitches.toString()}
            color="text-white/70"
          />
        </div>
      </div>
    </motion.div>
  );
}

function StatBox({ icon: Icon, label, value, color }: { icon: React.ElementType; label: string; value: string; color: string }) {
  return (
    <div className="bg-surface-700/30 rounded-xl p-3 border border-sov-700/20">
      <div className="flex items-center gap-1.5 mb-1">
        <Icon className="w-3 h-3 text-white/30" />
        <span className="text-[10px] uppercase tracking-wider text-white/40">{label}</span>
      </div>
      <span className={`text-sm font-mono font-bold ${color}`}>{value}</span>
    </div>
  );
}
