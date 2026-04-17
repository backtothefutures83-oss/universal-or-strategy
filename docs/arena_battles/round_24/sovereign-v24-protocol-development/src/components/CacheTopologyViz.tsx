import { useState } from "react";

type Arch = "desktop" | "server" | "arm";

const ARCH_DATA = {
  desktop: {
    label: "Desktop x86-64 (e.g., Intel Core i9 / AMD Ryzen 9)",
    sockets: 1,
    coresPerSocket: 8,
    l1: { size: "48 KB (I) + 32 KB (D)", lineSize: 64, latency: "~4 cycles", associativity: "8-way" },
    l2: { size: "512 KB–1 MB per core", lineSize: 64, latency: "~12 cycles", associativity: "8-way" },
    l3: { size: "16–32 MB shared", lineSize: 64, latency: "~40 cycles", associativity: "16-way" },
    numa: false,
    numaInfo: "Single socket — no NUMA. All cores share the same memory controller.",
    interconnect: "N/A",
    memoryModel: "TSO (Total Store Order)",
    fenceFreeSpsc: true,
    notes: [
      "All 8 cores share one L3 cache ring or mesh.",
      "Cache line is universally 64 bytes — NOT 256 bytes.",
      "TSO memory model: stores are ordered, loads may reorder with older stores.",
      "Fence-free SPSC is safe HERE (single socket, one writer thread).",
    ],
  },
  server: {
    label: "Dual-Socket Server x86-64 (e.g., Intel Xeon / AMD EPYC)",
    sockets: 2,
    coresPerSocket: 32,
    l1: { size: "48 KB (I) + 32 KB (D)", lineSize: 64, latency: "~4 cycles", associativity: "8-way" },
    l2: { size: "1–2 MB per core", lineSize: 64, latency: "~14 cycles", associativity: "16-way" },
    l3: { size: "64–256 MB per socket", lineSize: 64, latency: "~60 cycles", associativity: "16-way" },
    numa: true,
    numaInfo: "2 NUMA nodes. Cross-socket access adds ~80–150 ns (QPI/UPI hop + remote DRAM).",
    interconnect: "Intel UPI @ ~41.6 GT/s or AMD Infinity Fabric",
    memoryModel: "TSO per socket — but cross-socket requires explicit barriers for ordering",
    fenceFreeSpsc: false,
    notes: [
      "CPUID returns 64-byte cache lines — the '256B hardware stripe' does not exist as a cache primitive.",
      "Cross-socket coherence: MESIF/MOESI protocol via QPI/UPI link.",
      "Fence-free SPSC is UNSAFE across sockets — store visibility is not guaranteed without barriers.",
      "NUMA distance: local = 10, remote = 21 (Intel) or higher (AMD multi-die).",
      "AMD EPYC uses chiplet design — even within one 'socket', CCDs have inter-chiplet latency.",
    ],
  },
  arm: {
    label: "ARM AArch64 (e.g., AWS Graviton3, Apple M2, Ampere Altra)",
    sockets: 1,
    coresPerSocket: 16,
    l1: { size: "64 KB (I) + 64 KB (D)", lineSize: 64, latency: "~3 cycles", associativity: "4-way" },
    l2: { size: "256 KB–1 MB per core", lineSize: 64, latency: "~10 cycles", associativity: "8-way" },
    l3: { size: "16–64 MB shared", lineSize: 64, latency: "~30 cycles", associativity: "16-way" },
    numa: false,
    numaInfo: "Typically single socket. Ampere Altra Max is multi-die but presents unified address space.",
    interconnect: "AMBA CHI (Coherent Hub Interface)",
    memoryModel: "Weakly-Ordered (WMO) — requires DMB/DSB barriers for ANY cross-thread ordering",
    fenceFreeSpsc: false,
    notes: [
      "Cache line is 64 bytes on all production ARM64 CPUs. 128-byte is optional extension (FEAT_LSE2).",
      "Memory model: MUCH weaker than x86 TSO. Loads AND stores can reorder freely.",
      "Fence-free code that works on x86 WILL FAIL silently on ARM — this is a real production bug class.",
      "AWS Graviton, Apple M-series, Ampere Altra all use WMO — fence-free SPSC UNSAFE here.",
      "DMB ISH barrier costs ~2–8 ns; this is the correct trade-off vs. silent data corruption.",
    ],
  },
};



