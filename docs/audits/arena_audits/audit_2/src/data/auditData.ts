// ============================================================
// ANTIGRAVITY NEXUS OS — AUDIT DATA LAYER
// Sovereign Actor v2 | Zero-Heap / Open Pipes
// ============================================================

export type Verdict = "RESOLVED" | "PARTIAL" | "UNRESOLVED" | "CRITICAL" | "INFO";

export interface FindingRow {
  id: string;
  layer: string;
  mechanism: string;
  jitterBefore: string;
  jitterAfter: string;
  verdict: Verdict;
  notes: string;
}

export interface HiddenLock {
  id: string;
  path: string;
  lockType: string;
  magnitude: string;
  trigger: string;
  mitigation: string;
  residual: Verdict;
}

export interface PipeStage {
  id: number;
  label: string;
  sublabel: string;
  latency: string;
  color: string;
  locked: boolean;
  warning?: string;
}

export interface SlabBlock {
  id: string;
  name: string;
  size: string;
  count: number;
  status: "pre-allocated" | "active" | "free" | "danger";
  notes: string;
}

export interface ScoreCategory {
  label: string;
  before: number;
  after: number;
  maxPoints: number;
  notes: string;
}

// ─── PIPE STAGES ────────────────────────────────────────────
export const pipeStages: PipeStage[] = [
  {
    id: 1,
    label: "Engine Emit",
    sublabel: "Raw market data",
    latency: "~0ns",
    color: "cyan",
    locked: true,
  },
  {
    id: 2,
    label: "SharedArrayBuffer",
    sublabel: "Lock-free ring buffer",
    latency: "<100ns",
    color: "cyan",
    locked: true,
  },
  {
    id: 3,
    label: "Atomics.wait()",
    sublabel: "Kernel futex",
    latency: "0.3–1.2µs",
    color: "yellow",
    locked: false,
    warning: "Futex syscall — unavoidable kernel boundary crossing. ~0.3–1.2µs per wake.",
  },
  {
    id: 4,
    label: "Slab Pool Alloc",
    sublabel: "Pre-allocated slot claim",
    latency: "<50ns",
    color: "cyan",
    locked: true,
  },
  {
    id: 5,
    label: "Worker Thread",
    sublabel: "Isolated core execution",
    latency: "~0ns*",
    color: "cyan",
    locked: true,
  },
  {
    id: 6,
    label: "structuredClone / postMessage",
    sublabel: "IPC serialization",
    latency: "8–40µs",
    color: "red",
    locked: false,
    warning:
      "CRITICAL REMAINING FRICTION: V8 serialization codec acquires internal heap lock. Scales with payload size. Cannot be eliminated without SharedArrayBuffer-only IPC.",
  },
  {
    id: 7,
    label: "Redis Lua Script",
    sublabel: "Atomic persistence",
    latency: "80–300µs",
    color: "green",
    locked: true,
  },
  {
    id: 8,
    label: "Hardware FIFO",
    sublabel: "NIC / kernel socket",
    latency: "varies",
    color: "green",
    locked: true,
  },
];

// ─── SLUB FINDINGS ──────────────────────────────────────────
export const findings: FindingRow[] = [
  {
    id: "F-01",
    layer: "Kernel / Memory",
    mechanism: "mlockall(MCL_CURRENT | MCL_FUTURE)",
    jitterBefore: "5–15µs",
    jitterAfter: "0µs",
    verdict: "RESOLVED",
    notes:
      "Eliminates TLB miss + page-fault jitter from demand paging. All pages pinned in physical RAM. Confirmed effective against SLUB demand-path latency.",
  },
  {
    id: "F-02",
    layer: "Kernel / Memory",
    mechanism: "Custom Slab Pool (pre-alloc at startup)",
    jitterBefore: "5–15µs",
    jitterAfter: "<50ns",
    verdict: "RESOLVED",
    notes:
      "Hot-path allocations now claim a pre-warmed slot via atomic index bump. SLUB allocator is bypassed entirely on the critical path. Startup cost is front-loaded; runtime cost is negligible.",
  },
  {
    id: "F-03",
    layer: "Redis / Persistence",
    mechanism: "Atomic Lua Scripts (EVALSHA)",
    jitterBefore: "Race window: ~2–8µs",
    jitterAfter: "0µs (atomic)",
    verdict: "RESOLVED",
    notes:
      "Multi-account coordination collapsed into a single Redis script execution. Eliminates WATCH/MULTI/EXEC retry loops and their variable-latency race windows.",
  },
  {
    id: "F-04",
    layer: "Node.js / V8",
    mechanism: "postMessage() serialization lock",
    jitterBefore: "N/A (newly identified)",
    jitterAfter: "8–40µs (UNRESOLVED)",
    verdict: "CRITICAL",
    notes:
      "V8's structured clone codec acquires an internal heap mutex during serialization. This is a hidden kernel-adjacent lock within the JS runtime itself. Not addressed by mlockall or Slab Pool.",
  },
  {
    id: "F-05",
    layer: "Node.js / V8",
    mechanism: "GC safepoint during worker_thread emit",
    jitterBefore: "N/A (newly identified)",
    jitterAfter: "0–80µs (probabilistic)",
    verdict: "PARTIAL",
    notes:
      "V8 Stop-the-World GC safepoints can pause all threads including workers. mlockall does not suppress GC. Slab pool reduces allocation frequency (less GC pressure) but does not eliminate GC pauses entirely.",
  },
  {
    id: "F-06",
    layer: "OS / Scheduler",
    mechanism: "SCHED_FIFO / CPU pinning",
    jitterBefore: "0–200µs",
    jitterAfter: "0µs (if applied)",
    verdict: "INFO",
    notes:
      "Not explicitly listed in the remediation stack. If worker threads are not pinned with SCHED_FIFO + cpu affinity, scheduler preemption can introduce 50–200µs jitter independent of all memory fixes.",
  },
];

