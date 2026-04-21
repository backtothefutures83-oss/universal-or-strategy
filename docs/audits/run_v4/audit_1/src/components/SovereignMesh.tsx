import React, { useMemo } from 'react';
import { motion } from 'framer-motion';
import { MeshMode, Role } from '../types/mesh';

interface SovereignMeshProps {
  mode: MeshMode;
  tick: number;
}

const ROLES: Role[] = ['Ingress', 'Router', 'Transform', 'Actor', 'Egress', 'Mirror'];

export const SovereignMesh: React.FC<SovereignMeshProps> = ({ mode, tick }) => {
  const nodeCount = mode === '12-CORE' ? 12 : mode === '6-CORE' ? 6 : 4;
  
  const nodes = useMemo(() => {
    const rotationOffset = (tick * 0.005) % (Math.PI * 2);
    return Array.from({ length: 12 }).map((_, i) => {
      const angle = (i / 12) * Math.PI * 2 + rotationOffset;
      const radius = 220;
      const x = Math.cos(angle) * radius + 300;
      const y = Math.sin(angle) * radius + 250;
      
      const roleIndex = i % 6;
      const role = ROLES[roleIndex];
      const isActive = i < nodeCount;
      const isCompressed = !isActive && mode !== '12-CORE';
      
      // If compressed, find which active node this role is mapped to
      const mappedTo = isCompressed ? i % nodeCount : i;

      return { id: i, x, y, role, isActive, mappedTo };
    });
  }, [mode, nodeCount, tick]);

  return (
    <div className="flex-1 bg-slate-900/50 border border-slate-800 rounded-xl relative overflow-hidden flex items-center justify-center">
      <svg width="600" height="500" viewBox="0 0 600 500" className="drop-shadow-[0_0_15px_rgba(0,0,0,0.5)]">
        {/* Connection Pipes */}
        {nodes.map((node, i) => {
          const nextNode = nodes[(i + 1) % 12];
          const isWireActive = node.isActive && nextNode.isActive;
          
          return (
            <g key={`pipe-${i}`}>
              <line
                x1={node.x}
                y1={node.y}
                x2={nextNode.x}
                y2={nextNode.y}
                stroke={isWireActive ? 'rgba(6, 182, 212, 0.4)' : 'rgba(30, 41, 59, 0.4)'}
                strokeWidth={isWireActive ? 2 : 1}
                strokeDasharray={isWireActive ? "none" : "4 4"}
              />
              {isWireActive && (
                <motion.circle
                  r="3"
                  fill="#22d3ee"
                  initial={{ offset: 0 }}
                  animate={{ 
                    cx: [node.x, nextNode.x],
                    cy: [node.y, nextNode.y]
                  }}
                  transition={{ 
                    duration: 1.5, 
                    repeat: Infinity, 
                    ease: "linear",
                    delay: i * 0.2
                  }}
                />
              )}
            </g>
          );
        })}

        {/* Nodes */}
        {nodes.map((node) => (
          <g key={node.id}>
            <motion.circle
              cx={node.x}
              cy={node.y}
              r={node.isActive ? 22 : 18}
              className={`${
                node.isActive 
                  ? 'fill-slate-900 stroke-cyan-500' 
                  : 'fill-slate-950 stroke-slate-800'
              }`}
              strokeWidth="2"
              initial={false}
              animate={{
                r: node.isActive ? 22 : 18,
                stroke: node.isActive ? '#06b6d4' : '#1e293b'
              }}
            />
            {node.isActive && (
              <motion.circle
                cx={node.x}
                cy={node.y}
                r="26"
                className="fill-none stroke-cyan-500/20"
                strokeWidth="1"
                animate={{ scale: [1, 1.2, 1], opacity: [0.1, 0.3, 0.1] }}
                transition={{ duration: 2, repeat: Infinity }}
              />
            )}
            <text
              x={node.x}
              y={node.y - 30}
              textAnchor="middle"
              className={`text-[9px] font-bold uppercase tracking-tighter ${
                node.isActive ? 'fill-cyan-400' : 'fill-slate-600'
              }`}
            >
              {node.role}
            </text>
            <text
              x={node.x}
              y={node.y + 4}
              textAnchor="middle"
              className={`text-[8px] font-bold ${
                node.isActive ? 'fill-white' : 'fill-slate-700'
              }`}
            >
              #{node.id}
            </text>
          </g>
        ))}
      </svg>

      <div className="absolute top-4 right-4 flex flex-col gap-1 items-end">
        <div className="flex items-center gap-2">
          <span className="text-[10px] text-slate-500 uppercase font-bold">Mesh Density:</span>
          <span className="text-[10px] text-emerald-400 font-bold tracking-widest">{nodeCount}/12 STATIONS</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] text-slate-500 uppercase font-bold">Topology:</span>
          <span className="text-[10px] text-cyan-400 font-bold tracking-widest">RING-MESH</span>
        </div>
      </div>

      <div className="absolute bottom-4 left-4 bg-slate-950/80 border border-slate-800 rounded p-2 text-[9px] text-slate-400">
        <div className="flex items-center gap-2 mb-1">
          <div className="w-2 h-2 rounded-full bg-cyan-500" />
          <span>ACTIVE SPSC PIPE</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full border border-slate-700" />
          <span>COMPRESSED ROLE</span>
        </div>
      </div>
    </div>
  );
};
