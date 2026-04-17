import { Cpu, Zap, AlignVerticalSpaceAround, Server, Hash } from 'lucide-react';
import { motion } from 'framer-motion';

export const AtomicConstant = () => {
  return (
    <div className="space-y-12 pb-24">
      <header>
        <motion.div 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-slate-800 text-slate-300 text-xs font-mono font-bold tracking-widest mb-4 border border-slate-700"
        >
          <Cpu className="w-4 h-4 text-blood-500" />
          HURDLE 3
        </motion.div>
        <motion.h2 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="text-3xl lg:text-4xl font-bold tracking-tight text-white flex items-center gap-4"
        >
          The Atomic Constant <span className="text-slate-500 font-light italic">(1µs Gate)</span>
        </motion.h2>
        <motion.p 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="mt-4 text-lg text-slate-400 max-w-3xl font-light"
        >
          Achieving a sub-1µs total logic-pass by mapping the Memory-Store-Barrier (<span className="text-blood-400 font-mono">SFENCE/LFENCE</span>) and Spin-Poll detection to the exact topology of the Intel/AMD L3 Ring-Bus architecture.
        </motion.p>
      </header>

      {/* Interactive Diagram */}
      <motion.div 
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ delay: 0.3 }}
        className="bg-slate-900 border border-slate-800 rounded-2xl p-8 relative overflow-hidden min-h-[500px]"
      >
        <div 
          className="absolute inset-0 opacity-5" 
          style={{ backgroundImage: "url(\"data:image/svg+xml,%3Csvg width='40' height='40' viewBox='0 0 40 40' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 0h40v40H0V0zm1 1h38v38H1V1z' fill='%23334155' fill-opacity='0.1' fill-rule='evenodd'/%3E%3C/svg%3E\")" }}
        />
        
        <div className="relative z-10 grid grid-cols-1 md:grid-cols-2 gap-12 mt-4">
          {/* Hardware Ring Bus Simulation */}
          <div className="flex flex-col items-center justify-center relative bg-slate-950 p-6 rounded-2xl border border-slate-800 shadow-[0_0_50px_rgba(0,0,0,0.5)]">
            <h3 className="text-white font-mono font-bold mb-6 flex items-center gap-2">
              <Server className="w-5 h-5 text-blue-500" />
              L3 Ring-Bus Architecture
            </h3>
            
            <div className="relative w-64 h-64 border-4 border-slate-800 rounded-full flex items-center justify-center">
              {/* Spinning Token (Cache Line Invalidates) */}
              <motion.div 
                className="absolute w-4 h-4 bg-blood-500 rounded-full shadow-[0_0_15px_rgba(255,42,42,0.8)]"
                animate={{ rotate: 360 }}
                style={{ transformOrigin: '0 128px' }}
                transition={{ repeat: Infinity, duration: 1, ease: "linear" }}
              />

              {/* Cores on Ring */}
              {[0, 1, 2, 3].map((i) => (
                <div 
                  key={i}
                  className="absolute w-12 h-12 bg-slate-900 border-2 border-slate-700 rounded-lg flex items-center justify-center font-mono text-xs font-bold text-slate-300"
                  style={{
                    transform: `rotate(${i * 90}deg) translateY(-128px) rotate(-${i * 90}deg)`
                  }}
                >
                  Core {i}
                </div>
              ))}
              
              <div className="text-center">
                <div className="text-2xl font-mono text-blood-500 font-bold tracking-widest animate-pulse">0.82 µs</div>
                <div className="text-[10px] text-slate-500 uppercase mt-1">Constant Pass</div>
              </div>
            </div>

            <div className="mt-8 grid grid-cols-2 gap-4 w-full text-xs font-mono">
              <div className="bg-slate-900 p-2 rounded border border-slate-800 text-center text-slate-400">
                <span className="text-blue-500 block mb-1">Spin-Poll</span>
                <span className="bg-slate-950 px-2 py-0.5 rounded">PAUSE inst</span>
              </div>
              <div className="bg-slate-900 p-2 rounded border border-slate-800 text-center text-slate-400">
                <span className="text-blood-500 block mb-1">Barrier</span>
                <span className="bg-slate-950 px-2 py-0.5 rounded">SFENCE</span>
              </div>
            </div>
          </div>

          {/* Logic Flow */}
          <div className="flex flex-col justify-center space-y-6">
            <div className="bg-slate-950 border border-slate-800 p-6 rounded-xl relative group hover:border-blue-500/50 transition-colors">
              <div className="flex items-center gap-3 mb-2">
                <AlignVerticalSpaceAround className="text-blue-400 w-5 h-5" />
                <span className="text-sm font-bold text-white font-mono">1. Spin-Poll Optimization</span>
              </div>
              <p className="text-slate-400 text-sm leading-relaxed mb-4">
                Instead of hammering the memory bus with reads, the consumer executes a <code className="text-slate-300 bg-slate-800 px-1 rounded">PAUSE</code> or <code className="text-slate-300 bg-slate-800 px-1 rounded">_mm_pause()</code> instruction. This prevents pipeline flushes when exiting the spin-loop and reduces power draw on the ring-bus.
              </p>
              <div className="h-1 w-full bg-slate-800 rounded-full overflow-hidden">
                <motion.div className="h-full bg-blue-500" initial={{ width: 0 }} animate={{ width: '100%' }} transition={{ duration: 1, repeat: Infinity }} />
              </div>
            </div>

            <div className="bg-slate-950 border border-slate-800 p-6 rounded-xl relative group hover:border-blood-500/50 transition-colors">
              <div className="flex items-center gap-3 mb-2">
                <Hash className="text-blood-500 w-5 h-5" />
                <span className="text-sm font-bold text-white font-mono">2. Memory-Store-Barrier</span>
              </div>
              <p className="text-slate-400 text-sm leading-relaxed mb-4">
                After writing the payload, an <code className="text-blood-400 bg-blood-900/20 px-1 rounded">SFENCE</code> (Store Fence) guarantees the data is globally visible before setting the "Ready" flag. The Consumer sees the flag and executes an <code className="text-blood-400 bg-blood-900/20 px-1 rounded">LFENCE</code> (Load Fence) to prevent speculative execution reads.
              </p>
              <div className="h-1 w-full bg-slate-800 rounded-full overflow-hidden">
                <motion.div className="h-full bg-blood-500" initial={{ width: 0 }} animate={{ width: '100%' }} transition={{ duration: 0.5, repeat: Infinity }} />
              </div>
            </div>
            
            <div className="bg-blood-900/10 border border-blood-900/30 p-4 rounded-xl flex items-start gap-4">
              <Zap className="w-8 h-8 text-blood-500 shrink-0" />
              <div>
                <h4 className="text-white font-bold text-sm mb-1">100% SPSC Constraint</h4>
                <p className="text-slate-400 text-xs leading-relaxed">
                  No Mutexes. Exclusive thread ownership on pinned cores. Core 0 writes, Core 1 reads. The hardware cache-coherency protocol handles the rest.
                </p>
              </div>
            </div>
          </div>
        </div>
      </motion.div>

      {/* Code Snippet */}
      <motion.div 
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.5 }}
      >
        <div className="bg-slate-900 p-6 rounded-xl border border-slate-800 font-mono text-sm shadow-xl">
          <div className="flex justify-between items-center mb-4 pb-2 border-b border-slate-800">
            <span className="text-slate-400">src/ipc/atomic.rs</span>
            <span className="text-xs text-blood-500 bg-blood-900/20 px-2 py-1 rounded">1µs Gate</span>
          </div>
          <pre className="text-slate-300 overflow-x-auto">
<code className="language-rust">{`// Physics-Grade SPSC Ring Buffer Consumer
pub fn poll_next(&mut self) -> Option<&Payload> {
    let flag_ptr = &self.slots[self.tail].ready_flag;
    
    // 1. Spin-Poll mapped to Ring-Bus
    let mut attempts = 0;
    while flag_ptr.load(Ordering::Relaxed) == 0 {
        if attempts > SPIN_LIMIT { return None; }
        
        // Emits 'pause' on x86 to avoid pipeline flush
        std::hint::spin_loop(); 
        attempts += 1;
    }

    // 2. Load Barrier (LFENCE)
    // Prevents speculative execution from reading payload before flag is seen
    atomic::fence(Ordering::Acquire);
    
    let payload = unsafe { &*self.slots[self.tail].data.as_ptr() };
    self.tail = (self.tail + 1) % RING_SIZE;
    
    Some(payload)
}`}</code>
          </pre>
        </div>
      </motion.div>
    </div>
  );
};