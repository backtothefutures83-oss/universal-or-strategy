import { motion } from 'framer-motion';
import { Shield, Swords, Lock, Eye, Crosshair, Target } from 'lucide-react';

export function AdversarialChallenge() {
  const challenges = [
    {
      icon: Crosshair,
      title: 'High-Interrupt Context Switching',
      description: 'Protocol must survive OS-level preemption without data corruption.',
      mitigation: 'TSO guarantees store ordering persists across context switches.',
    },
    {
      icon: Target,
      title: 'Zero-Copy Data Integrity',
      description: 'No intermediate copies — data flows directly from producer to consumer.',
      mitigation: 'Marshal-allocated buffers with XOR shadow validation detect corruption.',
    },
    {
      icon: Lock,
      title: 'Multi-Socket Safety',
      description: 'Fence-less model must hold across NUMA node boundaries.',
      mitigation: 'NUMA distance-bounded verification ensures TSO invariants hold.',
    },
    {
      icon: Eye,
      title: 'Portable Hardware Fence-Less',
      description: 'Must work on x86, x64, and ARM64 without architecture-specific barriers.',
      mitigation: 'Runtime TSO detection + graceful fallback to striped mode.',
    },
  ];

  return (
    <div className="p-6 md:p-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-lg bg-red-500/10 border border-red-500/20">
          <Swords className="w-5 h-5 text-red-400" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-white">The Portability Gate — Adversarial Challenge</h3>
          <p className="text-xs text-gray-500 font-mono">Safety-under-Pressure vetting</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-8">
        {challenges.map((c, i) => (
          <motion.div
            key={c.title}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.15 }}
            className="p-5 rounded-xl bg-white/[0.02] border border-white/[0.04] hover:border-red-500/20 transition-colors"
          >
            <div className="flex items-start gap-3">
              <div className="p-2 rounded-lg bg-red-500/10 border border-red-500/20 mt-0.5">
                <c.icon className="w-4 h-4 text-red-400" />
              </div>
              <div>
                <h4 className="text-sm font-semibold text-white mb-1">{c.title}</h4>
                <p className="text-xs text-gray-500 mb-3">{c.description}</p>
                <div className="p-2 rounded bg-emerald-500/5 border border-emerald-500/10">
                  <div className="flex items-center gap-1.5 mb-1">
                    <Shield className="w-3 h-3 text-emerald-400" />
                    <span className="text-[10px] text-emerald-400 font-mono uppercase tracking-wide">Mitigation</span>
                  </div>
                  <p className="text-xs text-gray-400">{c.mitigation}</p>
                </div>
              </div>
            </div>
          </motion.div>
        ))}
      </div>

      {/* Bottom banner */}
      <div className="p-4 rounded-xl bg-gradient-to-r from-red-500/5 via-amber-500/5 to-emerald-500/5 border border-white/[0.06]">
        <div className="flex items-center justify-center gap-4 text-center">
          <div className="text-center">
            <div className="text-2xl font-bold font-mono text-white">0.87<span className="text-sm text-gray-500">ns</span></div>
            <div className="text-[10px] text-gray-500 font-mono">V23.1 Record</div>
          </div>
          <div className="text-gray-600">
            <Swords className="w-6 h-6" />
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold font-mono bg-gradient-to-r from-emerald-400 to-cyan-400 bg-clip-text text-transparent">&lt;0.5<span className="text-sm text-gray-500">ns</span></div>
            <div className="text-[10px] text-gray-500 font-mono">V24 Target</div>
          </div>
          <div className="text-gray-600">
            <Target className="w-6 h-6" />
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold font-mono text-emerald-400">100<span className="text-sm text-gray-500">%</span></div>
            <div className="text-[10px] text-gray-500 font-mono">Safety Rate</div>
          </div>
        </div>
      </div>
    </div>
  );
}
