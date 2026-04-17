import { useState } from 'react';
import type { CpuSocket } from '../hooks/useSovereignSimulation';

interface Props {
  sockets: CpuSocket[];
  detectedLineSize: number;
  crossSocketLatency: number;
  numaAwareness: boolean;
}

export function HardwareTopology({ sockets, detectedLineSize, crossSocketLatency, numaAwareness }: Props) {
  const [selectedSocket, setSelectedSocket] = useState<number | null>(null);
  const activeSocket = selectedSocket !== null ? sockets[selectedSocket] : null;

  const archIcon = (arch: string) => {
    if (arch.includes('x86')) return '🖥️';
    if (arch.includes('ARM')) return '📱';
    return '🔧';
  };

  return (
    <div className="panel-glow rounded-xl border border-sov-border bg-sov-panel p-5 relative overflow-hidden">
      {/* Animated scan line */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-20">
        <div className="w-full h-px bg-gradient-to-r from-transparent via-sov-cyan to-transparent animate-scan-line" />
      </div>

      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-sov-cyan/20 to-sov-cyan/5 border border-sov-cyan/30 flex items-center justify-center text-sm">
            🏗️
          </div>
          <div>
            <h3 className="text-sm font-semibold text-sov-text">Hardware Topology</h3>
            <p className="text-xs text-sov-text-muted font-mono">Auto-Detected &amp; Aligned</p>
          </div>
        </div>
        <div className="flex gap-2">
          <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium border ${numaAwareness ? 'bg-sov-green/10 border-sov-green/30 text-sov-green' : 'bg-sov-red/10 border-sov-red/30 text-sov-red'}`}>
            <span className={`w-1.5 h-1.5 rounded-full ${numaAwareness ? 'bg-sov-green animate-pulse-glow' : 'bg-sov-red'}`} />
            NUMA {numaAwareness ? 'Active' : 'Disabled'}
          </span>
        </div>
      </div>

      {/* Auto-detected specs bar */}
      <div className="grid grid-cols-3 gap-2 mb-4">
        <div className="bg-sov-dark/60 rounded-lg p-2.5 border border-sov-border">
          <div className="text-xs text-sov-text-muted mb-1">Cache Line Width</div>
          <div className="text-lg font-bold text-sov-cyan font-mono glow-cyan">{detectedLineSize}B</div>
          <div className="text-xs text-sov-text-dim">Auto-aligned</div>
        </div>
        <div className="bg-sov-dark/60 rounded-lg p-2.5 border border-sov-border">
          <div className="text-xs text-sov-text-muted mb-1">Cross-Socket Latency</div>
          <div className="text-lg font-bold text-sov-amber font-mono glow-amber">{crossSocketLatency.toFixed(1)}ns</div>
          <div className="text-xs text-sov-text-dim">QPI/UPI measured</div>
        </div>
        <div className="bg-sov-dark/60 rounded-lg p-2.5 border border-sov-border">
          <div className="text-xs text-sov-text-muted mb-1">Sockets Detected</div>
          <div className="text-lg font-bold text-sov-purple font-mono">{sockets.length}</div>
          <div className="text-xs text-sov-text-dim">Heterogeneous</div>
        </div>
      </div>

      {/* Socket cards */}
      <div className="grid grid-cols-2 gap-3 mb-4">
        {sockets.map((socket) => (
          <button
            key={socket.id}
            onClick={() => setSelectedSocket(selectedSocket === socket.id ? null : socket.id)}
            className={`relative rounded-lg border p-3 text-left transition-all duration-300 ${
              selectedSocket === socket.id
                ? 'border-sov-cyan/50 bg-sov-cyan/5 shadow-lg shadow-sov-cyan/10'
                : 'border-sov-border bg-sov-dark/40 hover:border-sov-border-bright'
            }`}
          >
            <div className="flex items-center gap-2 mb-2">
              <span className="text-lg">{archIcon(socket.architecture)}</span>
              <div>
                <div className="text-xs font-semibold text-sov-text">Socket #{socket.id}</div>
                <div className="text-xs text-sov-text-dim font-mono">{socket.architecture}</div>
              </div>
              <span className="ml-auto text-xs text-sov-text-muted font-mono">{socket.cores} cores</span>
            </div>

            {/* Mini cache bars */}
            <div className="space-y-1.5">
              {[socket.l1, socket.l2, socket.l3].map((cache) => (
                <div key={cache.level} className="flex items-center gap-2">
                  <span className="text-xs text-sov-text-dim w-5 font-mono">L{cache.level}</span>
                  <div className="flex-1 h-2 rounded-full bg-sov-black overflow-hidden">
                    <div
                      className="h-full rounded-full transition-all duration-500"
                      style={{
                        width: `${cache.hitRate}%`,
                        background: cache.level === 1
                          ? 'linear-gradient(90deg, #00ff88, #00e5ff)'
                          : cache.level === 2
                          ? 'linear-gradient(90deg, #00e5ff, #448aff)'
                          : 'linear-gradient(90deg, #448aff, #b388ff)',
                      }}
                    />
                  </div>
                  <span className="text-xs text-sov-text-dim font-mono w-14 text-right">
                    {cache.hitRate}%
                  </span>
                </div>
              ))}
            </div>

            {/* NUMA nodes */}
            <div className="flex gap-1.5 mt-2">
              {socket.numaNodes.map((node) => (
                <span
                  key={node.id}
                  className="text-xs px-1.5 py-0.5 rounded bg-sov-black/60 text-sov-text-dim font-mono border border-sov-border"
                >
                  N{node.id}: {node.utilization.toFixed(0)}%
                </span>
              ))}
            </div>

            {selectedSocket === socket.id && (
              <div className="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full bg-sov-cyan animate-pulse-glow" />
            )}
          </button>
        ))}
      </div>

      {/* Detailed socket info */}
      {activeSocket && (
        <div className="bg-sov-dark/60 rounded-lg p-3 border border-sov-border animate-slide-in">
          <div className="text-xs text-sov-cyan font-mono mb-2 flex items-center gap-2">
            <span className="w-1.5 h-1.5 rounded-full bg-sov-cyan animate-pulse-glow" />
            Socket #{activeSocket.id} — Detailed Telemetry
          </div>
          <div className="grid grid-cols-2 gap-x-4 gap-y-1.5 text-xs">
            <div className="flex justify-between">
              <span className="text-sov-text-muted">Architecture:</span>
              <span className="text-sov-text font-mono">{activeSocket.architecture}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sov-text-muted">L1 Latency:</span>
              <span className="text-sov-green font-mono">{activeSocket.l1.latency}ns</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sov-text-muted">L2 Latency:</span>
              <span className="text-sov-text font-mono">{activeSocket.l2.latency}ns</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sov-text-muted">L3 Latency:</span>
              <span className="text-sov-text font-mono">{activeSocket.l3.latency}ns</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sov-text-muted">TSO Parity:</span>
              <span className="text-sov-green font-mono">0x{activeSocket.tsoParity.toString(16).padStart(2, '0').toUpperCase()}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sov-text-muted">Line Alignment:</span>
              <span className="text-sov-cyan font-mono">{detectedLineSize}B ✓</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
