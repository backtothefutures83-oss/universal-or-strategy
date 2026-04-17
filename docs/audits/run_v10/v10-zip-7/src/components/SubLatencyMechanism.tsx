import { useState, useEffect } from 'react';
import { v10Design } from '../data/breakthroughs';

export default function SubLatencyMechanism() {
  const [animated, setAnimated] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setAnimated(true), 600);
    return () => clearTimeout(t);
  }, []);

  const { mechanism } = v10Design;
  let running = 243;

  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-2xl p-6">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-cyan-500/20 flex items-center justify-center text-cyan-400 text-sm">
          ⚡
        </div>
        <div>
          <h2 className="text-lg font-semibold text-gray-200">
            Sub-200ns Mechanism
          </h2>
          <p className="text-xs text-gray-500">{mechanism.name}</p>
        </div>
        <div className="ml-auto">
          <div className="text-3xl font-mono font-black text-cyan-400">
            {mechanism.projectedNs}ns
          </div>
          <div className="text-[10px] text-right text-gray-500">projected</div>
        </div>
      </div>

      {/* Waterfall breakdown */}
      <div className="bg-gray-800/50 rounded-xl p-4 mb-4">
        <div className="text-[10px] text-gray-500 uppercase tracking-wider mb-3 font-semibold">
          Latency Waterfall
        </div>
        {mechanism.breakdown.map((b, i) => {
          const prev = running;
          running += b.saving;
          const isBase = b.saving === 0;
          return (
            <div key={i} className="flex items-center gap-2 mb-2 last:mb-0">
              <div className="w-20 text-right">
                <span
                  className={`text-xs font-mono ${
                    isBase
                      ? 'text-gray-400'
                      : 'text-emerald-400'
                  }`}
                >
                  {isBase ? `${prev}ns` : `${b.saving}ns`}
                </span>
              </div>
              <div className="flex-1 relative">
                <div className="h-6 bg-gray-700/30 rounded overflow-hidden">
                  <div
                    className="h-full rounded transition-all duration-700 ease-out flex items-center px-2"
                    style={{
                      width: animated
                        ? `${(Math.abs(running) / 243) * 100}%`
                        : isBase
                        ? '100%'
                        : '100%',
                      background: isBase
                        ? 'linear-gradient(90deg, #6366f1, #818cf8)'
                        : 'linear-gradient(90deg, #059669, #34d399)',
                      transitionDelay: `${i * 200}ms`,
                    }}
                  >
                    <span className="text-[9px] text-white/80 whitespace-nowrap truncate">
                      {b.label}
                    </span>
                  </div>
                </div>
              </div>
              <div className="w-12 text-right">
                <span className="text-[10px] font-mono text-gray-500">
                  ={running}ns
                </span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Math summary */}
      <div className="bg-cyan-500/5 border border-cyan-500/20 rounded-xl p-4">
        <div className="text-[10px] text-cyan-400 uppercase tracking-wider mb-2 font-semibold">
          Critical Path Analysis
        </div>
        <p className="text-xs text-gray-300 leading-relaxed">
          {mechanism.description}
        </p>
        <div className="mt-3 flex gap-2 flex-wrap">
          {['Zero Syscalls', 'CPU Pinned', 'IRQ Isolated', 'NUMA Local', 'L1 Resident'].map((tag) => (
            <span
              key={tag}
              className="text-[9px] px-2 py-0.5 rounded-full bg-cyan-500/10 text-cyan-300 border border-cyan-500/20"
            >
              {tag}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}
