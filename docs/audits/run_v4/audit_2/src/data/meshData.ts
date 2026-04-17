export type NodeRole = "Ingress" | "Router" | "Transform" | "Actor" | "Egress" | "Mirror";

export interface MeshNode {
  id: string;
  label: string;
  role: NodeRole;
  corePin: number; // 0-indexed core
  slabPool: string;
  cyclesBudget: number; // ns
  priority: "hot" | "warm" | "cold";
  description: string;
}

export interface SPSCWire {
  id: string;
  from: string;
  to: string;
  latencyNs: number;
  ringSize: number; // entries (power of 2)
  cacheLevel: "L1" | "L2" | "L3";
  label: string;
}

export interface LatencyHop {
  hop: string;
  budget: number; // ns
  mechanism: string;
  cacheState: string;
  guarantee: string;
}

export interface CoreMapping {
  core: number;
  mode12: string[];
  mode6: string[];
  mode4: string[];
  affinity: "pinned" | "shared";
  priority: "hot" | "warm" | "cold";
}

// ── 12-Station Mesh Nodes ────────────────────────────────────────────────────
export const MESH_NODES: MeshNode[] = [
  {
    id: "ING-0",
    label: "Ingress-0",
    role: "Ingress",
    corePin: 0,
    slabPool: "SLAB_ING[256×64B]",
    cyclesBudget: 400,
    priority: "hot",
    description: "External packet capture. Kernel-bypass NIC via DPDK. Zero-copy DMA into slab.",
  },
  {
    id: "ING-1",
    label: "Ingress-1",
    role: "Ingress",
    corePin: 1,
    slabPool: "SLAB_ING[256×64B]",
    cyclesBudget: 400,
    priority: "hot",
    description: "Secondary ingress lane. Hot-standby mirror path. L1-resident ring head.",
  },
  {
    id: "RTR-0",
    label: "Router-0",
    role: "Router",
    corePin: 2,
    slabPool: "SLAB_RTR[512×32B]",
    cyclesBudget: 600,
    priority: "hot",
    description: "Lock-free routing table (CRC32 key). Dispatches frames to Transform shards.",
  },
  {
    id: "RTR-1",
    label: "Router-1",
    role: "Router",
    corePin: 3,
    slabPool: "SLAB_RTR[512×32B]",
    cyclesBudget: 600,
    priority: "hot",
    description: "Overflow router shard. Handles burst spillover from RTR-0 via SPSC handoff.",
  },
  {
    id: "TRF-0",
    label: "Transform-0",
    role: "Transform",
    corePin: 4,
    slabPool: "SLAB_TRF[128×128B]",
    cyclesBudget: 1200,
    priority: "warm",
    description: "Protocol normalization, header stripping, checksum verify. SIMD AVX-512.",
  },
  {
    id: "TRF-1",
    label: "Transform-1",
    role: "Transform",
    corePin: 5,
    slabPool: "SLAB_TRF[128×128B]",
    cyclesBudget: 1200,
    priority: "warm",
    description: "Payload transform lane. Encryption / decompression via hardware offload.",
  },
  {
    id: "ACT-0",
    label: "Actor-0",
    role: "Actor",
    corePin: 6,
    slabPool: "SLAB_ACT[64×256B]",
    cyclesBudget: 2000,
    priority: "hot",
    description: "Business logic reactor. Deterministic state machine. No allocations inside loop.",
  },
  {
    id: "ACT-1",
    label: "Actor-1",
    role: "Actor",
    corePin: 7,
    slabPool: "SLAB_ACT[64×256B]",
    cyclesBudget: 2000,
    priority: "hot",
    description: "Secondary actor for parallel state partitions. Isolated slab, no shared state.",
  },
  {
    id: "EGR-0",
    label: "Egress-0",
    role: "Egress",
    corePin: 8,
    slabPool: "SLAB_EGR[256×64B]",
    cyclesBudget: 800,
    priority: "warm",
    description: "Output serializer. Frames queued for kernel-bypass TX via DPDK burst-send.",
  },
  {
    id: "EGR-1",
    label: "Egress-1",
    role: "Egress",
    corePin: 9,
    slabPool: "SLAB_EGR[256×64B]",
    cyclesBudget: 800,
    priority: "warm",
    description: "Fallback egress lane. Handles retransmit and ACK frames on separate wire.",
  },
  {
    id: "MIR-0",
    label: "Mirror-0",
    role: "Mirror",
    corePin: 10,
    slabPool: "SLAB_MIR[1024×64B]",
    cyclesBudget: 1500,
    priority: "cold",
    description: "Async telemetry tap. Writes to shared-memory ring for out-of-band observability.",
  },
  {
    id: "MIR-1",
    label: "Mirror-1",
    role: "Mirror",
    corePin: 11,
    slabPool: "SLAB_MIR[1024×64B]",
    cyclesBudget: 1500,
    priority: "cold",
    description: "Replay recorder. Persists frames to mmap'd log file. Bounded by disk I/O, not CPU.",
  },
];

