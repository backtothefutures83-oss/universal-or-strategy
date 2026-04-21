interface DiagramBoxProps {
  title: string;
  children: React.ReactNode;
}

export default function DiagramBox({ title, children }: DiagramBoxProps) {
  return (
    <div className="rounded-xl border border-gray-700/50 bg-gray-900/60 overflow-hidden">
      <div className="px-4 py-2 bg-gray-800/60 border-b border-gray-700/50 text-xs text-gray-400 font-bold tracking-widest uppercase">
        {title}
      </div>
      <div className="p-4 font-mono text-xs text-gray-300 overflow-x-auto">
        {children}
      </div>
    </div>
  );
}
