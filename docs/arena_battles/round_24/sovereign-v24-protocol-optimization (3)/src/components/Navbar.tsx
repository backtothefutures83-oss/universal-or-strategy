import { useState } from 'react';
import { cn } from '../utils/cn';

const navItems = [
  { id: 'hero', label: 'Overview' },
  { id: 'topology', label: 'Topology' },
  { id: 'safety', label: 'Safety' },
  { id: 'striping', label: 'Striping' },
  { id: 'fenceless', label: 'Fence-Less' },
  { id: 'benchmark', label: 'Benchmark' },
  { id: 'code', label: 'Source' },
];

export default function Navbar({ activeSection, onNavigate }: { activeSection: string; onNavigate: (id: string) => void }) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 bg-sov-bg/90 backdrop-blur-xl border-b border-sov-border">
      <div className="max-w-7xl mx-auto px-4 sm:px-6">
        <div className="flex items-center justify-between h-14">
          <div className="flex items-center gap-3">
            <div className="w-7 h-7 rounded-md bg-gradient-to-br from-sov-cyan to-sov-purple flex items-center justify-center">
              <span className="text-xs font-bold text-white font-mono">S</span>
            </div>
            <span className="text-sm font-semibold text-sov-text-bright font-mono tracking-wide">
              SOVEREIGN <span className="text-sov-cyan">V24</span>
            </span>
            <span className="hidden sm:inline-block text-xs text-sov-text-dim font-mono border border-sov-border px-2 py-0.5 rounded">
              SOV-V24-GLOBAL-ROBUST
            </span>
          </div>

          <div className="hidden md:flex items-center gap-1">
            {navItems.map(item => (
              <button
                key={item.id}
                onClick={() => onNavigate(item.id)}
                className={cn(
                  'px-3 py-1.5 text-xs font-mono rounded-md transition-all duration-200',
                  activeSection === item.id
                    ? 'bg-sov-cyan/10 text-sov-cyan border border-sov-cyan/30'
                    : 'text-sov-text-dim hover:text-sov-text hover:bg-sov-surface'
                )}
              >
                {item.label}
              </button>
            ))}
          </div>

          <button
            className="md:hidden text-sov-text-dim p-2"
            onClick={() => setMobileOpen(!mobileOpen)}
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              {mobileOpen ? (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              ) : (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
              )}
            </svg>
          </button>
        </div>

        {mobileOpen && (
          <div className="md:hidden pb-3 flex flex-wrap gap-1">
            {navItems.map(item => (
              <button
                key={item.id}
                onClick={() => { onNavigate(item.id); setMobileOpen(false); }}
                className={cn(
                  'px-3 py-1.5 text-xs font-mono rounded-md transition-all',
                  activeSection === item.id
                    ? 'bg-sov-cyan/10 text-sov-cyan border border-sov-cyan/30'
                    : 'text-sov-text-dim hover:text-sov-text'
                )}
              >
                {item.label}
              </button>
            ))}
          </div>
        )}
      </div>
    </nav>
  );
}
