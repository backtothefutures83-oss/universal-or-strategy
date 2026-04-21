import { useMemo } from 'react';
import { motion } from 'framer-motion';

export const PipeVisualizer: React.FC = () => {
  const pipes = useMemo(() => Array.from({ length: 6 }), []);
  
  return (
    <div className="relative h-64 w-full bg-black/40 rounded-lg border border-cyan-900/50 overflow-hidden backdrop-blur-sm">
      <div className="absolute inset-0 flex flex-col justify-around p-4">
        {pipes.map((_, i) => (
          <div key={i} className="relative h-1 w-full bg-cyan-950/30 rounded-full overflow-hidden">
            <motion.div
              initial={{ x: '-100%' }}
              animate={{ x: '100%' }}
              transition={{
                duration: 0.5 + Math.random() * 0.5,
                repeat: Infinity,
                ease: "linear"
              }}
              className="absolute inset-0 bg-cyan-400 shadow-[0_0_10px_rgba(34,211,238,0.8)]"
            />
          </div>
        ))}
      </div>
      
      {/* Overlay labels */}
      <div className="absolute inset-0 flex justify-between items-center px-4 pointer-events-none">
        <div className="text-[10px] font-mono text-cyan-500 uppercase tracking-tighter">Producer_Core[0..11]</div>
        <div className="text-[10px] font-mono text-cyan-400 font-bold bg-cyan-950/80 px-2 py-1 border border-cyan-500/30 rounded">ZERO-COPY TUNNEL</div>
        <div className="text-[10px] font-mono text-cyan-500 uppercase tracking-tighter">Consumer_Core[0..11]</div>
      </div>

      {/* Grid effect */}
      <div className="absolute inset-0 pointer-events-none opacity-20" 
           style={{ backgroundImage: 'linear-gradient(to right, #0891b2 1px, transparent 1px), linear-gradient(to bottom, #0891b2 1px, transparent 1px)', backgroundSize: '40px 40px' }} />
    </div>
  );
};
