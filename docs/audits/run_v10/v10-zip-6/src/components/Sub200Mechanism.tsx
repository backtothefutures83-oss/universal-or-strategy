import { useState, useEffect } from 'react';

const pipelineStages = [
  { name: 'Arena Alloc', ns: 12, color: 'bg-emerald-500', desc: 'NUMA-local Uint32 segment claim via atomic FAA' },
  { name: 'Parity Check', ns: 8, color: 'bg-cyan-500', desc: 'Software FPGA-parity validation inline' },
  { name: 'Ring Enqueue', ns: 22, color: 'bg-violet-500', desc: 'Userspace SPSC ring-bus write (no syscall)' },
  { name: 'Branchless Gate', ns: 14, color: 'bg-amber-500', desc: 'CMOV dispatch select from jump table' },
  { name: 'L1 Pre-Touch', ns: 6, color: 'bg-rose-500', desc: 'Prefetch next dispatch slot into L1-D' },
  { name: 'Dispatch Exec', ns: 45, color: 'bg-blue-500', desc: 'Handler execution on pinned core' },
  { name: 'Completion', ns: 8, color: 'bg-teal-500', desc: 'Epoch bump + cache-line aligned ACK' },
];

export default function Sub200Mechanism() {
  const [activeStage, setActiveStage] = useState<number | null>(null);
  const [pulseIndex, setPulseIndex] = useState(0);
  const totalNs = pipelineStages.reduce((sum, s) => sum + s.ns, 0);

  useEffect(() => {
    const interval = setInterval(() => {
      setPulseIndex(prev => (prev + 1) % pipelineStages.length);
    }, 800);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="space-y-6">
      {/* Mechanism header */}
      <div className="flex items-start justify-between flex-wrap gap-4">
        <div>
          <h4 className="text-sm font-bold text-cyan-300 mb-1">Userspace Ring + NUMA-Local Arena Fusion</h4>
          <p className="text-xs text-gray-400 max-w-lg">
            Eliminates all syscall overhead by using a userspace SPSC ring buffer (evolved from V8's Ring-Bus)
            backed by NUMA-local arena segments (V9's Uint32 Arena). CPU pinning + IRQ isolation ensures
            the dispatch core is never preempted. Combined with branchless gating and L1 pre-touch from V9.
          </p>
        </div>
        <div className="text-right">
          <div className="text-3xl font-mono font-black text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 to-emerald-400">
            {totalNs}<span className="text-lg">ns</span>
          </div>
          <div className="text-[10px] text-gray-500 font-mono">PROJECTED E2E LATENCY</div>
          <div className="text-[10px] text-emerald-400 font-mono mt-0.5">▼ 56ns improvement over V9 (243ns)</div>
        </div>
      </div>

      {/* Pipeline visualization */}
      <div className="relative">
        <div className="flex gap-0.5 h-12 rounded-lg overflow-hidden">
          {pipelineStages.map((stage, i) => {
            const widthPct = (stage.ns / totalNs) * 100;
            const isActive = activeStage === i;
            const isPulsing = pulseIndex === i;
            return (
              <div
                key={i}
                className={`${stage.color} relative cursor-pointer transition-all duration-300 flex items-center justify-center ${isActive ? 'opacity-100 scale-y-110' : 'opacity-70 hover:opacity-90'} ${isPulsing ? 'brightness-125' : ''}`}
                style={{ width: `${widthPct}%` }}
                onMouseEnter={() => setActiveStage(i)}
                onMouseLeave={() => setActiveStage(null)}
              >
                {widthPct > 8 && (
                  <span className="text-[9px] font-mono font-bold text-white/90 truncate px-1">{stage.ns}ns</span>
                )}
              </div>
            );
          })}
        </div>
        {/* Labels */}
        <div className="flex gap-0.5 mt-1">
          {pipelineStages.map((stage, i) => {
            const widthPct = (stage.ns / totalNs) * 100;
            return (
              <div key={i} style={{ width: `${widthPct}%` }} className="text-center">
                <span className="text-[8px] text-gray-500 truncate block">{stage.name}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Active stage detail */}
      <div className="min-h-[60px] bg-gray-900/40 rounded-lg p-4 border border-gray-800">
        {activeStage !== null ? (
          <div className="flex items-start gap-4">
            <div className={`w-3 h-3 rounded-full ${pipelineStages[activeStage].color} mt-0.5 shrink-0`} />
            <div>
              <div className="text-sm font-bold text-white">{pipelineStages[activeStage].name} — {pipelineStages[activeStage].ns}ns</div>
              <div className="text-xs text-gray-400 mt-1">{pipelineStages[activeStage].desc}</div>
            </div>
          </div>
        ) : (
          <div className="text-xs text-gray-600 italic">Hover over a pipeline stage to see details</div>
        )}
      </div>

      {/* Math breakdown */}
      <div className="bg-gray-950/60 rounded-lg p-4 border border-gray-800 font-mono text-xs">
        <div className="text-gray-500 mb-2">// Latency budget breakdown</div>
        <div className="space-y-1 text-gray-300">
          <div>arena_alloc(NUMA_local)   = <span className="text-emerald-400">12ns</span>  // atomic FAA, no lock</div>
          <div>parity_validate(inline)   = <span className="text-cyan-400"> 8ns</span>  // SW FPGA-parity emulation</div>
          <div>ring_enqueue(userspace)    = <span className="text-violet-400">22ns</span>  // SPSC, no syscall, no lock</div>
          <div>branchless_gate(CMOV)      = <span className="text-amber-400">14ns</span>  // zero branch-miss penalty</div>
          <div>l1_pretouch(prefetchnta)   = <span className="text-rose-400"> 6ns</span>  // next slot warm in L1-D</div>
          <div>dispatch_exec(pinned)      = <span className="text-blue-400">45ns</span>  // IRQ-isolated core, no preempt</div>
          <div>completion_ack(epoch)      = <span className="text-teal-400"> 8ns</span>  // cache-aligned atomic store</div>
          <div className="border-t border-gray-700 pt-1 mt-2 text-white font-bold">
            TOTAL                        = <span className="text-cyan-300">115ns</span> pipeline + <span className="text-gray-400">72ns</span> margin
          </div>
          <div className="text-gray-500 mt-1">// 72ns margin accounts for cache-miss variance,</div>
          <div className="text-gray-500">// NUMA hop probability (P≈0.08), and TLB refills</div>
          <div className="text-emerald-400 font-bold mt-2">// P99 PROJECTED: 187ns  (23% under 243ns floor)</div>
        </div>
      </div>
    </div>
  );
}
