// ═══════════════════════════════════════════════════════════════════
//  1µs SOVEREIGN SPSC IPC — ARCHITECTURE DASHBOARD
//  12-Role Mesh • Zero-Copy • Lock-Free • Spin-Poll
//
//  Topology: Ingress(2) → Router(1) → Transform(2) → Actor(3)
//            → Egress(2) → Mirror(2) = 12 sovereign cores
// ═══════════════════════════════════════════════════════════════════

import { useState, useEffect, useRef, useCallback } from 'react';
import type { SPSCChannel, CoreMetrics, TimingGate, JitterSample } from './types';
import {
  MESH_ROLES,
  simulateChannels,
  simulateCoreMetrics,
  simulateTimingGate,
  simulateJitterSamples,
} from './simulation';
import TopologyMesh from './components/TopologyMesh';
import RingBufferViz from './components/RingBufferViz';
import TimingGatePanel from './components/TimingGatePanel';
import CorePinningGrid from './components/CorePinningGrid';
import JitterHeatmap from './components/JitterHeatmap';
import ArchitectureSpec from './components/ArchitectureSpec';
import DataFlowTracer from './components/DataFlowTracer';

type Tab = 'topology' | 'architecture';

export default function App() {
  const [channels, setChannels] = useState<SPSCChannel[]>([]);
  const [coreMetrics, setCoreMetrics] = useState<CoreMetrics[]>([]);
  const [gate, setGate] = useState<TimingGate | null>(null);
  const [gateHistory, setGateHistory] = useState<number[]>([]);
  const [jitterSamples, setJitterSamples] = useState<JitterSample[]>([]);
  const [selectedChannel, setSelectedChannel] = useState<SPSCChannel | null>(null);
  const [activeTab, setActiveTab] = useState<Tab>('topology');
  const [tickCount, setTickCount] = useState(0);
  const [running, setRunning] = useState(true);
  const intervalRef = useRef<ReturnType<typeof setInterval> | undefined>(undefined);

  const tick = useCallback(() => {
    const ch = simulateChannels();
    const cm = simulateCoreMetrics();
    const tg = simulateTimingGate();
    const js = simulateJitterSamples(80);

    setChannels(ch);
    setCoreMetrics(cm);
    setGate(tg);
    setGateHistory((prev) => [...prev.slice(-119), tg.totalPassNs]);
    setJitterSamples(js);
    setTickCount((t) => t + 1);

    // Update selected channel with new data
    setSelectedChannel((prev) => {
      if (!prev) return null;
      return ch.find((c) => c.from === prev.from && c.to === prev.to) ?? null;
    });
  }, []);

  useEffect(() => {
    tick(); // initial
    if (running) {
      intervalRef.current = setInterval(tick, 250);
    }
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [tick, running]);

  const passCount = gateHistory.filter((v) => v <= 1000).length;
  const passRate = gateHistory.length > 0 ? (passCount / gateHistory.length) * 100 : 100;

  return (
    <div className="min-h-screen bg-[#030712] text-white flex flex-col overflow-hidden">
      {/* ─── HEADER ─────────────────────────────────────────── */}
      <header className="border-b border-slate-800/60 bg-[#030712]/90 backdrop-blur-sm px-4 py-2.5 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <div className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
            <h1 className="text-sm font-mono font-bold tracking-tight">
              <span className="text-cyan-400">1µs</span>
              <span className="text-slate-500 mx-1">│</span>
              <span className="text-white">SOVEREIGN SPSC IPC</span>
            </h1>
          </div>
          <div className="hidden sm:flex items-center gap-1.5 ml-3">
            {['Zero-Copy', 'Lock-Free', 'Spin-Poll', 'Zero-Heap'].map((tag) => (
              <span key={tag} className="px-1.5 py-0.5 text-[9px] font-mono rounded bg-slate-900 border border-slate-800 text-slate-500">
                {tag}
              </span>
            ))}
          </div>
        </div>

        <div className="flex items-center gap-3">
          {/* Live metrics */}
          <div className="hidden md:flex items-center gap-3 font-mono text-[10px]">
            <div className="flex items-center gap-1">
              <span className="text-slate-600">GATE</span>
              <span className={gate?.verdict === 'PASS' ? 'text-emerald-400' : gate?.verdict === 'WARN' ? 'text-amber-400' : 'text-red-400'}>
                {gate?.totalPassNs.toFixed(0) ?? '—'}ns
              </span>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-slate-600">PASS%</span>
              <span className={passRate > 95 ? 'text-emerald-400' : 'text-amber-400'}>
                {passRate.toFixed(1)}%
              </span>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-slate-600">TICK</span>
              <span className="text-slate-400">{tickCount}</span>
            </div>
          </div>

          {/* Controls */}
          <button
            onClick={() => setRunning(!running)}
            className={`px-2.5 py-1 text-[10px] font-mono rounded border transition-colors ${
              running
                ? 'border-emerald-800 bg-emerald-950/50 text-emerald-400 hover:bg-emerald-950'
                : 'border-amber-800 bg-amber-950/50 text-amber-400 hover:bg-amber-950'
            }`}
          >
            {running ? '● LIVE' : '○ PAUSED'}
          </button>
        </div>
      </header>

      {/* ─── TAB BAR ────────────────────────────────────────── */}
      <div className="border-b border-slate-800/40 bg-[#030712] px-4 flex items-center gap-1 shrink-0">
        {[
          { id: 'topology' as Tab, label: 'Mesh Topology' },
          { id: 'architecture' as Tab, label: 'Architecture Spec' },
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-3 py-2 text-[11px] font-mono border-b-2 transition-colors ${
              activeTab === tab.id
                ? 'border-cyan-500 text-cyan-400'
                : 'border-transparent text-slate-600 hover:text-slate-400'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* ─── MAIN CONTENT ───────────────────────────────────── */}
      <main className="flex-1 p-3 overflow-hidden">
        {activeTab === 'topology' ? (
          <div className="h-full grid grid-cols-1 lg:grid-cols-12 gap-3" style={{ minHeight: 0 }}>
            {/* Left: Topology Mesh */}
            <div className="lg:col-span-7 xl:col-span-8 bg-[#0a0f1a] rounded-xl border border-slate-800/60 overflow-hidden flex flex-col">
              <div className="px-3 py-2 border-b border-slate-800/40 flex items-center justify-between">
                <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                  12-Role Mesh • SPSC Channels
                </span>
                <span className="text-[10px] font-mono text-slate-600">
                  {channels.length} channels
                </span>
              </div>
              <div className="flex-1 min-h-0">
                <TopologyMesh
                  roles={MESH_ROLES}
                  channels={channels}
                  selectedChannel={selectedChannel}
                  onSelectChannel={setSelectedChannel}
                />
              </div>
            </div>

            {/* Right: Panels */}
            <div className="lg:col-span-5 xl:col-span-4 flex flex-col gap-3 min-h-0 overflow-y-auto">
              {/* Ring Buffer */}
              <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60 shrink-0">
                <div className="px-3 py-2 border-b border-slate-800/40">
                  <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                    SPSC Ring Buffer
                  </span>
                </div>
                <div style={{ minHeight: 280 }}>
                  <RingBufferViz channel={selectedChannel} />
                </div>
              </div>

              {/* Data Flow Tracer */}
              <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60 shrink-0">
                <div className="px-3 py-2 border-b border-slate-800/40">
                  <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                    Packet Trace — Critical Path
                  </span>
                </div>
                <div style={{ minHeight: 220 }}>
                  <DataFlowTracer />
                </div>
              </div>
            </div>

            {/* Bottom row */}
            <div className="lg:col-span-12 grid grid-cols-1 md:grid-cols-3 gap-3">
              {/* Timing Gate */}
              <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60">
                <div className="px-3 py-2 border-b border-slate-800/40">
                  <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                    ⚡ 1µs Hard Gate
                  </span>
                </div>
                <div style={{ minHeight: 280 }}>
                  {gate && <TimingGatePanel gate={gate} history={gateHistory} />}
                </div>
              </div>

              {/* Core Pinning */}
              <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60">
                <div className="px-3 py-2 border-b border-slate-800/40">
                  <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                    🏗️ Core Pinning — Sovereign
                  </span>
                </div>
                <div style={{ minHeight: 280 }}>
                  <CorePinningGrid metrics={coreMetrics} />
                </div>
              </div>

              {/* Jitter Heatmap */}
              <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60">
                <div className="px-3 py-2 border-b border-slate-800/40">
                  <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                    🛡️ Jitter Containment
                  </span>
                </div>
                <div style={{ minHeight: 280 }}>
                  <JitterHeatmap samples={jitterSamples} />
                </div>
              </div>
            </div>
          </div>
        ) : (
          /* Architecture Spec Tab */
          <div className="h-full grid grid-cols-1 lg:grid-cols-2 gap-3">
            <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60 overflow-hidden flex flex-col">
              <div className="px-3 py-2 border-b border-slate-800/40">
                <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                  Code-Aligned Architecture
                </span>
              </div>
              <div className="flex-1 overflow-y-auto">
                <ArchitectureSpec />
              </div>
            </div>

            <div className="flex flex-col gap-3">
              {/* Timing Gate */}
              <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60 flex-1">
                <div className="px-3 py-2 border-b border-slate-800/40">
                  <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                    ⚡ 1µs Hard Gate — Live
                  </span>
                </div>
                <div style={{ minHeight: 280 }}>
                  {gate && <TimingGatePanel gate={gate} history={gateHistory} />}
                </div>
              </div>

              {/* Jitter */}
              <div className="bg-[#0a0f1a] rounded-xl border border-slate-800/60 flex-1">
                <div className="px-3 py-2 border-b border-slate-800/40">
                  <span className="text-[10px] font-mono text-slate-500 uppercase tracking-wider">
                    🛡️ Jitter Containment — Live
                  </span>
                </div>
                <div style={{ minHeight: 220 }}>
                  <JitterHeatmap samples={jitterSamples} />
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* ─── FOOTER STATUS BAR ──────────────────────────────── */}
      <footer className="border-t border-slate-800/40 bg-[#030712] px-4 py-1.5 flex items-center justify-between font-mono text-[9px] text-slate-600 shrink-0">
        <div className="flex items-center gap-4">
          <span>SharedArrayBuffer SPSC</span>
          <span>•</span>
          <span>Atomics.store / Atomics.load</span>
          <span>•</span>
          <span>isolcpus + nohz_full</span>
        </div>
        <div className="flex items-center gap-4">
          <span className="text-emerald-700">✕ No Workers</span>
          <span className="text-emerald-700">✕ No Mutexes</span>
          <span className="text-emerald-700">✕ No Cloning</span>
          <span className={running ? 'text-emerald-500' : 'text-amber-500'}>
            {running ? '● Simulation Active' : '○ Paused'}
          </span>
        </div>
      </footer>
    </div>
  );
}
