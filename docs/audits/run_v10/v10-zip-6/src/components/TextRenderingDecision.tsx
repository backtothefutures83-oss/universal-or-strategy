const approaches = [
  {
    name: '@chenglou/pretext',
    verdict: 'REJECTED',
    verdictColor: 'text-red-400 border-red-500/30',
    scores: { layoutReflow: 5, fps60: 3, streamingData: 2, integration: 4 },
    pros: ['Bypasses getBoundingClientRect', 'Pure JS font measurement', 'No canvas context needed'],
    cons: [
      'Still writes to DOM → triggers style recalc on text node mutation',
      'Font engine queries are synchronous on main thread',
      'No batching model for 12-worker streaming numeric updates',
      'Library is experimental; no streaming numeric pipeline',
    ],
    detail: 'pretext solves the measurement problem (avoiding gBCR) but NOT the reflow problem. For streaming numeric data from 12 workers at 60fps, every DOM text-node mutation still triggers style recalculation in the rendering pipeline. The bottleneck is not measurement — it is mutation.',
  },
  {
    name: 'Canvas 2D Direct-Write',
    verdict: 'SELECTED ✓',
    verdictColor: 'text-emerald-400 border-emerald-500/30',
    scores: { layoutReflow: 5, fps60: 5, streamingData: 5, integration: 4 },
    pros: [
      'Zero DOM mutation → zero layout recalc',
      'fillText() is GPU-accelerated on all browsers',
      'Single requestAnimationFrame loop batches all 12 worker updates',
      'Numeric glyph atlas can be pre-rendered for sub-pixel consistency',
    ],
    cons: [
      'No accessibility without ARIA overlay',
      'Text selection requires manual implementation',
    ],
    detail: 'Canvas 2D wins decisively for streaming numeric metrics. A single <canvas> element never triggers layout recalculation regardless of how many numbers change per frame. The rendering loop reads from a SharedArrayBuffer written by workers, and a single rAF pass calls fillText() for all 12 worker panels — total frame budget: ~2ms of the 16.6ms available.',
  },
  {
    name: 'OffscreenCanvas Worker',
    verdict: 'OVER-ENGINEERED',
    verdictColor: 'text-amber-400 border-amber-500/30',
    scores: { layoutReflow: 5, fps60: 4, streamingData: 4, integration: 2 },
    pros: ['Rendering fully off main thread', 'True parallel rendering pipeline'],
    cons: [
      'transferControlToOffscreen() is one-way — no fallback',
      'Worker→main sync adds ~1-3ms jitter at 60fps',
      'Compositor scheduling is less predictable than rAF on main',
      'Overkill for numeric text — GPU fillText is already <1ms',
    ],
    detail: 'OffscreenCanvas is optimal for heavy graphical workloads (3D, complex charts) but introduces unnecessary complexity for numeric metric display. The worker-to-compositor synchronization adds latency variance that defeats the purpose of consistent 60fps updates. Canvas 2D on main thread with a tight rAF loop is faster for this specific use case.',
  },
];

const scoreLabels: Record<string, string> = {
  layoutReflow: 'No Layout Reflow',
  fps60: '60fps Consistency',
  streamingData: 'Streaming Data',
  integration: 'Integration Ease',
};

export default function TextRenderingDecision() {
  return (
    <div className="space-y-4">
      <p className="text-xs text-gray-400 mb-4">
        Requirement: Update numeric metrics from 12 workers at 60fps with zero browser layout recalculation.
      </p>
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {approaches.map((a, i) => (
          <div key={i} className={`bg-gray-900/60 border ${a.verdictColor} rounded-xl p-5 flex flex-col`}>
            <div className="flex items-start justify-between mb-3">
              <h4 className="text-sm font-bold text-white">{a.name}</h4>
              <span className={`text-[10px] font-mono font-bold ${a.verdictColor}`}>{a.verdict}</span>
            </div>

            {/* Score bars */}
            <div className="space-y-2 mb-4">
              {Object.entries(a.scores).map(([key, val]) => (
                <div key={key}>
                  <div className="flex justify-between mb-0.5">
                    <span className="text-[10px] text-gray-500">{scoreLabels[key]}</span>
                    <span className="text-[10px] font-mono text-gray-400">{val}/5</span>
                  </div>
                  <div className="h-1.5 bg-gray-800 rounded-full overflow-hidden">
                    <div
                      className={`h-full rounded-full transition-all duration-500 ${val >= 4 ? 'bg-emerald-500' : val >= 3 ? 'bg-amber-500' : 'bg-red-500'}`}
                      style={{ width: `${(val / 5) * 100}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>

            {/* Pros/Cons */}
            <div className="space-y-1 mb-3 flex-1">
              {a.pros.map((p, j) => (
                <div key={`p${j}`} className="text-[11px] text-gray-400 flex gap-1.5">
                  <span className="text-emerald-400 shrink-0">+</span> {p}
                </div>
              ))}
              {a.cons.map((c, j) => (
                <div key={`c${j}`} className="text-[11px] text-gray-400 flex gap-1.5">
                  <span className="text-red-400 shrink-0">−</span> {c}
                </div>
              ))}
            </div>

            {/* Detail */}
            <div className="text-[11px] text-gray-500 border-t border-gray-800 pt-3 mt-auto leading-relaxed">
              {a.detail}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
