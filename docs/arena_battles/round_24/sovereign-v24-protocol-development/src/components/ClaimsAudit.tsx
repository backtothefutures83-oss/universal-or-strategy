import { useState } from "react";

type Verdict = "IMPOSSIBLE" | "MISLEADING" | "PARTIAL" | "VALID";

interface Claim {
  id: number;
  claim: string;
  source: string;
  verdict: Verdict;
  explanation: string;
  citation: string;
}

const CLAIMS: Claim[] = [
  {
    id: 1,
    claim: "0.5 ns target for a managed C# channel operation",
    source: "SOV-V24 spec §ARCHITECTURAL TARGET",
    verdict: "IMPOSSIBLE",
    explanation:
      "The .NET CLR JIT compilation, GC safe-point polling, and OS thread scheduling impose an irreducible floor well above 1 ns for any meaningful operation. A C# no-op method call costs ~1–3 ns; a cache-miss ~60–200 ns. The fastest known SPSC ring-buffer in unmanaged C++ on a pinned core achieves ~5–8 ns round-trip. 0.5 ns is below the latency of a single L1 cache hit on most CPUs (1–4 cycles @ 3 GHz = 0.33–1.3 ns), meaning the operation would have to complete faster than a memory access — with zero instructions executed.",
    citation: "Herlihy & Shavit, 'The Art of Multiprocessor Programming', 2nd ed. §1.2; Intel 64 IA-32 Arch. Manual Vol. 3A §11.3",
  },
  {
    id: 2,
    claim: "Fence-less discipline is safe across multiple NUMA sockets",
    source: "SOV-V24 spec §ADR-015",
    verdict: "IMPOSSIBLE",
    explanation:
      "x86/x64 TSO (Total Store Order) only guarantees store-ordering within a single CPU socket's coherence domain. Across NUMA sockets, stores from one socket become visible to another only after traversing the QPI/UPI interconnect, which has no ordering guarantee without explicit MFENCE or LOCK-prefix instructions. On ARM (used in AWS Graviton, Apple M-series), RISC-V, and Power architectures, even single-socket ordering requires explicit DMB/FENCE instructions. Fence-free multi-socket code produces data races by definition on any non-TSO architecture.",
    citation: "Paul McKenney, 'Is Parallel Programming Hard?', §C.3 (memory barriers); ARM Architecture Ref. Manual §B2.3 (memory ordering); AMD64 Vol. 2 §7.2",
  },
  {
    id: 3,
    claim: "Hardware-agnostic fence-free operation",
    source: "SOV-V24 spec §1 & §ADR-015",
    verdict: "IMPOSSIBLE",
    explanation:
      "'Hardware-agnostic' and 'fence-free' are mutually exclusive. x86 TSO is the only mainstream memory model that allows certain fence-free patterns (single-writer, single-reader, same socket). ARM uses a weakly-ordered model (WMO) requiring explicit barriers. RISC-V uses RVWMO, even weaker than ARM. A design that is genuinely hardware-agnostic MUST include fences — their cost is 1–5 ns on modern CPUs, not a performance catastrophe.",
    citation: "RISC-V Instruction Set Manual Vol. I §A (RVWMO); ARM Cortex-A Programmer's Guide §13.1",
  },
  {
    id: 4,
    claim: "Marshal.AllocHGlobal telemetry adds zero latency",
    source: "SOV-V24 spec §ADR-015",
    verdict: "MISLEADING",
    explanation:
      "Marshal.AllocHGlobal calls HeapAlloc (Windows) or malloc (Linux via dlmalloc/ptmalloc). These are NOT zero-latency: a hot allocation from a thread-local cache costs ~20–50 ns; a cold allocation crossing page boundaries costs hundreds of ns to microseconds. The word 'unmanaged' does not mean 'free'. The correct approach for latency-critical paths is pre-allocated pinned buffers with fixed offsets — never runtime allocation.",
    citation: "Dmitry Vyukov, 'Lock-Free Data Structures' (2008); Microsoft Docs: Marshal.AllocHGlobal implementation notes",
  },
  {
    id: 5,
    claim: "Bitwise sequence-shadow validation is non-latency-summing",
    source: "SOV-V24 spec §2",
    verdict: "MISLEADING",
    explanation:
      "'Non-latency-summing safety check' is not a recognized term in computer architecture or systems literature. Any validation — bitwise XOR, CRC, shadow-copy comparison — requires memory reads and arithmetic operations. These cost cycles. A SeqLock (the real technique this vaguely describes) requires two sequence-number reads per read operation: ~2–6 ns extra. This is acceptable overhead, but it is NOT zero. The claim obscures real costs behind invented jargon.",
    citation: "Linus Torvalds, Linux kernel seqlock.h implementation; LWN.net 'Sequence locks' (2003)",
  },
  {
    id: 6,
    claim: "Auto-detect L1/L2/L3 cache line widths dynamically",
    source: "SOV-V24 spec §1",
    verdict: "PARTIAL",
    explanation:
      "Cache geometry IS detectable: CPUID leaf 0x4 (Intel), 0x8000001D (AMD), or /sys/devices/system/cpu/cpu0/cache/ on Linux. In .NET, RuntimeInformation and P/Invoke to GetLogicalProcessorInformationEx work. HOWEVER: the CLR GC can relocate objects between any two instructions, nullifying cache-line alignment. Effective cache-aware allocation requires pinned unmanaged memory (Marshal.AllocHGlobal + manual alignment), huge pages, and CPU affinity — none of which are 'auto' or 'zero-friction'.",
    citation: "Intel CPUID Application Note AP-485; .NET RuntimeInformation docs; NUMA API documentation (libnuma)",
  },
  {
    id: 7,
    claim: "Banning Thread.MemoryBarrier() improves safety",
    source: "SOV-V24 spec §ADR-015",
    verdict: "MISLEADING",
    explanation:
      "Thread.MemoryBarrier() compiles directly to MFENCE (x86) or DMB ISH (ARM) — these are correct, well-specified hardware primitives. Banning them while claiming cross-platform safety is backwards: it removes the mechanism that PROVIDES safety. Removing barriers doesn't make code faster if it then produces wrong results — a 0 ns race condition is infinitely slower than a correct 5 ns barrier. The mandate confuses 'fence-free x86 SPSC (valid in narrow conditions)' with 'fence-free everywhere (data race)'.",
    citation: "ECMA-335 §I.12.6 (memory model); 'C++ and the Perils of Double-Checked Locking', Meyers & Alexandrescu (2004)",
  },
];

