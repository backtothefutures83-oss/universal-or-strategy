import { useState, useEffect, useRef } from "react";

interface MetricPoint {
  t: number;
  ingress: number;
  actor: number;
  egress: number;
  jitter: number;
}

function genPoint(prev: MetricPoint | null, t: number): MetricPoint {
  const jitter = 200 + Math.random() * 600;
  const ingress = prev ? Math.max(100, prev.ingress + (Math.random() - 0.48) * 80) : 420;
  const actor   = prev ? Math.max(500, prev.actor   + (Math.random() - 0.48) * 120) : 1800;
  const egress  = prev ? Math.max(100, prev.egress  + (Math.random() - 0.48) * 60) : 360;
  return { t, ingress, actor, egress, jitter };
}

function Sparkline({ data, lineKey, color, height = 48 }: { data: number[]; lineKey: string; color: string; height?: number }) {
  if (data.length < 2) return <div style={{ height }} />;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  const w = 200;
  const pts = data.map((v, i) => {
    const x = (i / (data.length - 1)) * w;
    const y = height - ((v - min) / range) * height * 0.85 - height * 0.075;
    return `${x},${y}`;
  }).join(" ");

  return (
    <svg viewBox={`0 0 ${w} ${height}`} className="w-full" style={{ height }}>
      <defs>
        <linearGradient id={`sg-${lineKey}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.3" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <polygon
        points={`0,${height} ${pts} ${w},${height}`}
        fill={`url(#sg-${lineKey})`}
      />
      <polyline
        points={pts}
        fill="none"
        stroke={color}
        strokeWidth="1.5"
        strokeLinejoin="round"
        strokeLinecap="round"
      />
      {/* last point dot */}
      {(() => {
        const last = data[data.length - 1];
        const x = w;
        const y = height - ((last - min) / range) * height * 0.85 - height * 0.075;
        return <circle cx={x} cy={y} r="2.5" fill={color} />;
      })()}
    </svg>
  );
}

export default function LiveMetrics() {
  const [history, setHistory] = useState<MetricPoint[]>([]);
  const tickRef = useRef(0);

  useEffect(() => {
    const id = setInterval(() => {
      tickRef.current += 1;
      setHistory(prev => {
        const last = prev[prev.length - 1] ?? null;
        const next = genPoint(last, tickRef.current);
        const arr = [...prev, next];
        return arr.length > 40 ? arr.slice(arr.length - 40) : arr;
      });
    }, 250);
    return () => clearInterval(id);
  }, []);

  const latest = history[history.length - 1];
  const totalPath = latest ? latest.ingress + latest.actor + latest.egress + 320 + 480 + 640 + 800 : 6860;

  const METRICS = [
    { label: "ING Latency",  key: "ingress" as keyof MetricPoint, color: "#06b6d4", unit: "ns", warn: 500 },
    { label: "ACT Latency",  key: "actor"   as keyof MetricPoint, color: "#10b981", unit: "ns", warn: 2200 },
    { label: "EGR Latency",  key: "egress"  as keyof MetricPoint, color: "#3b82f6", unit: "ns", warn: 900 },
    { label: "Jitter",       key: "jitter"  as keyof MetricPoint, color: "#f59e0b", unit: "ns", warn: 700 },
  ];

  return (
    <div className="bg-gray-950 border border-gray-800 rounded-xl overflow-hidden">
      <div className="flex items-center justify-between px-5 py-3 border-b border-gray-800 bg-black/40">
        <span className="text-gray-400 font-mono text-xs font-bold tracking-widest uppercase">
          ◈ Live Simulation — Real-time Pipe Metrics (250ms sample)
        </span>
        <div className="flex items-center gap-2 text-[10px] font-mono">
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          <span className="text-emerald-400">RUNNING · {history.length} samples</span>
        </div>
      </div>

      {/* Total path gauge */}
      <div className="px-5 pt-4 pb-3 border-b border-white/5">
        <div className="flex items-end justify-between mb-1.5">
          <span className="text-[10px] font-mono text-gray-500">HOT-PATH TOTAL (LIVE)</span>
          <div className="flex items-baseline gap-1">
            <span
              className="text-xl font-mono font-bold"
              style={{ color: totalPath > 9000 ? "#ef4444" : totalPath > 7500 ? "#f59e0b" : "#10b981" }}
            >
              {totalPath.toFixed(0)}
            </span>
            <span className="text-xs font-mono text-gray-500">ns</span>
            <span className={`text-[9px] font-mono ml-2 ${totalPath < 10000 ? "text-emerald-400" : "text-red-400"}`}>
              {totalPath < 10000 ? "✓ GATE CLEAR" : "⚠ GATE BREACH"}
            </span>
          </div>
        </div>
        <div className="h-3 bg-gray-900 rounded-full overflow-hidden border border-gray-800">
          <div
            className="h-full rounded-full transition-all duration-300 relative overflow-hidden"
            style={{
              width: `${Math.min(100, (totalPath / 10000) * 100)}%`,
              background: totalPath > 9000 ? "#ef4444" : totalPath > 7500
                ? "linear-gradient(90deg, #10b981, #f59e0b)"
                : "linear-gradient(90deg, #06b6d4, #10b981)",
            }}
          >
            <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/15 to-transparent animate-shimmer" />
          </div>
        </div>
        <div className="flex justify-end mt-0.5 text-[8px] font-mono text-gray-600">10,000 ns gate</div>
      </div>

      {/* 4 sparklines */}
      <div className="grid grid-cols-2 gap-px bg-gray-800/30">
        {METRICS.map((m) => {
          const vals = history.map(h => h[m.key] as number);
          const cur = vals[vals.length - 1] ?? 0;
          const isWarn = cur > m.warn;
          return (
            <div key={m.key} className="bg-gray-950 p-4">
              <div className="flex items-baseline justify-between mb-2">
                <span className="text-[10px] font-mono text-gray-500">{m.label}</span>
                <div className="flex items-baseline gap-1">
                  <span
                    className="text-lg font-mono font-bold transition-colors duration-300"
                    style={{ color: isWarn ? "#ef4444" : m.color }}
                  >
                    {cur.toFixed(0)}
                  </span>
                  <span className="text-[9px] font-mono text-gray-600">{m.unit}</span>
                  {isWarn && <span className="text-[8px] text-red-400 ml-1">⚠</span>}
                </div>
              </div>
              <Sparkline data={vals} lineKey={String(m.key)} color={isWarn ? "#ef4444" : m.color} />
              <div className="flex justify-between text-[8px] font-mono text-gray-700 mt-1">
                <span>MIN {Math.min(...vals, 9999).toFixed(0)}</span>
                <span>AVG {vals.length ? (vals.reduce((a, b) => a + b, 0) / vals.length).toFixed(0) : 0}</span>
                <span>MAX {Math.max(...vals, 0).toFixed(0)}</span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Wire status strip */}
      <div className="px-5 py-3 border-t border-gray-800 flex items-center gap-2 overflow-x-auto">
        {Array.from({ length: 14 }, (_, i) => {
          const active = (history.length + i) % 5 !== 0;
          const wireColors = ["#06b6d4","#06b6d4","#8b5cf6","#8b5cf6","#8b5cf6","#8b5cf6","#f59e0b","#f59e0b","#10b981","#10b981","#10b981","#10b981","#6b7280","#6b7280"];
          return (
            <div key={i} className="flex flex-col items-center gap-0.5 flex-shrink-0">
              <div
                className="w-7 h-1.5 rounded-full transition-all duration-300"
                style={{
                  background: wireColors[i],
                  opacity: active ? 0.85 : 0.2,
                  boxShadow: active ? `0 0 4px ${wireColors[i]}` : "none",
                }}
              />
              <span className="text-[7px] font-mono text-gray-700">W{String(i + 1).padStart(2, "0")}</span>
            </div>
          );
        })}
        <span className="ml-auto text-[9px] font-mono text-gray-600 flex-shrink-0">14 SPSC WIRES</span>
      </div>
    </div>
  );
}
