import { motion } from 'framer-motion';
import { Shield, CheckCircle2, XCircle, AlertCircle, Lock } from 'lucide-react';
import type { SafetyInvariant } from '../data/protocol';

interface SafetyInvariantsProps {
  invariants: SafetyInvariant[];
}

export default function SafetyInvariants({ invariants }: SafetyInvariantsProps) {
  const passCount = invariants.filter(i => i.status === 'pass').length;
  const allPass = passCount === invariants.length && passCount > 0;

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      className="bg-surface-800/50 rounded-2xl border border-sov-800/50 overflow-hidden"
    >
      {/* Header */}
      <div className="px-5 py-4 border-b border-sov-800/50 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${allPass ? 'bg-accent-green/20' : 'bg-accent-amber/20'}`}>
            <Shield className={`w-4 h-4 ${allPass ? 'text-accent-green' : 'text-accent-amber'}`} />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-white/90">Safety Invariants</h3>
            <p className="text-xs text-white/40 font-mono">Non-Latency-Summing Validation</p>
          </div>
        </div>
        <div className={`flex items-center gap-2 px-3 py-1.5 rounded-lg border ${allPass ? 'bg-accent-green/10 border-accent-green/30' : 'bg-accent-amber/10 border-accent-amber/30'}`}>
          {allPass ? (
            <CheckCircle2 className="w-4 h-4 text-accent-green" />
          ) : (
            <AlertCircle className="w-4 h-4 text-accent-amber" />
          )}
          <span className={`text-xs font-mono font-semibold ${allPass ? 'text-accent-green' : 'text-accent-amber'}`}>
            {passCount}/{invariants.length} PASS
          </span>
        </div>
      </div>

      <div className="p-5 space-y-3">
        {invariants.map((inv, i) => (
          <motion.div
            key={inv.name}
            initial={{ opacity: 0, x: -10 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: i * 0.05 }}
            className={`flex items-center gap-4 p-3 rounded-xl border transition-colors ${
              inv.status === 'pass'
                ? 'bg-accent-green/5 border-accent-green/20'
                : inv.status === 'fail'
                ? 'bg-accent-red/5 border-accent-red/20'
                : 'bg-surface-700/30 border-sov-700/20'
            }`}
          >
            {/* Status Icon */}
            <div className="flex-shrink-0">
              {inv.status === 'pass' ? (
                <CheckCircle2 className="w-5 h-5 text-accent-green" />
              ) : inv.status === 'fail' ? (
                <XCircle className="w-5 h-5 text-accent-red" />
              ) : (
                <Lock className="w-5 h-5 text-white/20" />
              )}
            </div>

            {/* Content */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-semibold text-white/80">{inv.name}</span>
                <span className={`text-xs font-mono px-2 py-0.5 rounded ${
                  inv.status === 'pass' ? 'bg-accent-green/20 text-accent-green' :
                  inv.status === 'fail' ? 'bg-accent-red/20 text-accent-red' :
                  'bg-white/5 text-white/30'
                }`}>
                  {inv.value}
                </span>
              </div>
              <p className="text-xs text-white/40">{inv.description}</p>
            </div>

            {/* Metric */}
            <div className="hidden sm:flex flex-col items-end text-xs font-mono">
              <span className="text-white/30">{inv.metric}</span>
              <span className="text-white/20">{inv.threshold}</span>
            </div>
          </motion.div>
        ))}

        {/* ADR-015 Notice */}
        <div className="mt-4 p-4 rounded-xl bg-gradient-to-r from-accent-cyan/5 to-sov-600/5 border border-accent-cyan/20">
          <div className="flex items-center gap-2 mb-2">
            <Lock className="w-4 h-4 text-accent-cyan" />
            <span className="text-xs font-mono font-bold text-accent-cyan">ADR-015 FENCE-Less DISCIPLINE</span>
          </div>
          <p className="text-xs text-white/40 leading-relaxed">
            All invariants validated without <code className="text-accent-red/80 bg-accent-red/10 px-1 rounded">MemoryBarrier()</code>, 
            <code className="text-accent-red/80 bg-accent-red/10 px-1 rounded"> Interlocked.*</code>, 
            <code className="text-accent-red/80 bg-accent-red/10 px-1 rounded"> lock()</code>, or 
            <code className="text-accent-red/80 bg-accent-red/10 px-1 rounded"> volatile</code> barriers.
            Hardware TSO properties guarantee ordering. FenceCount remains at <span className="text-accent-green font-bold">0</span>.
          </p>
        </div>
      </div>
    </motion.div>
  );
}
