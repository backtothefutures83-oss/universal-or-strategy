import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Database } from 'lucide-react';

export const RingBufferVisualizer: React.FC = () => {
  const [head, setHead] = useState(0);
  const [tail, setTail] = useState(0);
  const slots = Array.from({ length: 32 });

  useEffect(() => {
    const interval = setInterval(() => {
      setHead(prev => (prev + 1) % 32);
      if (Math.random() > 0.3) {
        setTail(prev => (prev + 1) % 32);
      }
    }, 150);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="bg-zinc-950/80 border border-zinc-800 p-6 rounded-xl flex flex-col h-full">
      <div className="flex items-center justify-between mb-8">
        <h3 className="text-zinc-100 font-mono text-sm tracking-widest flex items-center gap-2 uppercase">
          <Database className="w-4 h-4 text-cyan-500" />
          SPSC Ring Buffer (SAB)
        </h3>
        <div className="flex gap-2 text-[9px] font-mono">
          <span className="text-cyan-400 bg-cyan-400/10 px-1.5 py-0.5 border border-cyan-400/20">SHARED_ARRAY_BUFFER</span>
          <span className="text-pink-400 bg-pink-400/10 px-1.5 py-0.5 border border-pink-400/20">ATOMICS_ONLY</span>
        </div>
      </div>

      <div className="relative flex-1 flex items-center justify-center py-10">
        <div className="relative w-48 h-48 md:w-64 md:h-64 flex items-center justify-center">
          {slots.map((_, i) => {
            const angle = (i / slots.length) * 360;
            const isActive = i === head || i === tail;
            const isFull = i < head && i > tail;
            
            return (
              <div
                key={i}
                className="absolute w-2 h-6 md:w-2 md:h-8"
                style={{
                  transform: `rotate(${angle}deg) translateY(-80px)`,
                  transformOrigin: 'bottom center'
                }}
              >
                <motion.div
                  animate={{
                    backgroundColor: i === head ? '#22d3ee' : i === tail ? '#f472b6' : isFull ? '#083344' : '#18181b',
                    scaleY: isActive ? 1.5 : 1,
                    boxShadow: i === head ? '0 0 10px #22d3ee' : i === tail ? '0 0 10px #f472b6' : 'none'
                  }}
                  className="w-full h-full rounded-sm"
                />
              </div>
            );
          })}
          
          <div className="absolute flex flex-col items-center justify-center text-center">
            <span className="text-[10px] font-mono text-zinc-500 uppercase">Wait-Free Ops</span>
            <span className="text-2xl font-mono text-zinc-100 font-bold tracking-tighter">
              {((head - tail + 32) % 32).toString().padStart(2, '0')}
            </span>
            <span className="text-[10px] font-mono text-zinc-600 uppercase">Queue Depth</span>
          </div>
        </div>
      </div>

      <div className="mt-auto grid grid-cols-2 gap-4 border-t border-zinc-900 pt-4">
        <div className="flex flex-col">
          <span className="text-[9px] font-mono text-cyan-400 uppercase">Producer Head (Atomics.add)</span>
          <span className="text-xs font-mono text-zinc-300">0x{(head * 64).toString(16).padStart(4, '0')}</span>
        </div>
        <div className="flex flex-col text-right">
          <span className="text-[9px] font-mono text-pink-400 uppercase">Consumer Tail (Atomics.load)</span>
          <span className="text-xs font-mono text-zinc-300">0x{(tail * 64).toString(16).padStart(4, '0')}</span>
        </div>
      </div>
    </div>
  );
};
