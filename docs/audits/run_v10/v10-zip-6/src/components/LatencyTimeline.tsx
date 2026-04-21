import { useState, useEffect } from 'react';
import { breakthroughs, type Breakthrough } from '../data/breakthroughs';

const categoryColors: Record<string, string> = {
  dispatch: 'bg-cyan-500',
  recovery: 'bg-amber-500',
  queue: 'bg-violet-500',
  memory: 'bg-emerald-500',
  cache: 'bg-rose-500',
};

const categoryBorders: Record<string, string> = {
  dispatch: 'border-cyan-500',
  recovery: 'border-amber-500',
  queue: 'border-violet-500',
  memory: 'border-emerald-500',
  cache: 'border-rose-500',
};

const categoryGlows: Record<string, string> = {
  dispatch: 'shadow-cyan-500/30',
  recovery: 'shadow-amber-500/30',
  queue: 'shadow-violet-500/30',
  memory: 'shadow-emerald-500/30',
  cache: 'shadow-rose-500/30',
};

export default function LatencyTimeline() {
  const [animatedItems, setAnimatedItems] = useState<number[]>([]);
  const maxLatency = 500;

  useEffect(() => {
    breakthroughs.forEach((_, i) => {
      setTimeout(() => {
        setAnimatedItems(prev => [...prev, i]);
      }, 150 * i);
    });
  }, []);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-4 mb-6 flex-wrap">
        {Object.entries(categoryColors).map(([cat, color]) => (
          <div key={cat} className="flex items-center gap-1.5">
            <div className={`w-2.5 h-2.5 rounded-full ${color}`} />
            <span className="text-xs text-gray-400 capitalize">{cat}</span>
          </div>
        ))}
      </div>
      {breakthroughs.map((b: Breakthrough, i: number) => {
        const widthPct = (b.latency / maxLatency) * 100;
        const isVisible = animatedItems.includes(i);
        return (
          <div key={i} className="group relative">
            <div className="flex items-center gap-3">
              <span className="text-[10px] font-mono text-gray-500 w-6 shrink-0">{b.round}</span>
              <div className="flex-1 relative h-8 bg-gray-900/50 rounded overflow-hidden">
                <div
                  className={`absolute inset-y-0 left-0 ${categoryColors[b.category]} rounded transition-all duration-700 ease-out ${isVisible ? 'opacity-90' : 'opacity-0'}`}
                  style={{ width: isVisible ? `${widthPct}%` : '0%' }}
                />
                <div className="absolute inset-0 flex items-center px-3 justify-between">
                  <span className="text-xs font-medium text-white truncate z-10">{b.name}</span>
                  <span className="text-xs font-mono font-bold text-white z-10">{b.latency}ns</span>
                </div>
              </div>
            </div>
            <div className={`absolute left-10 top-9 z-20 bg-gray-900 border ${categoryBorders[b.category]} rounded-lg p-3 shadow-lg ${categoryGlows[b.category]} opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none max-w-xs`}>
              <p className="text-xs text-gray-300">{b.description}</p>
            </div>
          </div>
        );
      })}
      {/* V10 projected */}
      <div className="relative mt-4 pt-4 border-t border-dashed border-cyan-800/50">
        <div className="flex items-center gap-3">
          <span className="text-[10px] font-mono text-cyan-400 w-6 shrink-0 font-bold">V10</span>
          <div className="flex-1 relative h-8 bg-gray-900/50 rounded overflow-hidden border border-cyan-500/30">
            <div
              className={`absolute inset-y-0 left-0 bg-gradient-to-r from-cyan-500 to-emerald-400 rounded transition-all duration-1000 ease-out ${animatedItems.length === breakthroughs.length ? 'opacity-90' : 'opacity-0'}`}
              style={{ width: animatedItems.length === breakthroughs.length ? `${(187 / maxLatency) * 100}%` : '0%' }}
            />
            <div className="absolute inset-0 flex items-center px-3 justify-between">
              <span className="text-xs font-medium text-cyan-100 truncate z-10">Photon-Gate Dispatch</span>
              <span className="text-xs font-mono font-bold text-cyan-200 z-10 flex items-center gap-1">
                <span className="text-[10px] text-emerald-400">▼ projected</span> 187ns
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
