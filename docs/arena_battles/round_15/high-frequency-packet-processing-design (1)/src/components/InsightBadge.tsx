interface InsightBadgeProps {
  icon: string;
  label: string;
  value: string;
  sub?: string;
  variant?: "cyan" | "violet" | "emerald" | "yellow" | "red";
}

const variants = {
  cyan:    "border-cyan-500/30 bg-cyan-900/20 text-cyan-300",
  violet:  "border-violet-500/30 bg-violet-900/20 text-violet-300",
  emerald: "border-emerald-500/30 bg-emerald-900/20 text-emerald-300",
  yellow:  "border-yellow-500/30 bg-yellow-900/20 text-yellow-300",
  red:     "border-red-500/30 bg-red-900/20 text-red-300",
};

export default function InsightBadge({ icon, label, value, sub, variant = "cyan" }: InsightBadgeProps) {
  return (
    <div className={`flex items-start gap-3 rounded-xl border p-4 ${variants[variant]}`}>
      <span className="text-2xl flex-shrink-0 mt-0.5">{icon}</span>
      <div className="min-w-0">
        <p className="text-xs uppercase tracking-wider opacity-60 font-semibold">{label}</p>
        <p className="text-base font-bold font-mono leading-snug mt-0.5">{value}</p>
        {sub && <p className="text-xs opacity-60 mt-0.5 leading-snug">{sub}</p>}
      </div>
    </div>
  );
}
