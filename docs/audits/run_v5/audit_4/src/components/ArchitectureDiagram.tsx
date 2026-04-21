import { useState, useEffect } from 'react';
import type { SimulationState } from '../hooks/useSimulation';

interface Props {
  state: SimulationState;
  onTriggerNMI: (shardId: string) => void;
}

const NODE_POSITIONS: Record<string, { x: number; y: number; ring: 'inner' | 'outer' | 'center' }> = {
  'INGRESS':  { x: 400, y: 60,  ring: 'outer' },
  'PARSER':   { x: 620, y: 120, ring: 'outer' },
  'STRATEGY': { x: 720, y: 280, ring: 'outer' },
  'RISK':     { x: 620, y: 440, ring: 'outer' },
  'EGRESS':   { x: 400, y: 500, ring: 'outer' },
  'AUDIT':    { x: 180, y: 440, ring: 'outer' },
  'SIDEBAND': { x: 80,  y: 280, ring: 'outer' },
  'MIRROR':   { x: 180, y: 120, ring: 'outer' },
  'SHARD-A':  { x: 330, y: 210, ring: 'inner' },
  'SHARD-B':  { x: 470, y: 210, ring: 'inner' },
  'SHARD-C':  { x: 470, y: 350, ring: 'inner' },
  'SHARD-D':  { x: 330, y: 350, ring: 'inner' },
};

const PIPES: [string, string, 'trade' | 'sideband' | 'mirror'][] = [
  ['INGRESS', 'PARSER', 'trade'],
  ['PARSER', 'STRATEGY', 'trade'],
  ['STRATEGY', 'SHARD-A', 'trade'],
  ['STRATEGY', 'SHARD-B', 'trade'],
  ['SHARD-A', 'SHARD-C', 'trade'],
  ['SHARD-B', 'SHARD-D', 'trade'],
  ['SHARD-C', 'RISK', 'trade'],
  ['SHARD-D', 'RISK', 'trade'],
  ['RISK', 'EGRESS', 'trade'],
  ['EGRESS', 'AUDIT', 'trade'],
  ['SIDEBAND', 'SHARD-A', 'sideband'],
  ['SIDEBAND', 'SHARD-B', 'sideband'],
  ['MIRROR', 'SHARD-A', 'mirror'],
  ['MIRROR', 'SHARD-B', 'mirror'],
  ['MIRROR', 'SHARD-C', 'mirror'],
  ['MIRROR', 'SHARD-D', 'mirror'],
];

