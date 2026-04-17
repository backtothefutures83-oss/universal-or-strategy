import { Database, Activity, HardDrive, Cpu, Radio, AlignVerticalSpaceAround, ShieldAlert } from 'lucide-react';
import { motion } from 'framer-motion';

export const MultimodalPipes = () => {
  return (
    <div className="space-y-12 pb-24">
      <header>
        <motion.div 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-slate-800 text-slate-300 text-xs font-mono font-bold tracking-widest mb-4 border border-slate-700"
        >
          <Activity className="w-4 h-4 text-blood-500" />
          HURDLE 1
        </motion.div>
        <motion.h2 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="text-3xl lg:text-4xl font-bold tracking-tight text-white flex items-center gap-4"
        >
          Multimodal Pipes <span className="text-slate-500 font-light italic">(The Heavy Flow)</span>
        </motion.h2>
        <motion.p 
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="mt-4 text-lg text-slate-400 max-w-3xl font-light"
        >
          Integrating Pipecat (Real-time Audio) and Vision-Agents (Screen-Capture) alongside thin-packet trade data. The "L1-Sideband" pipe must carry heavy binary data WITHOUT causing "L3 Cache Pollution" for the trade Actors.
        </motion.p>
      </header>

      {/* Interactive Diagram */}
      <motion.div 
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ delay: 0.3 }}
        className="bg-slate-900 border border-slate-800 rounded-2xl p-8 relative overflow-hidden"
      >
        <div 
          className="absolute inset-0 opacity-5" 
          style={{ backgroundImage: "url(\"data:image/svg+xml,%3Csvg width='40' height='40' viewBox='0 0 40 40' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 0h40v40H0V0zm1 1h38v38H1V1z' fill='%23334155' fill-opacity='0.1' fill-rule='evenodd'/%3E%3C/svg%3E\")" }}
        />
        <h3 className="text-lg font-mono text-white mb-8 border-b border-slate-800 pb-4 relative z-10 flex justify-between items-center">
          <span>L1-Sideband Architecture</span>
          <span className="text-xs bg-slate-800 px-2 py-1 rounded text-slate-400">Physics-Grade Logic</span>
        </h3>

        <div className="relative z-10 grid grid-cols-12 gap-6 min-h-[400px]">
          {/* Data Sources */}
          <div className="col-span-3 space-y-8 flex flex-col justify-center">
            <div className="bg-slate-950 border border-slate-800 p-4 rounded-xl shadow-lg relative group">
              <div className="flex items-center gap-3 mb-2">
                <Database className="text-blue-400 w-5 h-5" />
                <span className="text-sm font-bold text-slate-200">Trade Data</span>
              </div>
              <p className="text-xs text-slate-500 font-mono">Thin-packet (8-64 bytes)</p>
              <div className="absolute right-[-12px] top-1/2 -translate-y-1/2 w-3 h-3 bg-blue-500 rounded-full shadow-[0_0_10px_rgba(59,130,246,0.8)]" />
            </div>

            <div className="bg-slate-950 border border-slate-800 p-4 rounded-xl shadow-lg relative group">
              <div className="flex items-center gap-3 mb-2">
                <Radio className="text-blood-500 w-5 h-5" />
                <span className="text-sm font-bold text-slate-200">Multimodal</span>
              </div>
              <p className="text-xs text-slate-500 font-mono">Audio/Frames (MBs)</p>
              <div className="absolute right-[-12px] top-1/2 -translate-y-1/2 w-3 h-3 bg-blood-500 rounded-full shadow-[0_0_10px_rgba(255,42,42,0.8)]" />
            </div>
          </div>

          {/* SPSC Pipes & L1-Sideband */}
          <div className="col-span-6 relative flex items-center justify-center">
            {/* Main L3 Bus */}
            <div className="absolute inset-y-8 left-1/4 right-1/4 border-2 border-dashed border-slate-700/50 rounded-3xl" />
            
            <div className="space-y-16 w-full px-8 relative">
              {/* Trade Pipe */}
              <div className="relative h-12 bg-slate-950 border border-blue-900/30 rounded flex items-center overflow-hidden">
                <div className="absolute left-2 text-xs font-mono text-blue-500 font-bold z-10">MAIN PIPE (L3 Cached)</div>
                {/* Flow Animation */}
                <motion.div 
                  className="absolute h-full w-16 bg-gradient-to-r from-transparent via-blue-500/20 to-transparent"
                  animate={{ x: ['-100%', '500%'] }}
                  transition={{ repeat: Infinity, duration: 1.5, ease: 'linear' }}
                />
              </div>

              {/* Sideband Pipe */}
              <div className="relative h-20 bg-slate-950 border border-blood-900/30 rounded flex flex-col justify-center overflow-hidden">
                <div className="absolute left-2 text-xs font-mono text-blood-500 font-bold z-10 flex items-center gap-2">
                  <ShieldAlert className="w-3 h-3" /> L1-SIDEBAND (Bypass L3)
                </div>
                {/* Flow Animation */}
                <motion.div 
                  className="absolute h-full w-32 bg-gradient-to-r from-transparent via-blood-500/10 to-transparent"
                  animate={{ x: ['-100%', '300%'] }}
                  transition={{ repeat: Infinity, duration: 2, ease: 'linear' }}
                />
                <div className="px-4 mt-4 w-full flex justify-between gap-1">
                  {[...Array(10)].map((_, i) => (
                    <motion.div 
                      key={i} 
                      className="h-2 flex-1 bg-blood-900/50 rounded-sm"
                      animate={{ opacity: [0.3, 1, 0.3] }}
                      transition={{ repeat: Infinity, duration: 1 + (i % 3) * 0.2, delay: i * 0.1 }}
                    />
                  ))}
                </div>
              </div>
            </div>
          </div>

          {/* Trade Actor */}
          <div className="col-span-3 flex items-center">
            <div className="bg-slate-950 border-2 border-slate-800 p-6 rounded-2xl w-full shadow-2xl relative">
              <div className="absolute -left-3 top-1/4 w-3 h-3 bg-blue-500 rounded-full shadow-[0_0_10px_rgba(59,130,246,0.8)]" />
              <div className="absolute -left-3 bottom-1/4 w-3 h-3 bg-blood-500 rounded-full shadow-[0_0_10px_rgba(255,42,42,0.8)]" />
              
              <div className="flex items-center gap-3 mb-6 border-b border-slate-800 pb-4">
                <Cpu className="text-platinum-400 w-8 h-8" />
                <div>
                  <h4 className="font-bold text-white">Trade Actor</h4>
                  <p className="text-xs text-slate-500 font-mono">Shard C5</p>
                </div>
              </div>

              <div className="space-y-4">
                <div className="bg-slate-900 p-3 rounded border border-blue-900/30">
                  <div className="text-xs text-slate-400 font-mono mb-1">L1/L2 Cache</div>
                  <div className="h-1.5 w-full bg-slate-800 rounded-full overflow-hidden">
                    <div className="h-full bg-blue-500 w-[85%]" />
                  </div>
                  <div className="text-[10px] text-blue-400 mt-1 text-right">Hot Trade Data</div>
                </div>

                <div className="bg-slate-900 p-3 rounded border border-blood-900/30">
                  <div className="text-xs text-slate-400 font-mono mb-1">Mmapped DMA Buffers</div>
                  <div className="flex items-center gap-2">
                    <AlignVerticalSpaceAround className="w-4 h-4 text-blood-500" />
                    <span className="text-[10px] text-blood-400">Zero-copy heavy binary ptrs</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </motion.div>

      {/* Code / Solution Explanation */}
      <motion.div 
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.5 }}
        className="grid md:grid-cols-2 gap-8"
      >
        <div className="bg-slate-900 p-6 rounded-xl border border-slate-800 font-mono text-sm shadow-xl">
          <div className="flex justify-between items-center mb-4 pb-2 border-b border-slate-800">
            <span className="text-slate-400">src/ipc/sideband.rs</span>
            <span className="text-xs text-blood-500 bg-blood-900/20 px-2 py-1 rounded">No Copying</span>
          </div>
          <pre className="text-slate-300 overflow-x-auto">
