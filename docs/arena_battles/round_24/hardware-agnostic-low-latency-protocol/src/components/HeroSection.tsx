import { motion } from 'framer-motion';
import { Shield, Zap, Cpu, Globe, ArrowRight } from 'lucide-react';

export function HeroSection() {
  return (
    <div className="relative min-h-screen flex items-center justify-center overflow-hidden bg-[#0a0a0f]">
      {/* Animated grid background */}
      <div className="absolute inset-0 opacity-[0.03]">
        <div
          className="w-full h-full"
          style={{
            backgroundImage:
              'linear-gradient(rgba(0,255,170,0.3) 1px, transparent 1px), linear-gradient(90deg, rgba(0,255,170,0.3) 1px, transparent 1px)',
            backgroundSize: '60px 60px',
          }}
        />
      </div>

      {/* Radial glow */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-emerald-500/5 rounded-full blur-[120px]" />
      <div className="absolute top-1/3 left-1/3 w-[400px] h-[400px] bg-cyan-500/5 rounded-full blur-[100px]" />

      <div className="relative z-10 text-center px-6 max-w-5xl mx-auto">
        {/* Protocol badge */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8 }}
          className="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-emerald-500/20 bg-emerald-500/5 text-emerald-400 text-sm font-mono mb-8"
        >
          <Shield className="w-4 h-4" />
          <span>SOV-V24-GLOBAL-ROBUST</span>
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
        </motion.div>

        {/* Main title */}
        <motion.h1
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.15 }}
          className="text-5xl md:text-7xl lg:text-8xl font-bold tracking-tight mb-4"
        >
          <span className="text-white">SOVEREIGN</span>{' '}
          <span className="bg-gradient-to-r from-emerald-400 via-cyan-400 to-blue-400 bg-clip-text text-transparent">
            V24
          </span>
        </motion.h1>

        <motion.p
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.3 }}
          className="text-xl md:text-2xl text-gray-400 font-light mb-2 tracking-wide"
        >
          The Global Zero-Friction Handshake
        </motion.p>

        <motion.p
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.4 }}
          className="text-base text-gray-500 font-mono mb-10"
        >
          Sub-0.5ns Cross-Platform Resilient Protocol
        </motion.p>

        {/* Key metrics row */}
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.5 }}
          className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-12 max-w-3xl mx-auto"
        >
          <div className="p-6 rounded-2xl bg-white/[0.02] border border-white/[0.06] backdrop-blur-sm">
            <div className="flex items-center justify-center gap-2 mb-2">
              <Zap className="w-5 h-5 text-amber-400" />
              <span className="text-sm text-gray-500 font-mono">LATENCY TARGET</span>
            </div>
            <div className="text-4xl font-bold text-white font-mono">
              &lt;0.5<span className="text-lg text-gray-400">ns</span>
            </div>
          </div>

          <div className="p-6 rounded-2xl bg-white/[0.02] border border-white/[0.06] backdrop-blur-sm">
            <div className="flex items-center justify-center gap-2 mb-2">
              <Cpu className="w-5 h-5 text-cyan-400" />
              <span className="text-sm text-gray-500 font-mono">TOPOLOGY</span>
            </div>
            <div className="text-4xl font-bold text-white font-mono">
              AG<span className="text-lg text-gray-400">nostic</span>
            </div>
          </div>

          <div className="p-6 rounded-2xl bg-white/[0.02] border border-white/[0.06] backdrop-blur-sm">
            <div className="flex items-center justify-center gap-2 mb-2">
              <Globe className="w-5 h-5 text-emerald-400" />
              <span className="text-sm text-gray-500 font-mono">SCOPE</span>
            </div>
            <div className="text-4xl font-bold text-white font-mono">
              GL<span className="text-lg text-gray-400">OBAL</span>
            </div>
          </div>
        </motion.div>

        {/* Scroll indicator */}
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ duration: 1, delay: 1 }}
          className="flex flex-col items-center gap-2 text-gray-500"
        >
          <span className="text-xs font-mono tracking-widest">EXPLORE ARCHITECTURE</span>
          <motion.div
            animate={{ y: [0, 8, 0] }}
            transition={{ duration: 2, repeat: Infinity }}
          >
            <ArrowRight className="w-5 h-5 rotate-90" />
          </motion.div>
        </motion.div>
      </div>
    </div>
  );
}
