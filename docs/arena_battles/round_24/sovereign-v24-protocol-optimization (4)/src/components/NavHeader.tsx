import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Menu, X } from 'lucide-react';

const navItems = [
  { label: 'Topology', href: '#topology' },
  { label: 'Benchmarks', href: '#benchmarks' },
  { label: 'Striping', href: '#striping' },
  { label: 'Safety', href: '#safety' },
  { label: 'Architecture', href: '#architecture' },
  { label: 'Code', href: '#code' },
];

export function NavHeader() {
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 50);
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <motion.header
      initial={{ y: -100 }}
      animate={{ y: 0 }}
      className={`fixed top-0 left-0 right-0 z-50 transition-all duration-300 ${
        scrolled ? 'glass-panel border-b border-sov-border/50' : 'bg-transparent'
      }`}
    >
      <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
        {/* Logo */}
        <a href="#" className="flex items-center gap-2">
          <span className="text-sm font-black tracking-tight">
            <span className="text-sov-text">SOV</span>
            <span className="text-sov-cyan">.V24</span>
          </span>
          <span className="text-[10px] font-mono text-sov-text-dim/50 hidden sm:inline">GLOBAL-ROBUST</span>
        </a>

        {/* Desktop nav */}
        <nav className="hidden md:flex items-center gap-1">
          {navItems.map(item => (
            <a
              key={item.href}
              href={item.href}
              className="px-3 py-1.5 rounded-md text-xs font-mono text-sov-text-dim hover:text-sov-cyan hover:bg-sov-cyan/5 transition-colors"
            >
              {item.label}
            </a>
          ))}
        </nav>

        {/* Mobile menu button */}
        <button
          className="md:hidden text-sov-text-dim"
          onClick={() => setMobileOpen(!mobileOpen)}
        >
          {mobileOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
        </button>
      </div>

      {/* Mobile nav */}
      {mobileOpen && (
        <motion.div
          initial={{ opacity: 0, height: 0 }}
          animate={{ opacity: 1, height: 'auto' }}
          exit={{ opacity: 0, height: 0 }}
          className="md:hidden glass-panel border-t border-sov-border/50"
        >
          <nav className="flex flex-col p-4 gap-1">
            {navItems.map(item => (
              <a
                key={item.href}
                href={item.href}
                onClick={() => setMobileOpen(false)}
                className="px-3 py-2 rounded-md text-sm font-mono text-sov-text-dim hover:text-sov-cyan hover:bg-sov-cyan/5 transition-colors"
              >
                {item.label}
              </a>
            ))}
          </nav>
        </motion.div>
      )}
    </motion.header>
  );
}
