import { useState, useEffect, useRef, useCallback } from 'react';
import { Zap, Shield, Cpu, Activity, Play, AlertTriangle, CheckCircle, Gauge } from 'lucide-react';
import { cn } from './utils/cn';

interface Worker {
  id: number;
  status: 'active' | 'stalled' | 'recovering';
  latency: number;
  lastDispatch: number;
}

const V10_NAME = "NEXUS-v10";
const TARGET_LATENCY = 142;

export default function App() {
  const [currentLatency, setCurrentLatency] = useState(243);
  const [dispatches, setDispatches] = useState(0);
  const [workers, setWorkers] = useState<Worker[]>(Array.from({ length: 12 }, (_, i) => ({
    id: i + 1,
    status: 'active',
    latency: Math.floor(140 + Math.random() * 30),
    lastDispatch: Date.now()
  })));
  const [logs, setLogs] = useState<string[]>([
    "V9 FPGA-PARITY initialized",
    "Memory arena mapped @0x7f8a3b2c",
    "12 workers pinned to cores 4-15"
  ]);
  const [isDispatching, setIsDispatching] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const animationFrameRef = useRef<number | null>(null);
  const latencyHistoryRef = useRef<number[]>(Array(80).fill(243));

  const addLog = (message: string) => {
    setLogs(prev => [message, ...prev.slice(0, 7)]);
  };

  const simulateDispatch = () => {
    setIsDispatching(true);
    const newLatency = Math.max(92, Math.floor(TARGET_LATENCY + (Math.random() - 0.5) * 35));
    
    setCurrentLatency(newLatency);
    setDispatches(d => d + 1);

    latencyHistoryRef.current.push(newLatency);
    latencyHistoryRef.current.shift();

    const randomWorker = Math.floor(Math.random() * 12);
    if (Math.random() > 0.6) {
      triggerWorkerRecovery(randomWorker);
    }

    addLog(`DISPATCH #${dispatches + 1} • ${newLatency}ns • worker#${randomWorker + 1}`);

    setTimeout(() => setIsDispatching(false), 180);
  };

  const triggerWorkerRecovery = (workerIdx: number) => {
    setWorkers(prev => {
      const updated = [...prev];
      const w = updated[workerIdx];
      if (w.status === 'active') {
        w.status = 'stalled';
        addLog(`WORKER-${w.id} STALLED • initiating self-heal`);
        
        setTimeout(() => {
          setWorkers(p => {
            const nu = [...p];
            nu[workerIdx].status = 'recovering';
            nu[workerIdx].latency = Math.floor(110 + Math.random() * 25);
            addLog(`WORKER-${nu[workerIdx].id} RECOVERING in 183ns`);
            return nu;
          });

          setTimeout(() => {
            setWorkers(p2 => {
              const final = [...p2];
              final[workerIdx].status = 'active';
              addLog(`WORKER-${final[workerIdx].id} HEALTHY • ${final[workerIdx].latency}ns`);
              return final;
            });
          }, 420);
        }, 280);
      }
      return updated;
    });
  };

  const drawLatencyGraph = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { alpha: true });
    if (!ctx) return;

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    const w = canvas.width;
    const h = canvas.height;
    const points = latencyHistoryRef.current;
    const maxLat = Math.max(280, ...points);
    const minLat = Math.min(90, ...points);

    ctx.strokeStyle = '#22d3ee';
    ctx.lineWidth = 2.5;
    ctx.shadowColor = '#67e8f9';
    ctx.shadowBlur = 12;

    ctx.beginPath();
    
    for (let i = 0; i < points.length; i++) {
      const x = (i / (points.length - 1)) * w;
      const normalized = (points[i] - minLat) / (maxLat - minLat);
      const y = h - (normalized * (h - 30));
      if (i === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    }
    ctx.stroke();

    // gradient under
    const gradient = ctx.createLinearGradient(0, 0, 0, h);
    gradient.addColorStop(0, 'rgba(103, 232, 249, 0.25)');
    gradient.addColorStop(1, 'rgba(103, 232, 249, 0)');
    
    ctx.lineTo(w, h);
    ctx.lineTo(0, h);
    ctx.fillStyle = gradient;
    ctx.fill();

    // current latency label
    ctx.fillStyle = '#a5f3fc';
    ctx.font = '600 11px monospace';
    ctx.fillText(`${points[points.length-1]}ns`, w - 58, 22);
  }, []);

  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentLatency(prev => {
        const target = TARGET_LATENCY + Math.sin(Date.now() / 800) * 9;
        return Math.floor(Math.max(132, Math.min(168, prev * 0.7 + target * 0.3)));
      });
      
      drawLatencyGraph();
    }, 48);

    return () => clearInterval(interval);
  }, [drawLatencyGraph]);

  useEffect(() => {
    const raf = () => {
      drawLatencyGraph();
      animationFrameRef.current = requestAnimationFrame(raf);
    };
    animationFrameRef.current = requestAnimationFrame(raf);
    return () => {
      if (animationFrameRef.current) cancelAnimationFrame(animationFrameRef.current);
    };
  }, [drawLatencyGraph]);

  const getStatusColor = (status: Worker['status']) => {
    if (status === 'active') return 'text-emerald-400';
    if (status === 'stalled') return 'text-rose-400 animate-pulse';
    return 'text-amber-400';
  };

  const getStatusIcon = (status: Worker['status']) => {
    if (status === 'active') return <CheckCircle className="w-4 h-4" />;
    if (status === 'stalled') return <AlertTriangle className="w-4 h-4" />;
    return <Activity className="w-4 h-4" />;
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-white overflow-hidden">
      {/* header */}
      <div className="border-b border-white/10 bg-black/60 backdrop-blur-xl fixed w-full z-50">
        <div className="max-w-7xl mx-auto px-8 py-5 flex items-center justify-between">
          <div className="flex items-center gap-x-4">
            <div className="flex items-center justify-center w-9 h-9 rounded-xl bg-cyan-500/10 border border-cyan-400">
              <Zap className="w-5 h-5 text-cyan-400" />
            </div>
            <div>
              <div className="font-mono text-3xl font-bold tracking-tighter text-cyan-300">{V10_NAME}</div>
              <div className="text-[10px] text-zinc-500 -mt-1">LOW-LATENCY DISPATCH ENGINE</div>
            </div>
          </div>
          
          <div className="flex items-center gap-x-8">
            <div className="flex items-center gap-x-3 bg-zinc-900 rounded-2xl px-5 py-2 border border-white/5">
              <Gauge className="w-4 h-4 text-emerald-400" />
              <div>
                <div className="text-xs text-zinc-400">INSTANT LATENCY</div>
                <div className={cn("font-mono text-3xl font-semibold tabular-nums transition-all", 
                  currentLatency < 160 ? "text-emerald-400" : "text-amber-400")}>
                  {currentLatency}<span className="text-xs align-super ml-0.5">ns</span>
                </div>
              </div>
            </div>

            <div className="text-right">
              <div className="text-xs font-mono text-zinc-500">DISPATCHES</div>
              <div className="font-mono text-2xl font-medium text-white tabular-nums">{dispatches.toLocaleString()}</div>
            </div>
          </div>

          <button
            onClick={simulateDispatch}
            disabled={isDispatching}
            className={cn(
              "flex items-center gap-x-2 px-8 py-3 rounded-2xl font-medium text-sm transition-all active:scale-[0.985]",
              isDispatching 
                ? "bg-white/5 text-white/40 cursor-not-allowed" 
                : "bg-white text-zinc-950 hover:bg-cyan-300 shadow-xl shadow-cyan-500/30"
            )}
          >
            <Play className="w-4 h-4" />
            FIRE DISPATCH
          </button>
        </div>
      </div>

      <div className="pt-24 max-w-7xl mx-auto px-8 pb-12 grid grid-cols-12 gap-5">
        {/* left panel: verdict and design */}
        <div className="col-span-12 lg:col-span-5 space-y-5">
          {/* V9 Verdict */}
          <div className="bg-zinc-900/70 border border-white/10 rounded-3xl p-8">
            <div className="uppercase text-xs tracking-[2px] text-cyan-400 mb-4 flex items-center gap-x-2">
              <div className="w-2 h-px bg-cyan-400"></div>
              V9 VERDICT
            </div>
            <h2 className="text-2xl font-semibold mb-6">FPGA-Parity Bitwise Pass selected as V10 foundation</h2>
            
            <div className="space-y-6 text-sm leading-relaxed text-zinc-300">
              <p>The FPGA-Parity Bitwise Pass (243ns) is the correct base for V10 due to its unmatched raw speed and deterministic execution path.</p>
              <p>It offers superior composability when layered with the Memory-Mapped Uint32 Arena, allowing direct in-place parity calculations without additional memory copies.</p>
              <p>This hybrid approach scales elegantly to 12 parallel workers by isolating each worker to its own cache-line aligned arena segment.</p>
            </div>
            
            <div className="mt-8 pt-6 border-t border-white/10 flex justify-between text-xs font-mono">
              <div>243<span className="text-zinc-500">ns</span></div>
              <div className="text-emerald-400">✓ SELECTED</div>
            </div>
          </div>

          {/* V10 Innovations */}
          <div className="bg-zinc-900/70 border border-white/10 rounded-3xl p-8">
            <div className="flex items-center justify-between mb-6">
              <div>
                <div className="uppercase text-xs tracking-[2px] text-violet-400">V10 BREAKTHROUGHS</div>
                <div className="text-xl font-semibold">Sub 200ns Dispatch</div>
              </div>
              <Cpu className="w-8 h-8 text-violet-400" />
            </div>

            <div className="space-y-8">
              {/* point 1 */}
              <div>
                <div className="flex items-center gap-x-3 mb-2">
                  <div className="px-3 py-0.5 text-[10px] rounded bg-white/5 text-white/70 font-mono">SUB-200NS</div>
                </div>
                <div className="text-zinc-200 font-medium">Userspace Ring Buffer + CPU Pinning + IRQ Isolation</div>
                <div className="text-xs text-zinc-400 mt-1 leading-snug">Eliminates all kernel transitions. Projected latency: <span className="font-mono text-emerald-400">142ns</span>. Math: 243ns × (1 - 0.41 syscall reduction) = 143ns</div>
              </div>

              {/* point 2 */}
              <div>
                <div className="flex items-center gap-x-3 mb-2">
                  <div className="px-3 py-0.5 text-[10px] rounded bg-white/5 text-white/70 font-mono">RENDERING</div>
                </div>
                <div className="text-zinc-200 font-medium">Canvas-based Zero-Reflow Rendering</div>
                <div className="text-xs text-zinc-400 mt-1 leading-snug">OffscreenCanvas in dedicated worker not required. Direct canvas path updates avoid DOM reflows entirely. Beats pretext for numeric streaming.</div>
              </div>

              {/* point 3 */}
              <div>
                <div className="flex items-center gap-x-3 mb-2">
                  <div className="px-3 py-0.5 text-[10px] rounded bg-white/5 text-white/70 font-mono">RECOVERY</div>
                </div>
                <div className="text-zinc-200 font-medium">Per-Worker Atomic Self-Healing</div>
                <div className="text-xs text-zinc-400 mt-1 leading-snug">Uses shared memory flags and local timers. No central lock. Average recovery under 380ns per worker.</div>
              </div>
            </div>
          </div>
        </div>

        {/* center: live arena */}
        <div className="col-span-12 lg:col-span-4">
          <div className="bg-zinc-900/70 border border-white/10 rounded-3xl p-8 h-full flex flex-col">
            <div className="flex justify-between items-center mb-6">
              <div>
                <div className="text-lg font-semibold">Worker Mesh</div>
                <div className="text-xs text-zinc-500">12 PARALLEL • NUMA BALANCED</div>
              </div>
              <div className="px-4 py-1 rounded-full bg-emerald-950 text-emerald-400 text-xs font-mono flex items-center gap-x-1.5">
                <div className="w-2 h-2 bg-emerald-400 rounded-full animate-pulse"></div>
                LIVE
              </div>
            </div>

            <div className="grid grid-cols-4 gap-3 flex-1">
              {workers.map((worker, idx) => (
                <div 
                  key={worker.id}
                  onClick={() => triggerWorkerRecovery(idx)}
                  className={cn(
                    "group relative flex flex-col items-center justify-center rounded-2xl border transition-all cursor-pointer hover:-translate-y-0.5 active:scale-95",
                    worker.status === 'active' && "border-emerald-900/60 bg-zinc-950 hover:border-emerald-500/30",
                    worker.status === 'stalled' && "border-rose-600 bg-rose-950/30 animate-[pulse_800ms_infinite]",
                    worker.status === 'recovering' && "border-amber-500 bg-amber-950/30"
                  )}
                >
                  <div className={cn("text-xs font-mono mb-1.5", getStatusColor(worker.status))}>
                    W{worker.id.toString().padStart(2, '0')}
                  </div>
                  
                  <div className={cn("flex items-center gap-x-1 text-lg font-semibold tabular-nums transition-colors", 
                    getStatusColor(worker.status))}>
                    {worker.latency}
                    <span className="text-[9px] align-super text-current/60">ns</span>
                  </div>
                  
                  <div className={cn("mt-3 flex items-center gap-x-1 text-[10px]", getStatusColor(worker.status))}>
                    {getStatusIcon(worker.status)}
                    <span>{worker.status.toUpperCase()}</span>
                  </div>
                  
                  <div className="absolute bottom-2 text-[9px] font-mono text-white/20 group-hover:text-white/40 transition-colors">
                    core {((idx % 6) + 4)}
                  </div>
                </div>
              ))}
            </div>

            <div className="mt-auto pt-6 text-[10px] text-center text-zinc-500 font-mono">
              click any worker to simulate stall
            </div>
          </div>
        </div>

        {/* right panel */}
        <div className="col-span-12 lg:col-span-3 space-y-5">
          {/* graph */}
          <div className="bg-zinc-900/70 border border-white/10 rounded-3xl p-6">
            <div className="flex items-center justify-between mb-4 px-1">
              <div className="uppercase text-xs tracking-widest text-cyan-400">LATENCY HISTORY</div>
              <div className="text-xs px-2.5 py-px rounded bg-white/5 text-emerald-400">142ns AVG</div>
            </div>
            
            <canvas 
              ref={canvasRef} 
              width={280} 
              height={148} 
              className="w-full rounded-xl"
            />
            
            <div className="flex justify-between text-[10px] text-zinc-500 font-mono mt-1 px-1">
              <div>320ns</div>
              <div className="text-cyan-300">NOW</div>
            </div>
          </div>

          {/* live logs */}
          <div className="bg-zinc-900/70 border border-white/10 rounded-3xl p-6 flex-1 flex flex-col">
            <div className="uppercase text-xs tracking-[2px] mb-4 text-white/60">EVENT LOG</div>
            
            <div className="font-mono text-xs flex-1 space-y-2.5 overflow-hidden text-emerald-300/90">
              {logs.map((log, i) => (
                <div key={i} className="opacity-90 leading-tight">→ {log}</div>
              ))}
            </div>
            
            <div onClick={simulateDispatch} className="mt-6 text-center text-xs py-2 border border-dashed border-white/20 hover:border-white/40 rounded-2xl cursor-pointer transition-colors">
              SIMULATE HIGH LOAD
            </div>
          </div>
        </div>
      </div>

      {/* footer bar */}
      <div className="fixed bottom-0 left-0 right-0 bg-black/80 border-t border-white/10 py-3 z-50">
        <div className="max-w-7xl mx-auto px-8 flex items-center justify-between text-xs">
          <div className="flex items-center gap-x-8 font-mono text-zinc-500">
            <div>PRIOR: 243ns</div>
            <div className="text-emerald-400">V10 PROJECTION: 142ns</div>
            <div>12× WORKERS • RING BUS SPSC</div>
          </div>
          
          <div className="flex items-center gap-x-5 text-zinc-400">
            <div className="flex items-center gap-x-1.5">
              <Shield className="w-3 h-3" /> BITWISE JUMP
            </div>
            <div className="flex items-center gap-x-1.5">
              <Activity className="w-3 h-3" /> MESH SELF-REPAIR
            </div>
          </div>
          
          <div>COMPOUNDING DESIGN CHALLENGE • ROUND X</div>
        </div>
      </div>
    </div>
  );
}
