import { useEffect, useRef, useState } from 'react';
import { ChevronDown, Cpu, Zap, Shield, Layers } from 'lucide-react';

export function HeroSection() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [latency, setLatency] = useState('0.47');
  const [detectedCpu, setDetectedCpu] = useState('');
  const [cacheLine, setCacheLine] = useState('');

  // Simulate hardware detection animation
  useEffect(() => {
    const steps = [
      { cpu: 'Detecting CPU topology...', cache: '' },
      { cpu: 'AMD Ryzen 9 7950X', cache: 'L1: 64B | L2: 1MB | L3: 64MB' },
      { cpu: 'NUMA nodes: 2', cache: 'Stripes: Adaptive' },
      { cpu: '✓ Topology mapped', cache: '✓ Cache aligned' },
      { cpu: 'AMD Zen 4 — Dual NUMA', cache: 'L1: 32B → L3: 64B stripe' },
    ];
    let i = 0;
    const interval = setInterval(() => {
      if (i < steps.length) {
        setDetectedCpu(steps[i].cpu);
        setCacheLine(steps[i].cache);
        i++;
      } else {
        clearInterval(interval);
      }
    }, 800);
    return () => clearInterval(interval);
  }, []);

  // Latency counter animation
  useEffect(() => {
    const interval = setInterval(() => {
      const val = (0.44 + Math.random() * 0.06).toFixed(2);
      setLatency(val);
    }, 150);
    return () => clearInterval(interval);
  }, []);

  // Canvas grid animation
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let animId: number;
    const resize = () => {
      canvas.width = window.innerWidth;
      canvas.height = window.innerHeight;
    };
    resize();
    window.addEventListener('resize', resize);

    const particles: { x: number; y: number; vx: number; vy: number; size: number; alpha: number }[] = [];
    for (let i = 0; i < 80; i++) {
      particles.push({
        x: Math.random() * canvas.width,
        y: Math.random() * canvas.height,
        vx: (Math.random() - 0.5) * 0.3,
        vy: (Math.random() - 0.5) * 0.3,
        size: Math.random() * 2 + 0.5,
        alpha: Math.random() * 0.5 + 0.1,
      });
    }

    const draw = () => {
      ctx.clearRect(0, 0, canvas.width, canvas.height);

      // Draw grid
      ctx.strokeStyle = 'rgba(0, 240, 255, 0.04)';
      ctx.lineWidth = 0.5;
      const gridSize = 60;
      for (let x = 0; x < canvas.width; x += gridSize) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvas.height);
        ctx.stroke();
      }
      for (let y = 0; y < canvas.height; y += gridSize) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
        ctx.stroke();
      }

      // Draw and update particles
      particles.forEach((p, i) => {
        p.x += p.vx;
        p.y += p.vy;
        if (p.x < 0 || p.x > canvas.width) p.vx *= -1;
        if (p.y < 0 || p.y > canvas.height) p.vy *= -1;

        ctx.beginPath();
        ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(0, 240, 255, ${p.alpha})`;
        ctx.fill();

        // Connect nearby particles
        for (let j = i + 1; j < particles.length; j++) {
          const dx = particles[j].x - p.x;
          const dy = particles[j].y - p.y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          if (dist < 150) {
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
            ctx.lineTo(particles[j].x, particles[j].y);
            ctx.strokeStyle = `rgba(0, 240, 255, ${0.06 * (1 - dist / 150)})`;
            ctx.lineWidth = 0.5;
            ctx.stroke();
          }
        }
      });

      animId = requestAnimationFrame(draw);
    };
    draw();

    return () => {
      cancelAnimationFrame(animId);
      window.removeEventListener('resize', resize);
    };
  }, []);

  return (
    <section id="hero" className="relative min-h-screen flex items-center justify-center overflow-hidden">
      <canvas ref={canvasRef} className="absolute inset-0 w-full h-full" />

      {/* Scan line effect */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="w-full h-px bg-gradient-to-r from-transparent via-cyan-neon/20 to-transparent animate-scan-line" />
      </div>

      {/* Radial gradient overlay */}
      <div className="absolute inset-0 bg-gradient-to-b from-sovereign-950 via-transparent to-sovereign-950 pointer-events-none" />
      <div className="absolute inset-0 bg-gradient-radial from-cyan-neon/5 via-transparent to-transparent pointer-events-none" />

      <div className="relative z-10 text-center px-4 max-w-5xl mx-auto">
        {/* Build tag */}
        <div className="inline-flex items-center gap-2 font-mono text-[10px] sm:text-xs text-cyan-dim mb-6 px-4 py-2 border border-cyan-dim/30 rounded-full bg-cyan-neon/5">
          <span className="w-1.5 h-1.5 rounded-full bg-cyan-neon animate-glow-pulse" />
          PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST
        </div>

        {/* Main title */}
        <h1 className="font-mono text-4xl sm:text-6xl md:text-7xl lg:text-8xl font-black tracking-tighter mb-2">
          <span className="text-slate-500">SOVEREIGN</span>
          <span className="text-cyan-neon neon-glow ml-3">V24</span>
        </h1>

        {/* Subtitle */}
        <p className="font-mono text-sm sm:text-base md:text-lg text-slate-400 mb-8 tracking-wide">
          The Global Zero-Friction Handshake Protocol
        </p>

        {/* Latency display */}
        <div className="inline-block mb-10">
          <div className="relative">
            <div className="text-5xl sm:text-7xl md:text-8xl font-mono font-black text-green-neon neon-glow-green tabular-nums">
              {latency}
            </div>
            <span className="absolute -right-16 top-2 font-mono text-lg text-green-neon/60">ns</span>
          </div>
          <p className="font-mono text-[10px] text-green-dim mt-1 tracking-widest">
            MEASURED LATENCY — TARGET: &lt;0.5ns
          </p>
        </div>

        {/* Hardware detection status */}
        <div className="max-w-md mx-auto space-y-1 mb-10">
          <div className="font-mono text-[10px] text-slate-600">— HARDWARE AUTO-DETECTION —</div>
          <div className="font-mono text-xs text-cyan-neon/80">{detectedCpu}</div>
          <div className="font-mono text-xs text-cyan-neon/60">{cacheLine}</div>
        </div>

        {/* Feature cards */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 max-w-3xl mx-auto mb-12">
          {[
            { icon: Cpu, label: 'HW Auto-Detect', desc: 'L1/L2/L3/NUMA' },
            { icon: Shield, label: 'Fence-Less', desc: 'ADR-015 Compliant' },
            { icon: Layers, label: 'Adaptive Stripe', desc: 'L1↔L2 Shift' },
            { icon: Zap, label: 'Zero-Friction', desc: 'Safety Invariants' },
          ].map((item, i) => (
            <div
              key={i}
              className="neon-box rounded-lg p-4 text-left group hover:border-cyan-neon/40 transition-all cursor-default animate-fade-in-up"
              style={{ animationDelay: `${i * 100}ms`, opacity: 0, animationFillMode: 'forwards' }}
            >
              <item.icon className="w-5 h-5 text-cyan-neon mb-2 group-hover:animate-glow-pulse" />
              <div className="font-mono text-xs font-semibold text-slate-200">{item.label}</div>
              <div className="font-mono text-[10px] text-slate-500">{item.desc}</div>
            </div>
          ))}
        </div>

        {/* Scroll indicator */}
        <button
          onClick={() => document.getElementById('architecture')?.scrollIntoView({ behavior: 'smooth' })}
          className="animate-bounce text-slate-600 hover:text-cyan-neon transition-colors"
        >
          <ChevronDown className="w-6 h-6 mx-auto" />
        </button>
      </div>
    </section>
  );
}