export default function ArchitectureDiagram({ state, onTriggerNMI }: Props) {
  const [hoveredNode, setHoveredNode] = useState<string | null>(null);
  const [, setAnimTick] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => setAnimTick(t => t + 1), 60);
    return () => clearInterval(interval);
  }, []);

  const getNodeColor = (actor: typeof state.actors[0]) => {
    switch (actor.status) {
      case 'active': return actor.id === 'SIDEBAND' ? '#A855F7' : actor.id === 'MIRROR' ? '#22D3EE' : '#C0A040';
      case 'stalled': return '#DC2626';
      case 'mirror-inject': return '#22D3EE';
      case 'idle': return '#555';
      default: return '#C0A040';
    }
  };

  const getPipeColor = (type: 'trade' | 'sideband' | 'mirror') => {
    switch (type) {
      case 'trade': return '#C0A040';
      case 'sideband': return '#A855F7';
      case 'mirror': return '#22D3EE';
    }
  };

  return (
    <section className="relative py-24 px-4">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-12">
          <span className="font-mono text-xs text-[#C0A040]/60 tracking-[0.3em] uppercase">Live Architecture</span>
          <h2 className="text-4xl md:text-5xl font-black text-[#E5E4E2] mt-2">
            Adaptive Sovereign Mesh
          </h2>
          <p className="text-[#B0AFA8] mt-3 max-w-lg mx-auto text-sm">
            12 Station Static Instances · SPSC Pipes · Zero-Heap Transport
          </p>
        </div>

        {/* Legend */}
        <div className="flex flex-wrap justify-center gap-6 mb-8">
          <LegendItem color="#C0A040" label="Trade Pipe (SPSC)" />
          <LegendItem color="#A855F7" label="L1-Sideband (Binary)" />
          <LegendItem color="#22D3EE" label="Mirror Channel" />
          <LegendItem color="#DC2626" label="NMI / Stall" />
        </div>

        {/* SVG Diagram */}
        <div className="relative bg-[#0A0A0F] border border-[#C0A040]/10 rounded-xl overflow-hidden">
          <div className="absolute inset-0 grid-bg opacity-20" />

          {/* NMI trigger button */}
          <div className="absolute top-4 right-4 z-20">
            <button
              onClick={() => onTriggerNMI('SHARD-B')}
              className="px-4 py-2 bg-[#DC2626]/10 border border-[#DC2626]/40 rounded-lg font-mono text-xs text-[#DC2626] hover:bg-[#DC2626]/20 transition-all cursor-pointer"
              disabled={state.nmiDetected}
            >
              {state.nmiDetected ? '⚡ NMI IN PROGRESS...' : '💀 TRIGGER NMI (SHARD-B)'}
            </button>
          </div>

          {state.mirrorActive && (
            <div className="absolute top-4 left-4 z-20 px-4 py-2 bg-[#22D3EE]/10 border border-[#22D3EE]/40 rounded-lg font-mono text-xs text-[#22D3EE]">
              ✓ MIRROR TAKEOVER: {state.mirrorTakeoverUs.toFixed(2)}µs
            </div>
          )}

          <svg viewBox="0 0 800 560" className="w-full relative z-10" style={{ maxHeight: '560px' }}>
            <defs>
              <filter id="glow-gold">
                <feGaussianBlur stdDeviation="3" result="blur" />
                <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
              </filter>
              <filter id="glow-cyan">
                <feGaussianBlur stdDeviation="4" result="blur" />
                <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
              </filter>
              <filter id="glow-red">
                <feGaussianBlur stdDeviation="5" result="blur" />
                <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
              </filter>
            </defs>

            {/* Draw pipes */}
            {PIPES.map(([from, to, type], i) => {
              const f = NODE_POSITIONS[from];
              const t = NODE_POSITIONS[to];
              const color = getPipeColor(type);
              const opacity = type === 'mirror' ? (state.nmiDetected ? 0.8 : 0.15) : (type === 'sideband' ? 0.4 : 0.5);

              return (
                <g key={i}>
                  <line
                    x1={f.x} y1={f.y} x2={t.x} y2={t.y}
                    stroke={color}
                    strokeWidth={type === 'trade' ? 2 : 1.5}
                    opacity={opacity}
                    strokeDasharray={type === 'mirror' ? '6 4' : type === 'sideband' ? '3 3' : 'none'}
                  />
                  {/* Animated data packet */}
                  {(type === 'trade' || (type === 'mirror' && state.nmiDetected) || type === 'sideband') && (
                    <circle r={type === 'sideband' ? 2.5 : 3} fill={color} opacity={0.9}>
                      <animateMotion
                        dur={type === 'trade' ? '2s' : '1.5s'}
                        repeatCount="indefinite"
                        path={`M${f.x},${f.y} L${t.x},${t.y}`}
                      />
                    </circle>
                  )}
                </g>
              );
            })}

            {/* Draw nodes */}
            {state.actors.map((actor) => {
              const pos = NODE_POSITIONS[actor.id];
              if (!pos) return null;
              const color = getNodeColor(actor);
              const isHovered = hoveredNode === actor.id;
              const isInner = pos.ring === 'inner';
              const radius = isInner ? 28 : 24;
              const isStalled = actor.status === 'stalled';

              return (
                <g
                  key={actor.id}
                  onMouseEnter={() => setHoveredNode(actor.id)}
                  onMouseLeave={() => setHoveredNode(null)}
                  className="cursor-pointer"
                  onClick={() => {
                    if (actor.id.startsWith('SHARD')) onTriggerNMI(actor.id);
                  }}
                >
                  {/* Outer ring */}
                  <circle
                    cx={pos.x} cy={pos.y} r={radius + 6}
                    fill="none" stroke={color} strokeWidth={1}
                    opacity={isHovered ? 0.5 : 0.15}
                  />
                  {isStalled && (
                    <circle
                      cx={pos.x} cy={pos.y} r={radius + 12}
                      fill="none" stroke="#DC2626" strokeWidth={2}
                      opacity={0.6}
                      filter="url(#glow-red)"
                    >
                      <animate attributeName="r" from={String(radius + 8)} to={String(radius + 20)} dur="0.8s" repeatCount="indefinite" />
                      <animate attributeName="opacity" from="0.6" to="0" dur="0.8s" repeatCount="indefinite" />
                    </circle>
                  )}

                  {/* Node body */}
                  <circle
                    cx={pos.x} cy={pos.y} r={radius}
                    fill="#12121A"
                    stroke={color}
                    strokeWidth={isHovered ? 2.5 : 1.5}
                    filter={isHovered ? 'url(#glow-gold)' : undefined}
                  />

                  {/* Label */}
                  <text
                    x={pos.x} y={pos.y - 4}
                    textAnchor="middle" fill={color}
                    fontSize={isInner ? 9 : 8}
                    fontFamily="'JetBrains Mono', monospace"
                    fontWeight={600}
                  >
                    {actor.id}
                  </text>
                  <text
                    x={pos.x} y={pos.y + 10}
                    textAnchor="middle" fill="#888"
                    fontSize={7}
                    fontFamily="'JetBrains Mono', monospace"
                  >
                    C{actor.core}
                  </text>

                  {/* Status indicator */}
                  <circle
                    cx={pos.x + radius - 4} cy={pos.y - radius + 4} r={4}
                    fill={actor.status === 'active' ? '#10B981' : actor.status === 'stalled' ? '#DC2626' : actor.status === 'mirror-inject' ? '#22D3EE' : '#555'}
                  >
                    {actor.status === 'active' && (
                      <animate attributeName="opacity" values="1;0.4;1" dur="2s" repeatCount="indefinite" />
                    )}
                  </circle>

                  {/* Tooltip */}
                  {isHovered && (
                    <g>
                      <rect
                        x={pos.x - 80} y={pos.y + radius + 10}
                        width={160} height={55} rx={6}
                        fill="#1A1A28" stroke={color} strokeWidth={0.5}
                      />
                      <text x={pos.x - 72} y={pos.y + radius + 28} fill="#B0AFA8" fontSize={8} fontFamily="'JetBrains Mono', monospace">
                        Latency: {actor.currentLatencyNs.toFixed(0)}ns
                      </text>
                      <text x={pos.x - 72} y={pos.y + radius + 42} fill="#B0AFA8" fontSize={8} fontFamily="'JetBrains Mono', monospace">
                        Msgs: {actor.messagesProcessed.toLocaleString()}
                      </text>
                      <text x={pos.x - 72} y={pos.y + radius + 56} fill={color} fontSize={8} fontFamily="'JetBrains Mono', monospace">
                        Status: {actor.status.toUpperCase()}
                      </text>
                    </g>
                  )}
                </g>
              );
            })}

            {/* Center label */}
            <text x={400} y={278} textAnchor="middle" fill="#C0A040" fontSize={10} fontFamily="'JetBrains Mono', monospace" opacity={0.4}>
              SOVEREIGN MESH
            </text>
            <text x={400} y={292} textAnchor="middle" fill="#888" fontSize={7} fontFamily="'JetBrains Mono', monospace" opacity={0.3}>
              1µs CONSTANT GATE
            </text>
          </svg>
        </div>
      </div>
    </section>
  );
}

function LegendItem({ color, label }: { color: string; label: string }) {
  return (
    <div className="flex items-center gap-2">
      <div className="w-3 h-3 rounded-full" style={{ backgroundColor: color, boxShadow: `0 0 8px ${color}40` }} />
      <span className="font-mono text-xs text-[#B0AFA8]">{label}</span>
    </div>
  );
}
