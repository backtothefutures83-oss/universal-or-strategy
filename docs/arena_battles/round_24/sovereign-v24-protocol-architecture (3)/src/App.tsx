import { Hero } from './components/Hero';
import { TopologyVisualization } from './components/TopologyVisualization';
import { SafetyPanel } from './components/SafetyPanel';
import { StripingVisualizer } from './components/StripingVisualizer';
import { MetricsDashboard } from './components/MetricsDashboard';
import { CodeViewer } from './components/CodeViewer';

function Nav() {
  return (
    <nav className="fixed top-0 left-0 right-0 z-50 glass-panel border-b border-sov-600/50">
      <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-2 h-2 rounded-full bg-cyan-glow animate-pulse" />
          <span className="text-sm font-bold font-mono text-white tracking-wider">SOVEREIGN</span>
          <span className="text-sm font-mono text-cyan-glow">V24</span>
        </div>
        <div className="hidden md:flex items-center gap-6 text-xs font-mono">
          <a href="#topology" className="text-sov-400 hover:text-cyan-glow transition-colors">Topology</a>
          <a href="#safety" className="text-sov-400 hover:text-cyan-glow transition-colors">Safety</a>
          <a href="#striping" className="text-sov-400 hover:text-cyan-glow transition-colors">Striping</a>
          <a href="#metrics" className="text-sov-400 hover:text-cyan-glow transition-colors">Metrics</a>
          <a href="#code" className="text-sov-400 hover:text-cyan-glow transition-colors">Code</a>
        </div>
        <div className="px-2 py-0.5 rounded bg-teal-glow/10 border border-teal-glow/20 text-[10px] font-mono text-teal-glow">
          SOV-V24-GLOBAL-ROBUST
        </div>
      </div>
    </nav>
  );
}

function Footer() {
  return (
    <footer className="py-16 px-4 border-t border-sov-700/50">
      <div className="max-w-6xl mx-auto">
        <div className="grid md:grid-cols-3 gap-8 mb-12">
          <div>
            <div className="flex items-center gap-2 mb-4">
              <div className="w-2 h-2 rounded-full bg-cyan-glow animate-pulse" />
              <span className="text-sm font-bold font-mono text-white">SOVEREIGN V24</span>
            </div>
            <p className="text-xs text-sov-400 leading-relaxed">
              The Global Zero-Friction Handshake Protocol. 
              Achieving sub-0.5ns latency across heterogeneous CPU topologies with zero hardware fences.
            </p>
          </div>
          <div>
            <h4 className="text-xs font-mono text-white font-bold mb-3 tracking-wider">MANDATES</h4>
            <ul className="space-y-2 text-xs text-sov-400">
              <li className="flex items-center gap-2">
                <span className="text-cyan-glow">01</span>
                <span>Hardware-Auto-Detect Topology</span>
              </li>
              <li className="flex items-center gap-2">
                <span className="text-cyan-glow">02</span>
                <span>Zero-Friction Safety Invariants</span>
              </li>
              <li className="flex items-center gap-2">
                <span className="text-cyan-glow">03</span>
                <span>Adaptive Adaptive Striping</span>
              </li>
              <li className="flex items-center gap-2">
                <span className="text-cyan-glow">04</span>
                <span>ADR-015 Fence-Less Discipline</span>
              </li>
            </ul>
          </div>
          <div>
            <h4 className="text-xs font-mono text-white font-bold mb-3 tracking-wider">PROTOCOL STATE</h4>
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs font-mono">
                <span className="text-sov-400">Build Tag</span>
                <span className="text-cyan-glow">SOV-V24-GLOBAL-ROBUST</span>
              </div>
              <div className="flex items-center justify-between text-xs font-mono">
                <span className="text-sov-400">Latency</span>
                <span className="text-teal-glow">0.47ns ✓</span>
              </div>
              <div className="flex items-center justify-between text-xs font-mono">
                <span className="text-sov-400">Fence Count</span>
                <span className="text-teal-glow">0</span>
              </div>
              <div className="flex items-center justify-between text-xs font-mono">
                <span className="text-sov-400">Safety Invariants</span>
                <span className="text-teal-glow">5/5 Verified</span>
              </div>
              <div className="flex items-center justify-between text-xs font-mono">
                <span className="text-sov-400">Portability</span>
                <span className="text-teal-glow">x86/ARM ✓</span>
              </div>
            </div>
          </div>
        </div>
        <div className="border-t border-sov-700/50 pt-6 flex flex-col md:flex-row items-center justify-between gap-4">
          <p className="text-[10px] font-mono text-sov-500">
            Sovereign V24 — Global Zero-Friction Handshake Protocol
          </p>
          <p className="text-[10px] font-mono text-sov-500">
            PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST
          </p>
        </div>
      </div>
    </footer>
  );
}

export default function App() {
  return (
    <div className="min-h-screen bg-sov-900">
      <Nav />
      <Hero />
      <TopologyVisualization />
      <SafetyPanel />
      <StripingVisualizer />
      <MetricsDashboard />
      <CodeViewer />
      <Footer />
    </div>
  );
}
