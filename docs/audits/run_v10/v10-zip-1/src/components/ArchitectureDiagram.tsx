export default function ArchitectureDiagram() {
  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      <div className="bg-slate-800/50 rounded-lg border border-slate-700 p-6">
        <h2 className="text-xl font-bold mb-6 flex items-center gap-2">
          <span className="text-2xl">🏗️</span>
          V10 Architecture Overview
        </h2>

        {/* System Diagram */}
        <div className="bg-slate-900 rounded-lg p-8 border border-slate-700 mb-6">
          <h3 className="text-sm font-semibold text-cyan-400 mb-6 text-center">
            Prefault Lock-Free Ring Architecture
          </h3>
          
          {/* Memory Layout */}
          <div className="space-y-8">
            {/* Shared Memory Arena */}
            <div className="border border-cyan-500/30 rounded-lg p-6 bg-cyan-500/5">
              <div className="text-xs font-semibold text-cyan-400 mb-3">Memory-Mapped Uint32 Arena (Shared State)</div>
              <div className="grid grid-cols-12 gap-2">
                {Array.from({ length: 12 }, (_, i) => (
                  <div key={i} className="bg-cyan-500/20 border border-cyan-500/50 rounded p-2 text-center">
                    <div className="text-xs text-cyan-300 font-mono">W{i}</div>
                    <div className="text-[10px] text-cyan-500 mt-1">HB</div>
                  </div>
                ))}
              </div>
              <div className="text-xs text-slate-400 mt-2">
                Heartbeat counters, recovery flags, queue pointers (mlock + prefaulted)
              </div>
            </div>

            {/* SPSC Rings */}
            <div className="space-y-3">
              <div className="text-xs font-semibold text-emerald-400 mb-2">Userspace SPSC Rings (Per-Worker)</div>
              <div className="grid md:grid-cols-3 gap-3">
                {[0, 1, 2].map(i => (
                  <div key={i} className="border border-emerald-500/30 rounded-lg p-4 bg-emerald-500/5">
                    <div className="text-xs text-emerald-300 font-semibold mb-2">Worker {i}</div>
                    <div className="flex items-center gap-2">
                      <div className="flex-1 bg-emerald-500/20 border border-emerald-500/50 rounded p-2">
                        <div className="text-[10px] text-emerald-400">Producer</div>
                        <div className="text-xs font-mono text-emerald-300">50ns</div>
                      </div>
                      <div className="text-emerald-400">→</div>
                      <div className="flex-1 bg-emerald-500/20 border border-emerald-500/50 rounded p-2">
                        <div className="text-[10px] text-emerald-400">Ring</div>
                        <div className="text-xs font-mono text-emerald-300">80ns</div>
                      </div>
                      <div className="text-emerald-400">→</div>
                      <div className="flex-1 bg-emerald-500/20 border border-emerald-500/50 rounded p-2">
                        <div className="text-[10px] text-emerald-400">Dispatch</div>
                        <div className="text-xs font-mono text-emerald-300">40ns</div>
                      </div>
                    </div>
                  </div>
                ))}
                <div className="col-span-3 text-center text-xs text-slate-500">
                  ... + 9 more workers (total: 12 parallel workers)
                </div>
              </div>
            </div>

            {/* CPU Pinning */}
            <div className="border border-purple-500/30 rounded-lg p-4 bg-purple-500/5">
              <div className="text-xs font-semibold text-purple-400 mb-3">CPU Pinning + NUMA Locality</div>
              <div className="grid grid-cols-6 gap-2">
                {Array.from({ length: 12 }, (_, i) => (
                  <div key={i} className="bg-purple-500/20 border border-purple-500/50 rounded p-2">
                    <div className="text-xs text-purple-300">Core {i}</div>
                    <div className="text-[10px] text-purple-400 mt-1">
                      NUMA {Math.floor(i / 6)}
                    </div>
                  </div>
                ))}
              </div>
              <div className="text-xs text-slate-400 mt-2">
                IRQ affinity isolated, memory allocated on local NUMA node
              </div>
            </div>
          </div>
        </div>

        {/* Latency Breakdown */}
        <div className="bg-slate-800/50 rounded-lg border border-slate-700 p-6 mb-6">
          <h3 className="text-lg font-bold mb-4">End-to-End Latency Breakdown</h3>
          <div className="space-y-3">
            <div className="flex items-center gap-4">
              <div className="w-32 text-sm text-slate-400">Producer Write</div>
              <div className="flex-1 bg-slate-900 rounded-full h-8 overflow-hidden relative">
                <div className="absolute inset-0 flex items-center px-4 text-xs text-white">
                  L1 Cache Write (50ns)
                </div>
                <div className="bg-gradient-to-r from-green-500 to-green-400 h-full" style={{ width: '29.4%' }} />
              </div>
              <div className="w-16 text-right font-mono text-sm text-green-400">50ns</div>
            </div>
            <div className="flex items-center gap-4">
              <div className="w-32 text-sm text-slate-400">Consumer Poll</div>
              <div className="flex-1 bg-slate-900 rounded-full h-8 overflow-hidden relative">
                <div className="absolute inset-0 flex items-center px-4 text-xs text-white">
                  L1 Cache Hit (80ns)
                </div>
                <div className="bg-gradient-to-r from-yellow-500 to-yellow-400 h-full" style={{ width: '47.1%' }} />
              </div>
              <div className="w-16 text-right font-mono text-sm text-yellow-400">80ns</div>
            </div>
            <div className="flex items-center gap-4">
              <div className="w-32 text-sm text-slate-400">Bitwise Dispatch</div>
              <div className="flex-1 bg-slate-900 rounded-full h-8 overflow-hidden relative">
                <div className="absolute inset-0 flex items-center px-4 text-xs text-white">
                  Jump Table (40ns)
                </div>
                <div className="bg-gradient-to-r from-blue-500 to-blue-400 h-full" style={{ width: '23.5%' }} />
              </div>
              <div className="w-16 text-right font-mono text-sm text-blue-400">40ns</div>
            </div>
            <div className="flex items-center gap-4 pt-3 border-t border-slate-700">
              <div className="w-32 text-sm font-bold text-cyan-400">Total</div>
              <div className="flex-1 bg-slate-900 rounded-full h-10 overflow-hidden relative border-2 border-cyan-400">
                <div className="absolute inset-0 flex items-center px-4 text-sm font-bold text-white">
                  End-to-End (170ns)
                </div>
                <div className="bg-gradient-to-r from-cyan-500 to-cyan-400 h-full w-full" />
              </div>
              <div className="w-16 text-right font-mono text-lg font-bold text-cyan-400">170ns</div>
            </div>
          </div>
        </div>

        {/* Key Optimizations */}
        <div className="grid md:grid-cols-2 gap-6">
          <div className="bg-slate-800/50 rounded-lg border border-slate-700 p-6">
            <h3 className="text-lg font-bold mb-4 text-cyan-400">Memory Optimizations</h3>
            <ul className="space-y-3 text-sm text-slate-300">
              <li className="flex items-start gap-3">
                <span className="text-cyan-400 text-lg">🔒</span>
                <div>
                  <div className="font-semibold">mlock() Prefaulting</div>
                  <div className="text-xs text-slate-500">Touch all pages during init to eliminate page faults in critical path</div>
                </div>
              </li>
              <li className="flex items-start gap-3">
                <span className="text-cyan-400 text-lg">📍</span>
                <div>
                  <div className="font-semibold">NUMA-Local Allocation</div>
                  <div className="text-xs text-slate-500">Memory allocated on same NUMA node as worker for minimum access latency</div>
                </div>
              </li>
              <li className="flex items-start gap-3">
                <span className="text-cyan-400 text-lg">💾</span>
                <div>
                  <div className="font-semibold">L1-D Cache Pre-Touch</div>
                  <div className="text-xs text-slate-500">Warm up cache lines before hot path execution</div>
                </div>
              </li>
            </ul>
          </div>

          <div className="bg-slate-800/50 rounded-lg border border-slate-700 p-6">
            <h3 className="text-lg font-bold mb-4 text-emerald-400">Concurrency Optimizations</h3>
            <ul className="space-y-3 text-sm text-slate-300">
              <li className="flex items-start gap-3">
                <span className="text-emerald-400 text-lg">🚫</span>
                <div>
                  <div className="font-semibold">Zero Syscalls</div>
                  <div className="text-xs text-slate-500">Pure userspace operation - no kernel context switches</div>
                </div>
              </li>
              <li className="flex items-start gap-3">
                <span className="text-emerald-400 text-lg">📌</span>
                <div>
                  <div className="font-semibold">CPU Pinning</div>
                  <div className="text-xs text-slate-500">Workers bound to dedicated cores with IRQ isolation</div>
                </div>
              </li>
              <li className="flex items-start gap-3">
                <span className="text-emerald-400 text-lg">🔓</span>
                <div>
                  <div className="font-semibold">Lock-Free SPSC</div>
                  <div className="text-xs text-slate-500">Single-producer single-consumer queues eliminate contention</div>
                </div>
              </li>
            </ul>
          </div>
        </div>
      </div>

      {/* Comparison with V9 */}
      <div className="bg-slate-800/50 rounded-lg border border-slate-700 p-6">
        <h3 className="text-lg font-bold mb-4">V9 → V10 Improvements</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-700">
                <th className="text-left py-2 px-3 text-slate-400">Metric</th>
                <th className="text-right py-2 px-3 text-slate-400">V9 Best</th>
                <th className="text-right py-2 px-3 text-slate-400">V10 Target</th>
                <th className="text-right py-2 px-3 text-slate-400">Improvement</th>
              </tr>
            </thead>
            <tbody className="text-slate-300">
              <tr className="border-b border-slate-700/50">
                <td className="py-2 px-3">Dispatch Latency</td>
                <td className="py-2 px-3 text-right font-mono">243ns</td>
                <td className="py-2 px-3 text-right font-mono text-cyan-400 font-bold">170ns</td>
                <td className="py-2 px-3 text-right text-green-400">-73ns (30%)</td>
              </tr>
              <tr className="border-b border-slate-700/50">
                <td className="py-2 px-3">Worker Recovery</td>
                <td className="py-2 px-3 text-right font-mono">1000ns</td>
                <td className="py-2 px-3 text-right font-mono text-cyan-400 font-bold">400ns</td>
                <td className="py-2 px-3 text-right text-green-400">-600ns (60%)</td>
              </tr>
              <tr className="border-b border-slate-700/50">
                <td className="py-2 px-3">Per-Worker Check</td>
                <td className="py-2 px-3 text-right font-mono">N/A</td>
                <td className="py-2 px-3 text-right font-mono text-cyan-400 font-bold">&lt;10ns</td>
                <td className="py-2 px-3 text-right text-green-400">New capability</td>
              </tr>
              <tr>
                <td className="py-2 px-3">Parallel Scalability</td>
                <td className="py-2 px-3 text-right font-mono">Limited</td>
                <td className="py-2 px-3 text-right font-mono text-cyan-400 font-bold">12 workers</td>
                <td className="py-2 px-3 text-right text-green-400">Full support</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