// ─── HIDDEN LOCKS ────────────────────────────────────────────
export const hiddenLocks: HiddenLock[] = [
  {
    id: "HL-01",
    path: "worker.postMessage(obj)",
    lockType: "V8 Heap Mutex (internal)",
    magnitude: "8–40µs",
    trigger: "Any postMessage() call with non-SAB payload",
    mitigation:
      "Replace with SharedArrayBuffer + Atomics for all hot-path IPC. postMessage only for control signals (strings/numbers).",
    residual: "CRITICAL",
  },
  {
    id: "HL-02",
    path: "V8 GC Safepoint",
    lockType: "Stop-the-World (STW) Pause",
    magnitude: "0–80µs (probabilistic)",
    trigger: "Heap growth triggers incremental/major GC cycle",
    mitigation:
      "Slab pool dramatically reduces heap churn. --max-old-space-size cap + --expose-gc for forced idle-time GC. Residual risk remains.",
    residual: "PARTIAL",
  },
  {
    id: "HL-03",
    path: "Atomics.wait() → futex(2)",
    lockType: "Kernel Futex Syscall",
    magnitude: "0.3–1.2µs",
    trigger: "Every inter-thread wake event",
    mitigation:
      "Unavoidable. This is the hardware/kernel boundary cost of any IPC. Spin-wait (Atomics.waitAsync loop) can reduce to ~100ns but burns a core.",
    residual: "PARTIAL",
  },
  {
    id: "HL-04",
    path: "require() / dynamic import()",
    lockType: "Module Cache Lock (CommonJS)",
    magnitude: "1–10ms (startup only)",
    trigger: "Any dynamic require() on the hot path",
    mitigation:
      "All requires must be at module init. Hot path must be 100% synchronous with pre-loaded references. Not a runtime concern if enforced.",
    residual: "INFO",
  },
  {
    id: "HL-05",
    path: "console.log() / process.stdout",
    lockType: "libuv I/O Lock",
    magnitude: "2–50µs",
    trigger: "Any logging on the hot path",
    mitigation:
      "All hot-path logging must be disabled or routed to a dedicated async log worker via SAB ring buffer. Never log synchronously on a trading thread.",
    residual: "INFO",
  },
];

// ─── SLAB POOL LAYOUT ────────────────────────────────────────
export const slabBlocks: SlabBlock[] = [
  {
    id: "SB-01",
    name: "Order Object Pool",
    size: "256B × 65,536",
    count: 65536,
    status: "pre-allocated",
    notes: "Fixed-size order structs. Zero heap alloc on hot path.",
  },
  {
    id: "SB-02",
    name: "Market Tick Ring",
    size: "128B × 131,072",
    count: 131072,
    status: "active",
    notes: "Circular buffer for raw tick data. Overwrite semantics.",
  },
  {
    id: "SB-03",
    name: "Engine Message Pool",
    size: "512B × 32,768",
    count: 32768,
    status: "pre-allocated",
    notes: "Inter-engine payload buffers. Claimed by atomic fetch-add.",
  },
  {
    id: "SB-04",
    name: "Redis Lua Response",
    size: "1KB × 4,096",
    count: 4096,
    status: "pre-allocated",
    notes: "Pre-warmed response buffers for Lua script returns.",
  },
  {
    id: "SB-05",
    name: "GC Escape Objects",
    size: "Dynamic",
    count: 0,
    status: "danger",
    notes:
      "DANGER: Any object not drawn from a slab pool escapes to V8 heap. Triggers GC pressure. Must be zero on hot path.",
  },
];

// ─── SCORE MATRIX ────────────────────────────────────────────
export const scoreMatrix: ScoreCategory[] = [
  {
    label: "SLUB / Heap Allocator Jitter",
    before: 18,
    after: 25,
    maxPoints: 25,
    notes: "mlockall + Slab Pool fully resolves 5–15µs SLUB contention.",
  },
  {
    label: "IPC Serialization Latency",
    before: 8,
    after: 14,
    maxPoints: 25,
    notes:
      "postMessage hidden lock (HL-01) partially resolved. Requires SAB-only hot path to reach 25/25.",
  },
  {
    label: "Redis Coordination Atomicity",
    before: 15,
    after: 25,
    maxPoints: 25,
    notes: "Lua atomic scripts fully resolve multi-account race windows.",
  },
  {
    label: "OS / Scheduler Isolation",
    before: 12,
    after: 18,
    maxPoints: 25,
    notes:
      "SCHED_FIFO + CPU affinity not confirmed in remediation stack. 7 points locked behind scheduler config.",
  },
];
