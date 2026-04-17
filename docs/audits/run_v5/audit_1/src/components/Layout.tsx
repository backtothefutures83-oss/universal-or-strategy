import React from 'react';
import { Network, ShieldAlert, Cpu, Activity, Diamond } from 'lucide-react';

interface LayoutProps {
  children: React.ReactNode;
  activeView: string;
  setActiveView: (view: string) => void;
}

export const Layout: React.FC<LayoutProps> = ({ children, activeView, setActiveView }) => {
  const navItems = [
    { id: 'overview', icon: Diamond, label: 'Overview', desc: 'Sovereign Mesh v4' },
    { id: 'multimodal', icon: Network, label: 'L1-Sideband', desc: 'Multimodal Pipes' },
    { id: 'mirror', icon: ShieldAlert, label: 'Mirror Node', desc: 'Jitter-Free Redundancy' },
    { id: 'atomic', icon: Cpu, label: 'Atomic Constant', desc: '1µs Gate' },
  ];

  return (
    <div className="flex h-screen bg-slate-950 text-slate-200 font-sans overflow-hidden selection:bg-blood-500/30">
      {/* Sidebar */}
      <aside className="w-80 border-r border-slate-800 bg-slate-950/50 backdrop-blur flex flex-col z-20">
        <div className="p-6 border-b border-slate-800">
          <div className="flex items-center gap-3 text-platinum-400 mb-2">
            <Activity className="w-5 h-5 text-blood-500 animate-pulse" />
            <span className="text-xs uppercase tracking-[0.2em] font-bold">Arena v5</span>
          </div>
          <h1 className="text-xl font-medium text-white tracking-wide">The Platinum Battle</h1>
          <p className="text-slate-500 text-xs font-mono mt-2">Billionaire's Tax Challenge</p>
        </div>
        
        <nav className="flex-1 overflow-y-auto p-4 space-y-2">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = activeView === item.id;
            return (
              <button
                key={item.id}
                onClick={() => setActiveView(item.id)}
                className={`w-full text-left p-4 rounded-lg border transition-all duration-200 flex items-start gap-4 group relative overflow-hidden ${
                  isActive 
                    ? 'bg-slate-900 border-blood-500/30 text-white shadow-[0_0_15px_rgba(255,42,42,0.1)]' 
                    : 'border-transparent text-slate-400 hover:bg-slate-900/50 hover:text-slate-200'
                }`}
              >
                {isActive && (
                  <div className="absolute inset-0 bg-gradient-to-r from-blood-500/10 to-transparent" />
                )}
                <Icon className={`w-5 h-5 mt-0.5 ${isActive ? 'text-blood-500' : 'text-slate-500 group-hover:text-slate-300'}`} />
                <div>
                  <div className="font-mono text-sm font-semibold mb-1">{item.label}</div>
                  <div className={`text-xs ${isActive ? 'text-slate-400' : 'text-slate-600 group-hover:text-slate-500'}`}>
                    {item.desc}
                  </div>
                </div>
              </button>
            );
          })}
        </nav>
        
        <div className="p-4 border-t border-slate-800 text-xs font-mono text-slate-600 flex justify-between">
          <span>STATUS: IMMORTAL</span>
          <span className="text-blood-500 font-bold animate-pulse">100/100</span>
        </div>
      </aside>

      {/* Main Content Area */}
      <main 
        className="flex-1 relative overflow-y-auto"
        style={{ backgroundImage: "url(\"data:image/svg+xml,%3Csvg width='40' height='40' viewBox='0 0 40 40' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 0h40v40H0V0zm1 1h38v38H1V1z' fill='%23334155' fill-opacity='0.1' fill-rule='evenodd'/%3E%3C/svg%3E\")" }}
      >
        <div className="absolute inset-0 bg-gradient-to-br from-slate-950/90 via-slate-950/80 to-slate-950/95 z-0" />
        <div className="relative z-10 min-h-full p-8 lg:p-12 max-w-7xl mx-auto">
          {children}
        </div>
      </main>
    </div>
  );
};