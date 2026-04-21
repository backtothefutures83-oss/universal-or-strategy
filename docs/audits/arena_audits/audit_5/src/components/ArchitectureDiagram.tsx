import { useState } from 'react';

export default function ArchitectureDiagram() {
  const [hoveredEngine, setHoveredEngine] = useState<number | null>(null);

  const engines = Array.from({ length: 12 }, (_, i) => ({
    id: i + 1,
    name: `Engine ${i + 1}`,
    core: i + 1,
    status: 'ACTIVE',
    latency: Math.floor(Math.random() * 3 + 1),
  }));

  return (
    <div className="space-y-8">
      {/* Architecture Title */}
      <div className="text-center">
        <h2 className="text-2xl font-bold text-cyan-400 mb-2">"Open Pipes" Architecture</h2>
        <p className="text-slate-400 text-sm max-w-2xl mx-auto">
          12 Engines mapped directly to 12 Isolated Cores — Zero software-staggering, 
          Zero Supervisor-Sequencer. Raw "Hot Potato" pass-along to Hardware FIFO.
        </p>
      </div>

      {/* Visual Architecture */}
      <div className="bg-slate-900/50 border border-slate-700 rounded-xl p-6">
        <div className="grid grid-cols-4 gap-4 mb-8">
          {engines.map((engine) => (
            <div
              key={engine.id}
              onMouseEnter={() => setHoveredEngine(engine.id)}
              onMouseLeave={() => setHoveredEngine(null)}
              className={`relative p-4 rounded-lg border transition-all cursor-pointer ${
                hoveredEngine === engine.id
                  ? 'border-cyan-400 bg-cyan-500/10 shadow-lg shadow-cyan-500/20'
                  : 'border-slate-700 bg-slate-800/50 hover:border-slate-600'
              }`}
            >
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-mono text-slate-500">CORE {engine.core}</span>
                <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              </div>
              <div className="text-sm font-bold text-slate-200">{engine.name}</div>
              <div className="text-xs text-slate-400 mt-1">~{engine.latency}µs latency</div>
              
              {/* Arrow indicator */}
              <div className="absolute -right-2 top-1/2 -translate-y-1/2 text-cyan-500">
                →
              </div>
            </div>
          ))}
        </div>

        {/* FIFO Visualization */}
        <div className="flex items-center justify-center gap-4 py-6 border-t border-b border-slate-700">
          <div className="text-slate-400 text-sm">Hardware FIFO</div>
          <div className="flex gap-1">
            {Array.from({ length: 12 }).map((_, i) => (
              <div
                key={i}
                className="w-8 h-12 rounded border border-cyan-500/50 bg-cyan-500/10 flex items-center justify-center text-xs font-mono text-cyan-400"
              >
                {i + 1}
              </div>
            ))}
          </div>
          <div className="text-slate-400 text-sm">→ Persistence</div>
        </div>
      </div>

      {/* Architecture Principles */}
      <div className="grid grid-cols-3 gap-6">
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-6">
          <div className="text-3xl mb-3">⚡</div>
          <h3 className="font-bold text-cyan-400 mb-2">Zero Software-Staggering</h3>
          <p className="text-sm text-slate-400">
            No intermediate queuing layers. Each engine writes directly to its assigned FIFO slot 
            without passing through a supervisor or sequencer.
          </p>
        </div>
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-6">
          <div className="text-3xl mb-3">🔗</div>
          <h3 className="font-bold text-cyan-400 mb-2">Zero Supervisor-Sequencer</h3>
          <p className="text-sm text-slate-400">
            Eliminated the serialization bottleneck of a central coordinator. 
            Ordering is maintained through deterministic core assignment.
          </p>
        </div>
        <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-6">
          <div className="text-3xl mb-3">🥔</div>
          <h3 className="font-bold text-cyan-400 mb-2">Hot Potato Protocol</h3>
          <p className="text-sm text-slate-400">
            Messages are passed along immediately upon receipt — no buffering, 
            no batching. Each engine handles its slice of the stream independently.
          </p>
        </div>
      </div>

      {/* Data Flow Diagram */}
      <div className="bg-slate-900/50 border border-slate-700 rounded-xl p-6">
        <h3 className="font-bold text-lg mb-6 text-center">Data Flow — Physics of the Pipe</h3>
        <div className="flex items-center justify-between text-center">
          <div className="space-y-2">
            <div className="w-24 h-24 rounded-xl bg-gradient-to-br from-blue-500 to-cyan-500 flex items-center justify-center text-white text-sm font-bold">
              INGRESS
            </div>
            <div className="text-xs text-slate-400">Network Layer</div>
          </div>
          <div className="flex-1 px-4">
            <div className="border-t-2 border-dashed border-cyan-500/50 relative">
              <span className="absolute -top-3 left-1/2 -translate-x-1/2 bg-slate-900 px-2 text-xs text-cyan-400">
                ~500ns
              </span>
            </div>
          </div>
          <div className="space-y-2">
            <div className="w-24 h-24 rounded-xl bg-gradient-to-br from-cyan-500 to-teal-500 flex items-center justify-center text-white text-sm font-bold">
              ENGINE
            </div>
            <div className="text-xs text-slate-400">Processing Core</div>
          </div>
          <div className="flex-1 px-4">
            <div className="border-t-2 border-dashed border-cyan-500/50 relative">
              <span className="absolute -top-3 left-1/2 -translate-x-1/2 bg-slate-900 px-2 text-xs text-cyan-400">
                ~1µs
              </span>
            </div>
          </div>
          <div className="space-y-2">
            <div className="w-24 h-24 rounded-xl bg-gradient-to-br from-teal-500 to-emerald-500 flex items-center justify-center text-white text-sm font-bold">
              FIFO
            </div>
            <div className="text-xs text-slate-400">Hardware Buffer</div>
          </div>
          <div className="flex-1 px-4">
            <div className="border-t-2 border-dashed border-cyan-500/50 relative">
              <span className="absolute -top-3 left-1/2 -translate-x-1/2 bg-slate-900 px-2 text-xs text-cyan-400">
                ~10µs
              </span>
            </div>
          </div>
          <div className="space-y-2">
            <div className="w-24 h-24 rounded-xl bg-gradient-to-br from-emerald-500 to-green-500 flex items-center justify-center text-white text-sm font-bold">
              REDIS
            </div>
            <div className="text-xs text-slate-400">Persistence</div>
          </div>
        </div>
        <div className="text-center mt-6 text-sm text-slate-400">
          Total Pipeline Latency: <span className="text-cyan-400 font-mono">~11.5µs</span> (target: &lt;10µs with optimizations)
        </div>
      </div>
    </div>
  );
}
