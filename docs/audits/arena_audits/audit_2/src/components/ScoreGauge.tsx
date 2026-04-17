import { useEffect, useState } from "react";

interface Props {
  score: number;
  maxScore: number;
  label: string;
  sublabel?: string;
  size?: "sm" | "lg";
}

export default function ScoreGauge({ score, maxScore, label, sublabel, size = "lg" }: Props) {
  const [animated, setAnimated] = useState(0);
  const pct = score / maxScore;

  useEffect(() => {
    const timer = setTimeout(() => setAnimated(pct), 100);
    return () => clearTimeout(timer);
  }, [pct]);

  const radius = size === "lg" ? 54 : 36;
  const stroke = size === "lg" ? 7 : 5;
  const cx = size === "lg" ? 64 : 44;
  const cy = size === "lg" ? 64 : 44;
  const viewBox = size === "lg" ? "0 0 128 128" : "0 0 88 88";
  const circumference = 2 * Math.PI * radius;
  const dashOffset = circumference * (1 - animated);

  const color =
    pct >= 0.95
      ? "#22d3ee"
      : pct >= 0.8
      ? "#a3e635"
      : pct >= 0.65
      ? "#facc15"
      : "#f87171";

  const glowColor =
    pct >= 0.95
      ? "drop-shadow(0 0 8px #22d3ee)"
      : pct >= 0.8
      ? "drop-shadow(0 0 8px #a3e635)"
      : pct >= 0.65
      ? "drop-shadow(0 0 8px #facc15)"
      : "drop-shadow(0 0 8px #f87171)";

  const fontSize = size === "lg" ? "text-2xl" : "text-base";
  const labelSize = size === "lg" ? "text-xs" : "text-[10px]";

  return (
    <div className="flex flex-col items-center gap-1">
      <div className="relative" style={{ width: cx * 2, height: cy * 2 }}>
        <svg viewBox={viewBox} width={cx * 2} height={cy * 2}>
          {/* Track */}
          <circle
            cx={cx}
            cy={cy}
            r={radius}
            fill="none"
            stroke="#1e293b"
            strokeWidth={stroke}
          />
          {/* Progress */}
          <circle
            cx={cx}
            cy={cy}
            r={radius}
            fill="none"
            stroke={color}
            strokeWidth={stroke}
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={dashOffset}
            transform={`rotate(-90 ${cx} ${cy})`}
            style={{
              transition: "stroke-dashoffset 1.2s cubic-bezier(0.4,0,0.2,1)",
              filter: glowColor,
            }}
          />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className={`${fontSize} font-black tabular-nums`} style={{ color }}>
            {score}
          </span>
          <span className="text-slate-500 text-[9px] font-mono">/{maxScore}</span>
        </div>
      </div>
      <p className={`${labelSize} font-semibold text-slate-300 text-center leading-tight max-w-[120px]`}>
        {label}
      </p>
      {sublabel && (
        <p className="text-[9px] text-slate-500 text-center max-w-[110px]">{sublabel}</p>
      )}
    </div>
  );
}
