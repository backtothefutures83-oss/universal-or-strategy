import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Terminal, Shield, ArrowRight, Zap, Radio, Boxes } from 'lucide-react';
import { PipeVisualizer } from './components/PipeVisualizer';
import { CoreMap } from './components/CoreMap';
import { RingBufferVisualizer } from './components/RingBufferVisualizer';
import { LatencyMonitor } from './components/LatencyMonitor';
import { TechnicalSpecs } from './components/TechnicalSpecs';

function App() {
  const [booting, setBooting] = useState(true);

  useEffect(() => {
    const timer = setTimeout(() => setBooting(false), 2000);
    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="min-h-screen bg-[#050505] text-zinc-100 font-sans overflow-x-hidden selection:bg-cyan-500/30 selection:text-cyan-200 relative scanline">
      <AnimatePresence>
        {booting ? (
          <motion.div
            key="boot"
            initial={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 bg-black flex flex-col items-center justify-center gap-6"
          >
            <motion.div
              animate={{ rotate: 360 }}
              transition={{ duration: 2, repeat: Infinity, ease: "linear" }}
              className="w-12 h-12 border-2 border-cyan-500/20 border-t-cyan-500 rounded-full"
            />
            <div className="flex flex-col items-center gap-2">
              <span className="text-cyan-500 font-mono text-sm tracking-widest animate-pulse uppercase">BOOTING SOVEREIGN ACTOR v2</span>
              <span className="text-zinc-600 font-mono text-[10px] uppercase tracking-tighter">Antigravity Nexus OS | IPC Layer Init</span>
            </div>
          </motion.div>
        ) : (
          <motion.div
            key="dashboard"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="p-6 md:p-8 max-w-7xl mx-auto"
          >
            {/* Header */}
            <header className="flex flex-col md:flex-row md:items-center justify-between gap-6 mb-12 border-b border-zinc-900 pb-8">
              <div className="flex flex-col gap-2">
                <div className="flex items-center gap-3">
                  <div className="bg-cyan-500/10 p-2 rounded-lg border border-cyan-500/20 shadow-[0_0_15px_rgba(6,182,212,0.15)]">
                    <Boxes className="w-6 h-6 text-cyan-500" />
                  </div>
                  <h1 className="text-3xl font-bold tracking-tighter text-zinc-100 uppercase">
                    Antigravity <span className="text-cyan-500">Nexus</span> OS
                  </h1>
                </div>
                <div className="flex items-center gap-4 text-zinc-500 font-mono text-[10px] uppercase tracking-widest md:ml-12">
                  <span className="flex items-center gap-1.5"><Radio className="w-3 h-3 text-emerald-500" /> Real-time Kernel Status</span>
                  <span className="flex items-center gap-1.5"><Shield className="w-3 h-3 text-cyan-500" /> Sovereign Actor v2.0.4</span>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                <div className="flex flex-col items-end gap-1">
                  <span className="text-[10px] font-mono text-zinc-600 uppercase">System Uptime</span>
                  <span className="text-sm font-mono text-zinc-300">00:14:52:84</span>
                </div>
                <div className="h-10 w-[1px] bg-zinc-800 mx-2" />
                <button className="bg-zinc-100 text-black px-6 py-2.5 rounded-lg font-mono text-xs font-bold uppercase tracking-widest hover:bg-cyan-400 transition-colors flex items-center gap-2 group">
                  Deploy Pipe Layer
                  <ArrowRight className="w-3 h-3 group-hover:translate-x-1 transition-transform" />
                </button>
              </div>
            </header>

            {/* Main Grid */}
            <main className="grid grid-cols-1 lg:grid-cols-12 gap-6">
              {/* Left Column - Pipe Visualization & Latency */}
              <div className="lg:col-span-8 space-y-6">
                <div className="bg-zinc-950/80 border border-zinc-800 p-8 rounded-2xl relative overflow-hidden group">
                  <div className="absolute top-0 right-0 p-4">
                    <Zap className="w-6 h-6 text-cyan-500 animate-pulse opacity-50 shadow-[0_0_15px_rgba(6,182,212,0.5)]" />
                  </div>
                  <div className="relative z-10">
                    <h2 className="text-2xl font-bold tracking-tight text-white mb-2 uppercase">The Perfect Open Pipe</h2>
                    <p className="text-zinc-400 font-mono text-xs max-w-2xl mb-8 leading-relaxed">
                      Sovereign Actor v2 eliminates standard serialization. Data flows from Engine to Engine via physical-wire purity. 
                      No managers. No buffers. No blockages. Just the hardware physics.
                    </p>
                    <PipeVisualizer />
                  </div>
                  
                  {/* Background Glow */}
                  <div className="absolute -bottom-20 -right-20 w-96 h-96 bg-cyan-500/5 blur-[120px] rounded-full pointer-events-none" />
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <LatencyMonitor />
                  <RingBufferVisualizer />
                </div>
              </div>

              {/* Right Column - Specs & Core Map */}
              <div className="lg:col-span-4 space-y-6">
                <TechnicalSpecs />
                <CoreMap />
              </div>

              {/* Bottom Console */}
              <div className="lg:col-span-12">
                <div className="bg-black/80 border border-zinc-800 rounded-xl overflow-hidden shadow-2xl">
                  <div className="flex items-center gap-2 px-4 py-3 border-b border-zinc-900 bg-zinc-950">
                    <Terminal className="w-4 h-4 text-zinc-500" />
                    <span className="text-[10px] font-mono text-zinc-500 uppercase tracking-widest">Nexus Kernel Debug Console</span>
                    <div className="ml-auto flex gap-1.5">
                      <div className="w-2 h-2 rounded-full bg-zinc-800" />
                      <div className="w-2 h-2 rounded-full bg-zinc-800" />
                      <div className="w-2 h-2 rounded-full bg-zinc-800" />
                    </div>
                  </div>
                  <div className="p-4 font-mono text-[11px] h-48 overflow-y-auto space-y-1 custom-scrollbar">
                    <p className="text-emerald-500">[SYSTEM] Booting Sovereign Actor v2.0.4-STABLE...</p>
                    <p className="text-zinc-500">[KERNEL] CPU Isolation detected (cores: 0-11). Core Affinity set.</p>
                    <p className="text-zinc-500">[MEMORY] mlockall() successful. Memory pinned to L1/L2 cache lines.</p>
                    <p className="text-zinc-500">[IPC] Initializing 12x SPSC Ring Buffers over SharedArrayBuffer.</p>
                    <p className="text-zinc-500">[NET] Bypassing networking stack. Direct memory access (DMA) enabled.</p>
                    <p className="text-cyan-400 font-bold">[READY] PIPE LAYER ACTIVE: Latency Hard-Gate verified at 2.4µs.</p>
                    <p className="text-zinc-600 animate-pulse">_ Waiting for Actor Engine sync...</p>
                  </div>
                </div>
              </div>
            </main>

            {/* Footer */}
            <footer className="mt-12 pt-8 border-t border-zinc-900 flex flex-col md:flex-row justify-between items-center gap-4 pb-12">
              <span className="text-[10px] font-mono text-zinc-600 uppercase tracking-[0.2em]">Designed for Sovereign Autonomy | © 2026 Antigravity Nexus</span>
              <div className="flex gap-6">
                <a href="#" className="text-[10px] font-mono text-zinc-500 hover:text-cyan-400 transition-colors uppercase">Documentation</a>
                <a href="#" className="text-[10px] font-mono text-zinc-500 hover:text-cyan-400 transition-colors uppercase">Security Audits</a>
                <a href="#" className="text-[10px] font-mono text-zinc-500 hover:text-cyan-400 transition-colors uppercase">Hardware Schema</a>
              </div>
            </footer>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default App;