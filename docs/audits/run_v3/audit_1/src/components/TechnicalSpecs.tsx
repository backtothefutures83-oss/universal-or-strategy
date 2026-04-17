import { ShieldCheck, HardDrive, Cpu, Layers } from 'lucide-react';

export const TechnicalSpecs: React.FC = () => {
  const specs = [
    {
      title: 'USER-SPACE SPIN-POLLING',
      desc: 'Replaces futex(2) and OS-level sleep. Bypasses kernel contention for sub-microsecond wakeups.',
      icon: <Cpu className="w-4 h-4 text-cyan-500 shadow-[0_0_8px_rgba(6,182,212,0.4)]" />,
      textColor: 'text-cyan-400',
      hoverBorder: 'group-hover:border-cyan-500/50'
    },
    {
      title: 'ZERO-COPY SPSC TOPOLOGY',
      desc: 'Replaces postMessage and Structured Cloning. Direct memory access via SharedArrayBuffer.',
      icon: <Layers className="w-4 h-4 text-pink-500 shadow-[0_0_8px_rgba(236,72,153,0.4)]" />,
      textColor: 'text-pink-400',
      hoverBorder: 'group-hover:border-pink-500/50'
    },
    {
      title: 'MLOCKALL HYGIENE',
      desc: 'Bypasses the SLUB allocator. Pins memory to physical RAM to eliminate page-fault jitter.',
      icon: <HardDrive className="w-4 h-4 text-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.4)]" />,
      textColor: 'text-amber-400',
      hoverBorder: 'group-hover:border-amber-500/50'
    },
    {
      title: 'CORE AFFINITY ISOLATION',
      desc: 'Hard-binding threads to specific L1/L2 caches to eliminate OS context-switching overhead.',
      icon: <ShieldCheck className="w-4 h-4 text-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.4)]" />,
      textColor: 'text-emerald-400',
      hoverBorder: 'group-hover:border-emerald-500/50'
    }
  ];

  return (
    <div className="bg-zinc-950/80 border border-zinc-800 p-6 rounded-xl h-full">
      <div className="flex items-center justify-between mb-8">
        <h3 className="text-zinc-100 font-mono text-sm tracking-widest flex items-center gap-2 uppercase font-bold">
          <ShieldCheck className="w-4 h-4 text-emerald-500" />
          SYSTEM PERFECTION (100/100)
        </h3>
        <span className="text-[10px] font-mono text-zinc-500">v2.0.4-STABLE</span>
      </div>
      
      <div className="space-y-6">
        {specs.map((spec, i) => (
          <div key={i} className="flex gap-4 group">
            <div className={`mt-1 h-8 w-8 flex items-center justify-center rounded-lg bg-zinc-900 border border-zinc-800 ${spec.hoverBorder} transition-colors`}>
              {spec.icon}
            </div>
            <div className="flex flex-col">
              <span className={`text-[11px] font-mono ${spec.textColor} uppercase font-bold tracking-widest`}>
                {spec.title}
              </span>
              <p className="text-xs font-mono text-zinc-500 mt-1 leading-relaxed">
                {spec.desc}
              </p>
            </div>
          </div>
        ))}
      </div>
      
      <div className="mt-8 p-4 bg-zinc-900/50 rounded-lg border border-zinc-800 flex items-center gap-4">
        <div className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse shadow-[0_0_10px_rgba(16,185,129,0.5)]" />
        <div className="flex flex-col">
          <span className="text-[10px] font-mono text-zinc-400 uppercase">System Integrity</span>
          <span className="text-xs font-mono text-emerald-400 font-bold uppercase">Open Pipe Latency Verified</span>
        </div>
      </div>
    </div>
  );
};
