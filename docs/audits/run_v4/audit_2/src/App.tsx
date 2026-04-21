import { useState } from "react";
import Header from "./components/Header";
import MeshTopology from "./components/MeshTopology";
import LatencyBudget from "./components/LatencyBudget";
import HardwareShard from "./components/HardwareShard";
import SlabMemory from "./components/SlabMemory";
import LiveMetrics from "./components/LiveMetrics";
import PhilosophyCard from "./components/PhilosophyCard";

const TABS = [
  { id: "topology",  label: "① SPSC Topology",          icon: "⬡" },
  { id: "shard",     label: "② Hardware Sharding",       icon: "◧" },
  { id: "latency",   label: "③ Jitter Guard",            icon: "⏱" },
  { id: "slab",      label: "Slab Memory",               icon: "▤" },
  { id: "live",      label: "Live Simulation",           icon: "◉" },
  { id: "philosophy",label: "Philosophy & Code",         icon: "⬡" },
] as const;

type TabId = typeof TABS[number]["id"];

export default function App() {
  const [tab, setTab] = useState<TabId>("topology");

  return (
    <div className="min-h-screen bg-gray-950 text-white" style={{ background: "#030712" }}>
      <Header />

      {/* Hero strip */}
      <div className="border-b border-white/5 bg-black/60 relative overflow-hidden">
        {/* Animated grid background */}
        <div className="absolute inset-0 pointer-events-none">
          <svg className="w-full h-full opacity-[0.04]" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <pattern id="bgGrid" width="48" height="48" patternUnits="userSpaceOnUse">
                <path d="M 48 0 L 0 0 0 48" fill="none" stroke="#06b6d4" strokeWidth="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#bgGrid)" />
          </svg>
        </div>
        <div className="max-w-screen-2xl mx-auto px-6 py-6 relative z-10">
          <div className="flex flex-col md:flex-row items-start md:items-center gap-4 justify-between">
            <div>
              <h2 className="text-2xl font-bold font-mono tracking-tight text-white">
                IPC Layer — <span className="text-cyan-400">Adaptive Sovereign Mesh</span> v4
              </h2>
              <p className="text-gray-500 font-mono text-sm mt-1">
                Pure SPSC · Zero-Copy Shared Memory · Fixed Slab Pools · CPU-Pinned Roles · 10µs Gate
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <StatPill label="Nodes" value="12" sub="Stations" color="#06b6d4" />
              <StatPill label="Wires" value="14" sub="SPSC Rings" color="#8b5cf6" />
              <StatPill label="Hot Path" value="6.86µs" sub="of 10µs gate" color="#10b981" />
              <StatPill label="Heap Allocs" value="0" sub="after boot" color="#f59e0b" />
              <StatPill label="Copies" value="0" sub="on hot path" color="#ef4444" />
            </div>
          </div>
        </div>
      </div>

      {/* Tab nav */}
      <div className="sticky top-[57px] z-40 border-b border-white/5 bg-gray-950/95 backdrop-blur-md">
        <div className="max-w-screen-2xl mx-auto px-6">
          <div className="flex gap-0 overflow-x-auto">
            {TABS.map((t) => {
              const isActive = tab === t.id;
              return (
                <button
                  key={t.id}
                  onClick={() => setTab(t.id)}
                  className="flex items-center gap-2 px-5 py-3 text-xs font-mono font-bold border-b-2 whitespace-nowrap transition-all duration-150"
                  style={{
                    borderBottomColor: isActive ? "#06b6d4" : "transparent",
                    color: isActive ? "#06b6d4" : "#4b5563",
                    background: isActive ? "rgba(6,182,212,0.05)" : "transparent",
                  }}
                >
                  <span>{t.icon}</span>
                  {t.label}
                </button>
              );
            })}
          </div>
        </div>
      </div>

      {/* Main content */}
      <main className="max-w-screen-2xl mx-auto px-6 py-6 space-y-6">
        {tab === "topology"   && <MeshTopology />}
        {tab === "shard"      && <HardwareShard />}
        {tab === "latency"    && <LatencyBudget />}
        {tab === "slab"       && <SlabMemory />}
        {tab === "live"       && <LiveMetrics />}
        {tab === "philosophy" && <PhilosophyCard />}
      </main>

      {/* Footer */}
      <footer className="border-t border-white/5 mt-8 py-4 text-center font-mono text-[10px] text-gray-700">
        Antigravity Nexus OS · IPC Transport Layer v4 · Arena AI Design Challenge · Philosophy: Everything is a Pipe
      </footer>
    </div>
  );
}

function StatPill({ label, value, sub, color }: { label: string; value: string; sub: string; color: string }) {
  return (
    <div
      className="rounded-lg border px-3 py-2 text-center min-w-[80px]"
      style={{ borderColor: `${color}30`, background: `${color}08` }}
    >
      <p className="text-[9px] font-mono text-gray-500 uppercase tracking-wider">{label}</p>
      <p className="text-lg font-mono font-bold leading-tight" style={{ color }}>{value}</p>
      <p className="text-[8px] font-mono text-gray-600">{sub}</p>
    </div>
  );
}
