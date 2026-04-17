import { useState, useEffect } from 'react';
import { HeroSection } from './components/HeroSection';
import { ArchitectureSection } from './components/ArchitectureSection';
import { CodeSection } from './components/CodeSection';
import { SafetySection } from './components/SafetySection';
import { MetricsSection } from './components/MetricsSection';

function App() {
  const [activeSection, setActiveSection] = useState('hero');
  const [scrollProgress, setScrollProgress] = useState(0);

  useEffect(() => {
    const handleScroll = () => {
      const scrollTop = window.scrollY;
      const docHeight = document.documentElement.scrollHeight - window.innerHeight;
      setScrollProgress(docHeight > 0 ? (scrollTop / docHeight) * 100 : 0);
      
      const sections = ['hero', 'architecture', 'topology', 'striping', 'code', 'safety', 'metrics'];
      for (const id of sections) {
        const el = document.getElementById(id);
        if (el) {
          const rect = el.getBoundingClientRect();
          if (rect.top <= 200 && rect.bottom > 200) {
            setActiveSection(id);
            break;
          }
        }
      }
    };
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  const scrollTo = (id: string) => {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
  };

  const navItems = [
    { id: 'hero', label: 'V24' },
    { id: 'architecture', label: 'Architecture' },
    { id: 'topology', label: 'Topology' },
    { id: 'striping', label: 'Striping' },
    { id: 'code', label: 'Code' },
    { id: 'safety', label: 'Safety' },
    { id: 'metrics', label: 'Metrics' },
  ];

  return (
    <div className="min-h-screen bg-sovereign-950">
      {/* Scroll progress bar */}
      <div className="fixed top-0 left-0 right-0 z-50 h-[2px] bg-transparent">
        <div
          className="h-full bg-gradient-to-r from-cyan-neon via-green-neon to-purple-neon transition-all duration-150"
          style={{ width: `${scrollProgress}%` }}
        />
      </div>

      {/* Navigation */}
      <nav className="fixed top-[2px] left-0 right-0 z-40 glass border-b border-cyan-neon/10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="flex items-center justify-between h-12">
            <button onClick={() => scrollTo('hero')} className="flex items-center gap-2 group">
              <div className="w-2 h-2 rounded-full bg-cyan-neon animate-glow-pulse" />
              <span className="font-mono text-sm font-bold tracking-wider text-cyan-neon group-hover:text-white transition-colors">
                SOVEREIGN<span className="text-slate-400">V24</span>
              </span>
              <span className="hidden sm:inline font-mono text-[10px] text-slate-500 bg-sovereign-800 px-2 py-0.5 rounded border border-slate-700">
                BUILD_TAG: SOV-V24-GLOBAL-ROBUST
              </span>
            </button>
            <div className="hidden md:flex items-center gap-1">
              {navItems.map(item => (
                <button
                  key={item.id}
                  onClick={() => scrollTo(item.id)}
                  className={`px-3 py-1 text-xs font-mono rounded transition-all ${
                    activeSection === item.id
                      ? 'text-cyan-neon bg-cyan-neon/10 border border-cyan-neon/30'
                      : 'text-slate-500 hover:text-slate-300 hover:bg-slate-800/50'
                  }`}
                >
                  {item.label}
                </button>
              ))}
            </div>
            <div className="flex items-center gap-2">
              <div className="w-1.5 h-1.5 rounded-full bg-green-neon animate-glow-pulse" />
              <span className="font-mono text-[10px] text-green-neon">LIVE</span>
            </div>
          </div>
        </div>
      </nav>

      {/* Sections */}
      <HeroSection />
      <ArchitectureSection />
      <CodeSection />
      <SafetySection />
      <MetricsSection />
    </div>
  );
}

export default App;
