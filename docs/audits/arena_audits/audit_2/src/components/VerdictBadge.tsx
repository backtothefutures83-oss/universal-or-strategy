import type { Verdict } from "../data/auditData";

interface Props {
  verdict: Verdict;
  size?: "sm" | "md";
}

const CONFIG: Record<Verdict, { label: string; className: string; dot: string }> = {
  RESOLVED: {
    label: "RESOLVED",
    className: "bg-cyan-950 text-cyan-300 border border-cyan-700",
    dot: "bg-cyan-400",
  },
  PARTIAL: {
    label: "PARTIAL",
    className: "bg-yellow-950 text-yellow-300 border border-yellow-700",
    dot: "bg-yellow-400",
  },
  UNRESOLVED: {
    label: "UNRESOLVED",
    className: "bg-orange-950 text-orange-300 border border-orange-700",
    dot: "bg-orange-400",
  },
  CRITICAL: {
    label: "CRITICAL",
    className: "bg-red-950 text-red-300 border border-red-700",
    dot: "bg-red-400",
  },
  INFO: {
    label: "INFO",
    className: "bg-slate-800 text-slate-300 border border-slate-600",
    dot: "bg-slate-400",
  },
};

export default function VerdictBadge({ verdict, size = "md" }: Props) {
  const c = CONFIG[verdict];
  const px = size === "sm" ? "px-1.5 py-0.5 text-[9px]" : "px-2 py-0.5 text-[10px]";
  return (
    <span className={`inline-flex items-center gap-1 rounded font-bold font-mono ${px} ${c.className}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${c.dot}`} />
      {c.label}
    </span>
  );
}
