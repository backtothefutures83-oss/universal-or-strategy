import { useState } from "react";

type Pattern = "seqlock" | "disruptor" | "vyukov" | "hazard" | "rcu";

const PATTERNS: { id: Pattern; name: string; use: string; complexity: number; latency: string }[] = [
  { id: "seqlock",   name: "SeqLock",              use: "1 writer, N readers; infrequent writes", complexity: 2, latency: "3–8 ns read" },
  { id: "disruptor", name: "LMAX Disruptor",        use: "High-throughput event pipeline; SPSC/MPSC", complexity: 4, latency: "5–15 ns" },
  { id: "vyukov",    name: "Vyukov MPSC Queue",     use: "Multiple producers, single consumer", complexity: 3, latency: "8–20 ns" },
  { id: "hazard",    name: "Hazard Pointers",       use: "Safe memory reclamation in lock-free structures", complexity: 5, latency: "10–30 ns" },
  { id: "rcu",       name: "Read-Copy-Update (RCU)", use: "Read-mostly data structures; Linux kernel pattern", complexity: 4, latency: "~1–5 ns read" },
];

const PATTERN_DETAIL: Record<Pattern, {
  description: string;
  safetyMechanism: string;
  platforms: string;
  realWorldUse: string;
  code: string;
  pros: string[];
  cons: string[];
}> = {
  seqlock: {
    description:
      "SeqLock (Sequence Lock) is a simple, provably correct pattern for the case of one writer and multiple readers where reads vastly outnumber writes. The writer increments an odd sequence counter before writing and increments it again (to even) after. Readers check the counter before and after reading — if they differ, a write occurred mid-read and the reader retries.",
    safetyMechanism:
      "The odd/even sequence counter provides a non-blocking 'retry on conflict' mechanism. Readers never block writers. The counter increments use acquire/release semantics — this IS the non-CAS safety mechanism the SOV-V24 spec was vaguely describing, but with correct barrier semantics.",
    platforms: "x86-64, ARM64, RISC-V — correct on all architectures with proper acquire/release",
    realWorldUse: "Linux kernel: jiffies, gettimeofday(), NTP updates, network routing table reads. glibc: clock_gettime(). Windows: RtlAcquireSRWLockShared() fast path.",
    pros: [
      "Zero overhead for readers when no write is in progress",
      "Lock-free (readers never block writers)",
      "Works correctly on all memory architectures with acquire/release",
      "Read latency: ~2–4 ns on hot cache (just two atomic loads + the data reads)",
    ],
    cons: [
      "Readers may loop if a writer is active (live-lock under sustained writes)",
      "Cannot be used with pointers (reader might dereference a partially-written pointer)",
      "Write path is serialized (one writer at a time)",
      "Data must be copyable in bounded time (no variable-length structures)",
    ],
    code: `// SeqLock — correct implementation with acquire/release
struct SeqLock {
    std::atomic<uint64_t> seq{0};  // even = stable, odd = write in progress
    alignas(64) Data data;         // 64-byte aligned to prevent false sharing

    // Writer (must be serialized externally — one writer at a time)
    void write(const Data& newData) {
        // Begin write: increment to odd
        seq.fetch_add(1, std::memory_order_release);  // release: readers see seq change
        
        data = newData;  // Write the data (may be multiple stores)
        
        // End write: increment to even
        seq.fetch_add(1, std::memory_order_release);  // release: readers see data
    }

    // Reader — lock-free, may retry
    Data read() {
        Data result;
        uint64_t s1, s2;
        do {
            s1 = seq.load(std::memory_order_acquire);  // acquire: see writer's stores
            if (s1 & 1) { _mm_pause(); continue; }    // odd = write in progress, spin
            
            result = data;  // Read the data
            
            s2 = seq.load(std::memory_order_acquire);  // acquire: re-check sequence
        } while (s1 != s2);  // Retry if sequence changed during read
        return result;
    }
};
// Read cost (uncontended): 2 atomic loads + data copy + branch = ~3–8 ns
// Write cost: 2 fetch_add + data write = ~5–15 ns`,
  },

  disruptor: {
    description:
      "The LMAX Disruptor is a high-performance inter-thread messaging framework based on a pre-allocated ring buffer (circular array). It eliminates GC pressure by using fixed-size, pre-allocated slots and achieves throughput of ~50–100 million ops/sec in optimized configurations. The key insight: separate 'claim sequence' (producer claims slot) from 'publish sequence' (consumer sees slot) using sequence barriers.",
    safetyMechanism:
      "Sequence numbers serve as the coordination primitive. Producers claim slots by atomically incrementing the ring's cursor. Consumers track their own sequence and spin on the producer's published sequence. Each slot has its own sequence number (written only by the producer who claimed it) — this is what the SOV-V24 spec calls 'sequence-shadow validation', but done correctly with full acquire/release semantics.",
    platforms: "x86-64 (LMAX original), Java (original implementation), .NET (Disruptor-net port), C++ (LMAX Disruptor C++)",
    realWorldUse: "LMAX Exchange (FX trading, ~6 million orders/day at <1ms latency). Aeron messaging (used by Adaptive Financial Consulting). Multiple HFT shops.",
    pros: [
      "~50–100M ops/sec throughput; ~5–15 ns per operation",
      "Zero allocation after initialization — GC-immune",
      "Mechanical sympathy: ring buffer fits in L2/L3 cache",
      "Supports SPSC, MPSC, SPMC, MPMC topologies",
    ],
    cons: [
      "Complex to implement correctly (the LMAX paper is 12 pages)",
      "Fixed-capacity: must size the ring buffer at startup",
      "Backpressure is by spin-wait — not suitable for all workloads",
      ".NET port has extra overhead from managed runtime",
    ],
    code: `// Disruptor core concept — producer side (C++ pseudocode)
template<typename T, size_t CAPACITY>
class Disruptor {
    static_assert((CAPACITY & (CAPACITY - 1)) == 0, "Power of 2 required");
    static constexpr size_t MASK = CAPACITY - 1;

    alignas(64) std::atomic<int64_t> cursor{-1};   // published sequence
    alignas(64) std::atomic<int64_t> gatingSeq;    // slowest consumer's sequence

    alignas(64) Entry<T> ring[CAPACITY];  // Pre-allocated, padded entries

public:
    // Producer: claim next slot (SPSC version — no CAS needed)
    int64_t claim(int64_t& expected) {
        return ++cursor_claim;  // Single producer: simple increment
    }
    
    // Producer: publish claimed slot
    void publish(int64_t seq) {
        ring[seq & MASK].sequence.store(seq, std::memory_order_release);
        // ↑ release: consumer sees ring[seq] data before seeing seq in sequence
    }

    // Consumer: poll for next available entry
    bool tryConsume(int64_t seq, T& out) {
        if (ring[seq & MASK].sequence.load(std::memory_order_acquire) == seq) {
            out = ring[seq & MASK].data;
            return true;
        }
        return false;  // not published yet — caller spins
    }
};

// Entry structure — padded to 64 bytes to prevent false sharing
template<typename T>
struct alignas(64) Entry {
    std::atomic<int64_t> sequence{-1};
    T data;
    char _pad[64 - sizeof(std::atomic<int64_t>) - sizeof(T)];
};`,
  },

  vyukov: {
    description:
      "Dmitry Vyukov's MPSC (Multiple-Producer, Single-Consumer) queue is an intrusive linked-list queue that is wait-free for producers and lock-free for the consumer. It was designed for scenarios where many threads need to submit work items to a single worker thread. The algorithm achieves ~8–20 ns per operation in uncontended conditions.",
    safetyMechanism:
      "Each node contains an atomic 'next' pointer. Producers atomically swap the 'tail' pointer with their new node (using XCHG — an atomic exchange, cheaper than CAS). The consumer follows the 'next' chain from the head. The safety invariant: tail always points to the last published node; head always points to the oldest unprocessed node. No ABA problem due to the intrusive (node-per-item) design.",
    platforms: "x86-64 (uses XCHG which is implicitly locked), ARM64 (uses LDAXR/STLXR for the exchange), any platform with atomic exchange",
    realWorldUse: "Linux kernel work queues (conceptually similar), Golang runtime goroutine scheduling queues, Tokio (Rust async runtime) task queues, many HFT order management systems.",
    pros: [
      "Wait-free producers — no CAS retry loop; XCHG always succeeds",
      "No ABA problem — nodes are not recycled in the base algorithm",
      "~8–20 ns uncontended; degrades gracefully under contention",
      "Simple implementation (~30 lines of code)",
    ],
    cons: [
      "Consumer must handle 'inconsistent' state briefly after a push (spin on next pointer)",
      "Not safe with object pools without hazard pointers or epoch-based reclamation",
      "Single consumer only — MPMC requires a different algorithm",
      "Higher per-item overhead than ring-buffer (pointer chasing in cache)",
    ],
    code: `// Vyukov MPSC Queue — correct implementation
struct Node {
    std::atomic<Node*> next{nullptr};
    // ... payload ...
};

struct MPSCQueue {
    alignas(64) std::atomic<Node*> tail;  // producers write here
    alignas(64) Node* head;               // consumer reads here (no sharing)
    Node stub;                            // sentinel node

    MPSCQueue() : head(&stub), tail(&stub) {}

    // Producer — WAIT-FREE (no CAS loop, XCHG always succeeds)
    void push(Node* node) {
        node->next.store(nullptr, std::memory_order_relaxed);
        
        // Atomically swap tail to our node, get previous tail
        Node* prev = tail.exchange(node, std::memory_order_acq_rel);
        
        // Link previous tail to our node
        // Note: there's a brief window where prev->next is null but tail != prev
        // Consumer handles this by spinning on next
        prev->next.store(node, std::memory_order_release);
    }

    // Consumer — may spin briefly if producer is mid-push
    Node* pop() {
        Node* h = head;
        Node* next = h->next.load(std::memory_order_acquire);
        
        if (next == nullptr) return nullptr;  // empty
        
        head = next;  // advance head
        // h is now the old stub/consumed node; return next as new consumed item
        return next;
    }
};
// Producer push: ~8–12 ns (one XCHG + one store)
// Consumer pop: ~4–8 ns uncontended (two loads + pointer update)`,
  },

  hazard: {
    description:
      "Hazard Pointers (Maged Michael, 2004) are a safe memory reclamation scheme for lock-free data structures. The fundamental problem: Thread A reads a pointer to node N; Thread B deletes N and frees the memory; Thread A dereferences N — use-after-free. Hazard Pointers solve this by having each thread 'declare' which pointers it is currently accessing before dereferencing them. Memory is only freed when no thread's hazard list contains a pointer to that memory.",
    safetyMechanism:
      "Each thread maintains a small array of 'hazard pointers' (typically 2–4 per thread). Before dereferencing any shared pointer, a thread writes it to its hazard slot. After a thread finishes with the pointer, it clears the hazard slot. A deleter must scan ALL threads' hazard slots before freeing memory. This provides a non-blocking, fence-based guarantee of memory safety.",
    platforms: "Universal — language-agnostic concept; implemented in C++26 (std::hazard_pointer), Java (Folly::hazptr), Rust (crossbeam hazard_pointer)",
    realWorldUse: "Facebook Folly library (hazptr — used in production at Meta scale). Microsoft STL lock-free containers. PostgreSQL's SLRU cache. Java's java.util.concurrent lock-free structures.",
    pros: [
      "Provably safe memory reclamation — no use-after-free",
      "Non-blocking — no thread can be prevented from making progress",
      "Lower overhead than garbage collection for long-lived lock-free structures",
      "Well-understood safety proofs (Maged Michael's original paper, IBM Research 2004)",
    ],
    cons: [
      "Global scan on reclamation: O(H×R) where H = num threads, R = pending retirements",
      "Small constant overhead per dereference (hazard write + barrier)",
      "Complex to use correctly — easy to forget a hazard slot",
      "C++26 standardization recent — older code uses custom implementations",
    ],
    code: `// Hazard Pointer — simplified single-hazard-per-thread example
// (Real implementations handle N hazard pointers per thread)

thread_local std::atomic<void*> hazard_slot{nullptr};  // per-thread hazard

template<typename T>
T* acquire_hazard(std::atomic<T*>& shared_ptr) {
    T* ptr;
    do {
        ptr = shared_ptr.load(std::memory_order_relaxed);
        
        // Declare hazard BEFORE using ptr
        hazard_slot.store(ptr, std::memory_order_release);
        
        // Re-read: if it changed, our hazard is stale — retry
        if (shared_ptr.load(std::memory_order_acquire) == ptr)
            break;  // stable — safe to use ptr now
    } while (true);
    return ptr;
}

void release_hazard() {
    hazard_slot.store(nullptr, std::memory_order_release);
}

// Safe reclamation: only free when no thread has a hazard to this address
void retire(T* ptr) {
    retired_list.push_back(ptr);
    if (retired_list.size() > THRESHOLD) {
        // Scan all threads' hazard slots
        std::unordered_set<void*> hazards;
        for (auto& slot : all_thread_hazard_slots)
            if (void* h = slot.load(std::memory_order_acquire))
                hazards.insert(h);
        
        // Only free what's not in any hazard slot
        retired_list.erase_if([&](T* p) {
            if (!hazards.count(p)) { delete p; return true; }
            return false;
        });
    }
}`,
  },

  rcu: {
    description:
      "Read-Copy-Update (RCU) is a synchronization mechanism where reads are extremely cheap (zero overhead in the kernel; minimal overhead in userspace) and updates proceed by (1) copying the old data structure, (2) applying changes to the copy, and (3) atomically publishing the new version by updating a shared pointer. The old version is freed only when all ongoing readers have completed their read-side critical section ('grace period').",
    safetyMechanism:
      "Readers execute within a 'read-side critical section' (rcu_read_lock/rcu_read_unlock in Linux — compiler barrier only, no CPU barrier). Writers wait for a 'grace period': a time window during which all pre-existing read-side critical sections have completed. After the grace period, no reader holds a reference to the old data — it's safe to free.",
    platforms: "Linux kernel (native urcu library). Userspace: liburcu (Mathieu Desnoyers et al.), C++ folly::rcu, Rust's rcu-clean crate.",
    realWorldUse: "Linux kernel: network routing tables, dcache, task_struct lookups. liburcu: used in systemd, LTTng, PostgreSQL. Conceptually similar to Java's CopyOnWriteArrayList.",
    pros: [
      "Read-side: near-zero overhead (compiler barrier only in kernel RCU)",
      "Scales to any number of readers — no cache-line contention on reads",
      "No ABA problem — readers reference entire version, not individual pointers",
      "Proven: Linux kernel RCU has been production-hardened since 2002",
    ],
    cons: [
      "Writers must wait for a full grace period (can be milliseconds)",
      "Memory overhead: old version kept alive until grace period expires",
      "Not suitable for write-heavy workloads",
      "Userspace RCU more complex than kernel RCU (no preemption assistance)",
    ],
    code: `// RCU — userspace pattern (urcu-style)
// Read path: near zero overhead
struct Config { int value; /* ... */ };
std::atomic<Config*> g_config{new Config{.value = 42}};

// Reader — fast path (no locks, no barriers on x86 TSO)
void reader() {
    rcu_read_lock();  // compiler barrier only; no CPU fence on x86
    
    Config* cfg = g_config.load(std::memory_order_consume);
    // 'consume': only data-dependent loads are ordered — cheaper than acquire
    // Note: most compilers promote consume to acquire; C++26 may fix this
    
    use(cfg->value);  // safe: grace period guarantees cfg is alive
    
    rcu_read_unlock();  // compiler barrier; allows reclamation to proceed
}

// Writer — slow path (make new version, atomically publish, wait for grace period)
void writer(int new_value) {
    // 1. Read current version
    Config* old_cfg = g_config.load(std::memory_order_acquire);
    
    // 2. Create new version (copy + modify)
    Config* new_cfg = new Config(*old_cfg);
    new_cfg->value = new_value;
    
    // 3. Publish new version atomically
    g_config.store(new_cfg, std::memory_order_release);
    
    // 4. Wait for grace period — all pre-existing readers finish
    synchronize_rcu();  // may block for milliseconds
    
    // 5. Safe to free old version — no reader can access it now
    delete old_cfg;
}
// Read cost: ~1–3 ns (x86, hot cache) — minimal barriers
// Write cost: allocation + copy + store + grace period (~ms) — infrequent writes only`,
  },
};

