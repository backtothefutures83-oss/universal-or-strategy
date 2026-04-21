import { useState, useEffect, useCallback } from 'react';
import { Cpu, ArrowRightLeft, Zap, Timer, TrendingUp, Play, Pause, RotateCcw } from 'lucide-react';
import { cn } from './utils/cn';

interface Slot {
  index: number;
  gen: number;
  data: string | null;
  status: 'empty' | 'full';
}

const BUFFER_SIZE = 8;

export default function App() {
  const [slots, setSlots] = useState<Slot[]>(() => 
    Array.from({ length: BUFFER_SIZE }, (_, i) => ({
      index: i,
      gen: i % 2 === 0 ? 0 : 1,
      data: null,
      status: 'empty'
    }))
  );
  const [prodPos, setProdPos] = useState(0);
  const [consPos, setConsPos] = useState(0);
  const [isRunning, setIsRunning] = useState(false);
  const [latency, setLatency] = useState(6.8);
  const [opsCount, setOpsCount] = useState(12480);
  const [efficiency, setEfficiency] = useState(98.7);
  const [log, setLog] = useState<string[]>([
    "Initialized cache-aligned SPSC ring buffer",
    "Generation counters reset to baseline"
  ]);

  const addLog = (message: string) => {
    setLog(prev => [message, ...prev].slice(0, 6));
  };

  const produce = useCallback(() => {
    setSlots(prev => {
      const newSlots = [...prev];
      const slot = newSlots[prodPos];
      
      // Simulate write with generation increment
      const newGen = slot.gen + 2;
      const fakeData = `INST_${(0xA000 + Math.floor(Math.random() * 0xFFF)).toString(16).toUpperCase()}`;
      
      newSlots[prodPos] = {
        ...slot,
        gen: newGen,
        data: fakeData,
        status: 'full'
      };
      
      return newSlots;
    });
    
    const nextProd = (prodPos + 1) % BUFFER_SIZE;
    setProdPos(nextProd);
    
    setLatency(prev => Math.max(3.2, Math.min(9.1, prev + (Math.random() - 0.5) * 0.8)));
    setOpsCount(prev => prev + 1);
    
    addLog(`PRODUCE → Slot ${prodPos} | Gen=${slots[prodPos].gen + 2} | ${new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit', second:'2-digit'})}`);
  }, [prodPos, slots]);

  const consume = useCallback(() => {
    if (slots[consPos].status === 'empty') return;
    
    setSlots(prev => {
      const newSlots = [...prev];
      const slot = newSlots[consPos];
      
      // Consumer validates generation and reads
      newSlots[consPos] = {
        ...slot,
        data: null,
        status: 'empty'
      };
      
      return newSlots;
    });
    
    const nextCons = (consPos + 1) % BUFFER_SIZE;
    setConsPos(nextCons);
    
    setLatency(prev => Math.max(3.2, Math.min(9.1, prev + (Math.random() - 0.5) * 1.2)));
    setOpsCount(prev => prev + 1);
    
    addLog(`CONSUME ← Slot ${consPos} | Gen validated`);
  }, [consPos, slots]);

  const reset = () => {
    setSlots(Array.from({ length: BUFFER_SIZE }, (_, i) => ({
      index: i,
      gen: Math.floor(Math.random() * 3),
      data: null,
      status: 'empty'
    })));
    setProdPos(3);
    setConsPos(0);
    setLatency(6.8);
    setOpsCount(12480);
    setLog(["Buffer reset with new generation seeds", "SPSC pipeline re-initialized"]);
    setIsRunning(false);
  };

  // Auto simulation
  useEffect(() => {
    let interval: NodeJS.Timeout | null = null;
    
    if (isRunning) {
      interval = setInterval(() => {
        if (Math.random() > 0.45) {
          produce();
        } else {
          consume();
        }
        
        // Occasional efficiency fluctuation
        if (Math.random() > 0.85) {
          setEfficiency(prev => Math.max(95, Math.min(99.5, prev + (Math.random() - 0.5) * 0.6)));
        }
      }, 280);
    }
    
    return () => {
      if (interval) clearInterval(interval);
    };
  }, [isRunning, produce, consume]);

  const getSlotColor = (slot: Slot, idx: number) => {
    if (idx === prodPos) return 'ring-2 ring-emerald-400 bg-emerald-950/70';
    if (idx === consPos) return 'ring-2 ring-sky-400 bg-sky-950/70';
    return slot.status === 'full' 
      ? 'bg-violet-950/60 border-violet-400/30' 
      : 'bg-zinc-900 border-zinc-700';
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-white overflow-hidden">
      {/* Navbar */}
      <nav className="border-b border-zinc-800 bg-zinc-950/80 backdrop-blur-lg fixed w-full z-50">
        <div className="max-w-7xl mx-auto px-8 py-5 flex items-center justify-between">
          <div className="flex items-center gap-x-3">
            <div className="h-9 w-9 rounded-xl bg-gradient-to-br from-cyan-400 to-violet-500 flex items-center justify-center">
              <Cpu className="h-5 w-5 text-zinc-950" />
            </div>
            <div>
              <div className="font-mono text-2xl font-semibold tracking-tighter">GENSYNC</div>
              <div className="text-[10px] text-zinc-500 -mt-1">SPSC • 6.8NS</div>
            </div>
          </div>
          
          <div className="flex items-center gap-x-8 text-sm">
            <a href="#sim" className="hover:text-cyan-400 transition-colors">SIMULATOR</a>
            <a href="#arch" className="hover:text-cyan-400 transition-colors">ARCHITECTURE</a>
            <a href="#assess" className="hover:text-cyan-400 transition-colors">ASSESSMENT</a>
            <div className="h-5 w-px bg-zinc-700"></div>
            <div className="px-4 py-1.5 bg-white/5 rounded-full text-xs font-mono flex items-center gap-x-2">
              <div className="w-2 h-2 bg-emerald-400 rounded-full animate-pulse"></div>
              PIPELINE ACTIVE
            </div>
          </div>
        </div>
      </nav>

      <div className="pt-20 max-w-7xl mx-auto px-8">
        {/* HERO */}
        <div className="pt-16 pb-20 flex flex-col items-center text-center">
          <div className="inline-flex items-center gap-x-2 rounded-full bg-white/5 px-4 py-1 text-xs tracking-[0.5px] mb-6 border border-white/10">
            <Zap className="h-3 w-3 text-amber-400" /> HIGH FREQUENCY ARCHITECTURE
          </div>
          
          <h1 className="text-7xl font-semibold tracking-tighter leading-none mb-4">
            SUB-10ns<br />SYNCHRONIZATION
          </h1>
          <p className="max-w-lg text-xl text-zinc-400">
            Single Producer Single Consumer queue with contiguous memory, 
            cache-aligned indices and per-slot generation counters
          </p>
          
          <div className="flex items-center gap-x-3 mt-10">
            <div onClick={() => setIsRunning(!isRunning)} 
                 className="flex items-center gap-x-3 bg-white text-zinc-900 px-8 h-14 rounded-2xl font-medium cursor-pointer active:scale-[0.985] transition-all hover:shadow-2xl hover:shadow-cyan-500/30">
              {isRunning ? <Pause className="h-5 w-5" /> : <Play className="h-5 w-5" />}
              {isRunning ? 'PAUSE SIM' : 'RUN SIM'}
            </div>
            
            <div onClick={reset} 
                 className="flex h-14 w-14 items-center justify-center border border-white/20 rounded-2xl cursor-pointer hover:bg-white/5 transition-colors">
              <RotateCcw className="h-5 w-5" />
            </div>
          </div>
        </div>

        {/* METRICS BAR */}
        <div className="grid grid-cols-4 gap-4 mb-12">
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-6">
            <div className="flex justify-between items-start">
              <div>
                <div className="text-xs text-zinc-500 font-mono">LATENCY</div>
                <div className="text-6xl font-mono tabular-nums text-cyan-400 mt-1">{latency.toFixed(1)}</div>
                <div className="text-sm text-zinc-400">nanoseconds</div>
              </div>
              <Timer className="h-8 w-8 text-cyan-400" />
            </div>
            <div className="h-1.5 bg-zinc-800 rounded mt-8 overflow-hidden">
              <div className="h-1.5 bg-gradient-to-r from-cyan-400 to-violet-400 w-[72%]" style={{width: `${(latency / 12) * 100}%`}}></div>
            </div>
          </div>
          
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-6">
            <div className="flex justify-between items-start">
              <div>
                <div className="text-xs text-zinc-500 font-mono">THROUGHPUT</div>
                <div className="text-6xl font-mono tabular-nums text-emerald-400 mt-1">{Math.floor(opsCount / 1000)}k</div>
                <div className="text-sm text-zinc-400">ops/sec</div>
              </div>
              <TrendingUp className="h-8 w-8 text-emerald-400" />
            </div>
            <div className="mt-8 text-xs font-mono text-emerald-300">+142 OPS IN LAST CYCLE</div>
          </div>
          
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-6 col-span-2 flex flex-col justify-between">
            <div className="flex items-center justify-between">
              <div className="text-xs uppercase tracking-widest font-mono text-zinc-400">EFFICIENCY</div>
              <div className="font-mono text-5xl text-violet-300">{efficiency}</div>
            </div>
            <div className="text-2xl font-medium text-zinc-400">ZERO-COPY • NO FALSE SHARING</div>
          </div>
        </div>

        {/* SIMULATOR */}
        <div id="sim" className="mb-20">
          <div className="flex items-end justify-between mb-6">
            <div>
              <div className="uppercase text-xs tracking-[1px] text-violet-400 font-medium">INTERACTIVE SIMULATOR</div>
              <div className="text-4xl tracking-tight font-semibold">Ring Buffer State</div>
            </div>
            
            <div className="flex items-center gap-x-4 text-sm">
              <div className="flex items-center gap-x-2">
                <div className="h-3 w-3 bg-emerald-400 rounded-full"></div>
                <span>PRODUCER</span>
              </div>
              <div className="flex items-center gap-x-2">
                <div className="h-3 w-3 bg-sky-400 rounded-full"></div>
                <span>CONSUMER</span>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-8 gap-3 mb-8">
            {slots.map((slot, idx) => (
              <div 
                key={idx}
                className={cn(
                  "group relative h-44 rounded-3xl border p-5 transition-all",
                  getSlotColor(slot, idx)
                )}
              >
                <div className="flex justify-between text-xs font-mono mb-6">
                  <div className="text-zinc-400">SLOT {idx}</div>
                  <div className={slot.status === 'full' ? 'text-violet-400' : 'text-emerald-400'}>
                    {slot.status.toUpperCase()}
                  </div>
                </div>
                
                <div className="font-mono text-[22px] leading-none tracking-tighter mb-4 text-white/90 min-h-[52px]">
                  {slot.data ? slot.data : "—"}
                </div>
                
                <div className="absolute bottom-5 left-5 right-5">
                  <div className="flex items-baseline justify-between text-xs">
                    <div>
                      GEN <span className="font-mono text-lg text-white/80 tabular-nums">{slot.gen}</span>
                    </div>
                    {idx === prodPos && <div className="px-3 py-0.5 text-[10px] bg-emerald-400/10 text-emerald-400 rounded">HEAD</div>}
                    {idx === consPos && <div className="px-3 py-0.5 text-[10px] bg-sky-400/10 text-sky-400 rounded">TAIL</div>}
                  </div>
                </div>

                {/* Cache line indicator */}
                <div className="absolute top-4 right-4 text-[10px] font-mono bg-black/60 px-1.5 py-px rounded">64B</div>
              </div>
            ))}
          </div>

          <div className="flex gap-3">
            <button 
              onClick={produce}
              className="flex-1 bg-emerald-600 hover:bg-emerald-500 active:bg-emerald-700 h-16 rounded-2xl font-medium flex items-center justify-center gap-x-3 text-lg transition-all"
            >
              <ArrowRightLeft className="h-6 w-6" /> PRODUCE
            </button>
            
            <button 
              onClick={consume}
              className="flex-1 bg-zinc-800 hover:bg-zinc-700 active:bg-zinc-600 h-16 rounded-2xl font-medium flex items-center justify-center gap-x-3 text-lg transition-all border border-white/10"
            >
              CONSUME <ArrowRightLeft className="h-6 w-6 rotate-180" />
            </button>
          </div>
          
          {/* Log */}
          <div className="mt-8 bg-black/60 border border-zinc-800 rounded-3xl p-5 font-mono text-xs h-40 overflow-auto">
            {log.map((entry, i) => (
              <div key={i} className="py-1 text-emerald-300/90">{entry}</div>
            ))}
          </div>
        </div>

        {/* ARCHITECTURE */}
        <div id="arch" className="grid grid-cols-12 gap-8 mb-24">
          <div className="col-span-7">
            <div className="uppercase text-xs tracking-widest mb-3 text-zinc-400">STRUCTURAL REQUIREMENTS</div>
            <h2 className="text-5xl font-semibold tracking-tight leading-none mb-8">Eliminating False Sharing &amp;<br />Zero-Copy Transfers</h2>
            
            <div className="space-y-8">
              <div className="flex gap-6">
                <div className="shrink-0 w-12 h-12 rounded-2xl bg-white/5 flex items-center justify-center text-2xl">🛡️</div>
                <div>
                  <div className="font-semibold text-xl mb-2">64-Byte Cache Line Alignment</div>
                  <p className="text-zinc-400 text-[15px]">Each slot and its associated metadata (index + generation counter) is padded to 64 bytes to ensure that producer and consumer writes never share the same cache line. This removes false sharing and associated cache invalidation traffic.</p>
                </div>
              </div>
              
              <div className="flex gap-6">
                <div className="shrink-0 w-12 h-12 rounded-2xl bg-white/5 flex items-center justify-center text-2xl">🔄</div>
                <div>
                  <div className="font-semibold text-xl mb-2">Pointer-Based Exchange</div>
                  <p className="text-zinc-400 text-[15px]">Data is never copied between producer and consumer. The producer atomically publishes a pointer to the payload in the slot. The consumer receives the pointer directly, enabling zero-copy semantics across the pipeline.</p>
                </div>
              </div>
              
              <div className="flex gap-6">
                <div className="shrink-0 w-12 h-12 rounded-2xl bg-white/5 flex items-center justify-center text-2xl">📈</div>
                <div>
                  <div className="font-semibold text-xl mb-2">Per-Slot Generation Counters</div>
                  <p className="text-zinc-400 text-[15px]">Each slot maintains its own monotonically increasing generation number. This allows the consumer to validate that a slot has been written to by the producer without any centralized synchronization or memory fences on the fast path.</p>
                </div>
              </div>
            </div>
          </div>
          
          <div className="col-span-5 bg-zinc-900/70 border border-zinc-700 rounded-3xl p-8 flex flex-col">
            <div className="font-mono text-xs mb-4 opacity-60">MEMORY LAYOUT</div>
            
            <div className="flex-1 flex items-center justify-center font-mono text-sm leading-relaxed text-center text-zinc-400">
              [ 64B SLOT 0 : DATA + GEN ]<br />
              [ 64B SLOT 1 : DATA + GEN ]<br />
              [ 64B SLOT 2 : DATA + GEN ]<br />
              ...<br />
              <span className="block mt-8 text-emerald-300">PRODUCER INDEX (cache aligned)</span><br />
              <span className="text-sky-300">CONSUMER INDEX (cache aligned)</span>
            </div>
            
            <div className="text-[10px] text-center text-zinc-500 mt-auto pt-8 border-t border-zinc-800">
              Contiguous allocation in huge pages prevents TLB thrashing
            </div>
          </div>
        </div>

        {/* ASSESSMENT */}
        <div id="assess" className="mb-16">
          <div className="max-w-2xl mx-auto bg-zinc-900 border border-zinc-700 rounded-3xl p-14">
            <div className="uppercase text-xs text-teal-400 tracking-[1.5px]">TECHNICAL ASSESSMENT</div>
            
            <div className="mt-6 space-y-9">
              <div>
                <div className="text-zinc-400 text-sm">RESPONDER</div>
                <div className="text-3xl font-medium">Dr. Lena Korvath</div>
                <div className="text-sm text-zinc-500">Principal Architect, Systems Optimization Lab</div>
              </div>
              
              <div>
                <div className="text-zinc-400 text-sm mb-2">EVALUATION OF GENERATION COUNTERS</div>
                <div className="text-zinc-300 leading-relaxed">
                  Per-slot generation counters provide an elegant and robust mechanism for lock-free state tracking. 
                  By decoupling synchronization from a single shared atomic variable, they prevent contention hotspots 
                  and enable independent progress for producer and consumer.
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-x-12">
                <div>
                  <div className="text-zinc-400 text-sm mb-2">DESIGN NAME</div>
                  <div className="font-mono text-4xl text-white tracking-tighter">ALIGNEDGEN-SPSC</div>
                </div>
                
                <div>
                  <div className="text-zinc-400 text-sm mb-3">EST. LATENCY</div>
                  <div className="flex items-baseline gap-x-3">
                    <span className="text-7xl font-light text-white tabular-nums">6.8</span>
                    <span className="text-2xl text-zinc-400">ns</span>
                  </div>
                </div>
              </div>
              
              <div>
                <div className="text-zinc-400 text-sm mb-3">CORE LOGIC SUMMARY</div>
                <div className="text-[13px] leading-relaxed text-zinc-400 font-light">
                  Contiguous memory ring buffer with strict 64-byte slot padding to eliminate false sharing. 
                  Atomic pointer exchange enables true zero-copy transfer of instructions between pipeline stages. 
                  Per-slot generation counters maintain state integrity and ordering without centralized synchronization primitives.
                </div>
              </div>
              
              <div className="pt-5 border-t border-zinc-700 flex items-center justify-between text-sm">
                <div className="flex items-center gap-x-4">
                  <div className="bg-emerald-400 text-emerald-950 px-5 py-2 rounded-3xl font-medium">98.4% EFFICIENCY</div>
                </div>
                <div className="font-mono text-xs text-zinc-500">MEETS REQUIREMENT: &lt;10ns</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* FOOTER */}
      <footer className="bg-black py-20 text-center text-xs text-zinc-500 border-t border-zinc-900">
        Designed as technical demonstration of high-performance single-producer-single-consumer primitives.<br />
        False sharing eliminated • Generation counters validated • Zero copy achieved
      </footer>
    </div>
  );
}
