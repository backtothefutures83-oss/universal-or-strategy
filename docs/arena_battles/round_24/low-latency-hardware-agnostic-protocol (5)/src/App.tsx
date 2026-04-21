import { TopologyVisualizer } from './components/TopologyVisualizer';
import { CodeViewer } from './components/CodeViewer';
import { Explanation } from './components/Explanation';
import { Zap, ShieldCheck, Activity } from 'lucide-react';

export default function App() {
  return (
    <div className="min-h-screen bg-[#020617] text-slate-300 selection:bg-cyan-900 font-sans">
      <header className="border-b border-slate-800 bg-[#0f172a] sticky top-0 z-50 shadow-2xl">
        <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-4">
            <div className="h-8 w-8 bg-cyan-500 rounded flex items-center justify-center shadow-[0_0_15px_rgba(6,182,212,0.6)]">
              <Zap size={20} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl font-bold font-mono tracking-tight text-white">SOVEREIGN V24</h1>
              <p className="text-[10px] text-cyan-400 font-mono tracking-widest uppercase mt-0.5">Global Zero-Friction Handshake</p>
            </div>
          </div>
          
          <div className="hidden md:flex items-center gap-6 text-xs font-mono">
            <div className="flex items-center gap-2 text-green-400">
              <Activity size={14} className="animate-pulse" />
              <span>Target: &lt; 0.5ns</span>
            </div>
            <div className="flex items-center gap-2 text-cyan-400">
              <ShieldCheck size={14} />
              <span>ADR-015 Confirmed</span>
            </div>
            <div className="bg-slate-800 border border-slate-700 rounded px-3 py-1 flex items-center gap-2 text-slate-300">
              <div className="w-2 h-2 rounded-full bg-green-500 animate-pulse"></div>
              <span>ONLINE</span>
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-6 py-8 space-y-8">
        <section className="grid grid-cols-1 gap-8">
          {/* Topology Visualization Area */}
          <TopologyVisualizer />
        </section>
        
        <section className="grid grid-cols-1 lg:grid-cols-2 gap-8 items-stretch lg:h-[700px]">
          {/* Code Viewer Panel */}
          <CodeViewer />
          
          {/* Explanation Panel */}
          <Explanation />
        </section>
      </main>
      
      <footer className="border-t border-slate-800 bg-[#0f172a] py-6 mt-12 text-center text-xs font-mono text-slate-500">
        <p>Sovereign Core Initiative | PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST | Latency Optimization V24.1</p>
      </footer>
    </div>
  );
}
