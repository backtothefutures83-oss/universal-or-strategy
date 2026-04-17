import { useState, useEffect } from 'react';
import { Network, Server, Cpu, Database } from 'lucide-react';

export const TopologyMap: React.FC = () => {
  const [activeNode, setActiveNode] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setActiveNode(Math.floor(Math.random() * 4));
    }, 2000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="bg-black/50 border border-emerald-900/50 rounded-lg p-4 h-full flex flex-col relative overflow-hidden backdrop-blur-sm shadow-[0_0_15px_rgba(16,185,129,0.1)]">
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-emerald-400 font-bold uppercase text-xs tracking-widest flex items-center">
          <Network size={16} className="mr-2" />
          Hardware Topology Auto-Detect
        </h3>
        <div className="text-xs text-cyan-400 font-mono bg-cyan-950/30 px-2 py-1 rounded border border-cyan-900/50">
          STRIPE: ADAPTIVE
        </div>
      </div>

      <div className="flex-1 grid grid-cols-2 gap-4 relative">
        {/* Connection lines background */}
        <div className="absolute inset-0 z-0 flex items-center justify-center opacity-20 pointer-events-none">
          <div className="w-full h-full border-2 border-emerald-500 rounded-full border-dashed animate-spin-slow"></div>
          <div className="absolute w-3/4 h-3/4 border border-cyan-500 rounded-full rotate-45"></div>
        </div>

        {[0, 1, 2, 3].map((node) => (
          <div 
            key={node}
            className={`
              relative z-10 p-3 rounded border flex flex-col items-center justify-center transition-all duration-500
              ${activeNode === node 
                ? 'bg-emerald-900/40 border-emerald-400 shadow-[0_0_15px_rgba(16,185,129,0.4)]' 
                : 'bg-zinc-950/80 border-emerald-900/50 hover:border-emerald-700/50'}
            `}
          >
            <Server size={24} className={activeNode === node ? 'text-emerald-400' : 'text-emerald-700'} />
            <span className="text-[10px] font-mono mt-1 text-gray-400">NODE_{node}</span>
            <div className="mt-2 grid grid-cols-2 gap-1 w-full px-2">
              <div className="bg-zinc-900 p-1 rounded text-[8px] text-center border border-zinc-800">
                <Cpu size={10} className="mx-auto mb-1 text-cyan-500" />
                L1: 64B
              </div>
              <div className="bg-zinc-900 p-1 rounded text-[8px] text-center border border-zinc-800">
                <Database size={10} className="mx-auto mb-1 text-purple-500" />
                L2: 128B
              </div>
            </div>
            
            {activeNode === node && (
              <div className="absolute -top-1 -right-1 w-2 h-2 bg-emerald-400 rounded-full animate-ping"></div>
            )}
          </div>
        ))}
      </div>

      <div className="mt-4 border-t border-emerald-900/50 pt-2 text-[10px] text-gray-500 flex justify-between font-mono">
        <span>NUMA Dist: &lt; 20ns</span>
        <span>Cross-Socket: OK</span>
        <span>L3 Shared: 256B</span>
      </div>
    </div>
  );
};
