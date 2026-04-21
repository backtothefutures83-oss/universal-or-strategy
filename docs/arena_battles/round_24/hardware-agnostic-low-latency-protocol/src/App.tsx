import { useState, useCallback, useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { HeroSection } from './components/HeroSection';
import { NavBar } from './components/NavBar';
import { TopologyDetector } from './components/TopologyDetector';
import { CacheStripeVisualizer } from './components/CacheStripeVisualizer';
import { AdaptiveStriping } from './components/AdaptiveStriping';
import { SafetyInvariants } from './components/SafetyInvariants';
import { PerformanceMetrics } from './components/PerformanceMetrics';
import { FenceLessDiscipline } from './components/FenceLessDiscipline';
import { AdversarialChallenge } from './components/AdversarialChallenge';
import { CodeSubmission } from './components/CodeSubmission';
import { usePerformanceSimulation } from './hooks/usePerformanceSimulation';
import { Shield } from 'lucide-react';

export default function App() {
  const [activeSection, setActiveSection] = useState('topology');
  const [bootSequence, setBootSequence] = useState(true);
  const [bootPhase, setBootPhase] = useState(0);
  const sectionRefs = useRef<Record<string, HTMLDivElement | null>>({});

  const {
    latencyData,
    throughputData,
    contentionData,
    stripingMode,
    handshakeCount,
    currentPhase,
    phaseNames,
    safetyInvariants,
  } = usePerformanceSimulation();

  // Boot sequence animation
  useEffect(() => {
    const timer = setInterval(() => {
      setBootPhase((prev) => {
        if (prev >= 4) {
          clearInterval(timer);
          setTimeout(() => setBootSequence(false), 500);
          return 4;
        }
        return prev + 1;
      });
    }, 600);
    return () => clearInterval(timer);
  }, []);

  const handleNavigate = useCallback((id: string) => {
    setActiveSection(id);
    const el = sectionRefs.current[id];
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }, []);

  // Scroll spy
  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            setActiveSection(entry.target.id);
          }
        });
      },
      { threshold: 0.3 }
    );

    Object.values(sectionRefs.current).forEach((el) => {
      if (el) observer.observe(el);
    });

    return () => observer.disconnect();
  }, [bootSequence]);

  if (bootSequence) {
    return (
      <div className="min-h-screen bg-[#0a0a0f] flex items-center justify-center">
        <div className="text-center max-w-md px-6">
          <div className="flex items-center justify-center gap-3 mb-8">
            <Shield className="w-8 h-8 text-emerald-400 animate-pulse" />
            <span className="text-xl font-bold text-white font-mono">
              SOVEREIGN <span className="text-emerald-400">V24</span>
            </span>
          </div>

          <div className="space-y-3 text-left">
            {['Initializing hardware topology detection...', 'Probing L1/L2/L3 cache line widths...', 'Verifying NUMA node distances...', 'Activating safety invariants...', 'Fence-less protocol: ONLINE'].map((step, i) => (
              <motion.div
                key={step}
                initial={{ opacity: 0, x: -10 }}
                animate={{ opacity: i <= bootPhase ? 1 : 0.2, x: 0 }}
                className={`flex items-center gap-3 font-mono text-sm ${
                  i < bootPhase
                    ? 'text-emerald-400'
                    : i === bootPhase
                    ? 'text-cyan-400'
                    : 'text-gray-700'
                }`}
              >
                {i < bootPhase ? (
                  <span className="text-emerald-400">✓</span>
                ) : i === bootPhase ? (
                  <span className="animate-pulse">▸</span>
                ) : (
                  <span className="text-gray-700">○</span>
                )}
                {step}
              </motion.div>
            ))}
          </div>

          <div className="mt-8 w-full bg-white/5 rounded-full h-1 overflow-hidden">
            <motion.div
              className="h-full bg-gradient-to-r from-emerald-400 to-cyan-400"
              animate={{ width: `${((bootPhase + 1) / 5) * 100}%` }}
              transition={{ duration: 0.3 }}
            />
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#0a0a0f] text-white">
      <NavBar activeSection={activeSection} onNavigate={handleNavigate} />

      {/* Status bar */}
      <div className="fixed bottom-0 left-0 right-0 z-50 bg-[#0a0a0f]/80 backdrop-blur-xl border-t border-white/[0.06]">
        <div className="max-w-7xl mx-auto px-4 md:px-6 py-2">
          <div className="flex items-center justify-between text-[10px] font-mono text-gray-500">
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-1.5">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                <span>PROTOCOL: ONLINE</span>
              </div>
              <span>PHASE: <span className="text-emerald-400">{phaseNames[currentPhase]}</span></span>
              <span>HANDSHAKES: <span className="text-white">{handshakeCount.toLocaleString()}</span></span>
            </div>
            <div className="hidden md:flex items-center gap-4">
              <span>LATENCY: <span className="text-white">{latencyData.length > 0 ? latencyData[latencyData.length - 1].value.toFixed(3) : '—'}ns</span></span>
              <span>MODE: <span className="text-cyan-400">{stripingMode.mode}</span></span>
              <span>BUILD: <span className="text-gray-600">SOV-V24-GLOBAL-ROBUST</span></span>
            </div>
          </div>
        </div>
      </div>

      {/* Hero */}
      <HeroSection />

      {/* Main content */}
      <div className="max-w-7xl mx-auto px-4 md:px-6 pb-24 space-y-8">
        {/* Section: Hardware Topology */}
        <motion.div
          ref={(el) => { sectionRefs.current['topology'] = el; }}
          id="topology"
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
        >
          <TopologyDetector isActive={true} />
        </motion.div>

        {/* Two column: Striping + Cache Visualizer */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          <motion.div
            ref={(el) => { sectionRefs.current['striping'] = el; }}
            id="striping"
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
          >
            <AdaptiveStriping stripingMode={stripingMode} />
          </motion.div>

          <div
            ref={(el) => { sectionRefs.current['cache'] = el; }}
            className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
          >
            <CacheStripeVisualizer currentMode={stripingMode.mode} />
          </div>
        </div>

        {/* Performance Metrics */}
        <motion.div
          ref={(el) => { sectionRefs.current['metrics'] = el; }}
          id="metrics"
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
        >
          <PerformanceMetrics
            latencyData={latencyData}
            throughputData={throughputData}
            contentionData={contentionData}
          />
        </motion.div>

        {/* Safety Invariants */}
        <motion.div
          ref={(el) => { sectionRefs.current['safety'] = el; }}
          id="safety"
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
        >
          <SafetyInvariants invariants={safetyInvariants} />
        </motion.div>

        {/* Fence-Less Discipline */}
        <motion.div
          ref={(el) => { sectionRefs.current['fenceless'] = el; }}
          id="fenceless"
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
        >
          <FenceLessDiscipline />
        </motion.div>

        {/* Adversarial Challenge */}
        <motion.div
          ref={(el) => { sectionRefs.current['adversarial'] = el; }}
          id="adversarial"
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
        >
          <AdversarialChallenge />
        </motion.div>

        {/* Code Submission */}
        <motion.div
          ref={(el) => { sectionRefs.current['code'] = el; }}
          id="code"
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="rounded-2xl bg-white/[0.01] border border-white/[0.04] overflow-hidden"
        >
          <CodeSubmission />
        </motion.div>

        {/* Footer */}
        <div className="text-center py-12 border-t border-white/[0.04]">
          <div className="flex items-center justify-center gap-2 mb-3">
            <Shield className="w-5 h-5 text-emerald-400" />
            <span className="text-sm font-bold text-white font-mono">
              SOVEREIGN <span className="text-emerald-400">V24</span>
            </span>
          </div>
          <p className="text-xs text-gray-600 font-mono mb-1">
            Build Tag: SOV-V24-GLOBAL-ROBUST
          </p>
          <p className="text-xs text-gray-700 font-mono">
            The Global Zero-Friction Handshake Protocol — Sub-0.5ns Cross-Platform Resilient
          </p>
        </div>
      </div>
    </div>
  );
}
