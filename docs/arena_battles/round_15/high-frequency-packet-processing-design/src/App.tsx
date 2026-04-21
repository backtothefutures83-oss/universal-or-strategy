import { useState } from 'react';
import IngressBridge from './components/IngressBridge';
import TaggedPointers from './components/TaggedPointers';
import CacheConcurrency from './components/CacheConcurrency';
import LatencySummary from './components/LatencySummary';

export default function App() {
  const [activeSection, setActiveSection] = useState<string>('overview');

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950">
      {/* Header */}
      <header className="border-b border-slate-800 bg-slate-900/50 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-6 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-gradient-to-br from-cyan-500 to-blue-600 flex items-center justify-center">
                <svg className="h-6 w-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              </div>
              <div>
                <h1 className="text-xl font-bold text-white">5ns Packet Processing Pipeline</h1>
                <p className="text-xs text-slate-400">Zero-Allocation Memory Architecture</p>
              </div>
            </div>
            <div className="flex items-center gap-2 text-xs">
              <span className="px-3 py-1 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                Target: &lt;5ns
              </span>
              <span className="px-3 py-1 rounded-full bg-green-500/10 text-green-400 border border-green-500/20">
                12 Threads
              </span>
            </div>
          </div>
        </div>
      </header>

      <div className="max-w-7xl mx-auto px-6 py-8">
        {/* Navigation */}
        <nav className="mb-8 flex gap-2 overflow-x-auto pb-2">
          {[
            { id: 'overview', label: 'Overview', icon: '📋' },
            { id: 'ingress', label: 'Ingress Bridge', icon: '🔄' },
            { id: 'tagged', label: 'Tagged Pointers', icon: '🏷️' },
            { id: 'cache', label: 'Cache Guard', icon: '⚡' },
            { id: 'summary', label: 'Latency Summary', icon: '📊' },
          ].map((section) => (
            <button
              key={section.id}
              onClick={() => setActiveSection(section.id)}
              className={`px-4 py-2 rounded-lg font-medium text-sm transition-all whitespace-nowrap ${
                activeSection === section.id
                  ? 'bg-cyan-500 text-white shadow-lg shadow-cyan-500/30'
                  : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
              }`}
            >
              <span className="mr-2">{section.icon}</span>
              {section.label}
            </button>
          ))}
        </nav>

        {/* Content */}
        <div className="space-y-6">
          {activeSection === 'overview' && (
            <div className="space-y-6">
              <div className="bg-slate-800/50 backdrop-blur-sm border border-slate-700 rounded-xl p-8">
                <h2 className="text-2xl font-bold text-white mb-4">System Architecture Overview</h2>
                <p className="text-slate-300 mb-6 leading-relaxed">
                  This design specification addresses the critical performance regression from 50ns to &lt;5ns 
                  in high-frequency packet processing by eliminating generic C# collections and implementing 
                  custom atomic topologies with zero-allocation guarantees.
                </p>

                <div className="grid md:grid-cols-3 gap-4 mb-6">
                  <div className="bg-slate-900/50 border border-slate-700 rounded-lg p-4">
                    <h3 className="text-cyan-400 font-semibold mb-2">🎯 Performance Target</h3>
                    <p className="text-3xl font-bold text-white mb-1">&lt;5ns</p>
                    <p className="text-sm text-slate-400">Total cycle time per packet</p>
                  </div>
                  <div className="bg-slate-900/50 border border-slate-700 rounded-lg p-4">
                    <h3 className="text-green-400 font-semibold mb-2">🔧 Concurrency</h3>
                    <p className="text-3xl font-bold text-white mb-1">12 Threads</p>
                    <p className="text-sm text-slate-400">Parallel processing cores</p>
                  </div>
                  <div className="bg-slate-900/50 border border-slate-700 rounded-lg p-4">
                    <h3 className="text-purple-400 font-semibold mb-2">💾 Cache Line</h3>
                    <p className="text-3xl font-bold text-white mb-1">64 bytes</p>
                    <p className="text-sm text-slate-400">Hardware alignment target</p>
                  </div>
                </div>

                <div className="bg-gradient-to-r from-amber-500/10 to-orange-500/10 border border-amber-500/20 rounded-lg p-4">
                  <h3 className="text-amber-400 font-semibold mb-2">⚠️ Architectural Constraints</h3>
                  <ul className="space-y-1 text-sm text-slate-300">
                    <li>• NO generic C# collections (System.Collections.Concurrent)</li>
                    <li>• NO object pooling or standard queue mechanisms</li>
                    <li>• NO heap allocations in the hot path</li>
                    <li>• MUST use pre-allocated memory rings and bitwise atomics only</li>
                  </ul>
                </div>
              </div>

              <div className="bg-slate-800/50 backdrop-blur-sm border border-slate-700 rounded-xl p-8">
                <h2 className="text-xl font-bold text-white mb-4">Three-Pillar Design</h2>
                <div className="space-y-4">
                  <div className="flex gap-4 items-start">
                    <div className="flex-shrink-0 w-12 h-12 rounded-lg bg-cyan-500/10 border border-cyan-500/30 flex items-center justify-center text-cyan-400 font-bold">
                      1
                    </div>
                    <div>
                      <h3 className="text-white font-semibold mb-1">Ingress Bridge</h3>
                      <p className="text-slate-400 text-sm">Zero-allocation pre-allocated memory ring for socket-to-core packet transfer</p>
                    </div>
                  </div>
                  <div className="flex gap-4 items-start">
                    <div className="flex-shrink-0 w-12 h-12 rounded-lg bg-purple-500/10 border border-purple-500/30 flex items-center justify-center text-purple-400 font-bold">
                      2
                    </div>
                    <div>
                      <h3 className="text-white font-semibold mb-1">Bitwise Tagged Pointers</h3>
                      <p className="text-slate-400 text-sm">64-bit tagged pointer system with 48-bit index + 16-bit epoch for ABA safety</p>
                    </div>
                  </div>
                  <div className="flex gap-4 items-start">
                    <div className="flex-shrink-0 w-12 h-12 rounded-lg bg-green-500/10 border border-green-500/30 flex items-center justify-center text-green-400 font-bold">
                      3
                    </div>
                    <div>
                      <h3 className="text-white font-semibold mb-1">Cache Concurrency Guard</h3>
                      <p className="text-slate-400 text-sm">64-byte alignment and padding to prevent cache-line invalidation storms</p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activeSection === 'ingress' && <IngressBridge />}
          {activeSection === 'tagged' && <TaggedPointers />}
          {activeSection === 'cache' && <CacheConcurrency />}
          {activeSection === 'summary' && <LatencySummary />}
        </div>
      </div>

      {/* Footer */}
      <footer className="mt-16 border-t border-slate-800 bg-slate-900/30">
        <div className="max-w-7xl mx-auto px-6 py-6 text-center text-sm text-slate-500">
          <p>High-Frequency Packet Processing Pipeline Design • 2026</p>
        </div>
      </footer>
    </div>
  );
}
