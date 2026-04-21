import React, { useState } from 'react';
import { Cpu, Layers, HardDrive, Network } from 'lucide-react';

interface CacheNode {
  id: string;
  type: 'L1' | 'L2' | 'L3' | 'NUMA';
  label: string;
  size: string;
  latency: string;
  cores: number;
  status: 'active' | 'standby';
  stripe: number;
}

const TopologyMap: React.FC = () => {
  const [selectedNode, setSelectedNode] = useState<string | null>(null);

  const nodes: CacheNode[] = [
    // NUMA Node 0
    { id: 'numa0', type: 'NUMA', label: 'NUMA Node 0', size: '256GB', latency: '—', cores: 0, status: 'active', stripe: 0 },
    { id: 'l3-0', type: 'L3', label: 'L3 Shared (N0)', size: '64MB', latency: '12ns', cores: 8, status: 'active', stripe: 1 },
    { id: 'l2-0a', type: 'L2', label: 'L2 Cluster A', size: '4MB', latency: '4ns', cores: 4, status: 'active', stripe: 2 },
    { id: 'l2-0b', type: 'L2', label: 'L2 Cluster B', size: '4MB', latency: '4ns', cores: 4, status: 'active', stripe: 2 },
    { id: 'l1-0a', type: 'L1', label: 'L1D Core 0-3', size: '48KB×4', latency: '1ns', cores: 4, status: 'active', stripe: 3 },
    { id: 'l1-0b', type: 'L1', label: 'L1D Core 4-7', size: '48KB×4', latency: '1ns', cores: 4, status: 'active', stripe: 3 },

    // NUMA Node 1
    { id: 'numa1', type: 'NUMA', label: 'NUMA Node 1', size: '256GB', latency: '—', cores: 0, status: 'active', stripe: 0 },
    { id: 'l3-1', type: 'L3', label: 'L3 Shared (N1)', size: '64MB', latency: '12ns', cores: 8, status: 'active', stripe: 1 },
    { id: 'l2-1a', type: 'L2', label: 'L2 Cluster A', size: '4MB', latency: '4ns', cores: 4, status: 'active', stripe: 2 },
    { id: 'l2-1b', type: 'L2', label: 'L2 Cluster B', size: '4MB', latency: '4ns', cores: 4, status: 'active', stripe: 2 },
    { id: 'l1-1a', type: 'L1', label: 'L1D Core 8-11', size: '48KB×4', latency: '1ns', cores: 4, status: 'active', stripe: 3 },
    { id: 'l1-1b', type: 'L1', label: 'L1D Core 12-15', size: '48KB×4', latency: '1ns', cores: 4, status: 'active', stripe: 3 },
  ];

  const typeColors: Record<string, string> = {
    L1: 'border-emerald-500/40 bg-emerald-500/5',
    L2: 'border-cyan-500/40 bg-cyan-500/5',
    L3: 'border-purple-500/40 bg-purple-500/5',
    NUMA: 'border-amber-500/40 bg-amber-500/5',
  };

  const typeIcon: Record<string, React.ReactNode> = {
    L1: <Layers className="w-3 h-3 text-emerald-400" />,
    L2: <Layers className="w-3 h-3 text-cyan-400" />,
    L3: <HardDrive className="w-3 h-3 text-purple-400" />,
    NUMA: <Network className="w-3 h-3 text-amber-400" />,
  };

  const selected = nodes.find((n) => n.id === selectedNode);

  return (
    <section className="py-20 px-4" id="topology">
      <div className="max-w-6xl mx-auto">
        {/* Section header */}
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-purple-500/10 border border-purple-500/20 mb-4">
            <Cpu className="w-3 h-3 text-purple-400" />
            <span className="text-xs font-mono text-purple-300 tracking-wider">AUTO-DETECT TOPOLOGY</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold mb-3">
            <span className="text-shimmer">Hardware Topology Map</span>
          </h2>
          <p className="text-slate-400 max-w-xl mx-auto">
            Dynamic cache line width detection and NUMA node distance mapping during initialization.
            No hardcoded 256B assumptions — auto-aligned to detected hardware stripe.
          </p>
        </div>

        {/* Topology visualization */}
        <div className="grid md:grid-cols-2 gap-6 mb-8">
          {/* NUMA Node 0 */}
          <div className="glass-panel rounded-xl p-6">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-3 h-3 rounded-full bg-amber-400 animate-pulse" />
              <span className="text-sm font-semibold text-amber-300 font-mono">NUMA Node 0 — Socket 0</span>
            </div>

            <div className="space-y-3">
              {/* L3 */}
              <div
                className={`p-3 rounded-lg border ${typeColors.L3} ${selectedNode === 'l3-0' ? 'ring-2 ring-purple-400/50' : ''} cursor-pointer transition-all`}
                onClick={() => setSelectedNode(selectedNode === 'l3-0' ? null : 'l3-0')}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    {typeIcon.L3}
                    <span className="text-xs font-mono text-purple-300">L3 Shared</span>
                  </div>
                  <span className="text-xs font-mono text-slate-400">64MB / 12ns</span>
                </div>
                <div className="mt-2 grid grid-cols-2 gap-2">
                  <div className="p-2 rounded border border-cyan-500/20 bg-cyan-500/5">
                    <span className="text-[10px] font-mono text-cyan-300">L2 Cluster A</span>
                    <div className="text-xs font-mono text-slate-300">4MB / 4ns</div>
                  </div>
                  <div className="p-2 rounded border border-cyan-500/20 bg-cyan-500/5">
                    <span className="text-[10px] font-mono text-cyan-300">L2 Cluster B</span>
                    <div className="text-xs font-mono text-slate-300">4MB / 4ns</div>
                  </div>
                </div>
                <div className="mt-2 grid grid-cols-4 gap-1">
                  {[0, 1, 2, 3, 4, 5, 6, 7].map((i) => (
                    <div key={i} className="p-1 rounded border border-emerald-500/15 bg-emerald-500/5 text-center">
                      <div className="text-[9px] font-mono text-emerald-300/70">L1</div>
                      <div className="text-[8px] font-mono text-slate-500">C{i}</div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>

          {/* NUMA Node 1 */}
          <div className="glass-panel rounded-xl p-6">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-3 h-3 rounded-full bg-amber-400 animate-pulse" />
              <span className="text-sm font-semibold text-amber-300 font-mono">NUMA Node 1 — Socket 1</span>
            </div>

            <div className="space-y-3">
              {/* L3 */}
              <div
                className={`p-3 rounded-lg border ${typeColors.L3} ${selectedNode === 'l3-1' ? 'ring-2 ring-purple-400/50' : ''} cursor-pointer transition-all`}
                onClick={() => setSelectedNode(selectedNode === 'l3-1' ? null : 'l3-1')}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    {typeIcon.L3}
                    <span className="text-xs font-mono text-purple-300">L3 Shared</span>
                  </div>
                  <span className="text-xs font-mono text-slate-400">64MB / 12ns</span>
                </div>
                <div className="mt-2 grid grid-cols-2 gap-2">
                  <div className="p-2 rounded border border-cyan-500/20 bg-cyan-500/5">
                    <span className="text-[10px] font-mono text-cyan-300">L2 Cluster A</span>
                    <div className="text-xs font-mono text-slate-300">4MB / 4ns</div>
                  </div>
                  <div className="p-2 rounded border border-cyan-500/20 bg-cyan-500/5">
                    <span className="text-[10px] font-mono text-cyan-300">L2 Cluster B</span>
                    <div className="text-xs font-mono text-slate-300">4MB / 4ns</div>
                  </div>
                </div>
                <div className="mt-2 grid grid-cols-4 gap-1">
                  {[8, 9, 10, 11, 12, 13, 14, 15].map((i) => (
                    <div key={i} className="p-1 rounded border border-emerald-500/15 bg-emerald-500/5 text-center">
                      <div className="text-[9px] font-mono text-emerald-300/70">L1</div>
                      <div className="text-[8px] font-mono text-slate-500">C{i}</div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Inter-NUMA link */}
        <div className="glass-panel rounded-xl p-4 mb-8 flex items-center justify-center gap-4">
          <div className="h-px flex-1 bg-gradient-to-r from-transparent to-purple-500/30" />
          <div className="text-center">
            <div className="text-xs font-mono text-purple-300 mb-1">QPI / UPI Interconnect</div>
            <div className="text-[10px] font-mono text-slate-500">~40ns cross-socket latency • Hardware TSO Parity</div>
          </div>
          <div className="h-px flex-1 bg-gradient-to-l from-transparent to-purple-500/30" />
        </div>

        {/* Node detail panel */}
        {selected && (
          <div className="glass-panel rounded-xl p-6 animate-fade-in-up">
            <div className="flex items-center gap-2 mb-3">
              {typeIcon[selected.type]}
              <span className="text-sm font-semibold font-mono text-slate-200">{selected.label}</span>
              <span className={`px-2 py-0.5 rounded text-[10px] font-mono ${selected.status === 'active' ? 'bg-emerald-500/20 text-emerald-300' : 'bg-slate-700 text-slate-400'}`}>
                {selected.status.toUpperCase()}
              </span>
            </div>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Cache Size</div>
                <div className="text-sm font-mono text-cyan-300">{selected.size}</div>
              </div>
              <div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Latency</div>
                <div className="text-sm font-mono text-emerald-300">{selected.latency}</div>
              </div>
              <div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Cores</div>
                <div className="text-sm font-mono text-purple-300">{selected.cores}</div>
              </div>
              <div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Stripe Level</div>
                <div className="text-sm font-mono text-amber-300">L{selected.stripe}</div>
              </div>
            </div>
          </div>
        )}
      </div>
    </section>
  );
};

export default TopologyMap;
