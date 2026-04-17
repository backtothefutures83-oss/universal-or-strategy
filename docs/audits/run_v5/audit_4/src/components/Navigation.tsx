import { useState, useEffect } from 'react';

interface Props {
  activeSection: string;
}

export default function Navigation({ activeSection }: Props) {
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const handler = () => setScrolled(window.scrollY > 100);
    window.addEventListener('scroll', handler);
    return () => window.removeEventListener('scroll', handler);
  }, []);

  const sections = [
    { id: 'hero', label: 'OVERVIEW', short: 'OV' },
    { id: 'architecture', label: 'ARCHITECTURE', short: 'AR' },
    { id: 'challenge-1', label: 'SIDEBAND', short: 'C1' },
    { id: 'challenge-2', label: 'MIRROR', short: 'C2' },
    { id: 'challenge-3', label: 'GATE', short: 'C3' },
    { id: 'metrics', label: 'METRICS', short: 'MT' },
    { id: 'banned', label: 'BANNED', short: 'BN' },
  ];

  const scrollTo = (id: string) => {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
  };

  return (
    <nav className={`fixed top-0 left-0 right-0 z-50 transition-all duration-300 ${
      scrolled ? 'bg-[#0A0A0F]/95 backdrop-blur-md border-b border-[#C0A040]/10' : 'bg-transparent'
    }`}>
      <div className="max-w-7xl mx-auto px-4 h-14 flex items-center justify-between">
        {/* Logo */}
        <div className="flex items-center gap-3">
          <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-[#C0A040] to-[#E8D080] flex items-center justify-center">
            <span className="text-[#0A0A0F] font-black text-xs">Pt</span>
          </div>
          <span className="font-mono text-xs text-[#C0A040] tracking-widest hidden sm:block">PLATINUM v5</span>
        </div>

        {/* Navigation links */}
        <div className="flex items-center gap-1">
          {sections.map(s => (
            <button
              key={s.id}
              onClick={() => scrollTo(s.id)}
              className={`px-2 md:px-3 py-1.5 rounded-md font-mono text-[10px] tracking-wider transition-all cursor-pointer ${
                activeSection === s.id
                  ? 'bg-[#C0A040]/10 text-[#C0A040]'
                  : 'text-[#B0AFA8]/40 hover:text-[#B0AFA8] hover:bg-[#1A1A28]'
              }`}
            >
              <span className="hidden md:inline">{s.label}</span>
              <span className="md:hidden">{s.short}</span>
            </button>
          ))}
        </div>

        {/* Status indicator */}
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full bg-[#10B981] animate-pulse" />
          <span className="font-mono text-[10px] text-[#10B981] hidden sm:block">LIVE</span>
        </div>
      </div>
    </nav>
  );
}
