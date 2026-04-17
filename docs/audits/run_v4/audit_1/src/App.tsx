import React, { useState, useEffect } from 'react';
import { 
  Zap, 
  Cpu, 
  ArrowRightLeft, 
  Layers, 
  ShieldCheck, 
  Activity,
  Database
} from 'lucide-react';
import { motion } from 'framer-motion';
import { SovereignMesh } from './components/SovereignMesh';
import { SlabPool } from './components/SlabPool';
import { LatencyMonitor } from './components/LatencyMonitor';
import { MeshMode, Role } from './types/mesh';

const ROLES: Role[] = ['Ingress', 'Router', 'Transform', 'Actor', 'Egress', 'Mirror'];

const App: React.FC = () => {
  const [mode, setMode] = useState<MeshMode>('12-CORE');
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setTick(t => t + 1);
    }, 100);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 font-mono overflow-hidden selection:bg-cyan-500/30">
      {/* Background Grid */}
      <div className="fixed inset-0 bg-[linear-gradient(to_right,#1e293b_1px,transparent_1px),linear-gradient(to_bottom,#1e293b_1px,transparent_1px)] bg-[size:40px_40px] [mask-image:radial-gradient(ellipse_60%_50%_at_50%_0%,#000_70%,transparent_100%)] pointer-events-none opacity-20" />

      {/* Top Header */}
      <header className="relative z-10 border-b border-cyan-500/20 bg-slate-950/50 backdrop-blur-md px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-cyan-500 rounded flex items-center justify-center shadow-[0_0_20px_rgba(6,182,212,0.4)]">
            <Zap className="text-slate-950 fill-current" />
          </div>
          <div>
            <h1 className="text-xl font-bold tracking-tighter uppercase">Nexus OS</h1>
            <p className="text-[10px] text-cyan-400/70 tracking-widest uppercase">Adaptive Sovereign Mesh v4.0</p>
          </div>
        </div>

        <div className="flex items-center gap-6">
          <div className="flex bg-slate-900 border border-slate-800 rounded-lg p-1">
            {(['12-CORE', '6-CORE', '4-CORE'] as MeshMode[]).map((m) => (
              <button
                key={m}
                onClick={() => setMode(m)}
                className={`px-4 py-1.5 rounded text-xs font-bold transition-all ${
                  mode === m 
                    ? 'bg-cyan-500 text-slate-950 shadow-[0_0_15px_rgba(6,182,212,0.3)]' 
                    : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                {m}
              </button>
            ))}
          </div>
          <div className="flex items-center gap-2 text-xs text-cyan-400 font-bold">
            <div className="w-2 h-2 rounded-full bg-cyan-500 animate-pulse" />
            L1 CACHE SYNC: OPTIMAL
          </div>
        </div>
      </header>

      <main className="relative z-10 p-6 grid grid-cols-12 gap-6 max-w-[1600px] mx-auto h-[calc(100vh-80px)]">
        {/* Left Column - System Diagnostics */}
        <div className="col-span-3 space-y-6">
          <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-5 backdrop-blur-sm">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-sm font-bold text-slate-400 uppercase tracking-widest flex items-center gap-2">
                <Cpu size={16} className="text-cyan-500" />
                Hardware Sharding
              </h2>
              <span className="text-[10px] bg-cyan-500/10 text-cyan-400 px-2 py-0.5 rounded border border-cyan-500/20">
                LOCKED
              </span>
            </div>
            
            <div className="space-y-3">
              <div className="flex justify-between text-xs">
                <span className="text-slate-500">Active Threads</span>
                <span className="text-slate-200 font-bold">{mode === '12-CORE' ? '12/12' : mode === '6-CORE' ? '6/12' : '4/12'}</span>
              </div>
              <div className="h-1.5 bg-slate-800 rounded-full overflow-hidden">
                <motion.div 
                  initial={false}
                  animate={{ width: mode === '12-CORE' ? '100%' : mode === '6-CORE' ? '50%' : '33%' }}
                  className="h-full bg-cyan-500"
                />
              </div>
              
              <div className="grid grid-cols-2 gap-2 pt-2">
                <div className="bg-slate-950 p-2 rounded border border-slate-800">
                  <p className="text-[10px] text-slate-500 uppercase">Affinity</p>
                  <p className="text-xs font-bold text-emerald-400">HARD-PINNED</p>
                </div>
                <div className="bg-slate-950 p-2 rounded border border-slate-800">
                  <p className="text-[10px] text-slate-500 uppercase">Isolation</p>
                  <p className="text-xs font-bold text-cyan-400">SYMMETRIC</p>
                </div>
              </div>
            </div>
          </div>

          <LatencyMonitor mode={mode} />
          
          <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-5 backdrop-blur-sm">
            <h2 className="text-sm font-bold text-slate-400 uppercase tracking-widest flex items-center gap-2 mb-4">
              <ShieldCheck size={16} className="text-cyan-500" />
              SPSC Invariants
            </h2>
            <ul className="space-y-2">
              {[
                { label: 'Single-Producer', status: 'VERIFIED' },
                { label: 'Zero-Copy Shared', status: 'MAPPED' },
                { label: 'No-Heap Policy', status: 'ENFORCED' },
                { label: 'Jitter-Guard', status: 'ACTIVE' },
              ].map((item, i) => (
                <li key={i} className="flex items-center justify-between text-xs p-2 bg-slate-950/50 rounded">
                  <span className="text-slate-400">{item.label}</span>
                  <span className="text-emerald-400 font-bold">{item.status}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Center Column - Mesh Visualization */}
        <div className="col-span-6 flex flex-col gap-6">
          <SovereignMesh mode={mode} tick={tick} />
          
          <div className="flex-1 bg-slate-900/30 border border-slate-800 rounded-xl p-4 relative overflow-hidden">
            <div className="absolute top-4 left-4 flex items-center gap-2">
              <ArrowRightLeft size={16} className="text-cyan-500" />
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-widest">Pipe Logic: Pure SPSC Wire</h3>
            </div>
            <div className="h-full flex items-center justify-center">
              <div className="w-full max-w-lg space-y-8">
                <div className="flex items-center justify-between relative">
                  <div className="w-16 h-16 rounded-lg bg-cyan-500/10 border border-cyan-500/50 flex flex-col items-center justify-center relative z-10">
                    <Database size={20} className="text-cyan-400 mb-1" />
                    <span className="text-[8px] font-bold">WRITER</span>
                  </div>
                  <div className="flex-1 h-0.5 bg-gradient-to-r from-cyan-500/50 via-cyan-400/20 to-cyan-500/50 relative">
                    <motion.div 
                      animate={{ x: ['0%', '400%'] }}
                      transition={{ duration: 1.5, repeat: Infinity, ease: "linear" }}
                      className="absolute top-1/2 -translate-y-1/2 w-4 h-4 bg-cyan-400 rounded-full blur-sm"
                    />
                    <motion.div 
                      animate={{ x: ['0%', '400%'] }}
                      transition={{ duration: 1.5, repeat: Infinity, ease: "linear", delay: 0.5 }}
                      className="absolute top-1/2 -translate-y-1/2 w-3 h-3 bg-white rounded-full blur-[2px]"
                    />
                  </div>
                  <div className="w-16 h-16 rounded-lg bg-emerald-500/10 border border-emerald-500/50 flex flex-col items-center justify-center relative z-10">
                    <Activity size={20} className="text-emerald-400 mb-1" />
                    <span className="text-[8px] font-bold">READER</span>
                  </div>
                </div>
                <div className="grid grid-cols-3 gap-4 text-center">
                  <div>
                    <p className="text-[10px] text-slate-500 uppercase">Write-Line</p>
                    <p className="text-xs font-bold text-slate-300">Atomic Cursor</p>
                  </div>
                  <div>
                    <p className="text-[10px] text-slate-500 uppercase">Barrier</p>
                    <p className="text-xs font-bold text-slate-300">SFENCE / LFENCE</p>
                  </div>
                  <div>
                    <p className="text-[10px] text-slate-500 uppercase">Latency</p>
                    <p className="text-xs font-bold text-emerald-400">0.02 µs</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Right Column - Slab Pool & Actor State */}
        <div className="col-span-3 space-y-6">
          <SlabPool />
          
          <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-5 backdrop-blur-sm flex-1 overflow-auto">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-sm font-bold text-slate-400 uppercase tracking-widest flex items-center gap-2">
                <Layers size={16} className="text-cyan-500" />
                Node Topology
              </h2>
            </div>
            
            <div className="space-y-4">
              {ROLES.map((role, i) => (
                <div key={role} className="flex items-center gap-3">
                  <div className={`w-2 h-2 rounded-full ${i < 2 ? 'bg-orange-500' : i < 4 ? 'bg-cyan-500' : 'bg-emerald-500'}`} />
                  <div className="flex-1">
                    <div className="flex justify-between items-baseline">
                      <span className="text-xs font-bold text-slate-300">{role}</span>
                      <span className="text-[10px] text-slate-500">Node {i+1}</span>
                    </div>
                    <div className="flex gap-1 mt-1">
                      {Array.from({ length: mode === '12-CORE' ? 2 : 1 }).map((_, j) => (
                        <div key={j} className="h-1 flex-1 bg-slate-800 rounded-full">
                          <motion.div 
                            animate={{ opacity: [0.3, 1, 0.3] }}
                            transition={{ duration: 2, repeat: Infinity, delay: i * 0.2 + j * 0.5 }}
                            className="h-full bg-cyan-500/40 rounded-full"
                          />
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </main>

      {/* Status Bar */}
      <footer className="fixed bottom-0 left-0 right-0 h-8 bg-slate-900 border-t border-slate-800 flex items-center px-6 justify-between text-[10px] tracking-[0.2em] font-bold text-slate-500 uppercase">
        <div className="flex gap-6">
          <span className="flex items-center gap-2"><div className="w-1.5 h-1.5 rounded-full bg-emerald-500" /> MESH_SYNC: OK</span>
          <span className="flex items-center gap-2"><div className="w-1.5 h-1.5 rounded-full bg-emerald-500" /> SHM_ALLOC: STATIC</span>
          <span className="flex items-center gap-2"><div className="w-1.5 h-1.5 rounded-full bg-cyan-500" /> TOPOLOGY: {mode}</span>
        </div>
        <div>
          ANTIGRAVITY NEXUS OS // KERNEL_TIME: {Math.floor(Date.now() / 1000)}
        </div>
      </footer>
    </div>
  );
};

export default App;