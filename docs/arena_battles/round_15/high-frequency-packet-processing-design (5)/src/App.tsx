import { useState, useEffect } from 'react';
import { Zap, ArrowRight, Cpu, Shield, Layers, Play, Pause, RotateCcw } from 'lucide-react';

export default function App() {
  const [isProcessing, setIsProcessing] = useState(false);
  const [totalLatency, setTotalLatency] = useState(3.8);
  const [activeSection, setActiveSection] = useState<'ingress' | 'tagged' | 'cache' | null>(null);
  const [ringBuffer, setRingBuffer] = useState<boolean[]>(Array(16).fill(false));
  const [currentTag, setCurrentTag] = useState({ index: 0x2A3F, epoch: 0xB7 });
  const [processedCount, setProcessedCount] = useState(12487);

  const ingestPacket = () => {
    setRingBuffer(prev => {
      const next = [...prev];
      const idx = Math.floor(Math.random() * 16);
      next[idx] = true;
      setTimeout(() => {
        setRingBuffer(r => {
          const n = [...r];
          n[idx] = false;
          return n;
        });
      }, 800);
      return next;
    });

    setTotalLatency(prev => Math.max(2.9, Math.min(4.8, prev + (Math.random() - 0.5) * 0.3)));
    setProcessedCount(prev => prev + 1);
  };

  const simulatePipeline = () => {
    if (isProcessing) return;
    setIsProcessing(true);
    
    let count = 0;
    const interval = setInterval(() => {
      ingestPacket();
      count++;
      if (count > 12) {
        clearInterval(interval);
        setIsProcessing(false);
      }
    }, 120);
  };

  const updateTagDemo = () => {
    setCurrentTag({
      index: Math.floor(Math.random() * 0xFFFFFFFFFF),
      epoch: Math.floor(Math.random() * 0xFFFF)
    });
    setActiveSection('tagged');
    setTimeout(() => setActiveSection(null), 1200);
  };

  const simulateCacheAccess = () => {
    setActiveSection('cache');
    setTimeout(() => {
      setActiveSection(null);
      setTotalLatency(prev => Math.max(3.1, Math.min(4.6, prev)));
    }, 900);
  };

  useEffect(() => {
    const interval = setInterval(() => {
      setTotalLatency(prev => {
        const drift = (Math.random() - 0.5) * 0.08;
        return Math.max(3.4, Math.min(4.6, prev + drift));
      });
    }, 1400);

    return () => clearInterval(interval);
  }, []);

  const LatencyGauge = ({ value }: { value: number }) => (
    <div className="relative w-40 h-40">
      <svg className="w-full h-full -rotate-12" viewBox="0 0 120 120">
        <circle 
          cx="60" cy="60" r="48" 
          fill="none" 
          stroke="#1f2937" 
          strokeWidth="12" 
        />
        <circle 
          cx="60" cy="60" r="48" 
          fill="none" 
          stroke={value < 5 ? "#22c55e" : "#ef4444"} 
          strokeWidth="12" 
          strokeDasharray={`${(value / 8) * 302} 302`}
          strokeLinecap="round"
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <div className="text-5xl font-mono font-bold text-white tracking-tighter">{value.toFixed(1)}</div>
        <div className="text-[10px] font-medium text-emerald-400 -mt-1">NS</div>
        <div className="text-xs text-zinc-400 mt-1">TOTAL CYCLE</div>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-zinc-950 text-white overflow-hidden">
      {/* HEADER */}
      <header className="border-b border-zinc-800 bg-zinc-950/80 backdrop-blur-lg fixed w-full z-50">
        <div className="max-w-screen-2xl mx-auto px-8 py-5 flex items-center justify-between">
          <div className="flex items-center gap-x-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-emerald-500">
              <Zap className="h-5 w-5 text-black" />
            </div>
            <div>
              <div className="font-mono text-2xl font-semibold tracking-[-2px]">NANOPIPE</div>
              <div className="text-[10px] text-emerald-400 -mt-1 font-medium">5NS PACKET FABRIC</div>
            </div>
          </div>
          
          <div className="flex items-center gap-x-8">
            <div className="flex items-center gap-x-6 text-sm">
              <div onClick={() => setActiveSection('ingress')} className={`cursor-pointer flex items-center gap-x-1.5 px-4 py-1.5 rounded-2xl transition-all ${activeSection === 'ingress' ? 'bg-white text-zinc-900' : 'hover:bg-zinc-900'}`}>
                <Layers className="h-4 w-4" /> INGRESS
              </div>
              <div onClick={() => setActiveSection('tagged')} className={`cursor-pointer flex items-center gap-x-1.5 px-4 py-1.5 rounded-2xl transition-all ${activeSection === 'tagged' ? 'bg-white text-zinc-900' : 'hover:bg-zinc-900'}`}>
                <Shield className="h-4 w-4" /> TAGGED PTR
              </div>
              <div onClick={() => setActiveSection('cache')} className={`cursor-pointer flex items-center gap-x-1.5 px-4 py-1.5 rounded-2xl transition-all ${activeSection === 'cache' ? 'bg-white text-zinc-900' : 'hover:bg-zinc-900'}`}>
                <Cpu className="h-4 w-4" /> CACHE GUARD
              </div>
            </div>

            <div className="flex items-center gap-x-3 bg-zinc-900 rounded-3xl px-5 py-2 border border-zinc-700">
              <div className="text-emerald-400">
                <div className="text-[22px] font-mono font-semibold tabular-nums">{processedCount.toLocaleString()}</div>
                <div className="text-[9px] -mt-1 opacity-60">PKTS PROCESSED</div>
              </div>
              <div className="h-7 w-px bg-zinc-700" />
              <LatencyGauge value={totalLatency} />
            </div>
          </div>

          <div className="flex items-center gap-x-2">
            <button
              onClick={simulatePipeline}
              disabled={isProcessing}
              className="flex items-center gap-x-2 bg-white hover:bg-white/90 disabled:bg-zinc-700 text-zinc-950 px-6 py-2.5 rounded-2xl text-sm font-semibold transition-all active:scale-[0.985]"
            >
              {isProcessing ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
              {isProcessing ? 'RUNNING' : 'SIMULATE PIPELINE'}
            </button>
            
            <button 
              onClick={() => {
                setRingBuffer(Array(16).fill(false));
                setProcessedCount(12487);
              }}
              className="h-10 w-10 flex items-center justify-center rounded-2xl border border-zinc-700 hover:bg-zinc-900"
            >
              <RotateCcw className="h-4 w-4" />
            </button>
          </div>
        </div>
      </header>

      <div className="pt-24 max-w-screen-2xl mx-auto px-8">
        {/* PIPELINE OVERVIEW */}
        <div className="mb-12">
          <div className="flex justify-between items-end mb-6">
            <div>
              <div className="uppercase text-emerald-500 text-xs tracking-[3px] font-mono mb-1">LIVE HIGH FREQUENCY FABRIC</div>
              <h1 className="text-6xl font-semibold tracking-tighter">Zero Allocation<br />Packet Pipeline</h1>
            </div>
            <div className="text-right">
              <div className="inline-flex items-center gap-x-2 px-5 py-2 rounded-3xl bg-zinc-900 border border-emerald-900">
                <div className="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></div>
                <span className="font-mono text-sm">4.2ns AVG • 12 THREADS • 64B ALIGNED</span>
              </div>
            </div>
          </div>

          {/* FLOW DIAGRAM */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-8 mb-10">
            <div className="flex items-center justify-between relative">
              {/* INGRESS */}
              <div 
                onClick={() => setActiveSection('ingress')}
                className={`group relative w-56 h-56 rounded-3xl border-2 flex flex-col items-center justify-center cursor-pointer transition-all ${activeSection === 'ingress' ? 'border-emerald-400 scale-105 shadow-2xl shadow-emerald-500/30' : 'border-zinc-700 hover:border-zinc-600'}`}
              >
                <div className="text-emerald-400 mb-4">
                  <Layers className="h-10 w-10" />
                </div>
                <div className="text-xl font-semibold">INGRESS BRIDGE</div>
                <div className="text-zinc-400 text-xs mt-1 font-mono">RING • 16-SLOT</div>
                
                <div className="absolute -bottom-2 left-1/2 -translate-x-1/2 flex gap-1">
                  {ringBuffer.map((occupied, i) => (
                    <div key={i} className={`w-1.5 h-1.5 rounded-full transition-all ${occupied ? 'bg-emerald-400 scale-125' : 'bg-zinc-700'}`} />
                  ))}
                </div>
              </div>

              <ArrowRight className="h-8 w-8 text-zinc-500 mx-4" />

              {/* TAGGED POINTER */}
              <div 
                onClick={() => setActiveSection('tagged')}
                className={`group relative w-56 h-56 rounded-3xl border-2 flex flex-col items-center justify-center cursor-pointer transition-all ${activeSection === 'tagged' ? 'border-emerald-400 scale-105 shadow-2xl shadow-emerald-500/30' : 'border-zinc-700 hover:border-zinc-600'}`}
              >
                <div className="font-mono text-[42px] font-bold text-amber-400 tabular-nums tracking-tighter mb-1">
                  {currentTag.index.toString(16).toUpperCase().padStart(5, '0')}
                </div>
                <div className="text-[10px] font-medium text-amber-300/70">48-BIT INDEX + 16-BIT EPOCH</div>
                <div className="text-xs text-zinc-400 mt-6">TAGGED PTR</div>
                <div className="absolute bottom-6 text-[9px] font-mono bg-black/60 px-3 py-0.5 rounded">ABA SAFE</div>
              </div>

              <ArrowRight className="h-8 w-8 text-zinc-500 mx-4" />

              {/* CACHE GUARD */}
              <div 
                onClick={() => setActiveSection('cache')}
                className={`group relative w-56 h-56 rounded-3xl border-2 flex flex-col items-center justify-center cursor-pointer transition-all ${activeSection === 'cache' ? 'border-emerald-400 scale-105 shadow-2xl shadow-emerald-500/30' : 'border-zinc-700 hover:border-zinc-600'}`}
              >
                <div className="grid grid-cols-4 gap-1 mb-6">
                  {Array.from({ length: 12 }).map((_, i) => (
                    <div key={i} className={`w-4 h-4 rounded bg-sky-400/80`} style={{ animationDelay: i * 40 + 'ms' }} />
                  ))}
                </div>
                <div className="text-xl font-semibold text-sky-400">CACHE CONCURRENCY</div>
                <div className="text-xs text-zinc-400">64B PADDED • FALSE SHARING FREE</div>
              </div>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-3 gap-6">
          {/* 1. INGRESS BRIDGE */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-8">
            <div className="flex justify-between items-start mb-8">
              <div>
                <div className="uppercase tracking-widest text-xs text-teal-400 font-medium">01</div>
                <h3 className="text-2xl font-semibold">INGRESS BRIDGE</h3>
              </div>
              <div className="px-4 py-1 text-xs font-mono rounded-2xl bg-teal-950 text-teal-300">1.1ns</div>
            </div>
            
            <div className="mb-8 font-mono text-xs leading-relaxed bg-black/40 p-6 rounded-2xl border border-zinc-800">
              Preallocated 64KB memory mapped ring.<br />
              Lock-free head/tail using atomic 32-bit indices.<br />
              Direct socket DMA into fixed buffers.
            </div>

            <div className="h-56 relative flex items-center justify-center bg-zinc-950 rounded-2xl overflow-hidden border border-zinc-800">
              <div className="relative w-48 h-48">
                {Array.from({ length: 16 }).map((_, i) => {
                  const angle = (i * 22.5) - 90;
                  const x = 96 + Math.cos((angle * Math.PI) / 180) * 68;
                  const y = 96 + Math.sin((angle * Math.PI) / 180) * 68;
                  const occupied = ringBuffer[i];
                  return (
                    <div
                      key={i}
                      className={`absolute w-7 h-7 rounded-2xl flex items-center justify-center text-[10px] font-bold transition-all duration-300 border ${occupied ? 'bg-emerald-400 text-black border-emerald-300 scale-125 shadow-[0_0_20px_#10b981]' : 'bg-zinc-900 border-zinc-600'}`}
                      style={{ left: `${x - 14}px`, top: `${y - 14}px` }}
                    >
                      {i}
                    </div>
                  );
                })}
                <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 text-center">
                  <div className="text-[10px] text-zinc-500">MEMORY RING</div>
                  <div className="text-emerald-400 font-mono text-xl font-bold">0xF3A0_0000</div>
                </div>
              </div>
            </div>

            <button 
              onClick={ingestPacket}
              className="mt-6 w-full py-4 bg-white text-zinc-950 rounded-2xl text-sm font-semibold flex items-center justify-center gap-x-2 active:scale-[0.985] transition-all"
            >
              <ArrowRight className="h-4 w-4" /> INJECT PACKET
            </button>

            <div className="mt-8 text-[10px] text-zinc-400">
              Mechanism: mmap + atomic fetch_add on head index. No queues. Pure pointer arithmetic.
              <div className="text-emerald-400 mt-3 font-medium">+1.1ns</div>
            </div>
          </div>

          {/* 2. BITWISE TAGGED POINTERS */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-8">
            <div className="flex justify-between items-start mb-8">
              <div>
                <div className="uppercase tracking-widest text-xs text-amber-400 font-medium">02</div>
                <h3 className="text-2xl font-semibold">TAGGED POINTERS</h3>
              </div>
              <div className="px-4 py-1 text-xs font-mono rounded-2xl bg-amber-950 text-amber-300">1.4ns</div>
            </div>
            
            <div className="font-mono text-xs bg-black p-5 rounded-2xl border border-amber-900/40 mb-8">
              uint64_t ptr = (uint64_t)index &lt;&lt; 16 | epoch;<br />
              atomic_compare_exchange(&amp;slot, old, new_tag);
            </div>

            <div className="bg-zinc-950 rounded-2xl p-6 mb-8 font-mono text-sm border border-zinc-700">
              <div className="flex justify-between text-[10px] mb-3 text-zinc-400">
                <div>48-BIT INDEX</div>
                <div>16-BIT EPOCH</div>
              </div>
              <div className="h-8 bg-zinc-900 rounded flex items-center px-3 text-emerald-300 text-xs mb-6 border border-dashed border-zinc-700">
                0x{currentTag.index.toString(16).padStart(10, '0')} • {currentTag.epoch.toString(16).padStart(4, '0')}
              </div>
              
              <div 
                onClick={updateTagDemo}
                className="cursor-pointer text-center py-4 text-xs tracking-widest border border-dashed border-amber-400 hover:bg-amber-900/30 rounded-2xl text-amber-400 transition-colors"
              >
                GENERATE NEW TAGGED PTR
              </div>
            </div>

            <div className="text-[10px] leading-snug text-zinc-400">
              64-bit atomic with embedded generation counter. 
              Prevents ABA in lock-free MPMC ring without heap allocations.
              <div className="text-amber-400 mt-4 font-medium">+1.4ns</div>
            </div>
          </div>

          {/* 3. CACHE CONCURRENCY GUARD */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-8">
            <div className="flex justify-between items-start mb-8">
              <div>
                <div className="uppercase tracking-widest text-xs text-sky-400 font-medium">03</div>
                <h3 className="text-2xl font-semibold">CACHE GUARD</h3>
              </div>
              <div className="px-4 py-1 text-xs font-mono rounded-2xl bg-sky-950 text-sky-300">1.3ns</div>
            </div>

            <div className="mb-6">
              <div className="text-xs text-sky-300 font-medium mb-3">12 THREADS • NO FALSE SHARING</div>
              <div className="grid grid-cols-6 gap-2">
                {Array.from({ length: 12 }).map((_, i) => (
                  <div key={i} className="h-10 bg-zinc-800 rounded-xl flex items-center justify-center text-[10px] font-mono border border-sky-800">
                    T{i}
                  </div>
                ))}
              </div>
            </div>

            <div className="bg-[#111] p-5 rounded-2xl text-[10px] font-mono text-sky-200 border border-sky-900">
              struct alignas(64) ThreadLocalState &#123;<br />
                uint64_t head;<br />
                uint64_t tail;<br />
                uint64_t padding[5];<br />
              &#125;;
            </div>

            <button 
              onClick={simulateCacheAccess}
              className="mt-8 w-full py-4 border border-sky-600 hover:bg-sky-950 rounded-2xl text-sm font-medium flex items-center justify-center gap-x-2"
            >
              <Cpu className="h-4 w-4" /> SIMULATE MULTI-THREAD ACCESS
            </button>

            <div className="mt-8 text-xs text-zinc-400">
              64-byte alignment prevents cache line contention between cores.<br />
              Each thread gets its own cache line.
              <div className="text-sky-400 mt-3 font-medium">+1.3ns</div>
            </div>
          </div>
        </div>

        {/* FOOTER BAR */}
        <div className="mt-16 border-t border-zinc-800 pt-8 pb-12 flex items-center justify-between text-xs font-mono text-zinc-500">
          <div>ZERO HEAP • ATOMIC ONLY • MEMORY MAPPED BUFFERS</div>
          <div className="flex items-center gap-x-5">
            <div>ESTIMATED THROUGHPUT: <span className="text-emerald-400">38.4 Mpps</span></div>
            <div className="w-px h-3 bg-zinc-700" />
            <div>ARCH: x86-64 • AVX2</div>
          </div>
          <div>DEMO VISUALIZER — NOT REAL C#</div>
        </div>
      </div>
    </div>
  );
}
