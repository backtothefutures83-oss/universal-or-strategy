import { slabBlocks } from "../data/auditData";

const STATUS_STYLE = {
  "pre-allocated": "bg-cyan-950/60 border-cyan-800 text-cyan-300",
  active: "bg-emerald-950/60 border-emerald-800 text-emerald-300",
  free: "bg-slate-800/60 border-slate-700 text-slate-400",
  danger: "bg-red-950/70 border-red-700 text-red-300",
};

const STATUS_DOT = {
  "pre-allocated": "bg-cyan-400",
  active: "bg-emerald-400",
  free: "bg-slate-500",
  danger: "bg-red-500 animate-pulse",
};

const STATUS_LABEL = {
  "pre-allocated": "PRE-ALLOC",
  active: "ACTIVE",
  free: "FREE",
  danger: "⚠ DANGER",
};

export default function SlabPoolMap() {
  return (
    <div className="space-y-4">
      {/* Legend */}
      <div className="flex flex-wrap gap-3 text-[10px] font-mono text-slate-500">
        {(["pre-allocated", "active", "free", "danger"] as const).map((s) => (
          <span key={s} className="flex items-center gap-1.5">
            <span className={`w-2 h-2 rounded-full ${STATUS_DOT[s]}`} />
            {STATUS_LABEL[s]}
          </span>
        ))}
      </div>

      {/* Blocks */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {slabBlocks.map((block) => (
          <div
            key={block.id}
            className={`rounded-xl border p-4 ${STATUS_STYLE[block.status]}`}
          >
            <div className="flex items-start justify-between mb-2">
              <div>
                <p className="text-xs font-bold">{block.name}</p>
                <p className="text-[10px] text-slate-500 font-mono mt-0.5">{block.id}</p>
              </div>
              <span className={`flex items-center gap-1 text-[9px] font-mono font-bold rounded px-1.5 py-0.5 border ${STATUS_STYLE[block.status]}`}>
                <span className={`w-1.5 h-1.5 rounded-full ${STATUS_DOT[block.status]}`} />
                {STATUS_LABEL[block.status]}
              </span>
            </div>

            <div className="grid grid-cols-2 gap-2 mb-3">
              <div className="bg-slate-900/40 rounded-lg p-2">
                <p className="text-[9px] text-slate-600 font-mono uppercase">Slot Size</p>
                <p className="text-xs font-mono font-bold text-slate-200 mt-0.5">{block.size}</p>
              </div>
              <div className="bg-slate-900/40 rounded-lg p-2">
                <p className="text-[9px] text-slate-600 font-mono uppercase">Slots</p>
                <p className="text-xs font-mono font-bold text-slate-200 mt-0.5">
                  {block.count > 0 ? block.count.toLocaleString() : "∞ (heap)"}
                </p>
              </div>
            </div>

            {/* Mini slot visualization */}
            {block.count > 0 && block.status !== "danger" && (
              <div className="flex flex-wrap gap-0.5 mb-3">
                {Array.from({ length: Math.min(32, 32) }).map((_, i) => (
                  <div
                    key={i}
                    className={`w-1.5 h-1.5 rounded-[2px] ${
                      block.status === "active" && i < 20 ? "bg-emerald-500" : "opacity-30 " +
                      (block.status === "pre-allocated" ? "bg-cyan-600" : "bg-slate-600")
                    }`}
                  />
                ))}
                <span className="text-[8px] text-slate-600 font-mono self-end">…</span>
              </div>
            )}

            <p className="text-[10px] text-slate-500 leading-relaxed">{block.notes}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
