import { motion } from 'framer-motion';
import { Ban, Check, X, Code } from 'lucide-react';

export function FenceLessDiscipline() {
  const bannedOps = [
    { op: 'Thread.MemoryBarrier()', reason: 'Legacy fence — adds 15-40ns overhead' },
    { op: 'Interlocked.*', reason: 'Atomic RMW — violates fence-less discipline' },
    { op: 'lock() / Monitor', reason: 'OS-level mutex — adds 20-100ns contention' },
    { op: 'volatile barriers', reason: 'Compiler+CPU fence — hardware redundancy' },
    { op: 'MemoryFence()', reason: 'Full memory ordering — unnecessary under TSO' },
  ];

  const mandatedOps = [
    { op: 'Sequence-Differencing', detail: 'Pure hardware-based ordering via monotonically increasing sequence counters' },
    { op: 'Marshal-Allocated Telemetry', detail: 'Unmanaged memory regions bypass GC and provide deterministic access' },
    { op: 'Hardware TSO Properties', detail: 'Leverage x86/ARM Total Store Order for zero-barrier correctness' },
    { op: 'Bitwise Shadow Validation', detail: 'Non-blocking integrity verification via XOR shadow vectors' },
    { op: 'Cache-Line Alignment', detail: 'Auto-detected stripe widths eliminate false sharing at source' },
  ];

  return (
    <div className="p-6 md:p-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-lg bg-red-500/10 border border-red-500/20">
          <Ban className="w-5 h-5 text-red-400" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-white">ADR-015 — Total Fence-Less Discipline</h3>
          <p className="text-xs text-gray-500 font-mono">BANNED: All legacy barriers | MANDATED: Pure hardware sequencing</p>
        </div>
      </div>

      {/* Banned Operations */}
      <div className="mb-8">
        <h4 className="text-xs font-mono text-red-400/80 mb-3 uppercase tracking-wider flex items-center gap-2">
          <X className="w-3 h-3" />
          Banned Operations (Zero Tolerance)
        </h4>
        <div className="space-y-2">
          {bannedOps.map((item) => (
            <motion.div
              key={item.op}
              initial={{ opacity: 0, x: -10 }}
              animate={{ opacity: 1, x: 0 }}
              className="flex items-center gap-3 p-3 rounded-lg bg-red-500/5 border border-red-500/10"
            >
              <X className="w-3.5 h-3.5 text-red-400 shrink-0" />
              <code className="text-sm text-red-300/80 font-mono">{item.op}</code>
              <span className="text-xs text-gray-600 font-mono ml-auto hidden md:inline">{item.reason}</span>
            </motion.div>
          ))}
        </div>
      </div>

      {/* Mandated Operations */}
      <div>
        <h4 className="text-xs font-mono text-emerald-400/80 mb-3 uppercase tracking-wider flex items-center gap-2">
          <Check className="w-3 h-3" />
          Mandated Operations (Sovereign Protocol)
        </h4>
        <div className="space-y-2">
          {mandatedOps.map((item, i) => (
            <motion.div
              key={item.op}
              initial={{ opacity: 0, x: -10 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: i * 0.1 }}
              className="flex items-start gap-3 p-3 rounded-lg bg-emerald-500/5 border border-emerald-500/10"
            >
              <Check className="w-3.5 h-3.5 text-emerald-400 shrink-0 mt-0.5" />
              <div>
                <code className="text-sm text-emerald-300/80 font-mono">{item.op}</code>
                <p className="text-xs text-gray-500 mt-0.5">{item.detail}</p>
              </div>
            </motion.div>
          ))}
        </div>
      </div>

      {/* Code icon decoration */}
      <div className="mt-6 flex items-center justify-center text-gray-700">
        <Code className="w-6 h-6" />
      </div>
    </div>
  );
}