export default function CacheTopologyViz() {
  const [arch, setArch] = useState<Arch>("desktop");
  const data = ARCH_DATA[arch];

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-2xl font-black text-white mb-1">CPU Cache Topology</h2>
        <p className="text-gray-400 text-sm">
          Real cache hierarchy parameters across CPU architectures. Note: cache line width is
          universally 64 bytes on all x86-64 and ARM64 production CPUs — not 256 bytes.
        </p>
      </div>

      {/* Architecture selector */}
      <div className="flex flex-wrap gap-2">
        {(Object.keys(ARCH_DATA) as Arch[]).map((a) => (
          <button
            key={a}
            onClick={() => setArch(a)}
            className={`px-4 py-2 rounded-lg text-sm font-bold border transition-all ${
              arch === a
                ? "bg-red-600 border-red-500 text-white"
                : "bg-gray-900 border-gray-700 text-gray-400 hover:text-white"
            }`}
          >
            {ARCH_DATA[a].label.split("(")[0].trim()}
          </button>
        ))}
      </div>

      <div className="rounded-xl border border-gray-700 bg-gray-900 px-5 py-4">
        <div className="text-gray-300 font-bold mb-1">{data.label}</div>
        <div className="flex gap-3 flex-wrap text-xs text-gray-500">
          <span>🧩 {data.sockets} socket{data.sockets > 1 ? "s" : ""}</span>
          <span>⚙️ {data.coresPerSocket} cores/socket</span>
          <span>📐 Memory model: <span className="text-yellow-400 font-bold">{data.memoryModel}</span></span>
          <span className={`font-bold ${data.fenceFreeSpsc ? "text-green-400" : "text-red-400"}`}>
            {data.fenceFreeSpsc ? "✓ Fence-free SPSC: conditionally safe" : "✗ Fence-free SPSC: UNSAFE"}
          </span>
        </div>
      </div>

      {/* Cache hierarchy visual */}
      <div className="space-y-3">
        <div className="text-gray-400 text-xs font-bold uppercase tracking-wider">Cache Hierarchy</div>

        {/* Core representation */}
        <div className="flex flex-wrap gap-3 mb-4">
          {Array.from({ length: Math.min(data.coresPerSocket, 8) }).map((_, i) => (
            <div key={i} className="relative">
              <div className="w-20 rounded-lg border border-blue-700 bg-blue-950 text-center py-2">
                <div className="text-blue-300 text-xs font-bold">Core {i}</div>
                {/* L1 */}
                <div className="mt-1 mx-1 rounded bg-blue-600 text-white text-xs py-0.5 font-bold">
                  L1
                </div>
                {/* L2 */}
                <div className="mt-0.5 mx-1 rounded bg-green-700 text-white text-xs py-0.5 font-bold">
                  L2
                </div>
              </div>
            </div>
          ))}
          {data.coresPerSocket > 8 && (
            <div className="flex items-center text-gray-500 text-sm">
              +{data.coresPerSocket - 8} more…
            </div>
          )}
        </div>

        {/* L3 shared */}
        <div className="rounded-xl border border-yellow-700 bg-yellow-950/20 px-4 py-3">
          <div className="text-yellow-400 font-bold text-sm">L3 — Shared across all {data.coresPerSocket} cores</div>
          <div className="text-yellow-300 text-xs mt-1">{data.l3.size} · {data.l3.latency} · {data.l3.associativity}</div>
        </div>

        {/* NUMA */}
        {data.numa && (
          <div className="rounded-xl border border-orange-700 bg-orange-950/20 px-4 py-3">
            <div className="text-orange-400 font-bold text-sm">⚠️ NUMA — Second Socket</div>
            <div className="text-orange-300 text-xs mt-1">{data.numaInfo}</div>
            <div className="text-orange-400 text-xs mt-1 font-bold">Interconnect: {data.interconnect}</div>
          </div>
        )}

        {/* DRAM */}
        <div className="rounded-xl border border-red-800 bg-red-950/20 px-4 py-3">
          <div className="text-red-400 font-bold text-sm">DRAM — Main Memory</div>
          <div className="text-red-300 text-xs mt-1">~80 ns local · ~180 ns remote NUMA · DDR5 @ 4800–7200 MT/s</div>
        </div>
      </div>

      {/* Cache parameters table */}
      <div>
        <div className="text-gray-400 text-xs font-bold uppercase tracking-wider mb-3">Exact Cache Parameters</div>
        <div className="rounded-xl border border-gray-800 overflow-hidden">
          <table className="w-full text-xs">
            <thead>
              <tr className="bg-gray-900 text-gray-400 border-b border-gray-800">
                <th className="text-left px-4 py-3 font-bold">Level</th>
                <th className="text-right px-4 py-3 font-bold">Size</th>
                <th className="text-right px-4 py-3 font-bold text-red-300">Line Size</th>
                <th className="text-right px-4 py-3 font-bold">Latency</th>
                <th className="text-right px-4 py-3 font-bold">Assoc.</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {[
                { name: "L1", ...data.l1 },
                { name: "L2", ...data.l2 },
                { name: "L3", ...data.l3 },
              ].map((row) => (
                <tr key={row.name} className="bg-gray-950 hover:bg-gray-900">
                  <td className="px-4 py-3 text-white font-bold">{row.name}</td>
                  <td className="px-4 py-3 text-right text-gray-300">{row.size}</td>
                  <td className="px-4 py-3 text-right text-red-300 font-mono font-bold">{row.lineSize} bytes</td>
                  <td className="px-4 py-3 text-right text-green-400 font-mono">{row.latency}</td>
                  <td className="px-4 py-3 text-right text-gray-400">{row.associativity}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Key observations */}
      <div className="rounded-xl border border-gray-700 bg-gray-900 p-5">
        <div className="text-white font-bold mb-3">Key Observations for {data.label.split("(")[0].trim()}</div>
        <ul className="space-y-2">
          {data.notes.map((note, i) => (
            <li key={i} className="flex gap-3 text-sm">
              <span className="text-gray-500 flex-shrink-0 font-mono">{String(i + 1).padStart(2, "0")}.</span>
              <span className="text-gray-300">{note}</span>
            </li>
          ))}
        </ul>
      </div>

      {/* The 256-byte claim */}
      <div className="rounded-xl border border-red-800 bg-red-950/20 p-5">
        <div className="text-red-400 font-black text-sm mb-2">
          ✗ SOV-V24 Claim: "Hardcoded 256B assumptions are BANNED; implementation must auto-align to the detected hardware-stripe"
        </div>
        <p className="text-gray-300 text-sm leading-relaxed">
          There is no "hardware stripe" of 256 bytes in any mainstream CPU. Cache lines are{" "}
          <span className="text-white font-bold">64 bytes</span> on all production x86-64 and ARM64
          CPUs (verified by CPUID leaf 0x4 / 0x8000001D). Some DRAM controllers have 128-byte burst
          granularity, and some Intel prefetchers work in 128-byte pairs, but these are not "cache
          lines" and not what lock-free data structures should align to. The correct alignment target
          for false-sharing prevention is <span className="text-green-400 font-bold">64 bytes</span>,
          as used by the Linux kernel, glibc, .NET runtime, and every major HPC framework.
        </p>
      </div>
    </div>
  );
}
