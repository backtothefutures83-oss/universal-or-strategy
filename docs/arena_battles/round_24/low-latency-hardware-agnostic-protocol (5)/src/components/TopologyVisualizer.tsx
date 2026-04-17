import React from 'react';
import { motion } from 'framer-motion';
import { Cpu, Zap, Network } from 'lucide-react';

export const TopologyVisualizer: React.FC = () => {
  return (
    <div className="bg-slate-900 border border-slate-700 rounded-xl p-6 text-slate-300 shadow-xl overflow-hidden relative">
      <h3 className="text-xl font-mono text-cyan-400 mb-6 flex items-center gap-2">
        <Network size={20} />
        Auto-Detect Topology & Adaptive Striping
      </h3>
      
      <div className="flex flex-col md:flex-row gap-8 items-center justify-center min-h-[300px]">
        {/* NUMA Node 0 */}
        <div className="bg-slate-800 p-4 rounded-lg border border-slate-600 w-64 relative">
          <div className="absolute -top-3 left-4 bg-slate-900 px-2 text-xs font-mono text-slate-400">NUMA NODE 0</div>
          
          <div className="flex items-center gap-3 mb-4 text-cyan-300">
            <Cpu size={24} />
            <span className="font-mono text-sm">Socket A - 16 Cores</span>
          </div>
          
          <div className="space-y-3 relative z-10">
            <div className="bg-slate-700 p-2 rounded flex justify-between items-center text-xs font-mono">
              <span>L1 Cache</span>
              <span className="text-green-400">32KB (Detected: 64B)</span>
            </div>
            <div className="bg-slate-700 p-2 rounded flex justify-between items-center text-xs font-mono">
              <span>L2 Cache</span>
              <span className="text-green-400">1MB (Detected: 64B)</span>
            </div>
            <div className="bg-slate-700 p-2 rounded flex justify-between items-center text-xs font-mono">
              <span>L3 Cache</span>
              <span className="text-green-400">32MB (Shared)</span>
            </div>
          </div>
        </div>

        {/* QPI / Cross-Socket Interconnect */}
        <div className="flex-1 flex flex-col items-center justify-center relative min-w-[150px]">
          <div className="text-xs font-mono text-cyan-500 mb-2">ZERO-FRICTION PIPELINE</div>
          <div className="h-1 w-full bg-slate-700 relative overflow-hidden rounded">
            <motion.div 
              className="absolute top-0 bottom-0 left-0 w-20 bg-gradient-to-r from-transparent via-cyan-400 to-transparent"
              animate={{ left: ['-20%', '120%'] }}
              transition={{ repeat: Infinity, duration: 0.87, ease: "linear" }}
            />
          </div>
          <div className="flex items-center gap-2 mt-2 text-xs font-mono text-green-400">
            <Zap size={14} />
            <span>0.48ns latency</span>
          </div>
        </div>

        {/* NUMA Node 1 */}
        <div className="bg-slate-800 p-4 rounded-lg border border-slate-600 w-64 relative">
          <div className="absolute -top-3 left-4 bg-slate-900 px-2 text-xs font-mono text-slate-400">NUMA NODE 1</div>
          
          <div className="flex items-center gap-3 mb-4 text-purple-300">
            <Cpu size={24} />
            <span className="font-mono text-sm">Socket B - 16 Cores</span>
          </div>
          
          <div className="space-y-3 relative z-10">
            <div className="bg-slate-700 p-2 rounded flex justify-between items-center text-xs font-mono">
              <span>L1 Cache</span>
              <span className="text-green-400">32KB (Detected: 64B)</span>
            </div>
            <div className="bg-slate-700 p-2 rounded flex justify-between items-center text-xs font-mono">
              <span>L2 Cache</span>
              <span className="text-green-400">1MB (Detected: 64B)</span>
            </div>
            <div className="bg-slate-700 p-2 rounded flex justify-between items-center text-xs font-mono">
              <span>L3 Cache</span>
              <span className="text-green-400">32MB (Shared)</span>
            </div>
          </div>
        </div>
      </div>

      <div className="mt-6 flex justify-between items-center text-xs font-mono bg-slate-950 p-3 rounded border border-slate-800">
        <div className="flex gap-4">
          <span className="text-slate-400">Status: <span className="text-green-400">HARDWARE_ALIGNED</span></span>
          <span className="text-slate-400">Mode: <span className="text-cyan-400">L2_STRIPED (Adaptive)</span></span>
        </div>
        <span className="text-slate-500">Auto-Detecting NUMA distances...</span>
      </div>
    </div>
  );
};
