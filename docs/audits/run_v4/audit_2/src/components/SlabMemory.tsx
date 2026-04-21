import { useState, useEffect } from "react";

const SLAB_DEFS = [
  { id: "SLAB_ING",  label: "Ingress Slab",   entries: 256, entryBytes: 64,  color: "#06b6d4", users: ["ING-0", "ING-1"], description: "NIC DMA target. Cache-aligned 64B slots = 1 cache-line per frame. Pre-faulted at boot." },
  { id: "SLAB_RTR",  label: "Router Slab",    entries: 512, entryBytes: 32,  color: "#8b5cf6", users: ["RTR-0", "RTR-1"], description: "Routing metadata only. No payload. 32B = half cache-line. Lookup table pinned in L1." },
  { id: "SLAB_TRF",  label: "Transform Slab", entries: 128, entryBytes: 128, color: "#f59e0b", users: ["TRF-0", "TRF-1"], description: "In-place AVX-512 work buffer. 128B = 2 cache-lines. Payload never copied — ptr passed downstream." },
  { id: "SLAB_ACT",  label: "Actor Slab",     entries: 64,  entryBytes: 256, color: "#10b981", users: ["ACT-0", "ACT-1"], description: "State machine contexts. 256B = 4 cache-lines. Entire working set fits in 16KB L1D." },
  { id: "SLAB_EGR",  label: "Egress Slab",    entries: 256, entryBytes: 64,  color: "#3b82f6", users: ["EGR-0", "EGR-1"], description: "TX staging buffer. 64B aligned. DPDK burst-TX reads directly from slab — no copy to kernel." },
  { id: "SLAB_MIR",  label: "Mirror Slab",    entries: 1024,entryBytes: 64,  color: "#6b7280", users: ["MIR-0", "MIR-1"], description: "Large telemetry ring. mmap'd to shared-memory region. Observers read without writer coordination." },
];

