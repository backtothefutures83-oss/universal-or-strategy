import { v10Design } from '../data/breakthroughs';

export default function V9Verdict() {
  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-2xl p-6">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-amber-500/20 flex items-center justify-center text-amber-400 text-sm">
          ⚖️
        </div>
        <div>
          <h2 className="text-lg font-semibold text-gray-200">
            V9 Design Decision Resolved
          </h2>
          <p className="text-xs text-gray-500">
            Foundation selection for V10
          </p>
        </div>
      </div>

      {/* Comparison cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-5">
        <div className="border border-gray-700/40 rounded-xl p-4 bg-gray-800/30 relative overflow-hidden">
          <div className="absolute top-2 right-2 text-[10px] font-mono text-gray-600">
            Option A
          </div>
          <div className="text-sm font-medium text-violet-300 mb-1">
            FPGA-Parity Bitwise Pass
          </div>
          <div className="text-2xl font-mono font-bold text-violet-400">
            243ns
          </div>
          <div className="text-[10px] text-gray-500 mt-1">
            Raw speed winner · Validation layer
          </div>
          <div className="mt-3 text-[10px] px-2 py-1 rounded bg-violet-500/10 text-violet-300 inline-block">
            → Layered as validation gate
          </div>
        </div>
        <div className="border border-emerald-500/30 rounded-xl p-4 bg-emerald-500/5 relative overflow-hidden ring-1 ring-emerald-500/20">
          <div className="absolute top-2 right-2 text-[10px] font-mono text-emerald-500 font-bold">
            ✓ BASE
          </div>
          <div className="text-sm font-medium text-emerald-300 mb-1">
            Memory-Mapped Uint32 Arena
          </div>
          <div className="text-2xl font-mono font-bold text-emerald-400">
            250ns
          </div>
          <div className="text-[10px] text-gray-500 mt-1">
            Parallel-scaling champion · Memory substrate
          </div>
          <div className="mt-3 text-[10px] px-2 py-1 rounded bg-emerald-500/10 text-emerald-300 inline-block">
            → Selected as foundation
          </div>
        </div>
      </div>

      {/* Reasoning */}
      <div className="space-y-3">
        {v10Design.v9Verdict.reasoning.map((r, i) => {
          const [title, ...rest] = r.split(': ');
          return (
            <div key={i} className="flex gap-3">
              <div className="w-5 h-5 rounded-full bg-gray-800 border border-gray-600 flex items-center justify-center text-[10px] text-gray-400 mt-0.5 shrink-0">
                {i + 1}
              </div>
              <div>
                <span className="text-xs font-semibold text-gray-300">
                  {title}:
                </span>
                <span className="text-xs text-gray-400 ml-1">
                  {rest.join(': ')}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
