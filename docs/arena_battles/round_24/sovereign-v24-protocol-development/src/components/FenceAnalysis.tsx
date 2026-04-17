import { useState } from "react";

type Scenario = "spsc_x86" | "spsc_arm" | "mpsc_x86" | "multi_socket" | "dotnet_channel";

const SCENARIOS: { id: Scenario; label: string; safe: boolean }[] = [
  { id: "spsc_x86",       label: "SPSC — x86-64, single socket",    safe: true  },
  { id: "spsc_arm",       label: "SPSC — ARM64, fence-free",        safe: false },
  { id: "mpsc_x86",       label: "MPSC — x86-64, fence-free",       safe: false },
  { id: "multi_socket",   label: "Multi-socket, fence-free",        safe: false },
  { id: "dotnet_channel", label: ".NET Channel<T> (correct)",       safe: true  },
];

const SCENARIO_DETAIL: Record<Scenario, {
  title: string;
  verdict: string;
  verdictStyle: string;
  memory_model: string;
  why: string[];
  code: string;
  cost: string;
  recommendation: string;
}> = {
  spsc_x86: {
    title: "Single-Producer / Single-Consumer Ring Buffer — x86-64, Single Socket",
    verdict: "CONDITIONALLY SAFE",
    verdictStyle: "text-green-400 border-green-700 bg-green-950/30",
    memory_model: "x86 TSO (Total Store Order)",
    why: [
      "x86 TSO guarantees that stores from any core become visible to all other cores in program order.",
      "With exactly ONE writer thread and ONE reader thread, the reader always sees stores in the order they were written.",
      "The head (write cursor) is only written by the producer — no CAS needed.",
      "The tail (read cursor) is only written by the consumer — no CAS needed.",
      "A single compiler barrier (std::atomic with memory_order_relaxed on x86, or a compiler-only fence) is sufficient to prevent the compiler from reordering loads/stores.",
      "IMPORTANT: This safety guarantee evaporates if: (a) multiple producers exist, (b) thread migrates to different socket, or (c) you run on non-TSO architecture.",
    ],
    code: `// C++ SPSC ring buffer — correct for x86-64, single socket
// Uses std::atomic for compiler ordering only; no CPU fence emitted on x86
struct alignas(64) SPSCQueue {
    static constexpr size_t CAPACITY = 4096;
    
    // Pad to separate cache lines — CRITICAL for false-sharing prevention
    alignas(64) std::atomic<size_t> head{0};  // written only by producer
    alignas(64) std::atomic<size_t> tail{0};  // written only by consumer

    T slots[CAPACITY];

    bool push(const T& item) {
        size_t h = head.load(std::memory_order_relaxed);
        size_t next = (h + 1) % CAPACITY;
        if (next == tail.load(std::memory_order_acquire))  // acquire: see consumer writes
            return false; // full
        slots[h] = item;
        head.store(next, std::memory_order_release); // release: publish item to consumer
        return true;
    }

    bool pop(T& item) {
        size_t t = tail.load(std::memory_order_relaxed);
        if (t == head.load(std::memory_order_acquire))  // acquire: see producer writes
            return false; // empty
        item = slots[t];
        tail.store((t + 1) % CAPACITY, std::memory_order_release);
        return true;
    }
};
// On x86-64: acquire/release compile to plain MOV — zero hardware fence overhead
// On ARM64: acquire compiles to LDAR, release to STLR — correct and efficient`,
    cost: "~5–8 ns round-trip on x86-64 with hot cache; ~8–15 ns on ARM64 with LDAR/STLR",
    recommendation:
      "This is the correct pattern for SPSC IPC. Use std::atomic with explicit memory_order — not volatile, not bare pointers, not 'fence-free' without reasoning.",
  },

  spsc_arm: {
    title: "SPSC Ring Buffer — ARM64, Fence-Free (memory_order_relaxed only)",
    verdict: "DATA RACE — UNDEFINED BEHAVIOR",
    verdictStyle: "text-red-400 border-red-700 bg-red-950/30",
    memory_model: "ARM WMO (Weakly-Ordered Memory Model)",
    why: [
      "ARM64 uses a weakly-ordered memory model: loads and stores can be reordered by the CPU in almost any order.",
      "A producer writing `slots[h] = item` then `head = next` — the CPU may make `head` visible BEFORE `slots[h]`.",
      "The consumer sees the updated head, reads slots[h], and gets GARBAGE — the item hasn't been written yet.",
      "This bug is intermittent, hardware-specific, and impossible to reproduce under a debugger (which adds its own barriers).",
      "It will occur under load on any Graviton, Apple M-series, Ampere Altra, or similar ARM processor.",
      "The fix is exactly memory_order_acquire/release on the head/tail atomics — not banning barriers.",
    ],
    code: `// ⚠️ BROKEN on ARM64 — illustrating the failure mode
struct BrokenSPSC {
    std::atomic<size_t> head{0};
    std::atomic<size_t> tail{0};
    T slots[4096];

    bool push(const T& item) {
        size_t h = head.load(std::memory_order_relaxed);
        size_t next = (h + 1) % 4096;
        if (next == tail.load(std::memory_order_relaxed))
            return false;
        
        slots[h] = item;  // ← may become visible AFTER the store below on ARM!
        
        head.store(next, std::memory_order_relaxed);  // ← consumer sees this first
        return true;
    }
    // Result: consumer reads head, sees new value, reads slots[h] → GARBAGE
    // This is NOT "safe due to TSO" — ARM is NOT TSO
};

// Correct version requires memory_order_release on head.store()
// and memory_order_acquire on head.load() in the consumer.
// On ARM64, this compiles to: STLR (store-release) and LDAR (load-acquire)
// Cost: ~1-2 ns extra vs. relaxed — worth it for correctness`,
    cost: "Fence-free broken version: 0 ns overhead, infinite correctness cost. Correct version with acquire/release: +1–2 ns, correct on all architectures.",
    recommendation:
      "Never write 'fence-free' code that relies on x86 TSO without documenting that assumption and building in a compile-time architecture check (#ifdef __x86_64__). Use C++11 atomics with explicit memory orders — they compile to the minimum necessary barrier for each architecture.",
  },

  mpsc_x86: {
    title: "Multi-Producer / Single-Consumer — x86-64, Fence-Free",
    verdict: "DATA RACE — LOST UPDATES",
    verdictStyle: "text-red-400 border-red-700 bg-red-950/30",
    memory_model: "x86 TSO — but TSO does NOT help here",
    why: [
      "Even on x86-64, multiple producers writing to the same head pointer create a classic lost-update race.",
      "Producer A reads head=10, Producer B reads head=10 simultaneously.",
      "Both compute next=11. Both write slots[10]. Both store head=11.",
      "Result: one message is silently overwritten, head is wrong, queue is corrupted.",
      "TSO does not prevent this: TSO orders stores, it does not make read-modify-write operations atomic.",
      "Fix: use LOCK CMPXCHG (CAS loop) or a dedicated MPSC algorithm (e.g., Michael-Scott queue, Dmitry Vyukov's MPSC).",
    ],
    code: `// ⚠️ BROKEN multi-producer — even on x86-64
// Two threads run push() simultaneously:
struct BrokenMPSC {
    std::atomic<size_t> head{0};  // shared write target!
    T slots[4096];

    bool push(const T& item) {
        size_t h = head.load(std::memory_order_relaxed);  // Thread A: reads 10
                                                           // Thread B: reads 10 simultaneously
        size_t next = (h + 1) % 4096;  // Both compute 11
        slots[h] = item;               // Both write to slots[10] — one overwrites the other
        head.store(next, std::memory_order_release);  // Both store 11 — no progress
        return true;
    }
};

// Correct MPSC uses CAS:
bool push(const T& item) {
    size_t h, next;
    do {
        h = head.load(std::memory_order_relaxed);
        next = (h + 1) % CAPACITY;
        if (next == tail.load(std::memory_order_acquire))
            return false;
    } while (!head.compare_exchange_weak(
                 h, next,
                 std::memory_order_release,    // success: publish new head
                 std::memory_order_relaxed));   // failure: retry
    slots[h] = item;  // But: slot write ordering still needs care — use Vyukov's algorithm
    return true;
}
// CAS cost: ~5 ns uncontended, ~50 ns contended (cache-line ping-pong)`,
    cost: "Broken version: 0 ns, 100% data loss rate under concurrent producers. CAS version: +3–50 ns depending on contention.",
    recommendation:
      "For MPSC: use Dmitry Vyukov's MPSC queue (intrusive linked list, wait-free for producers), or LMAX Disruptor pattern with explicit claim-sequence. Never share a head pointer between producers without CAS.",
  },

  multi_socket: {
    title: "Cross-Socket Communication — Any Architecture, Fence-Free",
    verdict: "UNSAFE — ORDERING NOT GUARANTEED",
    verdictStyle: "text-red-400 border-red-700 bg-red-950/30",
    memory_model: "QPI/UPI coherence — stores visible only after interconnect traversal",
    why: [
      "NUMA systems consist of multiple CPU sockets, each with their own memory controller and L3 cache.",
      "Store propagation across sockets traverses the QPI/UPI (Intel) or Infinity Fabric (AMD) interconnect.",
      "Even under x86 TSO, the ORDERING of stores becoming visible across sockets is not instantaneous.",
      "A producer on socket 0 writing A then B: socket 1 may see B before A if stores are batched differently on the interconnect.",
      "MFENCE ensures the store buffer is drained before the fence completes — this is the correct solution.",
      "Cross-socket fence-free is documented as unsafe in Intel's Software Developer Manual Vol. 3A §8.2.3.",
    ],
    code: `// Cross-socket scenario — socket 0 writes, socket 1 reads
// (pseudocode showing the problem and solution)

// Socket 0 — Producer (CPU affinity: socket 0)
void producer() {
    data[idx] = value;        // Write data
    // ⚠️ Without SFENCE/MFENCE:
    // The CPU may delay this store in the store buffer
    // while sending header[idx] = READY first
    _mm_sfence();             // ← REQUIRED: drain store buffer before flag
    header[idx] = READY;      // Publish readiness flag
}

// Socket 1 — Consumer (CPU affinity: socket 1)
void consumer() {
    while (header[idx] != READY)  // Spin on flag
        _mm_pause();              // Reduce bus traffic during spin
    _mm_lfence();                 // ← REQUIRED: prevent load reordering
    use(data[idx]);               // Read data — now guaranteed visible
}

// Cost of SFENCE + LFENCE pair: ~3–10 ns total
// This is the CORRECT architecture for cross-socket IPC.
// Any "fence-free" version of this code is simply broken.`,
    cost: "Fence pair cost: ~3–10 ns. Fence-free 'cost': zero ns + undefined behavior + silent data corruption under load.",
    recommendation:
      "For cross-socket IPC: always use explicit store/load fences. Consider RDMA or DPDK for ultra-low-latency cross-socket paths where nanoseconds matter — hardware coherence is inherently slower than intra-socket paths.",
  },

  dotnet_channel: {
    title: ".NET System.Threading.Channel<T> — Correct Implementation",
    verdict: "SAFE — WITH REALISTIC LATENCY",
    verdictStyle: "text-green-400 border-green-700 bg-green-950/30",
    memory_model: "ECMA-335 memory model + .NET atomics (maps to hardware correctly)",
    why: [
      "System.Threading.Channel<T> uses Interlocked operations and Monitor for its slow path — both correct.",
      "The .NET memory model (ECMA-335 §I.12.6) is defined over acquire/release pairs, not TSO specifics.",
      "Volatile reads/writes in .NET correctly compile to MFENCE/LFENCE (x86) or DMB (ARM).",
      "The GC ensures that object references are always valid — no manual memory hazard tracking needed.",
      "Realistic latency: 50–500 ns depending on path (bounded/unbounded, contention, GC pressure).",
      "The .NET team has benchmarked and optimized this — it's correct AND as fast as a managed runtime allows.",
    ],
    code: `// System.Threading.Channel<T> — correct managed SPSC/MPMC
// (Simplified view of the hot path from dotnet/runtime source)

// Producer side (simplified):
public async ValueTask WriteAsync(T item, CancellationToken ct = default) {
    // Fast path — no lock needed if space available
    if (TryWrite(item)) return;
    // Slow path — async wait for space (uses TaskCompletionSource internally)
    await WaitToWriteAsync(ct);
    TryWrite(item);
}

// The actual TryWrite uses Interlocked.CompareExchange for the slot claim:
private bool TryWrite(T item) {
    SpinWait spinner = default;
    while (true) {
        int head = Volatile.Read(ref _head);  // acquire semantics
        int next = (head + 1) & _mask;
        if (next == Volatile.Read(ref _tail)) return false;  // full
        
        if (Interlocked.CompareExchange(ref _head, next, head) == head) {
            _slots[head].Item = item;
            Volatile.Write(ref _slots[head].SequenceNumber, next);  // release
            return true;
        }
        spinner.SpinOnce();  // back-off under contention
    }
}
// Latency: ~50–120 ns hot path, ~500 ns+ with async await
// Throughput: ~5–20 M ops/sec depending on bounded/unbounded`,
    cost: "~50–120 ns for the hot path (correct and safe). Attempting to 'optimize' by removing barriers produces silent corruption.",
    recommendation:
      "For .NET production code: use Channel<T> or implement a proper lock-free structure using Interlocked/Volatile. Do not remove fences to hit an arbitrary ns target — correctness is not optional.",
  },
};

