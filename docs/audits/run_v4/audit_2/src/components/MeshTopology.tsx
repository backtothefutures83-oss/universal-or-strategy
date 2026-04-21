import { useState } from "react";
import { MESH_NODES, SPSC_WIRES, type MeshNode } from "../data/meshData";

const ROLE_COLORS: Record<string, { stroke: string; fill: string; glow: string; text: string }> = {
  Ingress:   { stroke: "#06b6d4", fill: "#083344", glow: "#06b6d4", text: "#67e8f9" },
  Router:    { stroke: "#8b5cf6", fill: "#2e1065", glow: "#8b5cf6", text: "#c4b5fd" },
  Transform: { stroke: "#f59e0b", fill: "#451a03", glow: "#f59e0b", text: "#fcd34d" },
  Actor:     { stroke: "#10b981", fill: "#022c22", glow: "#10b981", text: "#6ee7b7" },
  Egress:    { stroke: "#3b82f6", fill: "#1e3a8a", glow: "#3b82f6", text: "#93c5fd" },
  Mirror:    { stroke: "#6b7280", fill: "#111827", glow: "#6b7280", text: "#9ca3af" },
};

// Layout positions for the 12 nodes on an SVG canvas (1000×620)
const NODE_POSITIONS: Record<string, { x: number; y: number }> = {
  "ING-0":  { x: 120,  y: 140 },
  "ING-1":  { x: 120,  y: 360 },
  "RTR-0":  { x: 290,  y: 140 },
  "RTR-1":  { x: 290,  y: 360 },
  "TRF-0":  { x: 470,  y: 140 },
  "TRF-1":  { x: 470,  y: 360 },
  "ACT-0":  { x: 640,  y: 140 },
  "ACT-1":  { x: 640,  y: 360 },
  "EGR-0":  { x: 810,  y: 140 },
  "EGR-1":  { x: 810,  y: 360 },
  "MIR-0":  { x: 940,  y: 230 },
  "MIR-1":  { x: 940,  y: 390 },
};

const NODE_R = 36;

function getArcPath(x1: number, y1: number, x2: number, y2: number): string {
  const mx = (x1 + x2) / 2;
  const my = (y1 + y2) / 2;
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len = Math.sqrt(dx * dx + dy * dy);
  // Slight curve perpendicular to the line
  const cx = mx - (dy / len) * 28;
  const cy = my + (dx / len) * 28;
  return `M ${x1} ${y1} Q ${cx} ${cy} ${x2} ${y2}`;
}

// Animated "packet" dot traveling along path
function PacketDot({ from, to, color, delay }: { from: string; to: string; color: string; delay: number }) {
  const p1 = NODE_POSITIONS[from];
  const p2 = NODE_POSITIONS[to];
  if (!p1 || !p2) return null;
  const duration = 1.8 + Math.random() * 0.6;
  return (
    <circle r="5" fill={color} opacity="0.95" filter={`url(#glow-${color.replace("#", "")})`}>
      <animateMotion
        dur={`${duration}s`}
        begin={`${delay}s`}
        repeatCount="indefinite"
        path={getArcPath(p1.x, p1.y, p2.x, p2.y)}
      />
      <animate attributeName="opacity" values="0;1;1;0" dur={`${duration}s`} begin={`${delay}s`} repeatCount="indefinite" />
    </circle>
  );
}

