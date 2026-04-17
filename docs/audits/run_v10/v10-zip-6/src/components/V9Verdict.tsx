export default function V9Verdict() {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Option A */}
        <div className="relative bg-gray-900/60 border border-cyan-700/40 rounded-xl p-5 overflow-hidden">
          <div className="absolute top-0 right-0 bg-cyan-600/20 text-cyan-300 text-[10px] font-mono px-2 py-0.5 rounded-bl-lg">OPTION A</div>
          <h4 className="text-sm font-bold text-cyan-300 mb-1">FPGA-Parity Bitwise Pass</h4>
          <div className="text-2xl font-mono font-black text-white mb-2">243<span className="text-sm text-gray-400">ns</span></div>
          <div className="space-y-1.5 text-xs text-gray-400">
            <div className="flex items-center gap-2"><span className="text-emerald-400">✓</span> Raw speed winner</div>
            <div className="flex items-center gap-2"><span className="text-emerald-400">✓</span> Minimal instruction path</div>
            <div className="flex items-center gap-2"><span className="text-amber-400">△</span> Single-lane affinity</div>
            <div className="flex items-center gap-2"><span className="text-red-400">✗</span> FPGA coupling limits composability</div>
          </div>
        </div>
        {/* Option B */}
        <div className="relative bg-gray-900/60 border border-emerald-700/40 rounded-xl p-5 overflow-hidden">
          <div className="absolute top-0 right-0 bg-emerald-600/20 text-emerald-300 text-[10px] font-mono px-2 py-0.5 rounded-bl-lg">OPTION B</div>
          <h4 className="text-sm font-bold text-emerald-300 mb-1">Memory-Mapped Uint32 Arena</h4>
          <div className="text-2xl font-mono font-black text-white mb-2">250<span className="text-sm text-gray-400">ns</span></div>
          <div className="space-y-1.5 text-xs text-gray-400">
            <div className="flex items-center gap-2"><span className="text-emerald-400">✓</span> Composable with any dispatch</div>
            <div className="flex items-center gap-2"><span className="text-emerald-400">✓</span> Scales to 12+ workers</div>
            <div className="flex items-center gap-2"><span className="text-emerald-400">✓</span> Zero GC pressure</div>
            <div className="flex items-center gap-2"><span className="text-amber-400">△</span> 7ns slower baseline</div>
          </div>
        </div>
      </div>

      {/* Verdict */}
      <div className="bg-gradient-to-r from-cyan-950/40 via-gray-900/60 to-emerald-950/40 border border-cyan-600/30 rounded-xl p-6">
        <div className="flex items-center gap-2 mb-3">
          <div className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          <h4 className="text-sm font-bold text-white tracking-wide">V9 FOUNDATION VERDICT</h4>
        </div>
        <div className="space-y-3 text-sm text-gray-300 leading-relaxed">
          <p>
            <span className="text-emerald-400 font-bold">Option B (Memory-Mapped Uint32 Arena)</span> is the correct V10 base because its composability allows layering any dispatch strategy on top — including Option A's parity-check logic — whereas Option A's FPGA-coupled design cannot easily absorb the arena's memory model without an adapter shim that would erase its 7ns advantage.
          </p>
          <p>
            For 12 parallel workers, the arena's pre-partitioned Uint32 segments give each worker a private allocation lane with zero cross-lane contention, whereas the FPGA-parity pass was benchmarked on a single dispatch path and would require 12× parity-check replication with shared-bus arbitration — a pattern that historically degrades super-linearly beyond 4 lanes.
          </p>
          <p>
            Crucially, <span className="text-cyan-300 font-semibold">the two approaches can and should be layered</span>: the arena provides the memory substrate while a software-emulated parity-bitwise validation pass runs inline within each arena segment's dispatch slot, recovering Option A's integrity guarantees at a cost of only ~8ns over the arena baseline, well under the original 243ns.
          </p>
        </div>
      </div>
    </div>
  );
}
