import { useEffect, useRef, useState } from "react";

const NUM_CORES = 12;
const RADIUS = 130;
const CX = 200;
const CY = 200;

function corePos(i: number) {
  const angle = (i / NUM_CORES) * 2 * Math.PI - Math.PI / 2;
  return {
    x: CX + RADIUS * Math.cos(angle),
    y: CY + RADIUS * Math.sin(angle),
  };
}

interface Packet {
  id: number;
  from: number;
  to: number;
  progress: number; // 0-1
  color: string;
}

const COLORS = ["#34d399", "#38bdf8", "#a78bfa", "#fb923c", "#f472b6", "#facc15"];

let packetId = 0;

export default function PipeTopology() {
  const [packets, setPackets] = useState<Packet[]>([]);
  const [activeCore, setActiveCore] = useState<number | null>(null);
  const [latencyLog, setLatencyLog] = useState<{ from: number; to: number; us: number }[]>([]);
  const rafRef = useRef<number>(0);
  const packetsRef = useRef<Packet[]>([]);
  const lastSpawnRef = useRef<number>(0);

  useEffect(() => {
    let running = true;

    const loop = (ts: number) => {
      if (!running) return;

      // Spawn new packet every ~300ms
      if (ts - lastSpawnRef.current > 300) {
        lastSpawnRef.current = ts;
        const from = Math.floor(Math.random() * NUM_CORES);
        let to = Math.floor(Math.random() * NUM_CORES);
        while (to === from) to = Math.floor(Math.random() * NUM_CORES);
        const color = COLORS[Math.floor(Math.random() * COLORS.length)];
        packetsRef.current = [
          ...packetsRef.current,
          { id: packetId++, from, to, progress: 0, color },
        ];
      }

      // Advance packets
      const next: Packet[] = [];
      const completed: { from: number; to: number; us: number }[] = [];
      for (const p of packetsRef.current) {
        const np = p.progress + 0.018;
        if (np >= 1) {
          completed.push({
            from: p.from,
            to: p.to,
            us: Math.floor(Math.random() * 5) + 3,
          });
        } else {
          next.push({ ...p, progress: np });
        }
      }
      packetsRef.current = next;
      setPackets([...next]);

      if (completed.length > 0) {
        setLatencyLog((prev) =>
          [...completed, ...prev].slice(0, 6)
        );
      }

      rafRef.current = requestAnimationFrame(loop);
    };

    rafRef.current = requestAnimationFrame(loop);
    return () => {
      running = false;
      cancelAnimationFrame(rafRef.current);
    };
  }, []);

  return (
    <div className="flex flex-col lg:flex-row gap-6 items-center justify-center">
      {/* SVG Mesh */}
      <div className="relative rounded-2xl border border-slate-700 bg-slate-900/80 p-4 backdrop-blur">
        <div className="text-xs font-semibold text-slate-500 uppercase tracking-widest mb-2 text-center">
          12-Core SPSC Ring Mesh · Live Simulation
        </div>
        <svg width={400} height={400} viewBox="0 0 400 400">
          <defs>
            <radialGradient id="bgGrad" cx="50%" cy="50%" r="50%">
              <stop offset="0%" stopColor="#0f172a" />
              <stop offset="100%" stopColor="#020617" />
            </radialGradient>
            {COLORS.map((_c, i) => (
              <filter key={i} id={`glow-${i}`}>
                <feGaussianBlur stdDeviation="3" result="blur" />
                <feMerge>
                  <feMergeNode in="blur" />
                  <feMergeNode in="SourceGraphic" />
                </feMerge>
              </filter>
            ))}
          </defs>

          <rect width={400} height={400} fill="url(#bgGrad)" rx={16} />

          {/* Ring circle */}
          <circle
            cx={CX}
            cy={CY}
            r={RADIUS}
            fill="none"
            stroke="#1e293b"
            strokeWidth={1}
            strokeDasharray="4 4"
          />

          {/* Adjacency spokes — SPSC channels */}
          {Array.from({ length: NUM_CORES }).map((_, i) => {
            const j = (i + 1) % NUM_CORES;
            const a = corePos(i);
            const b = corePos(j);
            return (
              <line
                key={i}
                x1={a.x} y1={a.y}
                x2={b.x} y2={b.y}
                stroke="#1e3a5f"
                strokeWidth={1}
              />
            );
          })}

          {/* Cross-core channels (every 4) */}
          {Array.from({ length: NUM_CORES }).map((_, i) => {
            const j = (i + 4) % NUM_CORES;
            const a = corePos(i);
            const b = corePos(j);
            return (
              <line
                key={`x${i}`}
                x1={a.x} y1={a.y}
                x2={b.x} y2={b.y}
                stroke="#0f2a3f"
                strokeWidth={0.8}
              />
            );
          })}

          {/* Packets */}
          {packets.map((p) => {
            const a = corePos(p.from);
            const b = corePos(p.to);
            const t = p.progress;
            // Quadratic bezier through center
            const mx = CX + (Math.random() * 20 - 10); // slight jitter for vis
            const my = CY + (Math.random() * 20 - 10);
            const x = (1 - t) * (1 - t) * a.x + 2 * (1 - t) * t * mx + t * t * b.x;
            const y = (1 - t) * (1 - t) * a.y + 2 * (1 - t) * t * my + t * t * b.y;
            return (
              <g key={p.id}>
                <circle cx={x} cy={y} r={5} fill={p.color} opacity={0.25} />
                <circle cx={x} cy={y} r={2.5} fill={p.color} />
              </g>
            );
          })}

          {/* Core nodes */}
          {Array.from({ length: NUM_CORES }).map((_, i) => {
            const { x, y } = corePos(i);
            const isActive = activeCore === i;
            return (
              <g
                key={i}
                onClick={() => setActiveCore(isActive ? null : i)}
                style={{ cursor: "pointer" }}
              >
                <circle
                  cx={x} cy={y} r={isActive ? 14 : 10}
                  fill={isActive ? "#10b981" : "#0f172a"}
                  stroke={isActive ? "#34d399" : "#334155"}
                  strokeWidth={isActive ? 2 : 1.5}
                />
                <text
                  x={x} y={y + 1}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  fill={isActive ? "#fff" : "#94a3b8"}
                  fontSize={isActive ? 7 : 6}
                  fontWeight="bold"
                  fontFamily="monospace"
                >
                  C{i}
                </text>
              </g>
            );
          })}

          {/* Center label */}
          <text
            x={CX} y={CY - 8}
            textAnchor="middle"
            fill="#475569"
            fontSize={8}
            fontFamily="monospace"
          >
            SAB SLAB
          </text>
          <text
            x={CX} y={CY + 6}
            textAnchor="middle"
            fill="#334155"
            fontSize={7}
            fontFamily="monospace"
          >
            mlockall
          </text>
        </svg>
      </div>

      {/* Live latency log */}
      <div className="flex flex-col gap-3 min-w-[220px]">
        <div className="text-xs font-semibold text-slate-500 uppercase tracking-widest">
          Live IPC Events
        </div>
        {latencyLog.length === 0 && (
          <div className="text-slate-600 text-xs">Waiting for packets…</div>
        )}
        {latencyLog.map((e, idx) => (
          <div
            key={idx}
            className="rounded-xl border border-slate-700 bg-slate-800/60 px-4 py-3 flex items-center justify-between gap-4"
          >
            <div className="font-mono text-xs text-slate-400">
              <span className="text-emerald-400">C{e.from}</span>
              <span className="text-slate-600 mx-1">→</span>
              <span className="text-sky-400">C{e.to}</span>
            </div>
            <div className="font-mono text-sm font-bold text-white">
              {e.us}
              <span className="text-xs text-slate-500 font-normal">µs</span>
            </div>
            <div
              className={`h-2 w-2 rounded-full ${e.us < 7 ? "bg-emerald-400" : "bg-yellow-400"}`}
            />
          </div>
        ))}
        <div className="rounded-xl border border-emerald-800/50 bg-emerald-950/30 px-4 py-3 text-xs text-emerald-400 font-mono">
          ✓ All paths below 10µs gate
        </div>
      </div>
    </div>
  );
}
