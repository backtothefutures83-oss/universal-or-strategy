// ═══════════════════════════════════════════════════════════════════
//  TOPOLOGY MESH — SVG-rendered 12-Role directed graph
//  Shows SPSC channels, data flow direction, role categories
// ═══════════════════════════════════════════════════════════════════

import { useEffect, useRef, useState, useCallback } from 'react';
import type { MeshRole, SPSCChannel } from '../types';

const CATEGORY_COLORS: Record<string, { bg: string; border: string; glow: string; text: string }> = {
  ingress:   { bg: '#0a2e1f', border: '#10b981', glow: '#10b98140', text: '#6ee7b7' },
  router:    { bg: '#1e1b4b', border: '#818cf8', glow: '#818cf840', text: '#c7d2fe' },
  transform: { bg: '#2d1810', border: '#f59e0b', glow: '#f59e0b40', text: '#fde68a' },
  actor:     { bg: '#1a0a2e', border: '#a855f7', glow: '#a855f740', text: '#d8b4fe' },
  egress:    { bg: '#0a1e2e', border: '#3b82f6', glow: '#3b82f640', text: '#93c5fd' },
  mirror:    { bg: '#1e0a1e', border: '#ec4899', glow: '#ec489940', text: '#f9a8d4' },
};

interface Props {
  roles: MeshRole[];
  channels: SPSCChannel[];
  selectedChannel: SPSCChannel | null;
  onSelectChannel: (ch: SPSCChannel | null) => void;
}

export default function TopologyMesh({ roles, channels, selectedChannel, onSelectChannel }: Props) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [dims, setDims] = useState({ w: 900, h: 500 });

  useEffect(() => {
    const el = svgRef.current?.parentElement;
    if (!el) return;
    const ro = new ResizeObserver((entries) => {
      const { width, height } = entries[0].contentRect;
      setDims({ w: width, h: height });
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const pad = 70;
  const nodeW = 110;
  const nodeH = 52;

  const pos = useCallback(
    (role: MeshRole) => ({
      cx: pad + role.x * (dims.w - 2 * pad - nodeW) + nodeW / 2,
      cy: pad + role.y * (dims.h - 2 * pad - nodeH) + nodeH / 2,
    }),
    [dims]
  );

  const roleMap = new Map(roles.map((r) => [r.id, r]));

  return (
    <div className="w-full h-full relative">
      <svg
        ref={svgRef}
        viewBox={`0 0 ${dims.w} ${dims.h}`}
        className="w-full h-full"
        style={{ minHeight: 420 }}
      >
        <defs>
          <marker id="arrow" viewBox="0 0 10 6" refX="9" refY="3"
            markerWidth="8" markerHeight="6" orient="auto-start-reverse">
            <path d="M 0 0 L 10 3 L 0 6 z" fill="#475569" />
          </marker>
          <marker id="arrow-active" viewBox="0 0 10 6" refX="9" refY="3"
            markerWidth="8" markerHeight="6" orient="auto-start-reverse">
            <path d="M 0 0 L 10 3 L 0 6 z" fill="#38bdf8" />
          </marker>
          <filter id="glow">
            <feGaussianBlur stdDeviation="3" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        {/* NUMA Zone backgrounds */}
        <rect x={pad - 20} y={pad - 20}
          width={(dims.w - 2 * pad) * 0.45 + 40}
          height={dims.h - 2 * pad + 40}
          rx={12} fill="#0f172a" stroke="#1e293b" strokeWidth={1}
          strokeDasharray="4 4" opacity={0.5} />
        <text x={pad - 10} y={pad - 6} fill="#475569" fontSize={10}
          fontFamily="monospace">NUMA NODE 0</text>

        <rect x={pad + (dims.w - 2 * pad) * 0.45 + 30} y={pad - 20}
          width={(dims.w - 2 * pad) * 0.55 + 10}
          height={dims.h - 2 * pad + 40}
          rx={12} fill="#0c1222" stroke="#1e293b" strokeWidth={1}
          strokeDasharray="4 4" opacity={0.5} />
        <text x={pad + (dims.w - 2 * pad) * 0.45 + 40} y={pad - 6}
          fill="#475569" fontSize={10} fontFamily="monospace">NUMA NODE 1</text>

        {/* Channels (edges) */}
        {channels.map((ch, i) => {
          const fromRole = roleMap.get(ch.from);
          const toRole = roleMap.get(ch.to);
          if (!fromRole || !toRole) return null;
          const f = pos(fromRole);
          const t = pos(toRole);
          const isSelected = selectedChannel?.from === ch.from && selectedChannel?.to === ch.to;
          const dx = t.cx - f.cx;
          const dy = t.cy - f.cy;
          const len = Math.sqrt(dx * dx + dy * dy);
          const ux = dx / len;
          const uy = dy / len;
          const startX = f.cx + ux * (nodeW / 2 + 4);
          const startY = f.cy + uy * (nodeH / 2 + 4);
          const endX = t.cx - ux * (nodeW / 2 + 10);
          const endY = t.cy - uy * (nodeH / 2 + 10);

          return (
            <g key={i} onClick={() => onSelectChannel(isSelected ? null : ch)}
              className="cursor-pointer">
              <line x1={startX} y1={startY} x2={endX} y2={endY}
                stroke={isSelected ? '#38bdf8' : '#334155'}
                strokeWidth={isSelected ? 2.5 : 1.5}
                markerEnd={isSelected ? 'url(#arrow-active)' : 'url(#arrow)'}
                filter={isSelected ? 'url(#glow)' : undefined} />
              {/* Latency label on edge */}
              <text x={(startX + endX) / 2 + uy * 12}
                y={(startY + endY) / 2 - ux * 12}
                fill={isSelected ? '#38bdf8' : '#475569'}
                fontSize={9} fontFamily="monospace" textAnchor="middle">
                {ch.latencyNs.toFixed(0)}ns
              </text>
            </g>
          );
        })}

        {/* Nodes */}
        {roles.map((role) => {
          const { cx, cy } = pos(role);
          const colors = CATEGORY_COLORS[role.category];
          return (
            <g key={role.id}>
              <rect
                x={cx - nodeW / 2} y={cy - nodeH / 2}
                width={nodeW} height={nodeH}
                rx={8}
                fill={colors.bg}
                stroke={colors.border}
                strokeWidth={1.5}
                filter="url(#glow)"
              />
              <text x={cx} y={cy - 6} fill={colors.text}
                fontSize={11} fontFamily="monospace" fontWeight="bold"
                textAnchor="middle">{role.label}</text>
              <text x={cx} y={cy + 10} fill="#64748b"
                fontSize={9} fontFamily="monospace"
                textAnchor="middle">Core {role.coreId}</text>
              <text x={cx} y={cy + 21} fill="#475569"
                fontSize={8} fontFamily="monospace"
                textAnchor="middle">NUMA {role.numaNode}</text>
            </g>
          );
        })}
      </svg>
    </div>
  );
}