// ── SPSC Wire Topology ───────────────────────────────────────────────────────
export const SPSC_WIRES: SPSCWire[] = [
  { id: "W01", from: "ING-0", to: "RTR-0", latencyNs: 320, ringSize: 256, cacheLevel: "L1", label: "ING0→RTR0" },
  { id: "W02", from: "ING-1", to: "RTR-1", latencyNs: 320, ringSize: 256, cacheLevel: "L1", label: "ING1→RTR1" },
  { id: "W03", from: "RTR-0", to: "TRF-0", latencyNs: 480, ringSize: 512, cacheLevel: "L1", label: "RTR0→TRF0" },
  { id: "W04", from: "RTR-0", to: "TRF-1", latencyNs: 520, ringSize: 512, cacheLevel: "L2", label: "RTR0→TRF1" },
  { id: "W05", from: "RTR-1", to: "TRF-0", latencyNs: 520, ringSize: 512, cacheLevel: "L2", label: "RTR1→TRF0" },
  { id: "W06", from: "RTR-1", to: "TRF-1", latencyNs: 480, ringSize: 512, cacheLevel: "L1", label: "RTR1→TRF1" },
  { id: "W07", from: "TRF-0", to: "ACT-0", latencyNs: 640, ringSize: 128, cacheLevel: "L2", label: "TRF0→ACT0" },
  { id: "W08", from: "TRF-1", to: "ACT-1", latencyNs: 640, ringSize: 128, cacheLevel: "L2", label: "TRF1→ACT1" },
  { id: "W09", from: "ACT-0", to: "EGR-0", latencyNs: 380, ringSize: 256, cacheLevel: "L1", label: "ACT0→EGR0" },
  { id: "W10", from: "ACT-1", to: "EGR-1", latencyNs: 380, ringSize: 256, cacheLevel: "L1", label: "ACT1→EGR1" },
  { id: "W11", from: "ACT-0", to: "MIR-0", latencyNs: 900, ringSize: 1024, cacheLevel: "L3", label: "ACT0→MIR0" },
  { id: "W12", from: "ACT-1", to: "MIR-1", latencyNs: 900, ringSize: 1024, cacheLevel: "L3", label: "ACT1→MIR1" },
  { id: "W13", from: "EGR-0", to: "MIR-0", latencyNs: 1100, ringSize: 1024, cacheLevel: "L3", label: "EGR0→MIR0" },
  { id: "W14", from: "EGR-1", to: "MIR-1", latencyNs: 1100, ringSize: 1024, cacheLevel: "L3", label: "EGR1→MIR1" },
];

