import { useEffect, useRef, useState } from "react";

interface Metric {
  label: string;
  unit: string;
  base: number;
  delta: number;
  color: string;
  good: "high" | "low";
  target: string;
  targetLabel: string;
  format?: (v: number) => string;
}

const METRICS: Metric[] = [
  {
    label: "Messages / Second",
    unit: "M msg/s",
    base: 18.2,
    delta: 3.4,
    color: "#34d399",
    good: "high",
    target: "≥ 10M/s",
    targetLabel: "design target",
    format: (v) => v.toFixed(1),
  },
  {
    label: "IPC p99 Latency",
    unit: "µs",
    base: 5.8,
    delta: 2.1,
    color: "#38bdf8",
    good: "low",
    target: "< 10µs",
    targetLabel: "hard gate",
    format: (v) => v.toFixed(1),
  },
  {
    label: "Slab Alloc Time",
    unit: "ns",
    base: 38,
    delta: 12,
    color: "#a78bfa",
    good: "low",
    target: "< 100ns",
    targetLabel: "per call",
    format: (v) => Math.round(v).toString(),
  },
  {
    label: "Kernel Calls / Send",
    unit: "syscalls",
    base: 0,
    delta: 0,
    color: "#fb923c",
    good: "low",
    target: "= 0",
    targetLabel: "zero syscall path",
    format: () => "0",
  },
  {
    label: "GC Pressure",
    unit: "alloc/msg",
    base: 0,
    delta: 0,
    color: "#f472b6",
    good: "low",
    target: "= 0",
    targetLabel: "zero heap",
    format: () => "0",
  },
  {
    label: "Lock Contention",
    unit: "events",
    base: 0,
    delta: 0,
    color: "#facc15",
    good: "low",
    target: "= 0",
    targetLabel: "BANNED",
    format: () => "0",
  },
];

export default function MetricsPanel() {
  const [values, setValues] = useState<number[]>(METRICS.map((m) => m.base));
  const tickRef = useRef<ReturnType<typeof setInterval>>(null!);

  useEffect(() => {
    tickRef.current = setInterval(() => {
      setValues(
        METRICS.map((m) =>
          Math.max(0, m.base + (Math.random() - 0.5) * m.delta)
        )
      );
    }, 1200);
    return () => clearInterval(tickRef.current);
  }, []);

  return (
    <section className="max-w-5xl mx-auto px-4 py-8">
      <div className="text-center mb-8">
        <h2 className="text-3xl font-black text-white mb-2">
          Runtime Metrics · Live Simulation
        </h2>
        <p className="text-slate-500 text-sm">
          Simulated telemetry from a 12-core Antigravity Nexus OS deployment
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {METRICS.map((m, i) => {
          const v = values[i];
          const display = m.format ? m.format(v) : v.toFixed(1);
          const isGood =
            m.good === "high" ? v >= m.base * 0.5 : v <= (m.base || 1) * 2.5 || m.base === 0;

          return (
            <div
              key={m.label}
              className="rounded-2xl border border-slate-700 bg-slate-900/80 p-5 backdrop-blur relative overflow-hidden"
            >
              {/* Glow accent */}
              <div
                className="absolute top-0 right-0 w-24 h-24 rounded-full blur-2xl opacity-10 pointer-events-none"
                style={{ backgroundColor: m.color }}
              />

              <div className="text-xs text-slate-500 mb-2 font-semibold uppercase tracking-wider">
                {m.label}
              </div>

              <div className="flex items-end gap-2 mb-3">
                <span
                  className="text-4xl font-black tabular-nums"
                  style={{ color: m.color, transition: "color 0.5s ease" }}
                >
                  {display}
                </span>
                <span className="text-slate-500 text-sm mb-1">{m.unit}</span>
              </div>

              {/* Bar */}
              <div className="h-1.5 rounded-full bg-slate-800 overflow-hidden mb-3">
                <div
                  className="h-full rounded-full transition-all duration-700"
                  style={{
                    width: m.base === 0 ? "100%" : `${Math.min(100, (v / (m.base * 2)) * 100)}%`,
                    backgroundColor: m.color,
                  }}
                />
              </div>

              <div className="flex items-center justify-between">
                <span className="text-[10px] font-mono text-slate-600">
                  target: <span style={{ color: m.color }}>{m.target}</span>
                </span>
                <span
                  className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                    isGood
                      ? "bg-emerald-500/10 text-emerald-400"
                      : "bg-red-500/10 text-red-400"
                  }`}
                >
                  {isGood ? "✓ NOMINAL" : "⚠ CHECK"}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}
