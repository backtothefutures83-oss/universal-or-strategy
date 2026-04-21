interface SectionHeaderProps {
  number: string;
  title: string;
  subtitle: string;
  accent: "cyan" | "violet" | "emerald";
}

const accents = {
  cyan:    { bar: "bg-cyan-500",    num: "text-cyan-400 border-cyan-500/30 bg-cyan-900/20", title: "text-cyan-100" },
  violet:  { bar: "bg-violet-500",  num: "text-violet-400 border-violet-500/30 bg-violet-900/20", title: "text-violet-100" },
  emerald: { bar: "bg-emerald-500", num: "text-emerald-400 border-emerald-500/30 bg-emerald-900/20", title: "text-emerald-100" },
};

export default function SectionHeader({ number, title, subtitle, accent }: SectionHeaderProps) {
  const a = accents[accent];
  return (
    <div className="flex items-start gap-4">
      {/* Left accent bar */}
      <div className={`w-1 self-stretch rounded-full ${a.bar} flex-shrink-0`} />

      <div className="flex-1 min-w-0">
        {/* Number badge */}
        <div className={`inline-flex items-center justify-center w-8 h-8 rounded-lg border text-sm font-bold font-mono mb-3 ${a.num}`}>
          {number}
        </div>
        <h2 className={`text-2xl lg:text-3xl font-bold tracking-tight ${a.title}`}>
          {title}
        </h2>
        <p className="text-slate-400 mt-2 text-sm leading-relaxed max-w-2xl">
          {subtitle}
        </p>
      </div>
    </div>
  );
}