// ── Latency Budget per Hop ───────────────────────────────────────────────────
export const LATENCY_HOPS: LatencyHop[] = [
  {
    hop: "NIC DMA → ING Slab",
    budget: 400,
    mechanism: "DPDK zero-copy DMA + slab slot claim (CAS)",
    cacheState: "Cold DRAM → L1 fill",
    guarantee: "< 400 ns (bounded by PCIe Gen4 DMA)",
  },
  {
    hop: "ING → RTR (SPSC Ring)",
    budget: 320,
    mechanism: "Store-release head ptr, load-acquire tail ptr",
    cacheState: "L1-hot ring (256×64B = 16KB)",
    guarantee: "< 320 ns (1 cache-line publish)",
  },
  {
    hop: "RTR Dispatch Logic",
    budget: 600,
    mechanism: "CRC32 key lookup → branch-free dispatch table",
    cacheState: "L1-resident table (512×32B = 16KB)",
    guarantee: "< 600 ns (4 cycles CRC32C + table read)",
  },
  {
    hop: "RTR → TRF (SPSC Ring)",
    budget: 520,
    mechanism: "Cross-core SPSC with MESI exclusive line ownership",
    cacheState: "L2-warm (same NUMA node, diff cluster)",
    guarantee: "< 520 ns (L2 hit ~12 cycles @ 4GHz)",
  },
  {
    hop: "TRF AVX-512 Pipeline",
    budget: 1200,
    mechanism: "In-place transform on slab ptr, no copy. AVX-512 vectorized.",
    cacheState: "L2-resident frame (128B payload)",
    guarantee: "< 1200 ns (256-byte frame, 8 SIMD ops)",
  },
  {
    hop: "TRF → ACT (SPSC Ring)",
    budget: 640,
    mechanism: "Pointer handoff only (8 bytes). Zero payload copy.",
    cacheState: "L2-warm pointer",
    guarantee: "< 640 ns (ptr publish, single cache-line)",
  },
  {
    hop: "ACT State Machine",
    budget: 2000,
    mechanism: "Deterministic FSM, 64-entry slab. No branches on hot path.",
    cacheState: "L1-pinned state table",
    guarantee: "< 2000 ns (worst case 10-state traversal)",
  },
  {
    hop: "ACT → EGR (SPSC Ring)",
    budget: 380,
    mechanism: "Store-release ptr + DPDK TX burst hint",
    cacheState: "L1-hot",
    guarantee: "< 380 ns",
  },
  {
    hop: "EGR TX Burst",
    budget: 800,
    mechanism: "DPDK rte_eth_tx_burst, 32-frame batch",
    cacheState: "L1→PCIe write-combine",
    guarantee: "< 800 ns (PCIe Gen4, 32B header)",
  },
  {
    hop: "Mirror Tap (async)",
    budget: 1500,
    mechanism: "Relaxed-store to L3 ring. No ACK. Fire-and-forget.",
    cacheState: "L3 / DRAM (best-effort)",
    guarantee: "< 1500 ns (non-blocking, no back-pressure on hot path)",
  },
];

// Total hot-path: ING→RTR→TRF→ACT→EGR = 400+320+600+520+1200+640+2000+380+800 = 6860 ns < 10 µs ✓

// ── Hardware Sharding / Core Mapping ────────────────────────────────────────
export const CORE_MAPPINGS: CoreMapping[] = [
  { core: 0,  mode12: ["ING-0"],  mode6: ["ING-0"],          mode4: ["ING-0"],          affinity: "pinned", priority: "hot"  },
  { core: 1,  mode12: ["ING-1"],  mode6: ["ING-1"],          mode4: ["ING-1"],          affinity: "pinned", priority: "hot"  },
  { core: 2,  mode12: ["RTR-0"],  mode6: ["RTR-0", "RTR-1"], mode4: ["RTR-0", "RTR-1"], affinity: "pinned", priority: "hot"  },
  { core: 3,  mode12: ["RTR-1"],  mode6: ["TRF-0", "TRF-1"], mode4: ["TRF-0", "TRF-1"], affinity: "pinned", priority: "hot"  },
  { core: 4,  mode12: ["TRF-0"],  mode6: ["ACT-0"],          mode4: ["ACT-0"],          affinity: "pinned", priority: "hot"  },
  { core: 5,  mode12: ["TRF-1"],  mode6: ["ACT-1", "EGR-0", "MIR-0", "MIR-1"], mode4: ["ACT-1", "EGR-0", "EGR-1", "MIR-0", "MIR-1"], affinity: "pinned", priority: "warm" },
  { core: 6,  mode12: ["ACT-0"],  mode6: [],                 mode4: [],                 affinity: "pinned", priority: "hot"  },
  { core: 7,  mode12: ["ACT-1"],  mode6: [],                 mode4: [],                 affinity: "pinned", priority: "hot"  },
  { core: 8,  mode12: ["EGR-0"],  mode6: [],                 mode4: [],                 affinity: "pinned", priority: "warm" },
  { core: 9,  mode12: ["EGR-1"],  mode6: [],                 mode4: [],                 affinity: "pinned", priority: "warm" },
  { core: 10, mode12: ["MIR-0"],  mode6: [],                 mode4: [],                 affinity: "shared", priority: "cold" },
  { core: 11, mode12: ["MIR-1"],  mode6: [],                 mode4: [],                 affinity: "shared", priority: "cold" },
];

export const TOTAL_HOT_PATH_NS = 6860;
export const GATE_NS = 10000;
export const HEADROOM_NS = GATE_NS - TOTAL_HOT_PATH_NS;