export default function MeshTopology() {
  const [selected, setSelected] = useState<MeshNode | null>(null);
  const [hoveredWire, setHoveredWire] = useState<string | null>(null);

  return (
    <div className="bg-gray-950 border border-cyan-900/40 rounded-xl overflow-hidden">
      {/* Title bar */}
      <div className="flex items-center justify-between px-5 py-3 border-b border-cyan-900/30 bg-black/40">
        <div className="flex items-center gap-2">
          <span className="text-cyan-400 font-mono text-xs font-bold tracking-widest uppercase">
            ◈ STEP 1 — SPSC Topology: 12-Station Sovereign Mesh
          </span>
        </div>
        <div className="flex items-center gap-3">
          {Object.entries(ROLE_COLORS).map(([role, c]) => (
            <span key={role} className="flex items-center gap-1 text-[10px] font-mono" style={{ color: c.text }}>
              <span className="w-2 h-2 rounded-full" style={{ background: c.stroke }} />
              {role}
            </span>
          ))}
        </div>
      </div>

      <div className="relative">
        <svg
          viewBox="0 0 1060 520"
          className="w-full"
          style={{ minHeight: 320 }}
        >
          <defs>
            {/* Glow filters for each role color */}
            {Object.entries(ROLE_COLORS).map(([role]) => (
              <filter key={role} id={`glow-node-${role}`} x="-50%" y="-50%" width="200%" height="200%">
                <feGaussianBlur stdDeviation="6" result="blur" />
                <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
              </filter>
            ))}
            <filter id="glow-wire" x="-50%" y="-50%" width="200%" height="200%">
              <feGaussianBlur stdDeviation="3" result="blur" />
              <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
            </filter>
            {/* Arrow marker */}
            <marker id="arrow-cyan" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#06b6d4" opacity="0.8" />
            </marker>
            <marker id="arrow-violet" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#8b5cf6" opacity="0.8" />
            </marker>
            <marker id="arrow-amber" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#f59e0b" opacity="0.8" />
            </marker>
            <marker id="arrow-green" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#10b981" opacity="0.8" />
            </marker>
            <marker id="arrow-blue" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#3b82f6" opacity="0.8" />
            </marker>
            <marker id="arrow-gray" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#6b7280" opacity="0.8" />
            </marker>
          </defs>

          {/* Grid background */}
          <pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse">
            <path d="M 40 0 L 0 0 0 40" fill="none" stroke="#1f2937" strokeWidth="0.5" />
          </pattern>
          <rect width="1060" height="520" fill="url(#grid)" />

          {/* Role lane dividers */}
          {[170, 340, 510, 680, 850].map((x, i) => (
            <line key={i} x1={x} y1={20} x2={x} y2={500} stroke="#1f2937" strokeWidth="1" strokeDasharray="4,6" />
          ))}
          {/* Lane labels */}
          {["INGRESS", "ROUTER", "TRANSFORM", "ACTOR", "EGRESS", "MIRROR"].map((label, i) => {
            const centers = [85, 255, 430, 600, 770, 975];
            return (
              <text key={label} x={centers[i]} y={14} textAnchor="middle" fontSize="8" fill="#374151" fontFamily="monospace" letterSpacing="2" dominantBaseline="middle">
                {label}
              </text>
            );
          })}

          {/* SPSC Wires */}
          {SPSC_WIRES.map((wire) => {
            const p1 = NODE_POSITIONS[wire.from];
            const p2 = NODE_POSITIONS[wire.to];
            if (!p1 || !p2) return null;
            const fromNode = MESH_NODES.find(n => n.id === wire.from)!;
            const c = ROLE_COLORS[fromNode.role];
            const isHot = hoveredWire === wire.id;
            const arrowMap: Record<string, string> = {
              Ingress: "arrow-cyan", Router: "arrow-violet", Transform: "arrow-amber",
              Actor: "arrow-green", Egress: "arrow-blue", Mirror: "arrow-gray",
            };
            return (
              <g key={wire.id}>
                {/* glow underlay */}
                {isHot && (
                  <path
                    d={getArcPath(p1.x, p1.y, p2.x, p2.y)}
                    fill="none"
                    stroke={c.stroke}
                    strokeWidth="6"
                    opacity="0.25"
                    filter="url(#glow-wire)"
                  />
                )}
                <path
                  d={getArcPath(p1.x, p1.y, p2.x, p2.y)}
                  fill="none"
                  stroke={c.stroke}
                  strokeWidth={isHot ? 2.5 : 1.5}
                  opacity={isHot ? 1 : 0.5}
                  strokeDasharray={wire.cacheLevel === "L3" ? "5,4" : wire.cacheLevel === "L2" ? "3,3" : "none"}
                  markerEnd={`url(#${arrowMap[fromNode.role]})`}
                  style={{ cursor: "pointer", transition: "all 0.15s" }}
                  onMouseEnter={() => setHoveredWire(wire.id)}
                  onMouseLeave={() => setHoveredWire(null)}
                />
                {/* Wire latency label on hover */}
                {isHot && (() => {
                  const mx = (p1.x + p2.x) / 2;
                  const my = (p1.y + p2.y) / 2 - 14;
                  return (
                    <g>
                      <rect x={mx - 26} y={my - 9} width={52} height={16} rx="3" fill="#0f172a" stroke={c.stroke} strokeWidth="0.8" />
                      <text x={mx} y={my + 0.5} textAnchor="middle" fontSize="9" fill={c.text} fontFamily="monospace" dominantBaseline="middle">
                        {wire.latencyNs}ns · {wire.cacheLevel}
                      </text>
                    </g>
                  );
                })()}
              </g>
            );
          })}

          {/* Animated packet dots */}
          {SPSC_WIRES.map((wire, i) => {
            const fromNode = MESH_NODES.find(n => n.id === wire.from)!;
            const c = ROLE_COLORS[fromNode.role];
            return (
              <PacketDot
                key={wire.id + "-pkt"}
                from={wire.from}
                to={wire.to}
                color={c.stroke}
                delay={i * 0.22}
              />
            );
          })}

          {/* Nodes */}
          {MESH_NODES.map((node) => {
            const pos = NODE_POSITIONS[node.id];
            if (!pos) return null;
            const c = ROLE_COLORS[node.role];
            const isSelected = selected?.id === node.id;
            return (
              <g
                key={node.id}
                style={{ cursor: "pointer" }}
                onClick={() => setSelected(isSelected ? null : node)}
              >
                {/* Outer glow ring */}
                {isSelected && (
                  <circle cx={pos.x} cy={pos.y} r={NODE_R + 8} fill="none" stroke={c.stroke} strokeWidth="2" opacity="0.4">
                    <animate attributeName="r" values={`${NODE_R + 6};${NODE_R + 14};${NODE_R + 6}`} dur="2s" repeatCount="indefinite" />
                    <animate attributeName="opacity" values="0.4;0.1;0.4" dur="2s" repeatCount="indefinite" />
                  </circle>
                )}
                {/* Node circle */}
                <circle
                  cx={pos.x} cy={pos.y} r={NODE_R}
                  fill={c.fill}
                  stroke={c.stroke}
                  strokeWidth={isSelected ? 2.5 : 1.5}
                  filter={`url(#glow-node-${node.role})`}
                />
                {/* Priority ring */}
                <circle
                  cx={pos.x} cy={pos.y} r={NODE_R - 6}
                  fill="none"
                  stroke={node.priority === "hot" ? c.stroke : node.priority === "warm" ? "#374151" : "#1f2937"}
                  strokeWidth="0.8"
                  strokeDasharray={node.priority === "cold" ? "3,3" : "none"}
                  opacity="0.5"
                />
                {/* Core pin badge */}
                <circle cx={pos.x + NODE_R - 6} cy={pos.y - NODE_R + 6} r="9" fill="#000" stroke={c.stroke} strokeWidth="1" />
                <text x={pos.x + NODE_R - 6} y={pos.y - NODE_R + 6} textAnchor="middle" dominantBaseline="middle" fontSize="7" fill={c.text} fontFamily="monospace" fontWeight="bold">
                  C{node.corePin}
                </text>
                {/* Node ID */}
                <text x={pos.x} y={pos.y - 8} textAnchor="middle" dominantBaseline="middle" fontSize="9" fill={c.text} fontFamily="monospace" fontWeight="bold">
                  {node.id}
                </text>
                {/* Role */}
                <text x={pos.x} y={pos.y + 7} textAnchor="middle" dominantBaseline="middle" fontSize="7.5" fill={c.stroke} fontFamily="monospace" opacity="0.8">
                  {node.role.toUpperCase()}
                </text>
                {/* Hot indicator */}
                {node.priority === "hot" && (
                  <circle cx={pos.x - NODE_R + 6} cy={pos.y - NODE_R + 6} r="4" fill="#10b981">
                    <animate attributeName="opacity" values="1;0.3;1" dur="1.2s" repeatCount="indefinite" />
                  </circle>
                )}
              </g>
            );
          })}
        </svg>

        {/* Node detail panel */}
        {selected && (() => {
          const c = ROLE_COLORS[selected.role];
          const wires = SPSC_WIRES.filter(w => w.from === selected.id || w.to === selected.id);
          return (
            <div
              className="absolute top-4 right-4 w-72 rounded-lg border p-4 text-xs font-mono space-y-2 backdrop-blur-sm"
              style={{ background: `${c.fill}ee`, borderColor: c.stroke }}
            >
              <div className="flex items-center justify-between">
                <span className="font-bold text-sm" style={{ color: c.text }}>{selected.id}</span>
                <button onClick={() => setSelected(null)} className="opacity-50 hover:opacity-100 text-white text-sm">✕</button>
              </div>
              <p className="opacity-70 leading-relaxed text-[10px]">{selected.description}</p>
              <div className="grid grid-cols-2 gap-1.5 pt-1">
                <Stat label="Core Pin" value={`Core ${selected.corePin}`} color={c.text} />
                <Stat label="Priority"  value={selected.priority.toUpperCase()} color={c.text} />
                <Stat label="Budget"    value={`${selected.cyclesBudget} ns`} color={c.text} />
                <Stat label="Slab Pool" value={selected.slabPool} color={c.text} />
              </div>
              <div className="pt-1 border-t border-white/10">
                <p className="opacity-50 text-[9px] mb-1">CONNECTED WIRES</p>
                {wires.map(w => (
                  <div key={w.id} className="flex justify-between opacity-70 text-[9px]">
                    <span>{w.label}</span>
                    <span>{w.latencyNs}ns · {w.cacheLevel} · R={w.ringSize}</span>
                  </div>
                ))}
              </div>
            </div>
          );
        })()}
      </div>

      <div className="px-5 py-2 border-t border-cyan-900/30 bg-black/20 flex items-center gap-4 text-[10px] font-mono text-gray-500">
        <span className="text-cyan-600">━━</span> L1-hot wire
        <span className="text-cyan-600">╌╌</span> L2-warm wire
        <span className="text-cyan-600">┄┄</span> L3/async wire
        <span className="ml-auto">Click a node to inspect · Hover a wire for latency</span>
      </div>
    </div>
  );
}

function Stat({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div className="bg-black/30 rounded px-2 py-1">
      <p className="text-[8px] opacity-50 uppercase tracking-wider">{label}</p>
      <p className="font-bold text-[10px]" style={{ color }}>{value}</p>
    </div>
  );
}
