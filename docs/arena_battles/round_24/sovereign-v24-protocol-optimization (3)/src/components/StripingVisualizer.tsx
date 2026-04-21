import { useState, useEffect, useCallback } from 'react';
import { cn } from '../utils/cn';

type StripingMode = 'L1-Local' | 'L2-Striped' | 'L3-Shared' | 'Hybrid';

interface StripeBlock {
  id: number;
  mode: StripingMode;
  fill: number;
  latency: number;
}

export default function StripingVisualizer() {
  const [mode, setMode] = useState<StripingMode>('L1-Local');
  const [contention, setContention] = useState(15);
  const [blocks, setBlocks] = useState<StripeBlock[]>([]);
  const [autoMode, setAutoMode] = useState(true);
  const [modeSwitches, setModeSwitches] = useState(0);

  const generateBlocks = useCallback((currentMode: StripingMode) => {
    const count = currentMode === 'L1-Local' ? 8 : currentMode === 'L2-Striped' ? 16 : 24;
    const newBlocks: StripeBlock[] = [];
    for (let i = 0; i < count; i++) {
      newBlocks.push({
        id: i,
        mode: currentMode,
        fill: 30 + Math.random() * 70,
        latency: currentMode === 'L1-Local'
          ? 0.15 + Math.random() * 0.1
          : currentMode === 'L2-Striped'
          ? 0.25 + Math.random() * 0.15
          : currentMode === 'L3-Shared'
          ? 0.35 + Math.random() * 0.15
          : 0.2 + Math.random() * 0.25,
      });
    }
    setBlocks(newBlocks);
  }, []);

  useEffect(() => {
    generateBlocks(mode);
  }, [mode, generateBlocks]);

  useEffect(() => {
    if (!autoMode) return;
    
    const interval = setInterval(() => {
      const newContention = Math.floor(5 + Math.random() * 50);
      setContention(newContention);
      
      let newMode: StripingMode;
      if (newContention < 15) newMode = 'L1-Local';
      else if (newContention < 30) newMode = 'L2-Striped';
      else if (newContention < 45) newMode = 'L3-Shared';
      else newMode = 'Hybrid';
      
      if (newMode !== mode) {
        setMode(newMode);
        setModeSwitches(prev => prev + 1);
      }
    }, 1500);

    return () => clearInterval(interval);
  }, [autoMode, mode]);

  const modeColors: Record<StripingMode, string> = {
    'L1-Local': 'from-sov-green/80 to-sov-green-dim/80',
    'L2-Striped': 'from-sov-amber/80 to-yellow-600/80',
    'L3-Shared': 'from-sov-purple/80 to-sov-purple-dim/80',
    'Hybrid': 'from-sov-cyan/80 to-sov-cyan-dim/80',
  };

  const modeBorderColors: Record<StripingMode, string> = {
    'L1-Local': 'border-sov-green/30',
    'L2-Striped': 'border-sov-amber/30',
    'L3-Shared': 'border-sov-purple/30',
    'Hybrid': 'border-sov-cyan/30',
  };

  return (
    <section id="striping" className="py-20 relative">
      <div className="max-w-6xl mx-auto px-4">
        <div className="text-center mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-sov-amber/20 bg-sov-amber/5 mb-4">
            <span className="text-xs font-mono text-sov-amber">MANDATE #3</span>
          </div>
          <h2 className="text-3xl sm:text-4xl font-bold text-sov-text-bright mb-3">
            Adaptive <span className="text-sov-amber">Striping</span>
          </h2>
          <p className="text-sov-text-dim max-w-xl mx-auto">
            Friction-less scaling between L1-local and L2-striped modes via real-time cache contention diagnostics.
          </p>
        </div>

        {/* Controls */}
        <div className="flex flex-wrap items-center justify-center gap-4 mb-8">
          <button
            onClick={() => setAutoMode(!autoMode)}
            className={cn(
              'px-4 py-2 rounded-lg border font-mono text-sm transition-all',
              autoMode
                ? 'bg-sov-amber/10 border-sov-amber/30 text-sov-amber'
                : 'bg-sov-surface border-sov-border text-sov-text-dim'
            )}
          >
            {autoMode ? '⚡ Auto-Adapt ON' : '⚡ Auto-Adapt OFF'}
          </button>

          {!autoMode && (
            <div className="flex gap-2">
              {(['L1-Local', 'L2-Striped', 'L3-Shared', 'Hybrid'] as StripingMode[]).map(m => (
                <button
                  key={m}
                  onClick={() => { setMode(m); setModeSwitches(prev => prev + 1); }}
                  className={cn(
                    'px-3 py-2 rounded-lg border font-mono text-xs transition-all',
                    mode === m
                      ? `${modeBorderColors[m]} bg-white/5 text-sov-text-bright`
                      : 'border-sov-border text-sov-text-dim hover:text-sov-text'
                  )}
                >
                  {m}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Contention gauge */}
        <div className="mb-8 p-4 rounded-xl border border-sov-border bg-sov-surface/30">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs font-mono text-sov-text-dim">CACHE CONTENTION SCORE</span>
            <span className="text-sm font-mono font-bold text-sov-text-bright">{contention}</span>
          </div>
          <div className="w-full h-3 rounded-full bg-sov-surface border border-sov-border overflow-hidden">
            <div
              className={cn(
                'h-full rounded-full transition-all duration-500',
                contention < 15 ? 'bg-sov-green' :
                contention < 30 ? 'bg-sov-amber' :
                contention < 45 ? 'bg-sov-purple' : 'bg-sov-red'
              )}
              style={{ width: `${Math.min(contention * 2, 100)}%` }}
            />
          </div>
          <div className="flex justify-between mt-1">
            <span className="text-[10px] font-mono text-sov-text-dim">0</span>
            <span className="text-[10px] font-mono text-sov-text-dim">50</span>
          </div>
        </div>

        {/* Current mode display */}
        <div className={cn(
          'mb-8 p-6 rounded-xl border transition-all duration-500',
          modeBorderColors[mode], 'bg-sov-surface/30'
        )}>
          <div className="flex items-center justify-between">
            <div>
              <div className="text-xs font-mono text-sov-text-dim mb-1">ACTIVE STRIPING MODE</div>
              <div className={cn('text-2xl font-bold font-mono', 
                mode === 'L1-Local' ? 'text-sov-green' :
                mode === 'L2-Striped' ? 'text-sov-amber' :
                mode === 'L3-Shared' ? 'text-sov-purple' : 'text-sov-cyan'
              )}>
                {mode}
              </div>
            </div>
            <div className="text-right">
              <div className="text-xs font-mono text-sov-text-dim mb-1">MODE SWITCHES</div>
              <div className="text-2xl font-bold font-mono text-sov-text-bright">{modeSwitches}</div>
            </div>
          </div>
        </div>

        {/* Stripe blocks visualization */}
        <div className="rounded-xl border border-sov-border bg-sov-surface/30 p-4">
          <div className="text-xs font-mono text-sov-text-dim mb-3">CACHE STRIPE LAYOUT</div>
          <div className="flex flex-wrap gap-2">
            {blocks.map(block => (
              <div
                key={block.id}
                className={cn(
                  'relative rounded-md border border-white/10 overflow-hidden transition-all duration-500',
                  block.mode === 'L1-Local' ? 'w-12 h-12' :
                  block.mode === 'L2-Striped' ? 'w-10 h-10' :
                  block.mode === 'L3-Shared' ? 'w-8 h-8' : 'w-10 h-10'
                )}
              >
                <div
                  className={cn('absolute inset-0 bg-gradient-to-br', modeColors[block.mode])}
                  style={{ opacity: block.fill / 100 }}
                />
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-[8px] font-mono text-white/80">{block.id}</span>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Mode comparison */}
        <div className="mt-8 grid grid-cols-2 sm:grid-cols-4 gap-3">
          {([
            { mode: 'L1-Local' as StripingMode, latency: '~0.20ns', desc: 'All data in L1 cache', threshold: '< 15' },
            { mode: 'L2-Striped' as StripingMode, latency: '~0.32ns', desc: 'Striped across L2', threshold: '15–30' },
            { mode: 'L3-Shared' as StripingMode, latency: '~0.42ns', desc: 'Shared L3 fallback', threshold: '30–45' },
            { mode: 'Hybrid' as StripingMode, latency: '~0.35ns', desc: 'Dynamic mix', threshold: '> 45' },
          ]).map(item => (
            <div
              key={item.mode}
              className={cn(
                'p-4 rounded-lg border transition-all',
                mode === item.mode
                  ? `${modeBorderColors[item.mode]} bg-white/5`
                  : 'border-sov-border bg-sov-surface/30'
              )}
            >
              <div className={cn(
                'text-sm font-bold font-mono mb-1',
                item.mode === 'L1-Local' ? 'text-sov-green' :
                item.mode === 'L2-Striped' ? 'text-sov-amber' :
                item.mode === 'L3-Shared' ? 'text-sov-purple' : 'text-sov-cyan'
              )}>
                {item.mode}
              </div>
              <div className="text-xs font-mono text-sov-text-dim">{item.desc}</div>
              <div className="text-xs font-mono text-sov-text-bright mt-1">{item.latency}</div>
              <div className="text-[10px] font-mono text-sov-text-dim mt-0.5">Contention: {item.threshold}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
