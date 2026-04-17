import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Cpu } from 'lucide-react';

const CORE_COUNT = 12;

export const CoreMap: React.FC = () => {
  const [load, setLoad] = useState<number[]>(Array(CORE_COUNT).fill(0));

  useEffect(() => {
    const interval = setInterval(() => {
      setLoad(prev => prev.map(() => Math.random() * 100));
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="bg-zinc-950/80 border border-zinc-800 p-6 rounded-xl">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-zinc-100 font-mono text-sm tracking-widest flex items-center gap-2">
          <Cpu className="w-4 h-4 text-emerald-500" />
          ISOLATED CORE AFFINITY MAP
        </h3>
        <span className="text-[10px] font-mono text-emerald-500/80 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
          SCHED_FIFO READY
        </span>
      </div>
      
      <div className="grid grid-cols-4 md:grid-cols-6 gap-3">
        {load.map((v, i) => (
          <div key={i} className="flex flex-col gap-1.5">
            <div className="flex justify-between items-center text-[9px] font-mono text-zinc-500">
              <span>CORE_{i.toString().padStart(2, '0')}</span>
              <span className={v > 80 ? 'text-red-400' : 'text-emerald-400'}>{v.toFixed(1)}%</span>
            </div>
            <div className="h-12 w-full bg-zinc-900 rounded-sm overflow-hidden relative border border-zinc-800/50">
              <motion.div 
                className="absolute bottom-0 left-0 right-0 bg-emerald-500/40"
                animate={{ height: `${v}%` }}
                transition={{ duration: 0.5 }}
              />
              <div className="absolute inset-0 flex flex-col justify-between p-1">
                <div className="h-[1px] w-full bg-zinc-800" />
                <div className="h-[1px] w-full bg-zinc-800" />
                <div className="h-[1px] w-full bg-zinc-800" />
              </div>
            </div>
          </div>
        ))}
      </div>
      
      <div className="mt-6 pt-4 border-t border-zinc-900 flex justify-between items-center">
        <div className="flex gap-4">
          <div className="flex items-center gap-1.5">
            <div className="w-2 h-2 rounded-full bg-emerald-500 shadow-[0_0_5px_rgba(16,185,129,0.5)]" />
            <span className="text-[10px] font-mono text-zinc-400 tracking-tight uppercase">User-Space Polling</span>
          </div>
          <div className="flex items-center gap-1.5">
            <div className="w-2 h-2 rounded-full bg-amber-500/50" />
            <span className="text-[10px] font-mono text-zinc-400 tracking-tight uppercase">Kernel Context: 0%</span>
          </div>
        </div>
        <div className="text-[9px] font-mono text-zinc-600">NO_HZ_FULL ENABLED</div>
      </div>
    </div>
  );
};
