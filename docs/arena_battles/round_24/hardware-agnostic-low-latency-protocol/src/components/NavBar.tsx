import { motion } from 'framer-motion';
import { Shield, ChevronDown } from 'lucide-react';
import { useState } from 'react';

const navItems = [
  { id: 'topology', label: 'Topology' },
  { id: 'striping', label: 'Striping' },
  { id: 'metrics', label: 'Metrics' },
  { id: 'safety', label: 'Safety' },
  { id: 'fenceless', label: 'Fence-Less' },
  { id: 'adversarial', label: 'Adversarial' },
  { id: 'code', label: 'Code' },
];

interface NavBarProps {
  activeSection: string;
  onNavigate: (id: string) => void;
}

export function NavBar({ activeSection, onNavigate }: NavBarProps) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 bg-[#0a0a0f]/80 backdrop-blur-xl border-b border-white/[0.06]">
      <div className="max-w-7xl mx-auto px-4 md:px-6">
        <div className="flex items-center justify-between h-14">
          {/* Logo */}
          <div className="flex items-center gap-2.5">
            <div className="p-1.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20">
              <Shield className="w-4 h-4 text-emerald-400" />
            </div>
            <span className="text-sm font-bold text-white font-mono tracking-wide">
              SOVEREIGN <span className="text-emerald-400">V24</span>
            </span>
          </div>

          {/* Desktop Nav */}
          <div className="hidden md:flex items-center gap-1">
            {navItems.map((item) => (
              <button
                key={item.id}
                onClick={() => onNavigate(item.id)}
                className={`px-3 py-1.5 rounded-md text-xs font-mono transition-colors ${
                  activeSection === item.id
                    ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'
                    : 'text-gray-500 hover:text-white hover:bg-white/5'
                }`}
              >
                {item.label}
              </button>
            ))}
          </div>

          {/* Mobile menu button */}
          <button
            className="md:hidden p-2 rounded-lg text-gray-400 hover:text-white hover:bg-white/5"
            onClick={() => setMobileOpen(!mobileOpen)}
          >
            <ChevronDown className={`w-5 h-5 transition-transform ${mobileOpen ? 'rotate-180' : ''}`} />
          </button>
        </div>

        {/* Mobile Nav */}
        {mobileOpen && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            className="md:hidden border-t border-white/[0.06] py-3"
          >
            <div className="flex flex-wrap gap-2">
              {navItems.map((item) => (
                <button
                  key={item.id}
                  onClick={() => {
                    onNavigate(item.id);
                    setMobileOpen(false);
                  }}
                  className={`px-3 py-1.5 rounded-md text-xs font-mono transition-colors ${
                    activeSection === item.id
                      ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'
                      : 'text-gray-500 hover:text-white hover:bg-white/5'
                  }`}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </motion.div>
        )}
      </div>
    </nav>
  );
}
