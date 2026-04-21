import React, { useState, useEffect } from 'react';

interface LatencyMetrics {
  ingress: number;
  tagged: number;
  cache: number;
  total: number;
}

interface RingSlot {
  id: number;
  occupied: boolean;
  tag: number;
}

export default function App() {
  const [metrics, setMetrics] = useState<LatencyMetrics>({
    ingress: 1.1,
    tagged: 1.7,
    cache: 0.9,
    total: 3.7
  });
  
  const [isProcessing, setIsProcessing] = useState(false);
  const [packetCount, setPacketCount] = useState(124872);
  const [activeStage, setActiveStage] = useState<number | null>(null);
  const [ringSlots, setRingSlots] = useState<RingSlot[]>(
    Array.from({ length: 16 }, (_, i) => ({ id: i, occupied: i % 3 === 0, tag: 0xA3 + i }))
  );
  const [taggedValue, setTaggedValue] = useState(0x0001A2B3C4D50000n);
  const [activeThreads, setActiveThreads] = useState<number[]>([]);

  const simulatePacket = () => {
    setIsProcessing(true);
    setActiveStage(0);
    
    // Simulate ingress
    setTimeout(() => {
      setRingSlots(prev => {
        const newSlots = [...prev];
        const writePos = Math.floor(Math.random() * 16);
        newSlots[writePos] = { 
          ...newSlots[writePos], 
          occupied: true, 
          tag: (newSlots[writePos].tag + 1) % 256 
        };
        return newSlots;
      });
      setActiveStage(1);
      
      // Simulate tagged pointer update
      setTimeout(() => {
        setTaggedValue(prev => {
          const mask = BigInt(0xFFFFFFFFFFFF); // 48 bits
          const idx = (Number(prev & mask) + 7) & Number(mask);
          const epoch = (Number((prev >> 48n) & 0xFFFFn) + 1) & 0xFFFF;
          return (BigInt(epoch) << 48n) | BigInt(idx);
        });
        setActiveStage(2);
        
        // Simulate cache guarded access
        setTimeout(() => {
          const randomThreads = Array.from({length: 5}, () => Math.floor(Math.random() * 12));
          setActiveThreads(randomThreads);
          
          setTimeout(() => {
            setPacketCount(prev => prev + Math.floor(Math.random() * 180) + 70);
            setMetrics(prev => ({
              ingress: Math.max(0.8, prev.ingress + (Math.random() - 0.5) * 0.3),
              tagged: Math.max(1.2, prev.tagged + (Math.random() - 0.5) * 0.3),
              cache: Math.max(0.6, prev.cache + (Math.random() - 0.5) * 0.2),
              total: Math.max(3.2, prev.total + (Math.random() - 0.5) * 0.4)
            }));
            setActiveStage(null);
            setActiveThreads([]);
            setIsProcessing(false);
          }, 420);
        }, 380);
      }, 360);
    }, 280);
  };

  const resetDemo = () => {
    setRingSlots(Array.from({ length: 16 }, (_, i) => ({ 
      id: i, 
      occupied: i % 3 === 0, 
      tag: 0xA3 + i 
    })));
    setTaggedValue(0x0001A2B3C4D50000n);
    setActiveThreads([]);
  };

  // Auto simulate
  useEffect(() => {
    const interval = setInterval(() => {
      if (!isProcessing && Math.random() > 0.6) {
        simulatePacket();
      }
    }, 1850);
    return () => clearInterval(interval);
  }, [isProcessing]);

  const getLatencyColor = (lat: number) => {
    return lat < 2 ? 'text-emerald-400' : lat < 4 ? 'text-yellow-400' : 'text-red-400';
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-white overflow-hidden">
      {/* HEADER */}
      <header className="border-b border-zinc-800 bg-zinc-950 sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-8 py-5 flex items-center justify-between">
          <div className="flex items-center gap-x-3">
            <div className="w-9 h-9 rounded-xl bg-emerald-500 flex items-center justify-center text-xl font-bold">⚡</div>
            <div>
              <div className="font-mono text-3xl font-semibold tracking-tighter">NANOPULSE</div>
              <div className="text-[10px] text-emerald-500 -mt-1">5NS PACKET FABRIC</div>
            </div>
          </div>
          
          <div className="flex items-center gap-x-8">
            <div className="flex items-center gap-x-6 text-sm font-mono">
              <div className="flex items-center gap-x-2">
                <div className="w-2 h-2 bg-emerald-400 rounded-full animate-pulse"></div>
                <span>LOCKFREE</span>
              </div>
              <div>48-BIT INDEX</div>
              <div>64B ALIGNED</div>
            </div>
            
            <div className="flex items-center bg-zinc-900 rounded-2xl px-5 py-1.5 border border-zinc-700">
              <div className="text-emerald-400 font-mono text-xl font-medium tabular-nums">
                {metrics.total.toFixed(1)}<span className="text-xs text-zinc-500">ns</span>
              </div>
              <div className="ml-3 text-[10px] leading-none">
                TOTAL<br/>CYCLE
              </div>
            </div>
            
            <button 
              onClick={simulatePacket}
              disabled={isProcessing}
              className="px-6 py-2 bg-white text-zinc-900 hover:bg-emerald-400 hover:text-white transition-all rounded-xl font-medium flex items-center gap-x-2 disabled:opacity-40"
            >
              <span>INJECT PACKET</span>
              <span className="text-lg leading-none">↗</span>
            </button>
          </div>
        </div>
      </header>

      <div className="max-w-7xl mx-auto px-8 py-8">
        {/* PIPELINE OVERVIEW */}
        <div className="mb-12">
          <div className="flex justify-between items-end mb-4">
            <div>
              <div className="uppercase text-emerald-500 text-xs tracking-[2px] font-medium">ZERO-COPY DATA PLANE</div>
              <h1 className="text-5xl font-semibold tracking-tighter">High Frequency<br/>Ingress Pipeline</h1>
            </div>
            <div className="text-right">
              <div className="font-mono text-5xl font-semibold text-white tabular-nums">{packetCount.toLocaleString()}</div>
              <div className="text-xs text-zinc-500">PACKETS PROCESSED</div>
            </div>
          </div>

          {/* Pipeline Flow */}
          <div className="relative h-28 bg-zinc-900 border border-zinc-800 rounded-3xl flex items-center px-8 overflow-hidden">
            {['INGRESS BRIDGE', 'TAGGED PTR', 'CACHE CORE'].map((label, idx) => (
              <React.Fragment key={idx}>
                <div 
                  onClick={() => setActiveStage(idx)}
                  className={`group flex-1 h-20 mx-2 rounded-2xl border flex items-center justify-center relative cursor-pointer transition-all ${activeStage === idx ? 'border-emerald-400 shadow-[0_0_25px_-4px] shadow-emerald-500 scale-[1.03]' : 'border-zinc-700 hover:border-zinc-600'}`}
                >
                  <div className="text-center">
                    <div className="font-mono text-xs text-zinc-400 mb-0.5">STAGE {idx+1}</div>
                    <div className="font-semibold text-lg tracking-tight text-white">{label}</div>
                  </div>
                  
                  {activeStage === idx && (
                    <div className="absolute -top-1 -right-1 w-5 h-5 bg-emerald-400 rounded-full flex items-center justify-center text-[10px] font-bold text-black">◉</div>
                  )}
                </div>
                
                {idx < 2 && (
                  <div className="w-8 h-px bg-gradient-to-r from-transparent via-zinc-700 to-transparent relative">
                    <div className={`absolute h-px w-3 bg-emerald-400 ${activeStage !== null && activeStage > idx ? 'animate-[packetFlow_0.6s_linear_infinite]' : ''}`} style={{top: '0px'}}></div>
                  </div>
                )}
              </React.Fragment>
            ))}
            
            {/* Animated packet indicator */}
            {isProcessing && (
              <div className="absolute left-[18%] top-1/2 -translate-y-1/2 w-4 h-4 bg-white rounded-full shadow-[0_0_30px_10px] shadow-emerald-400 animate-[packetFlow_1.8s_linear_forwards]"></div>
            )}
          </div>
        </div>

        <div className="grid grid-cols-3 gap-6">
          {/* 1. INGRESS BRIDGE */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-8 flex flex-col">
            <div className="flex items-center justify-between mb-6">
              <div>
                <div className="px-3 py-1 text-xs rounded-full bg-teal-950 text-teal-400 inline-block">1</div>
                <h3 className="text-2xl font-semibold mt-3 tracking-tight">INGRESS BRIDGE</h3>
              </div>
              <div className={`font-mono text-xl font-medium ${getLatencyColor(metrics.ingress)}`}>{metrics.ingress.toFixed(1)}ns</div>
            </div>
            
            <div className="text-zinc-400 text-sm leading-relaxed mb-8">
              Preallocated 16-slot memory-mapped ring buffer. Lock-free single producer write using atomic head pointer. 
              Direct socket buffer DMA into fixed memory. No std collections.
            </div>
            
            {/* Ring Visualization */}
            <div className="mb-8">
              <div className="text-[10px] font-mono text-zinc-500 mb-3 tracking-widest">MEMORY RING BUFFER (PREALLOC 4KB)</div>
              <div className="grid grid-cols-8 gap-2">
                {ringSlots.map((slot, i) => (
                  <div 
                    key={i}
                    className={`aspect-square rounded-2xl flex items-center justify-center text-xs font-mono border transition-all relative overflow-hidden
                      ${slot.occupied 
                        ? 'bg-emerald-950 border-emerald-500 shadow-inner' 
                        : 'bg-zinc-950 border-zinc-800'}`}
                  >
                    {slot.occupied && (
                      <>
                        <div className="absolute inset-0 bg-[radial-gradient(#10b981_0.8px,transparent_1px)] [background-size:3px_3px] opacity-30"></div>
                        <div className="z-10 text-emerald-300 text-[13px]">{slot.tag.toString(16).padStart(2,'0')}</div>
                      </>
                    )}
                    <div className="absolute bottom-1 right-1 text-[8px] text-zinc-600 font-medium">{i}</div>
                  </div>
                ))}
              </div>
            </div>
            
            <div className="mt-auto">
              <div className="font-mono text-xs bg-black/60 p-4 rounded-2xl text-emerald-300 overflow-auto max-h-[148px]">
                {`struct IngressRing {
  alignas(64) std::atomic<uint64_t> head {0};
  PacketBuffer buffers[16]; // 256-byte fixed
  uint8_t padding[64]; // prevent false sharing
};`}
              </div>
              <div className="text-[10px] text-center text-zinc-500 mt-4">ZERO ALLOCATION • MMAP RING</div>
            </div>
            
            <button 
              onClick={simulatePacket}
              className="mt-6 w-full py-3 text-xs tracking-widest border border-zinc-700 hover:bg-white hover:text-zinc-900 transition-colors rounded-2xl"
            >
              WRITE TO RING
            </button>
          </div>

          {/* 2. BITWISE TAGGED POINTERS */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-8 flex flex-col">
            <div className="flex items-center justify-between mb-6">
              <div>
                <div className="px-3 py-1 text-xs rounded-full bg-violet-950 text-violet-400 inline-block">2</div>
                <h3 className="text-2xl font-semibold mt-3 tracking-tight">TAGGED POINTERS</h3>
              </div>
              <div className={`font-mono text-xl font-medium ${getLatencyColor(metrics.tagged)}`}>{metrics.tagged.toFixed(1)}ns</div>
            </div>
            
            <div className="text-zinc-400 text-sm leading-relaxed mb-6">
              64-bit pointer with embedded 16-bit epoch counter. 48-bit index into preallocated arena. 
              ABA-proof CAS using epoch bump on every update.
            </div>
            
            {/* Tagged Pointer Visual */}
            <div className="font-mono bg-black p-5 rounded-2xl mb-6 text-sm">
              <div className="flex justify-between text-[10px] text-zinc-500 mb-2">
                <div>48-BIT INDEX</div>
                <div>16-BIT EPOCH</div>
              </div>
              
              <div className="flex gap-px mb-5">
                {Array.from({length: 48}).map((_, i) => (
                  <div key={i} className="flex-1 h-5 bg-blue-600 rounded-l"></div>
                ))}
                {Array.from({length: 16}).map((_, i) => (
                  <div key={i} className="flex-1 h-5 bg-purple-600 rounded-r"></div>
                ))}
              </div>
              
              <div className="text-emerald-400 text-xs mb-1">CURRENT VALUE (HEX)</div>
              <div className="font-semibold text-lg text-white tracking-widest break-all">
                0x{ taggedValue.toString(16).toUpperCase().padStart(16, '0') }
              </div>
            </div>
            
            <div className="flex gap-3 mb-8">
              <button 
                onClick={() => {
                  setTaggedValue(prev => {
                    const idxPart = prev & BigInt(0x0000FFFFFFFFFFFF);
                    const epochPart = ((prev >> 48n) + 1n) & BigInt(0xFFFF);
                    return (epochPart << 48n) | idxPart;
                  });
                }}
                className="flex-1 py-2.5 text-xs border border-violet-400/70 hover:bg-violet-950 rounded-2xl transition-colors"
              >
                PERFORM CAS
              </button>
              
              <button 
                onClick={() => setTaggedValue(0x0001A2B3C4D50000n)}
                className="flex-1 py-2.5 text-xs border border-zinc-700 hover:bg-zinc-800 rounded-2xl transition-colors"
              >
                RESET
              </button>
            </div>
            
            <div className="text-xs text-zinc-400">
              Guarantees ABA-free lock-free stack/queue using generation counter embedded in unused address bits.
            </div>
            
            <div className="mt-auto pt-8 border-t border-zinc-800">
              <div className="font-mono text-xs text-violet-300 bg-zinc-950 p-4 rounded-2xl">
{`uint64_t pack(uint32_t idx, uint16_t epoch) {
  return ((uint64_t)epoch << 48) | (uint64_t)idx;
}

bool cas(TaggedPtr* ptr, uint64_t old, uint64_t next) {
  return __sync_bool_compare_and_swap(ptr, old, next);
}`}
              </div>
            </div>
          </div>

          {/* 3. CACHE CONCURRENCY GUARD */}
          <div className="bg-zinc-900 border border-zinc-800 rounded-3xl p-8 flex flex-col">
            <div className="flex items-center justify-between mb-6">
              <div>
                <div className="px-3 py-1 text-xs rounded-full bg-amber-950 text-amber-400 inline-block">3</div>
                <h3 className="text-2xl font-semibold mt-3 tracking-tight">CACHE GUARD</h3>
              </div>
              <div className={`font-mono text-xl font-medium ${getLatencyColor(metrics.cache)}`}>{metrics.cache.toFixed(1)}ns</div>
            </div>
            
            <div className="text-zinc-400 text-sm leading-relaxed mb-6">
              64-byte aligned structs. Padded to prevent false sharing across 12 hardware threads. 
              Each core operates on its own exclusive cache line.
            </div>
            
            {/* Thread Cache Visual */}
            <div>
              <div className="flex justify-between text-[10px] font-mono mb-2 text-zinc-500">
                <div>THREAD CORES</div>
                <div>64-BYTE LINES</div>
              </div>
              
              <div className="space-y-3">
                {Array.from({ length: 12 }).map((_, i) => (
                  <div key={i} className={`h-6 rounded-xl flex items-center px-3 text-xs font-mono transition-all overflow-hidden border ${activeThreads.includes(i) ? 'border-amber-400 bg-amber-900/60' : 'border-zinc-800 bg-zinc-950'}`}>
                    <div className="w-5 text-center">T{i}</div>
                    <div className="flex-1 h-2.5 mx-4 bg-zinc-800 rounded">
                      <div className={`h-2.5 rounded transition-all ${activeThreads.includes(i) ? 'bg-amber-400 w-[83%]' : 'bg-zinc-700 w-1/3'}`}></div>
                    </div>
                    <div className="text-emerald-400/70 text-[10px]">0x{((0x4000 + i * 64).toString(16)).toUpperCase()}</div>
                  </div>
                ))}
              </div>
            </div>
            
            <div className="mt-auto text-xs font-mono pt-8">
              <div className="bg-black p-4 rounded-2xl text-amber-300 leading-tight">
                struct alignas(64) ThreadState &#123;<br/>
                  uint64_t seq;<br/>
                  uint32_t flags;<br/>
                  uint8_t  pad[48];<br/>
                &#125;;
              </div>
            </div>
            
            <div className="text-[10px] text-amber-400/70 mt-5 text-center">FALSE SHARING FREE • 12x INDEPENDENT LINES</div>
          </div>
        </div>
        
        {/* FOOTER BAR */}
        <div className="mt-12 flex justify-center">
          <div onClick={resetDemo} className="cursor-pointer flex items-center gap-x-2 px-8 py-3 text-xs tracking-[1px] text-zinc-400 hover:text-white transition-colors border border-transparent hover:border-zinc-700 rounded-3xl">
            <span>⟳</span> 
            RESET SIMULATION
          </div>
        </div>
      </div>

      {/* BOTTOM STATUS */}
      <div className="fixed bottom-0 left-0 right-0 h-11 bg-zinc-900 border-t border-zinc-800 flex items-center px-8 text-xs font-mono z-50">
        <div className="flex-1 flex items-center gap-x-5">
          <div className="px-4 py-px bg-emerald-900 text-emerald-400 rounded">LIVE</div>
          <div>NO HEAP ALLOCATIONS • PURE ATOMICS • 4.2GHz AVX2</div>
        </div>
        
        <div className="flex items-center gap-x-8 text-zinc-500">
          <div>INGRESS: {metrics.ingress.toFixed(1)}ns</div>
          <div>TAGGED: {metrics.tagged.toFixed(1)}ns</div>
          <div>CACHE: {metrics.cache.toFixed(1)}ns</div>
        </div>
        
        <div className="flex-1 text-right text-emerald-500">DESIGNED FOR &lt; 5ns END-TO-END</div>
      </div>
    </div>
  );
}
