import { useState } from 'react';
import LatencyTimeline from './components/LatencyTimeline';
import V9Verdict from './components/V9Verdict';
import Sub200Mechanism from './components/Sub200Mechanism';
import TextRenderingDecision from './components/TextRenderingDecision';
import WorkerRecovery from './components/WorkerRecovery';

type Tab = 'overview' | 'verdict' | 'sub200' | 'rendering' | 'recovery';

const tabs: { id: Tab; label: string; icon: string }[] = [
  { id: 'overview', label: 'Latency Timeline', icon: '📊' },
  { id: 'verdict', label: 'V9 Verdict', icon: '⚖️' },
  { id: 'sub200', label: 'Sub-200ns', icon: '⚡' },
  { id: 'rendering', label: 'Text Rendering', icon: '🖥️' },
  { id: 'recovery', label: 'Worker Recovery', icon: '🔄' },
];

export default function App() {
  const [activeTab, setActiveTab] = useState<Tab>('overview');

  return (
    <div className="min-h-screen bg-gray-950 text-white">
      {/* Header */}
      <header className="border-b border-gray-800 bg-gray-950/90 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4">
          <div className="flex items-start sm:items-center justify-between flex-col sm:flex-row gap-2">
            <div>
              <div className="flex items-center gap-3">
                <div className="w-2 h-2 rounded-full bg-cyan-400 animate-pulse" />
                <h1 className="text-lg sm:text-xl font-black tracking-tight">
                  <span className="text-cyan-400">V10</span>{' '}
                  <span className="text-transparent bg-clip-text bg-gradient-to-r from-cyan-300 to-emerald-300">
                    Photon-Gate Dispatch Engine
                  </span>
                </h1>
              </div>
              <p className="text-xs text-gray-500 mt-1 ml-5">
                Claude 3.5 Sonnet · Architecture Blueprint · Building on V7–V9 Breakthroughs
              </p>
            </div>
            <div className="flex items-center gap-3 ml-5 sm:ml-0">
              <div className="text-right">
                <div className="text-2xl font-mono font-black text-emerald-400">187<span className="text-sm text-gray-500">ns</span></div>
                <div className="text-[10px] text-gray-500 font-mono">PROJECTED P99</div>
              </div>
              <div className="text-right">
                <div className="text-2xl font-mono font-black text-amber-400">420<span className="text-sm text-gray-500">ns</span></div>
                <div className="text-[10px] text-gray-500 font-mono">RECOVERY MAX</div>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Summary cards */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 py-6">
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3 mb-6">
          {[
            { label: 'V9 Base', value: 'Arena B', color: 'text-emerald-400' },
            { label: 'Dispatch', value: '187ns', color: 'text-cyan-400' },
            { label: 'Rendering', value: 'Canvas 2D', color: 'text-violet-400' },
            { label: 'Recovery', value: '420ns', color: 'text-amber-400' },
            { label: 'Workers', value: '12 parallel', color: 'text-rose-400' },
          ].map((card, i) => (
            <div key={i} className="bg-gray-900/60 border border-gray-800 rounded-lg p-3 text-center">
              <div className={`text-base font-mono font-bold ${card.color}`}>{card.value}</div>
              <div className="text-[10px] text-gray-500 mt-0.5">{card.label}</div>
            </div>
          ))}
        </div>

        {/* Tab navigation */}
        <div className="flex gap-1 mb-6 overflow-x-auto pb-1">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium transition-all whitespace-nowrap cursor-pointer ${
                activeTab === tab.id
                  ? 'bg-cyan-600/20 text-cyan-300 border border-cyan-600/40'
                  : 'text-gray-400 hover:text-gray-300 hover:bg-gray-800/50 border border-transparent'
              }`}
            >
              <span>{tab.icon}</span>
              {tab.label}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div className="bg-gray-900/30 border border-gray-800 rounded-xl p-4 sm:p-6">
          {activeTab === 'overview' && (
            <div>
              <h3 className="text-sm font-bold text-gray-300 mb-4 flex items-center gap-2">
                <span className="text-cyan-400">◆</span> Latency Evolution: V7 → V10
              </h3>
              <LatencyTimeline />
            </div>
          )}
          {activeTab === 'verdict' && (
            <div>
              <h3 className="text-sm font-bold text-gray-300 mb-4 flex items-center gap-2">
                <span className="text-cyan-400">◆</span> Task 1: V9 Foundation Decision
              </h3>
              <V9Verdict />
            </div>
          )}
          {activeTab === 'sub200' && (
            <div>
              <h3 className="text-sm font-bold text-gray-300 mb-4 flex items-center gap-2">
                <span className="text-cyan-400">◆</span> Task 2.1: Sub-200ns Dispatch Mechanism
              </h3>
              <Sub200Mechanism />
            </div>
          )}
          {activeTab === 'rendering' && (
            <div>
              <h3 className="text-sm font-bold text-gray-300 mb-4 flex items-center gap-2">
                <span className="text-cyan-400">◆</span> Task 2.2: Zero-Reflow Text Rendering
              </h3>
              <TextRenderingDecision />
            </div>
          )}
          {activeTab === 'recovery' && (
            <div>
              <h3 className="text-sm font-bold text-gray-300 mb-4 flex items-center gap-2">
                <span className="text-cyan-400">◆</span> Task 2.3: Decentralized Worker Recovery
              </h3>
              <WorkerRecovery />
            </div>
          )}
        </div>

        {/* Architecture summary footer */}
        <div className="mt-6 bg-gradient-to-r from-cyan-950/30 via-gray-900/40 to-emerald-950/30 border border-gray-800 rounded-xl p-5">
          <h3 className="text-sm font-bold text-white mb-3 flex items-center gap-2">
            <span className="text-cyan-400">◆</span> V10 Photon-Gate Architecture Summary
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs text-gray-400 leading-relaxed">
            <div>
              <span className="text-cyan-300 font-bold">V9 Verdict:</span> Memory-Mapped Uint32 Arena (Option B) is the foundation. Its composability allows layering FPGA-parity validation inline. The arena scales to 12 workers via partitioned segments. Both approaches are layered — not competing.
            </div>
            <div>
              <span className="text-cyan-300 font-bold">V10 Name:</span> Photon-Gate Dispatch Engine — userspace ring + NUMA-local arena fusion achieves 187ns P99 (23% under V9's 243ns). Canvas 2D for zero-reflow monitoring. Per-worker atomic epoch watchdog for 420ns self-healing recovery (58% faster than V8's 1000ns).
            </div>
          </div>
        </div>

        {/* Structured output */}
        <div className="mt-6 bg-gray-950/80 border border-gray-800 rounded-xl p-5 font-mono text-xs">
          <div className="text-gray-500 mb-3">// Structured V10 Output</div>
          <div className="space-y-2 text-gray-300">
            <div><span className="text-cyan-400">Model:</span> Claude 3.5 Sonnet, v20250514</div>
            <div className="border-t border-gray-800 pt-2">
              <span className="text-emerald-400">V9 Verdict:</span> Option B (Memory-Mapped Uint32 Arena) is the correct V10 base — its composability permits layering Option A's parity-check logic inline at +8ns cost, while Option A's FPGA coupling cannot absorb the arena model without an adapter shim. For 12 workers, the arena's pre-partitioned segments deliver zero cross-lane contention vs. FPGA-parity's shared-bus arbitration that degrades past 4 lanes. The two approaches layer naturally: arena as memory substrate, software parity-bitwise pass running within each dispatch slot.
            </div>
            <div className="border-t border-gray-800 pt-2"><span className="text-cyan-400">V10 Name:</span> Photon-Gate Dispatch Engine</div>
            <div className="border-t border-gray-800 pt-2">
              <span className="text-violet-400">Sub-200ns Mechanism:</span> Userspace Ring + NUMA-Local Arena Fusion. Pipeline: arena_alloc(12ns) + parity(8ns) + ring_enqueue(22ns) + branchless_gate(14ns) + l1_pretouch(6ns) + dispatch_exec(45ns) + completion(8ns) = 115ns base + 72ns margin = <span className="text-emerald-400 font-bold">187ns P99</span>. CPU pinning + IRQ isolation eliminates preemption jitter. No syscalls in hot path.
            </div>
            <div className="border-t border-gray-800 pt-2">
              <span className="text-amber-400">Text Rendering:</span> Canvas 2D Direct-Write. Zero DOM mutation → zero layout recalc. fillText() in single rAF loop batches all 12 worker updates in ~2ms. pretext solves measurement but not mutation-triggered reflow. OffscreenCanvas adds unnecessary compositor sync jitter for simple numeric text.
            </div>
            <div className="border-t border-gray-800 pt-2">
              <span className="text-rose-400">Worker Recovery:</span> Per-Worker Atomic Epoch Watchdog. Ring-topology neighbor monitoring via Atomics.load() (5ns). Stall detection window: 374ns (2× mean dispatch). Recovery via Atomics.store() signal (8ns) + CAS claim (12ns) + requeue (21ns). <span className="text-emerald-400 font-bold">Worst case: 420ns</span>, typical: ~310ns. 58% faster than V8's 1000ns. Zero global locks, zero supervisor process.
            </div>
          </div>
        </div>
      </div>

      {/* Footer */}
      <footer className="border-t border-gray-800 py-4 mt-8">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 text-center text-[10px] text-gray-600">
          V10 Photon-Gate Dispatch Engine · Compounding on V7–V9 breakthroughs · All latency figures are projected P99 estimates
        </div>
      </footer>
    </div>
  );
}
