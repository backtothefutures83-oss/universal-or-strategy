interface Props {
  questionNumber: string;
  question: string;
  verdict: "YES" | "NO" | "PARTIAL";
  answer: React.ReactNode;
}

export default function AnswerBlock({ questionNumber, question, verdict, answer }: Props) {
  const verdictConfig = {
    YES: { bg: "bg-cyan-950/40", border: "border-cyan-700", text: "text-cyan-300", badge: "bg-cyan-900 border-cyan-700 text-cyan-200" },
    NO: { bg: "bg-red-950/40", border: "border-red-700", text: "text-red-300", badge: "bg-red-900 border-red-700 text-red-200" },
    PARTIAL: { bg: "bg-yellow-950/40", border: "border-yellow-700/60", text: "text-yellow-300", badge: "bg-yellow-900 border-yellow-700 text-yellow-200" },
  };
  const c = verdictConfig[verdict];

  return (
    <div className={`rounded-2xl border ${c.border} ${c.bg} p-5 space-y-3`}>
      <div className="flex items-start gap-3">
        <span className={`text-[10px] font-mono font-black rounded px-2 py-1 border flex-shrink-0 ${c.badge}`}>
          Q{questionNumber}
        </span>
        <p className="text-sm font-semibold text-slate-200 leading-snug">{question}</p>
      </div>
      <div className="flex items-center gap-2">
        <span className={`text-lg font-black ${c.text}`}>▶ {verdict}</span>
      </div>
      <div className="text-sm text-slate-300 leading-relaxed space-y-2 border-t border-slate-800 pt-3">
        {answer}
      </div>
    </div>
  );
}
