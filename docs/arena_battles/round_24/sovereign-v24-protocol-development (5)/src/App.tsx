import { useState } from 'react';
import { Terminal } from './components/Terminal';
import { TelemetryMonitor } from './components/TelemetryMonitor';
import { CodeViewer } from './components/CodeViewer';
import { TopologyMap } from './components/TopologyMap';
import { Cpu, ShieldAlert, Activity, GitCommit, FileCode2 } from 'lucide-react';

function App() {
  const [activeTab, setActiveTab] = useState<'dash' | 'code'>('dash');

  return (
    <div className="min-h-screen flex flex-col relative overflow-hidden text-emerald-500 bg-zinc-950">
      <div className="scanline"></div>
      
      {/* Header */}
      <header className="bg-black/80 border-b border-emerald-900/80 p-4 sticky top-0 z-40 backdrop-blur-md">
        <div className="container mx-auto flex flex-col md:flex-row justify-between items-center gap-4">
          <div className="flex items-center space-x-3 group">
            <div className="p-2 bg-emerald-950/50 rounded-lg border border-emerald-800/50 group-hover:border-emerald-400/50 transition-colors">
              <Cpu size={28} className="text-emerald-400 group-hover:text-emerald-300 group-hover:shadow-[0_0_15px_rgba(16,185,129,0.5)] rounded-full" />
            </div>
            <div>
              <h1 className="text-xl md:text-2xl font-bold tracking-[0.2em] text-emerald-400 drop-shadow-[0_0_8px_rgba(16,185,129,0.8)]">
                SOVEREIGN CORE V24
              </h1>
              <p className="text-xs text-cyan-500 font-mono tracking-widest mt-0.5 uppercase flex items-center">
                <GitCommit size={12} className="mr-1" /> The Global Zero-Friction Handshake
              </p>
            </div>
          </div>
          
          <div className="flex bg-black/50 p-1 rounded-lg border border-emerald-900/50 backdrop-blur">
            <button
              onClick={() => setActiveTab('dash')}
              className={"flex items-center px-4 py-2 text-sm font-bold tracking-widest uppercase transition-all rounded " + (activeTab === 'dash' ? 'bg-emerald-900/80 text-emerald-100 shadow-[0_0_10px_rgba(16,185,129,0.3)]' : 'text-emerald-700 hover:text-emerald-400')}
            >
              <Activity size={16} className="mr-2" />
              Telemetry
            </button>
            <button
              onClick={() => setActiveTab('code')}
              className={"flex items-center px-4 py-2 text-sm font-bold tracking-widest uppercase transition-all rounded " + (activeTab === 'code' ? 'bg-cyan-900/80 text-cyan-100 shadow-[0_0_10px_rgba(34,211,238,0.3)]' : 'text-cyan-700 hover:text-cyan-400')}
            >
              <FileCode2 size={16} className="mr-2" />
              V24 Source
            </button>
          </div>

          <div className="flex items-center space-x-4 bg-red-950/20 px-4 py-2 rounded-lg border border-red-900/30">
            <ShieldAlert size={18} className="text-red-500 animate-pulse" />
            <div className="text-xs">
              <div className="text-red-400 font-bold uppercase tracking-widest">ADR-015 Discipline</div>
              <div className="text-red-600/80 font-mono">FENCE-LESS MODE: ENFORCED</div>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex-1 container mx-auto p-4 z-10 relative">
        {activeTab === 'dash' ? (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 h-full min-h-[calc(100vh-140px)]">
            {/* Left Column - Terminal & Topo */}
            <div className="lg:col-span-1 flex flex-col space-y-6">
              <div className="flex-1 min-h-[300px]">
                <TopologyMap />
              </div>
              <div className="flex-1 min-h-[300px]">
                <Terminal />
              </div>
            </div>
            
            {/* Right Column - Telemetry */}
            <div className="lg:col-span-2 flex flex-col space-y-6">
              <div className="flex-1 bg-black/30 border border-emerald-900/30 rounded-lg p-4 backdrop-blur-sm">
                <h2 className="text-lg font-bold text-emerald-400 mb-4 border-b border-emerald-900/50 pb-2 uppercase tracking-widest flex items-center">
                  <Activity size={20} className="mr-2" /> Real-Time Core Diagnostics
                </h2>
                <div className="h-[calc(100%-3rem)] min-h-[300px]">
                  <TelemetryMonitor />
                </div>
              </div>

              {/* Status Panel */}
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 h-32">
                <div className="bg-zinc-950/80 border border-emerald-900/50 p-4 rounded-lg flex flex-col justify-center items-center text-center backdrop-blur">
                  <span className="text-[10px] text-gray-500 font-bold mb-1 tracking-widest uppercase">L1/L2 STRIPE</span>
                  <span className="text-sm md:text-lg font-mono font-bold text-cyan-400 drop-shadow-[0_0_5px_currentColor]">ADAPTIVE</span>
                </div>
                <div className="bg-zinc-950/80 border border-emerald-900/50 p-4 rounded-lg flex flex-col justify-center items-center text-center backdrop-blur">
                  <span className="text-[10px] text-gray-500 font-bold mb-1 tracking-widest uppercase">SAFETY INVARIANT</span>
                  <span className="text-sm md:text-lg font-mono font-bold text-amber-400 drop-shadow-[0_0_5px_currentColor]">SHADOW-VAL</span>
                </div>
                <div className="bg-zinc-950/80 border border-emerald-900/50 p-4 rounded-lg flex flex-col justify-center items-center text-center backdrop-blur">
                  <span className="text-[10px] text-gray-500 font-bold mb-1 tracking-widest uppercase">TSO PARITY</span>
                  <span className="text-sm md:text-lg font-mono font-bold text-purple-400 drop-shadow-[0_0_5px_currentColor]">LOCKED</span>
                </div>
                <div className="bg-zinc-950/80 border border-emerald-900/50 p-4 rounded-lg flex flex-col justify-center items-center text-center backdrop-blur">
                  <span className="text-[10px] text-gray-500 font-bold mb-1 tracking-widest uppercase">LATENCY TARGET</span>
                  <span className="text-sm md:text-lg font-mono font-bold text-emerald-400 drop-shadow-[0_0_5px_currentColor]">0.47ns</span>
                </div>
              </div>
            </div>
          </div>
        ) : (
          <div className="h-[calc(100vh-140px)] bg-black/40 p-4 rounded-lg border border-cyan-900/30 backdrop-blur-md">
             <CodeViewer />
          </div>
        )}
      </main>
      
      {/* Background decorations */}
      <div className="fixed top-1/4 -left-64 w-[500px] h-[500px] bg-emerald-900/10 rounded-full blur-[100px] pointer-events-none z-0"></div>
      <div className="fixed bottom-1/4 -right-64 w-[500px] h-[500px] bg-cyan-900/10 rounded-full blur-[100px] pointer-events-none z-0"></div>
    </div>
  );
}

export default App;
