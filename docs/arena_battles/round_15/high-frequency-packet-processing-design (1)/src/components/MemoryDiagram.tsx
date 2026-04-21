import type React from "react";

interface MemoryRegion {
  label: string;
  bytes: number;
  color: string;
  textColor?: string;
  sublabel?: string;
  striped?: boolean;
}

interface MemoryDiagramProps {
  title: string;
  regions: MemoryRegion[];
  totalBytes: number;
  cacheLineBytes?: number;
}

export default function MemoryDiagram({ title, regions, totalBytes, cacheLineBytes = 64 }: MemoryDiagramProps) {
  const numCacheLines = Math.ceil(totalBytes / cacheLineBytes);

  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-800/50 p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-semibold text-slate-300 uppercase tracking-wider">{title}</h4>
        <div className="flex items-center gap-3 text-xs font-mono text-slate-500">
          <span>{totalBytes} B total</span>
          <span className="text-slate-600">·</span>
          <span>{numCacheLines} cache lines</span>
        </div>
      </div>

      {/* Memory strip */}
      <div className="space-y-1">
        <div className="flex items-stretch h-14 rounded-lg overflow-hidden border border-slate-700/40">
          {regions.map((region, i) => {
            const widthPct = (region.bytes / totalBytes) * 100;
            return (
              <div
                key={i}
                className={`relative flex flex-col items-center justify-center overflow-hidden transition-all
                  ${region.color} ${region.striped ? "bg-stripes" : ""}`}
                style={{ width: `${widthPct}%`, minWidth: widthPct < 5 ? "2px" : undefined }}
                title={`${region.label}: ${region.bytes} bytes`}
              >
                {widthPct > 6 && (
                  <>
                    <span className={`text-xs font-bold font-mono leading-tight ${region.textColor ?? "text-white"}`}>
                      {region.label}
                    </span>
                    {region.sublabel && (
                      <span className={`text-[10px] font-mono leading-tight opacity-80 ${region.textColor ?? "text-white"}`}>
                        {region.sublabel}
                      </span>
                    )}
                  </>
                )}
                {/* Stripe overlay for padding */}
                {region.striped && (
                  <div className="absolute inset-0 opacity-20"
                    style={{
                      backgroundImage: "repeating-linear-gradient(45deg, currentColor, currentColor 2px, transparent 2px, transparent 8px)",
                    }}
                  />
                )}
              </div>
            );
          })}
        </div>

        {/* Byte offset ruler */}
        <div className="relative h-5">
          {regions.reduce<{ offset: number; nodes: React.ReactNode[] }>(
            (acc, region, i) => {
              const pct = (acc.offset / totalBytes) * 100;
              acc.nodes.push(
                <div key={i} className="absolute top-0 flex flex-col items-start" style={{ left: `${pct}%` }}>
                  <div className="w-px h-2 bg-slate-600" />
                  <span className="text-[9px] font-mono text-slate-500 mt-0.5 -translate-x-1/2">
                    {acc.offset}
                  </span>
                </div>
              );
              acc.offset += region.bytes;
              return acc;
            },
            { offset: 0, nodes: [] }
          ).nodes}
          {/* End marker */}
          <div className="absolute top-0 right-0 flex flex-col items-end">
            <div className="w-px h-2 bg-slate-600 ml-auto" />
            <span className="text-[9px] font-mono text-slate-500 mt-0.5">{totalBytes}</span>
          </div>
        </div>
      </div>

      {/* Cache line grid visualization */}
      <div className="space-y-2">
        <p className="text-xs text-slate-500 uppercase tracking-wider">Cache lines ({cacheLineBytes}B each)</p>
        <div className="flex flex-wrap gap-1.5">
          {Array.from({ length: numCacheLines }).map((_, i) => {
            const lineStart = i * cacheLineBytes;
            const lineEnd = lineStart + cacheLineBytes;

            // Find which region(s) this line falls in
            let offset = 0;
            let dominantRegion: MemoryRegion | null = null;
            let isMixed = false;
            let lastRegionForLine: MemoryRegion | null = null;

            for (const region of regions) {
              const regionEnd = offset + region.bytes;
              if (lineStart < regionEnd && lineEnd > offset) {
                if (dominantRegion && dominantRegion !== region) {
                  isMixed = true;
                }
                dominantRegion = region;
                lastRegionForLine = region;
              }
              offset += region.bytes;
            }
            void lastRegionForLine;

            return (
              <div
                key={i}
                className={`w-10 h-10 rounded-md flex flex-col items-center justify-center border text-[9px] font-mono
                  ${isMixed
                    ? "border-yellow-600/60 bg-yellow-900/30 text-yellow-400"
                    : `${dominantRegion?.color ?? "bg-slate-700"} border-transparent text-white/80`
                  }`}
                title={`Cache line ${i}: bytes ${lineStart}–${lineEnd - 1}`}
              >
                <span className="leading-none opacity-70">L{i}</span>
                <span className="leading-none opacity-50 text-[8px]">{lineStart}</span>
              </div>
            );
          })}
        </div>
        {regions.some((_, i) => {
          let offset = 0;
          for (let j = 0; j < i; j++) offset += regions[j].bytes;
          return offset % cacheLineBytes !== 0;
        }) && (
          <div className="flex items-center gap-1.5 text-xs text-yellow-500">
            <div className="w-3 h-3 rounded-sm border border-yellow-600/60 bg-yellow-900/30" />
            <span>Mixed cache line — check alignment</span>
          </div>
        )}
      </div>

      {/* Legend */}
      <div className="flex flex-wrap gap-3">
        {regions.map((region, i) => (
          <div key={i} className="flex items-center gap-1.5">
            <div className={`w-3 h-3 rounded-sm ${region.color}`} />
            <span className="text-xs text-slate-400 font-mono">
              {region.label} <span className="text-slate-600">({region.bytes}B)</span>
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
