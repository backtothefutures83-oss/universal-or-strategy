import React, { useEffect, useRef, useState, useCallback } from 'react';
import { ShieldAlert, Activity, Cpu, Server, ShieldCheck, HeartPulse } from 'lucide-react';

interface Log {
  id: string;
  time: string;
  message: string;
  type: 'INFO' | 'WARNING' | 'ERROR' | 'SUCCESS';
}

interface WorkerState {
  id: number;
  latency: number;
  state: 'ACTIVE' | 'STALLED' | 'RECOVERING';
  partner: number;
}

const INITIAL_WORKERS: WorkerState[] = Array.from({ length: 12 }, (_, i) => ({
  id: i,
  latency: 140 + Math.random() * 10, // Target 140ns
  state: 'ACTIVE',
  partner: (i + 1) % 12, // Circular peer watchdog
}));

export default function App() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const workerRef = useRef<Worker | null>(null);
  const workersDataRef = useRef<WorkerState[]>([...INITIAL_WORKERS]);
  const [logs, setLogs] = useState<Log[]>([]);

  const addLog = useCallback((message: string, type: Log['type']) => {
    setLogs((prev) => [
      {
        id: Math.random().toString(36).substring(7),
        time: new Date().toISOString().split('T')[1].slice(0, 11),
        message,
        type,
      },
      ...prev.slice(0, 19), // Keep last 20
    ]);
  }, []);

  useEffect(() => {
    // Initialize Web Worker for OffscreenCanvas
    workerRef.current = new Worker(new URL('./canvas.worker.ts', import.meta.url), {
      type: 'module',
    });

    if (canvasRef.current && canvasRef.current.transferControlToOffscreen) {
      const dpr = window.devicePixelRatio || 1;
      const rect = canvasRef.current.getBoundingClientRect();
      
      canvasRef.current.width = rect.width * dpr;
      canvasRef.current.height = rect.height * dpr;
      
      const offscreen = canvasRef.current.transferControlToOffscreen();
      
      workerRef.current.postMessage({ 
        type: 'INIT', 
        canvas: offscreen, 
        dpr 
      }, [offscreen]);
      
      addLog('Initialized OffscreenCanvas worker for zero-reflow rendering', 'INFO');
      addLog('Project Singularity dispatch engine activated', 'SUCCESS');
    }

    return () => {
      workerRef.current?.terminate();
    };
  }, [addLog]);

  useEffect(() => {
    // Simulation Loop (60 updates per second)
    const interval = setInterval(() => {
      const current = [...workersDataRef.current];
      
      // Update normal latencies
      current.forEach((w) => {
        if (w.state === 'ACTIVE') {
          // Add small jitter around 140ns
          w.latency = 140 + (Math.random() * 5 - 2.5);
        } else if (w.state === 'STALLED') {
          // Simulated stalled latency skyrockets
          w.latency += 50;
        } else if (w.state === 'RECOVERING') {
          // Recovery drops latency back
          w.latency = Math.max(140, w.latency - 20);
          if (w.latency <= 145) {
            w.state = 'ACTIVE';
            addLog(`Worker ${w.id} recovered successfully (< 145ns)`, 'SUCCESS');
          }
        }
      });

      // Pass state to renderer worker
      workersDataRef.current = current;
      if (workerRef.current) {
        workerRef.current.postMessage({ type: 'UPDATE', workers: current });
      }
    }, 16);

    // Chaos Monkey Loop (Stalls a random worker occasionally)
    const chaosInterval = setInterval(() => {
      const current = [...workersDataRef.current];
      const activeWorkers = current.filter(w => w.state === 'ACTIVE');
      
      if (activeWorkers.length > 0 && Math.random() > 0.4) {
        const victim = activeWorkers[Math.floor(Math.random() * activeWorkers.length)];
        const victimIdx = current.findIndex(w => w.id === victim.id);
        
        current[victimIdx].state = 'STALLED';
        addLog(`Watchdog heartbeat lost on W${victim.id} (delta > 300ns)`, 'ERROR');

        // Peer-to-peer watchdog reaction
        const watcher = current.find(w => w.partner === victim.id);
        if (watcher) {
          setTimeout(() => {
            const upToDate = [...workersDataRef.current];
            const vIdx = upToDate.findIndex(w => w.id === victim.id);
            if (upToDate[vIdx].state === 'STALLED') {
              upToDate[vIdx].state = 'RECOVERING';
              addLog(`W${watcher.id} local interrupt triggered on W${victim.id}. Hot-standby mapped to Arena slice.`, 'WARNING');
            }
          }, 300); // 300ms visual delay, but represents sub-500ns in narrative
        }
      }
    }, 3000);

    return () => {
      clearInterval(interval);
      clearInterval(chaosInterval);
    };
  }, [addLog]);

  return (
    <div className="min-h-screen bg-[#020617] text-slate-200 font-sans p-6 flex flex-col">
      {/* Header */}
      <header className="flex justify-between items-center mb-8 pb-4 border-b border-slate-800">
        <div className="flex items-center gap-4">
          <div className="bg-emerald-500/10 p-2 rounded-lg border border-emerald-500/20">
            <Cpu className="w-8 h-8 text-emerald-500" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-white tracking-tight">Project Singularity</h1>
            <p className="text-slate-400 text-sm font-mono">V10 Userspace-Ring Hyper-Dispatch | 12 Parallel Nodes</p>
          </div>
        </div>
        <div className="flex gap-4">
          <div className="flex items-center gap-2 bg-slate-900 px-4 py-2 rounded border border-slate-800">
            <Activity className="w-4 h-4 text-emerald-500 animate-pulse" />
            <span className="font-mono text-sm">Base Arena: 140ns</span>
          </div>
          <div className="flex items-center gap-2 bg-slate-900 px-4 py-2 rounded border border-slate-800">
            <HeartPulse className="w-4 h-4 text-amber-500" />
            <span className="font-mono text-sm">Watchdog: &lt;500ns</span>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 flex-1">
        {/* Left Column: Worker Grid */}
        <div className="col-span-2 bg-slate-900 border border-slate-800 rounded-xl overflow-hidden flex flex-col relative min-h-[500px]">
          <div className="p-4 border-b border-slate-800 flex justify-between items-center bg-slate-800/50 z-10">
            <h2 className="text-lg font-semibold flex items-center gap-2">
              <Server className="w-5 h-5" /> Live Arena Visualization
            </h2>
            <span className="text-xs bg-emerald-500/20 text-emerald-400 px-2 py-1 rounded font-mono border border-emerald-500/30">
              OffscreenCanvas Enabled (Zero-Reflow)
            </span>
          </div>
          {/* Canvas Container */}
          <div className="flex-1 w-full h-full relative">
            <canvas
              ref={canvasRef}
              className="absolute inset-0 w-full h-full"
              style={{ width: '100%', height: '100%' }}
            />
          </div>
        </div>

        {/* Right Column: System Logs & Config */}
        <div className="flex flex-col gap-6">
          <div className="bg-slate-900 border border-slate-800 rounded-xl flex-1 flex flex-col overflow-hidden">
            <div className="p-4 border-b border-slate-800 bg-slate-800/50">
              <h2 className="text-lg font-semibold flex items-center gap-2">
                <ShieldAlert className="w-5 h-5" /> Real-time System Events
              </h2>
            </div>
            <div className="flex-1 overflow-y-auto p-4 space-y-3 font-mono text-xs">
              {logs.map((log) => (
                <div key={log.id} className="flex gap-3 animate-fade-in">
                  <span className="text-slate-500 shrink-0">[{log.time}]</span>
                  <span className={`
                    ${log.type === 'INFO' ? 'text-blue-400' : ''}
                    ${log.type === 'SUCCESS' ? 'text-emerald-400' : ''}
                    ${log.type === 'WARNING' ? 'text-amber-400' : ''}
                    ${log.type === 'ERROR' ? 'text-red-400 font-semibold' : ''}
                  `}>
                    {log.message}
                  </span>
                </div>
              ))}
            </div>
          </div>

          <div className="bg-slate-900 border border-slate-800 rounded-xl p-5">
             <h3 className="text-sm font-semibold text-slate-400 mb-4 uppercase tracking-wider flex items-center gap-2">
               <ShieldCheck className="w-4 h-4" /> Breakthrough Specifications
             </h3>
             <ul className="space-y-4 text-sm">
               <li className="border-l-2 border-emerald-500 pl-3">
                 <strong className="block text-slate-200">Sub-200ns Mechanism</strong>
                 <span className="text-slate-400">Userspace Ring + CPU Pinning/IRQ Isol (140ns floor)</span>
               </li>
               <li className="border-l-2 border-blue-500 pl-3">
                 <strong className="block text-slate-200">Rendering Protocol</strong>
                 <span className="text-slate-400">OffscreenCanvas Worker (60fps, 0 DOM Reflows)</span>
               </li>
               <li className="border-l-2 border-amber-500 pl-3">
                 <strong className="block text-slate-200">Recovery Protocol</strong>
                 <span className="text-slate-400">P2P Ring Watchdog (Self-healing in ~450ns)</span>
               </li>
             </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