<code className="language-rust">{`// The L1-Sideband implementation
#[repr(C, align(64))] // Cache-line alignment
pub struct SidebandDesc {
    pub magic: u64,
    pub mmap_offset: usize, // Pointer to heavy data
    pub size: usize,
    pub ready_flag: AtomicU8,
}

// Bypassing L3 Cache using Non-Temporal Stores
pub unsafe fn write_heavy_frame(
    dest: *mut SidebandDesc, 
    offset: usize, 
    size: usize
) {
    // NTCA (Non-Temporal Cache Aligned) instruction
    // Streams data directly to memory, bypassing L3
    _mm_stream_si64(
        dest as *mut i64, 
        pack_desc(offset, size)
    );
    _mm_sfence(); // Ensure globally visible
}`}</code>
          </pre>
        </div>

        <div className="space-y-6 flex flex-col justify-center">
          <div>
            <h4 className="text-lg font-bold text-white mb-2 flex items-center gap-2">
              <HardDrive className="w-5 h-5 text-blood-500" />
              Non-Temporal Memory Streaming
            </h4>
            <p className="text-slate-400 text-sm leading-relaxed">
              To prevent heavy video frames and audio blobs from evicting hot trade-data from the L3 cache, we utilize Non-Temporal stores (e.g., <code className="text-blood-400 bg-blood-900/20 px-1 rounded">_mm_stream</code> instructions). 
            </p>
          </div>
          <div>
            <h4 className="text-lg font-bold text-white mb-2 flex items-center gap-2">
              <Database className="w-5 h-5 text-blue-500" />
              Mmapped Pointer Passing
            </h4>
            <p className="text-slate-400 text-sm leading-relaxed">
              The SPSC ring buffer never holds the payload. It only transmits 64-byte aligned descriptors containing offsets to a shared `mmap` Zero-Heap SlabPool. The trade actors read pointers, never payloads.
            </p>
          </div>
        </div>
      </motion.div>
    </div>
  );
};