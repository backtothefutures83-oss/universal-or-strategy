import type { StripeConfig } from '../hooks/useSovereignSimulation';

interface Props {
  config: StripeConfig;
  contentionScore: number;
}

export function AdaptiveStriping({ config, contentionScore }: Props) {
  const isL1 = config.mode === 'L1-local';
  const isL2 = config.mode === 'L2-striped';
  
  const modeColor = isL1 ? 'text-sov-green' : isL2 ? 'text-sov-amber' : 'text-sov-red';
  const modeBorder = isL1 ? 'border-sov-green/40' : isL2 ? 'border-sov-amber/40' : 'border-sov-red/40';
  const modeBg = isL1 ? 'from-sov-green/10' : isL2 ? 'from-sov-amber/10' : 'from-sov-red/10';

  return (
    <div className="panel-glow rounded-xl border border-sov-border bg-sov-panel p-5 relative overflow-hidden">
      <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-10">
        <div className="w-full h-px bg-gradient-to-r from-transparent via-sov-amber to-transparent animate-scan-line" style={{ animationDuration: '7s' }} />
      </div>

      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-sov-amber/20 to-sov-amber/5 border border-sov-amber/30 flex items-center justify-center text-sm">
            🔄
          </div>
          <div>
            <h3 className="text-sm font-semibold text-sov-text">Adaptive Striping</h3>
            <p className="text-xs text-sov-text-muted font-mono">Friction-Less Scaling Engine</p>
          </div>
        </div>
        <div className={`px-3 py-1.5 rounded-full border text-xs font-bold font-mono ${modeBg} ${modeBorder} ${modeColor} bg-gradient-to-r to-transparent`}>
          {config.mode.toUpperCase()}
        </div>
      </div>

      {/* Contention gauge */}
      <div className="mb-4">
        <div className="flex justify-between items-center mb-1.5">
          <span className="text-xs text-sov-text-muted">Cache Contention</span>
          <span className={`text-xs font-mono font-bold ${
            contentionScore < 0.5 ? 'text-sov-green' : contentionScore < 0.7 ? 'text-sov-amber' : 'text-sov-red'
          }`}>
            {(contentionScore * 100).toFixed(1)}%
          </span>
        </div>
        <div className="h-3 rounded-full bg-sov-black border border-sov-border overflow-hidden">
          <div
            className="h-full rounded-full transition-all duration-500 ease-out"
            style={{
              width: `${contentionScore * 100}%`,
              background: contentionScore < 0.5
                ? 'linear-gradient(90deg, #00ff88, #00e5ff)'
                : contentionScore < 0.7
                ? 'linear-gradient(90deg, #00e5ff, #ffab00)'
                : 'linear-gradient(90deg, #ffab00, #ff3366)',
            }}
          />
        </div>
        <div className="flex justify-between mt-1">
          <span className="text-xs text-sov-text-muted">Threshold: {(config.switchThreshold * 100).toFixed(0)}%</span>
          <span className="text-xs text-sov-text-dim font-mono">Switch at: {config.lastSwitch}</span>
        </div>
      </div>

      {/* Stripe visualization */}
      <div className="mb-4">
        <div className="flex justify-between items-center mb-2">
          <span className="text-xs text-sov-text-muted">Active Stripes</span>
          <span className="text-xs text-sov-text font-mono font-bold">{config.activeStripes}</span>
        </div>
        <div className="grid grid-cols-8 gap-1.5">
          {Array.from({ length: 16 }, (_, i) => (
            <div
              key={i}
              className={`h-8 rounded transition-all duration-300 border ${
                i < config.activeStripes
                  ? isL1
                    ? 'bg-sov-green/30 border-sov-green/50 shadow-sm shadow-sov-green/20'
                    : 'bg-sov-amber/30 border-sov-amber/50 shadow-sm shadow-sov-amber/20'
                  : 'bg-sov-black/30 border-sov-border'
              }`}
            >
              {i < config.activeStripes && (
                <div className="w-full h-full flex items-center justify-center">
                  <div className={`w-1 h-1 rounded-full ${isL1 ? 'bg-sov-green' : 'bg-sov-amber'} animate-pulse-glow`} />
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Stripe details */}
      <div className="grid grid-cols-2 gap-2 text-xs">
        <div className="bg-sov-dark/40 rounded-lg p-2.5 border border-sov-border">
          <div className="text-sov-text-muted mb-1">Stripe Size</div>
          <div className="text-lg font-bold text-sov-text font-mono">{config.stripeSize}B</div>
          <div className="text-sov-text-dim">Auto-aligned</div>
        </div>
        <div className="bg-sov-dark/40 rounded-lg p-2.5 border border-sov-border">
          <div className="text-sov-text-muted mb-1">Mode Logic</div>
          <div className="text-sm font-bold text-sov-text font-mono">
            {isL1 ? 'LOCAL' : 'STRIPED'}
          </div>
          <div className="text-sov-text-dim">
            {isL1 ? 'Contention below threshold' : 'Contention exceeded threshold'}
          </div>
        </div>
      </div>

      {/* Flow diagram */}
      <div className="mt-3 p-2.5 rounded-lg bg-sov-dark/30 border border-sov-border">
        <div className="flex items-center justify-center gap-2 text-xs font-mono">
          <span className="px-2 py-1 rounded bg-sov-black text-sov-text-dim border border-sov-border">Producer</span>
          <span className="text-sov-cyan animate-pulse-glow">→</span>
          <span className="px-2 py-1 rounded bg-sov-black/80 text-sov-cyan border border-sov-cyan/30">
            {config.mode === 'L1-local' ? 'L1-Cache' : 'L2-Stripe'}
          </span>
          <span className="text-sov-cyan animate-pulse-glow">→</span>
          <span className="px-2 py-1 rounded bg-sov-black text-sov-text-dim border border-sov-border">Consumer</span>
        </div>
      </div>
    </div>
  );
}
