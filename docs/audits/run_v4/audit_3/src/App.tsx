import { useMemo, useState } from "react";

type Mode = "elite" | "compressed";

const stationRows = [
  ["Ingress-A", "Router-A", "Transform-A", "Actor-A", "Egress-A", "Mirror-A"],
  ["Ingress-B", "Router-B", "Transform-B", "Actor-B", "Egress-B", "Mirror-B"],
];

const hopBudgetUs = [
  { hop: "L1 publish + store fence", us: 0.55 },
  { hop: "Spin-poll detect (cache-hot)", us: 0.85 },
  { hop: "Router pointer swap", us: 0.45 },
  { hop: "Transform SIMD pass", us: 2.6 },
  { hop: "Actor dispatch + ack", us: 1.95 },
  { hop: "Egress mirror fork", us: 0.95 },
];

function MeshRail({ nodes, rail }: { nodes: string[]; rail: string }) {
  return (
    <div className="relative grid grid-cols-6 gap-2 md:gap-3">
      <div className="pointer-events-none absolute left-6 right-6 top-1/2 h-px -translate-y-1/2 bg-cyan-400/35" />
      <div
        className="mesh-flow pointer-events-none absolute top-1/2 h-1.5 w-1.5 -translate-y-1/2 rounded-full bg-cyan-300 shadow-[0_0_14px_2px_rgba(103,232,249,0.75)]"
        style={{ animationDelay: rail === "A" ? "0s" : "0.9s" }}
      />
      {nodes.map((node) => (
        <div key={node} className="relative z-10 px-1 py-4 text-center">
          <div className="mesh-node mx-auto h-2.5 w-2.5 rounded-full bg-cyan-300" />
          <p className="mt-2 text-[10px] tracking-[0.2em] text-slate-300 md:text-xs">{node}</p>
        </div>
      ))}
    </div>
  );
}

export default function App() {
  const [mode, setMode] = useState<Mode>("elite");

  const totalBudget = useMemo(
    () => hopBudgetUs.reduce((sum, item) => sum + item.us, 0),
    []
  );

  return (
    <main className="relative min-h-screen overflow-hidden bg-slate-950 text-slate-100">
      <div className="mesh-halo pointer-events-none absolute inset-0 opacity-70" />
      <section className="relative mx-auto flex min-h-screen w-full max-w-6xl flex-col justify-center px-6 py-16 md:px-10">
        <p className="text-xs tracking-[0.35em] text-cyan-300">ANTIGRAVITY NEXUS OS</p>
        <h1 className="mt-4 max-w-4xl text-4xl font-semibold tracking-tight text-white md:text-6xl">
          Adaptive Sovereign Mesh
        </h1>
        <p className="mt-5 max-w-3xl text-base text-slate-300 md:text-lg">
          IPC transport layer for a 12-to-6 symmetric SPSC fabric. Everything is a pipe: no
          multi-writer edges, no cloning, no heap churn after boot.
        </p>

        <div className="mt-9 flex flex-wrap gap-3">
          <button
            type="button"
            onClick={() => setMode("elite")}
            className={`border px-4 py-2 text-sm tracking-wide transition ${
              mode === "elite"
                ? "border-cyan-300 bg-cyan-300/15 text-cyan-100"
                : "border-slate-600 text-slate-300 hover:border-cyan-400"
            }`}
          >
            Elite Mode: 12-Core
          </button>
          <button
            type="button"
            onClick={() => setMode("compressed")}
            className={`border px-4 py-2 text-sm tracking-wide transition ${
              mode === "compressed"
                ? "border-cyan-300 bg-cyan-300/15 text-cyan-100"
                : "border-slate-600 text-slate-300 hover:border-cyan-400"
            }`}
          >
            Core-Compressed: 6/4-Core
          </button>
        </div>

        <div className="mt-10 space-y-5 border border-slate-800/90 bg-slate-950/60 p-5 backdrop-blur-sm md:p-7">
          <p className="text-xs tracking-[0.3em] text-slate-400">STEP 1: 12-STATION SPSC TOPOLOGY</p>
          <MeshRail nodes={stationRows[0]} rail="A" />
          <MeshRail nodes={stationRows[1]} rail="B" />
          <p className="text-sm text-slate-300">
            Each arrow is a private one-way ring (single producer, single consumer). Mirror nodes
            read from dedicated egress taps only; they never write into hot execution lanes.
          </p>
        </div>

        <div className="mt-10 grid gap-8 md:grid-cols-2">
          <section className="space-y-3">
            <p className="text-xs tracking-[0.3em] text-slate-400">
              STEP 2: HARDWARE-SYMMETRIC SHARDING
            </p>
            {mode === "elite" ? (
              <div className="space-y-2 text-sm text-slate-200">
                <p>Ingress, Router, Transform, Actor, Egress, Mirror each pinned per lane.</p>
                <p>12 stations use 12 pinned threads. Zero migration and no shared write target.</p>
                <p>Actor and Ingress get isolated physical cores with SMT sibling disabled.</p>
              </div>
            ) : (
              <div className="space-y-2 text-sm text-slate-200">
                <p>Protect hot path: Ingress and Actor remain pinned on dedicated cores.</p>
                <p>Collapse Router+Transform and Egress+Mirror into paired service threads.</p>
                <p>
                  Pipe purity remains SPSC by forwarding ownership token in-order through local
                  relays.
                </p>
              </div>
            )}
          </section>

          <section className="space-y-3">
            <p className="text-xs tracking-[0.3em] text-slate-400">STEP 3: JITTER GUARD (10 US GATE)</p>
            <div className="space-y-1 text-sm text-slate-200">
              {hopBudgetUs.map((item) => (
                <div key={item.hop} className="flex items-center justify-between border-b border-slate-800 py-1">
                  <span>{item.hop}</span>
                  <span>{item.us.toFixed(2)} us</span>
                </div>
              ))}
            </div>
            <p className="pt-2 text-sm font-medium text-cyan-200">
              Total: {totalBudget.toFixed(2)} us, headroom: {(10 - totalBudget).toFixed(2)} us under
              cache-hot L1/L2 conditions.
            </p>
          </section>
        </div>
      </section>
    </main>
  );
}
