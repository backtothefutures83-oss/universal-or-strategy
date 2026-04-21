import { protocolSpecs } from '../data/v24Code';

export default function Hero() {
  return (
    <section id="hero" className="relative min-h-screen flex items-center justify-center overflow-hidden grid-bg">
      {/* Animated background elements */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-sov-cyan/5 rounded-full blur-3xl animate-float" />
        <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-sov-purple/5 rounded-full blur-3xl animate-float" style={{ animationDelay: '3s' }} />
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] border border-sov-cyan/5 rounded-full animate-spin-slow" />
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] border border-sov-purple/5 rounded-full animate-spin-slow" style={{ animationDirection: 'reverse', animationDuration: '15s' }} />
      </div>

      <div className="relative z-10 max-w-5xl mx-auto px-4 text-center">
        {/* Status badge */}
        <div className="inline-flex items-center gap-2 mb-8 px-4 py-2 rounded-full border border-sov-green/30 bg-sov-green/5">
          <span className="w-2 h-2 rounded-full bg-sov-green animate-glow-pulse" />
          <span className="text-xs font-mono text-sov-green">PROTOCOL ACTIVE — ALL INVARIANTS SATISFIED</span>
        </div>

        {/* Main title */}
        <h1 className="text-5xl sm:text-7xl lg:text-8xl font-black tracking-tight mb-4">
          <span className="text-sov-text-bright">SOVEREIGN</span>
          <br />
          <span className="gradient-text">V24</span>
        </h1>

        <p className="text-lg sm:text-xl text-sov-text-dim max-w-2xl mx-auto mb-2 font-light">
          Global Zero-Friction Handshake Protocol
        </p>
        <p className="text-sm font-mono text-sov-text-dim/60 mb-10">
          Build Tag: {protocolSpecs.buildTag}
        </p>

        {/* Latency target */}
        <div className="inline-block mb-12 px-8 py-6 rounded-2xl border border-sov-cyan/20 bg-sov-surface/50 backdrop-blur-sm box-glow-cyan">
          <div className="text-xs font-mono text-sov-text-dim mb-1">ARCHITECTURAL TARGET</div>
          <div className="text-5xl sm:text-6xl font-black font-mono text-sov-cyan glow-cyan">
            &lt; 0.5<span className="text-2xl">ns</span>
          </div>
          <div className="text-xs font-mono text-sov-text-dim mt-1">
            Cross-Platform Resilient
          </div>
          <div className="text-xs font-mono text-sov-green mt-2">
            ↓ 42.5% from V23.1 ({protocolSpecs.previousRecord})
          </div>
        </div>

        {/* Mandate cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 max-w-3xl mx-auto">
          {protocolSpecs.mandates.map((mandate) => (
            <div
              key={mandate.id}
              className="group p-4 rounded-xl border border-sov-border bg-sov-surface/50 hover:border-sov-cyan/30 transition-all duration-300 text-left"
            >
              <div className="flex items-center gap-2 mb-2">
                <span className="w-6 h-6 rounded-md bg-sov-cyan/10 border border-sov-cyan/20 flex items-center justify-center text-xs font-mono text-sov-cyan">
                  {mandate.id}
                </span>
                <span className="text-sm font-semibold text-sov-text-bright group-hover:text-sov-cyan transition-colors">
                  {mandate.title}
                </span>
              </div>
              <p className="text-xs text-sov-text-dim leading-relaxed pl-8">
                {mandate.description}
              </p>
            </div>
          ))}
        </div>

        {/* Scroll indicator */}
        <div className="mt-16 animate-bounce">
          <svg className="w-6 h-6 mx-auto text-sov-text-dim/40" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 14l-7 7m0 0l-7-7m7 7V3" />
          </svg>
        </div>
      </div>
    </section>
  );
}
