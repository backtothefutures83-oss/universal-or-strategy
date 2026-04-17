import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import Header from './components/Header';
import TopologyView from './components/TopologyView';
import BenchmarkPanel from './components/BenchmarkPanel';
import SafetyInvariants from './components/SafetyInvariants';
import AdaptiveStriping from './components/AdaptiveStriping';
import CodeDisplay from './components/CodeDisplay';
import ProfileSelector from './components/ProfileSelector';
import { useSimulation } from './hooks/useSimulation';

type Tab = 'dashboard' | 'topology' | 'code';

export default function App() {
  const [activeTab, setActiveTab] = useState<Tab>('dashboard');
  const [codeTab, setCodeTab] = useState('code');

  const {
    profile,
    selectedProfile,
    setSelectedProfile,
    isRunning,
    startSimulation,
    stopSimulation,
    benchmarks,
    adaptiveState,
    safetyInvariants,
    stats,
  } = useSimulation();

  return (
    <div className="min-h-screen bg-surface-900 grid-bg">
      {/* Header */}
      <Header
        isRunning={isRunning}
        avgLatency={stats.avgLatency}
        mode={adaptiveState.mode}
      />

      {/* Tab Navigation */}
      <nav className="max-w-[1600px] mx-auto px-6 pt-4">
        <div className="flex gap-1 bg-surface-800/50 rounded-xl p-1 border border-sov-800/50 w-fit">
          {[
            { key: 'dashboard' as Tab, label: 'Dashboard' },
            { key: 'topology' as Tab, label: 'Topology' },
            { key: 'code' as Tab, label: 'V24 Code' },
          ].map(tab => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`px-5 py-2 rounded-lg text-sm font-medium transition-all ${
                activeTab === tab.key
                  ? 'bg-accent-cyan/20 text-accent-cyan shadow-lg shadow-accent-cyan/5'
                  : 'text-white/40 hover:text-white/60'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </nav>

      {/* Main Content */}
      <main className="max-w-[1600px] mx-auto px-6 py-6">
        <AnimatePresence mode="wait">
          {activeTab === 'dashboard' && (
            <motion.div
              key="dashboard"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="space-y-6"
            >
              {/* Top Row: Profile Selector + Benchmark */}
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
                <div className="lg:col-span-3">
                  <ProfileSelector
                    selected={selectedProfile}
                    onSelect={setSelectedProfile}
                    isRunning={isRunning}
                    onStart={startSimulation}
                    onStop={stopSimulation}
                  />
                </div>
                <div className="lg:col-span-9">
                  <BenchmarkPanel benchmarks={benchmarks} stats={stats} />
                </div>
              </div>

              {/* Middle Row: Safety + Adaptive */}
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <SafetyInvariants invariants={safetyInvariants} />
                <AdaptiveStriping state={adaptiveState} />
              </div>
            </motion.div>
          )}

          {activeTab === 'topology' && (
            <motion.div
              key="topology"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="space-y-6"
            >
              <TopologyView profile={profile} />
            </motion.div>
          )}

          {activeTab === 'code' && (
            <motion.div
              key="code"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="space-y-6"
            >
              <CodeDisplay activeTab={codeTab} onTabChange={setCodeTab} />
            </motion.div>
          )}
        </AnimatePresence>
      </main>

      {/* Footer */}
      <footer className="border-t border-sov-800/30 mt-8">
        <div className="max-w-[1600px] mx-auto px-6 py-4 flex items-center justify-between">
          <span className="text-xs text-white/20 font-mono">
            SOVEREIGN V24 • BUILD TAG: SOV-V24-GLOBAL-ROBUST
          </span>
          <span className="text-xs text-white/20 font-mono">
            ADR-015 COMPLIANT • FENCE_COUNT = 0
          </span>
        </div>
      </footer>
    </div>
  );
}
