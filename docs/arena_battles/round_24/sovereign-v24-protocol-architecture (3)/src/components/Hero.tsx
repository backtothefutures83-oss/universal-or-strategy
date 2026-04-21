import { useState, useEffect, useCallback } from 'react';

// Animated counter hook
function useCounter(end: number, duration: number = 2000, start: boolean = true) {
  const [value, setValue] = useState(0);
  useEffect(() => {
    if (!start) return;
    let startTime: number;
    const step = (timestamp: number) => {
      if (!startTime) startTime = timestamp;
      const progress = Math.min((timestamp - startTime) / duration, 1);
      setValue(progress * end);
      if (progress < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }, [end, duration, start]);
  return value;
}

// Particle type
interface Particle {
  id: number;
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  opacity: number;
}

export function Hero() {
  const [isInView, setIsInView] = useState(false);
  const [particles, setParticles] = useState<Particle[]>([]);
  const [mousePos, setMousePos] = useState({ x: 0, y: 0 });

  const nsValue = useCounter(0.47, 2500, isInView);
  const latencyValue = useCounter(0.87, 2000, isInView);
  const nodesValue = useCounter(512, 1800, isInView);
  const bandwidthValue = useCounter(248, 2200, isInView);

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    setMousePos({ x: e.clientX, y: e.clientY });
  }, []);

  useEffect(() => {
    setIsInView(true);
    const newParticles: Particle[] = Array.from({ length: 40 }, (_, i) => ({
      id: i,
      x: Math.random() * 100,
      y: Math.random() * 100,
      vx: (Math.random() - 0.5) * 0.3,
      vy: (Math.random() - 0.5) * 0.3,
      size: Math.random() * 2 + 1,
      opacity: Math.random() * 0.5 + 0.1,
    }));
    setParticles(newParticles);

    const interval = setInterval(() => {
      setParticles(prev => prev.map(p => ({
        ...p,
        x: ((p.x + p.vx + 100) % 100),
        y: ((p.y + p.vy + 100) % 100),
        opacity: Math.sin(Date.now() / 3000 + p.id) * 0.3 + 0.3,
      })));
    }, 50);

    return () => clearInterval(interval);
  }, []);

  return (
    <section 
      className="relative min-h-screen flex flex-col items-center justify-center overflow-hidden hex-bg"
      onMouseMove={handleMouseMove}
    >
      {/* Background particles */}
      <div className="absolute inset-0 pointer-events-none">
        {particles.map(p => (
          <div
            key={p.id}
            className="absolute rounded-full bg-cyan-glow"
            style={{
              left: `${p.x}%`,
              top: `${p.y}%`,
              width: `${p.size}px`,
              height: `${p.size}px`,
              opacity: p.opacity,
              boxShadow: `0 0 ${p.size * 4}px rgba(0, 240, 255, ${p.opacity})`,
            }}
          />
        ))}
      </div>

      {/* Mouse-following glow */}
      <div
        className="absolute w-96 h-96 rounded-full pointer-events-none animate-pulse-glow"
        style={{
          background: 'radial-gradient(circle, rgba(0,240,255,0.08) 0%, transparent 70%)',
          left: mousePos.x - 192,
          top: mousePos.y - 192,
        }}
      />

      {/* Scan line */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-20">
        <div className="w-full h-px bg-gradient-to-r from-transparent via-cyan-glow to-transparent animate-scan-line" />
      </div>

      {/* Grid overlay */}
      <div 
        className="absolute inset-0 pointer-events-none opacity-[0.03]"
        style={{
          backgroundImage: 'linear-gradient(rgba(0,240,255,0.3) 1px, transparent 1px), linear-gradient(90deg, rgba(0,240,255,0.3) 1px, transparent 1px)',
          backgroundSize: '60px 60px',
        }}
      />

      {/* Content */}
      <div className={`relative z-10 text-center px-4 transition-all duration-1000 ${isInView ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-10'}`}>
        {/* Protocol badge */}
        <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-cyan-glow/20 bg-cyan-glow/5 mb-8">
          <div className="w-2 h-2 rounded-full bg-teal-glow animate-pulse" />
          <span className="text-xs font-mono tracking-[0.3em] text-cyan-glow/80 uppercase">
            Protocol Active — Build Tag SOV-V24-GLOBAL-ROBUST
          </span>
        </div>

        {/* Title */}
        <h1 className="text-5xl md:text-7xl lg:text-8xl font-bold tracking-tight mb-4">
          <span className="text-white">SOVEREIGN</span>
          <span className="ml-3 bg-gradient-to-r from-cyan-glow via-teal-glow to-cyan-glow bg-clip-text text-transparent animate-gradient-shift">
            V24
          </span>
        </h1>

        <p className="text-xl md:text-2xl text-cyan-glow/60 font-light tracking-wider mb-2">
          The Global Zero-Friction Handshake
        </p>

        <div className="flex items-center justify-center gap-2 mb-12">
          <div className="h-px w-12 bg-gradient-to-r from-transparent to-cyan-glow/30" />
          <p className="text-sm font-mono text-sov-400 tracking-widest">
            ARCHITECTURAL TARGET: &lt; 0.5ns CROSS-PLATFORM RESILIENT
          </p>
          <div className="h-px w-12 bg-gradient-to-l from-transparent to-cyan-glow/30" />
        </div>

        {/* Primary metric */}
        <div className="mb-16">
          <div className="relative inline-block">
            <span className="text-8xl md:text-9xl font-bold glow-text font-mono text-cyan-glow">
              {nsValue.toFixed(2)}
            </span>
            <span className="text-3xl md:text-4xl text-cyan-glow/60 font-mono ml-2">ns</span>
            <div className="absolute -bottom-2 left-0 right-0 h-px bg-gradient-to-r from-transparent via-cyan-glow/50 to-transparent" />
          </div>
          <p className="text-sm text-sov-400 mt-4 font-mono">
            Achieved latency — 46% improvement over V23.1 baseline (0.87ns)
          </p>
        </div>

        {/* Stats grid */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 max-w-4xl mx-auto">
          {[
            { label: 'V23.1 Baseline', value: `${latencyValue.toFixed(2)}ns`, color: 'text-sov-400' },
            { label: 'V24 Achieved', value: `${nsValue.toFixed(2)}ns`, color: 'text-cyan-glow' },
            { label: 'NUMA Nodes', value: `${Math.floor(nodesValue)}`, color: 'text-teal-glow' },
            { label: 'GB/s Bandwidth', value: `${bandwidthValue.toFixed(0)}`, color: 'text-amber-glow' },
          ].map((stat, i) => (
            <div key={i} className="glass-panel rounded-lg p-4 glow-border transition-all hover:scale-105">
              <div className={`text-2xl font-bold font-mono ${stat.color}`}>{stat.value}</div>
              <div className="text-xs text-sov-400 mt-1 font-mono">{stat.label}</div>
            </div>
          ))}
        </div>

        {/* Scroll indicator */}
        <div className="mt-20 animate-float">
          <div className="flex flex-col items-center gap-2">
            <span className="text-xs font-mono text-sov-400 tracking-widest">EXPLORE PROTOCOL</span>
            <svg className="w-5 h-5 text-cyan-glow/40" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 14l-7 7m0 0l-7-7m7 7V3" />
            </svg>
          </div>
        </div>
      </div>
    </section>
  );
}
