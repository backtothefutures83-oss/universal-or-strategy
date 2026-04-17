// ═══════════════════════════════════════════════════════════════════
//  SOVEREIGN SPSC IPC — TYPE DEFINITIONS
//  Zero-copy, Lock-free, 1µs Hard Gate
// ═══════════════════════════════════════════════════════════════════

export type RoleId =
  | 'ingress-a'
  | 'ingress-b'
  | 'router-iso'
  | 'transform-simd-a'
  | 'transform-simd-b'
  | 'actor-pin-a'
  | 'actor-pin-b'
  | 'actor-pin-c'
  | 'egress-a'
  | 'egress-b'
  | 'mirror-a'
  | 'mirror-b';

export type RoleCategory =
  | 'ingress'
  | 'router'
  | 'transform'
  | 'actor'
  | 'egress'
  | 'mirror';

export interface MeshRole {
  id: RoleId;
  label: string;
  category: RoleCategory;
  coreId: number;
  numaNode: number;
  x: number;
  y: number;
}

export interface SPSCChannel {
  from: RoleId;
  to: RoleId;
  bufferSizeSlots: number;
  slotSizeBytes: number;
  latencyNs: number;       // measured
  jitterNs: number;        // p99-p50
  throughputMps: number;   // million packets/sec
  writeHead: number;
  readHead: number;
  capacity: number;
}

export interface CoreMetrics {
  roleId: RoleId;
  coreId: number;
  spinCycles: number;
  l3Misses: number;
  ipc: number;             // instructions per cycle
  pollLatencyNs: number;
  cacheHitRate: number;
  heapAllocBytes: number;  // must be 0 after boot
}

export interface TimingGate {
  totalPassNs: number;
  breakdown: {
    ingressNs: number;
    routerNs: number;
    transformNs: number;
    actorNs: number;
    egressNs: number;
  };
  csCount: number;         // context switches — must be 0
  interruptCount: number;  // OS interrupts — must be 0
  verdict: 'PASS' | 'FAIL' | 'WARN';
}

export interface JitterSample {
  timestamp: number;
  latencyNs: number;
  l3Miss: boolean;
}
