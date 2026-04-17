import { useState, useEffect } from 'react';

interface CacheNode {
  id: string;
  type: 'L1' | 'L2' | 'L3' | 'NUMA';
  size: string;
  line: string;
  cores: number;
  x: number;
  y: number;
  active: boolean;
  contention: number;
}

interface Link {
  from: string;
  to: string;
  bandwidth: string;
  latency: number;
}

export function TopologyVisualization() {
  const [detectedTopo, setDetectedTopo] = useState<CacheNode[]>([]);
  const [links, setLinks] = useState<Link[]>([]);
  const [selectedNode, setSelectedNode] = useState<string | null>(null);
  const [detectionPhase, setDetectionPhase] = useState(0);
  const [detecting, setDetecting] = useState(false);

  const topologies: CacheNode[][] = [
    // Intel-like topology
    [
      { id: 'numa0', type: 'NUMA', size: '—', line: '—', cores: 0, x: 150, y: 200, active: false, contention: 0 },
      { id: 'l3_0', type: 'L3', size: '36 MB', line: '64B', cores: 0, x: 150, y: 140, active: false, contention: 12 },
      { id: 'l2_0', type: 'L2', size: '1.25 MB', line: '64B', cores: 2, x: 100, y: 80, active: false, contention: 8 },
      { id: 'l2_1', type: 'L2', size: '1.25 MB', line: '64B', cores: 2, x: 200, y: 80, active: false, contention: 15 },
      { id: 'l1a_0', type: 'L1', size: '48 KB', line: '64B', cores: 1, x: 70, y: 30, active: false, contention: 5 },
      { id: 'l1b_0', type: 'L1', size: '48 KB', line: '64B', cores: 1, x: 130, y: 30, active: false, contention: 3 },
      { id: 'l1a_1', type: 'L1', size: '48 KB', line: '64B', cores: 1, x: 170, y: 30, active: false, contention: 12 },
      { id: 'l1b_1', type: 'L1', size: '48 KB', line: '64B', cores: 1, x: 230, y: 30, active: false, contention: 7 },
    ],
    // AMD-like topology
    [
      { id: 'numa0', type: 'NUMA', size: '—', line: '—', cores: 0, x: 150, y: 200, active: false, contention: 0 },
      { id: 'l3_0', type: 'L3', size: '64 MB', line: '64B', cores: 0, x: 150, y: 140, active: false, contention: 18 },
      { id: 'ccx0', type: 'L2', size: '4 MB', line: '64B', cores: 4, x: 80, y: 80, active: false, contention: 10 },
      { id: 'ccx1', type: 'L2', size: '4 MB', line: '64B', cores: 4, x: 220, y: 80, active: false, contention: 22 },
      { id: 'l1_0', type: 'L1', size: '32 KB', line: '64B', cores: 1, x: 50, y: 25, active: false, contention: 4 },
      { id: 'l1_1', type: 'L1', size: '32 KB', line: '64B', cores: 1, x: 110, y: 25, active: false, contention: 6 },
      { id: 'l1_2', type: 'L1', size: '32 KB', line: '64B', cores: 1, x: 190, y: 25, active: false, contention: 14 },
      { id: 'l1_3', type: 'L1', size: '32 KB', line: '64B', cores: 1, x: 250, y: 25, active: false, contention: 9 },
    ],
  ];

  const linkDefs: Link[][] = [
    [
      { from: 'numa0', to: 'l3_0', bandwidth: '64 GB/s', latency: 80 },
      { from: 'l3_0', to: 'l2_0', bandwidth: '128 GB/s', latency: 12 },
      { from: 'l3_0', to: 'l2_1', bandwidth: '128 GB/s', latency: 12 },
      { from: 'l2_0', to: 'l1a_0', bandwidth: '256 GB/s', latency: 4 },
      { from: 'l2_0', to: 'l1b_0', bandwidth: '256 GB/s', latency: 4 },
      { from: 'l2_1', to: 'l1a_1', bandwidth: '256 GB/s', latency: 4 },
      { from: 'l2_1', to: 'l1b_1', bandwidth: '256 GB/s', latency: 4 },
    ],
    [
      { from: 'numa0', to: 'l3_0', bandwidth: '80 GB/s', latency: 75 },
      { from: 'l3_0', to: 'ccx0', bandwidth: '200 GB/s', latency: 10 },
      { from: 'l3_0', to: 'ccx1', bandwidth: '200 GB/s', latency: 10 },
      { from: 'ccx0', to: 'l1_0', bandwidth: '320 GB/s', latency: 3 },
      { from: 'ccx0', to: 'l1_1', bandwidth: '320 GB/s', latency: 3 },
      { from: 'ccx1', to: 'l1_2', bandwidth: '320 GB/s', latency: 3 },
      { from: 'ccx1', to: 'l1_3', bandwidth: '320 GB/s', latency: 3 },
    ],
  ];

  const topoNames = ['Intel Xeon (Sapphire Rapids)', 'AMD EPYC (Genoa)'];

  const [topoIdx, setTopoIdx] = useState(0);

  useEffect(() => {
    setDetecting(true);
    setDetectionPhase(0);
    setSelectedNode(null);
    const t = topoIdx;
    setDetectedTopo([]);
    setLinks([]);

    // Phase 1: Detect NUMA
    setTimeout(() => {
      setDetectionPhase(1);
      setDetectedTopo(prev => [...prev, { ...topologies[t][0], active: true }]);
    }, 500);

    // Phase 2: Detect L3
    setTimeout(() => {
      setDetectionPhase(2);
      setDetectedTopo(prev => [...prev, { ...topologies[t][1], active: true }]);
      setLinks(linkDefs[t].filter(l => l.to === 'l3_0'));
    }, 1200);

    // Phase 3: Detect L2/CCX
    setTimeout(() => {
      setDetectionPhase(3);
      setDetectedTopo(prev => [...prev, { ...topologies[t][2], active: true }, { ...topologies[t][3], active: true }]);
      setLinks(linkDefs[t].filter(l => l.to === 'l3_0' || l.to === 'l2_0' || l.to === 'l2_1' || l.to === 'ccx0' || l.to === 'ccx1'));
    }, 2000);

    // Phase 4: Detect L1
    setTimeout(() => {
      setDetectionPhase(4);
      setDetectedTopo(prev => [...prev, ...topologies[t].slice(4).map(n => ({ ...n, active: true }))]);
      setLinks(linkDefs[t]);
      setDetecting(false);
    }, 2800);
  }, [topoIdx]);

  const getNodeById = (id: string) => detectedTopo.find(n => n.id === id);

  const typeColor = (type: string) => {
    switch (type) {
      case 'L1': return '#00ffc8';
      case 'L2': return '#00f0ff';
      case 'L3': return '#ffb300';
      case 'NUMA': return '#a855f7';
      default: return '#2a3d5a';
    }
  };

  return (
    <section id="topology" className="py-24 px-4 hex-bg">
      <div className="max-w-6xl mx-auto">
        {/* Header */}
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-cyan-glow/10 bg-cyan-glow/5 mb-4">
            <span className="text-xs font-mono text-cyan-glow/60 tracking-widest uppercase">MANDATE 1</span>
          </div>
          <h2 className="text-3xl md:text-5xl font-bold text-white mb-4">
            Hardware-Auto-Detect <span className="text-cyan-glow">Topology</span>
          </h2>
          <p className="text-sov-400 max-w-2xl mx-auto">
            Dynamic identification of L1/L2/L3 cache line widths and NUMA node distances.
            No hardcoded 256B assumptions — auto-alignment to detected hardware stripe.
          </p>
        </div>

        {/* Topology selector */}
        <div className="flex justify-center gap-4 mb-8">
          {topoNames.map((name, i) => (
            <button
              key={i}
              onClick={() => setTopoIdx(i)}
              className={`px-4 py-2 rounded-lg font-mono text-sm transition-all ${
                topoIdx === i
                  ? 'bg-cyan-glow/10 border border-cyan-glow/30 text-cyan-glow'
                  : 'bg-sov-700/50 border border-sov-600 text-sov-400 hover:border-cyan-glow/20'
              }`}
            >
              {name}
            </button>
          ))}
        </div>

        {/* Detection status */}
        <div className="flex items-center justify-center gap-4 mb-6">
          {['NUMA', 'L3', 'L2/CCX', 'L1'].map((phase, i) => (
            <div key={i} className="flex items-center gap-2">
              <div className={`w-3 h-3 rounded-full transition-all ${
                detectionPhase > i 
                  ? 'bg-cyan-glow shadow-lg shadow-cyan-glow/50' 
                  : detectionPhase === i 
                    ? 'bg-cyan-glow/50 animate-pulse' 
                    : 'bg-sov-600'
              }`} />
              <span className={`text-xs font-mono ${detectionPhase > i ? 'text-cyan-glow' : 'text-sov-400'}`}>
                {phase}
              </span>
              {i < 3 && <div className={`w-6 h-px ${detectionPhase > i + 1 ? 'bg-cyan-glow/50' : 'bg-sov-600'}`} />}
            </div>
          ))}
          {detecting && <span className="text-xs text-cyan-glow/60 font-mono ml-2 animate-pulse">DETECTING...</span>}
        </div>

        {/* SVG Topology Map */}
        <div className="glass-panel rounded-xl p-6 glow-border overflow-x-auto">
          <svg viewBox="0 0 300 220" className="w-full max-w-lg mx-auto" style={{ minHeight: '220px' }}>
            {/* Links */}
            {links.map((link, i) => {
              const from = getNodeById(link.from);
              const to = getNodeById(link.to);
              if (!from || !to) return null;
              return (
                <g key={i}>
                  <line
                    x1={from.x} y1={from.y}
                    x2={to.x} y2={to.y}
                    stroke={selectedNode === link.from || selectedNode === link.to ? '#00f0ff' : '#1f2e44'}
                    strokeWidth={selectedNode === link.from || selectedNode === link.to ? 2 : 1}
                    strokeDasharray={selectedNode === link.from || selectedNode === link.to ? '0' : '4,4'}
                    className={selectedNode === link.from || selectedNode === link.to ? 'animate-data-flow' : ''}
                  />
                </g>
              );
            })}

            {/* Nodes */}
            {detectedTopo.map(node => {
              const isSelected = selectedNode === node.id;
              const isHighlighted = links.some(l => 
                (l.from === selectedNode && l.to === node.id) || 
                (l.to === selectedNode && l.from === node.id)
              );
              const color = typeColor(node.type);
              
              return (
                <g 
                  key={node.id} 
                  onClick={() => setSelectedNode(node.id === selectedNode ? null : node.id)}
                  className="cursor-pointer"
                >
                  {/* Node circle */}
                  <circle
                    cx={node.x} cy={node.y}
                    r={node.type === 'NUMA' ? 22 : node.type === 'L3' ? 18 : node.type === 'L2' ? 14 : 10}
                    fill={`${color}15`}
                    stroke={isSelected ? color : isHighlighted ? `${color}80` : `${color}40`}
                    strokeWidth={isSelected ? 2.5 : isHighlighted ? 1.5 : 1}
                    className="transition-all duration-300"
                  />
                  {/* Glow for selected */}
                  {isSelected && (
                    <circle cx={node.x} cy={node.y} r={node.type === 'NUMA' ? 28 : node.type === 'L3' ? 24 : node.type === 'L2' ? 20 : 16}
                      fill="none" stroke={color} strokeWidth={0.5} opacity={0.3}>
                      <animate attributeName="r" from="20" to="32" dur="2s" repeatCount="indefinite" />
                      <animate attributeName="opacity" from="0.3" to="0" dur="2s" repeatCount="indefinite" />
                    </circle>
                  )}
                  {/* Label */}
                  <text x={node.x} y={node.y + 1} textAnchor="middle" dominantBaseline="middle" 
                    fill={color} fontSize={node.type === 'NUMA' ? 8 : 6} fontFamily="monospace" fontWeight="bold">
                    {node.type}
                  </text>
                  {/* Contention bar (L1 nodes) */}
                  {node.type === 'L1' && (
                    <rect x={node.x - 12} y={node.y + 14} width={24} height={2} fill="#1a2538" rx={1} />
                  )}
                  {node.type === 'L1' && (
                    <rect x={node.x - 12} y={node.y + 14} width={24 * (node.contention / 30)} height={2} fill={
                      node.contention > 15 ? '#ff3060' : node.contention > 8 ? '#ffb300' : '#00ffc8'
                    } rx={1} />
                  )}
                </g>
              );
            })}
          </svg>

          {/* Node detail panel */}
          {selectedNode && getNodeById(selectedNode) && (
            <div className="mt-4 glass-panel rounded-lg p-4 max-w-md mx-auto">
              {(() => {
                const node = getNodeById(selectedNode)!;
                return (
                  <div className="grid grid-cols-2 gap-3 text-sm font-mono">
                    <div><span className="text-sov-400">Node:</span> <span className="text-white">{node.id}</span></div>
                    <div><span className="text-sov-400">Type:</span> <span style={{ color: typeColor(node.type) }}>{node.type}</span></div>
                    <div><span className="text-sov-400">Size:</span> <span className="text-white">{node.size}</span></div>
                    <div><span className="text-sov-400">Line:</span> <span className="text-cyan-glow">{node.line}</span></div>
                    {node.cores > 0 && <div><span className="text-sov-400">Cores:</span> <span className="text-white">{node.cores}</span></div>}
                    {node.contention > 0 && (
                      <div>
                        <span className="text-sov-400">Contention:</span>{' '}
                        <span className={node.contention > 15 ? 'text-red-glow' : node.contention > 8 ? 'text-amber-glow' : 'text-teal-glow'}>
                          {node.contention}%
                        </span>
                      </div>
                    )}
                  </div>
                );
              })()}
            </div>
          )}
        </div>

        {/* Detection log */}
        <div className="mt-6 glass-panel rounded-lg p-4 max-w-3xl mx-auto font-mono text-xs">
          <div className="flex items-center gap-2 mb-3">
            <div className="w-2 h-2 rounded-full bg-teal-glow animate-pulse" />
            <span className="text-cyan-glow/60 uppercase tracking-wider">Hardware Detection Log</span>
          </div>
          <div className="space-y-1">
            <div className="text-sov-400">
              <span className="text-cyan-glow/40">[00:00.000]</span> Initiating hardware topology discovery...
            </div>
            <div className={`${detectionPhase >= 1 ? 'text-teal-glow/80' : 'text-sov-500'}`}>
              <span className="text-cyan-glow/40">[00:00.127]</span> NUMA node count: 1 · Inter-node latency: &lt;1ns
            </div>
            <div className={`${detectionPhase >= 2 ? 'text-teal-glow/80' : 'text-sov-500'}`}>
              <span className="text-cyan-glow/40">[00:00.341]</span> L3 cache detected: {topologies[topoIdx][1].size} · Line width: {topologies[topoIdx][1].line}
            </div>
            <div className={`${detectionPhase >= 3 ? 'text-teal-glow/80' : 'text-sov-500'}`}>
              <span className="text-cyan-glow/40">[00:00.589]</span> L2/CCX stripe aligned: {topologies[topoIdx][2].size} × 2 · Auto-aligned (NO hardcoded 256B)
            </div>
            <div className={`${detectionPhase >= 4 ? 'text-teal-glow/80' : 'text-sov-500'}`}>
              <span className="text-cyan-glow/40">[00:00.823]</span> L1 data/instruction: {topologies[topoIdx][4].size} × 4 · Contention profile loaded
            </div>
            <div className={`${detectionPhase >= 4 ? 'text-teal-glow' : 'text-sov-500'} animate-pulse`}>
              <span className="text-cyan-glow/40">[00:00.912]</span> ✓ Topology locked · Hardware stripe auto-aligned · Zero hard-coded assumptions
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
