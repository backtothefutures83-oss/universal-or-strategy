import { useState, useEffect, useCallback } from 'react';
import Navbar from './components/Navbar';
import Hero from './components/Hero';
import TopologyMap from './components/TopologyMap';
import SafetyPanel from './components/SafetyPanel';
import StripingVisualizer from './components/StripingVisualizer';
import FencelessCore from './components/FencelessCore';
import LatencyBenchmark from './components/LatencyBenchmark';
import CodeViewer from './components/CodeViewer';

const sections = ['hero', 'topology', 'safety', 'striping', 'fenceless', 'benchmark', 'code'];

export default function App() {
  const [activeSection, setActiveSection] = useState('hero');

  const handleNavigate = useCallback((id: string) => {
    const el = document.getElementById(id);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }, []);

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            setActiveSection(entry.target.id);
          }
        });
      },
      { threshold: 0.3, rootMargin: '-80px 0px -50% 0px' }
    );

    sections.forEach(id => {
      const el = document.getElementById(id);
      if (el) observer.observe(el);
    });

    return () => observer.disconnect();
  }, []);

  return (
    <div className="min-h-screen bg-sov-bg">
      <Navbar activeSection={activeSection} onNavigate={handleNavigate} />
      
      <main>
        <Hero />
        <TopologyMap />
        <SafetyPanel />
        <StripingVisualizer />
        <FencelessCore />
        <LatencyBenchmark />
        <CodeViewer />
      </main>

      {/* Footer */}
      <footer className="border-t border-sov-border bg-sov-surface/30 py-8">
        <div className="max-w-6xl mx-auto px-4 text-center">
          <div className="flex items-center justify-center gap-3 mb-3">
            <div className="w-6 h-6 rounded-md bg-gradient-to-br from-sov-cyan to-sov-purple flex items-center justify-center">
              <span className="text-xs font-bold text-white font-mono">S</span>
            </div>
            <span className="text-sm font-semibold text-sov-text-bright font-mono">
              SOVEREIGN <span className="text-sov-cyan">V24</span>
            </span>
          </div>
          <p className="text-xs font-mono text-sov-text-dim mb-1">
            Global Zero-Friction Handshake Protocol
          </p>
          <p className="text-xs font-mono text-sov-text-dim/50">
            Build Tag: SOV-V24-GLOBAL-ROBUST — Target: &lt; 0.5ns Cross-Platform Resilient
          </p>
          <div className="mt-4 flex items-center justify-center gap-2">
            <span className="w-2 h-2 rounded-full bg-sov-green animate-glow-pulse" />
            <span className="text-xs font-mono text-sov-green">ALL INVARIANTS SATISFIED</span>
          </div>
        </div>
      </footer>
    </div>
  );
}
