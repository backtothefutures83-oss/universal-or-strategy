import { Activity, ShieldAlert, Cpu, PowerOff, Zap, RefreshCw, Layers } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { useState, useEffect } from 'react';

export const JitterFreeRedundancy = () => {
  const [isStalled, setIsStalled] = useState(false);
  const [takeoverComplete, setTakeoverComplete] = useState(false);

  useEffect(() => {
    let timeout: NodeJS.Timeout;
    if (isStalled) {
      timeout = setTimeout(() => {
        setTakeoverComplete(true);
      }, 2000); // Simulate 2 microseconds (scaled to 2 seconds for visual)
    } else {
      setTakeoverComplete(false);
    }
    return () => clearTimeout(timeout);
  }, [isStalled]);

  return (
    <div className="space-y-12 pb-24">
      <header>
        <div className="flex justify-between items-start">
          <div>
            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-slate-800 text-slate-300 text-xs font-mono font-bold tracking-widest mb-4 border border-slate-700"
            >
              <ShieldAlert className="w-4 h-4 text-blood-500" />
              HURDLE 2
            </motion.div>
            <motion.h2 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 }}
              className="text-3xl lg:text-4xl font-bold tracking-tight text-white flex items-center gap-4"
            >
              Jitter-Free Redundancy <span className="text-slate-500 font-light italic">(Mirror Takeover)</span>
            </motion.h2>
          </div>
          <button 
            onClick={() => setIsStalled(!isStalled)}
            className={`px-6 py-3 rounded-lg font-mono font-bold flex items-center gap-2 transition-all shadow-lg ${
              isStalled 
                ? 'bg-slate-800 text-slate-400 hover:bg-slate-700' 
                : 'bg-blood-600 text-white hover:bg-blood-500 hover:shadow-[0_0_20px_rgba(255,42,42,0.4)]'
            }`}
          >
            {isStalled ? <RefreshCw className="w-5 h-5" /> : <PowerOff className="w-5 h-5" />}
            {isStalled ? 'Reset Simulation' : 'Trigger NMI (Stall C5)'}
          </button>
        </div>
        <motion.p 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="mt-4 text-lg text-slate-400 max-w-3xl font-light"
        >
          If an Actor Shard (C5) experiences a Hardware Stall (NMI), the Mirror Node (C11) must detect and "Hot-Inject" its state into the Egress pipe within 2.00µs, maintaining 100% SPSC.
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
        
        {/* Status Overlay */}
        <div className="absolute top-8 right-8 flex flex-col items-end gap-2 z-20">
          <div className="bg-slate-950 border border-slate-800 px-4 py-2 rounded-lg font-mono text-sm flex items-center gap-3">
            <span className="text-slate-500">SYSTEM STATE:</span>
            {isStalled ? (
              takeoverComplete ? (
                <span className="text-blue-500 font-bold flex items-center gap-2"><ShieldAlert className="w-4 h-4"/> RECOVERED via C11</span>
              ) : (
                <span className="text-blood-500 font-bold animate-pulse flex items-center gap-2"><Activity className="w-4 h-4"/> NMI DETECTED...</span>
              )
            ) : (
              <span className="text-green-500 font-bold flex items-center gap-2"><Zap className="w-4 h-4"/> OPTIMAL (C5 Active)</span>
            )}
          </div>
          
          <AnimatePresence>
            {isStalled && (
              <motion.div 
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 20 }}
                className="bg-blood-900/20 border border-blood-500/50 px-4 py-2 rounded-lg font-mono text-xs text-blood-400"
              >
                Time: {takeoverComplete ? '1.87 µs' : '< 1.00 µs'}
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        <div className="relative z-10 grid grid-cols-12 gap-8 mt-12">
          {/* C5 Primary Node */}
          <div className="col-span-4 relative">
            <motion.div 
              animate={isStalled && !takeoverComplete ? { x: [-5, 5, -5, 5, 0] } : {}}
              transition={{ duration: 0.5 }}
              className={`border-2 p-6 rounded-2xl h-64 relative flex flex-col items-center justify-center transition-all duration-500 ${
                isStalled 
                  ? 'bg-slate-950 border-blood-900/50 opacity-50 grayscale' 
                  : 'bg-slate-900 border-blue-500/50 shadow-[0_0_30px_rgba(59,130,246,0.15)]'
              }`}
            >
              <Cpu className={`w-16 h-16 mb-4 ${isStalled ? 'text-slate-700' : 'text-blue-400'}`} />
              <h3 className="text-xl font-bold text-white font-mono">Shard C5</h3>
              <p className="text-slate-500 font-mono text-sm">Primary Actor</p>
              
              {!isStalled && (
                <motion.div 
                  className="absolute -right-4 top-1/2 w-4 h-4 bg-blue-500 rounded-full"
                  animate={{ scale: [1, 1.5, 1], opacity: [0.5, 1, 0.5] }}
                  transition={{ repeat: Infinity, duration: 1 }}
                />
              )}
            </motion.div>
          </div>

          {/* Atomic Pointer Multiplexer */}
          <div className="col-span-4 flex items-center justify-center relative">
            <div className="flex flex-col items-center w-full">
              <div className="text-xs font-mono text-slate-500 mb-4 bg-slate-950 px-3 py-1 rounded border border-slate-800">Atomic Egress Mux</div>
              
              <div className="w-full h-32 border border-slate-700 rounded-xl relative overflow-hidden bg-slate-950/50 flex flex-col justify-around py-4 px-6">
                <div className="flex justify-between items-center w-full relative z-10">
                  <div className={`w-3 h-3 rounded-full ${!takeoverComplete ? 'bg-blue-500 shadow-[0_0_10px_rgba(59,130,246,0.8)]' : 'bg-slate-700'}`} />
                  <div className={`h-[2px] w-full mx-2 ${!takeoverComplete ? 'bg-blue-500/50' : 'bg-slate-800'}`} />
                  <div className={`w-3 h-3 rounded-full ${!takeoverComplete ? 'bg-blue-500' : 'bg-slate-700'}`} />
                </div>
                
                <div className="flex justify-between items-center w-full relative z-10">
                  <div className={`w-3 h-3 rounded-full ${takeoverComplete ? 'bg-blood-500 shadow-[0_0_10px_rgba(255,42,42,0.8)]' : 'bg-slate-700'}`} />
                  <div className={`h-[2px] w-full mx-2 ${takeoverComplete ? 'bg-blood-500/50' : 'bg-slate-800'}`} />
                  <div className={`w-3 h-3 rounded-full ${takeoverComplete ? 'bg-blood-500' : 'bg-slate-700'}`} />
                </div>

                {/* SPSC Egress Pipe */}
                <div className="absolute right-0 top-0 bottom-0 w-8 border-l border-slate-700 bg-slate-900 flex items-center justify-center">
                  <div className={`w-1 h-16 rounded-full ${takeoverComplete ? 'bg-blood-500' : 'bg-blue-500'}`} />
                </div>
              </div>
            </div>
          </div>

          {/* C11 Mirror Node */}
          <div className="col-span-4 relative">
            <div className={`border-2 p-6 rounded-2xl h-64 relative flex flex-col items-center justify-center transition-all duration-500 ${
              takeoverComplete 
                ? 'bg-slate-900 border-blood-500/50 shadow-[0_0_30px_rgba(255,42,42,0.15)]' 
                : 'bg-slate-950 border-slate-800'
            }`}>
              <Layers className={`w-16 h-16 mb-4 ${takeoverComplete ? 'text-blood-400' : 'text-slate-600'}`} />
              <h3 className="text-xl font-bold text-white font-mono">Shard C11</h3>
              <p className="text-slate-500 font-mono text-sm">Mirror Node</p>
              
              {takeoverComplete && (
                <motion.div 
                  className="absolute -left-4 bottom-1/4 w-4 h-4 bg-blood-500 rounded-full"
                  animate={{ scale: [1, 1.5, 1], opacity: [0.5, 1, 0.5] }}
                  transition={{ repeat: Infinity, duration: 1 }}
                />
              )}
            </div>
          </div>
        </div>
      </motion.div>

      {/* Code Explanation */}
      <motion.div 
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.5 }}
        className="grid md:grid-cols-2 gap-8"
      >
        <div className="space-y-6 flex flex-col justify-center">
          <div>
            <h4 className="text-lg font-bold text-white mb-2 flex items-center gap-2">
              <RefreshCw className="w-5 h-5 text-blood-500" />
              Egress Producer Pointer Swap
            </h4>
            <p className="text-slate-400 text-sm leading-relaxed">
              To remain 100% SPSC (Single Producer, Single Consumer), C5 and C11 do not write to the same buffer. Instead, the Consumer holds an <code className="text-blood-400 font-mono bg-blood-900/20 px-1 rounded">AtomicPtr</code> to the active Producer's Ring Buffer.
            </p>
          </div>
          <div>
            <h4 className="text-lg font-bold text-white mb-2 flex items-center gap-2">
              <Activity className="w-5 h-5 text-blue-500" />
              Dead-Man's Switch via TSC
            </h4>
            <p className="text-slate-400 text-sm leading-relaxed">
              C11 monitors C5's heartbeat using the CPU's Time Stamp Counter (RDTSC). If C5 misses its write-barrier within ~1.5µs, C11 atomically sets the Consumer's active pointer to itself. No Mutexes, just a single CAS operation.
            </p>
          </div>
        </div>

        <div className="bg-slate-900 p-6 rounded-xl border border-slate-800 font-mono text-sm shadow-xl">
          <div className="flex justify-between items-center mb-4 pb-2 border-b border-slate-800">
            <span className="text-slate-400">src/ipc/mirror.rs</span>
            <span className="text-xs text-blood-500 bg-blood-900/20 px-2 py-1 rounded">Physics-Grade Logic</span>
          </div>
          <pre className="text-slate-300 overflow-x-auto">
<code className="language-rust">{`// Executed by Mirror Node (C11)
pub fn monitor_and_takeover(
    c5_heartbeat: &AtomicU64,
    egress_mux: &AtomicPtr<RingBuffer>
) {
    let mut last_tsc = c5_heartbeat.load(Ordering::Acquire);
    
    loop {
        // CPU Time Stamp Counter
        let current_tsc = unsafe { _rdtsc() };
        let c5_tsc = c5_heartbeat.load(Ordering::Acquire);
        
        // Approx 2.00µs converted to CPU Cycles
        if (current_tsc - c5_tsc) > MAX_STALL_CYCLES {
            // NMI Detected on C5! Hot-Inject C11.
            let success = egress_mux.compare_exchange(
                c5_ring_ptr,
                c11_ring_ptr, // Mapped Pointer Passing
                Ordering::Release,
                Ordering::Relaxed
            );
            
            if success.is_ok() { break; /* Took over */ }
        }
    }
}`}</code>
          </pre>
        </div>
      </motion.div>
    </div>
  );
};