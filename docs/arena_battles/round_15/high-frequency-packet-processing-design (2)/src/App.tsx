import { useState } from 'react';
import { IngressBridge } from './components/IngressBridge';
import { TaggedPointers } from './components/TaggedPointers';
import { CacheConcurrency } from './components/CacheConcurrency';
import { PerformanceOverview } from './components/PerformanceOverview';

export default function App() {
  const [activeSection, setActiveSection] = useState<string>('overview');

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
      {/* Header */}
      <header className="border-b border-slate-700 bg-slate-900/80 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-6 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-gradient-to-br from-cyan-500 to-blue-600 flex items-center justify-center">
                <svg className="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              </div>
              <div>
                <h1 className="text-xl font-bold text-white">Sub-5ns Pipeline Design</h1>
                <p className="text-xs text-slate-400">High-Frequency Packet Processing Architecture</p>
              </div>
            </div>
            <div className="flex items-center gap-4">
              <div className="px-3 py-1 rounded-full bg-cyan-500/20 border border-cyan-500/30">
                <span className="text-cyan-400 text-sm font-mono font-semibold">&lt; 5ns</span>
              </div>
              <div className="px-3 py-1 rounded-full bg-green-500/20 border border-green-500/30">
                <span className="text-green-400 text-sm font-mono">Zero-Alloc</span>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Navigation */}
      <nav className="border-b border-slate-700 bg-slate-800/50">
        <div className="max-w-7xl mx-auto px-6">
          <div className="flex gap-1">
            {[
              { id: 'overview', label: 'Overview', icon: '📊' },
              { id: 'ingress', label: 'Ingress Bridge', icon: '🔄' },
              { id: 'tagged', label: 'Tagged Pointers', icon: '🏷️' },
              { id: 'cache', label: 'Cache Guards', icon: '⚡' }
            ].map(section => (
              <button
                key={section.id}
                onClick={() => setActiveSection(section.id)}
                className={`px-4 py-3 text-sm font-medium transition-all border-b-2 ${
                  activeSection === section.id
                    ? 'border-cyan-500 text-cyan-400 bg-slate-800'
                    : 'border-transparent text-slate-400 hover:text-slate-300 hover:bg-slate-800/50'
                }`}
              >
                <span className="mr-2">{section.icon}</span>
                {section.label}
              </button>
            ))}
          </div>
        </div>
      </nav>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-6 py-8">
        {activeSection === 'overview' && <PerformanceOverview />}
        {activeSection === 'ingress' && <IngressBridge />}
        {activeSection === 'tagged' && <TaggedPointers />}
        {activeSection === 'cache' && <CacheConcurrency />}
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-700 mt-16 py-6">
        <div className="max-w-7xl mx-auto px-6 text-center text-slate-500 text-sm">
          <p>High-Performance Systems Engineering • Custom Atomic Topologies • Lock-Free Architecture</p>
        </div>
      </footer>
    </div>
  );
}
