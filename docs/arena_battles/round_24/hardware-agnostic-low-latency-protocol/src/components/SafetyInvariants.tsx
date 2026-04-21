import { motion } from 'framer-motion';
import { Shield, ShieldCheck, ShieldAlert, Clock } from 'lucide-react';
import type { SafetyInvariant } from '../types';

interface SafetyInvariantsProps {
  invariants: SafetyInvariant[];
}

export function SafetyInvariants({ invariants }: SafetyInvariantsProps) {
  return (
    <div className="p-6 md:p-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-lg bg-emerald-500/10 border border-emerald-500/20">
          <Shield className="w-5 h-5 text-emerald-400" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-white">Safety Invariants — Zero-Friction</h3>
          <p className="text-xs text-gray-500 font-mono">Non-latency-summing safety checks</p>
        </div>
      </div>

      <div className="space-y-3">
        {invariants.map((inv, i) => (
          <motion.div
            key={inv.id}
            initial={{ opacity: 0, x: -10 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: i * 0.1 }}
            className="flex items-start gap-4 p-4 rounded-xl bg-white/[0.02] border border-white/[0.04]"
          >
            <div className={`p-1.5 rounded-lg mt-0.5 ${
              inv.status === 'PASS'
                ? 'bg-emerald-500/15'
                : inv.status === 'WARN'
                ? 'bg-amber-500/15'
                : 'bg-cyan-500/15'
            }`}>
              {inv.status === 'PASS' ? (
                <ShieldCheck className="w-4 h-4 text-emerald-400" />
              ) : inv.status === 'WARN' ? (
                <ShieldAlert className="w-4 h-4 text-amber-400" />
              ) : (
                <Clock className="w-4 h-4 text-cyan-400" />
              )}
            </div>

            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <span className="text-xs font-mono text-gray-500 bg-white/5 px-2 py-0.5 rounded">
                  {inv.id}
                </span>
                <span className={`text-sm font-semibold ${
                  inv.status === 'PASS' ? 'text-emerald-400' : inv.status === 'WARN' ? 'text-amber-400' : 'text-cyan-400'
                }`}>
                  {inv.name}
                </span>
              </div>
              <p className="text-xs text-gray-500">{inv.description}</p>
            </div>

            <div className="text-right shrink-0">
              <div className="text-xs font-mono text-emerald-400">+{inv.latency.toFixed(2)}ns</div>
              <div className="text-[10px] text-gray-600 font-mono">overhead</div>
            </div>
          </motion.div>
        ))}
      </div>

      {/* Key property */}
      <div className="mt-6 p-4 rounded-xl bg-emerald-500/5 border border-emerald-500/20">
        <div className="flex items-center gap-2 mb-2">
          <ShieldCheck className="w-4 h-4 text-emerald-400" />
          <span className="text-sm font-semibold text-emerald-400 font-mono">ZERO LATENCY SUMMATION</span>
        </div>
        <p className="text-xs text-gray-400 leading-relaxed">
          All safety invariants operate at <span className="text-emerald-400">0.00ns</span> added latency. 
          They are implemented via pure hardware sequence-differencing — no software barriers, 
          no interlocked operations, no locks. The fence-less model is proven safe through 
          hardware-TSO properties and bitwise shadow validation.
        </p>
      </div>
    </div>
  );
}
