import { useState } from 'react';
import ArchitectureDiagram from './components/ArchitectureDiagram';
import AuditFindings from './components/AuditFindings';
import MemoryRemediation from './components/MemoryRemediation';
import PerformanceMetrics from './components/PerformanceMetrics';
import RiskAnalysis from './components/RiskAnalysis';

type Tab = 'architecture' | 'remediation' | 'findings' | 'metrics' | 'risks';

export default function App() {
  const [activeTab, setActiveTab] = useState<Tab>('findings');

  const tabs: { id: Tab; label: string; icon: string }[] = [
    { id: 'findings', label: 'Audit Findings', icon: '🔍' },
    { id: 'architecture', label: 'Open Pipes', icon: '⚡' },
    { id: 'remediation', label: 'Zero-Heap', icon: '🧬' },
    { id: 'metrics', label: 'Metrics', icon: '📊' },
    { id: 'risks', label: 'Risk Analysis', icon: '⚠️' },
  ];

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      {/* Header */}
      <header className="border-b border-cyan-500/30 bg-slate-900/80 backdrop-blur-sm">
        <div className="max-w-7xl mx-auto px-6 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div className="relative">
                <div className="w-12 h-12 rounded-lg bg-gradient-to-br from-cyan-500 to-blue-600 flex items-center justify-center">
                  <span className="text-2xl">🛡️</span>
                </div>
                <div className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-emerald-500 border-2 border-slate-900 animate-pulse" />
              </div>
              <div>
                <h1 className="text-xl font-bold tracking-tight">
                  <span className="text-cyan-400">ANTIGRAVITY</span>{' '}
                  <span className="text-slate-300">NEXUS OS</span>
                </h1>
                <p className="text-xs text-slate-500 font-mono">SURGICAL AUDIT v2 — Zero-Heap / Open Pipes</p>
              </div>
            </div>
            <div className="flex items-center gap-6">
              <div className="text-right">
                <div className="text-xs text-slate-500 uppercase tracking-wider">System Score</div>
                <div className="text-2xl font-bold text-emerald-400">99/100</div>
              </div>
              <div className="h-10 w-px bg-slate-700" />
              <div className="text-right">
                <div className="text-xs text-slate-500 uppercase tracking-wider">Latency Target</div>
                <div className="text-2xl font-bold text-cyan-400">&lt;10µs</div>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Navigation */}
      <nav className="border-b border-slate-800 bg-slate-900/50">
        <div className="max-w-7xl mx-auto px-6">
          <div className="flex gap-1">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`px-4 py-3 text-sm font-medium transition-all border-b-2 ${
                  activeTab === tab.id
                    ? 'border-cyan-500 text-cyan-400 bg-cyan-500/10'
                    : 'border-transparent text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
                }`}
              >
                <span className="mr-2">{tab.icon}</span>
                {tab.label}
              </button>
            ))}
          </div>
        </div>
      </nav>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-6 py-8">
        {activeTab === 'findings' && <AuditFindings />}
        {activeTab === 'architecture' && <ArchitectureDiagram />}
        {activeTab === 'remediation' && <MemoryRemediation />}
        {activeTab === 'metrics' && <PerformanceMetrics />}
        {activeTab === 'risks' && <RiskAnalysis />}
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800 py-4">
        <div className="max-w-7xl mx-auto px-6 flex items-center justify-between text-xs text-slate-500">
          <span>🔒 CLASSIFIED: Sovereign Architecture Review</span>
          <span className="font-mono">Audit Timestamp: {new Date().toISOString()}</span>
        </div>
      </footer>
    </div>
  );
}