export default function FenceAnalysis() {
  const [scenario, setScenario] = useState<Scenario>("spsc_x86");
  const detail = SCENARIO_DETAIL[scenario];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-black text-white mb-1">Memory Fence Analysis</h2>
        <p className="text-gray-400 text-sm">
          Interactive analysis of fence requirements across CPU architectures, topologies, and producer-consumer
          configurations. Understand exactly when fence-free is safe — and when it is a silent data race.
        </p>
      </div>

      {/* Scenario selector */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
        {SCENARIOS.map((s) => (
          <button
            key={s.id}
            onClick={() => setScenario(s.id)}
            className={`px-4 py-3 rounded-xl text-sm font-bold border text-left transition-all ${
              scenario === s.id
                ? "bg-red-600 border-red-500 text-white"
                : "bg-gray-900 border-gray-700 text-gray-400 hover:text-white hover:border-gray-600"
            }`}
          >
            <div className="flex items-center gap-2">
              <span>{s.safe ? "✅" : "❌"}</span>
              <span>{s.label}</span>
            </div>
          </button>
        ))}
      </div>

      {/* Detail panel */}
      <div className={`rounded-xl border p-5 ${detail.verdictStyle}`}>
        <div className="font-black text-lg mb-1">{detail.title}</div>
        <div className={`inline-flex items-center gap-2 px-3 py-1 rounded-full border text-sm font-bold ${detail.verdictStyle}`}>
          Verdict: {detail.verdict}
        </div>
        <div className="text-xs mt-2 opacity-70">Memory Model: {detail.memory_model}</div>
      </div>

      {/* Why section */}
      <div className="rounded-xl border border-gray-700 bg-gray-900 p-5">
        <div className="text-white font-bold mb-3">Why?</div>
        <ol className="space-y-2">
          {detail.why.map((reason, i) => (
            <li key={i} className="flex gap-3 text-sm">
              <span className="text-gray-500 flex-shrink-0 font-mono font-bold">{i + 1}.</span>
              <span className="text-gray-300">{reason}</span>
            </li>
          ))}
        </ol>
      </div>

      {/* Code */}
      <div className="rounded-xl border border-gray-700 overflow-hidden">
        <div className="bg-gray-800 px-4 py-2 flex items-center gap-2">
          <div className="w-3 h-3 rounded-full bg-red-500" />
          <div className="w-3 h-3 rounded-full bg-yellow-500" />
          <div className="w-3 h-3 rounded-full bg-green-500" />
          <span className="text-gray-400 text-xs ml-2">Implementation</span>
        </div>
        <pre className="bg-gray-950 p-4 text-xs text-green-300 leading-relaxed overflow-x-auto whitespace-pre font-mono">
          {detail.code}
        </pre>
      </div>

      {/* Cost and recommendation */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="rounded-xl border border-yellow-800 bg-yellow-950/20 p-4">
          <div className="text-yellow-400 font-bold text-sm mb-2">⏱️ Latency Cost</div>
          <p className="text-gray-300 text-sm">{detail.cost}</p>
        </div>
        <div className="rounded-xl border border-blue-800 bg-blue-950/20 p-4">
          <div className="text-blue-400 font-bold text-sm mb-2">✅ Recommendation</div>
          <p className="text-gray-300 text-sm">{detail.recommendation}</p>
        </div>
      </div>

      {/* Memory model reference */}
      <div className="rounded-xl border border-gray-700 bg-gray-900 p-5">
        <div className="text-white font-bold mb-3">Memory Model Ordering Strength</div>
        <div className="space-y-2">
          {[
            { arch: "x86-64 TSO",      strength: 90, color: "bg-green-500",  desc: "Strongest — stores ordered, only StoreLoad reordering possible" },
            { arch: ".NET ECMA-335",   strength: 75, color: "bg-blue-500",   desc: "Acquire/release semantics; correct on all platforms" },
            { arch: "Java JMM",        strength: 75, color: "bg-blue-400",   desc: "Similar to .NET; volatile provides full barrier" },
            { arch: "ARM64 WMO",       strength: 35, color: "bg-orange-500", desc: "Weak — loads and stores both reorderable; DMB required" },
            { arch: "RISC-V RVWMO",    strength: 25, color: "bg-red-500",    desc: "Weakest mainstream — explicit FENCE instruction required" },
            { arch: "PowerPC",         strength: 30, color: "bg-red-400",    desc: "Weak ordering; SYNC/LWSYNC required for ordering" },
          ].map((m) => (
            <div key={m.arch} className="flex items-center gap-3">
              <div className="w-24 text-right text-xs text-gray-400 font-mono flex-shrink-0">{m.arch}</div>
              <div className="flex-1 bg-gray-800 rounded-full h-4 overflow-hidden">
                <div
                  className={`h-full ${m.color} rounded-full transition-all`}
                  style={{ width: `${m.strength}%` }}
                />
              </div>
              <div className="text-xs text-gray-500 hidden lg:block max-w-xs">{m.desc}</div>
            </div>
          ))}
        </div>
        <p className="text-gray-500 text-xs mt-4">
          "Stronger" = more ordering guarantees by default; requires fewer explicit fences. This does NOT mean
          "faster" — the performance trade-off is: x86 TSO = fast-by-default but expensive store buffers;
          ARM WMO = cheap stores by default but explicit DMB when ordering is needed.
        </p>
      </div>
    </div>
  );
}
