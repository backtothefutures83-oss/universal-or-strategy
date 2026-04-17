import type { InvariantCheck } from '../hooks/useSovereignSimulation';

interface Props {
  invariants: InvariantCheck[];
  invariantPassRate: number;
}

export function SafetyInvariant({ invariants, invariantPassRate }: Props) {
  return (
    <div className="panel-glow rounded-xl border border-sov-border bg-sov-panel p-5 relative overflow-hidden">
      <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-10">
        <div className="w-full h-px bg-gradient-to-r from-transparent via-sov-purple to-transparent animate-scan-line" style={{ animationDuration: '10s' }} />
      </div>

      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-sov-purple/20 to-sov-purple/5 border border-sov-purple/30 flex items-center justify-center text-sm">
            🛡️
          </div>
          <div>
            <h3 className="text-sm font-semibold text-sov-text">Safety Invariants</h3>
            <p className="text-xs text-sov-text-muted font-mono">Non-Latency-Summing Checks</p>
          </div>
        </div>
        <div className={`px-3 py-1.5 rounded-full border text-sm font-bold font-mono ${
          invariantPassRate === 100 
            ? 'bg-sov-green/10 border-sov-green/30 text-sov-green' 
            : 'bg-sov-red/10 border-sov-red/30 text-sov-red'
        }`}>
          {invariantPassRate}% PASS
        </div>
      </div>

      {/* Invariant list */}
      <div className="space-y-2">
        {invariants.map((inv) => (
          <div
            key={inv.id}
            className={`flex items-center gap-3 p-2.5 rounded-lg border transition-all duration-300 ${
              inv.status === 'pass'
                ? 'border-sov-green/20 bg-sov-green/5'
                : inv.status === 'fail'
                ? 'border-sov-red/20 bg-sov-red/5'
                : 'border-sov-amber/20 bg-sov-amber/5'
            }`}
          >
            {/* Status indicator */}
            <div className="flex-shrink-0">
              {inv.status === 'pass' ? (
                <div className="w-6 h-6 rounded-full bg-sov-green/20 flex items-center justify-center">
                  <svg className="w-3.5 h-3.5 text-sov-green" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                  </svg>
                </div>
              ) : inv.status === 'fail' ? (
                <div className="w-6 h-6 rounded-full bg-sov-red/20 flex items-center justify-center">
                  <svg className="w-3.5 h-3.5 text-sov-red" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </div>
              ) : (
                <div className="w-6 h-6 rounded-full bg-sov-amber/20 flex items-center justify-center animate-spin">
                  <svg className="w-3.5 h-3.5 text-sov-amber" fill="none" viewBox="0 0 24 24">
                    <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="3" strokeDasharray="30 10" />
                  </svg>
                </div>
              )}
            </div>

            {/* Info */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-sov-text font-mono">{inv.id}</span>
                <span className="text-xs text-sov-text truncate">{inv.name}</span>
              </div>
              <div className="flex items-center gap-3 mt-0.5">
                <span className="text-xs text-sov-text-dim font-mono">{inv.bitwise}</span>
                <span className="text-xs text-sov-text-muted font-mono">{inv.duration.toFixed(3)}ns</span>
              </div>
            </div>

            {/* Bitwise visual */}
            <div className="flex-shrink-0 hidden sm:flex items-center gap-0.5">
              {Array.from({ length: 8 }, (_, i) => (
                <div
                  key={i}
                  className={`w-1.5 h-1.5 rounded-full ${
                    inv.bitwise.charAt(2 + i * 2) !== '0' ? 'bg-sov-green' : 'bg-sov-border'
                  }`}
                />
              ))}
            </div>
          </div>
        ))}
      </div>

      {/* ADR-015 compliance badge */}
      <div className="mt-3 p-2.5 rounded-lg bg-sov-dark/40 border border-sov-border flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="text-xs text-sov-text-dim">ADR-015 Compliance:</span>
          <span className="text-xs font-bold text-sov-green font-mono">ZERO FENCES • ZERO ATOMICS</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-xs text-sov-text-dim">Overhead:</span>
          <span className="text-xs font-bold text-sov-green font-mono">&lt;0.01ns</span>
        </div>
      </div>
    </div>
  );
}
