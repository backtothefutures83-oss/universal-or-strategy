import { motion } from 'framer-motion';
import { Layers, ArrowRight } from 'lucide-react';

interface CacheStripeVisualizerProps {
  currentMode: string;
}

export function CacheStripeVisualizer({ currentMode }: CacheStripeVisualizerProps) {
  const stripeConfigs: Record<string, { label: string; color: string; stripes: number; description: string }> = {
    L1_LOCAL: {
      label: 'L1 Local Mode',
      color: 'emerald',
      stripes: 1,
      description: 'Single-core L1 cache residency — zero interconnect latency',
    },
    L2_STRIPED: {
      label: 'L2 Striped Mode',
      color: 'cyan',
      stripes: 4,
      description: 'Data striped across L2 slices — contention-aware distribution',
    },
    L3_DISTRIBUTED: {
      label: 'L3 Distributed Mode',
      color: 'amber',
      stripes: 8,
      description: 'Full L3 slice utilization — maximum throughput under pressure',
    },
  };

  const config = stripeConfigs[currentMode] || stripeConfigs.L1_LOCAL;

  return (
    <div className="p-6 md:p-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-lg bg-amber-500/10 border border-amber-500/20">
          <Layers className="w-5 h-5 text-amber-400" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-white">Cache Line Stripe Visualizer</h3>
          <p className="text-xs text-gray-500 font-mono">Auto-aligned to detected hardware stripe</p>
        </div>
      </div>

      {/* Current Mode Indicator */}
      <div className={`inline-flex items-center gap-2 px-4 py-2 rounded-full border mb-8
        ${currentMode === 'L1_LOCAL' ? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-400' : ''}
        ${currentMode === 'L2_STRIPED' ? 'bg-cyan-500/10 border-cyan-500/20 text-cyan-400' : ''}
        ${currentMode === 'L3_DISTRIBUTED' ? 'bg-amber-500/10 border-amber-500/20 text-amber-400' : ''}
      `}>
        <span className="w-2 h-2 rounded-full bg-current animate-pulse" />
        <span className="text-sm font-mono">{config.label}</span>
      </div>

      <p className="text-sm text-gray-500 mb-8">{config.description}</p>

      {/* Cache Line Visualization */}
      <div className="relative">
        <div className="flex items-center justify-between mb-3 px-2">
          <span className="text-xs text-gray-600 font-mono">Address →</span>
          <span className="text-xs text-gray-600 font-mono">→ 64B aligned</span>
        </div>

        {/* Stripe blocks */}
        <div className="flex gap-1 overflow-hidden">
          {Array.from({ length: 16 }).map((_, i) => {
            const stripeIndex = i % config.stripes;
            const colors = [
              'bg-emerald-500/30 border-emerald-500/40',
              'bg-emerald-500/20 border-emerald-500/30',
              'bg-emerald-500/10 border-emerald-500/20',
              'bg-emerald-500/5 border-emerald-500/10',
            ];
            const cyanColors = [
              'bg-cyan-500/30 border-cyan-500/40',
              'bg-cyan-500/20 border-cyan-500/30',
              'bg-cyan-500/10 border-cyan-500/20',
              'bg-cyan-500/5 border-cyan-500/10',
              'bg-cyan-500/30 border-cyan-500/40',
              'bg-cyan-500/20 border-cyan-500/30',
              'bg-cyan-500/10 border-cyan-500/20',
              'bg-cyan-500/5 border-cyan-500/10',
            ];
            const amberColors = [
              'bg-amber-500/30 border-amber-500/40',
              'bg-amber-500/20 border-amber-500/30',
              'bg-amber-500/10 border-amber-500/20',
              'bg-amber-500/5 border-amber-500/10',
              'bg-amber-500/30 border-amber-500/40',
              'bg-amber-500/20 border-amber-500/30',
              'bg-amber-500/10 border-amber-500/20',
              'bg-amber-500/5 border-amber-500/10',
            ];
            const colorSet =
              config.color === 'emerald'
                ? colors
                : config.color === 'cyan'
                ? cyanColors
                : amberColors;

            return (
              <motion.div
                key={i}
                initial={{ opacity: 0, scaleY: 0 }}
                animate={{ opacity: 1, scaleY: 1 }}
                transition={{ delay: i * 0.03, duration: 0.3 }}
                className={`flex-1 h-16 rounded border ${colorSet[stripeIndex]} flex items-center justify-center`}
              >
                <span className="text-[8px] font-mono text-white/40">
                  {stripeIndex + 1}
                </span>
              </motion.div>
            );
          })}
        </div>

        {/* Legend */}
        <div className="flex gap-4 mt-4 px-2">
          {Array.from({ length: config.stripes }).map((_, i) => (
            <div key={i} className="flex items-center gap-1.5">
              <div className={`w-3 h-3 rounded ${
                config.color === 'emerald'
                  ? 'bg-emerald-500/40'
                  : config.color === 'cyan'
                  ? 'bg-cyan-500/40'
                  : 'bg-amber-500/40'
              }`} />
              <span className="text-[10px] text-gray-500 font-mono">
                Stripe {i + 1}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* Flow arrows */}
      <div className="mt-6 flex items-center gap-2 text-gray-600">
        <ArrowRight className="w-4 h-4" />
        <span className="text-xs font-mono">
          Data flows through {config.stripes} stripe{config.stripes > 1 ? 's' : ''} with zero-copy integrity
        </span>
      </div>
    </div>
  );
}
