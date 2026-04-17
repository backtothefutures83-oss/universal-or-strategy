const LAWS = [
  {
    icon: "⟶",
    title: "Everything is a Pipe",
    color: "#06b6d4",
    body: "No shared queues. No event buses. No pub/sub brokers. Every interaction is a private, unidirectional SPSC ring between exactly two nodes. Topology is the protocol.",
  },
  {
    icon: "⊘",
    title: "No Blocks",
    color: "#ef4444",
    body: "Spin-poll with exponential back-off to PAUSE. Never yield to the OS scheduler on the hot path. The kernel does not exist inside the 10µs gate.",
  },
  {
    icon: "⊘",
    title: "No Managers",
    color: "#f59e0b",
    body: "No thread pools. No work-stealing schedulers. No dispatchers. Each node is sovereign — it owns its pipe, its slab, its core. Coordination is emergent from topology, not from a controller.",
  },
  {
    icon: "⊘",
    title: "No Copies",
    color: "#8b5cf6",
    body: "Structured cloning is abolished. The fax machine is unplugged. Downstream nodes receive a pointer into the upstream slab. The payload never moves in memory — only ownership of the pointer transfers.",
  },
];

const RULES = [
  { label: "SPSC Invariant",       text: "One producer. One consumer. Always. Forever.",                          color: "#06b6d4" },
  { label: "Core Pinning",         text: "pthread_setaffinity_np() at boot. No OS migration after init.",         color: "#10b981" },
  { label: "Slab-Only Allocation", text: "mmap + mlock at boot. Zero malloc/free on any hot path.",              color: "#f59e0b" },
  { label: "Memory Visibility",    text: "store-release on produce. load-acquire on consume. C++20 atomics.",     color: "#8b5cf6" },
  { label: "Cache Discipline",     text: "Producer/consumer ptrs separated by ≥64B (1 cache-line) padding.",     color: "#3b82f6" },
  { label: "Ring Power-of-2",      text: "All ring sizes are powers of 2. idx = seq & (size-1). No modulo.",     color: "#06b6d4" },
];

