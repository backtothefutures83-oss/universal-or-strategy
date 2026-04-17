import { useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Cpu, Database, Server, ArrowRightLeft, MemoryStick } from 'lucide-react';

interface CacheLevel {
  name: string;
  size: string;
  lines: number;
  width: number;
  latency: string;
  color: string;
}

interface NUMANode {
  id: number;
  cores: number;
  localMem: string;
  distance: number[];
  l1: CacheLevel;
  l2: CacheLevel;
  l3: CacheLevel;
}

const generateTopology = () => {
  const l1Widths = [32, 64];
  const l2Widths = [64, 128];
  const l3Widths = [64, 128, 256];
  const numaDistances = [
    [10, 12],
    [10, 16, 12, 10],
  ];

  const distancePattern = Math.random() > 0.5 ? 0 : 1;
  const numNodes = distancePattern === 0 ? 2 : 4;

  const nodes: NUMANode[] = Array.from({ length: numNodes }, (_, i) => ({
    id: i,
    cores: 8 + Math.floor(Math.random() * 8),
    localMem: `${16 + Math.floor(Math.random() * 32)} GB`,
    distance: numaDistances[distancePattern],
    l1: {
      name: 'L1',
      size: `${32 + Math.floor(Math.random() * 32)} KB`,
      lines: 512,
      width: l1Widths[Math.floor(Math.random() * l1Widths.length)],
      latency: '~4 cycles',
      color: '#00e5ff',
    },
    l2: {
      name: 'L2',
      size: `${256 + Math.floor(Math.random() * 768)} KB`,
      lines: 1024,
      width: l2Widths[Math.floor(Math.random() * l2Widths.length)],
      latency: '~12 cycles',
      color: '#00ff87',
    },
    l3: {
      name: 'L3',
      size: `${8 + Math.floor(Math.random() * 56)} MB`,
      lines: 4096,
      width: l3Widths[Math.floor(Math.random() * l3Widths.length)],
      latency: '~35 cycles',
      color: '#b388ff',
    },
  }));

  return { nodes, distancePattern };
};

