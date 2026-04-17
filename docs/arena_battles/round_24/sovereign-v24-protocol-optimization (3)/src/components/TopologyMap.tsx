import { cn } from '../utils/cn';
import { useTopologyDetection, type TopologyNode } from '../hooks/useSimulation';

const connectionMap: Record<string, string[]> = {
  cpu0: ['l1_0', 'l1_1'],
  cpu1: ['l1_2', 'l1_3'],
  l1_0: ['l2_0'],
  l1_1: ['l2_0'],
  l1_2: ['l2_1'],
  l1_3: ['l2_1'],
  l2_0: ['l3'],
  l2_1: ['l3'],
  l3: ['numa0', 'numa1'],
  numa0: ['mem'],
  numa1: ['mem'],
};

const typeColors: Record<string, string> = {
  cpu: 'from-sov-cyan to-sov-cyan-dim',
  l1: 'from-sov-green to-sov-green-dim',
  l2: 'from-sov-amber to-yellow-600',
  l3: 'from-sov-purple to-sov-purple-dim',
  numa: 'from-pink-500 to-rose-600',
  memory: 'from-blue-500 to-blue-700',
};

// Type labels reference for documentation
// cpu: CPU Core, l1: L1 Cache, l2: L2 Cache, l3: L3 Cache, numa: NUMA Node, memory: Memory

function TopologyNodeCard({ node }: { node: TopologyNode }) {
  const colors = typeColors[node.type] || 'from-gray-500 to-gray-600';
  
  return (
    <div
      className={cn(
        'absolute transform -translate-x-1/2 -translate-y-1/2 transition-all duration-500',
        node.detected ? 'opacity-100 scale-100' : 'opacity-0 scale-75'
      )}
      style={{ left: node.x, top: node.y }}
    >
      <div className={cn(
        'px-3 py-2 rounded-lg bg-gradient-to-br border border-white/10 min-w-[100px] text-center',
        colors, 'bg-opacity-10'
      )}>
        <div className="text-xs font-bold text-white font-mono">{node.label}</div>
        {node.cacheLine && (
          <div className="text-[10px] text-white/70 font-mono mt-0.5">
            Line: {node.cacheLine}B
          </div>
        )}
        {node.latency !== undefined && (
          <div className="text-[10px] text-white/70 font-mono">
            ~{node.latency} cycles
          </div>
        )}
      </div>
    </div>
  );
}

export default function TopologyMap() {
  const { detected, progress, nodes, currentStep, detect } = useTopologyDetection();

  return (
    <section id="topology" className="py-20 relative">
      <div className="max-w-6xl mx-auto px-4">
        {/* Section header */}
        <div className="text-center mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-sov-cyan/20 bg-sov-cyan/5 mb-4">
            <span className="text-xs font-mono text-sov-cyan">MANDATE #1</span>
          </div>
          <h2 className="text-3xl sm:text-4xl font-bold text-sov-text-bright mb-3">
            Hardware-Auto-Detect <span className="text-sov-cyan">Topology</span>
          </h2>
          <p className="text-sov-text-dim max-w-xl mx-auto">
            Dynamic L1/L2/L3 cache line width and NUMA node detection. Zero hardcoded 256B assumptions.
          </p>
        </div>

        {/* Detection controls */}
        <div className="flex flex-col sm:flex-row items-center justify-center gap-4 mb-8">
          <button
            onClick={detect}
            className="px-6 py-3 rounded-lg bg-sov-cyan/10 border border-sov-cyan/30 text-sov-cyan font-mono text-sm hover:bg-sov-cyan/20 transition-all active:scale-95"
          >
            {detected ? '↻ Re-Detect Topology' : '▶ Initialize Topology Detection'}
          </button>
          
          {progress > 0 && (
            <div className="flex items-center gap-3">
              <div className="w-48 h-2 rounded-full bg-sov-surface border border-sov-border overflow-hidden">
                <div
                  className="h-full bg-gradient-to-r from-sov-cyan to-sov-purple rounded-full transition-all duration-300"
                  style={{ width: `${progress}%` }}
                />
              </div>
              <span className="text-xs font-mono text-sov-text-dim">{progress}%</span>
            </div>
          )}
        </div>

        {/* Current step */}
        {currentStep && !detected && (
          <div className="text-center mb-6">
            <span className="text-xs font-mono text-sov-cyan animate-blink">
              {'> '}{currentStep}
            </span>
          </div>
        )}

        {/* Topology visualization */}
        <div className="relative w-full h-[450px] rounded-2xl border border-sov-border bg-sov-surface/30 overflow-hidden">
          {/* Connection lines */}
          <svg className="absolute inset-0 w-full h-full pointer-events-none">
            {detected && nodes.map(node =>
              connectionMap[node.id]?.map(childId => {
                const child = nodes.find(n => n.id === childId);
                if (!child) return null;
                return (
                  <line
                    key={`${node.id}-${childId}`}
                    x1={node.x}
                    y1={node.y + 20}
                    x2={child.x}
                    y2={child.y - 20}
                    stroke="#00f0ff20"
                    strokeWidth="1"
                    strokeDasharray="4 4"
                  />
                );
              })
            )}
          </svg>

          {/* Nodes */}
          {nodes.map(node => (
            <TopologyNodeCard key={node.id} node={node} />
          ))}

          {/* Empty state */}
          {!detected && nodes.length === 0 && (
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="text-center">
                <div className="w-16 h-16 mx-auto mb-4 rounded-full border-2 border-dashed border-sov-border animate-spin-slow" />
                <p className="text-sm font-mono text-sov-text-dim">
                  Click "Initialize" to detect hardware topology
                </p>
              </div>
            </div>
          )}
        </div>

        {/* Detection results */}
        {detected && (
          <div className="mt-6 grid grid-cols-2 sm:grid-cols-4 gap-3">
            {[
              { label: 'L1 Cache Line', value: '64B', color: 'text-sov-green' },
              { label: 'L2 Stripe Width', value: '64B', color: 'text-sov-amber' },
              { label: 'L3 Shared Width', value: '64B', color: 'text-sov-purple' },
              { label: 'NUMA Nodes', value: '2', color: 'text-pink-400' },
              { label: 'Optimal Stripe', value: '64B', color: 'text-sov-cyan' },
              { label: 'TSO Support', value: 'YES (x86)', color: 'text-sov-green' },
              { label: 'Core Count', value: '8', color: 'text-sov-text-bright' },
              { label: 'Prefetch Stride', value: '256B', color: 'text-sov-text-dim' },
            ].map(item => (
              <div key={item.label} className="p-3 rounded-lg border border-sov-border bg-sov-surface/50">
                <div className="text-[10px] font-mono text-sov-text-dim uppercase">{item.label}</div>
                <div className={cn('text-lg font-bold font-mono', item.color)}>{item.value}</div>
              </div>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
