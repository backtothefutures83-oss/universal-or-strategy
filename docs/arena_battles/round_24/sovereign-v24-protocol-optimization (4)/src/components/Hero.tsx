import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { ChevronDown, Cpu, Zap, Shield, Layers } from 'lucide-react';

const metrics = [
  { label: 'V23.1 Baseline', value: '0.87', unit: 'ns', icon: Layers, color: 'text-sov-amber' },
  { label: 'V24 Target', value: '< 0.50', unit: 'ns', icon: Zap, color: 'text-sov-cyan' },
  { label: 'Topology Detect', value: 'AUTO', unit: '', icon: Cpu, color: 'text-sov-green' },
  { label: 'Fence-Less Safety', value: '100%', unit: '', icon: Shield, color: 'text-sov-purple' },
];

const heroWords = ['Zero-Friction', 'Fence-Less', 'Hardware-Agnostic', 'Sub-Nanosecond'];

export function Hero() {
  const [wordIndex, setWordIndex] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setWordIndex((prev) => (prev + 1) % heroWords.length);
    }, 2500);
    return () => clearInterval(interval);
  }, []);

  return (
    <section className="relative min-h-screen flex flex-col items-center justify-center overflow-hidden grid-bg">
      {/* Background effects */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-sov-cyan/5 rounded-full blur-3xl" />
        <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-sov-purple/5 rounded-full blur-3xl" />
        <div className="absolute inset-0 bg-gradient-to-b from-transparent via-transparent to-sov-bg" />
      </div>

      {/* Scan line effect */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-20">
        <div className="w-full h-1 bg-sov-cyan/30 animate-scan-line" />
      </div>

      {/* Tag */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.8 }}
        className="mb-6 px-4 py-1.5 rounded-full border border-sov-cyan/20 bg-sov-cyan/5 text-sov-cyan text-xs font-mono tracking-widest"
      >
        PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST
      </motion.div>

      {/* Title */}
      <motion.h1
        initial={{ opacity: 0, y: 30 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.8, delay: 0.2 }}
        className="text-5xl sm:text-7xl md:text-8xl font-black text-center leading-[0.9] mb-6"
      >
        <span className="text-sov-text">SOVEREIGN</span>
        <br />
        <span className="bg-gradient-to-r from-sov-cyan via-sov-green to-sov-purple bg-clip-text text-transparent">
          V24
        </span>
      </motion.h1>

      {/* Rotating subtitle */}
      <div className="h-10 mb-8 overflow-hidden">
        <motion.p
          key={wordIndex}
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -30 }}
          className="text-xl sm:text-2xl text-sov-text-dim font-light text-center"
        >
          The Global <span className="text-sov-cyan font-semibold">{heroWords[wordIndex]}</span> Handshake
        </motion.p>
      </div>

      {/* Metrics grid */}
      <motion.div
        initial={{ opacity: 0, y: 40 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.8, delay: 0.5 }}
        className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 max-w-3xl mx-auto px-4 mb-12"
      >
        {metrics.map((m, i) => (
          <motion.div
            key={m.label}
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ delay: 0.6 + i * 0.15 }}
            className="glass-panel rounded-xl p-4 text-center glow-cyan hover:scale-105 transition-transform"
          >
            <m.icon className={`w-5 h-5 mx-auto mb-2 ${m.color}`} />
            <div className={`text-2xl font-bold font-mono ${m.color}`}>
              {m.value}<span className="text-sm text-sov-text-dim ml-1">{m.unit}</span>
            </div>
            <div className="text-xs text-sov-text-dim mt-1">{m.label}</div>
          </motion.div>
        ))}
      </motion.div>

      {/* Scroll indicator */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 1.5, duration: 1 }}
        className="absolute bottom-8 text-sov-text-dim animate-bounce"
      >
        <ChevronDown className="w-6 h-6" />
      </motion.div>
    </section>
  );
}
