import { useState, useEffect, useRef, useCallback } from 'react';
import Navigation from './components/Navigation';
import HeroSection from './components/HeroSection';
import ArchitectureDiagram from './components/ArchitectureDiagram';
import Challenge1 from './components/Challenge1_Sideband';
import Challenge2 from './components/Challenge2_Mirror';
import Challenge3 from './components/Challenge3_AtomicGate';
import MetricsDashboard from './components/MetricsDashboard';
import BannedPatterns from './components/BannedPatterns';
import SummaryFooter from './components/SummaryFooter';
import { useSimulation } from './hooks/useSimulation';

export default function App() {
  const { state, triggerNMI } = useSimulation();
  const [activeSection, setActiveSection] = useState('hero');
  const observerRef = useRef<IntersectionObserver | null>(null);

  const setupObserver = useCallback(() => {
    if (observerRef.current) observerRef.current.disconnect();

    observerRef.current = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            setActiveSection(entry.target.id);
          }
        });
      },
      { threshold: 0.3 }
    );

    const sections = document.querySelectorAll('[data-section]');
    sections.forEach((el) => observerRef.current?.observe(el));
  }, []);

  useEffect(() => {
    setupObserver();
    return () => observerRef.current?.disconnect();
  }, [setupObserver]);

  return (
    <div className="min-h-screen bg-[#0A0A0F] text-[#E5E4E2] selection:bg-[#C0A040]/30 selection:text-white">
      <Navigation activeSection={activeSection} />

      <div id="hero" data-section>
        <HeroSection />
      </div>

      <div id="architecture" data-section>
        <ArchitectureDiagram state={state} onTriggerNMI={triggerNMI} />
      </div>

      <div id="challenge-1" data-section>
        <Challenge1 />
      </div>

      <div id="challenge-2" data-section>
        <Challenge2 />
      </div>

      <div id="challenge-3" data-section>
        <Challenge3 state={state} />
      </div>

      <div id="metrics" data-section>
        <MetricsDashboard state={state} />
      </div>

      <div id="banned" data-section>
        <BannedPatterns />
      </div>

      <SummaryFooter />
    </div>
  );
}
