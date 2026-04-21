import { motion } from 'framer-motion';
import { GitBranch, ArrowRightLeft, ArrowRight, AlertTriangle } from 'lucide-react';
import type { StripingMode } from '../types';

interface AdaptiveStripingProps {
  stripingMode: StripingMode;
}

export function AdaptiveStriping({ stripingMode }: AdaptiveStripingProps) {
  const modes: StripingMode[] = [
    { mode: 'L1_LOCAL', latency: 0.42, throughput: 99.2, contention: 1.5 },
    { mode: 'L2_STRIPED', latency: 0.48, throughput: 97.5, contention: 3.2 },
    { mode: 'L3_DISTRIBUTED', latency: 0.45, throughput: 98.2, contention: 2.8 },
    { mode: 'NUMA_OPTIMIZED', latency: 0.47, throughput: 96.8, contention: 2.0 },
  ];

  return (
    <div className="p-6 md:p-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-lg bg-purple-500/10 border border-purple-500/20">
          <GitBranch className="w-5 h-5 text-purple-400" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-white">Adaptive Adaptive Striping</h3>
          <p className="text-xs text-gray-500 font-mono">Friction-Less scaling based on cache contention</p>
        </div>
      </div>

      {/* Mode Decision Matrix */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-8">
        {modes.map((m) => {
          const isActive = m.mode === stripingMode.mode;
          return (
            <motion.div
              key={m.mode}
              animate={{
                scale: isActive ? 1.02 : 1,
                borderColor: isActive ? 'rgba(16,185,129,0.3)' : 'rgba(255,255,255,0.04)',
                backgroundColor: isActive ? 'rgba(16,185,129,0.05)' : 'rgba(255,255,255,0.01)',
              }}
              className={`p-4 rounded-xl border transition-all cursor-default ${
                isActive ? 'border-emerald-500/30 bg-emerald-500/5' : 'border-white/[0.04] bg-white/[0.01]'
              }`}
            >
              <div className="flex items-center justify-between mb-3">
                <span className={`font-mono text-sm font-bold ${isActive ? 'text-emerald-400' : 'text-gray-500'}`}>
                  {m.mode}
                </span>
                {isActive && (
                  <motion.div
                    initial={{ scale: 0 }}
                    animate={{ scale: 1 }}
                    className="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-[10px] font-mono"
                  >
                    ACTIVE
                  </motion.div>
                )}
              </div>

              <div className="grid grid-cols-3 gap-2">
                <div>
                  <div className="text-[10px] text-gray-600 font-mono mb-1">LATENCY</div>
                  <div className={`text-sm font-bold font-mono ${isActive ? 'text-white' : 'text-gray-400'}`}>
                    {m.latency.toFixed(2)}ns
                  </div>
                </div>
                <div>
                  <div className="text-[10px] text-gray-600 font-mono mb-1">THROUGHPUT</div>
                  <div className={`text-sm font-bold font-mono ${isActive ? 'text-white' : 'text-gray-400'}`}>
                    {m.throughput}%
                  </div>
                </div>
                <div>
                  <div className="text-[10px] text-gray-600 font-mono mb-1">CONTENTION</div>
                  <div className={`text-sm font-bold font-mono ${isActive ? 'text-white' : 'text-gray-400'}`}>
                    {m.contention.toFixed(1)}
                  </div>
                </div>
              </div>
            </motion.div>
          );
        })}
      </div>

      {/* Transition Logic */}
      <div className="p-4 rounded-xl bg-white/[0.02] border border-white/[0.04]">
        <div className="flex items-center gap-2 mb-3">
          <ArrowRightLeft className="w-4 h-4 text-gray-500" />
          <span className="text-sm font-mono text-gray-400">Transition Logic</span>
        </div>
        <div className="space-y-2">
          <div className="flex items-center gap-3 text-xs text-gray-500 font-mono">
            <span className="text-emerald-400">IF</span>
            <span>contention &lt; 2.5</span>
            <ArrowRight className="w-3 h-3 text-gray-600" />
            <span className="text-emerald-400">L1_LOCAL</span>
            <span className="text-gray-600">(0.42ns)</span>
          </div>
          <div className="flex items-center gap-3 text-xs text-gray-500 font-mono">
            <span className="text-amber-400">IF</span>
            <span>contention &gt; 3.0</span>
            <ArrowRight className="w-3 h-3 text-gray-600" />
            <span className="text-cyan-400">L2_STRIPED</span>
            <span className="text-gray-600">(0.48ns)</span>
          </div>
          <div className="flex items-center gap-3 text-xs text-gray-500 font-mono">
            <span className="text-amber-400">IF</span>
            <span>contention &gt; 2.5</span>
            <ArrowRight className="w-3 h-3 text-gray-600" />
            <span className="text-amber-400">L3_DISTRIBUTED</span>
            <span className="text-gray-600">(0.45ns)</span>
          </div>
        </div>
      </div>

      {/* Safety note */}
      <div className="mt-4 flex items-start gap-2 p-3 rounded-lg bg-amber-500/5 border border-amber-500/10">
        <AlertTriangle className="w-4 h-4 text-amber-400 shrink-0 mt-0.5" />
        <p className="text-xs text-amber-400/80 font-mono leading-relaxed">
          Mode transitions occur atomically via sequence-differencing. No memory barriers required.
          Zero-latency switching guaranteed by hardware-TSO invariants.
        </p>
      </div>
    </div>
  );
}