export default function RealPatterns() {
  const [pattern, setPattern] = useState<Pattern>("seqlock");
  const detail = PATTERN_DETAIL[pattern];
  const meta = PATTERNS.find((p) => p.id === pattern)!;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-black text-white mb-1">Real Lock-Free Patterns</h2>
        <p className="text-gray-400 text-sm">
          Production-proven lock-free algorithms with correct safety semantics, real latency numbers,
          and implementation notes. Each pattern has well-defined safety proofs — unlike the invented
          "bitwise sequence-shadow validation" described in SOV-V24.
        </p>
      </div>

      {/* Pattern selector */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {PATTERNS.map((p) => (
          <button
            key={p.id}
            onClick={() => setPattern(p.id)}
            className={`rounded-xl border p-4 text-left transition-all ${
              pattern === p.id
                ? "border-red-600 bg-red-950/30"
                : "border-gray-700 bg-gray-900 hover:border-gray-600"
            }`}
          >
            <div className={`font-bold text-sm mb-1 ${pattern === p.id ? "text-red-300" : "text-white"}`}>
              {p.name}
            </div>
            <div className="text-gray-500 text-xs mb-2">{p.use}</div>
            <div className="flex items-center justify-between">
              <span className="text-green-400 text-xs font-mono">{p.latency}</span>
              <div className="flex gap-1">
                {Array.from({ length: 5 }).map((_, i) => (
                  <div
                    key={i}
                    className={`w-2 h-2 rounded-full ${
                      i < p.complexity ? "bg-yellow-500" : "bg-gray-700"
                    }`}
                  />
                ))}
              </div>
            </div>
          </button>
        ))}
      </div>

      {/* Detail */}
      <div className="space-y-5">
        {/* Description */}
        <div className="rounded-xl border border-gray-700 bg-gray-900 p-5">
          <div className="text-white font-black text-lg mb-3">{meta.name}</div>
          <p className="text-gray-300 text-sm leading-relaxed">{detail.description}</p>
        </div>

        {/* Safety mechanism */}
        <div className="rounded-xl border border-blue-800 bg-blue-950/20 p-5">
          <div className="text-blue-400 font-bold mb-2">🔒 Safety Mechanism</div>
          <p className="text-gray-300 text-sm leading-relaxed">{detail.safetyMechanism}</p>
          <div className="mt-3 text-xs text-gray-500">
            <strong className="text-gray-400">Platforms:</strong> {detail.platforms}
          </div>
          <div className="mt-1 text-xs text-gray-500">
            <strong className="text-gray-400">Real-world use:</strong> {detail.realWorldUse}
          </div>
        </div>

        {/* Code */}
        <div className="rounded-xl border border-gray-700 overflow-hidden">
          <div className="bg-gray-800 px-4 py-2 flex items-center gap-2">
            <div className="w-3 h-3 rounded-full bg-red-500" />
            <div className="w-3 h-3 rounded-full bg-yellow-500" />
            <div className="w-3 h-3 rounded-full bg-green-500" />
            <span className="text-gray-400 text-xs ml-2">{meta.name} — Correct Implementation</span>
          </div>
          <pre className="bg-gray-950 p-4 text-xs text-green-300 leading-relaxed overflow-x-auto whitespace-pre font-mono">
            {detail.code}
          </pre>
        </div>

        {/* Pros / Cons */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="rounded-xl border border-green-800 bg-green-950/20 p-4">
            <div className="text-green-400 font-bold mb-2">✅ Strengths</div>
            <ul className="space-y-1">
              {detail.pros.map((p, i) => (
                <li key={i} className="flex gap-2 text-sm text-gray-300">
                  <span className="text-green-500 flex-shrink-0">+</span>
                  <span>{p}</span>
                </li>
              ))}
            </ul>
          </div>
          <div className="rounded-xl border border-red-800 bg-red-950/20 p-4">
            <div className="text-red-400 font-bold mb-2">⚠️ Limitations</div>
            <ul className="space-y-1">
              {detail.cons.map((c, i) => (
                <li key={i} className="flex gap-2 text-sm text-gray-300">
                  <span className="text-red-500 flex-shrink-0">−</span>
                  <span>{c}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>

      {/* Summary table */}
      <div>
        <div className="text-gray-400 text-xs font-bold uppercase tracking-wider mb-3">Pattern Comparison Matrix</div>
        <div className="rounded-xl border border-gray-800 overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="bg-gray-900 text-gray-400 border-b border-gray-800">
                <th className="text-left px-4 py-3 font-bold">Pattern</th>
                <th className="text-center px-4 py-3 font-bold">Writers</th>
                <th className="text-center px-4 py-3 font-bold">Readers</th>
                <th className="text-center px-4 py-3 font-bold">Wait-Free?</th>
                <th className="text-center px-4 py-3 font-bold">Read ns</th>
                <th className="text-center px-4 py-3 font-bold">Write ns</th>
                <th className="text-center px-4 py-3 font-bold">Memory Safety</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800/50">
              {[
                { name: "SeqLock",          writers: "1",   readers: "N", wf: "No*",  rns: "3–8",   wns: "5–15",   ms: "Trivial"           },
                { name: "LMAX Disruptor",   writers: "1–N", readers: "N", wf: "Yes",  rns: "5–15",  wns: "5–20",   ms: "Pre-allocated"     },
                { name: "Vyukov MPSC",      writers: "N",   readers: "1", wf: "Yes*", rns: "4–8",   wns: "8–20",   ms: "Hazard / Epoch"    },
                { name: "Hazard Pointers",  writers: "N",   readers: "N", wf: "Yes",  rns: "5–15",  wns: "20–100", ms: "Hazard pointers"   },
                { name: "RCU",             writers: "1",   readers: "N", wf: "Yes",  rns: "1–3",   wns: "ms",     ms: "Grace period"      },
                { name: "std::mutex",       writers: "N",   readers: "N", wf: "No",   rns: "20–500",wns: "20–500", ms: "Trivial"           },
              ].map((row) => (
                <tr key={row.name} className="bg-gray-950 hover:bg-gray-900">
                  <td className="px-4 py-3 text-white font-bold">{row.name}</td>
                  <td className="px-4 py-3 text-center text-gray-300">{row.writers}</td>
                  <td className="px-4 py-3 text-center text-gray-300">{row.readers}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-0.5 rounded text-xs font-bold ${
                      row.wf.startsWith("Yes") ? "bg-green-900/40 text-green-400" : "bg-red-900/40 text-red-400"
                    }`}>{row.wf}</span>
                  </td>
                  <td className="px-4 py-3 text-center text-green-400 font-mono">{row.rns}</td>
                  <td className="px-4 py-3 text-center text-yellow-400 font-mono">{row.wns}</td>
                  <td className="px-4 py-3 text-center text-gray-400">{row.ms}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="text-gray-600 text-xs mt-2">* SeqLock readers may retry (not wait-free); Vyukov producers are wait-free; consumer may spin on push in progress.</p>
      </div>
    </div>
  );
}
