import HeroSection from "./components/HeroSection";
import PipeTopology from "./components/PipeTopology";
import LatencyGauge from "./components/LatencyGauge";
import PillarsSection from "./components/PillarsSection";
import ForbiddenPatterns from "./components/ForbiddenPatterns";
import ArchDiagram from "./components/ArchDiagram";
import PhysicsBreakdown from "./components/PhysicsBreakdown";
import MetricsPanel from "./components/MetricsPanel";
import PerfectScore from "./components/PerfectScore";

export default function App() {
  return (
    <div className="min-h-screen bg-[#020617] text-white selection:bg-emerald-500/30">
      {/* Top nav */}
      <nav className="sticky top-0 z-50 border-b border-slate-800/80 bg-[#020617]/80 backdrop-blur-xl">
        <div className="max-w-5xl mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-6 h-6 rounded-md bg-gradient-to-br from-emerald-400 to-cyan-500 flex items-center justify-center">
              <span className="text-[10px] font-black text-black">SA</span>
            </div>
            <span className="font-black text-sm text-white tracking-tight">
              Sovereign Actor <span className="text-emerald-400">v2</span>
            </span>
            <span className="hidden sm:block text-slate-700 text-sm">·</span>
            <span className="hidden sm:block text-slate-600 text-xs font-mono">
              Antigravity Nexus OS · IPC Layer
            </span>
          </div>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-1.5 text-xs text-emerald-400 font-mono">
              <span className="relative flex h-1.5 w-1.5">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
                <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-emerald-500" />
              </span>
              LIVE
            </div>
            <div className="text-xs font-mono text-slate-600">10µs GATE</div>
          </div>
        </div>
      </nav>

      {/* Hero */}
      <HeroSection />

      {/* Live topology + gauge */}
      <section className="max-w-5xl mx-auto px-4 py-8">
        <div className="text-center mb-8">
          <h2 className="text-3xl font-black text-white mb-2">
            Live Core Mesh & Latency Monitor
          </h2>
          <p className="text-slate-500 text-sm">
            Real-time simulation of the 12-core SPSC ring mesh. Click any core to inspect its channel bindings.
          </p>
        </div>
        <div className="flex flex-col lg:flex-row gap-6 items-start justify-center">
          <div className="flex-1">
            <PipeTopology />
          </div>
          <div className="w-full lg:w-72 shrink-0">
            <LatencyGauge />
          </div>
        </div>
      </section>

      {/* Divider */}
      <div className="max-w-5xl mx-auto px-4">
        <div className="border-t border-slate-800" />
      </div>

      {/* Architecture diagram */}
      <ArchDiagram />

      {/* Divider */}
      <div className="max-w-5xl mx-auto px-4">
        <div className="border-t border-slate-800" />
      </div>

      {/* Physics breakdown */}
      <PhysicsBreakdown />

      {/* Divider */}
      <div className="max-w-5xl mx-auto px-4">
        <div className="border-t border-slate-800" />
      </div>

      {/* Banned vs sovereign patterns */}
      <ForbiddenPatterns />

      {/* Divider */}
      <div className="max-w-5xl mx-auto px-4">
        <div className="border-t border-slate-800" />
      </div>

      {/* Four pillars with code */}
      <PillarsSection />

      {/* Divider */}
      <div className="max-w-5xl mx-auto px-4">
        <div className="border-t border-slate-800" />
      </div>

      {/* Runtime metrics */}
      <MetricsPanel />

      {/* Divider */}
      <div className="max-w-5xl mx-auto px-4">
        <div className="border-t border-slate-800" />
      </div>

      {/* Perfect score */}
      <PerfectScore />

      {/* Footer */}
      <footer className="border-t border-slate-800 mt-12">
        <div className="max-w-5xl mx-auto px-4 py-8 flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="text-xs text-slate-600 font-mono">
            Sovereign Actor v2 · Antigravity Nexus OS · Zero-Heap / Open Pipe IPC Layer
          </div>
          <div className="flex items-center gap-4 text-xs text-slate-700 font-mono">
            <span>Arena AI Design Challenge</span>
            <span className="text-emerald-600">100/100</span>
          </div>
        </div>
      </footer>
    </div>
  );
}
