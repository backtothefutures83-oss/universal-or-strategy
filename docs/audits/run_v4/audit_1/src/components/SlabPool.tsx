import React, { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Database } from 'lucide-react';

interface Slab {
  id: number;
  status: 'FREE' | 'LOCKED' | 'BUSY';
  owner: number | null;
}

export const SlabPool: React.FC = () => {
  const [slabs, setSlabs] = useState<Slab[]>(
    Array.from({ length: 32 }).map((_, i) => ({
      id: i,
      status: i % 8 === 0 ? 'LOCKED' : i % 5 === 0 ? 'BUSY' : 'FREE',
      owner: i % 8 === 0 ? i % 12 : null,
    }))
  );

  useEffect(() => {
    const interval = setInterval(() => {
      setSlabs(prev => prev.map(slab => {
        if (Math.random() > 0.9) {
          const statuses: ('FREE' | 'LOCKED' | 'BUSY')[] = ['FREE', 'LOCKED', 'BUSY'];
          const status = statuses[Math.floor(Math.random() * 3)];
          return {
            ...slab,
            status,
            owner: status === 'FREE' ? null : Math.floor(Math.random() * 12)
          };
        }
        return slab;
      }));
    }, 500);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-5 backdrop-blur-sm">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-sm font-bold text-slate-400 uppercase tracking-widest flex items-center gap-2">
          <Database size={16} className="text-cyan-500" />
          Slab Pool (Zero-Copy)
        </h2>
        <span className="text-[10px] text-slate-500 font-bold uppercase">4096B PAGES</span>
      </div>

      <div className="grid grid-cols-8 gap-2">
        {slabs.map((slab) => (
          <motion.div
            key={slab.id}
            initial={false}
            animate={{
              backgroundColor: slab.status === 'FREE' ? 'rgba(30, 41, 59, 0.4)' : slab.status === 'LOCKED' ? 'rgba(249, 115, 22, 0.3)' : 'rgba(34, 211, 238, 0.3)',
              borderColor: slab.status === 'FREE' ? 'rgba(71, 85, 105, 0.2)' : slab.status === 'LOCKED' ? 'rgba(249, 115, 22, 0.5)' : 'rgba(34, 211, 238, 0.5)',
            }}
            className="aspect-square border rounded-sm flex items-center justify-center relative group"
          >
            <div className="text-[6px] font-bold text-slate-500 opacity-40 group-hover:opacity-100 transition-opacity">
              {slab.id.toString(16).padStart(2, '0')}
            </div>
            {slab.status !== 'FREE' && (
              <motion.div 
                className={`absolute inset-0.5 rounded-sm ${slab.status === 'LOCKED' ? 'bg-orange-500' : 'bg-cyan-500'}`}
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
              />
            )}
          </motion.div>
        ))}
      </div>

      <div className="mt-4 grid grid-cols-3 gap-2 text-[8px] font-bold uppercase tracking-widest">
        <div className="flex items-center gap-1.5">
          <div className="w-1.5 h-1.5 rounded-full bg-slate-800 border border-slate-700" />
          <span className="text-slate-500">FREE</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="w-1.5 h-1.5 rounded-full bg-cyan-500" />
          <span className="text-cyan-400">BUSY</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="w-1.5 h-1.5 rounded-full bg-orange-500" />
          <span className="text-orange-400">LOCKED</span>
        </div>
      </div>
    </div>
  );
};
