import { useState, useEffect } from 'react';

export default function HeroSection() {
  const [glitch, setGlitch] = useState(false);
  const [counter, setCounter] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setCounter(c => c + 1);
      if (Math.random() < 0.05) {
        setGlitch(true);
        setTimeout(() => setGlitch(false), 100);
      }
    }, 50);
    return () => clearInterval(interval);
  }, []);

  const latencyDisplay = (780 + Math.sin(counter * 0.05) * 30).toFixed(0);

  return (
    <section className="relative min-h-screen flex items-center justify-center overflow-hidden">
      {/* Deep background grid */}
      <div className="absolute inset-0 grid-bg opacity-40" />

      {/* Radial gradient overlay */}
      <div className="absolute inset-0" style={{
        background: 'radial-gradient(ellipse at center, rgba(192,160,64,0.06) 0%, transparent 60%)'
      }} />

      {/* Scanline effect */}
      <div className="absolute inset-0 pointer-events-none overflow-hidden opacity-[0.03]">
        <div className="w-full h-px bg-white animate-scanline" />
      </div>

      {/* Hex pattern decorations */}
      <div className="absolute top-20 left-10 text-[#C0A040] opacity-10 font-mono text-xs leading-relaxed select-none hidden lg:block">
        {Array.from({length: 12}, (_, i) => (
          <div key={i}>{Array.from({length: 6}, () => Math.random().toString(16).slice(2, 6)).join(' ')}</div>
        ))}
      </div>
      <div className="absolute bottom-20 right-10 text-[#C0A040] opacity-10 font-mono text-xs leading-relaxed select-none hidden lg:block">
        {Array.from({length: 12}, (_, i) => (
          <div key={i}>{Array.from({length: 6}, () => Math.random().toString(16).slice(2, 6)).join(' ')}</div>
        ))}
      </div>

      <div className="relative z-10 text-center px-4 max-w-6xl mx-auto">
        {/* Version badge */}
        <div className="inline-flex items-center gap-2 mb-8 px-4 py-1.5 rounded-full border border-[#C0A040]/30 bg-[#C0A040]/5">
          <div className="w-2 h-2 rounded-full bg-[#10B981] animate-pulse" />
          <span className="font-mono text-xs text-[#C0A040] tracking-widest uppercase">
            Arena v5.0 — Architecture Score: 100/100
          </span>
        </div>

        {/* Title */}
        <h1 className={`text-6xl md:text-8xl lg:text-9xl font-black tracking-tighter mb-4 ${glitch ? 'translate-x-[2px] text-[#DC2626]' : 'text-[#E5E4E2]'} transition-all duration-75`}>
          <span className="text-shadow-sovereign">THE</span>
          <br />
          <span className="bg-gradient-to-r from-[#C0A040] via-[#E8D080] to-[#C0A040] bg-clip-text text-transparent">
            PLATINUM
          </span>
          <br />
          <span className="text-shadow-sovereign">BATTLE</span>
        </h1>

        {/* Subtitle */}
        <p className="text-lg md:text-xl text-[#B0AFA8] max-w-2xl mx-auto mb-8 font-light">
          From <span className="text-[#22D3EE] font-semibold">Fast</span> to{' '}
          <span className="text-[#C0A040] font-semibold text-shadow-sovereign">Infinite & Immortal</span>
          <br />
          <span className="text-sm opacity-60">The Sovereign IPC Transport Layer — 1µs Constant Gate</span>
        </p>

        {/* Live metrics bar */}
        <div className="flex flex-wrap justify-center gap-6 md:gap-10 mt-12 mb-8">
          <MetricPill label="GATE LATENCY" value={`${latencyDisplay}ns`} sub="< 1µs ✓" color="nerve" />
          <MetricPill label="PIPE TYPE" value="SPSC" sub="Single Writer" color="sovereign" />
          <MetricPill label="HEAP ALLOC" value="ZERO" sub="SlabPool Only" color="safe" />
          <MetricPill label="NODE COMPRESS" value="12/6/4" sub="Core Pinned" color="signal" />
        </div>

        {/* Scroll indicator */}
        <div className="mt-16 animate-bounce">
          <svg className="w-6 h-6 mx-auto text-[#C0A040] opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 14l-7 7m0 0l-7-7m7 7V3" />
          </svg>
        </div>
      </div>

      {/* Bottom border accent */}
      <div className="absolute bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-[#C0A040]/40 to-transparent" />
    </section>
  );
}

function MetricPill({ label, value, sub, color }: { label: string; value: string; sub: string; color: string }) {
  const colors: Record<string, string> = {
    nerve: 'text-[#22D3EE] border-[#22D3EE]/20',
    sovereign: 'text-[#C0A040] border-[#C0A040]/20',
    safe: 'text-[#10B981] border-[#10B981]/20',
    signal: 'text-[#A855F7] border-[#A855F7]/20',
  };
  return (
    <div className={`flex flex-col items-center px-4 py-2 rounded-lg border ${colors[color]} bg-[#12121A]/80`}>
      <span className="font-mono text-[10px] tracking-widest text-[#B0AFA8] uppercase">{label}</span>
      <span className={`font-mono text-2xl font-bold ${colors[color].split(' ')[0]}`}>{value}</span>
      <span className="font-mono text-[10px] text-[#B0AFA8]/60">{sub}</span>
    </div>
  );
}
