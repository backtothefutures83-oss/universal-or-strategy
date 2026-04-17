import { motion } from 'framer-motion';

export function OpenPipesAnimation() {
  return (
    <div className="relative w-full h-24 bg-slate-950 border border-slate-800 rounded-lg overflow-hidden mt-6 flex flex-col items-center justify-center group">
      <div className="absolute inset-0 opacity-20 bg-[linear-gradient(90deg,transparent_20%,#22d3ee_50%,transparent_80%)] animate-[pulse_2s_infinite]" />
      <div className="flex gap-[2px] overflow-hidden w-full px-2 absolute z-0 items-center h-full">
        {Array.from({ length: 50 }).map((_, i) => (
          <motion.div
            key={i}
            initial={{ opacity: 0 }}
            animate={{ opacity: [0, 1, 1, 0], x: ["0vw", "10vw", "20vw", "30vw"] }}
            transition={{
              duration: 2,
              repeat: Infinity,
              delay: i * 0.05,
              ease: "linear",
            }}
            className="w-2 h-8 bg-cyan-400 rounded-full flex-shrink-0 shadow-[0_0_10px_rgba(34,211,238,0.8)]"
          />
        ))}
      </div>
      <div className="absolute inset-0 flex items-center justify-center pointer-events-none z-10">
        <span className="bg-slate-950/90 px-4 py-1.5 rounded border border-cyan-500 text-cyan-400 font-bold text-xs uppercase tracking-widest backdrop-blur-md shadow-[0_0_20px_rgba(34,211,238,0.2)]">
          Raw Hardware FIFO
        </span>
      </div>
    </div>
  );
}
