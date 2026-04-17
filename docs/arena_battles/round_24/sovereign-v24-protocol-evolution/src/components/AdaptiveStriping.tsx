import { motion } from 'framer-motion';
import { Layers, ArrowRightLeft, Zap, Activity } from 'lucide-react';
import type { AdaptiveState } from '../data/protocol';

interface AdaptiveStripingProps {
  state: AdaptiveState;
}

export default function AdaptiveStriping({ state }: AdaptiveStripingProps) {
  const modes = [
    { key: 'L1-local', label: 'L1-Local', desc: 'Ultra-low latency, single-core', color: 'accent-cyan', threshold: '< 30%' },
    { key: 'L2-striped', label: 'L2-Striped', desc: 'Cross-core striped', color: 'sov-400', threshold: '30-70%' },
    { key: 'L3-fallback', label: 'L3-Fallback', desc: 'NUMA-aware fallback', color: 'accent-purple', threshold: '> 70%' },
  ];

  const contentionPercent = Math.round((state.contentionScore / 1000) * 100);

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      className="bg-surface-800/50 rounded-2xl border border-sov-800/50 overflow-hidden"
    >
      {/* Header */}
      <div className="px-5 py-4 border-b border-sov-800/50 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-accent-purple/20 flex items-center justify-center">
            <Layers className="w-4 h-4 text-accent-purple" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-white/90">Adaptive Striping Engine</h3>
            <p className="text-xs text-white/40 font-mono">Friction-Less L1↔L2 Mode Switching</p>
          </div>
        </div>
      </div>

      <div className="p-5 space-y-5">
        {/* Contention Gauge */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs uppercase tracking-wider text-white/40 flex items-center gap-1.5">
              <Activity className="w-3 h-3" />
              Real-Time Contention
            </span>
            <span className="text-sm font-mono font-bold text-white/70">{contentionPercent}%</span>
          </div>
          <div className="h-3 bg-surface-900/50 rounded-full overflow-hidden border border-sov-700/20">
            <motion.div
              className="h-full rounded-full transition-all duration-300"
              style={{
                width: `${contentionPercent}%`,
                background: contentionPercent < 30
                  ? 'linear-gradient(90deg, #06f7e0, #10f5a0)'
                  : contentionPercent < 70
                  ? 'linear-gradient(90deg, #338dff, #59b0ff)'
                  : 'linear-gradient(90deg, #a78bfa, #c4b5fd)',
              }}
              initial={{ width: 0 }}
              animate={{ width: `${contentionPercent}%` }}
            />
          </div>
          <div className="flex justify-between mt-1 text-[10px] text-white/20 font-mono">
            <span>0%</span>
            <span className="text-accent-cyan/40">30%</span>
            <span className="text-sov-400/40">70%</span>
            <span>100%</span>
          </div>
        </div>

        {/* Mode Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {modes.map((mode) => {
            const isActive = state.mode === mode.key;
            return (
              <motion.div
                key={mode.key}
                className={`relative rounded-xl p-4 border transition-all ${
                  isActive
                    ? `bg-${mode.color}/10 border-${mode.color}/40 shadow-lg shadow-${mode.color}/5`
                    : 'bg-surface-700/20 border-sov-700/20'
                }`}
                animate={{
                  scale: isActive ? 1.02 : 1,
                }}
              >
                {isActive && (
                  <div className="absolute -top-2 left-4 px-2 py-0.5 rounded-full bg-accent-green/20 border border-accent-green/40">
                    <span className="text-[10px] font-mono text-accent-green font-bold">ACTIVE</span>
                  </div>
                )}
                <div className="flex items-center gap-2 mb-2">
                  <div className={`w-3 h-3 rounded-full ${isActive ? `bg-${mode.color}` : 'bg-white/10'}`} />
                  <span className={`text-sm font-bold ${isActive ? `text-${mode.color}` : 'text-white/40'}`}>
                    {mode.label}
                  </span>
                </div>
                <p className="text-xs text-white/30 mb-2">{mode.desc}</p>
                <div className="text-[10px] font-mono text-white/20">
                  Threshold: {mode.threshold}
                </div>
              </motion.div>
            );
          })}
        </div>

        {/* Striping Details */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <DetailBox label="Stripe Width" value={`${state.stripeWidth}B`} />
          <DetailBox label="Active Stripes" value={state.activeStripes.toString()} />
          <DetailBox label="Switch Threshold" value={state.switchThreshold.toString()} />
          <DetailBox label="Contention Score" value={state.contentionScore.toString()} />
        </div>

        {/* Mode Transition Diagram */}
        <div className="bg-surface-900/50 rounded-xl p-4 border border-sov-700/20">
          <div className="flex items-center gap-2 mb-3">
            <ArrowRightLeft className="w-3 h-3 text-white/30" />
            <span className="text-xs uppercase tracking-wider text-white/40">Hysteresis-Based Mode Selection</span>
          </div>
          <div className="flex items-center justify-center gap-2 sm:gap-4">
            <ModeNode label="L1-Local" active={state.mode === 'L1-local'} color="accent-cyan" />
            <Arrow active={contentionPercent >= 30 && contentionPercent < 70} />
            <ModeNode label="L2-Striped" active={state.mode === 'L2-striped'} color="sov-400" />
            <Arrow active={contentionPercent >= 70} />
            <ModeNode label="L3-Fallback" active={state.mode === 'L3-fallback'} color="accent-purple" />
          </div>
          <p className="text-[10px] text-white/30 text-center mt-2 font-mono">
            Hysteresis prevents mode thrashing during rapid contention changes
          </p>
        </div>
      </div>
    </motion.div>
  );
}

function DetailBox({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-surface-700/30 rounded-lg p-3 border border-sov-700/20">
      <span className="text-[10px] uppercase tracking-wider text-white/40 block mb-1">{label}</span>
      <span className="text-sm font-mono font-bold text-white/80">{value}</span>
    </div>
  );
}

function ModeNode({ label, active, color }: { label: string; active: boolean; color: string }) {
  return (
    <div className={`px-3 py-2 rounded-lg text-xs font-mono font-bold transition-all ${
      active ? `bg-${color}/20 text-${color} border border-${color}/40` : 'bg-surface-700/30 text-white/20 border border-sov-700/20'
    }`}>
      {label}
    </div>
  );
}

function Arrow({ active }: { active: boolean }) {
  return (
    <div className={`flex items-center transition-colors ${active ? 'text-accent-cyan' : 'text-white/10'}`}>
      <Zap className={`w-3 h-3 ${active ? 'animate-pulse' : ''}`} />
    </div>
  );
}
