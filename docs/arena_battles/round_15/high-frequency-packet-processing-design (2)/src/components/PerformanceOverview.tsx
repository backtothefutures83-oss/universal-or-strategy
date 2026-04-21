export function PerformanceOverview() {
  return (
    <div className="space-y-8">
      {/* Hero Section */}
      <div className="bg-gradient-to-br from-slate-800 to-slate-900 rounded-2xl p-8 border border-slate-700 shadow-2xl">
        <h2 className="text-3xl font-bold text-white mb-4">Engineering Specification</h2>
        <p className="text-slate-300 text-lg leading-relaxed mb-6">
          This document outlines a comprehensive design for a sub-5ns packet processing pipeline
          optimized for high-frequency trading and network packet processing scenarios. All components
          are designed around custom atomic topologies, avoiding generic C# collections that regressed to 50ns.
        </p>
        <div className="grid grid-cols-3 gap-4 mt-6">
          <div className="bg-slate-900/50 rounded-lg p-4 border border-slate-600">
            <div className="text-cyan-400 text-2xl font-bold mb-1">Zero</div>
            <div className="text-slate-400 text-sm">Allocations</div>
          </div>
          <div className="bg-slate-900/50 rounded-lg p-4 border border-slate-600">
            <div className="text-cyan-400 text-2xl font-bold mb-1">&lt; 5ns</div>
            <div className="text-slate-400 text-sm">Total Cycle Time</div>
          </div>
          <div className="bg-slate-900/50 rounded-lg p-4 border border-slate-600">
            <div className="text-cyan-400 text-2xl font-bold mb-1">12</div>
            <div className="text-slate-400 text-sm">Parallel Threads</div>
          </div>
        </div>
      </div>

      {/* Architecture Components */}
      <div className="grid md:grid-cols-3 gap-6">
        <div className="bg-slate-800 rounded-xl p-6 border border-slate-700 hover:border-cyan-500/50 transition-colors">
          <div className="text-4xl mb-4">🔄</div>
          <h3 className="text-xl font-bold text-white mb-2">Ingress Bridge</h3>
          <p className="text-slate-400 text-sm mb-4">
            Zero-allocation pre-allocated memory ring that moves byte-buffers from external sockets
            to local processing cores without concurrent collections.
          </p>
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-slate-500">Latency Impact:</span>
              <span className="text-green-400 font-mono">~0.8ns</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-slate-500">Technique:</span>
              <span className="text-slate-300">Memory Ring</span>
            </div>
          </div>
        </div>

        <div className="bg-slate-800 rounded-xl p-6 border border-slate-700 hover:border-cyan-500/50 transition-colors">
          <div className="text-4xl mb-4">🏷️</div>
          <h3 className="text-xl font-bold text-white mb-2">Tagged Pointers</h3>
          <p className="text-slate-400 text-sm mb-4">
            64-bit tagged pointer system packing 48-bit indices with 16-bit epoch/generation
            for ABA safety in multi-producer environments.
          </p>
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-slate-500">Latency Impact:</span>
              <span className="text-green-400 font-mono">~1.2ns</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-slate-500">Technique:</span>
              <span className="text-slate-300">Bitwise Packing</span>
            </div>
          </div>
        </div>

        <div className="bg-slate-800 rounded-xl p-6 border border-slate-700 hover:border-cyan-500/50 transition-colors">
          <div className="text-4xl mb-4">⚡</div>
          <h3 className="text-xl font-bold text-white mb-2">Cache Guards</h3>
          <p className="text-slate-400 text-sm mb-4">
            Memory alignment and struct padding strategy preventing L1/L2 cache-line invalidation
            storms across 12 parallel threads.
          </p>
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-slate-500">Latency Impact:</span>
              <span className="text-green-400 font-mono">~0.5ns</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-slate-500">Technique:</span>
              <span className="text-slate-300">Cache Alignment</span>
            </div>
          </div>
        </div>
      </div>

      {/* Performance Budget */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4">Performance Budget Breakdown</h3>
        <div className="space-y-3">
          <div className="flex items-center gap-4">
            <div className="w-32 text-slate-400 text-sm">Ingress Bridge:</div>
            <div className="flex-1 bg-slate-900 rounded-full h-6 overflow-hidden">
              <div className="bg-gradient-to-r from-cyan-500 to-cyan-600 h-full flex items-center px-3" style={{width: '16%'}}>
                <span className="text-xs text-white font-mono">0.8ns</span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-32 text-slate-400 text-sm">Tagged Pointers:</div>
            <div className="flex-1 bg-slate-900 rounded-full h-6 overflow-hidden">
              <div className="bg-gradient-to-r from-blue-500 to-blue-600 h-full flex items-center px-3" style={{width: '24%'}}>
                <span className="text-xs text-white font-mono">1.2ns</span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-32 text-slate-400 text-sm">Cache Guards:</div>
            <div className="flex-1 bg-slate-900 rounded-full h-6 overflow-hidden">
              <div className="bg-gradient-to-r from-green-500 to-green-600 h-full flex items-center px-3" style={{width: '10%'}}>
                <span className="text-xs text-white font-mono">0.5ns</span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-32 text-slate-400 text-sm">Budget Reserve:</div>
            <div className="flex-1 bg-slate-900 rounded-full h-6 overflow-hidden">
              <div className="bg-gradient-to-r from-slate-600 to-slate-700 h-full flex items-center px-3" style={{width: '50%'}}>
                <span className="text-xs text-white font-mono">2.5ns</span>
              </div>
            </div>
          </div>
          <div className="border-t border-slate-700 pt-3 mt-2">
            <div className="flex justify-between items-center">
              <span className="text-white font-semibold">Total Budget:</span>
              <span className="text-2xl font-bold text-cyan-400 font-mono">&lt; 5.0ns</span>
            </div>
          </div>
        </div>
      </div>

      {/* Key Constraints */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4">Architectural Constraints</h3>
        <div className="grid md:grid-cols-2 gap-4">
          <div className="space-y-3">
            <div className="flex items-start gap-3">
              <div className="text-red-400 mt-1">✗</div>
              <div>
                <div className="text-white font-medium">No Generic Collections</div>
                <div className="text-slate-400 text-sm">C# collections regressed to 50ns latency</div>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <div className="text-red-400 mt-1">✗</div>
              <div>
                <div className="text-white font-medium">No Wait-Free Queues</div>
                <div className="text-slate-400 text-sm">Standard queues introduce unacceptable overhead</div>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <div className="text-red-400 mt-1">✗</div>
              <div>
                <div className="text-white font-medium">No Object Pooling</div>
                <div className="text-slate-400 text-sm">Traditional object pools add allocation overhead</div>
              </div>
            </div>
          </div>
          <div className="space-y-3">
            <div className="flex items-start gap-3">
              <div className="text-green-400 mt-1">✓</div>
              <div>
                <div className="text-white font-medium">Custom Atomic Topologies</div>
                <div className="text-slate-400 text-sm">Hand-crafted lock-free data structures</div>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <div className="text-green-400 mt-1">✓</div>
              <div>
                <div className="text-white font-medium">Pre-Allocated Memory</div>
                <div className="text-slate-400 text-sm">Zero runtime allocation strategy</div>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <div className="text-green-400 mt-1">✓</div>
              <div>
                <div className="text-white font-medium">Cache-Line Awareness</div>
                <div className="text-slate-400 text-sm">64-byte alignment for false-sharing prevention</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
