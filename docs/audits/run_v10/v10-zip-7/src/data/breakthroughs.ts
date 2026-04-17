export interface Breakthrough {
  round: string;
  name: string;
  latency: number;
  category: 'dispatch' | 'recovery' | 'memory' | 'cache' | 'queue';
  description: string;
  active: boolean;
}

export const breakthroughs: Breakthrough[] = [
  {
    round: 'V7',
    name: 'Bitwise Jump-Dispatch',
    latency: 490,
    category: 'dispatch',
    description: 'Eliminated branch prediction misses via bitwise operation tables for handler resolution.',
    active: true,
  },
  {
    round: 'V7',
    name: 'Mesh Self-Repair Matrix',
    latency: 320,
    category: 'recovery',
    description: 'Topology-aware mesh where each node maintains repair vectors for adjacent nodes.',
    active: true,
  },
  {
    round: 'V8',
    name: 'Ring-Bus SPSC Queue',
    latency: 487,
    category: 'queue',
    description: 'Single-producer single-consumer lock-free queue on a ring-bus topology.',
    active: true,
  },
  {
    round: 'V8',
    name: '1000ns Autonomous Error Recovery',
    latency: 500,
    category: 'recovery',
    description: 'Periodic health checks at 1μs intervals with automatic failover.',
    active: true,
  },
  {
    round: 'V9',
    name: 'FPGA-Parity Bitwise Pass',
    latency: 243,
    category: 'dispatch',
    description: 'Hardware-grade parity validation in a single bitwise pass — raw speed champion.',
    active: true,
  },
  {
    round: 'V9',
    name: 'Memory-Mapped Uint32 Arena',
    latency: 250,
    category: 'memory',
    description: 'Flat, indexable memory surface with zero-contention partitioned worker regions.',
    active: true,
  },
  {
    round: 'V9',
    name: 'Branchless Dispatch Gate',
    latency: 250,
    category: 'dispatch',
    description: 'Conditional-move based dispatch eliminating all branch mispredictions.',
    active: true,
  },
  {
    round: 'V9',
    name: 'L1-D Cache Pre-Touch',
    latency: 250,
    category: 'cache',
    description: 'Prefetch critical dispatch paths into L1 data cache before execution.',
    active: true,
  },
];

export interface V10Design {
  name: string;
  targetLatency: number;
  mechanism: {
    name: string;
    projectedNs: number;
    breakdown: { label: string; saving: number }[];
    description: string;
  };
  textRendering: {
    winner: string;
    alternatives: { name: string; pros: string; cons: string }[];
    reasoning: string;
  };
  workerRecovery: {
    approach: string;
    latencyEstimate: number;
    steps: string[];
    description: string;
  };
  v9Verdict: {
    base: string;
    reasoning: string[];
  };
}

export const v10Design: V10Design = {
  name: 'Phantom Gate',
  targetLatency: 180,
  mechanism: {
    name: 'Userspace Ring + CPU Pinning + IRQ Isolation',
    projectedNs: 180,
    breakdown: [
      { label: 'V9 FPGA-Parity baseline', saving: 0 },
      { label: 'Eliminate kernel transitions (pure userspace ring)', saving: -40 },
      { label: 'IRQ isolation prevents L1 cache pollution', saving: -15 },
      { label: 'NUMA-local allocation eliminates cross-socket penalty', saving: -8 },
    ],
    description:
      'Pure userspace ring buffer with zero syscalls. Each worker is CPU-pinned to an isolated core with IRQ affinity masks clearing all interrupts except NMI. The Arena memory from V9 is allocated NUMA-local, and the FPGA-Parity gate validates dispatch slots inline — no kernel boundary ever crossed. Combined with L1-D Pre-Touch from V9, the critical path fits entirely in L1 with deterministic timing.',
  },
  textRendering: {
    winner: '@chenglou/pretext',
    alternatives: [
      {
        name: '@chenglou/pretext',
        pros: 'Pure JS text measurement via browser font engine. Zero layout/reflow. Pre-computes glyph metrics. DOM updates only change textContent with known widths.',
        cons: 'Newer library, smaller ecosystem. Requires font metric precomputation step.',
      },
      {
        name: 'Canvas 2D',
        pros: 'Immediate mode — no DOM overhead. measureText() is fast for simple cases.',
        cons: 'Full canvas redraw per frame. No subpixel text rendering on some platforms. Accessibility is lost.',
      },
      {
        name: 'OffscreenCanvas Worker',
        pros: 'Off-main-thread rendering. No jank from compute.',
        cons: 'postMessage serialization adds 50-200μs per frame. Bitmap transfer overhead. Overkill for numeric-only updates.',
      },
    ],
    reasoning:
      'For streaming numeric metrics at 60fps, pretext wins decisively. Numeric data uses fixed-width digits (monospace or tabular-nums), meaning glyph widths are constant and pre-computable. pretext measures text purely in JS using the browser font engine without ever calling getBoundingClientRect() or triggering layout recalculation. DOM updates become textContent-only mutations with zero reflow — the browser skips layout entirely because widths are invariant. Canvas requires full redraw and loses accessibility. OffscreenCanvas adds worker messaging overhead that exceeds the layout cost it tries to avoid.',
  },
  workerRecovery: {
    approach: 'Neighbor-Watch CAS Protocol via SharedArrayBuffer',
    latencyEstimate: 350,
    steps: [
      'Each worker owns a 64-byte-aligned cache line in SharedArrayBuffer (prevents false sharing)',
      'Worker writes monotonic timestamp via Atomics.store() after each dispatch cycle (~20ns)',
      'Adjacent workers (ring topology) check neighbor via Atomics.load() — no lock, no coordinator (~30ns)',
      'If neighbor timestamp is stale by >2 dispatch cycles, detector executes Atomics.compareExchange() to claim slot (~50ns)',
      'Claiming worker re-enqueues dead worker\'s pending items to ring-bus SPSC queue (~100ns)',
      'New worker spawned into claimed slot with Arena memory region intact (~150ns amortized)',
    ],
    description:
      'Zero central coordination. Each worker monitors exactly one neighbor in a ring topology — 12 workers form a watch-ring. Detection uses two atomic operations (load + compare). Recovery uses one CAS to atomically claim the dead slot, preventing duplicate recovery. Total worst-case: 350ns from stall detection to work re-enqueue. This beats V8\'s 1000ns periodic check by 2.85× while eliminating the supervisor process entirely.',
  },
  v9Verdict: {
    base: 'Memory-Mapped Uint32 Arena (Option B)',
    reasoning: [
      'Composability: The Uint32 Arena provides a flat, indexable memory substrate that any dispatch mechanism — including FPGA-Parity — can target as a validation layer on top of arena-allocated slots, making it the more composable foundation.',
      'Scalability: Arena partitioning gives each of 12 workers a dedicated, contention-free memory region via simple offset math (worker_id × region_size), while FPGA-Parity alone requires a shared bitstream that serializes validation at scale.',
      'Layering: The two approaches are orthogonal — Arena provides the memory surface, FPGA-Parity provides the dispatch validation gate — so V10 layers both, with Arena as the base and Parity as an inline validation pass over arena slots, capturing 243ns speed with 12-worker scalability.',
    ],
  },
};
