import { useState } from 'react';
import { v10Design } from '../data/breakthroughs';

export default function TextRenderingDecision() {
  const { textRendering } = v10Design;
  const [expanded, setExpanded] = useState<number | null>(null);

  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-2xl p-6">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-fuchsia-500/20 flex items-center justify-center text-fuchsia-400 text-sm">
          🖥️
        </div>
        <div>
          <h2 className="text-lg font-semibold text-gray-200">
            Zero-Reflow Text Rendering
          </h2>
          <p className="text-xs text-gray-500">
            60fps numeric monitoring without layout recalc
          </p>
        </div>
      </div>

      {/* Candidates comparison */}
      <div className="space-y-2 mb-4">
        {textRendering.alternatives.map((alt, i) => {
          const isWinner = alt.name === textRendering.winner;
          return (
            <div
              key={i}
              className={`rounded-xl p-3 cursor-pointer transition-all duration-200 ${
                isWinner
                  ? 'bg-fuchsia-500/10 border border-fuchsia-500/30 ring-1 ring-fuchsia-500/20'
                  : 'bg-gray-800/40 border border-gray-700/30 hover:border-gray-600/40'
              }`}
              onClick={() => setExpanded(expanded === i ? null : i)}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  {isWinner && (
                    <span className="text-[9px] px-1.5 py-0.5 rounded bg-fuchsia-500/20 text-fuchsia-300 font-bold">
                      WINNER
                    </span>
                  )}
                  <span
                    className={`text-sm font-medium ${
                      isWinner ? 'text-fuchsia-200' : 'text-gray-300'
                    }`}
                  >
                    {alt.name}
                  </span>
                </div>
                <span className="text-gray-500 text-xs">
                  {expanded === i ? '−' : '+'}
                </span>
              </div>
              {expanded === i && (
                <div className="mt-3 space-y-2">
                  <div>
                    <span className="text-[10px] text-emerald-400 font-semibold">
                      PROS:{' '}
                    </span>
                    <span className="text-[11px] text-gray-400">
                      {alt.pros}
                    </span>
                  </div>
                  <div>
                    <span className="text-[10px] text-red-400 font-semibold">
                      CONS:{' '}
                    </span>
                    <span className="text-[11px] text-gray-400">
                      {alt.cons}
                    </span>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Reasoning */}
      <div className="bg-fuchsia-500/5 border border-fuchsia-500/20 rounded-xl p-4">
        <div className="text-[10px] text-fuchsia-400 uppercase tracking-wider mb-2 font-semibold">
          Decision Rationale
        </div>
        <p className="text-xs text-gray-300 leading-relaxed">
          {textRendering.reasoning}
        </p>
      </div>
    </div>
  );
}
