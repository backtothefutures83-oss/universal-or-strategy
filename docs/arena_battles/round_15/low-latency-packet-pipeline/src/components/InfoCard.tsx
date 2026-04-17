interface InfoCardProps {
  icon: string;
  title: string;
  children: React.ReactNode;
  variant?: "info" | "warn" | "danger" | "success";
}

export default function InfoCard({ icon, title, children, variant = "info" }: InfoCardProps) {
  const variants = {
    info: "border-sky-500/25 bg-sky-500/5 text-sky-300",
    warn: "border-amber-500/25 bg-amber-500/5 text-amber-300",
    danger: "border-red-500/25 bg-red-500/5 text-red-300",
    success: "border-emerald-500/25 bg-emerald-500/5 text-emerald-300",
  };

  return (
    <div className={`rounded-xl border p-4 ${variants[variant]}`}>
      <div className="flex items-start gap-3">
        <span className="text-lg flex-shrink-0 mt-0.5">{icon}</span>
        <div>
          <div className="font-bold text-sm mb-1">{title}</div>
          <div className="text-xs leading-relaxed opacity-90">{children}</div>
        </div>
      </div>
    </div>
  );
}
