import { Shield, Zap, Workflow, Server } from 'lucide-react';
import { motion } from 'framer-motion';

export const Overview = () => {
  return (
    <div className="space-y-12">
      <header>
        <motion.div 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="inline-flex items-center px-3 py-1 rounded-full bg-blood-900/30 text-blood-500 border border-blood-500/20 text-xs font-mono font-bold tracking-widest mb-4"
        >
          ARENA AI DESIGN CHALLENGE v5
        </motion.div>
        <motion.h1 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="text-4xl lg:text-5xl font-bold tracking-tight text-white"
        >
          The Platinum Battle
        </motion.h1>
        <motion.p 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="mt-4 text-xl text-slate-400 max-w-3xl font-light"
        >
          Compounding the intelligence of the Adaptive Sovereign Mesh (v4). Moving the platform from "Fast" to <span className="text-blood-500 font-semibold drop-shadow-[0_0_8px_rgba(255,42,42,0.8)]">"Infinite & Immortal."</span>
        </motion.p>
      </header>

      <motion.div 
        initial={{ opacity: 0, y: 30 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.3 }}
        className="grid md:grid-cols-2 gap-6"
      >
        <div className="bg-slate-900/50 border border-slate-800 p-8 rounded-2xl relative overflow-hidden group hover:border-slate-700 transition-colors">
          <div className="absolute top-0 right-0 w-32 h-32 bg-blue-500/5 blur-[100px] group-hover:bg-blue-500/10 transition-colors" />
          <Server className="w-8 h-8 text-blue-400 mb-6" />
          <h3 className="text-xl font-semibold text-white mb-2">Current State: 100/100 Perfection</h3>
          <ul className="space-y-3 mt-6">
            {['1-to-1 SPSC Pipes', 'Node Compression (12/6/4 cores)', 'Zero-Heap SlabPools', 'Adaptive Sovereign Mesh (v4)'].map((item, i) => (
              <li key={i} className="flex items-center text-slate-300 font-mono text-sm">
                <span className="w-1.5 h-1.5 rounded-full bg-blue-500 mr-3" />
                {item}
              </li>
            ))}
          </ul>
        </div>

        <div className="bg-slate-900/50 border border-blood-900/30 p-8 rounded-2xl relative overflow-hidden group hover:border-blood-500/30 transition-colors">
          <div className="absolute top-0 right-0 w-32 h-32 bg-blood-500/5 blur-[100px] group-hover:bg-blood-500/10 transition-colors" />
          <Zap className="w-8 h-8 text-blood-500 mb-6" />
          <h3 className="text-xl font-semibold text-white mb-2">The "Billionaire's Tax" Challenge</h3>
          <p className="text-slate-400 text-sm mb-6">Optimize the IPC transport layer to achieve a 1µs Constant Gate (excluding I/O wait) while addressing three "Sovereign" engineering hurdles.</p>
          <div className="space-y-4">
            <div className="flex items-center gap-3 text-sm font-mono text-blood-400 p-3 rounded bg-blood-900/10 border border-blood-900/20">
              <Workflow className="w-4 h-4" /> Multimodal Pipes (L1-Sideband)
            </div>
            <div className="flex items-center gap-3 text-sm font-mono text-blood-400 p-3 rounded bg-blood-900/10 border border-blood-900/20">
              <Shield className="w-4 h-4" /> Jitter-Free Redundancy
            </div>
            <div className="flex items-center gap-3 text-sm font-mono text-blood-400 p-3 rounded bg-blood-900/10 border border-blood-900/20">
              <Zap className="w-4 h-4" /> The Atomic Constant
            </div>
          </div>
        </div>
      </motion.div>

      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 0.6 }}
        className="mt-12 bg-slate-900 p-6 rounded-xl border border-slate-800 text-sm font-mono"
      >
        <h4 className="text-blood-500 font-bold mb-4 uppercase flex items-center gap-2">
          <Shield className="w-4 h-4" /> Restricted Patterns (BANNED)
        </h4>
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-slate-950 p-4 rounded border border-slate-800/50 flex flex-col items-center text-center">
            <span className="text-slate-500 line-through decoration-blood-500 decoration-2 mb-2">Worker Factories</span>
            <span className="text-green-400 text-xs">Station Static Instances</span>
          </div>
          <div className="bg-slate-950 p-4 rounded border border-slate-800/50 flex flex-col items-center text-center">
            <span className="text-slate-500 line-through decoration-blood-500 decoration-2 mb-2">Mutexes</span>
            <span className="text-green-400 text-xs">Exclusive Thread Ownership</span>
          </div>
          <div className="bg-slate-950 p-4 rounded border border-slate-800/50 flex flex-col items-center text-center">
            <span className="text-slate-500 line-through decoration-blood-500 decoration-2 mb-2">Copying</span>
            <span className="text-green-400 text-xs">Mmapped Pointer Passing</span>
          </div>
          <div className="bg-slate-950 p-4 rounded border border-slate-800/50 flex flex-col items-center text-center">
            <span className="text-slate-500 line-through decoration-blood-500 decoration-2 mb-2">'Ultrathink' Fluff</span>
            <span className="text-green-400 text-xs">Physics-Grade logic only</span>
          </div>
        </div>
      </motion.div>
    </div>
  );
};