const VERDICT_STYLES: Record<Verdict, { badge: string; border: string; icon: string }> = {
  IMPOSSIBLE: { badge: "bg-red-600 text-white",          border: "border-red-800",    icon: "✗" },
  MISLEADING: { badge: "bg-orange-600 text-white",       border: "border-orange-800", icon: "⚠" },
  PARTIAL:    { badge: "bg-yellow-600 text-gray-950",    border: "border-yellow-800", icon: "≈" },
  VALID:      { badge: "bg-green-600 text-white",        border: "border-green-800",  icon: "✓" },
};

export default function ClaimsAudit() {
  const [expanded, setExpanded] = useState<number | null>(1);

  const counts = CLAIMS.reduce((acc, c) => {
    acc[c.verdict] = (acc[c.verdict] || 0) + 1;
    return acc;
  }, {} as Record<Verdict, number>);

  return (
    <div className="space-y-8">
      {/* Summary */}
      <div>
        <h2 className="text-2xl font-black text-white mb-1">SOV-V24 Claims Audit</h2>
        <p className="text-gray-400 text-sm mb-6">
          Each claim from the SOVEREIGN V24 specification is evaluated against established
          computer architecture literature, hardware vendor documentation, and empirical benchmarks.
        </p>

        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-8">
          {(["IMPOSSIBLE", "MISLEADING", "PARTIAL", "VALID"] as Verdict[]).map((v) => (
            <div key={v} className={`rounded-xl border p-4 ${VERDICT_STYLES[v].border} bg-gray-900`}>
              <div className="flex items-center gap-2 mb-1">
                <span className={`text-xs font-black px-2 py-0.5 rounded ${VERDICT_STYLES[v].badge}`}>
                  {VERDICT_STYLES[v].icon} {v}
                </span>
              </div>
              <div className="text-3xl font-black text-white">{counts[v] ?? 0}</div>
              <div className="text-xs text-gray-500">claims</div>
            </div>
          ))}
        </div>
      </div>

      {/* Claims list */}
      <div className="space-y-3">
        {CLAIMS.map((claim) => {
          const style = VERDICT_STYLES[claim.verdict];
          const isOpen = expanded === claim.id;
          return (
            <div
              key={claim.id}
              className={`rounded-xl border ${style.border} bg-gray-900 overflow-hidden transition-all duration-200`}
            >
              <button
                className="w-full text-left px-5 py-4 flex items-start gap-4"
                onClick={() => setExpanded(isOpen ? null : claim.id)}
              >
                <span className={`mt-0.5 flex-shrink-0 text-xs font-black px-2 py-1 rounded ${style.badge}`}>
                  {style.icon} {claim.verdict}
                </span>
                <div className="flex-1 min-w-0">
                  <div className="text-white font-semibold text-sm leading-snug">
                    "{claim.claim}"
                  </div>
                  <div className="text-gray-500 text-xs mt-1">Source: {claim.source}</div>
                </div>
                <span className="text-gray-500 text-lg flex-shrink-0">{isOpen ? "▲" : "▼"}</span>
              </button>

              {isOpen && (
                <div className="px-5 pb-5 border-t border-gray-800 pt-4">
                  <p className="text-gray-300 text-sm leading-relaxed mb-4">{claim.explanation}</p>
                  <div className="bg-gray-950 rounded-lg px-4 py-3 border border-gray-800">
                    <div className="text-xs text-gray-500 font-bold uppercase tracking-wider mb-1">📚 Citations</div>
                    <div className="text-xs text-gray-400 leading-relaxed">{claim.citation}</div>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Closing note */}
      <div className="rounded-xl border border-blue-800 bg-blue-950/30 p-6">
        <div className="text-blue-300 font-black text-lg mb-2">💡 The Core Problem</div>
        <p className="text-gray-300 text-sm leading-relaxed">
          The SOV-V24 spec uses real-sounding technical vocabulary — "TSO parity",
          "hardware-stripe", "friction-less scaling" — but combines them in ways that
          contradict each other and violate fundamental hardware constraints. This is a pattern
          known as <span className="text-yellow-400 font-bold">specification theater</span>: documents
          that sound authoritative but describe physically impossible systems.
          A genuine low-latency design starts from hardware constraints (memory models, cache geometry,
          OS scheduling) and works upward — not the reverse.
        </p>
      </div>
    </div>
  );
}