export default function PhilosophyCard() {
  return (
    <div className="grid grid-cols-1 gap-5">
      {/* Philosophy laws */}
      <div className="bg-gray-950 border border-gray-800 rounded-xl overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-800 bg-black/40">
          <span className="text-gray-300 font-mono text-xs font-bold tracking-widest uppercase">
            ◈ OS Philosophy — The Four Laws of Antigravity Nexus
          </span>
        </div>
        <div className="grid grid-cols-2 gap-px bg-gray-800/20 p-px">
          {LAWS.map((law, i) => (
            <div key={i} className="bg-gray-950 p-5">
              <div className="flex items-center gap-2 mb-3">
                <span className="text-2xl font-mono" style={{ color: law.color }}>{law.icon}</span>
                <h3 className="font-bold text-sm text-white font-mono">{law.title}</h3>
              </div>
              <p className="text-[11px] text-gray-400 leading-relaxed font-mono">{law.body}</p>
              <div className="mt-3 h-px" style={{ background: `linear-gradient(90deg, ${law.color}40, transparent)` }} />
            </div>
          ))}
        </div>
      </div>

      {/* Implementation axioms */}
      <div className="bg-gray-950 border border-gray-800 rounded-xl overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-800 bg-black/40">
          <span className="text-gray-300 font-mono text-xs font-bold tracking-widest uppercase">
            ◈ Implementation Axioms — Surgical Precision
          </span>
        </div>
        <div className="p-5 grid grid-cols-2 gap-3">
          {RULES.map((rule, i) => (
            <div
              key={i}
              className="rounded-lg border p-3"
              style={{ borderColor: `${rule.color}30`, background: `${rule.color}06` }}
            >
              <p className="text-[10px] font-mono font-bold mb-1" style={{ color: rule.color }}>{rule.label}</p>
              <p className="text-[10px] font-mono text-gray-400 leading-relaxed">{rule.text}</p>
            </div>
          ))}
        </div>

        {/* Banned patterns */}
        <div className="mx-5 mb-5 rounded-lg border border-red-900/40 bg-red-950/10 p-4 font-mono text-[10px]">
          <p className="text-red-400 font-bold mb-2 text-[11px]">🚫 BANNED PATTERNS — HARD VIOLATIONS</p>
          <div className="grid grid-cols-2 gap-2">
            {[
              ["Multi-Producer Writes",     "No two nodes may write to the same SPSC ring. Ever."],
              ["Structured Cloning",        "memcpy of payload is a capital offense. Pass the pointer."],
              ["Dynamic Allocation",        "No malloc/new/free on any path inside the mesh."],
              ["Implicit Context Switch",   "No blocking syscalls. No OS-led thread migration. No mutexes."],
            ].map(([k, v], i) => (
              <div key={i} className="flex gap-2">
                <span className="text-red-500 flex-shrink-0">✗</span>
                <div>
                  <p className="text-red-400 font-bold">{k}</p>
                  <p className="text-gray-500 text-[9px] mt-0.5">{v}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Pseudocode snippet */}
      <div className="bg-gray-950 border border-gray-800 rounded-xl overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-800 bg-black/40 flex items-center justify-between">
          <span className="text-gray-300 font-mono text-xs font-bold tracking-widest uppercase">
            ◈ SPSC Ring — Reference Implementation (C++20)
          </span>
          <span className="text-[9px] font-mono text-gray-600">lock-free · zero-copy · power-of-2 ring</span>
        </div>
        <pre className="p-5 text-[10px] font-mono leading-relaxed overflow-x-auto">
<span className="text-gray-600">{"// ─── SLAB-BACKED SPSC RING ────────────────────────────────────────────────"}</span>
{"\n"}<span className="text-violet-400">{"template"}</span><span className="text-white">{"<"}</span><span className="text-cyan-300">{"typename T"}</span><span className="text-white">{", "}</span><span className="text-cyan-300">{"size_t N"}</span><span className="text-white">{">"}</span>
{"\n"}<span className="text-violet-400">{"struct"}</span><span className="text-cyan-300">{" SPSCRing"}</span><span className="text-white">{" {"}</span>
{"\n  "}<span className="text-gray-600">{"// Producer and consumer pointers on separate cache lines"}</span>
{"\n  "}<span className="text-violet-400">{"alignas"}</span><span className="text-white">{"(64) "}</span><span className="text-cyan-300">{"std::atomic"}</span><span className="text-white">{"<"}</span><span className="text-cyan-300">{"uint64_t"}</span><span className="text-white">{">"}</span><span className="text-amber-300">{" head_"}</span><span className="text-white">{"{"}</span><span className="text-green-400">{"0"}</span><span className="text-white">{"};  "}</span><span className="text-gray-600">{"// producer writes"}</span>
{"\n  "}<span className="text-violet-400">{"alignas"}</span><span className="text-white">{"(64) "}</span><span className="text-cyan-300">{"std::atomic"}</span><span className="text-white">{"<"}</span><span className="text-cyan-300">{"uint64_t"}</span><span className="text-white">{">"}</span><span className="text-amber-300">{" tail_"}</span><span className="text-white">{"{"}</span><span className="text-green-400">{"0"}</span><span className="text-white">{"};  "}</span><span className="text-gray-600">{"// consumer writes"}</span>
{"\n  "}<span className="text-cyan-300">{"T"}</span><span className="text-amber-300">{" slots_"}</span><span className="text-white">{"[N];  "}</span><span className="text-gray-600">{"// N must be power-of-2, slab-backed"}</span>

{"\n\n  "}<span className="text-gray-600">{"// PRODUCE — called ONLY by pinned producer core"}</span>
{"\n  "}<span className="text-violet-400">{"[[nodiscard]] bool"}</span><span className="text-amber-300">{" try_push"}</span><span className="text-white">{"("}</span><span className="text-cyan-300">{"T"}</span><span className="text-white">{"* ptr) noexcept {"}</span>
{"\n    "}<span className="text-violet-400">{"const auto"}</span><span className="text-white">{" h = head_.load("}</span><span className="text-cyan-300">{"memory_order_relaxed"}</span><span className="text-white">{");"}</span>
{"\n    "}<span className="text-violet-400">{"if"}</span><span className="text-white">{" (h - tail_.load("}</span><span className="text-cyan-300">{"memory_order_acquire"}</span><span className="text-white">{") == N) "}</span><span className="text-violet-400">{"return false"}</span><span className="text-white">{";"}</span>
{"\n    "}<span className="text-amber-300">{"slots_"}</span><span className="text-white">{"[h & (N-1)] = *ptr;"}</span>
{"\n    "}<span className="text-amber-300">{"head_"}</span><span className="text-white">{".store(h + 1, "}</span><span className="text-cyan-300">{"memory_order_release"}</span><span className="text-white">{");  "}</span><span className="text-gray-600">{"// ← single store, visible to consumer"}</span>
{"\n    "}<span className="text-violet-400">{"return true"}</span><span className="text-white">{";"}</span>
{"\n  }"}</pre>
      </div>
    </div>
  );
}
