import { motion, AnimatePresence } from 'framer-motion';
import { Cpu, MemoryStick, Server, ArrowDownUp } from 'lucide-react';
import type { CacheTopology } from '../types';

interface TopologyDetectorProps {
  isActive: boolean;
}

export function TopologyDetector({ isActive }: TopologyDetectorProps) {
  const detectedTopology: CacheTopology = {
    l1LineSize: 64,
    l2LineSize: 64,
    l3LineSize: 64,
    numaNodes: [
      { id: 0, cores: 8, distance: [10, 21], localLatency: 0.42, remoteLatency: 0.67 },
      { id: 1, cores: 8, distance: [21, 10], localLatency: 0.42, remoteLatency: 0.67 },
    ],
    cpuTopology: {
      sockets: 2,
      coresPerSocket: 8,
      threadsPerCore: 2,
      totalCores: 16,
      tsoCapable: true,
    },
  };

  const steps = [
    { label: 'L1 Cache Probe', value: `${detectedTopology.l1LineSize}B`, icon: MemoryStick },
    { label: 'L2 Cache Probe', value: `${detectedTopology.l2LineSize}B`, icon: MemoryStick },
    { label: 'L3 Cache Probe', value: `${detectedTopology.l3LineSize}B`, icon: MemoryStick },
    { label: 'NUMA Nodes', value: `${detectedTopology.numaNodes.length}`, icon: Server },
    { label: 'Total Cores', value: `${detectedTopology.cpuTopology.totalCores}`, icon: Cpu },
    { label: 'TSO Support', value: detectedTopology.cpuTopology.tsoCapable ? 'YES' : 'NO', icon: ArrowDownUp },
  ];

  const activeStep = isActive ? steps.length : 0;

  return (
    <div className="p-6 md:p-8">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-lg bg-cyan-500/10 border border-cyan-500/20">
          <Cpu className="w-5 h-5 text-cyan-400" />
        </div>
        <div>
          <h3 className="text-lg font-semibold text-white">Hardware-Auto-Detect Topology</h3>
          <p className="text-xs text-gray-500 font-mono">SOV-V24-MANDATE-01</p>
        </div>
      </div>

      {/* Detection Steps */}
      <div className="space-y-3 mb-8">
        {steps.map((step, i) => (
          <motion.div
            key={step.label}
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: i <= activeStep ? 1 : 0.3, x: 0 }}
            transition={{ duration: 0.4, delay: i * 0.15 }}
            className="flex items-center gap-4 p-4 rounded-xl bg-white/[0.02] border border-white/[0.04]"
          >
            <div
              className={`p-2 rounded-lg transition-colors ${
                i < activeStep
                  ? 'bg-emerald-500/15 border border-emerald-500/30'
                  : i === activeStep
                  ? 'bg-cyan-500/15 border border-cyan-500/30 animate-pulse'
                  : 'bg-white/5 border border-white/10'
              }`}
            >
              <step.icon
                className={`w-4 h-4 ${
                  i < activeStep
                    ? 'text-emerald-400'
                    : i === activeStep
                    ? 'text-cyan-400'
                    : 'text-gray-600'
                }`}
              />
            </div>
            <div className="flex-1">
              <div className="text-sm text-gray-400">{step.label}</div>
              <div className="text-xs text-gray-600 font-mono">Auto-detecting hardware parameters...</div>
            </div>
            <AnimatePresence>
              {i <= activeStep && (
                <motion.div
                  initial={{ opacity: 0, scale: 0.8 }}
                  animate={{ opacity: 1, scale: 1 }}
                  className="font-mono text-sm font-bold"
                >
                  {i < activeStep ? (
                    <span className="text-emerald-400">{step.value}</span>
                  ) : (
                    <span className="text-cyan-400 animate-pulse">{step.value}</span>
                  )}
                </motion.div>
              )}
            </AnimatePresence>
          </motion.div>
        ))}
      </div>

      {/* NUMA Topology Map */}
      <div className="p-4 rounded-xl bg-white/[0.02] border border-white/[0.04]">
        <h4 className="text-sm font-mono text-gray-400 mb-4">NUMA Distance Matrix</h4>
        <div className="flex items-center justify-center gap-8">
          {detectedTopology.numaNodes.map((node) => (
            <div key={node.id} className="text-center">
              <div className="w-24 h-24 rounded-2xl bg-gradient-to-br from-cyan-500/10 to-blue-500/10 border border-cyan-500/20 flex flex-col items-center justify-center">
                <Server className="w-6 h-6 text-cyan-400 mb-1" />
                <span className="text-xs text-gray-500 font-mono">NODE {node.id}</span>
                <span className="text-lg font-bold text-white font-mono">{node.cores}c</span>
              </div>
            </div>
          ))}
          <div className="flex flex-col items-center gap-1">
            <ArrowDownUp className="w-5 h-5 text-gray-500 animate-pulse" />
            <span className="text-xs font-mono text-gray-600">d=21</span>
          </div>
        </div>
      </div>
    </div>
  );
}