function useTicker(intervalMs: number) {
  const [tick, setTick] = useState(0);
  useEffect(() => {
    const id = setInterval(() => setTick(t => t + 1), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return tick;
}

export default function SlabMemory() {
  const tick = useTicker(800);
  const [selected, setSelected] = useState<string | null>(null);

  return (
    <div className="bg-gray-950 border border-emerald-900/40 rounded-xl overflow-hidden">
      <div className="px-5 py-3 border-b border-emerald-900/30 bg-black/40">
        <span className="text-emerald-400 font-mono text-xs font-bold tracking-widest uppercase">
          ◈ Slab Memory Architecture — Fixed Pools · Zero Heap · Zero Copy
        </span>
      </div>

      <div className="p-5 grid grid-cols-1 gap-4">
        {SLAB_DEFS.map((slab) => {
          const totalBytes = slab.entries * slab.entryBytes;
          const isSelected = selected === slab.id;
          // Simulate utilization fluctuation
          const utilPct = Math.min(95, 30 + ((tick * slab.entries * 7 + slab.entryBytes * 3) % 60));

          return (
            <div
              key={slab.id}
              className="rounded-lg border overflow-hidden cursor-pointer transition-all duration-200"
              style={{
                borderColor: isSelected ? slab.color : `${slab.color}30`,
                background: isSelected ? `${slab.color}08` : "transparent",
              }}
              onClick={() => setSelected(isSelected ? null : slab.id)}
            >
              <div className="flex items-center gap-4 px-4 py-3">
                {/* Icon */}
                <div
                  className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0 text-sm font-mono font-bold"
                  style={{ background: `${slab.color}20`, color: slab.color, border: `1px solid ${slab.color}40` }}
                >
                  SB
                </div>

                {/* Name + meta */}
                <div className="flex-shrink-0 w-36">
                  <p className="text-[10px] font-mono font-bold" style={{ color: slab.color }}>{slab.id}</p>
                  <p className="text-[9px] font-mono text-gray-500">{slab.label}</p>
                  <p className="text-[9px] font-mono text-gray-600">{slab.users.join(" · ")}</p>
                </div>

                {/* Stats */}
                <div className="flex gap-4 text-[10px] font-mono flex-shrink-0">
                  <div>
                    <p className="text-gray-600 text-[8px] uppercase">Entries</p>
                    <p style={{ color: slab.color }}>{slab.entries}</p>
                  </div>
                  <div>
                    <p className="text-gray-600 text-[8px] uppercase">Entry Size</p>
                    <p className="text-gray-300">{slab.entryBytes}B</p>
                  </div>
                  <div>
                    <p className="text-gray-600 text-[8px] uppercase">Total</p>
                    <p className="text-gray-300">{(totalBytes / 1024).toFixed(0)}KB</p>
                  </div>
                  <div>
                    <p className="text-gray-600 text-[8px] uppercase">Cache Fit</p>
                    <p className="text-gray-300">
                      {totalBytes <= 32 * 1024 ? "✓ L1" : totalBytes <= 256 * 1024 ? "✓ L2" : "✓ L3"}
                    </p>
                  </div>
                </div>

                {/* Utilization bar */}
                <div className="flex-1">
                  <div className="flex justify-between text-[8px] font-mono text-gray-600 mb-1">
                    <span>UTIL</span><span>{utilPct}%</span>
                  </div>
                  <div className="h-2 bg-gray-900 rounded-full overflow-hidden border border-gray-800">
                    <div
                      className="h-full rounded-full transition-all duration-700"
                      style={{ width: `${utilPct}%`, background: slab.color, opacity: 0.8 }}
                    />
                  </div>
                </div>

                {/* Slot visualizer */}
                <div className="flex gap-0.5 flex-wrap w-32 flex-shrink-0">
                  {Array.from({ length: Math.min(32, slab.entries / 8) }).map((_, i) => {
                    const isUsed = (tick + i * 3) % 10 < Math.floor(utilPct / 10);
                    return (
                      <div
                        key={i}
                        className="w-3 h-3 rounded-sm transition-all duration-500"
                        style={{ background: isUsed ? slab.color : "#1f2937", opacity: isUsed ? 0.85 : 1 }}
                      />
                    );
                  })}
                </div>

                <span className="text-gray-600 text-xs">{isSelected ? "▲" : "▼"}</span>
              </div>

              {/* Expanded detail */}
              {isSelected && (
                <div
                  className="px-4 pb-4 pt-0 border-t text-[10px] font-mono"
                  style={{ borderColor: `${slab.color}20` }}
                >
                  <p className="text-gray-400 mt-3 leading-relaxed">{slab.description}</p>
                  <div className="mt-3 grid grid-cols-4 gap-2">
                    <SlabStat label="Allocation" value="O(1) CAS" color={slab.color} />
                    <SlabStat label="Free" value="O(1) store" color={slab.color} />
                    <SlabStat label="Alignment" value="64B (cacheline)" color={slab.color} />
                    <SlabStat label="NUMA" value="Node-local mmap" color={slab.color} />
                  </div>
                  <div className="mt-3 rounded border border-white/5 bg-black/40 p-3 font-mono text-[9px] text-gray-400 leading-loose">
                    <span className="text-gray-600">// Boot-time init (no runtime malloc)</span><br />
                    <span style={{ color: slab.color }}>{slab.id}</span>
                    <span className="text-gray-300"> = mmap(NULL, </span>
                    <span className="text-amber-300">{slab.entries}×{slab.entryBytes}</span>
                    <span className="text-gray-300">, MAP_HUGETLB | MAP_LOCKED, ...);</span><br />
                    <span className="text-gray-300">mlock(</span><span style={{ color: slab.color }}>{slab.id}</span>
                    <span className="text-gray-300">, size);  </span>
                    <span className="text-gray-600">// pin to RAM, no page faults</span>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="mx-5 mb-5 rounded-lg border border-white/5 bg-black/30 p-3 font-mono text-[10px] text-gray-500 flex items-center gap-3">
        <span className="text-emerald-400 font-bold">∑ TOTAL STATIC FOOTPRINT:</span>
        <span className="text-gray-300">
          {SLAB_DEFS.reduce((a, s) => a + s.entries * s.entryBytes, 0) / 1024} KB
        </span>
        <span className="mx-2 text-gray-700">|</span>
        <span className="text-emerald-400">HEAP ALLOCATIONS AFTER BOOT:</span>
        <span className="text-emerald-300 font-bold">0</span>
        <span className="mx-2 text-gray-700">|</span>
        <span className="text-emerald-400">COPY OPERATIONS ON HOT PATH:</span>
        <span className="text-emerald-300 font-bold">0</span>
      </div>
    </div>
  );
}

function SlabStat({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div className="bg-black/40 rounded px-2 py-1.5 border border-white/5">
      <p className="text-[7px] opacity-40 uppercase tracking-wider mb-0.5">{label}</p>
      <p className="font-bold" style={{ color }}>{value}</p>
    </div>
  );
}
