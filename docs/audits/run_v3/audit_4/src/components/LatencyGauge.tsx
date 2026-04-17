import { useEffect, useRef, useState } from "react";

const GAUGE_SIZE = 200;
const STROKE = 14;
const R = (GAUGE_SIZE / 2) - STROKE - 4;
const CIRCUMFERENCE = 2 * Math.PI * R;
// Gauge arc: 240 degrees (from -210deg to +30deg)
const ARC_FRAC = 240 / 360;
const ARC_LEN = CIRCUMFERENCE * ARC_FRAC;

function getColor(val: number): string {
  if (val < 5) return "#34d399"; // green
  if (val < 8) return "#facc15"; // yellow
  return "#f87171";              // red
}

export default function LatencyGauge() {
  const [value, setValue] = useState(4.2); // µs
  const [history, setHistory] = useState<number[]>([4.2, 3.8, 5.1, 4.6, 3.9, 6.2, 4.1, 3.5]);
  const tickRef = useRef<ReturnType<typeof setInterval>>(null!);

  useEffect(() => {
    tickRef.current = setInterval(() => {
      const next = +(2.5 + Math.random() * 6.5).toFixed(1);
      setValue(next);
      setHistory((h) => [...h.slice(-23), next]);
    }, 800);
    return () => clearInterval(tickRef.current);
  }, []);

  // Fraction of max (10µs)
  const frac = Math.min(value / 10, 1);
  const arcDraw = ARC_LEN * frac;
  const dashOffset = -(CIRCUMFERENCE * (1 - ARC_FRAC) / 2);
  const color = getColor(value);

  const cx = GAUGE_SIZE / 2;
  const cy = GAUGE_SIZE / 2;

  const maxH = Math.max(...history);

  return (
    <div className="rounded-2xl border border-slate-700 bg-slate-900/80 p-6 backdrop-blur flex flex-col items-center gap-4">
      <div className="text-xs font-semibold text-slate-500 uppercase tracking-widest">
        IPC Latency Monitor · 10µs Hard Gate
      </div>

      {/* Radial gauge */}
      <div className="relative" style={{ width: GAUGE_SIZE, height: GAUGE_SIZE }}>
        <svg width={GAUGE_SIZE} height={GAUGE_SIZE} viewBox={`0 0 ${GAUGE_SIZE} ${GAUGE_SIZE}`}>
          {/* Background track */}
          <circle
            cx={cx} cy={cy} r={R}
            fill="none"
            stroke="#1e293b"
            strokeWidth={STROKE}
            strokeLinecap="round"
            strokeDasharray={`${ARC_LEN} ${CIRCUMFERENCE - ARC_LEN}`}
            strokeDashoffset={dashOffset}
            style={{ transform: "rotate(-120deg)", transformOrigin: "center" }}
          />
          {/* Value arc */}
          <circle
            cx={cx} cy={cy} r={R}
            fill="none"
            stroke={color}
            strokeWidth={STROKE}
            strokeLinecap="round"
            strokeDasharray={`${arcDraw} ${CIRCUMFERENCE - arcDraw}`}
            strokeDashoffset={dashOffset}
            style={{
              transform: "rotate(-120deg)",
              transformOrigin: "center",
              transition: "stroke-dasharray 0.5s ease, stroke 0.5s ease",
              filter: `drop-shadow(0 0 8px ${color})`,
            }}
          />
          {/* Tick marks */}
          {[0, 2, 4, 6, 8, 10].map((tick) => {
            const angleDeg = -120 + (tick / 10) * 240;
            const angleRad = (angleDeg * Math.PI) / 180;
            const x1 = cx + (R - 10) * Math.cos(angleRad);
            const y1 = cy + (R - 10) * Math.sin(angleRad);
            const x2 = cx + (R + 2) * Math.cos(angleRad);
            const y2 = cy + (R + 2) * Math.sin(angleRad);
            return (
              <g key={tick}>
                <line x1={x1} y1={y1} x2={x2} y2={y2} stroke="#475569" strokeWidth={1.5} />
                <text
                  x={cx + (R - 22) * Math.cos(angleRad)}
                  y={cy + (R - 22) * Math.sin(angleRad)}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  fill="#475569"
                  fontSize={7}
                  fontFamily="monospace"
                >
                  {tick}
                </text>
              </g>
            );
          })}
          {/* 10µs gate line */}
          {(() => {
            const angleDeg = -120 + 240;
            const angleRad = (angleDeg * Math.PI) / 180;
            const x1 = cx + (R - 18) * Math.cos(angleRad);
            const y1 = cy + (R - 18) * Math.sin(angleRad);
            const x2 = cx + (R + 6) * Math.cos(angleRad);
            const y2 = cy + (R + 6) * Math.sin(angleRad);
            return <line x1={x1} y1={y1} x2={x2} y2={y2} stroke="#f87171" strokeWidth={2} />;
          })()}
        </svg>

        {/* Center readout */}
        <div className="absolute inset-0 flex flex-col items-center justify-center mt-4">
          <span
            className="text-4xl font-black tabular-nums"
            style={{ color, transition: "color 0.5s ease" }}
          >
            {value.toFixed(1)}
          </span>
          <span className="text-xs text-slate-500">µs · p99</span>
          <span
            className="mt-1 text-xs font-semibold"
            style={{ color: value < 10 ? "#34d399" : "#f87171" }}
          >
            {value < 10 ? "✓ GATE CLEAR" : "✗ BREACH"}
          </span>
        </div>
      </div>

      {/* Micro histogram */}
      <div className="w-full">
        <div className="text-xs text-slate-600 mb-1 font-mono">History (800ms cadence)</div>
        <div className="flex items-end gap-0.5 h-10">
          {history.map((h, i) => (
            <div
              key={i}
              className="flex-1 rounded-sm transition-all duration-500"
              style={{
                height: `${(h / Math.max(maxH, 10)) * 100}%`,
                backgroundColor: getColor(h),
                opacity: 0.4 + (i / history.length) * 0.6,
              }}
            />
          ))}
        </div>
        <div className="flex justify-between text-[10px] text-slate-600 mt-1 font-mono">
          <span>oldest</span>
          <span>now</span>
        </div>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-3 gap-2 w-full">
        {[
          { label: "p50", val: "3.8µs" },
          { label: "p99", val: `${value.toFixed(1)}µs` },
          { label: "p999", val: "8.9µs" },
        ].map((s) => (
          <div key={s.label} className="rounded-lg bg-slate-800 px-2 py-1.5 text-center">
            <div className="text-xs text-slate-500">{s.label}</div>
            <div className="text-sm font-bold text-white font-mono">{s.val}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
