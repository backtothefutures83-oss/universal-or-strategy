interface SectionBadgeProps {
  number: string;
  title: string;
  subtitle: string;
  color: "cyan" | "violet" | "amber";
  latency: string;
  latencyNote: string;
}

export default function SectionBadge({ number, title, subtitle, color, latency, latencyNote }: SectionBadgeProps) {
  const colors = {
    cyan: {
      border: "border-cyan-500/40",
      bg: "bg-cyan-500/8",
      glow: "bg-cyan-500/20",
      num: "text-cyan-400 border-cyan-500/50 bg-cyan-500/10",
      title: "text-cyan-300",
      badge: "border-cyan-500/30 bg-cyan-500/10 text-cyan-300",
    },
    violet: {
      border: "border-violet-500/40",
      bg: "bg-violet-500/8",
      glow: "bg-violet-500/20",
      num: "text-violet-400 border-violet-500/50 bg-violet-500/10",
      title: "text-violet-300",
      badge: "border-violet-500/30 bg-violet-500/10 text-violet-300",
    },
    amber: {
      border: "border-amber-500/40",
      bg: "bg-amber-500/8",
      glow: "bg-amber-500/20",
      num: "text-amber-400 border-amber-500/50 bg-amber-500/10",
      title: "text-amber-300",
      badge: "border-amber-500/30 bg-amber-500/10 text-amber-300",
    },
  };

  const c = colors[color];

  return (
    <div className={`relative rounded-2xl border ${c.border} p-6 mb-8 overflow-hidden`} style={{ background: `rgba(var(--${color}-rgb, 6 182 212) / 0.03)` }}>
      <div className={`absolute top-0 right-0 w-64 h-32 ${c.glow} blur-3xl opacity-20 rounded-full`} />
      <div className="relative flex flex-col sm:flex-row sm:items-start gap-4">
        <div className={`flex-shrink-0 w-14 h-14 rounded-xl border-2 ${c.num} flex items-center justify-center text-2xl font-black`}>
          {number}
        </div>
        <div className="flex-1 min-w-0">
          <h2 className={`text-2xl font-black tracking-tight ${c.title}`}>{title}</h2>
          <p className="text-gray-400 text-sm mt-1 leading-relaxed">{subtitle}</p>
        </div>
        <div className="flex-shrink-0 flex flex-col items-end gap-1">
          <div className={`px-3 py-1.5 rounded-lg border text-xs font-bold tracking-wider ${c.badge}`}>
            +{latency} ns
          </div>
          <span className="text-[10px] text-gray-600 text-right">{latencyNote}</span>
        </div>
      </div>
    </div>
  );
}