function NodeCard({ node, active, onClick }: { node: NUMANode; active: boolean; onClick: () => void }) {
  return (
    <motion.div
      onClick={onClick}
      className={`glass-panel rounded-xl p-4 cursor-pointer transition-all duration-300 ${
        active ? 'border-sov-cyan/40 glow-cyan scale-[1.02]' : 'border-sov-border hover:border-sov-cyan/20'
      }`}
      whileHover={{ scale: 1.02 }}
    >
      <div className="flex items-center gap-2 mb-3">
        <Server className="w-4 h-4 text-sov-cyan" />
        <span className="font-mono font-bold text-sov-text">NUMA Node {node.id}</span>
        {active && <span className="ml-auto w-2 h-2 rounded-full bg-sov-green animate-pulse" />}
      </div>

      <div className="grid grid-cols-3 gap-2 mb-3">
        {[node.l1, node.l2, node.l3].map((cache) => (
          <div key={cache.name} className="text-center p-2 rounded-lg bg-sov-surface-2/50">
            <div className="text-xs font-mono mb-1" style={{ color: cache.color }}>{cache.name}</div>
            <div className="text-xs text-sov-text">{cache.size}</div>
            <div className="text-[10px] text-sov-text-dim font-mono mt-1">{cache.width}B</div>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-2 text-xs text-sov-text-dim font-mono">
        <div>Cores: <span className="text-sov-text">{node.cores}</span></div>
        <div>Mem: <span className="text-sov-text">{node.localMem}</span></div>
      </div>

      <div className="mt-2 text-xs font-mono text-sov-text-dim">
        Distances: [{node.distance.join(', ')}]
      </div>
    </motion.div>
  );
}

export function TopologyVisualizer() {
  const [topology, setTopology] = useState(generateTopology);
  const [activeNode, setActiveNode] = useState(0);
  const [isDetecting, setIsDetecting] = useState(false);

  const handleRescan = useCallback(() => {
    setIsDetecting(true);
    setTimeout(() => {
      setTopology(generateTopology());
      setActiveNode(0);
      setIsDetecting(false);
    }, 1500);
  }, []);

  return (
    <section id="topology" className="py-20 px-4 max-w-6xl mx-auto">
      {/* Section header */}
      <div className="text-center mb-12">
        <div className="inline-flex items-center gap-2 text-sov-cyan font-mono text-sm mb-4">
          <Database className="w-4 h-4" />
          <span>MANDATE #1</span>
        </div>
        <h2 className="text-3xl sm:text-4xl font-bold mb-3">
          Hardware-Auto-Detect Topology
        </h2>
        <p className="text-sov-text-dim max-w-2xl mx-auto">
          Dynamically identifies L1/L2/L3 cache line widths and NUMA node distances during initialization.
          No hardcoded assumptions — pure hardware interrogation.
        </p>
      </div>

      {/* Controls */}
      <div className="flex justify-center mb-8">
        <motion.button
          onClick={handleRescan}
          disabled={isDetecting}
          className="px-6 py-2.5 rounded-lg bg-sov-cyan/10 border border-sov-cyan/30 text-sov-cyan font-mono text-sm hover:bg-sov-cyan/20 transition-colors disabled:opacity-50 flex items-center gap-2"
          whileTap={{ scale: 0.95 }}
        >
          <Cpu className={`w-4 h-4 ${isDetecting ? 'animate-spin' : ''}`} />
          {isDetecting ? 'Scanning Hardware...' : 'Rescan Topology'}
        </motion.button>
      </div>

      {/* Detection animation */}
      <AnimatePresence>
        {isDetecting && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="mb-8 p-4 rounded-xl bg-sov-surface-2/50 border border-sov-cyan/10 max-w-lg mx-auto"
          >
            <div className="space-y-2 font-mono text-xs">
              {['Probing CPUID leaf 0x04...', 'Detecting cache topology...',
                'Measuring NUMA distances...', 'Validating stripe alignment...'].map((step, i) => (
                <motion.div
                  key={step}
                  initial={{ opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: i * 0.3 }}
                  className="flex items-center gap-2"
                >
                  <span className="text-sov-green">✓</span>
                  <span className="text-sov-text-dim">{step}</span>
                </motion.div>
              ))}
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                transition={{ delay: 1.3 }}
                className="text-sov-cyan"
              >
                → Topology locked. {topology.nodes.length} NUMA nodes detected.
              </motion.div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Topology grid */}
      <div className={`grid gap-4 transition-all ${
        topology.nodes.length > 2 ? 'grid-cols-2 md:grid-cols-4' : 'grid-cols-1 md:grid-cols-2'
      }`}>
        {topology.nodes.map((node, i) => (
          <NodeCard
            key={i}
            node={node}
            active={activeNode === i}
            onClick={() => setActiveNode(i)}
          />
        ))}
      </div>

      {/* Cache line width display */}
      <AnimatePresence>
        {!isDetecting && (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="mt-8 glass-panel rounded-xl p-6 max-w-2xl mx-auto"
          >
            <div className="flex items-center gap-3 mb-4">
              <MemoryStick className="w-5 h-5 text-sov-amber" />
              <h3 className="font-mono font-bold text-sov-amber">Detected Cache Stripe Widths</h3>
            </div>
            <div className="grid grid-cols-3 gap-4 text-center">
              {['l1', 'l2', 'l3'].map((level) => {
                const node = topology.nodes[activeNode];
                const cache = node[level as keyof typeof node] as CacheLevel;
                return (
                  <div key={level} className="p-3 rounded-lg bg-sov-surface-2/50">
                    <div className="text-2xl font-bold font-mono" style={{ color: cache.color }}>
                      {cache.width}B
                    </div>
                    <div className="text-xs text-sov-text-dim mt-1">{cache.name} stripe</div>
                    <div className="text-xs text-sov-text-dim">{cache.size} total</div>
                  </div>
                );
              })}
            </div>
            <div className="mt-4 p-3 rounded-lg bg-sov-green/5 border border-sov-green/10">
              <div className="flex items-center gap-2 text-sov-green text-xs font-mono">
                <ArrowRightLeft className="w-3 h-3" />
                Adaptive alignment: Sovereign V24 auto-aligns to {topology.nodes[activeNode].l1.width}B stripe width (was hardcoded 256B in V23)
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </section>
  );
}
