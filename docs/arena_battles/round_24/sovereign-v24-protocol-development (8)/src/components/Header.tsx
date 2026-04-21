export function Header() {
  return (
    <header className="relative py-6 px-4 border-b border-sov-border/50 bg-sov-dark/30">
      {/* Background grid */}
      <div className="absolute inset-0 grid-bg opacity-50" />

      <div className="relative max-w-7xl mx-auto">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            {/* Logo */}
            <div className="relative">
              <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-sov-cyan/20 to-sov-purple/20 border border-sov-cyan/30 flex items-center justify-center">
                <div className="text-2xl font-black text-sov-cyan font-mono glow-cyan tracking-tighter">
                  S
                </div>
              </div>
              <div className="absolute -top-1 -right-1 w-3 h-3 rounded-full bg-sov-green animate-pulse-glow border-2 border-sov-dark" />
            </div>

            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-xl font-black text-sov-text tracking-tight">
                  SOVEREIGN <span className="text-sov-cyan glow-cyan">V24</span>
                </h1>
                <span className="text-xs font-mono px-2 py-0.5 rounded bg-sov-cyan/10 border border-sov-cyan/30 text-sov-cyan">
                  GLOBAL-ROBUST
                </span>
              </div>
              <p className="text-xs text-sov-text-dim mt-0.5">
                Zero-Friction Handshake Protocol • Cross-Platform Resilient • &lt;0.5ns Target
              </p>
            </div>
          </div>

          {/* Status bar */}
          <div className="flex items-center gap-3 text-xs font-mono">
            <span className="flex items-center gap-1.5 text-sov-green">
              <span className="w-2 h-2 rounded-full bg-sov-green animate-pulse-glow" />
              ACTIVE
            </span>
            <span className="text-sov-text-muted">|</span>
            <span className="text-sov-text-dim">ADR-015</span>
            <span className="text-sov-text-muted">|</span>
            <span className="text-sov-text-dim">SOV-V24-GLOBAL-ROBUST</span>
          </div>
        </div>
      </div>
    </header>
  );
}
