import { useState, useEffect, useRef } from 'react';

const BOOT_SEQUENCE = [
  "[SYS] INITIALIZING SOVEREIGN CORE V24...",
  "[SYS] ESTABLISHING KERNEL HOOKS [OK]",
  "[HW]  INITIATING TOPOLOGY DISCOVERY...",
  "[HW]  DETECTED NUMA NODES: 4",
  "[HW]  L1 CACHE LINE WIDTH: 64B (STRIPE: 1)",
  "[HW]  L2 CACHE LINE WIDTH: 128B (STRIPE: 2)",
  "[HW]  L3 CACHE LINE WIDTH: 256B (STRIPE: 4)",
  "[SYS] ALIGNING MEMORY BOUNDARIES [OK]",
  "[SEC] VALIDATING SAFETY INVARIANTS...",
  "[SEC] SEQUENCE-SHADOW VALIDATION [PASS]",
  "[SEC] HARDWARE-LEVEL TSO PARITY [PASS]",
  "[OPT] ADAPTIVE STRIPING: L1-LOCAL MODE ENGAGED",
  "[OPT] LATENCY TARGET SET: < 0.50ns",
  "[SYS] ADR-015 TOTAL FENCE-LESS DISCIPLINE: ENFORCED",
  "[SYS] SOVEREIGN CORE ONLINE. AWAITING TELEMETRY."
];

export const Terminal: React.FC = () => {
  const [lines, setLines] = useState<string[]>([]);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let delay = 0;
    BOOT_SEQUENCE.forEach((line) => {
      setTimeout(() => {
        setLines(prev => [...prev, line]);
      }, delay);
      delay += Math.random() * 300 + 100; // Random delay between 100-400ms
    });

    // Simulated periodic updates
    const interval = setInterval(() => {
      const msgs = [
        "[OPT] CACHE CONTENTION DETECTED on NODE 2. SHIFTING TO L2-STRIPED MODE.",
        "[OPT] CONTENTION RESOLVED. REVERTING TO L1-LOCAL MODE.",
        "[NET] SYNCHRONIZING CROSS-SOCKET CHANNELS...",
        "[SEC] TSO PARITY CHECK: ZERO-COPY INTEGRITY MAINTAINED."
      ];
      if (Math.random() > 0.7) {
        setLines(prev => {
          const newLines = [...prev, msgs[Math.floor(Math.random() * msgs.length)]];
          if (newLines.length > 50) return newLines.slice(newLines.length - 50);
          return newLines;
        });
      }
    }, 2500);

    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [lines]);

  return (
    <div className="bg-black/50 border border-emerald-900/50 rounded-lg p-4 font-mono text-sm h-full flex flex-col relative overflow-hidden backdrop-blur-sm shadow-[0_0_15px_rgba(16,185,129,0.1)]">
      <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-emerald-500/50 to-transparent"></div>
      <div className="flex items-center justify-between mb-4 border-b border-emerald-900/50 pb-2">
        <span className="text-emerald-400 font-bold uppercase tracking-wider">Terminal / Kernel Logs</span>
        <div className="flex space-x-2">
          <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></div>
          <div className="w-2 h-2 rounded-full bg-emerald-700"></div>
          <div className="w-2 h-2 rounded-full bg-emerald-900"></div>
        </div>
      </div>
      <div className="flex-1 overflow-y-auto space-y-1 scrollbar-thin scrollbar-thumb-emerald-700 scrollbar-track-transparent">
        {lines.map((line, i) => (
          <div key={i} className={`
            ${line.includes('[SYS]') ? 'text-emerald-500' : ''}
            ${line.includes('[HW]') ? 'text-cyan-400' : ''}
            ${line.includes('[SEC]') ? 'text-amber-400' : ''}
            ${line.includes('[OPT]') ? 'text-purple-400' : ''}
            ${line.includes('ERROR') ? 'text-red-500 bg-red-950/30' : ''}
          `}>
            {'>'} {line}
          </div>
        ))}
        <div ref={bottomRef} />
      </div>
    </div>
  );
};
