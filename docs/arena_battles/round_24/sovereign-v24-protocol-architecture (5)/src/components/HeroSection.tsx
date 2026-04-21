import React, { useEffect, useRef } from 'react';

const HeroSection: React.FC = () => {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let animationId: number;
    const particles: { x: number; y: number; vx: number; vy: number; size: number; opacity: number }[] = [];

    const resize = () => {
      canvas.width = canvas.offsetWidth * window.devicePixelRatio;
      canvas.height = canvas.offsetHeight * window.devicePixelRatio;
      ctx.scale(window.devicePixelRatio, window.devicePixelRatio);
    };

    const init = () => {
      resize();
      const count = Math.floor(canvas.offsetWidth / 12);
      for (let i = 0; i < count; i++) {
        particles.push({
          x: Math.random() * canvas.offsetWidth,
          y: Math.random() * canvas.offsetHeight,
          vx: (Math.random() - 0.5) * 0.3,
          vy: (Math.random() - 0.5) * 0.3,
          size: Math.random() * 2 + 0.5,
          opacity: Math.random() * 0.5 + 0.1,
        });
      }
    };

    const animate = () => {
      ctx.clearRect(0, 0, canvas.offsetWidth, canvas.offsetHeight);
      const w = canvas.offsetWidth;
      const h = canvas.offsetHeight;

      particles.forEach((p, i) => {
        p.x += p.vx;
        p.y += p.vy;
        if (p.x < 0 || p.x > w) p.vx *= -1;
        if (p.y < 0 || p.y > h) p.vy *= -1;

        ctx.beginPath();
        ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(6, 182, 212, ${p.opacity})`;
        ctx.fill();

        // Connect nearby particles
        for (let j = i + 1; j < particles.length; j++) {
          const dx = particles[j].x - p.x;
          const dy = particles[j].y - p.y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          if (dist < 120) {
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
            ctx.lineTo(particles[j].x, particles[j].y);
            ctx.strokeStyle = `rgba(6, 182, 212, ${0.08 * (1 - dist / 120)})`;
            ctx.lineWidth = 0.5;
            ctx.stroke();
          }
        }
      });

      animationId = requestAnimationFrame(animate);
    };

    init();
    animate();
    window.addEventListener('resize', init);

    return () => {
      cancelAnimationFrame(animationId);
      window.removeEventListener('resize', init);
    };
  }, []);

  return (
    <section className="relative min-h-screen flex items-center justify-center overflow-hidden">
      {/* Canvas background */}
      <canvas
        ref={canvasRef}
        className="absolute inset-0 w-full h-full"
        style={{ zIndex: 0 }}
      />

      {/* Radial gradient overlay */}
      <div className="absolute inset-0 bg-gradient-to-b from-transparent via-slate-950/50 to-slate-950" style={{ zIndex: 1 }} />

      {/* Central glow */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-cyan-500/10 rounded-full blur-[120px] animate-glow-pulse" style={{ zIndex: 1 }} />

      {/* Scan line */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none" style={{ zIndex: 2 }}>
        <div className="w-full h-[2px] bg-gradient-to-r from-transparent via-cyan-400/30 to-transparent animate-scan-line" />
      </div>

      {/* Content */}
      <div className="relative text-center px-4" style={{ zIndex: 3 }}>
        {/* Protocol badge */}
        <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full border border-cyan-500/30 bg-cyan-500/5 mb-8 animate-fade-in-up">
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          <span className="text-xs font-mono text-cyan-300 tracking-widest uppercase">Protocol Active — V24.0-rc1</span>
        </div>

        {/* Main title */}
        <h1 className="text-6xl md:text-8xl lg:text-9xl font-black tracking-tighter mb-4 animate-fade-in-up" style={{ animationDelay: '0.1s' }}>
          <span className="text-shimmer">SOVEREIGN</span>
        </h1>

        {/* Version */}
        <div className="flex items-center justify-center gap-4 mb-6 animate-fade-in-up" style={{ animationDelay: '0.2s' }}>
          <div className="h-px w-16 bg-gradient-to-r from-transparent to-cyan-500/50" />
          <span className="text-3xl md:text-4xl font-bold text-slate-300 font-mono">V24</span>
          <div className="h-px w-16 bg-gradient-to-l from-transparent to-cyan-500/50" />
        </div>

        {/* Subtitle */}
        <p className="text-lg md:text-xl text-slate-400 max-w-2xl mx-auto mb-8 animate-fade-in-up font-light leading-relaxed" style={{ animationDelay: '0.3s' }}>
          The Global Zero-Friction Handshake
          <br />
          <span className="text-cyan-400/80 font-mono text-sm">Cross-Platform Resilient • &lt; 0.5ns Latency • Fence-Less Discipline</span>
        </p>

        {/* Key stats row */}
        <div className="flex flex-wrap justify-center gap-8 md:gap-16 animate-fade-in-up" style={{ animationDelay: '0.4s' }}>
          {[
            { label: 'Latency', value: '< 0.5ns', color: 'text-emerald-400' },
            { label: 'Throughput', value: '∞ MOPS', color: 'text-cyan-400' },
            { label: 'Topology', value: 'Auto-Detect', color: 'text-purple-400' },
            { label: 'Fences', value: 'ZERO', color: 'text-amber-400' },
          ].map((stat) => (
            <div key={stat.label} className="text-center">
              <div className={`text-2xl md:text-3xl font-bold font-mono ${stat.color}`}>{stat.value}</div>
              <div className="text-xs text-slate-500 uppercase tracking-wider mt-1">{stat.label}</div>
            </div>
          ))}
        </div>

        {/* Scroll indicator */}
        <div className="mt-16 animate-float">
          <div className="w-6 h-10 rounded-full border-2 border-cyan-500/30 mx-auto flex justify-center pt-2">
            <div className="w-1 h-3 bg-cyan-400 rounded-full animate-bounce" />
          </div>
        </div>
      </div>
    </section>
  );
};

export default HeroSection;
